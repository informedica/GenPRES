module Tests

open Expecto

[<Tests>]
let test =
    testCase "Hello World" <| fun _ ->
        Expect.isTrue true "This is true"

