
#load "../../../scripts/Expecto.fsx"


#load "load.fsx"

open Expecto



module Tests =

    open System
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


        /// Create the necessary test generators
        module Generators =

            open FsCheck

            let bigRGen (n, d) =
                    let d = if d = 0 then 1 else d
                    let n' = abs(n) |> BigRational.FromInt
                    let d' = abs(d) |> BigRational.FromInt
                    n'/d'

            let bigRGenerator =
                gen {
                    let! n = Arb.generate<int>
                    let! d = Arb.generate<int>
                    return bigRGen(n, d)
                }

            type MyGenerators () =
                static member BigRational () =
                    { new Arbitrary<BigRational>() with
                        override x.Generator = bigRGenerator }


        [<Tests>]
        let tests =

            let equals exp txt res = Expect.equal res exp txt

            let config =
                { FsCheckConfig.defaultConfig with
                    maxTest = 10000
                    arbitrary = [ typeof<Generators.MyGenerators> ] }

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
                        let a = b |> BigRational.toMultipleOf c
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
                        Swensen.Unquote.Assertions.test<@ s1 = "trailing " @>
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
                        Swensen.Unquote.Assertions.test<@ r = "trailing spaces" @>
                }

                test "without option fails" {
                    try
                        "cannot cast to integer"
                        |> Csv.tryCast<int> Csv.Int32Data
                        |> ignore
                        Swensen.Unquote.Assertions.test<@ false @>
                    with
                    | _ ->
                        Swensen.Unquote.Assertions.test<@ true @>
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



[
    Tests.testHelloWorld
    Tests.String.tests
    Tests.Double.tests
    Tests.BigRational.tests
    Tests.DateTime.tests
    Tests.List.tests
    Tests.Reflection.tests
    Tests.Csv.tests
]
//|> List.skip 4
//|> List.take 1
|> testList "Tests"
|> Expecto.run




