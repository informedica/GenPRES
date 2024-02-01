
open System

Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

let logger = Informedica.GenOrder.Lib.OrderLogger.logger

let path = $"{__SOURCE_DIRECTORY__}/log.txt"
logger.Start (Some path) Informedica.GenOrder.Lib.OrderLogger.Level.Informative



Shared.ScenarioResult.empty
|> fun sc ->
    { sc with
        AgeInDays =  Some (365.)
        WeightInKg =  Some 10.
        HeightInCm =  Some 80.
        Indication = Some "Pijn, acuut/post-operatief"
        Medication = Some "paracetamol"
        Shape = Some "drank"
        Route = Some "oraal"
    }
|> serverApi.getScenarioResult
|> Async.RunSynchronously |> ignore
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
            printfn "== calc minincrmax"
            sc.Order
            |> Option.iter (fun dto ->
                dto
                |> ScenarioResult.mapFromOrder
                |> Informedica.GenOrder.Lib.Order.Dto.fromDto
                |> Informedica.GenOrder.Lib.Order.toString
                |> List.iter (printfn "%s")
            )
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
                printfn "== calc specific quantity"
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


#time

module Api = Informedica.GenOrder.Lib.Api
module Patient = Informedica.GenOrder.Lib.Patient

// load demo or product cache
Environment.SetEnvironmentVariable(Informedica.ZIndex.Lib.FilePath.GENPRES_PROD, "1")
Environment.SetEnvironmentVariable(Informedica.GenOrder.Lib.Utils.Constants.GENPRES_URL_ID, "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks")




