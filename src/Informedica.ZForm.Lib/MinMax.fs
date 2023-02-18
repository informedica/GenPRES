namespace Informedica.ZForm.Lib


/// Functions to handle a `MinMax` type.
/// The concept is that of a range definition with
/// either a min value, a max value, none or both.
/// The min and/or max value can be inclusive or exclusive
/// to model the difference of something being >= or >.
/// This in turns enables ranges to be be complementary.
module MinMax =

    open MathNet.Numerics

    open Aether

    module ValueUnit = Informedica.GenUnits.Lib.ValueUnit

    type ValueUnit = ValueUnit.ValueUnit


    let (<?) = ValueUnit.st
    let (>?) = ValueUnit.gt
    let (<=?) = ValueUnit.ste
    let (>=?) = ValueUnit.gte
    let (>>=) l r = ValueUnit.convertTo r l

    /// Range with min and/or max
    type MinMax =
        {
            Min : Value option
            Max : Value option
        }
    /// Can be either `Inclusive` or `Exclusive`
    and Value = Inclusive of ValueUnit | Exclusive of ValueUnit


    let inclusive v = v |> Inclusive


    let exclusive v = v |> Exclusive


    let create min max = { Min = min; Max = max }


    let empty = create None None

    /// A `MinMax` range with value 1, can
    /// be used in calculations as a "unit"
    /// with multiplication and division
    let one u =
        {
            Min = 1N |> ValueUnit.create u |> inclusive |> Some
            Max = 1N |> ValueUnit.create u |> inclusive |> Some
        }


    let inline applyValue1 f1 f2 v1 =
            match v1 with
            | Inclusive vu1 -> vu1 |> f1 |> Inclusive
            | Exclusive vu1 -> vu1 |> f2 |> Exclusive


    let inline applyValue2 f1 f2 f3 f4 v1 v2 =
            match v1, v2 with
            | Inclusive vu1, Inclusive vu2 -> vu1 |> f1 <| vu2
            | Inclusive vu1, Exclusive vu2 -> vu1 |> f2 <| vu2
            | Exclusive vu1, Inclusive vu2 -> vu1 |> f3 <| vu2
            | Exclusive vu1, Exclusive vu2 -> vu1 |> f4 <| vu2


    /// Check whether v1 > v2 using
    /// inclusive and exclusive logic
    let valueLT = applyValue2 (>?) (>=?) (>?) (>?)


    /// Check whether v1 < v2 using
    /// inclusive and exclusive logic
    let valueST = applyValue2 (<?) (<?) (<=?) (<?)


    /// Check whether v1 >= v2 using
    /// inclusive and exclusive logic
    let valueLTE = applyValue2 (>=?) (>=?) (>=?) (>=?)


    /// Check whether v1 <= v2 using
    /// inclusive and exclusive logic
    let valueSTE = applyValue2 (<=?) (<=?) (<=?) (<=?)

    /// Calculate the comparison of 2
    /// optional `Value` types `v1` and `v2`.
    let compOpt comp nn sn ns v1 v2 =
        match v1, v2 with
        | None, None -> nn
        | Some _, None -> sn
        | None, Some _ -> ns
        | Some v1, Some v2 -> comp v1 v2


    let valueOptLT = compOpt valueLT false true false


    let valueOptST = compOpt valueST false false true


    let valueOptLTE = compOpt valueLTE false true false


    let valueOptSTE = compOpt valueSTE false false true

    /// Check whether a `MinMax` is valid in the
    /// sense that the lower limit never can exceed
    /// the upper limit.
    let isValid { Min = min; Max = max } =
        match min, max with
        | None, None -> true
        | Some _, None | None, Some _ -> true
        | Some v1, Some v2 ->
            match v1, v2 with
            | Inclusive vu1, Inclusive vu2
            | Exclusive vu1, Exclusive vu2
            | Inclusive vu1, Exclusive vu2
            | Exclusive vu1, Inclusive vu2 ->
                vu1 |> ValueUnit.eqsGroup vu2 &&
                applyValue2 (<=?) (<?) (<?) (<?) v1 v2


    let setMin min mm =
        let mm_ = { mm with Min = min }
        if mm_ |> isValid then mm_ else mm


    let setMax max mm =
        let mm_ = { mm with Max = max }
        if mm_ |> isValid then mm_ else mm

    /// Set the min value to `min` only
    /// when the condition `cond` appies
    /// to the `MinMax` `mm`.
    let setMinCond cond min (mm : MinMax) =
        match mm.Min, mm.Max with
        | Some m, Some max ->
            if cond min m |> not then mm
            else
                mm
                |> setMin (Some min)
                |> setMax (Some max)
        | None, Some max ->
            mm
            |> setMin (Some min)
            |> setMax (Some max)
        | None, None    ->
            mm
            |> setMin (Some min)
        | Some m, None   ->
            if cond min m |> not then mm
            else
                mm
                |> setMin (Some min)


    /// Set the max value to `max` only
    /// when the condition `cond` appies
    /// to the `MinMax` `mm`.
    let setMaxCond cond max (mm : MinMax) =
        match mm.Min, mm.Max with
        | Some min, Some m ->
            if cond max m |> not then mm
            else
                mm
                |> setMin (Some min)
                |> setMax (Some max)
        | Some min, None ->
            mm
            |> setMin (Some min)
            |> setMax (Some max)
        | None, None  ->
            mm
            |> setMax (Some max)
        | None, Some m ->
            if cond max m |> not then mm
            else
                mm
                |> setMax (Some max)

    /// Calculate the resulting `MinMax` value
    /// based on a list of `MinMax` values accoring
    /// to a conditioning rule `cond`.
    let foldCond cond (mms : MinMax list) =
        let condMax m1 m2 = cond m2 m1
        mms |> List.fold (fun acc mm ->
            match mm.Min, mm.Max with
            | None, None         -> acc
            | Some min, None     -> setMinCond cond min acc
            | None, Some max     -> setMaxCond condMax max acc
            | Some min, Some max ->
                acc
                |> setMinCond cond min
                |> setMaxCond condMax max
        ) empty


    /// Calculate the smallest range from
    /// a list of `MinMax` values.
    let foldMinimize = foldCond valueLT


    /// Calculate the largest range from
    /// a list of `MinMax` values.
    let foldMaximize = foldCond valueST

    /// Check whether a value `v` is in
    /// the range of a `MinMax` `mm`.
    let inRange v (mm : MinMax) =
        match mm.Min, mm.Max with
        | None, None -> true
        | Some v_, None -> valueLTE v v_
        | None, Some v_ -> valueSTE v v_
        | Some v1, Some v2 ->
            (valueLTE v v1) && (valueSTE v v2)


    /// perform a calculation `op` to
    /// 2 values `v1` and `v2`.
    let calcValue op v1 v2 =
        match v1, v2 with
        | Inclusive v1, Inclusive v2 -> v1 |> op <| v2 |> Inclusive
        | Exclusive v1, Exclusive v2 -> v1 |> op <| v2 |> Exclusive
        | Inclusive v1, Exclusive v2 -> v1 |> op <| v2 |> Exclusive
        | Exclusive v1, Inclusive v2 -> v1 |> op <| v2 |> Exclusive


    /// Perform a calculation for `Value` types
    /// of the `MinMax` values `mm1` and `mm2`.
    let calc op (mm1 : MinMax) (mm2 : MinMax) =
        let c m1 m2 =
            match m1, m2 with
            | None, None
            | Some _, None | None, Some _ -> None
            | Some v1, Some v2 -> v1 |> op <| v2 |> Some
        {
            empty with
                Min = c mm1.Min mm2.Min
                Max = c mm1.Max mm2.Max
        }


    /// Convert the units of the `ValueUnit` values
    /// in a `MinMax` `mm` to unit `u`.
    let convertTo u (mm : MinMax) =
        let convert =
            applyValue1 (ValueUnit.convertTo u) (ValueUnit.convertTo u)
            >> Some
        {
            Min =
                match mm.Min with
                | None -> None
                | Some v -> v |> convert
            Max =
                match mm.Max with
                | None -> None
                | Some v -> v |> convert
        }


    /// Set the units of the `ValueUnit` values
    /// in a `MinMax` `mm` to unit `u`.
    let withUnit u (mm : MinMax) =
        let convert =
            applyValue1
                (fun vu -> vu |> ValueUnit.getValue |> ValueUnit.create u)
                (fun vu -> vu |> ValueUnit.getValue |> ValueUnit.create u)
            >> Some
        {
            Min =
                match mm.Min with
                | None -> None
                | Some v -> v |> convert
            Max =
                match mm.Max with
                | None -> None
                | Some v -> v |> convert
        }


    /// Extension methods for the `Value` type
    /// to enable lenses.
    type Value with

        static member Inclusive_ =
            (fun v ->
                match v with
                | Inclusive v_ -> v_ |> Some
                | Exclusive _  -> None
            ),
            (fun x v ->
                match v with
                | Inclusive _ -> x |> Inclusive
                | Exclusive _ -> v
            )


        static member Exclusive_ =
            (fun v ->
                match v with
                | Inclusive _  -> None
                | Exclusive v_ -> v_ |> Some
            ),
            (fun x v ->
                match v with
                | Inclusive _ -> v
                | Exclusive _ -> x |> Exclusive
            )

        static member (*) (v1, v2) = calcValue (*) v1 v2

        static member (/) (v1, v2) = calcValue (/) v1 v2


    /// Extension methods for the `MinMax` type
    /// to enable lenses.
    type MinMax with

        static member Min_ :
            (MinMax -> Value Option) * (Value -> MinMax -> MinMax) =
            (fun mm -> mm.Min),
            (fun v mm -> mm |> setMin (Some v))

        static member Max_ :
            (MinMax -> Value Option) * (Value -> MinMax -> MinMax) =
            (fun mm -> mm.Max),
            (fun v mm -> mm |> setMax (Some v))


        static member (*) (mm1, mm2) = calc (*) mm1 mm2

        static member (/) (mm1, mm2) = calc (/) mm1 mm2

    /// Contains the lenses for the `Value` and
    /// the `MinMax` type.
    module Optics =


        let getMin = Optic.get MinMax.Min_


        let setMin = Optic.set MinMax.Min_


        let inclMinLens =
            (fun mm ->
                match mm |> getMin with
                | Some min ->
                    match min with
                    | Inclusive v -> Some v
                    | _ -> None
                | None -> None),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMin (vu_ |> inclusive)
                | None -> mm
            )


        let exclMinLens =
            (fun mm ->
                match mm |> getMin with
                | Some min ->
                    match min with
                    | Exclusive v -> Some v
                    | _ -> None
                | None -> None),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMin (vu_ |> exclusive)
                | None -> mm
            )


        let getMax = Optic.get MinMax.Max_


        let setMax = Optic.set MinMax.Max_


        let inclMaxLens =
            (fun mm ->
                match mm |> getMax with
                | Some max ->
                    match max with
                    | Inclusive v -> Some v
                    | _ -> None
                | None -> None),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMax (vu_ |> inclusive)
                | None -> mm
            )


        let exclMaxLens =
            (fun mm ->
                match mm |> getMax with
                | Some max ->
                    match max with
                    | Exclusive v -> Some v
                    | _ -> None
                | None -> None),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMax (vu_ |> exclusive)
                | None -> mm
            )


    /// The dto object to represent a `MinMax` type
    module Dto =

        type Dto () =
            member val Min = ValueUnit.Dto.dto () with get, set
            member val HasMin = false with get, set
            member val MinIncl = true with get, set
            member val Max = ValueUnit.Dto.dto () with get, set
            member val HasMax = false with get, set
            member val MaxIncl = true with get, set

        let dto () = Dto ()

        let fromDto (dto : Dto) =

            match dto.HasMin, dto.HasMax with
            | false, false -> empty |> Some
            | true, false ->
                match dto.Min |> ValueUnit.Dto.fromDto with
                | None -> None
                | Some vu ->
                    let min =
                        match dto.MinIncl with
                        | true  -> inclusive vu
                        | false -> exclusive vu
                        |> Some
                    create min None |> Some
            | false, true ->
                match dto.Max |> ValueUnit.Dto.fromDto with
                | None -> None
                | Some vu ->
                    let max =
                        match dto.MaxIncl with
                        | true  -> inclusive vu
                        | false -> exclusive vu
                        |> Some
                    create None max |> Some
            | true, true ->
                match dto.Min |> ValueUnit.Dto.fromDto,
                      dto.Max |> ValueUnit.Dto.fromDto with
                | None,   None
                | Some _, None
                | None,   Some _ -> None
                | Some vu1, Some vu2 ->

                    let min, max =
                        match dto.MinIncl, dto.MaxIncl with
                        | false, false ->
                            exclusive vu1, exclusive vu2
                        | true, true ->
                            inclusive vu1, inclusive vu2
                        | true, false ->
                            inclusive vu1, exclusive vu2
                        | false, true ->
                            exclusive vu1, inclusive vu2

                    create (Some min) (Some max)
                    |> (fun mm -> if mm |> isValid then mm |> Some else None)


        let toDto (minmax : MinMax) =
            let dto = dto ()

            match minmax.Min, minmax.Max with
            | None, None -> dto
            | Some min, Some max ->
                let v1, v2 =
                    match min, max with
                    | Inclusive v1, Inclusive v2 ->
                        dto.MinIncl <- true
                        dto.MaxIncl <- true
                        v1, v2
                    | Exclusive v1, Exclusive v2 ->
                        dto.MinIncl <- false
                        dto.MaxIncl <- false
                        v1, v2
                    | Inclusive v1, Exclusive v2 ->
                        dto.MinIncl <- true
                        dto.MaxIncl <- false
                        v1, v2
                    | Exclusive v1, Inclusive v2 ->
                        dto.MinIncl <- false
                        dto.MaxIncl <- true
                        v1, v2
                dto.Min <- v1 |> ValueUnit.Dto.toDtoDutchShort
                dto.HasMin <- true
                dto.Max <- v2 |> ValueUnit.Dto.toDtoDutchShort
                dto.HasMax <- true
                dto
            | Some m, None ->
                let v1 =
                    match m with
                    | Inclusive v1 ->
                        dto.MinIncl <- true
                        v1
                    | Exclusive v1 ->
                        dto.MinIncl <- false
                        v1
                dto.Min <- v1 |> ValueUnit.Dto.toDtoDutchShort
                dto.HasMin <- true
                dto
            | None, Some m ->
                let v2 =
                    match m with
                    | Inclusive v2 ->
                        dto.MaxIncl <- true
                        v2
                    | Exclusive v2 ->
                        dto.MaxIncl <- false
                        v2
                dto.Max <- v2 |> ValueUnit.Dto.toDtoDutchShort
                dto.HasMax <- true
                dto



    let valueToString = function
        | Inclusive vu -> sprintf "incl %s" (vu |> ValueUnit.toStringPrec 2)
        | Exclusive vu -> sprintf "excl %s" (vu |> ValueUnit.toStringPrec 2)


    /// Turn a `MinMax` to a string with
    /// `mins` and `maxs` as annotations
    /// for resp. the min and max value.
    let toString mins maxs { Min = min; Max = max } =
        let vuToStr vu =
            let milliGram = ValueUnit.Units.Mass.milliGram
            let gram = ValueUnit.Units.Mass.gram
            let day = ValueUnit.Units.Time.day

            let per = ValueUnit.per
            let convertTo = ValueUnit.convertTo

            let milliGramPerDay = milliGram |> per day
            let gramPerDay = gram |> per day

            vu
            |> (fun vu ->
                match vu |> ValueUnit.get with
                | v, u when v >= 1000N && u = milliGram -> vu |> convertTo gram
                | v, u when v >= 1000N && u = milliGramPerDay -> vu |> convertTo gramPerDay
                | _ -> vu
            )
            |> ValueUnit.toStringPrec 2

        let minToString min =
            match min with
            | Inclusive vu ->
                vu |> vuToStr |> sprintf "%s"
            | Exclusive vu ->
                vu |> vuToStr |> sprintf "%s"

        let maxToString min =
            match min with
            | Inclusive vu ->
                vu |> vuToStr |> sprintf "%s"
            | Exclusive vu ->
                vu |> vuToStr |> sprintf "%s"

        match min, max with
        | None, None -> ""
        | Some min_, Some max_ ->
            $"%s{min_ |> minToString} - %s{max_ |> maxToString}"
        | Some min_, None ->
            (min_ |> minToString)
            |> sprintf "%s %s" mins
        | None, Some max_ ->
            (max_ |> maxToString)
            |> sprintf "%s %s" maxs


    let ageToString { Min = min; Max = max } =
        let oneWk = 1N |> ValueUnit.create ValueUnit.Units.Time.week
        let oneMo = 1N |> ValueUnit.create ValueUnit.Units.Time.month
        let oneYr = 1N |> ValueUnit.create ValueUnit.Units.Time.year

        let convert =
            let c vu =
                match vu with
                | _ when vu <? oneWk -> vu >>= ValueUnit.Units.Time.day
                | _ when vu <? oneMo -> vu >>= ValueUnit.Units.Time.week
                | _ when vu <? oneYr -> vu >>= ValueUnit.Units.Time.month
                | _ -> vu >>= ValueUnit.Units.Time.year
            Option.bind (applyValue1 c c >> Some)

        { Min = min |> convert; Max = max |> convert } |> toString "van" "tot"



    let gestAgeToString { Min = min; Max = max } =

        let convert =
            let c vu = vu >>= ValueUnit.Units.Time.week
            Option.bind (applyValue1 c c >> Some)

        { Min = min |> convert; Max = max |> convert } |> toString "van" "tot"


    module Tests =

        let tests () =
            let (|>!) x f =
                x |> printfn  "%A"
                f x

            // Create dto and there and back again
            let dto = Dto.dto ()
            dto
            |> Dto.fromDto
            |>! ignore

            // Add min and max to dto and there and back again
            dto.Min.Value <- 1m
            dto.Min.Unit <- "mg"
            dto.Min.Group <- "mass"
            dto.HasMin <- true
            dto.MinIncl <- false
            dto.Max.Value <- 2m
            dto.Max.Unit <- "g"
            dto.Max.Group <- "mass"
            dto.HasMax <- true
            dto
            |>! Dto.fromDto
            |>! Option.bind (Dto.toDto >> Some)
            |>! Option.bind (Dto.fromDto >> Some)
            |>! ignore

            // Add min > and max to dto and there and back again
            dto.Min.Value <- 1m
            dto.Min.Unit <- "g"
            dto.Min.Group <- "mass"
            dto.HasMin <- true
            dto.MinIncl <- false
            dto.Max.Value <- 2m
            dto.Max.Unit <- "mg"
            dto.Max.Group <- "mass"
            dto.HasMax <- true
            dto
            |>! Dto.fromDto
            |>! Option.bind (Dto.toDto >> Some)
            |>! Option.bind (Dto.fromDto >> Some)
            |>! ignore