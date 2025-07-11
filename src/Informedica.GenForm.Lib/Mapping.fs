namespace Informedica.GenForm.Lib


module Mapping =

    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib



    let getRouteMappingWithDataUrlId dataUrlId =
        Web.getDataFromSheet dataUrlId "Routes"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {
                    Long = get "ZIndex"
                    Short = get "ShortDutch"
                }
            )


    /// Mapping of long Z-index route names to short names
    [<Obsolete("Use getRouteMappingWithDataUrlId instead")>]
    let getRouteMapping () = getRouteMappingWithDataUrlId (Web.getDataUrlIdGenPres ())


    let getUnitMappingWithDataUrlId dataUrlId =
        Web.getDataFromSheet dataUrlId "Units"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {
                    Long = get "ZIndexUnitLong"
                    Short = get "Unit"
                    MV = get "MetaVisionUnit"
                    Group = get "Group"
                }
            )


    /// Mapping of long Z-index unit names to short names
    [<Obsolete("Use getUnitMappingWithDataUrlId instead")>]
    let getUnitMapping () = getUnitMappingWithDataUrlId (Web.getDataUrlIdGenPres ())


    let mapUnitWithMapping (mapping : UnitMapping array) s =
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


    [<Obsolete("Use getUnitMapping instead")>]
    let mapUnit =
        mapUnitWithMapping (getUnitMapping ())


    let mapRouteWithMapping (mapping : RouteMapping array) s =
        if s |> String.isNullOrWhiteSpace then None
        else
            let s = s |> String.trim
            mapping
            |> Array.tryFind (fun r ->
                r.Long |> String.equalsCapInsens s ||
                r.Short |> String.equalsCapInsens s
            )
            |> Option.map _.Long


    /// Try to find mapping for a route
    let mapRoute = mapRouteWithMapping (getRouteMapping ())


    let eqsRoute r1 r2 =
        if r1 |> Option.isNone then true
        else
            match r1.Value |> mapRoute, r2 |> mapRoute with
            | Some r1, Some r2 -> r1 = r2
            | _ -> false


    /// Get the array of ShapeRoute records
    let getShapeRouteMappingWithDataUrlId dataUrlId =
        Web.getDataFromSheet dataUrlId "ShapeRoute"
        |> fun data ->
            let inline getColumn get =
                data
                |> Array.head
                |> get

            data
            |> Array.tail
            |> Array.map (fun r ->
                let getStr = getColumn Csv.getStringColumn r
                let getFlt = getColumn Csv.getFloatOptionColumn r

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
            )


    [<Obsolete("Use getShapeRouteMappingWithDataUrlId instead")>]
    let getShapeRouteMappingMemoized =
        fun () ->
            getShapeRouteMappingWithDataUrlId (Web.getDataUrlIdGenPres ())
        |> Memoization.memoize


    let filterRouteShapeUnitWithMapping (mapping : ShapeRoute []) rte shape unt =
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


    /// <summary>
    /// Filter the mappingRouteShape array on route, shape and unit
    /// </summary>
    /// <param name="rte">The Route</param>
    /// <param name="shape">The Shape</param>
    /// <param name="unt">The Unit</param>
    /// <returns>An array of RouteShape records</returns>
    [<Obsolete("Use filterRouteShapeUnitWithMapping instead")>]
    let filterRouteShapeUnit rte shape unt = 
        filterRouteShapeUnitWithMapping (getShapeRouteMappingMemoized ()) rte shape unt


    let private requires_ (rtes, unt, shape) =
        rtes
        |> Array.collect (fun rte ->
            filterRouteShapeUnit rte shape unt
        )
        |> Array.map _.Reconstitute
        |> Array.exists id


    /// Check if reconstitution is required for a route, shape and unit
    let requiresReconstitutionMemoized =
        Memoization.memoize requires_


    /// Mapping of long Z-index unit names to short names
    let getValidShapesWithDataUrlId dataUrlId =
        Web.getDataFromSheet dataUrlId "ValidShapes"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                get "Shape"
            )
            |> Array.distinct


    /// Mapping of long Z-index unit names to short names
    [<Obsolete("Use getValidShapesWithDataUrlId instead")>]
    let getValidShapes () = 
        getValidShapesWithDataUrlId (Web.getDataUrlIdGenPres ())


    [<Obsolete("Use IResources instead")>]
    let getValidShapesMemoized =
        Memoization.memoize getValidShapes