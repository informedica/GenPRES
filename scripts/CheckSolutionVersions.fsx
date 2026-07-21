// Check Solution Versions
//
// Proves that every project shipped in GenPRES.sln reports the same
// version as the repo-root Directory.Build.props, by inspecting each
// built DLL's actual file-version metadata — not just re-reading the
// XML that's supposed to produce it.
//
// Prerequisite: run `dotnet build GenPRES.sln` first so the DLLs exist.
// Run with: dotnet fsi scripts/CheckSolutionVersions.fsx

open System
open System.IO
open System.Diagnostics
open System.Xml.Linq

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let slnPath = Path.Combine(repoRoot, "GenPRES.sln")
let propsPath = Path.Combine(repoRoot, "Directory.Build.props")
let configuration = "Debug"
let targetFramework = "net10.0"

// ---------------------------------------------------------------------------
// Expected version, read from the single source of truth
// ---------------------------------------------------------------------------

let expectedVersion =
    let doc = XDocument.Load(propsPath)
    let ns = doc.Root.Name.Namespace

    doc.Descendants(ns + "Version")
    |> Seq.tryHead
    |> Option.map _.Value
    |> Option.defaultWith (fun () -> failwith $"No <Version> found in %s{propsPath}")

// ---------------------------------------------------------------------------
// Projects actually shipped, per GenPRES.sln
// ---------------------------------------------------------------------------

let projectsInSln =
    let psi =
        ProcessStartInfo(
            "dotnet",
            $"sln \"%s{slnPath}\" list",
            RedirectStandardOutput = true,
            UseShellExecute = false
        )

    use proc = Process.Start psi
    let output = proc.StandardOutput.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failwith $"`dotnet sln %s{slnPath} list` failed with exit code %i{proc.ExitCode}"

    output.Split('\n')
    |> Array.map (fun l -> l.Trim().Replace('\\', '/'))
    |> Array.filter (fun l -> l.EndsWith ".fsproj")
    |> Array.distinct
    |> Array.sort
    |> List.ofArray

// ---------------------------------------------------------------------------
// Per-project Version check
// ---------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type VersionResult =
    | InSync of fileVersion: string * productVersion: string
    | Mismatch of fileVersion: string * productVersion: string
    | NotBuilt of expectedDll: string


let checkProject (relativeFsproj: string) =
    let fullFsproj = Path.Combine(repoRoot, relativeFsproj.Replace('/', Path.DirectorySeparatorChar))
    let dir = Path.GetDirectoryName(fullFsproj)
    let name = Path.GetFileNameWithoutExtension(fullFsproj)
    let dll = Path.Combine(dir, "bin", configuration, targetFramework, $"{name}.dll")

    if not (File.Exists dll) then
        VersionResult.NotBuilt dll
    else
        let vi = FileVersionInfo.GetVersionInfo(dll)

        let orEmpty =
            function
            | null -> ""
            | (v: string) -> v

        let fileVersion = vi.FileVersion |> orEmpty
        let productVersion = vi.ProductVersion |> orEmpty
        let productBase = productVersion.Split('+') |> Array.head

        if productBase = expectedVersion then
            VersionResult.InSync(fileVersion, productVersion)
        else
            VersionResult.Mismatch(fileVersion, productVersion)


// ---------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------

printfn $"Expected version (from %s{propsPath}): %s{expectedVersion}"
printfn $"Projects declared in GenPRES.sln: %i{projectsInSln.Length}{Environment.NewLine}"

let results = projectsInSln |> List.map (fun p -> p, checkProject p)

for proj, result in results do
    let projName = Path.GetFileName proj

    match result with
    | VersionResult.InSync(fv, pv) ->
        printfn $"MATCH: %-45s{projName} FileVersion=%-10s{fv} ProductVersion=%s{pv}"
    | VersionResult.Mismatch(fv, pv) ->
        printfn $"MISMATCH: %-45s{projName} FileVersion=%-10s{fv} ProductVersion=%s{pv} (expected %s{expectedVersion})"
    | VersionResult.NotBuilt dll -> 
        printfn $"NOT BUILT: %-45s{projName} (expected at %s{dll})"

let inSync, mismatched, notBuilt =
    results
    |> List.fold
        (fun (inSync, mismatched, notBuilt) (_, r) ->
            match r with
            | VersionResult.InSync _ -> inSync + 1, mismatched, notBuilt
            | VersionResult.Mismatch _ -> inSync, mismatched + 1, notBuilt
            | VersionResult.NotBuilt _ -> inSync, mismatched, notBuilt + 1
        )
        (0, 0, 0)

printfn $"{Environment.NewLine}Summary: %i{inSync} in sync, %i{mismatched} mismatched, %i{notBuilt} not built (of %i{results.Length})"

if mismatched > 0 || notBuilt > 0 then
    printfn $"{Environment.NewLine}Version check FAILED."
    exit 1
else
    printfn $"{Environment.NewLine}Version check PASSED - every shipped project's DLL matches Directory.Build.props."
