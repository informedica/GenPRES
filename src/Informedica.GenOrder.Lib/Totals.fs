namespace Informedica.GenOrder.Lib



module Totals =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib
    open Informedica.GenOrder.Lib
    open Informedica.GenSolver.Lib.Variable.Operators
    open Informedica.GenForm.Lib.Utils


    let isVolume (var : Variable) =
        var
        |> Variable.getUnit
        |> Option.map (fun u ->
            u |> Units.hasGroup Units.Volume.liter
        )
        |> Option.defaultValue false


    let getDosePerTime u tu pres (dose: Dose) =
        match pres with
        | Timed _
        | Discontinuous _ ->
            dose.PerTime
            |> OrderVariable.PerTime.convertFirstUnit u
            |> OrderVariable.PerTime.convertTimeUnit tu
            |> OrderVariable.PerTime.toOrdVar
            |> OrderVariable.getVar
        | Continuous _ ->
            dose.Rate
            |> OrderVariable.Rate.convertFirstUnit u
            |> OrderVariable.Rate.convertTimeUnit tu
            |> OrderVariable.Rate.toOrdVar
            |> OrderVariable.getVar
        | Once
        | OnceTimed _ ->
            let var =
                dose.Quantity
                |> OrderVariable.Quantity.convertFirstUnit u
                |> OrderVariable.Quantity.toOrdVar
                |> OrderVariable.getVar

            let unt =
                var
                |> Variable.getUnit
                |> Option.map (fun u -> u |> Units.per tu)

            unt
            |> Option.map (fun u ->
                var
                |> Variable.setUnit u
            )
            |> Option.defaultValue var


    let getVolume tu pres (dose: Dose) =
        if dose 
           |> Order.Orderable.Dose.toOrdVars
           |> List.map _.Variable
           |> List.exists isVolume then
            getDosePerTime Units.Volume.milliLiter tu pres dose
            |> Some
        else None


    let calc (ords : Order[]) wght name fu tu =
        match wght with
        | None -> [||]
        | Some w ->
            let w =
                Name.create ["wght"]
                |> Variable.empty
                |> fun var ->
                    var 
                    |> Variable.setValueRange (
                        w
                        |> Variable.ValueRange.ValueSet.create
                        |> ValSet
                    )

            [|
                for o in ords do
                    let vol = getVolume tu o.Prescription o.Orderable.Dose
                    if vol.IsSome then "volume", vol.Value

                    for cmp in o.Orderable.Components do
                        for itm in cmp.Items do
                            if itm.Name |> Name.toString = name then
                                itm.Name |> Name.toString, getDosePerTime fu tu o.Prescription itm.Dose
            |]
            |> Array.groupBy fst
            |> Array.map (fun (item, xs) ->
                item,
                xs
                |> Array.map snd
                |> Array.reduce (^+)
            )
            |> Array.choose (fun (n, tot) ->
                match tot |> Variable.getUnit with
                | None   -> None
                | Some u ->
                    let u =
                        u
                        |> ValueUnit.getUnits
                        |> List.head
                        |> Units.per Units.Weight.kiloGram
                        |> Units.per tu

                    (n,
                    tot ^/ w
                    |> Variable.setUnit u)
                    |> Some
            )


    let totals =
        Web.GoogleSheets.getCsvDataFromSheetSync
            "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
            "Totals"

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
                        Name = get "Name"
                        MinAge = get "MinAge" |> toBrOpt
                        MaxAge = get "MaxAge" |> toBrOpt
                        MinWeight = get "MinWeight" |> toBrOpt
                        MaxWeight = get "MaxWeight" |> toBrOpt
                        Unit = get "Unit" |> Units.fromString
                        Adj = get "Adj" |> Units.fromString
                        TimeUnit = get "TimeUnit" |> Units.fromString
                        MinPerTime = get "MinPerTime" |> toBrOpt
                        MaxPerTime = get "MaxPerTime" |> toBrOpt
                        MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                        MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                    |}
                )



    let getTotals (age: Informedica.GenUnits.Lib.ValueUnit option) (wght : Informedica.GenUnits.Lib.ValueUnit option) (dtos: Order.Dto.Dto []) : Totals =
        let ords =
            dtos
            |> Array.map Order.Dto.fromDto

        let calc = calc ords wght

        let totals =
            totals
            |> Array.filter (fun t ->
                if wght.IsNone then true
                else
                    let w =
                        wght.Value
                        |> ValueUnit.convertTo Units.Weight.gram
                        |> ValueUnit.getValue
                        |> Array.head

                    match t.MinWeight, t.MaxWeight with
                    | Some min, Some max ->
                        min <= w && w < max
                    | Some min, None ->
                        min <= w
                    | None, Some max ->
                        max > w
                    | None, None -> true
            )
            |> Array.filter (fun t ->
                if age.IsNone then true
                else
                    let a =
                        age.Value
                        |> ValueUnit.convertTo Units.Time.day
                        |> ValueUnit.getValue
                        |> Array.head

                    match t.MinAge, t.MaxAge with
                    | Some min, Some max ->
                        min <= a && a < max
                    | Some min, None ->
                        min <= a
                    | None, Some max ->
                        max > a
                    | None, None -> true
            )

        let calculated =
            totals
            |> Array.collect (fun t ->
                match t.Unit, t.TimeUnit with
                | Some fu, Some tu ->
                    calc t.Name fu tu
                | _ -> [||]
            )

        let get n =
            calculated
            |> Array.tryFind (fst >> String.equalsCapInsens n)
            |> Option.map (fun (n, var) ->
                let s =
                    var
                    |> Informedica.GenSolver.Lib.Variable.getValueRange
                    |> Informedica.GenSolver.Lib.Variable.ValueRange.toMarkdown 3

                match totals |> Array.tryFind (fun t -> t.Name |> String.equalsCapInsens n) with
                | None -> s
                | Some tot ->
                    match tot.MinPerTimeAdj, tot.MaxPerTimeAdj with
                    | Some minAdj, Some maxAdj ->
                        let norm = $"{minAdj |> BigRational.toDouble} - {maxAdj |> BigRational.toDouble}"
                        s + $" ({norm})"
                    | None, Some maxAdj ->
                        let norm = $"{maxAdj |> BigRational.toDouble}"
                        s + $" (max {norm})"
                    | _ -> s

            )

        {
            Volume = get "volume"
            Energy = get "energie"
            Protein = get "eiwit"
            Carbohydrate = get "koolhydraat"
            Fat = get "vet"
            Sodium = get "natrium"
            Potassium = get "kalium"
            Chloride = get "chloor"
            Calcium = get "calcium"
            Phosphate = get "fosfaat"
            Magnesium = get "magnesium"
            Iron = get "ijzer"
            VitaminD = get "VitD"
            Ethanol = get "ethanol"
            Propyleenglycol = get "propyleenglycol"
            BenzylAlcohol = get "benzylalcohol"
            BoricAcid = get "boorzuur"
        }