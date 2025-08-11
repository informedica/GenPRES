namespace Informedica.GenForm.Lib


module Mapping =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open GenFormResult


    module Constants =

        let [<Literal>] unitMappingSheet = "Units"

        let [<Literal>] routeMappingSheet = "Routes"

        let [<Literal>] validShapesSheet = "ValidShapes"

        let [<Literal>] shapeRoutesSheet = "ShapeRoute"



    let getData dataUrlId sheet f =
        try
            Web.getDataFromSheet dataUrlId sheet
            |> fun data ->
                match data |> Array.tryHead with
                | None -> 
                    [
                        ("Sheet is empty or not found", None)
                        |> ErrorMsg
                    ]
                    |> Error
                | Some h ->
                    let getStringColumn = Csv.getStringColumn h
                    let getFloatOptColumn = Csv.getFloatOptionColumn h

                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let getString = getStringColumn r
                        let getFloat = getFloatOptColumn r

                        f getString getFloat
                    )
                    |> createOk
        with
        | exn -> createError "getData" exn


    let getRouteMapping dataUrlId =
        fun get _ ->
            {
                Long = get "ZIndex"
                Short = get "ShortDutch"
            }
        |> getData dataUrlId Constants.routeMappingSheet
        |> mapErrorSource "getRouteMapping"



    let getUnitMapping dataUrlId =
        fun get _ ->
            {
                Long = get "ZIndexUnitLong"
                Short = get "Unit"
                MV = get "MetaVisionUnit"
                Group = get "Group"
            }
        |> getData dataUrlId Constants.unitMappingSheet
        |> mapErrorSource "getUnitMapping"


    let mapUnit (mapping : UnitMapping array) s =
        if s |> String.isNullOrWhiteSpace then None
        else
            let s = s |> String.trim
            mapping
            |> Array.tryFind (fun r ->
                r.Long |> String.equalsCapInsens s ||
                r.Short |> String.equalsCapInsens s ||
                r.MV |> String.equalsCapInsens s
            )
            |> function
                | Some r -> $"{r.Short}[{r.Group}]" |> Units.fromString
                | None -> None


    let mapRoute (mapping : RouteMapping array) s =
        if s |> String.isNullOrWhiteSpace then None
        else
            let s = s |> String.trim
            mapping
            |> Array.tryFind (fun r ->
                r.Long |> String.equalsCapInsens s ||
                r.Short |> String.equalsCapInsens s
            )
            |> Option.map _.Long


    let eqsRoute routeMapping r1 r2 =
        let mapRoute = mapRoute routeMapping

        if r1 |> Option.isNone then true
        else
            match r1.Value |> mapRoute, r2 |> mapRoute with
            | Some r1, Some r2 -> r1 = r2
            | _ -> false


    let getShapeRoutes dataUrlId unitMapping =
        let mapUnit = mapUnit unitMapping
        
        fun getStr getFlt ->
            let un = getStr "Unit" |> mapUnit |> Option.defaultValue NoUnit

            {
                Route = getStr "Route"
                Shape = getStr "Shape"
                Unit = un
                DoseUnit = getStr "DoseUnit" |> mapUnit |> Option.defaultValue NoUnit
                MinDoseQty =
                    if un = NoUnit then None
                    else
                        getFlt "MinDoseQty"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit un)
                MaxDoseQty =
                    if un = NoUnit then None
                    else
                        getFlt "MaxDoseQty"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit un)
                MinDoseQtyPerKg =
                    if un = NoUnit then None
                    else
                        let un = un |> Units.per Units.Weight.kiloGram

                        getFlt "MinDoseQtyKg"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit un)
                MaxDoseQtyPerKg =
                    if un = NoUnit then None
                    else
                        let un = un |> Units.per Units.Weight.kiloGram

                        getFlt "MaxDoseQtyKg"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit un)
                Divisibility =
                    getFlt "Divisible"
                    |> Option.bind BigRational.fromFloat
                Timed = getStr "Timed" |> String.equalsCapInsens "true"
                Reconstitute = getStr "Reconstitute" |> String.equalsCapInsens "true"
                IsSolution = getStr "IsSolution" |> String.equalsCapInsens "true"
            }
            |> fun rs ->
                match rs.DoseUnit with
                | NoUnit -> rs
                | du ->
                    { rs with
                        MinDoseQty =
                            getFlt "MinDoseQty"
                            |> Option.bind (fun v ->
                                v
                                |> BigRational.fromFloat
                                |> Option.map (ValueUnit.singleWithUnit du)
                            )
                        MaxDoseQty =
                            getFlt "MaxDoseQty"
                            |> Option.bind (fun v ->
                                v
                                |> BigRational.fromFloat
                                |> Option.map (ValueUnit.singleWithUnit du)
                            )
                    }
        |> getData dataUrlId Constants.shapeRoutesSheet
        |> mapErrorSource "getShapeRoutes"


    let filterShapeRoutes routeMapping (mapping : ShapeRoute []) rte shape unt =
        let mapRoute = mapRoute routeMapping

        mapping
        |> Array.filter (fun xs ->
            let eqsRte =
                rte |> String.isNullOrWhiteSpace ||
                rte |> String.trim |> String.equalsCapInsens xs.Route ||
                xs.Route |> mapRoute |> Option.map (String.equalsCapInsens (rte |> String.trim)) |> Option.defaultValue false
            let eqsShp = shape |> String.isNullOrWhiteSpace || shape |> String.trim |> String.equalsCapInsens xs.Shape
            let eqsUnt =
                unt = NoUnit ||
                unt |> Units.eqsUnit xs.Unit
            eqsRte && eqsShp && eqsUnt
        )


    let requiresReconstitution routeMapping shapeRoutes (rtes, unt, shape) =
        rtes
        |> Array.collect (fun rte ->
            filterShapeRoutes routeMapping shapeRoutes rte shape unt
        )
        |> Array.map _.Reconstitute
        |> Array.exists id


    let getValidShapes dataUrlId =
        fun get _ ->
            get "Shape"
        |> getData dataUrlId Constants.validShapesSheet
        |> mapErrorSource "getValidShapesResult"
