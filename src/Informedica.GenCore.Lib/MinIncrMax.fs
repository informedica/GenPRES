namespace Informedica.GenCore.Lib.Ranges


open Informedica.GenUnits.Lib



/// Range with min, incr and/or max
type MinIncrMax =
    {
        Min: Limit option
        Incr: LimitIncr option
        Max: Limit option
    }

/// Can be either `Inclusive` or `Exclusive`
and Limit =
    | Inclusive of ValueUnit
    | Exclusive of ValueUnit

and LimitIncr = LimitIncr of ValueUnit


module Errors =


    type Msg<'TLimit, 'TIncr> =
        | MinLargerThanMax of min: 'TLimit * max: 'TLimit
        | NoValidLimitIncr of incr: 'TIncr
        | DifferentUnitGroup of MinIncrMax

    let toString minToStr incrToStr maxToStr =
        function
        | MinLargerThanMax (min, max) -> $"{min |> minToStr} > {max |> maxToStr}"
        | NoValidLimitIncr incr -> incr |> incrToStr
        | DifferentUnitGroup mim ->
            let minS, incrS, maxS =
                mim.Min |> minToStr, mim.Incr |> incrToStr, mim.Max |> maxToStr

            $"different unit groups in {minS}, {incrS} {maxS}"



module LimitIncr =

    let getIncr (LimitIncr incr) = incr



module Limit =

    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    let toString isMin lim =
        let s = if isMin then "Min" else "Max"

        match lim with
        | Inclusive vu -> $"{s} Incl {vu |> ValueUnit.toReadableDutchStringWithPrec 3}"
        | Exclusive vu -> $"{s} Excl {vu |> ValueUnit.toReadableDutchStringWithPrec 3}"


    let inclusive v = v |> Inclusive


    let exclusive v = v |> Exclusive


    let getIncr (LimitIncr incr) = incr


    let isInclusive =
        function
        | Inclusive _ -> true
        | Exclusive _ -> false


    let zero =
        1N
        |> Times
        |> Count
        |> ValueUnit.zero
        |> Inclusive


    let one =
        1N |> Times |> Count |> ValueUnit.one |> Inclusive


    let inline map fIncl fExcl lim =
        match lim with
        | Inclusive vu -> vu |> fIncl
        | Exclusive vu -> vu |> fExcl


    let getValueUnit = map id id


    let inline apply fIncl fExcl =
        map (fIncl >> Inclusive) (fExcl >> Exclusive)



    let inline map2 fInclIncl fInclExcl fExclIncl fExclExcl lim1 lim2 =
        match lim1, lim2 with
        | Inclusive vu1, Inclusive vu2 -> vu1 |> fInclIncl <| vu2
        | Inclusive vu1, Exclusive vu2 -> vu1 |> fInclExcl <| vu2
        | Exclusive vu1, Inclusive vu2 -> vu1 |> fExclIncl <| vu2
        | Exclusive vu1, Exclusive vu2 -> vu1 |> fExclExcl <| vu2


    /// Check whether v1 > v2 using
    /// inclusive and exclusive logic
    let gt isMin1 isMin2 =
        match isMin1, isMin2 with
        // min > min
        | true, true ->
            let fInclIncl = (>?) // [1 > [1 = false
            let fInclExcl = (>?) // [1 > <1 = false
            let fExclIncl = (>=?) // <1 > [1 = true
            let fExclExcl = (>?) // <1 > <1 = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl
        // max > min
        | false, true ->
            let fInclIncl = (>?) // 1] > [1 = false
            let fInclExcl = (>?) // 1] > <1 = false
            let fExclIncl = (>?) // 1> > [1 = false
            let fExclExcl = (>?) // 1> > <1 = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // min > max
        | true, false ->
            let fInclIncl = (>?) // [1 > 1] = false
            let fInclExcl = (>=?) // [1 > 1> = true
            let fExclIncl = (>=?) // <1 > 1] = true
            let fExclExcl = (>=?) // <1 > 1> = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // max > max
        | false, false ->
            let fInclIncl = (>?) // 1] > 1] = false
            let fInclExcl = (>=?) // 1] > 1> = true
            let fExclIncl = (>?) // 1> > 1] = false
            let fExclExcl = (>?) // 1> > 1> = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        |> fun (fInclIncl, fInclExcl, fExclIncl, fExclExcl) -> map2 fInclIncl fInclExcl fExclIncl fExclExcl


    /// Check whether v1 < v2 using
    /// inclusive and exclusive logic
    let st isMin1 isMin2 =
        match isMin1, isMin2 with
        // min < min
        | true, true ->
            let fInclIncl = (<?) // [1 < [1 = false
            let fInclExcl = (<=?) // [1 < <1 = true
            let fExclIncl = (<?) // <1 < [1 = false
            let fExclExcl = (<?) // <1 < <1 = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl
        // max < min
        | false, true ->
            let fInclIncl = (<?) // 1] < [1 = false
            let fInclExcl = (<=?) // 1] < <1 = true
            let fExclIncl = (<=?) // 1> < [1 = true
            let fExclExcl = (<=?) // 1> < <1 = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // min < max
        | true, false ->
            let fInclIncl = (<?) // [1 < 1] = false
            let fInclExcl = (<?) // [1 < 1> = false
            let fExclIncl = (<?) // <1 < 1] = false
            let fExclExcl = (<?) // <1 < 1> = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // max < max
        | false, false ->
            let fInclIncl = (<?) // 1] < 1] = false
            let fInclExcl = (<?) // 1] < 1> = false
            let fExclIncl = (<=?) // 1> < 1] = true
            let fExclExcl = (<?) // 1> < 1> = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        |> fun (fInclIncl, fInclExcl, fExclIncl, fExclExcl) -> map2 fInclIncl fInclExcl fExclIncl fExclExcl


    /// Check whether v1 >= v2 using
    /// inclusive and exclusive logic
    let gte isMin1 isMin2 =
        match isMin1, isMin2 with
        // min >= min
        | true, true ->
            let fInclIncl = (>=?) // [1 >= [1 = true
            let fInclExcl = (>?) // [1 >= <1 = false
            let fExclIncl = (>=?) // <1 >= [1 = true
            let fExclExcl = (>=?) // <1 >= <1 = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl
        // max >= min
        | false, true ->
            let fInclIncl = (>=?) // 1] >= [1 = true
            let fInclExcl = (>?) // 1] >= <1 = false
            let fExclIncl = (>?) // 1> >= [1 = false
            let fExclExcl = (>?) // 1> >= <1 = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // min >= max
        | true, false ->
            let fInclIncl = (>=?) // [1 >= 1] = true
            let fInclExcl = (>=?) // [1 >= 1> = true
            let fExclIncl = (>=?) // <1 >= 1] = true
            let fExclExcl = (>=?) // <1 >= 1> = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // max >= max
        | false, false ->
            let fInclIncl = (>=?) // 1] >= 1] = true
            let fInclExcl = (>=?) // 1] >= 1> = true
            let fExclIncl = (>?) // 1> >= 1] = false
            let fExclExcl = (>=?) // 1> >= 1> = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        |> fun (fInclIncl, fInclExcl, fExclIncl, fExclExcl) -> map2 fInclIncl fInclExcl fExclIncl fExclExcl


    /// Check whether v1 <= v2 using
    /// inclusive and exclusive logic
    let ste isMin1 isMin2 =
        match isMin1, isMin2 with
        // min <= min
        | true, true ->
            let fInclIncl = (<=?) // [1 <= [1 = true
            let fInclExcl = (<=?) // [1 <= <1 = true
            let fExclIncl = (<?) // <1 <= [1 = false
            let fExclExcl = (<=?) // <1 <= <1 = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl
        // max <= min
        | false, true ->
            let fInclIncl = (<=?) // 1] <= [1 = true
            let fInclExcl = (<?) // 1] <= <1 = true
            let fExclIncl = (<?) // 1> <= [1 = false
            let fExclExcl = (<?) // 1> <= <1 = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // min <= max
        | true, false ->
            let fInclIncl = (<=?) // [1 <= 1] = true
            let fInclExcl = (<?) // [1 <= 1> = false
            let fExclIncl = (<?) // <1 <= 1] = false
            let fExclExcl = (<?) // <1 <= 1> = false

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        // max <= max
        | false, false ->
            let fInclIncl = (<=?) // 1] <= 1] = true
            let fInclExcl = (<?) // 1] <= 1> = false
            let fExclIncl = (<=?) // 1> <= 1] = true
            let fExclExcl = (<=?) // 1> <= 1> = true

            fInclIncl, fInclExcl, fExclIncl, fExclExcl

        |> fun (fInclIncl, fInclExcl, fExclIncl, fExclExcl) -> map2 fInclIncl fInclExcl fExclIncl fExclExcl


    let eq =
        let returnFalse _ _ = false
        map2 (=?) returnFalse returnFalse returnFalse


    /// Calculate the comparison of 2
    /// optional `Value` types `v1` and `v2`.
    let compOpt comp nn sn ns v1 v2 =
        match v1, v2 with
        | None, None -> nn
        | Some _, None -> sn
        | None, Some _ -> ns
        | Some v1, Some v2 -> comp v1 v2


    let optLT isMin1 isMin2 =
        compOpt (gt isMin1 isMin2) false true false


    let optST isMin1 isMin2 =
        compOpt (st isMin1 isMin2) false false true


    let optLTE isMin1 isMin2 =
        compOpt (gte isMin1 isMin2) false true false


    let optSTE isMin1 isMin2 =
        compOpt (ste isMin1 isMin2) false false true


    let minGTmax min max = min |> gt false true max


    let cmpToStr cp =
        let z = zero
        let o = one

        match cp with
        | _ when
            (z |> cp <| z)
            && not (z |> cp <| o)
            && not (o |> cp <| z)
            ->
            "="
        | _ when
            (z |> cp <| z)
            && (z |> cp <| o)
            && not (o |> cp <| z)
            ->
            "<="
        | _ when
            (z |> cp <| z)
            && not (z |> cp <| o)
            && (o |> cp <| z)
            ->
            ">="
        | _ when
            not (z |> cp <| z)
            && (z |> cp <| o)
            && not (o |> cp <| z)
            ->
            "<"
        | _ when
            not (z |> cp <| z)
            && not (z |> cp <| o)
            && (o |> cp <| z)
            ->
            ">"
        | _ -> "unknown comparison"


    let toMultipleOf inclMultOf exclMultOf (LimitIncr incr) min =
        let brs =
            incr |> ValueUnit.toBaseValue |> Set.ofArray

        let calc toMult vu =
            vu
            |> ValueUnit.toBase
            |> ValueUnit.mapValues (toMult brs >> snd)
            |> ValueUnit.toUnit
            |> Inclusive

        match min with
        | Inclusive vu -> vu |> calc inclMultOf
        | Exclusive vu -> vu |> calc exclMultOf


    let minToMultipleOf =
        toMultipleOf BigRational.minInclMultipleOf BigRational.minExclMultipleOf


    let maxToMultipleOf =
        toMultipleOf BigRational.maxInclMultipleOf BigRational.maxExclMultipleOf


    let calcLimitIncr (LimitIncr vu) =
        vu
        |> ValueUnit.filterValues (fun br -> br >= 0N)
        |> ValueUnit.applyToValue (
            Set.ofArray
            >> Set.removeBigRationalMultiples
            >> Set.toArray
        )
        |> LimitIncr


    let validate (LimitIncr vu) =
        if vu |> ValueUnit.getValue |> Array.isEmpty then
            vu
            |> LimitIncr
            |> Errors.NoValidLimitIncr
            |> Error
        else
            vu |> LimitIncr |> Ok





/// Functions to handle a `MinMax` type.
/// The concept is that of a range definition with
/// either a min value, a max value, none or both.
/// The min and/or max value can be inclusive or exclusive
/// to model the difference of something being >= or >.
/// This in turns enables ranges to be be complementary.
module MinIncrMax =

    open MathNet.Numerics
    open Aether

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    module Calculator =



        let minGTmax gt gte isMinIncl isMaxIncl max min =
            if max |> isMaxIncl && min |> isMinIncl then
                min |> gt <| max
            else
                min |> gte <| max


        let validate calcIncr minMultOf maxMultOf minGTmax min incr max =
            match min, incr, max with
            | None, None, None
            | Some _, None, None
            | None, None, Some _
            | Some _, None, Some _ -> (min, incr, max) |> Ok
            | None, Some incr, None ->
                incr
                |> calcIncr
                |> Result.map (fun incr -> (min, incr |> Some, max))
            | Some min, Some incr, None ->
                incr
                |> calcIncr
                |> Result.map (fun incr -> (min |> minMultOf incr |> Some, incr |> Some, max))
            | None, Some incr, Some max ->
                incr
                |> calcIncr
                |> Result.map (fun incr -> (min, incr |> Some, max |> maxMultOf incr |> Some))
            | Some min, Some incr, Some max ->
                incr
                |> calcIncr
                |> Result.map (fun incr -> (min |> minMultOf incr |> Some, incr |> Some, max |> maxMultOf incr |> Some))
            |> function
                | Ok (Some min, incr, Some max) ->
                    if min |> minGTmax max then
                        (min, max) |> Errors.MinLargerThanMax |> Error
                    else
                        (Some min, incr, Some max) |> Ok
                | result -> result


        let toString_ st ste gt gte dotsL dotsR dotsM brToStr min incr max =
            let toStr xs =
                xs |> Seq.map brToStr |> String.concat ","

            match min, incr, max with
            | None, None, None -> ""
            | Some (minIncl, min), None, None ->
                let gte = if minIncl then gte else gt
                $"{gte}%s{min |> brToStr}"
            | None, Some incr, None -> $"{dotsL}{incr |> toStr}{dotsR}"
            | None, None, Some (maxIncl, max) ->
                let ste = if maxIncl then ste else st
                $"{ste}%s{max |> brToStr}"
            | Some (minIncl, min), Some incr, None ->
                let gte = if minIncl then gte else gt
                $"{gte}%s{min |> brToStr}{dotsL}{incr |> toStr}"
            | None, Some incr, Some (maxIncl, max) ->
                let ste = if maxIncl then ste else st
                $"{incr |> toStr}{dotsR}{ste}%s{max |> brToStr}"
            | Some (_, min), None, Some (_, max) -> $"%s{min |> brToStr}{dotsM}%s{max |> brToStr}"
            | Some (minIncl, min), Some incr, Some (maxIncl, max) ->
                let gte = if minIncl then gte else gt
                let ste = if maxIncl then ste else st
                $"{gte}%s{min |> brToStr}{dotsL}{incr |> toStr}{dotsR}{ste}%s{max |> brToStr}"


        let toString brToStr min incr max =
            let gt, gte = ">", "\u2265"
            let st, ste = "<", "\u2264"
            let dotsL, dotsR, dotsM = "..", "..", ".."

            toString_ st ste gt gte dotsL dotsR dotsM brToStr min incr max


        let toStringNL brToStr min incr max =
            let gt, gte = "vanaf ", "vanaf "
            let st, ste = " tot ", " tot en met "
            let dotsL, dotsR, dotsM = " per ", "", " - "

            toString_ st ste gt gte dotsL dotsR dotsM brToStr min incr max


    // ToDo use value unit operators
    let (<?) = ValueUnit.st
    let (>?) = ValueUnit.gt
    let (<=?) = ValueUnit.ste
    let (>=?) = ValueUnit.gte
    let (>>=) l r = ValueUnit.convertTo r l


    let create min incr max = { Min = min; Incr = incr; Max = max }


    let validate { Min = min; Incr = incr; Max = max } =
        let getGroup =
            Limit.getValueUnit >> ValueUnit.getGroup

        let validate_ =
            Calculator.validate
                (Limit.calcLimitIncr >> Limit.validate)
                Limit.minToMultipleOf
                Limit.maxToMultipleOf
                Limit.minGTmax

        [
            min |> Option.map getGroup
            max |> Option.map getGroup
            incr
            |> Option.map (Limit.getIncr >> ValueUnit.getGroup)
        ]
        |> List.choose id
        |> List.distinct
        |> List.length
        |> fun n ->
            if n > 1 then
                { Min = min; Incr = incr; Max = max }
                |> Errors.DifferentUnitGroup
                |> Error
            else
                validate_ min incr max
                |> Result.map (fun (min, incr, max) -> create min incr max)


    let empty = create None None None


    /// A `MinMax` range with value 1, can
    /// be used in calculations as a "unit"
    /// with multiplication and division
    let one u =
        {
            Min =
                1N
                |> ValueUnit.createSingle u
                |> Limit.inclusive
                |> Some
            Incr =
                1N
                |> ValueUnit.createSingle u
                |> LimitIncr
                |> Some
            Max =
                1N
                |> ValueUnit.createSingle u
                |> Limit.inclusive
                |> Some
        }


    let setMin min mm =
        { mm with Min = min }
        |> validate
        |> function
            | Ok mm_ -> mm_
            | Error _ -> mm


    let setMax max mm =
        { mm with Max = max }
        |> validate
        |> function
            | Ok mm_ -> mm_
            | Error _ -> mm


    /// Set the min value to `min` only
    /// when the condition `cond` appies
    /// to the `MinMax` `mm`.
    let setMinCond cond min (mm: MinIncrMax) =
        match mm.Min, mm.Max with
        | Some m, Some max ->
            if cond min m |> not then
                mm
            else
                mm |> setMin (Some min) |> setMax (Some max)
        | None, Some max -> mm |> setMin (Some min) |> setMax (Some max)
        | None, None -> mm |> setMin (Some min)
        | Some m, None ->
            if cond min m |> not then
                mm
            else
                mm |> setMin (Some min)



    /// Set the max value to `max` only
    /// when the condition `cond` applies
    /// to the `MinMax` `mm`.
    let setMaxCond cond max (mm: MinIncrMax) =
        match mm.Min, mm.Max with
        | Some min, Some m ->
            if cond max m |> not then
                mm
            else
                mm |> setMin (Some min) |> setMax (Some max)
        | Some min, None -> mm |> setMin (Some min) |> setMax (Some max)
        | None, None -> mm |> setMax (Some max)
        | None, Some m ->
            if cond max m |> not then
                mm
            else
                mm |> setMax (Some max)


    /// Calculate the resulting `MinMax` value
    /// based on a list of `MinMax` values according
    /// to a conditioning rule `cond`.
    let foldCond cond (mms: MinIncrMax list) =
        let condMax m1 m2 = cond m2 m1

        mms
        |> List.fold
            (fun acc mm ->
                match mm.Min, mm.Max with
                | None, None -> acc
                | Some min, None -> setMinCond cond min acc
                | None, Some max -> setMaxCond condMax max acc
                | Some min, Some max ->
                    acc
                    |> setMinCond cond min
                    |> setMaxCond condMax max
            )
            empty


    /// Calculate the smallest range from
    /// a list of `MinMax` values.
    let foldMinimize =
        foldCond (Limit.gt true false)


    /// Calculate the largest range from
    /// a list of `MinMax` values.
    let foldMaximize =
        foldCond (Limit.st true false)


    /// Check whether a value `v` is in
    /// the range of a `MinMax` `mm`.
    let inRange v (mm: MinIncrMax) =
        match mm.Min, mm.Max with
        | None, None -> true
        | Some min, None ->
            match min with
            | Inclusive vu -> v >=? vu
            | Exclusive vu -> v >? vu
        | None, Some max ->
            match max with
            | Inclusive vu -> v <=? vu
            | Exclusive vu -> v <? vu
        | Some min, Some max ->
            match min, max with
            | Inclusive vuMin, Inclusive vuMax -> v >=? vuMin && v <=? vuMax
            | Exclusive vuMin, Inclusive vuMax -> v >? vuMin && v <=? vuMax
            | Inclusive vuMin, Exclusive vuMax -> v >=? vuMin && v <? vuMax
            | Exclusive vuMin, Exclusive vuMax -> v >? vuMin && v <? vuMax


    /// perform a calculation `op` to
    /// 2 values `v1` and `v2`.
    let calcLimit op v1 v2 =
        match v1, v2 with
        | Inclusive v1, Inclusive v2 -> v1 |> op <| v2 |> Inclusive
        | Exclusive v1, Exclusive v2 -> v1 |> op <| v2 |> Exclusive
        | Inclusive v1, Exclusive v2 -> v1 |> op <| v2 |> Exclusive
        | Exclusive v1, Inclusive v2 -> v1 |> op <| v2 |> Exclusive


    /// Perform a calculation for `Value` types
    /// of the `MinMax` values `mm1` and `mm2`.
    let calc op (mm1: MinIncrMax) (mm2: MinIncrMax) =
        let c m1 m2 =
            match m1, m2 with
            | None, None
            | Some _, None
            | None, Some _ -> None
            | Some v1, Some v2 -> v1 |> op <| v2 |> Some

        { empty with
            Min = c mm1.Min mm2.Min
            Max = c mm1.Max mm2.Max
        }


    /// Convert the units of the `ValueUnit` values
    /// in a `MinMax` `mm` to unit `u`.
    let convertTo u (mm: MinIncrMax) =
        let convert =
            Limit.apply (ValueUnit.convertTo u) (ValueUnit.convertTo u)
            >> Some

        {
            Min =
                match mm.Min with
                | None -> None
                | Some v -> v |> convert
            Incr =
                mm.Incr
                |> Option.map (
                    Limit.getIncr
                    >> (ValueUnit.convertTo u)
                    >> LimitIncr
                )

            Max =
                match mm.Max with
                | None -> None
                | Some v -> v |> convert
        }



    /// Set the units of the `ValueUnit` values
    /// in a `MinMax` `mm` to unit `u`.
    let withUnit u (mm: MinIncrMax) =
        let f =
            fun vu -> vu |> ValueUnit.getValue |> ValueUnit.create u

        let convert = Limit.apply f f >> Some

        {
            Min =
                match mm.Min with
                | None -> None
                | Some v -> v |> convert
            Incr =
                mm.Incr
                |> Option.map (Limit.getIncr >> f >> LimitIncr)
            Max =
                match mm.Max with
                | None -> None
                | Some v -> v |> convert
        }


    /// Extension methods for the `Value` type
    /// to enable lenses.
    type Limit with

        static member Inclusive_ =
            (fun v ->
                match v with
                | Inclusive v_ -> v_ |> Some
                | Exclusive _ -> None
            ),
            (fun x v ->
                match v with
                | Inclusive _ -> x |> Inclusive
                | Exclusive _ -> v
            )


        static member Exclusive_ =
            (fun v ->
                match v with
                | Inclusive _ -> None
                | Exclusive v_ -> v_ |> Some
            ),
            (fun x v ->
                match v with
                | Inclusive _ -> v
                | Exclusive _ -> x |> Exclusive
            )


    /// Contains the lenses for the `Value` and
    /// the `MinMax` type.
    module Optics =


        let min_ =
            (fun mm -> mm.Min), (fun v mm -> mm |> setMin (Some v))

        let max_ =
            (fun mm -> mm.Max), (fun v mm -> mm |> setMax (Some v))


        let getMin = Optic.get min_


        let setMin = Optic.set min_


        let inclMinLens =
            (fun mm ->
                match mm |> getMin with
                | Some min ->
                    match min with
                    | Inclusive v -> Some v
                    | _ -> None
                | None -> None
            ),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMin (vu_ |> Limit.inclusive)
                | None -> mm
            )


        let exclMinLens =
            (fun mm ->
                match mm |> getMin with
                | Some min ->
                    match min with
                    | Exclusive v -> Some v
                    | _ -> None
                | None -> None
            ),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMin (vu_ |> Limit.exclusive)
                | None -> mm
            )


        let getMax = Optic.get max_


        let setMax = Optic.set max_


        let inclMaxLens =
            (fun mm ->
                match mm |> getMax with
                | Some max ->
                    match max with
                    | Inclusive v -> Some v
                    | _ -> None
                | None -> None
            ),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMax (vu_ |> Limit.inclusive)
                | None -> mm
            )


        let exclMaxLens =
            (fun mm ->
                match mm |> getMax with
                | Some max ->
                    match max with
                    | Exclusive v -> Some v
                    | _ -> None
                | None -> None
            ),
            (fun vu mm ->
                match vu with
                | Some vu_ -> mm |> setMax (vu_ |> Limit.exclusive)
                | None -> mm
            )


    /// The dto object to represent a `MinMax` type
    module Dto =

        type Dto() =
            member val Min = ValueUnit.Dto.dto () with get, set
            member val HasMin = false with get, set
            member val MinIncl = true with get, set
            member val HasIncr = false with get, set
            member val Incr = ValueUnit.Dto.dto () with get, set
            member val Max = ValueUnit.Dto.dto () with get, set
            member val HasMax = false with get, set
            member val MaxIncl = true with get, set

        let dto () = Dto()

        let fromDto (dto: Dto) =

            match dto.HasMin, dto.HasMax with
            | false, false -> empty |> Some
            | true, false ->
                match dto.Min |> ValueUnit.Dto.fromDto with
                | None -> None
                | Some vu ->
                    let min =
                        match dto.MinIncl with
                        | true -> Limit.inclusive vu
                        | false -> Limit.exclusive vu
                        |> Some

                    create min None None |> Some
            | false, true ->
                match dto.Max |> ValueUnit.Dto.fromDto with
                | None -> None
                | Some vu ->
                    let max =
                        match dto.MaxIncl with
                        | true -> Limit.inclusive vu
                        | false -> Limit.exclusive vu
                        |> Some

                    create None None max |> Some
            | true, true ->
                match dto.Min |> ValueUnit.Dto.fromDto, dto.Max |> ValueUnit.Dto.fromDto with
                | None, None
                | Some _, None
                | None, Some _ -> None
                | Some vu1, Some vu2 ->

                    let min, max =
                        match dto.MinIncl, dto.MaxIncl with
                        | false, false -> Limit.exclusive vu1, Limit.exclusive vu2
                        | true, true -> Limit.inclusive vu1, Limit.inclusive vu2
                        | true, false -> Limit.inclusive vu1, Limit.exclusive vu2
                        | false, true -> Limit.exclusive vu1, Limit.inclusive vu2

                    create (Some min) None (Some max)
                    |> validate
                    |> function
                        | Ok mm -> mm |> Some
                        | Error _ -> None


        let toDto (minmax: MinIncrMax) =
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


    /// TODO: this makes no sense in this lib!!!
    /// Turn a `MinMax` to a string with
    /// `mins` and `maxs` as annotations
    /// for resp. the min and max value.
    let toString minInclStr minExclStr maxInclStr maxExclStr { Min = min; Max = max } =
        let vuToStr vu =
            let milliGram = Units.Mass.milliGram

            let gram = Units.Mass.gram
            let day = Units.Time.day

            let per = ValueUnit.per
            let convertTo = ValueUnit.convertTo

            let milliGramPerDay = milliGram |> per day
            let gramPerDay = gram |> per day

            vu
            |> (fun vu ->
                match vu |> ValueUnit.get with
                | v, u when v >= [| 1000N |] && u = milliGram -> vu |> convertTo gram
                | v, u when v >= [| 1000N |] && u = milliGramPerDay -> vu |> convertTo gramPerDay
                | _ -> vu
            )
            |> ValueUnit.toReadableDutchStringWithPrec 2

        let minToString min =
            match min with
            | Inclusive vu -> $"{minInclStr}{vu |> vuToStr}"
            | Exclusive vu -> $"{minExclStr}{vu |> vuToStr}"

        let maxToString min =
            match min with
            | Inclusive vu -> $"{maxInclStr}{vu |> vuToStr}"
            | Exclusive vu -> $"{maxExclStr}{vu |> vuToStr}"

        match min, max with
        | None, None -> ""
        | Some min_, Some max_ -> $"%s{min_ |> minToString} - %s{max_ |> maxToString}"
        | Some min_, None -> (min_ |> minToString)
        | None, Some max_ -> (max_ |> maxToString)



/// Extension methods for the `Limit` type
type Limit with


    static member (*)(v1, v2) = MinIncrMax.calcLimit (*) v1 v2

    static member (/)(v1, v2) = MinIncrMax.calcLimit (/) v1 v2



/// Extension methods for the `MinMax` type
type MinIncrMax with


    static member (*)(mm1, mm2) = MinIncrMax.calc (*) mm1 mm2

    static member (/)(mm1, mm2) = MinIncrMax.calc (/) mm1 mm2