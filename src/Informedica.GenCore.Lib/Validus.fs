namespace Informedica.GenCore.Lib


module Validus =

    open System
    open Validus


    let map f validator =
        fun s x ->
            validator s x
            |> Result.map f


    let mapOpt f validator =
        fun s x ->
            validator s x
            |> Result.map (Option.map f)



    module Validators =

        open Microsoft.FSharp.Core

        let dateValidator =
            let msg = sprintf "pleas provide a valid date %s"
            let rule (y : int<year>, m : int<month> , d : int<day>) =
                let y = y |> int
                let m = m |> int
                let d = d |> int
                try
                    DateTime(y, m, d)
                    |> ignore
                    true
                with
                | _ -> false

            Validator.create msg rule


        let onlyOneOfTwoOpt field value =
            let msg = sprintf "can only be one of two %s"
            let rule (opt1, opt2) =
                match opt1, opt2 with
                | Some _, Some _ -> false
                | _ -> true

            Validator.create msg rule field value

    
