[<RequireQualifiedAccess>]
module PdfGeneration

open System
open QuestPDF.Fluent
open QuestPDF.Infrastructure
open QuestPDF.Helpers
open Informedica.GenOrder.Lib.Types

// TODO: buy a license
QuestPDF.Settings.License <- LicenseType.Evaluation

let formatDate (date: DateTime) =
    let month =
        match date.Month with
        | 1 -> "jan"
        | 2 -> "feb"
        | 3 -> "mar"
        | 4 -> "apr"
        | 5 -> "mei"
        | 6 -> "jun"
        | 7 -> "jul"
        | 8 -> "aug"
        | 9 -> "sep"
        | 10 -> "okt"
        | 11 -> "nov"
        | _ -> "dec"
    $"%d{date.Day}-%s{month}-%d{date.Year}"

let formatTime (time: DateTime) = $"%02d{time.Hour}:%02d{time.Minute}:%02d{time.Second}"

let formatWeight (patient: Patient) =
    match patient.Weight with
    | None -> ""
    | Some value -> "TODO: weight"

// writes cell in the shape of a key-value pair within a table
// the format is `key: value` where value is bold
let headerCell (table: TableDescriptor) (key: string) (value: string) =
    let fontSize = 8.0f
    let _ =
        table
            .Cell()
            .Table(fun innerTable ->
                innerTable.ColumnsDefinition(fun columns ->
                    columns.RelativeColumn()
                    columns.RelativeColumn()
                )

                let _ = innerTable.Cell().Text($"{key}:").FontSize(fontSize)
                let _ = innerTable.Cell().Text(value).AlignCenter().Bold().FontSize(fontSize)
                ignore ()
            )

    ignore ()

type AdditionalOrderPlanInfo =
    {
        Timestamp: DateTime
        PatientName: string
        Number: string
        Bed: string
    }

[<RequireQualifiedAccess>]
type PharmacyReport =
    | GeneratedPdfs of list<QuestPDF.Fluent.Document>
    | Error of string

// An order plan has order contexts
// per context, there are scenarios
// per scenario, there is one order


// Category(TPN) OrderPlan generates 1 PDF for all orders within the plan.
//  --- 1 page for all orders
//  --- every order has a pump rate

// Category(Medication/Drug) multiple PDFs per order
// -- each Order generates a separate PDF (1 page per PDF)
// --
let pharmacyReportFromOrderPlan (orderPlan: OrderPlan) (info: AdditionalOrderPlanInfo) : PharmacyReport =
    let date = info.Timestamp
    let patientName = info.PatientName
    let renderPatientInfo (table: TableDescriptor) =
        // define three equally sized columns
        table.ColumnsDefinition(fun columns ->
            columns.RelativeColumn()
            columns.RelativeColumn()
            columns.RelativeColumn()
        )

        let _ = table.Cell().Text("NICU VTGM protocol").Bold().FontSize(10.0f)
        headerCell table "datum" (formatDate date)
        headerCell table "bed" info.Bed
        headerCell table "patient" patientName
        headerCell table "naam arts" ""
        headerCell table "tijd" (formatTime date)
        headerCell table "nummer" info.Number
        headerCell table "gewicht" (formatWeight orderPlan.Patient)
        headerCell table "pieper" ""

    let listItem (column: ColumnDescriptor) (fontSize: float32) (text: string) =
        let _ =
            column
                .Item()
                .Row(fun row ->
                    let _ = row.ConstantItem(10.0f).Text("•").FontSize(fontSize)
                    let _ = row.RelativeItem().Text(text).FontSize(fontSize)
                    ignore ()
                )
        ignore ()

    // TODO: parameterize instructions based on products
    let renderInstructions (column: ColumnDescriptor) =
        let fontSize = 10.0f
        let _ = column.Item().Text("Aantal spuiten: 1").Bold().FontSize(fontSize)
        let _ = column.Item().Text("Bereidingstekst").Bold().FontSize(fontSize)
        let _ =
            column.Item().Text("WERK ASEPTISCH IN EEN VAN DE LAF-KASTEN met achtergrond klasse D").FontSize(fontSize)
        let _ = column.Item().Text("LINE CLEARANCE UITGEVOERD? JA/NEE").FontSize(fontSize)
        listItem
            column
            fontSize
            "Trek via een filternaald de benodigde hoeveelheid adrenaline 0,1 mg/ml op in een passende injectiespuit."
        listItem
            column
            fontSize
            "Trek de benodigde hoeveelheid glucose ……% op in een gekleurde injectiespuit van 50 ml."
        listItem column fontSize "Voeg de adrenaline oplossing 0,1 mg/ml toe aan de glucose …...%."
        listItem column fontSize "Zuig lucht in de injectiespuit van 50 ml en meng."
        listItem column fontSize "Ontlucht de spuit en sluit af met een rode afsluitdop"
        listItem column fontSize "Herhaal indien meerdere spuiten zijn aangevraagd."
        listItem column fontSize "Etiketteer de injectiespuit(en)."

    let category = orderPlan.Contexts |> Array.tryHead |> Option.map (fun ctx -> ctx.Category)
    match category with
    | Some(OrderCategory.Nutrition NutritionCategory.TPN) ->
        // every TPN order becomes one entry in one PDF
        // every one of these order will have a pump rate
        let document =
            Document.Create(fun container ->
                for planCtx in orderPlan.Contexts do
                    let scenarios = planCtx.Context.Scenarios
                    for scenario in scenarios do
                        let order = scenario.Order
                        let _ =
                            container.Page(fun page ->
                                page.Size PageSizes.A4
                                page.MarginHorizontal 50.0f
                                page.MarginVertical 50.0f
                                page.PageColor Colors.White

                                let _ =
                                    page
                                        .Content()
                                        .Column(fun column ->
                                            let _ = column.Item().Table renderPatientInfo
                                            renderInstructions column
                                        )

                                ignore ()
                            )

                        ignore ()
            )

        PharmacyReport.GeneratedPdfs [ document ]
    | _ ->
        // everything else becomes one PDF per order
        // specifically, one medication per PDF
        let pdfs =
            [
                for orderCtx in orderPlan.Contexts do
                    for scenario in orderCtx.Context.Scenarios do
                        let order = scenario.Order
                        let document =
                            Document.Create(fun container ->
                                let _ =
                                    container.Page(fun page ->
                                        page.Size PageSizes.A4
                                        page.MarginHorizontal 50.0f
                                        page.MarginVertical 50.0f
                                        page.PageColor Colors.White

                                        let _ =
                                            page
                                                .Content()
                                                .Column(fun column ->
                                                    let _ = column.Item().Table renderPatientInfo
                                                    renderInstructions column
                                                )

                                        ignore ()
                                    )

                                ignore ()
                            )
                        yield document
            ]

        PharmacyReport.GeneratedPdfs pdfs
