namespace Informedica.GenUnits.Lib


module Api =

    open FParsec
    open Informedica.Utils.Lib.BCL


    /// <summary>
    /// Converts a string to a ValueUnit
    /// </summary>
    /// <param name="s">The string to convert</param>
    let eval s =
        let addSpace s = " " + s + " "
        let mults  = "*" |> addSpace
        let divs   = "/" |> addSpace
        let adds   = "+" |> addSpace
        let subtrs = "-" |> addSpace

        let del = "#"
        let addDel s = del + s + del

        let fromStr s =
            match s |> ValueUnit.fromString with
            | Success (vu, _, _) -> Some vu
            | Failure (err, _, _)  -> None

        let opts s =
            let s = s |> String.trim
            match s with
            | _ when s = "*" -> (*)
            | _ when s = "/" -> (/)
            | _ when s = "+" -> (+)
            | _ when s = "-" -> (-)
            | _ -> failwith <| $"Cannot evaluate string %s{s}"

        let rec eval' acc terms =
            if acc |> Option.isNone then
                eval' (terms |> List.head |> fromStr) (terms |> List.tail)
            else
                match terms with
                | [] -> acc |> Option.get
                | os::vus::rest ->
                    let op = os |> opts
                    let vu =
                        match vus |> fromStr with
                        | Some vu -> ((acc |> Option.get) |> op <| vu) |> Some
                        | None -> None

                    rest
                    |> eval' vu

                | _ -> failwith <| sprintf "Cannot evaluate string %s" (terms |> String.concat ",")

        s
        |> String.replace mults  (mults  |> addDel)
        |> String.replace divs   (divs   |> addDel)
        |> String.replace adds   (adds   |> addDel)
        |> String.replace subtrs (subtrs |> addDel)
        |> String.split del
        |> eval' None


    /// <summary>
    /// Converts a string representing a ValueUnit to another ValueUnit
    /// </summary>
    /// <param name="s1">The string to convert</param>
    /// <param name="s2">The string representing the unit to convert to</param>
    /// <param name="loc">The locale to use for the conversion</param>
    /// <param name="verb">The verb to use for the conversion</param>
    let convert loc verb s2 s1 =
        let vu =
            s1
            |> ValueUnit.fromString
            |> function
            | Failure (err, _, _) -> None
            | Success (vu, _, _)  -> vu |> Some

        match vu, s2 |> Units.fromString with
        | Some vu, Some u ->
            vu
            |> ValueUnit.convertTo u
            |> ValueUnit.toString BigRational.toString loc verb
        | _ -> s1
