// Tool script: builds the ZIndex cache files and the demo cache files.
//
// The cache files (data/cache/*.cache) hold every product, ATC group, dose rule and
// substance of the G-Standaard. The demo files (data/cache/*.demo) hold the products,
// ATC groups and dose rules of the generic products in the demo GPK list, and every
// substance. The demo GPK list is the fixed list below, which the test scenarios use,
// together with every medication that has a dilution rule in the DilutionRules sheet.
//
// A DilutionRules row names a medication by Generic, Form and Route. The Formulary
// sheet maps a Generic to GPKs. A GPK is used when the generic product in the cache
// has the route of the rule and, when the rule names a form, has that form as well.
// A Formulary row with a GPKODE that is not a number, such as "NaCl 0,9%" or
// "gluc 5%", is a parenteral product defined in the sheet itself and needs no
// G-Standaard product, so the solutions of a rule need no GPK either.
//
// Needs a build (dotnet run build) and the G-Standaard files in data/zindex.
// Run from this directory:
//
//   dotnet fsi CreateCache.fsx           runs the tests, then builds the cache and the demo files
//   dotnet fsi CreateCache.fsx --cache   runs the tests, then builds only the cache files
//   dotnet fsi CreateCache.fsx --demo    runs the tests, then builds only the demo files
//                                        from the existing cache files
//   dotnet fsi CreateCache.fsx --test    runs only the tests
//
// The spreadsheet is the one in GENPRES_URL_ID, or the default spreadsheet below
// when that variable is not set.


#I __SOURCE_DIRECTORY__


System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


#load "load.fsx"


open System
open Expecto
open Expecto.Flip
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib


/// The spreadsheet with the DilutionRules and Formulary sheets, used when GENPRES_URL_ID is not set.
let defaultUrlId = "1StESB1RJTOiLOglvGeziC784JQwPkc3Sm98wrjId6rI"


/// The spreadsheet id from GENPRES_URL_ID, or the default spreadsheet when the variable is not set.
let dataUrlId () =
    match Env.getItem "GENPRES_URL_ID" with
    | Some id when id |> String.notEmpty -> id
    | _ -> defaultUrlId


/// A medication named by a row of the DilutionRules sheet.
type DilutionMedication =
    {
        /// The generic name, as in the Generic column.
        Generic: string
        /// The pharmaceutical form; empty when the rule applies to every form.
        Form: string
        /// The route of administration.
        Route: string
    }


/// A row of the Formulary sheet that refers to a G-Standaard generic product.
type FormularyGpk =
    {
        /// The generic product code from the GPKODE column.
        Gpk: int
        /// The generic name from the Generic column.
        Name: string
    }


/// The form and the routes of a generic product in the cache.
type CacheProduct =
    {
        /// The generic product code.
        Id: int
        /// The pharmaceutical form.
        ProductForm: string
        /// The routes of administration.
        Routes: string[]
    }


module SheetData =

    /// Maps every data row of a sheet with a header row, or returns no rows for an empty sheet.
    let mapRows f (data: string[][]) =
        if data |> Array.isEmpty then
            [||]
        else
            let getColumn = data |> Array.head |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun row ->
                let get = getColumn row
                f (fun col -> get col |> String.trim)
            )


    /// The distinct medications in the DilutionRules sheet, without the rows that have no Generic.
    let parseDilutionMedications data =
        data
        |> mapRows (fun get ->
            {
                Generic = get "Generic"
                Form = get "Form"
                Route = get "Route"
            }
        )
        |> Array.filter (_.Generic >> String.notEmpty)
        |> Array.distinct


    /// The Formulary rows with a numeric GPKODE; a row with another GPKODE is not a G-Standaard product.
    let parseFormularyGpks data =
        data
        |> mapRows (fun get ->
            get "GPKODE"
            |> Int32.tryParse
            |> Option.map (fun gpk -> { Gpk = gpk; Name = get "Generic" })
        )
        |> Array.choose id


module DemoSelection =

    let private equalsText s1 s2 = String.equalsCapInsens s1 s2


    /// Whether a cache product has the route of the medication and, when the medication names a form, that form.
    let matchesMedication med prod =
        (med.Form |> String.isNullOrWhiteSpace || prod.ProductForm |> equalsText med.Form)
        && prod.Routes |> Array.exists (equalsText med.Route)


    /// The sorted GPKs of each medication: the Formulary GPKs with the same generic name
    /// whose cache product matches the medication.
    let gpksPerMedication (tryFindProduct: int -> CacheProduct option) formulary meds =
        meds
        |> Array.map (fun med ->
            let gpks =
                formulary
                |> Array.filter (fun f -> f.Name |> equalsText med.Generic)
                |> Array.choose (fun f -> f.Gpk |> tryFindProduct)
                |> Array.filter (matchesMedication med)
                |> Array.map _.Id
                |> Array.distinct
                |> Array.sort

            med, gpks
        )


    /// The distinct, sorted GPKs of all medications.
    let selectGpks tryFindProduct formulary meds =
        gpksPerMedication tryFindProduct formulary meds
        |> Array.collect snd
        |> Array.distinct
        |> Array.sort


    /// The medications without a GPK.
    let unmatched tryFindProduct formulary meds =
        gpksPerMedication tryFindProduct formulary meds
        |> Array.filter (snd >> Array.isEmpty)
        |> Array.map fst


    /// The fixed GPKs and the dilution rule GPKs together, distinct and sorted.
    let demoGpks fixedGpks dilutionGpks =
        dilutionGpks
        |> Array.toList
        |> List.append fixedGpks
        |> List.distinct
        |> List.sort


/// The GPKs of the demo that the test scenarios use.
let scenarioGpks =
    [
        1465 // clemastine
        698 // rifampicine
        1112 // hydrocortison
        1961 // diazepam
        2194 // paracetamol
        3387 // cotrimoxazol
        3492 // metronidazol
        3689 // gentamicine
        4766 // tetracycline
        5533 // clonidine
        5886 // amiodaron
        6238 // digoxine
        6262 // furosemide
        6629 // bupivacaine
        7552 // xylometazoline
        8567 // methylprednisolon
        8710 // kaliumchloride
        9504 // paracetamol
        10529 // cotrimoxazol
        10995 // furosemide
        11509 // alfacalcidol
        11770 // spironolacton
        12009 // spironolacton
        12653 // cotrimoxazol
        12661 // cotrimoxazol
        12688 // cotrimoxazol
        13439 // clonidine
        13714 // amoxicilline
        13730 // amoxicilline
        13749 // amoxicilline
        13757 // amoxicilline
        14281 // dexamethason
        14494 // prednisolon
        15113 // erytromycine
        15393 // metronidazol
        15423 // amoxicilline
        15652 // amfotericine-b
        15741 // gentamicine
        16438 // dopamine
        16721 // digoxine
        16764 // digoxine
        16772 // digoxine
        16802 // furosemide
        16810 // furosemide
        17353 // bupivacaine
        18546 // dexamethason
        18643 // xylometazoline
        18651 // xylometazoline
        18872 // benzylpenicilline
        18880 // benzylpenicilline
        19046 // fenylefrine
        19054 // fenylefrine
        19518 // estradiol
        19739 // rifampicine
        19747 // rifampicine
        20303 // acenocoumarol
        20591 // diazepam
        20605 // diazepam
        20656 // diazepam
        20664 // diazepam
        20672 // diazepam
        20974 // clemastine
        21792 // prednisolon
        22098 // salbutamol
        22268 // diclofenac
        22276 // diclofenac
        22284 // diclofenac
        22292 // diclofenac
        23302 // hydrocortison
        23671 // fenobarbital
        23728 // fenobarbital
        23809 // prednisolon
        26646 // hydrocortison
        28487 // atropine
        28746 // morfine
        30309 // kaliumchloride
        31224 // atropine
        31410 // prednisolon
        36331 // alfacalcidol
        36722 // salbutamol
        37044 // xylometazoline
        37176 // paracetamol
        37184 // paracetamol
        38504 // trimethoprim
        38520 // spironolacton
        38857 // digoxine
        39136 // trimethoprim
        39586 // trimethoprim
        40258 // diclofenac
        40266 // diclofenac
        40487 // metronidazol
        41033 // atropine
        42080 // ibuprofen
        43079 // amoxicilline/clavulaanzuur
        43095 // amoxicilline/clavulaanzuur
        43966 // paracetamol
        43974 // paracetamol
        45411 // alfacalcidol
        47929 // fenylefrine
        50423 // ibuprofen
        50997 // ibuprofen
        51047 // ceftriaxon
        51268 // furosemide
        51306 // gentamicine
        51829 // rifampicine
        53422 // ceftazidim
        53430 // ceftazidim
        53449 // ceftazidim
        53740 // tobramycine
        53813 // amoxicilline/clavulaanzuur
        53821 // amoxicilline/clavulaanzuur
        53856 // amoxicilline/clavulaanzuur
        53864 // amoxicilline/clavulaanzuur
        54259 // metronidazol
        54380 // midazolam
        54593 // erytromycine
        54607 // erytromycine
        54798 // amoxicilline/clavulaanzuur
        54976 // morfine
        54984 // morfine
        54992 // morfine
        56766 // diclofenac
        64548 // ringer/lactaat
        64718 // hydrocortison
        65366 // amiodaron
        65498 // alprostadil
        69159 // domperidon
        69280 // mupirocine
        70181 // algeldraat/magnesiumhydroxide
        70777 // methylprednisolon
        70785 // methylprednisolon
        72532 // ganciclovir
        73318 // erytromycine
        73342 // tobramycine
        74179 // domperidon
        74357 // vancomycine
        75124 // salbutamol
        75272 // salbutamol
        77348 // milrinon
        77712 // midazolam
        78514 // clonidine
        79251 // immunoglobuline-normaal
        79278 // immunoglobuline-normaal
        81159 // ibuprofen
        82422 // vancomycine
        83631 // furosemide
        83704 // salbutamol
        84131 // paracetamol
        84271 // dexamethason/framycetine/gramicidine
        85200 // ibuprofen
        85839 // alfacalcidol
        86649 // amikacine
        87041 // diclofenac
        87920 // epoprostenol
        88749 // noradrenaline
        90123 // piperacilline/tazobactam
        90131 // piperacilline/tazobactam
        92088 // ipratropium
        92096 // ipratropium
        92118 // ipratropium
        92126 // ipratropium
        92215 // granisetron
        92266 // amfotericine-b-liposomaal
        92762 // atropine
        96288 // azijnzuur/triamcinolonacetonide
        96482 // dexamethason
        97225 // ropivacaine
        97241 // ropivacaine
        97713 // dobutamine
        100315 // remifentanil
        100323 // remifentanil
        102024 // immunoglobuline-normaal
        102776 // prednisolon
        103764 // ciprofloxacine
        103772 // ciprofloxacine
        106542 // salmeterol/fluticason
        106550 // salmeterol/fluticason
        108251 // insuline-aspart
        110884 // propofol
        110892 // propofol
        111414 // dobutamine
        111562 // atovaquon/proguanil
        111872 // prednisolon
        112836 // salmeterol/fluticason
        112852 // diclofenac
        112968 // granisetron
        113565 // tobramycine
        114081 // epoetine-beta
        114111 // epoetine-beta
        114138 // epoetine-beta
        114510 // valganciclovir
        115800 // prednisolon
        117072 // amoxicilline
        117099 // amoxicilline
        117439 // diclofenac
        117579 // amoxicilline/clavulaanzuur
        117978 // ceftriaxon
        118176 // fenobarbital
        119091 // ceftriaxon
        120383 // colecalciferol
        121576 // coffeine
        121789 // dexamethason
        121967 // paracetamol
        122645 // morfine
        122653 // morfine
        122661 // morfine
        122718 // paracetamol
        124354 // paracetamol
        124370 // paracetamol
        124672 // immunoglobuline-normaal
        124710 // posaconazol
        124915 // dexamethason
        125008 // immunoglobuline-normaal
        125679 // ibuprofen
        126101 // salbutamol/ipratropium
        126128 // furosemide
        127280 // ethinylestradiol/levonorgestrel
        127299 // ethinylestradiol/levonorgestrel
        128562 // spironolacton
        130699 // bof/mazelen/rubellavaccin
        130796 // tocilizumab
        132683 // micafungine
        132691 // micafungine
        133779 // midazolam
        134643 // ropivacaine
        135402 // prednisolon
        136131 // prednisolon
        136255 // immunoglobuline-normaal
        136980 // tobramycine
        138436 // amylase/lipase/protease
        138452 // amylase/lipase/protease
        138460 // amylase/lipase/protease
        141933 // midazolam
        143014 // hydrocortison
        144290 // colecalciferol
        144916 // dexamethason
        144940 // furosemide
        145874 // midazolam
        146102 // hydrocortison
        149322 // cetomacrogolzalf
        149888 // coffeine
        150258 // tocilizumab
        151122 // posaconazol
        152234 // paracetamol
        152269 // fibrinogeen
        153478 // posaconazol
        160083 // noradrenaline
        162868 // benzylpenicilline
        164526 // valganciclovir
        165573 // abacavir
        167002 // ciprofloxacine
        167460 // ciprofloxacine
        167479 // ciprofloxacine
        167487 // ciprofloxacine
        170925 // adrenaline
        170933 // adrenaline
        170976 // adrenaline
        175552 // argipressine
        88641 // tramadol
        88668 // tramadol
        88692 // tramadol
        94625 // tramadol
        101761 // tramadol
        104213 // tramadol
        104221 // tramadol
        104248 // tramadol
        109959 // tramadol
        107166 // tramadol
        107174 // tramadol
        107182 // tramadol
        109738 // tramadol
        117498 // tramadol
        16918 // methotrexaat
        168459 // methotrexaat
        47996 // methotrexaat
        109568 // methotrexaat
        168440 // methotrexaat
        168505 // methotrexaat
        168513 // methotrexaat
        170453 // methotrexaat
        185884 // methotrexaat
        151084 // methotrexaat
        153133 // methotrexaat
        134627 // meropenem
        134635 // meropenem
        153877 // meropenem
        156329 // meropenem
        156337 // meropenem
        156345 // meropenem
        156353 // meropenem
        159913 // meropenem
        168734 // meropenem
        177997 // meropenem
        185159 // meropenem
        185175 // meropenem
        185728 // meropenem
        185736 // meropenem
        185744 // meropenem
        187518 // meropenem
        188042 // meropenem
        188468 // meropenem
        192279 // meropenem
        192694 // meropenem
        194301 // meropenem
    ]


let tests =
    let dilutionRules =
        [|
            [| "Generic"; "Form"; "Route"; "Diluents" |]
            [| "morfine"; ""; "INTRAVENEUS"; "NaCl 0,9%|gluc 5%" |]
            [| "morfine"; ""; "INTRAVENEUS"; "NaCl 0,9%" |]
            [| "dexamethason"; "injectievloeistof"; "INTRAVENEUS"; "" |]
            [| ""; ""; "INTRAVENEUS"; "" |]
        |]

    let formulary =
        [|
            [| "GPKODE"; "Type"; "Generic" |]
            [| "1"; "medication"; "MORFINE" |]
            [| "2"; "medication"; "MORFINE" |]
            [| "3"; "medication"; "DEXAMETHASON" |]
            [| "4"; "medication"; "DEXAMETHASON" |]
            [| "NaCl 0,9%"; "parenteral"; "NaCl 0,9%" |]
        |]

    let products =
        [
            { Id = 1; ProductForm = "INJECTIEVLOEISTOF"; Routes = [| "INTRAVENEUS"; "SUBCUTAAN" |] }
            { Id = 2; ProductForm = "DRANK"; Routes = [| "ORAAL" |] }
            { Id = 3; ProductForm = "INJECTIEVLOEISTOF"; Routes = [| "INTRAVENEUS" |] }
            { Id = 4; ProductForm = "TABLET"; Routes = [| "INTRAVENEUS" |] }
        ]
        |> List.map (fun p -> p.Id, p)
        |> Map.ofList

    let meds = dilutionRules |> SheetData.parseDilutionMedications
    let gpks = formulary |> SheetData.parseFormularyGpks

    testList "CreateCache" [
        testList "SheetData" [
            test "solution medications are distinct and have a generic name" {
                meds
                |> Array.map _.Generic
                |> Expect.equal "morfine once, dexamethason once" [| "morfine"; "dexamethason" |]
            }

            test "an empty sheet gives no rows" {
                [||]
                |> SheetData.parseDilutionMedications
                |> Expect.isEmpty "no medications"
            }

            test "formulary rows with a GPKODE that is not a number are skipped" {
                gpks
                |> Array.map _.Gpk
                |> Expect.equal "only the numeric GPKs" [| 1; 2; 3; 4 |]
            }
        ]

        testList "DemoSelection" [
            test "a rule without a form matches every form with the route" {
                let med = { Generic = "morfine"; Form = ""; Route = "intraveneus" }

                products[1]
                |> DemoSelection.matchesMedication med
                |> Expect.isTrue "the route is compared case insensitive"
            }

            test "a rule with a form matches only that form" {
                let med = { Generic = "dexamethason"; Form = "injectievloeistof"; Route = "INTRAVENEUS" }

                products[4]
                |> DemoSelection.matchesMedication med
                |> Expect.isFalse "a tablet is not an injectievloeistof"
            }

            test "the GPKs with the route and form of a rule are selected" {
                meds
                |> DemoSelection.selectGpks products.TryFind gpks
                |> Expect.equal "morfine iv and dexamethason injectievloeistof" [| 1; 3 |]
            }

            test "a medication without a matching GPK is reported" {
                [| { Generic = "doxapram"; Form = ""; Route = "INTRAVENEUS" } |]
                |> DemoSelection.unmatched products.TryFind gpks
                |> Array.map _.Generic
                |> Expect.equal "doxapram is not in the formulary" [| "doxapram" |]
            }

            test "the demo GPKs are the distinct sorted union" {
                [| 3; 1 |]
                |> DemoSelection.demoGpks [ 3; 2 ]
                |> Expect.equal "union" [ 1; 2; 3 ]
            }
        ]
    ]


module Build =

    let private setProd isProd =
        Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, (if isProd then "1" else "0"))


    /// Deletes the cache files and builds them from the G-Standaard files.
    let createCache () =
        setProd true
        Json.clearCache false

        printfn "Creating the cache files ..."
        GenPresProduct.load []
        ATCGroup.load ()
        DoseRule.load []
        Substance.load ()


    /// The products of the cache files, by GPK.
    let cacheProducts () =
        setProd true

        GenPresProduct.get []
        |> Array.collect _.GenericProducts
        |> Array.map (fun gp ->
            {
                Id = gp.Id
                ProductForm = gp.Form
                Routes = gp.Route
            }
        )
        |> Array.distinctBy _.Id
        |> Array.map (fun p -> p.Id, p)
        |> Map.ofArray


    /// The GPKs of the medication with a dilution rule, after printing the medications without a GPK.
    let dilutionRuleGpks () =
        let urlId = dataUrlId ()

        match
            Web.GoogleSheets.getCsvDataFromSheetSync urlId "DilutionRules",
            Web.GoogleSheets.getCsvDataFromSheetSync urlId "Formulary"
        with
        | Ok rules, Ok formulary ->
            let meds = rules |> SheetData.parseDilutionMedications
            let gpks = formulary |> SheetData.parseFormularyGpks
            let products = cacheProducts ()

            meds
            |> DemoSelection.unmatched products.TryFind gpks
            |> Array.iter (fun med -> printfn $"No GPK for: %s{med.Generic} %s{med.Form} %s{med.Route}")

            meds |> DemoSelection.selectGpks products.TryFind gpks
        | Error e, _
        | _, Error e -> invalidOp $"Cannot read the spreadsheet %s{urlId}: %A{e}"


    /// Deletes the demo files and builds them for the given GPKs.
    /// Calls the loaders that are not memoized, because the memoized ones keep the cache data of this session.
    let createDemo gpks =
        setProd false
        Json.clearCache true

        printfn $"Creating the demo files for %i{gpks |> List.length} GPKs ..."
        // the memoized get with a GPK list not used before reads the demo setting and creates the demo file
        GenPresProduct.get gpks |> ignore

        GenPresProduct.getGPKS gpks
        |> ATCGroup.parse
        |> Json.cache (FilePath.groupCache true)

        DoseRule._get gpks |> ignore
        Substance._get () |> ignore


let args = fsi.CommandLineArgs |> Array.tail |> Set.ofArray


if runTestsWithCLIArgs [] [||] tests <> 0 then
    invalidOp "The tests failed, no cache files are created"
elif args.Contains "--test" then
    ()
else
    let buildCache = args.Contains "--cache" || not (args.Contains "--demo")
    let buildDemo = args.Contains "--demo" || not (args.Contains "--cache")

    if buildCache then Build.createCache ()

    if buildDemo then
        let dilutionGpks = Build.dilutionRuleGpks ()
        printfn $"Found %i{dilutionGpks.Length} GPKs with a dilution rule"

        dilutionGpks
        |> DemoSelection.demoGpks scenarioGpks
        |> Build.createDemo
