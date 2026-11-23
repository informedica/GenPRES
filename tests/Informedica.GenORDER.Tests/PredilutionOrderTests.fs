/// The product of an order component carries the predilution it was made with. The Dtos carry it, the
/// preparation and the caption show it when one product applies, and the pick id tells two predilutions of
/// one GPK apart.
module PredilutionOrderTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

module GenForm = Informedica.GenForm.Lib.Types


module Fixture =

    let mL = Units.Volume.milliLiter
    let vol v = mL |> ValueUnit.singleWithValue v


    let water: ComponentReconstitution =
        {
            Solvent = "water"
            SolventVolume = vol 3N
            ExpansionVolume = None
        }


    /// 3 mL of product with 7 mL gluc 5%.
    let predilution =
        {
            MedVolume = vol 3N
            DiluentVolume = vol 7N
            Diluent = "gluc 5%"
        }


    /// 1 mL of product with 9 mL gluc 5%.
    let oneWithNine =
        { predilution with
            MedVolume = vol 1N
            DiluentVolume = vol 9N
        }


    /// An order product with the id, the reconstitution and the predilution.
    let product id recon pred : ComponentProduct =
        {
            Id = id
            FormQuantities = Some(vol 10N)
            Strengths =
                [
                    {
                        Substance = "nitroprusside"
                        Strength = 6N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> ValueUnit.per mL)
                    }
                ]
            Reconstitution = recon
            Predilution = pred
        }


    /// A GenFORM predilution of 3 mL with 7 mL gluc 5%.
    let formPredilution: GenForm.ProductPredilution =
        {
            Rule =
                {
                    GPK = "1"
                    Route = "iv"
                    Indication = None
                    Location = None
                    Department = None
                    MinAge = None
                    MaxAge = None
                    MinWeight = None
                    MaxWeight = None
                    MedVolume = vol 3N
                    DiluentVolume = vol 7N
                    Diluent = "gluc 5%"
                }
            DiluentSubstances = [||]
        }


    let products =
        [
            product "1" None (Some predilution)
            product "1" (Some water) (Some predilution)
            product "1" None None
        ]


open Fixture


[<Tests>]
let tests =
    testList
        "the predilution in the order"
        [
            testList
                "the predilution of a GenFORM product"
                [
                    test "a pre-diluted product gives its product volume, diluent volume and diluent" {
                        { SplitByDivisibleTests.Fixture.reconstituted "1" "water" with
                            Predilution = Some formPredilution
                        }
                        |> Medication.predilutionOf
                        |> Expect.equal "the predilution" (Some predilution)
                    }

                    test "a product that is not pre-diluted gives none" {
                        SplitByDivisibleTests.Fixture.reconstituted "1" "water"
                        |> Medication.predilutionOf
                        |> Expect.isNone "no predilution"
                    }
                ]

            testList
                "the medication Dto"
                [
                    for p in products do
                        test $"round trip of %A{p.Reconstitution}, %A{p.Predilution}" {
                            p
                            |> Medication.Dto.productToDto
                            |> Medication.Dto.productFromDto "product"
                            |> Expect.equal "the same product" (Ok p)
                        }
                ]

            test "a medication Dto predilution without a product volume is an error" {
                let dto = product "1" None (Some predilution) |> Medication.Dto.productToDto
                dto.Predilution.MedVolume <- null

                dto
                |> Medication.Dto.productFromDto "product"
                |> Result.isError
                |> Expect.isTrue "an error"
            }

            testList
                "the order Dto"
                [
                    for p in products do
                        test $"round trip of %A{p.Reconstitution}, %A{p.Predilution}" {
                            p
                            |> Order.Orderable.Component.Dto.productToDto
                            |> Order.Orderable.Component.Dto.productFromDto
                            |> Expect.equal "the same product" p
                        }
                ]

            testList
                "the pick id"
                [
                    test "a reconstituted and pre-diluted product has its id, the solvent and the predilution" {
                        Order.pickId "185663" (Some water) (Some predilution)
                        |> Expect.equal "the id" "185663 water 3 mL + 7 mL gluc 5%"
                    }

                    test "a pre-diluted liquid has its id and the predilution" {
                        Order.pickId "88749" None (Some oneWithNine)
                        |> Expect.equal "the id" "88749 1 mL + 9 mL gluc 5%"
                    }

                    test "two predilutions of one product have different ids" {
                        [ predilution; oneWithNine ]
                        |> List.map (Some >> Order.pickId "1" None)
                        |> List.distinct
                        |> List.length
                        |> Expect.equal "two ids" 2
                    }
                ]

            testList
                "the preparation row"
                [
                    test "one pre-diluted product puts 3 mL verdunnen met 7 mL gluc 5% in one cell" {
                        [ product "1" (Some water) (Some predilution) ]
                        |> Order.Print.printPredilution
                        |> Expect.equal
                            "the row"
                            [|
                                [| Valid "3 mL verdunnen met 7 mL gluc 5%"; Valid ""; Valid ""; Valid "" |]
                            |]
                    }

                    test "two products that apply give no row" {
                        [ product "1" None (Some predilution); product "2" None (Some oneWithNine) ]
                        |> Order.Print.printPredilution
                        |> Expect.isEmpty "no row"
                    }

                    test "a product that is not pre-diluted gives no row" {
                        [ product "1" (Some water) None ]
                        |> Order.Print.printPredilution
                        |> Expect.isEmpty "no row"
                    }
                ]

            testList
                "the caption text"
                [
                    test "one pre-diluted product gives 3 mL verdund met 7 mL gluc 5%" {
                        [ product "1" (Some water) (Some predilution) ]
                        |> Order.Print.predilutionText "verdund met"
                        |> Expect.equal "the text" (Some "3 mL verdund met 7 mL gluc 5%")
                    }

                    test "a product that is not pre-diluted gives no text" {
                        [ product "1" (Some water) None ]
                        |> Order.Print.predilutionText "verdund met"
                        |> Expect.isNone "no text"
                    }
                ]
        ]
