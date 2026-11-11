namespace Informedica.GenPRES.Client.Core.Policies

/// The rows of the composition of a nutrition order: the components the order holds, each additive
/// the rules offer, checked or not, and the diluent; and the command a checkbox of an additive sends.
module CompositionPolicy =

    open Informedica.GenPRES.Shared.Api


    /// A row of the composition.
    [<RequireQualifiedAccess>]
    type Row =
        /// A component the prescriber cannot leave out: the main component, or a component that is not
        /// an additive.
        | Fixed of name: string
        /// An additive, and whether the order holds it.
        | Additive of name: string * inOrder: bool
        /// The diluent, with the diluents to choose from; none when the rule offers this one only.
        | Diluent of name: string * choices: string[]


    /// One row per component of the dose rule, in the order of the rule: an additive with whether the order
    /// holds it, or a component the prescriber cannot leave out; then the diluent of the order, with the
    /// diluents to choose from when there is more than one.
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


    /// The command a checkbox of an additive sends: the positions in the offered additives of the
    /// additives the order holds, with the one given switched on or off.
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
