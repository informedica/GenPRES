namespace Informedica.Utils.Lib



[<RequireQualifiedAccess>]
module Result =


    let get = function
        | Ok r -> r
        | Error _ -> failwith "cannot get result from Error"


    module Tests =

        open Swensen.Unquote


        // Test get
        let testGet () =
            let result = Ok 1 in
            let actual = get result in
            let expected = 1 in
            test <@ actual = expected @>

        // Test get error
        let testGetError () =
            Assertions.raises<System.Exception> <@ Error "error" |> get @>

        // Test all
        let testAll () =
            testGet ()
            testGetError ()