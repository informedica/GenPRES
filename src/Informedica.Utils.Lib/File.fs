namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module File =

    open System.IO


    let enumerate path =
        seq { for file in DirectoryInfo(path).EnumerateFiles() -> file }


    let readAllLines path = File.ReadAllLines(path)


    let readAllLinesAsync path =
        async {
            return File.ReadAllLines(path) |> Array.toList
        }


    let writeTextToFile path text =
        File.WriteAllText(path, text)


    let appendTextToFile path text =
        File.AppendAllText(path, text)


    let exists path =
        File.Exists(path)
