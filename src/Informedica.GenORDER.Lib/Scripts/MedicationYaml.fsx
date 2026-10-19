/// The text literals of the tests, YAML documents, read through the library's MedicationText.
///
/// Run from this directory, after a build:
///
///   dotnet fsi MedicationYaml.fsx --check    every text literal that does not read, with its errors

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: YamlDotNet, 16.3.0"
#load "../../../tests/Informedica.GenORDER.Tests/MedicationTextTests.fs"


open Informedica.GenOrder.Lib


/// The text literals of the tests, read with MedicationText.
module Fixtures =

    module Texts = MedicationTextTests.MedicationTexts


    /// Every text literal of the tests.
    let texts =
        [
            "Scenarios.kaliumchlorideOnceTimedText", Scenarios.kaliumchlorideOnceTimedText
            "Scenarios.amoxClavText", Scenarios.amoxClavText
            "Scenarios.amoxClavWithoutClavDoseText", Scenarios.amoxClavWithoutClavDoseText
            "Scenarios.amoxClavEveryCombinationText", Scenarios.amoxClavEveryCombinationText
            "Scenarios.prednisolonText", Scenarios.prednisolonText
            "Scenarios.naclLargeRateText", Scenarios.naclLargeRateText
            "MedicationTexts.onceSingleComponentMultipleItemsNoDose", Texts.onceSingleComponentMultipleItemsNoDose
            "MedicationTexts.onceSingleComponentSingleItem", Texts.onceSingleComponentSingleItem
            "MedicationTexts.onceTimedSingleComponentSingleItem", Texts.onceTimedSingleComponentSingleItem
            "MedicationTexts.discontinuousSingleComponentSingleItem", Texts.discontinuousSingleComponentSingleItem
            "MedicationTexts.discontinousMultipleComponentMultipleItems",
            Texts.discontinousMultipleComponentMultipleItems
            "MedicationTexts.timedSingleComponentSingleItem", Texts.timedSingleComponentSingleItem
            "MedicationTexts.continuousSingleComponentSingleItem", Texts.continuousSingleComponentSingleItem
            "MedicationTexts.continuousMultipleComponent", Texts.continuousMultipleComponent
            "MedicationTexts.timedMultipleComponentsDoseComponent", Texts.timedMultipleComponentsDoseComponent
            "MedicationTexts.pcmDrink", Texts.pcmDrink
            "MedicationTexts.vancoReconst", Texts.vancoReconst
            "MedicationTexts.tpnWithMaxQuantity", Texts.tpnWithMaxQuantity
            "MedicationTexts.adenosinDayOne", Texts.adenosinDayOne
            "MedicationTexts.gentamicin36hText", Texts.gentamicin36hText
            "MedicationTexts.benzylpenicillineText", Texts.benzylpenicillineText
            "MedicationTexts.continuousInfusionText", Texts.continuousInfusionText
            "MedicationTexts.vancomycinReconstitutionText", Texts.vancomycinReconstitutionText
            "MedicationTexts.noradrenalineInSolutionText", Texts.noradrenalineInSolutionText
            "MedicationTexts.feedingWithPowder", Texts.feedingWithPowder
        ]


    /// The text literals MedicationText reads, and the ones it refuses with its errors.
    let parsedTexts, refusedTexts =
        let read = texts |> List.map (fun (n, t) -> n, t |> MedicationText.fromString)

        let parsed =
            read
            |> List.choose (fun (n, r) ->
                match r with
                | Ok med -> Some(n, med)
                | Error _ -> None
            )

        let refused =
            read
            |> List.choose (fun (n, r) ->
                match r with
                | Ok _ -> None
                | Error errs -> Some(n, errs)
            )

        parsed, refused


if fsi.CommandLineArgs |> Array.contains "--check" then
    printfn $"%i{Fixtures.parsedTexts.Length} of %i{Fixtures.texts.Length} texts read"

    for n, errs in Fixtures.refusedTexts do
        printfn $"# %s{n}: refused\n%A{errs}\n"
