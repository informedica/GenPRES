namespace Informedica.ZIndex.Tests

open System
open System.IO


/// Ensures fixture files are in place before any test reads a ZIndex table.
/// Ordering is guaranteed because F# initializes modules in compilation order:
/// FixtureSetup (this module) precedes FixtureTests in the same file.
/// Since issue #523 the ZIndex modules no longer read the G-Standaard files at
/// module initialisation, so loading them is safe on its own. The reads are
/// memoized, though, and a memoized failure is cached for the process just as a
/// success is, so the fixtures must still exist before the first read.
[<AutoOpen>]
module FixtureSetup =

    let fixtureZindexDir = Informedica.Utils.Lib.AppPath.zindexDir ()

    // Run fixture setup at module initialization time (before any test runs).
    // Pre-existing BST files are backed up and restored via ProcessExit handler.
    let fixtureCreatedFiles: ZIndexFixture.FixtureResult =
        let zDir = fixtureZindexDir

        if Directory.GetFiles(zDir, "BST*.test") |> Array.isEmpty then
            ZIndexFixture.generate zDir

        let result = ZIndexFixture.setupForTest zDir
        // Register ProcessExit handler for reliable teardown.
        // This fires regardless of whether Main.fs finally block runs.
        AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> ZIndexFixture.teardownAfterTest result)
        result


module FixtureTests =

    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    open Informedica.ZIndex.Lib


    module BstTableTests =

        let tests =
            testList
                "BST table parsing with synthetic data"
                [

                    test "BST001T has field definitions for multiple tables" {
                        BST001T.getPosl "BST902T"
                        |> List.isEmpty
                        |> Expect.isFalse "BST001T should define BST902T fields"
                    }

                    test "BST711T contains paracetamol tablet record" {
                        Zindex.BST711T.records ()
                        |> Array.exists (fun r -> r.GPKODE = 694554)
                        |> Expect.isTrue "GPK 694554 (paracetamol tablet) should exist"
                    }

                    test "BST711T contains meropenem record" {
                        Zindex.BST711T.records ()
                        |> Array.map (fun r -> Names.getName r.GPNMNR Names.Full)
                        |> Array.filter (fun n -> n |> String.toLower |> String.contains "meropenem")
                        |> Array.isEmpty
                        |> Expect.isFalse "BST711T should contain a record whose name includes 'meropenem'"
                    }

                    test "BST711T paracetamol has correct ATC code" {
                        Zindex.BST711T.records ()
                        |> Array.tryFind (fun r -> r.GPKODE = 694554)
                        |> Option.map _.ATCODE.Trim()
                        |> Option.defaultValue ""
                        |> Expect.equal "paracetamol ATC should be N02BE01" "N02BE01"
                    }

                    test "BST902T contains route entries" {
                        Names.getItems Names.Route Names.Fifty
                        |> Array.length
                        |> fun n -> n >= 5
                        |> Expect.isTrue "should have at least 5 routes"
                    }

                    test "BST902T contains unit entries for GenericUnit" {
                        Names.getItems Names.GenericUnit Names.Fifty
                        |> Array.length
                        |> fun n -> n >= 5
                        |> Expect.isTrue "should have at least 5 units"
                    }

                    test "BST902T has 'oraal' in routes" {
                        Names.getItems Names.Route Names.TwentyFive
                        |> Array.map snd
                        |> Array.exists (fun s -> s |> String.toLower |> String.contains "oraal")
                        |> Expect.isTrue "routes should include 'oraal'"
                    }

                    test "BST902T has 'mg' in generic units" {
                        Names.getItems Names.GenericUnit Names.TwentyFive
                        |> Array.map snd
                        |> Array.exists (fun s -> s.Trim() = "mg")
                        |> Expect.isTrue "units should include 'mg'"
                    }

                    test "BST750T natriumchloride has correct molar mass" {
                        Zindex.BST750T.records ()
                        |> Array.tryFind (fun r -> r.GNGNAM |> String.equalsCapInsens "natriumchloride")
                        |> function
                            | None -> failtest "natriumchloride not found in BST750T"
                            | Some s ->
                                s.GNMOLE
                                |> Expect.floatClose "natriumchloride molar mass should be ~58.44" Accuracy.low 58.44
                    }

                    test "BST750T paracetamol molar mass is ~151.163" {
                        Zindex.BST750T.records ()
                        |> Array.tryFind (fun r -> r.GNGNAM |> String.equalsCapInsens "paracetamol")
                        |> function
                            | None -> failtest "paracetamol not found in BST750T"
                            | Some s ->
                                s.GNMOLE
                                |> Expect.floatClose "paracetamol molar mass should be ~151.163" Accuracy.low 151.163
                    }

                    test "BST643T has dose categories" {
                        Zindex.BST643T.records ()
                        |> Array.length
                        |> fun n -> n >= 1
                        |> Expect.isTrue "should have at least 1 dose category"
                    }

                    test "BST649T has dose values" {
                        Zindex.BST649T.records ()
                        |> Array.length
                        |> fun n -> n >= 1
                        |> Expect.isTrue "should have at least 1 dose value record"
                    }

                    test "BST649T paracetamol oral norm min is 500 mg" {
                        Zindex.BST649T.records ()
                        |> Array.tryFind (fun r -> r.GPDDNR = 3001)
                        |> function
                            | None -> failtest "dose number 3001 not found in BST649T"
                            | Some d ->
                                d.GPNRMMIN
                                |> Expect.floatClose "paracetamol oral norm min should be 500 mg" Accuracy.low 500.0
                    }

                    test "BST922T has text block records" {
                        Zindex.BST922T.records ()
                        |> Array.length
                        |> fun n -> n >= 1
                        |> Expect.isTrue "should have at least 1 text block"
                    }

                    test "BST801T has paracetamol ATC entry" {
                        Zindex.BST801T.records ()
                        |> Array.exists (fun r -> r.ATCODE.Trim() = "N02BE01")
                        |> Expect.isTrue "BST801T should contain N02BE01 (paracetamol)"
                    }

                ]


    module SubstanceTests =

        let tests =
            testList
                "Substance cache (demo)"
                [

                    test "demo cache has natriumchloride" {
                        Substance.get ()
                        |> Array.exists (fun s -> s.Name |> String.equalsCapInsens "natriumchloride")
                        |> Expect.isTrue "natriumchloride should be in substance cache"
                    }

                    test "natriumchloride 1g ≈ 17 mmol" {
                        Substance.get ()
                        |> Array.tryFind (fun s -> s.Name |> String.equalsCapInsens "natriumchloride")
                        |> function
                            | None -> failtest "natriumchloride not found in substance cache"
                            | Some s ->
                                1000.0 / (s.Mole |> float)
                                |> Expect.floatClose "1 g NaCl should be ≈17.11 mmol" Accuracy.low 17.11
                    }

                ]


    module GenPresProductTests =

        let tests =
            testList
                "GenPresProduct cache (demo)"
                [

                    test "demo cache returns products" {
                        GenPresProduct.get []
                        |> Array.length
                        |> fun n -> n >= 1
                        |> Expect.isTrue "demo cache should have products"
                    }

                ]


    let tests =
        testList
            "ZIndex synthetic fixture tests"
            [
                BstTableTests.tests
                SubstanceTests.tests
                GenPresProductTests.tests
            ]
