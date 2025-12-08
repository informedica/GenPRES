namespace Informedica.DataPlatform.Tests


module Tests =

    open Expecto

    let testHelloWorld =
        test "hello world test" {
            "Hello World"
            |> Expect.equal "Strings should be equal" "Hello World"
        }

    [<Tests>]
    let tests =
        testList "DataPlatform" [
            testHelloWorld
        ]
