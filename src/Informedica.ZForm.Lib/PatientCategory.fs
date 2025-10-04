namespace Informedica.ZForm.Lib


module PatientCategory =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Aether
    open Aether.Operators


    /// Create a PatientCategory.
    let create ga age wght bsa gend =
        {
            GestAge = ga
            Age = age
            Weight = wght
            BSA = bsa
            Gender = gend
        }


    /// An empty PatientCategory.
    let empty = create MinMax.empty MinMax.empty MinMax.empty MinMax.empty Undetermined


    module Optics =

        module MinMax = MinMax.Optics


        let setGender = Optic.set PatientCategory.Gender_


        let inclMinGestAge =
            PatientCategory.GestAge_ >-> MinMax.inclMinLens


        let setInclMinGestAge = Optic.set inclMinGestAge


        let exclMinGestAge =
            PatientCategory.GestAge_ >-> MinMax.exclMinLens


        let setExclMinGestAge = Optic.set exclMinGestAge


        let inclMaxGestAge =
            PatientCategory.GestAge_ >-> MinMax.inclMaxLens


        let setInclMaxGestAge = Optic.set inclMaxGestAge


        let exclMaxGestAge =
            PatientCategory.GestAge_ >-> MinMax.exclMaxLens


        let setExclMaxGestAge = Optic.set exclMaxGestAge


        let inclMinAge =
            PatientCategory.Age_ >-> MinMax.inclMinLens


        let setInclMinAge = Optic.set inclMinAge


        let exclMinAge =
            PatientCategory.Age_ >-> MinMax.exclMinLens


        let setExclMinAge = Optic.set exclMinAge


        let inclMaxAge =
            PatientCategory.Age_ >-> MinMax.inclMaxLens


        let setInclMaxAge = Optic.set inclMaxAge


        let exclMaxAge =
            PatientCategory.Age_ >-> MinMax.exclMaxLens


        let setExclMaxAge = Optic.set exclMaxAge


        let inclMinWeight =
            PatientCategory.Weight_ >-> MinMax.inclMinLens


        let setInclMinWeight = Optic.set inclMinWeight


        let exclMinWeight =
            PatientCategory.Weight_ >-> MinMax.exclMinLens


        let setExclMinWeight = Optic.set exclMinWeight


        let inclMaxWeight =
            PatientCategory.Weight_ >-> MinMax.inclMaxLens


        let setInclMaxWeight = Optic.set inclMaxWeight


        let exclMaxWeight =
            PatientCategory.Weight_ >-> MinMax.exclMaxLens


        let setExclMaxWeight = Optic.set exclMaxWeight


        let inclMinBSA =
            PatientCategory.BSA_ >-> MinMax.inclMinLens


        let setInclMinBSA = Optic.set inclMinBSA


        let exclMinBSA =
            PatientCategory.BSA_ >-> MinMax.exclMinLens


        let setExclMinBSA = Optic.set exclMinBSA


        let inclMaxBSA =
            PatientCategory.BSA_ >-> MinMax.inclMaxLens


        let setInclMaxBSA = Optic.set inclMaxBSA


        let exclMaxBSA =
            PatientCategory.BSA_ >-> MinMax.exclMaxLens


        let setExclMaxBSA = Optic.set exclMaxBSA


    /// Get the string representation of a Gener.
    let genderToString = function
    | Male -> "man"
    | Female -> "vrouw"
    | Undetermined -> ""

    /// Create a Gender from a string.
    let stringToGender s =
        match s with
        | _ when s |> String.toLower |> String.trim = "man" -> Male
        | _ when s |> String.toLower |> String.trim = "vrouw" -> Female
        | _  -> Undetermined


    /// Get the string representation of a PatientCategory.
    let toString { GestAge = ga; Age = age; Weight = wght; BSA = bsa; Gender = gen } =
        let (>+) sl sr =
            let l, s = sr

            let s = s |> String.trim
            let sl = sl |> String.trim

            if s |> String.isNullOrWhiteSpace then sl
            else sl + (if sl = "" then " " else  ", ") + l + s

        let mmToStr =
            MinMax.toString
                (ValueUnit.toStringDecimalDutchShortWithPrec 2)
                "van "
                "van "
                "tot "
                "tot "

        ""
        >+ ("Zwangerschapsduur: ", ga |> MinMax.gestAgeToString)
        >+ ("Leeftijd: ", age |> MinMax.ageToString)
        >+ ("Gewicht: ", wght |> mmToStr)
        >+ ("BSA: ", bsa |> mmToStr)
        >+ ("Geslacht: ", gen |> genderToString)
        |> String.removeTrailing ["\n"]


    module Dto =

        type Dto () =
            member val GestAge = MinMax.Dto.dto () with get ,set
            member val Age = MinMax.Dto.dto () with get ,set
            member val Weight = MinMax.Dto.dto () with get ,set
            member val BSA = MinMax.Dto.dto () with get ,set
            member val Gender = "" with get, set


        let dto () = Dto ()

        let toDto { GestAge = gestAge; Age = age; Weight = wght; BSA = bsa; Gender = gnd } =
            let dto = dto ()

            dto.GestAge <- gestAge |> MinMax.Dto.toDto
            dto.Age <- age |> MinMax.Dto.toDto
            dto.Weight <- wght |> MinMax.Dto.toDto
            dto.BSA <- bsa |> MinMax.Dto.toDto
            dto.Gender <- gnd |> genderToString

            dto


        let fromDto (dto : Dto) =
            let gestAge = dto.GestAge |> MinMax.Dto.fromDto
            let age = dto.Age |> MinMax.Dto.fromDto
            let wght = dto.Weight |> MinMax.Dto.fromDto
            let bsa = dto.BSA |> MinMax.Dto.fromDto
            let gnd = dto.Gender |> stringToGender

            match gestAge, age, wght, bsa with
            | Some ga, Some age, Some wght, Some bsa ->
                create ga age wght bsa gnd
                |> Some
            | _ -> None


