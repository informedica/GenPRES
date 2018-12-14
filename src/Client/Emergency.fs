module Emergency

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Elmish
open Fulma


module TreatmentData = Shared.Data.TreatmentData
module NormalValues = Shared.Data.NormalValues
module String = Shared.Utils.String
module Math = Shared.Utils.Math
module List = Shared.Utils.List
module Select = Component.Select
module Table = Component.Table
module Modal = Component.Modal
module Patient = Shared.Models.Patient


module EmergencyList =

    type Model = { Selections : Select.Model }

    type Msg = 
        | SelectMsg of Select.Msg


    let update msg model =
        match msg with
        | SelectMsg msg ->
            { model with Selections = model.Selections |> Select.update msg }


    let init isMobile = 

        let sels =
            TreatmentData.medicationDefs
            |> List.map (fun (cat, _, _, _, _, _, _, _) ->
                cat
            )
            |> List.distinct
            |> List.sort
            |> List.append [ "alles" ]
            |> Select.init (not isMobile) "Selecteer"

        { Selections = sels }         


    let calcDoseVol kg doserPerKg conc min max =
        let d = 
            kg * doserPerKg
            |> (fun d ->
                if max > 0. && d > max then 
                    max 
                else if min > 0. && d < min then
                    min
                else d
            )

        let v =
            d / conc
            |> (fun v ->
                if v >= 10. then
                    v |> Math.roundBy 1.
                else 
                    v |> Math.roundBy 0.1
            )
            |> Math.fixPrecision 2

        (v * conc |> Math.fixPrecision 2, v)


    let view isMobile ageInMo wght (model : Model) dispatch =
        let age = (ageInMo |> float) / 12.

        let tube = 
            let m = 
                4. + age / 4. 
                |> Math.roundBy0_5
                |> (fun m -> if m > 7. then 7. else m)
            sprintf "%A-%A-%A" (m - 0.5) m (m + 0.5)

        let oral = 
            let m = 12. + age / 2. |> Math.roundBy0_5
            sprintf "%A cm" m

        let nasal =
            let m = 15. + age / 2. |> Math.roundBy0_5
            sprintf "%A cm" m

        let epiIv = 
            let d, v =
                    calcDoseVol wght 0.01 0.1 0.01 0.5
            
            (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 1)) ,
            (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v / 10. |> Math.fixPrecision 2))

        let epiTr = 
            let d, v =
                    calcDoseVol wght 0.1 0.1 0.1 5.
            
            (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 1)) ,
            (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v / 10. |> Math.fixPrecision 2))

        let fluid =
            let d, _ =
                calcDoseVol wght 20. 1. 0. 500.
            
            (sprintf "%A ml NaCl 0.9%%" d) , ("")

        let defib =
            let j = 
                TreatmentData.joules
                |> List.findNearestMax (wght * 4. |> int)
            
            sprintf "%A Joule" j

        let cardio =
            let j = 
                TreatmentData.joules
                |> List.findNearestMax (wght * 2. |> int)
            
            sprintf "%A Joule" j

        let calcMeds (ind, item, dose, min, max, conc, unit, rem) =
            let d, v =
                calcDoseVol wght dose conc min max

            let adv s =
                if s <> "" then s
                else
                    let minmax =
                        match (min = 0., max = 0.) with
                        | true,  true  -> ""
                        | true,  false -> sprintf "(max %A %s)" max unit
                        | false, true  -> sprintf "(min %A %s)" min unit
                        | false, false -> sprintf "(%A - %A %s)" min max unit

                    sprintf "%A %s/kg %s" dose unit minmax

            [
                ind; item; (sprintf "%A %s (%A %s/kg)" d unit (d / wght |> Math.fixPrecision 1) unit); (sprintf "%A ml van %A %s/ml" v conc unit); adv rem 
            ]

        let selected = 
            if model.Selections.Items.Head.Selected then []
            else
                model.Selections.Items
                |> List.filter (fun item -> item.Selected)
                |> List.map (fun item -> item.Name)
        
        [ 
            [ "reanimatie"; "tube maat"; tube; ""; "4 + leeftijd / 4" ]
            [ "reanimatie"; "tube lengte oraal"; oral; ""; "12 + leeftijd / 2" ]
            [ "reanimatie"; "tube lengte nasaal"; nasal; ""; "15 + leeftijd / 2" ]
            [ "reanimatie"; "epinephrine iv/io"; epiIv |> fst; epiIv |> snd; "0,01 mg/kg" ]
            [ "reanimatie"; "epinephrine trach"; epiTr |> fst; epiTr |> snd; "0,1 mg/kg" ]
            [ "reanimatie"; "vaatvulling"; fluid |> fst; fluid |> snd; "20 ml/kg" ]
            [ "reanimatie"; "defibrillatie"; defib; ""; "4 Joule/kg" ]
            [ "reanimatie"; "cardioversie"; cardio; ""; "2 Joule/kg" ]
        ] @ (TreatmentData.medicationDefs |> List.map calcMeds)
        |> List.filter (fun xs ->
            selected = List.empty || selected |> List.exists ((=) xs.Head)
        )
        |> List.map (fun xs ->
            match xs with 
            | cat::item::calc::prep::adv::_ ->
                [
                    Text.span [ Props [ Style [CSSProp.FontStyle "italic"] ] ]   [ cat  |> str ]
                    Text.span [ Modifiers [ Modifier.TextWeight TextWeight.SemiBold; Modifier.TextColor Color.IsGreyDark ] ] [ item |> str ]
                    Text.span [ Modifiers [ Modifier.TextWeight TextWeight.SemiBold ] ] 
                        [ calc |> String.replace "(" "(= " |> str ]
                    prep |> str
                    Text.span [ Modifiers [ Modifier.TextColor Color.IsGrey ] ] [adv |> str]
                ]
            | _ -> []
        )
        |> List.append [ 
                [ "Indicatie"; "Interventie"; "Berekend"; "Bereiding"; "Advies" ]
                |> List.map str ]
        |> Table.create isMobile []
        |> (fun table ->
            if not isMobile then
                [
                    div [ Style [ CSSProp.PaddingBottom "10px" ] ] [ Select.dropdownView model.Selections (SelectMsg >> dispatch) ]
                    table
                ]
            else 
                [
                    div [ Style [ CSSProp.PaddingBottom "10px" ] ] [ Select.selectView model.Selections (SelectMsg >> dispatch) ]
                    table
                ]
        ) |> div []


module ContMeds =

    type Model = { ContMeds : Shared.Continuous list; Selections : Select.Model; ShowMed : Shared.Continuous Option }

    type Msg = 
        | SelectMsg of Select.Msg
        | ClickContMed of Shared.Continuous
        | ModalMsg of Modal.Msg

    let update (msg : Msg) (model : Model) =
        match msg with
        | ModalMsg _ ->
            { model with ShowMed = None }

        | ClickContMed cont -> 
            { model with ShowMed = Some cont }

        | SelectMsg msg ->
            { model with Selections = model.Selections |> Select.update msg }


    let calcQty wght (med : Shared.Continuous) =
        match wght with
        | _ when wght < 6.  -> med.Quantity2To6
        | _ when wght < 11. -> med.Quantity6To11
        | _ when wght < 40. -> med.Quantity11To40
        | _ -> med.QuantityFrom40


    let calcVol wght (med : Shared.Continuous) =
         match wght with
         | _ when wght < 6.  -> med.Volume2To6
         | _ when wght < 11. -> med.Volume6To11
         | _ when wght < 40. -> med.VolumeFrom40
         | _ -> med.QuantityFrom40


    let createModalTitleContent wght (cont : Shared.Continuous) =
            let t = sprintf "Bereiding voor %s" cont.Generic
            let c =
                match TreatmentData.products 
                      |> List.tryFind (fun (_, gen, _, _) ->
                        gen = cont.Generic
                      ) with
                | Some (_, _, un, conc) ->
                    if conc = 0. then "Geen bereiding, oplossing is puur" |> str
                    else
                        let q = (cont |> calcQty wght) / conc
                        let v = (cont |> calcVol wght) - q
                        [ sprintf "= %A ml van %s %A %s/ml + %A ml %s" q cont.Generic conc un v cont.Solution |> str ] 
                        |> List.append 
                            [
                                let q = cont |> calcQty wght
                                let v = cont |> calcVol wght
                                yield [sprintf "%A %s %s in %A ml %s" q cont.Unit cont.Generic v cont.Solution |> str] |> Heading.h5 []
                            ]
                        |> Content.content []
                | None ->
                    "Bereidings tekst niet mogelijk" |> str
            (t, c)


    let createContMed (indication, generic, unit, doseunit, qty2to6, vol2to6, qty6to11, vol6to11, qty11to40, vol11to40,  qtyfrom40, volfrom40, mindose, maxdose, absmax, minconc, maxconc, solution) : Shared.Continuous =
        {
            Indication = indication
            Generic = generic
            Unit = unit
            DoseUnit = doseunit
            Quantity2To6 = qty2to6
            Volume2To6 = vol2to6
            Quantity6To11 = qty6to11
            Volume6To11 = vol6to11
            Quantity11To40 = qty11to40
            Volume11To40 = vol11to40
            QuantityFrom40 = qtyfrom40
            VolumeFrom40 = volfrom40
            MinDose = mindose
            MaxDose = maxdose
            AbsMax = absmax
            MinConc = minconc
            MaxConc = maxconc
            Solution = solution
        }


    let init isMobile =
        let conts =
            TreatmentData.contMeds
            |> List.map createContMed

        let sels =
            conts
            |> List.map (fun med -> med.Indication)
            |> List.distinct
            |> List.append [ "alles" ]
            |> List.sort
            |> Select.init (not isMobile) "Selecteer"

        { ContMeds = conts; Selections = sels; ShowMed = None }         


    let calcContMed age wght =

        let calcDose qty vol unit doseU =
            let f = 
                let t =
                    match doseU with
                    | _ when doseU |> String.contains "dag" -> 24.
                    | _ when doseU |> String.contains "min" -> 1. / 60.
                    | _ -> 1.

                let u = 
                    match unit, doseU with
                    | _ when unit = "mg" && doseU |> String.contains "microg" ->
                        1000.
                    | _ when unit = "mg" && doseU |> String.contains "nanog" ->
                        1000. * 1000.
                    | _ -> 1.

                1. * t * u

            let d = (f * qty / vol / wght) |> Math.fixPrecision 2 
            sprintf "%A %s" d doseU


        let printAdv min max unit =
            sprintf "%A - %A %s" min max unit

        TreatmentData.contMeds
        |> List.map createContMed
        |> List.sortBy (fun med -> med.Indication, med.Generic)
        |> List.map (fun med ->
            let vol = med |> calcVol wght
            let qty = med |> calcQty wght

            if vol = 0. then []
            else
                [ 
                    med.Indication
                    med.Generic
                    ((qty |> string) + " " + med.Unit)
                    ("in " + (vol |> string ) + " ml " + med.Solution)
                    sprintf "1 ml/uur = %s" (calcDose (qty) (vol) med.Unit med.DoseUnit) 
                    (printAdv med.MinDose med.MaxDose med.DoseUnit)
                ]
        )
        |> List.filter (List.isEmpty >> not)


    let view isMobile ageInMo wght (model : Model) dispatch =
        let age = (ageInMo |> float) / 12.

        let selected = 
            if model.Selections.Items.Head.Selected then []
            else
                model.Selections.Items
                |> List.filter (fun item -> item.Selected)
                |> List.map (fun item -> item.Name)

        let meds =
            calcContMed age wght
            |> List.filter (fun xs ->
                if selected |> List.isEmpty then true
                else
                    selected |> List.exists ((=) xs.Head)
            )

        let onclick =
            meds 
            |> List.map (fun xs ->
                match xs with
                | _::gen::_ -> fun _ -> 
                    match TreatmentData.contMeds 
                          |> List.map createContMed 
                            |> List.tryFind (fun cont -> cont.Generic = gen) with
                    | Some cont ->  cont |> ClickContMed |> dispatch
                    | None -> sprintf "OnClick creation error, cannot find %s" gen |> failwith
                | _ ->  failwith "OnClick creation error, not a valid cont meds string list"
            )

        let table =    
            meds
            |> List.map (fun xs ->
                match xs with 
                | ind::gen::qty::sol::dose::adv::_ ->
                    [
                        Text.span [ Props [ Style [CSSProp.FontStyle "italic"] ] ]   [ ind  |> str ]
                        Text.span [ Modifiers [ Modifier.TextWeight TextWeight.SemiBold; Modifier.TextColor Color.IsGreyDark ] ] [ gen |> str ]
                        Text.span [ Modifiers [ Modifier.TextWeight TextWeight.SemiBold ] ] 
                            [ qty |> str ]
                        sol |> str
                        dose |> str
                        Text.span [ Modifiers [ Modifier.TextColor Color.IsGrey ] ] [ adv |> str]
                    ]
                | _ -> []
            )
            |> List.append 
                [ 
                    [ "Indicatie"; "Generiek"; "Hoeveelheid"; "Oplossing"; "Dosering"; "Advies" ] 
                    |> List.map str
                ]
            |> Table.create isMobile onclick

        let content =
            let selView = 
                if isMobile then 
                    div [ Style [ CSSProp.PaddingBottom "10px" ] ] [ Select.selectView model.Selections (SelectMsg >> dispatch) ]
                else
                    div [ Style [ CSSProp.PaddingBottom "10px" ] ] [ Select.dropdownView model.Selections (SelectMsg >> dispatch) ]

            match model.ShowMed with
            | Some med -> 
                let t, c = createModalTitleContent age med
                [ table; Modal.cardModal t c (ModalMsg >> dispatch)]
            | None -> [ table ]
            |> List.append [ selView ]

        div [ ] content



module NormalValues =

    let calcLowBP a =
        match a with 
        | _ when a < 1.   -> 60
        | _ when a < 12.  -> 70
        | _ when a < 120. -> ((a / 12.) * 2. + 70.) |> int
        | _ -> 90 


    let view (pat : Shared.Models.Patient.Patient) =
        let age = pat.Age.Years * 12 + pat.Age.Months |> float

        let ht =
            match NormalValues.ageHeight
                  |> List.filter (fun (a, _) -> age < a) with
            | (_, h)::_ -> sprintf "%A cm" (h |> Math.fixPrecision 2)
            | _ -> ""          

        let hr =
            match NormalValues.heartRate
                  |> List.filter (fun (a, _) -> age < a) with
            | (_, s)::_ -> sprintf "%s /min" s
            | _ -> ""          

        let rr =
            match NormalValues.respRate
                  |> List.filter (fun (a, _) -> age < a) with
            | (_, s)::_ -> sprintf "%s /min" s
            | _ -> ""          

        let sbp =
            match NormalValues.sbp
                  |> List.filter (fun (a, _) -> age < a) with
            | (_, s)::_ -> sprintf "%s mmHg (shock bij <%i mmHg)" s (age |> calcLowBP)
            | _ -> ""          

        let dbp =
            match NormalValues.dbp
                  |> List.filter (fun (a, _) -> age < a) with
            | (_, s)::_ -> sprintf "%s mmHg" s
            | _ -> ""          


        let bsa =
            match pat |> Shared.Models.Patient.calcBSA true with
            | Some bsa -> bsa |> Math.fixPrecision 2 |> sprintf "%A m2"
            | None -> ""


        let bmi =
            match pat |> Shared.Models.Patient.calcBMI true with
            | Some bmi -> bmi |> Math.fixPrecision 2 |> sprintf "%A kg/m2"
            | None -> ""

        let fp =
            if pat |> Patient.getAgeInMonths < 6 then
                "4 uur voor melk (babymelk) of sondevoeding, 3 uur voor borstvoeding, 2 uur voor heldere vloeistof"
            else
                "6 uur voor lichte maaltijd of sondevoeding, 2 uur voor heldere vloeistof (max 220 ml = volle beker)"

        let bcap =
            (pat |> Patient.getAgeInYears) * 30. + 30. 
            |> Math.fixPrecision 0

        [ 
            [ "Gewicht"; pat.Weight.Estimated |> string |> sprintf "%s kg" ]
            [ "Lengte"; ht ]
            [ "Body Surface Area"; bsa ]
            [ "Body Mass Index"; bmi ]
            [ "Hartslag"; hr ]
            [ "Ademhaling"; rr ]
            [ "Teug Volume"; pat.Weight.Estimated * 6. |> Math.fixPrecision 2 |> sprintf "%A ml (6 ml/kg)" ]
            [ "Systolische Bloeddruk"; sbp ]
            [ "Diastolische Bloeddruk"; dbp ]
            [ "Blaascapaciteit"; sprintf "%A ml" bcap  ]
            [ "Nuchter beleid"; fp ]
        ]
        |> List.append [ [ ""; "Waarde" ] ]
        |> List.map (List.map str)
        |> Table.create false []



module Materials =


    let tubeData =
        [
            (None, Some 1200., 2.5, None, "7")
            (None, Some 2000., 3.0, None, "7 - 8")
            (None, Some 3000., 3.5, None, "8 - 9")
            ((3 |> Some), None, 3.5, (Some 3.0), "9 - 10")
            ((6 |> Some), None, 4.0, (Some 3.0), "11 - 12")
            ((2 * 12 |> Some), None, 4.5, (Some 4.0), "12 - 13")
            ((3 * 12 |> Some), None, 5.0, (Some 4.5), "13 - 14")
            ((5 * 12 |> Some), None, 5.5, (Some 5.0), "14 - 15")
            ((8 * 12 |> Some), None, 6.0, (Some 5.5), "15 - 16")
            ((10 * 12 |> Some), None, 6.5, (Some 6.0), "16 - 17")
            (None, None, 7.0, (Some 7.0), "16 - 17")
        ]


    let maskData =
        [
            (5., 1.,   "< 5")            
            (10., 1.5, "< 7")            
            (20., 2.,  "< 10")            
            (30., 2.5, "< 15")            
            (50., 3.,  "< 20")            
            (70., 4.,  "< 35")            
            (999., 5., "< 40")            
        ]


    let venousCathData =
        [
            (1 * 12, 4, 8)
            (4 * 12, 5, 12)
            (12 * 12, 6, 15)
            (19 * 12, 7, 20)
        ]


    let nonZeroString n =
        if n > 0. then n |> string else ""


    let tubes wghtGram ageMo =

        match wghtGram, ageMo with
        | None, None ->
            tubeData
            |> List.rev
            |> List.head
            |> Some
        // find tube data with weight        
        | Some w, None ->
            tubeData |> List.tryFind (fun (_, w', _, _, _) -> 
                match w' with 
                | Some w' -> w < w'
                | None -> false
            )
        // find tube data with age        
        | None, Some a ->
            tubeData |> List.tryFind (fun (a', _, _, _, _) -> 
                match a' with 
                | Some a' -> a < a'
                | None -> false
            )
        // cannot find with weight and age
        | Some _, Some _ -> None        
        |> (fun td ->
            match td with
            | Some (_, _, ts, tc, tl) ->
                match tc with
                | Some tc' -> (ts, tc', tl)
                | None     -> (ts, 0., tl)
            | None -> (0., 0., "")            
        )


    let laryngealMask wght =
        match maskData
              |> List.tryFind (fun (w, _, _) -> wght < w) with
        | Some (_, m, v) -> sprintf "Maat %A, max. cuff volume %s ml" m v          
        | None -> ""


    let venCath a =
        match venousCathData 
              |> List.tryFind (fun (a', _, _) -> a < a') with
        | Some (_, d, l) -> sprintf "%A Fr" d, sprintf "%A cm" l           
        | None -> "", ""


    let view (pat : Shared.Models.Patient.Patient) =
        let ts, tc, tl = tubes None (pat |> Patient.getAgeInMonths |> Some)

        let tn = 
            pat 
            |> Patient.getAgeInYears
            |> (fun a -> a / 2. + 15.)
            |> Math.fixPrecision 0
            |> string

        let zco, zcn =
            ts * 2. |> nonZeroString,
            tc * 2. |> nonZeroString 

        let bn = 
            if pat |> Patient.getWeight < 40. then
                "15 G x 15 mm naald"
            else "15 G x 25 mm naald"

        let vcd, vcl = 
            pat 
            |> Patient.getAgeInMonths
            |> venCath

        let bc =
            ts * 2.
            |> System.Math.Floor
            |> Math.fixPrecision 0

        [
           [ "Tube maat"; sprintf "Zonder cuff %s, met cuff %s, lengte vanaf de mondhoek %s cm via de neus %s cm" (ts |> nonZeroString) (tc |> nonZeroString) tl tn ]
           [ "Maat uitzuigcatheter"; sprintf "%s French bij tube zonder cuff of %s French met cuff" zco zcn ]
           [ "Larynxmasker"; pat |> Patient.getWeight |> laryngealMask ]
           [ "Botnaald"; bn ]
           [ "Centraal Veneuze Catheter"; sprintf "Diameter %s met lengte %s" vcd vcl ]
           [ "Blaascatheter"; sprintf "%A Charriere" bc ]
           [ "Maagsonde"; sprintf "%A French/Charriere" bc ]
        ]
        |> List.append [ [ "Item"; "Waarde" ] ]
        |> List.map (List.map str)
        |> Table.create false []



type Model = 
    { 
        ActiveTab : ActiveTab
        EmergencyListModel : EmergencyList.Model
        ContMedsModel : ContMeds.Model
    }
and ActiveTab =
    | EmergencyListTab
    | ContMedsTab
    | NormalValuesTab
    | MaterialsTab


type Msg = 
    | TabChange of ActiveTab
    | EmergencyListMsg of EmergencyList.Msg
    | ContMedsMsg of ContMeds.Msg


let update (msg : Msg) (model : Model) =
    match msg with

    | TabChange tab ->
        { model with ActiveTab = tab }

    | EmergencyListMsg msg ->
        { model with EmergencyListModel = model.EmergencyListModel |> EmergencyList.update msg }    

    | ContMedsMsg msg ->
        { model with ContMedsModel = ContMeds.update msg model.ContMedsModel }


let init isMobile = 
    { 
        ActiveTab = EmergencyListTab
        EmergencyListModel = EmergencyList.init isMobile
        ContMedsModel = ContMeds.init isMobile 
    }


let view isMobile (pat : Shared.Models.Patient.Patient) (model: Model) dispatch =

    let age =  pat |> Shared.Models.Patient.getAgeInMonths
    let wght = pat |> Shared.Models.Patient.getWeight

    let tabs (model : Model) dispatch =
        Tabs.tabs [ Tabs.IsFullWidth; Tabs.IsBoxed ] 
            [ Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = EmergencyListTab)
                         Tabs.Tab.Props [ OnClick (fun _ -> EmergencyListTab |> TabChange |> dispatch) ] ] [ a [] [str "Noodlijst"] ]
              Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = ContMedsTab) 
                         Tabs.Tab.Props [ OnClick (fun _ -> ContMedsTab |> TabChange |> dispatch) ]] [ a [] [str "Standaard Pompen"]] 
              Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = NormalValuesTab) 
                         Tabs.Tab.Props [ OnClick (fun _ -> NormalValuesTab |> TabChange |> dispatch) ]] [ a [] [str "Normaal Waarden"]] 
              Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = MaterialsTab) 
                         Tabs.Tab.Props [ OnClick (fun _ -> MaterialsTab |> TabChange |> dispatch) ]] [ a [] [str "Materialen"]] ]

    let content =
        match model.ActiveTab with
        | EmergencyListTab ->
            EmergencyList.view isMobile age wght model.EmergencyListModel (EmergencyListMsg >> dispatch)
        | ContMedsTab -> 
            ContMeds.view isMobile age wght model.ContMedsModel (ContMedsMsg >> dispatch)
        | NormalValuesTab ->
            NormalValues.view pat
        | MaterialsTab ->
            Materials.view pat   

    div []
        [
            tabs model dispatch
            content
        ]

