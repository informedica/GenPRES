namespace Informedica.Utils.Lib


module Csv =

    open Informedica.Utils.Lib.BCL


    type DataType =
        | StringData
        | FloatData
        | FloatOptionData
        | Int32Data
        | Int32OptionData
        | DecimalData
        | DecimalOptionData


    /// Flexible parsing function with optional option wrapping and error handling.
    ///
    /// This function attempts parsing using the provided `tryParse` function.
    /// Depending on the `isOption` flag, the parsed value can be wrapped in an `Option` or not.
    /// If parsing fails, it handles errors by returning `None` or throwing an exception.
    ///
    /// Parameters:
    ///   - isOption: Flag indicating whether to wrap the parsed value in an `Option`.
    ///   - typeDescr: Description of the target type for error messages.
    ///   - tryParse: Function attempting to parse the input value and returning an `Option`.
    ///   - x: Input value to be parsed.
    ///
    /// Returns:
    ///   - Parsed value (wrapped in `Option` if `isOption` is true).
    ///
    /// Exceptions:
    ///   - Throws an exception with error message if parsing fails and `isOption` is false.
    let inline parse isOption typeDescr tryParse x =
        match tryParse x with
        | Some n ->
            if not isOption then box n
            else
                n
                |> Some
                |> box
        | None ->
            if isOption then None
            else
                $"cannot parse {x} to {typeDescr}"
                |> failwith


    /// Try to cast a string to a value of the given type.
    /// Example: "123" |> tryCast Int32Data will return 123.
    let inline tryCast<'T> dt (x: string) =

        match dt with
        | StringData -> box (x.Trim())
        | Int32Data -> parse false "int32" Int32.tryParse x
        | Int32OptionData -> parse true  "int32" Int32.tryParse x
        | FloatData -> parse false "double" Double.tryParse x
        | FloatOptionData -> parse true "double" Double.tryParse x
        | DecimalData -> parse false "decimal" Decimal.tryParse x
        | DecimalOptionData -> parse true "decimal" Decimal.tryParse x
        |> unbox<'T>


    /// Get a column from a row of a CSV file.
    /// Example: getColumn StringData [| "a"; "b" |] [| "1"; "2" |] "a" will return "1".
    let inline getColumn<'T> dataType columns row name =
        columns
        |> Array.tryFindIndex ((=) name)
        |> function
            | None ->
                $"""cannot find column {name} in {columns |> String.concat ", "}"""
                |> failwith
            | Some i ->
                row
                |> Array.item i
                |> tryCast<'T> dataType


    let getStringColumn columns sl s =
        getColumn<string> StringData columns sl s


    let getInt32Column columns sl s =
        getColumn<int> Int32Data columns sl s


    let getInt32OptionColumn columns sl s =
        getColumn<int option> Int32OptionData columns sl s


    let getFloatColumn columns sl s =
        getColumn<float> FloatData columns sl s


    let getFloatOptionColumn columns sl s =
        getColumn<float option> FloatOptionData columns sl s


    let getDecimalColumn columns sl s =
        getColumn<decimal> DecimalData columns sl s


    let getDecimalOptionColumn columns sl s =
        getColumn<decimal option> DecimalOptionData columns sl s


    let parseCSV (s: string) =
        s.Split("\n")
        |> Array.filter (String.isNullOrWhiteSpace >> not)
        // replace comma between quotes with a special character
        |> Array.map (String.replace "\",\"" "")
        // remove quotes
        |> Array.map (String.replace "\"" "")
        // split on special character
        |> Array.map (fun s ->
            s.Split("")
            |> Array.map (fun s -> s.Trim())
        )



    module Tests =

        open Swensen.Unquote


        // Test tryCast
        let testTryCast () =
            test <@ "123" |> tryCast Int32Data = 123 @>


        // Test parseCSV
        let testParseCSV () =
            let testCsv = "a\",\"b\",\"c\n1\",\"2\",\"3\n4\",\"5\",\"6"
            test <@ parseCSV testCsv = [|
                [| "a"; "b"; "c" |]
                [| "1"; "2"; "3" |]
                [| "4"; "5"; "6" |]
            |] @>


        // Test getStringColumn
        let testGetStringColumn () =
            test <@ getStringColumn [| "a"; "b" |] [| "1"; "2" |] "a" = "1" @>
            test <@ getStringColumn [| "a"; "b" |] [| "1"; "2" |] "b" = "2" @>
            Assertions.raises<System.Exception> <@ getStringColumn [| "a"; "b" |] [| "1"; "2" |] "c" = "1" @>


        // Test all
        let testAll () =
            testTryCast()
            testParseCSV()
            testGetStringColumn()
            ()