/// Models a server response the way the page shows it: not requested yet, loading, loaded, or
/// reloading while the last response stays on screen.
[<AutoOpen>]
module Deferred

/// A value the server is asked for, in the four states a page can find it in. A page renders
/// from Resolved and Refreshing alike, so that the screen stays populated while a fetch runs
/// again, and acts on Resolved only.
type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Resolved of 't
    | Refreshing of 't


/// Functions over Deferred values.
module Deferred =

    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> Resolved(transform value)
        | Refreshing value -> Refreshing(transform value)


    /// A value kept while a fetch runs again stays kept: a Resolved answer of the function over it
    /// is Refreshing.
    let bind (transform: 'T -> Deferred<'U>) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> transform value
        | Refreshing value ->
            match transform value with
            | Resolved result -> Refreshing result
            | other -> other


    let defaultValue defVal =
        function
        | HasNotStartedYet
        | InProgress -> defVal
        | Resolved value
        | Refreshing value -> value


    let toOption =
        function
        | HasNotStartedYet
        | InProgress -> None
        | Resolved value
        | Refreshing value -> Some value


    /// The fetch asked again: the value kept and shown meanwhile when there is one, nothing to
    /// show otherwise.
    let refresh =
        function
        | Resolved value
        | Refreshing value -> Refreshing value
        | HasNotStartedYet
        | InProgress -> InProgress
