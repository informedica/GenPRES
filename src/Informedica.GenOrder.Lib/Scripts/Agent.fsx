
#load "load.fsx"


#time

open Informedica.ZIndex.Lib
open Informedica.GenOrder.Lib


// load demo or product cache
System.Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "1")
System.Environment.SetEnvironmentVariable(Constants.GENPRES_URL_ID, "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks")

Api.orderAgent.Start ()


Patient.patient
|> Api.orderAgent.Create


Api.orderAgent.Restart ()


Patient.patient
|> Api.orderAgent.Create

