namespace Informedica.GenSolver.Lib


module Variable =

    open System
    open MathNet.Numerics

    open Informedica.GenUnits.Lib

    let raiseExc errs m = m |> Exceptions.raiseExc None errs


    module Name =

        open Informedica.Utils.Lib
        open Informedica.Utils.Lib.BCL

        /// Create with continuation with **succ** function
        /// when success and **fail** function when failure.
        /// Creates a `Name` from a`string`.
        let create succ fail s =
            let s = s |> String.trim

            if s |> String.IsNullOrWhiteSpace then
                Exceptions.NameNullOrWhiteSpaceException |> fail
            else if s |> String.length <= 1000 then
                s |> Name |> succ
            else
                s |> Exceptions.NameLongerThan1000 |> fail

        /// Returns a `Name` option if creation
        /// succeeds else `None`.
        let createOpt = create Some Option.none

        /// Create a `Name` that, raises
        /// an `NameException` when it fails.
        let createExc = create id (raiseExc [])

        /// Return the `string` value of a `Name`.
        let toString (Name s) = s



    /// Functions and types to create and handle `ValueRange`.
    module ValueRange =

        open Informedica.Utils.Lib
        open Informedica.Utils.Lib.BCL



        module Increment =


            let create vu =
                vu
                |> ValueUnit.filter ((<) 0N)
                |> ValueUnit.removeBigRationalMultiples
                |> fun vu ->
                    if vu |> ValueUnit.isEmpty |> not then
                        vu |> Increment
                    else
                        Exceptions.ValueRangeEmptyIncrement |> raiseExc []


            let map f (Increment incr) = incr |> f |> create


            let toValueUnit (Increment vu) = vu


            let minElement =
                toValueUnit >> ValueUnit.minElement


            let eqs incr1 incr2 =
                let vu1 = incr1 |> toValueUnit
                let vu2 = incr2 |> toValueUnit
                vu1 =? vu2


            let intersect (Increment incr1) (Increment incr2) =
                incr1 |> ValueUnit.intersect incr2 |> create


            let calc op incr1 incr2 =
                let incr1 = incr1 |> ValueUnit.toBase
                let incr2 = incr2 |> ValueUnit.toBase

                match op with
                // y.incr = x1.incr * x2.incr
                | ValueUnit.Operators.Mult -> incr1 |> op <| incr2 |> create |> Some

                // when y = x1 + x2 then y.incr = x1.incr and x2.incr
                | ValueUnit.Operators.Add -> //| BigRational.Subtr ->
                    let vs1 = incr1 |> ValueUnit.getValue
                    let vs2 = incr2 |> ValueUnit.getValue

                    incr1
                    |> ValueUnit.setValue (Array.append vs1 vs2)
                    |> create
                    |> Some

                // incr cannot be calculated based on division
                | _ -> None

            /// Calculate an increment with
            /// **incr1** of x1 and **incr2** of x2
            /// in an equation: y = x1 **op** x2
            let calcOpt op incr1 incr2 =
                match incr1, incr2 with
                | Some (Increment i1), Some (Increment i2) -> calc op i1 i2
                | _ -> None


            let toList (Increment incr) =
                incr |> ValueUnit.getValue |> Array.toList


            let isEmpty (Increment incr) = incr |> ValueUnit.isEmpty


            let count (Increment incr) =
                incr |> ValueUnit.getValue |> Array.length


            let restrict (Increment newIncr) (Increment oldIncr) =
                if newIncr =? oldIncr then
                    oldIncr
                else
                    newIncr
                    |> ValueUnit.toBase
                    |> ValueUnit.filter (fun i1 ->
                        oldIncr
                        |> ValueUnit.getBaseValue
                        |> Array.exists (fun i2 -> i1 |> BigRational.isMultiple i2)
                    )
                    |> ValueUnit.toUnit
                |> fun vu ->
                    if vu |> ValueUnit.isEmpty then
                        oldIncr
                    else
                        vu
                        |> ValueUnit.convertTo (oldIncr |> ValueUnit.getUnit)
                    |> create


            let toString exact (Increment incr) = $"{incr |> ValueUnit.toStr exact}"



        module Minimum =

            /// Create a `Minimum` that is
            /// either inclusive or exclusive.
            let create isIncl vu =
                if vu |> ValueUnit.isSingleValue then
                    if isIncl then
                        vu |> MinIncl
                    else
                        vu |> MinExcl
                else
                    failwith "a minimum can only be a single value"


            /// Apply **f** to the bigrational
            /// value of `Minimum`
            let apply fincl fexcl =
                function
                | MinIncl (m) -> m |> fincl
                | MinExcl (m) -> m |> fexcl


            let map fIncl fExcl =
                apply (fIncl >> (create true)) (fExcl >> (create false))

            /// Checks whether `Minimum` **m2** > **m1**
            /// Note that the fact that a Minimum is inclusive or exclusive
            /// must be taken into account.
            let minGTmin min1 min2 =
                match min2, min1 with
                | MinIncl m2, MinIncl m1
                | MinExcl m2, MinExcl m1
                | MinIncl m2, MinExcl m1 -> m2 >? m1
                | MinExcl m2, MinIncl m1 -> m2 >=? m1

            /// Checks whether `Minimum` **m2** <= **m1**
            let minSTEmin m1 m2 = m2 |> minGTmin m1 |> not


            let minGTEmin min1 min2 = min1 = min2 || minGTmin min1 min2


            let minSTmin min1 min2 = min2 |> minGTEmin min1 |> not


            /// Checks whether `Minimum` is exclusive.
            let isExcl =
                function
                | MinIncl _ -> false
                | MinExcl _ -> true


            /// Checks whether `Minimum` is inclusive.
            let isIncl = isExcl >> not


            /// Creates a `Minimum` from a `ValueUnit`.
            /// Returns `None` if an empty set.
            let minElement = ValueUnit.minElement >> Option.map MinIncl


            /// Convert a `Minimum` to a `ValueUnit`.
            let toValueUnit =
                function
                | MinIncl v
                | MinExcl v -> v


            let convertTo min =
                let u =
                    min |> toValueUnit |> ValueUnit.getUnit

                map (ValueUnit.convertTo u) (ValueUnit.convertTo u)


            let hasZeroUnit =
                toValueUnit >> ValueUnit.hasZeroUnit


            /// Convert a `Minimum` to a `ValueUnit` and a `bool`
            /// that signifies inclusive or exclusive
            let toBoolValueUnit =
                apply (fun vu -> true, vu) (fun vu -> false, vu)


            let eqs min1 min2 =
                let b1, vu1 = min1 |> toBoolValueUnit
                let b2, vu2 = min2 |> toBoolValueUnit
                (vu1 =? vu2) && (b1 = b2)


            let multipleOf incr min =
                let incr = incr |> Increment.toValueUnit

                match min |> toBoolValueUnit with
                | true, vu -> vu |> ValueUnit.minInclMultipleOf incr
                | false, vu -> vu |> ValueUnit.minExclMultipleOf incr
                |> create true


            let checkTooSmall min =
                if min
                   |> toValueUnit
                   |> ValueUnit.denominator
                   |> Array.exists (fun x -> x > Constants.MAX_BIGINT) then
                    min
                    |> Exceptions.ValueRangeMinOverFlow
                    |> raiseExc []


            let nonZeroNonNeg =
                let fIncl vu =
                    let vu =
                        vu |> ValueUnit.filter (fun br -> br > 0N)

                    if vu |> ValueUnit.isEmpty |> not then
                        vu |> create true
                    else
                        vu |> ValueUnit.setZeroNonNegative |> create false

                let fExcl vu =
                    let vu =
                        vu |> ValueUnit.filter (fun br -> br > 0N)

                    if vu |> ValueUnit.isEmpty |> not then
                        vu |> create false
                    else
                        vu |> ValueUnit.setZeroNonNegative |> create false

                apply fIncl fExcl



            let restrict newMin oldMin =
                newMin |> checkTooSmall

                if newMin |> minGTmin oldMin then
                    newMin |> convertTo oldMin
                else
                    oldMin


            let toString exact min =
                let b, vu = min |> toBoolValueUnit

                $"""{if b then "[" else "<"}{vu |> ValueUnit.toStr exact}"""



        module Maximum =


            /// Create a `Maximum` that is
            /// either inclusive or exclusive.
            let create isIncl m =
                if isIncl then
                    m |> MaxIncl
                else
                    m |> MaxExcl

            /// Apply **f** to the bigrational
            /// value of `Maximum`
            let apply fIncl fExcl =
                function
                | MaxIncl (m) -> m |> fIncl
                | MaxExcl (m) -> m |> fExcl


            let map fIncl fExcl =
                apply (fIncl >> (create true)) (fExcl >> (create false))


            /// Checks whether `Maximum` **m2** > **m1**
            /// Note that the fact that a maximum is inclusive or exclusive
            /// must be taken into account.
            let maxGTmax max1 max2 =
                match max2, max1 with
                | MaxIncl m2, MaxIncl m1
                | MaxExcl m2, MaxExcl m1
                | MaxExcl m2, MaxIncl m1 -> m2 >? m1
                | MaxIncl m2, MaxExcl m1 -> m2 >=? m1

            /// Checks whether `Maximum` **m2** <= **m1**
            let maxSTEmax m1 m2 = m2 |> maxGTmax m1 |> not


            let maxGTEmax max1 max2 = max1 = max2 || maxGTmax max1 max2


            let maxSTmax max1 max2 = max2 |> maxGTEmax max1 |> not


            /// Get the maximum value in a `BigRational` set.
            /// Returns `None` if an empty set.
            let maxElement = ValueUnit.maxElement >> Option.map MaxIncl


            /// Convert a `Maximum` to a `BigRational`.
            let toValueUnit =
                function
                | MaxIncl v
                | MaxExcl v -> v


            let convertTo max =
                let u =
                    max |> toValueUnit |> ValueUnit.getUnit

                map (ValueUnit.convertTo u) (ValueUnit.convertTo u)


            /// Checks whether `Maximum` is exclusive.
            let isExcl =
                function
                | MaxIncl _ -> false
                | MaxExcl _ -> true


            /// Checks whether `Maximum` is inclusive.
            let isIncl = isExcl >> not


            /// Turn a `Maximum` into a `BigRational` and a `bool` to indicate
            /// inclusive or exclusive.
            let toBoolValueUnit =
                apply (fun m -> true, m) (fun m -> false, m)


            let eqs max1 max2 =
                let b1, vu1 = max1 |> toBoolValueUnit
                let b2, vu2 = max2 |> toBoolValueUnit
                (vu1 =? vu2) && (b1 = b2)


            let multipleOf incr max =
                let incr = incr |> Increment.toValueUnit

                match max |> toBoolValueUnit with
                | true, vu -> vu |> ValueUnit.maxInclMultipleOf incr
                | false, vu -> vu |> ValueUnit.maxExclMultipleOf incr
                |> create true


            let checkTooLarge max =
                if max
                   |> toValueUnit
                   |> ValueUnit.numerator
                   |> Array.exists (fun x -> x > Constants.MAX_BIGINT) then
                    max
                    |> Exceptions.ValueRangeMaxOverFlow
                    |> raiseExc []


            let nonZeroNonNeg max =
                max
                |> toValueUnit
                |> ValueUnit.getUnit
                |> ValueUnit.withSingleValue 0N
                |> Minimum.create false,
                max
                |> map (ValueUnit.filter (fun br -> br > 0N)) (ValueUnit.filter (fun br -> br > 0N))


            let restrict newMax oldMax =
                newMax |> checkTooLarge

                if newMax |> maxSTmax oldMax then
                    newMax |> convertTo oldMax
                else
                    oldMax


            let toString exact max =
                let b, vu = max |> toBoolValueUnit

                $"""{vu |> ValueUnit.toStr exact}{if b then "]" else ">"}"""



        module ValueSet =


            /// Create a `ValueSet` from a set of `BigRational`.
            let create vu =
                if vu |> ValueUnit.isEmpty then
                    Exceptions.ValueRangeEmptyValueSet |> raiseExc []

                else
                    vu |> ValueSet


            let toSet (ValueSet vu) = vu


            let map f (ValueSet vu) = vu |> f |> create


            // ToDo refactor to just ValueUnit.getMin
            let getMin (ValueSet vu) = vu |> Minimum.minElement


            // ToDo idem
            let getMax (ValueSet vu) = vu |> Maximum.maxElement


            let count (ValueSet vu) =
                vu |> ValueUnit.getValue |> Array.length


            let isEmpty (ValueSet vu) = vu |> ValueUnit.isEmpty


            let contains v (ValueSet vu) = vu |> ValueUnit.containsValue v


            let intersect (ValueSet vu1) (ValueSet vu2) =
                vu1
                |> ValueUnit.intersect vu2
                |> ValueUnit.convertTo (vu1 |> ValueUnit.getUnit)
                |> create


            let isSubset (ValueSet vu1) (ValueSet vu2) = ValueUnit.isSubset vu1 vu2


            let eqs (ValueSet vu1) (ValueSet vu2) = vu1 =? vu2


            let nonZeroNonNeg =
                map (ValueUnit.filterValues (fun br -> br > 0N))


            // ToDo refactor
            let calc op (ValueSet s1) (ValueSet s2) =
                let count =
                    ValueUnit.getValue >> Array.length
                // When one of the sets does not contain any value then the result of
                // of the calculation cannot contain any value either
                // if s1 |> Set.isEmpty || s2 |> Set.isEmpty then
                //     Exceptions.ValueRangeEmptyValueSet
                //     |> raiseExc
                // make sure the calculation doesn't take too long
                if (s1 |> count) + (s2 |> count) > Constants.MAX_CALC_COUNT then
                    (s1 |> count) + (s2 |> count)
                    |> Exceptions.ValueRangeTooManyValues
                    |> raiseExc []

                else
                    s1 |> op <| s2 |> create


            // ToDo refactor
            let toString exact (ValueSet vs) =
                let count =
                    ValueUnit.getValue >> Array.length

                if vs |> count <= 10 then
                    $"""[{vs |> ValueUnit.toStr exact}]"""
                else
                    let first3 = vs |> ValueUnit.takeFirst 3
                    let last3 = vs |> ValueUnit.takeLast 3
                    $"[{first3 |> ValueUnit.toStr exact} .. {last3 |> ValueUnit.toStr exact}]"


        module Property =


            let createMinProp b v = v |> Minimum.create b |> MinProp
            let createMinInclProp = createMinProp true
            let createMinExclProp = createMinProp false
            let createMaxProp b v = v |> Maximum.create b |> MaxProp
            let createMaxInclProp = createMaxProp true
            let createMaxExclProp = createMaxProp false
            let createIncrProp vs = vs |> Increment.create |> IncrProp
            let createValsProp vs = vs |> ValueSet.create |> ValsProp


            let mapValue f =
                function
                | MinProp min -> min |> Minimum.map f f |> MinProp
                | MaxProp max -> max |> Maximum.map f f |> MaxProp
                | IncrProp incr -> incr |> Increment.map f |> IncrProp
                | ValsProp vs -> vs |> ValueSet.map f |> ValsProp


            let toValueRange p =

                match p with
                | MinProp min -> min |> Min
                | MaxProp max -> max |> Max
                | IncrProp incr -> incr |> Incr
                | ValsProp vs -> vs |> ValSet


            let getMin =
                function
                | MinProp min -> min |> Some
                | _ -> None


            let getMax =
                function
                | MaxProp max -> max |> Some
                | _ -> None


            let getIncr =
                function
                | IncrProp incr -> incr |> Some
                | _ -> None


            let toString exact =
                function
                | MinProp min -> $"{min |> Minimum.toString exact}.."
                | MaxProp max -> $"..{max |> Maximum.toString exact}"
                | IncrProp incr -> $"..{incr |> Increment.toString exact}.."
                | ValsProp vs -> vs |> ValueSet.toString exact


        let map fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet vr =
            match vr with
            | Unrestricted -> vr
            | NonZeroNoneNegative -> vr
            | Min min -> min |> fMin |> Min
            | Max max -> max |> fMax |> Max
            | MinMax (min, max) -> (min, max) |> fMinMax |> MinMax
            | Incr incr -> incr |> fIncr |> Incr
            | MinIncr (min, incr) -> (min, incr) |> fMinIncr |> MinIncr
            | IncrMax (incr, max) -> (incr, max) |> fIncrMax |> IncrMax
            | MinIncrMax (min, incr, max) -> (min, incr, max) |> fMinIncrMax |> MinIncrMax
            | ValSet vs -> vs |> fValueSet |> ValSet


        let apply unr nonz fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet =
            function
            | Unrestricted -> unr
            | NonZeroNoneNegative -> nonz
            | Min min -> min |> fMin
            | Max max -> max |> fMax
            | MinMax (min, max) -> (min, max) |> fMinMax
            | Incr incr -> incr |> fIncr
            | MinIncr (min, incr) -> (min, incr) |> fMinIncr
            | IncrMax (incr, max) -> (incr, max) |> fIncrMax
            | MinIncrMax (min, incr, max) -> (min, incr, max) |> fMinIncrMax
            | ValSet vs -> vs |> fValueSet


        /// Count the number of values in a `ValueRange`.
        /// Returns 0 if no count is possible.
        let cardinality =
            let zero _ = 0
            apply 0 0 zero zero zero zero zero zero zero ValueSet.count


        /// Checks whether a `ValueRange` is `Unrestricted`
        let isUnrestricted =
            let returnFalse = Boolean.returnFalse

            apply
                true
                false
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse

        /// Checks whether a `ValueRange` is `Unrestricted`
        let isNonZeroOrNegative =
            let returnFalse = Boolean.returnFalse

            apply
                false
                true
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse


        /// Checks whether a `ValueRange` is `Min`
        let isMin =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                Boolean.returnTrue
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse


        /// Checks whether a `ValueRange` is `Max`
        let isMax =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                Boolean.returnTrue
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse


        /// Checks whether a `ValueRange` is `MinMax`
        let isMinMax =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                returnFalse
                Boolean.returnTrue
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse


        let isIncr =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                returnFalse
                returnFalse
                Boolean.returnTrue
                returnFalse
                returnFalse
                returnFalse
                returnFalse


        let isMinIncr =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                Boolean.returnTrue
                returnFalse
                returnFalse
                returnFalse


        let isIncrMax =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                Boolean.returnTrue
                returnFalse
                returnFalse


        let isMinIncrMax =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                Boolean.returnTrue
                returnFalse


        let isValueSet =
            let returnFalse = Boolean.returnFalse

            apply
                false
                false
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                returnFalse
                Boolean.returnTrue


        /// Checks whether a `BigRational` is between an optional
        /// **min** and an optional **max**
        let isBetweenMinMax min max br =
            let fMin =
                function
                | None -> true
                | Some (Minimum.MinIncl m) ->
                    m
                    |> ValueUnit.getBaseValue
                    |> Array.forall (fun v -> br >= v)
                | Some (Minimum.MinExcl m) ->
                    m
                    |> ValueUnit.getBaseValue
                    |> Array.forall (fun v -> br > v)

            let fMax =
                function
                | None -> true
                | Some (Maximum.MaxIncl m) ->
                    m
                    |> ValueUnit.getBaseValue
                    |> Array.forall (fun v -> br <= v)

                | Some (Maximum.MaxExcl m) ->
                    m
                    |> ValueUnit.getBaseValue
                    |> Array.forall (fun v -> br < v)

            (fMin min) && (fMax max)


        let isMultipleOfIncr incrOpt br =
            let isDiv i = br |> BigRational.isMultiple i

            match incrOpt with
            | None -> true
            | Some (Increment incr) -> incr |> ValueUnit.getBaseValue |> Seq.exists isDiv

        /// Filter a set of `BigRational` according
        /// to **min** **max** and incr constraints
        let filter minOpt incrOpt maxOpt (ValueSet vs) =
            vs
            |> ValueUnit.filter (fun v ->
                v |> isBetweenMinMax minOpt maxOpt
                && v |> isMultipleOfIncr incrOpt
            )
            |> ValueSet.create


        let minEQmax max min =
            match min, max with
            | Minimum.MinIncl min, Maximum.MaxIncl max -> min =? max
            | _ -> false

        /// Checks whether `Minimum` **min** > `Maximum` **max**.
        /// Note that inclusivity or exclusivity of a minimum and maximum must be
        /// accounted for.
        let minGTmax max min =
            match min, max with
            | Minimum.MinIncl min, Maximum.MaxIncl max -> min >? max
            | Minimum.MinExcl min, Maximum.MaxIncl max
            | Minimum.MinExcl min, Maximum.MaxExcl max
            | Minimum.MinIncl min, Maximum.MaxExcl max -> min >=? max

        /// Checks whether `Minimum` **min** <= `Maximum` **max**
        let minSTEmax max min = min |> minGTmax max |> not


        let minMultipleOf incr min = min |> Minimum.multipleOf incr


        let maxMultipleOf incr max = max |> Maximum.multipleOf incr


        /// An `Unrestricted` `ValueRange`.
        let unrestricted = Unrestricted


        let nonZeroOrNegative = NonZeroNoneNegative


        /// Create a `MinMax` `ValueRange`. If **min** > **max** raises
        /// an `MinLargetThanMax` exception. If min equals max, a `ValueSet` with
        /// value min (= max).
        let minMaxToValueRange min max =
            if min |> minGTmax max then
                // printfn $"min:\n{min}\nmax:\n{max}"
                (min, max)
                |> Exceptions.Message.ValueRangeMinLargerThanMax
                |> raiseExc []

            elif min |> minEQmax max then
                min
                |> Minimum.toValueUnit
                |> ValueSet.create
                |> ValSet

            else
                (min, max) |> MinMax


        let minIncrToValueRange min incr =
            if min |> Minimum.hasZeroUnit |> not then
                (min |> minMultipleOf incr, incr)
                |> MinIncr
            else
                match incr |> Increment.minElement with
                | Some vu ->
                    (vu |> Minimum.create true, incr)
                    |> MinIncr
                | None -> incr |> Incr


        /// Create an `IncrMax` `ValueRange`.
        let incrMaxToValueRange incr max =
            (incr, max |> maxMultipleOf incr) |> IncrMax


        /// Create a `MinIncrMax` `ValueRange`. If **min** > **max** raises
        /// an `MinLargetThanMax` exception. If min equals max, a `ValueSet` with
        /// value min (=max).
        let minIncrMaxToValueRange onlyMinIncrMax min incr max =
            let min = min |> minMultipleOf incr
            let max = max |> maxMultipleOf incr

            if min |> minGTmax max then
                (min, max)
                |> Exceptions.Message.ValueRangeMinLargerThanMax
                |> raiseExc []
            else if onlyMinIncrMax && (min |> minEQmax max |> not) then
                MinIncrMax(min, incr, max)
            else
                // TODO: ugly hack to prevent expensive calc
                try
                    let min =
                        min
                        |> Minimum.toValueUnit
                        |> ValueUnit.getBaseValue

                    let max =
                        max
                        |> Maximum.toValueUnit
                        |> ValueUnit.getBaseValue

                    incr
                    |> Increment.toValueUnit
                    |> ValueUnit.toBase
                    |> ValueUnit.applyToValue (fun incr ->
                        [|
                            for i in incr do
                                for mi in min do
                                    for ma in max do
                                        if (ma - mi) / i > 10000N then
                                            printfn "calculating valset is too expensive"
                                        else
                                            [| mi..i..ma |]
                        |]
                        |> Array.collect id
                    )
                    |> ValueUnit.toUnit
                    |> ValueSet.create
                    |> ValSet
                with
                | _ -> MinIncrMax(min, incr, max)


        /// Create a `Minimum` `Range` that is
        /// either inclusive or exclusive.
        let createMin isIncl m = m |> Minimum.create isIncl |> Min


        /// Create a `Maximum` `Range` that is
        /// either inclusive or exclusive.
        let createMax isIncl m = m |> Maximum.create isIncl |> Max


        let createMinMax min minIncl max maxIncl =
            let min = min |> Minimum.create minIncl
            let max = max |> Maximum.create maxIncl

            minMaxToValueRange min max

        /// Create a `Range` with a `Minimum`, `Increment` and a `Maximum`.
        let createIncr = Increment.create >> Incr


        let createValSet brs = brs |> ValueSet.create |> ValSet


        /// Create a `MinIncr` `ValueRange`.
        let createMinIncr min minIncl incr =
            incr
            |> Increment.create
            |> minIncrToValueRange (Minimum.create min minIncl)


        let createIncrMax incr max maxIncl =
            max
            |> Maximum.create maxIncl
            |> incrMaxToValueRange (incr |> Increment.create)


        /// Create a `ValueRange` using a `ValueSet` **vs**
        /// an optional `Minimum` **min**, **incr** and `Maximum` **max**.
        /// If both **min**, **incr** and **max** are `None` an `Unrestricted`
        /// `ValueRange` is created.
        let create onlyMinIncrMax min incr max vs =
            match vs with
            | None ->
                match min, incr, max with
                | None, None, None -> unrestricted
                | Some min, None, None -> min |> Min
                | None, None, Some max -> max |> Max
                | Some min, None, Some max -> minMaxToValueRange min max
                | Some min, Some incr, None -> minIncrToValueRange min incr
                | None, Some incr, Some max -> incrMaxToValueRange incr max
                | None, Some incr, None -> incr |> Incr
                | Some min, Some incr, Some max -> minIncrMaxToValueRange onlyMinIncrMax min incr max

            | Some vs -> vs |> filter min incr max |> ValSet

        /// Get an optional `Minimum` in a `ValueRange`
        let getMin =
            apply
                None
                None
                Some
                Option.none
                (fst >> Some)
                Option.none
                (fst >> Some)
                Option.none
                (Tuple.fstOf3 >> Some)
                ValueSet.getMin

        /// Get an optional `Maximum` in a `ValueRange`
        let getIncr =
            apply
                None
                None
                Option.none
                Option.none
                Option.none
                Some
                (snd >> Some)
                (fst >> Some)
                (Tuple.sndOf3 >> Some)
                Option.none

        /// Get an optional `Maximum` in a `ValueRange`
        let getMax =
            apply
                None
                None
                Option.none
                Some
                (snd >> Some)
                Option.none
                Option.none
                (snd >> Some)
                (Tuple.thrdOf3 >> Some)
                ValueSet.getMax

        /// Get an optional `ValueSet` in a `ValueRange`
        let getValSet =
            apply None None Option.none Option.none Option.none Option.none Option.none Option.none Option.none Some

        /// Check whether a `ValueRange` **vr** contains
        /// a `BigRational` **v**.
        let contains v vr =
            match vr with
            | ValSet vs -> vs |> ValueSet.contains v
            | _ ->
                let min = vr |> getMin
                let max = vr |> getMax

                let incr = vr |> getIncr

                v
                |> ValueUnit.getBaseValue
                |> Array.forall (isBetweenMinMax min max)
                && v
                   |> ValueUnit.getBaseValue
                   |> Array.forall (isMultipleOfIncr incr)


        /// Apply a **incr** to a `ValueRange` **vr**.
        /// If increment cannot be set the original is returned.
        /// So, the resulting increment is always more restrictive as the previous one
        let setIncr onlyMinIncrMax newIncr vr =
            let restrict = Increment.restrict newIncr

            let nonZero =
                match newIncr
                      |> Increment.toValueUnit
                      |> Minimum.minElement
                    with
                | Some min -> (min, newIncr) |> MinIncr
                | None -> NonZeroNoneNegative

            let fMin min = minIncrToValueRange min newIncr

            let fMax max = incrMaxToValueRange newIncr max

            let fMinMax (min, max) =
                minIncrMaxToValueRange onlyMinIncrMax min newIncr max

            let fIncr = restrict >> Incr

            let fMinIncr (min, incr) =
                minIncrToValueRange min (incr |> restrict)

            let fIncrMax (incr, max) =
                incrMaxToValueRange (incr |> restrict) max

            let fMinIncrMax (min, incr, max) =
                minIncrMaxToValueRange onlyMinIncrMax min (incr |> restrict) max

            let fValueSet =
                filter None (Some newIncr) None >> ValSet

            vr
            |> apply (newIncr |> Incr) nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet


        /// Apply a `Minimum` **min** to a `ValueRange` **vr**.
        /// If minimum cannot be set the original `Minimum` is returned.
        /// So, it always returns a more restrictive, i.e. larger, or equal `Minimum`.
        let setMin onlyMinIncrMax newMin (vr: ValueRange) =
            let restrict = Minimum.restrict newMin

            let nonZero =
                newMin |> Minimum.nonZeroNonNeg |> Min

            let fMin = restrict >> Min

            let fMax max = minMaxToValueRange newMin max

            let fMinMax (min, max) =
                minMaxToValueRange (min |> restrict) max

            let fIncr incr = minIncrToValueRange newMin incr

            let fMinIncr (min, incr) =
                minIncrToValueRange (min |> restrict) incr

            let fIncrMax (incr, max) =
                minIncrMaxToValueRange onlyMinIncrMax newMin incr max

            let fMinIncrMax (min, incr, max) =
                minIncrMaxToValueRange onlyMinIncrMax (min |> restrict) incr max

            let fValueSet =
                filter (Some newMin) None None >> ValSet

            vr
            |> apply (newMin |> Min) nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet



        let setMax onlyMinIncrMax newMax (vr: ValueRange) =
            let restrict = Maximum.restrict newMax

            let nonZero =
                newMax |> Maximum.nonZeroNonNeg |> MinMax

            let fMin min = minMaxToValueRange min newMax

            let fMax max = max |> restrict |> Max

            let fMinMax (min, max) =
                minMaxToValueRange min (max |> restrict)

            let fIncr incr = incrMaxToValueRange incr newMax

            let fMinIncr (min, incr) =
                minIncrMaxToValueRange onlyMinIncrMax min incr newMax

            let fIncrMax (incr, max) =
                incrMaxToValueRange incr (max |> restrict)

            let fMinIncrMax (min, incr, max) =
                minIncrMaxToValueRange onlyMinIncrMax min incr (max |> restrict)

            let fValueSet =
                filter None None (Some newMax) >> ValSet

            vr
            |> apply (newMax |> Max) nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet


        let setValueSet newVs (vr: ValueRange) =
            let min, incr, max, oldVs =
                vr |> getMin, vr |> getIncr, vr |> getMax, vr |> getValSet

            match oldVs with
            | None -> newVs |> filter min incr max
            | Some vs -> newVs |> ValueSet.intersect vs
            |> ValSet


        let nonZeroNonNegative vr =
            let fMin min = min |> Minimum.nonZeroNonNeg |> Min

            let fMax max = max |> Maximum.nonZeroNonNeg |> MinMax

            let fMinMax (min, max) =
                let newMin, max =
                    max |> Maximum.nonZeroNonNeg
                let min = min |> Minimum.restrict newMin

                minMaxToValueRange min max

            let fIncr incr =
                match incr
                      |> Increment.toValueUnit
                      |> Minimum.minElement
                    with
                | Some min -> (min, incr) |> MinIncr
                | None -> NonZeroNoneNegative

            let fMinIncr (min, incr) =
                let min = min |> Minimum.nonZeroNonNeg
                minIncrToValueRange min incr

            let fIncrMax (incr, max) =
                let newMin, max =
                    max |> Maximum.nonZeroNonNeg

                minIncrMaxToValueRange true newMin incr max

            let fMinIncrMax (min, incr, max) =
                let newMin, max =
                    max |> Maximum.nonZeroNonNeg
                let min = min |> Minimum.restrict newMin

                minIncrMaxToValueRange true min incr max

            let fValueSet =
                ValueSet.nonZeroNonNeg >> ValSet

            vr
            |> apply
                NonZeroNoneNegative
                NonZeroNoneNegative
                fMin
                fMax
                fMinMax
                fIncr
                fMinIncr
                fIncrMax
                fMinIncrMax
                fValueSet


        let eqs vr1 vr2 =
            match vr1, vr2 with
            | Unrestricted, Unrestricted
            | NonZeroNoneNegative, NonZeroNoneNegative -> true
            | Min m1, Min m2 -> m1 |> Minimum.eqs m2
            | Max m1, Max m2 -> m1 |> Maximum.eqs m2
            | MinMax (min1, max1), MinMax (min2, max2) ->
                min1 |> Minimum.eqs min2
                && max1 |> Maximum.eqs max2
            | Incr incr1, Incr incr2 -> incr1 |> Increment.eqs incr2
            | MinIncr (min1, incr1), MinIncr (min2, incr2) ->
                min1 |> Minimum.eqs min2
                && incr1 |> Increment.eqs incr2
            | IncrMax (incr1, max1), IncrMax (incr2, max2) ->
                max1 |> Maximum.eqs max2
                && incr1 |> Increment.eqs incr2
            | MinIncrMax (min1, incr1, max1), MinIncrMax (min2, incr2, max2) ->
                min1 |> Minimum.eqs min2
                && incr1 |> Increment.eqs incr2
                && max1 |> Maximum.eqs max2
            | ValSet vs1, ValSet vs2 -> vs1 |> ValueSet.eqs vs2
            | _ -> false


        /// Create a string (to print) representation of a `ValueRange`.
        /// `Exact` true prints exact bigrationals, when false
        /// print as floating numbers
        let print exact isNonZero min incr max vs =
            if isNonZero then
                "<0..>"

            else
                let printRange min incr max =
                    let minToStr = Minimum.toString exact
                    let maxToStr = Maximum.toString exact
                    let incrToStr = Increment.toString exact

                    match min, incr, max with
                    | None, None, None -> "<..>"
                    | Some min, None, None -> $"{min |> minToStr}..>"
                    | Some min, None, Some max -> $"{min |> minToStr}..{max |> maxToStr}"
                    | None, None, Some max -> $"<..{max |> maxToStr}"
                    | None, Some incr, None -> $"<..{incr |> incrToStr}..>"
                    | Some min, Some incr, None -> $"{min |> minToStr}..{incr |> incrToStr}..>"
                    | None, Some incr, Some max -> $"<..{incr |> incrToStr}..{max |> maxToStr}"
                    | Some min, Some incr, Some max -> $"{min |> minToStr}..{incr |> incrToStr}..{max |> maxToStr}"

                match vs with
                | Some vs -> $"{vs |> ValueSet.toString exact}"
                | None -> printRange min incr max


        /// Convert a `ValueRange` to a `string`.
        let toString exact vr =
            let fVs vs =
                print exact false None None None (Some vs)

            let unr =
                print exact false None None None None

            let nonZero =
                print exact true None None None None

            let print min incr max = print exact false min incr max None

            let fMin min = print (Some min) None None

            let fMax max = print None None (Some max)

            let fIncr incr = print None (Some incr) None

            let fMinIncr (min, incr) = print (Some min) (Some incr) None

            let fIncrMax (incr, max) = print None (Some incr) (Some max)

            let fMinIncrMax (min, incr, max) = print (Some min) (Some incr) (Some max)

            let fMinMax (min, max) = print (Some min) None (Some max)

            vr
            |> apply unr nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fVs

        /// Functions to calculate the `Minimum`
        /// and `Maximum` in a `ValueRange`.
        /// I.e. what happens when you mult, div, add or subtr
        /// a `Range`, for example:
        /// <1N..3N> * <4N..5N> = <4N..15N>
        module MinMaxCalculator =


            /// Calculate **x1** and **x2** with operator **op**
            /// and use **incl1** and **inc2** to determine whether
            /// the result is inclusive. Use constructor **c** to
            /// create the optional result.
            let calc c op (x1, incl1) (x2, incl2) =
                // printfn "start minmax calc"
                let opIsMultOrDiv =
                    (op |> ValueUnit.Operators.opIsMult
                     || op |> ValueUnit.Operators.opIsDiv)

                let incl =
                    match incl1, incl2 with
                    | true, true -> true
                    | _ -> false
                // printfn "start minmax calc match"
                match x1, x2 with
                // This disables correct unit calculation!!
                | Some v, None when opIsMultOrDiv && v |> ValueUnit.isZero ->
                    0N
                    |> ValueUnit.singleWithUnit ZeroUnit
                    |> c incl1
                    |> Some

                | Some v, None
                | None, Some v when
                    op |> ValueUnit.Operators.opIsMult
                    && v |> ValueUnit.isZero
                    ->
                    0N
                    |> ValueUnit.singleWithUnit ZeroUnit
                    |> c incl
                    |> Some

                | Some _, None when op |> ValueUnit.Operators.opIsDiv ->
                    0N
                    |> ValueUnit.singleWithUnit ZeroUnit
                    |> c incl
                    |> Some

                | Some v1, Some v2 when
                    v1 |> ValueUnit.hasZeroUnit
                    || v2 |> ValueUnit.hasZeroUnit
                    ->
                    None

                // Units can correctly be calculated if both have dimensions
                | Some v1, Some v2 ->
                    if op |> ValueUnit.Operators.opIsDiv
                       && v2 |> ValueUnit.isZero then
                        None
                    else
                        v1 |> op <| v2 |> c incl |> Some

                | _ -> None


            /// Calculate an optional `Minimum`
            let calcMin = calc Minimum.create


            /// Calculate an optional `Maximum`
            let calcMax = calc Maximum.create


            let minimize min1 min2 =
                match min1, min2 with
                | None, None -> None
                | Some _, None
                | None, Some _ -> None
                | Some m1, Some m2 ->
                    if m1 |> Minimum.minSTmin m2 then
                        m1
                    else
                        m2
                    |> Some


            let maximize max1 max2 =
                match max1, max2 with
                | None, None -> None
                | Some _, None
                | None, Some _ -> None
                | Some m1, Some m2 ->
                    if m1 |> Maximum.maxGTmax m2 then
                        m1
                    else
                        m2
                    |> Some


            /// Match a min, max tuple **min**, **max**
            /// to:
            ///
            /// * `PP`: both positive
            /// * `NN`: both negative
            /// * `NP`: one negative, the other positive
            let (|PP|NN|NP|NZ|ZP|) (min, max) =
                match min, max with
                | Some min, _ when min |> ValueUnit.gtZero -> PP
                | _, Some max when max |> ValueUnit.stZero -> NN
                | Some min, Some max when min |> ValueUnit.stZero && max |> ValueUnit.gtZero -> NP
                | None, Some max when max |> ValueUnit.gtZero -> NP
                | Some min, None when min |> ValueUnit.stZero -> NP
                | None, None -> NP
                | _, Some max when max |> ValueUnit.isZero -> NZ
                | Some min, _ when min |> ValueUnit.isZero -> ZP
                // failing cases
                | Some min, Some max when min |> ValueUnit.isZero && max |> ValueUnit.isZero ->
                    //printfn "failing case"
                    $"{min} = {max} = 0"
                    |> Exceptions.ValueRangeMinMaxException
                    |> Exceptions.raiseExc None []

                | Some min, Some max when
                    min |> ValueUnit.gteZero
                    && max |> ValueUnit.stZero
                    ->
                    $"{min} > {max}"
                    |> Exceptions.ValueRangeMinMaxException
                    |> Exceptions.raiseExc None []

                | _ ->
                    printfn "could not handel failing case"

                    $"could not handle {min} {max}"
                    |> Exceptions.ValueRangeMinMaxException
                    |> Exceptions.raiseExc None []



            /// Calculate `Minimum` option and
            /// `Maximum` option for addition of
            /// (**min1**, **max1**) and (**min2**, **max2)
            let addition min1 max1 min2 max2 =
                let min = calcMin (+) min1 min2
                let max = calcMax (+) max1 max2
                min, max


            /// Calculate `Minimum` option and
            /// `Maximum` option for subtraction of
            /// (**min1**, **max1**) and (**min2**, **max2)
            let subtraction min1 max1 min2 max2 =
                let min = calcMin (-) min1 max2
                let max = calcMax (-) max1 min2
                min, max


            /// Calculate `Minimum` option and
            /// `Maximum` option for multiplication of
            /// (**min1**, **max1**) and (**min2**, **max2)
            let multiplication min1 max1 min2 max2 =
                //printfn "start multiplication"
                match ((min1 |> fst), (max1 |> fst)), ((min2 |> fst), (max2 |> fst)) with
                | PP, PP -> // min = min1 * min2, max = max1 * max2
                    calcMin (*) min1 min2, calcMax (*) max1 max2
                | PP, ZP -> // min = min1 * min2, max = max1 * max2
                    calcMin (*) min1 min2, calcMax (*) max1 max2
                | PP, NN -> // min = max1 * min2, max = min1 * max2
                    calcMin (*) max1 min2, calcMax (*) min1 max2
                | PP, NZ -> // min = max1 * min2, max = min1 * max2
                    calcMin (*) max1 min2, calcMax (*) min1 max2
                | PP, NP -> // min = min1 * min2, max = max1 * max2
                    calcMin (*) max1 min2, calcMax (*) max1 max2

                | ZP, PP -> // min = min1 * min2, max = max1 * max2
                    calcMin (*) min1 min2, calcMax (*) max1 max2
                | ZP, ZP -> // min = min1 * min2, max = max1 * max2
                    calcMin (*) min1 min2, calcMax (*) max1 max2
                | ZP, NN -> // min = max1 * min2, max = min1 * max2
                    calcMin (*) max1 min2, calcMax (*) min1 max2
                | ZP, NZ -> // min = max1 * min2, max = min1 * max2
                    calcMin (*) max1 min2, calcMax (*) min1 max2
                | ZP, NP -> // min = min1 * min2, max = max1 * max2
                    calcMin (*) min1 min2, calcMax (*) max1 max2

                | NN, PP -> // min = min1 * max2, max = max1 * min2
                    calcMin (*) min1 max2, calcMax (*) max1 min2
                | NN, ZP -> // min = min1 * max2, max = max1 * min2
                    calcMin (*) min1 max2, calcMax (*) max1 min2
                | NN, NN -> // min = max1 * max2, max = min1 * min2
                    calcMin (*) max1 max2, calcMax (*) min1 min2
                | NN, NZ -> // min = max1 * max2, max = min1 * min2
                    calcMin (*) max1 max2, calcMax (*) min1 min2
                | NN, NP -> // min = min1 * max2, max = min1 * min2
                    calcMin (*) min1 max2, calcMax (*) min1 min2

                | NZ, PP -> // min = min1 * max2, max = max1 * min2
                    calcMin (*) min1 max2, calcMax (*) max1 min2
                | NZ, ZP -> // min = min1 * max2, max = max1 * min2
                    calcMin (*) min1 max2, calcMax (*) max1 min2
                | NZ, NN -> // min = max1 * max2, max = min1 * min2
                    calcMin (*) max1 max2, calcMax (*) min1 min2
                | NZ, NZ -> // min = max1 * max2, max = min1 * min2
                    calcMin (*) max1 max2, calcMax (*) min1 min2
                | NZ, NP -> // min = min1 * max2, max = min1 * min2
                    calcMin (*) min1 max2, calcMax (*) min1 min2

                | NP, PP -> // min = min1 * max2, max = max1 * max2
                    calcMin (*) min1 max2, calcMax (*) max1 max2
                | NP, ZP -> // min = min1 * max2, max = max1 * max2
                    calcMin (*) min1 max2, calcMax (*) max1 max2
                | NP, NN -> // min = max1 * min2, max = min1 * min2
                    calcMin (*) max1 min2, calcMax (*) min1 min2
                | NP, NZ -> // min = max1 * min2, max = min1 * min2
                    minimize (calcMin (*) min1 max2) (calcMin (*) min2 max1),
                    maximize (calcMax (*) max1 max2) (calcMax (*) min1 min2)
                | NP, NP -> // min = min1 * max2, max = max1 * max2
                    minimize (calcMin (*) min1 max2) (calcMin (*) min2 max1),
                    maximize (calcMax (*) max1 max2) (calcMax (*) min1 min2)


            /// Calculate `Minimum` option and
            /// `Maximum` option for division of
            /// (**min1**, **max1**) and (**min2**, **max2)
            let division min1 max1 min2 max2 =
                match (min1 |> fst, max1 |> fst), (min2 |> fst, max2 |> fst) with
                | PP, PP -> // min = min1 / max2, max =	max1 / min2
                    calcMin (/) min1 max2, calcMax (/) max1 min2
                | PP, NN -> // min = max1 / max2	, max = min1 / min2
                    calcMin (/) max1 max2, calcMax (/) min1 min2
                | PP, ZP -> calcMin (/) min1 max2, calcMax (/) max1 min2

                | ZP, PP -> // min = min1 / max2, max =	max1 / min2
                    calcMin (/) min1 max2, calcMax (/) max1 min2
                | ZP, NN -> // min = max1 / max2	, max = min1 / min2
                    calcMin (/) max1 max2, calcMax (/) min1 min2
                | ZP, ZP -> calcMin (/) min1 max2, calcMax (/) max1 min2

                | NN, PP -> // min = min1 / min2, max = max1 / max2
                    calcMin (/) min1 min2, calcMax (/) max1 max2
                | NN, NN -> // min = max1 / min2	, max = min1 / max2
                    calcMin (/) max1 min2, calcMax (/) min1 max2
                | NN, NZ -> calcMin (/) max1 min2, calcMax (/) min1 max2
                | NN, ZP -> calcMin (/) min1 min2, calcMax (/) max1 max2

                | NZ, PP -> // min = min1 / min2, max = max1 / max2
                    calcMin (/) min1 min2, calcMax (/) max1 max2
                | NZ, NN -> // min = max1 / min2	, max = min1 / max2
                    calcMin (/) max1 min2, calcMax (/) min1 max2
                | NZ, NZ -> calcMin (/) max1 min2, calcMax (/) min2 max2

                | NP, PP -> // min = min1 / min2, max = max1 / min2
                    calcMin (/) min1 min2, calcMax (/) max1 min2
                | NP, NN -> // min = max1 / max2, max = min1 / max2
                    calcMin (/) max1 max2, calcMax (/) min1 max2
                // division by range containing zero
                | NN, NP
                | PP, NP
                | NP, NP
                | NZ, NP
                | ZP, NP

                | NP, ZP
                | NZ, ZP

                | PP, NZ
                | NP, NZ
                | ZP, NZ -> None, None


            /// Match the right minmax calcultion
            /// according to the operand
            let calcMinMax op =
                match op with
                | ValueUnit.Operators.Mult -> multiplication
                | ValueUnit.Operators.Div -> division
                | ValueUnit.Operators.Add -> addition
                | ValueUnit.Operators.Subtr -> subtraction


        /// Applies an infix operator **op**
        /// to `ValueRange` **x1** and **x2**.
        /// Calculates `Minimum`, increment or `Maximum`
        /// if either **x1** or **x2** is not a `ValueSet`.
        /// Doesn't perform any calculation when both
        /// **x1** and **x2** are `Unrestricted`.
        let calc onlyMinIncrMax op (x1, x2) =
            //printfn "start valuerange calc"
            let calcMinMax min1 max1 min2 max2 =
                //printfn "start minmax calc"
                let getMin m =
                    let incl =
                        match m with
                        | Some v -> v |> Minimum.isIncl
                        | None -> false

                    m |> Option.bind (Minimum.toValueUnit >> Some), incl

                let getMax m =
                    let incl =
                        match m with
                        | Some v -> v |> Maximum.isIncl
                        | None -> false

                    m |> Option.bind (Maximum.toValueUnit >> Some), incl

                MinMaxCalculator.calcMinMax op (min1 |> getMin) (max1 |> getMax) (min2 |> getMin) (max2 |> getMax)

            match x1, x2 with
            | Unrestricted, Unrestricted -> unrestricted
            | ValSet s1, ValSet s2 ->
                let min1, max1 = x1 |> getMin, x1 |> getMax
                let min2, max2 = x2 |> getMin, x2 |> getMax

                let min, max =
                    calcMinMax min1 max1 min2 max2

                if not onlyMinIncrMax then
                    ValueSet.calc op s1 s2 |> ValSet
                else
                    match min, max with
                    | None, None -> unrestricted
                    | _ -> create onlyMinIncrMax min None max None

            // A set with an increment results in a new set of increment
            // Need to match all scenarios with a valueset and an increment
            | ValSet s, Incr i
            | Incr i, ValSet s

            | ValSet s, MinIncr (_, i)
            | MinIncr (_, i), ValSet s

            | ValSet s, IncrMax (i, _)
            | IncrMax (i, _), ValSet s

            | ValSet s, MinIncrMax (_, i, _)
            | MinIncrMax (_, i, _), ValSet s ->

                let min1, max1 = x1 |> getMin, x1 |> getMax
                let min2, max2 = x2 |> getMin, x2 |> getMax

                let min, max =
                    calcMinMax min1 max1 min2 max2

                // calculate a new increment based upon the valueset and an increment
                let incr1 = i |> Some

                let incr2 =
                    let (ValueSet s) = s
                    s |> Increment.create |> Some

                let incr = Increment.calcOpt op incr1 incr2

                match min, incr, max with
                | None, None, None -> unrestricted
                | _ -> create onlyMinIncrMax min incr max None

            // In any other case calculate min, incr and max
            | _ ->
                let min1, incr1, max1 =
                    x1 |> getMin, x1 |> getIncr, x1 |> getMax

                let min2, incr2, max2 =
                    x2 |> getMin, x2 |> getIncr, x2 |> getMax

                let min, max =
                    calcMinMax min1 max1 min2 max2

                // calculate a new increment based upon the incr1 and incr2
                let incr = Increment.calcOpt op incr1 incr2

                match min, incr, max with
                | None, None, None -> unrestricted
                | _ -> create onlyMinIncrMax min incr max None


        /// Checks whether a `ValueRange` vr1 is a subset of
        /// `ValueRange` vr2.
        let isSubSetOf vr2 vr1 =
            match vr1, vr2 with
            | ValSet s1, ValSet s2 -> s2 |> ValueSet.isSubset s1
            | _ -> false


        let toProperties vr =
            let unr = set []

            let nonZero = set []

            let fMin min = set [ min |> MinProp ]

            let fMax max = set [ max |> MaxProp ]

            let fMinMax (min, max) = set [ min |> MinProp; max |> MaxProp ]

            let fIncr incr = set [ incr |> IncrProp ]

            let fMinIncr (min, incr) =
                set [ min |> MinProp; incr |> IncrProp ]

            let fIncrMax (incr, max) =
                set [ incr |> IncrProp; max |> MaxProp ]

            let fMinIncrMax (min, incr, max) =
                set [
                    min |> MinProp
                    incr |> IncrProp
                    max |> MaxProp
                ]

            let fVs vs = set [ vs |> ValsProp ]

            vr
            |> apply unr nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fVs


        let diffWith vr1 vr2 =
            vr1
            |> toProperties
            |> Set.difference (vr2 |> toProperties)


        /// Set a `ValueRange` expr to a `ValueRange` y.
        /// So, the result is equal to or more restrictive than the original `y`.
        let applyExpr onlyMinIncrMax y expr =
            let appl s get set vr =
                //printfn $"{s}"
                match expr |> get with
                | Some m -> vr |> set m
                | None -> vr

            match expr with
            | Unrestricted -> y
            | ValSet vs -> y |> setValueSet vs
            | _ ->
                y
                |> appl "incr" getIncr (setIncr onlyMinIncrMax)
                |> appl "min" getMin (setMin onlyMinIncrMax)
                |> appl "max" getMax (setMax onlyMinIncrMax)


        module Operators =

            let inline (^*) vr1 vr2 = calc false (*) (vr1, vr2)

            let inline (^/) vr1 vr2 = calc false (/) (vr1, vr2)

            let inline (^+) vr1 vr2 = calc false (+) (vr1, vr2)

            let inline (^-) vr1 vr2 = calc false (-) (vr1, vr2)

            let inline (^<-) vr1 vr2 = applyExpr false vr1 vr2


            let inline (@*) vr1 vr2 = calc true (*) (vr1, vr2)

            let inline (@/) vr1 vr2 = calc true (/) (vr1, vr2)

            let inline (@+) vr1 vr2 = calc true (+) (vr1, vr2)

            let inline (@-) vr1 vr2 = calc true (-) (vr1, vr2)

            let inline (@<-) vr1 vr2 = applyExpr true vr1 vr2



    open Informedica.Utils.Lib.BCL
    open ValueRange.Operators

    module Minimum = ValueRange.Minimum
    module Maximum = ValueRange.Maximum


    /// Create a `Variable` and passes
    /// the result to **succ**
    let create succ n vs = { Name = n; Values = vs } |> succ


    /// Create a `Variable` and directly
    /// return the result.
    let createSucc = create id


    let empty n = Unrestricted |> createSucc n


    /// Helper create function to
    /// store the result of a `Variable`
    /// calculation before applying to
    /// the actual result `Variable`.
    let createRes =
        createSucc ("Result" |> Name.createExc)


    /// Apply **f** to `Variable` **var**.
    let apply f (var: Variable) = var |> f


    /// Helper function for type inference
    let get = apply id


    let toString exact ({ Name = n; Values = vs }: Variable) =
        vs
        |> ValueRange.toString exact
        |> sprintf "%s %s" (n |> Name.toString)


    /// Get the `Name` of a `Variable`.
    let getName v = (v |> get).Name


    /// Get the `ValueRange of a `Variable`.
    let getValueRange v = (v |> get).Values


    let contains v vr =
        vr |> getValueRange |> ValueRange.contains v


    /// Change `Name` to **n**.
    let setName n v : Variable = { v with Name = n }


    /// Apply a `ValueRange` **vr** to
    /// `Variable` **v**.
    let setValueRange onlyMinIncrMax var vr =
        let op =
            if onlyMinIncrMax then (@<-) else (^<-)

        try
            { var with
                Values = (var |> get).Values |> op <| vr
            }

        with
        | Exceptions.SolverException errs ->
            (var, vr)
            |> Exceptions.VariableCannotSetValueRange
            |> raiseExc errs
        | e ->
            printfn $"couldn't catch exeption:\{e}"
            raise e

    /// Set the values to a `ValueRange`
    /// that prevents zero or negative values.
    let setNonZeroOrNegative v =
        { v with
            Values = v.Values |> ValueRange.nonZeroNonNegative
        }


    /// Get the number of distinct values
    let count v =
        v |> getValueRange |> ValueRange.cardinality


    /// Checks whether **v1** and **v2** have the
    /// same `Name`
    let eqName v1 v2 = v1 |> getName = (v2 |> getName)


    let eqValues var1 var2 =
        var1 |> getValueRange = (var2 |> getValueRange)


    /// Checks whether a `Variable` **v** is solved,
    /// i.e. there is but one possible value left.
    let isSolved var =
        (var |> getValueRange |> ValueRange.isValueSet)
        && (var |> count = 1)


    /// Checks whether a `Variable` is *solvable*
    /// i.e. can be further restricted to one value
    /// (or no values at all)
    let isSolvable = isSolved >> not


    /// Checks whether there are no restrictions to
    /// possible values a `Variable` can contain
    let isUnrestricted =
        getValueRange >> ValueRange.isUnrestricted


    /// Apply the operator **op** to **v1** and **v2**
    /// return an intermediate *result* `Variable`.
    let calc op (v1, v2) =
        try
            (v1 |> getValueRange) |> op
            <| (v2 |> getValueRange)
            |> createRes
        with
        | Exceptions.SolverException errs ->
            (v1, op, v2)
            |> Exceptions.VariableCannotCalcVariables
            |> raiseExc errs
        | e ->
            printfn "unrecognized error with calc operation"
            printfn $"{v1} {v2}"
            raise e


    module Operators =

        let inline (^*) vr1 vr2 = calc (^*) (vr1, vr2)

        let inline (^/) vr1 vr2 = calc (^/) (vr1, vr2)

        let inline (^+) vr1 vr2 = calc (^+) (vr1, vr2)

        let inline (^-) vr1 vr2 = calc (^-) (vr1, vr2)

        let inline (^<-) vr1 vr2 =
            vr2 |> getValueRange |> setValueRange false vr1


        let inline (@*) vr1 vr2 = calc (@*) (vr1, vr2)

        let inline (@/) vr1 vr2 = calc (@/) (vr1, vr2)

        let inline (@+) vr1 vr2 = calc (@+) (vr1, vr2)

        let inline (@-) vr1 vr2 = calc (@-) (vr1, vr2)

        let inline (@<-) vr1 vr2 =
            vr2 |> getValueRange |> setValueRange true vr1


        /// Constant 1
        let one =
            ValueUnit.Operators.one
            |> ValueRange.createValSet
            |> createSucc (Name.createExc "one")


        /// Constant 2
        let two =
            ValueUnit.Operators.two
            |> ValueRange.createValSet
            |> createSucc (Name.createExc "two")


        /// Constant 3
        let three =
            ValueUnit.Operators.three
            |> ValueRange.createValSet
            |> createSucc (Name.createExc "three")

        /// Check whether the operator is subtraction
        let opIsSubtr op =
            (three |> op <| two) |> eqValues (three ^- two) // = 1

        /// Check whether the operator is addition
        let opIsAdd op =
            (three |> op <| two) |> eqValues (three ^+ two) // = 5

        /// Check whether the operator is multiplication
        let opIsMult op =
            (three |> op <| two) |> eqValues (three ^* two) // = 6

        /// Check whether the operator is divsion
        let opIsDiv op =
            (three |> op <| two) |> eqValues (three ^/ two) // = 3/2


        let toString op =
            match op with
            | _ when op |> opIsMult -> "x"
            | _ when op |> opIsDiv -> "/"
            | _ when op |> opIsAdd -> "+"
            | _ when op |> opIsSubtr -> "-"
            | _ -> ""


    /// Handle the creation of a `Variable` from a `Dto` and
    /// vice versa.
    module Dto =

        open Informedica.Utils.Lib

        module ValueSet = ValueRange.ValueSet
        module Increment = ValueRange.Increment

        /// The `Dto` representation of a `Variable`
        type Dto() =
            member val Name = "" with get, set
            member val isNonZeroNegative = false with get, set
            member val Min: ValueUnit.Dto.Dto option = None with get, set
            member val MinIncl = false with get, set
            member val Incr: ValueUnit.Dto.Dto option = None with get, set
            member val Max: ValueUnit.Dto.Dto option = None with get, set
            member val MaxIncl = false with get, set
            member val Vals: ValueUnit.Dto.Dto option = None with get, set


        let isUnr (dto: Dto) =
            dto.Min.IsNone
            && dto.Max.IsNone
            && dto.Incr.IsNone
            && dto.Vals.IsNone
            && not dto.isNonZeroNegative


        let dto () = Dto()

        let withName n =
            let dto = dto ()
            dto.Name <- n
            dto


        /// Create a `Variable` from a `Dto` and
        /// raise a `DtoException` if this fails.
        let fromDto (dto: Dto) =
            let succ = id

            let n =
                dto.Name
                |> Name.create succ (fun m -> m |> raiseExc [])

            let vs =
                dto.Vals
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map ValueSet.create
                )

            let min =
                dto.Min
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map (Minimum.create dto.MinIncl)
                )

            let max =
                dto.Max
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map (Maximum.create dto.MaxIncl)
                )

            let incr =
                dto.Incr
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map (Increment.create)
                )

            let vr =
                if dto.isNonZeroNegative then
                    NonZeroNoneNegative
                else
                    ValueRange.create true min incr max vs

            create succ n vr


        /// Return a `string` representation of a `Dto`
        let toString exact = fromDto >> toString exact


        /// Create a `Dto` from a `Variable`.
        let toDto (v: Variable) =
            let vuToDto = ValueUnit.Dto.toDtoDutchShort

            let dto = dto ()
            dto.Name <- v.Name |> Name.toString

            match v.Values with
            | Unrestricted -> dto
            | NonZeroNoneNegative ->
                dto.isNonZeroNegative <- true
                dto
            | _ ->
                let incr =
                    v.Values
                    |> ValueRange.getIncr
                    |> Option.map (Increment.toValueUnit >> vuToDto)

                let minincl =
                    match v.Values |> ValueRange.getMin with
                    | Some m -> m |> Minimum.isExcl |> not
                    | None -> false

                let maxincl =
                    match v.Values |> ValueRange.getMax with
                    | Some m -> m |> Maximum.isExcl |> not
                    | None -> false

                let min =
                    v.Values
                    |> ValueRange.getMin
                    |> Option.map (Minimum.toValueUnit >> vuToDto)

                let max =
                    v.Values
                    |> ValueRange.getMax
                    |> Option.map (Maximum.toValueUnit >> vuToDto)

                let vals =
                    v.Values
                    |> ValueRange.getValSet
                    |> Option.map (ValueSet.toSet >> vuToDto)

                dto.Incr <- incr
                dto.Min <- min
                dto.MinIncl <- minincl
                dto.Max <- max
                dto.MaxIncl <- maxincl
                dto.Vals <- vals

                dto