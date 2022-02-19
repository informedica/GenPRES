namespace GenPres.Server

module Utils =
    open System

    module String =
        /// Apply `f` to string `s`
        let apply f (s: string) = f s

        /// Utility to enable type inference
        let get = apply id

        /// Count the number of times that a
        /// string t starts with character c
        let countFirstChar c t =
            let _, count =
                if String.IsNullOrEmpty(t) then
                    (false, 0)
                else
                    t
                    |> Seq.fold
                        (fun (flag, dec) c' ->
                            if c' = c && flag then
                                (true, dec + 1)
                            else
                                (false, dec)
                        )
                        (true, 0)

            count

        /// Check if string `s2` contains string `s1`
        let contains = fun (s1: string) (s2: string) -> (s2 |> get).Contains(s1)

        let toLower s = (s |> get).ToLower()
        let replace (s1: string) (s2: string) s = (s |> get).Replace(s1, s2)

    module Math =
        let roundBy s n =
            (n / s) |> round |> double |> (fun f -> f * s)

        let roundBy0_5 = roundBy 0.5

        /// Calculates the number of decimal digits that
        /// should be shown according to a precision
        /// number n that specifies the number of non
        /// zero digits in the decimals.
        /// * 66.666 |> getPrecision 1 = 0
        /// * 6.6666 |> getPrecision 1 = 0
        /// * 0.6666 |> getPrecision 1 = 1
        /// * 0.0666 |> getPrecision 1 = 2
        /// * 0.0666 |> getPrecision 0 = 0
        /// * 0.0666 |> getPrecision 1 = 2
        /// * 0.0666 |> getPrecision 2 = 3
        /// * 0.0666 |> getPrecision 3 = 4
        /// * 6.6666 |> getPrecision 0 = 0
        /// * 6.6666 |> getPrecision 1 = 0
        /// * 6.6666 |> getPrecision 2 = 1
        /// * 6.6666 |> getPrecision 3 = 2
        /// etc
        /// If n < 0 then n = 0 is used.
        let getPrecision n f = // ToDo fix infinity case
            let n = if n < 0 then 0 else n

            if f = 0. || n = 0 then
                n
            else
                let s = (f |> abs |> string).Split([| '.' |])

                // calculate number of remaining decimal digits (after '.')
                let p = n - (if s.[0] = "0" then 0 else s.[0].Length)

                let p = if p < 0 then 0 else p

                if (int s.[0]) > 0 then
                    p
                else
                    // calculate the the first occurance of a non-zero decimal digit
                    let c = (s.[1] |> String.countFirstChar '0')
                    c + p

        /// Fix the precision of a float f to
        /// match a minimum of non zero digits n
        /// * 66.666 |> fixPrecision 1 = 67
        /// * 6.6666 |> fixPrecision 1 = 7
        /// * 0.6666 |> fixPrecision 1 = 0.7
        /// * 0.0666 |> fixPrecision 1 = 0.07
        /// * 0.0666 |> fixPrecision 0 = 0
        /// * 0.0666 |> fixPrecision 1 = 0.07
        /// * 0.0666 |> fixPrecision 2 = 0.067
        /// * 0.0666 |> fixPrecision 3 = 0.0666
        /// * 6.6666 |> fixPrecision 0 = 7
        /// * 6.6666 |> fixPrecision 1 = 7
        /// * 6.6666 |> fixPrecision 2 = 6.7
        /// * 6.6666 |> fixPrecision 3 = 6.67
        /// etc
        /// If n < 0 then n = 0 is used.
        let fixPrecision n (f: float) = Math.Round(f, f |> getPrecision n)

    module List =
        let create x = x :: []

        let findNearestMax n ns =
            match ns with
            | [] -> n
            | _ ->
                ns
                |> List.sort
                |> List.rev
                |> List.fold (fun x a -> if (a - x) < (n - x) then x else a) n

        let removeDuplicates xs =
            xs
            |> List.fold
                (fun xs x ->
                    if xs |> List.exists ((=) x) then
                        xs
                    else
                        [ x ] |> List.append xs
                )
                []

    module DateTime =
        let apply f (dt: DateTime) = f dt
        let get = apply id

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