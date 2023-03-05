open Expecto

open System


[<EntryPoint>]
let main argv =
    printfn $"{Environment.CurrentDirectory}"

    Informedica.ZIndex.Tests.Tests.tests
    |> runTestsWithCLIArgs [] argv
    //runTestsInAssemblyWithCLIArgs [] argv
