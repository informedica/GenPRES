module Patient

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Fable.Core.JsInterop
open Elmish
open Fulma


module Math = Utils.Math
module NormalValues = Data.NormalValues


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


let init () = { Shared.Age = { Years = 0; Months = 0 }; Shared.Weight = { Estimated = calculateWeight 0 0 ; Measured = 0. } }


let getWeight pat = 
    if (pat |> get).Weight.Measured = 0. then pat.Weight.Estimated else pat.Weight.Measured


let getAge pat =
    let y = (pat |> get).Age.Years |> float
    let m = (pat.Age.Months |> float) / 12.
    y + m



let show pat = 
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
            if i > 18 || i < 0 then model, Cmd.none
            else
                let newModel = 
                    let w =  calculateWeight i (model.Age.Months)
                    { model with Age = { model.Age  with Years = i }; Weight = { model.Weight with Estimated = w } }
                newModel, Cmd.none
        | false, _ -> 
            model, Cmd.none

    | MonthChange s -> 
        printfn "Month: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            let newModel = 
                let age = model.Age
                let weight = model.Weight

                let w = calculateWeight (age.Years) i

                let y = 
                    if i = 12 && age.Years < 18 then 
                        age.Years + 1 
                    else if i = -1 && model.Age.Years > 0 then  
                        age.Years - 1
                    else
                        age.Years

                let m =
                    if i >= 12 then 0
                    else if i = -1 then 11
                    else i
                   
                { model with Age = { age with Months = m; Years = y }; Weight = { weight with Estimated = w } }

            newModel, Cmd.none
        | false, _ -> 
            model, Cmd.none

    | WeightChange s ->
        printf "Weight: %s" s
        match s |> Double.TryParse with
        | true, x ->
            let x = x |> Math.fixPrecision 2
            if x < 2. then model, Cmd.none
            else
                let newModel =
                    { model with Weight = { model.Weight with Measured = x }}
                newModel, Cmd.none
        | false, _ -> model, Cmd.none

    | Clear ->
        let newModel = init ()
        newModel, Cmd.none



let button txt dispatch =
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px"] ]]
              [
                Button.button
                    [ Button.IsFullWidth
                      Button.Color IsPrimary
                      Button.OnClick (fun _ -> Clear |> dispatch) ]
                    [ str txt ]

              ]


let yrInput dispatch (n : int)  =
    let ph = string n
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
        [ Label.label [] 
            [ str "Jaren" ] 
          Control.div [ Control.Props [ OnChange (fun ev -> !! ev.target?value |> YearChange |> dispatch) ] ]
             [ Input.number [ Input.Value ph ] ]]


let moInput dispatch (n : int) =
    let ph = string n
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
        [ Label.label [] 
            [ str "Maanden" ] 
          Control.div [ Control.Props [ OnChange (fun ev -> !! ev.target?value |> MonthChange |> dispatch) ] ]
             [ Input.number [ Input.Value ph; Input.Props [  ] ] ]]


let wtInput dispatch (n : double) =
    let s = 
        if n >= 10. then 1. else 0.1 
        |> string
    let n = 
        n
        |> Math.fixPrecision 2
        |> string

    printfn "weight input %s, step %s" n s
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
        [ Label.label [] 
            [ str "Gewicht" ] 
          Control.div [ Control.Props [ OnChange (fun ev -> !! ev.target?value |> WeightChange |> dispatch) ] ]
             [ Input.number [ Input.Value n; Input.Props [ Fable.Helpers.React.Props.Step s ] ] ]]


let view (model : Model) (dispatch : Msg -> unit) =
    form [ ]
        [ 
            Field.div [ Field.IsHorizontal; Field.Props [ Style [ ] ] ] 
                      [ model.Age.Years  |> yrInput dispatch
                        model.Age.Months |> moInput dispatch
                        model |> getWeight |> wtInput dispatch ] 
            button "Verwijder" dispatch  ] 
