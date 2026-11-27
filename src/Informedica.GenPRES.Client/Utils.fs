[<AutoOpen>]
module Utils


open Fable.Core
open Feliz


let inline toJsx (el: ReactElement) : JSX.Element = unbox el

let inline toReact (el: JSX.Element) : ReactElement = unbox el

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


module Logging =

    open Browser.Dom

    let log (msg: string) a = console.log (box msg, [| box a |])

    let error (msg: string) e = console.error (box msg, [| box e |])

    let warning (msg: string) a = console.warn (box msg, [| box a |])


module GoogleDocs =

    open Fable.SimpleHttp
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types

    // A sheet that cannot be read or parsed returns an Error. An exception would send no message,
    // and the load would stay pending forever.
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


    let private dataGPUrlId = "1M90b_kPmANIdPFTvsDVaEGIEeQQ3md-Bt359Dmc2vIE"


    let loadLocalization () = dataGPUrlId |> createUrl "Localization" |> getUrl id
