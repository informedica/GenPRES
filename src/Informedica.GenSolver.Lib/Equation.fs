namespace Informedica.GenSolver.Lib



/// Functions that handle the `Equation` type that
/// either represents a `ProductEquation` </br>
/// y = x1 \* x2 * ... \* xn </br>
/// or a `SumEquations` </br>
/// y = x1 + x2 + ... + xn
module Equation =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime

    open Types
    open Variable.Operators

    module Name = Variable.Name
    module ValueRange = Variable.ValueRange

    module SolveResult =


        module Property = Variable.ValueRange.Property

        /// <summary>
        /// Get the string representation of a SolveResult
        /// </summary>
        let toString = function
            | Unchanged -> "Unchanged"
            | Changed cs ->
                let toStr (var : Variable, props)  =
                    $"""changes: {var.Name |> Variable.Name.toStringReplace true}: {props |> Set.map (Property.toString false) |> String.concat ", "}"""
                if cs |> List.isEmpty then ""
                else
                    cs
                    |> List.map toStr
                    |> String.concat ", "
            | Errored ms ->
                ms
                |> List.map string
                |> String.concat ", "


    /// <summary>
    /// Create an `Equation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time using the **fail** function.
    /// The type of Equation product or sum
    /// is determined by the constructor **c**.
    /// </summary>
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


    /// <summary>
    /// Create an `ProductEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time using the **fail** function.
    /// </summary>
    let createProductEq = create ProductEquation


    /// <summary>
    /// Create an `SumEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time using the **fail** function.
    /// </summary>
    let createSumEq = create SumEquation


    /// <summary>
    /// Create an `ProductEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time raising an exception.
    /// </summary>
    let createProductEqExc = createProductEq id (Exceptions.raiseExc None [])


    /// <summary>
    /// Create an `SumEquation` with an **y** and
    /// **xs**. Fails if a variable is added more
    /// than one time raising an exception.
    /// </summary>
    let createSumEqExc = createSumEq id (Exceptions.raiseExc None [])


    /// <summary>
    /// Apply **fp** to a `ProductEquation` and
    /// **fs** to a `SumEquation`.
    /// </summary>
    let apply fp fs = function
        | ProductEquation (y,xs) -> fp y xs
        | SumEquation (y, xs)    -> fs y xs


    /// <summary>
    /// Check whether an `Equation` is a product equation
    /// </summary>
    let isProduct = apply (fun _ _ -> true) (fun _ _ -> false)


    /// <summary>
    /// Check whether an `Equation` is a sum equation
    /// </summary>
    let isSum = apply (fun _ _ -> true) (fun _ _ -> false)


    /// <summary>
    /// Turn an `Equation` into a list of `Variable`
    /// </summary>
    let toVars =
        let f y xs = y::xs
        apply f f


    /// <summary>
    /// Get the count of `Variable`s in an `Equation`
    /// </summary>
    /// <param name="onlyMinMax">Whether only min max is used</param>
    /// <param name="eq">The equation</param>
    let count onlyMinMax eq =
        let vars = eq |> toVars
        let b =
            let n =
                vars
                |> List.filter Variable.isSolved
                |> List.length
            (vars |> List.length) - n = 1
        if b then -100
        else
            if onlyMinMax &&
               eq
               |> toVars
               |> List.exists (fun var ->
                   var.Values
                   |> ValueRange.isValueSet
               )
               then -50
            else
                eq
                |> toVars
                |> List.fold (fun (acc : int) v ->
                    (+) (v |> Variable.count) acc
                ) 0


    /// <summary>
    /// Get product of the `Variable`s in an `Equation`
    /// </summary>
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


    /// <summary>
    /// Get the string representation of an `Equation`
    /// </summary>
    let toString exact eq =
        let op = if eq |> isProduct then " * " else " + "
        let varToString = Variable.toString exact

        match eq |> toVars with
        | [] -> ""
        | [ _ ] -> ""
        | y::xs ->
            $"""{y |> varToString} = {xs |> List.map varToString |> String.concat op}"""


    /// <summary>
    /// Get the string representation of an `Equation`
    /// </summary>
    let toStringShort eq =
        let op = if eq |> isProduct then " * " else " + "
        let varToString = Variable.toStringShort

        match eq |> toVars with
        | [] -> ""
        | [ _ ] -> ""
        | y::xs ->
            $"""{y |> varToString} = {xs |> List.map varToString |> String.concat op}"""


    /// <summary>
    /// Make sure that the `Variables` in the
    /// `Equation` can only contain positive
    /// non zero values.
    /// </summary>
    let nonZeroOrNegative eq =
        let set c y xs =
            let y = y |> Variable.setNonZeroOrNegative
            let xs = xs |> List.map Variable.setNonZeroOrNegative
            (y, xs) |> c
        let fp = set ProductEquation
        let fs = set SumEquation
        eq |> apply fp fs


    /// <summary>
    /// Check whether an `Equation` contains
    /// a `Variable` **v**
    /// </summary>
    let contains v = toVars >> (List.exists (Variable.eqName v))


    /// <summary>
    /// Check whether `Equation`s
    /// **eq1** and **eq2** are equal
    /// </summary>
    let equals eq1 eq2 =
        let vrs1 = eq1 |> toVars
        let vrs2 = eq2 |> toVars
        vrs1 |> List.forall (fun vr ->
            vrs2 |> List.exists (Variable.eqName vr)) &&
        ((eq1 |> isProduct) && (eq2 |> isProduct) ||
         (eq1 |> isSum)     && (eq2 |> isSum))


    /// <summary>
    /// Find a `Variable` **vr** in
    /// an `Equation` **eq** and return
    /// the result in a list
    /// </summary>
    let find var eq =
        eq
        |> toVars
        |> List.filter (fun v -> v |> Variable.getName = (var |> Variable.getName))


    /// <summary>
    /// Find a `Variable` with `Name`
    /// **n** in an `Equation` **eq**
    /// and return the result as a list
    /// </summary>
    let findName n eq =
        eq
        |> toVars
        |> List.filter (fun vr -> vr |> Variable.getName = n)


    /// <summary>
    /// Replace a `Variable` **v** in the
    /// `Equation` **e**.
    /// </summary>
    let replace var eq =
        let r c v vs =
            let vs = vs |> List.replace (Variable.eqName v) v
            c id (fun _ -> eq) ((vs |> List.head), (vs|> List.tail))
        let fp y xs = r createProductEq var (y::xs)
        let fs y xs = r createSumEq var (y::xs)
        eq |> apply fp fs



    /// <summary>
    /// Check whether an equation is solved
    /// </summary>
    let isSolved = function
        | ProductEquation (y, xs)
        | SumEquation (y, xs) ->
            y::xs |> List.forall Variable.isSolved


    /// <summary>
    /// Check whether an equation will change by calc
    /// This is not the same as `isSolved`!! If all
    /// the variables are unrestricted than the equation
    /// is not solvable but is also not solved.
    /// </summary>
    let isSolvable = function
        | ProductEquation (y, xs)
        | SumEquation (y, xs) ->
            let es = y::xs
            es |> List.exists Variable.isSolvable &&
            es |> List.filter Variable.isUnrestricted
               |> List.length > 1
               |> not


    /// <summary>
    /// Check whether an equation is valid
    /// </summary>
    /// <param name="eq">The equation</param>
    let check eq =
        let isSub op (y : Variable) (xs : Variable list) =
            match xs with
            | [] -> true
            | _  ->
                let toStr = ValueRange.toString true
                if y.Values |> ValueRange.isValueSet &&
                   xs |> List.map Variable.getValueRange
                      |> List.forall ValueRange.isValueSet then

                    let b =
                        y.Values
                        |> ValueRange.isSubSetOf (xs |> List.reduce op).Values
                    if not b then
                        $"not a subset: {y.Values |> toStr} {(xs |> List.reduce op).Values |> toStr}"
                        |> writeErrorMessage

                    b
                else true

        if eq |> isSolvable || eq |> isSolved then
            match eq with
            | ProductEquation (y, xs) -> xs |> isSub (^*) y
            | SumEquation (y, xs) -> xs |> isSub (^+) y

        else true


    /// <summary>
    /// Get the string representation of a calculation
    /// </summary>
    let calculationToString b op1 op2 y xs =
        let varToStr =
            if b then Variable.toString b else Variable.toStringShort

        let opToStr op  = $" {op |> Variable.Operators.toString} "
        let cost = xs |> List.map Variable.count |> List.reduce (*)
        let x1 = xs |> List.head
        let xs = xs |> List.filter (fun x -> x.Name <> x1.Name)

        $"""{y |> varToStr} = {x1 |> varToStr}{op2 |> opToStr}{xs |> List.map varToStr |> String.concat (op1 |> opToStr)} (cost: {cost})"""


    // perform the calculations on the vars
    let private calcVars log op1 op2 vars =
        // perform a calculation with op1 for list reduction and
        // op1 for the first var and the reduced list
        // i.e. a = b + c + d -> b = a - (c + d)
        // op1 = (+) and op2 = (-)
        let calc op1 op2 xs =
            match xs with
            | []    -> None
            | [ x ] -> Some x
            | y::xs ->
                y |> op2 <| (xs |> List.reduce op1)
                |> Some

        vars
        |> List.fold (fun acc vars ->
            if acc |> Option.isSome then acc
            else
                match vars with
                | _, []
                | _, [ _ ] -> acc
                | i, y::xs ->
                    // skip calculation if variable is already solved
                    if y |> Variable.isSolved then None
                    else
                        let op2 = if i = 0 then op1 else op2
                        // log starting the calculation
                        (op1, op2, y, xs)
                        |> Events.EquationStartCalculation
                        |> Logging.logInfo log

                        xs
                        |> calc op1 op2
                    |> function
                        | None ->
                            // log finishing the calculation
                            (y::xs, false)
                            |> Events.EquationFinishedCalculation
                            |> Logging.logInfo log

                            None
                        | Some var ->
                            let yNew = y @<- var

                            if yNew <> y then
                                // log finishing the calculation
                                ([yNew], true)
                                |> Events.EquationFinishedCalculation
                                |> Logging.logInfo log

                                Some yNew
                            else
                                // log finishing the calculation
                                ([], false)
                                |> Events.EquationFinishedCalculation
                                |> Logging.logInfo log

                                None
        ) None


    // loop until no changes are detected, i.e.
    // calcVars returns None
    [<TailCall>]
    let rec private loop log onlyMinIncrMax op1 op2 acc vars =
        let vars =
            if onlyMinIncrMax then vars
            else
                vars
                |> List.sortBy(fun (_, xs) ->
                    xs
                    |> List.tail
                    |> List.sumBy Variable.count
                )

        match calcVars log op1 op2 vars with
        | None -> acc, vars
        | Some var ->
            let vars =
                vars
                |> List.map (fun (i, xs) ->
                    i,
                    xs |> List.replace (Variable.eqName var) var
                )
                |> fun vars ->
                    if onlyMinIncrMax then vars
                    else
                        vars
                        |> List.sortBy(fun (_, xs) ->
                            xs
                            |> List.tail
                            |> List.map Variable.count
                            |> List.reduce (*)
                        )
            let acc =
                acc
                |> List.replaceOrAdd (Variable.eqName var) var

            loop log onlyMinIncrMax op1 op2 acc vars


    // The actual solving function
    let private solve_ onlyMinIncrMax log eq =
        // orders a list of vars such that most expensive calculations
        // will be performed last (i.e. possibly prevented)
        let reorder = List.reorder >> List.mapi (fun i x -> (i, x))
        // perform a calculation with op1 for list reduction and
        // op1 for the first var and the reduced list
        // i.e. a = b + c + d -> b = a - (c + d)
        // op1 = (+) and op2 = (-)
        if eq |> isSolved then eq, Unchanged
        else
            // log starting the equation solve
            eq
            |> Events.EquationStartedSolving
            |> Logging.logInfo log

            // get the vars and the matching operators
            let vars, op1, op2 =
                match eq with
                | ProductEquation (y, xs) ->
                    if onlyMinIncrMax then
                        y::xs, (@*), (@/)
                    else
                        y::xs, (^*), (^/)
                | SumEquation (y, xs) ->
                    if onlyMinIncrMax then
                        y::xs, (@+), (@-)
                    else
                        y::xs, (^+), (^-)
            // reorder the vars such that
            // a = b + d becomes a list representing
            // [ a = b + d; b = a - d; d = a - b ]
            let vars = vars |> reorder
            // loop until no changes are detected, i.e.
            // calcVars returns None
            vars
            |> loop log onlyMinIncrMax op1 op2 []
            |> fun (changed, vars) ->
                if changed |> List.isEmpty then eq, Unchanged
                else
                    // calculate which vars are changed from the original eq
                    let solveResult =
                        let vars = eq |> toVars
                        changed
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

                    let y, xs =
                        let vars = vars |> List.find (fst >> (=) 0) |> snd
                        vars |> List.head,
                        vars |> List.tail

                    (match eq with
                    | ProductEquation _ -> createProductEqExc (y, xs)
                    | SumEquation _ -> createSumEqExc (y, xs)
                    , solveResult)
                    |> fun (eq, sr) ->
                        // log finishing equation solving
                        (eq, sr)
                        |> Events.EquationFinishedSolving
                        |> Logging.logInfo log

                        eq, sr


    /// <summary>
    /// Solve an equation eq and return the resulting eq with
    /// the variables that were changed as a SolveResult
    /// </summary>
    /// <param name="onlyMinIncrMax">Calculate only Min, Incr and Max if true</param>
    /// <param name="log">The logger to log solving steps</param>
    /// <param name="eq">The equation</param>
    /// <returns>The resulting equation and the changed variables</returns>
    /// <exception cref="Exceptions.SolverException">If solving the equation runs into an error</exception>
    let solve onlyMinIncrMax log eq =
        try
            solve_ onlyMinIncrMax log eq
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