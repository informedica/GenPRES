
#r "nuget: Expecto, 9.0.4"
#r "nuget: Expecto.FsCheck, 9.0.4"
#r "nuget: Unquote"

#load "../../../src/Informedica.GenSolver.Lib/Scripts/load.fsx"
#load "../Tests.fs"

open Expecto
open Expecto.Logging
open Expecto.Flip


let run = runTestsWithCLIArgs [] [|"--summary" |]

open Informedica.GenSolver.Tests


module MMTests = Tests.VariableTests.ValueRangeTests.MinMaxCalculatorTests
module Calculator = Informedica.GenSolver.Lib.Variable.ValueRange.MinMaxCalculator
module Variable = Informedica.GenSolver.Lib.Variable

MMTests.scenarios "mult"
|> List.filter (fun s -> s.StartsWith("299"))
|> List.map (fun s ->
    MMTests.scenarioToString
        Calculator.multiplication "x"
        298
        MMTests.validPermutations[299]

)


MMTests.validPermutations[299]
|> fun (min1, max1, min2, max2) ->
    Calculator.multiplication
        (min1 |> MMTests.createVuOpt)
        (max1 |> MMTests.createVuOpt)
        (min2 |> MMTests.createVuOpt)
        (max2 |> MMTests.createVuOpt)
|> fun (min, max) -> Variable.ValueRange.create min None max None
