namespace Informedica.KinderFormularium.Lib


module Mapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib


    let validShapes =
        [
            "aerosol"
            "ampul"
            "blaasspoeling"
            "bruisgranulaat"
            "bruistablet"
            "capsule"
            "capsule maagsapresistent"
            "capsule met gereguleerde afgifte"
            "capsule, zacht"
            "concentraat voor drank"
            "concentraat voor oplossing voor infusie"
            "concentraat voor oplossing voor injectie/infusie"
            "creme"
            "creme voor vaginaal gebruik"
            "dispergeerbare tablet"
            "dragee"
            "drank"
            "druppels voor oraal gebruik"
            "emulsie voor cutaan gebruik"
            "emulsie voor injectie"
            "filmomhulde tablet"
            "gel"
            "gel voor gastro-enteraal gebruik"
            "gel voor oraal gebruik"
            "granulaat"
            "granulaat met gereguleerde afgifte"
            "granulaat voor orale suspensie"
            "infusiepoeder"
            "infusievloeistof"
            "inhalatiepoeder"
            "inhalatiepoeder voor nasaal gebruik"
            "injectie/infusieoplossing"
            "injectievloeistof"
            "kauwtablet"
            "klysma"
            "lyophilisaat voor oraal gebruik"
            "maagsapresistent granulaat"
            "maagsapresistente capsule"
            "maagsapresistente tablet"
            "neusdruppels"
            "neusspray"
            "omhulde tablet"
            "oogdruppels"
            "oogdruppels, suspensie"
            "ooggel"
            "oogzalf"
            "oordruppels"
            "oorzalf"
            "oplossing"
            "oplossing voor cutaan gebruik"
            "oplossing voor parenteraal/oraal gebruik"
            "pleister voor transdermaal gebruik"
            "poeder voor drank"
            "poeder voor injectie/infusieoplossing"
            "poeder voor injectievloeistof"
            "poeder voor oplossing voor infusie"
            "poeder voor oraal gebruik"
            "poeder voor orale suspensie"
            "poeder voor suspensie voor injectie"
            "poeder voor suspensie voor oraal/rectaal gebruik"
            "poeder voor vernevelvloeistof"
            "schuim voor cutaan gebruik"
            "schuim voor rectaal gebruik"
            "shampoo"
            "smelttablet"
            "spoeling voor urethraal gebruik"
            "stroop"
            "suspensie"
            "suspensie voor injectie"
            "suspensie voor oraal gebruik"
            "tablet"
            "tablet met gereguleerde afgifte"
            "tablet voor buccaal gebruik"
            "tablet voor klysma"
            "tablet voor sublinguaal gebruik"
            "vernevelvloeistof"
            "zalf"
            "zetpil"
            "zuigtablet"
        ]


    /// Mapping of long Z-index route names to short names
    let routeMapping =
        let dataUrlId = Web.getDataUrlIdGenPres ()
        Web.getDataFromSheet dataUrlId "Routes"
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
                    Long = get "ZIndex"
                    Short = get "ShortDutch"
                |}
            )


    /// Mapping of long Z-index unit names to short names
    let unitMapping =
        let dataUrlId = Web.getDataUrlIdGenPres ()
        Web.getDataFromSheet dataUrlId "Units"
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
                    Long = get "ZIndexUnitLong"
                    Short = get "Unit"
                    MV = get "MetaVisionUnit"
                    Group = get "Group"
                |}
            )


    let mapUnit s =
        if s |> String.isNullOrWhiteSpace then None
        else
            let s = s |> String.toLower |> String.trim
            unitMapping
            |> Array.tryFind (fun r ->
                r.Long = s ||
                r.Short = s ||
                r.MV = s
            )
            |> function
                | Some r -> $"{r.Short}[{r.Group}]" |> Units.fromString
                | None -> None


    /// Try to find mapping for a route
    let mapRoute rte =
        routeMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens rte ||
            r.Short |> String.equalsCapInsens rte

        )
        |> Option.map _.Long


    let eqsRoute r1 r2 =
        if r1 |> Option.isNone then true
        else
            match r1.Value |> mapRoute, r2 |> mapRoute with
            | Some r1, Some r2 -> r1 = r2
            | _ -> false


