namespace Informedica.GenPRES.Client.Core.Policies

/// Decides per intent case, a command of the order view, whether it is allowed on the order shown.
/// It reads only the wire order: the values a variable holds, its defined constraints and the
/// dose type the schedule flags give.
module IntentPolicy =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Shared.Models.OrderContext


    /// The number of values an order variable holds; 0 for a range or nothing.
    let valuesOf (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (_.Value >> Array.length)
        |> Option.defaultValue 0


    /// Whether the rules constrain an order variable. Constraints that are empty, or a minimum of
    /// zero alone, which only says the value is above zero, do not count.
    let hasConstraints (ovar: OrderVariable) =
        let c = ovar.DefinedConstraints

        let isEmpty = c.Min.IsNone && c.Incr.IsNone && c.Max.IsNone && c.Vals.IsNone

        let isAboveZeroAlone =
            c.Incr.IsNone
            && c.Max.IsNone
            && c.Vals.IsNone
            && c.Min
               |> Option.exists (fun m -> not c.MinIncl && m.Value |> Array.forall (snd >> (=) 0m))

        not isEmpty && not isAboveZeroAlone


    /// The eight variables of a dose.
    let doseVariables (dos: Dose) =
        [
            dos.Quantity
            dos.PerTime
            dos.Rate
            dos.Total
            dos.QuantityAdjust
            dos.PerTimeAdjust
            dos.RateAdjust
            dos.TotalAdjust
        ]


    /// Whether the rules constrain the dose of a component: its own dose or the dose of one of its
    /// items. The doses of a component the rules do not constrain only follow from the others.
    let isDoseConstrained (cmp: Component) =
        cmp.Dose :: (cmp.Items |> Array.map _.Dose |> Array.toList)
        |> List.collect doseVariables
        |> List.exists hasConstraints


    /// What a recalculation recalculates, which decides the dose types it is allowed on.
    [<RequireQualifiedAccess>]
    type Kind =
        /// The dose quantity: on Once, Discontinuous, Timed and OnceTimed.
        | Dose
        /// The dose per time: on Discontinuous and Timed.
        | PerTime
        /// The time and the dose rate: on Continuous, Timed and OnceTimed.
        | Time


    /// Whether a recalculation of this kind is allowed on an order with this schedule.
    let isKindAllowed kind (sch: Schedule) =
        match kind with
        | Kind.Dose -> sch.IsOnce || sch.IsDiscontinuous || sch.IsTimed || sch.IsOnceTimed
        | Kind.PerTime -> sch.IsDiscontinuous || sch.IsTimed
        | Kind.Time -> sch.IsContinuous || sch.IsTimed || sch.IsOnceTimed


    /// The kind of a recalculation of an item property; None for the properties that are never
    /// recalculated: the orderable quantity and concentration, which have no kind, and the
    /// concentration in the component, a constant of the product.
    let itemKind prop =
        match prop with
        | ItemProperty.DoseQuantity
        | ItemProperty.DoseQuantityAdjust -> Some Kind.Dose
        | ItemProperty.DosePerTime
        | ItemProperty.DosePerTimeAdjust -> Some Kind.PerTime
        | ItemProperty.DoseRate
        | ItemProperty.DoseRateAdjust -> Some Kind.Time
        | ItemProperty.ComponentConcentration
        | ItemProperty.OrderableConcentration
        | ItemProperty.OrderableQuantity -> None


    /// The component of the order with this name.
    let tryComponent name (ord: Order) =
        ord.Orderable.Components |> Array.tryFind (fun c -> c.Name = name)


    /// Whether a recalculation of the target is allowed on the order: the target holds one value
    /// and the target is a field a recalculation can recalculate on the order's dose type. An item
    /// dose also needs a component dose the rules constrain. The basic variables, the frequency,
    /// the orderable dose quantity and rate and a component quantity, are stepped or selected,
    /// never recalculated, and neither is the orderable quantity or a component's adjusted dose.
    let isReCalcAllowed target (ord: Order) =
        let holdsOne = ord |> Target.tryGet target |> Option.exists (fun ovar -> valuesOf ovar = 1)

        let isAllowed =
            match target with
            | Target.Schedule ScheduleProperty.Time -> ord.Schedule |> isKindAllowed Kind.Time
            | Target.Item(cmp, _, prop) ->
                match itemKind prop with
                | Some kind ->
                    ord.Schedule |> isKindAllowed kind
                    && ord |> tryComponent cmp |> Option.exists isDoseConstrained
                | None -> false
            | Target.Schedule ScheduleProperty.Frequency
            | Target.Orderable _
            | Target.Component _ -> false

        holdsOne && isAllowed


    /// The target of a recalculation command; None for any other command.
    let reCalcTarget cmd =
        match cmd with
        | OrderViewCommand.ReCalcScheduleProperty prop -> Some(Target.Schedule prop)
        | OrderViewCommand.ReCalcOrderableProperty prop -> Some(Target.Orderable prop)
        | OrderViewCommand.ReCalcComponentProperty(cmp, prop) -> Some(Target.Component(cmp, prop))
        | OrderViewCommand.ReCalcItemProperty(cmp, itm, prop) -> Some(Target.Item(cmp, itm, prop))
        | _ -> None


    /// Whether the command is a recalculation, after which a list closed without a pick puts the
    /// state before it back.
    let reopens cmd = cmd |> reCalcTarget |> Option.isSome


    /// Every variable of the order as a target: the schedule, the orderable, and each component
    /// and each item by name.
    let targets (ord: Order) =
        [
            Target.Schedule ScheduleProperty.Frequency
            Target.Schedule ScheduleProperty.Time
            Target.Orderable OrderableProperty.Quantity
            Target.Orderable OrderableProperty.DoseQuantity
            Target.Orderable OrderableProperty.DoseRate
            for cmp in ord.Orderable.Components do
                Target.Component(cmp.Name, ComponentProperty.OrderableQuantity)
                Target.Component(cmp.Name, ComponentProperty.DoseQuantityAdjust)

                for itm in cmp.Items do
                    for prop in
                        [
                            ItemProperty.DoseQuantity
                            ItemProperty.DoseQuantityAdjust
                            ItemProperty.DosePerTime
                            ItemProperty.DosePerTimeAdjust
                            ItemProperty.DoseRate
                            ItemProperty.DoseRateAdjust
                            ItemProperty.ComponentConcentration
                            ItemProperty.OrderableConcentration
                            ItemProperty.OrderableQuantity
                        ] do
                        Target.Item(cmp.Name, itm.Name, prop)
        ]


    /// Whether a recalculation of the variable with this name is allowed on the order; false when
    /// the order holds no variable of the name.
    let isReCalcAllowedOf name (ord: Order) =
        ord
        |> targets
        |> List.tryFind (fun t -> ord |> Target.tryGet t |> Option.exists (fun v -> v.Name = name))
        |> Option.exists (fun t -> ord |> isReCalcAllowed t)
