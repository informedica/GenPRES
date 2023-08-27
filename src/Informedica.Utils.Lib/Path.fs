namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Path =

    open System.IO


    /// Normalize backslashes to forward slashes
    /// Example: "./temp/test.txt" -> ".\temp\test.txt" (on Windows)
    let normalize (path: string) =
        let sep = Path.DirectorySeparatorChar.ToString()
        path.Replace(@"\", sep).Replace(@"/", sep)


    /// Combines two paths p1 and p2 into a single path.
    /// Example: "c:\temp" |> combineWith "test.txt" -> "c:\temp\test.txt"
    let combineWith p2 p1 =
        Path.Combine(p1, p2)



    module Tests =

        open Swensen.Unquote


        // Test normalize
        let testNormalize() =
            let sep = Path.DirectorySeparatorChar.ToString()
            test <@ @".\temp" |> normalize = $".{sep}temp" @>
            test <@ @".\temp\test.txt" |> normalize = $".{sep}temp{sep}test.txt" @>
            test <@ @".\temp/test.txt" |> normalize = $".{sep}temp{sep}test.txt" @>


        // Test combineWith
        let testCombineWith() =
            test <@ @"c:\temp" |> combineWith "test.txt" |> normalize = normalize @"c:\temp\test.txt" @>


        // Test all
        let testAll() =
            testNormalize()
            testCombineWith()