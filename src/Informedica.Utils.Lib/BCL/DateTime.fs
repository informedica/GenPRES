namespace Informedica.Utils.Lib.BCL


[<RequireQualifiedAccess>]
module DateTime =

    open System


    //----------------------------------------------------------------------------
    // Identity create functions
    //----------------------------------------------------------------------------

    /// Utility function to apply a function f to a DateTime dt
    let apply f (dt: DateTime) = f dt


    /// Utility function to enable type inference
    let get = apply id


    /// Create a date from a year, month and day
    let create (yr: int) mo ds = DateTime(yr, mo, ds)


    /// Create a DateTime from Now
    let now () = DateTime.Now


    /// Create a DateTime using only the date part of a DateTime.
    /// I.e. round the time part to 00:00:00
    let date (dt: DateTime) = DateTime(dt.Year, dt.Month, dt.Day)


    /// Try to create a DateTime from a year, month and day option.
    /// When one is None, None is returned
    let optionToDate (yr: int option) mo dy =
        match yr, mo, dy with
        | Some y, Some m, Some d -> DateTime(y, m, d) |> Some
        | _ -> None


    //----------------------------------------------------------------------------
    // Constants
    //----------------------------------------------------------------------------


    let daysInWeek = 7


    //----------------------------------------------------------------------------
    // Calculation functions
    //----------------------------------------------------------------------------


    /// Add years to a DateTime
    let addYears yr dt = (dt |> get).AddYears(yr)


    /// Add months to a DateTime
    let addMonths mo dt = (dt |> get).AddMonths(mo)


    /// Add weeks to a DateTime
    let addWeeks wk dt = (dt |> get).AddDays((float wk) * (float daysInWeek))


    /// Add days to a DateTime
    let addDays (ds: int) dt = (dt |> get).AddDays(ds |> float)


    /// Calculate the difference between two DateTimes
    /// where the first is subtracted with the second,
    /// i.e. dt1 - dt2
    let dateDiff dt1 dt2 = (dt1 |> get) - (dt2 |> get)


    /// Calculate the difference between two DateTimes in days
    /// where the first is subtracted with the second, i.e.
    /// dt1 - dt2
    let dateDiffDays dt1 dt2 = (dateDiff dt1 dt2).Days


    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------


    /// Return a string representation of a DateTime
    let formattedString (s: String) (dt: DateTime) = dt.ToString(s)


    //----------------------------------------------------------------------------
    // Age functions
    //----------------------------------------------------------------------------


    /// Calculate the age between two DateTimes
    /// in years, months, weeks and days such that
    /// date1 = date2 + age by adding first years,
    /// then months, then weeks and finally days.
    /// Note if date1 > date2 then date2 will be
    /// used as first date!
    let age (date1: DateTime) (date2: DateTime) =
        let correct y m w d dtFirst dtLast =
            let dtFirst = dtFirst |> addYears y |> addMonths m |> addWeeks w |> addDays d
            d + (dtLast - dtFirst).Days

        let dtLast, dtFirst = if date1 > date2 then date1, date2 else date2, date1

        let y, date2 = dtLast.Year - dtFirst.Year, dtFirst.AddYears(dtLast.Year - dtFirst.Year)

        let y, date2 =
            if (dtLast - date2).Days < 0 then
                y - 1, date2.AddYears(-1)
            else
                y, date2

        let m, date2 =
            if dtLast.Year = date2.Year then
                dtLast.Month - date2.Month, date2.AddMonths(dtLast.Month - date2.Month)
            else
                (12 - date2.Month) + dtLast.Month, date2.AddMonths((12 - date2.Month) + dtLast.Month)

        let m, date2 =
            if (dtLast - date2).Days < 0 then
                m - 1, date2.AddMonths(-1)
            else
                m, date2

        let d = (dtLast - date2).Days
        let d, w = d % 7, d / 7

        y, m, w, correct y m w d dtFirst dtLast
