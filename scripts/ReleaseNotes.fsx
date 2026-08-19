// Release facts for a shipped version, read out of CHANGELOG.md.
//
// Emits the version, pre-release flag, and changelog section needed to publish a
// GitHub Release. Shared by CI and local dry runs.
//
// Run with:
//   dotnet fsi scripts/ReleaseNotes.fsx                     # current version, notes to stdout
//   dotnet fsi scripts/ReleaseNotes.fsx 0.1.2-alpha.2       # a specific shipped version
//   dotnet fsi scripts/ReleaseNotes.fsx --out notes.md      # notes to a file, relative to the cwd
//   dotnet fsi scripts/ReleaseNotes.fsx --github-output     # append facts to $GITHUB_OUTPUT

#load "Versioning.fsx"
#load "Changelog.fsx"

open System
open System.IO


let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let propsPath = Path.Combine(repoRoot, "Directory.Build.props")
let changelogPath = Path.Combine(repoRoot, "CHANGELOG.md")


// ---------------------------------------------------------------------------
// Arguments
// ---------------------------------------------------------------------------

type Options =
    {
        Version: string option
        OutFile: string option
        GitHubOutput: bool
    }


/// Parse args once and fail fast: unknown flags, duplicates, or extra values are errors.
/// This keeps release publishing strict and avoids writing unintended files.
let parseArgs args =
    let isFlag (arg: string) = arg.StartsWith("--", StringComparison.Ordinal)

    let rec loop options remaining =
        match remaining with
        | [] -> options
        | "--github-output" :: tail ->
            if options.GitHubOutput then
                failwith "--github-output given more than once"

            loop { options with GitHubOutput = true } tail
        | "--out" :: value :: tail when not (isFlag value) ->
            if options.OutFile |> Option.isSome then
                failwith "--out given more than once"

            loop { options with OutFile = Some value } tail
        | "--out" :: _ -> failwith "--out requires a value"
        | arg :: _ when isFlag arg -> failwith $"Unknown option: %s{arg}"
        | value :: tail ->
            if options.Version |> Option.isSome then
                failwith $"Unexpected extra argument: %s{value}"

            loop { options with Version = Some value } tail

    args
    |> List.ofArray
    |> loop
        {
            Version = None
            OutFile = None
            GitHubOutput = false
        }


// ---------------------------------------------------------------------------
// Resolve everything before writing anything
// ---------------------------------------------------------------------------

let options = fsi.CommandLineArgs |> Array.skip 1 |> parseArgs

let githubOutputPath =
    if not options.GitHubOutput then
        None
    else
        match Environment.GetEnvironmentVariable "GITHUB_OUTPUT" with
        | path when String.IsNullOrWhiteSpace path -> failwith "--github-output given but GITHUB_OUTPUT is not set"
        | path -> Some path

let version =
    options.Version
    |> Option.defaultWith (fun () -> Versioning.readVersion propsPath)

let tag = $"v%s{version}"
let isPreRelease = Versioning.isPreRelease version

let notes =
    match File.ReadAllLines changelogPath |> Changelog.sectionFor version with
    | Ok notes -> notes
    | Error error -> error |> Changelog.describeError version |> failwith

let facts =
    [
        $"version=%s{version}"
        $"tag=%s{tag}"
        $"prerelease=%b{isPreRelease}"
    ]


// ---------------------------------------------------------------------------
// Emit
// ---------------------------------------------------------------------------

// Facts go to stderr so stdout stays pure Release body when --out is omitted.
for fact in facts do
    eprintfn $"%s{fact}"

match options.OutFile with
| Some path ->
    File.WriteAllText(path, notes + "\n")
    eprintfn $"notes written to %s{path}"
| None -> printfn $"%s{notes}"

githubOutputPath
|> Option.iter (fun path -> File.AppendAllLines(path, facts))
