

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"



#load "../../../scripts/Expecto.fsx"


#load "load.fsx"




module Tests =

    open Expecto
    open Expecto.Flip


    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.ZForm.Lib


    let vuFromStr v u = 
        ValueUnit.valueUnitFromZIndexString v u
        |> Some


    module MinIncrMaxTests =

        open Informedica.GenUnits.Lib
        open Informedica.GenCore.Lib
        open Informedica.GenCore.Lib.Ranges



        let fromDecimal (v: decimal) u =
            v
            |> BigRational.fromDecimal
            |> ValueUnit.createSingle u


        let ageInMo =  (fun n -> fromDecimal n Units.Time.month)


        let ageInYr =  (fun n -> fromDecimal n Units.Time.year)



        let ageInclOneMo, ageExclOneYr =
            1m |> ageInMo |> Inclusive,
            1m |> ageInYr |> Exclusive


        let ageRange =
            MinIncrMax.empty
            |> MinIncrMax.Optics.setMin ageInclOneMo
            |> MinIncrMax.Optics.setMax ageExclOneYr


        let tests = testList "MinIncrMax" [
            test "ageToString" {
                ageRange
                |> MinIncrMax.ageToString
                |> Expect.equal "should equal" "van 1 maand - tot 1 jaar"
            }
        ]


    module MappingTests =

        open Informedica.GenUnits.Lib


        let tests = testList "Mapping" [

            test "all units that can be mapped have a mapping" {
                // Test all unit mappings
                Informedica.ZIndex.Lib.Names.getShapeUnits ()
                |> Array.append (Informedica.ZIndex.Lib.Names.getGenericUnits ())
                |> Array.distinct
                |> Array.map (fun s -> s, s |> Mapping.stringToUnit (Mapping.getUnitMapping ()))
                |> Array.filter (fst >> (<>) "NIET VAN TOEPASSING")
                |> Array.filter (fst >> (<>) "n.v.t.")
                |> Array.forall (snd >> (<>) NoUnit)
                |> Expect.isTrue "should all have a unit"
            }

            test "all routes can be mapped" {
                let gpp =
                    Informedica.ZIndex.Lib.GenPresProduct.get true
                    |> Array.collect (fun gpp -> gpp.Routes)
                    |> Array.distinct
                    |> Array.sort
                    |> Array.map (fun s ->
                        s,
                        s |> Informedica.ZIndex.Lib.Route.fromString (Informedica.ZIndex.Lib.Route.routeMapping ())
                    )
                    |> Array.filter (snd >> ((=) Route.NoRoute))

                let dr =
                    Informedica.ZIndex.Lib.DoseRule.get ()
                    |> Array.collect (fun dr -> dr.Routes)
                    |> Array.distinct
                    |> Array.sort
                    |> Array.map (fun s ->
                        s,
                        s |> Informedica.ZIndex.Lib.Route.fromString (Informedica.ZIndex.Lib.Route.routeMapping ())
                    )
                    |> Array.filter (snd >> ((=) Route.NoRoute))

                ((gpp |> Array.isEmpty) && (dr |> Array.isEmpty))
                |> Expect.isTrue "should be true"
            }

            test "all frequencies can be mapped" {
                Informedica.ZIndex.Lib.DoseRule.get ()
                |> Array.map (fun dr -> dr.Freq.Frequency, dr.Freq.Time |> String.replace "per " "")
                |> Array.distinct
                |> Array.map (fun (v, s) -> v, s, Mapping.mapFrequency v s)
                |> Array.filter (fun (_, _, u) -> u |> Option.isNone)
                |> Expect.equal "should be true" [| 99.99m, "dag", None |]
            }
        ]


    module PatientTests =

        open PatientCategory

        let processDto f dto =
            dto |> f
            dto

        let setMinAge =
            fun (dto : Dto.Dto) ->
                dto.Age.HasMin <- true
                dto.Age.Min.Value <- [|1m|]
                dto.Age.Min.Unit <- "maand"
                dto.Age.Min.Group <- "Time"
                dto.Age.Min.Language <- "dutch"
                dto.Age.Min.Short <- true
                dto.Age.MinIncl <- true


        let setWrongUnit =
            fun (dto : Dto.Dto) ->
                // group defaults to general when no unit can be found in group
                // ToDo: need to fix this behaviour
                dto.Age.HasMin <- true
                dto.Age.Min.Value <- [|1m|]
                dto.Age.Min.Unit <- "m"
                dto.Age.Min.Group <- "Time"
                dto.Age.Min.Language <- "dutch"
                dto.Age.Min.Short <- true
                dto.Age.MinIncl <- true

        let setWrongGroup =
            fun (dto : Dto.Dto) ->
                // need to check for the correct units
                // ToDo!!
                dto.Age.HasMin <- true
                dto.Age.Min.Value <- [|1m|]
                dto.Age.Min.Unit <- "g"
                dto.Age.Min.Group <- "Mass"
                dto.Age.Min.Language <- "dutch"
                dto.Age.Min.Short <- true
                dto.Age.MinIncl <- true



        let tests = testList "Patient" [

            test "an 'empty patient'" {
                Dto.dto ()
                |> Dto.fromDto
                |> function
                | None -> "false"
                | Some p ->
                    p |> toString
                |> Expect.equal "should be an empty string" ""
            }

            test "a patient with a min age" {
                Dto.dto ()
                |> processDto setMinAge
                |> Dto.fromDto
                |> function
                | None -> "false"
                | Some p ->
                    p |> toString
                |> Expect.equal "should be 'Leeftijd: van 1 mnd'" "Leeftijd: van 1 maand"
            }

            test "a patient with a min age wrong unit" {
                Dto.dto ()
                |> processDto setWrongUnit
                |> Dto.fromDto
                |> function
                | None -> "false"
                | Some p ->
                    p |> toString
                |> ignore // TODO: not implemented yet
                //|> Expect.equal "should be an empty string" ""
            }

            test "a patient with a min age wrong group" {
                Dto.dto ()
                |> processDto setWrongGroup
                |> Dto.fromDto
                |> function
                | None -> "false"
                | Some p ->
                    p |> toString
                |> ignore // TODO: not implemented yet
                //|> Expect.equal "should be an empty string" ""
            }

        ]



    module DoseRangeTests =

        open Aether
        open Informedica.GenUnits.Lib

        module Dto = DoseRule.DoseRange.Dto

        module DoseRange = DoseRule.DoseRange

        let setMinNormDose = Optic.set DoseRange.Optics.inclMinNormLens
        let setMaxNormDose = Optic.set DoseRange.Optics.inclMaxNormLens

        let setMinNormPerKgDose vu dr = 
            dr
            |> Optic.set DoseRange.Optics.inclMinNormWeightLens vu
            |> Optic.set DoseRange.Optics.normWeightUnitLens Units.Weight.kiloGram 

        let setMaxNormPerKgDose vu dr  =
            dr
            |> Optic.set DoseRange.Optics.inclMaxNormWeightLens vu
            |> Optic.set DoseRange.Optics.normWeightUnitLens Units.Weight.kiloGram

        let setMinAbsDose vu dr = 
            dr
            |> Optic.set DoseRange.Optics.inclMinAbsLens vu
            |> Optic.set DoseRange.Optics.absBSAUnitLens Units.BSA.m2

        let setMaxAbsDose vu dr = 
            dr
            |> Optic.set DoseRange.Optics.inclMaxAbsLens vu
            |> Optic.set DoseRange.Optics.absBSAUnitLens Units.BSA.m2

        let drToStr = DoseRange.toString None


        let processDto f dto = dto |> f; dto


        let addValues  =
            fun (dto : Dto.Dto) ->
                dto.Norm.HasMin <- true
                dto.Norm.Min.Value <- [|1m|]
                dto.Norm.Min.Unit <- "mg"
                dto.Norm.Min.Group <- "mass"

                dto.Norm.HasMax <- true
                dto.Norm.Max.Value <- [|10m|]
                dto.Norm.Max.Unit <- "mg"
                dto.Norm.Max.Group <- "mass"

                dto.NormWeight.HasMin <- true
                dto.NormWeight.Min.Value <- [|0.01m|]
                dto.NormWeight.Min.Unit <- "mg"
                dto.NormWeight.Min.Group <- "mass"
                dto.NormWeightUnit <- "kg"

                dto.NormWeight.HasMax <- true
                dto.NormWeight.Max.Value <- [|1m|]
                dto.NormWeight.Max.Unit <- "mg"
                dto.NormWeight.Max.Group <- "mass"
                dto.NormWeightUnit <- "kg"



        let tests = testList "DoseRange" [

            test "there and back again empty doserange dto" {
                let expct =
                    Dto.dto ()
                    |> Dto.fromDto

                expct
                |> Dto.toDto
                |> Dto.fromDto
                |> Expect.equal "should be equal" expct
            }

            test "there and back again with filled doserange dto" {
                let expct =
                    Dto.dto ()
                    |> processDto addValues
                    |> Dto.fromDto

                expct
                |> Dto.toDto
                |> Dto.fromDto
                |> Expect.equal "should be equal" expct
            }

            test "can create a dose range" {
                DoseRange.empty
                |> setMaxNormDose (vuFromStr 10m "milligram")
                |> setMaxAbsDose (vuFromStr 100m "milligram")
                |> drToStr
                |> Expect.equal "should be a range" "tot 10 mg maximaal tot 100 mg"
            }

            test "can create a dose range with a rate" {
                DoseRange.empty
                |> setMinNormDose (vuFromStr 10m "milligram")
                |> setMaxNormDose (vuFromStr 100m "milligram")
                |> DoseRange.toString (Some ValueUnit.Units.hour)
                |> Expect.equal "should be a rate" "van 10 mg/uur - tot 100 mg/uur"
            }

            test "can create a dose range with a rate per kg" {
                DoseRange.empty
                |> setMinNormPerKgDose (vuFromStr 0.001m "milligram")
                |> setMaxNormPerKgDose (vuFromStr 1.m "milligram")
                |> DoseRange.convertTo (ValueUnit.Units.mcg)
                |> DoseRange.toString (Some ValueUnit.Units.hour)
                |> Expect.equal "should be a rate" "van 1 microg/kg/uur - tot 1000 microg/kg/uur"
            }


            test "can covert a unit" {
                DoseRange.empty
                |> setMaxNormDose (vuFromStr 1.m "milligram")
                |> setMinNormDose (vuFromStr 0.001m "milligram")
                |> DoseRange.convertTo (ValueUnit.Units.mcg)
                |> drToStr
                |> Expect.equal "should be a rate with a different unit" "van 1 microg - tot 1000 microg"
            }

        ]



    module DoseRuleTests =

        module Dto = DoseRule.Dto


        let (|>!) x f =
            printfn $"%A{x}"
            x |> f



        let processDto f dto = dto |> f; dto


        let print () =

            let dto = Dto.dto ()


            dto
            |>! Dto.fromDto
            |>! ignore

        let tests = testList "DoseRule" [
            test "there and back again with an empty doserule" {
                let doseRule =
                    Dto.dto ()
                    |> Dto.fromDto

                doseRule
                |> Dto.toDto
                |> Dto.fromDto
                |> Expect.equal "should be equal" doseRule

            }
        ]


    let tests =
        [
            MinIncrMaxTests.tests
            MappingTests.tests
            PatientTests.tests
            DoseRangeTests.tests
            DoseRuleTests.tests
        ]
//        |> List.skip 1
        |> testList "ZForm"


open Expecto


Tests.tests
|> Expecto.run




module Temp =


    open Aether

    open Informedica.GenUnits.Lib
    open Informedica.ZForm.Lib



    let vuFromStr v u = 
        ValueUnit.valueUnitFromZIndexString v u
        |> Some


    module DosageTests =

        module Dosage = DoseRule.Dosage

        let setNormMinStartDose = Optic.set Dosage.Optics.inclMinNormStartDosagePrism
        let setAbsMaxStartDose = Optic.set Dosage.Optics.inclMaxAbsStartDosagePrism

        let setNormMinSingleDose = Optic.set Dosage.Optics.inclMinNormSingleDosagePrism
        let setAbsMaxSingleDose = Optic.set Dosage.Optics.inclMaxAbsSingleDosagePrism

        let setNormMaxSingleDose = Optic.set Dosage.Optics.inclMaxNormSingleDosagePrism

        let setNormMinRateDose = Optic.set Dosage.Optics.inclMinNormRateDosagePrism
        let setNormMaxRateDose = Optic.set Dosage.Optics.inclMaxNormRateDosagePrism
        let setRateUnit = Optic.set Dosage.Optics.rateUnitRateDosagePrism

        let toString () =
            Dosage.empty
            |> setNormMinStartDose (vuFromStr 10.m "milligram")
            |> setAbsMaxStartDose (vuFromStr 1.m "gram")
            |> setNormMinSingleDose (vuFromStr 10.m "milligram")
            |> setAbsMaxSingleDose (vuFromStr 1.m "gram")
            |> Dosage.toString true


        let convert () =
            Dosage.empty
            |> setNormMinSingleDose (vuFromStr 0.01m "milligram")
            |> setNormMaxSingleDose (vuFromStr 1.m "milligram")
            |> Dosage.convertSubstanceUnitTo (ValueUnit.Units.mcg)
            |> Dosage.toString false


        let convertRate () =
            Dosage.empty
            |> setNormMinRateDose (vuFromStr 0.01m "milligram")
            |> setNormMaxRateDose (vuFromStr 1.m "milligram")
            |> setRateUnit (ValueUnit.Units.hour)
            |> Dosage.convertSubstanceUnitTo (ValueUnit.Units.mcg)
            |> Dosage.convertRateUnitTo (ValueUnit.Units.min)
            |> Dosage.toString false



    module PatientTests =


        module Patient = PatientCategory.Optics

        let toString () =
            PatientCategory.empty
            |> Patient.setInclMinGestAge (28.m  |> ValueUnit.ageInWk |> Some)
            |> Patient.setExclMaxGestAge (33.m  |> ValueUnit.ageInWk |> Some)
            |> Patient.setExclMinAge (1.m |> ValueUnit.ageInMo |> Some)
            |> Patient.setInclMaxAge (120.m |> ValueUnit.ageInWk |> Some)
            |> Patient.setInclMinWeight (0.15m  |> ValueUnit.weightInKg |> Some)
            |> Patient.setInclMaxWeight (4.00m  |> ValueUnit.weightInKg |> Some)
            |> Patient.setInclMinBSA (0.15m  |> ValueUnit.bsaInM2 |> Some)
            |> Patient.setInclMaxBSA (1.00m  |> ValueUnit.bsaInM2 |> Some)
            |> (fun p -> p |> (Optic.set PatientCategory.Gender_) Gender.Male)
            |> PatientCategory.toString




    module GStandTests =

        open GStand
        open Informedica.Utils.Lib.BCL

        module Dosage = DoseRule.Dosage

        module RF = Informedica.ZIndex.Lib.RuleFinder
        module DR = Informedica.ZIndex.Lib.DoseRule
        module GPP = Informedica.ZIndex.Lib.GenPresProduct

        let cfg = { UseAll = true ; IsRate = false ; SubstanceUnit = None ; TimeUnit = None }

        let cfgmcg = { cfg with SubstanceUnit = (Some ValueUnit.Units.mcg) }

        let createWithCfg cfg = GStand.createDoseRules cfg None None None None

        let createDoseRules = createWithCfg cfg

        let createCont su tu =
            let cfg = { cfg with IsRate = true ; SubstanceUnit = su ; TimeUnit = tu }
            GStand.createDoseRules cfg None None None None

        let mdText = """
    ## _Stofnaam_: {generic}
    Synoniemen: {synonym}

    ---

    ### _ATC code_: {atc}

    ### _Therapeutische groep_: {thergroup}

    ### _Therapeutische subgroep_: {thersub}

    ### _Generiek groep_: {gengroup}

    ### _Generiek subgroep_: {gensub}

    """

        let mdIndicationText = """

    ---

    ### _Indicatie_: {indication}
    """


        let mdRouteText = """
    * _Route_: {route}
    """

        let mdShapeText = """
    * _Vorm_: {shape}
    * _Producten_:
    * {products}
    """

        let mdPatientText = """
        * _Patient_: __{patient}__
    """

        let mdDosageText = """
        {dosage}

    """


        let mdConfig =
            {
                DoseRule.mdConfig with
                    MainText = mdText
                    IndicationText = mdIndicationText
                    RouteText = mdRouteText
                    ShapeText = mdShapeText
                    PatientText = mdPatientText
                    DosageText = mdDosageText
            }


        let toStr = DoseRule.toStringWithConfig mdConfig false


        let printDoseRules rs =
            rs
            |> Seq.iter (fun dr ->
                dr
                |> toStr
                |> printfn "%s" //Markdown.toBrowser
            )


        let mapFrequency () =
            DR.get ()
            |> Seq.map (fun dr -> dr.Freq)
            |> Seq.distinct
            |> Seq.sortBy (fun fr -> fr.Time, fr.Frequency)
            |> Seq.map (fun fr -> fr, fr |> GStand.mapFreq)
            |> Seq.iter (fun (fr, vu) ->
                printfn "%A %s = %s" fr.Frequency fr.Time (vu |> ValueUnit.toReadableDutchStringWithPrec 0)
            )

        let tests () =
            // Doserules for cotrimoxazol
            createDoseRules "trimethoprim/sulfamethoxazol" "" "intraveneus"
            |> printDoseRules

            // Doserules for clonidin orally
            createDoseRules "clonidine" "" "oraal"
            |> printDoseRules

            // Doserules for newborn with 12?
            GStand.createDoseRules cfg (Some 0.m) (Some 12.m) None None "paracetamol" "" "oraal"
            |> printDoseRules

            // Doserules for 100 mo and gpk = 167541
            GStand.createDoseRules cfg (Some 100.m) None (None) (Some 167541) "" "" ""
            |> printDoseRules
            |> (printfn "%A")

            // Doserules for gentamicin
            GStand.createDoseRules cfg (Some 0.m) (Some 1.5m) None None "gentamicine" "" "intraveneus"
            |> printDoseRules

            // Doserules for fentanyl
            createWithCfg cfgmcg "fentanyl" "" "intraveneus"
            |> printDoseRules

            // Doserules for dopamin
            createCont (Some ValueUnit.Units.mcg) (Some ValueUnit.Units.min) "dopamine" "" "intraveneus"
            |> printDoseRules

            // Doserules for digoxin
            createWithCfg cfgmcg "digoxine" "" ""
            |> printDoseRules

            // Doserules for paracetamol
            RF.createFilter None None None None "paracetamol" "" ""
            |> RF.find true
            |> getSubstanceDoses cfg
            |> Seq.iter (fun r ->
                printfn "Indication %s" (r.indications |> String.concat ", ")
                printfn "%s" (r.Dosage |> Dosage.toString true)
            )

            // Doserules for gentamicin
            RF.createFilter None None None None "gentamicine" "" ""
            |> RF.find true
            |> getPatients cfg
            |> Seq.iter (fun r ->
                printfn "%s" (r.patientCategory |> PatientCategory.toString)
                r.substanceDoses
                |> Seq.iter (fun sd ->
                printfn "Indication %s" (sd.indications |> String.concat ", ")
                printfn "%s" (sd.Dosage |> Dosage.toString true)
                )
            )

            // Doserules with frequency per hour
            DR.get ()
            |> Seq.filter (fun dr ->
                dr.Freq.Frequency = 1.m &&
                dr.Freq.Time = "per uur" &&
                dr.Routes = [|"intraveneus"|]
            )
            |> Seq.collect (fun dr -> dr.GenericProduct |> Seq.map (fun gp -> gp.Name))
            |> Seq.distinct
            |> Seq.sort
            |> Seq.iter (printfn "%s")

            // Dose rules for salbutamol
            DR.get ()
            |> Seq.filter (fun dr ->
                dr.GenericProduct
                |> Seq.map (fun gp -> gp.Name)
                |> Seq.exists (String.startsWithCapsInsens "salbutamol")
            )
            //|> Seq.collect (fun dr ->
            //    dr.GenericProduct
            //    |> Seq.map (fun gp -> gp.Name, dr.Routes)
            //)
            |> Seq.map (DR.toString ",")
            |> Seq.distinct
            |> Seq.iter (printfn "%A")

            // Doserules with fentanyl once
            DR.get ()
            |> Seq.filter (fun dr ->
                dr.GenericProduct
                |> Seq.map (fun gp -> gp.Name)
                |> Seq.exists (String.startsWithCapsInsens "fentanyl") &&
                dr.Freq.Time |> String.startsWithCapsInsens "eenmalig"
            )
            //|> Seq.collect (fun dr ->
            //    dr.GenericProduct
            //    |> Seq.map (fun gp -> gp.Name, dr.Routes)
            //)
            |> Seq.map (DR.toString ",")
            |> Seq.distinct
            |> Seq.iter (printfn "%A")

            // Doserules for fentanyl
            DR.get ()
            |> Seq.filter (fun dr ->
                dr.GenericProduct
                |> Seq.map (fun gp -> gp.Name)
                |> Seq.exists (String.startsWithCapsInsens "fentanyl")
            )
            //|> Seq.collect (fun dr ->
            //    dr.GenericProduct
            //    |> Seq.map (fun gp -> gp.Name, dr.Routes)
            //)
            |> Seq.map (fun dr -> dr.Freq.Time)
            |> Seq.distinct
            |> Seq.iter (printfn "%A")

            // GenPresProducts = paracetamol
            GPP.get false
            |> Seq.filter (fun gpp -> gpp.Name |> String.equalsCapInsens "paracetamol")
            |> Seq.iter (fun gpp ->
                gpp
                |> (printfn "%A")
            )

            // All routes in doserules
            DR.get ()
            |> Seq.collect (fun r -> r.Routes)
            |> Seq.distinct
            |> Seq.sort
            |> Seq.iter (printfn "%s")

            // GenPresProducts with route = parenteraal
            GPP.get true
            |> Seq.filter (fun gpp ->
                gpp.Routes |> Seq.exists (fun r -> r |> String.equalsCapInsens "parenteraal")
            )
            |> Seq.distinct
            |> Seq.sort
            |> Seq.iter (GPP.toString >> printfn "%s")

            // Filter GenPresProducts with route = oraal
            GPP.filter true "" "" "oraal"
            |> Seq.length
            |> ignore

            printfn "DoseRule routes without products"
            DR.routes ()
            |> Seq.filter (fun r ->

                GPP.getRoutes ()
                |> Seq.exists (fun r' -> r = r')
                |> not
            )
            |> Seq.sort
            |> Seq.iter (printfn "|%s|")
            printfn ""
            printfn "GenPresProduct routes without doserules"
            GPP.getRoutes ()
            |> Seq.filter (fun r ->
                DR.routes ()
                |> Seq.exists (fun r' -> r = r')
                |> not
            )
            |> Seq.sort
            |> Seq.iter (printfn "|%s|")


        //GStand.createDoseRules cfg (Some 1.1m) (Some 5.m) None None "gentamicine" "" "intraveneus"
        //|> printDoseRules

