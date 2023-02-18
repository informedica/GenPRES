namespace Informedica.GenSolver.Lib



/// Functions that handle the `Equation` type that
/// either represents a `ProductEquation` </br>
/// y = x1 \* x2 * ... \* xn </br>
/// or a `SumEquations` </br>
/// y = x1 + x2 + ... + xn
module Equation =

    open Informedica.Utils.Lib

    open Types
    open Variable.Operators

    module Name = Variable.Name
    module ValueRange = Variable.ValueRange

    module SolveResult =


        module Property = Variable.ValueRange.Property

        let toString = function
            | Unchanged -> "Unchanged"
            | Changed cs ->
                let toStr (var : Variable, props)  =
                    $"""changes: {var.Name |> Variable.Name.toString}: {props |> Set.map (Property.toString true) |> String.concat ", "}"""
                if cs |> List.isEmpty then ""
                else
                    cs
                    |> List.map toStr
                    |> String.concat ", "
            | Errored ms ->
                ms
                |> List.map string
                |> String.concat ", "


    /// Create an `Equation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time using the **fail** function.
    /// The type of Equation product or sum
    /// is determined by the constructor **c**.
    let create c succ fail (y, xs) =
        y::xs
        |> List.filter (fun v ->
            y::xs
            |> List.filter (Variable.eqName v) |> List.length > 1)
        |> function
        | [] -> (y, xs) |> c |> succ
        | duplicates ->
            duplicates
            |> Exceptions.EquationDuplicateVariables
            |> fail

    /// Create an `ProductEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time using the **fail** function.
    let createProductEq = create ProductEquation

    /// Create an `SumEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time using the **fail** function.
    let createSumEq = create SumEquation

    /// Create an `ProductEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time raising an exception.
    let createProductEqExc = createProductEq id (Exceptions.raiseExc None [])

    /// Create an `SumEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time raising an exception.
    let createSumEqExc = createSumEq id (Exceptions.raiseExc None [])

    /// Apply **fp** to a `ProductEquation` and
    /// **fs** to a `SumEquation`.
    let apply fp fs = function
        | ProductEquation (y,xs) -> fp y xs
        | SumEquation (y, xs)    -> fs y xs

    /// Check whether an `Equation` is a product equation
    let isProduct = apply (fun _ _ -> true) (fun _ _ -> false)

    /// Check whether an `Equation` is a sum equation
    let isSum = apply (fun _ _ -> true) (fun _ _ -> false)

    /// Turn an `Equation` into a list of `Variable`
    let toVars =
        let f y xs = y::xs
        apply f f


    let count eq =
        let vars = eq |> toVars
        let b =
            let n =
                vars
                |> List.filter Variable.isSolved
                |> List.length
            (vars |> List.length) - n = 1
        if b then -100
        else
            eq
            |> toVars
            |> List.fold (fun (acc : int) v ->
                (+) (v |> Variable.count) acc
            ) 0


    let countProduct eq =
        //match eq with
        //| SumEquation _ -> -1
        //| _ ->
        eq
        |> toVars
        |> List.fold (fun acc v ->
            let c = v |> Variable.count
            (if c = 0 then 1 else c) * acc
        ) 1


    let toString exact eq =
        let op = if eq |> isProduct then " * " else " + "
        let varToString = Variable.toString exact

        match eq |> toVars with
        | [] -> ""
        | [ _ ] -> ""
        | y::xs ->
            $"""{y |> varToString} = {xs |> List.map varToString |> String.concat op}"""


    /// Make sure that the `Variables` in the
    /// `Equation` can only contain positive
    /// non zero values.
    let nonZeroOrNegative eq =
        let set c y xs =
            let y = y |> Variable.setNonZeroOrNegative
            let xs = xs |> List.map Variable.setNonZeroOrNegative
            (y, xs) |> c
        let fp = set ProductEquation
        let fs = set SumEquation
        eq |> apply fp fs

    /// Check whether an `Equation` contains
    /// a `Variable` **v**
    let contains v = toVars >> (List.exists (Variable.eqName v))

    /// Check whether `Equation`s
    /// **eq1** and **eq2** are equal
    let equals eq1 eq2 =
        let vrs1 = eq1 |> toVars
        let vrs2 = eq2 |> toVars
        vrs1 |> List.forall (fun vr ->
            vrs2 |> List.exists (Variable.eqName vr)) &&
        ((eq1 |> isProduct) && (eq2 |> isProduct) ||
         (eq1 |> isSum)     && (eq2 |> isSum))

    /// Find a `Variable` **vr** in
    /// an `Equation` **eq** and return
    /// the result in a list
    let find var eq =
        eq
        |> toVars
        |> List.filter (fun v -> v |> Variable.getName = (var |> Variable.getName))

    /// Find a `Variable` with `Name`
    /// **n** in an `Equation` **eq**
    /// and return the result as a list
    let findName n eq =
        eq
        |> toVars
        |> List.filter (fun vr -> vr |> Variable.getName = n)

    /// Replace a `Variable` **v** in the
    /// `Equation` **e**.
    let replace var eq =
        let r c v vs =
            let vs = vs |> List.replace (Variable.eqName v) v
            c id (fun _ -> eq) ((vs |> List.head), (vs|> List.tail))
        let fp y xs = r createProductEq var (y::xs)
        let fs y xs = r createSumEq var (y::xs)
        eq |> apply fp fs


    // Check whether an equation is solved
    let isSolved = function
        | ProductEquation (y, xs)
        | SumEquation (y, xs) ->
            y::xs |> List.forall Variable.isSolved


    // Check whether an equation will change by calc
    // This is not the same as `isSolved`!! If all
    // the variables are unrestricted than the equation
    // is not solvable but is also not solved.
    let isSolvable = function
        | ProductEquation (y, xs)
        | SumEquation (y, xs) ->
            let es = y::xs
            es |> List.exists Variable.isSolvable &&
            es |> List.filter Variable.isUnrestricted
               |> List.length > 1
               |> not


    let check eq =
        let isSub op (y : Variable) (xs : Variable list) =
            match xs with
            | [] -> true
            | _  ->
                if y.Values |> ValueRange.isValueSet &&
                   xs |> List.map Variable.getValueRange
                      |> List.forall ValueRange.isValueSet then

                    y.Values
                    |> ValueRange.isSubSetOf (xs |> List.reduce op).Values

                else true

        if eq |> isSolvable then
            match eq with
            | ProductEquation (y, xs) -> xs |> isSub (^*) y
            | SumEquation (y, xs) -> xs |> isSub (^+) y

        else true

    let calculationToString b op1 op2 x y xs =
        let varToStr = Variable.toString b
        let opToStr op  = $" {op |> Variable.Operators.toString} "
        let filter x xs = xs |> List.filter (Variable.eqName x >> not)

        $"""{x |> varToStr} = {y |> varToStr}{op2 |> opToStr}{xs |> filter x |> List.map varToStr |> String.concat (op1 |> opToStr)} """


    /// Solve an equation **e**, return a list of
    /// changed `Variable`s.
    let solve onlyMinIncrMax log eq =
        // helper functions
        let without x xs = xs |> List.filter (Variable.eqName x >> not)
        let replAdd x xs = xs |> List.replaceOrAdd(Variable.eqName x) x

        let (<==) = if onlyMinIncrMax then (@<-) else (^<-)

        let rec calcXs op1 op2 y xs rest xChanged =
            match rest with
            | []  ->
                // log finishing the calculation
                (y::xs, xChanged)
                |> Events.EquationFinishedCalculation // TODO: need to rename
                |> Logging.logInfo log
                // return the result and whether this is changed
                xs, xChanged

            | x::tail ->
                let newX =
                    match xs |> without x with
                    | [] ->  x <== y
                    | _  ->
                        if x |> Variable.isSolved then x
                        else
                            // log the calculation
                            (op1, op2, x, y, xs)
                            |> Events.EquationStartCalculation
                            |> Logging.logInfo log
                            // recalculate x
                            x <== (y |> op2 <| (xs |> without x |> List.reduce op1))

                (xChanged || (x.Values |> ValueRange.eqs newX.Values |> not)) 
                |> calcXs op1 op2 y (xs |> replAdd newX) tail

        let calcY op1 y xs =
            if y |> Variable.isSolved then 
                y, false
            else
                // log the calculation
                (op1, op1, y, (xs |> List.head), (xs |> List.tail))
                |> Events.EquationStartCalculation
                |> Logging.logInfo log
                // recalculate y
                let temp = xs |> List.reduce op1
                let newY = y <== temp //(xs |> List.reduce op1)

                let yChanged = newY.Values |> ValueRange.eqs y.Values |> not

                // log finishing the calculation
                (newY::xs, yChanged)
                |> Events.EquationFinishedCalculation
                |> Logging.logInfo log
                // return the result and whether it changed
                newY, yChanged
        // op1 = (*) or (+) and op2 = (/) or (-)
        let rec loop op1 op2 y xs eqChanged =
            let y, yChanged, xs, xChanged =
                // for performance reasons pick the most efficient order of
                // calculations, first xs then y or vice versa.
                if xs |> List.forall (Variable.count >> ((<) (y |> Variable.count))) then
                    // Calculate x1 = y op2 (x2 op1 x3 .. op1 xn)
                    //       and x2 = y op2 (x1 op1 x3 .. op1 xn)
                    //       etc..
                    let xs, xChanged = calcXs op1 op2 y xs xs false
                    // Calculate y = x1 op1 x2 op1 .. op1 xn
                    let y, yChanged = calcY op1 y xs

                    y, yChanged, xs, xChanged
                else
                    // Calculate y = x1 op1 x2 op1 .. op1 xn
                    let y, yChanged = calcY op1 y xs
                    // Calculate x1 = y op2 (x2 op1 x3 .. op1 xn)
                    //       and x2 = y op2 (x1 op1 x3 .. op1 xn)
                    //       etc..
                    let xs, xChanged = calcXs op1 op2 y xs xs false

                    y, yChanged, xs, xChanged

            // If neither yChanged or xChanged return the result
            if not (yChanged || xChanged) then (y, xs, eqChanged)
            else
                // y or x has changed so loop
                loop op1 op2 y xs true

        let calcResult (y, xs, isChanged) =
            let result =
                // nothing has changed!
                if not isChanged then Unchanged
                // calculate the changes
                else
                    let vars = eq |> toVars

                    y::xs
                    |> List.map (fun v2 ->
                        vars
                        |> List.tryFind (Variable.eqName v2)
                        |> function
                        | Some v1 ->
                            v2, v2.Values
                            |> Variable.ValueRange.diffWith v1.Values
                        | None ->
                            $"cannot find {v2}! in {vars}!"
                            |> failwith
                    )
                    |> List.filter (snd >> Set.isEmpty >> not)
                    |> Changed

            let eq =
                match eq with
                | ProductEquation _ -> createProductEqExc (y, xs)
                | SumEquation _     -> createSumEqExc (y, xs)
            // log finishing equation solving
            (eq, result)
            |> Events.EquationFinishedSolving
            |> Logging.logInfo log
            // return the eq and solve result
            eq, result

        if eq |> isSolved then eq, Unchanged
        else
            // log starting the equation solve
            eq
            |> Events.EquationStartedSolving
            |> Logging.logInfo log
            // get the right operators
            let y, xs, op1, op2 =
                match eq with
                | ProductEquation (y, xs) ->
                    if onlyMinIncrMax then
                        y, xs, (@*), (@/)
                    else
                        y, xs, (^*), (^/)
                | SumEquation (y, xs) ->
                    if onlyMinIncrMax then
                        y, xs, (@+), (@-)
                    else
                        y, xs, (^+), (^-)

            match xs with
            | [] -> eq, Unchanged
            | _  ->
                try
                    loop op1 op2 y xs false
                    |> calcResult
                with
                | Exceptions.SolverException errs ->
                    errs
                    |> List.iter (Logging.logError log)

                    eq, Errored errs


    module Dto =

        type VariableDto = Variable.Dto.Dto

        /// `Dto` for an `Equation`
        type Dto = { Vars: VariableDto[]; IsProdEq: bool }

        /// Create a `Dto` with `vars` (variable dto array)
        /// that is either a `ProductEquation` or a `SumEquation`
        let create isProd vars  = { Vars = vars; IsProdEq = isProd }

        /// Create a `ProductEquation` `Dto`
        let createProd = create true

        /// Create a `SumEquation` `Dto`
        let createSum  = create false

        /// Return the `string` representation of a `Dto`
        let toString exact (dto: Dto) =
            let op = if dto.IsProdEq then "*" else "+"
            let varToString = Variable.Dto.toString exact

            match dto.Vars |> Array.toList with
            | [] -> ""
            | [ _ ] -> ""
            | y::xs ->
                let s =
                    $"%s{y |> varToString} = " +
                    (xs |> List.fold (fun s v -> s + (v |> varToString) + " " + op + " ") "")
                s.Substring(0, s.Length - 2)


        /// Create a `Dto` and raise an exception if it fails
        let fromDto dto =
            let succ = id
            let fail = Exceptions.raiseExc None []

            match dto.Vars |> Array.toList with
            | [] -> Exceptions.EquationEmptyVariableList |> fail
            | y::xs ->
                let y = y |> Variable.Dto.fromDto
                let e = (y, xs |> List.map Variable.Dto.fromDto)

                if dto.IsProdEq then
                    e
                    |> createProductEq succ fail
                else
                    e
                    |> createSumEq succ fail

        /// Create a `Dto` from an `Equation` **e**
        let toDto e =
            let c isProd y xs =
                { Vars = y::xs |> List.map Variable.Dto.toDto |> List.toArray; IsProdEq = isProd }

            let fp = c true
            let fs = c false

            e |> apply fp fs
