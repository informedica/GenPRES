/// Whether a section of order fields is open or folded: folded once every value it shows holds
/// one value, open while one is still to be chosen, and the user's to toggle in between.
module SectionFoldPolicy

open Shared.Types
open Shared.Models


/// The fold of a section: what the last answer said about its values, and the user's last
/// toggle since that changed.
type Fold =
    {
        /// Whether every variable the section shows held one value at the last answer.
        Solved: bool
        /// The user's last toggle since the solved state last changed; None follows the rule.
        Override: bool option
    }


/// Whether every variable holds one value. An empty section counts as solved: there is nothing
/// in it left to choose.
let allSolved (ovars: OrderVariable seq) = ovars |> Seq.forall Order.OrderVariable.isSolved


/// The fold of a section as first shown: it follows the rule.
let initial solved =
    {
        Solved = solved
        Override = None
    }


/// An answer of the server. A change of the solved state drops the user's toggle, so the
/// section follows the rule again; no change keeps the toggle.
let observe solved fold = if solved = fold.Solved then fold else initial solved


/// Open while a value is to be chosen, folded once all are solved, unless the user said
/// otherwise since.
let isOpen fold = fold.Override |> Option.defaultValue (not fold.Solved)


/// The user opens a folded section or folds an open one.
let toggle fold = { fold with Override = Some(not (isOpen fold)) }
