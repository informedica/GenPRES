namespace Informedica.GenPRES.Client.Core.Policies

/// Decides whether an order view command is allowed on the shown order. It reads only the wire order:
/// the values of a variable, its defined constraints, and the dose type from the schedule flags.
module IntentPolicy =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Shared.Models.OrderContext


    /// The number of values of an order variable; 0 for a range or no values.
    let valuesOf (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (_.Value >> Array.length)
        |> Option.defaultValue 0


    /// Whether the rules constrain an order variable. Empty constraints, or only an exclusive minimum of
    /// zero, do not count.
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


    /// Whether a recalculation of the target is allowed on the order. The target must have a single value
    /// and be the schedule time or an item dose that the dose type allows. An item dose also needs a
    /// component whose dose the rules constrain. The frequency and the orderable and component properties
    /// are stepped or selected, never recalculated.
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


    /// Whether the command is a recalculation. Closing its list without a pick restores the previous
    /// state.
    let isReCalc cmd = cmd |> reCalcTarget |> Option.isSome
