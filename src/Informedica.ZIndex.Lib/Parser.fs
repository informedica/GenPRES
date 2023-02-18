namespace Informedica.ZIndex.Lib


module Parser =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib

    let splitRecord pl (s: string) =
        pl
        |> List.mapi (fun i p ->
                let start = pl |> List.take i |> List.sum
                s.Substring(start, p))
        |> List.toArray

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

    let isDecimalFormat s =
        let s = s |> String.replace "(" "" |> String.replace ")" ""
        if s |> String.contains "," then
            match s |> String.splitAt ',' with
            | [|_;d|] -> d |> Int32.parse > 0
            | _ -> false
        else false

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