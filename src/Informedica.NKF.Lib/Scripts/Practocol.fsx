
#time

#load "load.fsx"

#load "../Utils.fs"
#load "../OpenAI.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib


let medications =
    [
        "Abemaciclib", "ABEMACICLIB"
        "abiraterone acetate", "ABIRATERON"
        "antihemophilic factor", "FACTOR VIII"
        "Afatinib", "AFATINIB"
        "aldesleukin", "ALDESLEUKINE"
        "Alemtuzumab", "ALEMTUZUMAB"
        "Allopurinol", "ALLOPURINOL"
        "amsacrine", "AMSACRINE"
        "anastrozole", "ANASTROZOL"
        "avelumab", "AVELUMAB"
        "aprepitant", "APREPITANT"
        "aprepitant", "APREPITANT"
        "Arsenic trioxide", "ARSEENTRIOXIDE"
        "Asciminib", "ASCIMINIB"
        "Asparaginase", "ASPARAGINASE"
        "Asparaginase", "ASPARAGINASE"
        "Asparaginase", "ASPARAGINASE"
        "Atezolizumab", "ATEZOLIZUMAB"
        "Atezolizumab", "ATEZOLIZUMAB"
        "Antithymocyte Globulin (Rabbit)", "THYMOCYTENGLOBULINE"
        "Antithymocyte Globulin (horse)", "THYMOCYTENGLOBULINE"
        "Atropine", "ATROPINE"
        "Avelumab", "AVELUMAB"
        "Azacitidine", "AZACITIDINE"
        "Bempegaldesleukin", ""
        "Bendamustine", "BENDAMUSTINE"
        "penicillin G benzathine", "Benzathinebenzylpenicilline "
        "Bevacizumab", "BEVACIZUMAB"
        "Bleomycin", "bleomycine"
        "Blinatumomab", "BLINATUMOMAB"
        "Blinatumomab", "BLINATUMOMAB"
        "Blinatumomab", "BLINATUMOMAB"
        "Bortezomib", "BORTEZOMIB"
        "Bosutinib", "BOSUTINIB"
        "Brentuximab", "Brentuximab vedotine"
        "Brigatinib", "BRIGATINIB"
        "Busulfan", "BUSULFAN"
        "Cabazitaxel", "CABAZITAXEL"
        "Calcium gluconate", "Calciumgluconaat "
        "Calcium Gluconate", "Calciumgluconaat "
        "Capecitabine", "CAPECITABINE"
        "Caplacizumab", "CAPLACIZUMAB"
        "Carboplatin", "carboplatine"
        "Carfilzomib", "CARFILZOMIB"
        "Carmustine", "CARMUSTINE"
        "Cefixime", "CEFIXIM"
        "ceftibuten dihydrate", "CEFTIBUTEN"
        "Celecoxib", "CELECOXIB"
        "Ceralasertib", ""
        "Ceritinib", "CERITINIB"
        "Cetuximab", "CETUXIMAB"
        "Chlorambucil", ""
        "Chloormethine", "CHLOORMETHINE"
        "Chlorpromazine", "Chloorpromazine"
        "Cyclosporine", "Ciclosporine "
        "ciprofloxacin", "Ciprofloxacine "
        "Cisplatin", "Cisplatine "
        "Cisplatin", "Cisplatine "
        "Cladribine", "CLADRIBINE"
        "Clemastine", "CLEMASTINE"
        "Clobazam", "CLOBAZAM"
        "Clofarabine", "CLOFARABINE"
        "Co-trimoxazole", "trimethoprim/sulfamethoxazol"
        "cobimetinib", "COBIMETINIB"
        "Docetaxel", "DOCETAXEL"
        "Crizotinib", "CRIZOTINIB"
        "Cyclophosphamide", "Cyclofosfamide"
        "Cytarabine", "CYTARABINE"
        "Cytarabine", "CYTARABINE"
        "Dabrafenib", "DABRAFENIB"
        "Dacarbazine", "DACARBAZINE"
        "Dactinomycin", "Dactinomycine"
        "Daratumumab", "DARATUMUMAB"
        "Dasatinib", "DASATINIB"
        "Daunorubicin", "Daunorubicine"
        "Daunorubicin", "Daunorubicine"
        "Decitabine", "DECITABINE"
        "Desmopressin", "Desmopressine"
        "Dexamethasone", "DEXAMETHASON"
        "Dexrazoxane", "Dexrazoxaan"
        "Dexrazoxane", "Dexrazoxaan"
        "Dinutuximab", "Dinutuximab beta"
        "Dinutuximab", "Dinutuximab beta"
        "Diphenhydramine", ""
        "Docetaxel", "DOCETAXEL"
        "Dovitinib", ""
        "Doxorubicin", "Doxorubicine"
        "Doxorubicin liposomal", "Doxorubicine"
        "Durvalumab", "DURVALUMAB"
        "Eltrombopag", "ELTROMBOPAG"
        "Entinostat", ""
        "Entrectinib", "ENTRECTINIB"
        "Enzalutamide", "ENZALUTAMIDE"
        "Epacadostat", ""
        "Epirubicin", "Epirubicine"
        "Epratuzumab", ""
        "Eribulin", "Eribuline"
        "Erlotinib", "ERLOTINIB"
        "Etoposide", "ETOPOSIDE"
        "Etoposide", "ETOPOSIDE"
        "Everolimus", "EVEROLIMUS"
        "Exemestane", "Exemestaan"
        "Fenofibrate", ""
        "Phenytoin", "Fenytoine"
        "Ferric carboxymaltose", ""
        "Filanesib", ""
        "Filgrastim", "FILGRASTIM"
        "Fludarabine", "FLUDARABINE"
        "Fluorouracil", "FLUOROURACIL"
        "Leucovorin", "Folinezuur"
        "Leucovorin", "Folinezuur"
        "Forodesine", ""
        "Fosaprepitant", "APREPITANT, FOSAPREPITANT"
        "Fulvestrant", "FULVESTRANT"
        "Furosemide", "FUROSEMIDE"
        "Futibatinib", ""
        "Gefitinib", "GEFITINIB"
        "Gemcitabine", "GEMCITABINE"
        "Gemfibrozil", "GEMFIBROZIL"
        "Gemtuzumab ozogamicin", ""
        "Granisetron", "GRANISETRON"
        "Heparin", "Heparine"
        "Hydrocortisone", "HYDROCORTISON, CORTISON"
        "Hydroxycarbamide", "HYDROXYCARBAMIDE"
        "Hydroxycarbamide", "HYDROXYCARBAMIDE"
        "Ibrutinib", "IBRUTINIB"
        "Idarubicin", "Idarubicine"
        "Idasanutlin", ""
        "Ifosfamide", "IFOSFAMIDE"
        "Imatinib", "IMATINIB"
        "Normal immunoglobulin", "IMMUNOGLOBULINE, NORMAAL"
        "Normal immunoglobulin", "IMMUNOGLOBULINE, NORMAAL"
        "Normal immunoglobulin", "IMMUNOGLOBULINE, NORMAAL"
        "Normal immunoglobulin", "IMMUNOGLOBULINE, NORMAAL"
        "Normal immunoglobulin", "IMMUNOGLOBULINE, NORMAAL"
        "Indomethacin", "Indomethacine"
        "Inotuzumab", ""
        "Inotuzumab ozogamicin", ""
        "Interferon Gamma", "INTERFERON GAMMA 1B"
        "Ipatasertib", ""
        "Ipilimumab", "IPILIMUMAB"
        "Ipilimumab", "IPILIMUMAB"
        "Irinotecan", "IRINOTECAN"
        "Isatuximab", "ISATUXIMAB"
        "Isotretinoin", ""
        "Itraconazole", "ITRACONAZOL"
        "Ixazomib", "IXAZOMIB"
        "Talacotuzumab", ""
        "Lapatinib", "LAPATINIB"
        "Laromustine", ""
        "Larotrectinib", "LAROTRECTINIB"
        "Larotrectinib", "LAROTRECTINIB"
        "Lenalidomide", "LENALIDOMIDE"
        "Lenograstim", ""
        "elivaldogene autotemcel", ""
        "Lenvatinib", "LENVATINIB"
        "Letrozole", "LETROZOL"
        "WNT-974", ""
        "Lidocaine and prilocaine topical", "LIDOCAINE, PRILOCAINE"
        "Lipegfilgrastim", "FILGRASTIM, PEGFILGRASTIM, LIPEGFILGRASTIM"
        "cytarabine and daunorubicin (liposomal)", "CYTARABINE"
        "Lirilumab", ""
        "Lomustine", "LOMUSTINE"
        "Loperamide", "LOPERAMIDE"
        "Loratadine", "LORATADINE"
        "Lorazepam", "LORAZEPAM"
        "Luspatercept", "LUSPATERCEPT"
        "lutetium Lu 177 dotatate", ""
        "Macrogol", "MACROGOL"
        "Magnesium Sulfate", ""
        "Magrolimab", ""
        "Mannitol", "MANNITOL"
        "Masitinib", ""
        "melphalan", "Melfalan"
        "Melphalan flufenamide", ""
        "Mercaptopurine", "MERCAPTOPURINE"
        "mesna", "Mercapto-ethaansulfonzuur"
        "mesna", "MERCAPTO-ETHAANSULFONZUUR"
        "mesna", "MERCAPTO-ETHAANSULFONZUUR"
        "Methotrexate", "methotrexaat"
        "methylcellulose", "METHYLCELLULOSE"
        "Methylprednisolone", "METHYLPREDNISOLON, PREDNISOLON"
        "Mitomycin", "Mitomycine"
        "mitotane", ""
        "mitoxantrone", "MITOXANTRON"
        "ixazomib", "IXAZOMIB"
        "mogamulizumab", "MOGAMULIZUMAB"
        "momelotinib", ""
        "montelukast", "MONTELUKAST"
        "moxetumomab pasudotox", ""
        "Mycophenolate Mofetil Oral Suspension", "Mycofenolaatmofetil"
        "N-acetyl-L-cysteïne", "Acetylcysteine"
        "nanoparticle albumin-bound paclitaxel", "PACLITAXEL"
        "narsoplimab", ""
        "natalizumab", "NATALIZUMAB"
        "Sodium", ""
        "Sodium", ""
        "Sodium", ""
        "Sodium", ""
        "nelarabine", "NELARABINE"
        "nilotinib", "NILOTINIB"
        "niraparib", "NIRAPARIB"
        "nivolumab", "NIVOLUMAB"
        "obinutuzumab", "OBINUTUZUMAB"
        "ofatumumab", "OFATUMUMAB"
        "olaparib", "OLAPARIB"
        "Olaratumab", "OLARATUMAB"
        "ondansetron", "ONDANSETRON"
        "Oprozomib", ""
        "Orteronel", ""
        "oxaliplatin", ""
        "paclitaxel", "PACLITAXEL"
        "pacritinib", ""
        "palbociclib", "PALBOCICLIB"
        "pamidronate", ""
        "panitumumab", "PANITUMUMAB"
        "panobinostat", "PANOBINOSTAT"
        "paracetamol", "PARACETAMOL"
        "Paxalisib", ""
        "pazopanib", "PAZOPANIB"
        "pegaspargase", "PEGASPARGASE"
        "pegfilgrastim", "FILGRASTIM, PEGFILGRASTIM"
        "Peginterferon Alfa-2b", ""
        "pembrolizumab", "PEMBROLIZUMAB"
        "pembrolizumab", "PEMBROLIZUMAB"
        "pemetrexed", "PEMETREXED"
        "pentostatin", "pentostatine"
        "pertuzumab", "PERTUZUMAB"
        "Pimasertib", ""
        "pixantrone", "PIXANTRON"
        "plerixafor", "PLERIXAFOR"
        "pomalidomide", "POMALIDOMIDE"
        "ponatinib", "PONATINIB"
        "pravastatin", "pravastatine"
        "prednisolone", "PREDNISOLON"
        "procarbazine", "PROCARBAZINE"
        "quizartinib", ""
        "ramucirumab", "RAMUCIRUMAB"
        "ranitidine", "RANITIDINE"
        "rasburicase", "RASBURICASE"
        "regorafenib", "REGORAFENIB"
        "regorafenib", "REGORAFENIB"
        "Relatlimab", ""
        "ribociclib", "RIBOCICLIB"
        "Rifampicin", "Rifampicine"
        "rituximab", "RITUXIMAB"
        "rituximab", "RITUXIMAB"
        "ruxolitinib", "RUXOLITINIB"
        "sargramostim", ""
        "selinexor", ""
        "selumetinib", "SELUMETINIB"
        "Sevuparin", ""
        "siltuximab", "SILTUXIMAB"
        "sorafenib", "SORAFENIB"
        "Sotrastaurin", ""
        "streptozocin", ""
        "sunitinib", "SUNITINIB"
        "surufatinib", ""
        "tipiracil and trifluridine", ""
        "tazemetostat", ""
        "temozolomide", "TEMOZOLOMIDE"
        "temsirolimus", "SIROLIMUS, TEMSIROLIMUS"
        "Teniposide", "TENIPOSIDE"
        "thalidomide", "THALIDOMIDE"
        "thiotepa", "THIOTEPA"
        "thioguanine", ""
        "thioguanine", ""
        "tipifarnib", ""
        "tisagenlecleucel", "TISAGENLECLEUCEL"
        "topotecan", "TOPOTECAN"
        "Tosedostat", ""
        "trabectedin", "Trabectedine"
        "trametinib", "TRAMETINIB"
        "trastuzumab", "TRASTUZUMAB"
        "ado-trastuzumab emtansine", "TRASTUZUMAB, TRASTUZUMAB EMTANSINE"
        "tremelimumab", ""
        "treosulfan", "TREOSULFAN"
        "tretinoin topical", "Tretinoine"
        "Trofosfamide", ""
        "Tegafur-uracil", ""
        "Valproate", "valproinezuur"
        "Vanucizumab", ""
        "Veliparib", ""
        "vemurafenib", "VEMURAFENIB"
        "venetoclax", "VENETOCLAX"
        "iron sucrose", ""
        "vinblastine", "VINBLASTINE"
        "vincristine", "VINCRISTINE"
        "Vindesine", "VINDESINE"
        "Vinflunine", "VINFLUNINE"
        "vinorelbine", "VINORELBINE"
        "vismodegib", "VISMODEGIB"
        "Volasertib", ""
        "zoledronic acid", "Zoledroninezuur"
    ]


medications
|> List.map (fun (m, g) ->
    let gpps =
        GenPresProduct.get []
        |> Array.filter (fun gpp ->
            if  g |> (gpp.Name |> String.equalsCapInsens) then true
            else
                m
                |> (gpp.Name |> String.containsCapsInsens)
        )
        |> Array.distinctBy (fun gpp -> gpp.Name, gpp.Shape, gpp.Routes)

    m,
    gpps |> Array.map _.Name |> Array.distinct |> Array.tryExactlyOne |> Option.defaultValue "",
    gpps |> Array.map _.Shape |> Array.distinct |> Array.tryExactlyOne |> Option.defaultValue "",
    gpps |> Array.map _.Routes |> Array.distinct |> Array.tryExactlyOne |> Option.bind Array.tryExactlyOne |> Option.defaultValue ""

)
|> List.iter (fun (s, generic, shape, route) ->
    printfn $"{s}\t{generic |> String.toLower}\t{shape |> String.toLower}\t{route}"
)



"Alutard SQ"
|> GenPresProduct.findByBrand



medications
|> List.toArray
|> Array.map (fun (m, g) ->
    m,
    GenPresProduct.get []
    |> Array.filter (fun gpp ->
        m
        |> (gpp.Name |> String.containsCapsInsens)
        ||
        g
        |> (gpp.Name |> String.equalsCapInsens)
    )

)
|> Array.groupBy fst
|> Array.filter (fun (_, v) -> v |> Array.length = 1)
|> Array.collect snd
|> Array.collect snd
|> Array.distinct
|> Array.collect (fun gpp ->
    gpp.Routes
    |> Array.map (fun r ->
        $"{gpp.Name |> String.toLower}\t{gpp.Shape |> String.toLower}\t{r}"
    )
)
|> Array.distinct
|> Array.iter (printfn "%s")


medications
|> List.toArray
|> Array.map (fun (m, g) ->
    m,
    GenPresProduct.get []
    |> Array.filter (fun gpp ->
        m
        |> (gpp.Name |> String.containsCapsInsens)
        ||
        g
        |> (gpp.Name |> String.equalsCapInsens)
    )

)
|> Array.groupBy fst
|> Array.filter (fun (_, v) -> v |> Array.length = 1)
|> Array.collect snd
|> Array.collect snd
|> Array.distinct
|> Array.collect (fun gpp ->
    gpp.GenericProducts
    |> Array.map _.Id
    |> Array.map string
)
|> Array.distinct
|> Array.iter (printfn "%s\t\t\t\t\t\tx")


let reconstitute =
    [
        "factor viii", "poeder voor injectievloeistof", "INTRAVENEUS"
        "aldesleukine", "poeder voor injectie/infusieoplossing", "INTRAVENEUS"
        "allopurinol", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "bendamustine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "bleomycine", "poeder voor injectievloeistof", "INTRAVENEUS"
        "bortezomib", "poeder voor injectievloeistof", "INTRAVENEUS"
        "caplacizumab", "poeder voor injectievloeistof", "INTRAVENEUS"
        "carfilzomib", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "carmustine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "cyclofosfamide", "poeder voor injectievloeistof", "INTRAVENEUS"
        "dacarbazine", "poeder voor injectievloeistof", "INTRAVENEUS"
        "dacarbazine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "dactinomycine", "poeder voor injectievloeistof", "INTRAVENEUS"
        "decitabine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "fosaprepitant", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "gemcitabine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "hydrocortison", "poeder voor injectievloeistof", "INTRAVENEUS"
        "ifosfamide", "poeder voor injectievloeistof", "INTRAVENEUS"
        "methylprednisolon", "poeder voor injectievloeistof", "INTRAVENEUS"
        "prednisolon", "poeder voor injectievloeistof", "INTRAVENEUS"
        "methylprednisolon", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "mitomycine", "poeder voor injectie/infusieoplossing", "INTRAVENEUS"
        "mitomycine", "poeder voor injectie/infusieoplossing", "INTRAVESICAAL"
        "mitomycine", "poeder voor oplossing voor intravesicaal gebruik", "INTRAVESICAAL"
        "paclitaxel", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "pegaspargase", "poeder voor injectie/infusieoplossing", "INTRAVENEUS"
        "pemetrexed", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "rasburicase", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "rifampicine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "siltuximab", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "thiotepa", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "topotecan", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "trabectedine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "trastuzumab", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "trastuzumab emtansine", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "treosulfan", "poeder voor oplossing voor infusie", "INTRAVENEUS"
        "valproinezuur", "poeder voor injectievloeistof", "INTRAVENEUS"
        "vindesine", "poeder voor injectievloeistof", "INTRAVENEUS"
    ]


reconstitute
|> List.toArray
|> Array.collect (fun (g, s, r) ->
    GenPresProduct.filter g s r
    |> Array.collect (fun gpp ->
        gpp.GenericProducts
        |> Array.map (fun gp ->
            {|
                gpk = gp.Id
                label = gp.Label
                shape = s
                route = r
            |}
        )
    )
)
|> Array.distinct
|> Array.iter (fun r ->
    printfn $"{r.gpk}\t{r.label}\t{r.shape}\t{r.route}"
)


GenPresProduct.get []
|> Array.filter (fun gpp -> gpp.Name |> String.equalsCapInsens "allopurinol")
|> Array.map (fun gpp -> gpp.Name, gpp.Shape, gpp.Routes)


let solutions =
    [
        "alemtuzumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "amsacrine", "", "INTRAVENEUS", "Glucose 5%", "mg/ml", "0,4"
        "avelumab", "concentraat voor oplossing voor infusie", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "arseentrioxide", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "asparaginase", "", "", "Glucose 5%", "eenheid/ml", "4000"
        "asparaginase", "", "", "NaCl 0,9%", "eenheid/ml", "4000"
        "asparaginase", "", "", "NaCl 0,9%", "", ""
        "atezolizumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "atezolizumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "", "", "", "NaCl 0,9%", "", "0,5"
        "", "", "", "NaCl 0,9%", "", "4"
        "azacitidine", "", "", "NaCl 0,9%", "", "4"
        "", "", "", "NaCl 0,9%", "", ""
        "bendamustine", "", "INTRAVENEUS", "NaCl 0,9%", "", "0,6"
        "bevacizumab", "", "", "NaCl 0.9%", "", "16,5"
        "bleomycine", "", "", "NaCl 0.9%", "IU", "3"
        "blinatumomab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "blinatumomab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "blinatumomab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "bortezomib", "", "", "NaCl 0.9%", "", ""
        "", "", "", "NaCl 0,9%", "", "1,2"
        "busulfan", "", "", "NaCl 0,9%", "", "0,5"
        "cabazitaxel", "", "INTRAVENEUS", "NaCl 0,9%", "", "0,26"
        "carboplatine", "", "INTRAVENEUS", "NaCl 0.9%", "mg/ml", "5"
        "carfilzomib", "", "INTRAVENEUS", "Glucose 5%", "", ""
        "carmustine", "poeder voor oplossing voor infusie", "INTRAVENEUS", "NaCl 0.9%", "", "1"
        "cetuximab", "", "INTRAVENEUS", "NaCl 0.9%", "", "2"
        "chloormethine", "gel", "CUTAAN", "NaCl 0,9%", "", ""
        "chloorpromazine", "", "", "NaCl 0.9%", "", ""
        "ciclosporine", "", "", "NaCl 0,9%", "", ""
        "cisplatine", "", "INTRAVENEUS", "NaCl 0.9%", "mg/ml", "0,5"
        "cisplatine", "", "INTRAVENEUS", "NaCl 3%", "", ""
        "cladribine", "", "", "NaCl 0,9%", "", ""
        "clemastine", "", "", "NaCl 0.9%", "", ""
        "clofarabine", "", "INTRAVENEUS", "NaCl 0.9%", "", "0,75"
        "docetaxel", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "cyclofosfamide", "", "", "NaCl 0,9%", "mg/ml", "10"
        "cytarabine", "", "", "NaCl 0,9%", "mg/ml", "50"
        "dacarbazine", "", "INTRAVENEUS", "NaCl 0,9%", "mg/ml", "4"
        "dactinomycine", "poeder voor injectievloeistof", "INTRAVENEUS", "NaCl 0.9%", "", ""
        "daratumumab", "", "", "NaCl 0,9%", "", ""
        "daunorubicine", "", "INTRAVENEUS", "NaCl 0,9%", "mg/ml", "2,5"
        "daunorubicine", "", "INTRAVENEUS", "Glucose 5%", "", "1"
        "decitabine", "", "INTRAVENEUS", "NaCl 0,9%", "", "1"
        "dexamethason", "", "", "NaCl 0.9%", "", ""
        "docetaxel", "", "INTRAVENEUS", "NaCl 0.9%", "", "0,74"
        "doxorubicine", "", "", "NaCl 0,9%", "mg/ml", "1"
        "doxorubicine", "", "", "Glucose 5%", "", "356"
        "durvalumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "epirubicine", "", "", "NaCl 0,9%", "mg/ml", "1"
        "eribuline", "", "INTRAVENEUS", "NaCl 0,9%", "", "0,18"
        "etoposide", "", "", "NaCl 0,9%", "mg/ml", "0,4"
        "etoposide", "", "", "NaCl 0.9%", "", "11,4"
        "filgrastim", "", "", "Glucose 5%", "", ""
        "fludarabine", "", "", "NaCl 0.9%", "", ""
        "fluorouracil", "", "", "NaCl 0,9%", "", "25"
        "folinezuur", "", "", "NaCl 0,9%", "", "10"
        "folinezuur", "", "", "Glucose 5%", "", "10"
        "", "", "", "NaCl 0,9%", "", ""
        "gemcitabine", "", "INTRAVENEUS", "NaCl 0.9%", "", "19"
        "", "", "", "NaCl 0.9%", "", ""
        "granisetron", "", "", "NaCl 0,9%", "", ""
        "idarubicine", "", "INTRAVENEUS", "NaCl 0,9%", "mg/ml", "0,5"
        "ifosfamide", "", "INTRAVENEUS", "NaCl 0,9%", "mg/ml", "10"
        "", "", "", "NaCl 0,9%", "", ""
        "", "", "", "NaCl 0,9%", "", ""
        "ipilimumab", "", "INTRAVENEUS", "NaCl 0,9%", "", "4"
        "ipilimumab", "", "INTRAVENEUS", "NaCl 0,9%", "", "1"
        "irinotecan", "", "INTRAVENEUS", "NaCl 0.9%", "", "2,8"
        "", "", "", "Glucose 5%", "", ""
        "cytarabine", "", "", "NaCl 0,9%", "", "1,32"
        "", "", "", "NaCl 0,9%", "", ""
        "melfalan", "", "", "NaCl 0,9%", "", ""
        "mercapto-ethaansulfonzuur", "", "", "NaCl 0,9%", "", "20"
        "mercapto-ethaansulfonzuur", "", "", "NaCl 0.9%", "", ""
        "methotrexaat", "", "", "NaCl 0,9%", "mg/ml", "20"
        "", "", "", "NaCl 0,9%", "", ""
        "mitomycine", "", "", "NaCl 0.9%", "", ""
        "mitoxantron", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "paclitaxel", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "", "", "", "NaCl 0,9%", "", ""
        "natalizumab", "", "", "NaCl 0,9%", "", ""
        "nivolumab", "", "INTRAVENEUS", "NaCl 0,9%", "", "10"
        "obinutuzumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "olaratumab", "infuus", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "", "", "", "Glucose 5%", "", "2"
        "paclitaxel", "", "INTRAVENEUS", "NaCl 0.9%", "mg/ml", "1,2"
        "", "", "", "NaCl 0.9%", "", ""
        "panitumumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "pegaspargase", "", "", "NaCl 0,9%", "eenheid/ml", ""
        "", "", "", "Glucose 5%", "", ""
        "pembrolizumab", "", "INTRAVENEUS", "NaCl 0,9%", "", "10"
        "pembrolizumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "pemetrexed", "", "INTRAVENEUS", "NaCl 0.9%", "", ""
        "", "", "", "NaCl 0,9%", "", ""
        "pertuzumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "prednisolon", "", "", "NaCl 0,9%", "", ""
        "ramucirumab", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "ranitidine", "", "", "NaCl 0.9%", "", "2,5"
        "", "", "", "NaCl 0,9%", "", ""
        "rituximab", "", "", "NaCl 0.9%", "", "4"
        "rituximab", "", "", "NaCl 0.9%", "", ""
        "siltuximab", "", "INTRAVENEUS", "Glucose 5%", "", ""
        "", "", "", "NaCl 0,9%", "", "20"
        "", "", "", "NaCl 0,9%", "", ""
        "teniposide", "infuus", "INTRAVENEUS", "NaCl 0,9%", "mg/ml", "0,6"
        "thiotepa", "", "INTRAVENEUS", "NaCl 0,9%", "mg/ml", "1"
        "topotecan", "", "", "NaCl 0,9%", "mg/ml", "0,05"
        "trabectedine", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "trastuzumab", "", "", "NaCl 0.9%", "", ""
        "", "", "", "NaCl 0,9%", "", ""
        "", "", "", "NaCl 0,9%", "", ""
        "treosulfan", "", "INTRAVENEUS", "NaCl 0,9%", "", "50"
        "vinblastine", "", "INTRAVENEUS", "NaCl 0.9%", "", ""
        "vincristine", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "vindesine", "poeder voor injectievloeistof", "INTRAVENEUS", "NaCl 0.9%", "", ""
        "vinflunine", "infuus", "INTRAVENEUS", "NaCl 0,9%", "", ""
        "vinorelbine", "", "", "NaCl 0.9%", "", "3"
        "", "", "", "NaCl 0,9%", "", "1"
        "zoledroninezuur", "", "INTRAVENEUS", "NaCl 0,9%", "", ""
    ]



solutions
|> List.toArray
|> Array.filter (fun (g, _, _, _, _, _) -> g |> String.notEmpty)
|> Array.map (fun (gen, shp, rte, sol, unt, max) ->
    {|
        generic = gen
        shape = shp
        route = rte
        solutions = sol
        unit = unt
        maxConc = max
    |}
)
|> Array.collect (fun r ->
    let route = "INTRAVENEUS"
    GenPresProduct.filter r.generic r.shape route
    |> Array.map (fun gpp ->
        {|
            generic = r.generic
            shape = gpp.Shape |> String.toLower
            route = route
            solutions = r.solutions
            unit = r.unit
            maxConc = r.maxConc
        |}
    )
)
|> Array.distinct
|> Array.iter (fun r ->
    [
        r.generic
        r.shape
        r.route
        r.solutions
        r.unit
        r.maxConc
    ]
    |> String.concat "\t"
    |> printfn "%s"
)


GenPresProduct.filter "insuline-gewoon" "" ""
|> Array.map _.Shape
|> Array.distinct
|> Array.map String.toLower


GenPresProduct.get []
|> Array.filter (fun gpp ->
    gpp.Name |> String.containsCapsInsens "IJZER(III)"
)
|> Array.collect _.GenericProducts
|> Array.map _.Name
|> Array.distinct
|> Array.iter (printfn "%s")


GenPresProduct.getRoutes ()
|> Array.iter (printfn "%s")


GenPresProduct.filter "" "" "INTRATHECAAL"
|> Array.map _.Name
|> Array.distinct
|> Array.iter (printfn "%s")


GenPresProduct.filter "bcg-vaccin" "" ""
|> Array.collect _.Routes
|> Array.distinct