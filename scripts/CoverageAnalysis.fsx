/// Test Coverage Analysis Script
/// Scans source and test directories to identify coverage gaps
/// across all GenPRES libraries.
///
/// Run with: dotnet fsi scripts/CoverageAnalysis.fsx

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

let srcRoot = Path.Combine(repoRoot, "src")
let testsRoot = Path.Combine(repoRoot, "tests")

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let countCodeLines (file: string) =
    File.ReadAllLines(file)
    |> Array.filter (fun l ->
        let t = l.Trim()
        t.Length > 0 && not (t.StartsWith("//")) && not (t.StartsWith("(*"))
    )
    |> Array.length

let countPatternInDir (pattern: string) (dir: string) =
    if not (Directory.Exists dir) then
        0
    else
        Directory.GetFiles(dir, "*.fs", SearchOption.AllDirectories)
        |> Array.sumBy (fun f -> Regex.Matches(File.ReadAllText(f), pattern).Count)

let countPublicFunctions dir =
    countPatternInDir @"(?m)^\s{0,8}let\s+\w" dir

let countTestCases dir =
    countPatternInDir @"\btest\b|\btestCase\b|\btestAsync\b|\btestTask\b" dir

let countProperties dir =
    countPatternInDir @"\btestProperty\b|\btestPropertyWithConfig\b" dir

// ---------------------------------------------------------------------------
// Library model
// ---------------------------------------------------------------------------

type LibCoverage =
    {
        Name: string
        SrcFiles: int
        SrcLines: int
        SrcFunctions: int
        HasTests: bool
        TestFiles: int
        TestCases: int
        Properties: int
        IsStub: bool
    }

let coverageRatio (c: LibCoverage) =
    if c.SrcFunctions = 0 then
        1.0
    else
        float c.TestCases / float c.SrcFunctions

let private libName (dir: string) =
    let m = Regex.Match(Path.GetFileName(dir), @"Informedica\.(\w+)\.Lib")

    if m.Success then
        m.Groups.[1].Value
    else
        Path.GetFileName(dir)

let analyseLib (srcDir: string) =
    let name = libName srcDir
    let testDir = Path.Combine(testsRoot, sprintf "Informedica.%s.Tests" name)

    let srcFiles =
        if Directory.Exists srcDir then
            Directory.GetFiles(srcDir, "*.fs", SearchOption.TopDirectoryOnly)
            |> Array.filter (fun f -> not (f.EndsWith(".fsx")))
        else
            [||]

    let hasTests = Directory.Exists testDir
    let testCases = countTestCases testDir

    {
        Name = name
        SrcFiles = srcFiles.Length
        SrcLines = srcFiles |> Array.sumBy countCodeLines
        SrcFunctions = countPublicFunctions srcDir
        HasTests = hasTests
        TestFiles =
            if hasTests then
                Directory.GetFiles(testDir, "*.fs", SearchOption.AllDirectories).Length
            else
                0
        TestCases = testCases
        Properties = countProperties testDir
        IsStub = hasTests && testCases <= 3
    }

// ---------------------------------------------------------------------------
// Analysis
// ---------------------------------------------------------------------------

let libraries =
    Directory.GetDirectories(srcRoot, "Informedica.*.Lib", SearchOption.TopDirectoryOnly)
    |> Array.sortBy Path.GetFileName
    |> Array.map analyseLib

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

let private bar n maxN width =
    let filled = if maxN = 0 then 0 else min width (n * width / maxN)
    String.replicate filled "#" + String.replicate (width - filled) "."

let private status (c: LibCoverage) =
    if not c.HasTests || c.IsStub then "STUB"
    elif coverageRatio c >= 0.20 then "OK  "
    elif coverageRatio c >= 0.10 then "LOW "
    else "GAP "

let printReport () =
    let maxFns = libraries |> Array.map _.SrcFunctions |> Array.max |> max 1
    let maxTests = libraries |> Array.map _.TestCases |> Array.max |> max 1
    let sep = String.replicate 84 "="

    printfn ""
    printfn "%s" sep
    printfn "  GenPRES -- W3 Test Coverage Analysis"
    printfn "  Generated: %s UTC" (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"))
    printfn "%s" sep
    printfn ""

    printfn
        "  %-16s %8s %7s %10s %6s %7s  %-30s %s"
        "Library"
        "SrcFiles"
        "SrcFns"
        "TestCases"
        "Props"
        "Ratio"
        "Coverage [src|tests]"
        "Status"

    printfn
        "  %s %s %s %s %s %s  %s %s"
        (String.replicate 16 "-")
        (String.replicate 8 "-")
        (String.replicate 7 "-")
        (String.replicate 10 "-")
        (String.replicate 6 "-")
        (String.replicate 7 "-")
        (String.replicate 30 "-")
        (String.replicate 6 "-")

    for lib in libraries do
        let srcBar = bar lib.SrcFunctions maxFns 14
        let testBar = bar lib.TestCases maxTests 14

        printfn
            "  %-16s %8d %7d %10d %6d %6.0f%%  [%s|%s] %s"
            lib.Name
            lib.SrcFiles
            lib.SrcFunctions
            lib.TestCases
            lib.Properties
            (coverageRatio lib * 100.0)
            srcBar
            testBar
            (status lib)

    printfn ""
    printfn "%s" sep
    printfn "  Summary"
    printfn "%s" sep

    let stubs = libraries |> Array.filter _.IsStub |> Array.length
    let noTests = libraries |> Array.filter (fun l -> not l.HasTests) |> Array.length

    let gaps =
        libraries
        |> Array.filter (fun l -> l.HasTests && not l.IsStub && coverageRatio l < 0.10)
        |> Array.length

    let totalFns = libraries |> Array.sumBy _.SrcFunctions
    let totalTests = libraries |> Array.sumBy _.TestCases
    let totalProps = libraries |> Array.sumBy _.Properties

    printfn "  Libraries analysed     : %d" libraries.Length
    printfn "  Source functions       : %d" totalFns
    printfn "  Total test cases       : %d  (+ %d property-based)" totalTests totalProps
    printfn "  Overall ratio          : %.0f%%" (float totalTests / float totalFns * 100.0)
    printfn "  Stub test projects     : %d  (library not yet implemented)" stubs
    printfn "  Missing test dirs      : %d" noTests
    printfn "  Coverage gaps (< 10%%) : %d" gaps

    printfn ""
    printfn "%s" sep
    printfn "  W3 Priority: Libraries with source code and < 20%% test coverage"
    printfn "%s" sep

    libraries
    |> Array.filter (fun l -> l.SrcFunctions > 20 && coverageRatio l < 0.20 && not l.IsStub)
    |> Array.sortBy coverageRatio
    |> Array.iter (fun l ->
        let needed = max 0 (l.SrcFunctions / 5 - l.TestCases)

        printfn
            "  %-16s  %4d source fns, %4d tests (%3.0f%%) - needs ~%d more tests for 20%% target"
            l.Name
            l.SrcFunctions
            l.TestCases
            (coverageRatio l * 100.0)
            needed
    )

    printfn ""
    printfn "Status key:"
    printfn "  OK    >= 20%% test-to-function ratio"
    printfn "  LOW   10-19%% (light coverage)"
    printfn "  GAP   < 10%% (coverage gap)"
    printfn "  STUB  No tests or placeholder stub only"
    printfn ""

printReport ()
