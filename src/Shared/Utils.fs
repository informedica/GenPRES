namespace Shared


module String =


    open System


    let isNullOrWhiteSpace (s: String) = String.IsNullOrWhiteSpace(s)


    let replace (s1: string) s2 (s: string) = s.Replace(s1, s2)


    let split (del: string) (s: string) = s.Split(del)



module NormalValues =


    let ageWeight =
        [
            0., 3.84
            0.7, 3.84
            1.4, 4.53
            2.1, 5.13
            2.8, 5.66
            4.2, 6.62
            6., 7.61
            7.4, 8.24
            9.2, 9.05
            11.1, 9.75
            12., 10.1
            18., 11.8
            24., 13.1
            30., 14.3
            36., 15.4
            42., 16.5
            48., 17.6
            54., 18.6
            60., 19.6
            66., 20.6
            72., 21.7
            78., 22.9
            84., 24.2
            90., 25.5
            96., 26.8
            102., 28.3
            108., 29.8
            114., 31.4
            120., 33.
            126., 34.6
            132., 36.3
            138., 38.1
            144., 39.9
            150., 42.
            156., 44.5
            162., 47.7
            168., 50.9
            174., 54.
            180., 57.1
            186., 59.7
            192., 62.1
            198., 64.1
            204., 65.9
            210., 67.6
            216., 69.
            222., 70.2
            228., 71.2
            234., 72.
        ]


    let ageHeight =
        [
            0., 50.
            0.6792, 52.95
            3., 52.95
            6., 60.5
            9., 67.25
            12., 71.8
            18., 75.6
            24., 82.3
            30., 88.15
            36., 93.1
            42., 97.6
            48., 101.55
            54., 105.25
            60., 108.8
            66., 112.15
            72., 115.35
            78., 118.55
            84., 121.7
            90., 124.7
            96., 127.7
            102., 130.7
            108., 133.5
            114., 136.2
            120., 139.
            126., 141.8
            132., 144.5
            138., 147.25
            144., 150.15
            150., 153.25
            156., 156.25
            162., 159.05
            168., 161.95
            174., 164.9
            180., 167.45
            186., 169.8
            192., 171.5
            198., 172.55
            204., 173.25
            210., 173.8
            216., 174.2
            222., 174.55
            228., 174.85
            234., 175.05
            240., 175.15
        ]


    let heartRate =
        [
            1., "90-205"
            12., "90-190"
            36., "80-140"
            72., "65-120"
            144., "58-118"
            180., "50-100"
            228., "50-100"
        ]


    let respRate =
        [
            12., "30-53"
            36., "22-37"
            72., "20-28"
            144., "18-25"
            228., "12-20"
        ]


    let sbp =
        [
            1., "60-84"
            12., "72-104"
            36., "86-106"
            72., "89-112"
            120., "97-115"
            144., "102-120"
            228., "110-131"
        ]


    let dbp =
        [
            1., "31-53"
            12., "37-56"
            36., "42-63"
            72., "46-72"
            120., "57-76"
            144., "61-80"
            228., "64-83"
        ]


    let gcs =
        [
            // Pediatric
            5 * 12,
            [
                "Eye Opening",
                [
                    (4, "spontaan")
                    (3, "bij geluid")
                    (2, "bij pijn")
                    (1, "geen")
                    (0, "zwelling of verband")
                ]
                "Motor Response",
                [
                    (6, "normale spontane bewegingen")
                    (5, "lokaliseert bij pijn")
                    (4, "trekt terug bij pijn")
                    (3, "flexie bij pijn")
                    (2, "extensie bij pijn")
                    (1, "geen reactie op pijn")
                ]
                "Verbal Response",
                [
                    (5, "alert, brabbelt, conform leeftijd")
                    (4, "minder, geirriteerd huilen")
                    (3, "huilt bij pijn")
                    (2, "kreunt bij pijn")
                    (1, "geen reactie op pijn")
                    (0, "tube")
                ]
            ]
            // Adult
            19 * 12,
            [
                "Eye Opening",
                [
                    (4, "spontaan")
                    (3, "bij geluid")
                    (2, "bij pijn")
                    (1, "geen")
                    (0, "zwelling of verband")
                ]
                "Motor Response",
                [
                    (6, "normale spontane bewegingen")
                    (5, "lokaliseert bij pijn")
                    (4, "trekt terug bij pijn")
                    (3, "flexie bij pijn")
                    (2, "extensie bij pijn")
                    (1, "geen reactie op pijn")
                ]
                "Verbal Response",
                [
                    (5, "voert opdrachten uit")
                    (4, "verward")
                    (3, "niet adequaat")
                    (2, "niet verstaanbaar")
                    (1, "geen reactie op pijn")
                    (0, "tube")
                ]
            ]
        ]


module Csv =

    open System
    open Types


    let tryCast dt (x: string) =
        match dt with
        | StringData -> box (x.Trim())
        | FloatData ->
            match Double.TryParse(x) with
            | true, n -> n |> box
            | _ ->
                $"cannot parse {x} to double"
                |> failwith
        | FloatOptionData ->
            match Double.TryParse(x) with
            | true, n -> n |> Some |> box
            | _ -> None |> box


    let getColumn dt columns sl s =
        columns
        |> Array.tryFindIndex ((=) s)
        |> function
            | None ->
                $"""cannot find column {s} in {columns |> String.concat ", "}"""
                |> failwith
            | Some i ->
                sl
                |> Array.item i
                |> tryCast dt


    let getStringColumn columns sl s =
        getColumn StringData columns sl s |> unbox<string>


    let getFloatColumn columns sl s =
        getColumn FloatData columns sl s |> unbox<float>


    let getFloatOptionColumn columns sl s =
        getColumn FloatOptionData columns sl s
        |> unbox<float option>


    let parseCSV (s: string) =
        s.Split("\n")
        |> Array.filter (String.isNullOrWhiteSpace >> not)
        |> Array.map (String.replace "\",\"" "|")
        |> Array.map (String.replace "\"" "")
        |> Array.map (fun s ->
            s.Split("|")
            |> Array.map (fun s -> s.Trim())
        )