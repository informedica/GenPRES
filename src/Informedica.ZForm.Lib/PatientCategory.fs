namespace Informedica.ZForm.Lib


module PatientCategory =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


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


        let setGender g (pc: PatientCategory) = { pc with Gender = g }


        let setInclMinGestAge vu (pc: PatientCategory) =
            { pc with GestAge = pc.GestAge |> (snd MinMax.inclMinLens) vu }


        let setExclMinGestAge vu (pc: PatientCategory) =
            { pc with GestAge = pc.GestAge |> (snd MinMax.exclMinLens) vu }


        let setInclMaxGestAge vu (pc: PatientCategory) =
            { pc with GestAge = pc.GestAge |> (snd MinMax.inclMaxLens) vu }


        let setExclMaxGestAge vu (pc: PatientCategory) =
            { pc with GestAge = pc.GestAge |> (snd MinMax.exclMaxLens) vu }


        let setInclMinAge vu (pc: PatientCategory) =
            { pc with Age = pc.Age |> (snd MinMax.inclMinLens) vu }


        let setExclMinAge vu (pc: PatientCategory) =
            { pc with Age = pc.Age |> (snd MinMax.exclMinLens) vu }


        let setInclMaxAge vu (pc: PatientCategory) =
            { pc with Age = pc.Age |> (snd MinMax.inclMaxLens) vu }


        let setExclMaxAge vu (pc: PatientCategory) =
            { pc with Age = pc.Age |> (snd MinMax.exclMaxLens) vu }


        let setInclMinWeight vu (pc: PatientCategory) =
            { pc with Weight = pc.Weight |> (snd MinMax.inclMinLens) vu }


        let setExclMinWeight vu (pc: PatientCategory) =
            { pc with Weight = pc.Weight |> (snd MinMax.exclMinLens) vu }


        let setInclMaxWeight vu (pc: PatientCategory) =
            { pc with Weight = pc.Weight |> (snd MinMax.inclMaxLens) vu }


        let setExclMaxWeight vu (pc: PatientCategory) =
            { pc with Weight = pc.Weight |> (snd MinMax.exclMaxLens) vu }


        let setInclMinBSA vu (pc: PatientCategory) =
            { pc with BSA = pc.BSA |> (snd MinMax.inclMinLens) vu }


        let setExclMinBSA vu (pc: PatientCategory) =
            { pc with BSA = pc.BSA |> (snd MinMax.exclMinLens) vu }


        let setInclMaxBSA vu (pc: PatientCategory) =
            { pc with BSA = pc.BSA |> (snd MinMax.inclMaxLens) vu }


        let setExclMaxBSA vu (pc: PatientCategory) =
            { pc with BSA = pc.BSA |> (snd MinMax.exclMaxLens) vu }


    /// Get the string representation of a Gener.
    let genderToString =
        function
        | Male -> "man"
        | Female -> "vrouw"
        | Undetermined -> ""

    /// Create a Gender from a string.
    let stringToGender s =
        match s with
        | _ when s |> String.toLower |> String.trim = "man" -> Male
        | _ when s |> String.toLower |> String.trim = "vrouw" -> Female
        | _ -> Undetermined


    /// Get the string representation of a PatientCategory.
    let toString
        {
            GestAge = ga
            Age = age
            Weight = wght
            BSA = bsa
            Gender = gen
        }
        =
        let (>+) sl sr =
            let l, s = sr

            let s = s |> String.trim
            let sl = sl |> String.trim

            if s |> String.isNullOrWhiteSpace then
                sl
            else
                sl + (if sl = "" then " " else ", ") + l + s

        let mmToStr =
            MinMax.toString
                (ValueUnit.toStringDecimalDutchShortWithPrec 2)
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
        |> String.removeTrailing [ "\n" ]


    module Dto =

        type Dto() =
            member val GestAge = MinMax.Dto.dto () with get, set
            member val Age = MinMax.Dto.dto () with get, set
            member val Weight = MinMax.Dto.dto () with get, set
            member val BSA = MinMax.Dto.dto () with get, set
            member val Gender = "" with get, set


        let dto () = Dto()

        let toDto
            {
                GestAge = gestAge
                Age = age
                Weight = wght
                BSA = bsa
                Gender = gnd
            }
            =
            let dto = dto ()

            dto.GestAge <- gestAge |> MinMax.Dto.toDto
            dto.Age <- age |> MinMax.Dto.toDto
            dto.Weight <- wght |> MinMax.Dto.toDto
            dto.BSA <- bsa |> MinMax.Dto.toDto
            dto.Gender <- gnd |> genderToString

            dto


        let fromDto (dto: Dto) =
            let gestAge = dto.GestAge |> MinMax.Dto.fromDto
            let age = dto.Age |> MinMax.Dto.fromDto
            let wght = dto.Weight |> MinMax.Dto.fromDto
            let bsa = dto.BSA |> MinMax.Dto.fromDto
            let gnd = dto.Gender |> stringToGender

            match gestAge, age, wght, bsa with
            | Some ga, Some age, Some wght, Some bsa -> create ga age wght bsa gnd |> Some
            | _ -> None
