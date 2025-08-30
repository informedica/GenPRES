namespace Informedica.Utils.Lib



[<RequireQualifiedAccess>]
module File =

    open System.IO
    open System.Linq


    /// Recursively tries to find the parent of a file starting from a directory
    /// Returns Some <directory> that contains the file, or None if not found (safe, no throw)
    let rec findParent (directory: string) (fileToFind: string) : string option =
        let tryGetStartDir (p: string) =
            try
                if System.String.IsNullOrWhiteSpace p then None
                elif Directory.Exists p then Some p
                else
                    let parent = Directory.GetParent p
                    if isNull parent then None else Some parent.FullName
            with _ -> None

        let rec loop (path: string) =
            try
                let files = Directory.GetFiles(path)
                if files.Any(fun file -> Path.GetFileName(file).Equals(fileToFind, System.StringComparison.OrdinalIgnoreCase)) then
                    Some path
                else
                    let parent = Directory.GetParent(path)
                    if isNull parent then None else loop parent.FullName
            with _ -> None

        match tryGetStartDir directory with
        | Some start -> loop start
        | None -> None


    /// Returns a sequence of all files in the given directory
    let enumerate path =
        seq { for file in DirectoryInfo(path).EnumerateFiles() -> file }


    /// Reads all lines from the given file
    let readAllLines path = File.ReadAllLines(path)


    /// Reads all lines from the given file asynchronously
    let readAllLinesAsync path =
        async {
            return File.ReadAllLines(path) |> Array.toList
        }


    /// Writes the given text to the given file
    let writeTextToFile path (text : string) =
        File.WriteAllText(path, text)


    /// Appends the given text to the given file
    let appendTextToFile path (text : string) =
        File.AppendAllText(path, text)


    /// Returns true if the given file exists
    let exists path =
        File.Exists(path)
