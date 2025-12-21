namespace Informedica.GenForm.Lib


module Mapping =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open Utils


    module Constants =

        let [<Literal>] unitsSheet = "Units"

        let [<Literal>] routesSheet = "Routes"

        let [<Literal>] validFormsSheet = "ValidForms"

        let [<Literal>] formRouteSheet = "FormRoute"



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
                    |> GenFormResult.createOkNoMsgs
        with
        | exn -> GenFormResult.createError "getData" exn


    let getRouteMapping dataUrlId =
        fun get _ ->
            {
                Long = get "ZIndex"
                Short = get "ShortDutch"
            }
        |> getData dataUrlId Constants.routesSheet
        |> GenFormResult.mapErrorSource "getRouteMapping"


    let getUnitMapping dataUrlId =
        fun get _ ->
            {
                Long = get "ZIndexUnitLong"
                Short = get "Unit"
                MV = get "MetaVisionUnit"
                Group = get "Group"
            }
        |> getData dataUrlId Constants.unitsSheet
        |> GenFormResult.mapErrorSource "getUnitMapping"


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


    let getFormRoutes dataUrlId unitMapping =
        let mapUnit = mapUnit unitMapping

        fun getStr getFlt ->
            let un = getStr "Unit" |> mapUnit |> Option.defaultValue NoUnit
            let du = getStr "DoseUnit" |> mapUnit |> Option.defaultValue un

            {
                Route = getStr "Route"
                Form = getStr "Form"
                Unit = un
                DoseUnit = getStr "DoseUnit" |> mapUnit |> Option.defaultValue NoUnit
                MinDoseQty =
                    if du = NoUnit then None
                    else
                        getFlt "MinDoseQty"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                MaxDoseQty =
                    if du = NoUnit then None
                    else
                        getFlt "MaxDoseQty"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                MinDoseQtyPerKg =
                    if du = NoUnit then None
                    else
                        let du = du |> Units.per Units.Weight.kiloGram

                        getFlt "MinDoseQtyKg"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                MaxDoseQtyPerKg =
                    if du = NoUnit then None
                    else
                        let du = du |> Units.per Units.Weight.kiloGram

                        getFlt "MaxDoseQtyKg"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                Divisibility =
                    getFlt "Divisible"
                    |> Option.bind BigRational.fromFloat
                Timed = getStr "Timed" |> String.equalsCapInsens "true"
                Reconstitute = getStr "Reconstitute" |> String.equalsCapInsens "true"
                IsSolution = getStr "IsSolution" |> String.equalsCapInsens "true"
            }
        |> getData dataUrlId Constants.formRouteSheet
        |> GenFormResult.mapErrorSource "getFormRoutes"


    let filterFormRoutes routeMapping (mapping : FormRoute []) rte form unt =
        let mapRoute = mapRoute routeMapping

        mapping
        |> Array.filter (fun sr ->
            let eqsRte =
                rte |> String.isNullOrWhiteSpace ||
                rte |> String.trim |> String.equalsCapInsens sr.Route ||
                sr.Route |> mapRoute |> Option.map (String.equalsCapInsens (rte |> String.trim)) |> Option.defaultValue false
            let eqsForm = form |> String.isNullOrWhiteSpace || form |> String.trim |> String.equalsCapInsens sr.Form
            let eqsUnt =
                unt = NoUnit ||
                unt |> Units.eqsUnit sr.Unit
            eqsRte && eqsForm && eqsUnt
        )


    let requiresReconstitution routeMapping formRoutes (rtes, unt, form) =
        rtes
        |> Array.collect (fun rte ->
            filterFormRoutes routeMapping formRoutes rte form unt
        )
        |> Array.map _.Reconstitute
        |> Array.exists id


    let getValidForms dataUrlId =
        fun get _ ->
            get "Form"
        |> getData dataUrlId Constants.validFormsSheet
        |> GenFormResult.mapErrorSource "getValidFormResult"