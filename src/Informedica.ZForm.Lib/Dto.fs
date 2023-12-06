namespace Informedica.ZForm.Lib


module Dto =

    open Aether

    open System

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    module Dosage = DoseRule.Dosage
    module DoseRange = DoseRule.DoseRange

    module RF = Informedica.ZIndex.Lib.RuleFinder
    module DR = Informedica.ZIndex.Lib.DoseRule
    module GPP = Informedica.ZIndex.Lib.GenPresProduct
    module GP = Informedica.ZIndex.Lib.GenericProduct
    module FP = Informedica.ZIndex.Lib.FilePath
    module ATC = Informedica.ZIndex.Lib.ATCGroup


    let (>?) = Limit.optLT
    let (<?) = Limit.optST



    /// A Dto to work wih the ZForm library.
    [<CLIMutable>]
    type Dto =
        {
            AgeInMo : float
            WeightKg : float
            HeightCm : float
            BSAInM2 : float
            Gender : string
            BirthWeightGram : float
            GestAgeWeeks : int
            GestAgeDays : int
            GPK : int list
            ATC : string
            TherapyGroup : string
            TherapySubGroup : string
            Generic : string
            TradeProduct : string
            Shape : string
            Label : string
            Concentration : float
            ConcentrationUnit : string
            Multiple : float
            MultipleUnit : string
            Route : string
            Indication : string
            IsRate : Boolean
            RateUnit : string
            Rules : Rule []
            Text : string
        }
    and Rule =
        {
            Substance : string
            Concentration : float
            Unit : string

            Frequency : string

            NormTotalDose : float
            MinTotalDose : float
            MaxTotalDose : float
            MaxPerDose : float

            NormTotalDosePerKg : float
            MinTotalDosePerKg : float
            MaxTotalDosePerKg : float
            MaxPerDosePerKg : float

            NormTotalDosePerM2 : float
            MinTotalDosePerM2 : float
            MaxTotalDosePerM2 : float
            MaxPerDosePerM2 : float
        }

    let rule =
        {
            Substance = ""
            Concentration = 0.
            Unit = ""
            Frequency = ""
            NormTotalDose = 0.
            MinTotalDose = 0.
            MaxTotalDose = 0.
            MaxPerDose = 0.
            NormTotalDosePerKg = 0.
            MinTotalDosePerKg = 0.
            MaxTotalDosePerKg = 0.
            MaxPerDosePerKg = 0.
            NormTotalDosePerM2 = 0.
            MinTotalDosePerM2 = 0.
            MaxTotalDosePerM2 = 0.
            MaxPerDosePerM2 = 0.
        }



    /// An empty Dto.
    let dto =
        {
            AgeInMo = 0.
            WeightKg = 0.
            HeightCm = 0.
            BSAInM2 = 0.
            Gender = ""
            BirthWeightGram = 0.
            GestAgeWeeks = 0
            GestAgeDays = 0
            GPK = []
            ATC = ""
            TherapyGroup = ""
            TherapySubGroup = ""
            Generic = ""
            TradeProduct = ""
            Shape = ""
            Label = ""
            Concentration = 0.
            ConcentrationUnit = ""
            Multiple = 0.
            MultipleUnit = ""
            Route = ""
            Indication = ""
            IsRate = false
            RateUnit = ""
            Rules = [||]
            Text = ""
        }


    /// Load the Zindex data in memory.
    let loadGenForm () =
        printfn "Start loading GenPresProducts ..."
        GPP.load []
        printfn "Start loading DoseRules ..."
        DR.load []
        printfn "Start loading ATCGroups ..."
        ATC.load ()
        printfn "Finisched loading"



    /// <summary>
    /// Find the Generic Product info for the given dto.
    /// </summary>
    /// <param name="dto">The Dto</param>
    /// <returns>The Generic Product info</returns>
    let find (dto : Dto) =
        let gpps =
            let ps =
                match dto.GPK with
                | [ gpk ] -> gpk |> GPP.findByGPK
                | _ -> [||]
            if ps |> Array.length = 0 then
                GPP.filter dto.Generic dto.Shape dto.Route
            else ps
            |> Array.toList

        match gpps with
        | [ gpp ] ->
            let gpk, lbl, conc, unt, tps =
                let gp =
                    match gpp.GenericProducts
                          |> Seq.tryFind (fun p -> dto.GPK |> Seq.exists ((=) p.Id)) with
                    | Some gp -> gp |> Some
                    | None ->
                        if gpp.GenericProducts |> Seq.length = 1 then
                            gpp.GenericProducts |> Seq.head |> Some
                        else
                            printfn $"too many products ({gpp.GenericProducts |> Seq.length}) narrow the search"
                            None

                match gp with
                | Some gp ->
                    let conc, unt =
                        match gp.Substances |> Seq.tryFind (fun s -> s.SubstanceName |> String.equalsCapInsens gpp.Name) with
                        | Some s -> s.SubstanceQuantity, s.SubstanceUnit
                        | None -> 0., ""

                    let tps =
                        gp.PrescriptionProducts
                        |> Array.fold (fun acc pp ->
                            pp.TradeProducts
                            |> Array.map (fun tp -> tp.Label)
                            |> Array.toList
                            |> List.append acc
                        ) []
                        |> String.concat "||"

                    gp.Id, gp.Label, conc, unt, tps

                | None ->
                    printfn $"Could not find product %s{dto.Generic} %s{dto.Shape} %s{dto.Route} with GPK: %A{dto.GPK}"
                    0, "", 0., "", ""

            gpk, gpp.Name, gpp.Shape, lbl, conc, unt, tps
        | _ ->
            printfn $"Could not find product %s{dto.Generic} %s{dto.Shape} %s{dto.Route} with GPK: %A{dto.GPK}"
            0, "", "", "", 0., "", ""


    /// <summary>
    /// Fill the given Rule dto with the given Dosage for a given GPK.
    /// </summary>
    /// <param name="gpk">The GPK</param>
    /// <param name="d">The Dosage</param>
    /// <param name="r">The Rule dto</param>
    /// <returns>The filled Rule dto</returns>
    let fillRuleWithDosage gpk (d : Dosage)  (r : Rule) =

        let conc, unt =
            match
                gpk
                |> GPP.getSubstQtyUnit
                |> Array.tryFind (fun (n, _, _) -> n |> String.equalsCapInsens d.Name) with
            | Some (_, conc, unt) -> conc, unt
            | None -> 0., ""

        let freqsToStr (fr : Frequency) =
            fr.Frequencies
            |> List.map (fun f ->
                f
                |> ValueUnit.createSingle (ValueUnit.createCombiUnit (Units.Count.times, OpPer, fr.TimeUnit))
                |> ValueUnit.freqToValueUnitString
            )
            |> String.concat "||"

        let getValue prism d =
            d
            |> (Optic.get prism)
            |> (fun vu ->
                match vu with
                | Some vu ->
                    vu
                    |> ValueUnit.getValue
                    |> Array.head
                    |> BigRational.toFloat
                    |> Double.fixPrecision 2
                | None -> 0.
            )

        {
            r with
                Substance = d.Name
                Concentration = conc
                Unit = unt

                Frequency =
                    d.TotalDosage
                    |> snd
                    |> freqsToStr

                MinTotalDose = d |> getValue Dosage.Optics.inclMinNormTotalDosagePrism
                MaxTotalDose = d |> getValue Dosage.Optics.exclMaxNormTotalDosagePrism

                MinTotalDosePerKg = d |> getValue Dosage.Optics.inclMinNormWeightTotalDosagePrism
                MaxTotalDosePerKg = d |> getValue Dosage.Optics.exclMaxNormWeightTotalDosagePrism

                MinTotalDosePerM2 = d |> getValue Dosage.Optics.inclMinNormBSATotalDosagePrism
                MaxTotalDosePerM2 = d |> getValue Dosage.Optics.exclMaxNormBSATotalDosagePrism

                MaxPerDose   =
                    if r.MaxPerDose = 0. then
                        let d1 = d |> getValue Dosage.Optics.exclMaxNormSingleDosagePrism
                        let d2 = d |> getValue Dosage.Optics.exclMaxNormStartDosagePrism
                        if d1 = 0. then d2 else d1
                    else r.MaxPerDose

                MaxPerDosePerKg =
                    if r.MaxPerDosePerKg = 0. then
                        let d1 = d |> getValue Dosage.Optics.exclMaxNormWeightSingleDosagePrism
                        let d2 = d |> getValue Dosage.Optics.exclMaxNormWeightStartDosagePrism
                        if d1 = 0. then d2 else d1
                    else r.MaxPerDosePerKg

                MaxPerDosePerM2 =
                    if r.MaxTotalDosePerM2 = 0. then
                        let d1 = d |> getValue Dosage.Optics.exclMaxNormBSASingleDosagePrism
                        let d2 = d |> getValue Dosage.Optics.exclMaxNormBSAStartDosagePrism
                        if d1 = 0. then d2 else d1
                    else r.MaxTotalDosePerM2
        }


    /// <summary>
    /// Convert the given Dto to a Dto with the rules filled in.
    /// </summary>
    /// <param name="dto">The Dto</param>
    /// <returns>The Dto with the rules filled in</returns>
    let processDto (dto : Dto) =

        let u =
            if dto.MultipleUnit |> String.isNullOrWhiteSpace then None
            else
                dto.MultipleUnit
                |> Mapping.stringToUnit
                |> Some

        let ru =
            dto.RateUnit |> Units.fromString

        let rte =
            dto.Route
            |> Mapping.stringToRoute
            |> Mapping.routeToString

        let dto =
            if dto.BSAInM2 > 0. then dto
            else
                if dto.HeightCm > 0. && dto.WeightKg > 0. then
                    {
                        dto with
                            BSAInM2 =
                                // (w / (l  ** 2.)) |> Some
                                dto.WeightKg / (((dto.HeightCm |> float) ** 2.) |> float)
                    }
                else
                    dto

        let gpk, gen, shp, lbl, conc, unt, tps = find dto

        let prodName = $"%i{gpk}: %s{lbl} "

        let rs =
            let su =
                if dto.MultipleUnit = "" then None
                else
                    dto.MultipleUnit
                    |> Mapping.stringToUnit
                    |> Some

            let tu =
                if dto.RateUnit = "" then None
                else
                    dto.RateUnit
                    |> Mapping.stringToUnit
                    |> Some

            let cfg : CreateConfig =
                {
                    GPKs = []
                    IsRate = dto.IsRate
                    SubstanceUnit = su
                    TimeUnit = tu
                }

            GStand.createDoseRules
                cfg
                (Some dto.AgeInMo)
                (Some dto.WeightKg)
                (Some dto.BSAInM2)
                (Some gpk)
                gen
                shp
                rte

        if rs |> Seq.length <> 1 then
            printfn $"found %i{rs |> Seq.length} rules for %s{prodName}"
            { dto with
                GPK =
                    GPP.filter dto.Generic dto.Shape rte
                    |> Array.collect (fun gpp ->
                        gpp.GenericProducts
                        |> Array.map (fun gp -> gp.Id)
                    )
                    |> Array.toList
            }
        else
            let r = rs |> Seq.head

            let rules =
                let ids =
                    r.IndicationsDosages
                    |> Seq.filter (fun d ->
                        d.Indications
                        |> List.exists (fun s ->
                            if dto.Indication |> String.isNullOrWhiteSpace then
                                d.Indications |> Seq.length = 1 ||
                                s = "Algemeen"
                            else
                                s = dto.Indication
                        )
                    )

                if ids |> Seq.length <> 1 then
                    printfn $"wrong ids count: %i{ids |> Seq.length} for %s{prodName}"
                    []
                else
                    let id = ids |> Seq.head
                    let rds =
                        id.RouteDosages

                    if rds |> Seq.length <> 1 then
                        let rts =
                            rds
                            |> List.map(fun rd -> rd.Route)
                            |> String.concat ", "
                        printfn $"wrong rds count: %i{rds |> Seq.length} for %s{prodName} with routes: %s{rts} using route: %s{rte}"
                        []
                    else
                        let rd = rds |> Seq.head
                        if rd.ShapeDosages |> Seq.length <> 1 then
                            printfn $"wrong sds count: %i{rd.ShapeDosages |> Seq.length} for %s{prodName}"
                            []
                        else
                            let sd = rd.ShapeDosages |> Seq.head

                            sd.PatientDosages
                            |> List.collect (fun pd ->
                                pd.SubstanceDosages
                            )
                            |> List.groupBy (fun sd ->
                                sd
                                |> Dosage.Optics.getFrequencyTimeUnit
                            )
                            |> List.collect (fun (_, sds) ->

                                sds
                                |> List.fold (fun (acc : Rule list) d ->
                                    match acc |> List.tryFind (fun d_ -> d_.Substance = d.Name) with
                                    | Some r ->
                                        let rest =
                                            acc
                                            |> List.filter (fun r_ -> r_.Substance <> d.Name)
                                        [ r
                                        |> fillRuleWithDosage gpk d ]
                                        |> List.append rest

                                    | None ->
                                        [ rule
                                        |> fillRuleWithDosage gpk d ]
                                        |> List.append acc
                                ) []
                            )
                |> (fun rules ->
                    match rules |> List.tryFind (fun r -> r.Frequency = "") with
                    | None -> rules
                    | Some noFreq ->
                        if rules |> Seq.length = 1 then rules
                        else
                            rules
                            |> List.filter (fun r -> r.Frequency <> "")
                            |> List.map (fun r ->
                                {
                                    r with
                                        MaxPerDose = noFreq.MaxPerDose
                                        MaxPerDosePerKg = noFreq.MaxPerDosePerKg
                                        MaxPerDosePerM2 = noFreq.MaxPerDosePerM2
                                }
                            )
                )

            {
                dto with
                    ATC = r.ATC
                    TherapyGroup = r.ATCTherapyGroup
                    TherapySubGroup = r.ATCTherapySubGroup
                    GPK =
                        if dto.GPK |> Seq.exists ((=) gpk) then [ gpk ]
                        else dto.GPK
                    Generic = gen
                    TradeProduct = tps
                    Shape = shp
                    Label = lbl
                    Concentration = conc
                    ConcentrationUnit =
                        unt
                        //TODO: rewrite to new online mapping
                        //|> Mapping.mapUnit Mapping.ZIndex Mapping.GenPres
                    Multiple =
                        if dto.Multiple = 0. then conc
                        else dto.Multiple
                    MultipleUnit =
                        if dto.MultipleUnit = "" then
                            unt
                            //TODO: rewrite to new online mapping
                            //|> Mapping.mapUnit Mapping.ZIndex Mapping.GenPres
                        else dto.MultipleUnit
                    Rules = rules |> List.toArray
                    Text =
                        r
                        |> (fun dr -> match u  with | Some u -> dr |> DoseRule.convertSubstanceUnitTo gen u | None -> dr)
                        |> (fun dr -> match ru with | Some u -> dr |> DoseRule.convertRateUnitTo gen u | None -> dr)
                        |> DoseRule.toString false
                        |> Markdown.toHtml
            }
