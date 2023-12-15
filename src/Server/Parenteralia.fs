module Parenteralia


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib

type Parenteralia = Shared.Types.Parenteralia


let get (par : Parenteralia) =
    ConsoleWriter.writeInfoMessage $"{par}" true false

    { par with
        Markdown =
            SolutionRule.get ()
            |> SolutionRule.filter
                { Filter.filter with
                    Generic = par.Generic
                    Shape = par.Shape
                    Route = par.Route
                }
            |> SolutionRule.Print.toMarkdown ""
    }
    |> Ok