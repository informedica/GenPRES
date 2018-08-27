module Client

open System

open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Fable.Core.JsInterop

open Shared

open Fulma
open System.Runtime.InteropServices
open Fable.Import.React
open Fable.Helpers.React.ReactiveComponents

module Treatment = Data.Treatment
module Math = Utils.Math

let calculateWeight yr mo =
    let age = (double yr) + (double mo) / 12.
    match age with
    | _ when age > 18.  -> 0.
    | _ when age >= 1.  -> age * 2.5 + 8.
    | _ when age >= 0.5 -> 6.
    | _ when age >= 0.  -> 3.5
    | _ -> 0.


let calcDoseVol kg doserPerKg conc min max =
    let d = 
        kg * doserPerKg
        |> (fun d ->
            if max > 0. && d > max then 
                max 
            else if min > 0. && d < min then
                min
            else d
        )

    let v =
        d / conc
        |> (fun v ->
            if v >= 10. then
                v |> Math.roundBy 1.
            else 
                v |> Math.roundBy 0.1
        )
        |> Math.fixPrecision 2

    (v * conc |> Math.fixPrecision 2, v)


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = { GenPres: GenPres option }


let getYears = function 
| { GenPres = Some x } ->
    x.Patient.Age.Years
| { GenPres = None } -> 0


let getMonths = function 
| { GenPres = Some x } ->
    x.Patient.Age.Months
| { GenPres = None } -> 0


// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| YearChange of string
| MonthChange of string
| GenPresLoaded of Result<GenPres, exn>


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    let initialModel = { GenPres = None }
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
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | YearChange s -> 
        printfn "Year: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            if i > 18 || i < 0 then currentModel, Cmd.none
            else
                let newModel = 
                    let w = calculateWeight i (currentModel |> getMonths)

                    match currentModel.GenPres with
                    | Some gp ->
                        let gp' = { gp with Patient = { gp.Patient with Age  =  { gp.Patient.Age  with Years = i }; Weight = w } }
                        { currentModel with GenPres = Some gp' }
                    | None -> currentModel
                newModel, Cmd.none
        | false, _ -> 
            currentModel, Cmd.none

    | MonthChange s -> 
        printfn "Month: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            if i > 11 || i < 0 then currentModel, Cmd.none
            else
                let newModel = 
                    let w = calculateWeight (currentModel |> getYears) i

                    match currentModel.GenPres with
                    | Some gp ->
                        let gp' = { gp with Patient = { gp.Patient with Age  =  { gp.Patient.Age  with Months = i }; Weight = w } }
                        { currentModel with GenPres = Some gp' }
                    | None -> currentModel
                newModel, Cmd.none
        | false, _ -> 
            currentModel, Cmd.none
    | GenPresLoaded (Ok genpres) ->
        let newModel = { GenPres = Some genpres }
        newModel, Cmd.none

    | _ -> currentModel, Cmd.none


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


let showPatient = function
| { GenPres = Some x } ->
    let wght = 
        let w = calculateWeight x.Patient.Age.Years x.Patient.Age.Months
        if w < 3.5 then "" else 
            ( w * 10. |> Math.Round ) / 10. |> string
    sprintf "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg" x.Patient.Age.Years x.Patient.Age.Months wght
| { GenPres = None } -> ""


let button txt onClick =
    Button.button
        [ Button.IsFullWidth
          Button.Color IsPrimary
          Button.OnClick onClick ]
        [ str txt ]


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
             [ Input.number [ Input.Value ph ] ]]


let createTable data =
    match data with
    | h::rs ->
        let header =
            thead [] (h |> List.map (fun s -> th [] [ str s ]))
        
        let body =
            rs 
            |> List.map (fun xs ->
                tr [] (xs |> List.map (fun x -> td [] [ str x]))
            )

        Table.table [ Table.IsBordered
                      Table.IsFullWidth
                      Table.IsStriped
                      Table.IsHoverable]
                    [ header; tbody [] body ]
    | _ -> div [] []


let treatment (model : Model) =
    match model with
    | { GenPres = Some (genpres) } ->
        let age = 
            let yrs = genpres.Patient.Age.Years |> double
            let mos = (genpres.Patient.Age.Months |> double) / 12.
            yrs + mos

        let wght = calculateWeight genpres.Patient.Age.Years genpres.Patient.Age.Months

        let tube = 
            let m = 
                4. + age / 4. 
                |> Math.roundBy0_5
                |> (fun m -> if m > 7. then 7. else m)
            sprintf "%A-%A-%A" (m - 0.5) m (m + 0.5)

        let oral = 
            let m = 12. + age / 2. |> Math.roundBy0_5
            sprintf "%A cm" m

        let nasal =
            let m = 15. + age / 2. |> Math.roundBy0_5
            sprintf "%A cm" m

        let epiIv = 
            let d, v =
                    calcDoseVol wght 0.01 0.1 0.01 0.5
            
            (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 2)) ,
            (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v * 10.))

        let epiTr = 
            let d, v =
                    calcDoseVol wght 0.1 0.1 0.1 5.
            
            (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 2)) ,
            (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v * 10.))

        let fluid =
            let d, _ =
                calcDoseVol wght 20. 1. 0. 500.
            
            (sprintf "%A ml NaCl 0.9%%" d) , ("")

        let defib =
            let j = 
                Treatment.joules
                |> Utils.List.findNearestMax (wght * 4. |> int)
            
            sprintf "%A Joule" j

        let cardio =
            let j = 
                Treatment.joules
                |> Utils.List.findNearestMax (wght * 2. |> int)
            
            sprintf "%A Joule" j

        let calcMeds (ind, item, dose, min, max, conc, unit, rem) =
            let d, v =
                calcDoseVol wght dose conc min max

            let adv =
                let minmax =
                    match (min = 0., max = 0.) with
                    | true,  true  -> ""
                    | true,  false -> sprintf "(max %A %s)" max unit
                    | false, true  -> sprintf "(min %A %s)" min unit
                    | false, false -> sprintf "(%A - %A %s)" min max unit

                sprintf "%A %s/kg %s" dose unit minmax

            [
                ind; item; (sprintf "%A %s (%A %s/kg)" d unit (d / wght |> Math.fixPrecision 2) unit); (sprintf "%A ml van %A %s/ml" v conc unit); adv 
            ]

        [ 
            [ "Indicatie"; "Interventie"; "Berekend"; "Bereiding"; "Advies" ]
            [ "Reanimatie"; "tube maat"; tube; ""; "4 + leeftijd / 4" ]
            [ "Reanimatie"; "tube lengte oraal"; oral; ""; "12 + leeftijd / 2" ]
            [ "Reanimatie"; "tube lengte nasaal"; nasal; ""; "15 + leeftijd / 2" ]
            [ "Reanimatie"; "epinephrine iv/io"; epiIv |> fst; epiIv |> snd; "0,01 mg/kg" ]
            [ "Reanimatie"; "epinephrine trach"; epiTr |> fst; epiTr |> snd; "0,1 mg/kg" ]
            [ "Reanimatie"; "vaatvulling"; fluid |> fst; fluid |> snd; "20 ml/kg" ]
            [ "Reanimatie"; "defibrillatie"; defib; ""; "4 Joule/kg" ]
            [ "Reanimatie"; "cardioversie"; cardio; ""; "2 Joule/kg" ]
        ] @ (Treatment.medicationDefs |> List.map calcMeds)

    | { GenPres = None } ->
        [ 
            [ "Indicatie"; "Interventie"; "Berekend"; "Bereiding"; "Advies" ]
        ]
    |> createTable


let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ Navbar.navbar [ Navbar.Color IsPrimary ; Navbar.Props [ Style [ CSSProp.Padding "30px"] ] ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ ]
                    [ str (show model) ] ] ]

          Container.container []
              [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h3 [] [ str (showPatient model) ] ]
                
                form [ ]
                    [ Field.div [ Field.IsHorizontal; Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] [ model |> getYears |> yrInput dispatch; model |> getMonths |> moInput dispatch ] ] 
                
                treatment model]
          
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
