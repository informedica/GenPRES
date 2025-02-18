[<AutoOpen>]
module Utils

open System


module String =

    let replace (oldS: string) newS  (s : string) = s.Replace(oldS, newS)


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

        JSX.jsx $"""
        import React from 'react';

        <React.Fragment key={key}>
            {el}
        </React.Fragment>
        """)


let toClass (classes: (string * bool) list) : string =
    classes
    |> List.choose (fun (c, b) ->
        match c.Trim(), b with
        | "", _
        | _, false -> None
        | c, true -> Some c)
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

    open System
    open Fable.SimpleHttp
    open Shared

    let inline getUrl parseResponse msg url =
        async {
            let! (statusCode, responseText) = Http.get url

            let result =
                match statusCode with
                | 200 ->
                    responseText
                    |> Csv.parseCSV
                    |> parseResponse
                    |> Ok
                    |> Finished
                | _ -> Finished(Error $"Status {statusCode} => {responseText}")
                |> msg

            return result
        }


    let createUrl sheet id =
        $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}"


    //https://docs.google.com/spreadsheets/d/1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM/edit?usp=sharing
    [<Literal>]
    let dataEMLUrlId =
        "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


    let dataGPUrlId =
        "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"


    let loadBolusMedication msg =
        dataEMLUrlId
        |> createUrl "emergencylist"
        |> getUrl EmergencyTreatment.parse msg


    let loadContinuousMedication msg =
        dataEMLUrlId
        |> createUrl "continuousmeds"
        |> getUrl ContinuousMedication.parse msg


    let loadProducts msg =
        dataEMLUrlId
        |> createUrl "products"
        |> getUrl Products.parse msg


    let loadLocalization msg =
        dataGPUrlId
        |> createUrl "Localization"
        |> getUrl id msg
