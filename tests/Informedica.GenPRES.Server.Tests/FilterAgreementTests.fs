/// The domain's filter commands and the client's preview in Shared make the same filter, so a pick
/// shows while it runs as the server will answer it.
module Informedica.GenPRES.Server.Tests.FilterAgreementTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib

open FilterFixtures

module S = Shared.Types
module SCtx = Shared.Models.OrderContext
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


/// The domain's change with the option at the index, as Shared's changeFilter picks it.
let changeAt category field (n: int option) (ctx: OrderContext) =
    let at (xs: 'a[]) = n |> Option.map (fun i -> xs[i])

    match field with
    | FilterField.Indication -> ctx |> OrderContext.change category field (at ctx.Filter.Indications) None
    | FilterField.Generic -> ctx |> OrderContext.change category field (at ctx.Filter.Generics) None
    | FilterField.Route -> ctx |> OrderContext.change category field (at ctx.Filter.Routes) None
    | FilterField.Form -> ctx |> OrderContext.change category field (at ctx.Filter.Forms) None
    | FilterField.DoseType -> ctx |> OrderContext.change category field None (at ctx.Filter.DoseTypes)
    | FilterField.Diluent
    | FilterField.Components -> invalidArg (nameof field) "not a field the lookup reads"


[<Tests>]
let tests =
    testList
        "the domain's filter cascade and Shared's"
        [
            for cname, category in categories do
                for fname, filter in filters do
                    for field in OrderContext.filterFields do
                        // Shared's change empties the scenarios, so both start without
                        let ctx = { context filter with Scenarios = [||] }
                        let shared = ctx |> Contract.context category

                        for n in [ None ] @ [ for i in 0 .. count field filter - 1 -> Some i ] do
                            test $"%s{cname}, %s{fname}, %A{field} %A{n}" {
                                sameFilter
                                    (ctx |> changeAt category field n |> Ok)
                                    (shared |> SCtx.changeFilter (Contract.field field) n)
                            }
        ]
