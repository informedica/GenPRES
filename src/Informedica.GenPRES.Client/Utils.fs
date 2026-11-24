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


    // The emergency list spreadsheet, a separate workbook from the one GENPRES_URL_ID selects. The
    // client fetches its sheet "products" directly, not through the GenFORM resource loading. The id is
    // fixed in code, so a deployment cannot point the emergency data elsewhere. The parser is in
    // Shared/Models.fs.
    //https://docs.google.com/spreadsheets/d/1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM/edit?usp=sharing
    [<Literal>]
    let private dataEMLUrlId = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


    let private dataGPUrlId = "1M90b_kPmANIdPFTvsDVaEGIEeQQ3md-Bt359Dmc2vIE"


    open Informedica.GenPRES.Shared.Models


    let loadProducts () = dataEMLUrlId |> createUrl "products" |> getUrl Products.parse


    let loadLocalization () = dataGPUrlId |> createUrl "Localization" |> getUrl id
