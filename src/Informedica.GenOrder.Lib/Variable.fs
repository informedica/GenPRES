namespace Informedica.GenOrder.Lib


module Variable =

    open Informedica.GenSolver.Lib.Types


    module ValueRange =

        open Informedica.GenSolver.Lib.Variable.ValueRange


        let inline setOpt m set vr =
            try
                match m with
                | Some m -> vr |> set true m
                | None   -> vr
            with | _ -> vr // TODO: ugly fix need to refactor 


        let setOptMin min vr = vr |> setOpt min setMin


        let setOptMax max vr = vr |> setOpt max setMax


        let setOptIncr incr vr = vr |> setOpt incr setIncr


        let setOptVs vs vr =
            try
                match vs with
                | Some vs -> vr |> setValueSet vs
                | None    -> vr

            with | _ -> vr // TODO: ugly fix need to refactor 
