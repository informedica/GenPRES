module Patient

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma


module Math = Utils.Math
module NormalValues = Data.NormalValueData


type Model = Shared.Patient


type Msg =
    | YearChange of string
    | MonthChange of string
    | WeightChange of string
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


let init () = 
    { Shared.Age = { Years = 0; Months = 0 }; Shared.Weight = { Estimated = calculateWeight 0 0 ; Measured = 0. } }


let getWeight (pat : Shared.Patient) =
    if pat.Weight.Measured = 0. then pat.Weight.Estimated else pat.Weight.Measured


let getAge (pat : Shared.Patient) =
    let y = pat.Age.Years |> float
    let m = (pat.Age.Months |> float) / 12.
    y + m


let updateAge yr mo (model: Model) =

    match yr, mo with
    | Some y, None ->
        if y > 18 || y < 0 then model
        else
            let w =  calculateWeight y (model.Age.Months)
            
            { model with Age = { model.Age  with Years = y }; Weight = { model.Weight with Estimated = w } }

    | None, Some m ->
        let age    = model.Age
        let weight = model.Weight

        let w = calculateWeight (age.Years) m

        let y = 
            if m = 12 && age.Years < 18 then 
                age.Years + 1 
            else if m = -1 && model.Age.Years > 0 then  
                age.Years - 1
            else
                age.Years

        let m =
            if m >= 12 then 0
            else if m = -1 && y = 0 then 0
            else if m = -1 && y > 0 then 11
            else m
           
        { model with Age = { age with Months = m; Years = y }; Weight = { weight with Estimated = w } }


    | _ -> model


let show model =
    let pat = model |> get
    let wght = 
        let w = pat |> getWeight
        if w < 2. then "" else 
            w |> Math.fixPrecision 2 |> string

    let e = pat.Weight.Estimated |> Math.fixPrecision 2 |> string

    sprintf "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg (geschat %s kg)" pat.Age.Years pat.Age.Months wght e


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


let createInput value name isMobile change dispatch vals =
    let cb =  OnChange (fun ev -> !! ev.target?value |> change |> dispatch) 
    
    let inp = 
        if isMobile then
            let opts =
                vals
                |> List.map (fun n ->
                    option [ n |> Value ] [ n |> str ]
                )

            Select.select []
                [ select [ Value value ]
                    opts
                ]
        else        
            Input.number [ Input.Value value ]

    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
        [ Label.label [] 
            [ str name ] 
          Control.div [ Control.Props [ cb ] ]
            [ inp ]
        ]


let yrInput isMobile dispatch yrs  = 
    [0 .. 18]
    |> List.map string
    |> createInput (yrs |> string) " Leeftijd Jaren" isMobile YearChange dispatch


let moInput isMobile dispatch mos =
    [0 .. 11]
    |> List.map string
    |> createInput (mos |> string) "Leeftijd Maanden" isMobile MonthChange dispatch


let wtInput isMobile dispatch wght =
    [11. .. 100.]
    |> List.append [2. .. 0.1 .. 10.]
    |> List.map (Math.fixPrecision 2)
    |> List.map string
    |> createInput (wght |> Math.fixPrecision 2 |> string) "Gewicht Kg" isMobile WeightChange dispatch


let view isMobile (model : Model) (dispatch : Msg -> unit) =
    form [ ]
        [ 
            Field.div [ Field.IsHorizontal; Field.Props [ Style [ ] ] ] 
                      [ model.Age.Years    |> yrInput isMobile dispatch
                        model.Age.Months   |> moInput isMobile dispatch
                        model |> getWeight |> wtInput isMobile dispatch ] 
            button "Verwijder" dispatch  ] 
