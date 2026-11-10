// Issue #1597, implementation plan docs/implementation-plans/1597-nutrition-components-and-diluent.md.
//
// Steps 1, 2a and 2b are migrated to GenFORM and GenORDER. The tests below run on the library code
// on the production sheet, with the sheet edits of the plan made in memory, and check the
// DilutionRules_Test and DoseRules_Test sheets (--test-sheet, --test-sheets). Steps 3 and 4 are
// prototyped here once their open points are settled.

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "nuget: Expecto"

open System
open Expecto
open Expecto.Flip
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")


open Informedica.GenOrder.Lib
open Patient.Optics


// ---- the sheet edits of the plan, in memory ----------------------------------------------

let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"
let logger = OrderLogging.noOp

let compositions = [ "Samenstelling B"; "Samenstelling C"; "Samenstelling D"; "Samenstelling E" ]
let isGlucose (s: string) = s.StartsWith "gluc "


type SheetEdit =
    /// The sheet as it is.
    | AsIs
    /// Glucose out of the dose rules and in the Diluents column, its dilution row keeps its name.
    | NamedRow
    /// As NamedRow, and the glucose dilution row says "diluent" in the Component column.
    | DiluentRow
    /// As DiluentRow, and Samenstelling C day 3 offers gluc 30% then gluc 20%.
    | DiluentRowTwoChoices
    /// The rows of the DilutionRules_Test sheet in place of the Samenstelling C day 3 rows; the dose
    /// rules as they are.
    | TestSheet
    /// As TestSheet, and the glucose out of the Samenstelling C dose rules.
    | TestSheetNoGlucose
    /// As TestSheetNoGlucose, with the Diluents cell split on ';' as the sheet writes it.
    | TestSheetSemicolon
    /// As TestSheetSemicolon, with the Diluents cell of the diluent row on every row of its weight band.
    | TestSheetEveryRow
    /// The DoseRules_Test rows in place of every Samenstelling C dose row, and the DilutionRules_Test
    /// rows in place of every Samenstelling C dilution row.
    | TestSheets


let editDilutionRowsBy edit (rows: DilutionRuleData[]) =
    let glucoseOf (r: DilutionRuleData) =
        rows
        |> Array.filter (fun x ->
            x.Generic = r.Generic
            && x.DoseText = r.DoseText
            && x.MinWeight = r.MinWeight
            && x.MaxWeight = r.MaxWeight
            && isGlucose x.Component
        )
        |> Array.map _.Component
        |> Array.distinct
        |> Array.toList

    rows
    |> Array.map (fun r ->
        if edit = AsIs || compositions |> List.contains r.Generic |> not then
            r
        else
            let diluents =
                if edit = DiluentRowTwoChoices && r.Generic = "Samenstelling C" && r.DoseText = "dag 3" then
                    [ "gluc 30%"; "gluc 20%" ]
                else
                    glucoseOf r

            let cmp =
                if edit <> NamedRow && isGlucose r.Component then
                    DilutionRule.diluentComponent
                else
                    r.Component

            { r with
                Diluents = diluents
                Component = cmp
            }
    )


let editDoseRowsBy edit (rows: DoseRuleData[]) =
    rows
    |> Array.filter (fun d ->
        edit = AsIs
        || not (compositions |> List.contains d.Generic.Name && d.ScheduleData.DoseLimitData.Component |> isGlucose)
    )


let testRows =
    lazy (Informedica.GenForm.Lib.Utils.Web.getDataFromSheet dataUrlId "DilutionRules_Test" |> DilutionRule.parseDilutionRuleData |> Result.get)


let doseTestRows =
    lazy (Informedica.GenForm.Lib.Utils.Web.getDataFromSheet dataUrlId "DoseRules_Test" |> DoseRuleData.parseDoseRuleData |> Result.get)


let isTestRule (r: DilutionRuleData) = r.Generic = "Samenstelling C" && r.DoseText = "dag 3"


let editDilutionRows edit (rows: DilutionRuleData[]) =
    match edit with
    | TestSheet
    | TestSheetNoGlucose -> rows |> Array.filter (isTestRule >> not) |> Array.append testRows.Value
    | TestSheets ->
        // only the Samenstelling C rows of the test sheet, whatever filter the sheet has on
        rows
        |> Array.filter (fun r -> r.Generic <> "Samenstelling C")
        |> Array.append (testRows.Value |> Array.filter (fun r -> r.Generic = "Samenstelling C"))
    | TestSheetSemicolon ->
        testRows.Value
        |> Array.map (fun r -> { r with Diluents = r.Diluents |> List.collect (String.splitAt ';' >> Array.toList) |> List.map String.trim })
        |> Array.append (rows |> Array.filter (isTestRule >> not))
    | TestSheetEveryRow ->
        let split (r: DilutionRuleData) =
            r.Diluents |> List.collect (String.splitAt ';' >> Array.toList) |> List.map String.trim |> List.filter String.notEmpty

        let test = testRows.Value

        test
        |> Array.map (fun r ->
            let cell =
                test
                |> Array.filter (fun x -> x.MinWeight = r.MinWeight && x.MaxWeight = r.MaxWeight)
                |> Array.map split
                |> Array.tryFind (List.isEmpty >> not)
                |> Option.defaultValue []

            { r with Diluents = cell }
        )
        |> Array.append (rows |> Array.filter (isTestRule >> not))
    | _ -> rows |> editDilutionRowsBy edit


let editDoseRows edit (rows: DoseRuleData[]) =
    match edit with
    | TestSheet -> rows
    | TestSheets ->
        rows
        |> Array.filter (fun d -> d.Generic.Name <> "Samenstelling C")
        |> Array.append (doseTestRows.Value |> Array.filter (fun d -> d.Generic.Name = "Samenstelling C"))
    | TestSheetNoGlucose
    | TestSheetSemicolon
    | TestSheetEveryRow ->
        rows
        |> Array.filter (fun d ->
            not (d.Generic.Name = "Samenstelling C" && d.ScheduleData.DoseLimitData.Component |> isGlucose)
        )
    | _ -> rows |> editDoseRowsBy edit


let providerOf edit =
    let reg = Resources.defaultRegistry logger dataUrlId

    reg
    |> Map.add
        Resources.Keys.dilutionRuleData.Name
        (Resources.ofResult (fun () -> DilutionRule.getData dataUrlId |> Result.map (editDilutionRows edit)))
    |> Map.add
        Resources.Keys.doseRuleData.Name
        (Resources.ofResult (fun () -> DoseRuleLoader.getData dataUrlId |> Result.map (editDoseRows edit)))
    |> fun reg ->
        Resources.CachedResourceProvider(logger, (fun () -> Resources.loadAllResourcesWithRegistry reg), None)
        :> Resources.IResourceProvider


let providers = [ AsIs; NamedRow; DiluentRow; DiluentRowTwoChoices; TestSheet; TestSheetNoGlucose; TestSheetSemicolon; TestSheetEveryRow; TestSheets ] |> List.map (fun e -> e, lazy providerOf e) |> Map.ofList
let provider edit = providers[edit].Value


// ---- orders --------------------------------------------------------------------------------

let eval provider ctx =
    ctx
    |> OrderContext.UpdateOrderContext
    |> OrderContext.evaluate DateTime.UtcNow logger provider
    |> Result.get
    |> OrderContext.Command.get


let contextFor provider generic (weight: decimal) day diluent =
    let ctx =
        Patient.infant
        |> Patient.setWeight (weight |> Kilogram |> Some)
        |> Patient.setDepartment (Some "ICK")
        |> OrderContext.create logger provider
        |> fun ctx ->
            { ctx with
                OrderContext.Filter.Indication = Some "Standaard Totale Parenterale Voeding"
                OrderContext.Filter.Generic = Some generic
                OrderContext.Filter.Route = Some "INTRAVENEUS"
            }
        |> eval provider

    let dt = ctx.Filter.DoseTypes |> Array.find (fun dt -> (dt |> DoseType.toDescription) = day)
    let ctx = { ctx with OrderContext.Filter.DoseType = Some dt } |> eval provider

    match diluent with
    | None -> ctx
    | Some d -> { ctx with OrderContext.Filter.Diluent = Some d } |> eval provider


/// The orderable quantity of each component and of the order, as text.
let quantities (ctx: OrderContext) =
    let text (ovar: OrderVariable) = ovar |> OrderVariable.toStringWithConstraints false false

    ctx.Scenarios
    |> Array.toList
    |> List.map (fun sc ->
        [
            for cmp in sc.Order.Orderable.Components do
                cmp.OrderableQuantity |> OrderVariable.Quantity.toOrdVar |> text
            sc.Order.Orderable.OrderableQuantity |> OrderVariable.Quantity.toOrdVar |> text
        ]
    )


let componentQty name (ctx: OrderContext) =
    ctx.Scenarios
    |> Array.exactlyOne
    |> _.Order.Orderable.Components
    |> List.find (fun c -> (c.Name |> WrappedString.Name.toString) = name)
    |> _.OrderableQuantity
    |> OrderVariable.Quantity.toOrdVar
    |> OrderVariable.toStringWithConstraints false false


let cases =
    [
        for g, w, days in
            [
                "Samenstelling B", 5m, [ "dag 1"; "dag 2"; "dag 3" ]
                "Samenstelling C", 8m, [ "dag 1"; "dag 2"; "dag 3" ]
                "Samenstelling C", 12m, [ "dag 1"; "dag 2"; "dag 3" ]
                "Samenstelling D", 20m, [ "dag 1"; "dag 2"; "dag 3" ]
                "Samenstelling E", 33m, [ "dag 1"; "dag 2"; "dag 3" ]
                "Samenstelling E", 40m, [ "dag 1"; "dag 2"; "dag 3" ]
            ] do
            for d in days do
                g, w, d
    ]


let formulary () = provider AsIs |> Api.get Resources.Keys.formularyProducts

let additive = lazy (Api.isAdditive (provider AsIs))

/// The isAdditive lookup by GPK on the production Formulary.
let isAdditive gpk = additive.Value gpk


// ---- tests --------------------------------------------------------------------------------

let tests =
    testList
        "#1597 nutrition components and diluent"
        [
            testList
                "a component row named diluent"
                [
                    test "binds each diluent as its own named row does, in every B to E order" {
                        for g, w, d in cases do
                            let named = contextFor (provider NamedRow) g w d None |> quantities
                            let diluent = contextFor (provider DiluentRow) g w d None |> quantities
                            diluent |> Expect.equal $"%s{g} %M{w} kg %s{d}" named
                    }

                    test "gives a second diluent choice the limits of the diluent row" {
                        let ctx = contextFor (provider DiluentRowTwoChoices) "Samenstelling C" 12m "dag 3" (Some "gluc 20%")
                        ctx |> componentQty "gluc 20%" |> Expect.stringContains "36-44 mL/kg at 12 kg" "432 mL..1 mL..528 mL"
                    }
                ]

            testList
                "the diluents in the order of the cell"
                [
                    test "offers the diluents in the order of the cell" {
                        let ctx = contextFor (provider DiluentRowTwoChoices) "Samenstelling C" 12m "dag 3" None
                        ctx.Filter.Diluents |> Expect.equal "gluc 30% first" [| "gluc 30%"; "gluc 20%" |]
                    }

                    test "takes the first diluent of the cell when none is chosen" {
                        let ctx = contextFor (provider DiluentRowTwoChoices) "Samenstelling C" 12m "dag 3" None
                        ctx.Filter.Diluent |> Expect.equal "gluc 30%" (Some "gluc 30%")
                    }
                ]

            testList
                "a diluent that is also a dose rule component"
                [
                    test "is found when the dilution rule names it" {
                        let p = provider DiluentRow
                        let pairs =
                            DilutionRule.diluentsThatAreComponents (p.GetRouteMappings()) (p.GetDoseRules()) (p.GetDilutionRules())

                        printfn $"\n%i{pairs.Length} pairs refused on the production sheet with the B to E edits:"
                        pairs |> List.iter (printfn "  %A")
                    }
                ]

            testList
                "the IsDiluent flag"
                [
                    test "warns for a diluent the Formulary does not mark" {
                        let warns =
                            DilutionRule.getData dataUrlId
                            |> Result.get
                            |> editDilutionRows DiluentRow
                            |> DilutionRule.unmarkedDiluents (formulary ())

                        printfn $"\n%i{warns.Length} diluents not marked IsDiluent on the production sheet:"
                        warns |> List.iter (printfn "  %A")
                    }
                ]

            testList
                "the optional components"
                [
                    test "are the additives after the first component in every B to E rule" {
                        let p = provider DiluentRow

                        for dr in p.GetDoseRules() do
                            if compositions |> List.contains (dr.Generic |> Generic.toString) then
                                dr.ComponentLimits
                                |> PrescriptionRule.optionalComponents isAdditive
                                |> Array.forall (fun n -> [ "NaCl 3%"; "KCl 7,4%"; "Peditrace" ] |> List.contains n)
                                |> Expect.isTrue $"%s{dr.Generic |> Generic.toString}"
                    }

                    test "keep a component that is not an additive when it is not selected" {
                        let p = provider AsIs

                        let dr =
                            p.GetDoseRules()
                            |> Array.find (fun dr ->
                                dr.Generic |> Generic.toString = "NICU mix" && dr.ComponentLimits.Length = 2
                            )

                        dr.ComponentLimits
                        |> PrescriptionRule.selectComponents isAdditive [ "none of them" ]
                        |> Array.map _.Name
                        |> Expect.equal "NICU mix and gluc 10% stay" (dr.ComponentLimits |> Array.map _.Name)
                    }

                    test "lists, per nutrition rule, the main and the optional components" {
                        let p = provider DiluentRow
                        printfn "\nmain | optional | fixed, per nutrition dose rule:"

                        p.GetDoseRules()
                        |> Array.filter (fun dr -> dr.ComponentLimits.Length > 1)
                        |> Array.filter (fun dr ->
                            [ "Totale Parenterale Voeding"; "Parenterale vetten"; "Parenterale suppletie" ]
                            |> List.exists dr.Indication.Contains
                        )
                        |> Array.map (fun dr ->
                            let cls = dr.ComponentLimits
                            let opt = cls |> PrescriptionRule.optionalComponents isAdditive
                            let fixd = cls |> Array.tail |> Array.map _.Name |> Array.filter (fun n -> opt |> Array.contains n |> not)
                            $"  %s{dr.Generic |> Generic.toString}: %s{cls[0].Name} | %A{opt} | %A{fixd}"
                        )
                        |> Array.distinct
                        |> Array.iter (printfn "%s")
                    }
                ]
        ]



let testSheetTests =
    testList
        "the DilutionRules_Test sheet, Samenstelling C day 3"
        [
            test "parses the Diluents cell" {
                for r in testRows.Value do
                    if r.Diluents |> List.exists String.notEmpty then
                        printfn $"\n%s{r.Component} %A{r.MinWeight}-%A{r.MaxWeight}: Diluents parsed as %A{r.Diluents}"
            }

            test "gives the dilution rules" {
                for edit in [ TestSheet; TestSheetNoGlucose; TestSheetSemicolon; TestSheetEveryRow ] do
                    let p = provider edit

                    let rules =
                        p.GetDilutionRules()
                        |> Array.filter (fun r -> r.Generic = "Samenstelling C" && (r.DoseType |> DoseType.toDescription) = "dag 3")

                    printfn $"\n%A{edit}: %i{rules.Length} dilution rules for Samenstelling C day 3"

                    for r in rules do
                        let targets = r.DilutionLimits |> Array.map (_.DilutionLimitTarget >> LimitTarget.toString)
                        printfn $"   weight %s{r.PatientCategory.Weight |> string}; diluents %A{r.Diluents |> Array.map _.Generic}; limits %A{targets}"

                    let warns = DilutionRule.diluentsThatAreComponents (p.GetRouteMappings()) (p.GetDoseRules()) rules
                    printfn $"   refused pairs: %A{warns}"
            }

            test "gives the orders" {
                for edit in [ TestSheet; TestSheetNoGlucose; TestSheetSemicolon; TestSheetEveryRow ] do
                    for w in [ 8m; 12m ] do
                        let ctx = contextFor (provider edit) "Samenstelling C" w "dag 3" None
                        printfn $"\n%A{edit} %M{w} kg: Filter.Diluents %A{ctx.Filter.Diluents}, Filter.Diluent %A{ctx.Filter.Diluent}, scenarios %i{ctx.Scenarios.Length}"
                        ctx |> quantities |> List.iter (List.iter (printfn "   %s"))

                        for dil in ctx.Filter.Diluents do
                            let ctx = contextFor (provider edit) "Samenstelling C" w "dag 3" (Some dil)
                            printfn $"   chosen %s{dil}: scenarios %i{ctx.Scenarios.Length}"
                            ctx |> quantities |> List.iter (List.iter (printfn "      %s"))
            }
        ]


let testSheetsTests =
    testList
        "DoseRules_Test with DilutionRules_Test, Samenstelling C"
        [
            test "gives the orders" {
                let p = provider TestSheets
                let pairs = DilutionRule.diluentsThatAreComponents (p.GetRouteMappings()) (p.GetDoseRules()) (p.GetDilutionRules())
                let cPairs = pairs |> List.filter (fun w -> (string w).Contains "Samenstelling C")
                printfn $"\nrefused pairs: %A{cPairs}"

                for w in [ 8m; 12m ] do
                    for d in [ "dag 1"; "dag 2"; "dag 3" ] do
                        let ctx = contextFor p "Samenstelling C" w d None
                        printfn $"\n%M{w} kg %s{d}: Filter.Diluents %A{ctx.Filter.Diluents}, Filter.Diluent %A{ctx.Filter.Diluent}, scenarios %i{ctx.Scenarios.Length}"
                        ctx |> quantities |> List.iter (List.iter (printfn "   %s"))

                        for dil in ctx.Filter.Diluents |> Array.truncate 2 |> Array.skip (min 1 ctx.Filter.Diluents.Length) do
                            let ctx = contextFor p "Samenstelling C" w d (Some dil)
                            printfn $"   chosen %s{dil}: scenarios %i{ctx.Scenarios.Length}"
                            ctx |> quantities |> List.iter (List.iter (printfn "      %s"))
            }
        ]


let args = fsi.CommandLineArgs |> Array.tail

if args |> Array.contains "--test-sheets" then
    runTestsWithCLIArgs [] [||] testSheetsTests |> ignore
elif args |> Array.contains "--test-sheet" then
    runTestsWithCLIArgs [] [||] testSheetTests |> ignore
else
    runTestsWithCLIArgs [] [||] tests |> ignore
