
#load "../../../scripts/Expecto.fsx"
#load "load.fsx"
#load "../Tests.fs"

open Expecto
open Informedica.GenUnits.Tests


Tests.tests
|> Expecto.run


