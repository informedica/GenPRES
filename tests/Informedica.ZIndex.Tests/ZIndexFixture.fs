namespace Informedica.ZIndex.Tests

open System
open System.IO


/// Generates synthetic Z-Index BST fixture files for testing.
module ZIndexFixture =

    // ── String helpers ────────────────────────────────────────────────────

    /// Pad or truncate a string to exactly n chars by right-padding with spaces.
    let padR (n: int) (s: string) =
        if s.Length >= n then s.Substring(0, n) else s.PadRight(n)

    /// Pad or truncate a number string to exactly n chars by left-padding with spaces.
    let padL (n: int) (s: string) =
        if s.Length >= n then
            s.Substring(s.Length - n, n)
        else
            s.PadLeft(n)

    // ── BST001T record builder ────────────────────────────────────────────

    let private bst001Record bstNum mdbst mdvnr mdrnam mdroms mdrtyp mdrlen mdrdec mdropm =
        padL 4 bstNum
        + "0"
        + padR 20 mdbst
        + padL 3 (string mdvnr)
        + padR 10 mdrnam
        + padR 50 mdroms
        + "        "
        + "  "
        + padR 1 mdrtyp
        + padL 4 (string mdrlen)
        + padL 2 (string mdrdec)
        + padR 6 mdropm
        + padR 17 ""

    let private tableDefs bstNum bstName (fields: (string * string * string * int * int * string) list) =
        fields
        |> List.mapi (fun i (nm, ds, tp, ln, dc, fm) -> bst001Record bstNum bstName (i + 1) nm ds tp ln dc fm)

    // ── BST001T field definitions for each synthetic table ────────────────

    let private bst020TDefs =
        tableDefs
            "0020"
            "BST020T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "NMNR", "Naamnummer", "N", 7, 0, ""
                "NMMEMO", "Memokode", "A", 3, 0, ""
                "NMETIK", "Etiketnaam", "A", 25, 0, ""
                "NMNM40", "Korte handelsnaam", "A", 40, 0, ""
                "NMNAAM", "Naam volledig", "A", 60, 0, ""
            ]

    let private bst360TDefs =
        tableDefs
            "0360"
            "BST360T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "TTEHNR", "Uniek nummer tijdseenheid", "N", 5, 0, ""
                "SKIP1", "Omschrijving kode", "N", 5, 0, ""
                "TTEHOM", "Omschrijving tijdseenheid", "A", 25, 0, ""
            ]

    let private bst380TDefs =
        tableDefs
            "0380"
            "BST380T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "ICPCNR1", "ICPC1-nummer", "N", 6, 0, ""
                "SKIP1", "Tekstkode", "N", 5, 0, ""
                "ICPCTXT", "ICPC omschrijving", "A", 60, 0, ""
            ]

    let private bst640TDefs =
        tableDefs
            "0640"
            "BST640T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GPKODE", "Generiekproductcode", "N", 8, 0, "(7+1)"
                "SKIP1", "GPK naam nummer", "N", 4, 0, ""
                "SKIP2", "Indicatiekode", "N", 4, 0, ""
                "SKIP3", "Indicatietekst nummer", "N", 4, 0, ""
                "GPDGST", "Geslacht code", "N", 1, 0, ""
                "SKIP4", "Risicogroep kode", "N", 4, 0, ""
                "GPRISC", "Hoog risico kode", "A", 2, 0, ""
            ]

    let private bst641TDefs =
        tableDefs
            "0641"
            "BST641T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GPKODE", "Generiekproductcode", "N", 8, 0, "(7+1)"
                "PRKODE", "PRK-code", "N", 8, 0, "(7+1)"
                "HPKODE", "HPK-code", "N", 8, 0, "(7+1)"
                "GPDCTH", "Thesaurus dosering kode", "N", 4, 0, ""
                "GPDCOD", "Soort doseringscode", "N", 4, 0, ""
                "GPDBAS", "Dosis basisnummer", "N", 6, 0, ""
            ]

    let private bst642TDefs =
        tableDefs
            "0642"
            "BST642T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GPDBAS", "Dosis basisnummer", "N", 6, 0, ""
                "GPDID1", "Identificerend volgnummer", "N", 6, 0, ""
                "SKIP1", "Zorggroep skip", "N", 4, 0, ""
                "GPDZCO", "Zorggroep codering", "N", 5, 0, ""
                "ICPCNR1", "ICPC1-nummer", "N", 6, 0, ""
                "SKIP2", "Verbijzondering skip", "N", 4, 0, ""
                "ICPCTO", "Verbijzondering", "N", 2, 0, ""
                "SKIP3", "Skip 3", "N", 4, 0, ""
                "SKIP4", "Skip 4", "N", 4, 0, ""
                "GPKTTH", "Thesaurus toedieningsweg", "N", 5, 0, ""
                "GPKTWG", "Toedieningsweg kode", "N", 5, 0, ""
                "GPDCAT", "Dosis categorienummer", "N", 6, 0, ""
            ]

    let private bst643TDefs =
        tableDefs
            "0643"
            "BST643T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GPDCAT", "Dosis categorienummer", "N", 6, 0, ""
                "GPDID2", "Identificerend recordnummer", "N", 6, 0, ""
                "GPDLFM", "Leeftijd vanaf (maanden)", "N", 6, 2, "(4,2)"
                "GPDLFX", "Leeftijd tot (maanden)", "N", 6, 2, "(4,2)"
                "GPDKGM", "Gewicht vanaf (kg)", "N", 6, 3, "(3,3)"
                "GPDKGX", "Gewicht tot (kg)", "N", 6, 3, "(3,3)"
                "GPDM2M", "Oppervlakte vanaf m2", "N", 6, 3, "(3,3)"
                "GPDM2X", "Oppervlakte tot m2", "N", 6, 3, "(3,3)"
                "GPDFAA", "Frequentie aantal", "N", 4, 2, "(2,2)"
                "GPDFEE", "Frequentie tijdseenheid", "N", 4, 0, ""
                "GPDDEN", "Denekamp berekening", "A", 1, 0, ""
                "GPDDNR", "Dosisnummer", "N", 6, 0, ""
            ]

    let private bst649TDefs =
        tableDefs
            "0649"
            "BST649T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GPDDNR", "Dosisnummer", "N", 6, 0, ""
                "GPNRMMIN", "Norm minimum", "N", 11, 3, "(8,3)"
                "GPNRMMAX", "Norm maximum", "N", 11, 3, "(8,3)"
                "GPABSMIN", "Absoluut minimum", "N", 11, 3, "(8,3)"
                "GPABSMAX", "Absoluut maximum", "N", 11, 3, "(8,3)"
                "GPNRMMINK", "Norm min per KG", "N", 11, 3, "(8,3)"
                "GPNRMMAXK", "Norm max per KG", "N", 11, 3, "(8,3)"
                "GPABSMINK", "Absoluut min per KG", "N", 11, 3, "(8,3)"
                "GPABSMAXK", "Absoluut max per KG", "N", 11, 3, "(8,3)"
                "GPNRMMINM", "Norm min per M2", "N", 11, 3, "(8,3)"
                "GPNRMMAXM", "Norm max per M2", "N", 11, 3, "(8,3)"
                "GPABSMINM", "Absoluut min per M2", "N", 11, 3, "(8,3)"
                "GPABSMAXM", "Absoluut max per M2", "N", 11, 3, "(8,3)"
            ]

    let private bst701TDefs =
        tableDefs
            "0701"
            "BST701T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "HPKODE", "HPK-code", "N", 8, 0, "(7+1)"
                "GNVOLG", "Volgnummer", "N", 4, 0, ""
                "GNMWHS", "Werkzaam/hulpstof", "A", 1, 0, ""
                "GNGNK", "GeneriekeNaamKode", "N", 6, 0, "(5+1)"
                "GNMINH", "Hoeveelheid werkzame stof", "N", 12, 3, "(9,3)"
                "THMINE", "Eenheid thesaurus", "N", 4, 0, ""
                "XNMINE", "Eenheid kode", "N", 4, 0, ""
                "GNSTAM", "Stamnaamcode", "N", 6, 0, "(5+1)"
                "THSTWG", "Toedieningsweg thesaurus", "N", 4, 0, ""
                "SSKTWG", "Toedieningsweg kode", "N", 4, 0, ""
            ]

    let private bst711TDefs =
        tableDefs
            "0711"
            "BST711T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GPKODE", "Generiekproductcode", "N", 8, 0, "(7+1)"
                "GSKODE", "GSK-code", "N", 8, 0, "(7+1)"
                "SKIP1", "Skip 1", "N", 4, 0, ""
                "GPKTVR", "Farmaceutische vorm", "N", 5, 0, ""
                "SKIP2", "Skip 2", "N", 4, 0, ""
                "GPKTWG", "Toedieningsweg kode", "N", 5, 0, ""
                "GPNMNR", "Naamnummer GPK", "N", 7, 0, ""
                "SKIP3", "Skip 3", "N", 4, 0, ""
                "SKIP4", "Skip 4", "N", 4, 0, ""
                "GPMLCI", "Min leeftijd CI", "N", 5, 0, ""
                "GPMLCT", "Min leeftijd CI tekst", "N", 6, 0, ""
                "SKIP5", "Skip 5", "N", 4, 0, ""
                "SKIP6", "Skip 6", "N", 4, 0, ""
                "SKIP7", "Skip 7", "N", 4, 0, ""
                "SKIP8", "Skip 8", "N", 4, 0, ""
                "SPKODE", "SuperProductKode", "N", 8, 0, "(7+1)"
                "SKIP9", "Skip 9", "N", 4, 0, ""
                "SKIP10", "Skip 10", "N", 4, 0, ""
                "ATCODE", "ATC-code", "A", 7, 0, ""
                "SKIP11", "Skip 11", "N", 4, 0, ""
                "XPEHHV", "Basiseenheid product", "N", 5, 0, ""
            ]

    let private bst715TDefs =
        tableDefs
            "0715"
            "BST715T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GNMWHS", "Werkzaam/hulpstof", "A", 1, 0, ""
                "GSKODE", "GSK-code", "N", 8, 0, "(7+1)"
                "GNNKPK", "Volledige generieke naam", "N", 6, 0, "(5+1)"
                "GNMOMH", "Omgerekende hoeveelheid", "N", 12, 3, "(9,3)"
                "XNMOME", "Eenheid omgerekend", "N", 4, 0, ""
                "XPEHHV", "Basiseenheid product", "N", 5, 0, ""
            ]

    let private bst720TDefs =
        tableDefs
            "0720"
            "BST720T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "SPKODE", "SuperProductKode", "N", 8, 0, "(7+1)"
                "SSKODE", "SSK-kode", "N", 8, 0, "(7+1)"
            ]

    let private bst725TDefs =
        tableDefs
            "0725"
            "BST725T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "SSKODE", "SSK-kode", "N", 8, 0, "(7+1)"
                "GNSTAM", "Stamnaamcode", "N", 6, 0, "(5+1)"
                "SSKTWG", "Stamtoedieningsweg", "N", 5, 0, ""
            ]

    let private bst750TDefs =
        tableDefs
            "0750"
            "BST750T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "GNGNK", "GeneriekeNaamKode", "N", 6, 0, "(5+1)"
                "GNGNAM", "Generieke naam", "A", 40, 0, ""
                "GNSTAM", "Stamnaamcode", "N", 6, 0, "(5+1)"
                "GNNKPK", "Volledige naam kode", "N", 6, 0, "(5+1)"
                "GNSTNT", "Stamnaam toegestaan", "A", 1, 0, ""
                "GNWZHS", "Werkzaam/hulpstof", "A", 1, 0, ""
                "GNSTKD", "Informatorium kode", "N", 6, 0, ""
                "GNCAS", "CAS nummer", "N", 9, 0, "(8+1)"
                "GNFORM", "Bruto formule", "A", 20, 0, ""
                "GNMOLE", "Molekuulgewicht echt", "N", 12, 4, "(8,4)"
                "GNMOLI", "Molekuulgewicht indicator", "A", 1, 0, ""
                "GNMOLS", "Molekuulgewicht samenstelling", "N", 12, 4, "(8,4)"
                "GNSGEW", "Soortelijk gewicht", "N", 7, 5, "(2,5)"
                "GNVOOR", "Voorkeurseenheid", "A", 6, 0, ""
            ]

    let private bst760TDefs =
        tableDefs
            "0760"
            "BST760T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "HPKODE", "HPK-code", "N", 8, 0, "(7+1)"
                "SKIP1", "Thesaurus toedieningsweg", "N", 4, 0, ""
                "SKIP2", "Toedieningsweg skip", "N", 4, 0, ""
                "ENKTDW", "Enkelvoudige toedieningsweg", "N", 5, 0, ""
            ]

    let private bst801TDefs =
        tableDefs
            "0801"
            "BST801T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "ATCODE", "ATC-code", "A", 7, 0, ""
                "ATOMS", "ATC-NL omschrijving", "A", 60, 0, ""
                "ATOMSE", "ATC-EN omschrijving", "A", 60, 0, ""
                "ATKIND", "ATC-indicator", "A", 1, 0, ""
            ]

    let private bst902TDefs =
        tableDefs
            "0902"
            "BST902T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "TSNR", "Thesaurusnummer", "N", 4, 0, ""
                "TSITNR", "Thesaurus itemnummer", "N", 6, 0, ""
                "SKIP1", "Skip 1", "N", 4, 0, ""
                "SKIP2", "Skip 2", "N", 5, 0, ""
                "SKIP3", "Skip 3", "A", 10, 0, ""
                "THNM25", "Naam item 25 posities", "A", 25, 0, ""
                "THNM50", "Naam item 50 posities", "A", 50, 0, ""
            ]

    let private bst922TDefs =
        tableDefs
            "0922"
            "BST922T"
            [
                "BSTNUM", "Bestand nummer", "N", 4, 0, ""
                "MUTKOD", "Mutatiecode", "N", 1, 0, ""
                "THMODU", "Thesaurus tekstmodule", "N", 4, 0, ""
                "TXMODU", "Tekstmodule", "N", 6, 0, ""
                "THTSRT", "Thesaurus tekstsoort", "N", 4, 0, ""
                "TXTSRT", "Tekstsoort", "N", 6, 0, ""
                "TXKODE", "Tekst nivo kode", "N", 8, 0, ""
                "TXBLNR", "Tekstbloknummer", "N", 6, 0, ""
                "TXRGLN", "Tekstregelnummer", "N", 6, 0, ""
                "TXTEXT", "Tekst", "A", 200, 0, ""
            ]

    /// All BST001T lines for all synthetic tables in one list.
    let allBst001TLines =
        [
            bst020TDefs
            bst360TDefs
            bst380TDefs
            bst640TDefs
            bst641TDefs
            bst642TDefs
            bst643TDefs
            bst649TDefs
            bst701TDefs
            bst711TDefs
            bst715TDefs
            bst720TDefs
            bst725TDefs
            bst750TDefs
            bst760TDefs
            bst801TDefs
            bst902TDefs
            bst922TDefs
        ]
        |> List.concat

    // ── Field length arrays for buildRecord ───────────────────────────────

    let private bst020TLen = [| 4; 1; 7; 3; 25; 40; 60 |]
    let private bst360TLen = [| 4; 1; 5; 5; 25 |]
    let private bst380TLen = [| 4; 1; 6; 5; 60 |]
    let private bst640TLen = [| 4; 1; 8; 4; 4; 4; 1; 4; 2 |]
    let private bst641TLen = [| 4; 1; 8; 8; 8; 4; 4; 6 |]
    let private bst642TLen = [| 4; 1; 6; 6; 4; 5; 6; 4; 2; 4; 4; 5; 5; 6 |]
    let private bst643TLen = [| 4; 1; 6; 6; 6; 6; 6; 6; 6; 6; 4; 4; 1; 6 |]

    let private bst649TLen =
        [|
            4
            1
            6
            11
            11
            11
            11
            11
            11
            11
            11
            11
            11
            11
            11
        |]

    let private bst701TLen = [| 4; 1; 8; 4; 1; 6; 12; 4; 4; 6; 4; 4 |]

    let private bst711TLen =
        [|
            4
            1
            8
            8
            4
            5
            4
            5
            7
            4
            4
            5
            6
            4
            4
            4
            4
            8
            4
            4
            7
            4
            5
        |]

    let private bst715TLen = [| 4; 1; 1; 8; 6; 12; 4; 5 |]
    let private bst720TLen = [| 4; 1; 8; 8 |]
    let private bst725TLen = [| 4; 1; 8; 6; 5 |]
    let private bst750TLen = [| 4; 1; 6; 40; 6; 6; 1; 1; 6; 9; 20; 12; 1; 12; 7; 6 |]
    let private bst760TLen = [| 4; 1; 8; 4; 4; 5 |]
    let private bst801TLen = [| 4; 1; 7; 60; 60; 1 |]
    let private bst902TLen = [| 4; 1; 4; 6; 4; 5; 10; 25; 50 |]
    let private bst922TLen = [| 4; 1; 4; 6; 4; 6; 8; 6; 6; 200 |]

    /// Build one fixed-width record by padding/truncating each value
    /// to the corresponding field length and concatenating.
    let private buildRecord (lens: int[]) (vals: string[]) =
        if lens.Length <> vals.Length then
            invalidArg (nameof vals) $"buildRecord: field count mismatch — expected {lens.Length}, got {vals.Length}"

        Array.map2
            (fun (len: int) (v: string) ->
                if v.Length <= len then
                    v.PadRight(len)
                else
                    v.Substring(0, len)
            )
            lens
            vals
        |> String.concat ""

    // ── Numeric format helpers ────────────────────────────────────────────

    let private fmtDecimal (intPart: int) (decPart: int) (value: float) =
        let scale = pown 10.0 decPart
        let raw = int (Math.Round(value * scale, 0))
        (string raw).PadLeft(intPart + decPart, '0')

    let private fmtD42 (v: float) = fmtDecimal 4 2 v
    let private fmtD33 (v: float) = fmtDecimal 3 3 v
    let private fmtD22 (v: float) = fmtDecimal 2 2 v
    let private fmtD83 (v: float) = fmtDecimal 8 3 v
    let private fmtD84 (v: float) = fmtDecimal 8 4 v
    let private fmtD25 (v: float) = fmtDecimal 2 5 v
    let private fmtD93 (v: float) = fmtDecimal 9 3 v

    let private idFmt (n: int) (v: int) = "0" + (string v).PadLeft(n, '0')

    let private id51 v = idFmt 5 v
    let private id71 v = idFmt 7 v
    let private id81 v = idFmt 8 v

    // ── Per-table record builders ─────────────────────────────────────────

    let private bst020Record nmnr nmmemo nmetik nmnm40 nmnaam =
        buildRecord
            bst020TLen
            [|
                "0020"
                "0"
                (string nmnr).PadLeft(7)
                padR 3 nmmemo
                padR 25 nmetik
                padR 40 nmnm40
                padR 60 nmnaam
            |]

    let private bst360Record ttehnr ttehom =
        buildRecord
            bst360TLen
            [|
                "0360"
                "0"
                (string ttehnr).PadLeft(5)
                "     "
                padR 25 ttehom
            |]

    let private bst380Record icpcnr icpctxt =
        buildRecord
            bst380TLen
            [|
                "0380"
                "0"
                (string icpcnr).PadLeft(6)
                "     "
                padR 60 icpctxt
            |]

    let private bst640Record gpkode gpdgst gprisc =
        buildRecord
            bst640TLen
            [|
                "0640"
                "0"
                id71 gpkode
                "    "
                "    "
                "    "
                (string gpdgst)
                "    "
                padR 2 gprisc
            |]

    let private bst641Record gpkode prkode hpkode gpdcth gpdcod gpdbas =
        buildRecord
            bst641TLen
            [|
                "0641"
                "0"
                id71 gpkode
                id71 prkode
                id71 hpkode
                (string gpdcth).PadLeft(4)
                (string gpdcod).PadLeft(4)
                (string gpdbas).PadLeft(6)
            |]

    let private bst642Record gpdbas gpdid1 gpdzco icpcnr1 icpcto gpktth gpktwg gpdcat =
        buildRecord
            bst642TLen
            [|
                "0642"
                "0"
                (string gpdbas).PadLeft(6)
                (string gpdid1).PadLeft(6)
                "    "
                (string gpdzco).PadLeft(5)
                (string icpcnr1).PadLeft(6)
                "    "
                (string icpcto).PadLeft(2)
                "    "
                "    "
                (string gpktth).PadLeft(5)
                (string gpktwg).PadLeft(5)
                (string gpdcat).PadLeft(6)
            |]

    let private bst643Record gpdcat gpdid2 gpdlfm gpdlfx gpdkgm gpdkgx gpdm2m gpdm2x gpdfaa gpdfee gpdden gpddnr =
        buildRecord
            bst643TLen
            [|
                "0643"
                "0"
                (string gpdcat).PadLeft(6)
                (string gpdid2).PadLeft(6)
                fmtD42 gpdlfm
                fmtD42 gpdlfx
                fmtD33 gpdkgm
                fmtD33 gpdkgx
                fmtD33 gpdm2m
                fmtD33 gpdm2x
                fmtD22 gpdfaa
                (string gpdfee).PadLeft(4)
                padR 1 gpdden
                (string gpddnr).PadLeft(6)
            |]

    let private bst649Record
        gpddnr
        nrmmin
        nrmmax
        absmin
        absmax
        nrmmink
        nrmmaxk
        absmink
        absmaxk
        nrmminm
        nrmmaxm
        absminm
        absmaxm
        =
        buildRecord
            bst649TLen
            [|
                "0649"
                "0"
                (string gpddnr).PadLeft(6)
                fmtD83 nrmmin
                fmtD83 nrmmax
                fmtD83 absmin
                fmtD83 absmax
                fmtD83 nrmmink
                fmtD83 nrmmaxk
                fmtD83 absmink
                fmtD83 absmaxk
                fmtD83 nrmminm
                fmtD83 nrmmaxm
                fmtD83 absminm
                fmtD83 absmaxm
            |]

    let private bst701Record hpkode gnvolg gnmwhs gngnk gnminh thmine xnmine gnstam thstwg ssktwg =
        buildRecord
            bst701TLen
            [|
                "0701"
                "0"
                id71 hpkode
                (string gnvolg).PadLeft(4)
                padR 1 gnmwhs
                id51 gngnk
                fmtD93 gnminh
                (string thmine).PadLeft(4)
                (string xnmine).PadLeft(4)
                id51 gnstam
                (string thstwg).PadLeft(4)
                (string ssktwg).PadLeft(4)
            |]

    let private bst711Record gpkode gskode gpktvr gpktwg gpnmnr gpmlci gpmlct spkode atcode xpehhv =
        buildRecord
            bst711TLen
            [|
                "0711"
                "0"
                id71 gpkode
                id71 gskode
                "    "
                (string gpktvr).PadLeft(5)
                "    "
                (string gpktwg).PadLeft(5)
                (string gpnmnr).PadLeft(7)
                "    "
                "    "
                (string gpmlci).PadLeft(5)
                (string gpmlct).PadLeft(6)
                "    "
                "    "
                "    "
                "    "
                id71 spkode
                "    "
                "    "
                padR 7 atcode
                "    "
                (string xpehhv).PadLeft(5)
            |]

    let private bst715Record gnmwhs gskode gnnkpk gnmomh xnmome xpehhv =
        buildRecord
            bst715TLen
            [|
                "0715"
                "0"
                padR 1 gnmwhs
                id71 gskode
                id51 gnnkpk
                fmtD93 gnmomh
                (string xnmome).PadLeft(4)
                (string xpehhv).PadLeft(5)
            |]

    let private bst720Record spkode sskode =
        buildRecord bst720TLen [| "0720"; "0"; id71 spkode; id71 sskode |]

    let private bst725Record sskode gnstam ssktwg =
        buildRecord
            bst725TLen
            [|
                "0725"
                "0"
                id71 sskode
                id51 gnstam
                (string ssktwg).PadLeft(5)
            |]

    let private bst750Record
        gngnk
        gngnam
        gnstam
        gnnkpk
        gnstnt
        gnwzhs
        gnstkd
        gncas
        gnform
        gnmole
        gnmoli
        gnmols
        gnsgew
        gnvoor
        =
        buildRecord
            bst750TLen
            [|
                "0750"
                "0"
                id51 gngnk
                padR 40 gngnam
                id51 gnstam
                id51 gnnkpk
                padR 1 gnstnt
                padR 1 gnwzhs
                (string gnstkd).PadLeft(6)
                id81 gncas
                padR 20 gnform
                fmtD84 gnmole
                padR 1 gnmoli
                fmtD84 gnmols
                fmtD25 gnsgew
                padR 6 gnvoor
            |]

    let private bst760Record hpkode enktdw =
        buildRecord
            bst760TLen
            [|
                "0760"
                "0"
                id71 hpkode
                "    "
                "    "
                (string enktdw).PadLeft(5)
            |]

    let private bst801Record atcode atoms atomse atkind =
        buildRecord
            bst801TLen
            [|
                "0801"
                "0"
                padR 7 atcode
                padR 60 atoms
                padR 60 atomse
                padR 1 atkind
            |]

    let private bst902Record tsnr tsitnr thnm25 thnm50 =
        buildRecord
            bst902TLen
            [|
                "0902"
                "0"
                (string tsnr).PadLeft(4)
                (string tsitnr).PadLeft(6)
                "    "
                "     "
                "          "
                padR 25 thnm25
                padR 50 thnm50
            |]

    let private bst922Record thmodu txmodu thtsrt txtsrt txkode txblnr txrgln txtext =
        buildRecord
            bst922TLen
            [|
                "0922"
                "0"
                (string thmodu).PadLeft(4)
                (string txmodu).PadLeft(6)
                (string thtsrt).PadLeft(4)
                (string txtsrt).PadLeft(6)
                (string txkode).PadLeft(8)
                (string txblnr).PadLeft(6)
                (string txrgln).PadLeft(6)
                padR 200 txtext
            |]

    // ── Synthetic data records ────────────────────────────────────────────

    let private thesaurusRecords =
        [
            bst902Record 2 1 "mg" "milligram"
            bst902Record 2 2 "g" "gram"
            bst902Record 2 3 "ml" "milliliter"
            bst902Record 2 4 "mmol" "millimol"
            bst902Record 2 5 "microg" "microgram"
            bst902Record 2 6 "IE" "internationale eenheid"
            bst902Record 2 7 "mval" "milliequivalent"
            bst902Record 2 8 "druppels" "druppels"
            bst902Record 2 9 "puf" "puf"
            bst902Record 7 1 "oraal" "oraal"
            bst902Record 7 2 "intraveneus" "intraveneus"
            bst902Record 7 3 "inhalatie" "inhalatie"
            bst902Record 7 4 "rectaal" "rectaal"
            bst902Record 7 5 "subcutaan" "subcutaan"
            bst902Record 7 6 "intramusculair" "intramusculair"
            bst902Record 7 7 "transdermaal" "transdermaal"
            bst902Record 7 8 "intranasaal" "intranasaal"
            bst902Record 7 9 "oogdruppels" "oogdruppels"
            bst902Record 7 10 "oordruppels" "oordruppels"
            bst902Record 6 1 "tablet" "tablet"
            bst902Record 6 2 "pdr iv" "poeder voor injectieoplossing"
            bst902Record 6 3 "stroop" "stroop"
            bst902Record 6 4 "capsule" "capsule"
            bst902Record 6 5 "zetpil" "zetpil"
            bst902Record 6 6 "inj. vlst" "injectievloeistof"
            bst902Record 6 7 "druppels" "druppels"
            bst902Record 4 1 "fl" "flacon"
            bst902Record 4 2 "strip" "strip"
            bst902Record 4 3 "ampul" "ampul"
            bst902Record 4 4 "tube" "tube"
            bst902Record 73 1 "fl" "flacon"
            bst902Record 73 2 "strip" "strip"
            bst902Record 73 3 "ampul" "ampul"
        ]

    let private nameRecords =
        [
            bst020Record 1001 "PCM" "paracetamol tablet" "paracetamol tablet" "paracetamol tablet 500 mg"
            bst020Record 1002 "MER" "meropenem" "meropenem" "meropenem 500 mg"
            bst020Record 1003 "PCM" "paracetamol zetpil" "paracetamol zetpil" "paracetamol zetpil 250 mg"
            bst020Record 1004 "PCM" "paracetamol drank" "paracetamol drank" "paracetamol drank 24 mg/ml"
            bst020Record 1005 "AMO" "amoxicilline capsule" "amoxicilline capsule" "amoxicilline capsule 500 mg"
        ]

    let private substanceRecords =
        [
            bst750Record 11036 "PARACETAMOL" 11036 11036 "J" "W" 1234 0 "C8H9NO2" 151.163 "E" 151.163 0.0 "1"
            bst750Record 47602 "NATRIUMCHLORIDE" 47602 47602 "J" "W" 5678 0 "ClNa" 58.44 "E" 58.44 0.0 "4"
            bst750Record 55700 "MEROPENEM" 55700 55700 "J" "W" 9012 0 "C17H25N3O5S" 383.46 "E" 383.46 0.0 "1"
            bst750Record 4059 "AMOXICILLINE" 4059 4059 "J" "W" 3456 0 "C16H19N3O5S" 365.4 "E" 365.4 0.0 "1"
        ]

    let private atcRecords =
        [
            bst801Record "N02BE01" "paracetamol" "paracetamol" "5"
            bst801Record "J01DH02" "meropenem" "meropenem" "5"
            bst801Record "J01CA04" "amoxicilline" "amoxicillin" "5"
        ]

    let private superProductRecords =
        [
            bst720Record 1001001 1001001
            bst720Record 1002001 1002001
            bst720Record 1003001 1003001
        ]

    let private stemNameRecords =
        [
            bst725Record 1001001 11036 1
            bst725Record 1002001 55700 2
            bst725Record 1003001 4059 1
        ]

    let private genericCompositionRecords =
        [
            bst715Record "W" 12345 11036 500.0 1 1
            bst715Record "W" 22222 11036 24.0 1 3
            bst715Record "W" 33333 11036 250.0 1 1
            bst715Record "W" 54321 55700 500.0 1 1
            bst715Record "W" 11111 4059 500.0 1 1
        ]

    let private genericProductRecords =
        [
            bst711Record 694554 12345 1 1 1001 0 0 1001001 "N02BE01" 1
            bst711Record 10066 54321 2 2 1002 0 0 1002001 "J01DH02" 1
            bst711Record 694546 33333 5 4 1003 0 0 1001001 "N02BE01" 1
            bst711Record 694449 22222 3 1 1004 0 0 1001001 "N02BE01" 3
            bst711Record 20168 11111 4 1 1005 0 0 1003001 "J01CA04" 1
        ]

    let private doseBaseRecords =
        [
            bst640Record 694554 0 "N"
            bst640Record 694546 0 "N"
            bst640Record 10066 0 "N"
        ]

    let private doseArticleRecords =
        [
            bst641Record 694554 2956600 2956608 0 0 1001
            bst641Record 694546 2956601 2956609 0 0 1002
            bst641Record 10066 2956602 2956610 0 0 1003
        ]

    let private doseExceptionRecords =
        [
            bst642Record 1001 1 0 0 0 0 1 2001
            bst642Record 1002 1 0 0 0 0 4 2002
            bst642Record 1003 1 0 0 0 0 2 2003
        ]

    let private doseCategoryRecords =
        [
            bst643Record 2001 1 0.0 9999.0 0.0 9999.0 0.0 9999.0 4.0 1 "N" 3001
            bst643Record 2002 1 0.0 9999.0 0.0 9999.0 0.0 9999.0 3.0 1 "N" 3002
            bst643Record 2003 1 216.0 9999.0 0.0 9999.0 0.0 9999.0 3.0 1 "N" 3003
        ]

    let private doseDataRecords =
        [
            bst649Record 3001 500.0 500.0 250.0 1000.0 10.0 15.0 5.0 20.0 0.0 0.0 0.0 0.0
            bst649Record 3002 250.0 250.0 125.0 500.0 10.0 15.0 5.0 20.0 0.0 0.0 0.0 0.0
            bst649Record 3003 500.0 500.0 250.0 1000.0 10.0 20.0 5.0 40.0 0.0 0.0 0.0 0.0
        ]

    let private timeUnitRecords =
        [
            bst360Record 1 "dag"
            bst360Record 2 "week"
            bst360Record 3 "maand"
            bst360Record 4 "uur"
            bst360Record 5 "nacht"
        ]

    let private icpcRecords = [ bst380Record 0 "alle indicaties" ]

    let private tradeCompositionRecords =
        [
            bst701Record 2956608 1 "W" 11036 500.0 1 1 11036 0 1
            bst701Record 2956609 1 "W" 11036 250.0 1 1 11036 0 4
            bst701Record 2956610 1 "W" 55700 500.0 1 1 55700 0 2
        ]

    let private tradeRouteRecords =
        [
            bst760Record 2956608 1
            bst760Record 2956609 4
            bst760Record 2956610 2
        ]

    let private textBlockRecords =
        [
            bst922Record
                103
                1
                104
                1
                694554
                1
                1
                "<p>Paracetamol is een veelgebruikt pijnstiller en koortsverlagend middel.</p>"
            bst922Record 103 2 104 1 10066 1 1 "<p>Meropenem is een breedspectrum-carbapenem antibioticum.</p>"
        ]

    // ── File writer ───────────────────────────────────────────────────────

    let private writeLines (path: string) (lines: string list) =
        let dir = Path.GetDirectoryName(path)

        if not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore

        File.WriteAllLines(path, lines)
        printfn $"  Written {path} ({lines.Length} records)"

    /// Generate all synthetic BST fixture files as BST*.test in the given directory.
    let generate (zindexDir: string) =
        printfn $"Generating synthetic Z-Index fixture files in:"
        printfn $"  {zindexDir}"
        printfn ""
        writeLines (Path.Combine(zindexDir, "BST001T.test")) allBst001TLines
        writeLines (Path.Combine(zindexDir, "BST020T.test")) nameRecords
        writeLines (Path.Combine(zindexDir, "BST360T.test")) timeUnitRecords
        writeLines (Path.Combine(zindexDir, "BST380T.test")) icpcRecords
        writeLines (Path.Combine(zindexDir, "BST640T.test")) doseBaseRecords
        writeLines (Path.Combine(zindexDir, "BST641T.test")) doseArticleRecords
        writeLines (Path.Combine(zindexDir, "BST642T.test")) doseExceptionRecords
        writeLines (Path.Combine(zindexDir, "BST643T.test")) doseCategoryRecords
        writeLines (Path.Combine(zindexDir, "BST649T.test")) doseDataRecords
        writeLines (Path.Combine(zindexDir, "BST701T.test")) tradeCompositionRecords
        writeLines (Path.Combine(zindexDir, "BST711T.test")) genericProductRecords
        writeLines (Path.Combine(zindexDir, "BST715T.test")) genericCompositionRecords
        writeLines (Path.Combine(zindexDir, "BST720T.test")) superProductRecords
        writeLines (Path.Combine(zindexDir, "BST725T.test")) stemNameRecords
        writeLines (Path.Combine(zindexDir, "BST750T.test")) substanceRecords
        writeLines (Path.Combine(zindexDir, "BST760T.test")) tradeRouteRecords
        writeLines (Path.Combine(zindexDir, "BST801T.test")) atcRecords
        writeLines (Path.Combine(zindexDir, "BST902T.test")) thesaurusRecords
        writeLines (Path.Combine(zindexDir, "BST922T.test")) textBlockRecords
        printfn ""
        printfn "BST*.test fixture files are ready."


    /// Result of setupForTest tracking which files were created and backed up.
    type FixtureResult =
        {
            // Files that were newly created (no pre-existing file)
            Created: string list
            // Files that were backed up: (original path, backup path)
            BackedUp: (string * string) list
        }


    /// Copy every BST*.test file to its corresponding name without the .test
    /// extension, making the synthetic data visible to the ZIndex parsers.
    /// Pre-existing files are backed up to .bak and restored in teardown.
    let setupForTest (zindexDir: string) : FixtureResult =
        printfn "Setting up fixture files for test run..."
        let created = System.Collections.Generic.List<string>()
        let backedUp = System.Collections.Generic.List<string * string>()

        Directory.GetFiles(zindexDir, "BST*.test")
        |> Array.iter (fun src ->
            let dest = Path.Combine(zindexDir, Path.GetFileNameWithoutExtension(src))

            if File.Exists(dest) then
                let backup = dest + ".bak"
                File.Copy(dest, backup, true)
                File.Copy(src, dest, true)
                backedUp.Add((dest, backup))
                printfn $"  Backed up {Path.GetFileName(dest)} → {Path.GetFileName(backup)}, replaced with test data"
            else
                File.Copy(src, dest)
                created.Add(dest)
                printfn $"  Copied {Path.GetFileName(src)} → {Path.GetFileName(dest)}"
        )

        {
            Created = created |> Seq.toList
            BackedUp = backedUp |> Seq.toList
        }


    /// Restore backed-up files and delete files created by setupForTest.
    let teardownAfterTest (result: FixtureResult) =
        printfn ""
        printfn "Tearing down temporary fixture files..."

        result.Created
        |> List.iter (fun f ->
            if File.Exists(f) then
                File.Delete(f)
                printfn $"  Deleted {Path.GetFileName(f)}"
        )

        result.BackedUp
        |> List.iter (fun (original, backup) ->
            if File.Exists(backup) then
                File.Copy(backup, original, true)
                File.Delete(backup)
                printfn $"  Restored {Path.GetFileName(original)} from backup"
        )

        printfn "Done."
