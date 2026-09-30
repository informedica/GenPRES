// Probe for #1235: with the form left open the server answers scenarios, one of them with the
// form "poeder voor injectievloeistof"; with that form chosen it answers Refused NoProducts.
// Evaluates the same context twice and prints what each lookup step sees.
// Run from this directory: dotnet fsi FormRefusal.fsx (build first, so load.fsx finds the DLLs).

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

module GenFormApi = Informedica.GenForm.Lib.Api

let logger = OrderLogging.noOp
let start = DateTime.Now

let provider: Resources.IResourceProvider =
    GenFormApi.getCachedProviderWithDataUrlId logger (Environment.GetEnvironmentVariable "GENPRES_URL_ID")

let generic = "amoxicilline/clavulaanzuur"
let route = "INTRAVENEUS"
let indication = "Ernstige bacteriele infecties"
let form = "poeder voor injectievloeistof"

open Patient.Optics

/// The browser's patient of the trail: 6 years, 24 kg, 124 cm, no department; and variants.
let six department =
    Patient.patient
    |> Patient.setAge [ 6 |> Years ]
    |> Patient.setWeight (24m |> Kilogram |> Some)
    |> Patient.setHeight (124 |> Centimeter |> Some)
    |> Patient.setDepartment department

let patients =
    [
        "child 4 y ICK", Patient.child
        "6 y no department", six None
        "6 y ICK", six (Some "ICK")
    ]

let withPicks (frm: string option) (ctx: OrderContext) =
    { ctx with
        Filter =
            { ctx.Filter with
                Generic = Some generic
                Route = Some route
                Indication = Some indication
                Form = frm
            }
    }

let evaluate ctx =
    OrderContext.UpdateOrderContext ctx |> OrderContext.evaluateOutcome start logger provider

let describe label ctx =
    printfn "\n== %s ==" label

    match evaluate ctx with
    | Error msgs -> printfn "Error: %A" msgs
    | Ok(Refused(_, r)) -> printfn "Refused %A" r
    | Ok(Evaluated cmd) ->
        let ctx = OrderContext.Command.get cmd
        printfn "Evaluated: %i scenarios; forms offered %A; form chosen %A" ctx.Scenarios.Length ctx.Filter.Forms ctx.Filter.Form

        ctx.Scenarios
        |> Array.iter (fun sc -> printfn "  scenario form=%s doseType=%A" sc.Form sc.DoseType)

for name, pat in patients do
    let ctxBase = OrderContext.create logger provider pat
    describe $"{name}, form open" (ctxBase |> withPicks None)
    describe $"{name}, form chosen" (ctxBase |> withPicks (Some form))

// the lookup steps, form open and chosen, patient left out and in
let doseFilter (frm: string option) (pat: Patient) =
    { Filter.doseFilter with
        Generic = Some generic
        Route = Some route
        Indication = Some indication
        Form = frm
        Patient = pat
    }

let allRules = GenFormApi.getDoseRules provider

for name, pat in patients do
  for frm in [ None; Some form ] do
    do
        let filter = doseFilter frm pat
        printfn "\n== %s ==" name
        let drs = allRules |> GenFormApi.filterDoseRules provider filter
        let prs = GenFormApi.filterPrescriptionRules provider filter |> Result.defaultValue [||]

        printfn "-- form %A: %i dose rules, %i prescription rules" frm drs.Length prs.Length

        drs
        |> Array.iter (fun dr ->
            let products = dr.ComponentLimits |> Array.collect _.Products
            let forms = products |> Array.map _.Form |> Array.distinct

            printfn
                "   rule form=%s doseType=%A products=%i productForms=%A"
                (dr.Generic.Form |> PharmaceuticalForm.toString)
                dr.DoseType
                products.Length
                forms
        )


// the browser's way: the answer to the open form, sent back with the form chosen
printfn "\n\n==== replay: the answer with the form open, sent back with the form chosen ===="

for name, pat in patients do
    let ctxBase = OrderContext.create logger provider pat

    match evaluate (ctxBase |> withPicks None) with
    | Ok(Evaluated cmd) ->
        let answered = OrderContext.Command.get cmd

        printfn
            "\n== %s: answer has %i scenarios, diluent %A, selected components %A, components %A"
            name
            answered.Scenarios.Length
            answered.Filter.Diluent
            answered.Filter.SelectedComponents
            answered.Filter.Components

        let sent =
            { answered with
                Filter = { answered.Filter with Form = Some form }
            }

        describe $"{name}, replay form chosen" sent

        // the same with the scenarios and lists dropped, as a fresh context would carry
        describe
            $"{name}, replay form chosen, scenarios dropped"
            { sent with Scenarios = [||] }
    | other -> printfn "%s: %A" name other


// the server's path: reconcile against a fresh context, refuse when a choice is dropped
printfn "\n\n==== the server's path: reconcile, then Filter.dropped ===="

for name, pat in patients do
    let fresh = OrderContext.create logger provider pat

    printfn
        "\n== %s: fresh forms %i, holds the form %b; fresh generics %i, routes %i, dose types %A"
        name
        fresh.Filter.Forms.Length
        (fresh.Filter.Forms |> Array.exists (String.equalsCapInsens form))
        fresh.Filter.Generics.Length
        fresh.Filter.Routes.Length
        fresh.Filter.DoseTypes

    match evaluate (fresh |> withPicks None) with
    | Ok(Evaluated cmd) ->
        let answered = OrderContext.Command.get cmd

        let sent =
            { answered with
                Filter = { answered.Filter with Form = Some form }
                Scenarios = [||]
            }

        let reconciled = sent |> OrderContext.reconcile logger provider

        printfn
            "   sent: form %A doseType %A; reconciled: form %A doseType %A forms %A; dropped %b"
            sent.Filter.Form
            sent.Filter.DoseType
            reconciled.Filter.Form
            reconciled.Filter.DoseType
            reconciled.Filter.Forms
            (Filter.dropped sent.Filter reconciled.Filter)

        if Filter.dropped sent.Filter reconciled.Filter then
            printfn "   refusal the server would answer: %A" (OrderContext.refusal provider sent)
    | other -> printfn "%s: %A" name other


// ==== the cause ====
//
// Reproduced on the demo data (GENPRES_PROD unset or 0, the running server's setting), not on
// the production cache. OrderContext.create read the rules for the patient as sent: a patient
// without a department matches no rule whose reconstitution names one, so the fresh list of
// forms lacked "poeder voor injectievloeistof". The evaluation matches with the provider's
// default department (matchedPatient) and offered the form. The reconcile that precedes every
// lookup compared the held pick against the fresh list, found the form missing, dropped it, and
// the server refused; the refusal kind read NoProducts since dose rules exist either way. The
// same fresh list made the form field empty after a scenario pick. Fixed in Api.fs: create reads
// the rules for the patient as the rules match it (matchedDepartment), the patient keeps its own.
