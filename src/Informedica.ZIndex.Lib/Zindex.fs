
namespace Informedica.ZIndex.Lib

/// <summary>
/// <para>BST004T: Bestand 004 Artikelen</para>
/// <para>BST020T: Bestand 020 Namen</para>
/// <para>BST031T: Bestand 031 Handelsproducten</para>
/// <para>BST050T: Bestand 050 Voorschrijfproducten (PRK)</para>
/// <para>BST051T: Bestand 051 Voorschrijfpr. geneesmiddel identific.</para>
/// <para>BST200T: Bestand 200 Relatie tussen ZI-nummer / HIBC</para>
/// <para>BST360T: Bestand 360 t-tabel (tijdseenheid)</para>
/// <para>BST380T: Bestand 380 ICPC - 1</para>
/// <para>BST640T: Bestand 640 Doseringen Basis-Algemeen</para>
/// <para>BST641T: Bestand 641 Doseringen Basis-Artikelkeuze</para>
/// <para>BST642T: Bestand 642 Doseringen Uitzonderingen op Basis</para>
/// <para>BST643T: Bestand 643 Categorieen</para>
/// <para>BST649T: Bestand 649 Dosisgegevens - Nieuw per 01/11/2016</para>
/// <para>BST701T: Bestand 701 Ingegeven samenstellingen</para>
/// <para>BST711T: Bestand 711 Generieke producten</para>
/// <para>BST715T: Bestand 715 Generieke samenstellingen</para>
/// <para>BST720T: Bestand 720 Superprodukten</para>
/// <para>BST725T: Bestand 725 Stamnaam + stamtoedieningsweg</para>
/// <para>BST750T: Bestand 750 Generieke namen</para>
/// <para>BST760T: Bestand 760 Enkelvoudige toedieningswegen HPK</para>
/// <para>BST801T: Bestand 801 ATC codes</para>
/// <para>BST902T: Bestand 902 Thesauri totaal</para>
/// <para>BST921T: Bestand 921 Tekstblokken ASCII (vervangt 920)</para>
/// </summary>
module Zindex =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib


    /// <summary>
    /// <para> Tabel: BST004T: Bestand 004 Artikelen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	ATKODE: 	ZI-nummer </para>
    /// <para> 2.	HPKODE: 	Handels Product Kenmerken (HPK) code </para>
    /// <para> 3.	ATNMNR: 	Artikelnaamnummer </para>
    /// <para> 4.	VPDLOM: 	Deelverpakking omschrijving kode </para>
    /// <para> 5.	VPDLHV: 	Hoeveelheid per deelverpakking </para>
    /// </summary>
    module BST004T =

        let name = "BST004T"

        type BST004T =
            {
                MUTKOD : int
                ATKODE : int
                HPKODE : int
                ATNMNR : int
                VPDLOM : int
                VPDLHV : decimal
            }


        let create mutkod atkode hpkode atnmnr vpdlom vpdlhv  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                ATKODE = atkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                HPKODE = hpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                ATNMNR = atnmnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                VPDLOM = vpdlom |> ((Parser.parseValue "N" "") >> Int32.parse)
                VPDLHV = vpdlhv |> ((Parser.parseValue "N" "(6,2)") >> Decimal.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;11;12]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5))

        let records : unit -> BST004T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST020T: Bestand 020 Namen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	NMNR: 	Naamnummer </para>
    /// <para> 2.	NMMEMO: 	Memokode </para>
    /// <para> 3.	NMETIK: 	Etiketnaam </para>
    /// <para> 4.	NMNM40: 	Korte handelsnaam </para>
    /// <para> 5.	NMNAAM: 	Naam volledig </para>
    /// </summary>
    module BST020T =

        let name = "BST020T"

        type BST020T =
            {
                MUTKOD : int
                NMNR : int
                NMMEMO : string
                NMETIK : string
                NMNM40 : string
                NMNAAM : string
            }


        let create mutkod nmnr nmmemo nmetik nmnm40 nmnaam  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                NMNR = nmnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                NMMEMO = nmmemo |> String.trim
                NMETIK = nmetik |> String.trim
                NMNM40 = nmnm40 |> String.trim
                NMNAAM = nmnaam |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5))

        let records : unit -> BST020T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST031T: Bestand 031 Handelsproducten </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	HPKODE: 	Handels Product Kenmerken (HPK) code </para>
    /// <para> 2.	PRKODE: 	PRK-code </para>
    /// <para> 3.	HPNAMN: 	Handelsproduktnaamnummer </para>
    /// <para> 4.	MSNAAM: 	Merkstamnaam </para>
    /// <para> 5.	FSNAAM: 	Firmastamnaam </para>
    /// <para> 6.	HPSGEW: 	Soortelijk gewicht </para>
    /// <para> 7.	GRP001: 	FTK 1 </para>
    /// <para> 8.	GRP002: 	FTK 2 </para>
    /// <para> 9.	GRP003: 	FTK 3 </para>
    /// <para> 10.	GRP004: 	FTK 4 </para>
    /// <para> 11.	GRP005: 	FTK 5 </para>
    /// <para> 12.	HPDEEL: 	Deelbaarheid aantal </para>
    /// </summary>
    module BST031T =

        let name = "BST031T"

        type BST031T =
            {
                MUTKOD : int
                HPKODE : int
                PRKODE : int
                HPNAMN : int
                MSNAAM : string
                FSNAAM : string
                HPSGEW : decimal
                GRP001 : int
                GRP002 : int
                GRP003 : int
                GRP004 : int
                GRP005 : int
                HPDEEL : int
            }


        let create mutkod hpkode prkode hpnamn msnaam fsnaam hpsgew grp001 grp002 grp003 grp004 grp005 hpdeel  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                HPKODE = hpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                PRKODE = prkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                HPNAMN = hpnamn |> ((Parser.parseValue "N" "") >> Int32.parse)
                MSNAAM = msnaam |> String.trim
                FSNAAM = fsnaam |> String.trim
                HPSGEW = hpsgew |> ((Parser.parseValue "N" "(2,5)") >> Decimal.parse)
                GRP001 = grp001 |> ((Parser.parseValue "N" "") >> Int32.parse)
                GRP002 = grp002 |> ((Parser.parseValue "N" "") >> Int32.parse)
                GRP003 = grp003 |> ((Parser.parseValue "N" "") >> Int32.parse)
                GRP004 = grp004 |> ((Parser.parseValue "N" "") >> Int32.parse)
                GRP005 = grp005 |> ((Parser.parseValue "N" "") >> Int32.parse)
                HPDEEL = hpdeel |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;5;6;7;10;18;19;20;21;22;38]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8) (xs |> Array.item 9) (xs |> Array.item 10) (xs |> Array.item 11) (xs |> Array.item 12))

        let records : unit -> BST031T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST050T: Bestand 050 Voorschrijfproducten (PRK) </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	PRKODE: 	PRK-code </para>
    /// <para> 2.	PRNMNR: 	Naamnummer prescriptie product </para>
    /// </summary>
    module BST050T =

        let name = "BST050T"

        type BST050T =
            {
                MUTKOD : int
                PRKODE : int
                PRNMNR : int
            }


        let create mutkod prkode prnmnr  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                PRKODE = prkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                PRNMNR = prnmnr |> ((Parser.parseValue "N" "(7,0)") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2))

        let records : unit -> BST050T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST051T: Bestand 051 Voorschrijfpr. geneesmiddel identific. </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	PRKODE: 	PRK-code </para>
    /// <para> 2.	GPKODE: 	Generiekeproductcode (GPK) </para>
    /// <para> 3.	HPEMBT: 	Emballagetype kode </para>
    /// <para> 4.	XPEHHV: 	Basiseenheid product kode </para>
    /// <para> 5.	HPGALG: 	HPK-grootte algemeen </para>
    /// </summary>
    module BST051T =

        let name = "BST051T"

        type BST051T =
            {
                MUTKOD : int
                PRKODE : int
                GPKODE : int
                HPEMBT : int
                XPEHHV : int
                HPGALG : decimal
            }


        let create mutkod prkode gpkode hpembt xpehhv hpgalg  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                PRKODE = prkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GPKODE = gpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                HPEMBT = hpembt |> ((Parser.parseValue "N" "") >> Int32.parse)
                XPEHHV = xpehhv |> ((Parser.parseValue "N" "") >> Int32.parse)
                HPGALG = hpgalg |> ((Parser.parseValue "N" "(5,2)") >> Decimal.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;4;5;6;7]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5))

        let records : unit -> BST051T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST200T: Bestand 200 Relatie tussen ZI-nummer / HIBC </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	ATKODE: 	ZI-nummer </para>
    /// <para> 2.	BARCOD: 	HIBC-barcode </para>
    /// <para> 3.	HPKODA: 	Handelsproduktkode (36-tallig) </para>
    /// <para> 4.	VPKODA: 	Identificatie nummer (36-tallig) </para>
    /// </summary>
    module BST200T =

        let name = "BST200T"

        type BST200T =
            {
                MUTKOD : int
                ATKODE : int
                BARCOD : string
                HPKODA : string
                VPKODA : string
            }


        let create mutkod atkode barcod hpkoda vpkoda  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                ATKODE = atkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                BARCOD = barcod |> String.trim
                HPKODA = hpkoda |> String.trim
                VPKODA = vpkoda |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4))

        let records : unit -> BST200T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST360T: Bestand 360 t-tabel (tijdseenheid) </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	TTEHNR: 	Uniek nummer per tijdseenheid </para>
    /// <para> 2.	TTEHOM: 	Omschrijving tijdseenheid </para>
    /// </summary>
    module BST360T =

        let name = "BST360T"

        type BST360T =
            {
                MUTKOD : int
                TTEHNR : int
                TTEHOM : string
            }


        let create mutkod ttehnr ttehom  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                TTEHNR = ttehnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                TTEHOM = ttehom |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;4]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2))

        let records : unit -> BST360T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST380T: Bestand 380 ICPC - 1 </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	ICPCNR1: 	ICPC1-nummer </para>
    /// <para> 2.	ICPCTXT: 	ICPC-omschrijvingen </para>
    /// </summary>
    module BST380T =

        let name = "BST380T"

        type BST380T =
            {
                MUTKOD : int
                ICPCNR1 : int
                ICPCTXT : string
            }


        let create mutkod icpcnr1 icpctxt  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                ICPCNR1 = icpcnr1 |> ((Parser.parseValue "N" "") >> Int32.parse)
                ICPCTXT = icpctxt |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;4]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2))

        let records : unit -> BST380T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST640T: Bestand 640 Doseringen Basis-Algemeen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GPKODE: 	Generiekeproductcode (GPK) </para>
    /// <para> 2.	GPDGST: 	Toegestaan voor geslacht </para>
    /// <para> 3.	GPRISC: 	Kode hoog risico overdosering </para>
    /// </summary>
    module BST640T =

        let name = "BST640T"

        type BST640T =
            {
                MUTKOD : int
                GPKODE : int
                GPDGST : int
                GPRISC : string
            }


        let create mutkod gpkode gpdgst gprisc  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPKODE = gpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GPDGST = gpdgst |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPRISC = gprisc |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;6;8]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3))

        let records : unit -> BST640T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST641T: Bestand 641 Doseringen Basis-Artikelkeuze </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GPKODE: 	Generiekeproductcode (GPK) </para>
    /// <para> 2.	PRKODE: 	PRK-code </para>
    /// <para> 3.	HPKODE: 	Handels Product Kenmerken (HPK) code </para>
    /// <para> 4.	GPDCTH: 	Thesaurus soort-doseringscode (1004) </para>
    /// <para> 5.	GPDCOD: 	Soort-doseringscode </para>
    /// <para> 6.	GPDBAS: 	Dosis-basisnummer </para>
    /// </summary>
    module BST641T =

        let name = "BST641T"

        type BST641T =
            {
                MUTKOD : int
                GPKODE : int
                PRKODE : int
                HPKODE : int
                GPDCTH : int
                GPDCOD : int
                GPDBAS : int
            }


        let create mutkod gpkode prkode hpkode gpdcth gpdcod gpdbas  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPKODE = gpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                PRKODE = prkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                HPKODE = hpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GPDCTH = gpdcth |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDCOD = gpdcod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDBAS = gpdbas |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6))

        let records : unit -> BST641T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST642T: Bestand 642 Doseringen Uitzonderingen op Basis </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GPDBAS: 	Dosis-basisnummer </para>
    /// <para> 2.	GPDID1: 	Identificerend volgnummer </para>
    /// <para> 3.	GPDZCO: 	Zorggroep-codering </para>
    /// <para> 4.	ICPCNR1: 	ICPC1-nummer </para>
    /// <para> 5.	ICPCTO: 	Verbijzondering </para>
    /// <para> 6.	GPKTTH: 	Thesaurus afwijkende toedieningsweg (7) </para>
    /// <para> 7.	GPKTWG: 	Toedieningsweg code </para>
    /// <para> 8.	GPDCAT: 	Dosis-categorienummer </para>
    /// </summary>
    module BST642T =

        let name = "BST642T"

        type BST642T =
            {
                MUTKOD : int
                GPDBAS : int
                GPDID1 : int
                GPDZCO : int
                ICPCNR1 : int
                ICPCTO : int
                GPKTTH : int
                GPKTWG : int
                GPDCAT : int
            }


        let create mutkod gpdbas gpdid1 gpdzco icpcnr1 icpcto gpktth gpktwg gpdcat  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDBAS = gpdbas |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDID1 = gpdid1 |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDZCO = gpdzco |> ((Parser.parseValue "N" "") >> Int32.parse)
                ICPCNR1 = icpcnr1 |> ((Parser.parseValue "N" "") >> Int32.parse)
                ICPCTO = icpcto |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPKTTH = gpktth |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPKTWG = gpktwg |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDCAT = gpdcat |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;5;6;8;11;12;13]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8))

        let records : unit -> BST642T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST643T: Bestand 643 Categorieen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GPDCAT: 	Dosis-categorienummer </para>
    /// <para> 2.	GPDID2: 	Identificerend recordnummer </para>
    /// <para> 3.	GPDLFM: 	Leeftijd in maanden vanaf </para>
    /// <para> 4.	GPDLFX: 	Leeftijd in maanden tot </para>
    /// <para> 5.	GPDKGM: 	Gewicht in KG vanaf </para>
    /// <para> 6.	GPDKGX: 	Gewicht in KG tot </para>
    /// <para> 7.	GPDM2M: 	Lichaamsoppervlakte in M2 vanaf </para>
    /// <para> 8.	GPDM2X: 	Lichaamsoppervlakte in M2 tot </para>
    /// <para> 9.	GPDFAA: 	Frequentie  aantal </para>
    /// <para> 10.	GPDFEE: 	Frequentie  tijdseenheid </para>
    /// <para> 11.	GPDDEN: 	Basisset voor Denekamp berekening (J/N) </para>
    /// <para> 12.	GPDDNR: 	Dosisnummer </para>
    /// </summary>
    module BST643T =

        let name = "BST643T"

        type BST643T =
            {
                MUTKOD : int
                GPDCAT : int
                GPDID2 : int
                GPDLFM : decimal
                GPDLFX : decimal
                GPDKGM : decimal
                GPDKGX : decimal
                GPDM2M : decimal
                GPDM2X : decimal
                GPDFAA : decimal
                GPDFEE : int
                GPDDEN : string
                GPDDNR : int
            }


        let create mutkod gpdcat gpdid2 gpdlfm gpdlfx gpdkgm gpdkgx gpdm2m gpdm2x gpdfaa gpdfee gpdden gpddnr  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDCAT = gpdcat |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDID2 = gpdid2 |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDLFM = gpdlfm |> ((Parser.parseValue "N" "(4,2)") >> Decimal.parse)
                GPDLFX = gpdlfx |> ((Parser.parseValue "N" "(4,2)") >> Decimal.parse)
                GPDKGM = gpdkgm |> ((Parser.parseValue "N" "(3,3)") >> Decimal.parse)
                GPDKGX = gpdkgx |> ((Parser.parseValue "N" "(3,3)") >> Decimal.parse)
                GPDM2M = gpdm2m |> ((Parser.parseValue "N" "(3,3)") >> Decimal.parse)
                GPDM2X = gpdm2x |> ((Parser.parseValue "N" "(3,3)") >> Decimal.parse)
                GPDFAA = gpdfaa |> ((Parser.parseValue "N" "(2,2)") >> Decimal.parse)
                GPDFEE = gpdfee |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDDEN = gpdden |> String.trim
                GPDDNR = gpddnr |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7;8;9;10;11;12;13]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8) (xs |> Array.item 9) (xs |> Array.item 10) (xs |> Array.item 11) (xs |> Array.item 12))

        let records : unit -> BST643T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST649T: Bestand 649 Dosisgegevens - Nieuw per 01/11/2016 </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GPDDNR: 	Dosisnummer </para>
    /// <para> 2.	GPNRMMIN: 	Norm minimum </para>
    /// <para> 3.	GPNRMMAX: 	Norm maximum </para>
    /// <para> 4.	GPABSMIN: 	Absoluut minimum </para>
    /// <para> 5.	GPABSMAX: 	Absoluut maximum </para>
    /// <para> 6.	GPNRMMINK: 	Norm minimum per KG </para>
    /// <para> 7.	GPNRMMAXK: 	Norm maximum per KG </para>
    /// <para> 8.	GPABSMINK: 	Absoluut minimum per KG </para>
    /// <para> 9.	GPABSMAXK: 	Absoluut maximum per KG </para>
    /// <para> 10.	GPNRMMINM: 	Norm minimum per M2 </para>
    /// <para> 11.	GPNRMMAXM: 	Norm maximum per M2 </para>
    /// <para> 12.	GPABSMINM: 	Absoluut minimum per M2 </para>
    /// <para> 13.	GPABSMAXM: 	Absoluut maximum per M2 </para>
    /// </summary>
    module BST649T =

        let name = "BST649T"

        type BST649T =
            {
                MUTKOD : int
                GPDDNR : int
                GPNRMMIN : decimal
                GPNRMMAX : decimal
                GPABSMIN : decimal
                GPABSMAX : decimal
                GPNRMMINK : decimal
                GPNRMMAXK : decimal
                GPABSMINK : decimal
                GPABSMAXK : decimal
                GPNRMMINM : decimal
                GPNRMMAXM : decimal
                GPABSMINM : decimal
                GPABSMAXM : decimal
            }


        let create mutkod gpddnr gpnrmmin gpnrmmax gpabsmin gpabsmax gpnrmmink gpnrmmaxk gpabsmink gpabsmaxk gpnrmminm gpnrmmaxm gpabsminm gpabsmaxm  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPDDNR = gpddnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPNRMMIN = gpnrmmin |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPNRMMAX = gpnrmmax |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPABSMIN = gpabsmin |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPABSMAX = gpabsmax |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPNRMMINK = gpnrmmink |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPNRMMAXK = gpnrmmaxk |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPABSMINK = gpabsmink |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPABSMAXK = gpabsmaxk |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPNRMMINM = gpnrmminm |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPNRMMAXM = gpnrmmaxm |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPABSMINM = gpabsminm |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
                GPABSMAXM = gpabsmaxm |> ((Parser.parseValue "N" "(8,3)") >> Decimal.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7;8;9;10;11;12;13;14]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8) (xs |> Array.item 9) (xs |> Array.item 10) (xs |> Array.item 11) (xs |> Array.item 12) (xs |> Array.item 13))

        let records : unit -> BST649T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST701T: Bestand 701 Ingegeven samenstellingen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	HPKODE: 	Handels Product Kenmerken (HPK) code </para>
    /// <para> 2.	GNVOLG: 	Volgnummer </para>
    /// <para> 3.	GNMWHS: 	Aanduiding werkzaam/hulpstof (W/H) </para>
    /// <para> 4.	GNGNK: 	GeneriekeNaamKode (GNK) </para>
    /// <para> 5.	GNMINH: 	Hoeveelheid werkzame stof </para>
    /// <para> 6.	THMINE: 	Eenh. hvh werkz.stof - thesaurus 2 </para>
    /// <para> 7.	XNMINE: 	Eenh.hoeveelheid werkzame stof kode </para>
    /// <para> 8.	GNSTAM: 	Stamnaamcode (SNK) </para>
    /// <para> 9.	THSTWG: 	Stamtoedieningsweg - thesaurus 58 </para>
    /// <para> 10.	SSKTWG: 	Stamtoedieningsweg code </para>
    /// </summary>
    module BST701T =

        let name = "BST701T"

        type BST701T =
            {
                MUTKOD : int
                HPKODE : int
                GNVOLG : int
                GNMWHS : string
                GNGNK : int
                GNMINH : decimal
                THMINE : int
                XNMINE : int
                GNSTAM : int
                THSTWG : int
                SSKTWG : int
            }


        let create mutkod hpkode gnvolg gnmwhs gngnk gnminh thmine xnmine gnstam thstwg ssktwg  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                HPKODE = hpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GNVOLG = gnvolg |> ((Parser.parseValue "N" "") >> Int32.parse)
                GNMWHS = gnmwhs |> String.trim
                GNGNK = gngnk |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                GNMINH = gnminh |> ((Parser.parseValue "N" "(9,3)") >> Decimal.parse)
                THMINE = thmine |> ((Parser.parseValue "N" "") >> Int32.parse)
                XNMINE = xnmine |> ((Parser.parseValue "N" "") >> Int32.parse)
                GNSTAM = gnstam |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                THSTWG = thstwg |> ((Parser.parseValue "N" "") >> Int32.parse)
                SSKTWG = ssktwg |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7;8;9;10;11]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8) (xs |> Array.item 9) (xs |> Array.item 10))

        let records : unit -> BST701T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST711T: Bestand 711 Generieke producten </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GPKODE: 	Generiekeproductcode (GPK) </para>
    /// <para> 2.	GSKODE: 	GSK-code </para>
    /// <para> 3.	GPKTVR: 	Farmaceutische vorm code </para>
    /// <para> 4.	GPKTWG: 	Toedieningsweg code </para>
    /// <para> 5.	GPNMNR: 	Naamnummer volledige GPK-naam </para>
    /// <para> 6.	GPMLCI: 	Min. leeftijd als contra-indicatie </para>
    /// <para> 7.	GPMLCT: 	Min.leeftijd als CI tekstnummer </para>
    /// <para> 8.	SPKODE: 	SuperProduktKode (SPK) </para>
    /// <para> 9.	ATCODE: 	ATC-code </para>
    /// <para> 10.	XPEHHV: 	Basiseenheid product kode </para>
    /// </summary>
    module BST711T =

        let name = "BST711T"

        type BST711T =
            {
                MUTKOD : int
                GPKODE : int
                GSKODE : int
                GPKTVR : int
                GPKTWG : int
                GPNMNR : int
                GPMLCI : int
                GPMLCT : int
                SPKODE : int
                ATCODE : string
                XPEHHV : int
            }


        let create mutkod gpkode gskode gpktvr gpktwg gpnmnr gpmlci gpmlct spkode atcode xpehhv  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPKODE = gpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GSKODE = gskode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GPKTVR = gpktvr |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPKTWG = gpktwg |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPNMNR = gpnmnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPMLCI = gpmlci |> ((Parser.parseValue "N" "") >> Int32.parse)
                GPMLCT = gpmlct |> ((Parser.parseValue "N" "") >> Int32.parse)
                SPKODE = spkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                ATCODE = atcode
                XPEHHV = xpehhv |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;5;7;8;11;12;17;20;22]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8) (xs |> Array.item 9) (xs |> Array.item 10))

        let records : unit -> BST711T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST715T: Bestand 715 Generieke samenstellingen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GNMWHS: 	Aanduiding werkzaam/hulpstof (W/H) </para>
    /// <para> 2.	GSKODE: 	GSK-code </para>
    /// <para> 3.	GNNKPK: 	Volledige generieke naam kode </para>
    /// <para> 4.	GNMOMH: 	Omgerekende hoeveelheid </para>
    /// <para> 5.	XNMOME: 	Eenh omgerekende hoeveelheid kode </para>
    /// <para> 6.	XPEHHV: 	Basiseenheid product kode </para>
    /// </summary>
    module BST715T =

        let name = "BST715T"

        type BST715T =
            {
                MUTKOD : int
                GNMWHS : string
                GSKODE : int
                GNNKPK : int
                GNMOMH : decimal
                XNMOME : int
                XPEHHV : int
            }


        let create mutkod gnmwhs gskode gnnkpk gnmomh xnmome xpehhv  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GNMWHS = gnmwhs |> String.trim
                GSKODE = gskode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GNNKPK = gnnkpk |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                GNMOMH = gnmomh |> ((Parser.parseValue "N" "(9,3)") >> Decimal.parse)
                XNMOME = xnmome |> ((Parser.parseValue "N" "") >> Int32.parse)
                XPEHHV = xpehhv |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6))

        let records : unit -> BST715T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST720T: Bestand 720 Superprodukten </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	SPKODE: 	SuperProduktKode (SPK) </para>
    /// <para> 2.	SSKODE: 	SSK-kode </para>
    /// </summary>
    module BST720T =

        let name = "BST720T"

        type BST720T =
            {
                MUTKOD : int
                SPKODE : int
                SSKODE : int
            }


        let create mutkod spkode sskode  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                SPKODE = spkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                SSKODE = sskode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2))

        let records : unit -> BST720T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST725T: Bestand 725 Stamnaam + stamtoedieningsweg </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	SSKODE: 	SSK-kode </para>
    /// <para> 2.	GNSTAM: 	Stamnaamcode (SNK) </para>
    /// <para> 3.	SSKTWG: 	Stamtoedieningsweg code </para>
    /// </summary>
    module BST725T =

        let name = "BST725T"

        type BST725T =
            {
                MUTKOD : int
                SSKODE : int
                GNSTAM : int
                SSKTWG : int
            }


        let create mutkod sskode gnstam ssktwg  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                SSKODE = sskode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                GNSTAM = gnstam |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                SSKTWG = ssktwg |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3))

        let records : unit -> BST725T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST750T: Bestand 750 Generieke namen </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	GNGNK: 	GeneriekeNaamKode (GNK) </para>
    /// <para> 2.	GNGNAM: 	Generieke naam </para>
    /// <para> 3.	GNSTAM: 	Stamnaamcode (SNK) </para>
    /// <para> 4.	GNNKPK: 	Volledige generieke naam kode </para>
    /// <para> 5.	GNSTNT: 	Kode stamnaam toegestaan </para>
    /// <para> 6.	GNWZHS: 	Kode werkzaam bestanddeel/hulpstof </para>
    /// <para> 7.	GNSTKD: 	Informatorium stof kode </para>
    /// <para> 8.	GNCAS: 	CAS nummer </para>
    /// <para> 9.	GNFORM: 	Bruto formule </para>
    /// <para> 10.	GNMOLE: 	Molekuulgewicht (echt) </para>
    /// <para> 11.	GNMOLI: 	Molekuulgewicht indicator </para>
    /// <para> 12.	GNMOLS: 	Molekuulgewicht voor samenstelling </para>
    /// <para> 13.	GNSGEW: 	Soortelijk gewicht </para>
    /// <para> 14.	GNVOOR: 	Voorkeurseenheid </para>
    /// </summary>
    module BST750T =

        let name = "BST750T"

        type BST750T =
            {
                MUTKOD : int
                GNGNK : int
                GNGNAM : string
                GNSTAM : int
                GNNKPK : int
                GNSTNT : string
                GNWZHS : string
                GNSTKD : int
                GNCAS : int
                GNFORM : string
                GNMOLE : decimal
                GNMOLI : string
                GNMOLS : decimal
                GNSGEW : decimal
                GNVOOR : string
            }


        let create mutkod gngnk gngnam gnstam gnnkpk gnstnt gnwzhs gnstkd gncas gnform gnmole gnmoli gnmols gnsgew gnvoor  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                GNGNK = gngnk |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                GNGNAM = gngnam |> String.trim
                GNSTAM = gnstam |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                GNNKPK = gnnkpk |> ((Parser.parseValue "N" "(5+1)") >> Int32.parse)
                GNSTNT = gnstnt |> String.trim
                GNWZHS = gnwzhs |> String.trim
                GNSTKD = gnstkd |> ((Parser.parseValue "N" "") >> Int32.parse)
                GNCAS = gncas |> ((Parser.parseValue "N" "(8+1)") >> Int32.parse)
                GNFORM = gnform |> String.trim
                GNMOLE = gnmole |> ((Parser.parseValue "N" "(8,4)") >> Decimal.parse)
                GNMOLI = gnmoli |> String.trim
                GNMOLS = gnmols |> ((Parser.parseValue "N" "(8,4)") >> Decimal.parse)
                GNSGEW = gnsgew |> ((Parser.parseValue "N" "(2,5)") >> Decimal.parse)
                GNVOOR = gnvoor |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7;8;9;10;11;12;13;14;15]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8) (xs |> Array.item 9) (xs |> Array.item 10) (xs |> Array.item 11) (xs |> Array.item 12) (xs |> Array.item 13) (xs |> Array.item 14))

        let records : unit -> BST750T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST760T: Bestand 760 Enkelvoudige toedieningswegen HPK </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	HPKODE: 	Handels Product Kenmerken (HPK) code </para>
    /// <para> 2.	ENKTDW: 	Enkelvoudige toedieningsweg itemnr </para>
    /// </summary>
    module BST760T =

        let name = "BST760T"

        type BST760T =
            {
                MUTKOD : int
                HPKODE : int
                ENKTDW : int
            }


        let create mutkod hpkode enktdw  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                HPKODE = hpkode |> ((Parser.parseValue "N" "(7+1)") >> Int32.parse)
                ENKTDW = enktdw |> ((Parser.parseValue "N" "") >> Int32.parse)
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;5]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2))

        let records : unit -> BST760T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST801T: Bestand 801 ATC codes </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	ATCODE: 	ATC-code </para>
    /// <para> 2.	ATOMS: 	ATC-Nederlandse omschrijving </para>
    /// <para> 3.	ATOMSE: 	ATC-Engelse omschrijving </para>
    /// <para> 4.	ATKIND: 	ATC-indicator </para>
    /// </summary>
    module BST801T =

        let name = "BST801T"

        type BST801T =
            {
                MUTKOD : int
                ATCODE : string
                ATOMS : string
                ATOMSE : string
                ATKIND : string
            }


        let create mutkod atcode atoms atomse atkind  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                ATCODE = atcode
                ATOMS = atoms |> String.trim
                ATOMSE = atomse |> String.trim
                ATKIND = atkind |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4))

        let records : unit -> BST801T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST902T: Bestand 902 Thesauri totaal </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	TSNR: 	Thesaurusnummer (in nieuwe thesauri) </para>
    /// <para> 2.	TSITNR: 	Thesaurus itemnummer (in nieuwe thesauri) </para>
    /// <para> 3.	THNM25: 	Naam item 25 posities </para>
    /// <para> 4.	THNM50: 	Naam item 50 posities </para>
    /// </summary>
    module BST902T =

        let name = "BST902T"

        type BST902T =
            {
                MUTKOD : int
                TSNR : int
                TSITNR : int
                THNM25 : string
                THNM50 : string
            }


        let create mutkod tsnr tsitnr thnm25 thnm50  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                TSNR = tsnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                TSITNR = tsitnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                THNM25 = thnm25 |> String.trim
                THNM50 = thnm50 |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;7;8]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4))

        let records : unit -> BST902T [] = Memoization.memoize _records





    /// <summary>
    /// <para> Tabel: BST921T: Bestand 921 Tekstblokken ASCII (vervangt 920) </para>
    /// <para> --------------- </para>
    /// <para> 0.	MUTKOD: 	Mutatiecode </para>
    /// <para> 1.	THMODU: 	Thesaurus verwijzing tekstmodule (=103) </para>
    /// <para> 2.	TXMODU: 	Tekstmodule </para>
    /// <para> 3.	THTSRT: 	Thesaurus verwijzing tekstsoort (=104) </para>
    /// <para> 4.	TXTSRT: 	Tekstsoort </para>
    /// <para> 5.	TXKODE: 	Tekst nivo kode </para>
    /// <para> 6.	TXBLNR: 	Tekstbloknummer </para>
    /// <para> 7.	TXRGLN: 	Tekstregelnummer </para>
    /// <para> 8.	TXTEXT: 	Tekst </para>
    /// </summary>
    module BST921T =

        let name = "BST921T"

        type BST921T =
            {
                MUTKOD : int
                THMODU : int
                TXMODU : int
                THTSRT : int
                TXTSRT : int
                TXKODE : int
                TXBLNR : int
                TXRGLN : int
                TXTEXT : string
            }


        let create mutkod thmodu txmodu thtsrt txtsrt txkode txblnr txrgln txtext  =
            {
                MUTKOD = mutkod |> ((Parser.parseValue "N" "") >> Int32.parse)
                THMODU = thmodu |> ((Parser.parseValue "N" "") >> Int32.parse)
                TXMODU = txmodu |> ((Parser.parseValue "N" "") >> Int32.parse)
                THTSRT = thtsrt |> ((Parser.parseValue "N" "") >> Int32.parse)
                TXTSRT = txtsrt |> ((Parser.parseValue "N" "") >> Int32.parse)
                TXKODE = txkode |> ((Parser.parseValue "N" "") >> Int32.parse)
                TXBLNR = txblnr |> ((Parser.parseValue "N" "") >> Int32.parse)
                TXRGLN = txrgln |> ((Parser.parseValue "N" "") >> Int32.parse)
                TXTEXT = txtext |> String.trim
            }


        let posl = BST001T.getPosl name

        let pickList = [1;2;3;4;5;6;7;8;9]

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map (fun xs -> create  (xs |> Array.item 0) (xs |> Array.item 1) (xs |> Array.item 2) (xs |> Array.item 3) (xs |> Array.item 4) (xs |> Array.item 5) (xs |> Array.item 6) (xs |> Array.item 7) (xs |> Array.item 8))

        let records : unit -> BST921T [] = Memoization.memoize _records



