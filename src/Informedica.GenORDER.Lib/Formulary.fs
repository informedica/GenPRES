namespace Informedica.GenOrder.Lib


module Formulary =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib


    let getDoseRules provider filter = Api.getDoseRules provider |> Api.filterDoseRules provider filter


    let getSolutionRules provider generic form route =
        Api.getSolutionRules provider
        |> Array.filter (fun solRule ->
            generic
            |> Option.map (String.equalsCapInsens solRule.Generic)
            |> Option.defaultValue true
            && solRule.Form
               |> Option.map (fun s ->
                   if form |> Option.isNone then
                       true
                   else
                       form.Value |> String.equalsCapInsens s
               )
               |> Option.defaultValue true
            && route |> Option.map ((=) solRule.Route) |> Option.defaultValue true
        )
