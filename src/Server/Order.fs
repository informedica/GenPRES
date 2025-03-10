module Order


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Mappers


let calcValues (ord : Order) =
    if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
        let path = $"{__SOURCE_DIRECTORY__}/log.txt"
        OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

    let sns =
        ord.Orderable.Components
        |> Array.collect _.Items
        |> Array.filter (_.IsAdditional >> not)
        |> Array.map _.Name

    try
        ord
        |> mapFromOrder
        |> Api.orderCalcValues
        |> Result.map (Order.Dto.toDto >> mapToOrder sns)
        |> Result.map Calculated
    with
    | e ->
        ConsoleWriter.writeErrorMessage $"error calculating values from min incr max {e}" true true
        "error calculating values from min incr max"
        |> Error


let getIntake wghtInGram (ords: Order []) =
    let wghtInKg =
        wghtInGram
        |> Option.map BigRational.fromInt
        |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
        |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

    ords
    |> Array.map mapFromOrder
    |> Api.getIntake wghtInKg
    |> mapToIntake


let solveOrder (ord : Order) =
    if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
        let path = $"{__SOURCE_DIRECTORY__}/log.txt"
        OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

    let sns =
        ord.Orderable.Components
        |> Array.collect _.Items
        |> Array.filter (_.IsAdditional >> not)
        |> Array.map _.Name

    let dto = ord |> mapFromOrder

    dto
    |> Order.Dto.toString
    |> sprintf "solving order:\n%s"
    |> fun s -> ConsoleWriter.writeInfoMessage s true false

    try
        let ord =
            ord
            |> mapFromOrder
            |> Api.orderSolve

        let state =
            match ord with
            | Ok ord ->
                if ord |> Order.isSolved then Solved else Calculated
            | Error _ -> Constrained

        ord
        |> Result.map (Order.Dto.toDto >> mapToOrder sns >> state)
        |> Result.mapError (fun (_, errs) ->
            let s =
                errs
                |> List.map string
                |> String.concat "\n"
            ConsoleWriter.writeErrorMessage $"error solving order\n{s}" true false
            s
        )
    with
    | e ->
        failwith $"error solving order\n{e}"