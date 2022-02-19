namespace Shared


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
            "stridor", "adrenaline vernevelen", 5.0, 5.0, 5.0, 1.0, "mg", "5 mg/keer"
            "astma", "prednisolon", 2.0, 0.0, 25.0, 12.5, "mg", ""
            "astma", "magnesiumsulfaat 16%", 40.0, 0.0, 2000.0, 160.0, "mg", ""
            "astma", "magnesiumsulfaat 50%", 40.0, 0.0, 2000.0, 500.0, "mg", ""
            "astma", "salbutamol oplaad", 15.0, 0.0, 0.0, 500.0, "microg", ""
            "anafylaxie", "adrenaline im", 0.01, 0.0, 0.5, 1.0, "mg", ""
            "anafylaxie", "adrenaline vernevelen", 5.0, 5.0, 5.0, 1.0, "mg", "5 mg/keer"
            "anafylaxie", "clemastine", 0.05, 0.0, 2.0, 1.0, "mg", ""
            "anafylaxie", "hydrocortison", 4.0, 0.0, 100.0, 10.0, "mg", ""
            "anti-arrythmica", "adenosine 1e gift", 100.0, 0.0, 12000.0, 3000.0, "microg", ""
            "anti-arrythmica", "adenosine 2e gift", 200.0, 0.0, 12000.0, 3000.0, "microg", ""
            "anti-arrythmica", "adenosine 3e gift", 300.0, 0.0, 12000.0, 3000.0, "microg", ""
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
            "anticonvulsiva", "midazolam buc/nas/im", 0.2, 0.0, 10.0, 5.0, "mg", ""
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
            "elektrolyten", "kaliumchloride 7,4%", 0.5, 0.0, 40.0, 1.0, "mmol", ""
            "elektrolyten", "calciumgluconaat", 0.13, 0.0, 4.5, 0.225, "mmol", ""
            "elektrolyten", "magnesiumchloride 10%", 0.08, 0.0, 0.0, 0.5, "mmol", ""
            "lokaal anesthesie", "licocaine 1%", 5.0, 0.0, 200.0, 10.0, "mg", ""
            "lokaal anesthesie", "licocaine 2%", 5.0, 0.0, 200.0, 20.0, "mg", ""
            "lokaal anesthesie", "bupivacaine", 3.0, 0.0, 0.0, 2.5, "mg", ""
            "lokaal anesthesie", "bupivacaine/adrenaline", 3.0, 0.0, 0.0, 2.5, "mg", ""
        ]