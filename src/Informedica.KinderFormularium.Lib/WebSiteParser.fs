namespace Informedica.KinderFormularium.Lib



module WebSiteParser =

    open FSharp.Data
    open FSharp.Data.JsonExtensions

    open FSharpPlus
    open Informedica.Utils.Lib.BCL

    open FormularyParser


    // get all medications from Kinderformularium
    let kinderFormUrl = "https://www.kinderformularium.nl/geneesmiddelen.json"


    let _medications () =
        let res = JsonValue.Load(kinderFormUrl)
        [ for v in res do
            Drug.createDrug
                      (v?id.AsString())
                      ""
                      (v?generic_name.AsString())
                      (v?branded_name.AsString())
        ]
        |> List.distinctBy (fun m -> m.Id, m.Generic.Trim().ToLower())


    let medications : unit -> Drug.Drug list = Memoization.memoizeN _medications


    let drugUrl = sprintf "https://www.kinderformularium.nl/geneesmiddel/%s/%s?nolayout"


    let inline getDoc get id gen = drugUrl id gen |> get


    let getDocSync id gen =
        try
            getDoc HtmlDocument.Load id gen
            |> Some
        with
        | e ->
            printfn $"couldn't get {id} {gen}\n{e.ToString()}"
            None

    let getDocAsync = getDoc HtmlDocument.AsyncLoad


    let getParentFromNode n1 n2 =
        n1
        |> HtmlNode.descendants false (HtmlNode.elements >> Seq.exists ((=) n2))
        |> Seq.head

    let getParentFromDoc d n =
        d
        |> HtmlDocument.descendants false (HtmlNode.elements >> Seq.exists ((=) n))
        |> Seq.head


    let getItemType desc v d =
        d |> desc true (HtmlNode.hasAttribute "itemType" v)


    let getItemTypeFromDoc = getItemType HtmlDocument.descendants


    let getItemTypeFromNode = getItemType HtmlNode.descendants


    let getIndications d = d |> getItemTypeFromDoc "https://schema.org/MedicalIndication"


    let doseSchedule n = n |> getItemTypeFromNode "http://schema.org/DoseSchedule"


    let getItemProp v n =
        n |> HtmlNode.descendantsAndSelf true (HtmlNode.hasAttribute "itemprop" v)


    let getItemPropString v n =
        match n |> getItemProp v
                |> List.ofSeq with
        | h::_ -> h |> HtmlNode.innerText
        | _ -> ""


    let printFormulary pf =
        let meds = medications () |> List.tail

        for med in (meds |> List.take (meds |> List.length)) do
            $"\n\n\n========== %s{med.Generic} ===========\n"
            |> pf
            match getDocSync med.Id med.Generic with
            | Some doc ->
                for ind in doc |> getItemTypeFromDoc "http://schema.org/MedicalIndication" do
                    let name = ind |> getItemPropString "name"
                    $"\n-- Indication: %A{name}\n" |> pf
                    let ind' =
                        ind
                        |> getParentFromDoc doc
                        |> getParentFromDoc doc

                    for r in ind' |> getItemProp "administrationRoute" do
                        let route = r |> getItemPropString "administrationRoute"
                        $"\n-- Route: %A{route}\n"
                        |> pf
                        let r' = r |> getParentFromDoc doc

                        for dose in r' |> getItemTypeFromNode "http://schema.org/DoseSchedule" do
                            let targetPop = dose |> getItemPropString "targetPopulation"
                            let doseVals = dose |> getItemPropString "doseValue"
                            let doseUnits = dose |> getItemPropString "doseUnit"
                            let freq = dose |> getItemPropString "frequency"

                            $"Target Population: %s{targetPop}\n" |> pf
                            $"Dose: %s{doseVals} %s{doseUnits} %s{freq}\n" |> pf
            | None -> $"Couldn't get {med.Id} {med.Generic}" |> pf


    let parseDocForDoses i (m: Drug.Drug) doc =
        printfn $"{i}. parsing dose rules for: %s{m.Generic}"
        let atc =
            match doc
                |> getItemTypeFromDoc "http://schema.org/MedicalCode" |> List.ofSeq with
            | h::_-> h |> getItemPropString "codeValue"
            | _ -> ""

        let getPar = getParentFromDoc doc
        try
            { m with
                Atc = atc
                Doses =
                    [
                        for i in doc |> getItemTypeFromDoc "http://schema.org/MedicalIndication" do
                            let n = i |> getItemPropString "name"
                            let i' = i |> getPar |> getPar
                            yield
                                {
                                    Indication = n
                                    Routes =
                                        [
                                            for r in i' |> getItemProp "administrationRoute" do
                                                let n = r |> getItemPropString "administrationRoute"
                                                let r' = r |> getParentFromNode i'
                                                yield
                                                    {
                                                        Name = n
                                                        Schedules =
                                                            [
                                                                for s in r' |> getItemTypeFromNode "http://schema.org/DoseSchedule" do
                                                                    let tp = s |> getItemPropString "targetPopulation"
                                                                    let dv = s |> getItemPropString "doseValue"
                                                                    let du = s |> getItemPropString "doseUnit"
                                                                    let fr = s |> getItemPropString "frequency"
                                                                    yield
                                                                        {
                                                                            Drug.TargetText = tp |> String.trim
                                                                            Drug.Target = tp |> TargetParser.parse
                                                                            Drug.FrequencyText = fr |> String.trim
                                                                            Drug.Frequency = fr |> FrequencyParser.parse
                                                                            Drug.ValueText = dv |> String.trim
                                                                            Drug.Value = dv |> MinMaxParser.parse |> snd
                                                                            Drug.Unit = du |> String.trim
                                                                        }
                                                            ]
                                                    }
                                        ]
                            }
                    ]
            }
        with
        | _ -> m


    let addDoses i (m: Drug.Drug) =
        async {
            try
                let! doc = getDocAsync m.Id m.Generic
                return doc |> parseDocForDoses i m |> Some
            with
            | e ->
                let l =
                    if e.ToString().Length - 1 > 100 then 100
                    else e.ToString().Length - 1
                let e = e.ToString().Substring(0, l)
                printfn $"couldn't add doses for {m.Id}, {m.Generic} because of:\n{e}"
                return None
        }


    let _parseWebSite ns =
        match ns with
        | [] ->
            medications ()
            |> List.skip 1
        | _ ->
            medications ()
            |> List.filter (fun m ->
                ns |> List.exists (fun n ->
                    m.Generic |> String.startsWithCapsInsens n))
        |> fun meds ->
            let meds =
                meds
                |> List.sortBy (_.Generic.Trim().ToLower())
                |> List.chunkBySize 20

            let n = ref 1

            [|
                for m in meds do
                    yield!
                        m
                        |> List.mapi (fun i -> addDoses (i + n.Value))
                        |> Async.Parallel
                        |> Async.RunSynchronously
                        |> Array.choose id

                    n.Value <- n.Value + (m |> List.length)
            |]


    let cacheFormulary (ds : Drug.Drug []) =
        ds
        |> Json.serialize
        |> File.writeTextToFile File.cachePath


    let _getFormulary () =
        if File.cachePath |> File.exists then
            File.cachePath
            |> File.readAllLines
            |> String.concat ""
            |> Json.deSerialize<Drug.Drug[]>
        else
            let ds = _parseWebSite []
            ds |> cacheFormulary
            ds


    let getFormulary : unit -> Drug.Drug [] = Memoization.memoizeN _getFormulary

    let getRoutes () =
        getFormulary () //|> Array.length
        |> Array.collect (fun d ->
            d.Doses
            |> List.toArray
            |> Array.collect (fun dose ->
                dose.Routes
                |> List.map (_.Name)
                |> List.toArray
            )
        )
        |> Array.distinct
        |> Array.sort



    let getSchedules () =
        getFormulary () //|> Array.length
        |> Array.collect (fun d ->
            d.Doses
            |> List.toArray
            |> Array.collect (fun dose ->
                dose.Routes
                |> List.collect _.Schedules
                |> List.toArray
            )
        )
        |> Array.distinct
        |> Array.sort


    let getUnits () =
        getSchedules ()
        |> Array.map _.Unit
        |> Array.map (String.replace "," "")
        |> Array.distinct
        |> Array.sort
