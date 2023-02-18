namespace Informedica.GenForm.Lib


module Mapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    let routeMapping =
        Web.getDataFromSheet Web.dataUrlId2 "Routes"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {|
                    Long = get "ZIndex"
                    Short = get "ShortDutch"
                |}
            )


    let unitMapping =
        Web.getDataFromSheet Web.dataUrlId2 "Units"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {|
                    Long = get "ZIndexUnitLong"
                    Short = get "Unit"
                    MV = get "MetaVisionUnit"
                |}
            )


    let mapRoute rte =
        routeMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens rte
        )
        |> Option.map (fun r -> r.Short)


    let mapUnit unt =
        unitMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens unt
        )
        |> Option.map (fun r -> r.MV)


    let mappingRouteShape =
        Web.getDataFromSheet Web.dataUrlId2  "ShapeRoute"


    let filterRouteShapeUnit rte shape unt =
        mappingRouteShape
        |> Array.filter (fun xs ->
            let eqsRte = rte |> String.isNullOrWhiteSpace || rte |> String.trim |> String.equalsCapInsens xs[0]
            let eqsShp = shape |> String.isNullOrWhiteSpace || shape |> String.trim |> String.equalsCapInsens xs[1]
            let eqsUnt = unt |> String.isNullOrWhiteSpace || unt |> String.trim |> String.equalsCapInsens xs[2]
            eqsRte && eqsShp && eqsUnt
        )


    let requiresReconstitution rtes unt shape =
        rtes
        |> Array.collect (fun rte ->
            filterRouteShapeUnit rte shape unt
        )
        |> Array.map (fun xs -> xs[5] = "TRUE")
        |> Array.fold (fun acc b -> acc || b) false

