module Treatment

open Shared

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Elmish
open Fulma


module TreatmentData = Data.TreatmentData
module String = Utils.String
module Math = Utils.Math
module List = Utils.List
module Select = Component.Select
module Table = Component.Table
module Modal = Component.Modal


type Model = { Selections : Select.Model; ShowModal : Modal.Model }


type Msg = 
    | ClickContMed of string
    | ModalMsg of Modal.Msg


let update msg model =
    match msg with
    | ModalMsg msg' ->
        { model with ShowModal = model.ShowModal |> Modal.update msg' }
    | ClickContMed s -> 
        let t = sprintf "Bereiding voor %s" s
        let c = "Bereidings tekst...."
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
            TreatmentData.contMeds
            |> List.map createContMed
            |> List.map (fun med -> med.Indication)
            |> List.distinct
            |> List.sort
            |> List.append [ "alles" ]
        else []
        |> List.sort
        |> Select.init   

    { Selections = sels; ShowModal = Modal.init () }         


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

    let qty (med : Continuous) =
        match age with
        | _ when age < 6. -> med.Quantity2To6
        | _ when age < 11. -> med.Quantity6To11
        | _ when age < 40. -> med.Quantity11To40
        | _ -> med.QuantityFrom40

    let vol (med : Continuous) =
         match age with
         | _ when age < 6. -> med.Volume2To6
         | _ when age < 11. -> med.Volume6To11
         | _ when age < 40. -> med.VolumeFrom40
         | _ -> med.QuantityFrom40


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
        if med |> vol = 0. then []
        else
            [ 
                med.Indication
                med.Generic
                ((med |> qty |> string) + " " + med.Unit)
                ("in " + (med |> vol |> string ) + " ml " + med.Solution)
                (calcDose (med |> qty) (med |> vol) med.Unit med.DoseUnit) 
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
            | _::gen::_ -> fun _ -> gen |> ClickContMed |> dispatch
            | _ ->         fun _ -> ""  |> ClickContMed |> dispatch
        )

    let table =    
        meds
        |> List.append [ [ "Indicatie"; "Generiek"; "Hoeveelheid"; "Oplossing"; "Dosering (stand 1 ml/uur)"; "Advies" ] ]
        |> Table.create onclick


    div [] [ table; Modal.cardModal model.ShowModal (ModalMsg >> dispatch) ]
