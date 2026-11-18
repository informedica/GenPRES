namespace Informedica.GenCore.Lib


module Validus =

    open System
    open Validus


    let map f validator = fun s x -> validator s x |> Result.map f


    let mapOpt f validator = fun s x -> validator s x |> Result.map (Option.map f)


    module Validators =

        open Microsoft.FSharp.Core

        let dateValidator =
            let msg = sprintf "pleas provide a valid date %s"

            let rule (y: int<year>, m: int<month>, d: int<day>) =
                let y = y |> int
                let m = m |> int
                let d = d |> int

                try
                    DateTime(y, m, d) |> ignore
                    true
                with _ ->
                    false

            Validator.create msg rule


        /// Validates that a date is set and is not after today. Only the dates are compared, not the time of day.
        /// DateTime.MinValue counts as a date that is not set.
        let notAfterValidator (today: DateTime) =
            let rule (dt: DateTime) = dt <> DateTime.MinValue && dt.Date <= today.Date

            Validator.create (fun field -> $"%s{field} is missing or is after the date it is read on") rule
