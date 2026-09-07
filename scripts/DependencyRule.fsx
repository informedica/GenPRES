// Dependency rule model for ADR-0001 (docs/adr/0001-system-architecture.md).
//
// The ring map below is the authoritative ring assignment of every project in
// GenPRES.sln, and `allowedReferences` is the inventory of today's outward project
// references. Loaded by scripts/CheckDependencyRule.fsx (the fitness test, run in CI)
// and scripts/ProjectGraph.fsx (the dependency diagram in ARCHITECTURE.md) so both
// read the same map. No packages; plain file and regex reading only.

module DependencyRule

open System.IO
open System.Text.RegularExpressions


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
