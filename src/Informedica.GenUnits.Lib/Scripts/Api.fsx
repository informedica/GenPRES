
//#I "C:\Development\Informedica\libs\GenUnits\src\Informedica.GenUnits.Lib\scripts"
//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL




module Parser =

    open FParsec

    open Units


    let setUnitValue u v =
        u
        |> ValueUnit.apply (fun _ -> v)


    let ws = spaces


    let str_ws s = pstring s >>. ws


    let pnumber = pfloat .>> ws


    let pBigRat =
        pnumber
        |>> (BigRational.fromFloat >> Option.defaultValue 0N)


    /// <summary>
    /// Parse a unit with an optional group
    /// </summary>
    let pUnitGroup (u : string) g =
        let pu = u |> pstringCI
        let pg = $"[%s{g}]" |> pstringCI
        (pu .>> ws .>> (opt pg))


    let pUnit =
        Units.UnitDetails.units
        |> List.collect (fun ud ->
            [
                {| unit = ud.Abbreviation.Eng; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Abbreviation.Dut; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.Eng; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.Dut; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.EngPlural; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.DutchPlural; grp = ud.Group; f = setUnitValue ud.Unit |}
                yield!
                    ud.Synonyms
                    |> List.map (fun s ->
                        {| unit = s; grp = ud.Group; f = setUnitValue ud.Unit |}
                    )
            ]
        )
        |> List.distinctBy (fun r -> r.unit, r.grp)
        |> List.filter (fun r ->
            (r.unit = "kg" && r.grp = Group.MassGroup ||
            r.unit = "kilogram" && r.grp = Group.MassGroup)
            |> not
        )
        |> List.sortByDescending (fun r -> r.unit)
        |> List.map (fun r ->
            printfn $"{r.unit}[{r.grp}]"
            let g = $"{r.grp |> ValueUnit.Group.toString}"

            attempt (
                opt pfloat
                .>> ws
                .>>. (pUnitGroup r.unit g >>% r.f)
                |>> (fun (f, u) ->
                    f
                    |> Option.map (decimal >> BigRational.fromDecimal)
                    |> Option.defaultValue 1N |> u
                )
            )
        )
        |> choice


    /// <summary>
    /// Parse a complex unit using FParsec's OperatorPrecedenceParser
    /// </summary>
    /// <returns>Parser of Unit, unit</returns>
    let parseUnit =

        let opp  = OperatorPrecedenceParser<Unit, unit, unit>()
        let expr = opp.ExpressionParser

        opp.TermParser <-
            pUnit <|> between (str_ws "(") (str_ws ")") expr

        let ( *! ) u1 u2 = (u1, OpTimes, u2) |> CombiUnit
        let ( /! ) u1 u2 = (u1, OpPer, u2) |> CombiUnit

        opp.AddOperator (InfixOperator("*", ws, 1, Associativity.Left, ( *! )))
        opp.AddOperator (InfixOperator("/", ws, 1, Associativity.Left, ( /! )))

        ws >>. expr .>> eof


    let parse s =
        let pBigRatList =
            sepBy pBigRat (ws >>. (pstring ";") .>> ws)

        let pValue =
            (between (pstring "[") (pstring "]") pBigRatList)
            <|> (many pBigRat)

        let p =
            pValue .>>. parseUnit
            |>> (fun (brs, u) ->
                brs
                |> List.toArray
                |> ValueUnit.create u
            )
        s |> run p



open FParsec


let test s =
    match s |> Parser.parse with
    | Success(result, _, _)   -> printfn "Success: %A" result
    | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg

let testParser s p =
    match s |> run p with
    | Success(result, _, _)   -> printfn "Success: %A" result
    | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg



"mL[volume]"
|> run (Parser.parseUnit)



"[1.4;  2] mg[Mass]/kg/2 dag[Time]"
|> test


"[1.4;  2] mg/kg[Weight]/day"
|> test

