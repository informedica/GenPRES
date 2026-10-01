/// The picks the workbench dialog keeps: the variables the user picked or stepped, in the order
/// picked. A field reads from them whether the user constrained its variable.
module PickList

open Shared.Types

/// The number of values an order variable holds; 0 for a range or nothing.
let valuesOf (ovar: OrderVariable) =
    ovar.Variable.Vals
    |> Option.map (_.Value >> Array.length)
    |> Option.defaultValue 0


/// The names of the variables a change picked: narrowed by it to one value. A variable the
/// change cleared, or left as it was, is no pick.
let picked (before: Order) (after: Order) =
    let was =
        ArgumentationPolicy.variables before
        |> List.map (fun v -> v.Name, v.Variable)
        |> Map.ofList

    ArgumentationPolicy.variables after
    |> List.filter (fun v ->
        valuesOf v = 1
        && not v.Variable.IsNonZeroPositive
        && was |> Map.tryFind v.Name <> Some v.Variable
    )
    |> List.map _.Name


/// The picks with this name the latest: moved to the end when it was picked before.
let add (name: string) (picks: string[]) = Array.append (picks |> Array.filter ((<>) name)) [| name |]


/// The picks after a change from the dialog: every variable it picked added, in the order the
/// order lists them. Unknown picks stay unknown: outside the workbench there are none.
let afterChange (before: Order) (after: Order) (picks: string[] option) =
    picks
    |> Option.map (fun ps -> picked before after |> List.fold (fun ps n -> ps |> add n) ps)


/// The picks after a step of the variable with this name: the latest pick when the step moved it.
/// A step at a bound leaves the variable, and the picks, as they were. Unknown picks stay unknown.
let afterStep name (before: Order) (after: Order) picks =
    let variableOf ord =
        ArgumentationPolicy.variables ord
        |> List.tryFind (fun v -> v.Name = name)
        |> Option.map _.Variable

    picks
    |> Option.map (fun ps ->
        if variableOf after <> variableOf before then
            ps |> add name
        else
            ps
    )


/// The picks a reopen of the variable with this name keeps: those made before it.
let beforeReopen name picks = picks |> Option.map (Array.takeWhile ((<>) name))


/// Whether the user constrained the variable with this name, as the field decision asks it.
let constrained (picks: string[] option) (name: string) =
    match picks with
    | None -> FieldOpenPolicy.Constrained.Unknown
    | Some ps when ps |> Array.contains name -> FieldOpenPolicy.Constrained.Yes
    | Some _ -> FieldOpenPolicy.Constrained.No
