namespace Informedica.ZIndex.Lib


/// The BST00T file contains all the
/// available files that need to be
/// parsed to tables
module BST000T =

    open System

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    let name = "BST000T"


    // Tabel: BST000T: Bestand 000 Bestanden
    // ---------------
    // 0.   BSTNUM:     Bestand-nummer
    // 1.   MUTKOD:     Mutatiecode
    // 2.   MDBST:      Naam van het Bestand
    // 3.   MDOBST:     Bestand-omschrijving
    // 4.   MDBCOD:     BestandsCode
    // 5.   MDRECL:     Recordlengte
    // 6.   MDDATI:     Ingangsdatum
    // 7.   MDDATW:     Eindedatum uitlevering
    // 8.   MDDATU:     Uitgavedatum
    // 9.   MDSTAT:     Status (in voorbereiding, test, productie)
    // 10.  MDANM0:     Aantal ongewijzigde records
    // 11.  MDANM1:     Aantal vervallen records
    // 12.  MDANM2:     Aantal gewijzigde records
    // 13.  MDANM3:     Aantal nieuwe records
    // 14.  MDANTL:     Totaal aantal records
    type BST000T =
        {
            MUTKOD: string
            MDBST: string
            MDOBST: string
            MDRECL: int
            MDSTAT: string
            MDANM0: int
            MDANM1: int
            MDANM2: int
            MDANM3: int
            MDANTL: int
        }


    let create mc nm ds rl st ua dl al nw tt =
        {
            MUTKOD = mc
            MDBST  = nm
            MDOBST = ds
            MDRECL = rl
            MDSTAT = st
            MDANM0 = ua
            MDANM1 = dl
            MDANM2 = al
            MDANM3 = nw
            MDANTL = tt
        }


    let posl = BST001T.getPosl name


    let pickList = [1] @ [2; 3] @ [5] @ [9..14]


    let records _ =
        Parser.getData name posl pickList
        |> Array.map (Array.map String.trim)
        |> Array.map (fun d ->
            let rl = Int32.Parse d[3]
            let ua = Int32.Parse d[5]
            let dl = Int32.Parse d[6]
            let al = Int32.Parse d[7]
            let nw = Int32.Parse d[8]
            let tt = Int32.Parse d[9]
            create d[0] d[1] d[2] rl d[4] ua dl al nw tt)


    let table n =
        records ()
        |> Array.find (fun r -> r.MDBST = n)


    let commentString n pl =
        let tab = "    "
        let d =
            let t = table n

            if t.MDRECL <> BST001T.recordLength n then
                $"Calculated record length: %i{t.MDRECL}, is not equal to table record length: %i{BST001T.recordLength n}"
                |> failwith
            t.MDOBST

        let i = ref -1
        let s = $"\n%s{tab}/// <summary>"
        let s = s + $"\n%s{tab}/// <para> Tabel: %s{n}: %s{d} </para>\n" + "    /// <para> --------------- </para>\n"
        (BST001T.columns n
        |> Seq.pickSeq pl
        |> Seq.fold (fun s c ->
                i.Value <- i.Value + 1
                s + $"%s{tab}/// <para> %i{i.Value}.\t%s{c.MDRNAM}: \t%s{c.MDROMS} </para>\n"
            ) s) + "    /// </summary>"
