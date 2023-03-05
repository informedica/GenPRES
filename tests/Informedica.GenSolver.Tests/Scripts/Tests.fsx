
#r "nuget: Expecto, 9.0.4"
#r "nuget: Expecto.FsCheck, 9.0.4"
#r "nuget: Unquote"

#load "../../../src/Informedica.GenSolver.Lib/Scripts/load.fsx"
#load "../Tests.fs"

open Expecto
open Expecto.Logging
open Expecto.Flip

module SU =
    
    open Swensen.Unquote
    let test = test

let run = runTestsWithCLIArgs [] [|"--summary" |]

open Informedica.GenSolver.Tests

testList "" [
    Utils.tests
    ValueRange.tests
]
|> run