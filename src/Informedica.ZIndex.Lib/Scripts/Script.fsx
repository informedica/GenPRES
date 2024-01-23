
#load "load.fsx"

#time

open System

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib

Environment.CurrentDirectory

// File
File.exists <| FilePath.GStandPath + "BST000T"

// Check the product cache
FilePath.productCache false
|> File.exists

Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "1")
FilePath.useDemo()

// Clear the cache
Json.clearCache (FilePath.useDemo ())

// Load all
printfn "Loading GenPresProduct ..."
GenPresProduct.load []
printfn "Loading ATCGroup ..."
ATCGroup.load ()
printfn "Loading DoseRule ..."
DoseRule.load []
printfn "Loading Substance"
Substance.load ()


GenPresProduct.getRoutes ()
|> Array.sortBy String.toLower
|> Array.iter (printfn "%s")


DoseRule.routes ()
|> Array.sortBy String.toLower
|> Array.iter (printfn "%s")


// load demo cache
Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "0")
FilePath.useDemo()

// Check the demo cache
FilePath.productCache (FilePath.useDemo ())
|> File.exists
Json.clearCache (FilePath.useDemo ())

// Load demo

let gpks =
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

printfn "Loading GenPresProduct ..."
GenPresProduct.load gpks
printfn "Loading ATCGroup ..."
ATCGroup.load ()
printfn "Loading DoseRule ..."
DoseRule.load gpks
printfn "Loading Substance"
Substance.load ()


GenPresProduct.filter "amfotericine b" "" ""
|> Array.collect (_.GenericProducts)
|> Array.map (fun gp ->
    gp.Id, gp.Shape,
    gp.Substances[0].SubstanceName,
    gp.Substances[0].SubstanceQuantity
)
|> Array.iter (fun (id, lbl, sn, sq) ->
    printfn $"{id}\t{lbl}\t{sn}\t{sq}"
)


GenPresProduct.filter "" "drank" ""
|> Array.filter (fun gpp ->
    gpp.GenericProducts
    |> Array.exists (fun gp ->
        gp.PrescriptionProducts
        |> Array.exists (fun pp ->
            pp.TradeProducts
            |> Array.exists (fun tp -> tp.Name |> String.containsCapsInsens "fna")
        )
    )
)
|> Array.map (fun gp ->
    gp.Name, gp.Shape
)
|> Array.distinct
|> Array.sortBy (fun (lbl, shp) -> lbl)
|> Array.iter (fun (lbl, shp) ->
    printfn $"{lbl}\t{shp}"
)

GenPresProduct.findByBrand "Abelccet"
