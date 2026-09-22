/// One value the client asks the server for, as the pages read it: not asked yet, asked with
/// nothing to show meanwhile, answered, or asked again with the previous answer kept and shown
/// meanwhile. The reading of a plain fetch, the formulary or the log files; the order lanes,
/// which show a value while a request is under way, have their own view types in the machines.
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


/// Utility functions around `Deferred<'T>` types.
module Deferred =

    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> Resolved(transform value)
        | Refreshing value -> Refreshing(transform value)


    /// Like `map` but instead of transforming just the value into another type in the `Resolved`
    /// case, it will transform the value into potentially a different case of the `Deferred<'T>`
    /// type. A value kept while a fetch runs again stays kept: an answer of the function over it
    /// is Refreshing too.
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
