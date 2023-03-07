
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"



#load "../../../scripts/Expecto.fsx"
#load "load.fsx"


#load "../../../src/Informedica.GenCore.Lib/Measures.fs"
#load "../../../src/Informedica.GenCore.Lib/Aether.fs"
#load "../../../src/Informedica.GenCore.Lib/Validus.fs"
#load "../../../src/Informedica.GenCore.Lib/Calculations.fs"
#load "../../../src/Informedica.GenCore.Lib/ValueUnit.fs"
#load "../../../src/Informedica.GenCore.Lib/MinIncrMax.fs"
#load "../../../src/Informedica.GenCore.Lib/Patient.fs"

#load "../Tests.fs"



open Expecto
open Informedica.GenCore.Tests

Tests.tests
|> Expecto.run

