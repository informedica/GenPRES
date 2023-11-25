
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#r "../../Informedica.ZIndex.Lib/bin/Debug/net6.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net6.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net6.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net6.0/Informedica.GenForm.Lib.dll"


//#load "load.fsx"

#time


open System
open Informedica.Utils.Lib


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__



module Expecto =

    open Expecto

    let run = runTestsWithCLIArgs [] [| "--summary" |]



module Tests =


    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib


    module PatientCategoryTests =

        let tests = testList "PatientCategory" [
            let filter = Filter.filter
            let patCat =
                {
                    Department = None
                    Diagnoses = [||]
                    Gender = AnyGender
                    Age = MinMax.none
                    Weight = MinMax.none
                    BSA = MinMax.none
                    GestAge = MinMax.none
                    PMAge = MinMax.none
                    Location = AnyAccess
                }

            test "an empty filter and empty patient category" {
                patCat
                |> PatientCategory.filter filter
                |> Expect.isTrue "should return true"
            }

            test "an empty filter and patient category with female gender" {
                { patCat with Gender = Female }
                |> PatientCategory.filter filter
                |> Expect.isTrue "should return true"
            }

            test "a filter with female gender and patient category with female gender" {
                { patCat with Gender = Female }
                |> PatientCategory.filter { filter with Gender = Female }
                |> Expect.isTrue "should return true"
            }

            test "a filter with female gender and patient category with no gender" {
                { patCat with Gender = AnyGender }
                |> PatientCategory.filter { filter with Gender = Female }
                |> Expect.isFalse "should return false"
            }

            test "an empty filter and a patient category with a max age of 7" {
                { patCat with Age = { patCat.Age with Maximum  = Some 7N  } }
                |> PatientCategory.filter filter
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 5 and a patient category with a max age of 7" {
                { patCat with Age = { patCat.Age with Maximum  = Some 7N  } }
                |> PatientCategory.filter { filter with AgeInDays = Some 5N }
                |> Expect.isTrue "should return true"
            }

        ]


    let tests = testList "GenForm Tests" [
        PatientCategoryTests.tests
    ]



Tests.tests
|> Expecto.run
