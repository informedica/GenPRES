// Prototype for #1457: an intravenous order with a solution rule and no diluent is offered at
// the concentration of the vial, although the rule forbids it.
//
// With one component nothing dilutes the product, so withItemQtyConc gives the item's
// orderable concentration the product's concentrations as a value set. withItemSolution then
// adds the solution rule's minimum and maximum, and Constraints.toValueRange drops those as
// soon as there is a value set. The solver never sees the limits, and isWithinConstraints,
// which reads the constraints through the same toValueRange, reports the order as fine.
//
// The change here keeps toValueRange as it is, since every order variable goes through it, and
// fixes the one place the conflict arises:
//
// - withItemQtyConc gives a single-component item only the product concentrations its
//   solution limit allows
// - toOrder refuses a single-component medication when a substance has none left, so the
//   medication yields no scenario, and evaluateRules logs which one and why
//
// A medication with a diluent has two components; its orderable concentration gets no value
// set and the limits already reach the solver, so it is left as it is.
//
// The pure tests use the amfo scenario. The check at the end runs against the demo sheet
// named in the issue, which has no parenteral products, so it needs the network or the cache.
// GENPRES_PROD is 0 so the products come from the demo cache in data/cache; with 1 they come
// from the proprietary cache, and without it no product loads and no rule is found.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "nuget: Expecto"

open System

Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module Medication =

    open Informedica.GenOrder.Lib.Medication
    open Informedica.GenOrder.Lib.Types
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


    /// The concentrations of a substance that its solution limit allows, judged by the
    /// bounds withItemSolution sets: inclusive, and widened to 90 and 110 percent when the
    /// limit is one value. A substance with no concentrations or no concentration limit keeps
    /// what it has, and so does one whose concentrations are in a unit group the limit cannot
    /// be compared with (a molar concentration against a mass limit): the bounds are compared
    /// on base values, so those would compare as numbers and give a wrong answer.
    let concentrationsWithinSolution (si: SubstanceItem) =
        match si.Concentrations, si.Solution with
        | Some vu, Some sl when sl.Concentration <> MinMax.empty ->
            let comparable =
                [ sl.Concentration.Min; sl.Concentration.Max ]
                |> List.choose id
                |> List.forall (Limit.getValueUnit >> ValueUnit.eqsGroup vu)

            if comparable then
                let cs =
                    OrderVariable.Constraints.create None None None None
                    |> OrderVariable.Constraints.setMinMax true sl.Concentration

                vu
                |> ValueUnit.filter (Informedica.GenSolver.Lib.Variable.ValueRange.isBetweenMinMax cs.Min cs.Max)
                |> Some
            else
                si.Concentrations
        | _ -> si.Concentrations


    /// The substances of a medication given undiluted whose solution limit allows none of
    /// the concentrations its product has. Only a medication with one component is given
    /// undiluted; with more, the orderable concentration is the solver's to find.
    let undilutedOutsideSolution (med: Medication) =
        match med.Components with
        | [ pc ] ->
            pc.Substances
            |> List.filter (fun si ->
                si.Concentrations |> Option.exists (ValueUnit.isEmpty >> not)
                && si |> concentrationsWithinSolution |> Option.exists ValueUnit.isEmpty
            )
        | _ -> []


    module OrderBuilder =

        open Informedica.GenOrder.Lib.Medication.OrderBuilder


        /// The quantities and concentrations the products give an item. With one component
        /// the orderable is the component, so the concentration in the one is the
        /// concentration in the other, and only the concentrations the solution limit allows
        /// are offered: nothing dilutes the product to bring another within the limit.
        let withItemQtyConc (med: Medication) (si: SubstanceItem) (itm: Types.Item) =
            let single = med.Components |> List.length = 1
            let setVals vu =
                OrderVariable.mapConstraints (OrderVariable.Constraints.setValues vu)

            { itm with
                ComponentConcentration =
                    itm.ComponentConcentration
                    |> OrderVariable.Concentration.apply (setVals si.Concentrations)
                ComponentQuantity = itm.ComponentQuantity |> OrderVariable.Quantity.apply (setVals si.Quantities)
                OrderableConcentration =
                    if single then
                        itm.OrderableConcentration
                        |> OrderVariable.Concentration.apply (setVals (si |> concentrationsWithinSolution))
                    else
                        itm.OrderableConcentration
            }
            |> fun itm ->
                match si.Solution with
                | None -> itm
                | Some sl -> itm |> withItemSolution sl


        /// Every constraint an item carries.
        let withItemConstraints (med: Medication) (ord: Order) =
            ord
            |> mapItems (fun _ si itm -> itm |> withItemQtyConc med si |> withItemDose med si) med


        /// Build an Order from a Medication: the shape, and then every constraint. The start
        /// is the caller's, in UTC.
        let build (start: DateTime) (med: Medication) =
            med
            |> newOrder start
            |> withComponents med
            |> withItemConstraints med
            |> withComponentConstraints med
            |> withOrderableConstraints med
            |> withPrescription med
            |> withAdjustment med


    /// Build an Order from a Medication, or say why it could not be built. A medication given
    /// undiluted at a concentration its solution limit forbids is not built: there is no
    /// diluent to bring it within the limit, so no order of it can be prescribed.
    let toOrder (start: DateTime) (med: Medication) : Result<Order, Exceptions.Message> =
        match med |> undilutedOutsideSolution with
        | [] ->
            try
                med |> OrderBuilder.build start |> Ok
            with exn ->
                exn |> Exceptions.OrderCouldNotBeCreated |> Error
        | sis ->
            let names = sis |> List.map _.Name |> String.concat ", "

            $"%s{med.Name} has no diluent, and its solution limit allows no concentration of %s{names} its product has"
            |> InvalidOperationException
            :> exn
            |> Exceptions.OrderCouldNotBeCreated
            |> Error


let start = DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc)

let mgPerMl = Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter

/// The amphotericin scenario: a 4 mg/mL vial, a solution limit of 0,2 to 2 mg/mL and
/// glucose 10% as the diluent.
let amfo = Scenarios.amfo

let amfoUndiluted = { amfo with Components = [ amfo.Components |> List.head ] }

/// The amphotericin item with its vial concentrations replaced.
let withVialConcentrations brs (med: Types.Medication) =
    { med with
        Components =
            med.Components
            |> List.mapi (fun i pc ->
                if i > 0 then
                    pc
                else
                    { pc with
                        Substances =
                            pc.Substances
                            |> List.map (fun si ->
                                if si.Name = "amfotericine b liposomaal" then
                                    { si with Concentrations = brs |> ValueUnit.withUnit mgPerMl |> Some }
                                else
                                    si
                            )
                    }
            )
    }

/// The value set and the minimum and maximum the orderable concentration of the amphotericin
/// item was defined with.
let amfoOrbCnc (ord: Order) =
    ord.Orderable.Components
    |> List.head
    |> _.Items
    |> List.find (fun itm -> (itm.Name |> Name.toString).Contains "amfotericine")
    |> _.OrderableConcentration
    |> OrderVariable.Concentration.toOrdVar
    |> _.DefinedConstraints


/// The value set of the orderable concentration of the amphotericin item, as a ValueUnit.
let amfoOrbCncValues (ord: Order) =
    ord
    |> amfoOrbCnc
    |> _.Values
    |> Option.map Informedica.GenSolver.Lib.Variable.ValueRange.ValueSet.toValueUnit


let pureTests =
    testList
        "#1457 undiluted concentration outside the solution limit"
        [
            test "a substance keeps only the concentrations its solution limit allows" {
                let si =
                    amfo
                    |> withVialConcentrations [| 1N; 4N |]
                    |> _.Components.Head.Substances
                    |> List.find (fun si -> si.Name = "amfotericine b liposomaal")

                si
                |> Medication.concentrationsWithinSolution
                |> Expect.equal "1 mg/mL is within 0,2 to 2 mg/mL, 4 mg/mL is not" (1N |> ValueUnit.singleWithUnit mgPerMl |> Some)
            }

            test "a one-value limit allows the concentrations within 90 to 110 percent of it" {
                let si =
                    { Medication.SubstanceItem.item with
                        Name = "vancomycine"
                        Concentrations = [| 9N; 21N / 2N; 12N; 50N |] |> ValueUnit.withUnit mgPerMl |> Some
                        Solution =
                            { SolutionLimit.limit with
                                Concentration =
                                    10N |> ValueUnit.singleWithUnit mgPerMl |> MinMax.createExact
                            }
                            |> Some
                    }

                si
                |> Medication.concentrationsWithinSolution
                |> Expect.equal
                    "9 and 10,5 mg/mL are within 9 to 11 mg/mL"
                    ([| 9N; 21N / 2N |] |> ValueUnit.withUnit mgPerMl |> Some)
            }

            test "a substance without a solution limit keeps every concentration" {
                let si =
                    amfo.Components.Head.Substances |> List.find (fun si -> si.Name = "saccharose")

                si
                |> Medication.concentrationsWithinSolution
                |> Expect.equal "saccharose has no solution limit" si.Concentrations
            }

            test "the undiluted amphotericin order is refused" {
                amfoUndiluted
                |> Medication.toOrder start
                |> Result.isError
                |> Expect.isTrue "a 4 mg/mL vial with no diluent cannot meet 0,2 to 2 mg/mL"
            }

            test "an undiluted order offers only the vial concentrations within the limit" {
                amfoUndiluted
                |> withVialConcentrations [| 1N; 4N |]
                |> Medication.toOrder start
                |> Result.map amfoOrbCncValues
                |> Expect.equal "only 1 mg/mL is offered" (Ok(1N |> ValueUnit.singleWithUnit mgPerMl |> Some))
            }

            test "the diluted amphotericin order is built as before" {
                let constraints f =
                    amfo |> f start |> Result.map amfoOrbCnc |> Result.map (fun cs -> cs.Min, cs.Max, cs.Values)

                constraints Medication.toOrder
                |> Expect.equal
                    "the orderable concentration is unchanged"
                    (constraints Informedica.GenOrder.Lib.Medication.toOrder)
            }

            test "a medication without a solution limit is built as before" {
                let pcm = Scenarios.pcmSupp

                pcm
                |> Medication.undilutedOutsideSolution
                |> Expect.isEmpty "paracetamol suppository has no solution limit"

                pcm
                |> Medication.toOrder start
                |> Result.isOk
                |> Expect.isTrue "it is built"
            }
        ]


runTestsWithCLIArgs [] [||] pureTests


// The check against the demo sheet: every intravenous medication the prototype refuses for want
// of a diluent, for the six patients of the issue. The issue lists eighteen drugs; the
// list here should hold those, less any whose product concentrations include one within the
// limit, which are now built at that concentration instead.

let demoUrlId = "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"

let provider = lazy (Api.getCachedProviderWithDataUrlId OrderLogging.noOp demoUrlId)

let patients =
    [
        "newborn", Patient.newBorn
        "infant", Patient.infant
        "toddler", Patient.toddler
        "child", Patient.child
        "teenager", Patient.teenager
        "adult", Patient.adult
    ]

/// The patient and the medication of each intravenous order the prototype refuses because its
/// solution limit allows no concentration of the undiluted product.
let refusedNow () =
    [
        for name, pat in patients do
            let meds =
                pat
                |> Api.getPrescriptionRules provider.Value
                |> Result.defaultValue [||]
                |> Array.collect (Medication.fromRule OrderLogging.noOp)
                |> Array.filter (fun med -> med.Route |> String.equalsCapInsens "intraveneus")

            for med in meds do
                match med |> Medication.toOrder start with
                | Error(Exceptions.OrderCouldNotBeCreated(:? InvalidOperationException)) -> name, med.Name
                | _ -> ()
    ]
    |> List.distinct


let providerTests =
    testList
        "#1457 against the demo sheet"
        [
            test "the demo sheet has no parenteral products" {
                provider.Value.GetParenteralMeds()
                |> Expect.isEmpty "every solution rule resolves to no diluent"
            }

            test "the undiluted vancomycin order is refused for the child" {
                refusedNow ()
                |> List.contains ("child", "vancomycine")
                |> Expect.isTrue "50 mg/mL is outside 9 to 11 mg/mL"
            }
        ]


runTestsWithCLIArgs [] [||] providerTests

refusedNow ()
|> List.groupBy snd
|> List.iter (fun (drug, xs) -> printfn $"""%s{drug}: %s{xs |> List.map fst |> String.concat ", "}""")
