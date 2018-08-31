namespace Data

module Treatment =

    open Shared
    open Utils

    type Medication =
        | Bolus of Bolus
        | Continuous of Continuous
    // ind, item, dose, min, max, conc, unit, rem
    and Bolus =
        {
            Indication : string
            Generic : string
            NormDose : float
            MinDose : float
            MaxDose : float
            Concentration : float
            Unit : string
            Remark : string
        }
    //                                          Standaard oplossingen								            Advies doseringen						
    //                                          2 tot 6		    6 tot 11	    11 tot 40	    vanaf 40								
    // Tbl_Ped_MedContIV	Eenheid	DosEenheid	Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    MinDos	MaxDos	AbsMax	MinConc	MaxConc	OplKeuze
    and Continuous =
        {
            Indication: string
            Generic : string
            Unit : string
            DoseUnit : string
            Quantity2To6 : float
            Volume2To6 : float
            Quantity6To11 : float
            Volume6To11 : float
            Quantity11To40 : float
            Volume11To40 : float
            QuantityFrom40 : float
            VolumeFrom40 : float
            MinDose : float
            MaxDose : float
            AbsMax : float
            MinConc : float
            MaxConc : float
            Solution : string
        }

    let joules =
        [ 
            1
            2
            3
            5
            7
            10
            20
            30
            50
            70
            100
            150
        ]


    // ind, item, dose, min, max, conc, unit, rem
    let medicationDefs =
        [ 
            ( "reanimatie", "glucose 10%", 0.2, 0., 25., 0.1, "gram", "" )
            ( "reanimatie", "NaBic 8,4", 0.5, 0., 50., 1., "mmol", "" )
            ( "intubatie", "propofol 1%", 2., 0., 0., 10., "mg", "" )
            ( "intubatie", "propofol 2%", 2., 0., 0., 20., "mg", "" )
            ( "intubatie", "midazolam", 0.2, 0., 10., 5., "mg", "" )
            ( "intubatie", "esketamine", 0.5, 0., 5., 5., "mg", "" )
            ( "intubatie", "etomidaat", 0.5, 0., 20., 2., "mg", "" )
            ( "intubatie", "fentanyl", 1., 0., 50., 50., "mcg", "" )
            ( "intubatie", "morfine", 0.1, 0., 10., 1., "mg", "" )
            ( "intubatie", "rocuronium", 1., 0., 10., 10., "mg", "" )
            ( "intubatie", "atropine", 0.02, 0.1, 0.5, 0.5, "mg", "" )
            ( "stridor", "dexamethason", 0.15, 0., 14., 4., "mg", "" )
            ( "stridor", "adrenaline vernevelen", 5., 5., 5., 1., "mg", "5 mg/keer" )
            ( "astma", "prednisolon", 2., 0., 25., 12.5, "mg", "" )
            ( "astma", "magnesiumsulfaat 16%", 40., 0., 2000., 160., "mg", "" )
            ( "astma", "magnesiumsulfaat 50%", 40., 0., 2000., 500., "mg", "" )
            ( "astma", "salbutamol oplaad", 15., 0., 0., 500., "microg", "" )
            ( "anafylaxie", "adrenaline im", 0.01, 0., 0.5, 1., "mg", "" )
            ( "anafylaxie", "adrenaline vernevelen", 5., 5., 5., 1., "mg", "5 mg/keer" )
            ( "anafylaxie", "clemastine", 0.05, 0., 2., 1., "mg", "" )
            ( "anafylaxie", "hydrocortison", 4., 0., 100., 10., "mg", "" )
            ( "anti-arrythmica", "adenosine 1e gift", 100., 0., 12000., 3000., "microg", "" )
            ( "anti-arrythmica", "adenosine 2e gift", 200., 0., 12000., 3000., "microg", "" )
            ( "anti-arrythmica", "adenosine 3e gift", 300., 0., 12000., 3000., "microg", "" )            
            ( "anti-arrythmica", "amiodarone", 5., 0., 300., 50., "mg", "" )
            ( "antidota", "flumazine", 0.02, 0., 0.3, 0.1, "mg", "" )
            ( "antidota", "naloxon", 0.01, 0., 0.5, 0.02, "mg", "" )
            ( "anticonvulsiva", "diazepam", 0.5, 0., 10., 2., "mg", "" )
            ( "anticonvulsiva", "fenytoine", 20., 0., 1500., 50., "mg", "" )
            ( "anticonvulsiva", "midazolam", 0.1, 0., 10., 5., "mg", "" )
            ( "anticonvulsiva", "midazolam buc/nas/im", 0.2, 0., 10., 5., "mg", "" )
            ( "hersenoedeem", "mannitol 15%", 0.5, 0., 50., 0.15, "gram", "" )
            ( "hersenoedeem", "NaCl 2,9%", 3., 0., 0., 1., "ml", "" )
            ( "pijnstilling", "paracetamol", 20., 0., 100., 10., "mg", "" )
            ( "pijnstilling", "diclofenac", 1., 0., 75., 25., "mg", "" )
            ( "pijnstilling", "fentanyl", 1., 0., 0., 50., "microg", "" )
            ( "pijnstilling", "morfine", 1., 0., 0., 10., "mg", "" )
            ( "anti-emetica", "ondansetron", 0.1, 0., 4., 2., "mg", "" )
            ( "anti-emetica", "metoclopramide", 0.1, 0., 10., 5., "mg", "" )
            ( "elektrolyten", "kaliumchloride 7,4%", 0.5, 0., 40., 1.0, "mmol", "" )
            ( "elektrolyten", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mmol", "" )
            ( "elektrolyten", "magnesiumchloride 10%", 0.08, 0., 0., 0.5, "mmol", "" )
            ( "lokaal anesthesie", "licocaine 1%", 5., 0., 200., 10., "mg", "" )
            ( "lokaal anesthesie", "licocaine 2%", 5., 0., 200., 20., "mg", "" )
        ]

    //                                          Standaard oplossingen								            Advies doseringen						
    //                                          2 tot 6		    6 tot 11	    11 tot 40	    vanaf 40								
    // Tbl_Ped_MedContIV	Eenheid	DosEenheid	Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    MinDos	MaxDos	AbsMax	MinConc	MaxConc	OplKeuze
    let contMeds =
        [
            "inotropie", "adrenaline", "mg", "microg/kg/min", 1., 50., 2., 50., 5., 50., 5., 50., 0.05, 0.5, 1., 0., 0.1, "NaCl"
            "suppletie", "albumine 20%", "g", "gram/kg/dag", 0.2, 1., 0.2, 1., 0.2, 1., 0.2, 1., 1., 2., 4., 0., 0., ""
            "ductus afhankelijk cor vitium", "alprostadil", "mg", "nanog/kg/min", 0.2, 50., 0., 0., 0., 0., 0., 0., 10., 50., 100., 0., 0.02, "NaCl"
            "anti-arrythmica", "amiodarone", "mg", "microg/kg/min", 50., 50., 150., 50., 300., 50., 600., 50., 5., 15., 25., 0.6, 50., "NaCl"
            "sedatie", "clonidine", "mg", "microg/kg/uur", 0.15, 50., 0.3, 50., 0.6, 50., 0.6, 50., 0.25, 2., 3., 0., 0.15, "NaCl"
            "inotropie", "dobutamine", "mg", "microg/kg/min", 80., 50., 200., 50., 400., 50., 400., 50., 1., 20., 30., 0., 12.5, "NaCl"
            "inotropie", "dopamine", "mg", "microg/kg/min", 80., 50., 200., 50., 400., 50., 400., 50., 1., 20., 30., 0., 40., "NaCl"
            // "pijnstilling", "Epi bupi 1,25mg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
            // "pijnstilling", "Epi bupi 1,25mg, sufenta 0,5 mcg /ml", "ml", "ml/uur", 0., 24., 0., 48., 0., 48., 0., 48., 1., 8., 8., 0., 0., ""
            "pulmonale hypertensie", "epoprostenol", "mg", "nanog/kg/min", 0.2, 50., 0.4, 50., 0.8, 50., 0.8, 50., 0.5, 50., 50., 0.005, 0.01, "NaCl"
            "sedatie", "esketamine", "mg", "mg/kg/uur", 100., 50., 250., 50., 250., 50., 250., 50., 0.5, 1., 2., 0., 5., "NaCl"
            "antihypertensiva", "esmolol", "mg", "mg/kg/min", 500., 50., 500., 50., 500., 50., 500., 50., 0.1, 1., 2., 0., 10., "NaCl"
            "pijnstilling", "fentanyl", "mg", "microg/kg/uur", 0.5, 50., 1., 50., 2.5, 50., 2.5, 50., 1., 5., 10., 0., 0.05, "NaCl"
            "vasopressie", "fenylefrine", "mg", "microg/kg/min", 1.5, 50., 2.5, 50., 5., 50., 5., 50., 0.05, 5., 10., 0., 10., "NaCl"
            "diuretica", "furosemide", "mg", "mg/kg/dag", 10., 50., 20., 50., 40., 50., 100., 50., 1., 4., 6., 0., 10., "NaCl"
            "antistolling", "heparine", "IE", "IE/kg/uur", 5000., 50., 10000., 50., 20000., 50., 20000., 50., 10., 20., 50., 0., 1000., "NaCl"
            "glucose regulatie", "insuline", "IE", "IE/kg/uur", 10., 50., 10., 50., 50., 50., 50., 50., 0.02, 0.125, 2., 0., 1., "NaCl"
            "chronotropie", "isoprenaline", "mg", "microg/kg/min", 2., 50., 2., 50., 2., 50., 2., 50., 0.01, 1.5, 3., 0., 1., "NaCl"
            "antihypertensiva", "labetalol", "mg", "mg/kg/uur", 250., 50., 250., 50., 250., 50., 250., 50., 0.25, 3., 4., 0., 5., "NaCl"
            "bronchodilatie", "magnesiumsulfaat", "mg", "mg/kg/uur", 500., 50., 1000., 50., 2000., 50., 2000., 50., 3., 20., 25., 1., 160., "NaCl"
            "sedatie", "midazolam", "mg", "mg/kg/uur", 25., 50., 50., 50., 50., 50., 100., 50., 0.05, 0.5, 1., 0., 5., "NaCl"
            "inotropie", "milrinone", "mg", "microg/kg/min", 5., 50., 10., 50., 20., 50., 20., 50., 0.15, 0.5, 0.75, 0., 1., "NaCl"
            "pijnstilling", "morfine", "mg", "mg/kg/dag", 2., 50., 5., 50., 10., 50., 50., 50., 0.1, 0.5, 1., 0., 1., "NaCl"
            "suppletie", "NaCl 2,9%", "mmol", "mmol/kg/dag", 25., 50., 25., 50., 25., 50., 25., 50., 2., 4., 6., 0., 0., ""
            "antihypertensiva", "nitroprusside", "mg", "microg/kg/min", 10., 50., 20., 50., 40., 50., 40., 50., 0.5, 8., 10., 0., 10., "NaCl"
            "vasopressie", "noradrenaline", "mg", "microg/kg/min", 1., 50., 2., 50., 5., 50., 5., 50., 0.05, 0.5, 1., 0., 1., "NaCl"
            "sedatie", "propofol 1%", "mg", "mg/kg/uur", 10., 1., 10., 1., 10., 1., 10., 1., 1., 4., 4., 0., 0., ""
            "sedatie", "propofol 2%", "mg", "mg/kg/uur", 20., 1., 20., 1., 20., 1., 20., 1., 1., 4., 4., 0., 0., ""
            "verslapping", "rocuronium", "mg", "mg/kg/uur", 50., 50., 100., 50., 200., 50., 200., 50., 0.6, 1.2, 2., 0., 10., "NaCl"
            "bronchodilatie", "salbutamol", "mg", "microg/kg/min", 5., 50., 10., 50., 20., 50., 20., 50., 0.1, 10., 15., 0.005, 0.42, "NaCl"
            "sedatie", "thiopental", "mg", "mg/kg/uur", 1250., 50., 1250., 50., 1250., 50., 1250., 50., 5., 10., 20., 5., 25., "NaCl"
        ]

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

        contMeds
        |> List.map createContMed
        |> List.sortBy (fun med -> med.Indication, med.Generic)
        |> List.map (fun med ->
            [ 
                med.Indication
                med.Generic
                ((med |> qty |> string) + " " + med.Unit)
                ((med |> vol |> string ) + " ml")
                med.Solution
                (calcDose (med |> qty) (med |> vol) med.Unit med.DoseUnit) 
                (printAdv med.MinDose med.MaxDose med.DoseUnit)
            ]
        )