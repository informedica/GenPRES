namespace Informedica.GenPRES.Client.Core.Helpers

/// Models a server response the way the page shows it: not requested yet, loading, loaded, or
/// reloading while the last response stays on screen.
[<AutoOpen>]
module Deferred =

    /// A value fetched from the server. Pages render Resolved and Refreshing alike, so the screen
    /// stays filled during a reload, and act on Resolved only.
    type Deferred<'t> =
        /// Not requested yet.
        | HasNotStartedYet
        /// Requested, with nothing to show yet.
        | InProgress
        /// Loaded.
        | Resolved of 't
        /// Requested again, with the last value still shown.
        | Refreshing of 't


    /// Functions over Deferred values.
    module Deferred =

        /// Transforms the value, in whichever state holds one.
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


        /// The value, or the default while there is none.
        let defaultValue defVal =
            function
            | HasNotStartedYet
            | InProgress -> defVal
            | Resolved value
            | Refreshing value -> value


        /// The value, if there is one.
        let toOption =
            function
            | HasNotStartedYet
            | InProgress -> None
            | Resolved value
            | Refreshing value -> Some value


        /// Starts a reload: Refreshing when there is a value to keep showing, InProgress otherwise.
        let refresh =
            function
            | Resolved value
            | Refreshing value -> Refreshing value
            | HasNotStartedYet
            | InProgress -> InProgress
