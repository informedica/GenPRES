namespace Informedica.GenForm.Lib


module DoseType =


    open Informedica.Utils.Lib.BCL


    let sortBy = function
        | Start -> 0
        | Once -> 1
        | PRN -> 2
        | Maintenance -> 3
        | Continuous -> 4
        | StepUp n -> 50 + n
        | StepDown n -> 100 + n
        | AnyDoseType -> 200
        | Contraindicated -> -1


    let fromString s =
        let s = s |> String.toLower |> String.trim

        match s with
        | "start" -> Start
        | "eenmalig" -> Once
        | "prn" -> PRN
        | "onderhoud" -> Maintenance
        | "continu" -> Continuous
        | "contra" -> Contraindicated

        | _ when s |> String.startsWith "afbouw" ->
            match s |> String.split(" ") with
            | [_;i] ->
                match i |> Int32.tryParse with
                | Some i -> StepDown i
                | None ->
                    printfn $"DoseType.fromString couldn't match {s}"
                    AnyDoseType
            | _ ->
                printfn $"DoseType.fromString couldn't match {s}"
                AnyDoseType

        | _ when s |> String.startsWith "opbouw" ->
            match s |> String.split(" ") with
            | [_;i] ->
                match i |> Int32.tryParse with
                | Some i -> StepUp i
                | None ->
                    printfn $"DoseType.fromString couldn't match {s}"
                    AnyDoseType
            | _ ->
                printfn $"DoseType.fromString couldn't match {s}"
                AnyDoseType

        | _ when s |> String.isNullOrWhiteSpace -> AnyDoseType

        | _ ->
            printfn $"DoseType.fromString couldn't match {s}"
            AnyDoseType


    let toString = function
        | Start -> "start"
        | Once -> "eenmalig"
        | PRN -> "prn"
        | Maintenance -> "onderhoud"
        | Continuous -> "continu"
        | StepDown i -> $"afbouw {i}"
        | StepUp i -> $"opbouw {i}"
        | Contraindicated -> "contra"
        | AnyDoseType -> ""

