namespace Informedica.ZForm.Lib


[<AutoOpen>]
module Utils =


    module MinIncrMax =

        open MathNet.Numerics
        
        open Informedica.GenUnits.Lib
        open Informedica.GenCore.Lib.Ranges


        let ageToString { Min = min; Max = max } =
            let oneWk = 1N |> ValueUnit.createSingle Units.Time.week
            let oneMo = 1N |> ValueUnit.createSingle Units.Time.month
            let oneYr = 1N |> ValueUnit.createSingle Units.Time.year

            let convert =
                let c vu =
                    match vu with
                    | _ when vu <? oneWk -> vu ==> Units.Time.day
                    | _ when vu <? oneMo -> vu ==> Units.Time.week
                    | _ when vu <? oneYr -> vu ==> Units.Time.month
                    | _ -> vu ==> Units.Time.year
                Option.bind (Limit.apply c c >> Some)

            { Min = min |> convert; Incr = None; Max = max |> convert } |> MinIncrMax.toString "van " "van " "tot " "tot "


        let gestAgeToString { Min = min; Max = max } =

            let convert =
                let c vu = vu ==> Units.Time.week
                Option.bind (Limit.apply c c >> Some)

            { Min = min |> convert; Incr = None;  Max = max |> convert } |> MinIncrMax.toString "van " "van " "tot " "tot "



    module Web =


        open Informedica.Utils.Lib
        open Informedica.ZIndex.Lib

        // Constraints spreadsheet GenPres
        //https://docs.google.com/spreadsheets/d/1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g/edit?usp=sharing
        [<Literal>]
        let dataUrlId = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        let download = Web.GoogleSheets.download


        let getDataFromSheet sheet =
            sheet 
            |> Web.GoogleSheets.getDataFromSheet FilePath.genpres

        