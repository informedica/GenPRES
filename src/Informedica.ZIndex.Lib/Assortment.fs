namespace Informedica.ZIndex.Lib


module Assortment =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.Web



    let create gpk gen tall div =
        {
            GPK = gpk 
            Generic = gen
            TallMan = tall
            Divisible = div
        }


    let get_ () =
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


    let assortment : unit -> Assortment [] = Memoization.memoize get_