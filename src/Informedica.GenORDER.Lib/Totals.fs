namespace Informedica.GenOrder.Lib


module Totals =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib
    open Informedica.GenOrder.Lib
    open Informedica.GenSolver.Lib.Variable.Operators

    let isVolume (var: Variable) =
        var
        |> Variable.getUnit
        |> Option.map (fun u -> u |> Units.hasGroup Units.Volume.liter)
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

            let unt = var |> Variable.getUnit |> Option.map (fun u -> u |> ValueUnit.per tu)

            unt
            |> Option.map (fun u -> var |> Variable.setUnit u)
            |> Option.defaultValue var


    /// Get the volume
    let getVolume tu pres (dose: Dose) =
        // TODO this is maybe too simplistic
        if
            dose
            |> Order.Orderable.Dose.toOrdVars
            |> List.map _.Variable
            |> List.exists isVolume
        then
            getDosePerTime Units.Volume.milliLiter tu pres dose |> Some
        else
            None


    let calc (ords: Order[]) wght name fu tu =
        match wght with
        | None -> [||]
        | Some w ->
            let w =
                Name.create [ "wght" ]
                |> Variable.empty
                |> fun var ->
                    var
                    |> Variable.setValueRange (w |> Variable.ValueRange.ValueSet.create |> ValSet)

            [|
                for o in ords do
                    let vol = getVolume tu o.Schedule o.Orderable.Dose

                    if vol.IsSome then
                        "volume", vol.Value

                    for cmp in o.Orderable.Components do
                        for itm in cmp.Items do
                            if itm.Name |> Name.toString = name then
                                itm.Name |> Name.toString, getDosePerTime fu tu o.Schedule itm.Dose
            |]
            |> Array.groupBy fst
            |> Array.map (fun (item, xs) -> item, xs |> Array.map snd |> Array.reduce (@+))
            |> Array.choose (fun (n, tot) ->
                match tot |> Variable.getUnit with
                | None -> None
                | Some u ->
                    let u =
                        u
                        |> ValueUnit.getUnits
                        |> List.head
                        |> ValueUnit.per Units.Weight.kiloGram
                        |> ValueUnit.per tu

                    (n, tot ^/ w |> Variable.setUnit u) |> Some
            )


    let getTotals
        (totals: Informedica.GenForm.Lib.Types.Data.TotalsData[])
        (age: Informedica.GenUnits.Lib.ValueUnit option)
        (wght: Informedica.GenUnits.Lib.ValueUnit option)
        (dtos: Order.Dto.Dto[])
        : Totals
        =
        let ords = dtos |> Array.choose (Order.Dto.fromDto >> Result.toOption)

        let calc = calc ords wght

        let totals =
            totals
            |> Array.filter (fun t ->
                if wght.IsNone then
                    true
                else
                    let w =
                        wght.Value
                        |> ValueUnit.convertTo Units.Weight.gram
                        |> ValueUnit.getValue
                        |> Array.head

                    match t.MinWeight, t.MaxWeight with
                    | Some min, Some max -> min <= w && w < max
                    | Some min, None -> min <= w
                    | None, Some max -> max > w
                    | None, None -> true
            )
            |> Array.filter (fun t ->
                if age.IsNone then
                    true
                else
                    let a =
                        age.Value
                        |> ValueUnit.convertTo Units.Time.day
                        |> ValueUnit.getValue
                        |> Array.head

                    match t.MinAge, t.MaxAge with
                    | Some min, Some max -> min <= a && a < max
                    | Some min, None -> min <= a
                    | None, Some max -> max > a
                    | None, None -> true
            )

        let calculated =
            totals
            |> Array.collect (fun t ->
                match t.Unit, t.TimeUnit with
                | Some fu, Some tu -> calc t.Name fu tu
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


    /// The serializable shape of the totals: the same string option fields, its own type.
    module Dto =

        type Dto =
            {
                Volume: string option
                Energy: string option
                Protein: string option
                Carbohydrate: string option
                Fat: string option
                Sodium: string option
                Potassium: string option
                Chloride: string option
                Calcium: string option
                Phosphate: string option
                Magnesium: string option
                Iron: string option
                VitaminD: string option
                Ethanol: string option
                Propyleenglycol: string option
                BenzylAlcohol: string option
                BoricAcid: string option
            }


        let toDto (t: Totals) : Dto =
            {
                Volume = t.Volume
                Energy = t.Energy
                Protein = t.Protein
                Carbohydrate = t.Carbohydrate
                Fat = t.Fat
                Sodium = t.Sodium
                Potassium = t.Potassium
                Chloride = t.Chloride
                Calcium = t.Calcium
                Phosphate = t.Phosphate
                Magnesium = t.Magnesium
                Iron = t.Iron
                VitaminD = t.VitaminD
                Ethanol = t.Ethanol
                Propyleenglycol = t.Propyleenglycol
                BenzylAlcohol = t.BenzylAlcohol
                BoricAcid = t.BoricAcid
            }


        /// A field copy; every Dto is totals. A null root is missing; the guard is spelled
        /// out here since this file compiles before the Dto helpers.
        let fromDto (dto: Dto) : Result<Totals, DtoError list> =
            if isNull (box dto) then
                Error [ DtoError.Missing "Totals" ]
            else
                Ok
                    {
                        Volume = dto.Volume
                        Energy = dto.Energy
                        Protein = dto.Protein
                        Carbohydrate = dto.Carbohydrate
                        Fat = dto.Fat
                        Sodium = dto.Sodium
                        Potassium = dto.Potassium
                        Chloride = dto.Chloride
                        Calcium = dto.Calcium
                        Phosphate = dto.Phosphate
                        Magnesium = dto.Magnesium
                        Iron = dto.Iron
                        VitaminD = dto.VitaminD
                        Ethanol = dto.Ethanol
                        Propyleenglycol = dto.Propyleenglycol
                        BenzylAlcohol = dto.BenzylAlcohol
                        BoricAcid = dto.BoricAcid
                    }
