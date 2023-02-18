namespace Informedica.MetaVision.Lib




module Constants =


    let [<Literal>] solveGroup = "Oplossen"

    let [<Literal>] diluteGroup = "Verdunnen"

    let [<Literal>] NoTime = "NoTime"

    let [<Literal>] TRUE = "TRUE"

    let [<Literal>] FALSE = "FALSE"

    let [<Literal>] ``G-Standaard`` = "G-Standaard"

    let [<Literal>] ``parenterale vloeistof`` = "parenterale vloeistof"

    let [<Literal>] voeding =  "voeding"

    let [<Literal>] Voeding = "Voeding"

    let [<Literal>] ``poeder voor voeding`` = "poeder voor voeding"

    let [<Literal>] Poeders = "Poeders"

    let [<Literal>] milliliter = "milliliter" 

    let [<Literal>] druppel = "druppel"

    let [<Literal>] mL = "mL"

    let [<Literal>] g = "g"

    let [<Literal>] water = "water"

    let [<Literal>] NaCl = "NaCl"

    let [<Literal>] gluc = "gluc"

    let [<Literal>] ``GPK-`` = "GPK-"

    let [<Literal>] GPK = "GPK"

    let [<Literal>] PROD = "PROD"

    let [<Literal>] eContinuous = "eContinuous"

    let [<Literal>] ``[All]`` = "[All]"

    let [<Literal>] MainComponent = "MainComponent"

    let [<Literal>] keer = "keer"

    let [<Literal>] dosis = "dosis"

    let [<Literal>] eenmalig = "eenmalig"

    let [<Literal>] Continue = "Continue"

    let [<Literal>] Nu = "Nu"

    let [<Literal>] ``Volgende geplande dosis`` = "Volgende geplande dosis"

    let [<Literal>] ``Geen tijdslimiet`` = "Geen tijdslimiet"

    let [<Literal>] ActualWeight = "ActualWeight"

    let [<Literal>] Parenteraal = "Parenteraal"

    let [<Literal>] ``1,234`` = "1,234"

    let [<Literal>] ``1,234.56`` = "1,234.56"

    let [<Literal>] ``[None]`` = "[None]"

    let [<Literal>] Apotheek = "Apotheek"

    let [<Literal>] emulsie = "emulsie"

    let [<Literal>] ``.`` = "."

    let [<Literal>] iv = "iv"

    let [<Literal>] im = "im"

    let [<Literal>] ``or`` = "or"

    let [<Literal>] ICC = "ICC"

    let [<Literal>] ICK = "ICK"

    let [<Literal>] NEO = "NEO"



module Units =

    open Informedica.Utils.Lib.BCL


    let volumeUnits =
        [|
            Constants.milliliter
            Constants.druppel
            Constants.mL
        |]


    let isVolumeUnit u =
        volumeUnits
        |> Array.exists (String.equalsCapInsens u)



module Data =


    open Informedica.Utils.Lib.BCL


    let scriptATCMedicationGroups = """
BEGIN TRANSACTION
DECLARE @SETNAME as varchar(max) 
DECLARE @GROUPNAME as varchar(max)
DECLARE @SETID as integer
DECLARE @GROUPID as integer

--CIRCULATIE
SELECT @SETNAME = 'Circulatie'
SELECT @GROUPNAME = 'Circulatie'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
BEGIN
INSERT INTO Orders_MedicationGroupsSets (SetName, LastUpdateGUID) VALUES (@SETNAME,NEWID())
END

SELECT @SETID = (SELECT TOP 1 SetID FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID  =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN( 
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
ATCCode LIKE 'C01A%' 
OR ATCCode LIKE '%, C01A%' 
Or ATCCode Like 'C01B%' 
OR ATCCode LIKE '%, C01B%' 
OR ATCCode LIKE 'C01C%' 
OR ATCCode LIKE '%, C01C%' 
OR ATCCode LIKE 'C02%'  
OR ATCCode LIKE '%, C02%' 
OR ATCCode LIKE 'C03%'  
OR ATCCode LIKE '%, C03%' 
OR ATCCode LIKE 'C04A%'  
OR ATCCode LIKE '%, C04A%' 
OR ATCCode LIKE 'C07%'  
OR ATCCode LIKE '%, C07%' 
OR ATCCode LIKE 'C08%'  
OR ATCCode LIKE '%, C08%' 
OR ATCCode LIKE 'C09%'  
OR ATCCode LIKE '%, C09%' 
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--NEUROLOGIE
SELECT @SETNAME = 'Neurologie'
SELECT @GROUPNAME = 'Neurologie'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
BEGIN
INSERT INTO Orders_MedicationGroupsSets (SetName, LastUpdateGUID) VALUES (@SETNAME,NEWID())
END

SELECT @SETID = (SELECT TOP 1 SetID FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN( 
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
ATCCode LIKE 'N01%' 
OR ATCCode LIKE '%, N01%' 
Or ATCCode Like 'N02%' 
OR ATCCode LIKE '%, N02%' 
OR ATCCode LIKE 'N03%' 
OR ATCCode LIKE '%, N03%' 
OR ATCCode LIKE 'N04%'  
OR ATCCode LIKE '%, N04%'
OR ATCCode LIKE 'N05%' 
OR ATCCode LIKE '%, N05%' 
OR ATCCode LIKE 'N06%'  
OR ATCCode LIKE '%, N06%' 
OR ATCCode LIKE 'N07%' 
OR ATCCode LIKE '%, N07%' 
OR ATCCode LIKE 'M03%' 
OR ATCCode LIKE '%, M03%' 
OR ATCCode LIKE '%V03AB15%'
OR ATCCode LIKE '%V03AB19%'
OR ATCCode LIKE '%V03AB25%' 
OR ATCCode LIKE '%V03AB35%'
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--INFECTIE
SELECT @SETNAME = 'Infectie'
SELECT @GROUPNAME = 'Infectie'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
BEGIN
INSERT INTO Orders_MedicationGroupsSets (SetName, LastUpdateGUID) VALUES (@SETNAME,NEWID())
END

SELECT @SETID = (SELECT TOP 1 SetID FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN(
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
ATCCode LIKE 'J01%' 
OR ATCCode LIKE '%, J01%' 
Or ATCCode Like 'J02%' 
OR ATCCode LIKE '%, J02%' 
OR ATCCode LIKE 'J04%' 
OR ATCCode LIKE '%, J04%' 
OR ATCCode LIKE 'J05%'  
OR ATCCode LIKE '%, J05%' 
OR ATCCode LIKE 'J06%'  
OR ATCCode LIKE '%, J06%' 
OR ATCCode LIKE 'P01%' 
OR ATCCode LIKE '%, P01%' 
OR ATCCode LIKE 'P02%' 
OR ATCCode LIKE '%, P02%'
OR ATCCode LIKE 'P03%' 
OR ATCCode LIKE '%, P03%'
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--Medicatie en Parenteralia en Voeding
SELECT @SETNAME = 'Medicatie en Parenteralia en Voeding'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
BEGIN
INSERT INTO Orders_MedicationGroupsSets (SetName, LastUpdateGUID) VALUES (@SETNAME,NEWID())
END
SELECT @SETID = (SELECT TOP 1 SetID FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)

--Medicatie
SELECT @GROUPNAME = 'Medicatie'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME AND SetID= @SETID)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID  =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN( 
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
NOT ', ' + ATCCode + ', ' LIKE '%, voeding, %' 
AND NOT ', ' + ATCCode + ', '  LIKE '%, parenteraal, %'
AND NOT ', ' + ATCCode + ', '   LIKE '%, B05BA%, %'
AND NOT (', ' + ATCCode + ', '  LIKE '%, B05BB%, %' AND NOT (ParameterName LIKE '%kaliumchloride%' OR ParameterName LIKE '%natriumwaterstofcarbonaat%'))
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--Voeding
SELECT @GROUPNAME = 'Voeding'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME AND SetID= @SETID)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID  =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN( 
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
', ' + ATCCode + ', ' LIKE '%, voeding, %' 
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--Parenteraal
SELECT @GROUPNAME = 'Parenteraal'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME AND SetID= @SETID)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID  =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN( 
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
', ' + ATCCode + ', ' LIKE '%, parenteraal, %'
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--Vocht
SELECT @GROUPNAME = 'Vocht'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME AND SetID= @SETID)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID  =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
ParameterID IN( 
SELECT OM.MedicationID 
FROM Orders_Medications OM 
WHERE 
', ' + ATCCode + ', '   LIKE '%, B05BA%, %'
OR (', ' + ATCCode + ', '  LIKE '%, B05BB%, %' AND NOT (ParameterName LIKE '%kaliumchloride%' OR ParameterName LIKE '%natriumwaterstofcarbonaat%'))
) 
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)

--MEDICATIE TBD
SELECT @SETNAME = 'Medicatie TBD'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)
BEGIN
INSERT INTO Orders_MedicationGroupsSets (SetName, LastUpdateGUID) VALUES (@SETNAME,NEWID())
END
SELECT @SETID = (SELECT TOP 1 SetID FROM Orders_MedicationGroupsSets WHERE SetName = @SETNAME)

SELECT @GROUPNAME = 'TBD'
IF NOT EXISTS (SELECT * FROM Orders_MedicationGroups WHERE GroupName = @GROUPNAME AND SetID= @SETID)
BEGIN
INSERT INTO Orders_MedicationGroups (SetID, GroupID, GroupName, TextColor, Color) VALUES (
@SETID,
COALESCE((SELECT MAX(GroupID) FROM Orders_MedicationGroups WHERE SetID = @SETID)+1,1),
@GROUPNAME,0,0)
END

SELECT @GROUPID  =(SELECT TOP 1 GroupID FROM Orders_MedicationGroups WHERE SetID=@SETID AND GroupName = @GROUPNAME)
INSERT INTO Orders_MedicationGroupParameters (SetID, GroupID, ParameterID) SELECT
@SETID,
@GROUPID,
ParameterID
FROM Parameters WHERE 
(
ParameterName LIKE 'tacro%'
OR ParameterName LIKE 'acenoc%'
OR ParameterName LIKE 'fenpro%'
OR ParameterName LIKE 'ciclos%'
)
AND
ParameterID NOT IN (SELECT ParameterID FROM Orders_MedicationGroupParameters WHERE SetID=@SETID AND GroupID=@GROUPID)



ROLLBACK
"""


    let excludeShapes =
        [|
            "infuus"
            "injectie"
            "injectie/infuus"
        |]


    let solutionMeds =
        [|
            "water", "oplosvloeistof"
            "oplvlst", "oplosvloeistof"
            "NaCl 0,9%", "oplosvloeistof"
            "gluc 5%", "oplosvloeistof"
            "gluc 10%", "oplosvloeistof"
            "emulsie", "emulsie"
        |]


    let includeSols =
        [|
            "abatacept"
            "abelcet"
            "acetazolamide"
            "acetylcholine"
            "acetylcysteine"
            "acetylsalicylzuur"
            "aciclovir"
            "adenosine"
            "adrenaline"
            "alanylglutamine"
            "albumine"
            "albutrepenonacog"
            "alcohol"
            "aldesleukine"
            "alemtuzumab"
            "alfacalcidol"
            "alfentanil"
            "alprostadil"
            "alteplase"
            "amfotericine"
            "amikacine"
            "amiodaron"
            "amoxicilline"
            "amoxicilline/clavulaanzuur"
            "anakinra"
            "anidulafungin"
            "anifrolumab"
            "antitrombine"
            "argatroban"
            "arginine"
            "argipressine"
            "artesunaat"
            "ascorbinezuur"
            "astrazeneca"
            "atosiban"
            "atracurium"
            "atropinesulfaat"
            "aztreonam"
            "basiliximab"
            "bcg-medac"
            "belatacept"
            "belimumab"
            "benzylpenicilline"
            "betamethason"
            "bevacizumab"
            "biperideen"
            "bivalirudine"
            "botuline"
            "bumetanide"
            "bupivacaine"
            "c1-esteraseremmer"
            "cabotegravir"
            "calcitonine"
            "calciumgluconaat"
            "cangrelor"
            "caplacizumab"
            "carbacholine"
            "carbetocine"
            "casirivimab"
            "caspofungin"
            "cefazoline"
            "cefiderocol"
            "cefotaxim"
            "ceftazidim"
            "wijzigingen"
            "ceftolozaan-tazobactam"
            "ceftriaxon"
            "cefuroxim"
            "cernevit"
            "cetrorelix"
            "chirhostim"
            "chlooramfenicol"
            "chloorprocaine"
            "choriongonadotrofine"
            "ciclosporine"
            "cidofovir"
            "ciprofloxacine"
            "cisatracurium"
            "citra-lock"
            "clemastine"
            "clindamycine"
            "clonazepam"
            "clonidine"
            "coffeine"
            "colistine"
            "comirnaty"
            "corticoreline"
            "cotrimoxazol"
            "cuvitru"
            "cyanocobalamine"
            "dalteparine"
            "danaparoide"
            "dantroleen"
            "daptomycine"
            "daratumumab"
            "darbepoetine"
            "deferoxamine"
            "defibrotide"
            "denosumab"
            "desmopressine"
            "dexamethason"
            "dexmedetomidine"
            "diazepam"
            "diclofenac"
            "digoxine"
            "dimenhydrinaat"
            "dobutamine"
            "dopamine"
            "doxapram"
            "doxycycline"
            "droperidol"
            "dtp-vaccin"
            "eculizumab"
            "efedrine"
            "efmoroctocog"
            "eftrenonacog"
            "emicizumab"
            "epoetine"
            "epoprostenol"
            "eptacog"
            "ertapenem"
            "erytromycine"
            "esketamine"
            "esmolol"
            "esomeprazol"
            "etanercept"
            "ethanol"
            "etomidaat"
            "factor"
            "fenobarbital"
            "fenol"
            "fentanyl"
            "fentolamine"
            "fenylefrine"
            "fenytoine"
            "ferricarboxymaltose"
            "ferriisomaltoside"
            "ferrioxidesaccharaat"
            "fibrinogeen"
            "filgrastim"
            "flecainide"
            "flucloxacilline"
            "fluconazol"
            "flucytosine"
            "flumazenil"
            "fluoresceine"
            "flupentixol"
            "folinezuur"
            "fondaparinux"
            "fosaprepitant"
            "foscarnet"
            "fosfomycine"
            "furosemide"
            "fytomenadion"
            "ganciclovir"
            "ganirelix"
            "gelatine"
            "gentamicine"
            "glucagon"
            "glucarpidase"
            "glucose"
            "glycerofosforzuur"
            "glycopyrronium"
            "golimumab"
            "gonadoreline"
            "granisetron"
            "haloperidol"
            "hemine"
            "heparine"
            "hepatitis-b-immunoglobuline"
            "hepatitis-b-vaccin"
            "hyaluronidase"
            "hyaluronidase/immunoglobuline"
            "hydrocortison"
            "hydroxocobalamine"
            "hydroxyethylzetmeel"
            "ibuprofen"
            "icatibant"
            "idarucizumab"
            "iloprost"
            "imipenem/cilastatine"
            "immunoglobuline"
            "indocyaninegroen"
            "infliximab"
            "insuline"
            "intralipid"
            "isatuximab"
            "isavuconazol"
            "isoniazide"
            "isoprenaline"
            "covid"
            "kaliumchloride"
            "ketanserine"
            "labetalol"
            "levetiracetam"
            "levobupivacaine"
            "levocarnitine"
            "levofloxacine"
            "levomepromazine"
            "levosimendan"
            "levothyroxine"
            "lidocaine"
            "linezolid"
            "liothyronine"
            "lipidemicrosferen"
            "lorazepam"
            "lutropine"
            "lymfocytenimmunoglobuline"
            "magnesiumchloride"
            "magnesiumsulfaat"
            "mannitol"
            "mecasermine"
            "meningokokkenvaccin"
            "menopauzegonadotrofine"
            "mepivacaine"
            "mepolizumab"
            "mercapto-ethaansulfonzuur"
            "meropenem"
            "metamizol"
            "methadon"
            "methoxypolyethyleenglycol-epoetine"
            "methyleenblauw"
            "methylergometrine"
            "methylnaltrexon"
            "methylprednisolon"
            "metoclopramide"
            "metoprolol"
            "metronidazol"
            "micafungine"
            "midazolam"
            "milrinon"
            "morfine"
            "moxifloxacine"
            "mycofenolaat"
            "nalbufine"
            "naloxon"
            "natalizumab"
            "natrium"
            "natriumbenzoaat"
            "natriumbenzoaat/natriumfenylacetaat"
            "natriumchloride"
            "natriumperchloraat"
            "natriumthiosulfaat"
            "natriumwaterstofcarbonaat"
            "neostigmine"
            "nicardipine"
            "nimodipine"
            "nitroglycerine"
            "nitroprusside"
            "nonacog"
            "noradrenaline"
            "nutriflex"
            "nuvaxovid"
            "octocog"
            "octreotide"
            "olanzapine"
            "olimel"
            "omalizumab"
            "omnipaque"
            "onasemnogene"
            "ondansetron"
            "oxybutynine"
            "oxycodon"
            "oxytocine"
            "palivizumab"
            "pamidronaat"
            "pantoprazol"
            "papaverine"
            "paracetamol"
            "parecoxib"
            "patentblauw"
            "pentamidine"
            "pethidine"
            "piperacilline"
            "piritramide"
            "pneumokokkenvaccin"
            "polidocanol"
            "posaconazol"
            "prednisolon"
            "prilocaine"
            "primene"
            "procainamide"
            "promethazine"
            "propofol"
            "protamine"
            "protrombine"
            "pyridoxine"
            "rabiesvaccin"
            "rasburicase"
            "remdesivir"
            "remifentanil"
            "remimazolam"
            "rhesus(d)"
            "rifampicine"
            "rilpivirine"
            "ringerlactaat"
            "risperidon"
            "rituximab"
            "rocuronium"
            "ropivacaine"
            "ropivacaïne"
            "ropivacaïne/sufentanil"
            "salbutamol"
            "sarilumab"
            "scopolaminebutyl"
            "sildenafil"
            "silibinin"
            "smoflipid"
            "somatoreline"
            "somatropine"
            "sotrovimab"
            "sufentanil"
            "sugammadex"
            "sulfametrol"
            "sulproston"
            "sumatriptan"
            "supliven"
            "suxamethonium"
            "tacrolimus"
            "taurolidine"
            "taurolock"
            "teicoplanine"
            "temoporfine"
            "terlipressine"
            "testosteron"
            "tetanusimmunoglobuline"
            "tetanusvaccin"
            "tetracosactide"
            "theofylline"
            "thiamine"
            "thiopental"
            "thymocytenimmunoglobuline"
            "tigecycline"
            "tirofiban"
            "tobramycine"
            "tocilizumab"
            "tramadol"
            "tranexaminezuur"
            "trastuzumab"
            "triamcinolonacetonide"
            "nieuwe"
            "triamcinolonhexacetonide"
            "triptoreline"
            "trometamol"
            "turoctocog"
            "urokinase"
            "ustekinumab"
            "valproaat"
            "vancomycine"
            "varicellazosterimmunoglobuline"
            "varicella-zostervaccin"
            "vedolizumab"
            "verapamil"
            "verteporfine"
            "vitaminen,"
            "willebrandfactor"
            "vonicog"
            "voriconazol"
            "wilate"
            "zanamivir"
            "zidovudine"
            "zoledroninezuur"
            "zuclopentixol"
        |]


    let routeHeadings =
        [|
            "ExternalCode"
            "RouteName"
            "OrderingType"
            "RouteLocations"
        |]


    let doseFormHeadings =
        [|
            "ExternalCode"
            "DoseFormName"
            "Routes"
            "DefaultUnit"
            "OrderingType"
            "IsDrugInSolution"
            "Category"
            "IsDispensableAmountAllowed"
        |]


    let ingredientHeadings =
        [|
            "ExternalCode"
            "IngredientName"
            "Unit"
        |]


    let medicationHeadings =
        [|
            "ExternalCode"
            "MedicationName"
            "Unit"
            "ATCCode"
            "Status"
            "Format"
            "IncrementValue"
            "CodeSnippetName"
            "Frequencies"
            "DoseForms"
            "Routes"
            "AdditivesGroup"
            "DiluentsGroup"
            "DrugInDiluentGroup"
            "DrugFamily"
            "DrugSubfamily"
            "HideInAllergyEntry"
            "AllergyLookBackTime"
            "AllergyLookBackTimeMeasure"
            "NormalQuantity"
            "NormalQuantityUnit"
            "MaskQuantity"
            "MaskQuantityUnit"
            "NormalRate"
            "NormalRateUnit"
            "MaskRate"
            "MaskRateUnit"
            "NormalConcentration"
            "NormalConcentrationMassUnit"
            "NormalConcentrationVolumeUnit"
            "MaskConcentration"
            "MaskConcentrationMassUnit"
            "MaskConcentrationVolumeUnit"
            "IsFormulary"
        |]


    let complexMedicationHeadings =
        [|
            "ComplexMedicationName"
            "IngredientName"
            "Concentration"
            "ConcentrationUnit"
            "In"
            "InUnit"
        |]


    let brandHeadings =
        [|
            "ExternalCode"
            "BrandName"
            "Manufacturer"
            "MedicationName"
        |]


    let productHeadings =
        [|
            "ExternalCode"
            "ProductID"
            "ProductName"
            "MedicationName"
            "BrandName"
            "Manufacturer"
            "DoseForm"
            "Routes"
            "Status"
            "Format"
            "IncrementValue"
            "Unit"
            "DefaultUnit"
            "IsUnknownStrength"
            "StrengthLEFT"
            "StrengthLEFTUnit"
            "StrengthRIGHT"
            "StrengthRIGHTUnit"
            "DiluentGroup"
            "ProductRequiresReconstitution"
            "IsVolumeKnown"
            "Volume"
            "VolumeUnit"
            "DiluentName"
            "Barcode"
            "ATCCode"
            "MinimumDispensibleAmount"
            "IsFormulary"
        |]


    let orderTemplateHeadings =
        [|
            "OrderTemplateName"
            "MedicationName"
            "ProductName"
            "DoseForm"
            "Route"
            "Location"
            "IsPRN"
            "PRNIndication"
            "MaxDosePer24Hr"
            "MaxDosePer24HrUnit"
            "PatternMode"
            "RepeatEvery"
            "RepeatUnit"
            "PatternPeriod"
            "DosePatternTime"
            "Frequency"
            "FrequencyValue"
            "FrequencyUnit"
            "OrderingStyle"
            "LockerTemplate"
            "TitrationMode"
            "ComponentType"
            "ComponentMedicationName"
            "ComponentProductName"
            "ComponentQuantityVolumeValue"
            "ComponentQuantityVolumeUnit"
            "ComponentConcentrationValue"
            "ComponentConcentrationMassUnit"
            "ComponentConcentrationVolumeUnit"
            "ComponentDrugInDiluentDiluentMedicationName"
            "ComponentDrugInDiluentDiluentProductName"
            "ComponentDrugInDiluentVolumeValue"
            "ComponentDrugInDiluentVolumeUnit"
            "ComponentDailyDosageAmount"
            "ComponentDailyDosageMaxRangeAmount"
            "ComponentDailyDosageUnit / Day"
            "DoseValue"
            "DoseMaxRange"
            "DoseUnit"
            "RateValue"
            "RateMaxRange"
            "RateUnit"
            "TotalVolumeValue"
            "TotalVolumeUnit"
            "InfuseOverValue"
            "InfuseOverMaxRange"
            "InfuseOverUnit"
            "StartMethod"
            "StartMethodValue"
            "StartMethodValueUnit"
            "EndMethod"
            "EndMethodValue"
            "EndMethodValueUnit"
            "DisallowSubstitutionReason"
            "WeightType"
            "Comment"
            "Caption"
            "AvailableInRT"
            "MarkForRemoval"
        |]


    let solutionHeadings =
        [|
            "GPK"
            "Generic"
            "Shape"
            "Route"
            "DoseType"
            "Dep"
            "CVL"
            "PVL"
            "DiluentVol"
            "Diluent"
            "MinAge"
            "MaxAge"
            "MinWeight"
            "MaxWeight"
            "MinDose"
            "MaxDose"
            "Solutions"
            "MinVol"
            "MaxVol"
            "MinPerc"
            "MaxPerc"
            "MinTime"
            "MaxTime"
            "MinRate"
            "MaxRate"
            "RateUnit"
            "Substance"
            "Unit"
            "Quantities"
            "MinQty"
            "MaxQty"
            "MinConc"
            "MaxConc"
        |]


    let parenteral n =
        let alsoEnt =
            match n with
            | _ when n |> String.contains Constants.water -> true
            | _ when n |> String.contains Constants.gluc  -> true 
            | _ when n |> String.contains Constants.NaCl  -> true
            | _ -> false

        [|
            "MedicationName", n
            "Unit", "mL"
            "ATCCode", "parenteraal"
            "Status", $"{Active}"
            "Format", Constants.``1,234.56``
            "IncrementValue", "0,1"
            "CodeSnippetName", n
            "Frequencies", Constants.``[All]``

            "DoseForms",
            if alsoEnt then
                $"{Constants.voeding};{Constants.``parenterale vloeistof``}"
            else Constants.``parenterale vloeistof``

            "Routes", Constants.``[All]`` // if alsoEnt then $"{Constants.``or``};{Constants.iv}" else Constants.iv
            "AdditivesGroup", Constants.``[None]``
            "DiluentsGroup", Constants.``[All]``
            "DrugInDiluentGroup", Constants.``[None]``
            "DrugFamily", Constants.``[None]``
            "DrugSubfamily", Constants.``[None]``
            "HideInAllergyEntry", Constants.TRUE
            "IsFormulary", Constants.TRUE
        |]


    let enteral n un =
        [|
            "MedicationName", n
            "Unit", un
            "ATCCode", Constants.voeding
            "Status", $"{Active}"
            "Format", Constants.``1,234.56``
            "IncrementValue", "0,1"
            "CodeSnippetName", n
            "Frequencies", Constants.``[All]``
            "DoseForms", if un = Constants.g then Constants.``poeder voor voeding`` else Constants.voeding
            "Routes", "or"
            "AdditivesGroup", if un = Constants.g then Constants.``[None]`` else Constants.Poeders
            "DiluentsGroup", if un = Constants.g then Constants.``[None]`` else Constants.``[All]``
            "DrugInDiluentGroup", Constants.``[None]``
            "DrugFamily", Constants.``[None]``
            "DrugSubfamily", Constants.``[None]``
            "HideInAllergyEntry", Constants.TRUE
            "IsFormulary", Constants.TRUE
        |]



