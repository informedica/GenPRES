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

let patient = StubPatientData.patient

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


/// The NEO test patients of the stub launch page, copied from StubPatientData in
/// src/Informedica.GenPRES.Server/ServerApi.StubAdapters.fs, as the server project cannot be loaded here.
module StubPatients =

    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients


    /// A patient of the stub EHR that the stub launch page offers by its PatientId.
    type TestPatient =
        {
            /// The PatientId the launch page posts.
            Id: string
            /// The name the patient panel shows.
            Name: string
            /// The ward the patient is admitted to.
            Department: Department
            /// The gender.
            Gender: Gender
            /// The age at the moment the EHR data is read.
            Age: AgeValue
            /// The weight, measured on the day the EHR data is read.
            Weight: WeightValue
            /// The height, measured on the day the EHR data is read.
            Height: HeightValue
            /// The gestational age at birth; absent when the EHR has none.
            GestationalAge: AgeWeeksDays option
        }


    /// A NEO patient of a gender, 2 days old, with a gestational age in weeks, a weight in gram and a
    /// height in cm.
    let neoPatient gender weeks gram cm =
        {
            Id = $"neo-%i{weeks}wk"
            Name = $"NEO %i{weeks} weeks"
            Department = Department.neonatalICU "NEO"
            Gender = gender
            Age = AgeValue.create None None None (Some(Conversions.dayFromInt 2))
            Weight = WeightValue.weightInGram gram
            Height = HeightValue.heightInCm cm
            GestationalAge = weeks |> Conversions.weekFromInt |> AgeWeeksDays.fromWeeks |> Some
        }


    /// The NEO patients of the stub launch page, male and female in turn.
    let neoPatients =
        [
            neoPatient Male 26 600m 35m
            neoPatient Female 28 800m 36m
            neoPatient Male 31 1100m 40m
            neoPatient Female 34 1800m 42m
            neoPatient Male 37 2400m 50m
            neoPatient Female 40 4000m 55m
        ]


    /// The birth date that gives an age on a day: the date the age before that day, moved by up to three
    /// days when GenPRES would count the age in days otherwise; on a day that no birth date fits, the
    /// calendar date.
    let birthDateAt (day: DateTime) age =
        let inDays birth =
            birth
            |> BirthDate.fromDate
            |> PatientAge.birthDate
            |> PatientAge.getAgeValue day
            |> Option.map (AgeValue.getAgeInDays >> int)

        let target = age |> AgeValue.getAgeInDays |> int |> Some

        age
        |> PatientAge.ageValue
        |> PatientAge.getBirthDate day
        |> Option.map (fun bd ->
            let calendar = bd |> BirthDate.toDate

            [ 0; 1; -1; 2; -2; 3; -3 ]
            |> List.map (float >> calendar.AddDays)
            |> List.tryFind (fun birth -> inDays birth = target)
            |> Option.defaultValue calendar
            |> BirthDate.fromDate
        )


    /// The EHR data of a test patient read at a moment: born the age before that day, measured that day.
    let testPatientData (now: DateTime) tp =
        let measuredOn = now.Date
        let weight = WeightAtDate.create measuredOn tp.Weight
        let height = HeightAtDate.create measuredOn tp.Height

        let age =
            tp.Age
            |> birthDateAt measuredOn
            |> Option.map PatientAge.birthDate
            |> Option.defaultValue PatientAge.unknown

        Patient.create
            tp.Id
            tp.Name
            tp.Department
            [||]
            tp.Gender
            age
            (Weight.create [ weight ] None [] (Some weight))
            (Height.create [ height ] None (Some height))
            tp.GestationalAge
            UnknownEnteral
            UnknownVenous
        |> fun pat -> Informedica.GenForm.Lib.EhrPatientData.create pat None []


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
    let patientOf tp = tp |> StubPatients.testPatientData now |> Patient.ofEhr now


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
            TestPatient: StubPatients.TestPatient
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
            StubPatients.neoPatients |> List.collect casesOf
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
                for tp in StubPatients.neoPatients do
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
                    let category = OrderCategory.Drug
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
