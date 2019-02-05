module Patient

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma


module Math = Shared.Utils.Math
module NormalValues = Shared.Data.NormalValues
module Patient = Shared.Models.Patient

type Model = Patient.Patient


type Msg =
    | YearChange of string
    | MonthChange of string
    | WeightChange of string
    | HeightChange of string
    | Clear

let apply f (p: Model) = f p


let get = apply id


let calculateWeight yr mo =
    let age = (double yr) * 12. + (double mo)
    match
        NormalValues.ageWeight
        |> List.filter (fun (a, _) -> age <= a) with
    | (_, w)::_  -> w
    | [] -> 0.    


let init () = Patient.patient 


let getWeight = Patient.getWeight


let getHeight = Patient.getHeight


let getAge = Patient.getAge


let updateAge yr mo (model: Model) =

    match yr, mo with
    | Some y, None -> model |> Patient.updateAgeYears y

    | None, Some m -> model |> Patient.updateAgeMonths m

    | _ -> model


let show model = model |> Patient.show


let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with
    | YearChange s -> 
        printfn "Year: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            model |> updateAge (Some i) None, Cmd.none
        | false, _ -> 
            model, Cmd.none

    | MonthChange s -> 
        printfn "Month: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            model |> updateAge None (Some i), Cmd.none
        | false, _ -> 
            model, Cmd.none

    | WeightChange s ->
        printf "Weight: %s" s
        match s |> Double.TryParse with
        | true, x ->
            let x = x |> Math.fixPrecision 2
            if x < 2. then model, Cmd.none
            else
                { model with Weight = { model.Weight with Measured = x }}, Cmd.none

        | false, _ -> model, Cmd.none

    | HeightChange s ->
        printf "Height: %s" s
        match s |> Double.TryParse with
        | true, x ->
            let x = x |> Math.fixPrecision 0
            if x < 40. || x > 200. then model, Cmd.none
            else
                { model with Height = { model.Height with Measured = x }}, Cmd.none
        
        | false, _ -> model, Cmd.none

    | Clear ->
        init (), Cmd.none


let button txt dispatch =
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px"] ]]
              [
                Button.button
                    [ Button.IsFullWidth
                      Button.Color IsPrimary
                      Button.OnClick (fun _ -> Clear |> dispatch) ]
                    [ str txt ]

              ]


let createInput value step name isMobile change dispatch vals =
    let cb =  OnChange (fun ev -> !! ev.target?value |> change |> dispatch) 
    
    let inp = 
        if isMobile then
            let opts =
                vals
                |> List.map (fun n ->
                    option [ n |> box |> Value ] [ n |> str ]
                )

            Select.select []
                [ select [ Value value ]
                    opts
                ]
        else 
            Input.number [ Input.Value value; Input.Props [ Fable.Helpers.React.Props.Step step ] ]

    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
        [ Label.label [] 
            [ str name ] 
          Control.div [ Control.Props [ cb ] ]
            [ inp ]
        ]


let yrInput isMobile dispatch yrs  = 
    [0 .. 18]
    |> List.map string
    |> createInput (yrs |> string) "" " Leeftijd Jaren" isMobile YearChange dispatch


let moInput isMobile dispatch mos =
    [0 .. 11]
    |> List.map string
    |> createInput (mos |> string) "" "Leeftijd Maanden" isMobile MonthChange dispatch


let wtInput isMobile dispatch wght =
    let s = 
        if wght >= 10. then 1. else 0.1 
        |> string

    [11. .. 100.]
    |> List.append [2. .. 0.1 .. 10.]
    |> List.map (Math.fixPrecision 2)
    |> List.map string
    |> createInput (wght |> Math.fixPrecision 2 |> string) s "Gewicht Kg" isMobile WeightChange dispatch


let htInput isMobile dispatch hght =
    let s = 
        if hght >= 10. then 1. else 0.1 
        |> string

    [11. .. 200.]
    |> List.append [2. .. 1. .. 10.]
    |> List.map (Math.fixPrecision 2)
    |> List.map string
    |> createInput (hght |> Math.fixPrecision 2 |> string) s "Lengte cm" isMobile HeightChange dispatch


let view isMobile (model : Model) (dispatch : Msg -> unit) =

    let title =
        Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
            [ Heading.h6 [] [ str (model |> Patient.show) ] ]

    form [ ]
        [ 
            Field.div [ Field.IsHorizontal; Field.Props [ Style [ ] ] ] 
                      [ model.Age.Years    |> yrInput isMobile dispatch
                        model.Age.Months   |> moInput isMobile dispatch
                        model |> getWeight |> wtInput isMobile dispatch
                        model |> getHeight |> htInput isMobile dispatch ] 
            button "Verwijder" dispatch
            title  ] 
