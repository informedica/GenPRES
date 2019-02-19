namespace Pages

module EmergencyList =

    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props

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
            ( "antidota", "naloxon", 0.01, 0., 0.5, 0.02, "mg", "" )
            ( "antidota", "intralipid 20%", 1.5, 0., 0., 1., "ml", "" )
            ( "antidota", "fenytoine", 5., 0., 1500., 50., "mg", "" )
            ( "hyperkaliemie", "resonium klysma", 1., 0., 0., 0.15, "gram", "" )
            ( "hyperkaliemie", "furosemide", 1., 0., 0., 10., "mg", "" )
            ( "hyperkaliemie", "NaBic 8,4%", 2.5, 0., 0., 1., "mmol", "" )
            ( "hyperkaliemie", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mg", "" )
            ( "anticonvulsiva", "diazepam rect", 0.5, 0., 0., 4., "mg", "" )
            ( "anticonvulsiva", "diazepam iv", 0.25, 0., 0., 5., "mg", "" )
            ( "anticonvulsiva", "fenytoine", 20., 0., 1500., 50., "mg", "" )
            ( "anticonvulsiva", "midazolam", 0.1, 0., 10., 5., "mg", "" )
            ( "anticonvulsiva", "midazolam buc/nas/im", 0.2, 0., 10., 5., "mg", "" )
            ( "anticonvulsiva", "thiopental", 5., 0., 0., 25., "mg", "" )
            ( "hersenoedeem", "mannitol 15%", 0.5, 0., 50., 0.15, "gram", "" )
            ( "hersenoedeem", "NaCl 2,9%", 3., 0., 0., 1., "ml", "" )
            ( "pijnstilling", "paracetamol", 20., 0., 100., 10., "mg", "" )
            ( "pijnstilling", "diclofenac", 1., 0., 75., 25., "mg", "" )
            ( "pijnstilling", "fentanyl", 1., 0., 0., 50., "microg", "" )
            ( "pijnstilling", "morfine", 0.1, 0., 0., 1., "mg", "" )
            ( "anti-emetica", "dexamethason", 0.1, 0., 8., 4., "mg", "" )
            ( "anti-emetica", "ondansetron", 0.1, 0., 4., 2., "mg", "" )
            ( "anti-emetica", "droperidol", 0.02, 0., 1.25, 2.5, "mg", "" )
            ( "anti-emetica", "metoclopramide", 0.1, 0., 10., 5., "mg", "" )
            ( "elektrolyten", "kaliumchloride 7,4%", 0.5, 0., 40., 1.0, "mmol", "" )
            ( "elektrolyten", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mmol", "" )
            ( "elektrolyten", "magnesiumchloride 10%", 0.08, 0., 0., 0.5, "mmol", "" )
            ( "lokaal anesthesie", "licocaine 1%", 5., 0., 200., 10., "mg", "" )
            ( "lokaal anesthesie", "licocaine 2%", 5., 0., 200., 20., "mg", "" )
            ( "lokaal anesthesie", "bupivacaine", 3., 0., 0., 2.5, "mg", "" )
            ( "lokaal anesthesie", "bupivacaine/adrenaline", 3., 0., 0., 2.5, "mg", "" )
        ]

    let treatment age wght =
        Domain.EmergencyTreatment.getTableData age wght joules medicationDefs

    let createHead items =
        tableHead []
          [ tableRow [] (items |> List.map (fun i -> tableCell [] [ str i ]))]  

    let createTableBody rows =
        tableBody []
            ( rows
              |> List.mapi (fun i row ->
                    tableRow
                        [ TableRowProp.Hover true
                          Style [ if i%2 = 0 then yield CSSProp.BackgroundColor Fable.MaterialUI.Colors.grey.``100`` ] ]
                        (row |> List.map (fun cell -> tableCell [] [ str cell ] ) ) ) )
            
        
    let view age wght dispatch =
        
        let head =
            [ "Indicatie"
              "Interventie"
              "Berekend"
              "Bereiding"
              "Advies"
            ] |> createHead
        let body =
            treatment age wght |> createTableBody
        table [] [ head; body ]