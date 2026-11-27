namespace Informedica.GenOrder.Lib


module Totals =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib
    open Informedica.GenOrder.Lib
    open Informedica.GenSolver.Lib.Variable.Operators

    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreGestationalAgeNeed = Informedica.GenCore.Lib.Patients.GestationalAgeNeed
    module MinMax = Informedica.GenForm.Lib.Utils.MinMax
    module Mapping = Informedica.GenForm.Lib.Mapping
    module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange

    type CoreNeed = Informedica.GenCore.Lib.Patients.GestationalAgeNeed


    /// Totals with every value None, for a plan or a context before an evaluation calculates the totals.
    let empty: Totals =
        {
            Volume = None
            Energy = None
            Protein = None
            Carbohydrate = None
            Fat = None
            Sodium = None
            Potassium = None
            Chloride = None
            Calcium = None
            Phosphate = None
            Magnesium = None
            Iron = None
            VitaminD = None
            Ethanol = None
            Propyleenglycol = None
            BenzylAlcohol = None
            BoricAcid = None
        }


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


    /// Returns the volume of a dose in mL per time unit, or None when the dose has no volume.
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


    /// Calculates the totals of the orders for the item name in the unit fu per time unit tu; for the name
    /// volume, the total volume of the orders in mL per time unit tu. Returns the name of each total with the
    /// total per time and the total per kg per time; no totals without a weight.
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
                    if name |> String.equalsCapInsens "volume" then
                        match getVolume tu o.Schedule o.Orderable.Dose with
                        | Some vol -> "volume", vol
                        | None -> ()

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

                    (n, tot, tot ^/ w |> Variable.setUnit u) |> Some
            )


    /// Returns the gestational age that selects the Totals rows: the given gestational age, or term when
    /// GenCORE does not require a gestational age for the age. Returns None when the gestational age is
    /// required and not given.
    let gestAgeOrTerm age gestAge =
        match gestAge with
        | Some ga -> Some ga
        | None ->
            match age |> Option.map CoreAgeValue.fromValueUnit |> CoreGestationalAgeNeed.ofAge with
            | CoreNeed.RequiredGestationalAge -> None
            | CoreNeed.OptionalGestationalAge
            | CoreNeed.NoGestationalAge -> CoreAgeWeeksDays.term |> CoreAgeWeeksDays.toValueUnit |> Some


    /// Creates a MinMax in a unit with an inclusive minimum and an exclusive maximum.
    let fromTupleInclExcl =
        MinMax.fromTuple Informedica.GenCore.Lib.Ranges.Inclusive Informedica.GenCore.Lib.Ranges.Exclusive


    /// Whether a Totals row has no patient bounds: no age, weight or gestational age.
    let isForEveryPatient (t: Informedica.GenForm.Lib.Types.Data.TotalsData) =
        [ t.MinAge; t.MaxAge; t.MinWeight; t.MaxWeight; t.MinGestAge; t.MaxGestAge ]
        |> List.forall Option.isNone


    /// Whether a Totals row applies to a patient with this age, weight and gestational age. An unknown age
    /// or weight does not limit the rows. A preterm patient matches a row only when the row has gestational
    /// age bounds or no patient bounds at all, the same as for the dose rules.
    let appliesTo age wght gestAge (t: Informedica.GenForm.Lib.Types.Data.TotalsData) =
        // MinMax.inRange returns false for a missing value, so a missing age or weight passes here.
        let within u bounds vu =
            vu
            |> Option.map (Some >> MinMax.inRange (bounds |> fromTupleInclExcl (Some u)))
            |> Option.defaultValue true

        let hasGestAgeBounds = t.MinGestAge.IsSome || t.MaxGestAge.IsSome

        wght |> within Units.Weight.gram (t.MinWeight, t.MaxWeight)
        && age |> within Units.Time.day (t.MinAge, t.MaxAge)
        && Some gestAge |> within Units.Time.day (t.MinGestAge, t.MaxGestAge)
        && (t |> isForEveryPatient
            || hasGestAgeBounds
            || gestAge |> CoreAgeWeeksDays.isPretermValue |> not)


    /// Returns the unit of a form of a component: the unit of the quantities of its first product, or NoUnit
    /// when the component has no product with quantities.
    let formUnit (cmp: Component) =
        cmp.Products
        |> List.tryHead
        |> Option.bind _.FormQuantities
        |> Option.map ValueUnit.getUnit
        |> Option.defaultValue NoUnit


    /// Whether an order counts in the totals: the form of one of its components, on the route of the order,
    /// matches a FormRoute row marked Totals. A component without a product with quantities matches a row in
    /// any unit. An order with an empty route, a component with an empty form, or a form that has no FormRoute
    /// row does not count.
    let isTotalsOrder routeMapping formRoutes (ord: Order) =
        // the order can carry the short route, such as "or", and the sheet the long one, such as "oraal"
        let route = ord.Route |> Mapping.mapRoute routeMapping |> Option.defaultValue ord.Route

        route |> String.isNullOrWhiteSpace |> not
        && ord.Orderable.Components
           |> List.exists (fun cmp ->
               cmp.Form |> String.isNullOrWhiteSpace |> not
               && Mapping.filterFormRoutes routeMapping formRoutes route cmp.Form (formUnit cmp)
                  |> Array.exists _.Totals
           )


    /// Whether every value of a calculated total lies within the bounds min and max in the unit u. The total
    /// and the bounds are compared as ValueUnits, so the total may be in another unit of the same group, such
    /// as mL/kg/min against bounds in mL/kg/day. A missing bound does not limit that side. A total without a
    /// lowest value lies partly below a minimum, and a total without a highest value lies partly above a
    /// maximum.
    let isWithin u min max total =
        let vr = total |> Variable.getValueRange
        let bound = ValueUnit.singleWithUnit u

        let lowerHolds =
            match min, vr |> ValueRange.getMin with
            | None, _ -> true
            | Some _, None -> false
            | Some m, Some lo -> ValueUnit.gte (lo |> ValueRange.Minimum.toValueUnit) (bound m)

        let upperHolds =
            match max, vr |> ValueRange.getMax with
            | None, _ -> true
            | Some _, None -> false
            | Some m, Some hi -> ValueUnit.ste (hi |> ValueRange.Maximum.toValueUnit) (bound m)

        lowerHolds && upperHolds


    /// Wraps the text of a total in a Valid block when the total lies within the bounds of its Totals row,
    /// else in a Warning block. The total per kg per time is checked against MinPerTimeAdj and
    /// MaxPerTimeAdj in perKgUnit, and the total per time against MinPerTime and MaxPerTime in perTimeUnit.
    let toBlock (row: Informedica.GenForm.Lib.Types.Data.TotalsData) perTimeUnit perKgUnit perTime perKg text =
        if
            perKg |> isWithin perKgUnit row.MinPerTimeAdj row.MaxPerTimeAdj
            && perTime |> isWithin perTimeUnit row.MinPerTime row.MaxPerTime
        then
            Valid text
        else
            Warning text


    /// Calculates the totals of the orders per kg and per time unit, with the norm of the Totals row that
    /// applies to the age, weight and gestational age of the patient. Only the orders whose route and form
    /// are marked Totals in the FormRoute rows count. A total that lies partly outside the bounds of its
    /// Totals row is a Warning block. Every total is None without a weight, or when the gestational age is
    /// required and not given.
    let getTotals routeMapping formRoutes totals age wght gestAge ords =
        match gestAgeOrTerm age gestAge with
        | None -> empty
        | Some gestAge ->
            let ords = ords |> Array.filter (isTotalsOrder routeMapping formRoutes)
            let calc = calc ords wght
            let totals = totals |> Array.filter (appliesTo age wght gestAge)

            let calculated =
                totals
                |> Array.collect (fun t ->
                    match t.Unit, t.TimeUnit with
                    | Some fu, Some tu ->
                        let perTimeUnit = fu |> ValueUnit.per tu
                        let perKgUnit = fu |> ValueUnit.per Units.Weight.kiloGram |> ValueUnit.per tu

                        // calc gives the volume in mL whatever the unit of the volume row, so the totals are
                        // converted to the units of their row
                        calc t.Name fu tu
                        |> Array.map (fun (n, perTime, perKg) ->
                            n,
                            (t,
                             perTimeUnit,
                             perKgUnit,
                             perTime |> Variable.convertToUnit perTimeUnit,
                             perKg |> Variable.convertToUnit perKgUnit)
                        )
                    | _ -> [||]
                )

            let get n =
                calculated
                |> Array.tryFind (fst >> String.equalsCapInsens n)
                |> Option.map (fun (_, (row, perTimeUnit, perKgUnit, perTime, perKg)) ->
                    let s = perKg |> Variable.getValueRange |> ValueRange.toMarkdown 3

                    let text =
                        match row.MinPerTimeAdj, row.MaxPerTimeAdj with
                        | Some minAdj, Some maxAdj ->
                            let norm = $"{minAdj |> BigRational.toDouble} - {maxAdj |> BigRational.toDouble}"
                            s + $" ({norm})"
                        | None, Some maxAdj ->
                            let norm = $"{maxAdj |> BigRational.toDouble}"
                            s + $" (max {norm})"
                        | _ -> s

                    text |> toBlock row perTimeUnit perKgUnit perTime perKg
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


    /// The serializable shape of the totals, each total as a text block Dto.
    module Dto =

        open FsToolkit.ErrorHandling


        type Dto =
            {
                Volume: TextBlock.Dto.Dto option
                Energy: TextBlock.Dto.Dto option
                Protein: TextBlock.Dto.Dto option
                Carbohydrate: TextBlock.Dto.Dto option
                Fat: TextBlock.Dto.Dto option
                Sodium: TextBlock.Dto.Dto option
                Potassium: TextBlock.Dto.Dto option
                Chloride: TextBlock.Dto.Dto option
                Calcium: TextBlock.Dto.Dto option
                Phosphate: TextBlock.Dto.Dto option
                Magnesium: TextBlock.Dto.Dto option
                Iron: TextBlock.Dto.Dto option
                VitaminD: TextBlock.Dto.Dto option
                Ethanol: TextBlock.Dto.Dto option
                Propyleenglycol: TextBlock.Dto.Dto option
                BenzylAlcohol: TextBlock.Dto.Dto option
                BoricAcid: TextBlock.Dto.Dto option
            }


        let toDto (t: Totals) : Dto =
            let block = Option.map TextBlock.Dto.toDto

            {
                Volume = t.Volume |> block
                Energy = t.Energy |> block
                Protein = t.Protein |> block
                Carbohydrate = t.Carbohydrate |> block
                Fat = t.Fat |> block
                Sodium = t.Sodium |> block
                Potassium = t.Potassium |> block
                Chloride = t.Chloride |> block
                Calcium = t.Calcium |> block
                Phosphate = t.Phosphate |> block
                Magnesium = t.Magnesium |> block
                Iron = t.Iron |> block
                VitaminD = t.VitaminD |> block
                Ethanol = t.Ethanol |> block
                Propyleenglycol = t.Propyleenglycol |> block
                BenzylAlcohol = t.BenzylAlcohol |> block
                BoricAcid = t.BoricAcid |> block
            }


        /// Reads the text block of every total. A Dto that is null after JSON deserialization gives
        /// DtoError.Missing "Totals", and a block with an unknown kind gives DtoError.UnknownTextKind.
        let fromDto (dto: Dto) : Result<Totals, DtoError list> =
            let block =
                function
                | None -> Ok None
                | Some b -> b |> TextBlock.Dto.fromDto |> Result.map Some |> Result.mapError List.singleton

            Nested.required
                "Totals"
                dto
                (fun dto ->
                    validation {
                        let! volume = dto.Volume |> block
                        and! energy = dto.Energy |> block
                        and! protein = dto.Protein |> block
                        and! carbohydrate = dto.Carbohydrate |> block
                        and! fat = dto.Fat |> block
                        and! sodium = dto.Sodium |> block
                        and! potassium = dto.Potassium |> block
                        and! chloride = dto.Chloride |> block
                        and! calcium = dto.Calcium |> block
                        and! phosphate = dto.Phosphate |> block
                        and! magnesium = dto.Magnesium |> block
                        and! iron = dto.Iron |> block
                        and! vitaminD = dto.VitaminD |> block
                        and! ethanol = dto.Ethanol |> block
                        and! propyleenglycol = dto.Propyleenglycol |> block
                        and! benzylAlcohol = dto.BenzylAlcohol |> block
                        and! boricAcid = dto.BoricAcid |> block

                        return
                            {
                                Volume = volume
                                Energy = energy
                                Protein = protein
                                Carbohydrate = carbohydrate
                                Fat = fat
                                Sodium = sodium
                                Potassium = potassium
                                Chloride = chloride
                                Calcium = calcium
                                Phosphate = phosphate
                                Magnesium = magnesium
                                Iron = iron
                                VitaminD = vitaminD
                                Ethanol = ethanol
                                Propyleenglycol = propyleenglycol
                                BenzylAlcohol = benzylAlcohol
                                BoricAcid = boricAcid
                            }
                    }
                )
