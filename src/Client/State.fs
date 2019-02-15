module State

open Elmish
open Elmish.React
open GenPres
open Types
open Fable.Helpers.Isomorphic
open Elmish.ReactNative


let cmdNone model = model, Cmd.none


let processResponse model resp =
    match resp with
    | None -> model
    | Some resp ->
        match resp with
        | Shared.Types.Response.Configuration config ->
            let yrs, mos, wths, hths =
                match config |> List.tryFind (fun s -> s.Department = "pediatrie") with
                | Some set ->
                    [set.MinAge .. set.MaxAge / 12]
                    , [0 .. 11]
                    , [set.MinWeight..1.0..set.MaxWeight]
                    , [50..200]
                | None -> [], [], [], []
            { model with Configuration = Some config
                         FormModel = Components.Form.init yrs mos wths hths }
        | Shared.Types.Response.Patient pat ->
            { model with Patient = Some pat
                         StatusBarModel = Components.StatusBar.Status "Online" }
            
        

let getConfiguration () =
    Shared.Types.Request.Configuration.Get
    |> Shared.Types.Request.ConfigMsg
    |> Utils.Request.post

let getPatient () =
    Shared.Types.Request.Patient.Init
    |> Shared.Types.Request.PatientMsg
    |> Utils.Request.post

// defines the initial state and initial command (= side-effect) of the application
let init() : Model * Cmd<Msg> =
    let initialModel =
        { Configuration = None
          Patient = None
          NavBarModel = Components.NavBar.init()
          SideMenuModel = Components.SideMenu.init()
          StatusBarModel = Components.StatusBar.init()
          FormModel = Components.Form.init [] [] [] [] }

    let loadConfig =
        Cmd.ofPromise
            getConfiguration
            ()
            (Ok >> ResponseMsg)
            (Error >> ResponseMsg)

    let loadPatient =
        Cmd.ofPromise
            getPatient
            ()
            (Ok >> ResponseMsg)
            (Error >> ResponseMsg)

    initialModel, (Cmd.batch [loadConfig; loadPatient])

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with
    | ResponseMsg(Ok resp) ->
        printfn "response received: %A" resp
        resp
        |> processResponse model
        |> cmdNone
    | ResponseMsg(Error err) ->
        printfn "error: %s" err.Message
        model |> cmdNone
    | NavBarMsg _ ->
        printfn "navbar message"
        { model with SideMenuModel =
                        model.SideMenuModel
                        |> Components.SideMenu.update
                                Components.SideMenu.ToggleMenu }
        |> cmdNone
    | FormMsg msg ->
        printfn "sidemenu message"
        { model with FormModel =
                        model.FormModel
                        |> Components.Form.update msg }
        |> cmdNone
    | _  -> model, Cmd.none
