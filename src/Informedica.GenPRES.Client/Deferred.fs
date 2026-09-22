/// One value the client asks the server for, as the pages read it: not asked yet, asked with
/// nothing to show meanwhile, or answered. The reading of a plain fetch, the formulary or the
/// log files; the order lanes, which show a value while a request is under way, have their own
/// view types in the machines.
[<AutoOpen>]
module Deferred

/// A value the server is asked for, in the three states a page can find it in.
type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Resolved of 't


/// Utility functions around `Deferred<'T>` types.
module Deferred =

    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> Resolved(transform value)


    /// Like `map` but instead of transforming just the value into another type in the `Resolved`
    /// case, it will transform the value into potentially a different case of the `Deferred<'T>`
    /// type.
    let bind (transform: 'T -> Deferred<'U>) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> transform value


    let defaultValue defVal =
        function
        | HasNotStartedYet
        | InProgress -> defVal
        | Resolved value -> value


    let toOption =
        function
        | HasNotStartedYet
        | InProgress -> None
        | Resolved value -> Some value
