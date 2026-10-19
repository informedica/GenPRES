/// The text literals of the tests in the old medication text format, and their conversion into
/// the new text, a YAML document, through the library's MedicationText. The input of the switch,
/// in which the literals of the tests are replaced by their converted text; it goes with the old
/// reader.
///
/// Run from this directory, after a build:
///
///   dotnet fsi MedicationYaml.fsx --convert    every text literal of the tests, in the new text

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: YamlDotNet, 16.3.0"
#load "../../../tests/Informedica.GenORDER.Tests/MedicationTextTests.fs"


open Informedica.GenOrder.Lib


/// The text literals of the tests, read with the old reader.
module Fixtures =

    module Texts = MedicationTextTests.MedicationTexts


    /// Every text literal of the tests, in the old format. Scenarios.pcmSuppText and
    /// Scenarios.fullMedicationText are left out: no test uses them, the old reader refuses them,
    /// and the switch deletes them.
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


    /// The text literals the old reader reads, and the ones it refuses with its errors.
    let parsedTexts, refusedTexts =
        let read = texts |> List.map (fun (n, t) -> n, t |> Medication.fromString)

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


if fsi.CommandLineArgs |> Array.contains "--convert" then
    for n, med in Fixtures.parsedTexts do
        printfn $"# %s{n}\n%s{med |> MedicationText.toString}"

    for n, errs in Fixtures.refusedTexts do
        printfn $"# %s{n}: refused by the old reader\n%A{errs}\n"
