module Calculator

open System

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Core.JsInterop
open Elmish
open Fulma

open Shared
open Patient

module Select = Component.Select
module Modal = Component.Modal
module NormalValueData = Data.NormalValues
module Patient = Models.Patient

module PEWS =

    type Model = 
        { 
            AgeInMo: int
            Score: int 
            Items : (string * Select.Model) List
        }


    let getPEWSAgeGroup a =
        let upper =
            match 
                NormalValueData.pews
                |> List.tryFind (fun (age, _) -> a < age) with
            | Some (a, _) -> a
            | None -> 0

        let lower =
            match 
                NormalValueData.pews
                |> List.rev
                |> List.tryFind (fun (age, _) -> a >= age) with
            | Some (a, _) -> a
            | None -> 0

        lower, upper


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
            Items = a |> get
        }


    type Msg = 
        | OpenPEWS of Shared.Models.Patient.Patient
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
                    option [ n |> box |> Value ] [ n |> str ]
                )

            Select.select []
                [ select [ Value value ]
                    opts
                ]

        Field.div [ ] 
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
                            Select.dropdownView xs (fun msg -> (i, msg) |> SelectItem |> dispatch) 
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
                div [] 
                    [ Heading.h5 [] [ str "PEWS Flow Diagram"]
                      Image.image []
                            [ img [ Src "images/PEWS.png" ] ] ]
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

module GlascowComaScale =

    type Model = 
        { 
            AgeInMo: int
            Score: string
            Items : (string * Select.Model) List
        }


    let getGCSAgeGroup a =
        let upper =
            match 
                NormalValueData.gcs
                |> List.tryFind (fun (age, _) -> a < age) with
            | Some (a, _) -> a
            | None -> 0

        let lower =
            match 
                NormalValueData.gcs
                |> List.rev
                |> List.tryFind (fun (age, _) -> a >= age) with
            | Some (a, _) -> a
            | None -> 0

        lower, upper


    let init a =
        let get a =
            match 
                NormalValueData.gcs
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
            Score = ""
            Items = get a
        }


    type Msg = 
        | OpenGCS of Shared.Models.Patient.Patient
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
                NormalValueData.gcs
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
        let (_, st, t) = 
            model.Items 
            |> List.fold (fun a (_, xs) ->
                let _, x = xs |> getSelected model.AgeInMo
                let (i, s, t) = a

                match x with
                | Some x -> 
                    let st =
                        match x with
                        | _ when x = 0 && i = 3 -> "c"
                        | _ when x = 0 && i = 1 -> "t" 
                        | _ -> x |> string

                    let sa = 
                        match i with 
                        | _ when i = 3 -> "E" + st
                        | _ when i = 2 -> "M" + st
                        | _ when i = 1 -> "V" + st
                        | _ -> ""

                    (i + 1, sa + s  , x + t)
                | None -> a
            ) (1, "", 0)

        { model with Score = sprintf "Score : %A (%s)" t st }


    let update (msg : Msg) (model : Model) =
        match msg with
        | OpenGCS pat ->
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
                    option [ n |> box |> Value ] [ n |> str ]
                )

            Select.select []
                [ select [ Value value ]
                    opts
                ]

        Field.div [ ] 
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

        let scoreText = model.Score

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
                            Select.dropdownView xs (fun msg -> (i, msg) |> SelectItem |> dispatch) 
                    toRow xs el
            )
            |> List.append 
                (
                    if isMobile then
                        [ tr [ Style [CSSProp.FontWeight "bold" ] ] [ td [  ] [ str "Totaal"  ] ; td [] [ scoreText |> str ] ] ]
                    else 
                        [ tr [ Style [CSSProp.FontWeight "bold" ] ] [ td [  ] [ str "Totaal"  ] ; td [] [ "" |> str ]; td [] [ str (model.Score |> string) ] ] ]
                )
            |> List.rev

        let content =
            div []
                [
                    Table.table [ Table.IsFullWidth
                                  Table.IsStriped
                                  Table.IsHoverable]
                        [ header
                          tbody []
                              items ]
                ]

        content



type Model = 
    { 
        ActiveTab : ActiveTab
        Patient : Shared.Models.Patient.Patient
        PEWSModel : PEWS.Model
        GlascowComaModel : GlascowComaScale.Model
    }
and ActiveTab =
    | PEWSTab
    | GlascowComaTab


type Msg = 
    | TabChange of ActiveTab
    | PEWSMsg of PEWS.Msg
    | GCSMsg of GlascowComaScale.Msg


let update (msg : Msg) (model : Model) =
    match msg with
    | TabChange tab ->
        { model with ActiveTab = tab }

    | PEWSMsg msg ->
        { model with PEWSModel = model.PEWSModel |> PEWS.update msg }

    | GCSMsg msg ->
        { model with GlascowComaModel = model.GlascowComaModel |> GlascowComaScale.update msg }


let init (pat : Shared.Models.Patient.Patient) =
    { 
        ActiveTab = PEWSTab
        Patient = pat
        PEWSModel = PEWS.init (pat.Age.Years * 12 + pat.Age.Months)
        GlascowComaModel = GlascowComaScale.init (pat.Age.Years * 12 + pat.Age.Months)
    }


let view isMobile (model: Model) dispatch =

    let pewsTab = 
        let l, u = PEWS.getPEWSAgeGroup model.PEWSModel.AgeInMo
        match l, u with
        | _, _ when l < 12 && u < 12 -> sprintf "PEWS Score %i tot %i maanden" (l) (u)
        | _, _ when l < 12           -> sprintf "PEWS Score %i maanden tot %i jaar" (l) (u/12)
        | _, _                       -> sprintf "PEWS Score %i tot %i jaar" (l/12) (u/12)

    let gcsTab = 
        let l, u = GlascowComaScale.getGCSAgeGroup model.GlascowComaModel.AgeInMo
        printfn "GCS age %A" u
        match u with
        | _ when u <= (5 * 12) -> sprintf "Glascow Coma Scale tot %i jaar" (u/12)
        | _                    -> sprintf "Glascow Coma Scale vanaf %i jaar" (l/12)

    let tabs (model : Model) dispatch =
        Tabs.tabs [ Tabs.IsFullWidth; Tabs.IsBoxed ] 
            [ Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = PEWSTab)
                         Tabs.Tab.Props [ OnClick (fun _ -> PEWSTab |> TabChange |> dispatch) ] ] [ a [] [str pewsTab] ]
                         
              Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = GlascowComaTab)
                         Tabs.Tab.Props [ OnClick (fun _ -> GlascowComaTab |> TabChange |> dispatch) ] ] [ a [] [str gcsTab ] ]           
            ]

    let content =
        match model.ActiveTab with
        | PEWSTab -> 
            PEWS.view isMobile model.PEWSModel (PEWSMsg >> dispatch)
        | GlascowComaTab ->
            GlascowComaScale.view isMobile model.GlascowComaModel (GCSMsg >> dispatch)

    div []
        [
            tabs model dispatch
            content
        ]