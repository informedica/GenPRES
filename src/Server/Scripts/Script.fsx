
#load "load.fsx"



let logger = Informedica.GenOrder.Lib.OrderLogger.logger

let path = $"{__SOURCE_DIRECTORY__}/log.txt"
logger.Start (Some path) Informedica.GenOrder.Lib.OrderLogger.Level.Informative




Shared.ScenarioResult.empty
|> fun sc ->
    { sc with
        Age = Some (18. * 365.)
        Weight = Some 70.
        Height = Some 180.
        Medication = Some "gentamicine"
        Route = Some "iv"
    }
|> serverApi.getScenarioResult
|> Async.RunSynchronously
|> Result.bind (fun sc ->
    { sc with Indication = sc.Indications[0] |> Some }
    |> serverApi.getScenarioResult
    |> Async.RunSynchronously
)
|> Result.bind (fun sc ->
    sc.Scenarios
    |> Array.tryHead
    |> Option.get
    |> fun sc ->
            sc.Order
            |> Option.get
            |> serverApi.calcMinIncrMax
            |> Async.RunSynchronously
            |> Result.bind (fun o ->
                let dto = o |> ScenarioResult.mapFromOrder
                dto.Orderable.OrderableQuantity.Variable.Vals <-
                    dto.Orderable.OrderableQuantity.Variable.Vals
                    |> Option.map (fun v ->
                        v.Value <- [| v.Value[0] |]
                        v
                    )

                dto
                |> Informedica.GenOrder.Lib.Order.Dto.fromDto
                |> Informedica.GenOrder.Lib.Order.solveOrder
                    false logger.Logger
                |> Result.map (fun o ->
                    let s =
                        o
                        |> Informedica.GenOrder.Lib.Order.toString
                        |> String.concat "\n"
                    printfn $"calculated quantity order\n{s}"
                    o
                )
                |> Result.bind (fun o ->
                    let dto = o |> Informedica.GenOrder.Lib.Order.Dto.toDto
                    dto.Orderable.Dose.Rate.Variable.Vals <-
                        dto.Orderable.Dose.Rate.Variable.Vals
                        |> Option.map (fun v ->
                            v.Value <- [| v.Value[ (v.Value |> Array.length) - 1 ] |]
                            v
                        )

                    dto
                    |> Informedica.GenOrder.Lib.Order.Dto.fromDto
                    |> Informedica.GenOrder.Lib.Order.solveOrder
                        false logger.Logger
                )
                |> Result.map (fun o ->
                    let s =
                        o
                        |> Informedica.GenOrder.Lib.Order.toString
                        |> String.concat "\n"
                    printfn $"calculated rate order\n{s}"
                    o
                )
                |> Result.map Informedica.GenOrder.Lib.Order.Dto.toDto
                |> Result.map ScenarioResult.mapToOrder
                |> Ok
            )

)
|> ignore

logger.Stop ()