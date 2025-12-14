namespace Informedica.GenPRES.Shared.Tests


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
        testList "GenPRES.Shared" [
            testHelloWorld
        ]
