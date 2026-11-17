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


        /// A date that is given and is not after a calendar date; the time of day of either date is
        /// not looked at. A date left at its initial value is not given.
        let notAfterValidator (today: DateTime) =
            let rule (dt: DateTime) = dt <> DateTime.MinValue && dt.Date <= today.Date

            Validator.create (fun field -> $"%s{field} is missing or is after the date it is read on") rule
