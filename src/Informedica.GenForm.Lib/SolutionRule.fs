namespace Informedica.GenForm.Lib


module SolutionRule =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    [<AutoOpen>]
    module Utils =

        let toBrs = BigRational.toBrs


        let toBrOpt = BigRational.toBrOpt


        let tupleBrOpt = BigRational.tupleBrOpt



    module SolutionLimit =

        let limit =
            {
                Substance = ""
                Unit = ""
                Quantity = MinMax.none
                Quantities = [||]
                Concentration = MinMax.none
            }


    let private get_ () =
        Web.getDataFromSheet Web.dataUrlId2 "SolutionRules"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r
                let toBrOpt = toBrs >> toBrOpt

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
                    Volumes = get "Volumes" |> toBrs
                    MinVol = get "MinVol" |> toBrOpt
                    MaxVol = get "MaxVol" |> toBrOpt
                    MinPerc = get "MinPerc" |> toBrOpt
                    MaxPerc = get "MaxPerc" |> toBrOpt
                    Substance = get "Substance"
                    Unit = get "Unit"
                    Quantities = get "Quantities" |> toBrs
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    MinConc = get "MinConc" |> toBrOpt
                    MaxConc = get "MaxConc" |> toBrOpt
                |}
            )
            |> Array.groupBy (fun r ->
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
                    Age = (r.MinAge, r.MaxAge) |> MinMax.fromTuple
                    Weight = (r.MinWeight, r.MaxWeight) |> MinMax.fromTuple
                    Dose = (r.MinDose, r.MaxDose) |> MinMax.fromTuple
                    DoseType = r.DoseType |> DoseType.fromString
                    Solutions = r.Solutions |> List.toArray
                    Volumes = r.Volumes
                    Volume = (r.MinVol, r.MaxVol) |> MinMax.fromTuple
                    DosePerc = (r.MinPerc, r.MaxPerc) |> MinMax.fromTuple
                    Products = [||]
                    SolutionLimits = [||]
                }
            )
            |> Array.map (fun (sr, rs) ->
                { sr with
                    SolutionLimits =
                        rs
                        |> Array.map (fun l ->
                            {
                                Substance = l.Substance
                                Unit = l.Unit
                                Quantity = (l.MinQty, l.MaxQty) |> MinMax.fromTuple
                                Quantities = l.Quantities
                                Concentration = (l.MinConc, l.MaxConc) |> MinMax.fromTuple
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


    let get : unit -> SolutionRule [] =
        Memoization.memoize get_


    let filter (filter : Filter) (solutionRules : SolutionRule []) =
        let eqs a b =
            a
            |> Option.map (fun x -> x = b)
            |> Option.defaultValue true

        [|
            fun (sr : SolutionRule) -> sr.Generic |> eqs filter.Generic
            fun (sr : SolutionRule) ->
                PatientCategory.isAgeWeight filter.Age filter.Weight sr.Age sr.Weight
            fun (sr : SolutionRule) -> sr.Shape |> eqs filter.Shape
            fun (sr : SolutionRule) -> sr.Route |> eqs filter.Route
            fun (sr : SolutionRule) -> sr.Department |> eqs filter.Department
            fun (sr : SolutionRule) ->
                match filter.DoseType, sr.DoseType with
                | AnyDoseType, _
                | _, AnyDoseType -> true
                | _ -> filter.DoseType = sr.DoseType
            fun (sr : SolutionRule) -> filter.Weight |> MinMax.isBetween sr.Weight
            fun (sr : SolutionRule) ->
                match sr.Location with
                | CVL  -> filter.Location = CVL
                | AnyAccess
                | PVL -> true
        |]
        |> Array.fold (fun (acc : SolutionRule[]) pred ->
            acc |> Array.filter pred
        ) solutionRules


    let getMember getter (rules : SolutionRule[]) =
        rules
        |> Array.map getter
        |> Array.distinct
        |> Array.sort


    let generics = getMember (fun sr -> sr.Generic)


    module Print =

        let printSolutionLimit (sr: SolutionRule) (limit: SolutionLimit) =
            let loc =
                match sr.Location with
                | CVL -> "###### centraal: \n* "
                | PVL -> "###### perifeer: \n* "
                | AnyAccess -> "* "

            let qs =
                if limit.Quantities |> Array.isEmpty then
                    ""
                else
                    limit.Quantities
                    |> Array.map BigRational.toStringNl
                    |> String.concat ", "
                    |> fun s -> $" {s} {limit.Unit}"

            let q =
                if limit.Quantity
                   |> MinMax.toString
                   |> String.isNullOrWhiteSpace then
                    ""
                else
                    limit.Quantity
                    |> MinMax.toString
                    |> fun s ->
                        if qs |> String.isNullOrWhiteSpace then
                            $" {s} {limit.Unit}"
                        else
                            $" ({s} {limit.Unit})"

            let vol =
                if sr.Volume
                   |> MinMax.toString
                   |> String.isNullOrWhiteSpace then
                    ""
                else
                    sr.Volume
                    |> MinMax.toString
                    |> fun s -> $""" in {s} ml {sr.Solutions |> String.concat "/"}"""
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace |> not then s
                    else
                        sr.Volumes
                        |> Array.map BigRational.toStringNl
                        |> String.concat "\n"
                        |> fun s ->
                            let sols = sr.Solutions |> String.concat "/"
                            if s |> String.isNullOrWhiteSpace then
                                if sols |> String.isNullOrWhiteSpace then " puur"
                                else $" in {sols}"
                            else
                                $" in {s} ml {sols}"

            let conc =
                if limit.Concentration |> MinMax.toString |> String.isNullOrWhiteSpace then ""
                else
                    $"* concentratie: {limit.Concentration |> MinMax.toString} {limit.Unit}/ml"

            let dosePerc =
                let p =
                    sr.DosePerc
                    |> MinMax.map (fun br -> br * 100N) (fun br -> br * 100N)
                    |> MinMax.toString

                if p |> String.isNullOrWhiteSpace then ""
                else
                    $"* geef {p}%% van de bereiding"

            $"\n{loc}{limit.Substance}: {q}{qs}{vol}\n{conc}\n{dosePerc}"


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


            ({| md = ""; rules = [||] |}, rules |> Array.groupBy (fun d -> d.Generic))
            ||> Array.fold (fun acc (generic, rs) ->
                let prods =
                    rs
                    |> Array.collect (fun d -> d.Products)
                    |> Array.sortBy (fun p ->
                        p.Substances
                        |> Array.sumBy (fun s -> s.Quantity |> Option.defaultValue 0N)
                    )
                    |> Array.collect (fun p ->
                        if p.Reconstitution |> Array.isEmpty then
                            [| product_md p.Label |]
                        else
                            p.Reconstitution
                            |> Array.map (fun r ->
                                $"{p.Label} oplossen in {r.DiluentVolume |> BigRational.toStringNl} ml voor {r.Route}"
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
                        (r, r.rules |> Array.groupBy (fun d -> d.Department))
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
                                            |> Array.groupBy (fun r -> r.Location)
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
                                                let s = sel.Weight |> MinMax.toString

                                                if s |> String.isNullOrWhiteSpace then
                                                    ""
                                                else
                                                    $"gewicht %s{s} kg"

                                            if a |> String.isNullOrWhiteSpace
                                               && w |> String.isNullOrWhiteSpace then
                                                ""
                                            else
                                                $"patient: %s{a} %s{w}" |> String.trim

                                        let dose =
                                            let d = sel.Dose |> MinMax.toString
                                            let u =
                                                match rs |> Array.collect (fun r -> r.SolutionLimits) with
                                                | [| sl |] -> sl.Unit
                                                | _ -> ""

                                            if d |> String.isNullOrWhiteSpace ||
                                               u |> String.isNullOrWhiteSpace then ""
                                            else
                                                $"{d} {u}"

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
            |> fun md -> md.md


        let printGenerics (rules: SolutionRule []) =
            rules
            |> generics
            |> Array.map (fun generic ->
                rules
                |> Array.filter (fun sr -> sr.Generic = generic)
                |> Array.sortBy (fun sr -> sr.Generic)
                |> toMarkdown ""
            )

