
#load "load.fsx"

#time

open System

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib


Environment.CurrentDirectory



Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "1")

let dataUrl = "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks"


let prods =
    Web.GoogleSheets.getCsvDataFromSheetSync dataUrl "Formulary"
    |> Array.skip 1
    |> Array.map (fun row -> row[0] |> int)
    |> Array.filter (fun gpk -> gpk < 90_000_000)
    |> Array.map (fun gpk ->
        GenPresProduct.findByGPK gpk
        |> Array.tryHead
        |> Option.map (fun prod ->
            prod.Name,
            prod.Form,
            prod.GenericProducts
            |> Array.filter (fun gp -> gp.Id = gpk)
            |> Array.collect (fun gp ->
                gp.PrescriptionProducts
                |> Array.collect (fun pp ->
                    pp.TradeProducts
                    |> Array.map (fun tp -> tp.Brand)
                )
            )
            |> Array.filter (String.isNullOrWhiteSpace >> not)
            |> Array.distinct
            |> String.concat "; ",
            prod.GenericProducts
            |> Array.filter (fun gp -> gp.Id = gpk)
            |> Array.map (fun gp ->
                gp.Substances
                |> Array.map _.GenericName
                |> String.concat "/"
            )
            |> Array.distinct
            |> Array.tryExactlyOne
            |> Option.defaultValue ""
        )
        |> Option.defaultValue ("", "", "", "")
    )


prods |> Array.iter (fun (name, form, brands, genName) -> printfn $"{name}\t{form}\t{brands}\t{genName}")


GenPresProduct.findByGPK 21000