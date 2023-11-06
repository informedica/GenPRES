namespace Informedica.ZIndex.Lib


module Assortment =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.Web



    /// <summary>
    /// Create an Assortment product, a generic
    /// product which is available in the assortment.
    /// </summary>
    /// <param name="gpk">The GPK code of the product.</param>
    /// <param name="gen">The generic name of the product.</param>
    /// <param name="tall">The tall man name of the product.</param>
    /// <param name="div">The divisibility of the product.</param>
    let create gpk gen tall div =
        {
            GPK = gpk
            Generic = gen
            TallMan = tall
            Divisible = div
        }


    let get_ () =
        fun () ->
            GoogleSheets.getDataFromSheet FilePath.genpres "Formulary"
            |> fun data ->
                data
                |> Array.tryHead
                |> function
                | None -> Array.empty
                | Some cs ->
                    let getStr c r = Csv.getStringColumn cs r c
                    let getInt c r = Csv.getInt32Column cs r c

                    data
                    |> Array.skip 1
                    |> Array.map (fun r ->
                        {|
                            gpk = r |> getInt "GPKODE"
                            generic = r |>  getStr "Generic"
                            tallMan = r |> getStr "TallMan"
                            divisible =
                                "Divisible"
                                |> Csv.getInt32OptionColumn cs r
                                |> Option.defaultValue 1
                        |}
                    )
            |> Array.map (fun r ->
                create r.gpk r.generic r.tallMan r.divisible
            )
        |> StopWatch.clockFunc "Getting Formulary"


    /// <summary>
    /// Gets the assortment of products.
    /// </summary>
    /// <remarks>
    /// This is a memoized function.
    /// </remarks>
    let assortment : unit -> Assortment [] = Memoization.memoize get_