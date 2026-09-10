namespace Informedica.GenPRES.Shared.Tests


/// The language precedence, linked in from the client project: url `la` and the User's choice
/// outrank the server default, whichever order the messages arrive in; a navigation without
/// `la` keeps the language.
module LanguagePolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Localization
    open LanguagePolicy


    [<Tests>]
    let tests =
        testList
            "LanguagePolicy"
            [
                test "no url language: the fallback, not chosen" {
                    Language.initial None
                    |> Expect.equal
                        "fallback"
                        {
                            Current = Language.fallback
                            Chosen = false
                        }
                }

                test "a url language is chosen from the start" {
                    Language.initial (Some English)
                    |> Expect.equal
                        "chosen"
                        {
                            Current = English
                            Chosen = true
                        }
                }

                test "the server default applies when nothing was chosen" {
                    Language.initial None
                    |> Language.onServerDefault English
                    |> Expect.equal
                        "server default"
                        {
                            Current = English
                            Chosen = false
                        }
                }

                test "settings arriving after a url language do not override it" {
                    Language.initial (Some Dutch)
                    |> Language.onServerDefault English
                    |> Expect.equal
                        "url wins"
                        {
                            Current = Dutch
                            Chosen = true
                        }
                }

                test "settings arriving after the User's choice do not override it" {
                    Language.initial None
                    |> Language.choose French
                    |> Language.onServerDefault English
                    |> Expect.equal
                        "choice wins"
                        {
                            Current = French
                            Chosen = true
                        }
                }

                test "the User's choice after the settings overrides the server default" {
                    Language.initial None
                    |> Language.onServerDefault English
                    |> Language.choose French
                    |> Expect.equal
                        "choice wins"
                        {
                            Current = French
                            Chosen = true
                        }
                }

                test "a navigation without la keeps the current language, chosen or not" {
                    Language.initial None
                    |> Language.onServerDefault English
                    |> Language.onUrl None
                    |> Expect.equal
                        "kept, not chosen"
                        {
                            Current = English
                            Chosen = false
                        }

                    Language.initial None
                    |> Language.choose French
                    |> Language.onUrl None
                    |> Expect.equal
                        "kept, chosen"
                        {
                            Current = French
                            Chosen = true
                        }
                }

                test "a navigation with la changes the language and counts as chosen" {
                    Language.initial None
                    |> Language.onServerDefault English
                    |> Language.onUrl (Some Dutch)
                    |> Expect.equal
                        "url"
                        {
                            Current = Dutch
                            Chosen = true
                        }
                }

                test "settings arriving after a navigation with la do not override it" {
                    Language.initial None
                    |> Language.onUrl (Some Dutch)
                    |> Language.onServerDefault English
                    |> Expect.equal
                        "url wins"
                        {
                            Current = Dutch
                            Chosen = true
                        }
                }
            ]
