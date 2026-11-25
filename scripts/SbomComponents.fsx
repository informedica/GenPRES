// Scopes the input of the SBOM to the software that ships, and checks the SBOM against it.
//
// sbom-tool hands the component path (-bc) to component-detection, which reports every manifest it
// finds below that path. On the repository root that includes paket.lock, which holds every Paket
// group (Test, Build and Benchmark too), the obj folders of the test projects and the devDependencies
// of the client lockfile. This script stages only what the release bundle is built from:
//
//   - the project.assets.json of every project in the server's deps.json (the server's closure)
//   - the project.assets.json of the client projects, whose NuGet packages Fable compiles into the bundle
//   - the client package.json and package-lock.json, with the entries npm marks as dev removed
//
// Run with:
//   dotnet fsi scripts/SbomComponents.fsx stage <server deps.json> <staging dir>
//   dotnet fsi scripts/SbomComponents.fsx verify <server deps.json> <manifest.spdx.json>
//
// verify exits with 1 when a package in the server's deps.json is missing from the SBOM, when the SBOM lists
// an npm package that the client lockfile marks as dev only, or when the SBOM lists a NuGet package
// that occurs only in the Paket groups that do not ship. The parsing and the comparison are in
// SbomScope.fsx and are covered by SbomComponentsTests.fsx.

#load "SbomScope.fsx"

open System
open System.IO
open System.Text.Json
open SbomScope


let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let srcDir = Path.Combine(repoRoot, "src")
let clientDir = Path.Combine(srcDir, "Informedica.GenPRES.Client")
let clientProjects = [ "Informedica.GenPRES.Client"; "Informedica.GenPRES.Client.Core" ]
let shippingGroups = set [ "Main"; "Client" ]
let stagingMarker = ".sbom-staging"


/// Prepares an empty staging folder. An existing folder is replaced only when it is empty or when this script made
/// it, which the marker file shows, so a wrong argument cannot delete another folder.
let prepareStagingDir stagingDir =
    if Directory.Exists stagingDir then
        let isOurs = File.Exists(Path.Combine(stagingDir, stagingMarker))
        let isEmpty = Directory.EnumerateFileSystemEntries stagingDir |> Seq.isEmpty

        if not (isOurs || isEmpty) then
            invalidOp $"%s{stagingDir} exists and was not made by this script, so it is not deleted"

        Directory.Delete(stagingDir, true)

    Directory.CreateDirectory stagingDir |> ignore
    File.WriteAllText(Path.Combine(stagingDir, stagingMarker), "Made by scripts/SbomComponents.fsx.\n")


/// Copies the project.assets.json and the project file of a project to the staging folder.
let copyAssets stagingDir project =
    let source = Path.Combine(srcDir, project, "obj", "project.assets.json")

    if not (File.Exists source) then
        raise (FileNotFoundException($"%s{source} is missing; restore or publish the project first", source))

    let target = Path.Combine(stagingDir, project, "obj", "project.assets.json")
    Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
    File.Copy(source, target, true)

    // The NuGet detector looks for the project file next to the obj folder and warns when it is absent.
    let projectFile = $"%s{project}.fsproj"
    File.Copy(Path.Combine(srcDir, project, projectFile), Path.Combine(stagingDir, project, projectFile), true)


/// Stages the assets files of the server's projects and of the client projects, and the client npm manifests without
/// their dev entries.
let stage depsPath stagingDir =
    prepareStagingDir stagingDir

    let serverProjects =
        readJson depsPath
        |> parseDeps
        |> List.filter (fun library -> library.Kind = LibraryKind.Project)
        |> List.map _.Id.Name

    if List.isEmpty serverProjects then
        formatError $"no project entries in %s{depsPath}"

    serverProjects @ clientProjects |> List.iter (copyAssets stagingDir)

    let lockfile = readJson (Path.Combine(clientDir, "package-lock.json"))
    let removed = removeDevEntries lockfile
    let kept = (parseLockfile lockfile).Length

    let manifest = readJson (Path.Combine(clientDir, "package.json"))
    removeDevDependencies manifest

    let clientStage = Path.Combine(stagingDir, "Informedica.GenPRES.Client")
    Directory.CreateDirectory clientStage |> ignore
    File.WriteAllText(Path.Combine(clientStage, "package-lock.json"), lockfile.ToJsonString())
    File.WriteAllText(Path.Combine(clientStage, "package.json"), manifest.ToJsonString())

    printfn $"staged %i{serverProjects.Length} server projects, %i{clientProjects.Length} client projects"
    printfn $"client lockfile: removed %i{removed} dev entries, kept %i{kept}"


/// Checks the SBOM against the software that ships, prints what is wrong and returns the report.
let verify depsPath sbomPath =
    let spdxPackages = readJson sbomPath |> parseSpdx
    let libraries = readJson depsPath |> parseDeps
    let paketEntries = File.ReadAllLines(Path.Combine(repoRoot, "paket.lock")) |> parsePaketLock
    let npmEntries = readJson (Path.Combine(clientDir, "package-lock.json")) |> parseLockfile

    let report = checkScope shippingGroups libraries paketEntries npmEntries spdxPackages

    let printIds heading ids =
        printfn $"%s{heading}: %i{Set.count ids}"
        ids |> Set.iter (fun id -> printfn $"  %s{id.Name}@%s{id.Version}")

    let count ecosystem =
        spdxPackages |> List.filter (fun package -> package.Ecosystem = ecosystem) |> List.length

    printfn $"SBOM packages: %i{count Ecosystem.Nuget} nuget, %i{count Ecosystem.Npm} npm"
    printIds "deps.json packages missing from the SBOM" report.Missing
    printIds "NuGet packages only in non-shipping Paket groups" report.LeakedNuget
    printIds "npm packages marked dev only in the lockfile" report.LeakedNpm
    report


/// Runs a command and returns the exit code.
let run args =
    match args with
    | [ "stage"; deps; staging ] ->
        stage (Path.GetFullPath deps) (Path.GetFullPath staging)
        0
    | [ "verify"; deps; sbom ] ->
        if verify (Path.GetFullPath deps) (Path.GetFullPath sbom) |> ScopeReport.isClean then
            0
        else
            1
    | _ ->
        eprintfn "usage: SbomComponents.fsx (stage <deps.json> <staging dir> | verify <deps.json> <manifest.spdx.json>)"
        1


let exitCode =
    try
        fsi.CommandLineArgs |> Array.toList |> List.tail |> run
    with
    | :? FormatException
    | :? FileNotFoundException
    | :? InvalidOperationException
    | :? JsonException as error ->
        eprintfn $"error: %s{error.Message}"
        1


exit exitCode
