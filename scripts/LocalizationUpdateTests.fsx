// Tests of the pure update in scripts/LocalizationUpdate.fsx: the parse of a snapshot, the
// update against the term keys and the locale columns, and the lines written back.
//
// Run: dotnet fsi scripts/LocalizationUpdateTests.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "LocalizationUpdate.fsx"

open LocalizationUpdate
open Expecto
open Expecto.Flip


let row term cells = { Term = term; Cells = cells }


let header = [ "Term"; "English"; "Dutch" ]


let tests =
    testList
        "LocalizationUpdate"
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
                [ "Term\tEnglish\tDutch"; "A\ta\t"; "" ]
                |> parse
                |> _.Rows
                |> List.length
                |> Expect.equal "one row" 1
            }

            test "parse fails on a header without Term" {
                (fun () -> [ "Key\tEnglish" ] |> parse |> ignore)
                |> Expect.throws "not Term"
            }

            test "parse fails on a row with the wrong number of cells" {
                (fun () -> [ "Term\tEnglish\tDutch"; "A\ta" ] |> parse |> ignore)
                |> Expect.throws "two cells, three headers"
            }

            test "parse fails on a repeated term" {
                (fun () -> [ "Term\tEnglish"; "A\ta"; "A\tb" ] |> parse |> ignore)
                |> Expect.throws "A twice"
            }

            test "update keeps existing rows unchanged and in order" {
                let snapshot = { Header = header; Rows = [ row "B" [ "b"; "" ]; row "A" [ "a"; "aa" ] ] }

                (update StaleRows.Drop header.Tail [ "A"; "B" ] snapshot).Snapshot.Rows
                |> Expect.equal "same rows, same order" snapshot.Rows
            }

            test "update appends missing terms in union order with empty cells" {
                let snapshot = { Header = header; Rows = [ row "B" [ "b"; "" ] ] }
                let result = update StaleRows.Drop header.Tail [ "C"; "B"; "A" ] snapshot

                result.Added |> Expect.equal "C then A" [ "C"; "A" ]

                result.Snapshot.Rows
                |> Expect.equal "B kept, C and A appended" [ row "B" [ "b"; "" ]; row "C" [ ""; "" ]; row "A" [ ""; "" ] ]
            }

            test "update drops a stale row and reports it" {
                let snapshot = { Header = header; Rows = [ row "A" [ "a"; "" ]; row "Old" [ "o"; "" ] ] }
                let result = update StaleRows.Drop header.Tail [ "A" ] snapshot

                result.Stale |> Expect.equal "Old is stale" [ row "Old" [ "o"; "" ] ]
                result.Snapshot.Rows |> Expect.equal "Old dropped" [ row "A" [ "a"; "" ] ]
            }

            test "update keeps a stale row on Keep and still reports it" {
                let snapshot = { Header = header; Rows = [ row "A" [ "a"; "" ]; row "Old" [ "o"; "" ] ] }
                let result = update StaleRows.Keep header.Tail [ "A" ] snapshot

                result.Stale |> Expect.equal "Old is stale" [ row "Old" [ "o"; "" ] ]
                result.Snapshot.Rows |> Expect.equal "Old kept" snapshot.Rows
            }

            test "update counts the empty cells per locale" {
                let snapshot = { Header = header; Rows = [ row "A" [ "a"; "" ]; row "B" [ ""; " " ] ] }

                (update StaleRows.Drop header.Tail [ "A"; "B"; "C" ] snapshot).EmptyCells
                |> Expect.equal "English 2 of 3, Dutch 3 of 3" [ "English", 2; "Dutch", 3 ]
            }

            test "update fails on a cell with a tab or a line break" {
                for bad in [ "a\tb"; "a\nb"; "a\rb" ] do
                    let snapshot = { Header = header; Rows = [ row "A" [ bad; "" ] ] }

                    (fun () -> update StaleRows.Drop header.Tail [ "A" ] snapshot |> ignore)
                    |> Expect.throws $"%A{bad} refused"
            }

            test "update appends a missing locale column with empty cells, existing columns first" {
                let snapshot = { Header = header; Rows = [ row "A" [ "a"; "aa" ] ] }
                let result = update StaleRows.Drop [ "Dutch"; "English"; "French" ] [ "A"; "B" ] snapshot

                result.AddedColumns |> Expect.equal "French only" [ "French" ]
                result.Snapshot.Header |> Expect.equal "kept order, French last" [ "Term"; "English"; "Dutch"; "French" ]

                result.Snapshot.Rows
                |> Expect.equal "padded and appended" [ row "A" [ "a"; "aa"; "" ]; row "B" [ ""; ""; "" ] ]

                result.EmptyCells |> Expect.equal "French empty twice" [ "English", 1; "Dutch", 1; "French", 2 ]
            }

            test "update keeps a header column that is no locale" {
                let snapshot = { Header = [ "Term"; "Note"; "English" ]; Rows = [ row "A" [ "n"; "a" ] ] }
                let result = update StaleRows.Drop [ "English" ] [ "A" ] snapshot

                result.AddedColumns |> Expect.isEmpty "nothing to add"
                result.Snapshot |> Expect.equal "unchanged" snapshot
            }

            test "the locale columns are the Locales cases in getTerm's order" {
                localeColumns ()
                |> Expect.equal "six locales" [ "English"; "Dutch"; "French"; "German"; "Spanish"; "Italian" ]
            }

            test "update is idempotent" {
                let snapshot = { Header = header; Rows = [ row "B" [ "b"; "" ]; row "Old" [ "o"; "" ] ] }
                let terms = [ "A"; "B" ]
                let once = (update StaleRows.Drop header.Tail terms snapshot).Snapshot
                let twice = update StaleRows.Drop header.Tail terms once

                twice.Snapshot |> Expect.equal "same snapshot" once
                twice.Added |> Expect.isEmpty "nothing added"
                twice.Stale |> Expect.isEmpty "nothing stale"
            }

            test "toLines and parse round trip" {
                let snapshot = { Header = header; Rows = [ row "A" [ "a"; "" ]; row "B" [ ""; "bb" ] ] }

                snapshot |> toLines |> parse |> Expect.equal "same snapshot" snapshot
            }

            test "every term key is a non-empty trimmed name" {
                termKeys ()
                |> List.iter (fun t ->
                    t |> Expect.isNotEmpty "not empty"
                    t.Trim() |> Expect.equal "trimmed" t
                )
            }

            test "the union holds no repeated term key" {
                termKeys () |> List.distinct |> List.length
                |> Expect.equal "all distinct" (termKeys () |> List.length)
            }
        ]


runTestsWithCLIArgs [] [||] tests |> exit
