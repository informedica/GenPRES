module Calculator

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma

open Shared

module Select = Component.Select
module Modal = Component.Modal
module NormalValueData = Data.NormalValueData

module PEWS =

    type Model = 
        { 
            Age: int
            Score: int 
            RespRate : Select.Model
            RespEffort : Select.Model
            Saturation : Select.Model
            Oxygen : Select.Model
            HeartRate : Select.Model
            CapillaryRefill : Select.Model
            SystolicBP : Select.Model
            Temperature : Select.Model
        }


    let init () =
        let get a s =
            match 
                NormalValueData.pews
                |> List.tryFind (fun (age, _) -> a < age) with
            | Some (_, xs) ->
                match xs |> List.tryFind (fun (x', _) -> x' = s ) with
                | Some (_, xs') -> xs'
                | None -> []
            | None -> []                
            |> List.map snd 
            |> List.filter (String.IsNullOrEmpty >> not)
            |> Select.init false s

        { 
            Age = 0
            Score = 0
            RespRate = get 0 "Ademfrequentie/min"
            RespEffort = get 0 "Ademarbeid"
            Saturation = get 0 "Saturatie"
            Oxygen = get 0 "Zuurstof"
            HeartRate = get 0 "Hartfrequentie/min"
            CapillaryRefill = get 0 "Capillaire refill"
            SystolicBP = get 0 "RR (systole)"
            Temperature = get 0 "Temperatuur"
        }


    type Msg = 
        | OpenPEWS of Patient.Model
        | SelectRespRate of Select.Msg
        | SelectRespEffort of Select.Msg
        | SelectSaturation of Select.Msg
        | SelectOxygen of Select.Msg
        | SelectHeartRate of Select.Msg
        | SelectCapillaryRefill of Select.Msg
        | SelectSystolicBP of Select.Msg
        | SelectTemperature of Select.Msg


    let calculateTotal (model : Model) =
        let get n (sel : Select.Model) =
            match sel.Items 
                  |> List.tryFind (fun i -> i.Selected) with
            | Some i -> 
                match 
                    NormalValueData.pews
                    |> List.tryFind (fun (age, _) -> model.Age < age) with
                | Some (_, xs) ->
                    match xs |> List.tryFind (fun (x', _) -> x' = n ) with
                    | Some (_, xs') ->
                        match xs' |> List.tryFind (fun (_, i') -> i' = i.Name) with
                        | Some (s, _) -> s
                        | None -> 0
                    | None -> 0
                | None -> 0
            | None -> 0            

        let t =
            (model.CapillaryRefill |> get "Capillaire refill") + 
            (model.HeartRate |> get "Hartfrequentie/min") +
            (model.Oxygen |> get "Zuurstof") +
            (model.RespEffort |> get "Ademarbeid") +
            (model.RespRate |> get "Ademfrequentie/min") +
            (model.Saturation |> get "Saturatie") +
            (model.SystolicBP |> get "RR (systole)") +
            (model.Temperature |> get "Temperatuur")
        
        { model with Score = t }

    let update (msg : Msg) (model : Model) =
        match msg with
        | OpenPEWS pat ->
            { model with Age = pat.Age.Years * 12 + pat.Age.Months }

        | SelectRespRate msg ->
            { model with RespRate = model.RespRate |> Select.update msg }
            |> calculateTotal 

        | SelectRespEffort msg ->
            { model with RespEffort = model.RespEffort |> Select.update msg }
            |> calculateTotal 

        | SelectSaturation msg ->
            { model with Saturation = model.Saturation |> Select.update msg }
            |> calculateTotal

        | SelectOxygen msg ->
            { model with Oxygen = model.Oxygen |> Select.update msg }
            |> calculateTotal

        | SelectHeartRate msg ->
            { model with HeartRate = model.HeartRate |> Select.update msg }
            |> calculateTotal

        | SelectCapillaryRefill msg ->
            { model with CapillaryRefill = model.CapillaryRefill |> Select.update msg }
            |> calculateTotal

        | SelectSystolicBP msg ->
            { model with SystolicBP = model.SystolicBP |> Select.update msg }
            |> calculateTotal

        | SelectTemperature msg ->
            { model with Temperature = model.Temperature |> Select.update msg }
            |> calculateTotal



    let getSelected age (model : Select.Model) = 
        let n =
            match model.Items 
                  |> List.tryFind (fun x -> x.Selected) with
            | Some item -> item.Name
            | None -> ""

        let s =
            match 
                NormalValueData.pews
                |> List.tryFind (fun (a, _) -> age < a) with
            | Some (_, xs) ->
                match xs |> List.tryFind (fun (x', _) -> x' = model.Title ) with
                | Some (_, xs') -> 
                    match xs' |> List.tryFind (fun (_, n') -> n <> "" && n' = n) with
                    | Some (s', _) -> s' |> string
                    | None -> ""
                | None -> ""
            | None -> ""

            
        str n, str s


    let view (model : Model) (dispatch : Msg -> unit) =
        let header =
            thead []
                [
                    th [] [ str "Item" ]
                    th [] [ str "Selectie" ]
                    th [] [ str "Score" ]
                ]

        let trow m el =
            let sel, score = getSelected model.Age  m
            tr [] [ td [] [ div [] [ el ] ]; td [] [sel]; td [] [score] ]

        let content =
            Table.table [ Table.IsFullWidth
                          Table.IsStriped
                          Table.IsHoverable]
                [ header
                  tbody []
                      [ Select.view model.RespRate (SelectRespRate >> dispatch) |> trow model.RespRate
                        Select.view model.RespEffort (SelectRespEffort >> dispatch) |> trow model.RespEffort
                        Select.view model.Saturation (SelectSaturation >> dispatch) |> trow model.Saturation
                        Select.view model.Oxygen (SelectOxygen >> dispatch) |> trow model.Oxygen
                        Select.view model.HeartRate (SelectHeartRate >> dispatch) |> trow model.HeartRate
                        Select.view model.CapillaryRefill (SelectCapillaryRefill >> dispatch) |> trow model.CapillaryRefill
                        Select.view model.SystolicBP (SelectSystolicBP >> dispatch) |> trow model.SystolicBP
                        Select.view model.Temperature (SelectTemperature >> dispatch) |> trow model.Temperature
                        tr [Style [CSSProp.FontWeight "bold"]] [ td [  ] [ str "Totaal"  ] ; td [] [ str "" ]; td [] [ str (model.Score |> string) ] ] ] ]

        content



type Model = 
    { 
        ActiveTab : ActiveTab
        PEWSModel : PEWS.Model
    }
and ActiveTab =
    | PEWSTab


type Msg = 
    | TabChange of ActiveTab
    | PEWSMsg of PEWS.Msg


let update (msg : Msg) (model : Model) =
    match msg with
    | TabChange tab ->
        { model with ActiveTab = tab }
    | PEWSMsg msg ->
        { model with PEWSModel = model.PEWSModel |> PEWS.update msg }

let init () = 
    { 
        ActiveTab = PEWSTab
        PEWSModel = PEWS.init ()
    }


let view (model: Model) dispatch =


    let tabs (model : Model) dispatch =
        Tabs.tabs [ Tabs.IsFullWidth; Tabs.IsBoxed ] 
            [ Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = PEWSTab)
                         Tabs.Tab.Props [ OnClick (fun _ -> PEWSTab |> TabChange |> dispatch) ] ] [ a [] [str "PEWS Score"] ] ]

    let content =
        if model.ActiveTab = PEWSTab then 
            PEWS.view model.PEWSModel (PEWSMsg >> dispatch)
        else div [] []   

    div []
        [
            tabs model dispatch
            content
        ]