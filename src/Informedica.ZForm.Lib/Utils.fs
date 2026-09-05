namespace Informedica.ZForm.Lib


module MinMax =

    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


    let private toString =
        MinMax.toString (ValueUnit.toStringDecimalDutchShortWithPrec 2) (ValueUnit.toStringDecimalDutchShortWithPrec 2)


    /// Print a MinIncrMax value as an age string.
    let ageToString minIncrMax =
        let {
                Min = min
                Max = max
            } =
            minIncrMax

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

        {
            Min = min |> convert
            Max = max |> convert
        }
        |> toString "van " "van " "tot " "tot "


    /// Print a MinIncrMax value as a gestational age string.
    let gestAgeToString minIncrMax =
        let {
                Min = min
                Max = max
            } =
            minIncrMax

        let convert =
            let c vu = vu ==> Units.Time.week
            Option.bind (Limit.apply c c >> Some)

        {
            Min = min |> convert
            Max = max |> convert
        }
        |> toString "van " "van " "tot " "tot "


module Web =


    open Informedica.Utils.Lib


    let private _genpresUrlId () =
        Env.loadDotEnv () |> ignore
        Env.getItem "GENPRES_URL_ID"


    /// The configured GENPRES_URL_ID, resolved on first use rather than at module
    /// initialisation, so it sees the process's final working directory and
    /// environment. Memoized, so .env is read at most once. See issue #523.
    let genpresUrlId: unit -> string option = Memoization.memoize _genpresUrlId


    /// <summary>
    /// Get the data from the GenPres sheet.
    /// </summary>
    /// <param name="sheet">The sheet name</param>
    let getDataFromSheet sheet =
        match genpresUrlId () with
        | None ->
            let msg = "Cannot load the GENPRES_URL_ID"
            ConsoleWriter.writeErrorMessage msg true false
            invalidOp msg
        | Some urlId ->
            sheet
            |> Web.GoogleSheets.getCsvDataFromSheetSync urlId
            |> Result.defaultValue [||]
