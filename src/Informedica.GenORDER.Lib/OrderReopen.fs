namespace Informedica.GenOrder.Lib


/// The reopen of a cleared variable the user picked. The picks are the variables the user picked,
/// in the order picked. A clear of a picked variable reopens it: the order is reset, the picks made
/// before the cleared one get their values back, and the order is solved. Picks made after it were
/// made given its old value and go.
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
        )
