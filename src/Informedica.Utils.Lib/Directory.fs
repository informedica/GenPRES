namespace Informedica.Utils.Lib


/// <summary>
/// Directory traversal helpers. The one primitive here is
/// <c>tryFindUpward</c>: walk up the directory tree until a predicate holds.
/// </summary>
/// <remarks>
/// This module shadows <c>System.IO.Directory</c> for a file that opens both, the
/// situation <c>File</c> is already in. The BCL calls below are spelled in full for
/// that reason.
/// </remarks>
[<RequireQualifiedAccess>]
module Directory =

    open System
    open System.IO


    /// <summary>
    /// Walk up the directory tree from <paramref name="startDir"/> (inclusive),
    /// returning the first directory for which <paramref name="found"/> holds,
    /// or <c>None</c> when the filesystem root is reached.
    /// </summary>
    /// <param name="found">Predicate tested against each directory on the way up.</param>
    /// <param name="startDir">The directory to start the upward search from.</param>
    /// <returns>The first matching directory, or <c>None</c> if none matches.</returns>
    let tryFindUpward (found: string -> bool) (startDir: string) : string option =
        let rec search dir =
            if String.IsNullOrWhiteSpace dir then
                None
            elif found dir then
                Some dir
            else
                match System.IO.Directory.GetParent dir with
                | null -> None
                | p -> search p.FullName

        if String.IsNullOrWhiteSpace startDir then
            None
        else
            search startDir


    /// <summary>
    /// The first directory at or above <paramref name="startDir"/> that directly
    /// contains a file named <paramref name="fileName"/>, probed by exact path.
    /// </summary>
    let tryFindDirWithFile fileName startDir =
        startDir |> tryFindUpward (fun dir -> File.Exists(Path.Combine(dir, fileName)))


    /// <summary>
    /// The full path of the first file named <paramref name="fileName"/> at or above
    /// <paramref name="startDir"/>, or <c>None</c> when no ancestor holds one.
    /// </summary>
    let tryFindFileUpward fileName startDir =
        startDir
        |> tryFindDirWithFile fileName
        |> Option.map (fun dir -> Path.Combine(dir, fileName))


    /// <summary>
    /// The first directory at or above <paramref name="startDir"/> that contains a file
    /// whose name matches <paramref name="fileToFind"/> ignoring case. Accepts a file as
    /// <paramref name="startDir"/> and starts from its directory. Never throws.
    /// </summary>
    /// <remarks>
    /// The guard is per directory: one that cannot be enumerated (permissions) counts as
    /// "no match" and the walk continues upward.
    /// </remarks>
    let tryFindParent (fileToFind: string) (startDir: string) =
        let startFrom path =
            try
                if String.IsNullOrWhiteSpace path then
                    None
                elif System.IO.Directory.Exists path then
                    Some path
                else
                    match System.IO.Directory.GetParent path with
                    | null -> None
                    | p -> Some p.FullName
            with _ ->
                None

        let found dir =
            try
                System.IO.Directory.GetFiles dir
                |> Array.exists (fun file ->
                    Path.GetFileName(file).Equals(fileToFind, StringComparison.OrdinalIgnoreCase)
                )
            with _ ->
                false

        startFrom startDir |> Option.bind (tryFindUpward found)
