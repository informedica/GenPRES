
#load "load.fsx"

#r "../bin/Debug/net9.0/Informedica.GenForm.Lib.dll"


open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources

#time


System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

let provider : IResourceProvider = Api.getCachedProviderWithDataUrlId "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"


provider.GetResourceInfo () |> ignore

provider
|> Api.getPrescriptionRules
|> fun f -> Patient.patient |> f