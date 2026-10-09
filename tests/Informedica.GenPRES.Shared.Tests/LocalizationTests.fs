namespace Informedica.GenPRES.Shared.Tests


/// One language vocabulary: `Localization.tryParse` serves the `GENPRES_LANG` setting and the
/// client's `lan` url parameter, ISO 639-1 codes only (the sheet header keeps `tryFromString`,
/// display names only).
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

                testList
                    "the ISO 639-1 codes"
                    [
                        for code, l in
                            [
                                "en", English
                                "nl", Dutch
                                "fr", French
                                "de", German
                                "es", Spanish
                                "it", Italian
                            ] do
                            test code { code |> tryParse |> Expect.equal code (Some l) }
                    ]

                test "the earlier url codes and the display names are not read" {
                    for s in [ "du"; "gr"; "sp"; "Nederlands"; "English" ] do
                        s |> tryParse |> Expect.isNone s
                }

                test "whitespace is ignored" { "  nl " |> tryParse |> Expect.equal "padded" (Some Dutch) }

                test "unknown, empty and null are None" {
                    for s in [ "xx"; ""; " "; "dutch"; null ] do
                        s |> tryParse |> Expect.isNone $"'{s}'"
                }
            ]
