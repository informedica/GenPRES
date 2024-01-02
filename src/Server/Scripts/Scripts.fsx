
#load "load.fsx"

open System

Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1S4hXyqksDMYD0veSvUjwWTRb8Imm8nSa_yeYauqSbh0")

let logger = Informedica.GenOrder.Lib.OrderLogger.logger

let path = $"{__SOURCE_DIRECTORY__}/log.txt"
logger.Start (Some path) Informedica.GenOrder.Lib.OrderLogger.Level.Informative



Shared.ScenarioResult.empty
|> fun sc ->
    { sc with
        AgeInDays =  Some (18. * 365.)
        WeightInKg =  Some 70.
        HeightInCm =  Some 180.
        Medication = Some "benzylpenicilline"
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
                dto.Orderable.OrderableQuantity.Variable.ValsOpt <-
                    dto.Orderable.OrderableQuantity.Variable.ValsOpt
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
                    dto.Orderable.Dose.Rate.Variable.ValsOpt <-
                        dto.Orderable.Dose.Rate.Variable.ValsOpt
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


{Shared.Parenteralia.empty with Generic = None }
|> Parenteralia.get

open Informedica.GenForm.Lib

SolutionRule.get ()
|> SolutionRule.filter
    Filter.filter


Shared.Formulary.empty
|> fun sc ->
    { sc with
        Generic = Some "benzylpenicilline"
        Route = Some "iv"
        Indication = Some "meningitis"
    }
|> serverApi.getFormulary
|> Async.RunSynchronously
