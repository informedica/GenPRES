namespace Informedica.MetaVision.Lib




[<AutoOpen>]
module Utils =

    open ClosedXML.Excel

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.ZForm.Lib.Utils
    open Informedica.ZIndex.Lib


    module Web =


        // Constraints spreadsheet
        //https://docs.google.com/spreadsheets/d/1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g/edit?usp=sharing
        [<Literal>]
        let dataUrlId = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        let download = Web.GoogleSheets.download


        let getDataFromSheet sheet = Web.GoogleSheets.getDataFromSheet dataUrlId sheet



    [<RequireQualifiedAccess>]
    module Array =

        let mapHeadings def headings xs =
            headings
            |> Array.map (fun h ->
                match xs |> Array.tryFind (fst >> (=) h) with
                | Some x -> x |> snd
                | None   -> def
            )


        let mapStringHeadings headings xs = mapHeadings "" headings xs



    let mappingRoute =
        Web.getDataFromSheet "RouteMapping"


    let mappingRouteShape =
        Web.getDataFromSheet "RouteShapeUnit"


    let mappingFreqs =
        Web.getDataFromSheet "Frequencies"


    let mappingUnits =
        Web.getDataFromSheet "Units"


    let mappingFormulary =
        Web.getDataFromSheet "Formulary"


    let mapParentMeds =
        Web.getDataFromSheet "ParentMeds"


    let mapEntFeeding =
        Web.getDataFromSheet "EntFeeding"


    let mapReconsitution =
        Web.getDataFromSheet "Reconstitution"



    [<Literal>]
    let NA = "NIET VAN TOEPASSING"

    let capitalize s =
            let s =
                s
                |> String.trim
                |> String.replace "'" ""

            match s |> String.trim |> String.splitAt '-' with
            | [|s1; s2|] -> $"{s1 |> String.capitalize}-{s2 |> String.capitalize}"
            | _ -> s |> String.capitalize


    let capitalizeRoute route =
        route
        |> capitalize
        |> fun s ->
            if s |> String.startsWith "Intra-" ||
               s |> String.startsWith "Intra" |> not then s
            else s |> String.replace "Intra" "Intra-" |> capitalize
            |> String.replace "-" ""


    let drugFamilyName code name = $"{code}. {name |> capitalize}"


    let mapRoute s =
        let s = s |> String.toLower |> String.trim
        mappingRoute
        |> Array.tryFind (fun xs -> xs[0] |> String.toLower |> String.trim = s)
        |> function
        | Some xs -> xs[1]
        | None ->
            $"cannot find route: |{s}|"
            |> failwith


    let isMultiple (gpp : GenPresProduct) =
        gpp.GenericProducts
        |> Array.exists (fun gp ->
            gp.Substances
            |> Array.map (fun s -> s.SubstanceName)
            |> Array.distinct
            |> Array.length > 1)


    let hasNoUnit (gpp : GenPresProduct) =
        gpp.GenericProducts
        |> Array.filter (fun gp ->
            gp.Substances
            |> Array.forall (fun s -> s.GenericUnit <> NA)
        )
        |> Array.isEmpty


    let filterRouteShapeUnit rte shape unt =
        mappingRouteShape
        |> Array.filter (fun xs ->
            let eqsRte = rte |> String.isNullOrWhiteSpace || rte |> String.trim |> String.equalsCapInsens xs[0]
            let eqsShp = shape |> String.isNullOrWhiteSpace || shape |> String.trim |> String.equalsCapInsens xs[1]
            let eqsUnt = unt |> String.isNullOrWhiteSpace || unt |> String.trim |> String.equalsCapInsens xs[2]
            eqsRte && eqsShp && eqsUnt
        )


    let shapeIsInfuseOver shape =
        filterRouteShapeUnit "" shape ""
        |> Array.map (fun xs -> xs[4] = "TRUE")
        |> Array.fold (fun acc b -> acc || b) false


    let shapeInDiluent rte unt shape =
        filterRouteShapeUnit rte shape unt
        |> Array.map (fun xs -> xs[5] = "TRUE")
        |> Array.fold (fun acc b -> acc || b) false


    let shapeIsSolution rte unt shape =
        if unt |> String.equalsCapInsens "druppel" ||
           unt = "mL" || unt = "µL" then true
        else
            filterRouteShapeUnit rte shape unt
            |> Array.map (fun xs -> xs[6] = "TRUE")
            |> Array.fold (fun acc b -> acc || b) false


    let shapeDoseUnit rts unt shape =
        rts
        |> Array.collect (fun rt -> filterRouteShapeUnit rt shape unt)
        |> Array.fold (fun acc xs ->
            match acc with
            | None   -> Some xs[3]
            | Some u ->
                if u |> String.startsWithCapsInsens xs[3] then acc
                else Some ""
        ) None


    let getATCCodes (gpp : GenPresProduct) =
        gpp.GenericProducts
        |> Array.map (fun gp -> gp.ATC |> String.trim)
        |> Array.distinct


    let getRoutesLongShort () =
        Names.getItems Names.Route Names.TwentyFive
        |> Array.map snd
        |> Array.zip (Names.getItems Names.Route Names.Fifty |> Array.map snd)
        |> Array.map (fun (l, s) ->
            l
            |> String.replace "," "/"
            |> String.replace "/ " "/",
            s
            |> String.replace "," "/"
            |> String.replace "/ " "/"
            |> String.toLower
        )
        |> Array.collect (fun (l, s) ->
            s
            |> String.splitAt '/'
            |> Array.zip (l |> String.splitAt '/')
        )
        |> Array.distinct
        |> Array.sort
        |> Array.map (fun (l, s) -> $"{l}\t{s}")
        |> Array.iter (printfn "%s")


    let mapUnit un =
        let un = un |> String.trim |> String.toLower

        mappingUnits
        |> Array.tryFind (fun r ->
            r[0] = un || r[1] = un
        )
        |> function
        | Some r -> r[2]
        | None   ->
            printfn $"cannot find {un} in mapping"
            un


    let mapBool b =
        if b then Constants.TRUE else Constants.FALSE


    let mapFreq freq =
        mappingFreqs
        |> Array.filter (fun r ->
            r[0] = freq
        )
        |> function
        | [||]   ->
            printfn $"cannot find {freq} in mapping"
            [||]
        | xs ->
            xs
            |> Array.collect (fun r -> r[1..2])
            |> Array.filter (String.isNullOrWhiteSpace >> not)
            |> Array.distinct


    let getFormulary gpk =
        mappingFormulary
        |> Array.skip 1
        |> Array.tryFind (fun xs -> xs[0] |> Int32.parse = gpk)
        |> function
        | Some xs ->
            [|
                if xs[1] |> String.notEmpty then UMCU
                if xs[6] = Constants.TRUE then ICC
                if xs[8] = Constants.TRUE then ICK
                if xs[7] = Constants.TRUE then NEO
            |]
        | None -> [||]


    let isSolutionUnit = Units.isVolumeUnit


    let removeEmptyUnitSubstances (gp : GenericProduct) =
        { gp with
            Substances =
                gp.Substances
                |> Array.filter (fun s ->
                    s.SubstanceUnit <> NA
                )
        }


    let filterGenericProducts gpps =
        gpps
        |> Array.filter (fun gpp ->
            gpp |> hasNoUnit |> not &&
            Data.excludeShapes
            |> Array.exists (fun s -> gpp.Shape |> String.equalsCapInsens s)
            |> not
        )
        |> Array.map (fun gpp ->
            { gpp with
                GenericProducts =
                    gpp.GenericProducts
                    |> Array.filter (fun gp ->
                        gp.Substances
                        |> Array.forall (fun s ->
                            s.SubstanceUnit
                            |> String.trim
                            |> String.equalsCapInsens NA
                            |> not &&
                            s.SubstanceUnit
                            |> String.trim
                            |> String.equalsCapInsens "niet gespecificeerd of meervoudig"
                            |> not
                        )
                    )
            }
        )
        |> Array.collect (fun gpp -> gpp.GenericProducts)
        |> Array.map removeEmptyUnitSubstances
        |> Array.filter (fun gp -> gp.Substances |> Array.isEmpty |> not)
        |> Array.distinct


    let getSynonyms (gp: GenericProduct) =
        GenPresProduct.get true
        |> Array.filter (fun gpp -> gpp.GenericProducts |> Array.exists ((=) gp))
        |> Array.collect (fun gpp ->
            gpp.GenericProducts
            |> Array.collect (fun gp ->
                gp.PrescriptionProducts
                |> Array.collect (fun pp ->
                    pp.TradeProducts
                    |> Array.map (fun tp -> tp.Brand)
                )
            )
        )
        |> Array.distinct


    let getFrequencies isInfusedOver (drs : DoseRule []) =
        drs
        |> Array.map (fun dr -> $"{dr.Freq.Frequency} {dr.Freq.Time}")
        |> Array.distinct
        |> Array.collect mapFreq
        |> Array.filter (fun s ->
            if isInfusedOver then true
            else
                s = "eContinuous"
                |> not
        )
        |> Array.distinct
        |> String.concat ";"


    let getDoseUnit (drs: DoseRule[]) =
        drs
        |> Array.fold(fun acc dr ->
            match acc with
            | None -> Some dr.Unit
            | Some u ->
                if u = dr.Unit then acc
                else Some ""
        ) None


    let getReconsitiution gpk rte dep =
        mapReconsitution
        |> Array.tryFind (fun xs ->
            xs[0] = gpk &&
            xs[3] = rte &&
            xs[5] = dep
        )
        |> Option.map (fun xs -> xs[8] |> Decimal.tryParse, xs[9])
        |> Option.filter (fun (n, _) -> n |> Option.isSome)
        |> Option.map (fun (n, s) -> n |> Option.get, s)


    let createDataImport (file : string) (newFile: string) (sheet : string) xs =
        if xs |> Array.isEmpty then ()
        else
            let xs =
                xs
                |> Array.map (String.split "\t")
                |> Array.map List.toArray
                |> Array.skip 1
            let wb =
                if newFile |> File.exists then new XLWorkbook(newFile)
                else new XLWorkbook(file)

            wb.Worksheet(sheet).Cell(2, 3).Value <- xs

            if newFile |> File.exists then wb.Save()
            else
                wb.SaveAs(newFile)


