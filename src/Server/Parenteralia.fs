module Parenteralia


open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
open Informedica.GenForm.Lib

type Parenteralia = Shared.Types.Parenteralia

module Api = Informedica.GenOrder.Lib.Api


let get (par : Parenteralia) : Result<Parenteralia, string> =
    writeInfoMessage $"getting parenteralia for {par.Generic}"

    let srs =
        Api.getSolutionRules
            par.Generic
            par.Shape
            par.Route

    let gens = srs |> SolutionRule.generics
    let shps = srs |> SolutionRule.shapes
    let rtes = srs |> SolutionRule.routes

    { par with
        Generics = gens
        Shapes = shps
        Routes = rtes
        Generic =
            if gens |> Array.length = 1 then Some gens[0]
            else
                par.Generic
        Shape =
            if shps |> Array.length = 1 then Some shps[0]
            else
                par.Shape
        Route =
            if rtes |> Array.length = 1 then Some rtes[0]
            else
                par.Route

        Markdown =
            if par.Generic |> Option.isNone then ""
            else
                srs
                |> SolutionRule.Print.toMarkdown ""
    }
    |> Ok