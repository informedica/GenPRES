namespace Informedica.GenPRES.Client.Core.Policies

/// Builds the composition rows of a nutrition order and the command that an additive checkbox sends.
module CompositionPolicy =

    open Informedica.GenPRES.Shared.Api


    /// A row of the composition.
    [<RequireQualifiedAccess>]
    type Row =
        /// A component the prescriber cannot leave out: the main component, or a component that is not
        /// an additive.
        | Fixed of name: string
        /// An additive, and whether the order contains it.
        | Additive of name: string * inOrder: bool
        /// The diluent, with the diluents to choose from; empty when the rule offers only this diluent.
        | Diluent of name: string * choices: string[]


    /// Returns one row per dose rule component, in rule order: an additive with whether the order contains it,
    /// or a fixed component. Adds a diluent row when the order contains the diluent, with the diluents to
    /// choose from when there is more than one.
    let rows doseRuleComponents orderComponents additives diluent diluents =
        let body =
            doseRuleComponents
            |> Array.map (fun name ->
                if additives |> Array.contains name then
                    Row.Additive(name, orderComponents |> Array.contains name)
                else
                    Row.Fixed name
            )

        let diluentRow =
            match diluent with
            | Some d when orderComponents |> Array.contains d ->
                let choices = if diluents |> Array.length > 1 then diluents else [||]
                [| Row.Diluent(d, choices) |]
            | _ -> [||]

        Array.append body diluentRow


    /// Returns the command for an additive checkbox: the indices, in the offered additives, of the checked
    /// additives after the given additive is switched on or off.
    let toggle (additives: string[]) (rows: Row[]) name =
        let selected =
            rows
            |> Array.choose (
                function
                | Row.Additive(n, true) -> Some n
                | _ -> None
            )

        let next =
            if selected |> Array.contains name then
                selected |> Array.filter ((<>) name)
            else
                selected |> Array.append [| name |]

        additives
        |> Array.indexed
        |> Array.choose (fun (i, a) -> if next |> Array.contains a then Some i else None)
        |> OrderViewCommand.SetNthComponentsProperty
