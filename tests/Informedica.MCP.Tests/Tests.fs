namespace Informedica.MCP.Tests


module Tests =

    open Expecto

    let testHelloWorld =
        test "hello world test" {
            "Hello World"
            |> Expect.equal "Strings should be equal" "Hello World"
        }

    [<Tests>]
    let tests =
        testList "MCP" [
            testHelloWorld
        ]
