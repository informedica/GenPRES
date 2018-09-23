module Client

open Elmish
open Elmish.React
open Elmish.Browser.Navigation
open Elmish.Browser.UrlParser

open Fable.Core
open Fable.Core.JsInterop

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Fulma
open Component

open Shared
open System
open Fulma

module Math = Utils.Math
module String = Utils.String
module DateTime = Utils.DateTime
module List = Utils.List
module Select = Component.Select
module Table = Component.Table
module Navbar = Component.Navbar


/// Setup older browser support for
/// `fetch` and `promises`.
importDefault "isomorphic-fetch"
let polyFill : unit -> unit = import "polyfill" "es6-promise"
polyFill ()


[<Emit("navigator.userAgent")>]
let userAgent : string = jsNative



[<Import("count", "./lib/gitCount.js")>]
let gitCount : int = jsNative


let version = sprintf "0.0.%i" gitCount


type MarkDown =
    abstract render : string -> obj


let md = 
    createNew (importDefault<MarkDown> "markdown-it") ()
    :?> MarkDown    


[<Pojo>]
type DangerousInnerHtml =
    { __html : string }


let htmlFromMarkdown str = 
    div [ DangerouslySetInnerHTML { __html = md.render str |> string } ] []


module Query = 

    type PatientQuery = 
        {
            BirthYear : int option
            BirthMonth : int option
            BirthDay : int option
            WeightGram : int option
            HeightCm : int option            
        }

    type Route =
        | Query of PatientQuery
        static member FromParams by bm bd wt ht =
            { BirthYear = by; BirthMonth = bm; BirthDay = bd; WeightGram = wt; HeightCm = ht }
            |> Query

    let route : Parser<Route -> Route,_> =
        let map = Elmish.Browser.UrlParser.map
        let s = Elmish.Browser.UrlParser.s

        oneOf [
            map (Route.FromParams) (top <?> intParam "bty" <?> intParam "btm" <?> intParam "btd" <?> intParam "wth" <?> intParam "hth") 
            map (Route.FromParams) (s "noodlijst2" </> top <?> intParam "bty" <?> intParam "btm" <?> intParam "btd" <?> intParam "wth" <?> intParam "hth") 
        ]


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = 
    { 
        GenPres : GenPres option
        NavbarModel : Navbar.Model
        PatientModel : Patient.Model
        Page : Page
        Device : Device
        ShowMenu : NavbarMenu
        EmergencyModel : Emergency.Model
        CalculatorModel : Calculator.Model
    }
and NavbarMenu = { CalculatorMenu : bool; MainMenu : bool }
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

let updateGenPres (model : Model) =
    let gp =
        match model.GenPres with
        | Some gp' -> { gp' with Version = version }
        | None ->  { Name = "GenPres OFFLINE"; Version = version }
    { model with GenPres = Some gp }




// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| NavbarMsg of Navbar.Msg
| PatientMsg of Patient.Msg
| EmergencyMsg of Emergency.Msg
| MenuMsg of MenuMsg
| ChangePage of Page
| CalculatorMsg of Calculator.Msg
| GenPresLoaded of Result<GenPres, exn>
and MenuMsg = CalculatorMenuMsg | MainMenuMsg


let urlUpdate (result : Query.Route Option) (model : Model) =
    let loadCountCmd =
        Cmd.ofPromise
            ( fetchAs<GenPres> "http://localhost:8085/api/init" )
            []
            (Ok >> GenPresLoaded)
            (Error >> GenPresLoaded)

    match result with
    | Some (Query.Query pat) ->
        printfn "urlUpdate: %A" pat

        let pat =
            DateTime.optionToDate pat.BirthYear pat.BirthMonth pat.BirthDay
            |> DateTime.dateDiffYearsMonths DateTime.Now
            |> (fun (yr, mo) ->
                model.PatientModel 
                |> Models.Patient.updateAgeYears yr
                |> Models.Patient.updateAgeMonths mo
                |> (fun p ->
                    match pat.WeightGram with
                    | Some gr -> p |> Models.Patient.updateWeightGram (gr |> float)
                    | None -> p
                )
            )
        // Dirty fix to enable right calculator model patient
        { model with PatientModel = pat ; CalculatorModel = Calculator.init pat } , loadCountCmd

    | None -> model , loadCountCmd
    

// defines the initial state and initial command (= side-effect) of the application
let init result : Model * Cmd<Msg> =

    printfn "User Agent = %s" userAgent
    
    let device = createDevice ()

    let pat = Patient.init ()

    let showMenu = { CalculatorMenu = false; MainMenu = false }
    
    let initialModel = 
        { 
            GenPres = None
            NavbarModel = Navbar.init ()
            Page = EmergencyListPage
            PatientModel = pat
            Device = device
            ShowMenu = showMenu
            EmergencyModel = Emergency.init device.IsMobile 
            CalculatorModel = Calculator.init pat
        }


    initialModel |> urlUpdate result


// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with
    | NavbarMsg msg -> 
        { model with NavbarModel = model.NavbarModel |> Navbar.update msg }, Cmd.none

    | PatientMsg msg ->
        let pat, cmd = Patient.update msg model.PatientModel
        { model with PatientModel = pat; CalculatorModel = Calculator.init pat }, Cmd.map PatientMsg cmd

    | EmergencyMsg msg ->
        { model with EmergencyModel = model.EmergencyModel |> Emergency.update msg }, Cmd.none
 
    | GenPresLoaded (Ok genpres) ->
        
        { model with GenPres = Some genpres } |> updateGenPres, Cmd.none

    | CalculatorMsg msg ->
        { model with CalculatorModel = model.CalculatorModel |> Calculator.update msg  }, Cmd.none

    | ChangePage page ->
        { model with Page = page}, Cmd.none

    | GenPresLoaded (_) -> 
        model |> updateGenPres, Cmd.none

    | MenuMsg msg ->
        match msg with
        | CalculatorMenuMsg -> 
            { model with ShowMenu =  { model.ShowMenu with CalculatorMenu = not model.ShowMenu.CalculatorMenu } }, Cmd.none
        | MainMenuMsg -> 
            { model with ShowMenu =  { model.ShowMenu with MainMenu = not model.ShowMenu.MainMenu } }, Cmd.none


let disclaimer =
    let txt = """
***Disclaimer***

Deze applicatie is nog in ontwikkeling en validatie van de inhoud heeft nog niet plaatsgevond. 
De gebruiker is zelf verantwoordelijk voor het gebruik van de getoonde informatie. Indien u fouten vindt
of suggesties hebt gaarne dit per [mail](mailto:c.w.bollen@umcutrecht.nl) vermelden. 

Verdere informatie kunt u vinden op de [PICU WKZ site](http://picuwkz.nl). De code voor deze webapplicatie
is te vinden op [Github](http://github.com/halcwb/GenPres2.git).
"""
    htmlFromMarkdown txt

let createBrand model = 
    let inl = Heading.Props [ Style [ CSSProp.Display "inline-block"; CSSProp.PaddingLeft "10px" ] ]

    match model with
    | { GenPres = Some x } -> 
        div [ ]
            [ Heading.h2 [ Heading.Option.CustomClass "has-text-white"; inl ] [ x.Name |> str ]
              Heading.h6 [ Heading.Option.CustomClass "has-text-white"; inl ] [ "versie " + x.Version |> str ]
            ]

    | { GenPres = None   } ->
        Heading.h3 [ Heading.Option.CustomClass "has-text-white" ] [ "Laden ..." |> str ]


let navbarView dispatch model =

    let openCalc = fun _ -> CalculatorPage    |> ChangePage |> dispatch
    let openERL  = fun _ -> EmergencyListPage |> ChangePage |> dispatch

    let menu =
        [ Navbar.menuItem (Some FontAwesome.Fa.I.Ambulance) "Noodlijst" openERL 
          Navbar.divider
          Navbar.menuItem (Some FontAwesome.Fa.I.Calculator) "Calculators" openCalc
        ]

    let config = 
        Navbar.config (createBrand model) [] menu

    Navbar.navbarView (NavbarMsg >> dispatch) config model.NavbarModel


    // let calcMenu isMobile (model : NavbarMenu) =
    //     if isMobile && not model.CalculatorMenu then []
    //     else
    //         [ Navbar.Item.a [ Navbar.Item.Props  [ OnClick openPEWS ] ] [ str "Score Calculators" ] ]

    // let mainMenu isMobile (model : NavbarMenu) =
    //     if isMobile && not model.MainMenu then []
    //     else
    //         [ Navbar.Item.a [ Navbar.Item.Props [ OnClick openERL ] ] [ str "Acute Opvang" ]
    //           Navbar.Item.a [] [ str "Medicatie Voorschrijven" ] ]    

    // let iconModWhite = Icon.Modifiers [ Modifier.TextColor IsWhite ]

    // Navbar.navbar 
    //     [ Navbar.Color Color.IsDark
    //       Navbar.Modifiers [ Modifier.TextColor IsWhite ]
    //       Navbar.Props [ Style [ CSSProp.Padding "10px" ] ]
    //       Navbar.HasShadow ]
        
    //     [ Navbar.Brand.div [ ]
    //             [ show model ] 

    //       Navbar.End.div []
    //           [ Navbar.Item.div 
    //                 [ Navbar.Item.IsHoverable
    //                   Navbar.Item.HasDropdown ] 
    //                 [ Navbar.Link.div [ Navbar.Link.Props [OnClick (fun _ -> CalculatorMenuMsg |> MenuMsg |> dispatch )] ] 
    //                     [ Fulma.FontAwesome.Icon.faIcon 
    //                         [ Icon.Size IsSmall; iconModWhite ] 
    //                         [ FontAwesome.Fa.icon FontAwesome.Fa.I.Calculator ] ]
    //                   Navbar.Dropdown.div [ Navbar.Dropdown.IsRight ] 
    //                      (calcMenu model.Device.IsMobile model.ShowMenu) ]
                           
    //             Navbar.Item.div 
    //                 [ Navbar.Item.IsHoverable
    //                   Navbar.Item.HasDropdown ] 
    //                 [ Navbar.Link.div [ Navbar.Link.Props [OnClick (fun _ -> MainMenuMsg |> MenuMsg |> dispatch )] ]  
    //                     [ Fulma.FontAwesome.Icon.faIcon 
    //                         [ Icon.Size IsSmall; iconModWhite ] 
    //                         [ FontAwesome.Fa.icon FontAwesome.Fa.I.Bars ] ]
    //                   Navbar.Dropdown.div [ Navbar.Dropdown.IsRight ] 
    //                      (mainMenu model.Device.IsMobile model.ShowMenu) ] ] ]


let bottomView =
    Footer.footer [ ]
        [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Left) ] ]
            [ disclaimer ] ]


let view (model : Model) (dispatch : Msg -> unit) =

    let patView =
        div [ Style [ CSSProp.PaddingBottom "10px" ] ] 
            [ Patient.view model.Device.IsMobile model.PatientModel (PatientMsg >> dispatch) ]
    
    let content =
        match model.Page with
        | CalculatorPage    -> Calculator.view model.Device.IsMobile model.CalculatorModel (CalculatorMsg >> dispatch)
        | EmergencyListPage -> Emergency.view model.Device.IsMobile model.PatientModel model.EmergencyModel (EmergencyMsg >> dispatch)
    
    div [ ]
        [ model |> navbarView (dispatch)  

          Container.container [ Container.Props [Style [ CSSProp.Padding "10px"] ] ]
              [ patView
                content ]
          bottomView  ] 


#if DEBUG
open Elmish.Debug
open Elmish.HMR 
#endif

Program.mkProgram init update view
|> Program.toNavigable (parsePath Query.route) urlUpdate
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR  
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
