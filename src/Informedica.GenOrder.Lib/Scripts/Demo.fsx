
#load "load.fsx"


#time



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib




let path = Some $"{__SOURCE_DIRECTORY__}/log.txt"
let startLogger () =
    // Start the logger at an informative level
    OrderLogger.logger.Start path Logging.Level.Informative
let stopLogger () = OrderLogger.logger.Stop ()




startLogger()


Patient.child
|> fun p -> 
{ p with
    Age = Some 1460N
    Weight = Some 17400N

}
|> Demo.scenarioResult
|> Demo.filter
|> fun scr -> 
    { scr with
        Indication = Some "hypertensie, supraventriculaire aritmieën, marfan syndroom"
        Generic = Some "atenolol"
        Route = Some "or"

    }
|> Demo.filter

