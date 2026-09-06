// Check Dependency Rule
//
// Architecture fitness test for ADR-0001 (docs/adr/0001-system-architecture.md):
// project references point inward, the core never reaches network, filesystem,
// environment, clock or entropy, and only the DMZ (the server-side outer ring) knows
// configuration and owns entry points.
//
// The ring map below is the authoritative ring assignment of every project in
// GenPRES.sln. The allow-lists are the inventory of today's violations; each entry
// names a reason and is a ratchet: an entry that no longer matches anything fails the
// run, so the list can only shrink. Add an entry only with a reason and an issue.
//
// Run with: dotnet fsi scripts/CheckDependencyRule.fsx
// Prototype per the script-only policy in AGENTS.md; no build is required.

#r "nuget: Expecto"

open System
open System.IO
open System.Text.RegularExpressions
open Expecto
open Expecto.Flip


let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))


// ---------------------------------------------------------------------------
// Rings
// ---------------------------------------------------------------------------

/// The onion, outermost last. `Client` runs on an untrusted machine and is
/// outside the DMZ; `Tooling` is the extraction pipeline, outside the runtime onion.
[<RequireQualifiedAccess>]
type Ring =
    | Core
    | Contract
    | Infrastructure
    | Presentation
    | Client
    | Tooling


/// Which rings a project in a given ring may reference. Core references only core;
/// the DMZ (Infrastructure, Presentation) may reach inward; the Client sees only the
/// contract.
let mayReference (from: Ring) (target: Ring) =
    match from, target with
    | Ring.Core, Ring.Core -> true
    | Ring.Contract, Ring.Contract -> true
    | Ring.Infrastructure, (Ring.Core | Ring.Infrastructure) -> true
    | Ring.Presentation, (Ring.Core | Ring.Contract | Ring.Infrastructure | Ring.Presentation) -> true
    | Ring.Client, Ring.Contract -> true
    | Ring.Tooling, (Ring.Core | Ring.Infrastructure | Ring.Tooling) -> true
    | _ -> false


/// Target ring of every project in `src/`. A project not listed here fails the run.
let rings =
    Map.ofList
        [
            "Informedica.Utils.Lib", Ring.Core
            "Informedica.Logging.Lib", Ring.Core
            "Informedica.GenUNITS.Lib", Ring.Core
            "Informedica.GenCORE.Lib", Ring.Core
            "Informedica.GenSOLVER.Lib", Ring.Core
            "Informedica.GenFORM.Lib", Ring.Core
            "Informedica.GenORDER.Lib", Ring.Core
            "Informedica.GenINTERACT.Lib", Ring.Core
            "Informedica.GenPRES.Shared", Ring.Contract
            "Informedica.Agents.Lib", Ring.Infrastructure
            "Informedica.ZIndex.Lib", Ring.Infrastructure
            "Informedica.ZForm.Lib", Ring.Infrastructure
            "Informedica.NKF.Lib", Ring.Infrastructure
            "Informedica.FTK.Lib", Ring.Infrastructure
            "Informedica.GenPRES.Server", Ring.Presentation
            "Informedica.MCP.Lib", Ring.Presentation
            "Informedica.MCP.Server", Ring.Presentation
            "Informedica.GenPRES.Client", Ring.Client
            "Informedica.NLP.Lib", Ring.Tooling
        ]


let isDmz ring =
    match ring with
    | Ring.Infrastructure
    | Ring.Presentation -> true
    | _ -> false


// ---------------------------------------------------------------------------
// Allow-lists: the inventory of today's violations. Every entry must still match.
// ---------------------------------------------------------------------------

/// A project reference that points outward today. Removed when the reference is
/// inverted (issue #378).
let allowedReferences =
    [
        "Informedica.Logging.Lib",
        "Informedica.Agents.Lib",
        "Logging.Lib bundles the Logger port with the agent runtime; split pending (#378, #416)"
        "Informedica.GenFORM.Lib",
        "Informedica.ZForm.Lib",
        "GenFORM consumes G-Standaard types from the ZForm/ZIndex adapters; contract types pending (#378)"
    ]


/// A core source file that reaches outside. `Token = None` allows the whole file
/// (an IO module awaiting eviction); `Token = Some t` allows one banned token in it.
type Allowance =
    {
        File: string
        Token: string option
        Reason: string
    }


let allowFile file reason =
    {
        File = file
        Token = None
        Reason = reason
    }


let allowToken file token reason =
    {
        File = file
        Token = Some token
        Reason = reason
    }


/// Tokens a core project may not contain outside comments. Matched on a word
/// boundary before the token, so `AppEnv.` does not match `Env.`.
let bannedTokens =
    [
        "System.IO"
        "System.Net"
        "HttpClient"
        "Environment.GetEnvironmentVariable"
        "Environment.SetEnvironmentVariable"
        "Environment.GetEnvironmentVariables"
        "Environment.CurrentDirectory"
        "Environment.ProcessorCount"
        "Environment.MachineName"
        "Environment.UserName"
        "AppDomain.CurrentDomain"
        "AppContext.BaseDirectory"
        "DateTime.Now"
        "DateTime.UtcNow"
        "DateTimeOffset.Now"
        "DateTimeOffset.UtcNow"
        "Guid.NewGuid"
        "Console."
        "ConsoleWriter"
        "writeErrorMessage"
        "writeWarningMessage"
        "writeInfoMessage"
        "writeDebugMessage"
        "printfn"
        "eprintfn"
        "File."
        "Directory."
        "Web."
        "Env."
        "AppPath"
        "StopWatch."
        "Stopwatch"
        "Async.RunSynchronously"
        "Memoization.memoize"
        "MailboxProcessor"
        "FileWriterAgent"
        "AgentLogging"
    ]


/// Phase numbers refer to docs/implementation-plans/378-dependency-rule.md.
/// "Permanent" entries are accepted exceptions recorded in ADR-0001.
let allowances =
    let utilsSplit = "IO module in Utils.Lib; leaves the core with the Utils split (Phase 2)"
    let loggingSplit = "Logger port and agent runtime share one file; split pending (Phase 1)"
    let viaLogger = "console write below the injected Logger; route through Logger (Phase 1)"
    let evict = "Google-Sheets/NKF loader in GenFORM; moves to the adapter project (Phase 2)"
    let factory = "logger factory in a core library; moves to the composition root (Phase 1)"
    let clock = "ambient clock in the core; becomes a `now` parameter (Phase 4)"
    let chunking = "Permanent: Environment.ProcessorCount only sizes parallel chunks, never a result"

    [
        // Utils.Lib: whole IO modules awaiting the pure/IO split
        allowFile "src/Informedica.Utils.Lib/File.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/Env.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/App.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/AppPath.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/Console.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/StopWatch.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/Web.fs" utilsSplit
        allowToken "src/Informedica.Utils.Lib/Path.fs" "System.IO" "Permanent: System.IO.Path string helpers only, no filesystem access"
        allowToken "src/Informedica.Utils.Lib/Memoization.fs" "Stopwatch" "Permanent: timing in an example function, no IO"
        allowToken "src/Informedica.Utils.Lib/Json.fs" "printfn" viaLogger
        allowToken "src/Informedica.Utils.Lib/BCL/Int32.fs" "printfn" viaLogger
        allowToken "src/Informedica.Utils.Lib/BCL/BigInteger.fs" "printfn" viaLogger
        allowToken "src/Informedica.Utils.Lib/BCL/DateTime.fs" "DateTime.Now" clock
        // Logging.Lib
        allowFile "src/Informedica.Logging.Lib/Logging.fs" loggingSplit
        // GenUNITS.Lib
        allowToken "src/Informedica.GenUNITS.Lib/UnitsParse.fs" "printfn" viaLogger
        allowToken "src/Informedica.GenUNITS.Lib/ValueUnit.fs" "printfn" viaLogger
        // GenCORE.Lib
        allowToken "src/Informedica.GenCORE.Lib/Patient.fs" "DateTime.Now" clock
        // GenSOLVER.Lib
        allowToken "src/Informedica.GenSOLVER.Lib/Utils.fs" "Environment.ProcessorCount" chunking
        allowToken "src/Informedica.GenSOLVER.Lib/Variable.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Variable.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Variable.fs" "writeDebugMessage" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Variable.fs" "printfn" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Equation.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Equation.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Equation.fs" "printfn" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Solver.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/Solver.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenSOLVER.Lib/SolverLogging.fs" "AgentLogging" factory
        // GenFORM.Lib
        allowToken "src/Informedica.GenFORM.Lib/Utils.fs" "Environment.ProcessorCount" chunking
        allowToken "src/Informedica.GenFORM.Lib/Utils.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Utils.fs" "StopWatch." evict
        allowToken "src/Informedica.GenFORM.Lib/Mapping.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "Async.RunSynchronously" evict
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "StopWatch." evict
        allowToken "src/Informedica.GenFORM.Lib/DoseRuleLoader.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/DoseRuleLoader.fs" "Async.RunSynchronously" evict
        allowFile "src/Informedica.GenFORM.Lib/SourceLoader.fs" evict
        allowToken "src/Informedica.GenFORM.Lib/SolutionRule.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/RenalRule.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenFORM.Lib/RenalRule.fs" "writeWarningMessage" viaLogger
        allowToken "src/Informedica.GenFORM.Lib/RenalRule.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/FormLogging.fs" "AgentLogging" "unreferenced top-level agent logger; delete (Phase 0)"
        allowToken "src/Informedica.GenFORM.Lib/Resources.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenFORM.Lib/Resources.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenFORM.Lib/Resources.fs" "DateTime.UtcNow" "CachedResourceProvider TTL clock; provider moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Export.fs" "File." "cwd-relative export file write; moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Export.fs" "Environment.CurrentDirectory" "cwd-relative export file write; moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Api.fs" "Async.RunSynchronously" "parallel rule filtering blocks on Async; keep pure or move to the edge (Phase 2)"
        // GenORDER.Lib
        allowToken "src/Informedica.GenORDER.Lib/Utils.fs" "Env." "dead getDataFromGenPres reads env; delete (Phase 0)"
        allowToken "src/Informedica.GenORDER.Lib/Utils.fs" "printfn" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Exceptions.fs" "printfn" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderVariable.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderVariable.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/EquationMapping.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/EquationMapping.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/EquationMapping.fs" "Memoization.memoize" "Permanent: memoizes a hard-coded, pure equation list"
        allowToken "src/Informedica.GenORDER.Lib/Order.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Order.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Order.fs" "writeDebugMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Order.fs" "DateTime.Now" clock
        allowToken "src/Informedica.GenORDER.Lib/OrderProcessor.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderProcessor.fs" "writeWarningMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Medication.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Medication.fs" "writeWarningMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Medication.fs" "Guid.NewGuid" "ambient entropy in a constructor; becomes a `newId` parameter (Phase 4)"
        allowToken "src/Informedica.GenORDER.Lib/Nutrition.fs" "printfn" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderLogging.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderLogging.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderLogging.fs" "writeInfoMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/OrderLogging.fs" "AgentLogging" factory
        allowToken "src/Informedica.GenORDER.Lib/Api.fs" "ConsoleWriter" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Api.fs" "writeErrorMessage" viaLogger
        allowToken "src/Informedica.GenORDER.Lib/Api.fs" "writeWarningMessage" viaLogger
        // GenINTERACT.Lib
        allowToken "src/Informedica.GenINTERACT.Lib/Data.fs" "System.IO" "cwd-relative cache read; loader moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenINTERACT.Lib/Data.fs" "File." "cwd-relative cache read; loader moves to the adapter project (Phase 2)"
    ]


/// The prefixes under which settings are read, as they appear in source (`"GENPRES_URL_ID"`).
/// One entry today. If GenPRES is split into separately deployed modules, either every module
/// keeps this prefix or each executable gets its own and this becomes a map from executable to
/// prefix; that is decided with the modular design, not here. Until then the list is the single
/// place to change.
let settingPrefixes = [ "GENPRES_" ]


/// True when a code line names a setting under any known prefix.
let namesSetting (line: string) =
    settingPrefixes |> List.exists (fun p -> line.Contains("\"" + p))


/// Core files that may name a `GENPRES_*` setting today.
let allowedConfigMentions =
    [
        "src/Informedica.Utils.Lib/AppPath.fs", "GENPRES_ROOT root resolution; leaves the core with the Utils split (Phase 2)"
        "src/Informedica.Utils.Lib/Console.fs", "GENPRES_DEBUG read inside the console writer; leaves the core with the Utils split (Phase 2)"
        "src/Informedica.GenORDER.Lib/Utils.fs", "GENPRES_URL_ID constant used only by dead getDataFromGenPres; delete (Phase 0)"
    ]


// ---------------------------------------------------------------------------
// Solution and project model
// ---------------------------------------------------------------------------

type Project =
    {
        Name: string
        Path: string
        Ring: Ring option
        References: string list
        SourceFiles: string list
    }


let normalise (p: string) = p.Replace('\\', '/')


/// Projects under `src/` declared in GenPRES.sln, read from the solution file.
let srcProjects () =
    let sln = Path.Combine(repoRoot, "GenPRES.sln") |> File.ReadAllText

    Regex.Matches(sln, "\"([^\"]+\\.fsproj)\"")
    |> Seq.map (fun m -> m.Groups[1].Value |> normalise)
    |> Seq.filter (fun p -> p.StartsWith "src/")
    |> Seq.distinct
    |> Seq.map (fun rel ->
        let full = Path.Combine(repoRoot, rel)
        let dir = Path.GetDirectoryName full
        let name = Path.GetFileNameWithoutExtension full
        let xml = File.ReadAllText full

        let references =
            Regex.Matches(xml, "ProjectReference Include=\"([^\"]+)\"")
            |> Seq.map (fun m -> m.Groups[1].Value |> normalise |> Path.GetFileNameWithoutExtension)
            |> List.ofSeq

        let sources =
            Regex.Matches(xml, "Compile Include=\"([^\"]+)\"")
            |> Seq.map (fun m -> Path.Combine(dir, m.Groups[1].Value |> normalise) |> Path.GetFullPath)
            |> List.ofSeq

        {
            Name = name
            Path = rel
            Ring = rings |> Map.tryFind name
            References = references
            SourceFiles = sources
        }
    )
    |> List.ofSeq


let relative (full: string) =
    Path.GetRelativePath(repoRoot, full) |> normalise


/// A line that is only a comment does not reach outside.
let isComment (line: string) = line.TrimStart().StartsWith "//"


/// `token` occurs in `line` and is not the tail of a longer identifier.
let containsToken (token: string) (line: string) =
    let rec search from =
        match line.IndexOf(token, from, StringComparison.Ordinal) with
        | -1 -> false
        | i when i = 0 || not (Char.IsLetterOrDigit line[i - 1]) -> true
        | i -> search (i + 1)

    search 0


let codeLines (file: string) =
    File.ReadAllLines file
    |> Array.mapi (fun i l -> i + 1, l)
    |> Array.filter (fun (_, l) -> not (isComment l))


let failWithAll what (violations: string list) =
    if not violations.IsEmpty then
        violations
        |> String.concat Environment.NewLine
        |> sprintf "%s:%s%s" what Environment.NewLine
        |> failtest


// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

let ringMapTests =
    testList
        "ring map"
        [
            test "every src project in GenPRES.sln has a ring" {
                srcProjects ()
                |> List.filter (fun p -> p.Ring.IsNone)
                |> List.map _.Name
                |> failWithAll "projects without a ring"
            }

            test "every ring map entry names a project in GenPRES.sln" {
                let names = srcProjects () |> List.map _.Name |> Set.ofList

                rings
                |> Map.keys
                |> Seq.filter (fun n -> not (names.Contains n))
                |> List.ofSeq
                |> failWithAll "ring map entries without a project"
            }
        ]


let referenceTests =
    testList
        "T1 project references point inward"
        [
            test "no project references an outer ring, except the allow-list" {
                let projects = srcProjects ()
                let ringOf name = rings |> Map.tryFind name

                let allowed =
                    allowedReferences |> List.map (fun (f, t, _) -> f, t) |> Set.ofList

                projects
                |> List.collect (fun p ->
                    p.References
                    |> List.choose (fun dep ->
                        match p.Ring, ringOf dep with
                        | Some from, Some target when not (mayReference from target) ->
                            if allowed.Contains(p.Name, dep) then
                                None
                            else
                                Some $"%s{p.Name} (%A{from}) -> %s{dep} (%A{target})"
                        | _ -> None
                    )
                )
                |> failWithAll "outward references"
            }

            test "every allowed reference still exists (ratchet)" {
                let projects = srcProjects ()

                allowedReferences
                |> List.filter (fun (f, t, _) ->
                    projects
                    |> List.exists (fun p -> p.Name = f && p.References |> List.contains t)
                    |> not
                )
                |> List.map (fun (f, t, _) -> $"%s{f} -> %s{t}")
                |> failWithAll "allow-list entries for references that no longer exist; remove them"
            }
        ]


let coreTests =
    let coreFiles () =
        srcProjects ()
        |> List.filter (fun p -> p.Ring = Some Ring.Core)
        |> List.collect _.SourceFiles

    let isAllowed (file: string) (token: string) =
        allowances
        |> List.exists (fun a -> a.File = file && (a.Token.IsNone || a.Token = Some token))

    testList
        "T2 the core does not reach outside"
        [
            test "no core source line uses a banned token, except the allow-list" {
                coreFiles ()
                |> List.collect (fun file ->
                    let rel = relative file

                    codeLines file
                    |> Array.toList
                    |> List.collect (fun (n, line) ->
                        bannedTokens
                        |> List.filter (fun t -> containsToken t line && not (isAllowed rel t))
                        |> List.map (fun t -> $"%s{rel}:%i{n} %s{t}")
                    )
                )
                |> failWithAll "core files reaching outside"
            }

            test "every allowance still matches something (ratchet)" {
                let files = coreFiles () |> List.map (fun f -> relative f, f) |> Map.ofList

                allowances
                |> List.filter (fun a ->
                    match files |> Map.tryFind a.File with
                    | None -> true
                    | Some full ->
                        let lines = codeLines full |> Array.map snd

                        let tokens =
                            match a.Token with
                            | Some t -> [ t ]
                            | None -> bannedTokens

                        lines
                        |> Array.exists (fun l -> tokens |> List.exists (fun t -> containsToken t l))
                        |> not
                )
                |> List.map (fun a -> $"%s{a.File} %A{a.Token}")
                |> failWithAll "allowances that no longer match; remove them"
            }
        ]


let dmzTests =
    testList
        "T3/T4 only the DMZ knows configuration and owns entry points"
        [
            test "GENPRES_ settings are named only in DMZ projects, except the allow-list" {
                let allowed = allowedConfigMentions |> List.map fst |> Set.ofList

                srcProjects ()
                |> List.filter (fun p ->
                    match p.Ring with
                    | Some r -> not (isDmz r) && r <> Ring.Client
                    | None -> false
                )
                |> List.collect _.SourceFiles
                |> List.collect (fun file ->
                    let rel = relative file

                    if allowed.Contains rel then
                        []
                    else
                        codeLines file
                        |> Array.filter (fun (_, l) -> namesSetting l)
                        |> Array.map (fun (n, _) -> $"%s{rel}:%i{n}")
                        |> Array.toList
                )
                |> failWithAll "configuration named outside the DMZ"
            }

            test "every config allowance still matches something (ratchet)" {
                allowedConfigMentions
                |> List.filter (fun (rel, _) ->
                    let full = Path.Combine(repoRoot, rel)

                    not (File.Exists full)
                    || codeLines full |> Array.exists (fun (_, l) -> namesSetting l) |> not
                )
                |> List.map fst
                |> failWithAll "config allowances that no longer match; remove them"
            }

            test "only Presentation projects declare an entry point" {
                srcProjects ()
                |> List.filter (fun p -> p.Ring <> Some Ring.Presentation)
                |> List.collect _.SourceFiles
                |> List.filter (fun file -> File.ReadAllText file |> _.Contains("[<EntryPoint>]"))
                |> List.map relative
                |> failWithAll "entry points outside Presentation"
            }
        ]


runTestsWithCLIArgs
    []
    [| "--summary" |]
    (testList
        "dependency rule"
        [
            ringMapTests
            referenceTests
            coreTests
            dmzTests
        ])
|> exit
