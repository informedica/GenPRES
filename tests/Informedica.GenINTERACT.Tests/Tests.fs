module Tests

open System.IO
open Expecto
open Expecto.Flip

open Informedica.GenInteract.Lib


/// An interaction between two drug classes, each given by its name and drugs.
let interaction (class1, drugs1) (class2, drugs2) =
    {
        Interaction.DrugClass1 =
            {
                DrugClass.Name = class1
                Drugs = drugs1
            }
        DrugClass2 =
            {
                DrugClass.Name = class2
                Drugs = drugs2
            }
    }


/// The interaction cache shipped in data/cache/interactions, found by searching upward from the test assembly.
let readInteractionCache () =
    let rec find (dir: DirectoryInfo) =
        let path = Path.Combine(dir.FullName, "data", "cache", "interactions", "Data.JSON")

        if File.Exists path then
            path
        elif isNull dir.Parent then
            raise (FileNotFoundException("No data/cache/interactions/Data.JSON found"))
        else
            find dir.Parent

    DirectoryInfo(System.AppContext.BaseDirectory) |> find |> File.ReadAllText


[<Tests>]
let tests =
    testList
        "GenInteract Tests"
        [
            testList
                "Interactions"
                [
                    test "tupleizeInteractions deduplicates symmetric pairs" {
                        [
                            interaction ("ClassA", [ "drugA" ]) ("ClassB", [ "drugB" ])
                            interaction ("ClassB", [ "drugB" ]) ("ClassA", [ "drugA" ])
                        ]
                        |> Interactions.tupleizeInteractions
                        |> Array.length
                        |> Expect.equal "should have 1 tuple (not 2)" 1
                    }

                    test "check detects a known interaction" {
                        let result =
                            [
                                interaction
                                    ("ACE Inhibitors", [ "lisinopril"; "enalapril" ])
                                    ("NSAIDs", [ "ibuprofen"; "naproxen" ])
                            ]
                            |> Interactions.check [ "lisinopril"; "ibuprofen" ]

                        result |> List.length |> Expect.equal "should detect 1 interaction" 1
                        result[0].Drug1 |> Expect.equal "drug1 should be lisinopril" "lisinopril"
                        result[0].Drug2 |> Expect.equal "drug2 should be ibuprofen" "ibuprofen"
                    }

                    test "check returns nothing for drugs that do not interact" {
                        [ interaction ("ACE Inhibitors", [ "lisinopril" ]) ("NSAIDs", [ "ibuprofen" ]) ]
                        |> Interactions.check [ "paracetamol"; "amoxicillin" ]
                        |> List.length
                        |> Expect.equal "should detect 0 interactions" 0
                    }

                    test "check returns nothing for an empty drug list" {
                        [ interaction ("ClassA", [ "drugA" ]) ("ClassB", [ "drugB" ]) ]
                        |> Interactions.check []
                        |> List.length
                        |> Expect.equal "should detect 0 interactions" 0
                    }

                    test "check matches drug names case-insensitively" {
                        [ interaction ("ClassA", [ "DrugA" ]) ("ClassB", [ "DrugB" ]) ]
                        |> Interactions.check [ "druga"; "drugb" ]
                        |> List.length
                        |> Expect.equal "should detect 1 interaction (case insensitive)" 1
                    }

                    test "dataToInteractions converts tuples" {
                        let result =
                            [ ("ClassA", [ "drugA1"; "drugA2" ], "ClassB", [ "drugB1" ]) ]
                            |> Interactions.dataToInteractions

                        result |> List.length |> Expect.equal "should have 1 interaction" 1
                        result[0].DrugClass1.Name |> Expect.equal "class1 name" "ClassA"

                        result[0].DrugClass1.Drugs
                        |> List.length
                        |> Expect.equal "class1 should have 2 drugs" 2
                    }
                ]

            testList
                "Api"
                [
                    test "the shipped interaction cache finds ciclosporine with colestyramine" {
                        [ "ciclosporine"; "colestyramine" ]
                        |> Api.checkInteractions (readInteractionCache ())
                        |> List.length
                        |> Expect.equal "should detect ciclosporine-colestyramine interaction" 1
                    }
                ]
        ]
