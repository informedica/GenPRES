/// A medication with the placeholder product GenFORM makes when no product matches: its text is read
/// back, and its order is refused.
module PlaceholderProductTests

open Informedica.Utils.Lib.BCL
open Expecto
open Expecto.Flip

open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


/// paracetamol drank, with the placeholder product of its component.
module Fixture =

    let logger = OrderLogging.noOp


    /// A component limit of paracetamol drank whose only product is the placeholder.
    let placeholderLimit: Types.ComponentLimit =
        {
            Name = "paracetamol"
            ProductIds = [||]
            Limit = None
            Products = [| Product.create "paracetamol" "drank" "ORAAL" [| "paracetamol" |] |]
            SubstanceLimits = [||]
        }


    /// The component product createComponents makes of the placeholder.
    let placeholderProduct =
        [| placeholderLimit |]
        |> Medication.createComponents logger
        |> List.head
        |> _.Products
        |> List.head


    /// paracetamol drank with these products.
    let withProducts products =
        { Scenarios.pcmDrink with
            Components =
                Scenarios.pcmDrink.Components
                |> List.map (fun c -> { c with Products = products })
        }


    let roundTrip med = med |> MedicationText.toString |> MedicationText.fromString


    /// The medication's order, run to its bounds.
    let processed med =
        med
        |> Medication.toOrder Scenarios.testStart
        |> Result.mapError (fun e -> $"%A{e}")
        |> Result.bind (fun ord ->
            OrderProcessor.processPipeline logger (CalcMinMax ord)
            |> Result.mapError (fun (_, errs) -> $"%A{errs}")
        )


open Fixture


[<Tests>]
let tests =
    testList
        "a placeholder product"
        [
            test "createComponents gives the placeholder a product without an id" {
                placeholderProduct.Id |> Expect.equal "no id" ""
            }

            test "a medication with that product is read back from text" {
                let med = withProducts [ placeholderProduct ]

                med |> roundTrip |> Expect.equal "the same" (Ok med)
            }

            test "a product with an id is read back with its id" {
                withProducts [ { placeholderProduct with Id = "12345" } ]
                |> roundTrip
                |> Result.map (_.Components >> List.collect _.Products >> List.map _.Id)
                |> Expect.equal "the id" (Ok [ "12345" ])
            }

            test "the order of a medication with that product is refused" {
                withProducts [ placeholderProduct ]
                |> processed
                |> Result.isError
                |> Expect.isTrue "refused"
            }

            test "the same medication without products gives an order" {
                withProducts [] |> processed |> Result.isOk |> Expect.isTrue "an order"
            }

            testList
                "a value of the text"
                [
                    let read v u =
                        Medication.Dto.ValueDto(Value = v, Unit = u)
                        |> Medication.Dto.valueOptFromDto "x"

                    test "a missing value is no value" {
                        null |> Medication.Dto.valueOptFromDto "x" |> Expect.equal "none" (Ok None)
                    }

                    test "no numbers and no unit is the empty value" {
                        read [||] "" |> Expect.equal "empty" (Ok(Some ValueUnit.empty))
                    }

                    test "a null array with a unit is read as no numbers in that unit" {
                        read null "kg"
                        |> Expect.equal "kg without numbers" (Ok(Some(ValueUnit.create Units.Weight.kiloGram [||])))
                    }

                    test "a null array without a unit is the empty value" {
                        read null "" |> Expect.equal "empty" (Ok(Some ValueUnit.empty))
                    }

                    test "a text with a null array is read, not thrown" {
                        let text =
                            Scenarios.amoxClavText
                            |> String.replace "Adjust:\n  Value:\n    - \"14.5\"" "Adjust:\n  Value: null"

                        text
                        |> String.contains "Value: null"
                        |> Expect.isTrue "the text has the null array"

                        text
                        |> MedicationText.fromString
                        |> Result.map _.Adjust
                        |> Expect.equal "kg without numbers" (Ok(Some(ValueUnit.create Units.Weight.kiloGram [||])))
                    }

                    test "numbers without a unit are refused" {
                        read [| "1" |] "" |> Result.isError |> Expect.isTrue "refused"
                    }
                ]
        ]
