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
        |> Option.map (fun r -> r.Short)


    let mappingRouteShape =
        Web.getDataFromSheet Web.dataUrlId2 "ShapeRoute"
        |> fun data ->
            let inline getColumn get =
                data
                |> Array.head
                |> get

            data
            |> Array.tail
            |> Array.map (fun r ->
                let getStr = getColumn Csv.getStringColumn r
                let getDec = getColumn Csv.getDecimalOptionColumn r

                {
                    Route = getStr "Route"
                    Shape = getStr "Shape"
                    Unit = getStr "Unit"
                    DoseUnit = getStr "Unit"
                    MinDoseQty = getDec "MinDoseQty"
                    MaxDoseQty = getDec "MaxDoseQty"
                    Timed = getStr "Timed" |> String.equalsCapInsens "true"
                    Reconstitute = getStr "Reconstitute" |> String.equalsCapInsens "true"
                    IsSolution = getStr "IsSolution" |> String.equalsCapInsens "true"
                }
            )


    let filterRouteShapeUnit rte shape unt =
        mappingRouteShape
        |> Array.filter (fun xs ->
            let eqsRte = 
                rte |> String.isNullOrWhiteSpace || 
                rte |> String.trim |> String.equalsCapInsens xs.Route ||
                xs.Route |> mapRoute |> Option.map (String.equalsCapInsens (rte |> String.trim)) |> Option.defaultValue false
            let eqsShp = shape |> String.isNullOrWhiteSpace || shape |> String.trim |> String.equalsCapInsens xs.Shape
            let eqsUnt = 
                unt |> String.isNullOrWhiteSpace || 
                unt |> String.trim |> String.equalsCapInsens xs.Unit ||
                xs.Unit |> mapUnit |> Option.map (String.equalsCapInsens (unt |> String.trim)) |> Option.defaultValue false
            eqsRte && eqsShp && eqsUnt
        )


    let requiresReconstitution rtes unt shape =
        rtes
        |> Array.collect (fun rte ->
            filterRouteShapeUnit rte shape unt
        )
        |> Array.map (fun xs -> xs.Reconstitute)
        |> Array.fold (fun acc b -> acc || b) false


        
