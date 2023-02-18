namespace Informedica.ZForm.Lib


module GStand =

    open MathNet.Numerics

    open Informedica.Utils.Lib
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


    let groupByFst xs =
        xs
        |> Seq.groupBy fst
        |> Seq.sortBy fst
        |> Seq.map (fun (k, v) -> k, v |> Seq.map snd)


    let config =
        {
            UseAll = true
            IsRate = false
            SubstanceUnit = None
            TimeUnit = None

        }


    /// Map GSTand min max float Option values to
    /// a `DoseRule` `MinMax`
    let mapMinMax<'a>
        (setMin: decimal Option -> 'a -> 'a)
        (setMax: decimal Option -> 'a -> 'a)
        (minmax: ZIndexTypes.RuleMinMax)
        (o: 'a)
        =
        o |> setMin minmax.Min |> setMax minmax.Max


    // Get the min max weight if there is one min weight or max weight
    let calcWeightMinMax (drs: ZIndexTypes.DoseRule seq) =

        match drs |> Seq.toList with
        | [] -> DR.minmax
        | [ h ] -> h.Weight
        | h :: tail ->
            if tail
               |> List.forall (fun mm -> mm.Weight = h.Weight) then
                h.Weight
            else
                DR.minmax
        |> mapMinMax
            ((Option.map ValueUnit.weightInKg)
             >> (Optic.set MinIncrMax.Optics.inclMinLens))
            ((Option.map ValueUnit.weightInKg)
             >> (Optic.set MinIncrMax.Optics.exclMaxLens))


    // Get the min max bsa if there is one min bsa or max bsa
    let calcBSAMinMax (drs: ZIndexTypes.DoseRule seq) =

        match drs |> Seq.toList with
        | [] -> DR.minmax
        | [ h ] -> h.BSA
        | h :: tail ->
            if tail |> List.forall (fun mm -> mm.BSA = h.Weight) then
                h.BSA
            else
                DR.minmax
        |> mapMinMax
            ((Option.map ValueUnit.bsaInM2)
             >> (Optic.set MinIncrMax.Optics.inclMinLens))
            ((Option.map ValueUnit.bsaInM2)
             >> (Optic.set MinIncrMax.Optics.exclMaxLens))


    /// Make sure that a GSTand time string
    /// is a valid unit time string
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


    /// Map a GStand time period to a valid unit
    let mapTime s =
        s |> parseTimeString |> Units.fromString


    // TODO: rewrite to frequency mapping
    /// Map GStand frequency string to a valid
    /// frequency `ValueUnit`.
    let mapFreq (fr: DR.Frequency) =
        let map vu =
            match [
                      2N, ValueUnit.freqUnitPerNday 3N, ValueUnit.freqUnitPerNHour 36N
                  ]
                  |> List.tryFind (fun (f, u, _) -> f |> ValueUnit.createSingle u = vu)
                with
            | Some (_, _, c) -> vu |> ValueUnit.convertTo c
            | None -> vu

        let s =
            fr.Frequency |> BigRational.fromDecimal |> string

        let s = s + " X[Count]"

        fr.Time
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
        |> ValueUnit.fromString
        |> map


    /// Map GSTand doserule doses to
    /// - normal   min max dose
    /// - absolute min max dose
    /// - normal   min max dose per kg
    /// - absolute min max dose per kg
    /// - normal   min max dose per m2
    /// - absolute min max dose per m2
    /// by calculating
    /// - substance shape concentration * dose shape quantity * frequency
    /// for each dose
    /// {| absDose: MinIncrMax; absM2: MinIncrMax; absPerKg: MinIncrMax; doserule: Informedica.ZIndex.Lib.Types.DoseRule; frequency: ValueUnit; groupBy: {| isOne: bool; name: string; time: string |}; indication: string; normDose: MinIncrMax; normM2: MinIncrMax; normPerKg: MinIncrMax; routes: list<string> |}
    let mapDoses (n: string) qty unit (gstdsr: ZIndexTypes.DoseRule) =

        let fr = mapFreq gstdsr.Freq

        let setMin =
            Optic.set MinIncrMax.Optics.inclMinLens

        let setMax =
            Optic.set MinIncrMax.Optics.exclMaxLens

        // ToDo remove n and mapping
        let toVu _ _ v =

            unit
            |> ValueUnit.fromDecimal (v * qty)
            |> fun vu ->
                let x =
                    fr
                    |> ValueUnit.get
                    |> fst
                    |> ValueUnit.create Units.Count.times

                vu * x |> Some

        let minmax n mapping (mm: DR.MinMax) =
            MinIncrMax.empty
            |> setMin (mm.Min |> Option.bind (toVu n mapping))
            |> setMax (mm.Max |> Option.bind (toVu n mapping))

        {|
            groupBy =
                {|
                    name = n
                    time = gstdsr.Freq.Time
                    isOne = gstdsr.Freq.Frequency = 1m
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
            Dosage =
                Dosage.empty
                |> Dosage.Optics.setName n
                |> Dosage.Optics.setRules (
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


    // {| doseRange: DoseRange; doserules: list<Informedica.ZIndex.Lib.Types.DoseRule>; frequencies: list<ValueUnit>; inds: list<string>; routes: list<string> |}
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
            MinIncrMax.empty |> calcWeightMinMax dsg.doserules

        let b =
            MinIncrMax.empty |> calcBSAMinMax dsg.doserules

        // if weight or bsa is known the adjusted or unadjusted doses can be calculated
        let calcNoneAndAdjusted (c: MinMax) (un: MinMax) (adj: MinMax) =
            // remove the adjust unit by making it a count
            let c =
                c
                |> MinIncrMax.withUnit Units.Count.times

            let calc op x1 x2 y =
                match y with
                | Some _ -> y
                | None ->
                    match x1, x2 with
                    | Some x1_, Some x2_ ->
                        // printfn "calculating %A %A = %A" x1_ x2_ (x1_ |> op <| x2_)
                        (x1_ |> op <| x2_) |> Some
                    | _ -> y

            // Norm.min = PerKg.min * Wght.min
            // Norm.max = PerKg.max * Wght.max
            { un with
                Min = un.Min |> calc (*) adj.Min c.Min
                Max = un.Max |> calc (*) adj.Max c.Max
            },
            // PerKg.min = Norm.min / Wght.max
            // PerKg.max = norm.max / Wght.min
            { adj with
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
        match mm.Min, mm.Min with
        | Some m, None
        | None, Some m ->
            [
                mm |> MinIncrMax.setMin (Some m)
                mm_ |> MinIncrMax.setMin (Some m)
            ]
        | _ -> [ mm; mm_ ]
        |> MinIncrMax.foldMaximize


    let foldDosages
        (ds: {| absDose: MinIncrMax
                absM2: MinIncrMax
                absPerKg: MinIncrMax
                doserule: Informedica.ZIndex.Lib.Types.DoseRule
                frequency: ValueUnit
                groupBy: {| isOne: bool
                            name: string
                            time: string |}
                indication: string
                normDose: MinIncrMax
                normM2: MinIncrMax
                normPerKg: MinIncrMax
                routes: list<string> |} seq)
        =

        ds
        |> Seq.fold
            (fun acc d ->
                let _, inds, frs, gstdsrs, norm_, abs_, normKg_, absKg_, normM2_, absM2_ =
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
                            |> ValueUnit.toReadableDutchStringWithPrec 1

                        let s2 =
                            frs
                            |> List.map (ValueUnit.toReadableDutchStringWithPrec 1)
                            |> String.concat ", "

                        failwith
                        <| $"cannot add frequency %s{s1} to list with units %s{s2}"


                    if frs |> List.exists ((=) d.frequency) then
                        frs
                    else
                        d.frequency :: frs

                let inds =
                    if inds |> List.exists ((=) d.indication) then
                        inds
                    else
                        d.indication :: inds

                let gstdsrs = d.doserule :: gstdsrs

                let norm = foldMaximize d.normDose norm_
                let abs = foldMaximize d.absDose abs_

                let normKg =
                    foldMaximize d.normPerKg normKg_

                let absKg = foldMaximize d.absPerKg absKg_
                let normM2 = foldMaximize d.normM2 normM2_
                let absM2 = foldMaximize d.absM2 absM2_

                d.routes, inds, frs, gstdsrs, norm, abs, normKg, absKg, normM2, absM2
            )
            ([],
             [],
             [],
             [],
             MinIncrMax.empty,
             MinIncrMax.empty,
             MinIncrMax.empty,
             MinIncrMax.empty,
             MinIncrMax.empty,
             MinIncrMax.empty)
        |> fun (rts, inds, frs, gstdsrs, norm, abs, normKg, absKg, normM2, absM2) ->
            {|
                routes = rts
                indications = inds
                frequencies = frs
                normDose = norm
                absDose = abs
                normKg = normKg
                absKg = absKg
                normM2 = normM2
                absM2 = absM2
                doserules = gstdsrs
            |}



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
        |> Seq.groupBy (fun r -> r.groupBy) // group by substance name frequency time and whether frequency = 1
        |> Seq.map (fun (k, mappedDoses) ->
            mappedDoses
            |> foldDosages
            |> getDoseRange
            |> getDosage cfg k.name
        )


    let getPatients (cfg: CreateConfig) (drs: ZIndexTypes.DoseRule seq) =
        let map = mapMinMax<PatientCategory>

        let ageInMo = Option.map ValueUnit.ageInMo

        let wghtKg = Option.map ValueUnit.weightInKg

        let mapAge =
            map (ageInMo >> PatientCategory.Optics.setInclMinAge) (ageInMo >> PatientCategory.Optics.setExclMaxAge)

        let mapWght =
            map (wghtKg >> PatientCategory.Optics.setInclMinWeight) (wghtKg >> PatientCategory.Optics.setInclMaxWeight)

        let mapGender s =
            match s with
            | _ when s = "man" -> Male
            | _ when s = "vrouw" -> Female
            | _ -> Undetermined
            |> PatientCategory.Optics.setGender //(Optic.set Patient.Gender_)

        drs
        |> Seq.map (fun dr ->
            (dr.Indication,
             PatientCategory.empty
             |> mapAge dr.Age
             |> mapWght dr.Weight
             |> mapGender dr.Gender),
            dr
        )
        |> Seq.groupBy fst
        |> Seq.map (fun (k, v) ->
            {|
                patientCategory = k |> snd
                substanceDoses = v |> Seq.map snd |> getSubstanceDoses cfg
                doseRules = drs
            |}
        )


    // Get the ATC codes for a GenPresProduct
    let getATCs gpk (gpp: ZIndexTypes.GenPresProduct) =
        gpp.GenericProducts
        |> Array.filter (fun gp ->
            match gpk with
            | None -> true
            | Some id -> gp.Id = id
        )
        |> Array.map (fun gp -> gp.ATC)
        |> Array.distinct


    // Get the list of routes for a GenPresProduct
    let getRoutes (gpp: ZIndexTypes.GenPresProduct) =
        gpp.GenericProducts
        |> Array.collect (fun gp -> gp.Route)
        |> Array.distinct


    // Get the list of ATC groups for a GenPresProduct
    let getATCGroups gpk (gpp: ZIndexTypes.GenPresProduct) =

        ATC.get ()
        |> Array.filter (fun g ->
            gpp
            |> getATCs gpk
            |> Array.exists (fun a -> a |> String.equalsCapInsens g.ATC5)
            && g.Shape = gpp.Shape
        )
        |> Array.distinct


    // Get the doserules for a genpresproduct
    // ToDo Temp hack ignore route and shape
    let getDoseRules all (gpp: ZIndexTypes.GenPresProduct) =
        gpp.Routes
        |> Seq.collect (fun r ->
            RF.createFilter None None None None gpp.Name gpp.Shape r
            |> RF.find all
            |> Seq.map (fun dr -> dr.Indication, (r, dr))
        )
        |> Seq.groupBy fst


    let getTradeNames (gpp: ZIndexTypes.GenPresProduct) =
        gpp.GenericProducts
        |> Seq.collect (fun gp -> gp.PrescriptionProducts)
        |> Seq.collect (fun pp -> pp.TradeProducts)
        |> Seq.map (fun tp ->
            match tp.Name |> String.split " " with
            | h :: _ -> h |> String.trim
            | _ -> ""
        )
        |> Seq.filter (fun n -> n |> String.isNullOrWhiteSpace |> not)
        |> Seq.toList


    let mergeDosages d ds =

        let merge d1 d2 =
            //printfn "merging d1: %s" (d1 |> Dosage.toString false)
            //printfn "merging d2: %s" (d2 |> Dosage.toString false)

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
                if d1 |> (Optic.get Dosage.TotalDosage_) = (d2 |> Optic.get Dosage.TotalDosage_)
                   || d1 |> (Optic.get Dosage.TotalDosage_) |> fst = DoseRange.empty
                   || d2 |> (Optic.get Dosage.TotalDosage_) |> fst = DoseRange.empty then
                    d1
                    |> Dosage.Optics.getFrequencyValues
                    |> List.append (d2 |> Dosage.Optics.getFrequencyValues)
                    |> (fun vs -> d |> Dosage.Optics.setFrequencyValues vs)
                    // merge Total dose
                    |> (fun d ->
                        if d.TotalDosage |> fst = DoseRange.empty then
                            if d1.TotalDosage |> fst = DoseRange.empty then
                                d2.TotalDosage
                            else
                                d1.TotalDosage
                            |> (fun x -> d |> (Optic.set Dosage.TotalDosage_ x))
                        else
                            d
                    )

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
            )
        //|> (fun d ->
        //    printfn "dosage is now: %s" (d |> Dosage.toString false)
        //    d
        //)

        match ds |> Seq.toList with
        | [ d1 ] -> seq { merge d1 d }
        | _ -> ds |> Seq.append (seq { yield d })


    // add indications, route, shape, patient and dosages
    let addIndicationsRoutesShapePatientDosages
        dr
        (inds: seq<{| indications: list<string>
                      routes: seq<{| route: string
                                     shapeAndProducts: seq<{| genericProducts: seq<int * string>
                                                              patients: seq<PatientCategory * seq<Dosage>>
                                                              shape: string
                                                              tradeProducts: seq<int * string> |}> |}> |}>)
        =

        inds
        |> Seq.fold
            (fun acc ind ->
                let dr =
                    acc |> addIndications ind.indications

                ind.routes
                |> Seq.fold
                    (fun acc rt ->
                        let dr =
                            acc |> Optics.addRoute ind.indications rt.route

                        rt.shapeAndProducts
                        |> Seq.fold
                            (fun acc shp ->
                                let createGP =
                                    ShapeDosage.GenericProduct.create

                                let createTP =
                                    ShapeDosage.TradeProduct.create

                                let dr =
                                    acc
                                    |> Optics.addShape ind.indications rt.route [ shp.shape ]
                                    |> Optics.setGenericProducts
                                        ind.indications
                                        rt.route
                                        [ shp.shape ]
                                        (shp.genericProducts
                                         |> Seq.toList
                                         |> List.map (fun (id, nm) -> createGP id nm)
                                         |> List.sortBy (fun gp -> gp.Label))
                                    |> Optics.setTradeProducts
                                        ind.indications
                                        rt.route
                                        [ shp.shape ]
                                        (shp.tradeProducts
                                         |> Seq.toList
                                         |> List.map (fun (id, nm) -> createTP id nm)
                                         |> List.sortBy (fun hp -> hp.Label))

                                shp.patients
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
                                                                  d |> Dosage.Optics.getFrequencyTimeUnit = (sd
                                                                                                             |> Dosage.Optics.getFrequencyTimeUnit)
                                                                  || sd |> Dosage.Optics.getFrequencyValues = []
                                                              )
                                                            with
                                                        | [] -> acc |> Seq.append (seq { yield sd })
                                                        | _ -> acc |> mergeDosages sd

                                                )
                                                Seq.empty

                                        acc
                                        |> Optics.addPatient ind.indications rt.route [ shp.shape ] pat
                                        |> Optics.setSubstanceDosages
                                            ind.indications
                                            rt.route
                                            [ shp.shape ]
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


    // seq<{| indications: list<string>; routes: seq<{| route: string; shapeAndProducts: seq<{| genericProducts: seq<int * string>; patients: seq<PatientCategory * seq<Dosage>>; shape: string; tradeProducts: seq<int * string> |}> |}> |}>
    let groupGenPresProducts rte age wght bsa gpk cfg gpps =
        gpps
        |> Seq.collect (fun (gpp: ZIndexTypes.GenPresProduct) ->
            //printfn $"{gpp.Name}: routes{gpp.Routes}"
            gpp.Routes
            |> Seq.filter (fun r ->
                rte |> String.isNullOrWhiteSpace
                || r |> String.equalsCapInsens rte
            )
            |> Seq.collect (fun rte ->
                RF.createFilter age wght bsa gpk gpp.Name gpp.Shape rte
                |> RF.find cfg.UseAll
                |> getPatients cfg
                |> Seq.sortBy (fun pats -> pats.patientCategory.Age.Min, pats.patientCategory.Weight.Min)
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
                        dsg.indications, (rte, (gpp.Shape, gps, tps, pats.patientCategory, dsg.Dosage))
                    )
                )
            )
        )
        |> groupByFst // group by indications
        |> Seq.map (fun (k, v) ->
            {|
                indications = k
                routes =
                    v
                    |> groupByFst // group by route
                    |> Seq.map (fun (k, v) ->
                        {|
                            route = k
                            shapeAndProducts =
                                v
                                |> Seq.map (fun (shp, gps, tps, pat, sds) -> shp, gps, tps, pat, sds)
                                |> Seq.groupBy (fun (shp, gps, tps, _, _) -> (shp, gps, tps)) // group by shape and products
                                |> Seq.sortBy (fst >> (fun (shp, _, _) -> shp))
                                |> Seq.map (fun ((shp, gps, tps), v) ->
                                    {|
                                        shape = shp
                                        genericProducts = gps
                                        tradeProducts = tps
                                        patients =
                                            v
                                            |> Seq.map (fun (_, _, _, pat, sds) -> pat, sds)
                                            |> groupByFst // group by patient
                                    |}
                                )
                        |}
                    )
            |}
        )


    let foldDoseRules rte age wght bsa gpk cfg (dr, gpps) =
        let dr =
            dr
            |> Optics.setSynonyms (gpps |> Seq.collect getTradeNames |> Seq.toList)

        gpps
        |> groupGenPresProducts rte age wght bsa gpk cfg
        |> addIndicationsRoutesShapePatientDosages dr


    let createDoseRules (cfg: CreateConfig) age wght bsa gpk gen shp rte =

        GPP.filter cfg.UseAll gen shp rte
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

            foldDoseRules rte age wght bsa gpk cfg (dr, gpps)
        )