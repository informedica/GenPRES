namespace Informedica.ZForm.Lib


[<AutoOpen>]
module Utils =


    module MinMax =

        open MathNet.Numerics

        open Informedica.GenUnits.Lib
        open Informedica.GenCore.Lib.Ranges


        /// Print a MinIncrMax value as an age string.
        let ageToString minIncrMax =
            let { Min = min; Max = max } = minIncrMax

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

            { Min = min |> convert; Max = max |> convert } |> MinMax.toString "van " "van " "tot " "tot "


        /// Print a MinIncrMax value as a gestational age string.
        let gestAgeToString minIncrMax =
            let { Min = min; Max = max } = minIncrMax

            let convert =
                let c vu = vu ==> Units.Time.week
                Option.bind (Limit.apply c c >> Some)

            { Min = min |> convert; Max = max |> convert } |> MinMax.toString "van " "van " "tot " "tot "



    module Web =


        open Informedica.Utils.Lib
        open Informedica.ZIndex.Lib


        // Constraints spreadsheet GenPres
        //https://docs.google.com/spreadsheets/d/1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g/edit?usp=sharing
        [<Literal>]
        let dataUrlId = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        /// <summary>
        /// Get the data from the GenPres sheet.
        /// </summary>
        /// <param name="sheet">The sheet name</param>
        let getDataFromSheet sheet =
            sheet
            |> Web.GoogleSheets.getDataFromSheet FilePath.genpres

