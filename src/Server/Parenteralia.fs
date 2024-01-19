module Parenteralia


open Informedica.Utils.Lib
open Informedica.GenForm.Lib

type Parenteralia = Shared.Types.Parenteralia

module Api = Informedica.GenOrder.Lib.Api


let get (par : Parenteralia) : Result<Parenteralia, string> =
    ConsoleWriter.writeInfoMessage $"getting parenteralia for {par.Generic}" true false

    let srs =
        Api.getSolutionRules
            par.Generic
            par.Shape
            par.Route


    { par with
        Generics = srs |> SolutionRule.generics
        Shapes = srs |> SolutionRule.shapes
        Routes = srs |> SolutionRule.routes
        Markdown =
            if par.Generic |> Option.isNone then ""
            else
                srs
                |> SolutionRule.Print.toMarkdown ""
    }
    |> Ok