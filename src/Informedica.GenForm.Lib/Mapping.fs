namespace Informedica.GenForm.Lib


module Mapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib


    /// Mapping of long Z-index route names to short names
    let routeMapping =
        Web.getDataFromSheet Web.dataUrlIdGenPres "Routes"
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


    /// Mapping of long Z-index unit names to short names
    let unitMapping =
        Web.getDataFromSheet Web.dataUrlIdGenPres "Units"
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


    /// Try to find mapping for a route
    let mapRoute rte =
        routeMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens rte ||
            r.Short |> String.equalsCapInsens rte

        )
        |> Option.map (fun r -> r.Short)


    /// Try to map a unit to a short name
    let mapUnit unt =
        unitMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens unt ||
            r.Short |> String.equalsCapInsens unt
        )
        |> Option.map (fun r -> r.Short)


    /// Get the array of RouteShape records
    let mappingRouteShape =
        Web.getDataFromSheet Web.dataUrlIdGenPres "ShapeRoute"
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

                let un = getStr "Unit" |> Units.fromString |> Option.defaultValue NoUnit
                {
                    Route = getStr "Route"
                    Shape = getStr "Shape"
                    Unit = un
                    DoseUnit = getStr "DoseUnit" |> Units.fromString |> Option.defaultValue NoUnit
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


    /// <summary>
    /// Filter the mappingRouteShape array on route, shape and unit
    /// </summary>
    /// <param name="rte">The Route</param>
    /// <param name="shape">The Shape</param>
    /// <param name="unt">The Unit</param>
    /// <returns>An array of RouteShape records</returns>
    let filterRouteShapeUnit rte shape unt =
        mappingRouteShape
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


    let private requires_ (rtes, unt, shape) =
        rtes
        |> Array.collect (fun rte ->
            filterRouteShapeUnit rte shape unt
        )
        |> Array.map _.Reconstitute
        |> Array.exists id


    /// Check if reconstitution is required for a route, shape and unit
    let requiresReconstitution =
        Memoization.memoize requires_


