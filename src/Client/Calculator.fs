module Calculator

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma

open Shared
open System.Xml.Xsl

module Select = Component.Select
module Modal = Component.Modal
module NormalValueData = Data.NormalValueData

module PEWS =

    type Model = 
        { 
            Age: int
            Score: int 
            Items : (string * Select.Model) List
        }


    let init a =
        let get a =
            match 
                NormalValueData.pews
                |> List.tryFind (fun (age, _) -> a < age) with
            | Some (_, xs) ->
                xs 
                |> List.fold (fun a (n, xs') ->
                    let sel =
                        xs'
                        |> List.map (fun (_, n) ->
                            n
                        )
                        |> List.filter (String.IsNullOrEmpty >> not)
                        |> Select.init false n 
                    a
                    |> List.append [n, sel]
                ) []
            | None -> []
        { 
            Age = a
            Score = 0
            Items = get a
        }


    type Msg = 
        | OpenPEWS of Patient
        | SelectItem of int * Select.Msg



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
                    | Some (s', _) -> s' |> Some
                    | None -> None
                | None -> None
            | None -> None
    
        n, s


    let calculateTotal (model : Model) = 
        let t = 
            model.Items 
            |> List.fold (fun a (_, xs) ->
                let _, s = xs |> getSelected model.Age
                match s with
                | Some x -> x + a
                | None -> a
            ) 0

        { model with Score = t }


    let update (msg : Msg) (model : Model) =
        match msg with
        | OpenPEWS pat ->
            { model with Age = pat.Age.Years * 12 + pat.Age.Months }

        | SelectItem (i, msg) ->
            { 
                model with 
                    Items = 
                        model.Items |> List.mapi (fun i' (n,xs) ->
                            if i' = i then n, xs |> Select.update msg
                            else n, xs
                        )
            }
            |> calculateTotal 


    let view (model : Model) (dispatch : Msg -> unit) =
        let header =
            thead []
                [
                    th [] [ str "Item" ]
                    th [] [ str "Selectie" ]
                    th [] [ str "Score" ]
                ]

        let toRow m el =
            let sel, score = getSelected model.Age  m
            tr [] [ td [] [ div [] [ el ] ]; td [] [sel |> str]; td [] [(if score |> Option.isSome then score |> Option.get |> string |> str else str "")] ]

        let scoreText =
            match model.Score with
            | x when x >= 8 -> "binnen 10 minuten contact met d.d. arts"
            | x when x >= 6 -> "elk uur scoren"
            | x when x >= 4 -> "1 x/4 uur scoren"
            | _ -> ""

        let items =
            model.Items
            |> List.mapi (fun i (_, xs) ->
                    let el = Select.view xs (fun msg -> (i, msg) |> SelectItem |> dispatch) 
                    toRow xs el
            )
            |> List.append [ tr [ Style [CSSProp.FontWeight "bold" ] ] [ td [  ] [ str "Totaal"  ] ; td [] [ str scoreText ]; td [] [ str (model.Score |> string) ] ] ]
            |> List.rev

        let content =
            let pewsImg =
                Image.image []
                    [ img [ Src "images/PEWS.png" ] ]
            div []
                [
                    Table.table [ Table.IsFullWidth
                                  Table.IsStriped
                                  Table.IsHoverable]
                        [ header
                          tbody []
                              items ]
                    pewsImg
                ]

        content



type Model = 
    { 
        ActiveTab : ActiveTab
        Patient : Patient
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

let init (pat : Patient) =
    { 
        ActiveTab = PEWSTab
        Patient = pat
        PEWSModel = PEWS.init (pat.Age.Years * 12 + pat.Age.Months)
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