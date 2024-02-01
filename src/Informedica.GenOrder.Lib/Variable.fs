namespace Informedica.GenOrder.Lib


module Variable =


    module ValueRange =

        open Informedica.GenSolver.Lib.Variable.ValueRange


        let inline private setOpt aOption set vr =
            try
                match aOption with
                | Some m -> vr |> set m
                | None   -> vr
            with
            | e ->
                printfn $"couldn't set {aOption} to {vr}"
                vr // TODO: ugly fix need to refactor


        /// <summary>
        /// Sets an optional minimum value for the value range
        /// </summary>
        /// <param name="min">The optional Minimum</param>
        /// <param name="vr">The ValueRange</param>
        let setOptMin min vr = vr |> setOpt min setMin


        /// <summary>
        /// Sets an optional maximum value for the value range
        /// </summary>
        /// <param name="max">The optional Maximum</param>
        /// <param name="vr">The ValueRange</param>
        let setOptMax max vr = vr |> setOpt max setMax


        /// <summary>
        /// Sets an optional increment value for the value range
        /// </summary>
        /// <param name="incr">The optional Increment</param>
        /// <param name="vr">The ValueRange</param>
        let setOptIncr incr vr = vr |> setOpt incr setIncr


        /// <summary>
        /// Sets an optional value set for the value range
        /// </summary>
        /// <param name="vs">The optional ValueSet</param>
        /// <param name="vr">The ValueRange</param>
        let setOptVs vs vr = vr |> setOpt vs setValueSet
