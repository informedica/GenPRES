/// A value the client asks the server for, as the pages read it: not asked yet, asked with
/// nothing to show meanwhile, asked with a value shown meanwhile, or answered.
[<AutoOpen>]
module Deferred

/// Type that represents data which is loaded from an external source. Initially the process of
/// retrieving that data is `HasNotStartedYet`, then when data is loading, the state should become
/// `InProgress`. After some delay the data becomes available in the `Resolved` state. A value
/// shown that the server has not confirmed is `Provisional`: the previous one while a field
/// reloads, the one sent while a request is under way, a seed not yet evaluated; which one a
/// lane shows is the lane's decision, made in its projection.
type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Provisional of 't
    | Resolved of 't


/// Utility functions around `Deferred<'T>` types.
module Deferred =

    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Provisional value -> Provisional(transform value)
        | Resolved value -> Resolved(transform value)


    /// Whether a request is under way: with or without a value shown meanwhile.
    let inProgress =
        function
        | HasNotStartedYet -> false
        | InProgress -> true
        | Provisional _ -> true
        | Resolved _ -> false


    /// Like `map` but instead of transforming just the value into another type in the `Resolved`
    /// case, it will transform the value into potentially a different case of the `Deferred<'T>`
    /// type.
    let bind (transform: 'T -> Deferred<'U>) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Provisional value -> transform value
        | Resolved value -> transform value


    let defaultValue defVal =
        function
        | HasNotStartedYet
        | InProgress -> defVal
        | Provisional value -> value
        | Resolved value -> value


    let toOption =
        function
        | HasNotStartedYet
        | InProgress -> None
        | Provisional value -> Some value
        | Resolved value -> Some value
