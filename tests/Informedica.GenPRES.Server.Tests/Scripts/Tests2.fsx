
#load "load.fsx"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#load "../../../scripts/Expecto.fsx"

open System
open Expecto
open Expecto.Flip

fsi.AddPrinter<DateTime> (fun dt -> dt.ToShortDateString())

let tests =

    testList "mapping" [
        test "there and back again, simple dto" {
            let dto =
                Informedica.GenOrder.Lib.Order.Dto.discontinuous "test" "test" "test" []

            let ord =
                dto
                |> Informedica.GenOrder.Lib.Order.Dto.fromDto

            dto
            |> ScenarioResult.mapToOrder
            |> ScenarioResult.mapFromOrder
            |> Informedica.GenOrder.Lib.Order.Dto.fromDto
            |> Expect.equal "should be equal" ord
        }

        test "there and back again, pcm dto" {
            let dto =
                Informedica.GenOrder.Lib.Order.Dto.discontinuous
                    "1" "paracetamol" "rect"
                    [ "paracetamol", "supp", ["paracetamol"]  ]

            let ord =
                dto
                |> Informedica.GenOrder.Lib.Order.Dto.fromDto

            dto
            |> ScenarioResult.mapToOrder
            |> ScenarioResult.mapFromOrder
            |> Informedica.GenOrder.Lib.Order.Dto.fromDto
            |> Expect.equal "should be equal" ord
        }

        test "there and back again, sulfa trimethoprim dto" {
            let dto =
                Informedica.GenOrder.Lib.Order.Dto.discontinuous
                    "1" "cotrimoxazol" "iv"
                    [ "cotrimoxazol", "iv fluid", ["sulfa"; "trimethoprim"]  ]

            let ord =
                dto
                |> Informedica.GenOrder.Lib.Order.Dto.fromDto

            dto
            |> ScenarioResult.mapToOrder
            |> ScenarioResult.mapFromOrder
            |> Informedica.GenOrder.Lib.Order.Dto.fromDto
            |> Expect.equal "should be equal" ord
        }

        test "there and back again, pcm from formulary dto" {
            let ord =
                {  Shared.ScenarioResult.empty with
                    Age = Some 365.
                    Weight = Some 10.
                    Medication = Some "paracetamol"
                    Route = Some "rect"
                }
                |> ScenarioResult.get
                |> Result.map (fun r ->
                    { r with
                        Indication = r.Indications |> Array.tryHead
                    }
                )
                |> function
                    | Ok r -> r.Scenarios |> Array.tryHead |> Option.bind (fun  h -> h.Order)
                    | Error _ -> "cannot proces result" |> failwith
                |> Option.get

            ord
            |> ScenarioResult.mapFromOrder
            |> ScenarioResult.mapToOrder
            |> Expect.equal "should be equal" ord
        }

    ]


tests
|> Expecto.run
