namespace Data

module Treatment =

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
            ( "antidota", "flumazine", 0.02, 0., 0.3, 0.1, "mg", "" )
            ( "antidota", "naloxon", 0.01, 0., 0.5, 0.02, "mg", "" )
            ( "elektrolyten", "kaliumchloride 7,4%", 0.5, 0., 40., 1.0, "mmol", "" )
            ( "elektrolyten", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mmol", "" )
            ( "elektrolyten", "magnesiumchloride 10%", 0.08, 0., 0., 0.5, "mmol", "" )
            ( "antiarrythmica", "adenosine 1e gift", 100., 0., 12000., 3000., "microg", "" )
            ( "antiarrythmica", "adenosine 2e gift", 200., 0., 12000., 3000., "microg", "" )
            ( "antiarrythmica", "adenosine 3e gift", 300., 0., 12000., 3000., "microg", "" )            
            ( "antiarrythmica", "amiodarone", 5., 0., 300., 50., "mg", "" )
            ( "anticonvulsiva", "diazepam", 0.5, 0., 10., 2., "mg", "" )
            ( "anticonvulsiva", "fenytoine", 20., 0., 1500., 50., "mg", "" )
            ( "anticonvulsiva", "midazolam", 0.1, 0., 10., 5., "mg", "" )
            ( "astma", "prednisolon", 2., 0., 25., 12.5, "mg", "" )
            ( "astma", "magnesiumsulfaat 16%", 40., 0., 2000., 160., "mg", "" )
            ( "astma", "magnesiumsulfaat 50%", 40., 0., 2000., 500., "mg", "" )
            ( "astma", "salbutamol oplaad", 15., 0., 0., 500., "microg", "" )
            ( "hersenoedeem", "mannitol 15%", 0.5, 0., 50., 0.15, "gram", "" )
            ( "lokaal anesthesie", "licocaine 1%", 5., 0., 200., 10., "mg", "" )
            ( "lokaal anesthesie", "licocaine 2%", 5., 0., 200., 20., "mg", "" )
        ]        