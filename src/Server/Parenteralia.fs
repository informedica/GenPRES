module Parenteralia


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
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
        Generics = srs |> Array.map _.Generic |> Array.distinct
        Shapes = srs |> Array.map _.Shape |> Array.distinct
        Routes = srs |> Array.map _.Route |> Array.distinct
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