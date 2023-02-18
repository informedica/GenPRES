namespace Informedica.ZForm.Lib


module PatientCategory =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges

    open Aether
    open Aether.Operators


    let create ga age wght bsa gend =
        {
            GestAge = ga
            Age = age
            Weight = wght
            BSA = bsa
            Gender = gend
        }


    let empty = create MinIncrMax.empty MinIncrMax.empty MinIncrMax.empty MinIncrMax.empty Undetermined


    module Optics =

        module MinMax = MinIncrMax.Optics


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


    let genderToString = function
    | Male -> "man"
    | Female -> "vrouw"
    | Undetermined -> ""

    let stringToGender s =
        match s with
        | _ when s |> String.toLower |> String.trim = "man" -> Male
        | _ when s |> String.toLower |> String.trim = "vrouw" -> Female
        | _  -> Undetermined


    let toString { GestAge = ga; Age = age; Weight = wght; BSA = bsa; Gender = gen } =
        let (>+) sl sr =
            let l, s = sr

            let s = s |> String.trim
            let sl = sl |> String.trim

            if s |> String.isNullOrWhiteSpace then sl
            else sl + (if sl = "" then " " else  ", ") + l + s

        let mmToStr = MinIncrMax.toString "van" "van" "tot" "tot"

        ""
        >+ ("Zwangerschapsduur: ", ga |> MinIncrMax.gestAgeToString)
        >+ ("Leeftijd: ", age |> MinIncrMax.ageToString)
        >+ ("Gewicht: ", wght |> mmToStr)
        >+ ("BSA: ", bsa |> mmToStr)
        >+ ("Geslacht: ", gen |> genderToString)
        |> String.removeTrailing ["\n"]


    module Dto =

        type Dto () =
            member val GestAge = MinIncrMax.Dto.dto () with get ,set
            member val Age = MinIncrMax.Dto.dto () with get ,set
            member val Weight = MinIncrMax.Dto.dto () with get ,set
            member val BSA = MinIncrMax.Dto.dto () with get ,set
            member val Gender = "" with get, set


        let dto () = Dto ()

        let toDto { GestAge = gestAge; Age = age; Weight = wght; BSA = bsa; Gender = gnd } =
            let dto = dto ()

            dto.GestAge <- gestAge |> MinIncrMax.Dto.toDto
            dto.Age <- age |> MinIncrMax.Dto.toDto
            dto.Weight <- wght |> MinIncrMax.Dto.toDto
            dto.BSA <- bsa |> MinIncrMax.Dto.toDto
            dto.Gender <- gnd |> genderToString

            dto


        let fromDto (dto : Dto) =
            let gestAge = dto.GestAge |> MinIncrMax.Dto.fromDto
            let age = dto.Age |> MinIncrMax.Dto.fromDto
            let wght = dto.Weight |> MinIncrMax.Dto.fromDto
            let bsa = dto.BSA |> MinIncrMax.Dto.fromDto
            let gnd = dto.Gender |> stringToGender

            match gestAge, age, wght, bsa with
            | Some ga, Some age, Some wght, Some bsa ->
                create ga age wght bsa gnd
                |> Some
            | _ -> None


