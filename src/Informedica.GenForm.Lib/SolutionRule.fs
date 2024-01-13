namespace Informedica.GenForm.Lib


module SolutionRule =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    module SolutionLimit =

        open Informedica.GenCore.Lib.Ranges

        /// An empty SolutionLimit.
        let limit =
            {
                Substance = ""
                Quantity = MinMax.empty
                Quantities = None
                Concentration = MinMax.empty
            }


    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let private get_ () =
        let dataUrlId = Web.getDataUrlIdGenPres ()
        Web.getDataFromSheet dataUrlId "SolutionRules"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r
                let toBrOpt = BigRational.toBrs >> Array.tryHead

                {|
                    Generic = get "Generic"
                    Shape = get "Shape"
                    Route = get "Route"
                    Department = get "Dep"
                    CVL = get "CVL"
                    PVL = get "PVL"
                    MinAge = get "MinAge" |> toBrOpt
                    MaxAge = get "MaxAge" |> toBrOpt
                    MinWeight = get "MinWeight" |> toBrOpt
                    MaxWeight = get "MaxWeight" |> toBrOpt
                    MinDose = get "MinDose" |> toBrOpt
                    MaxDose = get "MaxDose" |> toBrOpt
                    DoseType = get "DoseType"
                    Solutions = get "Solutions" |> String.split "|"
                    Volumes = get "Volumes" |> BigRational.toBrs
                    MinVol = get "MinVol" |> toBrOpt
                    MaxVol = get "MaxVol" |> toBrOpt
                    MinPerc = get "MinPerc" |> toBrOpt
                    MaxPerc = get "MaxPerc" |> toBrOpt
                    Substance = get "Substance"
                    Unit = get "Unit"
                    Quantities = get "Quantities" |> BigRational.toBrs
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    MinConc = get "MinConc" |> toBrOpt
                    MaxConc = get "MaxConc" |> toBrOpt
                |}
            )
            |> Array.groupBy (fun r ->
                let du = r.Unit |> Units.fromString
                {
                    Generic = r.Generic
                    Shape = r.Shape
                    Route = r.Route
                    Department = r.Department
                    Location =
                        if r.CVL = "x" then CVL
                        else
                            if r.PVL = "x" then PVL
                            else
                                AnyAccess
                    Age =
                        (r.MinAge, r.MaxAge)
                        |> fromTupleInclExcl (Some Utils.Units.day)
                    Weight =
                        (r.MinWeight, r.MaxWeight)
                        |> fromTupleInclExcl (Some Utils.Units.weightGram)
                    Dose =
                        (r.MinDose, r.MaxDose)
                        |> fromTupleInclIncl du
                    DoseType = r.DoseType |> DoseType.fromString
                    Solutions = r.Solutions |> List.toArray
                    Volumes =
                        if r.Volumes |> Array.isEmpty then None
                        else
                            r.Volumes
                            |> ValueUnit.withUnit Units.mL
                            |> Some
                    Volume =
                        (r.MinVol, r.MaxVol)
                        |> fromTupleInclIncl (Some Units.mL)
                    DosePerc =
                        (r.MinPerc, r.MaxPerc)
                        |> fromTupleInclIncl (Some Units.Count.times)
                    Products = [||]
                    SolutionLimits = [||]
                }
            )
            |> Array.map (fun (sr, rs) ->
                { sr with
                    SolutionLimits =
                        rs
                        |> Array.map (fun l ->
                            let u = l.Unit |> Units.fromString
                            {
                                Substance = l.Substance
                                Quantity =
                                    (l.MinQty, l.MaxQty)
                                    |> fromTupleInclIncl u
                                Quantities =
                                    if l.Quantities |> Array.isEmpty then None
                                    else
                                        match u with
                                        | None -> None
                                        | Some u ->
                                            l.Quantities
                                            |> ValueUnit.withUnit u
                                            |> Some
                                Concentration =
                                    let u =
                                        u
                                        |> Option.map (Units.per Units.Volume.milliLiter)
                                    (l.MinConc, l.MaxConc)
                                    |> fromTupleInclIncl u
                            }
                        )
                    Products =
                        Product.get ()
                        |> Array.filter (fun p ->
                            p.Generic = sr.Generic &&
                            p.Shape = sr.Shape
                        )

                }
            )


    /// <summary>
    /// Gets the SolutionRules.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : unit -> SolutionRule [] =
        Memoization.memoize get_


    /// <summary>
    /// Get all the SolutionRules that match the given Filter.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="solutionRules">The SolutionRules</param>
    /// <returns>The matching SolutionRules</returns>
    let filter (filter : Filter) (solutionRules : SolutionRule []) =
        let eqs a b =
            a
            |> Option.map (fun x -> x = b)
            |> Option.defaultValue true

        [|
            fun (sr : SolutionRule) -> sr.Generic |> eqs filter.Generic
            fun (sr : SolutionRule) ->
                PatientCategory.checkAgeWeightMinMax filter.Patient.Age filter.Patient.Weight sr.Age sr.Weight
            fun (sr : SolutionRule) -> sr.Shape |> eqs filter.Shape
            fun (sr : SolutionRule) -> filter.Route |> Option.isNone || sr.Route |> Mapping.eqsRoute filter.Route
            fun (sr : SolutionRule) -> sr.Department |> eqs filter.Patient.Department
            fun (sr : SolutionRule) ->
                match filter.DoseType, sr.DoseType with
                | AnyDoseType, _
                | _, AnyDoseType -> true
                | _ -> filter.DoseType = sr.DoseType
            fun (sr : SolutionRule) -> filter.Patient.Weight |> Utils.MinMax.inRange sr.Weight
            fun (sr : SolutionRule) ->
                match sr.Location with
                | CVL -> filter.Patient.VenousAccess |> List.exists ((=) CVL)
                | PVL //-> filter.Location = PVL
                | AnyAccess -> true
        |]
        |> Array.fold (fun (acc : SolutionRule[]) pred ->
            acc |> Array.filter pred
        ) solutionRules


    /// Helper function to get the distinct values of a member of SolutionRule.
    let getMember getter (rules : SolutionRule[]) =
        rules
        |> Array.map getter
        |> Array.distinct
        |> Array.sort


    /// Get all the distinct Generics from the given SolutionRules.
    let generics = getMember _.Generic

    let shapes = getMember _.Shape

    let routes = getMember _.Route


    module Print =


        module MinMax = MinMax
        module Limit = Limit


        /// Get the string representation of a SolutionLimit.
        let printSolutionLimit (sr: SolutionRule) (limit: SolutionLimit) =
            let mmToStr = MinMax.toString "min. " "min. " "max. " "max. "

            let loc =
                match sr.Location with
                | CVL -> "###### centraal: \n* "
                | PVL -> "###### perifeer: \n* "
                | AnyAccess -> "* "

            let qs =
                limit.Quantities
                |> Option.map (Utils.ValueUnit.toString -1)
                |> Option.defaultValue ""

            let q =
                limit.Quantity
               |> mmToStr

            let vol =
                if sr.Volume
                   |> mmToStr
                   |> String.isNullOrWhiteSpace then
                    ""
                else
                    sr.Volume
                    |> mmToStr
                    |> fun s -> $""" in {s} {sr.Solutions |> String.concat "/"}"""
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace |> not then s
                    else
                        sr.Volumes
                        |> Option.map (Utils.ValueUnit.toString -1)
                        |> Option.defaultValue ""
                        |> fun s ->
                            let sols = sr.Solutions |> String.concat "/"
                            if s |> String.isNullOrWhiteSpace then
                                if sols |> String.isNullOrWhiteSpace then " puur"
                                else $" in {sols}"
                            else
                                $" in {s} {sols}"

            let conc =
                if limit.Concentration
                   |> mmToStr
                   |> String.isNullOrWhiteSpace then ""
                else
                    $"* concentratie: {limit.Concentration |> mmToStr}"

            let dosePerc =
                let toPerc l =
                    l
                    |> Limit.getValueUnit
                    |> ValueUnit.getValue
                    |> Array.item 0
                    |> BigRational.toDouble
                    |> fun x -> $"{x * 100.}"

                let p =
                    match sr.DosePerc.Min, sr.DosePerc.Max with
                    | None, None -> ""
                    | Some l, None -> $"min. {l |> toPerc}"
                    | None, Some l -> $"max. {l |> toPerc}"
                    | Some min, Some max ->
                        if min = max then
                            $"{min |> toPerc}"
                        else
                            $"{min |> toPerc} - {max |> toPerc}"


                if p |> String.isNullOrWhiteSpace then ""
                else
                    $"* geef %s{p}%% van de bereiding"

            $"\n{loc}{limit.Substance}: {q}{qs}{vol}\n{conc}\n{dosePerc}"


        /// Get the markdown representation of the given SolutionRules.
        let toMarkdown text (rules: SolutionRule []) =
            let generic_md generic products =
                let text = if text |> String.isNullOrWhiteSpace then generic else text
                $"\n# %s{text}\n---\n#### Producten\n%s{products}\n"

            let department_md dep =
                let dep =
                    match dep with
                    | _ when dep = "AICU" -> "ICC"
                    | _ -> dep

                $"\n### Afdeling: {dep}\n"

            let pat_md pat =
                $"\n##### %s{pat}\n"

            let product_md product =
                $"\n* %s{product}\n"


            ({| md = ""; rules = [||] |}, rules |> Array.groupBy _.Generic)
            ||> Array.fold (fun acc (generic, rs) ->
                let prods =
                    rs
                    |> Array.collect _.Products
                    |> Array.sortBy (fun p ->
                        p.Substances
                        |> Array.sumBy (fun s ->
                            s.Concentration
                            |> Option.map ValueUnit.getValue
                            |> Option.bind Array.tryHead
                            |> Option.defaultValue 0N
                        )
                    )
                    |> Array.collect (fun p ->
                        if p.Reconstitution |> Array.isEmpty then
                            if p.RequiresReconstitution then
                                [| $"{product_md p.Label} oplossen in ... " |]
                            else
                                [| product_md p.Label |]
                        else
                            p.Reconstitution
                            |> Array.map (fun r ->
                                $"{p.Label} oplossen in {r.DiluentVolume |> Utils.ValueUnit.toString -1} voor {r.Route}"
                                |> product_md
                            )
                    )
                    |> Array.distinct
                    |> String.concat "\n"

                {| acc with
                    md = generic_md generic prods
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then r
                    else
                        (r, r.rules |> Array.groupBy _.Department)
                        ||> Array.fold (fun acc (dep, rs) ->
                            {| acc with
                                md = acc.md + (department_md dep)
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules |> Array.isEmpty then r
                                else
                                    (r,
                                     r.rules
                                     |> Array.groupBy (fun r ->
                                        {|
                                            Age = r.Age
                                            Weight = r.Weight
                                            Dose = r.Dose
                                            DoseType = r.DoseType
                                        |}
                                     )
                                    )
                                    ||> Array.fold (fun acc (sel, rs) ->
                                        let sol =
                                            rs
                                            |> Array.groupBy _.Location
                                            |> Array.collect (fun (_, rs) ->
                                                rs
                                                |> Array.tryHead
                                                |> function
                                                    | None -> [||]
                                                    | Some r ->
                                                        r.SolutionLimits
                                                        |> Array.map (printSolutionLimit r)
                                            )
                                            |> String.concat "\n"

                                        let pat =
                                            let a = sel.Age |> PatientCategory.printAgeMinMax

                                            let w =
                                                let s =
                                                    sel.Weight
                                                    |> MinMax.convertTo Units.Weight.kiloGram
                                                    |> MinMax.toString
                                                        "van "
                                                        "van "
                                                        "tot "
                                                        "tot "

                                                if s |> String.isNullOrWhiteSpace then
                                                    ""
                                                else
                                                    $"gewicht %s{s}"

                                            if a |> String.isNullOrWhiteSpace
                                               && w |> String.isNullOrWhiteSpace then
                                                ""
                                            else
                                                $"patient: %s{a} %s{w}" |> String.trim

                                        let dose =
                                            sel.Dose
                                            |> MinMax.toString
                                                    "van "
                                                    "van "
                                                    "tot "
                                                    "tot "

                                        let dt =
                                            let s = sel.DoseType |> DoseType.toString
                                            if s |> String.isNullOrWhiteSpace then ""
                                            else
                                                $"{s}"


                                        {| acc with
                                            rules = rs
                                            md =
                                                if pat |> String.isNullOrWhiteSpace &&
                                                   dose |> String.isNullOrWhiteSpace then
                                                    acc.md + $"##### {dt}"
                                                else
                                                    acc.md + pat_md $"{dt}, {pat}{dose}"
                                                |> fun s -> $"{s}\n{sol}"
                                        |}
                                    )
                        )


            )
            |> _.md


        /// Get the markdown representation of the given SolutionRules.
        let printGenerics (rules: SolutionRule []) =
            rules
            |> generics
            |> Array.map (fun generic ->
                rules
                |> Array.filter (fun sr -> sr.Generic = generic)
                |> Array.sortBy _.Generic
                |> toMarkdown ""
            )


