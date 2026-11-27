namespace Informedica.GenPRES.Client.Core.Policies


/// Decides whether a section of order fields is open or folded: open while a value is still to be
/// chosen, folded once all are, and the user can toggle it in between.
module SectionFoldPolicy =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// Whether a section is folded: the solved state at the last server answer, and the user's toggle since.
    type Fold =
        {
            /// Whether every variable in the section had a single value at the last server answer.
            Solved: bool
            /// The user's last toggle since Solved changed; None uses the default, open until solved.
            Override: bool option
        }


    /// Whether every variable has a single value. An empty section counts as solved.
    let allSolved (ovars: OrderVariable seq) = ovars |> Seq.forall OrderDisplay.OrderVariable.isSolved


    /// The fold of a section when first shown, without a user toggle.
    let initial solved =
        {
            Solved = solved
            Override = None
        }


    /// The fold after a server answer: when Solved changes, the user's toggle is dropped and the
    /// default applies again.
    let observe solved fold = if solved = fold.Solved then fold else initial solved


    /// Whether the section is open: while a value is still to be chosen, unless the user toggled it.
    let isOpen fold = fold.Override |> Option.defaultValue (not fold.Solved)


    /// The fold after the user opens or folds the section.
    let toggle fold = { fold with Override = Some(not (isOpen fold)) }
