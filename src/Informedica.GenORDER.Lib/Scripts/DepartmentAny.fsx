// A patient without a department gets the rules of every department. Today `getRules` asks
// for a weight, a height and a department, and without the department answers a context made
// afresh and no rules at all: the selection resets and nothing can be prescribed. The server
// used to hide this by defaulting an empty department to "ICK" before the rules were filtered;
// plan 725 (step 3.1, issue #752) removed that default from the mapping, and plan 725 step 4.1
// (issue #762) then found every patient without a department, the demo's stub patient among
// them, unable to prescribe. The maintainer decided that the domain accepts the absent
// department: the dose filter's patient carries none, and GenFORM's filter matches a rule of
// any department to it. A dosing change, this library's.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Api.fs, `OrderContext.getRules`: the match below, the guard on the department gone.
//   2. tests/Informedica.GenORDER.Tests: a test over an in-memory provider that a patient
//      without a department keeps the selection and gets the rules of every department.
//
// Run from this directory: dotnet fsi DepartmentAny.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"
// the provider below reaches the G-Standaard through ZForm, which load.fsx leaves out
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"

open System
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Filters


// ---------------------------------------------------------------------------
// 1. Api.fs, module OrderContext: getRules with the department optional
// ---------------------------------------------------------------------------

module OrderContext =

    /// The rules for the context's selection and patient: a weight and a height are needed,
    /// a department is not, a patient without one taking the rules of every department. The
    /// body is the library's, but for the match.
    let getRules logger provider (ctx: OrderContext) =

        match ctx.Patient.Weight, ctx.Patient.Height, ctx.Patient.Department with
        | Some w, Some h, d ->

            let ind =
                if ctx.Filter.Indication.IsSome then
                    ctx.Filter.Indication
                else
                    ctx.Filter.Indications |> Array.someIfOne

            let gen =
                if ctx.Filter.Generic.IsSome then
                    ctx.Filter.Generic
                else
                    ctx.Filter.Generics |> Array.someIfOne

            let rte =
                if ctx.Filter.Route.IsSome then
                    ctx.Filter.Route
                else
                    ctx.Filter.Routes |> Array.someIfOne

            let frm =
                if ctx.Filter.Form.IsSome then
                    ctx.Filter.Form
                else
                    ctx.Filter.Forms |> Array.someIfOne

            let dst =
                if ctx.Filter.DoseType.IsSome then
                    ctx.Filter.DoseType
                else
                    ctx.Filter.DoseTypes |> Array.someIfOne

            let doseFilter =
                {
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Form = frm
                    DoseType = dst
                    Diluent = ctx.Filter.Diluent
                    Components = ctx.Filter.SelectedComponents |> Array.toList
                    Patient =
                        {
                            Location = ctx.Patient.Location
                            Department = d
                            Age = ctx.Patient.Age
                            GestAge = ctx.Patient.GestAge
                            PMAge = ctx.Patient.PMAge
                            Weight = Some w
                            Height = Some h
                            WeightMeasured = ctx.Patient.WeightMeasured
                            HeightMeasured = ctx.Patient.HeightMeasured
                            Diagnoses = [||]
                            Gender = ctx.Patient.Gender
                            Access = ctx.Patient.Access
                            RenalFunction = ctx.Patient.RenalFunction
                        }
                }

            let inds = doseFilter |> filterIndications logger provider
            let gens = doseFilter |> filterGenerics logger provider
            let rtes = doseFilter |> filterRoutes logger provider
            let frms = doseFilter |> filterForms logger provider
            let dsts = doseFilter |> filterDoseTypes logger provider

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let frm = frms |> Array.someIfOne
            let dst = dsts |> Array.someIfOne

            { ctx with
                Filter =
                    { ctx.Filter with
                        Indications = inds
                        Generics = gens
                        Routes = rtes
                        Forms = frms
                        DoseTypes = dsts
                        Indication = ind
                        Generic = gen
                        Route = rte
                        Form = frm
                        DoseType = dst
                    }
            },
            match ind, gen, rte, frm, dst with
            | Some _, Some _, Some _, _, Some _
            | Some _, Some _, _, Some _, Some _ ->

                { doseFilter with
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Form = frm
                    DoseType = dst
                }
                |> Informedica.GenForm.Lib.Api.filterPrescriptionRules provider
            | _ -> Ok [||]
        | _ -> ctx.Patient |> Informedica.GenOrder.Lib.OrderContext.create logger provider, Ok [||]


// ---------------------------------------------------------------------------
// 2. Against the demo data: the library's getRules and the one above, with and without a
//    department, for a ten-year-old choosing paracetamol.
// ---------------------------------------------------------------------------

Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"

let logger = OrderLogging.noOp

let child department =
    { Patient.patient with
        Department = department
        Age = Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 3650))
        Weight = Some(ValueUnit.singleWithUnit Units.Weight.kiloGram (BigRational.fromInt 32))
        Height = Some(ValueUnit.singleWithUnit Units.Height.centiMeter (BigRational.fromInt 140))
    }

let choosing generic department =
    let fresh = Informedica.GenOrder.Lib.OrderContext.create logger provider (child department)

    { fresh with
        Filter =
            { fresh.Filter with
                Generic = Some generic
                Generics = [| generic |]
            }
    }

let summary (ctx: OrderContext) =
    ctx.Filter.Generic, ctx.Filter.Generics.Length, ctx.Filter.Indications.Length, ctx.Filter.Routes.Length


module Tests =

    open Expecto
    open Expecto.Flip


    let tests =
        testList
            "a patient without a department"
            [
                test "today: the library's getRules resets the selection and finds no rules" {
                    let ctx, rules = choosing "paracetamol" None |> Informedica.GenOrder.Lib.OrderContext.getRules logger provider
                    ctx.Filter.Generic |> Expect.equal "the selection gone" None
                    rules |> Expect.equal "no rules" (Ok [||])
                }

                test "after: the selection stays and the indications are those of every department" {
                    let ctx, _ = choosing "paracetamol" None |> OrderContext.getRules logger provider
                    let ick, _ = choosing "paracetamol" (Some "ICK") |> OrderContext.getRules logger provider

                    ctx.Filter.Generic |> Expect.equal "the selection kept" (Some "paracetamol")
                    ctx.Filter.Indications |> Expect.isNonEmpty "indications offered"

                    ick.Filter.Indications
                    |> Array.forall (fun i -> ctx.Filter.Indications |> Array.contains i)
                    |> Expect.isTrue "every ICK indication among them"

                    printfn
                        "  no department: %A; ICK: %A"
                        (summary ctx)
                        (summary ick)
                }

                test "with a department, the library and the copy agree" {
                    let a = choosing "paracetamol" (Some "ICK") |> Informedica.GenOrder.Lib.OrderContext.getRules logger provider |> fst
                    let b = choosing "paracetamol" (Some "ICK") |> OrderContext.getRules logger provider |> fst
                    b.Filter |> Expect.equal "the same filter" a.Filter
                }
            ]


Tests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
