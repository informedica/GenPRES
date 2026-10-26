namespace Informedica.GenPRES.Client.Core.Policies


/// Decides which items of the totals changed since they were last shown, so the view can light
/// them up. Totals from another source, such as another patient, never count as a change.
module TotalsChangePolicy =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// The totals last shown and where they came from.
    type Seen =
        {
            /// What the totals are of, such as the plan of one patient.
            Source: string
            /// The totals as last shown.
            Totals: Totals
        }


    /// The names of the shown items whose value differs between two totals. An item with fewer
    /// than two text items is not shown, so an item that disappeared is not listed.
    let changedItems previous current =
        TotalsDisplay.intakeRows
        |> Array.map Array.head
        |> Array.filter (fun name ->
            let now = TotalsDisplay.substanceToField current name

            Array.length now >= 2 && now <> TotalsDisplay.substanceToField previous name
        )


    /// The totals as first shown: nothing changed yet.
    let initial source totals =
        {
            Source = source
            Totals = totals
        }


    /// The totals after they are shown again, with the names of the changed items; none when the
    /// source changed.
    let observe source totals seen =
        let changed =
            if source = seen.Source then
                changedItems seen.Totals totals
            else
                [||]

        initial source totals, changed
