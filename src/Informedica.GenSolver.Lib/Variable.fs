namespace Informedica.GenSolver.Lib



module Variable =

    open System
    open MathNet.Numerics

    open Informedica.GenUnits.Lib

    let raiseExc errs m = m |> Exceptions.raiseExc None errs


    module Name =

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


            /// <summary>
            /// Create an `Increment` from a `ValueUnit`.
            /// </summary>
            /// <param name="vu">The ValueUnit</param>
            /// <returns>An `Increment`</returns>
            /// <exception cref="Exceptions.ValueRangeEmptyIncrementException">When the `ValueUnit` is empty</exception>
            /// <remarks>
            /// Filters out negative values and removes multiples.
            /// </remarks>
            let create vu =
                vu
                |> ValueUnit.filter ((<) 0N)
                |> ValueUnit.removeBigRationalMultiples
                |> fun vu ->
                    if vu |> ValueUnit.isEmpty |> not then
                        vu |> Increment
                    else
                        Exceptions.ValueRangeEmptyIncrement |> raiseExc []


            let apply f (Increment vu) = vu |> f


            /// <summary>
            /// Maps a function over the ValueUnit of `Increment`.
            /// </summary>
            /// <param name="f">The function to map</param>
            /// <returns>A new `Increment`</returns>
            let map f (Increment vu) = vu |> f |> create


            /// <summary>
            /// Convert an `Increment` to a `ValueUnit`.
            /// </summary>
            let toValueUnit (Increment vu) = vu


            /// <summary>
            /// Convert the Unit of an `Increment` to **u**.
            /// </summary>
            /// <param name="u">The unit to convert to</param>
            let convertToUnit u = map (ValueUnit.convertTo u)


            /// <summary>
            /// Get the smallest increment value
            /// </summary>
            /// <returns>
            /// The smallest increment value as a ValueUnit option.
            /// </returns>
            let minElement =
                toValueUnit >> ValueUnit.minValue


            /// <summary>
            /// Check if 'incr1' equals 'incr2'
            /// </summary>
            /// <param name="incr1">The first increment</param>
            /// <param name="incr2">The second increment</param>
            /// <example>
            /// <remarks>
            /// Uses ValueUnit equality, so 1000 milligram equals 1 gram
            /// </remarks>
            /// <code>
            /// let incr1 = Increment.create ( [| 1000N |] |> ValueUnit.create Units.Mass.milliGram)
            /// let incr2 = Increment.create ( [| 1N |] |> ValueUnit.create Units.Mass.gram)
            /// incr1 |> Increment.eqs incr2
            /// // returns true, i.e. 1000 milligram equals 1 gram
            /// </code>
            /// </example>
            let eqs incr1 incr2 =
                let vu1 = incr1 |> toValueUnit
                let vu2 = incr2 |> toValueUnit
                vu1 =? vu2


            /// <summary>
            /// Get the intersection of two `Increment`.
            /// </summary>
            /// <returns>
            /// The intersection of the two increments.
            /// </returns>
            /// <remarks>
            /// Returns the ValueUnit with Unit of the first increment.
            /// </remarks>
            /// <example>
            /// <code>
            /// let incr1 = Increment.create ( [| 2000N; 3000N |] |> ValueUnit.create Units.Mass.milliGram)
            /// let incr2 = Increment.create ( [| 3N; 5N |] |> ValueUnit.create Units.Mass.gram)
            /// incr1 |> Increment.intersect incr2
            /// // returns Increment (ValueUnit ([|3000N|], Mass (MilliGram 1N)))
            /// </code>
            /// </example>
            let intersect (Increment incr1) (Increment incr2) =
                incr1 |> ValueUnit.intersect incr2 |> create


            /// <summary>
            /// Calculates the resulting increment of a calculation with **op** in
            /// an equation: y = x1 **op** x2, where incr1 is the increment of x1,
            /// incr2 is the increment of x2 and y.incr is the resulting increment.
            /// </summary>
            /// <param name="op">The operator, can be mult, div, add, sub</param>
            /// <param name="incr1">Increment 1</param>
            /// <param name="incr2">Increment 2</param>
            /// <remarks>
            /// An increment can only be calculated when the operator is multiplication
            /// or division by the identity value of 1 times, or when the operator is
            /// addition and the increments are equal.
            /// </remarks>
            let calc op incr1 incr2 =
                match op with
                // y.incr = x1.incr * x2.incr
                | ValueUnit.Operators.Mult -> incr1 |> op <| incr2 |> create |> Some
                // TODO: really need to check this!!
                // when y = x1 + x2 then y.incr = x1.incr and x2.incr
                | ValueUnit.Operators.Add -> //| BigRational.Subtract ->
                    let vs1 = incr1 |> ValueUnit.getBaseValue
                    let vs2 = incr2 |> ValueUnit.getBaseValue

                    if vs1 |> Array.forall (fun br -> vs2 |> Array.exists ((=) br)) |> not then None
                    else
                        incr1
                        |> ValueUnit.setValue vs1
                        |> ValueUnit.toUnit
                        |> create
                        |> Some

                // incr cannot be calculated based on division except when dividing
                // by the identity value of 1 times
                | ValueUnit.Operators.Div when incr2 = ValueUnit.one Units.Count.times ->
                    incr1
                    |> create
                    |> Some
                | _ -> None


            /// <summary>
            /// Calculate an increment with
            /// **incr1** of x1 and **incr2** of x2
            /// in an equation: y = x1 **op** x2
            /// </summary>
            /// <returns>
            /// The resulting increment or `None` if the increment cannot be calculated.
            /// </returns>
            /// <remarks>
            /// An increment can only be calculated when the operator is multiplication
            /// or division by the identity value of 1 times, or when the operator is
            /// addition and the increments are equal.
            /// </remarks>
            let calcOpt op incr1 incr2 =
                match incr1, incr2 with
                | Some (Increment i1), Some (Increment i2) -> calc op i1 i2
                | _ -> None


            /// <summary>
            /// Get the increment as a list of BigRationals
            /// </summary>
            let toList (Increment incr) =
                incr |> ValueUnit.getValue |> Array.toList


            /// <summary>
            /// Check if the increment is empty, i.e. has no values
            /// </summary>
            let isEmpty (Increment incr) = incr |> ValueUnit.isEmpty


            /// <summary>
            /// Get the number of values in the increment
            /// </summary>
            let count (Increment incr) =
                incr |> ValueUnit.getValue |> Array.length


            /// <summary>
            /// Restrict an oldIncr with a newIncr.
            /// </summary>
            /// <param name="newIncr">The new increment</param>
            /// <param name="oldIncr">The old increment</param>
            /// <returns>
            /// The restricted increment.
            /// </returns>
            /// <example>
            /// <code>
            /// let oldIncr = Increment.create ( [| 3N; 5N |] |> ValueUnit.create Units.Mass.gram)
            /// let newIncr = Increment.create ( [| 2N; 3N |] |> ValueUnit.create Units.Mass.gram)
            /// Increment.restrict newIncr oldIncr
            /// // returns Increment (ValueUnit ([|3N|], Mass (Gram 1N)))
            /// </code>
            /// </example>
            let restrict newIncr oldIncr =
                let (Increment newVu) = newIncr
                let (Increment oldVu) = oldIncr

                if newVu =? oldVu then oldVu
                else
                    newVu
                    |> ValueUnit.filter (fun i1 ->
                        oldVu
                        |> ValueUnit.getBaseValue
                        |> Array.exists (fun i2 -> i1 |> BigRational.isMultiple i2)
                    )
                |> fun vu ->
                    if vu |> ValueUnit.isEmpty then oldIncr
                    else
                        vu
                        |> ValueUnit.convertTo (oldVu |> ValueUnit.getUnit)
                        |> create


            /// <summary>
            /// Get the string representation of an `Increment`.
            /// </summary>
            /// <param name="exact">Print exact or not</param>
            let toString exact (Increment incr) = $"{incr |> ValueUnit.toStr exact}"



        module Minimum =


            /// <summary>
            /// Create a `Minimum` that is
            /// either inclusive or exclusive.
            /// </summary>
            /// <param name="isIncl">Whether the `Minimum` is inclusive or exclusive</param>
            /// <param name="vu">The ValueUnit</param>
            /// <returns>A `Minimum`</returns>
            /// <exception cref="Exceptions.ValueRangeEmptyMinimumException">When the `ValueUnit` is empty of has more than one value</exception>
            let create isIncl vu =
                if vu |> ValueUnit.isSingleValue then

                    if isIncl then
                        vu |> MinIncl
                    else
                        vu |> MinExcl
                else
                    vu
                    |> Exceptions.ValueRangeMinShouldHaveOneValue
                    |> raiseExc []


            /// <summary>
            /// Apply fIncl or fExcl to the BigRational
            /// value of `Minimum`
            /// </summary>
            /// <param name="fIncl">The function to apply to an inclusive `Minimum`</param>
            /// <param name="fExcl">The function to apply to an exclusive `Minimum`</param>
            let apply fIncl fExcl =
                function
                | MinIncl m -> m |> fIncl
                | MinExcl m -> m |> fExcl


            /// <summary>
            /// Map fIncl or fExcl to the BigRational value of `Minimum`
            /// </summary>
            /// <param name="fIncl">The function to apply to an inclusive `Minimum`</param>
            /// <param name="fExcl">The function to apply to an exclusive `Minimum`</param>
            let map fIncl fExcl =
                apply (fIncl >> (create true)) (fExcl >> (create false))


            /// <summary>
            /// Convert the unit of a `Minimum` to **u**.
            /// </summary>
            /// <param name="u">The unit to set</param>
            let setUnit u = map (ValueUnit.convertTo u)


            /// <summary>
            /// Checks whether `Minimum` minLeft &gt; minRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Minimum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let min1 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let min2 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minGTmin min1 // returns false
            ///
            /// // make min2 exclusive
            /// let min2 = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minGTmin min1 // returns true!
            /// </code>
            /// </example>
            let minGTmin minRight minLeft =
                match minLeft, minRight with
                | MinIncl m2, MinIncl m1
                | MinExcl m2, MinExcl m1
                | MinIncl m2, MinExcl m1 -> m2 >? m1
                | MinExcl m2, MinIncl m1 -> m2 >=? m1

            /// <summary>
            /// Checks whether `Minimum` minLeft &lt;= minRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Minimum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let min1 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let min2 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minSTEmin min1 // returns true
            ///
            /// // make min2 exclusive
            /// let min2 = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minSTEmin min1 // returns false!
            /// </code>
            /// </example>
            let minSTEmin minRight minLeft = minLeft |> minGTmin minRight |> not


            /// <summary>
            /// Checks whether `Minimum` minLeft &gt;= minRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Minimum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let min1 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let min2 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minGTEmin min1 // returns true
            ///
            /// // make min1 exclusive
            /// let min1 = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minGTEmin min1 // returns false!
            /// </code>
            /// </example>
            let minGTEmin minRight minLeft = minRight = minLeft || minGTmin minRight minLeft


            /// <summary>
            /// Checks whether `Minimum` minLeft &lt; minRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Minimum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let min1 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let min2 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minSTmin min1 // returns false
            ///
            /// // make min1 exclusive
            /// let min1 = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// min2 |> Minimum.minSTmin min1 // returns true!
            /// </code>
            /// </example>
            let minSTmin minRight minLeft = minLeft |> minGTEmin minRight |> not


            /// <summary>
            /// Checks whether `Minimum` is exclusive.
            /// </summary>
            let isExcl =
                function
                | MinIncl _ -> false
                | MinExcl _ -> true


            /// Checks whether `Minimum` is inclusive.
            let isIncl = isExcl >> not


            /// Creates a `Minimum` from a `ValueUnit`.
            /// Returns `None` if an empty set.
            let minElement = ValueUnit.minValue >> Option.map MinIncl


            /// Convert a `Minimum` to a `ValueUnit`.
            let toValueUnit =
                function
                | MinIncl v
                | MinExcl v -> v


            /// <summary>
            /// Convert the Unit of a `Minimum` to the Unit of the second `Minimum`.
            /// </summary>
            /// <example>
            /// <code>
            /// let min1 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let min2 = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.milliGram)
            /// min1 |> Minimum.convertTo min2
            /// // returns Minimum (ValueUnit ([|3000N|], Mass (MilliGram 1N)))
            /// </code>
            /// </example>
            let convertTo min =
                let u =
                    min |> toValueUnit |> ValueUnit.getUnit

                map (ValueUnit.convertTo u) (ValueUnit.convertTo u)


            /// <summary>
            /// Checks whether the `Minimum` has a ZeroUnit.
            /// </summary>
            let hasZeroUnit =
                toValueUnit >> ValueUnit.hasZeroUnit


            /// Convert a `Minimum` to a `ValueUnit` and a `bool`
            /// that signifies inclusive or exclusive
            let toBoolValueUnit =
                apply (fun vu -> true, vu) (fun vu -> false, vu)


            /// <summary>
            /// Check if 'min1' equals 'min2'
            /// </summary>
            /// <param name="min1">The first Minimum</param>
            /// <param name="min2">The second Minimum</param>
            /// <remarks>
            /// Uses ValueUnit equality, so 1000 milligram equals 1 gram. Also
            /// takes into account whether the Minimum is inclusive or exclusive.
            /// </remarks>
            let eqs min1 min2 =
                let b1, vu1 = min1 |> toBoolValueUnit
                let b2, vu2 = min2 |> toBoolValueUnit
                (vu1 =? vu2) && (b1 = b2)


            /// <summary>
            /// Recalculate the minimum value of a `Minimum` as a multiple of **incr**.
            /// </summary>
            /// <param name="incr">The increment</param>
            /// <param name="min">The minimum</param>
            /// <remarks>
            /// The recalculated minimum is always inclusive as it has to
            /// be a multiple of the increment.
            /// </remarks>
            /// <example>
            /// <code>
            /// let incr = Increment.create ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let min = Minimum.create false ( [| 5N |] |> ValueUnit.create Units.Mass.gram)
            /// min |> Minimum.multipleOf incr
            /// // returns Minimum (ValueUnit ([|6N|], Mass (Gram 1N)))
            /// </code>
            /// </example>
            let multipleOf incr min =
                let incr = incr |> Increment.toValueUnit

                match min |> toBoolValueUnit with
                | true, vu -> vu |> ValueUnit.minInclMultipleOf incr
                | false, vu -> vu |> ValueUnit.minExclMultipleOf incr
                |> create true


            /// <summary>
            /// Check if either the numerator or denominator of the `Minimum`
            /// is too large, i.e. there is an 'overflow'.
            /// </summary>
            /// <remarks>
            /// In theory a Minimum can have a numerator or denominator of any size.
            /// However, in practice, this will lead to performance issues. Therefore
            /// we limit the size of the numerator and denominator to a maximum value.
            /// </remarks>
            let checkOverflow min =
                let xs =
                    min |> toValueUnit |> ValueUnit.numerator
                    |> Array.append (min |> toValueUnit |> ValueUnit.denominator)

                if xs |> Array.exists (fun x -> x > Constants.MAX_BIGINT) then
                    min
                    |> Exceptions.ValueRangeMinOverFlow
                    |> raiseExc []


            /// <summary>
            /// Make a `Minimum` non-zero and non-negative.
            /// </summary>
            let nonZeroNonNeg =
                let fIncl vu =
                    let vu =
                        vu |> ValueUnit.filter (fun br -> br > 0N)

                    if vu |> ValueUnit.isEmpty |> not then
                        vu |> create true
                    else
                        vu |> ValueUnit.setZeroOrPositive |> create false

                let fExcl vu =
                    let vu =
                        vu |> ValueUnit.filter (fun br -> br > 0N)

                    if vu |> ValueUnit.isEmpty |> not then
                        vu |> create false
                    else
                        vu |> ValueUnit.setZeroOrPositive |> create false

                apply fIncl fExcl


            /// <summary>
            /// Restrict an oldMin with a newMin. Effectively this means
            /// that the newMin is the new minimum if it is larger than the
            /// old minimum.
            /// </summary>
            /// <param name="newMin">The new minimum</param>
            /// <param name="oldMin">The old minimum</param>
            /// <returns>
            /// The restricted minimum. This is the old minimum if the new minimum
            /// is smaller than the old minimum, or the new minimum if the new minimum
            /// is larger than the old minimum.
            /// </returns>
            let restrict newMin oldMin =
                newMin |> checkOverflow

                let oldMin =
                    if oldMin |> hasZeroUnit |> not then oldMin
                    else
                        oldMin
                        |> convertTo newMin

                if newMin |> minGTmin oldMin then
                    newMin |> convertTo oldMin
                else
                    oldMin


            /// <summary>
            /// Get the string representation of a `Minimum`.
            /// </summary>
            /// <param name="exact">Print exact or not</param>
            /// <param name="min">The minimum</param>
            let toString exact min =
                let b, vu = min |> toBoolValueUnit

                $"""{if b then "[" else "<"}{vu |> ValueUnit.toStr exact}"""


            /// <summary>
            /// Get the markdown representation of a `Minimum`.
            /// </summary>
            /// <param name="prec">The precision</param>
            /// <param name="min">The minimum</param>
            let toMarkdown prec min =
                let b, vu = min |> toBoolValueUnit


                $"""{if b then "[" else "<"}{vu |> ValueUnit.toDelimitedString prec}"""



        module Maximum =


            /// <summary>
            /// Create a `Maximum` that is
            /// either inclusive or exclusive.
            /// </summary>
            /// <param name="isIncl">Whether the `Maximum` is inclusive or exclusive</param>
            /// <param name="vu">The ValueUnit</param>
            /// <returns>A `Maximum`</returns>
            /// <exception cref="Exceptions.ValueRangeEmptyMaximumException">When the `ValueUnit` is empty of has more than one value</exception>
            let create isIncl vu =
                if vu |> ValueUnit.isSingleValue then

                    if isIncl then
                        vu |> MaxIncl
                    else
                        vu |> MaxExcl
                else
                    vu
                    |> Exceptions.ValueRangeMaxShouldHaveOneValue
                    |> raiseExc []


            /// <summary>
            /// Apply fIncl or fExcl to the BigRational
            /// value of `Maximum`
            /// </summary>
            /// <param name="fIncl">The function to apply to an inclusive `Maximum`</param>
            /// <param name="fExcl">The function to apply to an exclusive `Maximum`</param>
            let apply fIncl fExcl =
                function
                | MaxIncl m -> m |> fIncl
                | MaxExcl m -> m |> fExcl


            /// <summary>
            /// Map fIncl or fExcl to the BigRational value of `Maximum`
            /// </summary>
            /// <param name="fIncl">The function to apply to an inclusive `Maximum`</param>
            /// <param name="fExcl">The function to apply to an exclusive `Maximum`</param>
            let map fIncl fExcl =
                apply (fIncl >> (create true)) (fExcl >> (create false))


            /// <summary>
            /// Convert the unit of a `Maximum` to **u**.
            /// </summary>
            /// <param name="u">The unit to set</param>
            let setUnit u = map (ValueUnit.convertTo u)


            /// <summary>
            /// Checks whether `Maximum` maxLeft &gt; maxRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Maximum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let max1 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let max2 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxGTmax max1 // returns false
            ///
            /// // make max1 exclusive
            /// let max1 = Maximum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxGTmax max1 // returns true!
            /// </code>
            /// </example>
            let maxGTmax maxRight maxLeft =
                match maxLeft, maxRight with
                | MaxIncl m2, MaxIncl m1
                | MaxExcl m2, MaxExcl m1
                | MaxExcl m2, MaxIncl m1 -> m2 >? m1
                | MaxIncl m2, MaxExcl m1 -> m2 >=? m1


            /// <summary>
            /// Checks whether `Maximum` maxLeft &lt;= maxRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Maximum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let max1 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let max2 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxSTEmax max1 // returns true
            ///
            /// // make max1 exclusive
            /// let max1 = Maximum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxSTEmax max1 // returns false!
            /// </code>
            /// </example>
            let maxSTEmax maxRight maxLeft = maxLeft |> maxGTmax maxRight |> not


            /// <summary>
            /// Checks whether `Maximum` maxLeft &gt;= maxRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Maximum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let max1 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let max2 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxGTEmax max1 // returns true
            ///
            /// // make max2 exclusive
            /// let max2 = Maximum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxGTEmax max1 // returns false!
            /// </code>
            /// </example>
            let maxGTEmax maxRight maxLeft = maxRight = maxLeft || maxGTmax maxRight maxLeft


            /// <summary>
            /// Checks whether `Maximum` maxLeft &lt; maxRight
            /// </summary>
            /// <remarks>
            /// Note that the fact that a Maximum is inclusive or exclusive
            /// must be taken into account.
            /// </remarks>
            /// <example>
            /// <code>
            /// let max1 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let max2 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxSTmax max1 // returns false
            ///
            /// // make max2 exclusive
            /// let max2 = Maximum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// max2 |> Maximum.maxSTmax max1 // returns true!
            /// </code>
            /// </example>
            let maxSTmax maxRight maxLeft = maxLeft |> maxGTEmax maxRight |> not


            /// Checks whether `Maximum` is exclusive.
            let isExcl =
                function
                | MaxIncl _ -> false
                | MaxExcl _ -> true


            /// Checks whether `Maximum` is inclusive.
            let isIncl = isExcl >> not


            /// Creates a `Maximum` from a `ValueUnit`.
            /// Returns `None` if an empty set.
            let maxElement = ValueUnit.maxValue >> Option.map MaxIncl


            /// Convert a `Maximum` to a `ValueUnit`.
            let toValueUnit =
                function
                | MaxIncl v
                | MaxExcl v -> v


            /// <summary>
            /// Convert the Unit of a `Maximum` to the Unit of the second `Maximum`.
            /// </summary>
            /// <example>
            /// <code>
            /// let max1 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
            /// let max2 = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.milliGram)
            /// max1 |> Maximum.convertTo max2
            /// // returns Maximum (ValueUnit ([|3000N|], Mass (MilliGram 1N)))
            /// </code>
            /// </example>
            let convertTo max =
                let u =
                    max |> toValueUnit |> ValueUnit.getUnit

                map (ValueUnit.convertTo u) (ValueUnit.convertTo u)


            /// <summary>
            /// Checks whether the `Maximum` has a ZeroUnit.
            /// </summary>
            let hasZeroUnit =
                toValueUnit >> ValueUnit.hasZeroUnit


            /// Convert a `Maximum` to a `ValueUnit` and a `bool`
            /// that signifies inclusive or exclusive
            let toBoolValueUnit =
                apply (fun m -> true, m) (fun m -> false, m)


            /// <summary>
            /// Check if 'max1' equals 'max2'
            /// </summary>
            /// <param name="max1">The first Maximum</param>
            /// <param name="max2">The second Maximum</param>
            /// <remarks>
            /// Uses ValueUnit equality, so 1000 milligram equals 1 gram. Also
            /// takes into account whether the Maximum is inclusive or exclusive.
            /// </remarks>
            let eqs max1 max2 =
                let b1, vu1 = max1 |> toBoolValueUnit
                let b2, vu2 = max2 |> toBoolValueUnit
                (vu1 =? vu2) && (b1 = b2)


            /// <summary>
            /// Recalculate the maximum value of a `Maximum` as a multiple of **incr**.
            /// </summary>
            /// <param name="incr">The increment</param>
            /// <param name="max">The maximum</param>
            /// <remarks>
            /// The recalculated maximum is always inclusive as it has to
            /// be a multiple of the increment.
            /// </remarks>
            /// <example>
            /// <code>
            /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
            /// let max = Maximum.create false ( [| 5N |] |> ValueUnit.create Units.Mass.gram)
            /// max |> Maximum.multipleOf incr
            /// // returns Maximum (ValueUnit ([|4N|], Mass (Gram 1N)))
            /// </code>
            /// </example>
            let multipleOf incr max =
                let incr = incr |> Increment.toValueUnit

                match max |> toBoolValueUnit with
                | true, vu -> vu |> ValueUnit.maxInclMultipleOf incr
                | false, vu -> vu |> ValueUnit.maxExclMultipleOf incr
                |> create true


            /// <summary>
            /// Check if either the numerator or denominator of the `Maximum`
            /// is too large, i.e. there is an 'overflow'.
            /// </summary>
            /// <remarks>
            /// In theory a Maximum can have a numerator or denominator of any size.
            /// However, in practice, this will lead to performance issues. Therefore
            /// we limit the size of the numerator and denominator to a maximum value.
            /// </remarks>
            let checkOverflow max =
                let xs =
                    max |> toValueUnit |> ValueUnit.numerator
                    |> Array.append (max |> toValueUnit |> ValueUnit.denominator)

                if xs |> Array.exists (fun x -> x > Constants.MAX_BIGINT) then
                    max
                    |> Exceptions.ValueRangeMaxOverFlow
                    |> raiseExc []


            /// <summary>
            /// Make a `Maximum` non-zero and non-negative.
            /// </summary>
            let nonZeroNonNeg max =
                max
                |> toValueUnit
                |> ValueUnit.getUnit
                |> ValueUnit.withSingleValue 0N
                |> Minimum.create false,
                max
                |> map (ValueUnit.filter (fun br -> br > 0N)) (ValueUnit.filter (fun br -> br > 0N))


            /// <summary>
            /// Restrict an oldMax with a newMax. Effectively this means
            /// that the newMax is the new maximum if it is smaller than the
            /// old maximum.
            /// </summary>
            /// <param name="newMax">The new maximum</param>
            /// <param name="oldMax">The old maximum</param>
            /// <returns>
            /// The restricted maximum. This is the old maximum if the new maximum
            /// is greater than the old maximum, or the new maximum if the new maximum
            /// is smaller than the old maximum.
            /// </returns>
            let restrict newMax oldMax =
                newMax |> checkOverflow

                if newMax |> maxSTmax oldMax then
                    newMax |> convertTo oldMax
                else
                    oldMax


            /// <summary>
            /// Get the string representation of a `Maximum`.
            /// </summary>
            /// <param name="exact">Print exact or not</param>
            /// <param name="max">The maximum</param>
            let toString exact max =
                let b, vu = max |> toBoolValueUnit

                $"""{vu |> ValueUnit.toStr exact}{if b then "]" else ">"}"""


            /// <summary>
            /// Get the markdown representation of a `Maximum`.
            /// </summary>
            /// <param name="prec">The precision</param>
            /// <param name="max">The maximum</param>
            let toMarkdown prec max =
                let b, vu = max |> toBoolValueUnit

                $"""{vu |> ValueUnit.toDelimitedString prec}{if b then "]" else ">"}"""



        module ValueSet =


            /// <summary>
            /// Create a `ValueSet` from a `ValueUnit`.
            /// </summary>
            /// <param name="vu">The ValueUnit</param>
            /// <returns>A `ValueSet`</returns>
            /// <exception cref="Exceptions.ValueRangeEmptyValueSetException">When the `ValueSet` is empty</exception>
            /// <exception cref="Exceptions.ValueSetOverflow">When the `ValueSet` has too many values</exception>
            let create vu =
                if vu |> ValueUnit.isEmpty then
                    Exceptions.ValueRangeEmptyValueSet |> raiseExc []

                else
                    if (vu |> ValueUnit.getValue).Length > (Constants.MAX_CALC_COUNT * Constants.MAX_CALC_COUNT) then
                        (vu |> ValueUnit.getValue).Length
                        |> Exceptions.ValueSetOverflow
                        |> raiseExc []
                    else
                        vu
                        |> ValueSet


            /// <summary>
            /// Get the `ValueUnit` of a `ValueSet`.
            /// </summary>
            let toValueUnit (ValueSet vu) = vu


            let apply f (ValueSet vu) = vu |> f


            /// <summary>
            /// Map f to the `ValueUnit` of a `ValueSet`.
            /// </summary>
            /// <param name="f">The mapping function</param>
            let map f (ValueSet vu) = vu |> f |> create


            /// <summary>
            /// Prune the `ValueUnit` of a `ValueSet`. This means that
            /// the number of values is reduced to a constant **PRUNE**.
            /// </summary>
            /// <remarks>
            /// The minimum and maximum values are always kept.
            /// </remarks>
            let prune n =
                fun vu ->
                    let v =
                        vu
                        |> ValueUnit.getValue
                        |> Array.prune n //Constants.PRUNE
                    vu
                    |> ValueUnit.setValue v
                |> map


            /// <summary>
            /// Convert the unit of a `ValueSet` to **u**.
            /// </summary>
            let setUnit u = map (ValueUnit.convertTo u)


            /// <summary>
            /// Get the minimum value of a `ValueSet`.
            /// </summary>
            let getMin (ValueSet vu) = vu |> Minimum.minElement


            /// <summary>
            /// Get the maximum value of a `ValueSet`.
            /// </summary>
            let getMax (ValueSet vu) = vu |> Maximum.maxElement


            /// <summary>
            /// Count the number of values in a `ValueSet`.
            /// </summary>
            let count (ValueSet vu) =
                vu |> ValueUnit.getValue |> Array.length


            /// <summary>
            /// Check if a `ValueSet` is empty.
            /// </summary>
            let isEmpty (ValueSet vu) = vu |> ValueUnit.isEmpty


            /// <summary>
            /// Check if a `ValueSet` contains a value.
            /// </summary>
            /// <param name="v">The value to check</param>
            /// <remarks>
            /// Uses ValueUnit equality, so 1000 milligram equals 1 gram.
            /// </remarks>
            let contains v (ValueSet vu) = vu |> ValueUnit.containsValue v


            /// <summary>
            /// Calculate the intersection of two `ValueSet`s.
            /// </summary>
            /// <remarks>
            /// Returns a 'ValueSet' that contains the values that are
            /// in both 'ValueSet's and that has the unit of the first 'ValueSet'.
            /// </remarks>
            let intersect (ValueSet vu1) (ValueSet vu2) =
                vu1
                |> ValueUnit.intersect vu2
                |> ValueUnit.convertTo (vu1 |> ValueUnit.getUnit)
                |> create


            /// <summary>
            /// Check if the first `ValueSet` is a subset of the second `ValueSet`.
            /// </summary>
            let isSubsetOf vs1 vs2 =
                let ValueSet vu1, ValueSet vu2 = vs1, vs2
                ValueUnit.isSubset vu1 vu2


            /// <summary>
            /// Check if the first `ValueSet` is equal to the second `ValueSet`.
            /// </summary>
            /// <remarks>
            /// Uses ValueUnit equality, so 1000 milligram equals 1 gram.
            /// </remarks>
            let eqs (ValueSet vu1) (ValueSet vu2) = vu1 =? vu2


            /// <summary>
            /// Filters out the values that are zero or negative.
            /// </summary>
            /// <remarks>
            /// Will throw an exception if the resulting `ValueSet` is empty.
            /// </remarks>
            let nonZeroNonNeg =
                map (ValueUnit.filterValues (fun br -> br > 0N))


            /// <summary>
            /// Apply a binary operator to two `ValueSet`s.
            /// </summary>
            /// <param name="op">The operator to apply</param>
            /// <param name="vs1">The first `ValueSet`</param>
            /// <param name="vs2">The second `ValueSet`</param>
            let calc op vs1 vs2 =
                let ValueSet vu1, ValueSet vu2 = vs1, vs2
                vu1 |> op <| vu2 |>
                create


            /// <summary>
            /// Create a `ValueSet` from an `Increment`, `Minimum` and a `Maximum`.
            /// </summary>
            /// <param name="min">The minimum</param>
            /// <param name="incr">The increment</param>
            /// <param name="max">The maximum</param>
            /// <remarks>
            /// If the increment is the result of a calculation, it is possible
            /// that the set will contain more values that would be obtained by the
            /// calculation. If you multiply a `ValueSet` [3;6] with a `ValueSet`
            /// [2;4;6] you get [6;12;18;24;36]. However, if you create a value set
            /// using a minimum of 6, an increment of 6 and a maximum of 36, you get
            /// [6;12;18;24;30;36]. However 30 cannot be multiplying [3;6] with [2;4;6].
            /// </remarks>
            let minIncrMaxToValueSet min incr max =
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
                            for mi in min do
                                for ma in max do
                                    BigRational.minIncrMaxToSeq mi incr ma
                                    |> Seq.toArray
                        |]
                        |> Array.collect id
                    )
                    |> ValueUnit.toUnit
                    |> create


            /// <summary>
            /// Get a string representation of a `ValueSet`.
            /// </summary>
            /// <param name="exact">Whether values should be printed 'exact'</param>
            /// <param name="vs">The `ValueSet`</param>
            let toString exact vs =
                let (ValueSet vu) = vs
                let count =
                    ValueUnit.getValue >> Array.length

                if vu |> count <= 10 then
                    $"""[{vu |> ValueUnit.toStr exact}]"""
                else
                    let first3 = vu |> ValueUnit.takeFirst 3
                    let last3 = vu |> ValueUnit.takeLast 3
                    $"[{first3 |> ValueUnit.toStr exact} .. {last3 |> ValueUnit.toStr exact}]"


            /// <summary>
            /// Get a markdown representation of a `ValueSet`.
            /// </summary>
            /// <param name="prec">The precision to print values with</param>
            /// <param name="vs">The `ValueSet`</param>
            let toMarkdown prec vs =
                let (ValueSet vu) = vs
                let count =
                    ValueUnit.getValue >> Array.length

                if vu |> count <= 10 then
                    $"""[{vu |> ValueUnit.toDelimitedString prec}]"""
                else
                    let first3 = vu |> ValueUnit.takeFirst 3
                    let last3 = vu |> ValueUnit.takeLast 3
                    $"[{first3 |> ValueUnit.toDelimitedString prec} .. {last3 |> ValueUnit.toDelimitedString prec}]"



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


        // === START ValueRange ===

        /// <summary>
        /// Map a function over a `ValueRange`.
        /// </summary>
        /// <param name="fMin">The function to apply to a `Minimum`</param>
        /// <param name="fMax">The function to apply to a `Maximum`</param>
        /// <param name="fMinMax">The function to apply to a `MinMax`</param>
        /// <param name="fIncr">The function to apply to an `Increment`</param>
        /// <param name="fMinIncr">The function to apply to a `MinIncr`</param>
        /// <param name="fIncrMax">The function to apply to an `IncrMax`</param>
        /// <param name="fMinIncrMax">The function to apply to a `MinIncrMax`</param>
        /// <param name="fValueSet">The function to apply to a `ValueSet`</param>
        /// <param name="vr">The `ValueRange`</param>
        /// <returns>The mapped `ValueRange`</returns>
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


        /// <summary>
        /// Apply a function to a `ValueRange`.
        /// </summary>
        /// <param name="unr">Constant for `Unrestricted`</param>
        /// <param name="nonZero">Constant for `NonZeroNoneNegative`</param>
        /// <param name="fMin">The function to apply to a `Minimum`</param>
        /// <param name="fMax">The function to apply to a `Maximum`</param>
        /// <param name="fMinMax">The function to apply to a `MinMax`</param>
        /// <param name="fIncr">The function to apply to an `Increment`</param>
        /// <param name="fMinIncr">The function to apply to a `MinIncr`</param>
        /// <param name="fIncrMax">The function to apply to an `IncrMax`</param>
        /// <param name="fMinIncrMax">The function to apply to a `MinIncrMax`</param>
        /// <param name="fValueSet">The function to apply to a `ValueSet`</param>
        /// <returns>The result of applying the function to the `ValueRange`</returns>
        let apply unr nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet =
            function
            | Unrestricted -> unr
            | NonZeroNoneNegative -> nonZero
            | Min min -> min |> fMin
            | Max max -> max |> fMax
            | MinMax (min, max) -> (min, max) |> fMinMax
            | Incr incr -> incr |> fIncr
            | MinIncr (min, incr) -> (min, incr) |> fMinIncr
            | IncrMax (incr, max) -> (incr, max) |> fIncrMax
            | MinIncrMax (min, incr, max) -> (min, incr, max) |> fMinIncrMax
            | ValSet vs -> vs |> fValueSet


        /// <summary>
        /// Prune, limit the number of values, in a `ValueRange`.
        /// </summary>
        /// <remarks>
        /// Only a `ValueSet` can be pruned. The other `ValueRange`s
        /// are left untouched.
        /// </remarks>
        let prune n =
            map id id id id id id id (ValueSet.prune n)


        /// <summary>
        /// Map a function to the ValueUnit of a `ValueRange`.
        /// </summary>
        /// <param name="f">The function to apply to the `ValueUnit`</param>
        /// <param name="vr">The `ValueRange`</param>
        /// <returns>The mapped `ValueRange`</returns>
        let mapValueUnit f vr =
            vr
            |> map
                (Minimum.map f f)
                (Maximum.map f f)
                (fun (min, max) -> (min |> Minimum.map f f, max |> Maximum.map f f))
                (Increment.map f)
                (fun (min, incr) -> (min |> Minimum.map f f, incr |> Increment.map f))
                (fun (incr, max) -> (incr |> Increment.map f, max |> Maximum.map f f))
                (fun (min, incr, max) -> (min |> Minimum.map f f, incr |> Increment.map f, max |> Maximum.map f f))
                (ValueSet.map f)


        /// <summary>
        /// Map a function to the ValueUnit of a `ValueRange`.
        /// </summary>
        /// <param name="f">The function to apply to the `ValueUnit`</param>
        /// <param name="vr">The `ValueRange`</param>
        /// <returns>The mapped `ValueRange`</returns>
        let applyValueUnit f vr =
            vr
            |> apply
                None
                None
                (Minimum.apply f f)
                (Maximum.apply f f)
                (fun (min, _) -> min |> Minimum.apply f f)
                (Increment.apply f)
                (fun (min, _) -> min |> Minimum.apply f f)
                (fun (incr, _) -> incr |> Increment.apply f)
                (fun (min, _, _) -> min |> Minimum.apply f f)
                (ValueSet.apply f)


        /// <summary>
        /// Convert the unit of a `ValueRange` to **u**.
        /// </summary>
        /// <param name="u">The unit to convert to</param>
        let setUnit u = mapValueUnit (ValueUnit.convertTo u)


        let getUnit = applyValueUnit (ValueUnit.getUnit >> Some)


        /// <summary>
        /// Count the number of values in a `ValueRange`.
        /// Returns 0 if no count is possible.
        /// </summary>
        /// <remarks>
        /// Only when value range is a ValueSet there can be a count.
        /// </remarks>
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


        /// Checks whether a `ValueRange` is `Incr`
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


        /// Checks whether a `ValueRange` is `MinIncr`
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


        /// Checks whether a `ValueRange` is `IncrMax`
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


        /// Checks whether a `ValueRange` is `MinIncrMax`
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


        /// Checks whether a `ValueRange` is `ValueSet`
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


        /// <summary>
        /// Checks whether a `BigRational` is between an optional
        /// **min** and an optional **max**
        /// </summary>
        /// <param name="min">The optional minimum</param>
        /// <param name="max">The optional maximum</param>
        /// <param name="br">The `BigRational` to check</param>
        /// <returns>True if **br** is between **min** and **max**</returns>
        /// <example>
        /// <code>
        /// let min = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let max = Maximum.create true ( [| 5N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let br = 4N
        /// br |> isBetweenMinMax min max // returns true
        /// let br = 6N
        /// br |> isBetweenMinMax min max // returns false
        /// let max = None
        /// br |> isBetweenMinMax min max // returns true
        /// </code>
        /// </example>
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


        /// <summary>
        /// Checks whether a `BigRational` is a multiple of an optional
        /// **incr**.
        /// </summary>
        /// <param name="incrOpt"></param>
        /// <param name="br"></param>
        /// <example>
        /// <code>
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let br = 4N
        /// br |> isMultipleOfIncr incr // returns true
        /// let br = 5N
        /// br |> isMultipleOfIncr incr // returns false
        /// let incr = None
        /// br |> isMultipleOfIncr incr // returns true
        /// </code>
        /// </example>
        let isMultipleOfIncr incrOpt br =
            let isDiv i = br |> BigRational.isMultiple i

            match incrOpt with
            | None -> true
            | Some (Increment incr) -> incr |> ValueUnit.getBaseValue |> Seq.exists isDiv


        /// <summary>
        /// Filter a set of `ValueSet` according
        /// to **min** **max** and incr constraints
        /// </summary>
        /// <param name="minOpt">The optional minimum</param>
        /// <param name="incrOpt">The optional increment</param>
        /// <param name="maxOpt">The optional maximum</param>
        /// <returns>
        /// A `ValueSet` that contains the values that are between **min** and **max**
        /// and that are a multiple of **incr**.
        /// </returns>
        /// <example>
        /// <code>
        /// let vs = [| 1N; 2N; 3N; 4N; 5N; 6N; 7N; 8N; 9N; 10N |] |> ValueUnit.create Units.Mass.gram |> ValueSet.create
        /// vs |> filter (Some (Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram))) None None
        /// // returns ValueSet (ValueUnit ([|3N; 4N; 5N; 6N; 7N; 8N; 9N; 10N|], Mass (Gram 1N)))
        /// vs |> filter None None None // will return the same ValueSet
        /// </code>
        /// </example>
        let filter minOpt incrOpt maxOpt (ValueSet vs) =
            try
                vs
                |> ValueUnit.filter (fun v ->
                    v |> isBetweenMinMax minOpt maxOpt
                    && v |> isMultipleOfIncr incrOpt
                )
                |> ValueSet.create
            with
            | e ->
                printfn $"filter with {minOpt}, {incrOpt}, {maxOpt} and {vs} gives empty set"
                raise e


        /// <summary>
        /// Check if max = min.
        /// </summary>
        /// <param name="max">The maximum</param>
        /// <param name="min">The minimum</param>
        /// <remarks>
        /// Min can only be equal to max if both are inclusive!
        /// </remarks>
        let minEQmax max min =
            match min, max with
            | Minimum.MinIncl min, Maximum.MaxIncl max -> min =? max
            | _ -> false


        /// <summary>
        /// Checks whether `Minimum` **min** > `Maximum` **max**.
        /// Note that inclusivity or exclusivity of a minimum and maximum must be
        /// accounted for.
        /// </summary>
        /// <param name="max">The maximum</param>
        /// <param name="min">The minimum</param>
        /// <example>
        /// <code>
        /// let min = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// let max = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// min |> minGTmax max // returns true
        /// </code>
        /// </example>
        let minGTmax max min =
            match min, max with
            | Minimum.MinIncl min, Maximum.MaxIncl max -> min >? max
            | Minimum.MinExcl min, Maximum.MaxIncl max
            | Minimum.MinExcl min, Maximum.MaxExcl max
            | Minimum.MinIncl min, Maximum.MaxExcl max -> min >=? max


        /// <summary>
        /// Checks whether `Minimum` **min** &lt;= `Maximum` **max**
        /// </summary>
        /// <param name="max">The maximum</param>
        /// <param name="min">The minimum</param>
        /// <remarks>
        /// Note that inclusivity or exclusivity of a minimum and maximum must be
        /// accounted for.
        /// </remarks>
        /// <example>
        /// <code>
        /// let min = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// let max = Maximum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// min |> minSTEmax max // returns false
        /// </code>
        /// </example>
        let minSTEmax max min = min |> minGTmax max |> not


        /// <summary>
        /// Create a `Minimum` that is a multiple of **incr**.
        /// </summary>
        /// <param name="incr">The increment</param>
        /// <param name="min">The minimum</param>
        /// <example>
        /// <code>
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// let min = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// min |> minMultipleOf incr // returns Minimum.MinIncl (ValueUnit ([|4N|], Mass (Gram 1N)))
        /// </code>
        /// </example>
        /// <remarks>
        /// Note that the resulting min is always inclusive and greater than
        /// or equal to the original min.
        /// </remarks>
        let minMultipleOf incr min = min |> Minimum.multipleOf incr


        /// <summary>
        /// Create a `Maximum` that is a multiple of **incr**.
        /// </summary>
        /// <param name="incr">The increment</param>
        /// <param name="max">The maximum</param>
        /// <example>
        /// <code>
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// let max = Maximum.create false ( [| 5N |] |> ValueUnit.create Units.Mass.gram)
        /// max |> maxMultipleOf incr // returns Maximum.MaxIncl (ValueUnit ([|4N|], Mass (Gram 1N)))
        /// </code>
        /// </example>
        /// <remarks>
        /// Note that the resulting max is always inclusive and smaller than
        /// or equal to the original max.
        /// </remarks>
        let maxMultipleOf incr max = max |> Maximum.multipleOf incr


        /// An `Unrestricted` `ValueRange`.
        let unrestricted = Unrestricted


        /// A `ValueRange` that contains only non-zero, positive values.
        let nonZeroOrNegative = NonZeroNoneNegative


        /// <summary>
        /// Create a `MinMax` `ValueRange`. If **min** > **max** raises
        /// an `MinLargerThanMax` exception. If min equals max, a `ValueSet` with
        /// value min (= max).
        /// </summary>
        /// <param name="min">The minimum</param>
        /// <param name="max">The maximum</param>
        /// <returns>A `MinMax` `ValueRange`</returns>
        /// <exception cref="Exceptions.ValueRangeMinLargerThanMaxException">When **min** > **max**</exception>
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


        /// <summary>
        /// Create a `MinIncr` `ValueRange`.
        /// </summary>
        /// <param name="min">The minimum</param>
        /// <param name="incr">The increment</param>
        /// <returns>A `MinIncr` `ValueRange`</returns>
        /// <example>
        /// <code>
        /// let min = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// minIncrToValueRange min incr
        /// // returns MinIncr (Minimum.MinIncl (ValueUnit ([|4N|], Mass (Gram 1N))), Increment (ValueUnit ([|2N|], Mass (Gram 1N))))
        /// </code>
        /// </example>
        /// <remarks>
        /// Note that the resulting min is always inclusive and greater than
        /// or equal to the original min and is a multiple of **incr**.
        /// </remarks>
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


        /// <summary>
        /// Create an `IncrMax` `ValueRange`.
        /// </summary>
        /// <param name="incr">The increment</param>
        /// <param name="max">The maximum</param>
        /// <returns>An `IncrMax` `ValueRange`</returns>
        /// <example>
        /// <code>
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// let max = Maximum.create false ( [| 5N |] |> ValueUnit.create Units.Mass.gram)
        /// incrMaxToValueRange incr max
        /// // returns IncrMax (Increment (ValueUnit ([|2N|], Mass (Gram 1N))), Maximum.MaxIncl (ValueUnit ([|4N|], Mass (Gram 1N))))
        /// </code>
        /// </example>
        /// <remarks>
        /// Note that the resulting max is always inclusive and smaller than
        /// or equal to the original max and is a multiple of **incr**.
        /// </remarks>
        let incrMaxToValueRange incr max =
            (incr, max |> maxMultipleOf incr) |> IncrMax


        /// <summary>
        /// Create a `MinIncrMax` `ValueRange`.
        /// </summary>
        /// <param name="min">The minimum</param>
        /// <param name="incr">The increment</param>
        /// <param name="max">The maximum</param>
        /// <exception cref="Exceptions.ValueRangeMinLargerThanMaxException">When **min** > **max**</exception>
        let minIncrMaxToValueRange min incr max =
            let min = min |> minMultipleOf incr
            let max = max |> maxMultipleOf incr

            if min |> minGTmax max then
                (min, max)
                |> Exceptions.Message.ValueRangeMinLargerThanMax
                |> raiseExc []
            else if (min |> minEQmax max) then
                ValueSet.minIncrMaxToValueSet min incr max
                |> ValSet
            else
                MinIncrMax(min, incr, max)


        /// Create a `Minimum` `Range` that is
        /// either inclusive or exclusive.
        let createMin isIncl m = m |> Minimum.create isIncl |> Min


        /// Create a `Maximum` `Range` that is
        /// either inclusive or exclusive.
        let createMax isIncl m = m |> Maximum.create isIncl |> Max


        /// <summary>
        /// Create a `Range` with a `Minimum` and a `Maximum`.
        /// </summary>
        /// <param name="min">The minimum</param>
        /// <param name="minIncl">Whether the minimum is inclusive</param>
        /// <param name="max">The maximum</param>
        /// <param name="maxIncl">Whether the maximum is inclusive</param>
        let createMinMax min minIncl max maxIncl =
            let min = min |> Minimum.create minIncl
            let max = max |> Maximum.create maxIncl

            minMaxToValueRange min max


        /// Create a `ValueRange` with a `Minimum`, `Increment` and a `Maximum`.
        let createIncr = Increment.create >> Incr


        /// Create a `ValueSet` `ValueRange`.
        let createValSet brs = brs |> ValueSet.create |> ValSet


        /// Create a `MinIncr` `ValueRange`.
        let createMinIncr min minIncl incr =
            incr
            |> Increment.create
            |> minIncrToValueRange (Minimum.create min minIncl)


        /// Create a `IncrMax` `ValueRange`.
        let createIncrMax incr max maxIncl =
            max
            |> Maximum.create maxIncl
            |> incrMaxToValueRange (incr |> Increment.create)


        /// <summary>
        /// Create a ValueRange depending on the values of **min**, **incr** and **max**.
        /// </summary>
        /// <param name="min">Optional Minimum</param>
        /// <param name="incr">Optional Increment</param>
        /// <param name="max">Optional Maximum</param>
        /// <param name="vs">Optional ValueSet</param>
        /// <returns>A `ValueRange`</returns>
        /// <example>
        /// <code>
        /// let min = Minimum.create ( [| 3N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let max = Maximum.create ( [| 5N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let incr = None
        /// let vs = None
        /// create true min incr max vs
        /// // returns MinMax (Minimum.MinIncl (ValueUnit ([|3N|], Mass (Gram 1N))), Maximum.MaxIncl (ValueUnit ([|5N|], Mass (Gram 1N))))
        /// </code>
        /// </example>
        let create min incr max vs =
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
                | Some min, Some incr, Some max -> minIncrMaxToValueRange min incr max

            | Some vs ->
                vs
                |> filter min incr max
                |> ValSet


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


        /// <summary>
        /// Checks whether a value is in a `ValueRange`.
        /// </summary>
        /// <param name="vu">The ValueUnit (value)</param>
        /// <param name="vr">The ValueRange</param>
        /// <example>
        /// <code>
        /// let min = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let vr = create true min None None None
        /// let vu = [| 4N |] |> ValueUnit.create Units.Mass.gram
        /// vr |> contains vu // returns true
        /// let incr = Increment.create ( [| 3N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let vr = create true None incr None None
        /// vr |> contains vu // returns false
        /// </code>
        /// </example>
        let contains vu vr =
            match vr with
            | ValSet vs -> vs |> ValueSet.contains vu
            | _ ->
                let min = vr |> getMin
                let max = vr |> getMax

                let incr = vr |> getIncr

                vu
                |> ValueUnit.getBaseValue
                |> Array.forall (isBetweenMinMax min max)
                && vu
                   |> ValueUnit.getBaseValue
                   |> Array.forall (isMultipleOfIncr incr)


        /// <summary>
        /// Apply a new **newIncr** to a `ValueRange` **vr**.
        /// </summary>
        /// <param name="newIncr">The new increment</param>
        /// <param name="vr">The `ValueRange`</param>
        /// <returns>The resulting (more restrictive) `ValueRange`</returns>
        /// <remarks>
        /// A new increment can only be set if it is a multiple of the previous increment.
        /// If a new increment cannot be set, the original ValueRange is returned. So
        /// the resulting ValueRange will be equal or more restrictive than the original.
        /// </remarks>
        /// <example>
        /// <code>
        /// let min = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let vr = create true min incr None None
        /// // a value range with a minimum = 4 and an increment = 2
        /// let newIncr = Increment.create ( [| 6N |] |> ValueUnit.create Units.Mass.gram)
        /// vr |> setIncr newIncr
        /// // returns [6..6..>, i.e. a ValueRange with a Minimum = 4 and
        /// // and increment = 4
        /// </code>
        /// </example>
        let setIncr newIncr vr =
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
                minIncrMaxToValueRange min newIncr max

            let fIncr = restrict >> Incr

            let fMinIncr (min, incr) =
                minIncrToValueRange min (incr |> restrict)

            let fIncrMax (incr, max) =
                incrMaxToValueRange (incr |> restrict) max

            let fMinIncrMax (min, incr, max) =
                minIncrMaxToValueRange min (incr |> restrict) max

            let fValueSet =
                filter None (Some newIncr) None >> ValSet

            vr
            |> apply (newIncr |> Incr) nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet


        /// <summary>
        /// Apply a new `Minimum` **newMin** to a `ValueRange` **vr**.
        /// </summary>
        /// <param name="newMin">The new `Minimum`</param>
        /// <param name="vr">The `ValueRange`</param>
        /// <returns>The resulting (more restrictive) `ValueRange`</returns>
        /// <remarks>
        /// A new minimum can only be set if it is larger than the previous minimum.
        /// Also, when an increment is set, the new minimum must be a multiple of the
        /// increment. When a new minimum cannot be set, the original ValueRange is returned.
        /// </remarks>
        /// <example>
        /// <code>
        /// let min = Minimum.create true ( [| 3N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let vr = create true min incr None None
        /// // a value range with a minimum = 4 and an increment = 2
        /// let newMin = Minimum.create true ( [| 5N |] |> ValueUnit.create Units.Mass.gram)
        /// vr |> setMin newMin
        /// // returns [6..2..>, i.e. a ValueRange with a Minimum = 6 and
        /// // and increment = 2
        /// </code>
        /// </example>
        let setMin newMin (vr: ValueRange) =
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
                minIncrMaxToValueRange newMin incr max

            let fMinIncrMax (min, incr, max) =
                minIncrMaxToValueRange (min |> restrict) incr max

            let fValueSet =
                filter (Some newMin) None None >> ValSet

            vr
            |> apply (newMin |> Min) nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet


        /// <summary>
        /// Apply a new `Maximum` **newMax** to a `ValueRange` **vr**.
        /// </summary>
        /// <remarks>
        /// If maximum cannot be set the original `ValueRange` is returned.
        /// So it always returns an equal or more restrictive, ValueRange.
        /// </remarks>
        /// <param name="newMax">The new `Maximum`</param>
        /// <param name="vr">The `ValueRange`</param>
        /// <returns>The resulting (more restrictive) `ValueRange`</returns>
        /// <example>
        /// <code>
        /// let max = Maximum.create true ( [| 5N |] |> ValueUnit.create Units.Mass.gram) |> Some
        /// let vr = create true None None max None
        /// // a value range with a maximum = 5
        /// let newMax = Maximum.create true ( [| 4N |] |> ValueUnit.create Units.Mass.gram)
        /// vr |> setMax newMax
        /// // returns a ValueRange with a Maximum = 4
        /// </code>
        ///</example>
        let setMax newMax (vr: ValueRange) =
            let restrict = Maximum.restrict newMax

            let nonZero =
                newMax |> Maximum.nonZeroNonNeg |> MinMax

            let fMin min = minMaxToValueRange min newMax

            let fMax max = max |> restrict |> Max

            let fMinMax (min, max) =
                minMaxToValueRange min (max |> restrict)

            let fIncr incr = incrMaxToValueRange incr newMax

            let fMinIncr (min, incr) =
                minIncrMaxToValueRange min incr newMax

            let fIncrMax (incr, max) =
                incrMaxToValueRange incr (max |> restrict)

            let fMinIncrMax (min, incr, max) =
                minIncrMaxToValueRange min incr (max |> restrict)

            let fValueSet =
                filter None None (Some newMax) >> ValSet

            vr
            |> apply (newMax |> Max) nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fValueSet


        /// <summary>
        /// Apply a new `ValueSet` **newVs** to a `ValueRange` **vr**.
        /// </summary>
        /// <param name="newVs">The new ValueSet</param>
        /// <param name="vr">The ValueRange</param>
        /// <returns>The resulting (more restrictive) `ValueRange`</returns>
        /// <remarks>
        /// The resulting ValueRange will be equal or more restrictive than the original.
        /// This means that if the new ValueSet is empty, the original ValueRange is returned.
        /// If the new ValueSet contains values the result will be an intersection of the
        /// original ValueRange and the new ValueSet.
        /// </remarks>
        /// <example>
        /// <code>
        /// let vs = [| 1N..1N..10N |] |> ValueUnit.create Units.Mass.gram |> ValueSet.create
        /// let vr = create true None None None (Some vs)
        /// let newVs = [| 6N..2N..16N |] |> ValueUnit.create Units.Mass.gram |> ValueSet.create
        /// vr |> setValueSet newVs
        /// // returns a ValueRange with a values 6, 8 and 10
        /// </code>
        /// </example>
        let setValueSet newVs (vr: ValueRange) =
            let min, incr, max, oldVs =
                vr |> getMin, vr |> getIncr, vr |> getMax, vr |> getValSet

            match oldVs with
            | None    -> newVs |> filter min incr max
            | Some vs -> newVs |> ValueSet.intersect vs
            |> ValSet


        /// <summary>
        /// Make a ValueRange non zero and non negative. I.e. with at least
        /// a minimum that excludes zero.
        /// </summary>
        /// <param name="vr">The ValueRange</param>
        /// <returns>The resulting (more restrictive) non zero and non negative `ValueRange`</returns>
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

                minIncrMaxToValueRange newMin incr max

            let fMinIncrMax (min, incr, max) =
                let newMin, max =
                    max |> Maximum.nonZeroNonNeg
                let min = min |> Minimum.restrict newMin

                minIncrMaxToValueRange min incr max

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


        /// <summary>
        /// Get the count of a ValueRange with a Minimum, Increment and a Maximum.
        /// </summary>
        /// <param name="min">The Minimum</param>
        /// <param name="incr">The Increment</param>
        /// <param name="max">The Maximum</param>
        /// <returns>The count of the ValueRange</returns>
        /// <remarks>
        /// The Minimum and Maximum are restricted to multiples of the Increment.
        /// Therefore, they will also be inclusive.
        /// </remarks>
        /// <example>
        /// <code>
        /// let min = Minimum.create false ( [| 3N |] |> ValueUnit.create Units.Mass.gram)
        /// let max = Maximum.create true ( [|7N |] |> ValueUnit.create Units.Mass.gram)
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// minIncrMaxCount min incr max // returns 2
        /// </code>
        /// </example>
        let minIncrMaxCount min incr max =
                let min =
                    min
                    |> Minimum.multipleOf incr
                    |> Minimum.toValueUnit
                    |> ValueUnit.getBaseValue

                let max =
                    max
                    |> Maximum.multipleOf incr
                    |> Maximum.toValueUnit
                    |> ValueUnit.getBaseValue

                incr
                |> Increment.toValueUnit
                |> ValueUnit.getBaseValue
                |> Array.map (fun i ->
                    [|
                        for mi in min do
                            for ma in max do
                                if i > (ma - mi) then 0N
                                else
                                    1N + (ma - mi) / i
                    |]
                    |> Array.sum
                )
                |> Array.sum


        /// <summary>
        /// Try and increase the increment of a `ValueRange` **vr** to an
        /// increment in incrs such that the resulting ValueRange contains
        /// at most maxCount values.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The increment list</param>
        /// <param name="vr">The ValueRange</param>
        /// <returns>The resulting (more restrictive) `ValueRange`</returns>
        /// <remarks>
        /// When there is no increment in the list that can be used to increase
        /// the increment of the ValueRange to the maximum count, the largest possible
        /// increment is used.
        /// </remarks>
        /// <example>
        /// <code>
        /// let min = Minimum.create true ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// let max = Maximum.create true ( [| 1_000N |] |> ValueUnit.create Units.Mass.gram)
        /// let incr = Increment.create ( [| 2N |] |> ValueUnit.create Units.Mass.gram)
        /// minIncrMaxCount min incr max // returns 500
        /// let incrs = [2N;20N;50N;100N;500N] |> List.map (fun i -> Increment.create ( [| i |] |> ValueUnit.create Units.Mass.gram))
        /// let vr = create true (Some min) (Some incr) (Some max) None
        /// increaseIncrement 10N incrs vr
        /// // returns a ValueRange with a Minimum = 100 and an Increment = 100
        /// // and a Maximum = 1_000, such that the count of the ValueRange is &lt;= 10
        /// </code>
        /// </example>
        let increaseIncrement maxCount incrs vr =
            match vr with
            | MinIncrMax (min, _, max) ->
                incrs
                |> List.fold (fun (b, acc) i ->
                    if b then (b, acc)
                    else
                        let b = minIncrMaxCount min i max <= maxCount
                        try
                            let acc = acc |> setIncr i
                            (b, acc)
                        with
                        | _ -> (false, acc)
                ) (false, vr)
            | _ -> (false, vr)
            |> snd


        /// Check if ValueRange vr1 is equal to ValueRange vr2.
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


        /// Create a string (to print) representation of a `ValueRange` by
        /// supplying the optional min, incr and max of the ValueRange.
        /// `Exact` true prints exact BigRationals, when false
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



        /// Convert a `ValueRange` to a markdown `string`.
        let toMarkdown prec vr =
            let print prec isNonZero min max vs =
                if isNonZero then
                    "<0..>"

                else
                    let printRange min max =
                        let minToStr = Minimum.toMarkdown prec
                        let maxToStr = Maximum.toMarkdown prec

                        match min, max with
                        | None, None -> $""
                        | Some min, None -> $"{min |> minToStr} .."
                        | None, Some max -> $".. {max |> maxToStr}"
                        | Some min, Some max -> $"{min |> minToStr} .. {max |> maxToStr}"

                    match vs with
                    | Some vs -> $"{vs |> ValueSet.toMarkdown prec}"
                    | None -> printRange min max

            let fVs vs =
                print prec false None None (Some vs)

            let unr =
                print prec false None None None

            let nonZero =
                print prec true None None None

            let print min max = print prec false min max None

            let fMin min = print (Some min) None

            let fMax max = print None (Some max)

            let fIncr _ = print None None

            let fMinIncr (min, _) = print (Some min) None

            let fIncrMax (_, max) = print None (Some max)

            let fMinIncrMax (min, _, max) = print (Some min) (Some max)

            let fMinMax (min, max) = print (Some min) (Some max)

            vr
            |> apply unr nonZero fMin fMax fMinMax fIncr fMinIncr fIncrMax fMinIncrMax fVs



        /// Functions to calculate the `Minimum`
        /// and `Maximum` in a `ValueRange`.
        /// I.e. what happens when you mult, div, add or subtr
        /// a `Range`, for example:
        /// <1N..3N] * <4N..5N> = <4N..15N>
        module MinMaxCalculator =

            open Utils.ValueUnit.Operators

            /// Calculate **x1** and **x2** with operator **op**
            /// and use **incl1** and **inc2** to determine whether
            /// the result is inclusive. Use constructor **c** to
            /// create the optional result.
            let calc c op x1 x2 =
                let (vu1Opt, incl1), (vu2Opt, incl2) = x1, x2
                // determine if the result should be inclusive
                let isIncl =
                    match incl1, incl2 with
                    | true, true -> true
                    | _ -> false

                let createZero incl =
                    0N
                    |> ValueUnit.singleWithUnit ZeroUnit
                    |> c incl
                    |> Some

                let vu1IsZero, vu2IsZero =
                    vu1Opt |> Option.map ValueUnit.isZero |> Option.defaultValue false,
                    vu2Opt |> Option.map ValueUnit.isZero |> Option.defaultValue false

                match vu1Opt, incl1, vu2Opt, incl2, vu1IsZero, vu2IsZero, op with
                // if both values are None then the result is None
                | None, _, None, _, _, _, _ -> None
                // if both values are Some and are not zero then the result is Some
                | Some v1, _, Some v2, _, false, false, _ -> v1 |> op <| v2 |> c isIncl |> Some

                // MULTIPLICATION
                // when any of the two values is zero incl, the result
                // of multiplication will always be zero incl
                | _, true, _, _, true, _, Mult
                | _, _, _, true, _, true, Mult -> createZero true
                // when any of the two values is zero excl, the result
                // of multiplication will always be zero excl
                | _, false, _, _, true, _, Mult
                | _, _, _, false, _, true, Mult -> createZero false
                // multiplication by Some non zero by a None will result in a None
                | None, _, Some _, _, false, false, Mult
                | Some _, _, None, _, false, false, Mult -> None

                // DIVISION
                // division by zero incl is not possible, so an exception is thrown
                | _, _, _, true, _, true, Div ->
                    DivideByZeroException("MinMaxCalculator tries to divide by zero") |> raise
                // division by zero excl is possible, but the result is None
                | _, _, _, false, _, true, Div -> None
                // a zero value that divided by another value will always result in zero
                | _, _, _, _, true, _, Div -> createZero incl1
                // a None divided by any nonzero Some value will be None
                | None, _, Some _, _, false, false, Div -> None
                // any value that is divided by an unlimited value will
                // result in zero value that is exclusive, i.e. will
                // approach zero but never reach it
                | Some _, _, None, _, false, false, Div -> createZero false

                // ADDITION
                // adding a None to another value always results in a None
                | None, _, Some _, _, _, _, Add
                | Some _, _, None, _, _, _, Add -> None
                // in any other case we can calculate the result
                | Some v1, _, Some v2, _, _, _, Add -> v1 |> op <| v2 |> c isIncl |> Some

                // SUBTRACTION
                // subtracting a None to another value always results in a None
                | None, _, Some _, _, _, _, Sub
                | Some _, _, None, _, _, _, Sub -> None
                // in any other case we can calculate the result
                | Some v1, _, Some v2, _, _, _, Sub -> v1 |> op <| v2 |> c isIncl |> Some


            /// Calculate an optional `Minimum`
            let calcMin = calc Minimum.create


            /// Calculate an optional `Maximum`
            let calcMax = calc Maximum.create


            /// get the smallest minimum
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


            /// get the largest maximum
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


            /// Match the right minmax calculation
            /// according to the operand
            let calcMinMax op =
                match op with
                | Mult -> multiplication
                | Div -> division
                | Add -> addition
                | Sub -> subtraction


        /// <summary>
        /// Applies an infix operator `op` (either *, /, + or -)
        /// to `ValueRange` vr1 and vr2. If onlyMinIncrMax then
        /// only the minimum, increment and maximum are calculated.
        /// </summary>
        /// <param name="onlyMinIncrMax">Whether only the minimum, increment and maximum should be calculated</param>
        /// <param name="op">The infix operator</param>
        /// <param name="vr1">The first `ValueRange`</param>
        /// <param name="vr2">The second `ValueRange`</param>
        /// <returns>The resulting `ValueRange`</returns>
        /// <example>
        /// <code>
        /// let vs1 = [| 1N; 2N; 3N |] |> ValueUnit.withUnit Units.Count.times |> ValueSet.create
        /// let vs2 = [| 4N; 5N; 6N |] |> ValueUnit.withUnit Units.Count.times |> ValueSet.create
        /// let vr1 = create true None None None (Some vs1)
        /// let vr2 = create true None None None (Some vs2)
        /// calc true (*) (vr1, vr2)
        /// // returns a ValueRange with a Minimum = 4 and a Maximum = 18
        /// calc false (*) (vr1, vr2)
        /// // returns a ValueRange [|4N; 5N; 6N; 8N; 10N; 12N; 15N; 18N|]
        /// </code>
        /// </example>
        let calc onlyMinIncrMax op (vr1, vr2) =

            let calcMinMax min1 max1 min2 max2 =
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

            match vr1, vr2 with
            | Unrestricted, Unrestricted -> unrestricted
            | ValSet s1, ValSet s2 ->
                if not onlyMinIncrMax ||
                   (s1 |> ValueSet.count = 1 && (s2 |> ValueSet.count = 1)) then
                    ValueSet.calc op s1 s2
                    |> ValSet
                else
                    let min1, max1 = vr1 |> getMin, vr1 |> getMax
                    let min2, max2 = vr2 |> getMin, vr2 |> getMax

                    let min, max =
                        calcMinMax min1 max1 min2 max2

                    match min, max with
                    | None, None -> unrestricted
                    | _ -> create min None max None

            // A set with an increment results in a new set of increment
            // Need to match all scenarios with a ValueSet and an increment
            | ValSet s, Incr i
            | Incr i, ValSet s

            | ValSet s, MinIncr (_, i)
            | MinIncr (_, i), ValSet s

            | ValSet s, IncrMax (i, _)
            | IncrMax (i, _), ValSet s

            | ValSet s, MinIncrMax (_, i, _)
            | MinIncrMax (_, i, _), ValSet s ->

                let min1, max1 = vr1 |> getMin, vr1 |> getMax
                let min2, max2 = vr2 |> getMin, vr2 |> getMax

                let min, max =
                    calcMinMax min1 max1 min2 max2

                // calculate a new Increment based upon the ValueSet and an Increment
                let incr1 = i |> Some

                let incr2 =
                    let (ValueSet s) = s
                    s |> Increment.create |> Some

                let incr = Increment.calcOpt op incr1 incr2

                match min, incr, max with
                | None, None, None -> unrestricted
                | _ -> create min incr max None

            // In any other case calculate min, incr and max
            | _ ->
                let min1, incr1, max1 =
                    vr1 |> getMin, vr1 |> getIncr, vr1 |> getMax

                let min2, incr2, max2 =
                    vr2 |> getMin, vr2 |> getIncr, vr2 |> getMax

                let min, max =
                    calcMinMax min1 max1 min2 max2

                // calculate a new increment based upon the incr1 and incr2
                let incr = Increment.calcOpt op incr1 incr2

                match min, incr, max with
                | None, None, None -> unrestricted
                | _ -> create min incr max None


        /// <summary>
        /// Checks whether a `ValueRange` vr1 is a subset of
        /// `ValueRange` vr2.
        /// </summary>
        /// <param name="vr1">The first `ValueRange`</param>
        /// <param name="vr2">The second `ValueRange`</param>
        /// <remarks>
        /// Only checks whether the `ValueSet` of vr1 is a subset of
        /// the `ValueSet` of vr2.
        /// </remarks>
        /// <example>
        /// <code>
        /// let vs1 = [| 1N; 2N; 3N |] |> ValueUnit.withUnit Units.Count.times |> ValueSet.create
        /// let vs2 = [| 2N; 3N |] |> ValueUnit.withUnit Units.Count.times |> ValueSet.create
        /// let vr1 = create true None None None (Some vs1)
        /// let vr2 = create true None None None (Some vs2)
        /// vr1 |> isSubSetOf vr2 // returns false
        /// vr2 |> isSubSetOf vr1 // returns true
        /// </code>
        /// </example>
        let isSubSetOf vr2 vr1 =
            match vr1, vr2 with
            | ValSet s1, ValSet s2 -> s2 |> ValueSet.isSubsetOf s1
            | _ -> false


        /// <summary>
        /// Create a set of `Properties` from a `ValueRange`.
        /// </summary>
        /// <param name="vr"></param>
        /// <example>
        /// <code>
        /// let min = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Minimum.create true
        /// let incr = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Increment.create
        /// let max = 8N |> ValueUnit.singleWithUnit Units.Count.times |> Maximum.create false
        /// let vr = create true (Some min) (Some incr) (Some max) None
        /// vr |> toProperties
        /// // returns
        /// //     set
        /// //       [MinProp (MinIncl (ValueUnit ([|2N|], Count (Times 1N))));
        /// //        MaxProp (MaxIncl (ValueUnit ([|6N|], Count (Times 1N))));
        /// //        IncrProp (Increment (ValueUnit ([|2N|], Count (Times 1N))))]
        /// </code>
        /// </example>
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


        /// <summary>
        /// Get the difference between two `ValueRange`s.
        /// </summary>
        /// <param name="vr1">The first ValueRange</param>
        /// <param name="vr2">The second ValueRange</param>
        /// <returns>The difference between the two ValueRanges</returns>
        /// <example>
        /// <code>
        /// let min = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Minimum.create true
        /// let incr = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Increment.create
        /// let max = 8N |> ValueUnit.singleWithUnit Units.Count.times |> Maximum.create false
        /// let vr1 = create true (Some min) (Some incr) None None
        /// let vr2 = create true None None (Some max) None
        /// vr1 |> diffWith vr2
        /// </code>
        /// </example>
        let diffWith vr1 vr2 =
            vr1
            |> toProperties
            |> Set.difference (vr2 |> toProperties)


        /// <summary>
        /// Set a `ValueRange` expr to a `ValueRange` y.
        /// So, the result is equal to or more restrictive than the original `y`.
        /// </summary>
        /// <param name="y">The `ValueRange` to apply to</param>
        /// <param name="expr">The `ValueRange` to apply</param>
        /// <returns>The resulting (restricted) `ValueRange`</returns>
        /// <example>
        /// <code>
        /// let min = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Minimum.create true
        /// let incr = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Increment.create
        /// let max = 8N |> ValueUnit.singleWithUnit Units.Count.times |> Maximum.create false
        /// let vr1 = create true (Some min) (Some incr) None None
        /// let vr2 = create true None None (Some max) None
        /// vr1 |> applyExpr vr2
        /// // returns
        /// // MinIncrMax
        /// //   (MinIncl (ValueUnit ([|2N|], Count (Times 1N))),
        /// //    Increment (ValueUnit ([|2N|], Count (Times 1N))),
        /// //    MaxIncl (ValueUnit ([|6N|], Count (Times 1N))))
        /// </code>
        /// </example>
        let applyExpr y expr =

            let apply get set vr =
                match expr |> get with
                | Some m -> vr |> set m
                | None -> vr

            match expr with
            | Unrestricted -> y
            | ValSet vs -> y |> setValueSet vs
            | _ ->
                y
                |> apply getIncr setIncr
                |> apply getMin setMin
                |> apply getMax setMax


        module Operators =

            let inline (^*) vr1 vr2 = calc false (*) (vr1, vr2)

            let inline (^/) vr1 vr2 = calc false (/) (vr1, vr2)

            let inline (^+) vr1 vr2 = calc false (+) (vr1, vr2)

            let inline (^-) vr1 vr2 = calc false (-) (vr1, vr2)


            let inline (@*) vr1 vr2 = calc true (*) (vr1, vr2)

            let inline (@/) vr1 vr2 = calc true (/) (vr1, vr2)

            let inline (@+) vr1 vr2 = calc true (+) (vr1, vr2)

            let inline (@-) vr1 vr2 = calc true (-) (vr1, vr2)

            let inline (@<-) vr1 vr2 = applyExpr vr1 vr2



    open ValueRange.Operators

    module Minimum = ValueRange.Minimum
    module Maximum = ValueRange.Maximum


    /// Create a `Variable` and passes
    /// the result to **succ**
    let create succ n vs = { Name = n; Values = vs } |> succ


    /// Create a `Variable` and directly
    /// return the result.
    let createSucc = create id


    /// A Variable with a Unrestricted `ValueRange`
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


    /// Get the string representation of a `Variable`.
    let toString exact ({ Name = n; Values = vs }: Variable) =
        vs
        |> ValueRange.toString exact
        |> sprintf "%s %s" (n |> Name.toString)


    /// Get the `Name` of a `Variable`.
    let getName v = (v |> get).Name


    /// Get the `ValueRange of a `Variable`.
    let getValueRange v = (v |> get).Values


    /// Check whether a `Variable` **v** contains
    /// a value **v**.
    let contains v vr =
        vr |> getValueRange |> ValueRange.contains v


    /// Change `Name` to **n**.
    let setName n v : Variable = { v with Name = n }


    /// Apply a `ValueRange` **vr** to
    /// `Variable` **v**.
    let setValueRange var vr =
        try
            { var with
                Values =
                    (var |> get).Values @<- vr
            }

        with
        | Exceptions.SolverException errs ->
            (var, vr)
            |> Exceptions.VariableCannotSetValueRange
            |> raiseExc errs
        | e ->
            printfn $"couldn't catch exception:{e}"
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


    /// Check whether the `ValueRange` of **v1**
    /// and **v2** are equal.
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


    /// Checks whether the ValueRange of a Variable
    /// is a MinIncrMax
    let isMinIncrMax = getValueRange >> ValueRange.isMinIncrMax


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


        /// <summary>
        /// Try and increase the increment of a `ValueRange` of a Variable to an
        /// increment in incrs such that the resulting ValueRange contains
        /// at most maxCount values.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The increment list</param>
        /// <param name="var">The Variable</param>
        /// <returns>The resulting (more restrictive) `ValueRange`</returns>
        /// <remarks>
        /// When there is no increment in the list that can be used to increase
        /// the increment of the ValueRange to the maximum count, the largest possible
        /// increment is used.
        /// </remarks>
    let increaseIncrement maxCount incrs var =
        if var |> isMinIncrMax |> not then var
        else
            { var with
                Values =
                    var.Values
                    |> ValueRange.increaseIncrement maxCount incrs
            }


    /// <summary>
    /// Calculate a ValueSet for a Variable if the Value of the
    /// Variable is a MinIncrMax
    /// </summary>
    /// <param name="var">The variable to change min incr max to a ValueSet</param>
    /// <param name="n">Prune the ValueSet to n</param>
    /// <returns>A Variable with a ValueSet if this can be calculated</returns>
    let minIncrMaxToValues n var =
        if var |> isMinIncrMax |> not then var
        else
            try
                { var with
                    Values =
                        match var.Values |> ValueRange.getMin,
                              var.Values |> ValueRange.getIncr,
                              var.Values |> ValueRange.getMax with
                        | Some min, Some incr, Some max ->
                            ValueRange.ValueSet.minIncrMaxToValueSet min incr max
                            |> ValSet
                        | _ -> var.Values
                        |> ValueRange.prune n
                }
            with
            | e ->
                printfn $"cannot create values from min incr max: {var.Name}"
                raise e


    /// <summary>
    /// Set the unit of a Variable
    /// </summary>
    let setUnit unit var =
        { var with
            Values =
                var.Values
                |> ValueRange.setUnit unit
        }


    /// Get the unit of a Variable
    let getUnit var =
        var |> getValueRange |> ValueRange.getUnit

    module Operators =

        let inline (^*) vr1 vr2 = calc (^*) (vr1, vr2)

        let inline (^/) vr1 vr2 = calc (^/) (vr1, vr2)

        let inline (^+) vr1 vr2 = calc (^+) (vr1, vr2)

        let inline (^-) vr1 vr2 = calc (^-) (vr1, vr2)


        let inline (@*) vr1 vr2 = calc (@*) (vr1, vr2)

        let inline (@/) vr1 vr2 = calc (@/) (vr1, vr2)

        let inline (@+) vr1 vr2 = calc (@+) (vr1, vr2)

        let inline (@-) vr1 vr2 = calc (@-) (vr1, vr2)

        let inline (@<-) vr1 vr2 = vr2 |> getValueRange |> setValueRange vr1


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

        /// Check whether the operator is division
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

        module ValueSet = ValueRange.ValueSet
        module Increment = ValueRange.Increment

        /// The `Dto` representation of a `Variable`
        type Dto() =
            member val Name = "" with get, set
            member val IsNonZeroNegative = false with get, set
            member val MinOpt: ValueUnit.Dto.Dto option = None with get, set
            member val MinIncl = false with get, set
            member val IncrOpt: ValueUnit.Dto.Dto option = None with get, set
            member val MaxOpt: ValueUnit.Dto.Dto option = None with get, set
            member val MaxIncl = false with get, set
            member val ValsOpt: ValueUnit.Dto.Dto option = None with get, set


        let isUnr (dto: Dto) =
            dto.MinOpt.IsNone
            && dto.MaxOpt.IsNone
            && dto.IncrOpt.IsNone
            && dto.ValsOpt.IsNone
            && not dto.IsNonZeroNegative


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

            let vsOpt =
                dto.ValsOpt
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map ValueSet.create
                )

            let minOpt =
                dto.MinOpt
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map (Minimum.create dto.MinIncl)
                )

            let maxOpt =
                dto.MaxOpt
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map (Maximum.create dto.MaxIncl)
                )

            let incrOpt =
                dto.IncrOpt
                |> Option.bind (fun v ->
                    v
                    |> ValueUnit.Dto.fromDto
                    |> Option.map Increment.create
                )

            let vr =
                if dto.IsNonZeroNegative then
                    NonZeroNoneNegative
                else
                    ValueRange.create minOpt incrOpt maxOpt vsOpt

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
                dto.IsNonZeroNegative <- true
                dto
            | _ ->
                let incr =
                    v.Values
                    |> ValueRange.getIncr
                    |> Option.map (Increment.toValueUnit >> vuToDto)

                let minIncl =
                    match v.Values |> ValueRange.getMin with
                    | Some m -> m |> Minimum.isExcl |> not
                    | None -> false

                let maxIncl =
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
                    |> Option.map (ValueSet.toValueUnit >> vuToDto)

                dto.IncrOpt <- incr
                dto.MinOpt <- min
                dto.MinIncl <- minIncl
                dto.MaxOpt <- max
                dto.MaxIncl <- maxIncl
                dto.ValsOpt <- vals

                dto


        module Tests =

            /// there and back again dto test
            let dtoTest () =
                let min = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Minimum.create true
                let incr = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Increment.create
                let max = 8N |> ValueUnit.singleWithUnit Units.Count.times |> Maximum.create false
                let vr = ValueRange.create (Some min) (Some incr) (Some max) None
                let v = createSucc (Name.createExc "test") vr
                let dto = v |> toDto
                let v2 = dto |> fromDto
                v2 = v

