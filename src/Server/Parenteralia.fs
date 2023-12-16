module Parenteralia


open Informedica.Utils.Lib
open Informedica.GenForm.Lib

type Parenteralia = Shared.Types.Parenteralia


let get (par : Parenteralia) : Result<Parenteralia, string> =
    ConsoleWriter.writeInfoMessage $"getting parenteralia for {par.Generic}" true false

    let srs =
        SolutionRule.get ()
        |> SolutionRule.filter
            { Filter.filter with
                Generic = par.Generic
                Shape = par.Shape
                Route = par.Route
            }

    { par with
        Generics = srs |> SolutionRule.generics
        Shapes = srs |> SolutionRule.shapes
        Routes = srs |> SolutionRule.routes
        Markdown =
            if par.Generic |> Option.isNone then ""
            else
                srs
                |> SolutionRule.filter
                    { Filter.filter with
                        Generic = par.Generic
                        Shape = par.Shape
                        Route = par.Route
                    }
                |> SolutionRule.Print.toMarkdown ""
    }
    |> Ok