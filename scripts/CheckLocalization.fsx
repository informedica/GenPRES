// Checks that the localization file covers the Terms union.
//
// The terms and their translations live in data/localization/localization.tsv, tab separated,
// the shape of the Google "Localization" sheet: the header
// row Term, English, Dutch, French, German, Spanish, Italian, then one row per term with the
// term key in the first column. That file is the source; the sheet is what it is pasted into.
//
// The script scans it against the union:
//
//   - every case of Terms has a row, else it fails, naming the cases without one;
//   - a row whose term is no case is reported as stale;
//   - every locale of Locales has a column, and every cell is filled, else reported;
//   - a cell may not hold a tab or a line break.
//
// Run from the repository root:
//
//   dotnet fsi scripts/CheckLocalization.fsx           the summary, exit 1 when a term is missing
//   dotnet fsi scripts/CheckLocalization.fsx --print   the file's rows on stdout, the summary on
//                                                      stderr: pipe it to pbcopy, paste it over
//                                                      the sheet
//
// The main runs only when this file is the script given to dotnet fsi: loaded from another
// script (scripts/CheckLocalizationTests.fsx, or the FSI MCP server) it defines the functions
// and does nothing.

#I __SOURCE_DIRECTORY__

#load "../src/Informedica.GenPRES.Shared/Types.fs"
#load "../src/Informedica.GenPRES.Shared/Utils.fs"
#load "../src/Informedica.GenPRES.Shared/Localization.fs"

open System
open System.IO
open Microsoft.FSharp.Reflection
open Shared


/// One row of the file: the term key, then one cell per header column after Term.
type Row =
    {
        Term: string
        Cells: string list
    }


/// The file as read: the header cells and the rows below it.
type Localization =
    {
        Header: string list
        Rows: Row list
    }


/// What the scan found.
type Report =
    {
        /// The term keys of the union without a row, in the order of the union.
        Missing: string list
        /// The rows whose term is no case of the union.
        Stale: string list
        /// The locale columns the header lacks, in the order of the union.
        MissingColumns: string list
        /// Per header column after Term, the term keys with an empty cell.
        EmptyCells: (string * string list) list
        /// The rows holding a tab or a line break in a cell.
        Unwritable: string list
    }


/// The case names of the Terms union, in declaration order, as getTerm looks them up.
let termKeys () : string list =
    FSharpType.GetUnionCases typeof<Terms>
    |> Array.map (fun c -> c.Name.Trim())
    |> Array.toList


/// The column names of the locales, in the order getTerm reads the columns: the case names of
/// the Locales union, as the sheet names its columns.
let localeColumns () : string list =
    Localization.languages |> Array.map (fun l -> $"{l}") |> Array.toList


/// Parses the lines of the file. The first line is the header; every other line has to have the
/// same number of cells, and a line may not repeat a term.
let parse (lines: string list) : Localization =
    match lines with
    | [] -> invalidOp "the localization file is empty"
    | header :: rest ->
        let header = header.Split '\t' |> Array.toList

        match header with
        | "Term" :: _ -> ()
        | _ -> invalidOp "the first column of the header is not 'Term'"

        let rows =
            rest
            |> List.filter (String.IsNullOrEmpty >> not)
            |> List.mapi (fun i line ->
                let cells = line.Split '\t' |> Array.toList

                if cells.Length <> header.Length then
                    invalidOp
                        $"line %i{i + 2} has %i{cells.Length} cells, the header has %i{header.Length}"

                {
                    Term = cells.Head.Trim()
                    Cells = cells.Tail
                }
            )

        let dupes =
            rows
            |> List.countBy _.Term
            |> List.filter (fun (_, n) -> n > 1)
            |> List.map fst

        if not dupes.IsEmpty then
            invalidOp $"""the file repeats the terms: %s{String.Join(", ", dupes)}"""

        {
            Header = header
            Rows = rows
        }


/// Whether a cell holds what the tab separated shape cannot carry.
let unwritable (s: string) =
    s.Contains '\t' || s.Contains '\n' || s.Contains '\r'


/// Scans the file against the term keys and the locale columns.
let scan (locales: string list) (terms: string list) (file: Localization) : Report =
    let termSet = Set.ofList terms
    let present = file.Rows |> List.map _.Term |> Set.ofList

    {
        Missing = terms |> List.filter (present.Contains >> not)
        Stale =
            file.Rows
            |> List.map _.Term
            |> List.filter (termSet.Contains >> not)
        MissingColumns = locales |> List.filter (fun l -> file.Header |> List.contains l |> not)
        EmptyCells =
            file.Header.Tail
            |> List.mapi (fun i name ->
                name,
                file.Rows
                |> List.filter (fun r -> String.IsNullOrWhiteSpace r.Cells[i])
                |> List.map _.Term
            )
        Unwritable =
            file.Rows
            |> List.filter (fun r -> r.Term :: r.Cells |> List.exists unwritable)
            |> List.map _.Term
    }


/// Whether the report lets the check pass: every term has a row, every locale a column, and
/// every cell can be written.
let passes (report: Report) =
    report.Missing.IsEmpty
    && report.MissingColumns.IsEmpty
    && report.Unwritable.IsEmpty


/// The lines of the file, tab separated, no quoting.
let toLines (file: Localization) : string list =
    (file.Header |> String.concat "\t")
    :: (file.Rows |> List.map (fun r -> r.Term :: r.Cells |> String.concat "\t"))


/// The repository root: the first directory upward from `start` holding GenPRES.sln.
let repoRoot (start: string) =
    let rec go (dir: DirectoryInfo) =
        if isNull dir then
            invalidOp $"no GenPRES.sln found upward from %s{start}"
        elif File.Exists(Path.Combine(dir.FullName, "GenPRES.sln")) then
            dir.FullName
        else
            go dir.Parent

    go (DirectoryInfo start)


/// The localization file.
let localizationPath () =
    Path.Combine(repoRoot __SOURCE_DIRECTORY__, "data", "localization", "localization.tsv")


let readLocalization (path: string) =
    File.ReadAllLines path |> Array.toList |> parse


/// Where the summary goes: stdout, or stderr when stdout carries the rows.
[<RequireQualifiedAccess>]
type Output =
    | Summary
    | Rows


/// Reads the file, scans it, prints the summary and, with Output.Rows, the file's rows on
/// stdout with the summary on stderr. Returns the report.
let run (output: Output) =
    let path = localizationPath ()
    let terms = termKeys ()
    let locales = localeColumns ()
    let file = readLocalization path
    let report = scan locales terms file

    let say (s: string) =
        match output with
        | Output.Summary -> printfn $"%s{s}"
        | Output.Rows -> eprintfn $"%s{s}"

    say $"terms in the union: %i{terms.Length}, rows in the file: %i{file.Rows.Length}"
    say $"terms without a row: %i{report.Missing.Length}"

    for t in report.Missing do
        say $"  - %s{t}"

    say $"stale rows: %i{report.Stale.Length}"

    for t in report.Stale do
        say $"  ? %s{t}"

    say $"locales without a column: %i{report.MissingColumns.Length}"

    for c in report.MissingColumns do
        say $"  - %s{c}"

    say "empty cells per locale:"

    for name, ts in report.EmptyCells do
        say $"  %s{name}: %i{ts.Length}"

        for t in ts do
            say $"    %s{t}"

    for t in report.Unwritable do
        say $"tab or line break in the row: %s{t}"

    say (if passes report then "localization: ok" else "localization: FAILED")

    match output with
    | Output.Summary -> ()
    | Output.Rows ->
        for line in toLines file do
            printfn $"%s{line}"

    report


// ── Main ──────────────────────────────────────────────────────────────────────────────────────

let isEntry =
    fsi.CommandLineArgs
    |> Array.tryHead
    |> Option.map (fun a -> Path.GetFileName a = Path.GetFileName __SOURCE_FILE__)
    |> Option.defaultValue false

if isEntry then
    let args = fsi.CommandLineArgs |> Array.skip 1 |> Array.toList

    let output =
        if args |> List.contains "--print" then
            Output.Rows
        else
            Output.Summary

    if run output |> passes |> not then
        exit 1
