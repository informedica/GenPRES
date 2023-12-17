module Parenteralia


open Informedica.Utils.Lib
open Informedica.GenForm.Lib

type Parenteralia = Shared.Types.Parenteralia


let get (par : Parenteralia) : Result<Parenteralia, string> =
    ConsoleWriter.writeInfoMessage $"getting parenteralia for {par.Generic}" true false

    let srs =
        SolutionRule.get ()
        |> Array.filter (fun sr ->
            par.Generic
            |> Option.map ((=) sr.Generic)
            |> Option.defaultValue true &&
            par.Shape
            |> Option.map ((=) sr.Shape)
            |> Option.defaultValue true &&
            par.Route
            |> Option.map ((=) sr.Route)
            |> Option.defaultValue true
        )

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