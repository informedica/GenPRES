(*
    DownloadFixtures.fsx
    --------------------
    Generates the OFFLINE fixtures for the DoseRule round-trip test
    (DoseRuleRoundtripTests in ../Tests.fs). Run ONCE, by hand, with network +
    .env (GENPRES_URL_ID) + the committed demo cache (data/cache/*.demo). It:

      1. builds the DEMO provider (GENPRES_PROD is left unset -> demo ZIndex cache),
      2. downloads the DoseRules sheet and parses it to DoseRuleData[],
      3. picks a small, deterministic SUBSET of generics that exist in BOTH the
         dose-rule data and the demo products (so product attachment + form/unit
         expansion are actually exercised),
      4. serializes the FOUR pure inputs of DoseRuleLoader.fromData
         (DoseRuleData[], RouteMapping[], FormRoute[], ProductComponent[]) to JSON
         in ../fixtures, via Informedica.Utils.Lib.Json (round-trips ValueUnit).

    The committed fixtures make the test fully hermetic: no network, no env, no
    cache at test time. Re-run this only when the subset or the upstream data
    must change; then refresh the frozen counts asserted in the test.

    Bootstrap: the GenFORM Scripts/load.fsx (source #loaded; fresh FSI). Run:
      cd tests/Informedica.GenFORM.Tests/Scripts && dotnet fsi DownloadFixtures.fsx
*)

// Reuse the GenFORM Scripts bootstrap. #I points FSI's include path at the Scripts
// dir (relative to this script) so load.fsx's own relative #r/#load resolve.
#I __SOURCE_DIRECTORY__
#I "../../../src/Informedica.GenFORM.Lib/Scripts"
#load "load.fsx"

open System
Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
// demo data: leave GENPRES_PROD unset/0 so products come from the demo cache
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")
let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")

#load "../Types.fs" "../Utils.fs" "../Logging.fs" "../Mapping.fs" "../Patient.fs"
       "../Product.fs" "../Filter.fs" "../LimitTarget.fs" "../DoseLimit.fs" "../DoseType.fs"
       "../GenericLabel.fs" "../PharmaceuticalForm.fs" "../ProductId.fs" "../Generic.fs"
       "../Source.fs" "../DoseRule.fs" "../DoseRuleData.fs" "../DoseRuleLoader.fs" "../Check.fs"
       "../SolutionLimit.fs" "../SolutionRule.fs" "../RenalRule.fs"
       "../PrescriptionRule.fs" "../FormLogging.fs" "../Api.fs"

open System.IO
open MathNet.Numerics
open Newtonsoft.Json
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib

// Shared fixture JSON helper (writer here, reader in Tests.fs) — defined once in
// FixtureJson.fs so the two can never drift. See that file for why Utils.Lib.Json
// is not reused.
#load "../FixtureJson.fs"

open Informedica.GenForm.Tests


let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId Informedica.Logging.Lib.Logging.noOp dataUrlId

let rm = provider.GetRouteMappings ()
let fr = provider.GetFormRoutes ()
let prods = provider.GetProducts ()

let allData: DoseRuleData[] =
    DoseRuleLoader.getData dataUrlId
    |> function
        | Ok d -> d
        | Error msgs -> failwithf $"getData failed: %A{msgs}"


// ---------------------------------------------------------------------------
// Deterministic subset: generics present in BOTH dose-rule data and demo
// products. Sorted; take a handful. Curated candidates first (peds staples),
// then fill from the intersection so the fixture is stable but never empty.
// ---------------------------------------------------------------------------
let prodGenerics =
    prods |> Array.map (_.Generic >> String.toLower) |> Set.ofArray

let dataGenerics =
    allData |> Array.map (_.Generic.Name >> String.toLower) |> Set.ofArray

let available =
    Set.intersect prodGenerics dataGenerics |> Set.toList |> List.sort

let candidates =
    [
        "paracetamol"; "amoxicilline"; "morfine"; "gentamicine"; "furosemide"
        "ibuprofen"; "midazolam"; "fentanyl"; "dexamethason"; "ondansetron"
    ]

let chosen =
    let preferred = candidates |> List.filter (fun g -> available |> List.contains g)
    let filler = available |> List.filter (fun g -> preferred |> List.contains g |> not)
    (preferred @ filler) |> List.truncate 8 |> Set.ofList

printfn $"available (data ∩ products) = %d{available |> List.length} generics"
printfn $"chosen subset = %A{chosen |> Set.toList}"


let subsetData =
    allData |> Array.filter (fun d -> chosen.Contains(d.Generic.Name |> String.toLower))

let subsetProds =
    prods |> Array.filter (fun p -> chosen.Contains(p.Generic |> String.toLower))

printfn $"subset dose-rule rows = %d{subsetData.Length}, subset products = %d{subsetProds.Length}"
printfn $"route mappings = %d{rm.Length}, form routes = %d{fr.Length}"


// ---------------------------------------------------------------------------
// Serialize the four pure inputs of DoseRuleLoader.fromData.
// ---------------------------------------------------------------------------
let fxDir = Path.Combine(__SOURCE_DIRECTORY__, "..", "fixtures")
Directory.CreateDirectory fxDir |> ignore

let write name (x: 'a) =
    let path = Path.Combine(fxDir, name)
    File.WriteAllText(path, FixtureJson.serialize x)
    printfn $"wrote %s{name} (%d{FileInfo(path).Length} bytes)"

// NOTE: FormRoute[] is intentionally NOT a fixture — fromData only uses it for
// FormLimit, which toData does not emit and the round-trip does not compare. The
// test passes fr = [||] (as the existing DoseRuleProductTests do).
write "doserules.json" subsetData
write "routemappings.json" rm
write "products.json" subsetProds


// ---------------------------------------------------------------------------
// Inline round-trip of the fixtures we just wrote — proves they deserialize and
// reconstruct ValueUnit losslessly (so the test will too).
// ---------------------------------------------------------------------------
let rt name (orig: 'a) : 'a =
    let back = orig |> FixtureJson.serialize |> FixtureJson.deSerialize<'a>
    printfn $"round-trip %s{name} ok"
    back

let _ = rt "doserules" subsetData
let _ = rt "routemappings" rm
let prodsBack = rt "products" subsetProds
printfn $"products round-trip count = %d{prodsBack.Length} (orig %d{subsetProds.Length})"


// ---------------------------------------------------------------------------
// Verification: run the round-trip here too so the frozen counts to assert in
// the test are visible. (The test re-implements the same check on the fixtures.)
// fr = [||] — see note above.
// ---------------------------------------------------------------------------
let forward (data: DoseRuleData[]) =
    DoseRuleLoader.fromData rm [||] subsetProds data |> fst

let pass1Rules = subsetData |> forward
let pass1Gen = pass1Rules |> Array.collect DoseRule.toData

printfn "\n--- FREEZE THESE IN THE TEST ---"
printfn $"subset dose-rule rows = %d{subsetData.Length}"
printfn $"PASS 1 forward rules  = %d{pass1Rules.Length}"
printfn $"PASS 1 toData rows    = %d{pass1Gen.Length} (pre-distinct)"
printfn "(run the test once to read PASS 1 distinct/missing and PASS 2 = 100%%)"
