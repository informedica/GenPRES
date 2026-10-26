// Project dependency diagram, generated from the .fsproj files and the ring map.
//
// Emits a Mermaid graph of every project under `src/` in GenPRES.sln, grouped by the
// ADR-0001 ring assigned in scripts/DependencyRule.fsx. An edge points at the
// dependency. An inward edge is bright green; an outward reference, tolerated by
// `allowedReferences` but not meant to exist, is dashed and bright red.
// The diagram lives in ARCHITECTURE.md between the `project-graph` markers; CI runs
// `--check` so the picture cannot drift from the code.
//
// Run with:
//   dotnet fsi scripts/ProjectGraph.fsx            # mermaid to stdout
//   dotnet fsi scripts/ProjectGraph.fsx --write    # rewrite the block in ARCHITECTURE.md
//   dotnet fsi scripts/ProjectGraph.fsx --check    # exit 1 when that block is out of date

#load "DependencyRule.fsx"

open System
open System.IO
open DependencyRule


let architecturePath = Path.Combine(repoRoot, "ARCHITECTURE.md")
let startMarker = "<!-- project-graph:start -->"
let endMarker = "<!-- project-graph:end -->"


// ---------------------------------------------------------------------------
// Mermaid
// ---------------------------------------------------------------------------

/// Onion order, innermost first; the order the subgraphs are emitted in.
let ringOrder =
    [
        Ring.Core
        Ring.Contract
        Ring.Infrastructure
        Ring.Presentation
        Ring.Client
        Ring.Tooling
    ]


/// Mild fills that read on GitHub in both light and dark theme.
let ringStyle ring =
    match ring with
    | Ring.Core -> "fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a"
    | Ring.Contract -> "fill:#ede9fe,stroke:#6d28d9,color:#4c1d95"
    | Ring.Infrastructure -> "fill:#dcfce7,stroke:#15803d,color:#14532d"
    | Ring.Presentation -> "fill:#ffedd5,stroke:#c2410c,color:#7c2d12"
    | Ring.Client -> "fill:#fee2e2,stroke:#b91c1c,color:#7f1d1d"
    | Ring.Tooling -> "fill:#f3f4f6,stroke:#4b5563,color:#1f2937"


/// The colour of an inward reference, the default of every edge.
let inwardStyle = "stroke:#22c55e,stroke-width:2px"


/// The colour of an outward reference the rule still tolerates.
let outwardStyle = "stroke:#ef4444,stroke-width:2px"


/// `Informedica.GenORDER.Lib` -> `GenORDER.Lib`, the node label.
let shortName (name: string) =
    let prefix = "Informedica."

    if name.StartsWith prefix then
        name.Substring prefix.Length
    else
        name


/// `Informedica.GenORDER.Lib` -> `GenORDER_Lib`; Mermaid node ids take no dots.
let nodeId (name: string) = (shortName name).Replace('.', '_')


let ringName ring =
    match ring with
    | Ring.Core -> "Core"
    | Ring.Contract -> "Contract"
    | Ring.Infrastructure -> "Infrastructure"
    | Ring.Presentation -> "Presentation"
    | Ring.Client -> "Client"
    | Ring.Tooling -> "Tooling"


/// The mermaid source, deterministic: projects are ordered as in the ring map, edges
/// follow the project order and then the .fsproj order.
let mermaid (projects: Project list) =
    let allowed =
        allowedReferences |> List.map (fun (f, t, _) -> f, t) |> Set.ofList

    let ordered =
        projects
        |> List.sortBy (fun p ->
            let ringIndex =
                p.Ring
                |> Option.map (fun r -> ringOrder |> List.findIndex ((=) r))
                |> Option.defaultValue ringOrder.Length

            ringIndex, p.Name
        )

    let subgraphs =
        ringOrder
        |> List.collect (fun ring ->
            let members = ordered |> List.filter (fun p -> p.Ring = Some ring)

            if members.IsEmpty then
                []
            else
                [
                    $"    subgraph %s{ringName ring}"
                    yield! members |> List.map (fun p -> $"        %s{nodeId p.Name}[\"%s{shortName p.Name}\"]")
                    "    end"
                ]
        )

    // each edge with whether it points outward; Mermaid numbers the edges in this order
    let edges =
        ordered
        |> List.collect (fun p ->
            p.References
            |> List.map (fun dep ->
                let outward = allowed.Contains(p.Name, dep)
                let arrow = if outward then "-.->" else "-->"
                $"    %s{nodeId p.Name} %s{arrow} %s{nodeId dep}", outward
            )
        )

    let linkStyles =
        let outward =
            edges
            |> List.indexed
            |> List.choose (fun (i, (_, out)) -> if out then Some(string i) else None)

        let indices = outward |> String.concat ","

        [
            $"    linkStyle default %s{inwardStyle}"
            if not outward.IsEmpty then
                $"    linkStyle %s{indices} %s{outwardStyle}"
        ]

    let styles =
        ringOrder
        |> List.collect (fun ring ->
            let members = ordered |> List.filter (fun p -> p.Ring = Some ring)

            if members.IsEmpty then
                []
            else
                let names = members |> List.map (fun p -> nodeId p.Name) |> String.concat ","
                [ $"    classDef %s{ringName ring} %s{ringStyle ring}"; $"    class %s{names} %s{ringName ring}" ]
        )

    [ "graph BT"; yield! subgraphs; yield! (edges |> List.map fst); yield! styles; yield! linkStyles ]
    |> String.concat "\n"


/// The full block between the markers: a note, the diagram, a legend.
let block (projects: Project list) =
    let edgeCount = projects |> List.sumBy (fun p -> p.References.Length)

    [
        startMarker
        "<!-- Generated by `dotnet fsi scripts/ProjectGraph.fsx --write`; do not edit by hand. -->"
        ""
        "```mermaid"
        mermaid projects
        "```"
        ""
        $"%i{projects.Length} projects, %i{edgeCount} project references. An arrow points at the dependency. A green arrow points"
        "inward, as the dependency rule wants. A dashed red arrow is an outward reference the rule still tolerates but that should"
        "not exist; the reasons are in `scripts/DependencyRule.fsx`."
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


let run (args: string list) =
    let projects = srcProjects ()
    let generated = block projects

    match args with
    | [] ->
        printfn "%s" (mermaid projects)
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
            eprintfn "%s is out of date; run: dotnet fsi scripts/ProjectGraph.fsx --write" (relative architecturePath)
            1
    | _ ->
        eprintfn "usage: dotnet fsi scripts/ProjectGraph.fsx [--write | --check]"
        2


fsi.CommandLineArgs |> Array.toList |> List.tail |> run |> exit
