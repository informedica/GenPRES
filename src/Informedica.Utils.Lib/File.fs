namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module File =

    open System.IO


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
