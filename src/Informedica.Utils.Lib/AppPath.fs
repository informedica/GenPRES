namespace Informedica.Utils.Lib


/// <summary>
/// Unified resolution of the application root directory and the well-known
/// <c>data/</c> sub-directories (cache, zindex, logs, interactions).
/// </summary>
/// <remarks>
/// A single resolver replaces the several independent path-finding mechanisms
/// that previously diverged across the dev server, a published binary, the
/// Docker container, and the test projects. The resolved root is the directory
/// that contains <c>data/</c>; every other path derives from it, so all runtime
/// contexts agree on one location.
///
/// Resolution priority:
/// <list type="number">
/// <item><description><c>GENPRES_ROOT</c> environment variable, when set and the directory exists.</description></item>
/// <item><description>The directory of the <c>.env</c> file, searched upward from the current directory, then from the assembly location.</description></item>
/// <item><description>The parent of a <c>data/zindex/</c> directory, searched upward from the assembly location, then from the current directory.</description></item>
/// <item><description>The current directory (a warning is emitted).</description></item>
/// </list>
///
/// Steps 2 and 3 read the filesystem directly and do not depend on
/// <c>Env.loadDotEnv()</c> having run, so test projects and FSI scripts resolve
/// the same root with no extra setup. <c>data/zindex/</c> is used as the marker
/// in step 3 because a bare <c>data/</c> directory is ambiguous (it exists both
/// at the repository root and under the server project), whereas only the
/// repository-root <c>data/</c> holds the zindex data.
/// </remarks>
[<RequireQualifiedAccess>]
module AppPath =

    open System
    open System.IO


    /// <summary>
    /// Name of the environment variable that, when set, forces the application
    /// root explicitly. It must point at the directory containing <c>data/</c>
    /// (for example <c>/app</c> in the Docker container).
    /// </summary>
    [<Literal>]
    let GENPRES_ROOT = "GENPRES_ROOT"


    /// <summary>
    /// Walk up the directory tree from <paramref name="startDir"/> (inclusive),
    /// returning the first directory for which <paramref name="found"/> holds,
    /// or <c>None</c> when the filesystem root is reached.
    /// </summary>
    /// <param name="found">Predicate tested against each directory on the way up.</param>
    /// <param name="startDir">The directory to start the upward search from.</param>
    /// <returns>The first matching directory, or <c>None</c> if none matches.</returns>
    let tryWalkUp found startDir =
        let rec search dir =
            if String.IsNullOrEmpty dir then
                None
            elif found dir then
                Some dir
            else
                match Directory.GetParent dir with
                | null -> None
                | p -> search p.FullName

        if String.IsNullOrEmpty startDir then
            None
        else
            search startDir


    /// <summary>True when <paramref name="dir"/> directly contains a <c>.env</c> file.</summary>
    /// <param name="dir">The directory to test.</param>
    /// <returns><c>true</c> if <c>dir/.env</c> exists.</returns>
    let hasEnv dir = File.Exists(Path.Combine(dir, ".env"))

    /// <summary>True when <paramref name="dir"/> contains a <c>data/zindex</c> sub-directory.</summary>
    /// <param name="dir">The directory to test.</param>
    /// <returns><c>true</c> if <c>dir/data/zindex</c> exists.</returns>
    let hasZindex dir =
        Directory.Exists(Path.Combine(dir, "data", "zindex"))


    /// <summary>
    /// Resolve the application root from the three sources given explicitly.
    /// Pure: takes the <c>GENPRES_ROOT</c> value, the current directory, and the
    /// assembly base directory, so it can be exercised deterministically in
    /// tests. Returns <c>None</c> when every strategy fails; the caller decides
    /// the final fallback.
    /// </summary>
    /// <param name="genpresRoot">The <c>GENPRES_ROOT</c> override value, if any.</param>
    /// <param name="currentDir">The current working directory to search from.</param>
    /// <param name="assemblyBaseDir">The assembly base directory to search from.</param>
    /// <returns>The resolved root directory, or <c>None</c> if no strategy succeeds.</returns>
    let tryGetRootInternal genpresRoot currentDir assemblyBaseDir =
        let fromEnv =
            match genpresRoot with
            | Some v when not (String.IsNullOrWhiteSpace v) && Directory.Exists v -> Some v
            | _ -> None

        fromEnv
        |> Option.orElseWith (fun () -> tryWalkUp hasEnv currentDir)
        |> Option.orElseWith (fun () -> tryWalkUp hasEnv assemblyBaseDir)
        |> Option.orElseWith (fun () -> tryWalkUp hasZindex assemblyBaseDir)
        |> Option.orElseWith (fun () -> tryWalkUp hasZindex currentDir)
        |> Option.map Path.GetFullPath


    /// <summary>
    /// Resolve the application root from the live environment, current
    /// directory, and assembly location. Falls back to the current directory
    /// (emitting a warning) when no strategy succeeds.
    /// </summary>
    let resolveRoot () =
        let genpresRoot =
            match Environment.GetEnvironmentVariable GENPRES_ROOT with
            | null
            | "" -> None
            | v -> Some v

        match tryGetRootInternal genpresRoot Environment.CurrentDirectory AppContext.BaseDirectory with
        | Some root -> root
        | None ->
            eprintfn "[AppPath] Warning: could not resolve application root; using CurrentDirectory"
            Environment.CurrentDirectory |> Path.GetFullPath


    /// <summary>
    /// Lazily resolved, memoized application root. Resolution is deferred so
    /// that any <c>Env.loadDotEnv()</c> setting <c>GENPRES_ROOT</c> runs first.
    /// </summary>
    let root = lazy (resolveRoot ())

    /// <summary>
    /// The resolved application root directory (the directory containing <c>data/</c>).
    /// </summary>
    let rootPath () = root.Value

    /// <summary>
    /// The <c>data/</c> directory under the application root.
    /// </summary>
    let dataDir () = Path.Combine(rootPath (), "data")

    /// <summary>
    /// The <c>data/cache/</c> directory.
    /// </summary>
    let cacheDir () = Path.Combine(dataDir (), "cache")

    /// <summary>
    /// The <c>data/zindex/</c> directory.
    /// </summary>
    let zindexDir () = Path.Combine(dataDir (), "zindex")

    /// <summary>
    /// The <c>data/logs/</c> directory.
    /// </summary>
    let logsDir () = Path.Combine(dataDir (), "logs")

    /// <summary>
    /// The <c>data/cache/interactions/</c> directory.
    /// </summary>
    let interactionsDir () =
        Path.Combine(cacheDir (), "interactions")
