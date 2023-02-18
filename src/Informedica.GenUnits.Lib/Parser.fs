namespace Informedica.GenUnits.Lib


module Parser =

    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open FParsec


    let setUnitValue u v =
        u
        |> ValueUnit.apply (fun _ -> v)


    let ws =
        anyOf [' '; '\t'; '\n']
        |> many


    let pUnit (units : Units.UnitDetails list) =
        units
        |> List.collect (fun ud ->
            [
                ud.Abbreviation.Dut, fun n -> n |> setUnitValue ud.Unit
                ud.Abbreviation.Eng, fun n -> n |> setUnitValue ud.Unit
                ud.Name.Dut, fun n -> n |> setUnitValue ud.Unit
                ud.Name.Eng, fun n -> n |> setUnitValue ud.Unit
                yield! (ud.Synonyms |> List.map (fun s -> s, fun n -> n |> setUnitValue ud.Unit))
            ]
        )
        |> List.distinctBy fst
        |> List.sortByDescending (fun (s, _) -> s, s |> String.length)
        |> List.map (fun (s, u) -> s |> String.toLower |> pstring >>% u)
        |> choice
        |> fun p ->
            opt pfloat
            .>> ws
            .>>. p
            |>> (fun (f, u) -> f |> Option.map (decimal >> BigRational.fromDecimal), u)


    let pCombiUnit units =  sepBy1 (pUnit units) (ws >>. (pchar '/') .>> ws)


    let pValueUnit units =
        (opt pfloat)
        |>> (Option.bind BigRational.fromFloat)
        .>> ws
        .>>. (pCombiUnit units)
        .>> ws


    let parseWitUnits units s =
        if s |> String.isNullOrWhiteSpace then None
        else
            s
            |> String.trim
            |> String.replace "," "."
            |> String.toLower
            |> run (pValueUnit units)
            |> function
            | Success (result, _, _) ->
                let v, us = result
                match v with
                | Some br ->
                    us
                    |> List.map (fun (vo, tu) ->
                        match vo with
                        | Some v -> v  |> tu
                        | None   -> 1N |> tu
                    )
                    |> List.rev
                    |> List.reduce ValueUnit.per
                    |> ValueUnit.create
                    |> fun f -> f  [|br|]
                    |> Some
                | None -> None
            | Failure (msg, _, _) ->
                printfn $"parsing failure: %s{msg}"
                None

    let parse = parseWitUnits Units.units
