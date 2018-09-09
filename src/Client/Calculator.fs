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
            AgeInMo: int
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
            AgeInMo = a
            Score = 0
            Items = get a
        }


    type Msg = 
        | OpenPEWS of Patient
        | SelectItem of int * Select.Msg
        | InputChange of int * string



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
                let _, s = xs |> getSelected model.AgeInMo
                match s with
                | Some x -> x + a
                | None -> a
            ) 0

        { model with Score = t }


    let update (msg : Msg) (model : Model) =
        match msg with
        | OpenPEWS pat ->
            { model with AgeInMo = pat.Age.Years * 12 + pat.Age.Months }

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

        | InputChange (i, s) ->
            { 
                model with 
                    Items = 
                        model.Items |> List.mapi (fun i' (n, xs) ->
                            if i' = i then 
                                n, 
                                { xs with 
                                    Items = xs.Items 
                                    |> List.map (fun item ->
                                        if item.Name = s then { item with Selected = true }
                                        else { item with Selected = false }
                                    )}
                            else n, xs
                        )
            }
            |> calculateTotal 




    let createInput value name cb vals =
        
        let inp = 
            let opts =
                vals
                |> List.map (fun n ->
                    option [ n |> Value ] [ n |> str ]
                )

            Select.select []
                [ select [ Value value ]
                    opts
                ]

        Field.div [ Field.IsHorizontal; Field.Props [ Style [ CSSProp.Padding "10px" ] ] ] 
            [ Label.label [] 
                [ str name ] 
              Control.div [ Control.Props [ OnChange cb ] ]
                [ inp ]
            ]



    let view isMobile (model : Model) (dispatch : Msg -> unit) =
        let header =
            if isMobile then
                thead []
                    [
                        th [] [ str "Item" ]
                        th [] [ str "Score" ]
                    ]
            else
                thead []
                    [
                        th [] [ str "Item" ]
                        th [] [ str "Selectie" ]
                        th [] [ str "Score" ]
                    ]

        let toRow m el =
            let sel, score = getSelected model.AgeInMo  m
            if isMobile then
                tr [] 
                    [ 
                        td [] [ div [] [ el ] ]
                        td [] [(if score |> Option.isSome then score |> Option.get |> string |> str else str "")] 
                    ]
            else
                tr [] 
                    [ 
                        td [] [ div [] [ el ] ]
                        td [] [sel |> str]
                        td [] [(if score |> Option.isSome then score |> Option.get |> string |> str else str "")] 
                    ]

        let scoreText =
            let txt = 
                match model.Score with
                | x when x >= 8 -> "binnen 10 minuten contact met d.d. arts"
                | x when x >= 6 -> "elk uur scoren"
                | x when x >= 4 -> "1 x per 4 uur scoren"
                | _ -> ""

            if isMobile then 
                if txt <> "" then sprintf  "%A (%s)" model.Score txt else sprintf "%A" model.Score
            else txt

        let items =
            model.Items
            |> List.mapi (fun i (_, xs) ->
                    let el = 
                        if isMobile then
                            let value  =
                                match xs.Items |> List.tryFind (fun item -> item.Selected) with
                                | Some item -> item.Name
                                | None -> "" 

                            xs.Items
                            |> List.map (fun item -> item.Name)
                            |> List.append [""]
                            |> createInput value xs.Title (fun ev -> (i, !! ev.target?value ) |> InputChange |> dispatch)
                        else 
                            Select.view xs (fun msg -> (i, msg) |> SelectItem |> dispatch) 
                    toRow xs el
            )
            |> List.append 
                (
                    if isMobile then
                        [ tr [ Style [CSSProp.FontWeight "bold" ] ] [ td [  ] [ str "Totaal"  ] ; td [] [ scoreText |> str ] ] ]
                    else 
                        [ tr [ Style [CSSProp.FontWeight "bold" ] ] [ td [  ] [ str "Totaal"  ] ; td [] [ scoreText |> str ]; td [] [ str (model.Score |> string) ] ] ]
                )
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


let view isMobile (model: Model) dispatch =

    let tabs (model : Model) dispatch =
        Tabs.tabs [ Tabs.IsFullWidth; Tabs.IsBoxed ] 
            [ Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = PEWSTab)
                         Tabs.Tab.Props [ OnClick (fun _ -> PEWSTab |> TabChange |> dispatch) ] ] [ a [] [str "PEWS Score"] ] ]

    let content =
        if model.ActiveTab = PEWSTab then 
            PEWS.view isMobile model.PEWSModel (PEWSMsg >> dispatch)
        else div [] []   

    div []
        [
            tabs model dispatch
            content
        ]