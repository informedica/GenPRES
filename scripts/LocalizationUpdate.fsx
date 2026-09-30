// Regenerates the localization snapshot from the Terms union.
//
// Every UI label is a case of `Terms` in `Localization.fs`; the translations live in the Google
// "Localization" sheet, and `data/localization/GenPRES - Localization - Localization.tsv` is a
// snapshot of that sheet. This script brings the snapshot in step with the union:
//
//   - existing rows are kept unchanged, in their current order;
//   - a case without a row is appended with empty translation cells, so the missing translations
//     show up as gaps in the sheet;
//   - a row whose term is no longer a case is reported on stderr and dropped, unless
//     `--keep-stale` is given;
//   - a locale of the Locales union without a column is appended, named as the sheet names its
//     columns (English, Dutch, ...), with empty cells; the existing columns keep their order,
//     since getTerm reads them by position.
//
// The output overwrites the snapshot, and --print gives the rows to paste over the sheet.
//
// Run from the repository root or from this directory:
//
//   dotnet fsi scripts/LocalizationUpdate.fsx               write the snapshot, print the summary
//   dotnet fsi scripts/LocalizationUpdate.fsx --dry-run     print the summary only
//   dotnet fsi scripts/LocalizationUpdate.fsx --keep-stale  keep rows whose term left the union
//   dotnet fsi scripts/LocalizationUpdate.fsx --print       the full TSV on stdout, the summary
//                                                           on stderr: pipe it to pbcopy and
//                                                           paste it over the sheet
//   dotnet fsi scripts/LocalizationUpdate.fsx --file <tsv>  read and write that file instead of
//                                                           the snapshot: an export of the sheet
//
// The main runs only when this file is the script given to dotnet fsi: loaded from another
// script (scripts/LocalizationUpdateTests.fsx, or the FSI MCP server) it defines the functions
// and does nothing, and `run` is called by hand.

#I __SOURCE_DIRECTORY__

#load "../src/Informedica.GenPRES.Shared/Types.fs"
#load "../src/Informedica.GenPRES.Shared/Utils.fs"
#load "../src/Informedica.GenPRES.Shared/Localization.fs"

open System
open System.IO
open Microsoft.FSharp.Reflection
open Shared


/// One row of the snapshot: the term key, then one cell per header column after Term.
type Row =
    {
        Term: string
        Cells: string list
    }


/// The snapshot as read from the file: the header cells and the rows below it.
type Snapshot =
    {
        Header: string list
        Rows: Row list
    }


/// What one update did, next to the rows it produced.
type Update =
    {
        Snapshot: Snapshot
        /// The term keys appended, in the order of the union.
        Added: string list
        /// The locale columns appended, in the order of the union.
        AddedColumns: string list
        /// The rows whose term is no longer a case, dropped or kept.
        Stale: Row list
        /// Per header column after Term, the number of empty cells in the result.
        EmptyCells: (string * int) list
    }


/// Whether a row whose term left the union is kept in the result or dropped.
[<RequireQualifiedAccess>]
type StaleRows =
    | Drop
    | Keep


/// The case names of the Terms union, in declaration order, as `getTerm` looks them up.
let termKeys () : string list =
    FSharpType.GetUnionCases typeof<Terms>
    |> Array.map (fun c -> c.Name.Trim())
    |> Array.toList


/// The column names of the locales, in the order `getTerm` reads the columns: the case names of
/// the Locales union, as the sheet names its columns.
let localeColumns () : string list =
    Localization.languages |> Array.map (fun l -> $"{l}") |> Array.toList


/// Fails on a cell that the tab separated shape cannot carry.
let checkCell (term: string) (cell: string) =
    if cell.Contains '\t' || cell.Contains '\n' || cell.Contains '\r' then
        invalidOp $"the row for term '%s{term}' holds a tab or a line break"


/// Parses the lines of a snapshot. The first line is the header; every other line has to have
/// the same number of cells, and a line may not repeat a term.
let parse (lines: string list) : Snapshot =
    match lines with
    | [] -> invalidOp "the snapshot is empty"
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
            invalidOp $"""the snapshot repeats the terms: %s{String.Join(", ", dupes)}"""

        {
            Header = header
            Rows = rows
        }


/// Brings the snapshot in step with the term keys and the locale columns: columns kept in
/// order and missing locales appended, rows kept in order, missing terms appended with empty
/// cells, stale rows dropped or kept.
let update (stale: StaleRows) (locales: string list) (terms: string list) (snapshot: Snapshot) : Update =
    let termSet = Set.ofList terms
    let present = snapshot.Rows |> List.map _.Term |> Set.ofList

    let addedColumns =
        locales |> List.filter (fun l -> snapshot.Header |> List.contains l |> not)

    let header = snapshot.Header @ addedColumns
    let width = header.Length - 1
    let empty = List.replicate width ""
    let pad (cells: string list) = cells @ List.replicate addedColumns.Length ""

    let staleRows = snapshot.Rows |> List.filter (fun r -> termSet.Contains r.Term |> not)

    let kept =
        match stale with
        | StaleRows.Keep -> snapshot.Rows
        | StaleRows.Drop -> snapshot.Rows |> List.filter (fun r -> termSet.Contains r.Term)
        |> List.map (fun r -> { r with Cells = pad r.Cells })

    let added = terms |> List.filter (present.Contains >> not)

    let rows =
        kept
        @ (added
           |> List.map (fun t ->
               {
                   Term = t
                   Cells = empty
               }
           ))

    for r in rows do
        checkCell r.Term r.Term

        for c in r.Cells do
            checkCell r.Term c

    let emptyCells =
        header.Tail
        |> List.mapi (fun i name ->
            name, rows |> List.filter (fun r -> String.IsNullOrWhiteSpace r.Cells[i]) |> List.length
        )

    {
        Snapshot =
            {
                Header = header
                Rows = rows
            }
        Added = added
        AddedColumns = addedColumns
        Stale = staleRows
        EmptyCells = emptyCells
    }


/// The lines of a snapshot, tab separated, no quoting.
let toLines (snapshot: Snapshot) : string list =
    (snapshot.Header |> String.concat "\t")
    :: (snapshot.Rows |> List.map (fun r -> r.Term :: r.Cells |> String.concat "\t"))


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


/// The snapshot file.
let snapshotPath () =
    Path.Combine(
        repoRoot __SOURCE_DIRECTORY__,
        "data",
        "localization",
        "GenPRES - Localization - Localization.tsv"
    )


let readSnapshot (path: string) =
    File.ReadAllLines path |> Array.toList |> parse


/// UTF-8 without a byte order mark, LF line ends, one trailing newline, as the sheet export.
let writeSnapshot (path: string) (snapshot: Snapshot) =
    let text = (toLines snapshot |> String.concat "\n") + "\n"
    File.WriteAllText(path, text, Text.UTF8Encoding false)


/// Where the summary goes: stdout, or stderr when stdout carries the TSV.
[<RequireQualifiedAccess>]
type Output =
    | Summary
    | Tsv


/// Reads the snapshot, updates it from the union, writes it back unless `dryRun`, and prints
/// the summary. With `Output.Tsv` the full updated TSV goes to stdout, header first, and the
/// summary to stderr, so the output can be piped and pasted over the sheet. Stale rows go to
/// stderr either way.
let run (file: string option) (stale: StaleRows) (dryRun: bool) (output: Output) =
    let path = file |> Option.defaultWith snapshotPath
    let terms = termKeys ()
    let locales = localeColumns ()
    let before = readSnapshot path
    let result = update stale locales terms before

    let say (s: string) =
        match output with
        | Output.Summary -> printfn $"%s{s}"
        | Output.Tsv -> eprintfn $"%s{s}"

    let staleVerb =
        match stale with
        | StaleRows.Drop -> "dropped"
        | StaleRows.Keep -> "kept"

    for r in result.Stale do
        eprintfn $"stale row %s{staleVerb}: %s{r.Term}"

    if not dryRun then
        writeSnapshot path result.Snapshot

    say $"terms in the union: %i{terms.Length}, locales: %i{locales.Length}"
    say $"columns added: %i{result.AddedColumns.Length}"

    for c in result.AddedColumns do
        say $"  + %s{c}"

    say $"rows before: %i{before.Rows.Length}, after: %i{result.Snapshot.Rows.Length}"
    say $"rows added: %i{result.Added.Length}"

    for t in result.Added do
        say $"  + %s{t}"

    say $"rows stale (%s{staleVerb}): %i{result.Stale.Length}"
    say "empty cells per locale:"

    for name, n in result.EmptyCells do
        say $"  %s{name}: %i{n}"

    if dryRun then
        say "dry run: the snapshot was not written"
    else
        say $"written: %s{path}"

    match output with
    | Output.Summary -> ()
    | Output.Tsv ->
        for line in toLines result.Snapshot do
            printfn $"%s{line}"

    result


// ── Main ──────────────────────────────────────────────────────────────────────────────────────

let isEntry =
    fsi.CommandLineArgs
    |> Array.tryHead
    |> Option.map (fun a -> Path.GetFileName a = Path.GetFileName __SOURCE_FILE__)
    |> Option.defaultValue false

if isEntry then
    let args = fsi.CommandLineArgs |> Array.skip 1 |> Array.toList

    let stale =
        if args |> List.contains "--keep-stale" then
            StaleRows.Keep
        else
            StaleRows.Drop

    let output =
        if args |> List.contains "--print" then
            Output.Tsv
        else
            Output.Summary

    let file =
        match args |> List.tryFindIndex ((=) "--file") with
        | Some i when i + 1 < args.Length -> Some(Path.GetFullPath args[i + 1])
        | Some _ -> invalidOp "--file needs a path"
        | None -> None

    run file stale (args |> List.contains "--dry-run") output |> ignore
