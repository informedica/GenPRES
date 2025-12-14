namespace Informedica.FHIR.Tests


module Tests =

    open Expecto
    open Expecto.Flip

    let testHelloWorld =
        test "hello world test" {
            "Hello World"
            |> Expect.equal "Strings should be equal" "Hello World"
        }

    [<Tests>]
    let tests =
        testList "FHIR" [
            testHelloWorld
        ]
