namespace Informedica.ZIndex.Lib


module Route =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.Web

    open Types.Route


    let mapping_ () =
        GoogleSheets.getDataFromSheet FilePath.genpres "Routes"
        |> fun data ->
            data
            |> Array.tryHead
            |> function
            | None -> Array.empty
            | Some cs ->
                let getStr c r = Csv.getStringColumn cs r c

                data
                |> Array.skip 1
                |> Array.map (fun r ->
                    {|
                        name = r |> getStr "Route"
                        zindex = r |>  getStr "ZIndex"
                        product = r |> getStr "Products"
                        rule = r |> getStr "DoseRules"
                        short = r |> getStr "ShortDutch"
                    |}
                )
        |> Array.map (fun r ->
            {
                Route =
                    r.name
                    |> Reflection.fromString<Route.Route>
                    |> Option.defaultValue Route.NoRoute
                Name = r.name
                ZIndex = r.zindex
                Product = r.product
                Rule = r.rule
                Short = r.short
            }
        )


    /// <summary>
    /// Get the route mapping.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let routeMapping = Memoization.memoize mapping_


    /// Try find a route in the mapping.
    let tryFind mapping s =
        let eqs = String.equalsCapInsens s

        mapping
        |> Array.tryFind (fun r ->
            r.Name |> eqs ||
            r.ZIndex |> eqs ||
            r.Short |> eqs
        )



    /// Get the string representation of a route.
    let toString mapping r =
        $"%A{r}"
        |> tryFind mapping
        |> function
        | Some m -> m.ZIndex
        | None -> ""


    /// Get the route from a string.
    let fromString mapping s =
        s
        |> tryFind mapping
        |> function
        | Some m -> m.Route
        | None -> NoRoute


    /// Check if a route is in a list of routes.
    let routeExists mapping r rs =
        if r = NoRoute || r = NON_SPECIFIED then true
        else
            rs
            |> Array.map (fromString mapping)
            |> Array.exists ((=) r)
