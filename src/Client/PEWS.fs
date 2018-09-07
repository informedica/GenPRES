module PEWS

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma

open Shared

module Select = Component.Select
module Modal = Component.Modal

type Model = 
    { 
        Age: Age
        Score: int 
        RespRate : Select.Model
        RespEffort : Select.Model
        Saturation : Select.Model
        HeartRate : Select.Model
        CapillaryRefill : Select.Model
        SystolicBP : Select.Model
        Temperature : Select.Model
    }


let init () =
    { 
        Age = { Age.Years = 0; Age.Months = 0 }
        Score = 0
        RespRate = Select.init ["<15"; "15-19";"20-29";"30-60";"61-80";"81-90";">90"]
        RespEffort = Select.init []
        Saturation = Select.init []
        HeartRate = Select.init []
        CapillaryRefill = Select.init []
        SystolicBP = Select.init []
        Temperature = Select.init []
    }


type Msg = 
    | OpenPEWS of Patient.Model
    | SelectRespRate of Select.Msg
    // | SelectRespEffort of Select.Msg
    // | SelectSaturation of Select.Msg
    // | SelectHeartRate of Select.Msg
    // | SelectCapillaryRefill of Select.Msg
    // | SelectSystolicBP of Select.Msg
    // | SelectTemperature of Select.Msg


let update (msg : Msg) (model : Model) =
    match msg with
    | OpenPEWS pat ->
        let newModel = { model with Age = pat.Age }
        newModel
    | SelectRespRate sel ->
        model

let view (model : Model) (dispatch : Msg -> unit) =

    let content =
        form [ ]
            [ 
                Field.div [ Field.IsHorizontal; Field.Props [ Style [ ] ] ] 
                          [ Select.view2 "Ademfrequentie (/min)" model.RespRate (SelectRespRate >> dispatch) ] 
                  ] 

    content