namespace Informedica.GenOrder.Lib


/// The reopen of a cleared variable the user picked, and the picks a pick or a step adds. The
/// order scenario lists the variables the user picked, in the order picked. A clear of a picked
/// variable reopens it: the order is reset, the picks made before the cleared one get their values
/// back, and the order is solved. Picks made after it were made given its old value and go.
module OrderReopen =

    module Name = Informedica.GenSolver.Lib.Variable.Name


    /// The order through the pipeline for the command.
    let run logger cmd ord = ord |> cmd |> OrderProcessor.processPipeline logger


    /// The name of an order variable, as the picks list it.
    let nameOf (ovar: OrderVariable) = ovar |> OrderVariable.getName |> Name.toString


    /// The picks made before the first picked variable the order holds cleared; None when no
    /// picked variable is cleared.
    let clearedPick (picks: string list) (ord: Order) =
        let vars = ord |> Order.toOrdVars

        let isCleared name =
            vars |> List.exists (fun v -> nameOf v = name && v |> OrderVariable.isCleared)

        picks
        |> List.tryFindIndex isCleared
        |> Option.map (fun i -> picks |> List.take i)


    /// Reopens the order when a picked variable is cleared: reset, the earlier picks given their
    /// values back from the order as it arrived, solved. None when no picked variable is cleared.
    /// Returns the order and the picks that remain.
    let reopen logger (picks: string list) (ord: Order) =
        clearedPick picks ord
        |> Option.map (fun earlier ->
            let arrived =
                ord
                |> Order.toOrdVars
                |> List.filter (fun v -> earlier |> List.contains (nameOf v))

            ord
            |> run logger ReCalcValues
            |> Result.map (Order.fromOrdVars arrived)
            |> Result.bind (run logger SolveOrder)
            |> Result.map (fun ord -> ord, earlier)
        )


    /// The picks after the user picked the variable with this name: moved to the end when it was
    /// picked before, since it is now the latest pick.
    let add (name: string) (picks: string list) = (picks |> List.filter ((<>) name)) @ [ name ]


    /// The name of the variable a step command moves.
    let steppedBy (cmd: ChangePropertyCommand) (ord: Order) =
        let frequency () =
            ord.Schedule
            |> Order.Schedule.getFrequency
            |> Option.map (OrderVariable.Frequency.toOrdVar >> nameOf)

        let doseQuantity () =
            ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar |> nameOf |> Some

        let doseRate () =
            ord.Orderable.Dose.Rate |> OrderVariable.Rate.toOrdVar |> nameOf |> Some

        let componentQuantity cmp =
            ord.Orderable.Components
            |> List.tryFind (fun c -> c.Name |> Name.toString = cmp)
            |> Option.map (_.OrderableQuantity >> OrderVariable.Quantity.toOrdVar >> nameOf)

        match cmd with
        | DecreaseScheduleFrequency
        | IncreaseScheduleFrequency
        | SetMinScheduleFrequency
        | SetMedianScheduleFrequency
        | SetMaxScheduleFrequency -> frequency ()
        | DecreaseOrderableDoseQuantity _
        | IncreaseOrderableDoseQuantity _
        | SetMinOrderableDoseQuantity
        | SetMaxOrderableDoseQuantity
        | SetMedianOrderableDoseQuantity
        | SetOrderableDoseQuantityPerc _ -> doseQuantity ()
        | DecreaseOrderableDoseRate _
        | IncreaseOrderableDoseRate _
        | SetMinOrderableDoseRate
        | SetMaxOrderableDoseRate
        | SetMedianOrderableDoseRate -> doseRate ()
        | DecreaseComponentOrderableQuantity(cmp, _, _)
        | IncreaseComponentOrderableQuantity(cmp, _, _)
        | SetMinComponentOrderableQuantity cmp
        | SetMaxComponentOrderableQuantity cmp
        | SetMedianComponentOrderableQuantity cmp -> componentQuantity cmp
        | ComponentInStock _ -> None


    /// The variable with this name in the order, if it has one.
    let variableOf (name: string) (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.tryFind (fun v -> nameOf v = name)
        |> Option.map _.Variable


    /// The picks after a step: the variable the step moves becomes the latest pick, but only when
    /// the step moved it. A step at a bound, or without an increment, leaves the variable as it
    /// was and the picks too.
    let afterStep (cmd: ChangePropertyCommand) (before: Order) (after: Order) (picks: string list) =
        match before |> steppedBy cmd with
        | Some name when variableOf name after <> variableOf name before -> picks |> add name
        | _ -> picks
