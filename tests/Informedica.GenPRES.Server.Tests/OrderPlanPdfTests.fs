module Informedica.GenPRES.Server.Tests.OrderPlanPdfTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib

open Informedica.GenPRES.Server
open ServerApi
open System.IO
open System
open QuestPDF.Fluent

QuestPDF.Settings.License <- QuestPDF.Infrastructure.LicenseType.Evaluation

let outputDir = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "orderPlanPdfs"))

let write dir fileName (pdf: byte[]) =
    if not (Directory.Exists(Path.Combine(outputDir, dir))) then
        Directory.CreateDirectory(Path.Combine(outputDir, dir)) |> ignore
    File.WriteAllBytes(Path.Combine(outputDir, dir, fileName), pdf)


/// The sheet ID of the spreadsheet with the dose rules of the indication, from GENPRES_URL_ID; None when it is
/// not set.
let dataUrlId =
    match Environment.GetEnvironmentVariable "GENPRES_URL_ID" with
    | null
    | "" -> None
    | id -> Some id


/// The indication whose medications are the continuous medication of the NICU.
let indication = "Continue infusie neonatologie"


/// The NEO test patients of the stub launch page.
let neoPatients = StubEhrPatient.testPatients |> List.filter (fun tp -> tp.Id.StartsWith "neo-")


module Cases =
    open Informedica.Utils.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    /// The moment the patients are read and the orders start.
    let now = DateTime.Now


    let logger = OrderLogging.noOp


    /// The resources of the spreadsheet with the dose rules, read when the first case needs them. There are no
    /// cases without a sheet ID.
    let provider: Lazy<Resources.IResourceProvider> =
        lazy (dataUrlId |> Option.get |> Api.getCachedProviderWithDataUrlId logger)


    /// A context evaluated by a command, the context of the answer, or the reason it was refused.
    let evaluate cmd =
        cmd
        |> OrderContext.evaluate now logger provider.Value
        |> Result.map OrderContext.Command.get
        |> Result.mapError (fun msgs -> $"%A{msgs}")


    /// A test patient as the rules take it: the patient GenPRES reads from the stub EHR at the moment.
    let patientOf tp = tp |> StubEhrPatient.testPatient now |> _.Data


    /// Step 2: the full order context of a patient, its pick lists filled by the rules for the patient.
    let fullContext pat =
        pat
        |> OrderContext.create logger provider.Value
        |> OrderContext.UpdateOrderContext
        |> evaluate


    /// Step 3: the context narrowed to the indication, its pick lists the medications of the indication.
    let indicationContext ctx =
        ctx
        |> OrderContext.setFilterIndication indication
        |> OrderContext.UpdateOrderContext
        |> evaluate


    /// Step 3: the context of one medication of the indication, with its scenarios.
    let medicationContext generic ctx =
        ctx
        |> OrderContext.setFilterGeneric generic
        |> OrderContext.UpdateOrderContext
        |> evaluate


    /// Step 4: the context with its first scenario selected and the values of its order calculated.
    let selectFirstScenario ctx = OrderContext.SelectNthOrderScenario(ctx, 0) |> evaluate


    /// Step 5: the dose rate of the item of the one scenario, the substance of the medication in its component,
    /// set to its median with the order DSL. A scenario without a component and an item, or an order that
    /// cannot be solved, refuses the change.
    let setItemDoseRate (ctx: OrderContext) =
        match
            ctx.Scenarios
            |> Array.tryExactlyOne
            |> Option.map (fun sc -> sc.Component, sc.Item)
        with
        | Some(Some cmp, Some itm) ->
            let act = OrderAction.Set(OrderVariableReference.ItemDose(cmp, itm, DoseProperty.Rate), Choice.Median)

            let run ord =
                ord |> OrderProcessor.OrderAction.run Order.ValueCaps.defaults logger act

            match ctx |> OrderContext.processScenarioOrder logger run with
            | Evaluated ctx -> Ok ctx
            | Refused(_, refusal) -> Error $"dose rate of %s{itm} not set: %A{refusal}"
        | _ -> Error "no scenario with a component and an item"


    /// A test case for a pharmacy PDF: a NEO patient with the context of one continuous medication.
    type PharmacyCase =
        {
            /// The test patient the case is made for.
            TestPatient: StubEhrPatient.TestPatient
            /// The medication of the indication.
            Generic: string
            /// The context with one scenario, its order with the dose rate set, or why it could not be made.
            Context: Result<OrderContext, string>
        }


    /// The order of the one scenario of a context.
    let orderOf (ctx: OrderContext) = ctx.Scenarios |> Array.exactlyOne |> _.Order


    /// Steps 1 to 5 for one test patient: a case for each medication of the indication the patient can be
    /// given, or a single case without a medication when the indication gives the patient none.
    let casesOf tp =
        let indCtx = tp |> patientOf |> fullContext |> Result.bind indicationContext

        match indCtx with
        | Error err ->
            [
                {
                    TestPatient = tp
                    Generic = ""
                    Context = Error err
                }
            ]
        | Ok indCtx ->
            [
                for generic in indCtx.Filter.Generics do
                    {
                        TestPatient = tp
                        Generic = generic
                        Context =
                            indCtx
                            |> medicationContext generic
                            |> Result.bind selectFirstScenario
                            |> Result.bind setItemDoseRate
                    }
            ]


    /// Step 6: the test cases of every NEO test patient; none without the sheet ID of GENPRES_URL_ID.
    let cases () =
        if dataUrlId.IsSome then
            neoPatients |> List.collect casesOf
        else
            []

    let solvedCases () =
        cases ()
        |> List.filter (fun c ->
            match c.Context with
            | Ok ctx -> ctx |> orderOf |> Order.isSolved
            | Error _ -> false
        )


// Tests
module Tests =
    open Cases

    let tests =
        testList
            "neo continuous medication"
            [
                for tp in neoPatients do
                    test $"%s{tp.Id} has a case for at least one medication" {
                        solvedCases ()
                        |> List.filter (fun c -> c.TestPatient.Id = tp.Id && c.Generic <> "")
                        |> List.isEmpty
                        |> Expect.isFalse "no medication of the indication"
                    }

                for c in solvedCases () do
                    test $"%s{c.TestPatient.Id} / %s{c.Generic} gives one solved order" {
                        match c.Context with
                        | Error err -> failtest err
                        | Ok ctx ->
                            ctx.Scenarios |> Array.length |> Expect.equal "one scenario" 1
                            ctx |> orderOf |> Order.isSolved |> Expect.isTrue "the order is solved"
                    }
            ]

[<Tests>]
let tests =
    testList
        "OrderPlanPdfTests"
        [
            if dataUrlId.IsNone then
                ptest "GENPRES_URL_ID holds the sheet ID of the spreadsheet with the dose rules" { () }

            for case in Cases.solvedCases () do
                test $"%s{case.TestPatient.Id} / %s{case.Generic} generates a PDF" {
                    let ctx =
                        match case.Context with
                        | Ok ctx -> ctx
                        | Error e -> failtest (sprintf "Failed to get context: %A" e)

                    let patient = Cases.patientOf case.TestPatient
                    let category = OrderCategory.Medication
                    let planContext = PlanContext.create "id" category ctx
                    let plan = OrderPlan.create patient [| planContext |]
                    let timestamp = DateTime(2026, 09, 01, 15, 40, 15)
                    let additionalInfo: PdfGeneration.AdditionalOrderPlanInfo =
                        {
                            Timestamp = timestamp
                            PatientName = "test"
                            Number = "12345"
                            Bed = "Test A1"
                        }

                    let report = PdfGeneration.pharmacyReportFromOrderPlan plan additionalInfo
                    match report with
                    | PdfGeneration.PharmacyReport.GeneratedPdfs pdfs ->
                        Expect.isTrue "Generated at least one PDF" (List.isEmpty pdfs |> not)
                        for (i, pdf) in List.indexed pdfs do
                            write $"{case.TestPatient.Id}_{case.Generic}" $"basic-plan-{i}.pdf" (pdf.GeneratePdf())
                    | PdfGeneration.PharmacyReport.Error err -> failtest err
                }
        ]
