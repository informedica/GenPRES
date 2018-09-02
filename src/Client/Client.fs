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
open Fulma
open Component

module Treatment = Data.Treatment
module Math = Utils.Math
module Select = Component.Select

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
type Model = { GenPres: GenPres option; Device : Device; Selections : Select.Model; ActiveTab : string }
and Device = Computer | Tablet | Mobile

let createDevice x =
    if x < 1000. then Mobile
    else if x < 2000. then Tablet
    else Computer

let getYears = function 
| { GenPres = Some x } ->
    x.Patient.Age.Years
| { GenPres = None } -> 0


let getMonths = function 
| { GenPres = Some x } ->
    x.Patient.Age.Months
| { GenPres = None } -> 0


let getWeight = function 
| { GenPres = Some x } ->
    if x.Patient.Weight.Measured = 0. then x.Patient.Weight.Estimated else x.Patient.Weight.Measured
| { GenPres = None } -> 0.


// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| TabChange of string
| YearChange of string
| MonthChange of string
| WeightChange of string
| ClearInput
| Select of Select.Msg
| GenPresLoaded of Result<GenPres, exn>


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    printfn "Screen: x = %A, y = %A" Fable.Import.Browser.screen.width Fable.Import.Browser.screen.height
    
    let selections =
        Treatment.medicationDefs
        |> List.map (fun (cat, _, _, _, _, _, _, _) ->
            cat
        )
        |> List.distinct
        |> List.sort
        |> List.append [ "alles" ]
        |> Select.init

    let patient = { Age = { Years = 0; Months = 0 }; Weight = { Estimated = 2. ; Measured = 0. } }
    
    let genpres = { Name = "GenPres OFFLINE"; Version = "0.01"; Patient = patient }
    
    let initialModel = 
        { 
            GenPres = Some genpres
            Device = Fable.Import.Browser.screen.width |> createDevice
            Selections = selections 
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
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | TabChange tab ->
        let selections =
            if tab = "Noodlijst" then
                Treatment.medicationDefs
                |> List.map (fun (cat, _, _, _, _, _, _, _) ->
                    cat
                )
                |> List.distinct
                |> List.sort
                |> List.append [ "alles" ]
            else if tab = "Standaard Pompen" then
                Treatment.contMeds
                |> List.map Treatment.createContMed
                |> List.map (fun med -> med.Indication)
                |> List.distinct
                |> List.sort
                |> List.append [ "alles" ]
            else []
            |> List.sort
            |> Select.init            

        let updatedModel = { currentModel with ActiveTab = tab; Selections = selections }
        updatedModel, Cmd.none

    | YearChange s -> 
        printfn "Year: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            if i > 18 || i < 0 then currentModel, Cmd.none
            else
                let newModel = 

                    match currentModel.GenPres with
                    | Some gp ->
                        let w =  calculateWeight i (currentModel |> getMonths)
                        let gp' = { gp with Patient = { gp.Patient with Age  =  { gp.Patient.Age  with Years = i }; Weight = { gp.Patient.Weight with Estimated = w } } }
                        { currentModel with GenPres = Some gp' }
                    | None -> currentModel
                newModel, Cmd.none
        | false, _ -> 
            currentModel, Cmd.none

    | MonthChange s -> 
        printfn "Month: %s" s
        match s |> Int32.TryParse with
        | true, i -> 
            let newModel = 

                match currentModel.GenPres with
                | Some gp ->
                    let w = calculateWeight (currentModel |> getYears) i

                    let y = 
                        if i = 12 && gp.Patient.Age.Years < 18 then 
                            gp.Patient.Age.Years + 1 
                        else if i = -1 && gp.Patient.Age.Years > 0 then  
                            gp.Patient.Age.Years - 1
                        else
                            gp.Patient.Age.Years

                    let m =
                        if i >= 12 then 0
                        else if i = -1 then 11
                        else i
                       
                    let gp' = { gp with Patient = { gp.Patient with Age = { gp.Patient.Age with Months = m; Years = y }; Weight = { gp.Patient.Weight with Estimated = w } } }
                    { currentModel with GenPres = Some gp' }
                | None -> currentModel
            newModel, Cmd.none
        | false, _ -> 
            currentModel, Cmd.none

    | WeightChange s ->
        printf "Weight: %s" s
        match s |> Double.TryParse with
        | true, x ->
            let x = x |> Math.fixPrecision 2
            if x < 2. then currentModel, Cmd.none
            else
                let newModel =
                    match currentModel.GenPres with
                    | Some gp ->
                        let gp' = { gp with Patient = { gp.Patient with Weight = { gp.Patient.Weight with Measured = x }}}
                        { currentModel with GenPres = Some gp' }
                    | None -> currentModel
                newModel, Cmd.none
        | false, _ -> currentModel, Cmd.none

    | Select msg ->
        let model, cmd = Select.update msg currentModel.Selections
        { currentModel with Selections = model}, Cmd.map Select cmd

    | GenPresLoaded (Ok genpres) ->
        let newModel = { currentModel with GenPres = Some genpres }
        printfn "active tab: %s" currentModel.ActiveTab
        newModel, Cmd.none

    | ClearInput ->
        let newModel =
            match currentModel.GenPres with
            | Some gp ->
                let pat = { Age = { Years = 0; Months = 0 }; Weight = { Estimated = 2.; Measured = 0. } }
                let gp' = { gp with Patient = pat }
                { currentModel with GenPres = Some gp' }
            | None -> currentModel
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
        let w = if x.Patient.Weight.Measured = 0. then x.Patient.Weight.Estimated else x.Patient.Weight.Measured
        if w < 2. then "" else 
            ( w * 10. |> Math.Round ) / 10. |> string
    let e = x.Patient.Weight.Estimated |> Math.fixPrecision 2 |> string
    sprintf "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg (geschat %s kg)" x.Patient.Age.Years x.Patient.Age.Months wght e
| { GenPres = None } -> ""



let button txt onClick =
    Field.div [ Field.Props [ Style [ CSSProp.Padding "10px"] ]]
              [
                Button.button
                    [ Button.IsFullWidth
                      Button.Color IsPrimary
                      Button.OnClick (fun _ -> ClearInput |> onClick) ]
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

        let wght = model |> getWeight

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
            (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v / 10. |> Math.fixPrecision 2))

        let epiTr = 
            let d, v =
                    calcDoseVol wght 0.1 0.1 0.1 5.
            
            (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 2)) ,
            (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v / 10. |> Math.fixPrecision 2))

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

            let adv s =
                if s <> "" then s
                else
                    let minmax =
                        match (min = 0., max = 0.) with
                        | true,  true  -> ""
                        | true,  false -> sprintf "(max %A %s)" max unit
                        | false, true  -> sprintf "(min %A %s)" min unit
                        | false, false -> sprintf "(%A - %A %s)" min max unit

                    sprintf "%A %s/kg %s" dose unit minmax

            [
                ind; item; (sprintf "%A %s (%A %s/kg)" d unit (d / wght |> Math.fixPrecision 2) unit); (sprintf "%A ml van %A %s/ml" v conc unit); adv rem 
            ]

        let selected = 
            if model.Selections.Head.Selected then []
            else
                model.Selections
                |> List.filter (fun item -> item.Selected)
                |> List.map (fun item -> item.Name)
        
        [ 
            [ "reanimatie"; "tube maat"; tube; ""; "4 + leeftijd / 4" ]
            [ "reanimatie"; "tube lengte oraal"; oral; ""; "12 + leeftijd / 2" ]
            [ "reanimatie"; "tube lengte nasaal"; nasal; ""; "15 + leeftijd / 2" ]
            [ "reanimatie"; "epinephrine iv/io"; epiIv |> fst; epiIv |> snd; "0,01 mg/kg" ]
            [ "reanimatie"; "epinephrine trach"; epiTr |> fst; epiTr |> snd; "0,1 mg/kg" ]
            [ "reanimatie"; "vaatvulling"; fluid |> fst; fluid |> snd; "20 ml/kg" ]
            [ "reanimatie"; "defibrillatie"; defib; ""; "4 Joule/kg" ]
            [ "reanimatie"; "cardioversie"; cardio; ""; "2 Joule/kg" ]
        ] @ (Treatment.medicationDefs |> List.map calcMeds)
        |> List.filter (fun xs ->
            selected = List.empty || selected |> List.exists ((=) xs.Head)
        )
        |> List.append [[ "Indicatie"; "Interventie"; "Berekend"; "Bereiding"; "Advies" ]]

    | { GenPres = None } ->
        [ 
            [ "Indicatie"; "Interventie"; "Berekend"; "Bereiding"; "Advies" ]
        ]
    |> createTable


let contMeds (model : Model) =
    let selected = 
        if model.Selections.Head.Selected then []
        else
            model.Selections
            |> List.filter (fun item -> item.Selected)
            |> List.map (fun item -> item.Name)
        
    Treatment.calcContMed ((model |> getYears |> float) + ((model |> getMonths |> float) / 12.)) (model |> getWeight)
    |> List.filter (fun xs ->
        if selected |> List.isEmpty then true
        else
            selected |> List.exists ((=) xs.Head)
    )
    |> List.append [ [ "Indicatie"; "Generiek"; "Hoeveelheid"; "Oplossing"; "Dosering (stand 1 ml/uur)"; "Advies" ] ]
    |> createTable


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
                form [ ]
                    [ 
                        Field.div [ Field.IsHorizontal; Field.Props [ Style [ ] ] ] 
                                  [ model |> getYears  |> yrInput dispatch
                                    model |> getMonths |> moInput dispatch
                                    model |> getWeight |> wtInput dispatch ] 
                        button "Verwijder" dispatch ; Select.view model.Selections (Select >> dispatch)  ] 
                
                Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h5 [] [ str (showPatient model) ] ]
                
                
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
