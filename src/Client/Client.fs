module Client

open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Fable.Core.JsInterop

open Fulma
open Fable.Import.React
open Fable.Helpers.React.ReactiveComponents
open Fulma
open Component

open Shared

module Math = Utils.Math
module Select = Component.Select
module Table = Component.Table



// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = 
    { 
        GenPres : GenPres option
        Patient : Patient.Model
        Device : Device
        Selections : Treatment.Model
        ActiveTab : string 
    }
and Device = Computer | Tablet | Mobile


let createDevice x =
    if x < 1000. then Mobile
    else if x < 2000. then Tablet
    else Computer


// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| TabChange of string
| SelectMsg of Select.Msg
| PatientMsg of Patient.Msg
| GenPresLoaded of Result<GenPres, exn>


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    printfn "Screen: x = %A, y = %A" Fable.Import.Browser.screen.width Fable.Import.Browser.screen.height
       
    let genpres = { Name = "GenPres OFFLINE"; Version = "0.01" }
    
    let initialModel = 
        { 
            GenPres = Some genpres
            Patient = Patient.init ()
            Device = Fable.Import.Browser.screen.width |> createDevice
            Selections = Treatment.init "Noodlijst" 
            ActiveTab = "Noodlijst"
        }

    let loadCountCmd =
        Cmd.ofPromise
            ( fetchAs<GenPres> "/api/init" )
            []
            (Ok >> GenPresLoaded)
            (Error >> GenPresLoaded)
    initialModel, loadCountCmd


// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with
    | TabChange tab ->
        let updatedModel = { model with ActiveTab = tab; Selections = Treatment.init tab }
        updatedModel, Cmd.none

    | PatientMsg msg ->
        let patModel, cmd = Patient.update msg model.Patient
        { model with Patient = patModel}, Cmd.map PatientMsg cmd


    | SelectMsg msg ->
        let selModel, cmd = Select.update msg model.Selections
        { model with Selections = selModel}, Cmd.map SelectMsg cmd

    | GenPresLoaded (Ok genpres) ->
        let newModel = { model with GenPres = Some genpres }
        printfn "active tab: %s" model.ActiveTab
        newModel, Cmd.none

    | _ -> model, Cmd.none


let safeComponents =
    let components =
        span [ ]
           [
             a [ Href "https://saturnframework.github.io" ] [ str "Saturn" ]
             str ", "
             a [ Href "http://fable.io" ] [ str "Fable" ]
             str ", "
             a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
             str ", "
             a [ Href "https://mangelmaxime.github.io/Fulma" ] [ str "Fulma" ]
           ]

    p [ ]
        [ strong [] [ str "SAFE Template" ]
          str " powered by: "
          components ]


let show = function
| { GenPres = Some x } -> sprintf "%s version: %s" x.Name x.Version 
| { GenPres = None   } -> "Loading..."


let treatment (model : Model) =
    Treatment.treatment model.Selections model.Patient


let contMeds (model : Model) =
    Treatment.contMeds model.Selections model.Patient


let tabs dispatch (model : Model) =
    Tabs.tabs [ Tabs.IsFullWidth; Tabs.IsBoxed ] 
        [ Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = "Noodlijst")
                     Tabs.Tab.Props [ OnClick (fun _ -> "Noodlijst" |> TabChange |> dispatch) ] ] [ a [] [str "Noodlijst"] ]
          Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = "Standaard Pompen") 
                     Tabs.Tab.Props [ OnClick (fun _ -> "Standaard Pompen" |> TabChange |> dispatch) ]] [ a [] [str "Standaard Pompen"]] 
          Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = "Normaal Waarden") 
                     Tabs.Tab.Props [ OnClick (fun _ -> "Normaal Waarden" |> TabChange |> dispatch) ]] [ a [] [str "Normaal Waarden"]] ]


let view (model : Model) (dispatch : Msg -> unit) =
    div [ Style [ CSSProp.Padding "10px"] ]
        [ Navbar.navbar [ Navbar.Color IsPrimary ; Navbar.Props [ Style [ CSSProp.Padding "30px"] ] ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ ]
                    [ str (show model) ] ] ]

          Container.container []
              [ model |> tabs dispatch
                
                Patient.view model.Patient (PatientMsg >> dispatch)

                Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h5 [] [ str (model.Patient |> Patient.show) ] ]
                
                Select.view model.Selections (SelectMsg >> dispatch)
                
                (if model.ActiveTab = "Noodlijst" then 
                    treatment model 
                 else if model.ActiveTab = "Standaard Pompen" then 
                    div [] [ contMeds model ]
                 else div [] [ str "Normaal Waarden (volgt nog)"]) ]
          
          Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]


#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
