namespace Informedica.Utils.Lib.BCL


[<RequireQualifiedAccess>]
module DateTime =

    open System

    let apply f (dt: DateTime) = f dt


    let get = apply id


    let create yr mo ds = DateTime(yr, mo, ds)


    let now () = DateTime.Now


    let date (dt: DateTime) = DateTime(dt.Year, dt.Month, dt.Day)


    let daysInYear () = ((now ()).AddYears(1) - now ()).Days


    let daysInMonth () = daysInYear () / 12


    let daysInWeek = 7


    let addYears yr dt =
        (dt |> get).AddYears(yr)


    let addMonths mo dt =
        (dt |> get).AddMonths(mo)


    let addWeeks wk dt =
        (dt |> get).AddDays((float wk) * (float daysInWeek))


    let addDays (ds: int) dt =
        (dt |> get).AddDays(ds |> float)


    let age (date1:DateTime) (date2: DateTime) =
        let correct y m w d dtFirst dtLast =
            let dtFirst =
                dtFirst
                |> addYears y
                |> addMonths m
                |> addWeeks w
                |> addDays d
            d + (dtLast - dtFirst).Days

        let dtLast, dtFirst =
            if date1 > date2 then date1, date2 else date2, date1

        let y, date2 = dtLast.Year - dtFirst.Year, dtFirst.AddYears(dtLast.Year - dtFirst.Year)
        let y, date2 =
            if (dtLast - date2).Days < 0 then y - 1, date2.AddYears(-1) else y, date2

        let m, date2 =
            if dtLast.Year = date2.Year then
                dtLast.Month - date2.Month, date2.AddMonths(dtLast.Month - date2.Month)
            else
                (12 - date2.Month) + dtLast.Month, date2.AddMonths((12 - date2.Month) + dtLast.Month)
        let m, date2 =
            if (dtLast - date2).Days < 0 then m - 1, date2.AddMonths(-1) else m, date2

        let d = (dtLast - date2).Days
        let d, w = d % 7, d / 7

        y, m, w,
        correct y m w d dtFirst dtLast


    let ageToDate yr mo wk ds dt =
        (dt |> get)
        |> addYears (-1 * yr)
        |> addMonths (-1 * mo)
        |> addWeeks (-1 * wk)
        |> addDays (-1 * ds)


    let ageNow = age DateTime.Now


    let ageToString  years months weeks days age =
        let pluralize n s =
            match n with
            | 0 -> ""
            | 1 -> n.ToString() + " " + (s |> fst)
            | _ -> n.ToString() + " " + (s |> snd)
        let yr, mo, wk, d = age
        let s =
            match yr, mo with
            | _ when yr > 10 -> pluralize yr years
            | _ when yr > 0  -> pluralize yr years + " " + pluralize mo months
            | _ when mo > 0  -> pluralize mo months + " " + pluralize wk weeks
            | _              -> pluralize wk weeks + " " + pluralize d days
        s.Trim()


    let ageToStringDutch = ageToString ("jaar", "jaar")
                                       ("maand", "maanden")
                                       ("week", "weken")
                                       ("dag", "dagen")


    let getAgeFromDate = ageNow >> ageToStringDutch


    let formattedString (s: String) (dt : DateTime) =
        dt.ToString(s)


    let optionToDate yr mo dy =
        match yr, mo, dy with
        | Some y, Some m, Some d -> new DateTime(y, m, d) |> Some
        | _ -> None


    let dateDiff dt1 dt2 = (dt1 |> get) - (dt2 |> get)


    let dateDiffDays dt1 dt2 = (dateDiff dt1 dt2).Days


    let dateDiffMonths dt1 dt2 =
        (dateDiffDays dt1 dt2)
        |> float
        |> (fun x -> x / 365.)
        |> ((*) 12.)


    let dateDiffYearsMonths dt1 dt2 =
        let mos = (dateDiffMonths dt1 dt2) |> int
        (mos / 12), (mos % 12)
