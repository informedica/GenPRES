/// The product of an order component carries the reconstitution it was made with. The Dtos carry it, the
/// preparation shows it when one product applies, and the pick id tells two solvents of one GPK apart.
module ReconstitutionOrderTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module Fixture =

    let mL = Units.Volume.milliLiter
    let vol v = mL |> ValueUnit.singleWithValue v


    let reconstitution =
        {
            Solvent = "water"
            SolventVolume = vol 10N
            ExpansionVolume = None
        }


    /// An order product with the id and the reconstitution.
    let product id recon : ComponentProduct =
        {
            Id = id
            FormQuantities = Some(vol 1N)
            Strengths =
                [
                    {
                        Substance = "cefazoline"
                        Strength = 100N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> ValueUnit.per mL)
                    }
                ]
            Reconstitution = recon
            Predilution = None
        }


    let reconstitutions =
        [
            Some reconstitution
            Some { reconstitution with ExpansionVolume = Some(vol (7N / 10N)) }
            None
        ]


open Fixture


[<Tests>]
let tests =
    testList
        "the reconstitution in the order"
        [
            testList
                "the reconstitution of a GenFORM product"
                [
                    test "a reconstituted product gives its solvent and volumes" {
                        SplitByDivisibleTests.Fixture.reconstituted "1" "water"
                        |> Medication.reconstitutionOf
                        |> Expect.equal "the reconstitution" (Some reconstitution)
                    }

                    test "a product with two reconstitutions gives none" {
                        let p = SplitByDivisibleTests.Fixture.reconstituted "1" "water"

                        { p with Reconstitution = Array.append p.Reconstitution p.Reconstitution }
                        |> Medication.reconstitutionOf
                        |> Expect.isNone "no reconstitution"
                    }

                    test "a product that requires no reconstitution gives none" {
                        { SplitByDivisibleTests.Fixture.reconstituted "1" "water" with RequiresReconstitution = false }
                        |> Medication.reconstitutionOf
                        |> Expect.isNone "no reconstitution"
                    }
                ]

            testList
                "the medication Dto"
                [
                    for recon in reconstitutions do
                        test $"round trip of %A{recon}" {
                            product "1" recon
                            |> Medication.Dto.productToDto
                            |> Medication.Dto.productFromDto "product"
                            |> Expect.equal "the same product" (Ok(product "1" recon))
                        }
                ]

            test "a medication Dto reconstitution without a solvent volume is an error" {
                let dto = product "1" (Some reconstitution) |> Medication.Dto.productToDto
                dto.Reconstitution.SolventVolume <- null

                dto
                |> Medication.Dto.productFromDto "product"
                |> Result.isError
                |> Expect.isTrue "an error"
            }

            testList
                "the order Dto"
                [
                    for recon in reconstitutions do
                        test $"round trip of %A{recon}" {
                            product "1" recon
                            |> Order.Orderable.Component.Dto.productToDto
                            |> Order.Orderable.Component.Dto.productFromDto
                            |> Expect.equal "the same product" (product "1" recon)
                        }
                ]

            testList
                "the pick id"
                [
                    test "a product that is not reconstituted has its id" {
                        Order.pickId "1" None None |> Expect.equal "the id" "1"
                    }

                    test "a reconstituted product has its id and solvent" {
                        Order.pickId "1" (Some reconstitution) None
                        |> Expect.equal "the id and solvent" "1 water"
                    }

                ]

            testList
                "the preparation row"
                [
                    test
                        "one reconstituted product puts oplossen in with the solvent volume and the solvent in one cell" {
                        [ product "1" (Some reconstitution) ]
                        |> Order.Print.printReconstitution
                        |> Expect.equal
                            "the row"
                            [| [| Valid "oplossen in 10 mL water"; Valid ""; Valid ""; Valid "" |] |]
                    }

                    test "two products that apply give no row" {
                        [ product "1" (Some reconstitution); product "2" (Some reconstitution) ]
                        |> Order.Print.printReconstitution
                        |> Expect.isEmpty "no row"
                    }

                    test "a product that is not reconstituted gives no row" {
                        [ product "1" None ]
                        |> Order.Print.printReconstitution
                        |> Expect.isEmpty "no row"
                    }
                ]
        ]
