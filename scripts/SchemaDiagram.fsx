// Database schema diagrams, generated from the SQL migrations.
//
// Reads every migration in src/Informedica.GenPRES.Server/Sql/ in order: each `create table`
// gives a table, its columns and their keys, each `alter table ... add column` a column added
// later, and each `references` a declared foreign key. A table rebuilt to change a check, made
// anew, the old one dropped and the new one renamed to the old name, reads as the new table
// under the old name. The migrations cannot say how the store
// is split into readable parts, nor which tables join by id without a declared key; `parts`
// below says that, and the run fails when a table is in no part, in two, or when a join names
// a column no table has. The diagrams live in ARCHITECTURE.md between the `db-schema` markers;
// CI runs `--check` so the picture cannot drift from the migrations.
//
// Run with:
//   dotnet fsi scripts/SchemaDiagram.fsx            # the block to stdout
//   dotnet fsi scripts/SchemaDiagram.fsx --write    # rewrite the block in ARCHITECTURE.md
//   dotnet fsi scripts/SchemaDiagram.fsx --check    # exit 1 when that block is out of date

open System
open System.IO
open System.Text.RegularExpressions


let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let sqlDir = Path.Combine(repoRoot, "src", "Informedica.GenPRES.Server", "Sql")
let architecturePath = Path.Combine(repoRoot, "ARCHITECTURE.md")
let startMarker = "<!-- db-schema:start -->"
let endMarker = "<!-- db-schema:end -->"


// ---------------------------------------------------------------------------
// The migrations
// ---------------------------------------------------------------------------

type Column =
    {
        Name: string
        Type: string
        Primary: bool
        Unique: bool
        /// The table and column a declared foreign key names.
        References: (string * string) option
        /// The column's comment says it holds JSON.
        Json: bool
        /// The migration that added the column, when it came after the table's own.
        AddedIn: int option
    }


type Table = { Name: string; Columns: Column list }


/// `003-credentials.sql` -> 3.
let migrationNo (path: string) =
    let name = Path.GetFileName path
    Int32.Parse(name.Substring(0, name.IndexOf '-'))


let mermaidType (sqlType: string) =
    match sqlType.ToLowerInvariant() with
    | "integer" -> "int"
    | t -> t


let columnPattern =
    Regex(@"^\s*(?<name>[a-z_]+)\s+(?<type>integer|text|blob)\b(?<rest>.*)$", RegexOptions.IgnoreCase)


let referencesPattern =
    Regex(@"references\s+(?<table>[a-z_]+)\s*\(\s*(?<column>[a-z_]+)\s*\)", RegexOptions.IgnoreCase)


/// One column line of a `create table` or an `add column`, or None for a constraint line.
let parseColumn (addedIn: int option) (line: string) =
    let m = columnPattern.Match line

    if not m.Success then
        None
    else
        let rest = m.Groups["rest"].Value
        let code, comment =
            match rest.IndexOf "--" with
            | -1 -> rest, ""
            | i -> rest.Substring(0, i), rest.Substring(i + 2).Trim()

        let code = code.ToLowerInvariant()
        let r = referencesPattern.Match code

        Some
            {
                Name = m.Groups["name"].Value
                Type = mermaidType m.Groups["type"].Value
                Primary = code.Contains "primary key"
                Unique = code.Contains "unique"
                References = if r.Success then Some(r.Groups["table"].Value, r.Groups["column"].Value) else None
                Json = comment.StartsWith "json"
                AddedIn = addedIn
            }


let createPattern =
    Regex(@"create\s+table\s+(?<name>[a-z_]+)\s*\((?<body>.*?)\n\);", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)


let alterPattern =
    Regex(
        @"alter\s+table\s+(?<table>[a-z_]+)\s+add\s+column\s+(?<column>[^\n;]*?);(?<comment>[^\n]*)",
        RegexOptions.IgnoreCase
    )


let dropPattern =
    Regex(@"drop\s+table\s+(?<name>[a-z_]+)\s*;", RegexOptions.IgnoreCase)


let renamePattern =
    Regex(@"alter\s+table\s+(?<from>[a-z_]+)\s+rename\s+to\s+(?<to>[a-z_]+)\s*;", RegexOptions.IgnoreCase)


let compositeKeyPattern =
    Regex(@"^\s*primary\s+key\s*\((?<columns>[^)]*)\)", RegexOptions.IgnoreCase)


/// The tables the migrations build, in the order they were created.
let readTables () =
    let files = Directory.GetFiles(sqlDir, "*.sql") |> Array.sort

    (([]: Table list), files)
    ||> Array.fold (fun tables file ->
        let no = migrationNo file
        let text = File.ReadAllText file |> _.Replace("\r\n", "\n")

        let created =
            createPattern.Matches text
            |> Seq.map (fun m ->
                let lines = m.Groups["body"].Value.Split '\n'

                let composite =
                    lines
                    |> Array.tryPick (fun l ->
                        let k = compositeKeyPattern.Match l
                        if k.Success then Some k.Groups["columns"].Value else None
                    )
                    |> Option.map (fun cs -> cs.Split ',' |> Array.map _.Trim() |> Set.ofArray)
                    |> Option.defaultValue Set.empty

                {
                    Name = m.Groups["name"].Value
                    Columns =
                        lines
                        |> Array.choose (parseColumn None)
                        |> Array.map (fun c -> { c with Primary = c.Primary || composite.Contains c.Name })
                        |> Array.toList
                }
            )
            |> Seq.toList

        // a rebuild within one migration: the new table is created, the old one dropped and the
        // new one renamed to the old name
        let dropped =
            dropPattern.Matches text |> Seq.map _.Groups["name"].Value |> Set.ofSeq

        let renamed =
            renamePattern.Matches text
            |> Seq.map (fun m -> m.Groups["from"].Value, m.Groups["to"].Value)
            |> Map.ofSeq

        let tables =
            tables @ created
            |> List.filter (fun t -> dropped.Contains t.Name |> not)
            |> List.map (fun t ->
                match renamed |> Map.tryFind t.Name with
                | Some name -> { t with Name = name }
                | None -> t
            )

        (tables, alterPattern.Matches text)
        ||> Seq.fold (fun tables m ->
            let column = m.Groups["column"].Value
            let comment = m.Groups["comment"].Value.TrimStart('-', ' ')
            let line = $"%s{column} --%s{comment}"

            match parseColumn (Some no) line with
            | None -> tables
            | Some column ->
                tables
                |> List.map (fun t ->
                    if t.Name = m.Groups["table"].Value then
                        { t with Columns = t.Columns @ [ column ] }
                    else
                        t
                )
        )
    )


// ---------------------------------------------------------------------------
// The parts
// ---------------------------------------------------------------------------

/// A join by id that no foreign key declares: from, cardinality, to, the columns it joins on.
type Join =
    {
        From: string
        Cardinality: string
        To: string
        On: string list
    }


type Part =
    {
        Title: string
        /// The tables drawn whole, each in exactly one part.
        Tables: string list
        /// Tables of another part, drawn with only the columns this part joins on.
        Stubs: (string * string list) list
        Joins: Join list
    }


let join from cardinality ``to`` on =
    {
        From = from
        Cardinality = cardinality
        To = ``to``
        On = on
    }


let parts =
    [
        {
            Title = "The record and what a Session opens with"
            Tables = [ "order_plan"; "session"; "session_opened_with" ]
            Stubs = []
            Joins =
                [
                    join "order_plan" "||--o|" "order_plan" [ "base" ]
                    join "session_opened_with" "}o--o|" "order_plan" [ "version_id"; "head_id" ]
                    join "session" "}o--o{" "order_plan" [ "patient_id" ]
                ]
        }
        {
            Title = "A Session's life"
            Tables =
                [
                    "session_seen"
                    "session_ending"
                    "session_acknowledged"
                    "measurement"
                    "audit_entry"
                ]
            Stubs = [ ("session", [ "session_id" ]) ]
            Joins = [ join "session" "|o--o{" "audit_entry" [ "session_id" ] ]
        }
        {
            Title = "Signing"
            Tables = [ "data_notice"; "challenge"; "challenge_spent"; "submission_answer" ]
            Stubs = [ "session", [ "session_id" ]; "order_plan", [ "version_id" ] ]
            Joins = [ join "submission_answer" "}o--o|" "order_plan" [ "version_id" ] ]
        }
        {
            Title = "Launch, credentials and enrolment"
            Tables =
                [
                    "launch_record"
                    "launch_outcome"
                    "enrolment"
                    "enrolment_dropped"
                    "credential_event"
                    "confirmation_code"
                    "code_try"
                    "code_spent"
                ]
            Stubs = [ ("session", [ "session_id"; "user_id" ]) ]
            Joins =
                [
                    join "launch_outcome" "}o--o|" "session" [ "session_id" ]
                    join "launch_outcome" "}o--o|" "enrolment" [ "attempt" ]
                    join "session" "}o--o{" "credential_event" [ "user_id" ]
                    join "enrolment" "}o--o{" "confirmation_code" [ "user_id" ]
                ]
        }
    ]


/// What is wrong with the parts against the tables, nothing when they fit.
let problems (tables: Table list) =
    let names = tables |> List.map _.Name |> Set.ofList
    let drawn = parts |> List.collect _.Tables

    let column table name =
        tables
        |> List.tryFind (fun t -> t.Name = table)
        |> Option.exists (fun t -> t.Columns |> List.exists (fun c -> c.Name = name))

    [
        for t in tables do
            match drawn |> List.filter ((=) t.Name) |> List.length with
            | 0 -> $"table %s{t.Name} is in no part"
            | 1 -> ()
            | _ -> $"table %s{t.Name} is in more than one part"

        for p in parts do
            for t in p.Tables do
                if not (names.Contains t) then
                    $"part '%s{p.Title}' names table %s{t}, which no migration creates"

            for t, cs in p.Stubs do
                for c in cs do
                    if not (column t c) then
                        $"part '%s{p.Title}' shows %s{t}.%s{c}, which no migration creates"

            for j in p.Joins do
                // a join's column lives on one of its two ends
                for c in j.On do
                    if not (column j.From c || column j.To c) then
                        $"part '%s{p.Title}' joins %s{j.From} and %s{j.To} on %s{c}, which neither has"
    ]


// ---------------------------------------------------------------------------
// Mermaid
// ---------------------------------------------------------------------------

let keys (c: Column) =
    [
        if c.Primary then "PK"
        if c.References.IsSome then "FK"
        if c.Unique && not c.Primary then "UK"
    ]
    |> String.concat ", "


let note (c: Column) =
    [
        if c.Json then "json"
        match c.AddedIn with
        | Some no -> $"migr %i{no}"
        | None -> ()
    ]
    |> String.concat ", "


let columnLine (c: Column) =
    [
        $"        %s{c.Type} %s{c.Name}"
        match keys c with
        | "" -> ()
        | k -> $" %s{k}"
        match note c with
        | "" -> ()
        | n -> $" \"%s{n}\""
    ]
    |> String.concat ""


let entity (t: Table) (columns: Column list) =
    [
        $"    %s{t.Name} {{"
        yield! columns |> List.map columnLine
        "    }"
    ]


/// The declared foreign keys among the tables of a part: one to at most one when the key
/// column is the child's whole key or unique, else one to many.
let foreignKeys (tables: Table list) (drawn: Set<string>) =
    tables
    |> List.filter (fun t -> drawn.Contains t.Name)
    |> List.collect (fun t ->
        let wholeKey (c: Column) =
            c.Primary && (t.Columns |> List.filter _.Primary |> List.length) = 1

        t.Columns
        |> List.choose (fun c ->
            match c.References with
            | Some(parent, _) when drawn.Contains parent ->
                let cardinality = if wholeKey c || c.Unique then "||--o|" else "||--o{"
                Some $"    %s{parent} %s{cardinality} %s{t.Name} : \"FK %s{c.Name}\""
            | _ -> None
        )
    )


let diagram (tables: Table list) (part: Part) =
    let find name = tables |> List.find (fun t -> t.Name = name)
    let stubs = part.Stubs |> List.map fst

    let entities =
        tables
        |> List.filter (fun t -> part.Tables |> List.contains t.Name || stubs |> List.contains t.Name)
        |> List.collect (fun t ->
            match part.Stubs |> List.tryFind (fst >> (=) t.Name) with
            | Some(_, shown) ->
                entity t (shown |> List.map (fun n -> t.Columns |> List.find (fun c -> c.Name = n)))
            | None -> entity (find t.Name) t.Columns
        )

    let drawn = part.Tables @ stubs |> Set.ofList

    let joins =
        part.Joins
        |> List.map (fun j ->
            let on = j.On |> String.concat ", "
            $"    %s{j.From} %s{j.Cardinality} %s{j.To} : \"%s{on} (logical)\""
        )

    [
        $"### %s{part.Title}"
        ""
        "```mermaid"
        "erDiagram"
        yield! entities
        yield! foreignKeys tables drawn
        yield! joins
        "```"
    ]
    |> String.concat "\n"


/// The full block between the markers: a note and the diagrams, one per part.
let block (tables: Table list) =
    [
        startMarker
        "<!-- Generated by `dotnet fsi scripts/SchemaDiagram.fsx --write`; do not edit by hand. -->"
        ""
        yield! parts |> List.map (diagram tables) |> List.collect (fun d -> [ d; "" ])
        endMarker
    ]
    |> String.concat "\n"


// ---------------------------------------------------------------------------
// ARCHITECTURE.md
// ---------------------------------------------------------------------------

/// The text between the markers, inclusive, or an error naming what is missing.
let currentBlock (text: string) =
    match text.IndexOf(startMarker, StringComparison.Ordinal), text.IndexOf(endMarker, StringComparison.Ordinal) with
    | -1, _ -> Error $"%s{architecturePath} has no %s{startMarker} marker"
    | _, -1 -> Error $"%s{architecturePath} has no %s{endMarker} marker"
    | s, e when e < s -> Error $"%s{architecturePath} has %s{endMarker} before %s{startMarker}"
    | s, e -> Ok(text.Substring(s, e + endMarker.Length - s))


let normaliseNewlines (s: string) = s.Replace("\r\n", "\n")


let relative (full: string) =
    Path.GetRelativePath(repoRoot, full).Replace('\\', '/')


let run (args: string list) =
    let tables = readTables ()

    match problems tables with
    | _ :: _ as found ->
        found |> List.iter (eprintfn "%s")
        eprintfn "fix `parts` in scripts/SchemaDiagram.fsx"
        1
    | [] ->
        let generated = block tables

        match args with
        | [] ->
            printfn "%s" generated
            0
        | [ "--write" ] ->
            let text = File.ReadAllText architecturePath |> normaliseNewlines

            match currentBlock text with
            | Error msg ->
                eprintfn "%s" msg
                1
            | Ok current ->
                File.WriteAllText(architecturePath, text.Replace(current, generated))
                eprintfn "updated %s" (relative architecturePath)
                0
        | [ "--check" ] ->
            let text = File.ReadAllText architecturePath |> normaliseNewlines

            match currentBlock text with
            | Error msg ->
                eprintfn "%s" msg
                1
            | Ok current when current = generated ->
                eprintfn "%s is current" (relative architecturePath)
                0
            | Ok _ ->
                eprintfn "%s is out of date; run: dotnet fsi scripts/SchemaDiagram.fsx --write" (relative architecturePath)
                1
        | _ ->
            eprintfn "usage: dotnet fsi scripts/SchemaDiagram.fsx [--write | --check]"
            2


fsi.CommandLineArgs |> Array.toList |> List.tail |> run |> exit
