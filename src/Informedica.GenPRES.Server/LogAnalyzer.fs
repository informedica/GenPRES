module LogAnalyzer

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions


// ═══════════════════════════════════════════════════════════════
// Readable formatting of BigRational fractions and ValueUnit
// ═══════════════════════════════════════════════════════════════

module Format =

    /// Minimum significant digits for fixPrecision.
    /// Use 4 to show enough precision to distinguish close values.
    let private defaultPrecision = 4


    /// fixPrecision n f: round float f to show at least n
    /// non-zero significant digits.
    let private fixPrecision n (f: float) =
        if n < 0 || Double.IsNaN f || Double.IsInfinity f then
            f
        else
            let absF = abs f

            if absF = 0.0 then
                f
            else
                let s = absF.ToString("G", Globalization.CultureInfo.InvariantCulture)

                let precision =
                    if s.Contains "E" then
                        let eIndex = s.IndexOf("E") + 2
                        let h = int s[eIndex..]
                        h + n - 1
                    else
                        let parts = s.Split('.')
                        let leftPart = parts[0]
                        let p = n - (if leftPart = "0" then 0 else leftPart.Length)
                        let p = if p < 0 then 0 else p

                        if int leftPart > 0 then
                            p
                        else
                            let rightPart = if parts.Length > 1 then parts[1] else ""
                            let zeroCount = rightPart |> Seq.takeWhile (fun c -> c = '0') |> Seq.length
                            zeroCount + p

                Math.Round(f, precision)


    /// Convert a fraction string like "629/10" to a float option.
    let private tryParseFraction (s: string) =
        let parts = s.Trim().Split('/')

        match parts with
        | [| num; den |] ->
            match Double.TryParse(num.TrimEnd('N')), Double.TryParse(den.TrimEnd('N')) with
            | (true, n), (true, d) when d <> 0.0 -> Some(n / d)
            | _ -> None
        | [| num |] ->
            match Double.TryParse(num.TrimEnd('N')) with
            | true, n -> Some n
            | _ -> None
        | _ -> None


    /// Format a float with fixPrecision and strip trailing zeros.
    let private formatFloat prec (f: float) =
        let rounded = fixPrecision prec f
        // Use enough decimal places then strip trailing zeros
        let s = rounded.ToString("G10", Globalization.CultureInfo.InvariantCulture)
        s


    /// Pattern: standalone fraction like "629/10" (not inside ValueUnit).
    /// Matches patterns: digits/digits optionally followed by N.
    let private fractionPattern = Regex(@"(?<!\w)(-?\d+)/(\d+)(?!N?\|)")


    /// Replace bare fractions in text with readable decimals.
    let private replaceFractions prec (text: string) =
        fractionPattern.Replace(
            text,
            fun m ->
                let num = float m.Groups[1].Value
                let den = float m.Groups[2].Value
                if den = 0.0 then m.Value else formatFloat prec (num / den)
        )


    /// Pattern: BigRational-style fraction with N suffix like "629/10N" or "303/5N" or just "10N"
    let private brFractionPattern = Regex(@"(-?\d+)/(\d+)N\b|(?<!\d/)(\d+)N\b")


    /// Replace N-suffixed fractions.
    let private replaceBrFractions prec (text: string) =
        brFractionPattern.Replace(
            text,
            fun m ->
                if m.Groups[1].Success then
                    let num = float m.Groups[1].Value
                    let den = float m.Groups[2].Value
                    if den = 0.0 then m.Value else formatFloat prec (num / den)
                else
                    // plain "10N" -> "10"
                    m.Groups[3].Value
        )


    /// Known unit type mappings for readable output.
    let private unitMap =
        [
            "Mass (KiloGram", "kg"
            "Mass (Gram", "g"
            "Mass (MilliGram", "mg"
            "Mass (MicroGram", "mcg"
            "Mass (NanoGram", "ng"
            "Volume (Liter", "L"
            "Volume (MilliLiter", "mL"
            "Volume (MicroLiter", "mcL"
            "Count (Times", "x"
            "Time (Day", "day"
            "Time (Hour", "hr"
            "Time (Minute", "min"
            "Time (Second", "sec"
            "Time (Week", "wk"
            "Time (Month", "mo"
            "Molar (MilliMol", "mmol"
            "Molar (Mol", "mol"
            "InterNatUnit (MIU", "MIU"
            "InterNatUnit (IU", "IU"
            "Weight (KiloGram", "kg"
            "Height (CentiMeter", "cm"
            "BSA (M2", "m2"
        ]


    /// Parse a Unit type string like "Volume (MilliLiter 1N)" to a readable unit string.
    let private parseUnitType (unitStr: string) =
        unitMap
        |> List.tryPick (fun (pattern, label) -> if unitStr.Contains(pattern) then Some label else None)
        |> Option.defaultValue (unitStr.Trim())


    /// Pattern: ValueUnit ([|values|], UnitType (SubType nN))
    /// Handles nested parentheses in unit types.
    let private valueUnitPattern = Regex(@"ValueUnit\s*\(\[\|([^\|]*)\|\],\s*(\w+\s*\([^)]*\))\)")


    /// Replace ValueUnit(...) patterns with readable "value unit" text.
    let private replaceValueUnits prec (text: string) =
        valueUnitPattern.Replace(
            text,
            fun m ->
                let valuesStr = m.Groups[1].Value
                let unitStr = m.Groups[2].Value
                let unit = parseUnitType unitStr

                let values =
                    valuesStr.Split(';')
                    |> Array.choose (fun v ->
                        let v = v.Trim()

                        if v = "" then
                            None
                        else
                            tryParseFraction v
                            |> Option.map (formatFloat prec)
                            |> Option.orElseWith (fun () -> Some v)
                    )

                let valStr = values |> String.concat "; "
                $"%s{valStr} %s{unit}"
        )


    /// Apply all readable formatting to the full report text.
    let makeReadable (text: string) =
        text
        |> replaceValueUnits defaultPrecision
        |> replaceBrFractions defaultPrecision
        |> replaceFractions defaultPrecision


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
    | Division


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

    /// The text lines of a log file. A line of the JSON format holds the rendered text of one
    /// event in its Text field, with the line feeds escaped: it is unfolded into its lines,
    /// after a blank line where the flat format had the header line of the event, so that the
    /// text of one event never runs into the next and line numbers count as they did. Any other
    /// line is kept as it is, so a flat file from before the JSON format reads as it did.
    let textLines (lines: string[]) : string[] =
        lines
        |> Array.collect (fun line ->
            if line.StartsWith "{" |> not then
                [| line |]
            else
                try
                    use doc = JsonDocument.Parse line

                    match doc.RootElement.TryGetProperty "Text" with
                    | true, text when text.ValueKind = JsonValueKind.String ->
                        let unfolded = text.GetString().Split '\n' |> Array.map _.TrimEnd('\r')
                        Array.append [| "" |] unfolded
                    | _ -> [| line |]
                with :? JsonException ->
                    [| line |]
        )


    let private eqPattern =
        Regex(@"^\[([^\]]+)\]_(\S+)\s+(.+?)\s+=\s+\[([^\]]+)\]_(\S+)\s+(.+?)\s+(\*|\+|/)\s+\[([^\]]+)\]_(\S+)\s+(.+)$")


    let equationLine (line: string) =
        let m = eqPattern.Match(line.Trim())

        if m.Success then
            let mkRef (nameGroup: string) (varGroup: string) (domGroup: string) =
                {
                    FullName = $"[%s{nameGroup}]_%s{varGroup}"
                    Domain = domGroup.Trim()
                }

            let op =
                match m.Groups[7].Value with
                | "*" -> Product
                | "/" -> Division
                | _ -> Sum

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

        // Normalize " x " to " * " but preserve " / " as-is (already a valid operator)
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
                let attemptedRange = line.Trim().Substring("cannot be set with this range:".Length).Trim()

                let failVar = if i >= 1 then lines[i - 1].Trim() else ""

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
                    // Search backwards for "start calculating:" (up to 20 lines)
                    let startCalc =
                        [ i - 1 .. -1 .. max 0 (i - 20) ]
                        |> List.tryPick (fun j ->
                            if lines[j].Trim().StartsWith("start calculating:") then
                                Some(lines[j].Trim())
                            else
                                None
                        )
                    // Fallback: look for "=== Start solving Equation" line which
                    // contains the equation in a similar format
                    let startSolving =
                        [ i - 1 .. -1 .. max 0 (i - 20) ]
                        |> List.tryPick (fun j ->
                            if lines[j].Trim().StartsWith("=== Start solving Equation") then
                                // The next line contains the equation
                                if j + 1 < lines.Length then
                                    let eqLine = lines[j + 1].Trim()
                                    Some($"start calculating: %s{eqLine}")
                                else
                                    None
                            else
                                None
                        )

                    startCalc |> Option.orElse startSolving |> Option.defaultValue ""

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
                // Save previous table if it had rows (handles tables without End marker)
                if inTable && currentRows.Length > 0 then
                    tables <- (currentRows |> List.rev) :: tables

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
                    // Split on '|' but keep positional structure (don't filter empties)
                    // A line like "| | sch_frq | ... |" splits to [""; ""; " sch_frq "; ...]
                    // Skip first and last empty entries from leading/trailing '|'
                    let parts =
                        let raw = line.Split('|')

                        if raw.Length >= 2 then
                            raw[1 .. raw.Length - 2] |> Array.map _.Trim()
                        else
                            [||]

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
        let mutable allSteps: PipelineStep list = []
        let mutable currentStepName = ""
        let mutable currentStepStart = 0
        let mutable inStep = false
        let mutable stepSolverPasses: SolverPass list = []
        let mutable solverStart = 0
        let mutable maxLoop = 0
        let mutable maxEqs = 0
        let mutable inSolver = false

        let solverLoopPattern = Regex(@"solver looped que (\d+) times with (\d+) equations")

        for i in 0 .. lines.Length - 1 do
            let line = lines[i]

            if line.Contains("=== PIPELINE START ") then
                let nameStart = line.IndexOf("START ") + 6
                let nameEnd = line.IndexOf(" ===", nameStart)

                currentStepName <-
                    if nameEnd > nameStart then
                        line.Substring(nameStart, nameEnd - nameStart)
                    else
                        line.Substring(nameStart).TrimEnd()

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

                    if loopN > maxLoop then
                        maxLoop <- loopN

                    if eqN > maxEqs then
                        maxEqs <- eqN

                elif line.Contains("=== Solver Finished Solving ===") then
                    inSolver <- false

            if inStep && not inSolver && line.Contains("== Finished") then
                let result =
                    if line.Contains("Errors") then
                        let errMatch = Regex.Match(line, @"(\d+) Errors")

                        if errMatch.Success then
                            SolvedWithErrors(int errMatch.Groups[1].Value)
                        else
                            SolvedWithErrors 0
                    else
                        Solved

                stepSolverPasses <-
                    {
                        StartLine = solverStart
                        EndLine = i
                        LoopCount = maxLoop
                        EquationCount = maxEqs
                        Result = result
                    }
                    :: stepSolverPasses

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
                    }
                    :: allSteps

                inStep <- false

        let steps = allSteps |> List.rev
        let mutable runs: PipelineRun list = []
        let mutable currentRun: PipelineStep list = []
        let mutable seenNames: Set<string> = Set.empty
        let mutable runIdx = 0

        for step in steps do
            if seenNames.Contains(step.Name) then
                runs <-
                    {
                        RunIndex = runIdx
                        Steps = currentRun |> List.rev
                    }
                    :: runs

                runIdx <- runIdx + 1
                currentRun <- [ step ]
                seenNames <- Set.singleton step.Name
            else
                currentRun <- step :: currentRun
                seenNames <- seenNames |> Set.add step.Name

        if currentRun.Length > 0 then
            runs <-
                {
                    RunIndex = runIdx
                    Steps = currentRun |> List.rev
                }
                :: runs

        runs |> List.rev


// ═══════════════════════════════════════════════════════════════
// Analysis
// ═══════════════════════════════════════════════════════════════

module Analyze =

    let private guidPattern = Regex(@"\[?[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.")


    let shortName (fullName: string) = fullName.Replace("[ord.", "").Replace("[", "").Replace("]", "")


    let cleanGuid (name: string) = guidPattern.Replace(name, "")


    let deduplicateErrors (errors: SolverError list) =
        // Group by (cleaned variable, attempted range) then pick the best
        // from each group: prefer errors where the FailingEquation
        // references the same variable as the FailingVariable
        errors
        |> List.groupBy (fun e ->
            let cleanVar = cleanGuid (shortName e.FailingVariable)
            cleanVar, e.AttemptedRange
        )
        |> List.map (fun (_, group) ->
            // Extract the variable name suffix (e.g., "dos_qty") from the failing variable
            let getVarSuffix (e: SolverError) =
                let v = e.FailingVariable
                let idx = v.LastIndexOf("]_")

                if idx >= 0 then
                    let afterUnderscore = v.Substring(idx + 2)
                    let spaceIdx = afterUnderscore.IndexOf(' ')

                    if spaceIdx >= 0 then
                        afterUnderscore.Substring(0, spaceIdx)
                    else
                        afterUnderscore
                else
                    ""

            // Prefer an error whose FailingEquation contains the variable suffix
            group
            |> List.sortBy (fun e ->
                let varSuffix = getVarSuffix e

                if varSuffix <> "" && e.FailingEquation.Contains(varSuffix) then
                    0
                else
                    1
            )
            |> List.head
        )


    let buildVarDomains (equations: Equation list) =
        let mutable domains = Map.empty

        for eq in equations do
            domains <- domains |> Map.add eq.Result.FullName eq.Result.Domain

            for op in eq.Operands do
                domains <- domains |> Map.add op.FullName op.Domain

        domains


    let buildEqByResult (equations: Equation list) = equations |> List.groupBy _.Result.FullName |> Map.ofList


    let rec traceVariable
        (eqByResult: Map<string, Equation list>)
        (varDomains: Map<string, string>)
        (visited: Set<string>)
        (varName: string)
        (maxDepth: int)
        =
        if maxDepth <= 0 || visited.Contains(varName) then
            let domain = varDomains |> Map.tryFind varName |> Option.defaultValue "?"

            Leaf(varName, domain, "max-depth/cycle")
        else
            let domain = varDomains |> Map.tryFind varName |> Option.defaultValue "?"

            let isFixed = domain.StartsWith("[") && not (domain.Contains(".."))

            match eqByResult |> Map.tryFind varName with
            | None -> Leaf(varName, domain, "no-equation")
            | Some _ when isFixed && maxDepth < 5 -> Leaf(varName, domain, "fixed-value")
            | Some eqs ->
                let bestEq =
                    eqs
                    |> List.sortBy (fun eq ->
                        eq.Operands |> List.sumBy (fun op -> if op.Domain.Contains("..") then 1 else 0)
                    )
                    |> List.head

                let visited' = visited |> Set.add varName

                let children =
                    bestEq.Operands
                    |> List.map (fun op -> traceVariable eqByResult varDomains visited' op.FullName (maxDepth - 1))

                let eqStr =
                    let opSym =
                        match bestEq.Op with
                        | Product -> "*"
                        | Division -> "/"
                        | Sum -> "+"

                    sprintf
                        "%s %s = %s"
                        varName
                        domain
                        (bestEq.Operands
                         |> List.map (fun o -> $"%s{o.FullName} %s{o.Domain}")
                         |> String.concat $" %s{opSym} ")

                Computed(varName, domain, bestEq.Op, children, eqStr)


    let findMatchingEqBlock (eqBlocks: Equation list list) (failEq: Equation) =
        eqBlocks
        |> List.tryFind (fun block ->
            block
            |> List.exists (fun eq ->
                eq.Operands
                |> List.exists (fun o ->
                    o.FullName = failEq.Operands[0].FullName && o.Domain = failEq.Operands[0].Domain
                )
            )
        )
        |> Option.defaultValue (if eqBlocks.Length > 0 then eqBlocks[0] else [])


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
            prf $"    Patient:    %s{c.Patient}"
            prf $"    Indication: %s{c.Indication}"
            prf $"    Generic:    %s{c.Generic}"

            if c.Route <> "" then
                prf $"    Route:      %s{c.Route}"

            if c.DoseType <> "" then
                prf $"    DoseType:   %s{c.DoseType}"
        | None -> pr "  (No order context found)"

        pr ""

        let isDefaultConstraint (c: string) = c = "<0 ..>" || c = "<0 x..>" || c = "<0 x..1 x>"

        let printConstraintTable label (table: TableRow list) =
            let sigConstraints = table |> List.filter (fun row -> not (isDefaultConstraint row.Constraints))

            if sigConstraints.Length > 0 then
                prf $"  %s{label}:"
                pr "  ─────────────────────────────────────────────────────────────────"

                for row in sigConstraints do
                    // Use "(schedule)" for rows with empty name (schedule-level variables)
                    let name = if row.Name = "" then "(schedule)" else row.Name
                    prf $"    %-42s{name} %-12s{row.Variable} %s{row.Constraints}"

                pr ""

        if tables.Length > 0 then
            printConstraintTable "Initial Constraints (non-default)" tables[0]

        if tables.Length > 1 then
            printConstraintTable "Most Recent Constraints (non-default)" tables[tables.Length - 1]

        if not pipelineRuns.IsEmpty then
            pr "  Processing Pipeline:"
            pr "  ─────────────────────────────────────────────────────────────────"

            for run in pipelineRuns do
                if pipelineRuns.Length > 1 then
                    prf $"    Pass %d{run.RunIndex + 1}:"

                for stepIdx, step in run.Steps |> List.indexed do
                    let status = if step.ErrorOccurred then "FAILED" else "OK"

                    let statusMark = if step.ErrorOccurred then "x" else "v"

                    let solverInfo =
                        match step.SolverPasses with
                        | [] -> ""
                        | passes ->
                            let totalLoops = passes |> List.sumBy _.LoopCount
                            let maxEqs = passes |> List.map _.EquationCount |> List.max
                            $" (%d{totalLoops} solver loops, %d{maxEqs} equations)"

                    prf $"    %s{statusMark} %d{stepIdx + 1}. %-40s{step.Name} %s{status}%s{solverInfo}"

                    if step.ErrorOccurred then
                        for passIdx, pass in step.SolverPasses |> List.indexed do
                            let passResult =
                                match pass.Result with
                                | Solved -> "solved"
                                | SolvedWithErrors n -> $"FAILED (%d{n} errors)"

                            prf $"        solver pass %d{passIdx + 1}: %d{pass.LoopCount} loops, %s{passResult}"

                pr ""

        let uniqueErrors = Analyze.deduplicateErrors errors

        if uniqueErrors.IsEmpty then
            pr "  No constraint conflicts detected."
        else
            prf $"  Found %d{uniqueErrors.Length} unique constraint conflict(s):"
            pr ""

        for errIdx, error in uniqueErrors |> List.indexed do
            pr "  ────────────────────────────────────────────────────────────────"
            prf $"  CONFLICT #%d{errIdx + 1}"
            pr "  ────────────────────────────────────────────────────────────────"
            pr ""

            let cleanVar = Analyze.cleanGuid (Analyze.shortName error.FailingVariable)

            prf $"  Failing variable: %s{cleanVar}"
            prf $"  Current domain:   %s{error.CurrentDomain}"
            prf $"  Attempted range:  %s{error.AttemptedRange}"

            if error.DetailMessage <> "" then
                prf $"  Detail:           %s{error.DetailMessage}"

            pr ""

            match Parse.failingEquation error.FailingEquation with
            | Some failEq ->
                let opSym =
                    match failEq.Op with
                    | Product -> "x"
                    | Division -> "/"
                    | Sum -> "+"

                pr "  Failing equation:"

                prf $"    %s{Analyze.shortName failEq.Result.FullName} %s{failEq.Result.Domain}"

                prf
                    $"      = %s{Analyze.shortName failEq.Operands[0].FullName} %s{failEq.Operands[0].Domain}  %s{opSym}  %s{Analyze.shortName failEq.Operands[1].FullName} %s{failEq.Operands[1].Domain}"

                pr ""

                let targetBlock = Analyze.findMatchingEqBlock eqBlocks failEq

                if targetBlock.Length > 0 then
                    let eqByR = Analyze.buildEqByResult targetBlock
                    let varD = Analyze.buildVarDomains targetBlock

                    let trace = Analyze.traceVariable eqByR varD Set.empty failEq.Result.FullName 7

                    pr "  Backward trace (from failing variable to source constraints):"
                    pr "  ─────────────────────────────────────────────────────────────"

                    let rec printTraceReport indent node =
                        let pad = String.replicate indent "    "

                        match node with
                        | Leaf(name, domain, source) ->
                            prf $"%s{pad}%s{Analyze.shortName name} %s{domain}  <- %s{source}"
                        | Computed(name, domain, op, children, _) ->
                            let opWord =
                                match op with
                                | Product -> "product"
                                | Division -> "division"
                                | Sum -> "sum"

                            prf $"%s{pad}%s{Analyze.shortName name} %s{domain}"
                            prf $"%s{pad}  = %s{opWord} of:"

                            for child in children do
                                printTraceReport (indent + 1) child

                    printTraceReport 1 trace
                    pr ""

                    for op in failEq.Operands do
                        let opEqs =
                            targetBlock
                            |> List.filter (fun eq -> eq.Operands |> List.exists (fun o -> o.FullName = op.FullName))

                        if opEqs.Length > 0 then
                            prf $"  Equations involving %s{Analyze.shortName op.FullName} %s{op.Domain}:"

                            for eq in opEqs |> List.distinctBy _.Result.FullName do
                                let opS =
                                    match eq.Op with
                                    | Product -> "*"
                                    | Division -> "/"
                                    | Sum -> "+"

                                prf
                                    "    %s %s = %s"
                                    (Analyze.shortName eq.Result.FullName)
                                    eq.Result.Domain
                                    (eq.Operands
                                     |> List.map (fun o -> $"%s{Analyze.shortName o.FullName} %s{o.Domain}")
                                     |> String.concat $" %s{opS} ")

                            pr ""

            | None -> prf "  (Could not parse failing equation)"

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
// Service functions (listing and analyzing log files)
// ═══════════════════════════════════════════════════════════════

[<Literal>]
let private MaxFileSizeBytes = 50_000_000L

let private fileNamePattern = Regex(@"^genpres_[A-Za-z0-9_]+\.log$")


let listLogFiles () =
    let logDir = Informedica.Utils.Lib.AppPath.logsDir ()

    if not (Directory.Exists(logDir)) then
        [||]
    else
        Directory.GetFiles(logDir, "*.log")
        |> Array.filter (fun path -> fileNamePattern.IsMatch(Path.GetFileName(path)))
        |> Array.map (fun path ->
            let fi = FileInfo(path)

            {|
                FileName = fi.Name
                SizeBytes = fi.Length
                LastModifiedAt = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                LastWriteTime = fi.LastWriteTime
            |}
        )
        |> Array.sortByDescending _.LastWriteTime
        |> Array.map (fun f ->
            {
                Shared.Types.LogFileInfo.FileName = f.FileName
                SizeBytes = f.SizeBytes
                LastModifiedAt = f.LastModifiedAt
            }
            : Shared.Types.LogFileInfo
        )


let analyzeFile (fileName: string) : Result<string, string[]> =
    // Path traversal protection
    if fileName.Contains("/") || fileName.Contains("\\") || fileName.Contains("..") then
        Error [| "Invalid file name" |]
    elif not (fileNamePattern.IsMatch(fileName)) then
        Error [| "Invalid file name format" |]
    else
        let logDir = Informedica.Utils.Lib.AppPath.logsDir ()
        let fullPath = Path.Combine(logDir, fileName)

        if not (File.Exists(fullPath)) then
            Error [| $"Log file not found: %s{fileName}" |]
        else
            let fi = FileInfo(fullPath)

            if fi.Length > MaxFileSizeBytes then
                Error [| $"Log file too large (%d{fi.Length / 1_000_000L} MB). Maximum is 50 MB." |]
            else
                let lines = File.ReadAllLines(fullPath) |> Parse.textLines
                let ctx = Parse.orderContext lines
                let errors = Parse.errors lines
                let eqBlocks = Parse.equationBlocks lines
                let tables = Parse.constraintTables lines
                let pipelineRuns = Parse.pipelineSteps lines

                let report = Report.generate ctx errors eqBlocks tables pipelineRuns |> Format.makeReadable

                Ok report
