/// The domain's value commands and today's path give the same order: the wire order changed by
/// Shared, mapped to the domain, then solved or reopened.
module Informedica.GenPRES.Server.Tests.OrderValueAgreementTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib
open Informedica.GenOrder.Lib

module Ctx = Shared.Models.OrderContext


/// Today's path, through Shared and the server's order mapper.
module Today =

    let toDomain (ord: Shared.Types.Order) =
        ord
        |> ServerApi.Mappers.Order.mapFromSharedToOrder
        |> Order.Dto.fromDto
        |> Result.get


    let toShared items (ord: Order) =
        ord |> Order.Dto.toDto |> ServerApi.Mappers.Order.mapFromOrderToShared items


    /// The property commands for a wire target: the nth value and the clear, as the server will map
    /// the wire's specific commands.
    let toCommands (t: Ctx.Target) =
        match t with
        | Ctx.Target.Schedule Shared.Types.ScheduleProperty.Frequency -> SetNthScheduleFrequency, ClearScheduleFrequency
        | Ctx.Target.Schedule Shared.Types.ScheduleProperty.Time -> SetNthScheduleTime, ClearScheduleTime
        | Ctx.Target.Orderable Shared.Types.OrderableProperty.Quantity ->
            SetNthOrderableQuantity, ClearOrderableQuantity
        | Ctx.Target.Orderable Shared.Types.OrderableProperty.DoseQuantity ->
            SetNthOrderableDoseQuantity, ClearOrderableDoseQuantity
        | Ctx.Target.Orderable Shared.Types.OrderableProperty.DoseRate ->
            SetNthOrderableDoseRate, ClearOrderableDoseRate
        | Ctx.Target.Component(c, Shared.Types.ComponentProperty.OrderableQuantity) ->
            (fun n -> SetNthComponentOrderableQuantity(c, n)), ClearComponentOrderableQuantity c
        | Ctx.Target.Component(c, Shared.Types.ComponentProperty.DoseQuantityAdjust) ->
            (fun n -> SetNthComponentDoseQuantityAdjust(c, n)), ClearComponentDoseQuantityAdjust c
        | Ctx.Target.Item(c, i, p) ->
            match p with
            | Shared.Types.ItemProperty.DoseQuantity ->
                (fun n -> SetNthItemDoseQuantity(c, i, n)), ClearItemDoseQuantity(c, i)
            | Shared.Types.ItemProperty.DoseQuantityAdjust ->
                (fun n -> SetNthItemDoseQuantityAdjust(c, i, n)), ClearItemDoseQuantityAdjust(c, i)
            | Shared.Types.ItemProperty.DosePerTime ->
                (fun n -> SetNthItemDosePerTime(c, i, n)), ClearItemDosePerTime(c, i)
            | Shared.Types.ItemProperty.DosePerTimeAdjust ->
                (fun n -> SetNthItemDosePerTimeAdjust(c, i, n)), ClearItemDosePerTimeAdjust(c, i)
            | Shared.Types.ItemProperty.DoseRate -> (fun n -> SetNthItemDoseRate(c, i, n)), ClearItemDoseRate(c, i)
            | Shared.Types.ItemProperty.DoseRateAdjust ->
                (fun n -> SetNthItemDoseRateAdjust(c, i, n)), ClearItemDoseRateAdjust(c, i)
            | Shared.Types.ItemProperty.ComponentConcentration ->
                (fun n -> SetNthItemComponentConcentration(c, i, n)), ClearItemComponentConcentration(c, i)
            | Shared.Types.ItemProperty.OrderableConcentration ->
                (fun n -> SetNthItemOrderableConcentration(c, i, n)), ClearItemOrderableConcentration(c, i)
            | Shared.Types.ItemProperty.OrderableQuantity ->
                (fun n -> SetNthItemOrderableQuantity(c, i, n)), ClearItemOrderableQuantity(c, i)


    let private run ocmd =
        ocmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> Result.mapError (fun (_, msgs) -> $"%A{msgs}")


    /// Today's pick: the key at the index set on the wire order, which is then solved.
    let setNth t n (shared: Shared.Types.Order) =
        match shared |> Ctx.Target.tryGet t with
        | None -> Error "no target"
        | Some ovar ->
            match Ctx.values ovar with
            | [||] -> Ok(shared |> toDomain)
            | keys when n >= keys.Length -> Error "out of range"
            | keys ->
                shared
                |> Ctx.Target.map t (Shared.Models.Order.OrderVariable.setOvar (Some keys[n]))
                |> toDomain
                |> SolveOrder
                |> run


    /// Today's clear: the variable cleared on the wire order, which is then reopened.
    let clear t picks (shared: Shared.Types.Order) =
        match shared |> Ctx.Target.tryGet t with
        | None -> Error "no target"
        | Some _ ->
            (shared
             |> Ctx.Target.map t (Shared.Models.Order.OrderVariable.setOvar None)
             |> toDomain,
             picks)
            |> Reopen
            |> run


/// The domain's path: the property command applied, then solved, or reopened after a clear.
module Domain =

    let private run cmd =
        cmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> Result.mapError (fun (_, msgs) -> $"%A{msgs}")


    let setNth cmd ord =
        ord
        |> OrderProcessor.processChangeProperty OrderLogging.noOp cmd
        |> SolveOrder
        |> run


    let clear cmd picks ord =
        (ord |> OrderProcessor.processChangeProperty OrderLogging.noOp cmd, picks)
        |> Reopen
        |> run


/// The fixed medications as the dialog first shows them, on the wire and in the domain.
module Fixtures =

    let solve cmd ord =
        ord
        |> cmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> Result.defaultWith (fun (_, msgs) -> failtest $"not solved: %A{msgs}")


    /// The item names, the wire order, and the domain order mapped back from it, as the server
    /// receives it.
    let built (med: Medication) =
        let ord =
            med
            |> Medication.toOrder Scenarios.testStart
            |> Result.get
            |> solve CalcMinMax
            |> solve CalcValues

        let items =
            ord.Orderable.Components
            |> List.collect (fun c -> c.Items |> List.map (fun i -> i.Name |> Name.toString))
            |> List.distinct
            |> List.toArray

        let shared = ord |> Today.toShared items
        items, shared, shared |> Today.toDomain


    let all =
        lazy
            [
                "paracetamol rectal", built Scenarios.pcmSupp
                "paracetamol drink", built Scenarios.pcmDrink
                "morfine continuous", built Scenarios.morfCont
                "amfotericine", built Scenarios.amfo
                "cotrimoxazol", built Scenarios.cotrim
                "parenteral nutrition", built Scenarios.tpn
            ]


    /// Every wire target the order holds.
    let targets (ord: Shared.Types.Order) =
        [
            Ctx.Target.Schedule Shared.Types.ScheduleProperty.Frequency
            Ctx.Target.Schedule Shared.Types.ScheduleProperty.Time
            Ctx.Target.Orderable Shared.Types.OrderableProperty.Quantity
            Ctx.Target.Orderable Shared.Types.OrderableProperty.DoseQuantity
            Ctx.Target.Orderable Shared.Types.OrderableProperty.DoseRate
            for c in ord.Orderable.Components do
                Ctx.Target.Component(c.Name, Shared.Types.ComponentProperty.OrderableQuantity)
                Ctx.Target.Component(c.Name, Shared.Types.ComponentProperty.DoseQuantityAdjust)

                for i in c.Items do
                    for prop in
                        [
                            Shared.Types.ItemProperty.DoseQuantity
                            Shared.Types.ItemProperty.DoseQuantityAdjust
                            Shared.Types.ItemProperty.DosePerTime
                            Shared.Types.ItemProperty.DosePerTimeAdjust
                            Shared.Types.ItemProperty.DoseRate
                            Shared.Types.ItemProperty.DoseRateAdjust
                            Shared.Types.ItemProperty.ComponentConcentration
                            Shared.Types.ItemProperty.OrderableConcentration
                            Shared.Types.ItemProperty.OrderableQuantity
                        ] do
                        Ctx.Target.Item(c.Name, i.Name, prop)
        ]
        |> List.distinct


    /// The number of values the wire offers for the target, if the order holds it.
    let count t ord =
        ord |> Ctx.Target.tryGet t |> Option.map (Ctx.values >> Array.length)


    /// The first, the middle and the last index, and one past the last.
    let indexes c = [ 0; c / 2; c - 1; c ] |> List.distinct


/// Whether today's client changes more than the one variable: a component concentration is also
/// changed in the first component, when that is another component holding the item.
let fansOut (ord: Shared.Types.Order) t =
    match t, ord.Orderable.Components |> Array.toList with
    | Ctx.Target.Item(cmp, itm, Shared.Types.ItemProperty.ComponentConcentration), first :: _ ->
        first.Name <> cmp && first.Items |> Array.exists (fun i -> i.Name = itm)
    | _ -> false


/// Both answers in the wire's shape: the same order, or both refused.
let same items (domain: Result<Order, string>) (today: Result<Order, string>) =
    match domain, today with
    | Ok d, Ok t ->
        d
        |> Today.toShared items
        |> Expect.equal "the same order" (t |> Today.toShared items)
    | Error _, Error _ -> ()
    | d, t -> failtest $"one answers, the other refuses: %A{d |> Result.map ignore} %A{t |> Result.map ignore}"


[<Tests>]
let tests =
    testList
        "the domain's value commands and today's path"
        [
            testList
                "a pick"
                [
                    for name, (items, shared, ord) in Fixtures.all.Value do
                        for t in Fixtures.targets shared do
                            match shared |> Fixtures.count t with
                            | Some c when c > 1 && not (fansOut shared t) ->
                                let setNth, _ = Today.toCommands t

                                for n in Fixtures.indexes c |> List.filter (fun n -> n < c) do
                                    test $"%s{name}, %A{t} %i{n}" {
                                        same items (ord |> Domain.setNth (setNth n)) (shared |> Today.setNth t n)
                                    }
                            | _ -> ()
                ]

            testList
                "a clear"
                [
                    for name, (items, shared, ord) in Fixtures.all.Value do
                        for t in Fixtures.targets shared do
                            match shared |> Fixtures.count t with
                            | Some c when c > 1 && not (fansOut shared t) ->
                                let _, clear = Today.toCommands t

                                test $"%s{name}, %A{t}" {
                                    same items (ord |> Domain.clear clear []) (shared |> Today.clear t [])
                                }
                            | _ -> ()
                ]

            // the nutrition's natrium and kalium concentrations are changed in its first component
            // as well by today's client, but offer one value, so there is nothing to pick
            test "the fixtures hold no pick today's client sets twice" {
                [
                    for name, (_, shared, _) in Fixtures.all.Value do
                        for t in Fixtures.targets shared do
                            if fansOut shared t && shared |> Fixtures.count t > Some 1 then
                                name, t
                ]
                |> Expect.isEmpty "every pick of the fixtures sets one variable, today as well"
            }
        ]
