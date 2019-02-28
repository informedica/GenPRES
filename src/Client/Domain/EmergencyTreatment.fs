namespace Domain

module EmergencyTreatment =
    open Utils.Utils

    let calcDoseVol kg doserPerKg conc min max =
        let d =
            kg * doserPerKg
            |> (fun d ->
            if max > 0. && d > max then max
            else if min > 0. && d < min then min
            else d)

        let v =
            d / conc
            |> (fun v ->
            if v >= 10. then v |> Math.roundBy 1.
            else v |> Math.roundBy 0.1)
            |> Math.fixPrecision 2

        (v * conc |> Math.fixPrecision 2, v)

    let age ageInMo = (ageInMo |> float) / 12.

    let tube age =
        let m =
            4. + age / 4.
            |> Math.roundBy0_5
            |> (fun m ->
            if m > 7. then 7.
            else m)
        sprintf "%A-%A-%A" (m - 0.5) m (m + 0.5)

    let oral age =
        let m = 12. + age / 2. |> Math.roundBy0_5
        sprintf "%A cm" m

    let nasal age =
        let m = 15. + age / 2. |> Math.roundBy0_5
        sprintf "%A cm" m

    let epiIv wght =
        let d, v = calcDoseVol wght 0.01 0.1 0.01 0.5
        (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 1)),
        (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)"
             v (v / 10. |> Math.fixPrecision 2))

    let epiTr wght =
        let d, v = calcDoseVol wght 0.1 0.1 0.1 5.
        (sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 1)),
        (sprintf "%A ml van 0,1 mg/ml (1:10.000) of %A ml van 1 mg/ml (1:1000)"
             v (v / 10. |> Math.fixPrecision 2))

    let fluid wght =
        let d, _ = calcDoseVol wght 20. 1. 0. 500.
        (sprintf "%A ml NaCl 0.9%%" d), ("")

    let defib joules wght =
        let j = joules |> List.findNearestMax (wght * 4. |> int)
        sprintf "%A Joule" j

    let cardio joules wght =
        let j = joules |> List.findNearestMax (wght * 2. |> int)
        sprintf "%A Joule" j

    let calcMeds wght (ind, item, dose, min, max, conc, unit, rem) =
        let d, v = calcDoseVol wght dose conc min max

        let adv s =
            if s <> "" then s
            else
                let minmax =
                    match (min = 0., max = 0.) with
                    | true, true -> ""
                    | true, false -> sprintf "(max %A %s)" max unit
                    | false, true -> sprintf "(min %A %s)" min unit
                    | false, false -> sprintf "(%A - %A %s)" min max unit
                sprintf "%A %s/kg %s" dose unit minmax
        [ ind
          item

          (sprintf "%A %s (%A %s/kg)" d unit (d / wght |> Math.fixPrecision 1)
               unit)
          (sprintf "%A ml van %A %s/ml" v conc unit)
          adv rem ]

    let getTableData age wght joules medicationDefs =
        [ [ "reanimatie"
            "tube maat"
            tube age
            ""
            "4 + leeftijd / 4" ]
          [ "reanimatie"
            "tube lengte oraal"
            oral age
            ""
            "12 + leeftijd / 2" ]
          [ "reanimatie"
            "tube lengte nasaal"
            nasal age
            ""
            "15 + leeftijd / 2" ]
          [ "reanimatie"
            "adrenaline iv/io"
            epiIv wght |> fst
            epiIv wght |> snd
            "0,01 mg/kg" ]
          [ "reanimatie"
            "vaatvulling"
            fluid wght |> fst
            fluid wght |> snd
            "20 ml/kg" ]
          [ "reanimatie"
            "defibrillatie"
            defib joules wght
            ""
            "4 Joule/kg" ]
          [ "reanimatie"
            "cardioversie"
            cardio joules wght
            ""
            "2 Joule/kg" ] ]
        @ (medicationDefs |> List.map (calcMeds wght))
