module Patient

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma


module Math = Utils.Math
module NormalValues = Data.NormalValueData


type Model = 
    {
        Patient : Shared.Patient
        IsMobile : bool
    }

type Msg =
    | YearChange of string
    | MonthChange of string
    | WeightChange of string
    | Clear


let apply f (p: Model) = f p


let get m = (apply id m).Patient


let calculateWeight yr mo =
    let age = (double yr) * 12. + (double mo)
    match
        NormalValues.ageWeight
        |> List.filter (fun (a, _) -> age <= a) with
    | (_, w)::_  -> w
    | [] -> 0.    


let init b = 
    let p = { Shared.Age = { Years = 0; Months = 0 }; Shared.Weight = { Estimated = calculateWeight 0 0 ; Measured = 0. } }
    { Patient = p; IsMobile = b }    


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
            let pat = 
                let p = model.Patient
                let w =  calculateWeight y (p.Age.Months)
                
                { p with Age = { p.Age  with Years = y }; Weight = { p.Weight with Estimated = w } }
            { model with Patient = pat }

    | None, Some m ->
        let pat = 
            let p = model.Patient
            let age = p.Age
            let weight = p.Weight

            let w = calculateWeight (age.Years) m

            let y = 
                if m = 12 && age.Years < 18 then 
                    age.Years + 1 
                else if m = -1 && p.Age.Years > 0 then  
                    age.Years - 1
                else
                    age.Years

            let m =
                if m >= 12 then 0
                else if m = -1 && y = 0 then 0
                else if m = -1 && y > 0 then 11
                else m
               
            { p with Age = { age with Months = m; Years = y }; Weight = { weight with Estimated = w } }

        { model with Patient = pat }

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
                let pat =
                    let p = model |> get
                    { p with Weight = { p.Weight with Measured = x }}
                { model with Patient = pat }, Cmd.none
        | false, _ -> model, Cmd.none

    | Clear ->
        init model.IsMobile, Cmd.none


let button txt dispatch =
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px"] ]]
              [
                Button.button
                    [ Button.IsFullWidth
                      Button.Color IsPrimary
                      Button.OnClick (fun _ -> Clear |> dispatch) ]
                    [ str txt ]

              ]


let yrInput dispatch (model: Model)  =
    let ph = string model.Patient.Age.Years

    let ctrl = 
        let cb =  OnChange (fun ev -> !! ev.target?value |> YearChange |> dispatch) 
        
        Control.div [ Control.Props [ cb] ]
            [ Input.number [ Input.Value ph ] ]

    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
        [ Label.label [] 
            [ str "Jaren" ] 
          ctrl
        ]


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
                      [ model |> yrInput dispatch
                        model.Patient.Age.Months |> moInput dispatch
                        model.Patient |> getWeight |> wtInput dispatch ] 
            button "Verwijder" dispatch  ] 
