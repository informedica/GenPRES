namespace Informedica.GenOrder.Lib


module Formulary =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib


    let getDoseRules provider filter = Api.getDoseRules provider |> Api.filterDoseRules provider filter


    let getDilutionRules provider generic form route =
        Api.getDilutionRules provider
        |> Array.filter (fun dilRule ->
            generic
            |> Option.map (String.equalsCapInsens dilRule.Generic)
            |> Option.defaultValue true
            && dilRule.Form
               |> Option.map (fun s ->
                   if form |> Option.isNone then
                       true
                   else
                       form.Value |> String.equalsCapInsens s
               )
               |> Option.defaultValue true
            && route |> Option.map ((=) dilRule.Route) |> Option.defaultValue true
        )
