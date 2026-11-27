namespace Informedica.GenForm.Lib


module Mapping =

    open System

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open Utils


    module Constants =

        [<Literal>]
        let unitsSheet = "Units"

        [<Literal>]
        let routesSheet = "Routes"

        [<Literal>]
        let validFormsSheet = "ValidForms"

        [<Literal>]
        let formRouteSheet = "FormRoute"

        [<Literal>]
        let totalsSheet = "Totals"

        [<Literal>]
        let ruleSetsSheet = "RuleSets"

        [<Literal>]
        let linkRulesSheet = "LinkRules"


    let parseSheet apply (data: string[][]) =
        try
            match data |> Array.tryHead with
            | None -> [ ("Sheet is empty or not found", None) |> ErrorMsg ] |> Error
            | Some h ->
                let getStringColumn = Csv.getStringColumn h
                let getFloatOptColumn = Csv.getFloatOptionColumn h

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let getString = getStringColumn r
                    let getFloat = getFloatOptColumn r

                    apply getString getFloat
                )
                |> Ok
        with exn ->
            Result.createError "getData" exn


    let getData dataUrlId sheet apply = Web.getDataFromSheet dataUrlId sheet |> parseSheet apply


    /// Map one row of the "Routes" sheet. A separate function so the column-contract test runs without IO.
    let routeMappingRow (get: string -> string) (_: string -> float option) =
        {
            Long = get "ZIndex"
            Short = get "ShortDutch"
        }


    let getRouteMapping dataUrlId =
        routeMappingRow
        |> getData dataUrlId Constants.routesSheet
        |> Result.mapErrorSource "getRouteMapping"


    /// Map one row of the "Units" sheet.
    let unitMappingRow (get: string -> string) (_: string -> float option) =
        {
            Long = get "ZIndexUnitLong"
            Short = get "Unit"
            MV = get "MetaVisionUnit"
            Group = get "Group"
        }


    let getUnitMapping dataUrlId =
        unitMappingRow
        |> getData dataUrlId Constants.unitsSheet
        |> Result.mapErrorSource "getUnitMapping"


    /// Map one row of the "Totals" sheet.
    let totalsRow (get: string -> string) (_: string -> float option) =
        let toBrOpt = BigRational.toBrs >> Array.tryHead

        {
            Name = get "Name"
            MinAge = get "MinAge" |> toBrOpt
            MaxAge = get "MaxAge" |> toBrOpt
            MinWeight = get "MinWeight" |> toBrOpt
            MaxWeight = get "MaxWeight" |> toBrOpt
            MinGestAge = get "MinGestAge" |> toBrOpt
            MaxGestAge = get "MaxGestAge" |> toBrOpt
            Unit = get "Unit" |> UnitsParse.fromString
            Adj = get "Adj" |> UnitsParse.fromString
            TimeUnit = get "TimeUnit" |> UnitsParse.fromString
            MinPerTime = get "MinPerTime" |> toBrOpt
            MaxPerTime = get "MaxPerTime" |> toBrOpt
            MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
            MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
        }


    let getTotals dataUrlId =
        totalsRow
        |> getData dataUrlId Constants.totalsSheet
        |> Result.mapErrorSource "getTotals"


    /// Map one row of the "RuleSets" sheet, every cell trimmed; an empty Loc or Dep is None.
    let ruleSetRow (get: string -> string) (_: string -> float option) =
        let getOpt col =
            let s = get col |> String.trim
            if s |> String.isNullOrWhiteSpace then None else s |> Some

        {
            RuleSetData.Category = get "Category" |> String.trim
            RuleSetData.Label = get "Label" |> String.trim
            RuleSetData.Indication = get "Indication" |> String.trim
            RuleSetData.Generic = get "Generic" |> String.trim
            RuleSetData.Location = getOpt "Loc"
            RuleSetData.Department = getOpt "Dep"
        }


    /// Returns the rows of the "RuleSets" sheet, without the rows that have no category.
    let getRuleSets dataUrlId =
        ruleSetRow
        |> getData dataUrlId Constants.ruleSetsSheet
        |> Result.map (Array.filter (fun r -> r.Category |> String.notEmpty))
        |> Result.mapErrorSource "getRuleSets"


    /// A row of the "LinkRules" sheet, every cell trimmed.
    let linkRuleRow (get: string -> string) (_: string -> float option) =
        {
            LinkRuleData.Generic = get "Generic" |> String.trim
            LinkRuleData.Route = get "Route" |> String.trim
            LinkRuleData.Indication = get "Indication" |> String.trim
            LinkRuleData.DoseType = get "DoseType" |> String.trim
            LinkRuleData.DoseText = get "DoseText" |> String.trim
            LinkRuleData.MainGeneric = get "MainGeneric" |> String.trim
            LinkRuleData.MainRoute = get "MainRoute" |> String.trim
            LinkRuleData.MainIndication = get "MainIndication" |> String.trim
            LinkRuleData.MainDoseType = get "MainDoseType" |> String.trim
            LinkRuleData.MainDoseText = get "MainDoseText" |> String.trim
            LinkRuleData.Required = get "Required" |> String.trim
        }


    /// Returns the rows of the "LinkRules" sheet, blank rows included, so each row keeps its sheet row number.
    let getLinkRules dataUrlId =
        linkRuleRow
        |> getData dataUrlId Constants.linkRulesSheet
        |> Result.mapErrorSource "getLinkRules"


    /// Fetches the rows of each of the sheets, by sheet name. Returns an error when any sheet fails to load.
    /// GenCORE parses and checks the rows.
    let getSheetRows urlId sheets : Result<Map<string, string[][]>, Message list> =
        sheets
        |> List.fold
            (fun acc sheet ->
                acc
                |> Result.bind (fun rows ->
                    Web.GoogleSheets.getRowsFromSheetSync urlId sheet
                    |> Result.mapError (fun e -> [ ErrorMsg($"sheet %s{sheet}: %s{e}", None) ])
                    |> Result.map (fun data -> rows |> Map.add sheet data)
                )
            )
            (Ok Map.empty)


    let mapUnit (mapping: UnitMapping array) s =
        if s |> String.isNullOrWhiteSpace then
            None
        else
            let s = s |> String.trim

            mapping
            |> Array.tryFind (fun r ->
                r.Long |> String.equalsCapInsens s
                || r.Short |> String.equalsCapInsens s
                || r.MV |> String.equalsCapInsens s
            )
            |> function
                | Some r -> $"{r.Short}[{r.Group}]" |> UnitsParse.fromString
                | None -> None


    let mapRoute (mapping: RouteMapping array) s =
        if s |> String.isNullOrWhiteSpace then
            None
        else
            let s = s |> String.trim

            mapping
            |> Array.tryFind (fun r -> r.Long |> String.equalsCapInsens s || r.Short |> String.equalsCapInsens s)
            |> Option.map _.Long


    /// Returns None for an empty route, which asks for no route, and the route otherwise.
    let routeOption rte = if rte |> String.isNullOrWhiteSpace then None else Some rte


    let eqsRoute routeMapping r1 r2 =
        let mapRoute = mapRoute routeMapping

        if r1 |> Option.isNone then
            true
        else
            match r1.Value |> mapRoute, r2 |> mapRoute with
            | Some r1, Some r2 -> r1 = r2
            | _ -> false


    /// <summary>
    /// Map one row of the "FormRoute" sheet, using the unit mappings to parse the units.
    /// The dose limits are read only when the dose unit, or else the unit, resolves.
    /// </summary>
    let formRouteRow unitMapping =
        let mapUnit = mapUnit unitMapping

        fun (getStr: string -> string) (getFlt: string -> float option) ->
            let un = getStr "Unit" |> mapUnit |> Option.defaultValue NoUnit
            let du = getStr "DoseUnit" |> mapUnit |> Option.defaultValue un

            {
                Route = getStr "Route"
                Form = getStr "Form"
                Unit = un
                DoseUnit = getStr "DoseUnit" |> mapUnit |> Option.defaultValue NoUnit
                MinDoseQty =
                    if du = NoUnit then
                        None
                    else
                        getFlt "MinDoseQty"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                MaxDoseQty =
                    if du = NoUnit then
                        None
                    else
                        getFlt "MaxDoseQty"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                MinDoseQtyPerKg =
                    if du = NoUnit then
                        None
                    else
                        let du = du |> ValueUnit.per Units.Weight.kiloGram

                        getFlt "MinDoseQtyKg"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                MaxDoseQtyPerKg =
                    if du = NoUnit then
                        None
                    else
                        let du = du |> ValueUnit.per Units.Weight.kiloGram

                        getFlt "MaxDoseQtyKg"
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit du)
                Divisibility = getFlt "Divisible" |> Option.bind BigRational.fromFloat
                Timed = getStr "Timed" |> String.equalsCapInsens "true"
                Reconstitute = getStr "Reconstitute" |> String.equalsCapInsens "true"
                IsSolution = getStr "IsSolution" |> String.equalsCapInsens "true"
                Totals = getStr "Totals" |> String.equalsCapInsens "true"
            }


    let getFormRoutes dataUrlId unitMapping =
        formRouteRow unitMapping
        |> getData dataUrlId Constants.formRouteSheet
        |> Result.mapErrorSource "getFormRoutes"


    let filterFormRoutes routeMapping (mapping: FormRoute[]) rte form unt =
        let mapRoute = mapRoute routeMapping

        mapping
        |> Array.filter (fun sr ->
            let eqsRte =
                rte |> String.isNullOrWhiteSpace
                || rte |> String.trim |> String.equalsCapInsens sr.Route
                || sr.Route
                   |> mapRoute
                   |> Option.map (String.equalsCapInsens (rte |> String.trim))
                   |> Option.defaultValue false

            let eqsForm =
                form |> String.isNullOrWhiteSpace
                || form |> String.trim |> String.equalsCapInsens sr.Form

            let eqsUnt = unt = NoUnit || unt |> Units.eqsUnit sr.Unit
            eqsRte && eqsForm && eqsUnt
        )


    let requiresReconstitution routeMapping formRoutes (rtes, unt, form) =
        rtes
        |> Array.collect (fun rte -> filterFormRoutes routeMapping formRoutes rte form unt)
        |> Array.map _.Reconstitute
        |> Array.exists id


    /// Map one row of the "ValidForms" sheet.
    let validFormRow (get: string -> string) (_: string -> float option) = get "Form"


    let getValidForms dataUrlId =
        validFormRow
        |> getData dataUrlId Constants.validFormsSheet
        |> Result.mapErrorSource "getValidFormResult"
