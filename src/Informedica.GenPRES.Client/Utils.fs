[<AutoOpen>]
module Utils


module String =

    let replace (oldS: string) newS (s: string) = s.Replace(oldS, newS)


open Fable.Core
open Feliz
open Browser.Types


let inline toJsx (el: ReactElement) : JSX.Element = unbox el

let inline toReact (el: JSX.Element) : ReactElement = unbox el

/// Enables use of Feliz styles within a JSX hole
let inline toStyle (styles: IStyleAttribute list) : obj = JsInterop.createObj (unbox styles)

let inline withKey els =
    els
    |> Array.mapi (fun i (key, el) ->
        let key = $"{key}-{i}"

        JSX.jsx
            $"""
        import React from 'react';

        <React.Fragment key={key}>
            {el}
        </React.Fragment>
        """
    )


let toClass (classes: (string * bool) list) : string =
    classes
    |> List.choose (fun (c, b) ->
        match c.Trim(), b with
        | "", _
        | _, false -> None
        | c, true -> Some c
    )
    |> String.concat " "


let onEnterOrEscape dispatchOnEnter dispatchOnEscape (ev: KeyboardEvent) =
    let el = ev.target :?> HTMLInputElement

    match ev.key with
    | "Enter" ->
        dispatchOnEnter el.value
        el.value <- ""
    | "Escape" ->
        dispatchOnEscape ()
        el.value <- ""
        el.blur ()
    | _ -> ()


module Logging =

    open Browser.Dom

    let log (msg: string) a = console.log (box msg, [| box a |])

    let error (msg: string) e = console.error (box msg, [| box e |])

    let warning (msg: string) a = console.warn (box msg, [| box a |])


module GoogleDocs =

    open Fable.SimpleHttp
    open Shared
    open Shared.Types

    // a sheet that cannot be read or parsed answers as a failure: an exception that escaped
    // would send no message at all, and the load would stay out for good
    let inline getUrl parseResponse url =
        async {
            try
                let! statusCode, responseText = Http.get url

                return
                    match statusCode with
                    | 200 -> responseText |> Csv.parseCSV |> parseResponse |> Ok
                    | _ -> Error $"Status {statusCode} => {responseText}"
            with ex ->
                return Error ex.Message
        }


    let createUrl sheet id =
        $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}"


    // The emergency-list spreadsheet. NOTE this is a SEPARATE workbook from the one
    // GENPRES_URL_ID points at: the emergency sheets are fetched here by the client
    // and never pass through GenFORM.Lib's resource loading. Its id is hard-coded
    // rather than configurable, so a deployment cannot point emergency data
    // elsewhere. Sheets used: "emergencylist", "continuousmeds", "products",
    // "weight", "height", "weight neo", "height neo"; the parsers live in
    // Shared/Models.fs.
    //https://docs.google.com/spreadsheets/d/1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM/edit?usp=sharing
    [<Literal>]
    let private dataEMLUrlId = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


    let private dataGPUrlId = "1M90b_kPmANIdPFTvsDVaEGIEeQQ3md-Bt359Dmc2vIE"


    open Shared.Models


    let loadBolusMedication () =
        dataEMLUrlId |> createUrl "emergencylist" |> getUrl EmergencyTreatment.parse


    let loadContinuousMedication () =
        dataEMLUrlId |> createUrl "continuousmeds" |> getUrl ContinuousMedication.parse


    let loadProducts () = dataEMLUrlId |> createUrl "products" |> getUrl Products.parse


    let loadLocalization () = dataGPUrlId |> createUrl "Localization" |> getUrl id


    let loadNormalWeight () = dataEMLUrlId |> createUrl "weight" |> getUrl NormalValues.parse


    let loadNormalHeight () = dataEMLUrlId |> createUrl "height" |> getUrl NormalValues.parse


    let loadNormalNeoWeight () =
        dataEMLUrlId |> createUrl "weight neo" |> getUrl NormalValues.parse


    let loadNeoHeight () =
        dataEMLUrlId |> createUrl "height neo" |> getUrl NormalValues.parse


    let loadNormalValues () =
        async {
            let! weightsResult = loadNormalWeight () |> Async.StartChild
            let! heightsResult = loadNormalHeight () |> Async.StartChild
            let! neoWeightsResult = loadNormalNeoWeight () |> Async.StartChild
            let! neoHeightsResult = loadNeoHeight () |> Async.StartChild

            let! weights = weightsResult
            let! heights = heightsResult
            let! neoWeights = neoWeightsResult
            let! neoHeights = neoHeightsResult

            return
                match weights, heights, neoWeights, neoHeights with
                | Ok w, Ok h, Ok nw, Ok nh ->
                    {
                        Weights = w
                        Heights = h
                        NeoWeights = nw
                        NeoHeights = nh
                    }
                    |> Ok
                | Error e, _, _, _
                | _, Error e, _, _
                | _, _, Error e, _
                | _, _, _, Error e -> Error e
        }
