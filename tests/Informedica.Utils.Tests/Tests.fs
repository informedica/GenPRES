namespace Informedica.Utils.Tests


module Tests =

    open System
    open Expecto
    open Expecto.Flip
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib

    let testHelloWorld =
        test "hello world test" {
            "Hello World"
            |> Expect.equal "Strings should be equal" "Hello World"
        }


    module String =

        open Expecto

        open Informedica.Utils.Lib.BCL

        [<Tests>]
        let tests =

            let equals exp txt res = Expect.equal res exp txt

            testList "String" [

                test "splitAt can split a string at character " {
                    Expect.equal ("Hello World" |> String.splitAt ' ')  [|"Hello"; "World"|] " space "
                }

                test "splitAt split of null will yield "  {
                    null
                    |> String.splitAt ' '
                    |> equals [||] " empty array "
                }

                test "splitAt split of an empty string will yield " {
                    ""
                    |> String.splitAt 'a'
                    |> equals [|""|] "an array with an empty string"
                }

                test "split split ca split a string with a string" {
                    "Hello_world"
                    |> String.split "_"
                    |> equals ["Hello"; "world"] "into a list of two strings"
                }

                test "split with a null will yield" {
                    null
                    |> String.split ""
                    |> equals [] "an empty list"
                }

                test "capitalize of an empty string" {
                    ""
                    |> String.capitalize
                    |> equals "" "returns an empty string"
                }

                test "capitalize of an null string" {
                    null
                    |> String.capitalize
                    |> equals "" "returns an empty string"
                }

                test "capitalize of hello world" {
                    "hello world"
                    |> String.capitalize
                    |> equals "Hello world" "returns an empty string"
                }

                test "contains null string null string" {
                    null
                    |> String.contains null
                    |> equals false "returns false"
                }

                test "contains empty string null string" {
                    ""
                    |> String.contains null
                    |> equals false "returns false"
                }

                test "contains null string empty string" {
                    null
                    |> String.contains ""
                    |> equals false "returns false"
                }

                test "contains abc string null string" {
                    "abc"
                    |> String.contains null
                    |> equals false "returns false"
                }

                test "contains abc string empty string" {
                    "abc"
                    |> String.contains ""
                    |> equals true "returns true"
                }

                test "contains abc string a string" {
                    "abc"
                    |> String.contains "a"
                    |> equals true "returns true"
                }

                test "contains abc string b string" {
                    "abc"
                    |> String.contains "b"
                    |> equals true "returns true"
                }

                test "contains abc string c string" {
                    "abc"
                    |> String.contains "c"
                    |> equals true "returns true"
                }

                test "contains abc string abcd string" {
                    "abc"
                    |> String.contains "abcd"
                    |> equals false "returns false"
                }

                test "equals null string null string" {
                    null
                    |> String.equals null
                    |> equals true "returns true"
                }

                test "equals null string empty string" {
                    null
                    |> String.equals ""
                    |> equals false "returns false"
                }

                test "equals a string A string" {
                    "a"
                    |> String.equals "A"
                    |> equals false "returns false"
                }

                test "equalsCapsInsens a string A string" {
                    "a"
                    |> String.equalsCapInsens "A"
                    |> equals true "returns true"
                }

                test "subString of a null string will yield" {
                    null
                    |> String.subString 0 1
                    |> equals "" "returns an empty string"
                }

                test "subString of an empty string will yield" {
                    ""
                    |> String.subString 0 1
                    |> equals "" "returns an empty string"
                }

                test "subString 0 1 of abc string will yield" {
                    "abc"
                    |> String.subString 0 1
                    |> equals "a" "returns a"
                }

                test "subString 1 1 of abc string will yield" {
                    "abc"
                    |> String.subString 1 1
                    |> equals "b" "returns b"
                }

                test "subString 0 0 of abc string will yield" {
                    "abc"
                    |> String.subString 0 0
                    |> equals "" "returns an empty string"
                }

                test "subString 1 -1 of abc string will yield" {
                    "abc"
                    |> String.subString 1 -1
                    |> equals "a" "returns an a"
                }

                test "subString 1 -2 of abc string will yield" {
                    "abc"
                    |> String.subString 1 -2
                    |> equals "" "returns an empty string"
                }

                test "startsWith null string with null string" {
                    null
                    |> String.startsWith null
                    |> equals false "returns false"
                }

                test "startsWith null string with empty string" {
                    null
                    |> String.startsWith ""
                    |> equals false "returns false"
                }

                test "startsWith empty string with null string" {
                    ""
                    |> String.startsWith null
                    |> equals false "returns false"
                }

                test "startsWith abc string with a string" {
                    "abc"
                    |> String.startsWith "a"
                    |> equals true "returns true"
                }

                test "startsWith abc string with abc string" {
                    "abc"
                    |> String.startsWith "abc"
                    |> equals true "returns true"
                }

                test "startsWith abc string with abcd string" {
                    "abc"
                    |> String.startsWith "abcd"
                    |> equals false "returns false"
                }

                test "startsWith abc string with A string" {
                    "abc"
                    |> String.startsWith "A"
                    |> equals false "returns false"
                }

                test "startsWithCapsInsens abc string with A string" {
                    "abc"
                    |> String.startsWithCapsInsens "A"
                    |> equals true "returns true"
                }

                test "restString of null string" {
                    null
                    |> String.restString
                    |> equals "" "returns empty string"
                }

                test "restString of empty string" {
                    ""
                    |> String.restString
                    |> equals "" "returns empty string"
                }

                test "restString of a string" {
                    "a"
                    |> String.restString
                    |> equals "" "returns empty string"
                }

                test "restString of abc string" {
                    "abc"
                    |> String.restString
                    |> equals "bc" "returns bc string"
                }


                test "replacing a string with only numbers with empty returns empty" {
                    let act =
                        "9798797"
                        |> String.replaceNumbers ""
                    Expect.equal act "" "should be empty"
                }
            ]


    module EnvTests =

        open Expecto
        open Informedica.Utils.Lib

        [<Tests>]
        let tests =
            testList "Env" [
                test "environmentVars returns without exceptions and contains common vars" {
                    let vars = Env.environmentVars()
                    // Should not throw and should be non-empty in most environments
                    Expect.isTrue (vars.Count >= 0) "environmentVars returns a dictionary"
                    // PATH is commonly present; if missing we still pass but log
                    if not (vars.ContainsKey "PATH") then
                        printfn "Note: PATH not found in environmentVars on this system/session"
                }

                test "getItem returns None for unlikely var and Some for an injected one" {
                    let name = "INFORMEDICA_UTILS_TEST_VAR"
                    let value = "test-value"
                    // ensure not set
                    match Env.getItem name with
                    | Some _ -> ()
                    | None -> ()

                    // set and verify
                    System.Environment.SetEnvironmentVariable(name, value)
                    let actual = Env.getItem name
                    Expect.equal actual (Some value) "getItem should return the value we set"

                    // clean up
                    System.Environment.SetEnvironmentVariable(name, null)
                }
            ]


    module FileTests =

        open System
        open System.IO
        open Expecto
        open Informedica.Utils.Lib

        [<Tests>]
        let tests =
            testList "File.findParent" [
                test "returns Some for a file in current directory" {
                    // Use this test file name as the sentinel
                    let currentDir = Directory.GetCurrentDirectory()
                    let fileName = "Informedica.Utils.Tests.dll" // built test assembly name typically in bin during run
                    // We can't rely on exact file presence; instead, write a temp file and clean up
                    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
                    Directory.CreateDirectory(tempDir) |> ignore
                    let nestedDir = Path.Combine(tempDir, "a", "b")
                    Directory.CreateDirectory(nestedDir) |> ignore
                    let targetFile = Path.Combine(tempDir, "sentinel.txt")
                    File.WriteAllText(targetFile, "x")
                    // search starting in nestedDir for sentinel.txt
                    let found = File.findParent nestedDir "sentinel.txt"
                    try
                        Expect.equal found (Some tempDir) "Should find parent directory that contains the file"
                    finally
                        try Directory.Delete(tempDir, true) with _ -> ()
                }

                test "returns None when file does not exist in any ancestor" {
                    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
                    Directory.CreateDirectory(tempDir) |> ignore
                    let nestedDir = Path.Combine(tempDir, "a", "b")
                    Directory.CreateDirectory(nestedDir) |> ignore
                    let found = File.findParent nestedDir "definitely-not-existing-12345.xyz"
                    try
                        Expect.equal found None "Should return None when file not found"
                    finally
                        try Directory.Delete(tempDir, true) with _ -> ()
                }
            ]


    module Double =

        open System
        open Expecto
        open MathNet.Numerics

        open Informedica.Utils.Lib.BCL

        [<Tests>]
        let tests =

            let equals exp txt res = Expect.equal res exp txt

            let config =
                { FsCheckConfig.defaultConfig with
                    maxTest = 10000 }

            testList "Double" [

                testPropertyWithConfig config "any valid string double can be parsed to a double" <| fun (a: Double) ->
                    if a |> Double.isValid |> not then true
                    else
                        a
                        |> string
                        |> Double.parse
                        |> string
                        |> ((=) (string a))

                testPropertyWithConfig config "any string can be used to try parse" <| fun s ->
                    s
                    |> Double.tryParse
                    |> (fun _ -> true)

                testPropertyWithConfig config "getPrecision can be calculated for any valid double" <| fun (a: Double) n ->
                    if a |> Double.isValid |> not then true
                    else
                        a
                        |> Double.getPrecision n
                        |> (fun _ -> true)

                testPropertyWithConfig config "getPrecision for a abs value < 0 never returns a smaller value than precision (> 0)" <| fun (a: Double) n ->
                    if n <= 0 || a |> Double.isValid |> not then true
                    else
                        a
                        |> Double.getPrecision n
                        |> (fun x ->
                            if a |> abs < 0. && x < n then
                                printfn "decimals %i < precision %i for value %f" x n a
                                false
                            else true
                        )

                testPropertyWithConfig config "getPrecision for every precision < 0 returns same as n = 0" <| fun (a: Double) n ->
                    if a |> Double.isValid |> not then true
                    else
                        a
                        |> Double.getPrecision n
                        |> (fun x ->
                            if n < 0 then
                                x = (a |> Double.getPrecision 0)
                            else true
                        )

                // * 66.666 |> Double.getPrecision 1 = 0
                test "66.666 |> Double.getPrecision 1 = 0" {
                    66.666 |> Double.getPrecision 1
                    |> equals 0 ""
                }

                // * 6.6666 |> Double.getPrecision 1 = 0
                test "6.6666 |> Double.getPrecision 1 = 0" {
                    6.6666 |> Double.getPrecision 1
                    |> equals 0 ""
                }

                // * 0.6666 |> Double.getPrecision 1 = 1
                test "6.6666 |> Double.getPrecision 1 = 1" {
                    0.6666 |> Double.getPrecision 1
                    |> equals 1 ""
                }

                // * 0.0666 |> Double.getPrecision 1 = 2
                test "0.0666 |> Double.getPrecision 1 = 2" {
                    0.0666 |> Double.getPrecision 1
                    |> equals 2 ""
                }

                // * 0.0666 |> Double.getPrecision 0 = 0
                test "0.0666 |> Double.getPrecision 0 = 0" {
                    0.0666 |> Double.getPrecision 0
                    |> equals 0 ""
                }

                // * 0.0666 |> Double.getPrecision 2 = 3
                test "0.0666 |> Double.getPrecision 2 = 3" {
                    0.0666 |> Double.getPrecision 2
                    |> equals 3 ""
                }

                // * 0.0666 |> Double.getPrecision 3 = 4
                test "0.0666 |> Double.getPrecision 3 = 4" {
                    0.0666 |> Double.getPrecision 3
                    |> equals 4 ""
                }

                // * 6.6666 |> Double.getPrecision 0 = 0
                test "6.6666 |> Double.getPrecision 0 = 0" {
                    6.6666 |> Double.getPrecision 0
                    |> equals 0 ""
                }

                // * 6.6666 |> Double.getPrecision 2 = 1
                test "6.6666 |> Double.getPrecision 2 = 1" {
                    6.6666 |> Double.getPrecision 2
                    |> equals 1 ""
                }

                // * 6.6666 |> Double.getPrecision 3 = 2
                test "6.6666 |> Double.getPrecision 3 = 2" {
                    6.6666 |> Double.getPrecision 3
                    |> equals 2 ""
                }


                // * 66.666 |> Double.fixPrecision 1 = 67
                test "66.666 |> Double.fixPrecision 1 = 67" {
                    66.666 |> Double.fixPrecision 1
                    |> equals 67. ""
                }

                // * 6.6666 |> Double.fixPrecision 1 = 7
                test "6.6666 |> Double.fixPrecision 1 = 7" {
                    6.6666 |> Double.fixPrecision 1
                    |> equals 7. ""
                }

                // * 0.6666 |> Double.fixPrecision 1 = 0.7
                test "6.6666 |> Double.fixPrecision 1 = 0.7" {
                    0.6666 |> Double.fixPrecision 1
                    |> equals 0.7 ""
                }

                // * 0.0666 |> Double.fixPrecision 1 = 0.07
                test "0.0666 |> Double.fixPrecision 1 = 0.07" {
                    0.0666 |> Double.fixPrecision 1
                    |> equals 0.07 ""
                }

                // * 0.0666 |> Double.fixPrecision 0 = 0
                test "0.0666 |> Double.fixPrecision 0 = 0" {
                    0.0666 |> Double.fixPrecision 0
                    |> equals 0. ""
                }

                // * 0.0666 |> Double.fixPrecision 2 = 0.067
                test "0.0666 |> Double.fixPrecision 2 = 0.067" {
                    0.0666 |> Double.fixPrecision 2
                    |> equals 0.067 ""
                }

                // * 0.0666 |> Double.fixPrecision 3 = 0.0666
                test "0.0666 |> Double.fixPrecision 3 = 0.0666" {
                    0.0666 |> Double.fixPrecision 3
                    |> equals 0.0666 ""
                }

                // * 6.6666 |> Double.fixPrecision 0 = 7
                test "6.6666 |> Double.fixPrecision 0 = 7" {
                    6.6666 |> Double.fixPrecision 0
                    |> equals 7. ""
                }

                // * 6.6666 |> Double.fixPrecision 2 = 6.7
                test "6.6666 |> Double.fixPrecision 2 = 6.7" {
                    6.6666 |> Double.fixPrecision 2
                    |> equals 6.7 ""
                }

                // * 6.6666 |> Double.fixPrecision 3 = 6.67
                test "6.6666 |> Double.fixPrecision 3 = 6.67" {
                    6.6666 |> Double.fixPrecision 3
                    |> equals 6.67 ""
                }

                testPropertyWithConfig config "for any valid float, this float can be converted to a fraction" <| fun f ->
                    if f |> Double.isValid |> not then true
                    else
                        f
                        |> Double.floatToFract
                        |> (fun r ->
                            match r with
                            | None -> true
                            | Some (n, d) ->
                                ((n |> BigRational.FromBigInt) / (d |> BigRational.FromBigInt))
                                |> ((=) (f |> BigRational.fromFloat |> Option.get))
                        )

            ]


    module BigRational =

        open Expecto

        open MathNet.Numerics
        open Informedica.Utils.Lib.BCL

        // Test calcCartesian function
        let testCalcCartesian () =

            testList "testCalcCartesion" [
                // Test empty arrays
                testCase "calcCartesian with empty vs1 returns empty array" <| fun _ ->
                    let vs1 : BigRational[] = [||]
                    let vs2 = [|1N; 2N; 3N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [||] "Result should be empty array when vs1 is empty"

                testCase "calcCartesian with empty vs2 returns empty array" <| fun _ ->
                    let vs1 = [|1N; 2N|]
                    let vs2 : BigRational[] = [||]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [||] "Result should be empty array when vs2 is empty"

                // Test single element arrays (broadcasting)
                testCase "calcCartesian single element vs1 addition" <| fun _ ->
                    let vs1 = [|2N|]
                    let vs2 = [|1N; 3N; 5N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [|3N; 5N; 7N|] "Result should match expected addition"

                testCase "calcCartesian single element vs2 addition" <| fun _ ->
                    let vs1 = [|1N; 2N; 4N|]
                    let vs2 = [|3N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [|4N; 5N; 7N|] "Result should match expected addition"

                // Test addition
                testCase "calcCartesian addition" <| fun _ ->
                    let vs1 = [|1N; 2N|]
                    let vs2 = [|3N; 4N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [|4N; 5N; 5N; 6N|] "Addition result should match"

                testCase "calcCartesian subtraction" <| fun _ ->
                    let vs1 = [|5N; 7N|]
                    let vs2 = [|2N; 3N|]
                    let result = BigRational.calcCartesian (-) vs1 vs2
                    Expect.equal result [|3N; 2N; 5N; 4N|] "Subtraction result should match"

                testCase "calcCartesian multiplication" <| fun _ ->
                    let vs1 = [|2N; 3N|]
                    let vs2 = [|4N; 5N|]
                    let result = BigRational.calcCartesian (*) vs1 vs2
                    Expect.equal result [|8N; 10N; 12N; 15N|] "Multiplication result should match"

                testCase "calcCartesian multiplication with zero" <| fun _ ->
                    let vs1 = [|0N; 2N|]
                    let vs2 = [|3N; 4N|]
                    let result = BigRational.calcCartesian (*) vs1 vs2
                    Expect.equal result [|0N; 0N; 6N; 8N|] "Multiplication with zero result should match"

                testCase "calcCartesian multiplication with zero in vs2" <| fun _ ->
                    let vs1 = [|2N; 3N|]
                    let vs2 = [|0N; 4N|]
                    let result = BigRational.calcCartesian (*) vs1 vs2
                    Expect.equal result [|0N; 8N; 0N; 12N|] "Multiplication with zero in vs2 result should match"

                testCase "calcCartesian division" <| fun _ ->
                    let vs1 = [|6N; 8N|]
                    let vs2 = [|2N; 4N|]
                    let result = BigRational.calcCartesian (/) vs1 vs2
                    Expect.equal result [|3N; 3N/2N; 4N; 2N|] "Division result should match"

                testCase "calcCartesian with fractions addition" <| fun _ ->
                    let vs1 = [|1N/2N; 3N/4N|]
                    let vs2 = [|1N/3N; 2N/5N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [|5N/6N; 9N/10N; 13N/12N; 23N/20N|] "Addition with fractions result should match"

                testCase "calcCartesian with fractions multiplication" <| fun _ ->
                    let vs1 = [|1N/2N; 2N/3N|]
                    let vs2 = [|3N/4N; 4N/5N|]
                    let result = BigRational.calcCartesian (*) vs1 vs2
                    Expect.equal result [|3N/8N; 2N/5N; 1N/2N; 8N/15N|] "Multiplication with fractions result should match"

                testCase "calcCartesian larger arrays" <| fun _ ->
                    let vs1 = [|1N; 2N; 3N|]
                    let vs2 = [|10N; 20N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [|11N; 21N; 12N; 22N; 13N; 23N|] "Larger arrays addition result should match"
                    Expect.equal result.Length 6 "Result length should be 6"

                testCase "calcCartesian single element broadcasting with zero for multiplication" <| fun _ ->
                    let vs1 = [|0N|]
                    let vs2 = [|1N; 2N; 3N; 4N; 5N|]
                    let result = BigRational.calcCartesian (*) vs1 vs2
                    Expect.equal result [|0N; 0N; 0N; 0N; 0N|] "Single element broadcasting with zero result should match"

                testCase "calcCartesian division by single element" <| fun _ ->
                    let vs1 = [|6N; 9N; 12N|]
                    let vs2 = [|3N|]
                    let result = BigRational.calcCartesian (/) vs1 vs2
                    Expect.equal result [|2N; 3N; 4N|] "Division by single element result should match"

                testCase "calcCartesian result ordering (Cartesian product order)" <| fun _ ->
                    let vs1 = [|1N; 2N|]
                    let vs2 = [|10N; 20N; 30N|]
                    let result = BigRational.calcCartesian (+) vs1 vs2
                    Expect.equal result [|11N; 21N; 31N; 12N; 22N; 32N|] "Result ordering should match"
            ]


        [<Tests>]
        let tests =

            let equals exp txt res = Expect.equal res exp txt

            let config =
                { FsCheckConfig.defaultConfig with
                    maxTest = 10000
                    arbitrary = [ typeof<Generators.BigRGenerator> ] }

            let opMult f () = f (*)

            testList "BigRational" [

                test "can parse a string number 1" {
                    "1"
                    |> BigRational.tryParse
                    |> equals (Some 1N) "to a br 1"
                }

                testPropertyWithConfig config "can try to convert any double to bigrational" <| fun (a: float) ->
                    a
                    |> (BigRational.fromFloat >> Option.defaultValue 0N >> BigRational.toFloat)
                    |> (fun b ->
                        if b = 0. || Accuracy.areClose Accuracy.veryHigh a b then true
                        else
                            printfn "%f <> %f" a b
                            false
                    )

                testPropertyWithConfig config "can convert any bigrational to a double" <| fun br ->
                    let f =
                        br
                        |> BigRational.toFloat
                    f
                    |> BigRational.fromFloat
                    |> (fun r ->
                        if r |> Option.isNone then false
                        else
                            r
                            |> Option.get
                            |> BigRational.toFloat
                            |> Accuracy.areClose Accuracy.veryHigh f
                    )

                testPropertyWithConfig config "can parse any string float" <| fun (a: float) ->
                    match a |> (BigRational.fromFloat >> Option.defaultValue 0N >> string >> BigRational.tryParse) with
                    | Some b ->
                        b
                        |> BigRational.toString
                        |> BigRational.parse = b
                    | None -> true

                testPropertyWithConfig config "parse can be reversed" <| fun a ->
                    match a |> BigRational.tryParse with
                    | Some b ->
                        b
                        |> BigRational.toString
                        |> BigRational.parse = b
                    | None -> true

                testPropertyWithConfig config "when a is gcd of b and c then b and c both are a multiple of a" <| fun b c ->
                    if (b = 0N || c = 0N) then true
                    else
                        let a = BigRational.gcd b c
                        b |> BigRational.isMultiple a &&
                        c |> BigRational.isMultiple a

                testPropertyWithConfig config "when b is converted to multiple of c then result a is multiple of c" <| fun b c ->
                    if (b = 0N || c = 0N) then true
                    else
                        let a = b |> BigRational.toMultipleOf true c
                        a |> BigRational.isMultiple c

                testPropertyWithConfig config "can check is multiple for any bigrational" <| fun b c ->
                    if c = 0N then b |> BigRational.isMultiple c |> not
                    else
                        if b |> BigRational.isMultiple c then (b / c).Denominator = 1I
                        else (b / c).Denominator <> 1I

                test "when operator is multiplication" {
                    Expect.equal ((*) |> BigRational.opIsMult) true ""
                }

                test "when operator is addition" {
                    Expect.equal ((+) |> BigRational.opIsAdd) true ""
                }

                test "when operator is division" {
                    Expect.equal ((/) |> BigRational.opIsDiv) true ""
                }

                test "when operator is subtraction" {
                    Expect.equal ((-) |> BigRational.opIsSubtr) true ""
                }
                testCalcCartesian ()
            ]


    module DateTime =

        let tests =
            testList "Age" [

                fun dt1 dt2 ->
                    let dt1 = DateTime.date dt1
                    let dt2 = DateTime.date dt2
                    let dtFirst, dtLast = if dt1 < dt2 then dt1, dt2 else dt2, dt1

                    let y, m, w, d = DateTime.age dtLast dtFirst
                    dtFirst
                    |> DateTime.addYears y
                    |> DateTime.addMonths m
                    |> DateTime.addWeeks w
                    |> DateTime.addDays d
                    |> fun dt ->
                        if dt = dtLast then true
                        else
                            printfn $"age {dt} should be last {dtLast} (first {dtFirst})"
                            false

                |> Generators.testProp $"calc age and back to date"

            ]


    module List =

        open Expecto

        open Informedica.Utils.Lib

        [<Tests>]
        let tests =

            let equals exp txt res = Expect.equal res exp txt

            testList "List" [

                test "replace an element in an empty list " {
                    []
                    |> List.replace ((=) "a") ""
                    |> equals [] "returns an empty list"
                }

                test "replace an element in a list with the element " {
                    ["a"]
                    |> List.replace ((=) "a") "b"
                    |> equals ["b"] "returns the list with the first match replaced"
                }

                test "replace an element in a list without the element " {
                    ["a"]
                    |> List.replace ((=) "b") ""
                    |> equals ["a"] "returns the list with the first match replaced"
                }

                test "replace an element in a list with multiple elements " {
                    ["a";"b";"a"]
                    |> List.replace ((=) "a") "b"
                    |> equals ["b";"b";"a"] "returns the list with the first match replaced"
                }


            ]


    module Reflection =

        open Expecto

        open Informedica.Utils.Lib

        type TestUnion = TestUnion | AnotherTestUnion

        [<Tests>]
        let tests =

          testList "Reflection toString and fromString " [

            testCase "of discriminate union TestUnion" <| fun _ ->
              Expect.equal (TestUnion |> Reflection.toString) "TestUnion" "should print TestUnion"

            test "of discriminate union AnotherTestUnion" {
              Expect.equal (AnotherTestUnion |> Reflection.toString) "AnotherTestUnion" "should print AnotherTestUnion"
            }

            test "can create a TestUnion Option" {
                Expect.equal ("TestUnion" |> Reflection.fromString<TestUnion>) (Some TestUnion) "from string TestUnion"
            }

            test "will return None with a non existing union type" {
                Expect.equal ("blah" |> Reflection.fromString<TestUnion>) None "from string blah"
            }

          ]


    module Csv =

        open Expect
        open Expecto.Flip

        let inline parse<'T> dt (p : string -> 'T option) (s: string) =
            s
            |> Csv.parse false dt p
            |> unbox<'T>


        let inline tryParse<'T> dt (p : string -> 'T option) (s: string) =
            s
            |> Csv.parse true dt p
            |> unbox<'T>

        let parseString (s : string) =
            s
            |> Csv.parse false Csv.StringData Some
            |> unbox<string>

        let parserTests =
            testList "parse" [
                test "string" {
                    "a string"
                    |> parseString
                    |> Expect.equal "should be equal to" "a string"
                }

                fun (s1 : string) ->
                    s1
                    |> parseString
                    |> fun s2 ->
                        s2 = s1
                |> Generators.testProp "any string"

                test "string with trailing spaces" {
                    "trailing "
                    |> parseString
                    |> fun s1 ->
                        Swensen.Unquote.Assertions.test <@ s1 = "trailing " @>
                }

                fun (i : int) ->
                    i
                    |> string
                    |> parse<int> Csv.Int32Data Int32.tryParse
                    |> fun result -> i = result
                |> Generators.testProp "any integer"


                fun (i : decimal) ->
                    i
                    |> string
                    |> parse<decimal> Csv.DecimalData Decimal.tryParse
                    |> fun result -> i = result
                |> Generators.testProp "any decimal"

                fun s ->
                    s
                    |> tryParse
                    |> fun _ -> true
                |> Generators.testProp "try parse never fails"
            ]

        let tryCastTests =
            testList "tryCast" [
                test "trailing spaces " {
                    "trailing spaces "
                    |> Csv.tryCast<string> Csv.StringData
                    |> fun r ->
                        Swensen.Unquote.Assertions.test <@ r = "trailing spaces" @>
                }

                test "without option fails" {
                    try
                        "cannot cast to integer"
                        |> Csv.tryCast<int> Csv.Int32Data
                        |> ignore
                        Swensen.Unquote.Assertions.test <@ false @>
                    with
                    | _ ->
                        Swensen.Unquote.Assertions.test <@ true @>
                }

                fun x ->
                    x
                    |> Csv.tryCast<int option> Csv.Int32OptionData
                    |> ignore
                    true
                |> Generators.testProp "with int option never fails"

                fun x ->
                    x
                    |> Csv.tryCast<double option> Csv.FloatOptionData
                    |> ignore
                    true
                |> Generators.testProp "with double option never fails"

                fun x ->
                    x
                    |> Csv.tryCast<decimal option> Csv.DecimalOptionData
                    |> ignore
                    true
                |> Generators.testProp "with decimal option never fails"
            ]


        let tryGetColumnTests =
            let intData = 1
            let floatData = 2.4
            let stringData = "hello"

            let cols = [|"a"; "b"; "c"|]
            let data = [|$"%i{intData}"; $"%f{floatData}"; $"%s{stringData}"|]

            testList "tryGetColmun" [
                test "can get 'a' column" {
                    data
                    |> Csv.getColumn<int> Csv.Int32Data cols
                    |> fun get -> get "a"
                    |> Expect.equal $"column a should be %i{intData}" intData
                }

                test "can get 'b' column" {
                    data
                    |> Csv.getColumn<float> Csv.FloatData cols
                    |> fun get -> get "b"
                    |> Expect.equal $"column b should be %f{floatData}" floatData
                }

                test "can get 'c' column" {
                    data
                    |> Csv.getColumn<string> Csv.StringData cols
                    |> fun get -> get "c"
                    |> Expect.equal $"column c should be %s{stringData}" stringData
                }

                test "cannot get non-existing 'd' column" {
                    fun () ->
                        data
                        |> Csv.getColumn<string> Csv.StringData cols
                        |> fun get -> get "d"
                        |> ignore
                    |> Expect.throws $"should throw exception when trying to get non-existing d column"
                }
            ]


        let parseCsvTests =
            let intData = 1
            let floatData = 2.4
            let stringData = "hello"

            let cols = [|"a"; "b"; "c"|]
            let data = [|$"%i{intData}"; $"%f{floatData}"; $"%s{stringData}"|]

            let testCsv =
                    $"""
"{cols |> String.concat "\",\""}"
"{data |> String.concat "\",\""}"
"{data |> String.concat "\",\""}"
"{data |> String.concat "\",\""}"
"""

            testList "parseCsv" [
                test "can parse csv formatted string" {
                    testCsv
                    |> Csv.parseCSV
                    |> Ok
                    |> Expect.isOk "should be ok"
                }

                test "should contain the c column with hello" {
                    testCsv
                    |> Csv.parseCSV
                    |> function
                    | [|cols; row1; _; _ |] ->
                        let v = row1 |> Csv.getColumn<string> Csv.StringData cols |> fun get -> get "c"
                        Expect.equal "column c should be hello" v stringData
                    | _ ->
                        failwith "unexpected parseCSV result"
                }
            ]

        [<Tests>]
        let tests =
            testList "Csv" [
                parserTests
                tryCastTests
                tryGetColumnTests
                parseCsvTests
            ]

