/// The concentration limit of a component in a solution rule: the fraction of the whole orderable the
/// component may be, applied to the concentration of the component in the orderable.
module ComponentConcentrationTests

open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// The medications the limit is tested on.
module Fixture =

    let mL = Units.Volume.milliLiter


    /// An inclusive limit of a value in a unit.
    let inclusive u br = br |> ValueUnit.singleWithUnit u |> Limit.inclusive |> Some


    /// A fraction of the whole orderable, in mL of the component per mL of the orderable.
    let fraction = inclusive (mL |> ValueUnit.per mL)


    /// Kaliumchloride 1 mmol/mL with gluc 5%, 0.5 mmol/kg for 31 kg, without the concentration limit
    /// of the substance, so that only a limit on the component bounds the dilution.
    let kcl () =
        Scenarios.kaliumchlorideOnceTimedText
        |> Tests.TestOrder.parse
        |> Scenarios.mapSubstances "kaliumchloride" (fun sub -> { sub with Dilution = None })


    /// The medication with the dilution limit of the component with the name changed, starting from
    /// a limit without bounds when the component has no dilution limit.
    let withDilutionLimit name f med =
        med
        |> Scenarios.mapComponent
            name
            (fun pc ->
                { pc with
                    Dilution =
                        pc.Dilution
                        |> Option.defaultValue (SolutionRule.DilutionLimit.create (DilutionTarget.Component name))
                        |> f
                        |> Some
                }
            )


    /// The medication with a concentration limit on the component with the name.
    let withConcentration name conc med =
        med |> withDilutionLimit name (fun dl -> { dl with Concentration = conc })


    /// The medication with a quantity limit in mL on the component with the name.
    let withQuantity name lo hi med =
        med
        |> withDilutionLimit
            name
            (fun dl ->
                { dl with
                    Quantity =
                        {
                            Min = inclusive mL lo
                            Max = inclusive mL hi
                        }
                }
            )


    /// The electrolyte and glucose bag for 11 kg: NaCl 3%, KCl 7,4% and gluc 10% as the diluent, at
    /// most 75.5 mL/kg, which is 830.5 mL. As in the rule sheet the components have no dose limits:
    /// what the bag holds of a component is a dilution limit on the component. The substances have
    /// no dilution limit, so that only a limit on a component bounds the bag.
    let bag () =
        let tpn = Scenarios.tpn

        { tpn with
            Name = "electrolyt/glucose mengsel"
            Components =
                tpn.Components
                |> List.filter (fun pc -> pc.Name <> "Samenstelling C")
                |> List.map (fun pc ->
                    { pc with
                        Dose = None
                        Substances = pc.Substances |> List.map (fun sub -> { sub with Dilution = None })
                    }
                )
        }


    /// The order of the medication with its ranges calculated.
    let solved med =
        med |> Tests.TestOrder.toOrder |> Tests.TestOrder.solve CalcMinMax


/// What the tests read from a solved order.
module Read =

    open Informedica.GenSolver.Lib


    // A variable whose calculated range has a minimum and a maximum gets a value picked within the
    // range when the order is solved, so the tests read the calculated constraints, which hold
    // the range the limits leave.

    /// The lowest value the calculated constraints of the variable allow, in the unit of the variable.
    let calculatedMin (ovar: OrderVariable) =
        ovar.CalculatedConstraints.Min
        |> Option.map (Variable.ValueRange.Minimum.toValueUnit >> ValueUnit.getValue >> Array.head)


    /// The highest value the calculated constraints of the variable allow, in the unit of the variable.
    let calculatedMax (ovar: OrderVariable) =
        ovar.CalculatedConstraints.Max
        |> Option.map (Variable.ValueRange.Maximum.toValueUnit >> ValueUnit.getValue >> Array.head)


    /// Whether the calculated constraints of the variable have a maximum that is itself not allowed.
    let calculatedMaxIsExclusive (ovar: OrderVariable) =
        ovar.CalculatedConstraints.Max
        |> Option.exists Variable.ValueRange.Maximum.isExcl


    /// The quantity of the orderable, in mL.
    let orderableQuantity (ord: Order) =
        ord.Orderable.OrderableQuantity |> OrderVariable.Quantity.toOrdVar


    /// The concentration of the first component in the orderable, the kaliumchloride.
    let componentConcentration (ord: Order) =
        ord.Orderable.Components[0].OrderableConcentration
        |> OrderVariable.Concentration.toOrdVar


open Fixture


// The dose is 0.5 mmol/kg for 31 kg as a norm dose, so 14 to 17 mmol in steps of 0.1 mL, which is 14
// to 17 mL of kaliumchloride 1 mmol/mL.
[<Tests>]
let tests =
    let limited conc = kcl () |> withConcentration "kaliumchloride" conc |> solved

    let between lo hi =
        {
            Min = fraction lo
            Max = fraction hi
        }

    let atMost br = { between br br with Min = None }

    testList
        "component concentration limit"
        [
            test "without a limit the kaliumchloride may be nearly all of the orderable" {
                let conc = kcl () |> solved |> Read.componentConcentration

                (conc |> Read.calculatedMax, conc |> Read.calculatedMaxIsExclusive)
                |> Expect.equal "below 1" (Some 1N, true)
            }

            test "a maximum of 1 part in 8 is the maximum concentration of the component" {
                limited (atMost (1N / 8N))
                |> Read.componentConcentration
                |> Read.calculatedMax
                |> Expect.equal "at most 1/8" (Some(1N / 8N))
            }

            test "a maximum of 1 part in 8 makes the orderable at least 8 times the lowest dose" {
                limited (atMost (1N / 8N))
                |> Read.orderableQuantity
                |> Read.calculatedMin
                |> Expect.equal "14 mL in at least 112 mL" (Some 112N)
            }

            test "a minimum and a maximum bound the orderable on both sides" {
                let qty = limited (between (1N / 10N) (1N / 8N)) |> Read.orderableQuantity

                (qty |> Read.calculatedMin, qty |> Read.calculatedMax)
                |> Expect.equal "14 mL in 112 mL up to 17 mL in 170 mL" (Some 112N, Some 170N)
            }

            test "a minimum equal to the maximum fixes the concentration at 1/8" {
                let conc = limited (between (1N / 8N) (1N / 8N)) |> Read.componentConcentration

                (conc |> Read.calculatedMin, conc |> Read.calculatedMax)
                |> Expect.equal "exactly 1/8" (Some(1N / 8N), Some(1N / 8N))
            }

            test "a concentration fixed at 1/8 makes the orderable 8 times the dose" {
                let qty = limited (between (1N / 8N) (1N / 8N)) |> Read.orderableQuantity

                (qty |> Read.calculatedMin, qty |> Read.calculatedMax)
                |> Expect.equal "14 mL in 112 mL up to 17 mL in 136 mL" (Some 112N, Some 136N)
            }

            test "a peripheral limit of 1 in 8 asks more diluent than a central limit of 1 in 2" {
                let peripheral = limited (atMost (1N / 8N)) |> Read.orderableQuantity |> Read.calculatedMin
                let central = limited (atMost (1N / 2N)) |> Read.orderableQuantity |> Read.calculatedMin

                (peripheral, central)
                |> Expect.equal "112 mL peripheral, 28 mL central" (Some 112N, Some 28N)
            }

            test "a medication with one component is built without the limit" {
                let single (med: Medication) = { med with Components = [ med.Components.Head ] }

                kcl ()
                |> single
                |> withConcentration "kaliumchloride" (atMost (1N / 8N))
                |> solved
                |> Read.componentConcentration
                |> OrderVariable.getValSetValueUnit
                |> Option.map ValueUnit.getValue
                |> Expect.equal "the component is all of the orderable" (Some [| 1N |])
            }

            // the bag holds exactly 22 mL of KCl 7,4%, 2 mL/kg for 11 kg, and 0 to 66 mL of NaCl 3%
            testList
                "electrolyte and glucose bag, three components"
                [
                    let withElectrolytes med =
                        med |> withQuantity "KCl 7,4%" 22N 22N |> withQuantity "NaCl 3%" 0N 66N

                    // exactly 66 mL of NaCl 3%, at most 1 part in 4, beside the KCl 7,4% at most 1 part in 8
                    let bothLimited () =
                        bag ()
                        |> withQuantity "KCl 7,4%" 22N 22N
                        |> withConcentration "KCl 7,4%" (atMost (1N / 8N))
                        |> withQuantity "NaCl 3%" 66N 66N
                        |> withConcentration "NaCl 3%" (atMost (1N / 4N))

                    let bagMin med = med |> solved |> Read.orderableQuantity |> Read.calculatedMin

                    test "without a concentration limit the bag is the KCl 7,4% and one step of each other component" {
                        bag ()
                        |> withElectrolytes
                        |> bagMin
                        |> Expect.equal "22 mL and 1 mL and 1 mL" (Some 24N)
                    }

                    test "a peripheral limit of 1 in 8 on the KCl 7,4% makes the bag at least 176 mL" {
                        bag ()
                        |> withElectrolytes
                        |> withConcentration "KCl 7,4%" (atMost (1N / 8N))
                        |> bagMin
                        |> Expect.equal "22 mL in at least 176 mL" (Some 176N)
                    }

                    test "a central limit of 1 in 2 on the KCl 7,4% makes the bag at least 44 mL" {
                        bag ()
                        |> withElectrolytes
                        |> withConcentration "KCl 7,4%" (atMost (1N / 2N))
                        |> bagMin
                        |> Expect.equal "22 mL in at least 44 mL" (Some 44N)
                    }

                    test "with a limit on both electrolytes the limit that asks the larger bag decides" {
                        bothLimited ()
                        |> bagMin
                        |> Expect.equal "66 mL of NaCl 3% in at least 264 mL" (Some 264N)
                    }

                    test "the limits leave the gluc 10% the remainder of the bag" {
                        let ord = bothLimited () |> solved

                        ord.Orderable.Components[2].OrderableQuantity
                        |> OrderVariable.Quantity.toOrdVar
                        |> Read.calculatedMin
                        |> Expect.equal "264 mL less 66 mL and 22 mL" (Some 176N)
                    }
                ]
        ]
