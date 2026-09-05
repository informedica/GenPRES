namespace Informedica.GenUnits.Lib

module Api =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    /// <summary>
    /// Evaluates a string to a ValueUnit
    /// </summary>
    /// <param name="s">The string to convert</param>
    /// <example>
    /// <code>
    /// "1.2 mg" |> Api.eval
    /// // returns ValueUnit ([|6/5N|], Mass (MilliGram 1N))
    ///
    /// "20 mg/50 ml" |> Api.eval
    /// // returns ValueUnit ([|2/5N|], Concentration (Mass (MilliGram 1N), Volume (MilliLiter 1N)))
    /// </code>
    /// </example>
    let eval s =
        let addSpace s = " " + s + " "
        let mults = "*" |> addSpace
        let divs = "/" |> addSpace
        let adds = "+" |> addSpace
        let subtrs = "-" |> addSpace

        let del = "#"
        let addDel s = del + s + del

        let fromStr s =
            match s |> ValueUnit.fromString with
            | Ok vu -> Some vu
            | Error _ -> None

        let opts s : ValueUnit -> ValueUnit -> ValueUnit =
            let s = s |> String.trim

            match s with
            | _ when s = "*" -> (fun a b -> a * b)
            | _ when s = "/" -> (fun a b -> a / b)
            | _ when s = "+" -> (fun a b -> a + b)
            | _ when s = "-" -> (fun a b -> a - b)
            | _ -> raise (System.FormatException $"Cannot evaluate string %s{s}")

        let rec eval' acc terms =
            if acc |> Option.isNone then
                eval' (terms |> List.head |> fromStr) (terms |> List.tail)
            else
                match terms with
                | [] -> acc |> Option.get
                | os :: vus :: rest ->
                    let op = os |> opts

                    let vu =
                        match vus |> fromStr with
                        | Some vu -> ((acc |> Option.get) |> op <| vu) |> Some
                        | None -> None

                    rest |> eval' vu

                | _ -> raise (System.FormatException $"""Cannot evaluate string %s{terms |> String.concat ","}""")

        s
        |> String.replace mults (mults |> addDel)
        |> String.replace divs (divs |> addDel)
        |> String.replace adds (adds |> addDel)
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
                | Error _ -> None
                | Ok vu -> vu |> Some

        match vu, s2 |> UnitsParse.fromString with
        | Some vu, Some u ->
            vu
            |> ValueUnit.convertTo u
            |> ValueUnit.toString true BigRational.toString loc verb
        | _ -> s1
