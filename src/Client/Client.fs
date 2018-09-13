module Client

open Elmish
open Elmish.React

open Fable.Core


open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Fulma
open Component

open Shared

module Math = Utils.Math
module String = Utils.String
module Select = Component.Select
module Table = Component.Table

[<Emit("navigator.userAgent")>]
let userAgent : string = jsNative


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = 
    { 
        GenPres : GenPres option
        PatientModel : Patient.Model
        Page : Page
        Device : Device
        EmergencyModel : Emergency.Model
        CalculatorModel : Calculator.Model
    }
and Device = 
    {
        IsMobile : bool
        Width : float
        Height : float
    }
and Page =
    | CalculatorPage
    | EmergencyListPage
     

let createDevice () =
    let agent = userAgent |> String.toLower
    {
        IsMobile =
            [ "iphone"; "android"; "ipad"; "opera mini"; "windows mobile"; "windows phone"; "iemobile" ]
            |> List.exists (fun s -> agent |> String.contains s)
        Width = Fable.Import.Browser.screen.width
        Height = Fable.Import.Browser.screen.height        
    }

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| PatientMsg of Patient.Msg
| EmergencyMsg of Emergency.Msg
| ChangePage of Page
| CalculatorMsg of Calculator.Msg
| GenPresLoaded of Result<GenPres, exn>


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =

    printfn "User Agent = %s" userAgent
    
    let genpres = { Name = "GenPres OFFLINE"; Version = "0.01" }

    let device = createDevice ()

    let pat = Patient.init ()
    
    let initialModel = 
        { 
            GenPres = Some genpres
            Page = EmergencyListPage
            PatientModel = pat
            Device = device
            EmergencyModel = Emergency.init device.IsMobile 
            CalculatorModel = Calculator.init pat
        }

    let loadCountCmd =
        Cmd.ofPromise
            ( fetchAs<GenPres> "http://localhost:8085/api/init" )
            []
            (Ok >> GenPresLoaded)
            (Error >> GenPresLoaded)

    initialModel, loadCountCmd


// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with

    | PatientMsg msg ->
        let pat, cmd = Patient.update msg model.PatientModel
        { model with PatientModel = pat; CalculatorModel = Calculator.init pat }, Cmd.map PatientMsg cmd

    | EmergencyMsg msg ->
        { model with EmergencyModel = model.EmergencyModel |> Emergency.update msg }, Cmd.none
 
    | GenPresLoaded (Ok genpres) ->
        { model with GenPres = Some genpres }, Cmd.none

    | CalculatorMsg msg ->
        { model with CalculatorModel = model.CalculatorModel |> Calculator.update msg  }, Cmd.none

    | ChangePage page ->
        { model with Page = page}, Cmd.none

    | GenPresLoaded (_) -> model, Cmd.none


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
| { GenPres = Some x } -> sprintf "%s versie: %s" x.Name x.Version 
| { GenPres = None   } -> "Laden ..."


let topView dispatch model =
    let openPEWS = fun _ -> CalculatorPage    |> ChangePage |> dispatch
    let openERL  = fun _ -> EmergencyListPage |> ChangePage |> dispatch

    Navbar.navbar 
        [ Navbar.Color IsPrimary
          Navbar.Props [ Style [ CSSProp.Padding "10px" ] ]
          Navbar.HasShadow ]
        [ Navbar.Item.div [ ]
            [ Heading.h3 [ Heading.Option.CustomClass "has-text-white" ]
                [ str (show model) ] ]

          Navbar.End.div []
              [ Navbar.Item.div 
                    [ Navbar.Item.IsHoverable
                      Navbar.Item.HasDropdown ] 
                    [ Navbar.Item.a [ ] 
                        [ Fulma.FontAwesome.Icon.faIcon 
                            [ Icon.Size IsSmall ] 
                            [ FontAwesome.Fa.icon FontAwesome.Fa.I.Calculator ] ]
                      Navbar.Dropdown.div [ Navbar.Dropdown.IsRight ] 
                        [ Navbar.Item.a [ Navbar.Item.Props  [ OnClick openPEWS ] ] [ str "PEWS score" ] ] ]
                          
                Navbar.Item.div 
                    [ Navbar.Item.IsHoverable
                      Navbar.Item.HasDropdown ] 
                    [ Navbar.Item.a [ ] 
                        [ Fulma.FontAwesome.Icon.faIcon 
                            [ Icon.Size IsSmall ] 
                            [ FontAwesome.Fa.icon FontAwesome.Fa.I.Bars ] ]
                      Navbar.Dropdown.div [ Navbar.Dropdown.IsRight ] 
                        [ Navbar.Item.a [ Navbar.Item.Props [ OnClick openERL ] ] [ str "Acute Opvang" ]
                          Navbar.Item.a [] [ str "Medicatie Voorschrijven" ] ] ] ] ]


let bottomView =
    Footer.footer [ ]
        [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
            [ safeComponents ] ]


let view (model : Model) (dispatch : Msg -> unit) =

    let content =
        match model.Page with
        | CalculatorPage    -> Calculator.view model.Device.IsMobile model.CalculatorModel (CalculatorMsg >> dispatch)
        | EmergencyListPage -> Emergency.view model.Device.IsMobile model.PatientModel model.EmergencyModel (EmergencyMsg >> dispatch)
    
    div [ ]
        [ model |> topView dispatch  

          Container.container [ Container.Props [Style [ CSSProp.Padding "10px"]] ]
              [ Patient.view model.Device.IsMobile model.PatientModel (PatientMsg >> dispatch)

                Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h5 [] [ str (model.PatientModel |> Patient.show) ] ]
                content ]
          bottomView  ] 


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
