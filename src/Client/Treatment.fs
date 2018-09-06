module Treatment

open Shared

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Elmish
open Fulma


module TreatmentData = Data.TreatmentData
module NormalValues = Data.NormalValues
module String = Utils.String
module Math = Utils.Math
module List = Utils.List
module Select = Component.Select
module Table = Component.Table
module Modal = Component.Modal


type Model = { Age : float; ContMeds : Continuous list; Selections : Select.Model; ShowModal : Modal.Model }


type Msg = 
    | AgeChange of float
    | ClickContMed of Continuous
    | ModalMsg of Modal.Msg


let calcQty age (med : Continuous) =
    match age with
    | _ when age < 6.  -> med.Quantity2To6
    | _ when age < 11. -> med.Quantity6To11
    | _ when age < 40. -> med.Quantity11To40
    | _ -> med.QuantityFrom40


let calcVol age (med : Continuous) =
     match age with
     | _ when age < 6.  -> med.Volume2To6
     | _ when age < 11. -> med.Volume6To11
     | _ when age < 40. -> med.VolumeFrom40
     | _ -> med.QuantityFrom40


let update (msg : Msg) (model : Model) =
    match msg with
    | AgeChange a ->
        { model with Age = a }
    | ModalMsg msg' ->
        { model with ShowModal = model.ShowModal |> Modal.update msg' }

    | ClickContMed cont -> 
        let t = sprintf "Bereiding voor %s" cont.Generic
        let c =
            match TreatmentData.products 
                  |> List.tryFind (fun (_, gen, _, _) ->
                    gen = cont.Generic
                  ) with
            | Some (_, _, un, conc) ->
                if conc = 0. then "Geen bereiding, oplossing is puur" |> str
                else
                    let q = (cont |> calcQty model.Age) / conc
                    let v = (cont |> calcVol model.Age) - q
                    [ sprintf "= %A ml van %s %A %s/ml + %A ml %s" q cont.Generic conc un v cont.Solution |> str ] 
                    |> List.append 
                        [
                            let q = cont |> calcQty model.Age
                            let v = cont |> calcVol model.Age
                            yield [sprintf "%A %s %s in %A ml %s" q cont.Unit cont.Generic v cont.Solution |> str] |> Heading.h5 []
                        ]
                    |> Content.content []
            | None ->
                "Bereidings tekst niet mogelijk" |> str

        { model with ShowModal = model.ShowModal |> Modal.update ((t, c) |> Modal.Show) }


let createContMed (indication, generic, unit, doseunit, qty2to6, vol2to6, qty6to11, vol6to11, qty11to40, vol11to40,  qtyfrom40, volfrom40, mindose, maxdose, absmax, minconc, maxconc, solution) =
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
        


let init cat =
    let conts =
        TreatmentData.contMeds
        |> List.map createContMed

    let sels =
        if cat = "Noodlijst" then
            TreatmentData.medicationDefs
            |> List.map (fun (cat, _, _, _, _, _, _, _) ->
                cat
            )
            |> List.distinct
            |> List.sort
            |> List.append [ "alles" ]
        else if cat = "Standaard Pompen" then
            conts
            |> List.map (fun med -> med.Indication)
            |> List.distinct
            |> List.sort
            |> List.append [ "alles" ]
        else []
        |> List.sort
        |> Select.init   

    { Age = 0.; ContMeds = conts; Selections = sels; ShowModal = Modal.init () }         


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
        let vol = med |> calcVol age
        let qty = med |> calcQty age

        if vol = 0. then []
        else
            [ 
                med.Indication
                med.Generic
                ((qty |> string) + " " + med.Unit)
                ("in " + (vol |> string ) + " ml " + med.Solution)
                (calcDose (qty) (vol) med.Unit med.DoseUnit) 
                (printAdv med.MinDose med.MaxDose med.DoseUnit)
            ]
    )
    |> List.filter (List.isEmpty >> not)


let treatment (model : Model) (pat : Patient.Model) =
    let age = 
        let yrs = pat.Age.Years |> double
        let mos = (pat.Age.Months |> double) / 12.
        yrs + mos

    let wght = pat |> Patient.getWeight

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
        
        (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 2)) ,
        (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)" v (v / 10. |> Math.fixPrecision 2))

    let epiTr = 
        let d, v =
                calcDoseVol wght 0.1 0.1 0.1 5.
        
        (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 2)) ,
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
            ind; item; (sprintf "%A %s (%A %s/kg)" d unit (d / wght |> Math.fixPrecision 2) unit); (sprintf "%A ml van %A %s/ml" v conc unit); adv rem 
        ]

    let selected = 
        if model.Selections.Head.Selected then []
        else
            model.Selections
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
    |> List.append [[ "Indicatie"; "Interventie"; "Berekend"; "Bereiding"; "Advies" ]]
    |> Table.create []


let contMeds (model : Model) (pat : Patient.Model) dispatch =
    let selected = 
        if model.Selections.Head.Selected then []
        else
            model.Selections
            |> List.filter (fun item -> item.Selected)
            |> List.map (fun item -> item.Name)

    let meds =
        calcContMed (pat |> Patient.getAge) (pat |> Patient.getWeight)
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
        |> List.append [ [ "Indicatie"; "Generiek"; "Hoeveelheid"; "Oplossing"; "Dosering (stand 1 ml/uur)"; "Advies" ] ]
        |> Table.create onclick


    div [] [ table; Modal.cardModal model.ShowModal (ModalMsg >> dispatch) ]


let normalValues (pat : Patient) =
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
        | (_, s)::_ -> sprintf "%s mmHg" s
        | _ -> ""          

    let dbp =
        match NormalValues.dbp
              |> List.filter (fun (a, _) -> age < a) with
        | (_, s)::_ -> sprintf "%s mmHg" s
        | _ -> ""          

    [ 
        [ "Gewicht"; pat.Weight.Estimated |> string |> sprintf "%s kg" ]
        [ "Lengte"; ht ]
        [ "Hartslag"; hr ]
        [ "Ademhaling"; rr ]
        [ "Teug Volume"; pat.Weight.Estimated * 6. |> Math.fixPrecision 2 |> sprintf "%A ml (6 ml/kg)" ]
        [ "Systolische Bloeddruk"; sbp ]
        [ "Diastolische Bloeddruk"; dbp ]
    ]
    |> List.append [ [ ""; "Waarde" ] ]
    |> Table.create []