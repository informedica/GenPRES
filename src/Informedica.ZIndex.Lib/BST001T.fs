namespace Informedica.ZIndex.Lib


/// Functions to use the BST001T file which
/// contains per file which fields can be parsed
/// Each field is defined by a fixed length
module BST001T =

    open System

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    [<Literal>]
    let name = "BST001T"

    let posl = [ 0004; 0001; 0020; 0003; 0010; 0050; 0008; 0002; 0001; 0004; 0002; 0006; 0017 ]


    /// Tabel: BST001T: Bestand 001 Rubrieken
    /// ---------------
    /// 0.   BSTNUM:     Bestand-nummer
    /// 1.   MUTKOD:     Mutatiecode
    /// 2.   MDBST:      Naam van het Bestand
    /// 3.   MDVNR:      Volgnummer
    /// 4.   MDRNAM:     Naam van de rubriek
    /// 5.   MDROMS:     Omschrijving van de rubriek
    /// 6.   MDRCOD:     RubrieksCode
    /// 7.   MDRSLE:     Sleutelkode van de rubriek
    /// 8.   MDRTYP:     Type van de rubriek
    /// 9.   MDRLEN:     Lengte van de rubriek
    /// 10.  MDRDEC:     Aantal decimalen
    /// 11.  MDROPM:     Opmaak
    type BST001T =
        {
            MUTKOD: string
            MDBST: string
            MDVNR: int
            MDRNAM: string
            MDROMS: string
            MDRSLE: string
            MDRTYP: string
            MDRLEN: int
            MDRDEC: int
            MDROPM: string
        }


    let create mk tb vn nm bs ke tp ln dc fm =
        {
            MUTKOD = mk
            MDBST  = tb
            MDVNR = vn
            MDRNAM = nm
            MDROMS = bs
            MDRSLE = ke
            MDRTYP = tp
            MDRLEN = ln
            MDRDEC = dc
            MDROPM = fm
        }


    let pickList = [1..5] @ [7..11]


    let data _ =
        FilePath.GStandPath + "/" + name
        |> File.readAllLines
        |> Array.filter (String.length >> ((<) 10))
        |> Array.map (Parser.splitRecord posl)
        |> Array.map (Array.map String.trim)
        |> Array.map (Array.pickArray pickList)


    let _data = data ()


    let records _ =
        _data
        |> Array.map (fun d ->
            let vn = Int32.Parse d[2]
            let ln = Int32.Parse d[7]
            let dc = Int32.Parse d[8]
            create d[0] d[1] vn d[3] d[4] d[5] d[6] ln dc d[9])
        |> Array.filter (fun r -> r.MUTKOD <> "1")
        |> Array.sortBy (fun r -> r.MDBST, r.MDVNR)


    let _records = records ()


    let getPosl name =
        _records
        |> Array.toList
        |> List.filter (fun d -> d.MDBST = name)
        |> List.map (fun d -> d.MDRLEN)


    let recordLength n =
        _records
        |> Seq.filter (fun r -> r.MDBST = n)
        |> Seq.sumBy (fun r -> r.MDRLEN)


    let columns n =
        _records
        |> Seq.filter (fun r -> r.MDBST = n)
        |> Seq.filter (fun r -> r.MDRNAM <> "******")


    let columnCount n = columns n |> Seq.length


    let recordString n pl =
        let tab = "    "
        let s = $"type %s{n} =\n"
        let s = s + $"%s{tab}%s{tab}%s{tab}{{\n"
        let s =
            s + (
                columns n
                |> Seq.pickSeq pl
                |> Seq.fold (fun s c ->
                    let t =
                        if c.MDRTYP = "N" then
                            if c.MDROPM |> Parser.isDecimalFormat then "decimal" else "int"
                        else "string"
                    s + $"%s{tab}%s{tab}%s{tab}%s{tab}%s{c.MDRNAM} : %s{t}\n") "")
        s + $"%s{tab}%s{tab}%s{tab}}}\n"


    let createString n pl =
        let tab = "    "
        let cs =
            columns n
            |> Seq.pickSeq pl
        let args =
            cs
            |> Seq.fold (fun s c ->
                s + c.MDRNAM.ToLower() + " " ) ""

        let s = $"let create %s{args} =\n"
        let s = s + $"%s{tab}%s{tab}%s{tab}{{\n"
        let s =
            s + (
                cs
                |> Seq.fold (fun s c ->
                let m =
                    if c.MDRTYP = "N" then
                        let typ, opm = "\"" + c.MDRTYP + "\"", "\"" + c.MDROPM + "\""
                        if c.MDROPM |> Parser.isDecimalFormat then
                            $"|> ((Parser.parseValue %s{typ} %s{opm}) >> Decimal.parse)"
                        else
                            $"|> ((Parser.parseValue %s{typ} %s{opm}) >> Int32.parse)"
                    elif c.MDRNAM = "ATCODE" then ""
                    else "|> String.trim"
                s + $"%s{tab}%s{tab}%s{tab}%s{tab}%s{c.MDRNAM} = %s{c.MDRNAM.ToLower()} %s{m}\n") "")
        s + $"%s{tab}%s{tab}%s{tab}}}\n"
