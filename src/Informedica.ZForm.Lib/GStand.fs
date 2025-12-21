namespace Informedica.ZForm.Lib


module GStand =

    open FParsec
    open MathNet.Numerics

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Informedica.Utils.Lib.BCL

    open Aether
    open DoseRule

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


    module GPP = Informedica.ZIndex.Lib.GenPresProduct
    module ATC = Informedica.ZIndex.Lib.ATCGroup
    module DR = Informedica.ZIndex.Lib.DoseRule
    module RF = Informedica.ZIndex.Lib.RuleFinder

    module ZIndexTypes = Informedica.ZIndex.Lib.Types
    module Units = ValueUnit.Units


    /// <summary>
    /// Utility function to get the grouped
    /// sequence of a seq of tuples. Grouping
    /// is by the first element of the tuple.
    /// </summary>
    /// <example>
    /// <code>
    /// let xs = [ (1, "a"); (1, "b"); (2, "c"); (2, "d") ]
    /// groupByFst xs |> Seq.toList
    /// // [(1, seq ["a"; "b"]); (2, seq ["c"; "d"])]
    /// </code>
    /// </example>
    let groupByFst xs =
        xs
        |> Seq.groupBy fst
        |> Seq.sortBy fst
        |> Seq.map (fun (k, v) -> k, v |> Seq.map snd)


    /// An empty `CreateConfig`.
    let config =
        {
            GPKs = []
            IsRate = false
            SubstanceUnit = None
            TimeUnit = None
        }


    /// <summary>
    /// Map a ZIndex MinMax to a GenCore MinIncrMax
    /// </summary>
    /// <param name="setMin">Set the min value</param>
    /// <param name="setMax">Set the max value</param>
    /// <param name="minmax">The min max values</param>
    /// <param name="minIncrMax">The minIncrMax to map to</param>
    /// <example>
    /// <code>
    /// let setMin = ((Option.map ValueUnit.weightInKg) >> (Optic.set MinIncrMax.Optics.inclMinLens))
    /// let setMax = ((Option.map ValueUnit.weightInKg) >> (Optic.set MinIncrMax.Optics.inclMaxLens))
    /// mapMinMax setMin setMax DR.minmax MinIncrMax.empty
    /// </code>
    /// </example>
    let mapMinMax<'a>
        (setMin: float Option -> 'a -> 'a)
        (setMax: float Option -> 'a -> 'a)
        (minmax: ZIndexTypes.RuleMinMax)
        (minIncrMax: 'a)
        =
        minIncrMax
        |> setMin minmax.Min
        |> setMax minmax.Max


    /// <summary>
    /// Get the min max weight if there is one min weight or max weight
    /// i.e. if all min weights are the same and all max weights are the same.
    /// </summary>
    /// <param name="drs"></param>
    /// <exception cref="System.Exception">Cannot calculate weight min max with: {drs}</exception>
    let calcWeightMinMax (drs: ZIndexTypes.DoseRule seq) =

        match drs |> Seq.toList with
        | []    -> DR.minmax
        | [ h ] -> h.Weight
        | h :: tail ->
            if tail
               |> List.forall (fun mm -> mm.Weight = h.Weight) then
                h.Weight
            else
                failwith $"cannot calculate weight min max with: {drs}"
        |> mapMinMax
            ((Option.map ValueUnit.weightInKg)
             >> (Optic.set MinMax.Optics.inclMinLens))
            ((Option.map ValueUnit.weightInKg)
             >> (Optic.set MinMax.Optics.inclMaxLens))


    /// <summary>
    /// Get the min max bsa if there is one min bsa or max bsa
    /// i.e. if all min bsa are the same and all max bsa are the same.
    /// </summary>
    /// <param name="drs"></param>
    /// <exception cref="System.Exception">Cannot calculate bsa min max with: {drs}</exception>
    let calcBSAMinMax (drs: ZIndexTypes.DoseRule seq) =

        match drs |> Seq.toList with
        | [] -> DR.minmax
        | [ h ] -> h.BSA
        | h :: tail ->
            if tail |> List.forall (fun mm -> mm.BSA = h.BSA) then
                h.BSA
            else
                failwith $"cannot calculate bsa min max with: {drs}"
        |> mapMinMax
            ((Option.map ValueUnit.bsaInM2)
             >> (Optic.set MinMax.Optics.inclMinLens))
            ((Option.map ValueUnit.bsaInM2)
             >> (Optic.set MinMax.Optics.inclMaxLens))


    /// Make sure that a GSTand time string
    /// is a valid unit time string.
    let parseTimeString s =
        s
        |> String.replace "per " ""
        |> String.replace "dagen" "dag"
        |> String.replace "weken" "week"
        |> String.replace "maanden" "maand"
        |> String.replace "minuten" "minuut"
        |> String.replace "uren" "uur"
        |> String.replace "eenmalig" ""
        |> (fun s ->
            if s |> String.isNullOrWhiteSpace then
                s
            else
                s + "[Time]"
        )


    /// Try to map a GStand time period to a valid unit.
    let mapTime s =
        s
        |> parseTimeString
        |> Units.fromString


    /// <summary>
    /// Map GStand frequency string to a valid
    /// frequency `ValueUnit`.
    /// </summary>
    /// <param name="freq">The frequency to map</param>
    /// <returns>The mapped frequency</returns>
    /// <exception cref="System.Exception">Cannot parse freq value unit</exception>
    /// <example>
    /// <code>
    /// let freq : ZIndexTypes.RuleFrequency = { Frequency = 1.; Time = "dag" }
    /// mapFreq fr
    /// // ValueUnit ([|1N|], CombiUnit (Count (Times 1N), OpPer, Time (Day 1N)))
    /// </code>
    /// </example>
    let mapFreqToValueUnit (freq: ZIndexTypes.RuleFrequency) =
        let map vu =
            match [
                      2N, ValueUnit.freqUnitPerNday 3N, ValueUnit.freqUnitPerNHour 36N
                  ]
                  |> List.tryFind (fun (f, u, _) -> f |> ValueUnit.createSingle u = vu)
                with
            | Some (_, _, u) -> vu |> ValueUnit.convertTo u
            | None -> vu

        let s =
            freq.Frequency
            |> int
            |> BigRational.fromInt
            |> string

        let s = s + " X[Count]"

        freq.Time
        |> parseTimeString
        |> (fun s' ->
            match s' |> String.trim |> String.split " " with
            | [ v; u ] ->
                let br =
                    match v |> Double.tryParse with
                    | Some d ->
                        match d |> BigRational.fromFloat with
                        | Some s -> s |> string
                        | None -> ""
                    | None -> ""

                s + "/" + br + " " + u
            | [ u ] ->
                if u |> String.isNullOrWhiteSpace then
                    s
                else
                    s + "/1" + " " + u
            | _ -> ""
        )
        |> fun s ->
            s
            |> ValueUnit.fromString
            |> function
            | Failure (err, _, _) ->
                writeErrorMessage $"Cannot parse |{s}| freq value unit: {freq}\n{err}"
                err |> failwith
            | Success (vu, _, _) -> vu
            |> map


    /// <summary>
    /// Map G-Standard DoseRule doses to
    /// <list type="bullet">
    ///     <item>normal min max dose</item>
    ///     <item>absolute min max dose</item>
    ///     <item>normal min max dose per kg</item>
    ///     <item>absolute min max dose per kg</item>
    ///     <item>normal min max dose per m2</item>
    ///     <item>absolute min max dose per m2</item>
    /// </list>
    /// by calculating
    /// - substance form concentration * dose form quantity * frequency
    /// for each dose
    /// </summary>
    /// <param name="n">The name of the substance</param>
    /// <param name="qty">The quantity of the substance</param>
    /// <param name="unit">The unit of the substance</param>
    /// <param name="gstdsr">The GSTand DoseRule</param>
    /// <returns>
    /// The mapped doses
    /// {| absDose: MinIncrMax; absM2: MinIncrMax; absPerKg: MinIncrMax; doserule: Informedica.ZIndex.Lib.Types.DoseRule; frequency: ValueUnit; groupBy: {| isOne: bool; name: string; time: string |}; indication: string; normDose: MinIncrMax; normM2: MinIncrMax; normPerKg: MinIncrMax; routes: list of string |}
    /// </returns>
    let mapDoses (n: string) qty unit (gstdsr: ZIndexTypes.DoseRule) =

        let fr = mapFreqToValueUnit gstdsr.Freq

        let setMin =
            Optic.set MinMax.Optics.inclMinLens

        let setMax =
            Optic.set MinMax.Optics.inclMaxLens

        // ToDo remove n and mapping
        let toVu _ _ v =

            unit
            |> ValueUnit.fromFloat (v * qty)
            |> fun vu ->
                let x =
                    fr
                    |> ValueUnit.get
                    |> fst
                    |> ValueUnit.create Units.Count.times

                vu * x |> Some

        let minmax n mapping (mm: ZIndexTypes.RuleMinMax) =
            MinMax.empty
            |> setMin (mm.Min |> Option.bind (toVu n mapping))
            |> setMax (mm.Max |> Option.bind (toVu n mapping))

        {|
            groupBy =
                {|
                    name = n
                    time = gstdsr.Freq.Time
                    isOne = gstdsr.Freq.Frequency = 1.
                    unitGroup = unit |> ValueUnit.Group.unitToGroup
                |}
            routes = gstdsr.Routes |> Array.toList
            indication = gstdsr.Indication
            frequency = fr
            normDose = gstdsr.Norm |> minmax n Norm
            absDose = gstdsr.Abs |> minmax n Abs
            normPerKg = gstdsr.NormKg |> minmax n NormKg
            absPerKg = gstdsr.AbsKg |> minmax n AbsKg
            normM2 = gstdsr.NormM2 |> minmax n NormM2
            absM2 = gstdsr.AbsM2 |> minmax n AbsM2
            doserule = gstdsr
        |}


    /// <summary>
    /// Map MinMax DoseRanges to a DoseRange record.
    /// </summary>
    /// <param name="dsg">The DoseRange to map to</param>
    /// <returns>
    /// The mapped DoseRange
    /// {| doseRange: DoseRange; doserules: list of Informedica.ZIndex.Lib.Types.DoseRule; frequencies: list of ValueUnit; inds: list of string; routes: list of string |}
    /// </returns>
    let getDoseRange
        (dsg: {| absDose: MinMax
                 absKg: MinMax
                 absM2: MinMax
                 doserules: list<Informedica.ZIndex.Lib.Types.DoseRule>
                 frequencies: list<ValueUnit>
                 indications: list<string>
                 normDose: MinMax
                 normKg: MinMax
                 normM2: MinMax
                 routes: list<string> |})
        =
        let w =
            MinMax.empty |> calcWeightMinMax dsg.doserules

        let b =
            MinMax.empty |> calcBSAMinMax dsg.doserules

        // if weight or bsa is known the adjusted or unadjusted doses can be calculated
        let calcNoneAndAdjusted (c: MinMax) (un: MinMax) (adj: MinMax) =
            // remove the adjust unit by making it a count
            let c =
                c
                |> MinMax.withUnit Units.Count.times

            let calc op x1 x2 y =
                match y with
                | Some _ -> y
                | None ->
                    match x1, x2 with
                    | Some x1_, Some x2_ ->
                        (x1_ |> op <| x2_) |> Some
                    | _ -> y

            // Norm.min = PerKg.min * Wght.min
            // Norm.max = PerKg.max * Wght.max
            {
                Min = un.Min |> calc (*) adj.Min c.Min
                Max = un.Max |> calc (*) adj.Max c.Max
            },
            // PerKg.min = Norm.min / Wght.max
            // PerKg.max = norm.max / Wght.min
            {
                Min = adj.Min |> calc (/) un.Min c.Max
                Max = adj.Max |> calc (/) un.Max c.Min
            }

        {|
            routes = dsg.routes
            inds = dsg.indications
            frequencies = dsg.frequencies
            doserules = dsg.doserules
            doseRange =
                DoseRange.create
                    (calcNoneAndAdjusted w dsg.normDose dsg.normKg |> fst) // norm
                    (calcNoneAndAdjusted w dsg.normDose dsg.normKg |> snd, Units.Weight.kiloGram) // normKg
                    (calcNoneAndAdjusted b dsg.normDose dsg.normM2 |> snd, Units.BSA.m2) // normBSA
                    (calcNoneAndAdjusted w dsg.absDose dsg.absKg |> fst) // abs
                    (calcNoneAndAdjusted w dsg.absDose dsg.absKg |> snd, Units.Weight.kiloGram) // absKg
                    (calcNoneAndAdjusted b dsg.absDose dsg.absM2 |> snd, Units.BSA.m2) // absBSA
        |}


    // fold maximize with preservation of min
    let foldMaximize (mm: MinMax) (mm_: MinMax) =
        [mm; mm_]
        |> MinMax.foldMaximize true true


    /// <summary>
    /// Folds a sequence of `Dosages` to a single `Dosages`
    /// by minimizing the min and maximizing the max values.
    /// </summary>
    /// <param name="ds">The sequence of Dosages</param>
    let foldDosages
        (ds: {| absDose: MinMax
                absM2: MinMax
                absPerKg: MinMax
                doserule: Informedica.ZIndex.Lib.Types.DoseRule
                frequency: ValueUnit
                groupBy: {| isOne: bool
                            name: string
                            time: string
                            unitGroup : Group.Group |}
                indication: string
                normDose: MinMax
                normM2: MinMax
                normPerKg: MinMax
                routes: list<string> |} seq)
        =

        ds
        |> Seq.fold
            (fun acc d ->
                let frs, norm_, abs_, normKg_, absKg_, normM2_, absM2_ =
                    acc

                let frs =
                    let tu = d.frequency |> ValueUnit.get |> snd

                    if frs
                       |> List.exists (fun fr_ ->
                           //let u_ = fr_ |> ValueUnit.get |> snd

                           fr_ |> ValueUnit.get |> snd <> tu
                       ) then
                        let s1 =
                            d.frequency
                            |> ValueUnit.toStringDecimalDutchShortWithPrec 1

                        let s2 =
                            frs
                            |> List.map (ValueUnit.toStringDecimalDutchShortWithPrec 1)
                            |> String.concat ", "

                        failwith
                        <| $"cannot add frequency %s{s1} to list with units %s{s2}"


                    if frs |> List.exists ((=) d.frequency) then
                        frs
                    else
                        d.frequency :: frs

                (*
                let inds =
                    if inds |> List.exists ((=) d.indication) then
                        inds
                    else
                        d.indication :: inds
                *)

                //let gstdsrs = d.doserule :: gstdsrs

                let norm = foldMaximize d.normDose norm_
                let abs = foldMaximize d.absDose abs_

                let normKg =
                    foldMaximize d.normPerKg normKg_

                let absKg = foldMaximize d.absPerKg absKg_
                let normM2 = foldMaximize d.normM2 normM2_
                let absM2 = foldMaximize d.absM2 absM2_

                frs, norm, abs, normKg, absKg, normM2, absM2
            )
            ([],
             MinMax.empty,
             MinMax.empty,
             MinMax.empty,
             MinMax.empty,
             MinMax.empty,
             MinMax.empty)
        |> fun (frs, norm, abs, normKg, absKg, normM2, absM2) ->
            {|
                routes =
                    ds
                    |> Seq.collect _.routes
                    |> Seq.toList
                indications =
                    ds
                    |> Seq.map _.indication
                    |> Seq.distinct
                    |> Seq.toList
                frequencies = frs
                normDose = norm
                absDose = abs
                normKg = normKg
                absKg = absKg
                normM2 = normM2
                absM2 = absM2
                doserules =
                    ds
                    |> Seq.map _.doserule
                    |> Seq.toList
            |}


    /// <summary>
    /// Get the Dosage and Indications for a given DoseRange and list of DoseRules. Uses a
    /// config to create a Dosage with a start, rate or total dose.
    /// </summary>
    /// <param name="cfg">The Config</param>
    /// <param name="n">The 'Name' of the Dosage</param>
    /// <param name="dsr">The DoseRules</param>
    let getDosage
        cfg
        n
        (dsr: {| doseRange: DoseRange
                 doserules: list<Informedica.ZIndex.Lib.Types.DoseRule>
                 frequencies: list<ValueUnit>
                 inds: list<string>
                 routes: list<string> |})
        =
        let tu =
            match dsr.frequencies with
            | fr :: _ ->
                match fr |> ValueUnit.get |> snd with
                | CombiUnit (_, OpPer, tu) -> tu
                | _ -> NoUnit
            | _ -> NoUnit

        {|
            indications = dsr.inds
            dosage =
                Dosage.empty
                |> Dosage.Optics.setName n
                |> Dosage.Optics.setRules (
                    if dsr.doserules |> List.isEmpty then
                        printfn $"EMPTY DOSERULES: {n}"
                    dsr.doserules
                    |> List.map (DR.toString2 >> GStandRule)
                )
                |> (fun ds ->
                    match tu with
                    | _ when tu = Unit.NoUnit || (tu |> ValueUnit.isCountUnit) ->
                        ds
                        |> (Optic.set Dosage.StartDosage_ dsr.doseRange)

                    | _ when
                        cfg.IsRate
                        && dsr.frequencies |> List.length = 1
                        && tu = Units.Time.hour
                        ->

                        ds
                        |> (Optic.set Dosage.RateDosage_ (dsr.doseRange, tu))
                        |> (fun ds ->
                            match cfg.TimeUnit with
                            | Some u -> ds |> Dosage.convertRateUnitTo u
                            | None -> ds
                        )

                    | _ ->
                        let frs =
                            let fr =
                                dsr.frequencies
                                |> List.collect (ValueUnit.getValue >> Array.toList)
                                |> List.sort

                            Dosage.createFrequency fr tu None

                        ds
                        |> (Optic.set Dosage.TotalDosage_ (dsr.doseRange, frs))
                    // Perform unit conversion
                    |> (fun ds ->
                        match cfg.SubstanceUnit with
                        | Some u -> ds |> Dosage.convertSubstanceUnitTo u
                        | None -> ds

                    )
                )
        |}


    /// <summary>
    /// Create the `Dosages` for a given list of `DoseRules` for
    /// a Substance.
    /// </summary>
    /// <param name="cfg">The Config</param>
    /// <param name="drs">The DoseRules</param>
    let getSubstanceDoses (cfg: CreateConfig) (drs: ZIndexTypes.DoseRule seq) =
        drs
        |> Seq.collect (fun dr ->
            dr.GenericProduct
            |> Seq.collect (fun gp ->
                gp.Substances
                |> Seq.collect (fun s ->
                    match s.Unit
                        //TODO: rewrite to new online mapping
                          |> ValueUnit.unitFromZIndexString
                        with
                    | NoUnit -> []
                    | u -> [ mapDoses s.Name s.Quantity u dr ]
                )
            )
        )
        |> Seq.groupBy _.groupBy // group by substance name frequency time and whether frequency = 1
        |> Seq.map (fun (k, mappedDoses) ->
            mappedDoses
            |> foldDosages
            |> getDoseRange
            |> getDosage cfg k.name
        )


    /// <summary>
    /// Create the `Dosages` for a given list of `DoseRules` for
    /// a PatientCategory.
    /// </summary>
    /// <param name="cfg">The Config</param>
    /// <param name="drs">The ZIndex DoseRules</param>
    /// <returns>{| doseRules: DoseRule seq; patientCategory: PatientCategory; substanceDoses: {| Dosage: Dosage; indications: string list |} seq |} seq</returns>
    let getPatients (cfg: CreateConfig) (drs: ZIndexTypes.DoseRule seq) =
        let map = mapMinMax<PatientCategory>

        let ageInMo = Option.map ValueUnit.ageInMo

        let wghtKg = Option.map ValueUnit.weightInKg

        let bsaM2 = Option.map ValueUnit.bsaInM2

        let mapAge =
            map
                (ageInMo >> PatientCategory.Optics.setInclMinAge)
                (ageInMo >> PatientCategory.Optics.setExclMaxAge)

        let mapWght =
            map
                (wghtKg >> PatientCategory.Optics.setInclMinWeight)
                (wghtKg >> PatientCategory.Optics.setInclMaxWeight)

        let mapBSA =
            map
                (bsaM2 >> PatientCategory.Optics.setInclMinBSA)
                (bsaM2 >> PatientCategory.Optics.setInclMaxBSA)

        let mapGender s =
            match s with
            | _ when s = "man" -> Male
            | _ when s = "vrouw" -> Female
            | _ -> Undetermined
            |> PatientCategory.Optics.setGender

        drs
        |> Seq.map (fun dr ->
            {|
                indication = dr.Indication
                patientCat =
                     PatientCategory.empty
                     |> mapAge dr.Age
                     |> mapWght dr.Weight
                     |> mapBSA dr.BSA
                     |> mapGender dr.Gender
            |}, dr
        )
        |> Seq.groupBy fst
        |> Seq.map (fun (k, v) ->
            {|
                patientCategory = k.patientCat
                substanceDoses =
                    v
                    |> Seq.map snd
                    |> getSubstanceDoses cfg
                doseRules = drs
            |}
        )


    // Get the ATC codes for a GenPresProduct.
    let getATCs gpk (gpp: ZIndexTypes.GenPresProduct) =
        gpp.GenericProducts
        |> Array.filter (fun gp ->
            match gpk with
            | None -> true
            | Some id -> gp.Id = id
        )
        |> Array.map _.ATC
        |> Array.distinct


    // Get the list of routes for a GenPresProduct.
    let getRoutes (gpp: ZIndexTypes.GenPresProduct) =
        gpp.GenericProducts
        |> Array.collect _.Route
        |> Array.distinct


    // Get the list of ATC groups for a GenPresProduct.
    let getATCGroups gpk (gpp: ZIndexTypes.GenPresProduct) =

        ATC.get ()
        |> Array.filter (fun g ->
            gpp
            |> getATCs gpk
            |> Array.exists (fun a -> a |> String.equalsCapInsens g.ATC5)
            && g.Form = gpp.Form
        )
        |> Array.distinct


    /// Get a list of TradeNames for a GenPresProduct.
    let getTradeNames (gpp: ZIndexTypes.GenPresProduct) =
        gpp.GenericProducts
        |> Seq.collect _.PrescriptionProducts
        |> Seq.collect _.TradeProducts
        |> Seq.map (fun tp ->
            match tp.Name |> String.split " " with
            | h :: _ -> h |> String.trim
            | _ -> ""
        )
        |> Seq.filter (fun n -> n |> String.isNullOrWhiteSpace |> not)
        |> Seq.toList


    let mergeDosages d ds =
        let rules =
            d.Rules
            |> Seq.append (ds |> Seq.collect _.Rules)
            |> Seq.distinct

        let merge d1 d2 =
            Dosage.empty
            // merge name
            |> (fun d ->
                d
                |> Dosage.Optics.setName (d1 |> Dosage.Optics.getName)
            )
            // merge start dose
            |> (fun d ->
                if d.StartDosage = DoseRange.empty then
                    if d1.StartDosage = DoseRange.empty then
                        d2.StartDosage
                    else
                        d1.StartDosage
                    |> (fun x -> d |> (Optic.set Dosage.StartDosage_ x))
                else
                    d
            )
            // merge single dose
            |> (fun d ->
                if d.SingleDosage = DoseRange.empty then
                    if d1.SingleDosage = DoseRange.empty then
                        d2.SingleDosage
                    else
                        d1.SingleDosage
                    |> (fun x -> d |> (Optic.set Dosage.SingleDosage_ x))
                else
                    d
            )
            // merge Rate dose
            |> (fun d ->
                if d.RateDosage |> fst = DoseRange.empty then
                    if d1.RateDosage |> fst = DoseRange.empty then
                        d2.RateDosage
                    else
                        d1.RateDosage
                    |> (fun x -> d |> (Optic.set Dosage.RateDosage_ x))
                else
                    d
            )
            // merge frequencies when freq is 1 then check
            // whether the freq is a start dose
            |> (fun d ->
                // only merge frequencies for same total dose
                // ToDo use ValueUnit eqs function
                let td1 = d1 |> Optic.get Dosage.TotalDosage_ |> fst
                let td2 = d2 |> Optic.get Dosage.TotalDosage_ |> fst

                if td1 = td2
                   || td1 = DoseRange.empty
                   || td2 = DoseRange.empty then
                    let d =
                        if td1 = DoseRange.empty then
                            d2.TotalDosage
                        else
                            d1.TotalDosage
                        |> (fun td -> d |> (Optic.set Dosage.TotalDosage_ td))

                    d1
                    |> Dosage.Optics.getFrequencyValues
                    |> List.append (d2 |> Dosage.Optics.getFrequencyValues)
                    |> (fun vs -> d |> Dosage.Optics.setFrequencyValues vs)
                    // merged Total dose

                else if d1 |> Dosage.Optics.getFrequencyValues = [ 1N ] then
                    d
                    |> (Optic.set Dosage.SingleDosage_ (d1.TotalDosage |> fst))
                    |> (fun d ->
                        d2
                        |> Dosage.Optics.getFrequencyValues
                        |> (fun vs ->
                            d
                            |> Dosage.Optics.setFrequencyValues vs
                            |> (Optic.set Dosage.TotalDosage_ (d2 |> Optic.get Dosage.TotalDosage_))
                        )
                    )
                else if d2 |> Dosage.Optics.getFrequencyValues = [ 1N ] then
                    d
                    |> (Optic.set Dosage.SingleDosage_ (d2.TotalDosage |> fst))
                    |> (fun d ->
                        d1
                        |> Dosage.Optics.getFrequencyValues
                        |> (fun vs ->
                            d
                            |> Dosage.Optics.setFrequencyValues vs
                            |> (Optic.set Dosage.TotalDosage_ (d1 |> Optic.get Dosage.TotalDosage_))
                        )
                    )
                else
                    d

            )

            |> (fun d ->
                d
                |> Dosage.Optics.setFrequencyTimeUnit (d1 |> Dosage.Optics.getFrequencyTimeUnit)
                |> Dosage.Optics.setRules (rules |> Seq.toList)
            )

        match ds |> Seq.toList with
        | [ d1 ] -> seq { merge d1 d }
        | _ -> ds |> Seq.append (seq { yield d })


    /// <summary>
    /// Group the GenPresProducts by indications, routes, pharmaceutical form and products and
    /// add the patients and dosages.
    /// </summary>
    /// <param name="rte">The route</param>
    /// <param name="age">Age in Months</param>
    /// <param name="wght">Weight in Kg</param>
    /// <param name="bsa">Body Surface Area in mˆ2</param>
    /// <param name="gpk">Optional GPK</param>
    /// <param name="cfg">The CreateConfig</param>
    /// <param name="gpps">The GenPresProducts</param>
    /// <returns>
    /// The grouped GenPresProducts.
    /// </returns>
    let groupGenPresProducts rte age wght bsa gpk cfg gpps =
        gpps
        |> Seq.collect (fun (gpp: ZIndexTypes.GenPresProduct) ->
            gpp.Routes
            |> Seq.filter (fun r ->
                rte |> String.isNullOrWhiteSpace
                || r |> String.equalsCapInsens rte
            )
            |> Seq.collect (fun rte ->
                RF.createFilter age wght bsa gpk gpp.Name gpp.Form rte
                |> RF.find cfg.GPKs
                |> getPatients cfg
                |> Seq.sortBy (fun pats ->
                    pats.patientCategory.Age.Min,
                    pats.patientCategory.Weight.Min,
                    pats.patientCategory.BSA.Min
                )
                |> Seq.collect (fun pats ->
                    let gps =
                        pats.doseRules
                        |> Seq.collect (fun dr ->
                            dr.GenericProduct
                            |> Seq.map (fun gp -> gp.Id, gp.Name)
                        )

                    let tps =
                        pats.doseRules
                        |> Seq.collect (fun dr ->
                            dr.TradeProduct
                            |> Seq.map (fun tp -> tp.Id, tp.Name)
                        )

                    pats.substanceDoses
                    |> Seq.map (fun dsg ->
                        {|
                            indications = dsg.indications
                            route = rte
                            Form = gpp.Form
                            genericProducts = gps
                            tradeProducts = tps
                            patientCategory = pats.patientCategory
                            dosage = dsg.dosage
                        |}
                        //dsg.indications, (rte, (gpp.Form, gps, tps, pats.patientCategory, dsg.Dosage))
                    )
                )
            )
        )
        |> Seq.groupBy _.indications // group by indications
        |> Seq.map (fun (inds, v) ->
            {|
                indications = inds
                routes =
                    v
                    |> Seq.groupBy _.route // group by route
                    |> Seq.map (fun (rte, v) ->
                        {|
                            route = rte
                            formAndProducts =
                                v
                                |> Seq.groupBy (fun r -> r.Form, r.genericProducts, r.tradeProducts)
                                |> Seq.sortBy (fst >> fun (frm, _, _) -> frm)
                                |> Seq.map (fun ((frm, gps, tps), v) ->
                                    {|
                                        form = frm
                                        genericProducts = gps
                                        tradeProducts = tps
                                        patients =
                                            v
                                            |> Seq.groupBy _.patientCategory
                                            |> Seq.map (fun (k, v) ->
                                                k,
                                                v |> Seq.map _.dosage
                                            )
                                    |}
                                )
                        |}
                    )
            |}
        )


    /// <summary>
    /// Add indications, routes, pharmaceutical form, patient and dosages to
    /// a starting GStand DoseRule.
    /// </summary>
    /// <param name="dr">The starting Gstand DoseRule record</param>
    /// <param name="inds">The indications, routes, form, patient and dosages</param>
    let addIndicationsRoutesFormPatientDosages
        dr
        (inds: seq<{| indications: list<string>
                      routes: seq<{| route: string
                                     formAndProducts: seq<{| genericProducts: seq<int * string>
                                                             patients: seq<PatientCategory * seq<Dosage>>
                                                             form: string
                                                             tradeProducts: seq<int * string> |}> |}> |}>)
        =

        inds
        |> Seq.fold
            (fun acc ind ->
                let dr =
                    acc
                    |> addIndications ind.indications

                ind.routes
                |> Seq.fold
                    (fun acc rt ->
                        let dr =
                            acc |> Optics.addRoute ind.indications rt.route

                        rt.formAndProducts
                        |> Seq.fold
                            (fun acc frm ->
                                let createGP =
                                    FormDosage.GenericProduct.create

                                let createTP =
                                    FormDosage.TradeProduct.create

                                let dr =
                                    acc
                                    |> Optics.addForm ind.indications rt.route [ frm.form ]
                                    |> Optics.setGenericProducts
                                        ind.indications
                                        rt.route
                                        [ frm.form ]
                                        (frm.genericProducts
                                         |> Seq.toList
                                         |> List.map (fun (id, nm) -> createGP id nm)
                                         |> List.sortBy _.Label)
                                    |> Optics.setTradeProducts
                                        ind.indications
                                        rt.route
                                        [ frm.form ]
                                        (frm.tradeProducts
                                         |> Seq.toList
                                         |> List.map (fun (id, nm) -> createTP id nm)
                                         |> List.sortBy _.Label)

                                frm.patients
                                |> Seq.fold
                                    (fun acc pat ->
                                        let pat, sds = pat

                                        let sds =
                                            sds
                                            |> Seq.fold
                                                (fun acc sd ->
                                                    match acc
                                                          |> Seq.toList
                                                          |> List.filter (fun d -> d.Name = sd.Name)
                                                        with
                                                    | [] -> acc |> Seq.append (seq { yield sd })
                                                    | ns ->
                                                        match ns
                                                              |> List.filter (fun d ->
                                                                  d |> Dosage.Optics.getFrequencyTimeUnit =
                                                                     (sd
                                                                     |> Dosage.Optics.getFrequencyTimeUnit)
                                                                  || sd |> Dosage.Optics.getFrequencyValues = []
                                                              )
                                                            with
                                                        | [] -> acc |> Seq.append (seq { yield sd })
                                                        | _ -> acc |> mergeDosages sd

                                                )
                                                Seq.empty

                                        acc
                                        |> Optics.addPatient ind.indications rt.route [ frm.form ] pat
                                        |> Optics.setSubstanceDosages
                                            ind.indications
                                            rt.route
                                            [ frm.form ]
                                            pat
                                            (sds |> Seq.toList)
                                    )
                                    dr
                            )
                            dr
                    )
                    dr
            )
            dr


    /// <summary>
    /// Fold the GenPresProducts to a single DoseRule.
    /// </summary>
    /// <param name="rte">The route</param>
    /// <param name="age">Age in Months</param>
    /// <param name="wght">Weight in Kg</param>
    /// <param name="bsa">Body Surface Area in mˆ2</param>
    /// <param name="gpk">Optional GPK</param>
    /// <param name="cfg">The CreateConfig</param>
    /// <param name="dr">The DoseRule</param>
    /// <param name="gpps">The GenPresProducts</param>
    let foldDoseRules rte age wght bsa gpk cfg dr gpps =
        let dr =
            dr
            |> Optics.setSynonyms (gpps |> Seq.collect getTradeNames |> Seq.toList)

        gpps
        |> groupGenPresProducts rte age wght bsa gpk cfg
        |> addIndicationsRoutesFormPatientDosages dr


    /// <summary>
    /// Create the DoseRules.
    /// </summary>
    /// <param name="cfg">The CreateConfig</param>
    /// <param name="age">Age in Months</param>
    /// <param name="wght">Weight in Kg</param>
    /// <param name="bsa">Body Surface Area in mˆ2</param>
    /// <param name="gpk">Optional GPK</param>
    /// <param name="gen">Generic Name</param>
    /// <param name="frm">Form</param>
    /// <param name="rte">Route</param>
    /// <remarks>
    /// The GPK is used to filter the GenPresProducts and use those
    /// GenericProducts to create the DoseRules. If the GPK is None
    /// then generic name, pharmaceutical form and route are used to filter the
    /// GenPresProducts.
    /// </remarks>
    let createDoseRules (cfg: CreateConfig) age wght bsa gpk gen frm rte =

        GPP.filter gen frm rte
        |> Seq.filter (fun gpp ->
            match gpk with
            | None -> true
            | Some id ->
                gpp.GenericProducts
                |> Seq.exists (fun gp -> gp.Id = id)
        )
        |> Seq.collect (fun gpp ->
            gpp
            |> getATCGroups gpk
            |> Seq.map (fun atc ->
                {|
                    generic = atc.Generic
                    atc5 = atc.ATC5
                    mainGroup = atc.TherapeuticMainGroup
                    subGroup = atc.TherapeuticSubGroup
                    pharmacologic = atc.PharmacologicalGroup
                    substance = atc.Substance
                |},
                gpp
            )
        )
        |> groupByFst
        |> Seq.map (fun (r, gpps) ->
            let gen, atc, tg, tsg, pg, sg =
                r.generic, r.atc5, r.mainGroup, r.subGroup, r.pharmacologic, r.substance

            // create empty dose rule
            let dr = create gen [] atc tg tsg pg sg []

            foldDoseRules rte age wght bsa gpk cfg dr gpps
        )