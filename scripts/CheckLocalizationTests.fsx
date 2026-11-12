// Tests of the scan in scripts/CheckLocalization.fsx: the parse of the file, the report against
// the term keys and the locale columns, and the file itself against the Terms union.
//
// Run: dotnet fsi scripts/CheckLocalizationTests.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "CheckLocalization.fsx"

open CheckLocalization
open Expecto
open Expecto.Flip


let row term cells = { Term = term; Cells = cells }


let header = [ "Term"; "English"; "Dutch" ]


let locales = header.Tail


let tests =
    testList
        "CheckLocalization"
        [
            test "parse reads the header and the rows" {
                [ "Term\tEnglish\tDutch"; "A\ta\t"; "B\tb\tbb" ]
                |> parse
                |> Expect.equal
                    "header and two rows"
                    {
                        Header = header
                        Rows = [ row "A" [ "a"; "" ]; row "B" [ "b"; "bb" ] ]
                    }
            }

            test "parse skips empty lines" {
                [ "Term\tEnglish\tDutch"; "A\ta\t"; "" ] |> parse |> _.Rows |> List.length |> Expect.equal "one row" 1
            }

            test "parse fails on a header without Term" {
                (fun () -> [ "Key\tEnglish" ] |> parse |> ignore) |> Expect.throws "not Term"
            }

            test "parse fails on a row with the wrong number of cells" {
                (fun () -> [ "Term\tEnglish\tDutch"; "A\ta" ] |> parse |> ignore)
                |> Expect.throws "two cells, three headers"
            }

            test "parse fails on a repeated term" {
                (fun () -> [ "Term\tEnglish"; "A\ta"; "A\tb" ] |> parse |> ignore) |> Expect.throws "A twice"
            }

            test "scan names the terms without a row, in union order" {
                { Header = header; Rows = [ row "B" [ "b"; "bb" ] ] }
                |> scan locales [ "C"; "B"; "A" ]
                |> _.Missing
                |> Expect.equal "C then A" [ "C"; "A" ]
            }

            test "scan names the stale rows" {
                { Header = header; Rows = [ row "A" [ "a"; "aa" ]; row "Old" [ "o"; "oo" ] ] }
                |> scan locales [ "A" ]
                |> _.Stale
                |> Expect.equal "Old" [ "Old" ]
            }

            test "scan names the locales without a column" {
                { Header = header; Rows = [ row "A" [ "a"; "aa" ] ] }
                |> scan [ "English"; "Dutch"; "French" ] [ "A" ]
                |> _.MissingColumns
                |> Expect.equal "French" [ "French" ]
            }

            test "scan names the empty cells per locale" {
                { Header = header; Rows = [ row "A" [ "a"; "" ]; row "B" [ ""; " " ] ] }
                |> scan locales [ "A"; "B" ]
                |> _.EmptyCells
                |> Expect.equal "B in English, A and B in Dutch" [ "English", [ "B" ]; "Dutch", [ "A"; "B" ] ]
            }

            test "scan names a row with a tab or a line break" {
                for bad in [ "a\tb"; "a\nb"; "a\rb" ] do
                    { Header = header; Rows = [ row "A" [ bad; "aa" ] ] }
                    |> scan locales [ "A" ]
                    |> _.Unwritable
                    |> Expect.equal $"%A{bad}" [ "A" ]
            }

            test "a file that covers the union passes, with stale rows and empty cells reported" {
                let report =
                    { Header = header; Rows = [ row "A" [ "a"; "" ]; row "Old" [ "o"; "oo" ] ] }
                    |> scan locales [ "A" ]

                report |> passes |> Expect.isTrue "passes"
                report.Stale |> Expect.equal "Old reported" [ "Old" ]
                report.EmptyCells |> Expect.equal "A's Dutch reported" [ "English", []; "Dutch", [ "A" ] ]
            }

            test "a missing term, a missing column or an unwritable cell fails" {
                { Header = header; Rows = [] } |> scan locales [ "A" ] |> passes |> Expect.isFalse "missing term"

                { Header = header; Rows = [ row "A" [ "a"; "aa" ] ] }
                |> scan [ "English"; "Dutch"; "French" ] [ "A" ]
                |> passes
                |> Expect.isFalse "missing column"

                { Header = header; Rows = [ row "A" [ "a\tb"; "aa" ] ] }
                |> scan locales [ "A" ]
                |> passes
                |> Expect.isFalse "unwritable"
            }

            test "toLines and parse round trip" {
                let file = { Header = header; Rows = [ row "A" [ "a"; "" ]; row "B" [ ""; "bb" ] ] }

                file |> toLines |> parse |> Expect.equal "same file" file
            }

            test "the locale columns are the Locales cases in getTerm's order" {
                localeColumns ()
                |> Expect.equal "six locales" [ "English"; "Dutch"; "French"; "German"; "Spanish"; "Italian" ]
            }

            test "the union holds no repeated term key" {
                termKeys () |> List.distinct |> List.length |> Expect.equal "all distinct" (termKeys () |> List.length)
            }

            test "fallbacks finds the three ways the code writes a fallback, with their lines" {
                let source =
                    String.concat
                        "\n"
                        [
                            "let a = Terms.``Order Dose`` |> getTerm \"dose\""
                            "let b ="
                            "    Delete"
                            "    |> getTerm"
                            "        \"Delete\""
                            "    | Terms.Cancel -> \"Cancel\""
                            "    terms \"Enter patient data\" Terms.``Patient enter patient data``"
                        ]

                source
                |> fallbacks [ "Order Dose"; "Delete"; "Cancel"; "Patient enter patient data" ] "View.fs"
                |> List.map (fun f -> f.Line, f.Term, f.Text)
                |> Expect.equal
                    "four fallbacks"
                    [
                        1, "Order Dose", "dose"
                        5, "Delete", "Delete"
                        6, "Cancel", "Cancel"
                        7, "Patient enter patient data", "Enter patient data"
                    ]
            }

            test "fallbacks leaves out a name that is no term" {
                "Severity.Low |> getTerm \"low\"\n| Terms.Other -> \"other\""
                |> fallbacks [ "Cancel" ] "View.fs"
                |> Expect.isEmpty "no term"
            }

            test "fallbacks reads an escaped quote in the text" {
                "Terms.Quote |> getTerm \"say \\\"hi\\\"\""
                |> fallbacks [ "Quote" ] "View.fs"
                |> List.map _.Text
                |> Expect.equal "the quotes" [ "say \"hi\"" ]
            }

            test "wrongFallbacks names a fallback that differs from the English cell" {
                let file = { Header = header; Rows = [ row "A" [ "Delete"; "Verwijder" ]; row "B" [ "Edit"; "Bewerk" ] ] }

                [
                    { File = "View.fs"; Line = 3; Term = "A"; Text = "Verwijder" }
                    { File = "View.fs"; Line = 4; Term = "B"; Text = "Edit" }
                    { File = "View.fs"; Line = 5; Term = "C"; Text = "no row" }
                ]
                |> wrongFallbacks file
                |> List.map (fun (f, english) -> f.Term, english)
                |> Expect.equal "A only" [ ("A", "Delete") ]
            }

            test "every fallback in the client equals the English cell of its term" {
                let file = localizationPath () |> readLocalization
                let found = clientFallbacks (termKeys ())

                found |> Expect.isNonEmpty "the scan finds fallbacks"

                wrongFallbacks file found
                |> List.map (fun (f, english) -> $"%s{f.File}:%i{f.Line} %s{f.Term}: %s{f.Text} <> %s{english}")
                |> Expect.isEmpty "no fallback differs"
            }

            test "the localization file covers the union, every locale, every cell" {
                let report = localizationPath () |> readLocalization |> scan (localeColumns ()) (termKeys ())

                report.Missing |> Expect.isEmpty "every term has a row"
                report.Stale |> Expect.isEmpty "no stale row"
                report.MissingColumns |> Expect.isEmpty "every locale has a column"
                report.Unwritable |> Expect.isEmpty "every cell can be written"

                report.EmptyCells
                |> List.collect snd
                |> Expect.isEmpty "every cell is filled"
            }

            test "the placeholders of a row agree across its locales" {
                let placeholders (s: string) =
                    [ "{0}"; "{1}" ] |> List.filter (fun p -> s.Contains p) |> Set.ofList

                for r in (localizationPath () |> readLocalization).Rows do
                    r.Cells
                    |> List.map placeholders
                    |> List.distinct
                    |> List.length
                    |> Expect.equal $"%s{r.Term}: one set of placeholders" 1
            }
        ]


runTestsWithCLIArgs [] [||] tests |> exit
