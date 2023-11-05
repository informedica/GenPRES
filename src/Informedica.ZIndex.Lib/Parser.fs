namespace Informedica.ZIndex.Lib


module Parser =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib


    /// <summary>
    /// Split a string into an array of strings, based on the given positions.
    /// </summary>
    /// <param name="pl">The given positions.</param>
    /// <param name="s">The string</param>
    let splitRecord pl (s: string) =
        pl
        |> List.mapi (fun i p ->
                let start = pl |> List.take i |> List.sum
                s.Substring(start, p))
        |> List.toArray


    /// <summary>
    /// Get the data from a file.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="posl">The positions of the fields.</param>
    /// <param name="pick">The fields to pick.</param>
    let getData name posl pick =
        let data =
            FilePath.GStandPath + "/" + name
            |> File.readAllLines
            |> Array.filter (String.length >> ((<) 10))
            |> Array.map (splitRecord posl)

        if pick |> Seq.isEmpty then data
        else
            data
            |> Array.map (Array.pickArray pick)


    /// <summary>
    /// Check wether a string is a decimal format.
    /// </summary>
    let isDecimalFormat s =
        let s = s |> String.replace "(" "" |> String.replace ")" ""
        if s |> String.contains "," then
            match s |> String.splitAt ',' with
            | [|_;d|] -> d |> Int32.parse > 0
            | _ -> false
        else false


    /// <summary>
    /// Parse a string to a decimal a return as a string
    /// </summary>
    /// <param name="st">String that is 'N'if numerical</param>
    /// <param name="sf">The format of the numerical string</param>
    /// <param name="s">The string to format</param>
    let parseValue st sf (s: string) =
        if st = "N" then
            let vf = sf |> String.replace "(" "" |> String.replace ")" ""
            match vf |> String.splitAt ','  with
            | [|n;d|] ->
                let n = n |> Int32.parse
                let d = d |> Int32.parse
                if d = 0 then s
                else
                    (s |> String.subString 0 n) + "." + (s |> String.subString n d)
            | _ ->
                match vf |> String.splitAt '+' with
                | [|n;d|] ->
                    let n = n |> Int32.parse
                    let d = d |> Int32.parse
                    s |> String.subString d n
                | _ -> s

        else s