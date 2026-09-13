namespace Informedica.GenPRES.Shared.Tests


/// One language vocabulary: `Localization.tryParse` serves the `GENPRES_LANG` setting and the
/// client's `la` url parameter (the sheet header keeps `tryFromString`, display names only).
module LocalizationTests =

    open Expecto
    open Expecto.Flip
    open Shared.Localization


    [<Tests>]
    let tests =
        testList
            "Localization.tryParse"
            [
                test "every locale round-trips through its short code, upper or lower case" {
                    for l in languages do
                        l |> toShortCode |> tryParse |> Expect.equal $"{l} upper" (Some l)

                        l
                        |> toShortCode
                        |> _.ToLower()
                        |> tryParse
                        |> Expect.equal $"{l} lower" (Some l)
                }

                test "every locale round-trips through its display name" {
                    for l in languages do
                        l |> toString |> tryParse |> Expect.equal $"{l}" (Some l)
                }

                testList
                    "the client's legacy url codes"
                    [
                        for code, l in
                            [
                                "en", English
                                "du", Dutch
                                "fr", French
                                "gr", German
                                "sp", Spanish
                                "it", Italian
                            ] do
                            test code { code |> tryParse |> Expect.equal code (Some l) }
                    ]

                test "whitespace is ignored" { "  nl " |> tryParse |> Expect.equal "padded" (Some Dutch) }

                test "unknown, empty and null are None" {
                    for s in [ "xx"; ""; " "; "dutch"; null ] do
                        s |> tryParse |> Expect.isNone $"'{s}'"
                }
            ]
