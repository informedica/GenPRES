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


    let inline parse b t p x =
        match p x with
        | Some n ->
            if not b then box n
            else
                n
                |> Some
                |> box
        | None ->
            if b then None
            else
                $"cannot parse {x} to {t}"
                |> failwith


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


    let inline getColumn<'T> dt columns sl s =
        columns
        |> Array.tryFindIndex ((=) s)
        |> function
            | None ->
                $"""cannot find column {s} in {columns |> String.concat ", "}"""
                |> failwith
            | Some i ->
                sl
                |> Array.item i
                |> tryCast<'T> dt


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
        |> Array.map (String.replace "\",\"" "")
        |> Array.map (String.replace "\"" "")
        |> Array.map (fun s ->
            s.Split("")
            |> Array.map (fun s -> s.Trim())
        )



