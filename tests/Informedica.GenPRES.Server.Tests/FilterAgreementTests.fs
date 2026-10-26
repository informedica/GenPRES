/// The domain's filter commands and the client's preview in Shared make the same filter, so a pick
/// shows while it runs as the server will answer it.
module Informedica.GenPRES.Server.Tests.FilterAgreementTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib

open FilterFixtures

module S = Informedica.GenPRES.Shared.Types
module SCtx = Informedica.GenPRES.Shared.Models.OrderContext
module D = Informedica.GenForm.Lib.Types


/// The domain's filter in the contract's shape.
module Contract =

    let doseType (dt: D.DoseType) =
        match dt with
        | D.Once s -> S.DoseType.Once s
        | D.Discontinuous s -> S.DoseType.Discontinuous s
        | D.Continuous s -> S.DoseType.Continuous s
        | D.Timed s -> S.DoseType.Timed s
        | D.OnceTimed s -> S.DoseType.OnceTimed s
        | D.NoDoseType -> S.DoseType.NoDoseType


    let filter (f: Filter) : S.Filter =
        {
            Indications = f.Indications
            Generics = f.Generics
            Routes = f.Routes
            Forms = f.Forms
            DoseTypes = f.DoseTypes |> Array.map doseType
            Diluents = f.Diluents
            Components = f.Components
            Indication = f.Indication
            Generic = f.Generic
            Route = f.Route
            Form = f.Form
            DoseType = f.DoseType |> Option.map doseType
            Diluent = f.Diluent
            SelectedComponents = f.SelectedComponents
        }


    let category (c: OrderCategory) =
        match c with
        | OrderCategory.Drug -> S.OrderCategory.Drug
        | OrderCategory.Nutrition _ -> S.OrderCategory.Nutrition S.NutritionCategory.TPN


    /// The five fields the rule lookup reads, by their name in Shared.
    let field (f: FilterField) =
        match f with
        | FilterField.Indication -> SCtx.Indication
        | FilterField.Generic -> SCtx.Generic
        | FilterField.Route -> SCtx.Route
        | FilterField.Form -> SCtx.Form
        | FilterField.DoseType -> SCtx.DoseType
        | FilterField.Diluent
        | FilterField.Components -> invalidArg (nameof f) "Shared has no filter field for it"


    /// A Shared context with the category and the filter of the domain context, without scenarios.
    let context cat (ctx: OrderContext) : S.OrderContext =
        { SCtx.empty with
            Category = category cat
            Filter = filter ctx.Filter
            Scenarios = [||]
        }


let sameFilter (domain: Result<OrderContext, string>) (shared: Result<S.OrderContext, string>) =
    match domain, shared with
    | Ok d, Ok s ->
        d.Filter |> Contract.filter |> Expect.equal "the same filter" s.Filter
        d.Scenarios.Length
        |> Expect.equal "the same number of scenarios" s.Scenarios.Length
    | Error _, Error _ -> ()
    | d, s -> failtest $"one answers, the other refuses: %A{d |> Result.map ignore} %A{s |> Result.map ignore}"


[<Tests>]
let tests =
    testList
        "the domain's filter commands and Shared's"
        [
            testList
                "every lookup field, every index"
                [
                    for cname, category in categories do
                        for fname, filter in filters do
                            for field in OrderContext.filterFields do
                                // Shared's change empties the scenarios, so both start without
                                let ctx = { context filter with Scenarios = [||] }
                                let shared = ctx |> Contract.context category

                                for n in [ None ] @ [ for i in 0 .. count field filter -> Some i ] do
                                    test $"%s{cname}, %s{fname}, %A{field} %A{n}" {
                                        sameFilter
                                            (ctx |> OrderContext.changeFilter category field n)
                                            (shared |> SCtx.changeFilter (Contract.field field) n)
                                    }
                ]

            testList
                "the diluent, the components, everything"
                [
                    for fname, filter in filters do
                        let ctx = { context filter with Scenarios = [||] }
                        let shared = ctx |> Contract.context OrderCategory.Drug

                        for n in [ None; Some 0; Some 1; Some 2 ] do
                            test $"%s{fname}, diluent %A{n}" {
                                sameFilter
                                    (ctx |> OrderContext.changeFilter OrderCategory.Drug FilterField.Diluent n)
                                    (shared |> SCtx.changeDiluent n)
                            }

                            test $"%s{fname}, one component %A{n}" {
                                sameFilter
                                    (ctx |> OrderContext.changeFilter OrderCategory.Drug FilterField.Components n)
                                    (shared |> SCtx.setNthComponents (n |> Option.toArray))
                            }

                        for ns in [ [||]; [| 0 |]; [| 1; 2 |]; [| 3 |] ] do
                            test $"%s{fname}, components %A{ns}" {
                                sameFilter
                                    (ctx |> OrderContext.setNthComponents ns)
                                    (shared |> SCtx.setNthComponents ns)
                            }

                        test $"%s{fname}, everything cleared" {
                            sameFilter (ctx |> OrderContext.clearAll |> Ok) (shared |> SCtx.clearAll |> Ok)
                        }
                ]
        ]


[<Tests>]
let seedTests =
    let sources =
        [
            SeedSource.Url, S.SeedSource.Url
            SeedSource.MedicationList, S.SeedSource.MedicationList
            SeedSource.Formulary, S.SeedSource.Formulary
            SeedSource.Parenteralia, S.SeedSource.Parenteralia
            SeedSource.Reload, S.SeedSource.Reload
        ]

    // the filter's own choices, other choices, and none at all
    let choices (f: Filter) =
        [
            "the same", (f.Indication, f.Generic, f.Route, f.Form, f.DoseType)
            "others", (Some "sedatie", Some "morfine", Some "INTRAVENEUS", Some "drank", Some options.DoseTypes[1])
            "none", (None, None, None, None, None)
        ]

    testList
        "the domain's seeds and Shared's"
        [
            for fname, filter in filters do
                let ctx = { context filter with Scenarios = [||] }
                let shared = ctx |> Contract.context OrderCategory.Drug

                for source, sharedSource in sources do
                    for cname, (ind, gen, rte, frm, dt) in choices filter do
                        test $"%s{fname}, %A{source}, %s{cname}" {
                            sameFilter
                                (ctx |> OrderContext.seedFilter source ind gen rte frm dt |> Ok)
                                (shared
                                 |> SCtx.seedFilter sharedSource ind gen rte frm (dt |> Option.map Contract.doseType)
                                 |> Ok)
                        }
        ]
