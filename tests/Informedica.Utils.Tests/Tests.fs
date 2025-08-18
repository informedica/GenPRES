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
                    // printfn "%s %s %s" (b |> BigRational.toString) (c |> BigRational.toString) (a |> BigRational.toString)
                    if (b = 0N || c = 0N) then true
                    else
                        let a = BigRational.gcd b c
                        b |> BigRational.isMultiple a &&
                        c |> BigRational.isMultiple a


                testPropertyWithConfig config "when b is converted to multiple of c then result a is multiple of c" <| fun b c ->
                    // printfn "%s %s %s" (b |> BigRational.toString) (c |> BigRational.toString) (a |> BigRational.toString)
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
                        row1
                        |> Csv.getColumn<string> Csv.StringData cols
                        |> fun get -> get "c"
                        |> Expect.equal $"column a should be %s{stringData}" stringData
                    | _ ->
                        Console.WriteLine($"%A{testCsv}")
                        false |> Expect.isTrue "cannot get the c column"
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


    module Ringbuffer =

        open Informedica.Utils.Lib
        open Expecto
        open FsCheck



        // Basic functionality tests
        let basicTests =
            testList "Basic RingBuffer Operations" [
                
                test "create with positive capacity should succeed" {
                    let rb = RingBuffer.create 5
                    Expect.equal rb.Capacity 5 "Capacity should be set correctly"
                    Expect.equal rb.CountValue 0 "Initial count should be 0"
                    Expect.isFalse rb.IsFull "Should not be full initially"
                }
                
                test "create with zero capacity should throw" {
                    Expect.throws (fun () -> RingBuffer.create 0 |> ignore) "Should throw for zero capacity"
                }
                
                test "create with negative capacity should throw" {
                    Expect.throws (fun () -> RingBuffer.create -1 |> ignore) "Should throw for negative capacity"
                }
                
                test "add single item should work" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 42 rb
                    
                    Expect.equal rb.CountValue 1 "Count should be 1"
                    Expect.isFalse rb.IsFull "Should not be full"
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|42|] "Should contain the added item"
                }
                
                test "add items up to capacity" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    
                    Expect.equal rb.CountValue 3 "Count should be 3"
                    Expect.isTrue rb.IsFull "Should be full"
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|1; 2; 3|] "Should contain all items in order"
                }
                
                test "add beyond capacity should overwrite oldest" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    RingBuffer.add 4 rb  // Should overwrite 1
                    
                    Expect.equal rb.CountValue 3 "Count should stay at capacity"
                    Expect.isTrue rb.IsFull "Should remain full"
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|2; 3; 4|] "Should contain newest items, oldest first"
                }
                
                test "clear should reset buffer" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.clear rb
                    
                    Expect.equal rb.CountValue 0 "Count should be 0 after clear"
                    Expect.isFalse rb.IsFull "Should not be full after clear"
                    
                    let items = RingBuffer.toArray rb
                    Expect.isEmpty items "Should be empty after clear"
                }
                
                test "toSeq should return items in oldest-to-newest order" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    RingBuffer.add 4 rb  // Overwrites 1
                    
                    let items = RingBuffer.toSeq rb |> List.ofSeq
                    Expect.equal items [2; 3; 4] "Should return items oldest to newest"
                }
                
                test "iter should visit items in oldest-to-newest order" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    RingBuffer.add 4 rb  // Overwrites 1
                    
                    let mutable visited = []
                    RingBuffer.iter (fun x -> visited <- x :: visited) rb
                    
                    let visitedInOrder = List.rev visited
                    Expect.equal visitedInOrder [2; 3; 4] "Should visit items oldest to newest"
                }
                
                test "map should transform items in oldest-to-newest order" {
                    let rb = RingBuffer.create 3
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    
                    let doubled = RingBuffer.map (fun x -> x * 2) rb
                    Expect.equal doubled [|2; 4; 6|] "Should map items in correct order"
                }
            ]

        // Edge case tests
        let edgeTests =
            testList "Edge Cases" [
                
                test "single capacity buffer behavior" {
                    let rb = RingBuffer.create 1
                    
                    RingBuffer.add 1 rb
                    Expect.equal (RingBuffer.toArray rb) [|1|] "Should contain first item"
                    Expect.isTrue rb.IsFull "Should be full"
                    
                    RingBuffer.add 2 rb
                    Expect.equal (RingBuffer.toArray rb) [|2|] "Should contain second item, first overwritten"
                    
                    RingBuffer.add 3 rb
                    Expect.equal (RingBuffer.toArray rb) [|3|] "Should contain third item"
                }
                
                test "empty buffer operations" {
                    let rb = RingBuffer.create 5
                    
                    let items = RingBuffer.toArray rb
                    Expect.isEmpty items "Empty buffer should return empty array"
                    
                    let seqItems = RingBuffer.toSeq rb |> List.ofSeq
                    Expect.isEmpty seqItems "Empty buffer should return empty sequence"
                    
                    let mutable iterCount = 0
                    RingBuffer.iter (fun _ -> iterCount <- iterCount + 1) rb
                    Expect.equal iterCount 0 "Iter should not execute on empty buffer"
                    
                    let mapped = RingBuffer.map (fun x -> x * 2) rb
                    Expect.isEmpty mapped "Map should return empty array for empty buffer"
                }
                
                test "clear and reuse buffer" {
                    let rb = RingBuffer.create 3
                    
                    // Fill buffer
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    
                    // Clear and reuse
                    RingBuffer.clear rb
                    RingBuffer.add 10 rb
                    RingBuffer.add 20 rb
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|10; 20|] "Should work correctly after clear"
                }
                
                test "multiple wrap-arounds" {
                    let rb = RingBuffer.create 3
                    
                    // Add 10 items (multiple wrap-arounds)
                    for i in 1..10 do
                        RingBuffer.add i rb
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|8; 9; 10|] "Should contain last 3 items"
                }
            ]

        // Property-based tests using FsCheck
        let propertyTests =
            testList "Property-based Tests" [
                
                testProperty
                    "capacity is always preserved" <| fun (PositiveInt capacity) ->
                    (capacity > 0 && capacity < 1000) ==> lazy (
                        let rb = RingBuffer.create capacity
                        rb.Capacity = capacity
                    )
                
                testProperty
                    "count never exceeds capacity" <| fun (PositiveInt capacity) (items: int list) ->
                    (capacity > 0 && capacity < 100) ==> lazy (
                        let rb = RingBuffer.create capacity
                        items |> List.iter (fun item -> RingBuffer.add item rb)
                        rb.CountValue <= rb.Capacity
                    )
                
                testProperty
                    "toArray length equals count" <| fun (PositiveInt capacity) (items: int list) ->
                    (capacity > 0 && capacity < 100) ==> lazy (
                        let rb = RingBuffer.create capacity
                        items |> List.iter (fun item -> RingBuffer.add item rb)
                        let arr = RingBuffer.toArray rb
                        arr.Length = rb.CountValue
                    )
                

                testProperty
                    "map preserves count and order" <| fun (PositiveInt capacity) (items: int list) ->
                    (capacity > 0 && capacity < 100) ==> lazy (
                        let rb = RingBuffer.create capacity
                        items |> List.iter (fun item -> RingBuffer.add item rb)
                        
                        let original = RingBuffer.toArray rb
                        let mapped = RingBuffer.map (fun x -> x * 2) rb
                        
                        mapped.Length = original.Length &&
                        Array.zip original mapped |> Array.forall (fun (orig, map) -> map = orig * 2)
                    )


                testProperty 
                    "toSeq and toArray are equivalent" <| fun (PositiveInt capacity) (items: int list) ->
                    (capacity > 0 && capacity < 100) ==> lazy (
                        let rb = RingBuffer.create capacity
                        items |> List.iter (fun item -> RingBuffer.add item rb)
                        
                        let fromSeq = RingBuffer.toSeq rb |> Array.ofSeq
                        let fromArray = RingBuffer.toArray rb
                        
                        fromSeq = fromArray
                    )


                testProperty
                    "clear always resets to empty state" <| fun (PositiveInt capacity) (items: int list) ->
                    (capacity > 0 && capacity < 100 && items |> List.length < 200) ==> lazy (
                        let rb = RingBuffer.create capacity
                        items |> List.iter (fun item -> RingBuffer.add item rb)
                        
                        RingBuffer.clear rb
                        
                        rb.CountValue = 0 && 
                        not rb.IsFull && 
                        (RingBuffer.toArray rb).Length = 0
                    )


                testProperty
                    "adding items maintains newest-first property when full" <| fun (capacity: int) ->
                    (capacity > 0 && capacity <= 100) ==> lazy (
                        let rb = RingBuffer.create capacity
                        
                        // Fill beyond capacity
                        let totalItems = capacity + 5
                        for i in 1..totalItems do
                            RingBuffer.add i rb
                        
                        let items = RingBuffer.toArray rb 
                        let expectedStart = totalItems - capacity + 1
                        let expected = [|expectedStart..totalItems|]
                        
                        if not (items = expected) then printfn $"{capacity} -> items: {items}, expected: {expected}"
                        items = expected
                    )

            ]

        // Performance and stress tests
        let performanceTests =
            testList "Performance Tests" [
                
                test "large buffer operations should be fast" {
                    let capacity = 10000
                    let rb = RingBuffer.create capacity
                    
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    
                    // Add many items
                    for i in 1..50000 do
                        RingBuffer.add i rb
                    
                    sw.Stop()
                    
                    Expect.isLessThan sw.ElapsedMilliseconds 1000L "Should add 50k items quickly"
                    Expect.equal rb.CountValue capacity "Should maintain capacity"
                    
                    // Verify the buffer contains the last 'capacity' items
                    let items = RingBuffer.toArray rb
                    let expectedStart = 50000 - capacity + 1
                    Expect.equal items.[0] expectedStart "Should start with correct item"
                    Expect.equal items.[capacity - 1] 50000 "Should end with correct item"
                }
                
                test "frequent clear and refill should be efficient" {
                    let rb = RingBuffer.create 1000
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    
                    for cycle in 1..100 do
                        RingBuffer.clear rb
                        for i in 1..1000 do
                            RingBuffer.add (cycle * 1000 + i) rb
                    
                    sw.Stop()
                    
                    Expect.isLessThan sw.ElapsedMilliseconds 1000L "Should handle frequent clear/refill efficiently"
                }
                
                test "toSeq should handle large buffers efficiently" {
                    let capacity = 10000
                    let rb = RingBuffer.create capacity
                    
                    for i in 1..capacity do
                        RingBuffer.add i rb
                    
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    let items = RingBuffer.toSeq rb |> Array.ofSeq
                    sw.Stop()
                    
                    Expect.isLessThan sw.ElapsedMilliseconds 100L "Should convert to sequence quickly"
                    Expect.equal items.Length capacity "Should have correct length"
                }
            ]

        // Sequence and ordering tests
        let orderingTests =
            testList "Ordering and Sequence Tests" [
                
                test "partial fill maintains insertion order" {
                    let rb = RingBuffer.create 5
                    
                    RingBuffer.add 10 rb
                    RingBuffer.add 20 rb
                    RingBuffer.add 30 rb
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|10; 20; 30|] "Should maintain insertion order when not full"
                }
                
                test "wrap-around maintains oldest-to-newest order" {
                    let rb = RingBuffer.create 4
                    
                    // Fill completely
                    for i in 1..4 do
                        RingBuffer.add i rb
                    
                    // Add more to cause wrap-around
                    RingBuffer.add 5 rb
                    RingBuffer.add 6 rb
                    
                    let items = RingBuffer.toArray rb
                    Expect.equal items [|3; 4; 5; 6|] "Should maintain oldest-to-newest after wrap-around"
                }
                
                test "iter visits all elements exactly once" {
                    let rb = RingBuffer.create 3
                    
                    RingBuffer.add 1 rb
                    RingBuffer.add 2 rb
                    RingBuffer.add 3 rb
                    RingBuffer.add 4 rb  // Overwrites 1
                    
                    let mutable sum = 0
                    let mutable count = 0
                    
                    RingBuffer.iter (fun x -> 
                        sum <- sum + x
                        count <- count + 1) rb
                    
                    Expect.equal count 3 "Should visit exactly 3 elements"
                    Expect.equal sum 9 "Should visit 2+3+4 = 9"
                }
            ]

        // Main test suite
        [<Tests>]
        let allTests =
            testList "Informedica.Utils.Lib RingBuffer Tests" [
                basicTests
                edgeTests
                propertyTests
                performanceTests
                orderingTests
            ]

