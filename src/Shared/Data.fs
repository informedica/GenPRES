namespace Shared


module String =


    open System


    let isNullOrWhiteSpace (s: String) = String.IsNullOrWhiteSpace(s)


    let replace (s1: string) s2 (s: string) = s.Replace(s1, s2)


    let split (del: string) (s: string) = s.Split(del)



module NormalValues =


    let ageWeight =
        [
            (0., 3.785)
            (0.7, 3.785)
            (1.4, 4.415)
            (2.1, 5.)
            (2.8, 5.5)
            (4.2, 6.4)
            (6., 7.4)
            (7.4, 8.)
            (9.2, 8.8)
            (11.1, 9.5)
            (12., 9.8)
            (18., 11.5)
            (24., 12.8)
            (30., 14.1)
            (36., 15.2)
            (42., 16.3)
            (48., 17.4)
            (54., 18.5)
            (60., 19.5)
            (66., 20.5)
            (72., 21.6)
            (78., 22.8)
            (84., 24.2)
            (90., 25.5)
            (96., 26.9)
            (102., 28.3)
            (108., 29.8)
            (114., 31.4)
            (120., 33.1)
            (126., 34.9)
            (132., 36.8)
            (138., 38.9)
            (144., 41.2)
            (150., 43.7)
            (156., 46.2)
            (162., 48.9)
            (168., 51.5)
            (174., 54.1)
            (180., 56.4)
            (186., 58.2)
            (192., 59.8)
            (198., 61.1)
            (204., 62.2)
            (210., 63.3)
            (216., 64.2)
            (222., 65.)
            (228., 65.7)
            (234., 66.1)
        ]


    let ageHeight =
        [
            0.6792, 52.95
            3., 52.95
            6., 60.5
            9., 67.25
            12., 71.8
            18., 75.6
            24., 82.3
            30., 88.15
            36., 93.1
            42., 97.6
            48., 101.55
            54., 105.25
            60., 108.8
            66., 112.15
            72., 115.35
            78., 118.55
            84., 121.7
            90., 124.7
            96., 127.7
            102., 130.7
            108., 133.5
            114., 136.2
            120., 139.
            126., 141.8
            132., 144.5
            138., 147.25
            144., 150.15
            150., 153.25
            156., 156.25
            162., 159.05
            168., 161.95
            174., 164.9
            180., 167.45
            186., 169.8
            192., 171.5
            198., 172.55
            204., 173.25
            210., 173.8
            216., 174.2
            222., 174.55
            228., 174.85
            234., 175.05
            240., 175.15
        ]


    let heartRate =
        [
            1., "90-205"
            12., "90-190"
            36., "80-140"
            72., "65-120"
            144., "58-118"
            180., "50-100"
            228., "50-100"
        ]


    let respRate =
        [
            12., "30-53"
            36., "22-37"
            72., "20-28"
            144., "18-25"
            228., "12-20"
        ]


    let sbp =
        [
            1., "60-84"
            12., "72-104"
            36., "86-106"
            72., "89-112"
            120., "97-115"
            144., "102-120"
            228., "110-131"
        ]


    let dbp =
        [
            1., "31-53"
            12., "37-56"
            36., "42-63"
            72., "46-72"
            120., "57-76"
            144., "61-80"
            228., "64-83"
        ]


    let gcs =
        [
            // Pedicatric
            5 * 12,
            [
                "Eye Opening",
                [
                    (4, "spontaan")
                    (3, "bij geluid")
                    (2, "bij pijn")
                    (1, "geen")
                    (0, "zwelling of verband")
                ]
                "Motor Response",
                [
                    (6, "normale spontane bewegingen")
                    (5, "lokaliseert bij pijn")
                    (4, "trekt terug bij pijn")
                    (3, "flexie bij pijn")
                    (2, "extensie bij pijn")
                    (1, "geen reactie op pijn")
                ]
                "Verbal Response",
                [
                    (5, "alert, brabbelt, conform leeftijd")
                    (4, "minder, geirriteerd huilen")
                    (3, "huilt bij pijn")
                    (2, "kreunt bij pijn")
                    (1, "geen reactie op pijn")
                    (0, "tube")
                ]
            ]
            // Adult
            19 * 12,
            [
                "Eye Opening",
                [
                    (4, "spontaan")
                    (3, "bij geluid")
                    (2, "bij pijn")
                    (1, "geen")
                    (0, "zwelling of verband")
                ]
                "Motor Response",
                [
                    (6, "normale spontane bewegingen")
                    (5, "lokaliseert bij pijn")
                    (4, "trekt terug bij pijn")
                    (3, "flexie bij pijn")
                    (2, "extensie bij pijn")
                    (1, "geen reactie op pijn")
                ]
                "Verbal Response",
                [
                    (5, "voert opdrachten uit")
                    (4, "verward")
                    (3, "niet adequaat")
                    (2, "niet verstaanbaar")
                    (1, "geen reactie op pijn")
                    (0, "tube")
                ]
            ]
        ]



module Data =

    let medDefs =
        [
            "reanimatie", "glucose 10%", 0.2, 0.0, 25.0, 0.1, "gram", ""
            "reanimatie", "NaBic 8,4", 0.5, 0.0, 50.0, 1.0, "mmol", ""
            "intubatie", "propofol 1%", 2.0, 0.0, 0.0, 10.0, "mg", ""
            "intubatie", "propofol 2%", 2.0, 0.0, 0.0, 20.0, "mg", ""
            "intubatie", "midazolam", 0.2, 0.0, 10.0, 5.0, "mg", ""
            "intubatie", "esketamine", 0.5, 0.0, 5.0, 5.0, "mg", ""
            "intubatie", "etomidaat", 0.5, 0.0, 20.0, 2.0, "mg", ""
            "intubatie", "fentanyl", 1.0, 0.0, 50.0, 50.0, "mcg", ""
            "intubatie", "morfine", 0.1, 0.0, 10.0, 1.0, "mg", ""
            "intubatie", "rocuronium", 1.0, 0.0, 10.0, 10.0, "mg", ""
            "intubatie", "atropine", 0.02, 0.1, 0.5, 0.5, "mg", ""
            "stridor", "dexamethason", 0.15, 0.0, 14.0, 4.0, "mg", ""
            "stridor",
            "adrenaline vernevelen",
            5.0,
            5.0,
            5.0,
            1.0,
            "mg",
            "5 mg/keer"
            "astma", "prednisolon", 2.0, 0.0, 25.0, 12.5, "mg", ""
            "astma", "magnesiumsulfaat 16%", 40.0, 0.0, 2000.0, 160.0, "mg", ""
            "astma", "magnesiumsulfaat 50%", 40.0, 0.0, 2000.0, 500.0, "mg", ""
            "astma", "salbutamol oplaad", 15.0, 0.0, 0.0, 500.0, "microg", ""
            "anafylaxie", "adrenaline im", 0.01, 0.0, 0.5, 1.0, "mg", ""
            "anafylaxie",
            "adrenaline vernevelen",
            5.0,
            5.0,
            5.0,
            1.0,
            "mg",
            "5 mg/keer"
            "anafylaxie", "clemastine", 0.05, 0.0, 2.0, 1.0, "mg", ""
            "anafylaxie", "hydrocortison", 4.0, 0.0, 100.0, 10.0, "mg", ""
            "anti-arrythmica",
            "adenosine 1e gift",
            100.0,
            0.0,
            12000.0,
            3000.0,
            "microg",
            ""
            "anti-arrythmica",
            "adenosine 2e gift",
            200.0,
            0.0,
            12000.0,
            3000.0,
            "microg",
            ""
            "anti-arrythmica",
            "adenosine 3e gift",
            300.0,
            0.0,
            12000.0,
            3000.0,
            "microg",
            ""
            "anti-arrythmica", "amiodarone", 5.0, 0.0, 300.0, 50.0, "mg", ""
            "antidota", "flumazine", 0.02, 0.0, 0.3, 0.1, "mg", ""
            "antidota", "naloxon", 0.01, 0.0, 0.5, 0.02, "mg", ""
            "antidota", "naloxon", 0.01, 0.0, 0.5, 0.02, "mg", ""
            "antidota", "intralipid 20%", 1.5, 0.0, 0.0, 1.0, "ml", ""
            "antidota", "fenytoine", 5.0, 0.0, 1500.0, 50.0, "mg", ""
            "hyperkaliemie", "resonium klysma", 1.0, 0.0, 0.0, 0.15, "gram", ""
            "hyperkaliemie", "furosemide", 1.0, 0.0, 0.0, 10.0, "mg", ""
            "hyperkaliemie", "NaBic 8,4%", 2.5, 0.0, 0.0, 1.0, "mmol", ""
            "hyperkaliemie", "calciumgluconaat", 0.13, 0.0, 4.5, 0.225, "mg", ""
            "anticonvulsiva", "diazepam rect", 0.5, 0.0, 0.0, 4.0, "mg", ""
            "anticonvulsiva", "diazepam iv", 0.25, 0.0, 0.0, 5.0, "mg", ""
            "anticonvulsiva", "fenytoine", 20.0, 0.0, 1500.0, 50.0, "mg", ""
            "anticonvulsiva", "midazolam", 0.1, 0.0, 10.0, 5.0, "mg", ""
            "anticonvulsiva",
            "midazolam buc/nas/im",
            0.2,
            0.0,
            10.0,
            5.0,
            "mg",
            ""
            "anticonvulsiva", "thiopental", 5.0, 0.0, 0.0, 25.0, "mg", ""
            "hersenoedeem", "mannitol 15%", 0.5, 0.0, 50.0, 0.15, "gram", ""
            "hersenoedeem", "NaCl 2,9%", 3.0, 0.0, 0.0, 1.0, "ml", ""
            "pijnstilling", "paracetamol", 20.0, 0.0, 100.0, 10.0, "mg", ""
            "pijnstilling", "diclofenac", 1.0, 0.0, 75.0, 25.0, "mg", ""
            "pijnstilling", "fentanyl", 1.0, 0.0, 0.0, 50.0, "microg", ""
            "pijnstilling", "morfine", 0.1, 0.0, 0.0, 1.0, "mg", ""
            "anti-emetica", "dexamethason", 0.1, 0.0, 8.0, 4.0, "mg", ""
            "anti-emetica", "ondansetron", 0.1, 0.0, 4.0, 2.0, "mg", ""
            "anti-emetica", "droperidol", 0.02, 0.0, 1.25, 2.5, "mg", ""
            "anti-emetica", "metoclopramide", 0.1, 0.0, 10.0, 5.0, "mg", ""
            "elektrolyten",
            "kaliumchloride 7,4%",
            0.5,
            0.0,
            40.0,
            1.0,
            "mmol",
            ""
            "elektrolyten",
            "calciumgluconaat",
            0.13,
            0.0,
            4.5,
            0.225,
            "mmol",
            ""
            "elektrolyten",
            "magnesiumchloride 10%",
            0.08,
            0.0,
            0.0,
            0.5,
            "mmol",
            ""
            "lokaal anesthesie", "licocaine 1%", 5.0, 0.0, 200.0, 10.0, "mg", ""
            "lokaal anesthesie", "licocaine 2%", 5.0, 0.0, 200.0, 20.0, "mg", ""
            "lokaal anesthesie", "bupivacaine", 3.0, 0.0, 0.0, 2.5, "mg", ""
            "lokaal anesthesie",
            "bupivacaine/adrenaline",
            3.0,
            0.0,
            0.0,
            2.5,
            "mg",
            ""
        ]

    let contMeds =
        [
            "inotropie",
            "adrenaline",
            "mg",
            "microg/kg/min",
            1.,
            50.,
            2.,
            50.,
            5.,
            50.,
            5.,
            50.,
            0.05,
            0.5,
            1.,
            0.,
            0.1,
            "NaCl of Gluc"
            "suppletie",
            "albumine 20%",
            "g",
            "gram/kg/dag",
            0.2,
            1.,
            0.2,
            1.,
            0.2,
            1.,
            0.2,
            1.,
            1.,
            2.,
            4.,
            0.,
            0.,
            ""
            "ductus afhankelijk cor vitium",
            "alprostadil",
            "mg",
            "nanog/kg/min",
            0.2,
            50.,
            0.,
            0.,
            0.,
            0.,
            0.,
            0.,
            10.,
            50.,
            100.,
            0.,
            0.02,
            "NaCl of Gluc"
            "anti-arrythmica",
            "amiodarone",
            "mg",
            "microg/kg/min",
            50.,
            50.,
            150.,
            50.,
            300.,
            50.,
            600.,
            50.,
            5.,
            15.,
            25.,
            0.6,
            50.,
            "Gluc 5%"
            "sedatie",
            "clonidine",
            "mg",
            "microg/kg/uur",
            0.15,
            50.,
            0.3,
            50.,
            0.6,
            50.,
            0.6,
            50.,
            0.25,
            2.,
            3.,
            0.,
            0.15,
            "NaCl"
            "inotropie",
            "dobutamine",
            "mg",
            "microg/kg/min",
            80.,
            50.,
            200.,
            50.,
            400.,
            50.,
            400.,
            50.,
            1.,
            20.,
            30.,
            0.,
            12.5,
            "NaCl of Gluc"
            "inotropie",
            "dopamine",
            "mg",
            "microg/kg/min",
            80.,
            50.,
            200.,
            50.,
            400.,
            50.,
            400.,
            50.,
            1.,
            20.,
            30.,
            0.,
            40.,
            "NaCl of Gluc"
            // "pijnstilling", "Epi bupi 1,25mg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
            // "pijnstilling", "Epi bupi 1,25mg, sufenta 0,5 mcg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
            "pulmonale hypertensie",
            "epoprostenol",
            "mg",
            "nanog/kg/min",
            0.2,
            50.,
            0.4,
            50.,
            0.8,
            50.,
            0.8,
            50.,
            0.5,
            50.,
            50.,
            0.005,
            0.01,
            "glycine buffer"
            "sedatie",
            "esketamine",
            "mg",
            "mg/kg/uur",
            100.,
            50.,
            250.,
            50.,
            250.,
            50.,
            250.,
            50.,
            0.5,
            1.,
            2.,
            0.,
            5.,
            "NaCl of Gluc"
            "antihypertensiva",
            "esmolol",
            "mg",
            "mg/kg/min",
            500.,
            50.,
            500.,
            50.,
            500.,
            50.,
            500.,
            50.,
            0.1,
            1.,
            2.,
            0.,
            10.,
            "NaCl of Gluc"
            "pijnstilling",
            "fentanyl",
            "mg",
            "microg/kg/uur",
            0.5,
            50.,
            1.,
            50.,
            2.5,
            50.,
            2.5,
            50.,
            1.,
            5.,
            10.,
            0.,
            0.05,
            "NaCl of Gluc"
            "vasopressie",
            "fenylefrine",
            "mg",
            "microg/kg/min",
            1.5,
            50.,
            2.5,
            50.,
            5.,
            50.,
            5.,
            50.,
            0.05,
            5.,
            10.,
            0.,
            10.,
            "NaCl of Gluc"
            "diuretica",
            "furosemide",
            "mg",
            "mg/kg/dag",
            10.,
            50.,
            20.,
            50.,
            40.,
            50.,
            100.,
            50.,
            1.,
            4.,
            6.,
            0.,
            10.,
            "NaCl"
            "antistolling",
            "heparine",
            "IE",
            "IE/kg/uur",
            5000.,
            50.,
            10000.,
            50.,
            20000.,
            50.,
            20000.,
            50.,
            10.,
            20.,
            50.,
            0.,
            1000.,
            "NaCl"
            "glucose regulatie",
            "insuline",
            "IE",
            "IE/kg/uur",
            10.,
            50.,
            10.,
            50.,
            50.,
            50.,
            50.,
            50.,
            0.02,
            0.125,
            2.,
            0.,
            1.,
            "NaCl"
            "chronotropie",
            "isoprenaline",
            "mg",
            "microg/kg/min",
            2.,
            50.,
            2.,
            50.,
            2.,
            50.,
            2.,
            50.,
            0.01,
            1.5,
            3.,
            0.,
            1.,
            "Gluc"
            "antihypertensiva",
            "labetalol",
            "mg",
            "mg/kg/uur",
            250.,
            50.,
            250.,
            50.,
            250.,
            50.,
            250.,
            50.,
            0.25,
            3.,
            4.,
            0.,
            5.,
            "NaCl of Gluc"
            "bronchodilatie",
            "magnesiumsulfaat",
            "mg",
            "mg/kg/uur",
            500.,
            50.,
            1000.,
            50.,
            2000.,
            50.,
            2000.,
            50.,
            3.,
            20.,
            25.,
            1.,
            160.,
            "NaCl of Gluc"
            "sedatie",
            "midazolam",
            "mg",
            "mg/kg/uur",
            25.,
            50.,
            50.,
            50.,
            50.,
            50.,
            100.,
            50.,
            0.05,
            0.5,
            1.,
            0.,
            5.,
            "NaCl of Gluc"
            "inotropie",
            "milrinone",
            "mg",
            "microg/kg/min",
            5.,
            50.,
            10.,
            50.,
            20.,
            50.,
            20.,
            50.,
            0.15,
            0.5,
            0.75,
            0.,
            1.,
            "NaCl of Gluc"
            "pijnstilling",
            "morfine",
            "mg",
            "mg/kg/dag",
            2.,
            50.,
            5.,
            50.,
            10.,
            50.,
            50.,
            50.,
            0.1,
            0.5,
            1.,
            0.,
            1.,
            "NaCl of Gluc"
            "suppletie",
            "NaCl 2,9%",
            "mmol",
            "mmol/kg/dag",
            25.,
            50.,
            25.,
            50.,
            25.,
            50.,
            25.,
            50.,
            2.,
            4.,
            6.,
            0.,
            0.,
            ""
            "antihypertensiva",
            "nitroprusside",
            "mg",
            "microg/kg/min",
            10.,
            50.,
            20.,
            50.,
            40.,
            50.,
            40.,
            50.,
            0.5,
            8.,
            10.,
            0.,
            10.,
            "NaCl of Gluc"
            "vasopressie",
            "noradrenaline",
            "mg",
            "microg/kg/min",
            1.,
            50.,
            2.,
            50.,
            5.,
            50.,
            5.,
            50.,
            0.05,
            0.5,
            1.,
            0.,
            1.,
            "NaCl of Gluc"
            "sedatie",
            "propofol 1%",
            "mg",
            "mg/kg/uur",
            10.,
            1.,
            10.,
            1.,
            10.,
            1.,
            10.,
            1.,
            1.,
            4.,
            4.,
            0.,
            0.,
            ""
            "sedatie",
            "propofol 2%",
            "mg",
            "mg/kg/uur",
            20.,
            1.,
            20.,
            1.,
            20.,
            1.,
            20.,
            1.,
            1.,
            4.,
            4.,
            0.,
            0.,
            ""
            "verslapping",
            "rocuronium",
            "mg",
            "mg/kg/uur",
            50.,
            50.,
            100.,
            50.,
            200.,
            50.,
            200.,
            50.,
            0.6,
            1.2,
            2.,
            0.,
            10.,
            "NaCl of Gluc"
            "bronchodilatie",
            "salbutamol",
            "mg",
            "microg/kg/min",
            5.,
            50.,
            10.,
            50.,
            20.,
            50.,
            20.,
            50.,
            0.1,
            10.,
            15.,
            0.005,
            0.42,
            "NaCl of Gluc"
            "sedatie",
            "thiopental",
            "mg",
            "mg/kg/uur",
            1250.,
            50.,
            1250.,
            50.,
            1250.,
            50.,
            1250.,
            50.,
            5.,
            10.,
            20.,
            5.,
            25.,
            "NaCl of Gluc"
        ]


    let products =
        [
            "inotropie", "adrenaline", "mg", 1.
            "suppletie", "albumine 20%", "g", 0.
            "ductus afhankelijk cor vitium", "alprostadil", "mg", 0.5
            "anti-arrythmica", "amiodarone", "mg", 50.
            "sedatie", "clonidine", "mg", 0.15
            "inotropie", "dobutamine", "mg", 12.5
            "inotropie", "dopamine", "mg", 40.
            // "pijnstilling", "Epi bupi 1,25mg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
            // "pijnstilling", "Epi bupi 1,25mg, sufenta 0,5 mcg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
            "pulmonale hypertensie", "epoprostenol", "mg", (50. / 1000.)
            "sedatie", "esketamine", "mg", 5.
            "antihypertensiva", "esmolol", "mg", 10.
            "pijnstilling", "fentanyl", "mg", 0.05
            "vasopressie", "fenylefrine", "mg", 10.
            "diuretica", "furosemide", "mg", 10.
            "antistolling", "heparine", "IE", 5000.
            "glucose regulatie", "insuline", "IE", 100.
            "chronotropie", "isoprenaline", "mg", 1.
            "antihypertensiva", "labetalol", "mg", 5.
            "bronchodilatie", "magnesiumsulfaat", "mg", 100.
            "sedatie", "midazolam", "mg", 5.
            "inotropie", "milrinone", "mg", 1.
            "pijnstilling", "morfine", "mg", 10.
            "suppletie", "NaCl 2,9%", "mmol", 0.5
            "antihypertensiva", "nitroprusside", "mg", 25.
            "vasopressie", "noradrenaline", "mg", 1.
            "sedatie", "propofol 1%", "mg", 0.
            "sedatie", "propofol 2%", "mg", 0.
            "verslapping", "rocuronium", "mg", 10.
            "bronchodilatie", "salbutamol", "mg", 1.
            "sedatie", "thiopental", "mg", 25.
        ]


module Csv =

    open System
    open Types


    let tryCast dt (x: string) =
        match dt with
        | StringData -> box (x.Trim())
        | FloatData ->
            match Double.TryParse(x) with
            | true, dt -> dt |> box
            | _ ->
                $"cannot parse {x} to double"
                |> failwith
        | FloatOptionData ->
            match Int32.TryParse(x) with
            | true, n -> n |> box
            | _ -> None |> box


    let getColumn dt columns sl s =
        columns
        |> Array.tryFindIndex ((=) s)
        |> function
            | None ->
                $"""cannot find column {s} in {columns |> String.concat ", "}"""
                |> failwith
            | Some i -> sl |> Array.item i


    let getStringColumn columns sl s =
        getColumn StringData columns sl s |> unbox<string>


    let getFloatColumn columns sl s =
        getColumn FloatData columns sl s |> unbox<float>


    let getFloatOptionColumn columns sl s =
        getColumn FloatOptionData columns sl s
        |> unbox<float option>


    let parseCSV (s: string) =
        s.Split("\n")
        |> Array.filter (String.isNullOrWhiteSpace >> not)
        |> Array.map (String.replace "\",\"" "|")
        |> Array.map (String.replace "\"" "")
        |> Array.map (fun s ->
            s.Split("|")
            |> Array.map (fun s -> s.Trim())
        )