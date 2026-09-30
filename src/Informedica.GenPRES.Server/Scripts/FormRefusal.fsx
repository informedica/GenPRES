// Probe for #1235, the server's half: the browser's steps replayed through the server's own
// mappers and command path, with the anonymous 6-year-old as the client sends it (age in
// years, weight and height estimated, gender unknown, no department). Each step maps the
// answer out as the client receives it, applies the client's filter change from the shared
// model, and sends the result back in. Prints what the server answers at every step.
//
// Run from this directory: dotnet fsi FormRefusal.fsx (build first, so load.fsx finds the DLLs).

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open ServerApi

Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")

let logger = OrderLogging.noOp
let start = DateTime.Now

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId logger (Environment.GetEnvironmentVariable "GENPRES_URL_ID")

let generic = "amoxicilline/clavulaanzuur"
let route = "INTRAVENEUS"
let indication = "Ernstige bacteriele infecties"
let form = "poeder voor injectievloeistof"

/// The anonymous 6-year-old of the trail: 6 y, est 24.0 kg, est 124 cm, gender unknown.
let sixEstimated: Shared.Types.Patient =
    { Shared.Models.Patient.empty with
        Age =
            Some
                {
                    Years = Shared.Measures.toYear 6
                    Months = Shared.Measures.toMonth 0
                    Weeks = Shared.Measures.toWeek 0
                    Days = Shared.Measures.toDay 0
                }
        Weight =
            { Shared.Models.Patient.empty.Weight with
                Estimated = Some(Shared.Measures.toGram 24000)
            }
        Height =
            { Shared.Models.Patient.empty.Height with
                Estimated = Some(Shared.Measures.toCm 124)
            }
        Gender = Shared.Types.UnknownGender
    }

/// The same with the weight and height measured.
let sixMeasured: Shared.Types.Patient =
    { sixEstimated with
        Weight =
            { sixEstimated.Weight with
                Estimated = None
                Measured = Some(Shared.Measures.toGram 24000)
            }
        Height =
            { sixEstimated.Height with
                Estimated = None
                Measured = Some(Shared.Measures.toCm 124)
            }
    }

let demo = false

/// The server's command path: parse, evaluate, map out.
let send (cmd: Shared.Api.OrderContextCommand) (ctx: Shared.Types.OrderContext) =
    match ctx |> OrderContextService.parse with
    | Error errs -> Error errs
    | Ok pc ->
        pc
        |> OrderContextService.evaluateOutcome start logger provider (OrderContextMapper.Command.toDomain cmd)
        |> Result.map (OrderContextService.toResponse demo)

let describeFilter (f: Shared.Types.Filter) =
    $"ind %A{f.Indication} of %i{f.Indications.Length}; gen %A{f.Generic} of %i{f.Generics.Length}; "
    + $"rte %A{f.Route} of %i{f.Routes.Length}; frm %A{f.Form} of %A{f.Forms}; "
    + $"dst %A{f.DoseType} of %A{f.DoseTypes}; dil %A{f.Diluent} of %i{f.Diluents.Length}; "
    + $"cmps %A{f.SelectedComponents} of %i{f.Components.Length}"

let describe label (resp: Result<Shared.Types.OrderContextResponse, string[]>) =
    printfn "\n== %s" label

    match resp with
    | Error errs -> printfn "   Error %A" errs
    | Ok(Shared.Types.OrderContextResponse.Refused(ctx, r)) ->
        printfn "   Refused %A" r
        printfn "   sent: %s" (describeFilter ctx.Filter)
    | Ok(Shared.Types.OrderContextResponse.Evaluated ctx) ->
        printfn "   Evaluated %i scenarios" ctx.Scenarios.Length
        printfn "   %s" (describeFilter ctx.Filter)

        ctx.Scenarios
        |> Array.iter (fun sc -> printfn "   scenario form=%s doseType=%A diluent=%A" sc.Form sc.DoseType sc.Diluent)

/// The answered context, or the one sent back on a refusal.
let contextOf =
    function
    | Ok(Shared.Types.OrderContextResponse.Evaluated ctx)
    | Ok(Shared.Types.OrderContextResponse.Refused(ctx, _)) -> ctx
    | Error errs -> failwithf "%A" errs

module Model = Shared.Models.OrderContext

let update = send Shared.Api.OrderContextCommand.UpdateOrderContext

let run name (pat: Shared.Types.Patient) =
    printfn "\n\n######## %s" name

    let parsed = pat |> ServerApi.Patient.parse
    printfn "patient: %A" (parsed |> Result.map (fun p -> p.Age, p.Weight, p.Height, p.Department, p.Gender))

    let r1 = Model.empty |> Model.setPatient pat |> update
    describe "1 patient" r1

    let r2 = r1 |> contextOf |> Model.medicationChange (Some generic) |> update
    describe "2 generic" r2

    let r3 = r2 |> contextOf |> Model.indicationChange (Some indication) |> update
    describe "3 indication" r3

    let r4 = r3 |> contextOf |> Model.routeChange (Some route) |> update
    describe "4 route" r4

    let r5 = r4 |> contextOf |> Model.formChange (Some form) |> update
    describe "5 form chosen (the refusal of the issue)" r5

    let r6 = r5 |> contextOf |> Model.formChange None |> update
    describe "6 form cleared" r6

    let two = r6 |> contextOf

    match two.Scenarios |> Array.tryFind (fun sc -> sc.Form = form) with
    | None -> printfn "no scenario with the form"
    | Some sc ->
        let picked =
            { two with
                Filter = { two.Filter with Form = Some sc.Form }
                Scenarios = [| sc |]
            }

        let r7 = picked |> send Shared.Api.OrderContextCommand.SelectOrderScenario
        describe "7 scenario picked (the empty form box of the issue)" r7

        // what the page shows: the filter's form, the form field is hidden when Forms is empty
        let answered = r7 |> contextOf
        printfn "   page: form field shows %A, listed %A" answered.Filter.Form answered.Filter.Forms


run "6 y estimated (the browser)" sixEstimated


// variants of the sent context: what the browser could send that the run above does not
printfn "\n\n######## variants of the form-chosen step"

let upToRoute (pat: Shared.Types.Patient) =
    Model.empty
    |> Model.setPatient pat
    |> update
    |> contextOf
    |> Model.medicationChange (Some generic)
    |> update
    |> contextOf
    |> Model.indicationChange (Some indication)
    |> update
    |> contextOf
    |> Model.routeChange (Some route)
    |> update
    |> contextOf

let two = upToRoute sixEstimated

for label, frm in [ "capitalised", "Poeder voor injectievloeistof"; "trailing space", form + " "; "upper", form.ToUpper() ] do
    two |> Model.formChange (Some frm) |> update |> describe $"form {label}"

for label, pat in
    [
        "department blank", { sixEstimated with Department = Some "" }
        "department ICK", { sixEstimated with Department = Some "ICK" }
        "department unknown", { sixEstimated with Department = Some "XYZ" }
        "weight 23960 g",
        { sixEstimated with
            Weight =
                { sixEstimated.Weight with
                    Estimated = Some(Shared.Measures.toGram 23960)
                }
        }
        "measured", sixMeasured
    ] do
    let two = upToRoute pat
    printfn "\n-- %s: route step gives %i scenarios" label two.Scenarios.Length
    two |> Model.formChange (Some form) |> update |> describe $"form chosen, {label}"
