#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// LoopDetect.fsx — State-fingerprint loop detection for GenSolver
// =============================================================
//
// Context (issue #248):
//   A research project (K. Delic thesis) identified that GenSOLVER can
//   encounter situations where min/max bounds make only partial steps
//   toward their final values.  The current safeguard is a hard counter:
//
//       if n > equationCount * MAX_LOOP_COUNT then raise SolverTooManyLoops
//
//   This prevents truly infinite loops but may fire only after hundreds of
//   wasted iterations, and cannot distinguish a *cycle* (same state seen
//   twice) from genuinely slow but converging progress.
//
// What this script adds
// ----------------------
//
//   1. **StateFingerprint** — a lightweight hash of all variable bounds.
//      Two states with identical bounds hash to the same fingerprint.
//
//   2. **CycleDetector** — records fingerprints and detects the moment
//      a previously-visited state is revisited ("exact cycle").
//
//   3. **ConvergenceTracker** — monitors per-variable bound width across
//      iterations.  If width reduction stalls for `stallThreshold` steps
//      in a row it reports a potential slow-convergence / stuck case.
//
//   4. **DetectingLoop.solve** — a variant of the standard solver inner
//      loop that integrates both checks, providing richer diagnostics
//      on termination than the existing hard-count guard.
//
//   5. **Expecto tests** — verify each component in isolation plus one
//      end-to-end scenario.
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi LoopDetect.fsx
// =============================================================

open System
open System.Collections.Generic
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib


// ------------------------------------------------------------------
// Aliases for clarity
// ------------------------------------------------------------------

module VR = Variable.ValueRange


// ------------------------------------------------------------------
// Section 1 — StateFingerprint
// ------------------------------------------------------------------

/// A compact hash of the combined state of all equations' variable bounds.
/// Used to detect when the solver has revisited a state it has seen before.
type StateFingerprint = private StateFingerprint of int

module StateFingerprint =

    /// Produce a fingerprint from a list of equations.
    ///
    /// The fingerprint is based on every variable's name + value range string,
    /// which is stable under equation reordering but sensitive to any bound change.
    let ofEquations (eqs: Types.Equation list) : StateFingerprint =
        eqs
        |> List.collect Equation.toVars
        |> List.sortBy (Variable.getName >> Variable.Name.toString)
        |> List.map (fun v ->
            let name  = v |> Variable.getName  |> Variable.Name.toString
            let range = v |> Variable.toString false
            $"%s{name}|%s{range}"
        )
        |> String.concat ";"
        // FNV-1a 32-bit — deterministic, fast, no System.Security.Cryptography needed
        |> (fun s -> s |> Seq.fold (fun h c -> (h ^^^ int c) * 16777619) (int 0x811C9DC5u))
        |> StateFingerprint

    let value (StateFingerprint h) = h


// ------------------------------------------------------------------
// Section 2 — CycleDetector
// ------------------------------------------------------------------

[<RequireQualifiedAccess>]
type CycleCheckResult =
    | Fresh
    | CycleDetected of firstSeenAtStep: int * fingerprint: StateFingerprint


module CycleDetector =

    type T = { Seen: Dictionary<int, int> }

    let create () = { Seen = Dictionary<int, int>() }

    /// Record the fingerprint for step **n**.
    /// Returns `CycleDetected` if the same fingerprint was already recorded.
    let check (n: int) (fp: StateFingerprint) (detector: T) : CycleCheckResult =
        let h = fp |> StateFingerprint.value
        match detector.Seen.TryGetValue h with
        | true, firstSeen -> CycleCheckResult.CycleDetected(firstSeen, fp)
        | false, _        ->
            detector.Seen[h] <- n
            CycleCheckResult.Fresh


// ------------------------------------------------------------------
// Section 3 — ConvergenceTracker
// ------------------------------------------------------------------

module ConvergenceTracker =

    type BoundWidth =
        {
            VariableName : string
            // None means unbounded (open range or unrestricted)
            Width        : BigRational option
        }

    let private widthOf (v: Types.Variable) : BoundWidth =
        let name = v |> Variable.getName |> Variable.Name.toString
        let vr   = v |> Variable.getValueRange

        let width =
            match vr |> VR.getMin, vr |> VR.getMax with
            | Some mn, Some mx ->
                let lo = mn |> VR.Minimum.toValueUnit |> ValueUnit.toBase |> ValueUnit.getValue |> Array.tryHead
                let hi = mx |> VR.Maximum.toValueUnit |> ValueUnit.toBase |> ValueUnit.getValue |> Array.tryHead
                match lo, hi with
                | Some l, Some h -> Some (h - l)
                | _              -> None
            | _ -> None

        { VariableName = name; Width = width }

    type T =
        {
            mutable State  : Map<string, BigRational option * int>
            StallThreshold : int
        }

    let create stallThreshold = { State = Map.empty; StallThreshold = stallThreshold }

    type StallInfo = { StalledVariables: string list; Step: int }

    [<RequireQualifiedAccess>]
    type TrackResult =
        | Progressing
        | PotentialStall of StallInfo

    /// Update tracker with the current equation state.
    /// Returns `PotentialStall` if any variable has not improved for
    /// `StallThreshold` consecutive steps.
    let update (n: int) (eqs: Types.Equation list) (tracker: T) : TrackResult =
        let widths = eqs |> List.collect Equation.toVars |> List.map widthOf

        let stalled =
            widths
            |> List.choose (fun bw ->
                let runCount =
                    match tracker.State |> Map.tryFind bw.VariableName with
                    | None -> 0
                    | Some (prevW, run) -> if prevW = bw.Width then run + 1 else 0

                tracker.State <- tracker.State |> Map.add bw.VariableName (bw.Width, runCount)

                if runCount >= tracker.StallThreshold then Some bw.VariableName
                else None
            )

        if stalled.IsEmpty then TrackResult.Progressing
        else TrackResult.PotentialStall { StalledVariables = stalled; Step = n }


// ------------------------------------------------------------------
// Section 4 — Loop-detecting solver
// ------------------------------------------------------------------

module DetectingLoop =

    [<RequireQualifiedAccess>]
    type TerminationReason =
        | HardLimit   of step: int
        | CycleDetected of firstSeenAtStep: int * step: int
        | PotentialStall of step: int * variables: string list

    [<RequireQualifiedAccess>]
    type SolverOutcome =
        | Solved   of Types.Equation list
        | HardStop of reason: TerminationReason * equations: Types.Equation list

    /// Solve **eqs** with cycle-detection and convergence monitoring.
    ///
    /// Parameters:
    ///   onlyMinIncrMax  — mirror of the standard solver flag
    ///   stallThreshold  — steps without improvement before a stall warning
    ///   log             — solver logger (use Logger.noOp if no logging needed)
    let solve (onlyMinIncrMax: bool) (stallThreshold: int) (log: Informedica.Logging.Lib.Logger) (eqs: Types.Equation list) : SolverOutcome =

        let detector = CycleDetector.create ()
        let tracker  = ConvergenceTracker.create stallThreshold

        let rec loop (n: int) (que: Types.Equation list) (acc: Types.Equation list) =

            // If the queue is empty, all equations have been processed — done
            match que with
            | [] -> SolverOutcome.Solved acc
            | _ ->

            // Hard-count guard kept as a final safety net
            if n > (que @ acc |> List.length) * Constants.MAX_LOOP_COUNT then
                SolverOutcome.HardStop(TerminationReason.HardLimit n, que @ acc)

            else

            // State fingerprint cycle detection
            let fp = que @ acc |> StateFingerprint.ofEquations
            match detector |> CycleDetector.check n fp with
            | CycleCheckResult.CycleDetected (firstStep, _) ->
                SolverOutcome.HardStop(TerminationReason.CycleDetected(firstStep, n), que @ acc)

            | CycleCheckResult.Fresh ->

            // Convergence / stall detection
            match tracker |> ConvergenceTracker.update n (que @ acc) with
            | ConvergenceTracker.TrackResult.PotentialStall info ->
                SolverOutcome.HardStop(TerminationReason.PotentialStall(info.Step, info.StalledVariables), que @ acc)

            | ConvergenceTracker.TrackResult.Progressing ->
                // Sort the queue by equation complexity (cheapest first)
                let sortedQue = que |> Solver.sortQue onlyMinIncrMax |> List.map snd

                let nextEq = sortedQue |> List.head
                let rest   = sortedQue |> List.tail

                if nextEq |> Equation.isSolvable |> not then
                    loop (n + 1) rest (nextEq :: acc)
                else
                    let solvedEq, sr = nextEq |> Equation.solve onlyMinIncrMax log

                    match sr with
                    | Unchanged ->
                        loop (n + 1) rest (solvedEq :: acc)

                    | Changed cs ->
                        let vars        = cs |> List.map fst
                        let rpl, rst    = acc  |> Solver.replace vars
                        let queTail, _  = rest |> Solver.replace vars
                        loop (n + 1) (queTail @ rpl) (solvedEq :: rst)

                    | Errored _ ->
                        // On solver error stop with what we have
                        SolverOutcome.HardStop(TerminationReason.HardLimit n, que @ acc)

        loop 0 eqs []


// ------------------------------------------------------------------
// Section 5 — Test helpers
// ------------------------------------------------------------------

module Helpers =

    let noLog = Logger.noOp

    /// Build equation list from expression strings and apply named value arrays.
    ///
    /// Usage:
    ///   makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|])]
    let makeEqs (exprs: string list) (bindings: (string * BigRational[]) list) =
        let eqs = Api.init exprs

        bindings
        |> List.fold (fun acc (nm, vals) ->
            let n = nm |> Variable.Name.createExc
            let prop = vals |> ValueUnit.create Units.Count.times |> Variable.ValueRange.ValueSet.create |> ValsProp
            match Api.setVariableValues n prop acc with
            | Some var -> acc |> List.map (Equation.replace var)
            | None     -> acc
        ) eqs


// ------------------------------------------------------------------
// Section 6 — Expecto Tests
// ------------------------------------------------------------------

#r "nuget: Expecto, 9.0.4"

open Expecto
open Expecto.Flip


let fingerprintTests =
    testList "StateFingerprint" [

        test "same equations produce same fingerprint" {
            let eqs = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|]); ("c", [|6N|])]
            StateFingerprint.ofEquations eqs
            |> Expect.equal "same state → same fingerprint" (StateFingerprint.ofEquations eqs)
        }

        test "different variable values produce different fingerprint" {
            let eqs1 = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|])]
            let eqs2 = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|4N|])]
            let fp1  = StateFingerprint.ofEquations eqs1
            let fp2  = StateFingerprint.ofEquations eqs2
            (fp1 = fp2)
            |> Expect.isFalse "different values → different fingerprint"
        }

    ]


let cycleDetectorTests =
    testList "CycleDetector" [

        test "unique fingerprints are all Fresh" {
            let det  = CycleDetector.create ()
            let eqs1 = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|])]
            let eqs2 = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|4N|])]

            det |> CycleDetector.check 0 (StateFingerprint.ofEquations eqs1)
            |> Expect.equal "first fp" CycleCheckResult.Fresh

            det |> CycleDetector.check 1 (StateFingerprint.ofEquations eqs2)
            |> Expect.equal "second fp" CycleCheckResult.Fresh
        }

        test "revisiting the same state is detected" {
            let det = CycleDetector.create ()
            let fp  = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|])] |> StateFingerprint.ofEquations

            det |> CycleDetector.check 0 fp |> ignore

            match det |> CycleDetector.check 5 fp with
            | CycleCheckResult.CycleDetected (firstSeen, _) ->
                firstSeen |> Expect.equal "first seen at step 0" 0
            | CycleCheckResult.Fresh ->
                failtest "expected CycleDetected"
        }

    ]


let convergenceTrackerTests =
    testList "ConvergenceTracker" [

        test "improving bounds report Progressing" {
            let tracker = ConvergenceTracker.create 3
            let eqs1 = Helpers.makeEqs ["c = a * b"] [("a", [|1N; 2N|]); ("b", [|3N|])]
            let eqs2 = Helpers.makeEqs ["c = a * b"] [("a", [|1N|]);      ("b", [|3N|])]

            tracker |> ConvergenceTracker.update 0 eqs1 |> ignore
            tracker |> ConvergenceTracker.update 1 eqs2
            |> Expect.equal "one improvement → Progressing" ConvergenceTracker.TrackResult.Progressing
        }

        test "static bounds for stallThreshold steps trigger PotentialStall" {
            let threshold = 2
            let tracker   = ConvergenceTracker.create threshold
            let eqs       = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|]); ("c", [|6N|])]

            for step in 0 .. threshold do
                tracker |> ConvergenceTracker.update step eqs |> ignore

            match tracker |> ConvergenceTracker.update (threshold + 1) eqs with
            | ConvergenceTracker.TrackResult.PotentialStall info ->
                info.StalledVariables |> List.isEmpty
                |> Expect.isFalse "at least one stalled variable"
            | ConvergenceTracker.TrackResult.Progressing ->
                failtest "expected PotentialStall"
        }

    ]


let detectingLoopTests =
    testList "DetectingLoop.solve" [

        test "already-solved equation set completes as Solved" {
            let eqs = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|]); ("c", [|6N|])]
            match DetectingLoop.solve false 10 Helpers.noLog eqs with
            | DetectingLoop.SolverOutcome.Solved _ -> ()
            | DetectingLoop.SolverOutcome.HardStop (reason, _) ->
                failtest $"expected Solved, got HardStop: {reason}"
        }

        test "underdetermined equation produces Solved with propagated values" {
            // Give a and b, solver should infer c = 6
            let eqs = Helpers.makeEqs ["c = a * b"] [("a", [|2N|]); ("b", [|3N|])]
            match DetectingLoop.solve false 10 Helpers.noLog eqs with
            | DetectingLoop.SolverOutcome.Solved solved ->
                solved |> List.isEmpty
                |> Expect.isFalse "solved equations should not be empty"
            | DetectingLoop.SolverOutcome.HardStop (reason, _) ->
                failtest $"expected Solved, got HardStop: {reason}"
        }

    ]


let allTests =
    testList "LoopDetect" [
        fingerprintTests
        cycleDetectorTests
        convergenceTrackerTests
        detectingLoopTests
    ]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs [] argv allTests
