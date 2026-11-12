// Tool script: lists the generic products of the product cache that have no row in
// the Formulary sheet, as rows in the column order of that sheet, ready to append.
//
// Run from this directory: dotnet fsi FormularyGpks.fsx
// The spreadsheet is the one named by GENPRES_URL_ID, taken from the environment or
// from the .env file of the repository. The rows are written to
// data/sources/formulary-missing-gpks.tsv, tab separated, without a header row.

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open System.IO
open Informedica.Utils.Lib
open Informedica.ZIndex.Lib


// Anchored on the directory of this script, so the path does not depend on the
// current directory that load.fsx sets.
let outputPath =
    Path.Combine(__SOURCE_DIRECTORY__, "../../../data/sources/formulary-missing-gpks.tsv")
    |> Path.GetFullPath


/// The generic products that are deliberately left out of the Formulary sheet, by GPK.
let excludedGpks =
    Set.ofList
        [
            // a product without an active substance
            55697
            // piperacilline/tazobactam 200 g / 25 g, a bulk bag for compounding, not a vial
            199141
        ]


/// The id of the spreadsheet, from the environment or the .env file.
let dataUrlId () =
    Env.loadDotEnv () |> ignore

    match Env.getItem "GENPRES_URL_ID" with
    | Some id when id |> String.IsNullOrWhiteSpace |> not -> Ok id
    | _ -> Error "GENPRES_URL_ID is not set: set it in the environment or in the .env file"


/// The header row and the GPKs of the Formulary sheet.
let readFormulary dataUrlId =
    Web.GoogleSheets.getCsvDataFromSheetSync dataUrlId "Formulary"
    |> Result.map (fun rows ->
        let header = rows |> Array.head
        let gpkColumn = header |> Array.findIndex ((=) "GPKODE")

        header, rows |> Array.tail |> Array.map (Array.item gpkColumn) |> Set.ofArray
    )


/// The brands of the trade products of a generic product, each brand once.
let brands (gp: Types.GenericProduct) =
    gp.PrescriptionProducts
    |> Array.collect _.TradeProducts
    |> Array.map _.Brand
    |> Array.filter (String.IsNullOrWhiteSpace >> not)
    |> Array.distinct
    |> String.concat "; "


/// The generic names of the active substances of a generic product, in sort order.
let genericName (gp: Types.GenericProduct) =
    gp.Substances
    |> Array.filter (_.IsAdditional >> not)
    |> Array.sortBy _.SortOrder
    |> Array.map _.GenericName
    |> Array.distinct
    |> String.concat "/"


/// The Formulary cells of a generic product, by column name. A column that is not
/// named here stays empty.
let cells (gpp: Types.GenPresProduct) (gp: Types.GenericProduct) =
    Map.ofList
        [
            "GPKODE", string gp.Id
            "Type", "medication"
            "Generic", gpp.Name
            "GStand", gpp.Name
            "Form", gp.Form
            "Brand", brands gp
            "GenName", genericName gp
        ]


/// One Formulary row for each generic product of the cache that the sheet lacks and
/// that is not excluded.
let missingRows header formularyGpks =
    GenPresProduct.get []
    |> Array.collect (fun gpp -> gpp.GenericProducts |> Array.map (fun gp -> gpp, gp))
    |> Array.filter (fun (_, gp) ->
        formularyGpks |> Set.contains (string gp.Id) |> not
        && excludedGpks |> Set.contains gp.Id |> not
    )
    |> Array.distinctBy (fun (_, gp) -> gp.Id)
    |> Array.sortBy (fun (gpp, gp) -> gpp.Name, gp.Form, gp.Id)
    |> Array.map (fun (gpp, gp) ->
        let cells = cells gpp gp

        header
        |> Array.map (fun column -> cells |> Map.tryFind column |> Option.defaultValue "")
    )


match dataUrlId () |> Result.bind readFormulary with
| Error err -> eprintfn $"%s{err}"
| Ok(header, formularyGpks) ->
    let rows = missingRows header formularyGpks

    File.WriteAllLines(outputPath, rows |> Array.map (String.concat "\t"))

    printfn $"Formulary sheet: %i{formularyGpks |> Set.count} GPKs"
    printfn $"Excluded: %i{excludedGpks |> Set.count} generic products"
    printfn $"Missing from the sheet: %i{rows |> Array.length} generic products"
    printfn $"Written to %s{outputPath}"
