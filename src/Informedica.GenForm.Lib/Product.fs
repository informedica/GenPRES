namespace Informedica.GenForm.Lib



module Product =


    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module ATCGroup = Informedica.ZIndex.Lib.ATCGroup


    [<AutoOpen>]
    module Utils =

        let toBrs s =
            s
            |> String.splitAt ';'
            |> Array.choose Double.tryParse
            |> Array.choose BigRational.fromFloat


        let toBrOpt brs = brs |> Array.tryHead


        let tupleBrOpt brs1 brs2 =
            brs1 |> Array.tryHead,
            brs2 |> Array.tryHead



    module Location =


        let toString = function
            | PVL -> "PVL"
            | CVL -> "CVL"
            | AnyAccess -> ""


        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens "PVL" -> PVL
            | _ when s |> String.equalsCapInsens "CVL" -> CVL
            | _ -> AnyAccess



    module ShapeRoute =


        let private get_ () =
            Web.getDataFromSheet Web.dataUrlId2 "ShapeRoute"
            |> fun data ->

                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r
                    {
                        Shape = get "Shape"
                        Route = get "Route"
                        Unit = get "Unit"
                        DoseUnit = get "DoseUnit"
                        Timed = get "Timed" = "TRUE"
                        Reconstitute = get "Reconstitute" = "TRUE"
                        IsSolution = get "IsSolution" = "TRUE"
                    }
                )


        let get : unit -> ShapeRoute [] =
            Memoization.memoize get_


        let isSolution (srs : ShapeRoute []) shape  =
            srs
            |> Array.tryFind (fun sr ->
                sr.Shape |> String.equalsCapInsens shape
            )
            |> Option.map (fun sr -> sr.IsSolution)
            |> Option.defaultValue false



    module Reconstitution =

        // GPK
        // Route
        // DoseType
        // Dep
        // CVL
        // PVL
        // DiluentVol
        // ExpansionVol
        // Diluents
        let private get_ () =
            Web.getDataFromSheet Web.dataUrlId2 "Reconstitution"
            |> fun data ->

                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r
                    let toBrOpt = toBrs >> toBrOpt

                    {|
                        GPK = get "GPK"
                        Route = get "Route"
                        Location =
                            match get "CVL", get "PVL" with
                            | s1, _ when s1 |> String.isNullOrWhiteSpace |> not -> CVL
                            | _, s2 when s2 |> String.isNullOrWhiteSpace |> not -> PVL
                            | _ -> AnyAccess
                        DoseType = get "DoseType" |> DoseType.fromString
                        Dep = get "Dep"
                        DiluentVol = get "DiluentVol" |> toBrOpt
                        ExpansionVol = get "ExpansionVol" |> toBrOpt
                        Diluents = get "Diluents"
                    |}
                )


        let get =
            Memoization.memoize get_


        let filter (filter : Filter) (rs : Reconstitution []) =
            let eqs a b =
                a
                |> Option.map (fun x -> x = b)
                |> Option.defaultValue true

            [|
                fun (r : Reconstitution) -> r.Route |> eqs filter.Route
                fun (r : Reconstitution) ->
                    if filter.Location = AnyAccess then true
                    else
                        match filter.DoseType with
                        | AnyDoseType -> true
                        | _ -> filter.DoseType = r.DoseType
                fun (r : Reconstitution) -> r.Department |> eqs filter.Department
                fun (r : Reconstitution) ->
                    match r.Location, filter.Location with
                    | AnyAccess, _
                    | _, AnyAccess -> true
                    | _ -> r.Location = filter.Location
            |]
            |> Array.fold (fun (acc : Reconstitution[]) pred ->
                acc |> Array.filter pred
            ) rs


    module Parenteral =

        let private get_ () =
            Web.getDataFromSheet Web.dataUrlId2 "ParentMeds"
            |> fun data ->
                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r
                    let toBrOpt = toBrs >> toBrOpt

                    {|
                        Name = get "Name"
                        Substances =
                            [|
                                "volume mL", get "volume mL" |> toBrOpt
                                "energie kCal", get "energie kCal" |> toBrOpt
                                "eiwit g", get "eiwit g" |> toBrOpt
                                "KH g", get "KH g" |> toBrOpt
                                "vet g", get "vet g" |> toBrOpt
                                "Na mmol", get "Na mmol" |> toBrOpt
                                "K mmol", get "K mmol" |> toBrOpt
                                "Ca mmol", get "Ca mmol" |> toBrOpt
                                "P mmol", get "P mmol" |> toBrOpt
                                "Mg mmol", get "Mg mmol" |> toBrOpt
                                "Fe mmol", get "Fe mmol" |> toBrOpt
                                "VitD IE", get "VitD IE" |> toBrOpt
                                "Cl mmol", get "Cl mmol" |> toBrOpt

                            |]
                        Oplosmiddel = get "volume mL"
                        Verdunner = get "volume mL"
                    |}
                )
                |> Array.map (fun r ->
                    {
                        GPK =  r.Name
                        ATC = ""
                        MainGroup = ""
                        SubGroup = ""
                        Generic = r.Name
                        TallMan = "" //r.TallMan
                        Synonyms = [||]
                        Product = r.Name
                        Label = r.Name
                        Shape = ""
                        ShapeQuantities = [| 1N |]
                        ShapeUnit = "mL"
                        RequiresReconstitution = false
                        Reconstitution = [||]
                        Divisible = 10N |> Some
                        Substances =
                            r.Substances
                            |> Array.map (fun (s, q) ->
                                let n, u =
                                    match s |> String.split " " with
                                    | [n; u] -> n |> String.trim, u |> String.trim
                                    | _ -> failwith $"cannot parse substance {s}"
                                {
                                    Name = n
                                    Quantity = q
                                    Unit = u
                                    MultipleQuantity = None
                                    MultipleUnit = ""
                                }
                            )
                    }
                )


        let get : unit -> Product [] =
            Memoization.memoize get_



    let private get_ () =
        let isSol = ShapeRoute.isSolution (ShapeRoute.get ())

        Web.getDataFromSheet Web.dataUrlId2 "Formulary"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {|
                    GPKODE = get "GPKODE" |> Int32.parse
                    Apotheek = get "UMCU"
                    ICC = get "ICC"
                    NEO = get "NEO"
                    ICK = get "ICK"
                    HCK = get "HCK"
                |}
            )
            |> Array.collect (fun r ->
                r.GPKODE
                |> GenPresProduct.findByGPK
            )
            |> Array.collect (fun gpp ->
                gpp.GenericProducts
                |> Array.map (fun gp -> gpp, gp)
            )
            |> Array.map (fun (gpp, gp) ->
                let atc =
                    gp.ATC
                    |> ATCGroup.findByATC5 ()

                let su =
                    gp.Substances[0].ShapeUnit
                    |> String.toLower

                {
                    GPK =  $"{gp.Id}"
                    ATC = gp.ATC |> String.trim
                    MainGroup =
                        atc
                        |> Array.map (fun g -> g.AnatomicalGroup)
                        |> Array.tryHead
                        |> Option.defaultValue ""
                    SubGroup =
                        atc
                        |> Array.map (fun g -> g.TherapeuticSubGroup)
                        |> Array.tryHead
                        |> Option.defaultValue ""
                    Generic = gpp.Name |> String.toLower
                    TallMan = "" //r.TallMan
                    Synonyms =
                        gpp.GenericProducts
                        |> Array.collect (fun gp ->
                            gp.PrescriptionProducts
                            |> Array.collect (fun pp ->
                                pp.TradeProducts
                                |> Array.map (fun tp -> tp.Brand)
                            )
                        )
                        |> Array.distinct
                        |> Array.filter String.notEmpty
                    Product =
                        gp.PrescriptionProducts
                        |> Array.collect (fun pp ->
                            pp.TradeProducts
                            |> Array.map (fun tp -> tp.Label)
                        )
                        |> Array.distinct
                        |> function
                        | [| p |] -> p
                        | _ -> ""
                    Label = gp.Label
                    Shape = gp.Shape |> String.toLower
                    ShapeQuantities =
                        gpp.GenericProducts
                        |> Array.collect (fun gp ->
                            gp.PrescriptionProducts
                            |> Array.map (fun pp -> pp.Quantity)
                            |> Array.map BigRational.fromDecimal
                        )
                        |> Array.filter (fun br -> br > 0N)
                        |> Array.distinct
                        |> fun xs ->
                            if xs |> Array.isEmpty then [| 1N |] else xs
                    ShapeUnit =
                        gp.Substances[0].ShapeUnit
                        |> Mapping.mapUnit
                        |> Option.defaultValue ""
                    RequiresReconstitution =
                        Mapping.requiresReconstitution gp.Route su gp.Shape
                    Reconstitution =
                        Reconstitution.get ()
                        |> Array.filter (fun r ->
                            r.GPK = $"{gp.Id}" &&
                            r.DiluentVol |> Option.isSome
                        )
                        |> Array.map (fun r ->
                            {
                                Route = r.Route
                                DoseType = r.DoseType
                                Department = r.Dep
                                Location = r.Location
                                DiluentVolume = r.DiluentVol.Value
                                ExpansionVolume = r.ExpansionVol
                                Diluents =
                                    r.Diluents
                                    |> String.splitAt ';'
                                    |> Array.map String.trim
                            }
                        )
                    Divisible =
                        // TODO: need to map this to a config setting
                        if gp.Shape |> String.contains "DRUPPEL" then Some 20N
                        else
                            if isSol gp.Shape then 10N |> Some
                                else Some 1N
                    Substances =
                        gp.Substances
                        |> Array.map (fun s ->
                            {
                                Name =
                                    s.SubstanceName
                                    |> String.toLower
                                Quantity =
                                    s.SubstanceQuantity
                                    |> BigRational.fromDecimal
                                    |> Some
                                Unit =
                                    s.SubstanceUnit
                                    |> Mapping.mapUnit
                                    |> Option.defaultValue ""
                                MultipleQuantity = None
                                MultipleUnit = ""
                            }
                        )
                }
            )


    let get : unit -> Product [] =
        Memoization.memoize get_


    let reconstitute rte dtp dep loc (prod : Product) =
        if prod.RequiresReconstitution |> not then None
        else
            prod.Reconstitution
            |> Array.filter (fun r ->
                (rte |> String.isNullOrWhiteSpace || r.Route |> String.equalsCapInsens rte) &&
                (r.DoseType = AnyDoseType || r.DoseType = dtp) &&
                (dep |> String.isNullOrWhiteSpace || r.Department |> String.equalsCapInsens dep) &&
                (r.Location = AnyAccess || r.Location = loc)
            )
            |> Array.map (fun r ->
                { prod with
                    ShapeUnit = "milliliter"
                    ShapeQuantities = [| r.DiluentVolume |]
                    Substances =
                        prod.Substances
                        |> Array.map (fun s ->
                            { s with
                                Quantity =
                                    s.Quantity
                                    |> Option.map (fun q -> q / r.DiluentVolume)
                            }
                        )
                }
            )
            |> function
            | [| p |] -> Some p
            | _       -> None


    let filter (filter : Filter) (prods : Product []) =
        let repl s =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""

        let eqs s1 s2 =
            match s1, s2 with
            | Some s1, s2 ->
                let s1 = s1 |> repl
                let s2 = s2 |> repl
                s1 |> String.equalsCapInsens s2
            | _ -> false

        prods
        |> Array.filter (fun p -> p.Generic |> eqs filter.Generic && p.Shape |> eqs filter.Shape)
        |> Array.map (fun p ->
            { p with
                Reconstitution =
                    p.Reconstitution
                    |> Reconstitution.filter filter
            }
        )


    let generics (products : Product array) =
        products
        |> Array.map (fun p ->
            p.Generic
        )
        |> Array.distinct


    let synonyms (products : Product array) =
        products
        |> Array.collect (fun p ->
            p.Synonyms
        )
        |> Array.append (generics products)
        |> Array.distinct


    let shapes  (products : Product array) =
        products
        |> Array.map (fun p -> p.Shape)
        |> Array.distinct


