/// Log Analyzer for GenPRES Solver Constraint Conflicts
///
/// Parses solver log files and produces human-readable analysis
/// of constraint incompatibility errors, tracing the conflict
/// back to source constraints.
///
/// Usage:
///   dotnet fsi LogAnalyzer.fsx <path-to-log-file>
///
/// Or via FSI MCP:
///   #load "LogAnalyzer.fsx"
///   LogAnalyzer.analyzeFile "/path/to/logfile.log"

#I __SOURCE_DIRECTORY__

open System
open System.IO
open System.Text.RegularExpressions


// ═══════════════════════════════════════════════════════════════
// Types
// ═══════════════════════════════════════════════════════════════

type OrderContext =
    {
        Patient: string
        Indication: string
        Generic: string
        Route: string
        DoseType: string
    }


type TableRow =
    {
        Name: string
        Variable: string
        Value: string
        Constraints: string
        Calculated: string
    }


type VarRef =
    {
        FullName: string
        Domain: string
    }


type Operator =
    | Product
    | Sum


type Equation =
    {
        Result: VarRef
        Operands: VarRef list
        Op: Operator
    }


type SolverError =
    {
        FailingVariable: string
        CurrentDomain: string
        AttemptedRange: string
        DetailMessage: string
        FailingEquation: string
    }


type TraceNode =
    | Leaf of varName: string * domain: string * source: string
    | Computed of varName: string * domain: string * op: Operator * children: TraceNode list * eqStr: string


type SolverPassResult =
    | Solved
    | SolvedWithErrors of errorCount: int


type SolverPass =
    {
        StartLine: int
        EndLine: int
        LoopCount: int
        EquationCount: int
        Result: SolverPassResult
    }


type PipelineStep =
    {
        Name: string
        StartLine: int
        EndLine: int
        SolverPasses: SolverPass list
        ErrorOccurred: bool
    }


type PipelineRun =
    {
        RunIndex: int
        Steps: PipelineStep list
    }


// ═══════════════════════════════════════════════════════════════
// Parsing
// ═══════════════════════════════════════════════════════════════

module Parse =

    let private eqPattern =
        Regex(
            @"^\[([^\]]+)\]_(\S+)\s+(.+?)\s+=\s+\[([^\]]+)\]_(\S+)\s+(.+?)\s+(\*|\+)\s+\[([^\]]+)\]_(\S+)\s+(.+)$"
        )


    let equationLine (line: string) =
        let m = eqPattern.Match(line.Trim())

        if m.Success then
            let mkRef (nameGroup: string) (varGroup: string) (domGroup: string) =
                {
                    FullName = $"[{nameGroup}]_{varGroup}"
                    Domain = domGroup.Trim()
                }

            let op =
                if m.Groups[7].Value = "*" then Product
                else Sum

            Some
                {
                    Result = mkRef m.Groups[1].Value m.Groups[2].Value m.Groups[3].Value
                    Operands =
                        [
                            mkRef m.Groups[4].Value m.Groups[5].Value m.Groups[6].Value
                            mkRef m.Groups[8].Value m.Groups[9].Value m.Groups[10].Value
                        ]
                    Op = op
                }
        else
            None


    let failingEquation (calcLine: string) =
        let stripped =
            let s = calcLine.Replace("start calculating: ", "")
            let costIdx = s.LastIndexOf(" (cost:")
            if costIdx >= 0 then s.Substring(0, costIdx) else s

        let normalized = Regex.Replace(stripped, @"\s+x\s+", " * ")
        equationLine normalized


    let orderContext (lines: string[]) =
        let mutable ctx = None
        let mutable i = 0

        while i < lines.Length do
            if lines[i].Contains("=== Order Context ===") then
                let mutable patient = ""
                let mutable indication = ""
                let mutable generic = ""
                let mutable route = ""
                let mutable doseType = ""
                let mutable j = i + 1

                while j < lines.Length && j < i + 15 do
                    let line = lines[j].Trim()

                    if line.StartsWith("Patient:") then
                        patient <- line.Substring(8).Trim()
                    elif line.StartsWith("Indication:") then
                        indication <- line.Substring(11).Trim()
                    elif line.StartsWith("Generic:") then
                        generic <- line.Substring(8).Trim()
                    elif line.StartsWith("Route:") then
                        route <- line.Substring(6).Trim()
                    elif line.StartsWith("DoseType:") then
                        doseType <- line.Substring(9).Trim()

                    j <- j + 1

                ctx <-
                    Some
                        {
                            Patient = patient
                            Indication = indication
                            Generic = generic
                            Route = route
                            DoseType = doseType
                        }

                i <- j
            else
                i <- i + 1

        ctx


    let errors (lines: string[]) =
        let mutable errors = []
        let mutable i = 0

        while i < lines.Length do
            let line = lines[i]

            if line.Trim().StartsWith("cannot be set with this range:") then
                let attemptedRange =
                    line.Trim().Substring("cannot be set with this range:".Length).Trim()

                let failVar =
                    if i >= 1 then lines[i - 1].Trim() else ""

                let currentDomain =
                    let idx = failVar.LastIndexOf("]_")

                    if idx >= 0 then
                        let afterUnderscore = failVar.Substring(idx + 2)
                        let spaceIdx = afterUnderscore.IndexOf(' ')

                        if spaceIdx >= 0 then
                            afterUnderscore.Substring(spaceIdx + 1).Trim()
                        else
                            ""
                    else
                        ""

                let detail =
                    [ i + 1 .. min (i + 4) (lines.Length - 1) ]
                    |> List.tryPick (fun j ->
                        if lines[j].Contains("is larger than") then
                            Some(lines[j].Trim())
                        else
                            None
                    )
                    |> Option.defaultValue ""

                let failEq =
                    [ i - 1 .. -1 .. max 0 (i - 10) ]
                    |> List.tryPick (fun j ->
                        if lines[j].Trim().StartsWith("start calculating:") then
                            Some(lines[j].Trim())
                        else
                            None
                    )
                    |> Option.defaultValue ""

                errors <-
                    {
                        FailingVariable = failVar
                        CurrentDomain = currentDomain
                        AttemptedRange = attemptedRange
                        DetailMessage = detail
                        FailingEquation = failEq
                    }
                    :: errors

                i <- i + 1
            else
                i <- i + 1

        errors |> List.rev


    let equationBlocks (lines: string[]) =
        let mutable allBlocks = []
        let mutable currentBlock = []
        let mutable inBlock = false

        for line in lines do
            if line.Contains("=== Solver Finished Solving ===") then
                inBlock <- true
                currentBlock <- []
            elif inBlock then
                match equationLine line with
                | Some eq -> currentBlock <- eq :: currentBlock
                | None ->
                    if currentBlock.Length > 0 then
                        allBlocks <- (currentBlock |> List.rev) :: allBlocks
                        currentBlock <- []
                        inBlock <- false

        if currentBlock.Length > 0 then
            allBlocks <- (currentBlock |> List.rev) :: allBlocks

        allBlocks |> List.rev


    let constraintTables (lines: string[]) =
        let mutable tables = []
        let mutable currentRows = []
        let mutable inTable = false
        let mutable prevName = ""

        for line in lines do
            if line.Contains("| 1 - NAME") then
                inTable <- true
                currentRows <- []
                prevName <- ""
            elif line.Contains("== End of Table ==") then
                if currentRows.Length > 0 then
                    tables <- (currentRows |> List.rev) :: tables

                currentRows <- []
                inTable <- false
            elif inTable && line.TrimStart().StartsWith("|") then
                if not (line.Contains("---")) then
                    let parts =
                        line.Split('|')
                        |> Array.map (fun s -> s.Trim())
                        |> Array.filter (fun s -> s.Length > 0)

                    if parts.Length >= 5 then
                        let name =
                            let n = parts[0]

                            if n.Length > 0 then
                                prevName <- n
                                n
                            else
                                prevName

                        currentRows <-
                            {
                                Name = name
                                Variable = parts[1]
                                Value = parts[2]
                                Constraints = parts[3]
                                Calculated = parts[4]
                            }
                            :: currentRows

        if currentRows.Length > 0 then
            tables <- (currentRows |> List.rev) :: tables

        tables |> List.rev


    let pipelineSteps (lines: string[]) =
        let mutable allSteps : PipelineStep list = []
        let mutable currentStepName = ""
        let mutable currentStepStart = 0
        let mutable inStep = false
        // Solver pass tracking within a step
        let mutable stepSolverPasses : SolverPass list = []
        let mutable solverStart = 0
        let mutable maxLoop = 0
        let mutable maxEqs = 0
        let mutable inSolver = false

        let solverLoopPattern =
            Regex(@"solver looped que (\d+) times with (\d+) equations")

        for i in 0 .. lines.Length - 1 do
            let line = lines[i]

            if line.Contains("=== PIPELINE START ") then
                let nameStart = line.IndexOf("START ") + 6
                let nameEnd = line.IndexOf(" ===", nameStart)
                currentStepName <-
                    if nameEnd > nameStart then line.Substring(nameStart, nameEnd - nameStart)
                    else line.Substring(nameStart).TrimEnd()
                currentStepStart <- i
                inStep <- true
                stepSolverPasses <- []

            elif line.Contains("=== Solver Start Solving") then
                solverStart <- i
                maxLoop <- 0
                maxEqs <- 0
                inSolver <- true

            elif inSolver then
                let m = solverLoopPattern.Match(line)
                if m.Success then
                    let loopN = int m.Groups[1].Value
                    let eqN = int m.Groups[2].Value
                    if loopN > maxLoop then maxLoop <- loopN
                    if eqN > maxEqs then maxEqs <- eqN

                elif line.Contains("=== Solver Finished Solving ===") then
                    inSolver <- false

            // "== Finished ..." is an Order-level result line that appears
            // AFTER "=== Solver Finished Solving ===" but before PIPELINE END
            if inStep && not inSolver && line.Contains("== Finished") then
                let result =
                    if line.Contains("Errors") then
                        let errMatch = Regex.Match(line, @"(\d+) Errors")
                        if errMatch.Success then SolvedWithErrors (int errMatch.Groups[1].Value)
                        else SolvedWithErrors 0
                    else Solved
                stepSolverPasses <-
                    {
                        StartLine = solverStart
                        EndLine = i
                        LoopCount = maxLoop
                        EquationCount = maxEqs
                        Result = result
                    } :: stepSolverPasses

            if line.Contains("=== PIPELINE END ") && inStep then
                let hasError =
                    stepSolverPasses
                    |> List.exists (fun p ->
                        match p.Result with
                        | SolvedWithErrors _ -> true
                        | Solved -> false
                    )
                allSteps <-
                    {
                        Name = currentStepName
                        StartLine = currentStepStart
                        EndLine = i
                        SolverPasses = stepSolverPasses |> List.rev
                        ErrorOccurred = hasError
                    } :: allSteps
                inStep <- false

        // Group into runs: a run starts when we see a step name that already appeared
        let steps = allSteps |> List.rev
        let mutable runs : PipelineRun list = []
        let mutable currentRun : PipelineStep list = []
        let mutable seenNames : Set<string> = Set.empty
        let mutable runIdx = 0

        for step in steps do
            if seenNames.Contains(step.Name) then
                // New run starts
                runs <-
                    { RunIndex = runIdx; Steps = currentRun |> List.rev }
                    :: runs
                runIdx <- runIdx + 1
                currentRun <- [ step ]
                seenNames <- Set.singleton step.Name
            else
                currentRun <- step :: currentRun
                seenNames <- seenNames |> Set.add step.Name

        if currentRun.Length > 0 then
            runs <-
                { RunIndex = runIdx; Steps = currentRun |> List.rev }
                :: runs

        runs |> List.rev


// ═══════════════════════════════════════════════════════════════
// Analysis
// ═══════════════════════════════════════════════════════════════

module Analyze =

    let private guidPattern =
        Regex(@"\[?[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.")


    let shortName (fullName: string) =
        fullName
            .Replace("[ord.", "")
            .Replace("[", "")
            .Replace("]", "")


    let cleanGuid (name: string) = guidPattern.Replace(name, "")


    let deduplicateErrors (errors: SolverError list) =
        errors
        |> List.distinctBy (fun e ->
            let cleanVar = cleanGuid (shortName e.FailingVariable)
            cleanVar, e.AttemptedRange
        )


    let buildVarDomains (equations: Equation list) =
        let mutable domains = Map.empty

        for eq in equations do
            domains <- domains |> Map.add eq.Result.FullName eq.Result.Domain

            for op in eq.Operands do
                domains <- domains |> Map.add op.FullName op.Domain

        domains


    let buildEqByResult (equations: Equation list) =
        equations
        |> List.groupBy (fun eq -> eq.Result.FullName)
        |> Map.ofList


    let rec traceVariable
        (eqByResult: Map<string, Equation list>)
        (varDomains: Map<string, string>)
        (visited: Set<string>)
        (varName: string)
        (maxDepth: int)
        =
        if maxDepth <= 0 || visited.Contains(varName) then
            let domain =
                varDomains |> Map.tryFind varName |> Option.defaultValue "?"

            Leaf(varName, domain, "max-depth/cycle")
        else
            let domain =
                varDomains |> Map.tryFind varName |> Option.defaultValue "?"

            let isFixed =
                domain.StartsWith("[")
                && not (domain.Contains(".."))

            match eqByResult |> Map.tryFind varName with
            | None -> Leaf(varName, domain, "no-equation")
            | Some _ when isFixed && maxDepth < 5 ->
                Leaf(varName, domain, "fixed-value")
            | Some eqs ->
                let bestEq =
                    eqs
                    |> List.sortBy (fun eq ->
                        eq.Operands
                        |> List.sumBy (fun op ->
                            if op.Domain.Contains("..") then 1
                            else 0
                        )
                    )
                    |> List.head

                let visited' = visited |> Set.add varName

                let children =
                    bestEq.Operands
                    |> List.map (fun op ->
                        traceVariable eqByResult varDomains visited' op.FullName (maxDepth - 1)
                    )

                let eqStr =
                    let opSym =
                        if bestEq.Op = Product then "*"
                        else "+"

                    sprintf
                        "%s %s = %s"
                        varName
                        domain
                        (bestEq.Operands
                         |> List.map (fun o -> sprintf "%s %s" o.FullName o.Domain)
                         |> String.concat (sprintf " %s " opSym))

                Computed(varName, domain, bestEq.Op, children, eqStr)


    let findMatchingEqBlock (eqBlocks: Equation list list) (failEq: Equation) =
        eqBlocks
        |> List.tryFind (fun block ->
            block
            |> List.exists (fun eq ->
                eq.Operands
                |> List.exists (fun o ->
                    o.FullName = failEq.Operands[0].FullName
                    && o.Domain = failEq.Operands[0].Domain
                )
            )
        )
        |> Option.defaultValue (
            if eqBlocks.Length > 0 then
                eqBlocks[0]
            else
                []
        )


// ═══════════════════════════════════════════════════════════════
// Report Generation
// ═══════════════════════════════════════════════════════════════

module Report =

    let generate
        (ctx: OrderContext option)
        (errors: SolverError list)
        (eqBlocks: Equation list list)
        (tables: TableRow list list)
        (pipelineRuns: PipelineRun list)
        =
        let sb = Text.StringBuilder()
        let pr (s: string) = sb.AppendLine(s) |> ignore
        let prf fmt = Printf.kprintf pr fmt

        pr ""
        pr "================================================================================"
        pr "  CONSTRAINT CONFLICT ANALYSIS"
        pr "================================================================================"
        pr ""

        match ctx with
        | Some c ->
            pr "  Order Context:"
            prf "    Patient:    %s" c.Patient
            prf "    Indication: %s" c.Indication
            prf "    Generic:    %s" c.Generic

            if c.Route <> "" then
                prf "    Route:      %s" c.Route

            if c.DoseType <> "" then
                prf "    DoseType:   %s" c.DoseType
        | None -> pr "  (No order context found)"

        pr ""

        // Initial constraints
        if tables.Length > 0 then
            let sigConstraints =
                tables[0]
                |> List.filter (fun row ->
                    row.Constraints <> "<0 ..>"
                    && row.Constraints <> "<0 x..>"
                    && row.Constraints <> "<0 x..1 x>"
                )

            pr "  Initial Constraints (non-default):"
            pr "  ─────────────────────────────────────────────────────────────────"

            for row in sigConstraints do
                prf "    %-42s %-12s %s" row.Name row.Variable row.Constraints

            pr ""

        // Pipeline steps
        if not pipelineRuns.IsEmpty then
            pr "  Processing Pipeline:"
            pr "  ─────────────────────────────────────────────────────────────────"

            for run in pipelineRuns do
                if pipelineRuns.Length > 1 then
                    prf "    Pass %d:" (run.RunIndex + 1)

                for stepIdx, step in run.Steps |> List.indexed do
                    let status =
                        if step.ErrorOccurred then "FAILED"
                        else "OK"

                    let statusMark =
                        if step.ErrorOccurred then "x"
                        else "v"

                    let solverInfo =
                        match step.SolverPasses with
                        | [] -> ""
                        | passes ->
                            let totalLoops =
                                passes |> List.sumBy (fun p -> p.LoopCount)
                            let maxEqs =
                                passes |> List.map (fun p -> p.EquationCount) |> List.max
                            sprintf " (%d solver loops, %d equations)" totalLoops maxEqs

                    prf "    %s %d. %-40s %s%s" statusMark (stepIdx + 1) step.Name status solverInfo

                    // Show solver pass details for failed steps
                    if step.ErrorOccurred then
                        for passIdx, pass in step.SolverPasses |> List.indexed do
                            let passResult =
                                match pass.Result with
                                | Solved -> "solved"
                                | SolvedWithErrors n -> sprintf "FAILED (%d errors)" n

                            prf "        solver pass %d: %d loops, %s" (passIdx + 1) pass.LoopCount passResult

                pr ""

        // Deduplicate errors
        let uniqueErrors = Analyze.deduplicateErrors errors

        if uniqueErrors.IsEmpty then
            pr "  No constraint conflicts detected."
        else
            prf "  Found %d unique constraint conflict(s):" uniqueErrors.Length
            pr ""

        for errIdx, error in uniqueErrors |> List.indexed do
            pr "  ────────────────────────────────────────────────────────────────"
            prf "  CONFLICT #%d" (errIdx + 1)
            pr "  ────────────────────────────────────────────────────────────────"
            pr ""

            let cleanVar =
                Analyze.cleanGuid (Analyze.shortName error.FailingVariable)

            prf "  Failing variable: %s" cleanVar
            prf "  Current domain:   %s" error.CurrentDomain
            prf "  Attempted range:  %s" error.AttemptedRange

            if error.DetailMessage <> "" then
                prf "  Detail:           %s" error.DetailMessage

            pr ""

            match Parse.failingEquation error.FailingEquation with
            | Some failEq ->
                let opSym =
                    if failEq.Op = Product then "x"
                    else "+"

                pr "  Failing equation:"

                prf
                    "    %s %s"
                    (Analyze.shortName failEq.Result.FullName)
                    failEq.Result.Domain

                prf
                    "      = %s %s  %s  %s %s"
                    (Analyze.shortName failEq.Operands[0].FullName)
                    failEq.Operands[0].Domain
                    opSym
                    (Analyze.shortName failEq.Operands[1].FullName)
                    failEq.Operands[1].Domain

                pr ""

                let targetBlock =
                    Analyze.findMatchingEqBlock eqBlocks failEq

                if targetBlock.Length > 0 then
                    let eqByR = Analyze.buildEqByResult targetBlock
                    let varD = Analyze.buildVarDomains targetBlock

                    let trace =
                        Analyze.traceVariable eqByR varD Set.empty failEq.Result.FullName 7

                    pr "  Backward trace (from failing variable to source constraints):"
                    pr "  ─────────────────────────────────────────────────────────────"

                    let rec printTraceReport indent node =
                        let pad = String.replicate indent "    "

                        match node with
                        | Leaf(name, domain, source) ->
                            prf "%s%s %s  <- %s" pad (Analyze.shortName name) domain source
                        | Computed(name, domain, op, children, _) ->
                            let opWord =
                                if op = Product then "product"
                                else "sum"

                            prf "%s%s %s" pad (Analyze.shortName name) domain
                            prf "%s  = %s of:" pad opWord

                            for child in children do
                                printTraceReport (indent + 1) child

                    printTraceReport 1 trace
                    pr ""

                    // Show equations involving operands of the failing equation
                    for op in failEq.Operands do
                        let opEqs =
                            targetBlock
                            |> List.filter (fun eq ->
                                eq.Operands
                                |> List.exists (fun o -> o.FullName = op.FullName)
                            )

                        if opEqs.Length > 0 then
                            prf
                                "  Equations involving %s %s:"
                                (Analyze.shortName op.FullName)
                                op.Domain

                            for eq in opEqs |> List.distinctBy (fun e -> e.Result.FullName) do
                                let opS =
                                    if eq.Op = Product then "*"
                                    else "+"

                                prf
                                    "    %s %s = %s"
                                    (Analyze.shortName eq.Result.FullName)
                                    eq.Result.Domain
                                    (eq.Operands
                                     |> List.map (fun o ->
                                         sprintf
                                             "%s %s"
                                             (Analyze.shortName o.FullName)
                                             o.Domain
                                     )
                                     |> String.concat (sprintf " %s " opS))

                            pr ""

            | None ->
                prf "  (Could not parse failing equation)"

            pr ""

        if not uniqueErrors.IsEmpty then
            pr "  ════════════════════════════════════════════════════════════════"
            pr "  EXPLANATION"
            pr "  ════════════════════════════════════════════════════════════════"
            pr ""
            pr "  A constraint conflict occurs when the solver computes a value"
            pr "  range for a variable that has no overlap with the variable's"
            pr "  allowed domain. Common causes:"
            pr ""
            pr "  1. GRID MISALIGNMENT: A dose rule produces values that don't"
            pr "     align with discrete component quantity steps. E.g., the"
            pr "     required component volume falls between allowed multiples."
            pr ""
            pr "  2. OVERDETERMINED SYSTEM: Multiple fixed constraints jointly"
            pr "     make the equation system infeasible."
            pr ""
            pr "  3. VOLUME INCOMPATIBILITY: None of the allowed orderable"
            pr "     volumes can accommodate the required component volumes"
            pr "     for the computed dose."
            pr ""
            pr "  To resolve: check whether the orderable volumes, component"
            pr "  quantities, dose rules, and frequency are mutually compatible"
            pr "  for the given patient weight."
            pr ""

        sb.ToString()


// ═══════════════════════════════════════════════════════════════
// Main entry point
// ═══════════════════════════════════════════════════════════════

module LogAnalyzer =

    let analyzeFile (filePath: string) =
        if not (File.Exists(filePath)) then
            printfn "Error: file not found: %s" filePath
        else
            let lines = File.ReadAllLines(filePath)
            printfn "Analyzing %s (%d lines)..." filePath lines.Length

            let ctx = Parse.orderContext lines
            let errors = Parse.errors lines
            let eqBlocks = Parse.equationBlocks lines
            let tables = Parse.constraintTables lines
            let pipelineRuns = Parse.pipelineSteps lines

            let report = Report.generate ctx errors eqBlocks tables pipelineRuns
            printfn "%s" report


    let analyzeAndSave (filePath: string) (outputPath: string) =
        if not (File.Exists(filePath)) then
            printfn "Error: file not found: %s" filePath
        else
            let lines = File.ReadAllLines(filePath)
            let ctx = Parse.orderContext lines
            let errors = Parse.errors lines
            let eqBlocks = Parse.equationBlocks lines
            let tables = Parse.constraintTables lines
            let pipelineRuns = Parse.pipelineSteps lines

            let report = Report.generate ctx errors eqBlocks tables pipelineRuns
            File.WriteAllText(outputPath, report)
            printfn "Report saved to: %s" outputPath


let file = "genpres_OrderContext_2026_03_31_09_37_47_79a2"
let path = $"{__SOURCE_DIRECTORY__}/../../../data/logs/{file}.log"
LogAnalyzer.analyzeFile path


// CLI entry point
let args = Environment.GetCommandLineArgs()

if args.Length > 1 then
    let logFile = args[args.Length - 1]

    if logFile.EndsWith(".log") then
        LogAnalyzer.analyzeFile logFile
