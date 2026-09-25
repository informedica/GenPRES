/// The estimate on the server: the normal-value tables as a resource, the contract model's
/// estimate applied to a patient read from the platform and to the MCP host's values, proved
/// to give the weight and height the web client shows for the same age and sex.
///
/// Four libraries prototyped at once, each shadowed under its own name:
///
/// - GenFORM.Lib `Resources`: the four normal-value sheets of the emergency-list workbook as
///   raw rows, one loader in the registry, keyed `normalValueRows`. The rows stay rows there,
///   since the parser is the contract model's and the contract is not the domain's to
///   reference.
/// - GenPRES.Shared `NormalValues`: the rows as the tables, `ofRows`, beside the `parse` the
///   client already uses on each sheet.
/// - GenPRES.Server `Mappers.Patient`: `estimated`, the domain patient with the estimates the
///   contract model computes, measured values untouched, and `estimating`, the platform port
///   wrapped so that every reading arrives estimated.
/// - MCP.Lib `GenOrderTools`: `buildPatient` estimating what the caller left out from the age
///   and the sex it gave, and the gate `requireAgeOrMeasures` in place of
///   `requireWeightAndHeight`: an age is a patient by the domain rule.
///
/// Needs the network: the sheets are fetched from the workbook the client fetches them from.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi Estimate.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "../../Informedica.MCP.Lib/bin/Debug/net10.0/Informedica.MCP.Lib.dll"
#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Patient.Optics


// ── GenFORM.Lib, Resources.fs ─────────────────────────────────────────────────────────────


module Resources =

    open Informedica.GenForm.Lib.Resources


    /// The rows of one workbook's sheets, by sheet name, as the sheets give them: a header
    /// row and the data rows, unparsed.
    type SheetRows = Map<string, string[][]>


    /// The emergency-list workbook, where the normal values of weight and height by age and
    /// sex live, in four sheets. One hospital's workbook written in code, as it is in the
    /// client; the two literals become one setting together.
    let normalValuesUrlId = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


    /// The four sheets: the weight and the height by age in years, and by post-conceptional
    /// age in weeks for the newborn.
    let normalValueSheets = [| "weight"; "height"; "weight neo"; "height neo" |]


    /// The four sheets fetched, every one or none: a table missing would make the estimate
    /// silently blank for the ages it covers.
    let getNormalValueRows urlId : Result<SheetRows, Message list> =
        normalValueSheets
        |> Array.fold
            (fun acc sheet ->
                acc
                |> Result.bind (fun rows ->
                    Web.GoogleSheets.getCsvDataFromSheetSync urlId sheet
                    |> Result.map (fun data -> rows |> Map.add sheet data)
                    |> Result.mapError (fun e -> [ ErrorMsg($"normal values, sheet %s{sheet}: %s{e}", None) ])
                )
            )
            (Ok Map.empty)


    module Keys =

        let normalValueRows = ResourceKey.create<SheetRows> "normalValueRows"


    /// The registry entry: `Keys.normalValueRows.Name, normalValueRowsLoader`.
    let normalValueRowsLoader: ResourceLoader =
        ofResult (fun () -> getNormalValueRows normalValuesUrlId)


// ── GenPRES.Shared, Models.fs ─────────────────────────────────────────────────────────────


module NormalValues =

    open Shared.Types
    open Shared.Models.NormalValues


    /// The tables from the rows of the four sheets, a sheet that is absent giving an empty
    /// table, as a sheet the client could not fetch does.
    let ofRows (rows: Map<string, string[][]>) : NormalValues =
        let table sheet =
            rows |> Map.tryFind sheet |> Option.map parse |> Option.defaultValue []

        {
            Weights = table "weight"
            Heights = table "height"
            NeoWeights = table "weight neo"
            NeoHeights = table "height neo"
        }


    /// The contract model's estimate applied, from the tables.
    let apply (nv: NormalValues) (pat: Patient) : Patient =
        pat
        |> Shared.Models.Patient.applyNormalValues
            (Some nv.Weights)
            (Some nv.Heights)
            (Some nv.NeoWeights)
            (Some nv.NeoHeights)


// ── GenPRES.Server, ServerApi.Mappers.Patient.fs ──────────────────────────────────────────


module Mappers =

    module Patient =

        open ServerApi.Patient

        module LibPatient = Informedica.GenForm.Lib.Patient


        /// The domain patient with the estimates the contract model computes for its age,
        /// gestational age and sex: the one estimate, on both sides, so the server's answer
        /// and the panel's display cannot drift. A measured value is untouched, and a patient
        /// the contract model cannot read back stays as it was.
        let estimated (nv: Shared.Types.NormalValues) (pat: Types.Patient) : Types.Patient =
            pat
            |> LibPatient.Dto.toDto
            |> toModel
            |> NormalValues.apply nv
            |> ofModel
            |> LibPatient.Dto.fromDto
            |> Result.defaultValue pat


        /// The platform port with every reading estimated: the inbound boundary, so no reading
        /// with an age alone reaches the rules without a weight and a height.
        let estimating (nv: unit -> Shared.Types.NormalValues option) (port: ServerApi.PatientDataPort) =
            { port with
                read =
                    fun pid ->
                        port.read pid
                        |> Option.map (fun pat ->
                            match nv () with
                            | Some nv -> pat |> estimated nv
                            | None -> pat
                        )
            }


// ── MCP.Lib, McpTools.GenOrder.fs ─────────────────────────────────────────────────────────


module GenOrderTools =

    open Informedica.GenForm.Lib.Resources
    open Informedica.MCP.Lib.GenOrderTools


    /// The measure a caller left out, estimated from the age and the sex it gave through the
    /// contract model, the one estimate the web client shows; a measure given stays measured.
    /// Without an age there is nothing to estimate from, and the patient stays as built.
    let estimated (nv: Shared.Types.NormalValues) (input: CreateOrderContextInput) (pat: Patient.Patient) =
        match input.AgeMonths, input.WeightKg, input.HeightCm with
        | _, Some _, Some _
        | None, _, _ -> pat
        | Some months, _, _ ->
            let gender =
                match input.Sex |> Option.map _.ToLowerInvariant() with
                | Some "male" -> Shared.Types.Male
                | Some "female" -> Shared.Types.Female
                | _ -> Shared.Types.UnknownGender

            let draft =
                Shared.Models.Patient.create
                    None
                    (months |> System.Math.Round |> int |> Shared.Measures.toMonth |> Some)
                    None
                    None
                    None
                    None
                    None
                    None
                    gender
                    []
                    None
                    None
                |> Option.map (NormalValues.apply nv)

            let weight =
                draft
                |> Option.bind _.Weight.Estimated
                |> Option.map (fun g -> decimal g / 1000m |> Kilogram)

            let height = draft |> Option.bind _.Height.Estimated |> Option.map (int >> Centimeter)

            let pat =
                match input.WeightKg, weight with
                | None, Some w ->
                    { (pat |> Patient.setWeight (Some w)) with
                        WeightMeasured = false
                    }
                | _ -> pat

            match input.HeightCm, height with
            | None, Some h ->
                { (pat |> Patient.setHeight (Some h)) with
                    HeightMeasured = false
                }
            | _ -> pat


    /// The patient built from the input, the department the default when none is given, and
    /// what the caller left out of the weight and the height estimated from the age.
    let buildPatient (provider: IResourceProvider) (input: CreateOrderContextInput) : Patient.Patient =
        let nv = provider.Get Resources.Keys.normalValueRows |> NormalValues.ofRows

        Informedica.MCP.Lib.GenOrderTools.buildPatient provider input |> estimated nv input


    /// Whether the input is a patient by the domain rule: an age, or a measured weight and
    /// height. With an age alone the weight and height are estimated, as the web client does.
    let requireAgeOrMeasures (input: CreateOrderContextInput) =
        match input.AgeMonths, input.WeightKg, input.HeightCm with
        | Some _, _, _
        | _, Some _, Some _ -> Ok()
        | _ ->
            Error
                "A patient needs an age, or both WeightKg and HeightCm. With an age alone the \
                 weight and height are estimated from it."


// ── The proof ─────────────────────────────────────────────────────────────────────────────


open Informedica.MCP.Lib.GenOrderTools


/// The rows, fetched once for every test.
let rows =
    lazy
        (match Resources.getNormalValueRows Resources.normalValuesUrlId with
         | Ok rows -> rows
         | Error msgs -> failtestf "the sheets did not load: %A" msgs)


let tables = lazy (rows.Value |> NormalValues.ofRows)


/// What the web client shows for a patient of this age and sex: the contract model's own
/// estimate, as App.fs applies it to the draft.
let clientShows years months gender =
    Shared.Models.Patient.create
        (years |> Option.map Shared.Measures.toYear)
        (months |> Option.map Shared.Measures.toMonth)
        None
        None
        None
        None
        None
        None
        gender
        []
        None
        None
    |> Option.map (NormalValues.apply tables.Value)
    |> Option.get
    |> fun p -> p.Weight.Estimated, p.Height.Estimated


/// A provider that answers the normal-value rows and the departments and nothing else.
let provider: Resources.IResourceProvider =
    let departments = Resources.Departments.ofNamed []

    { new Resources.IResourceProvider with
        member _.Get(key: Resources.ResourceKey<'T>) : 'T =
            if key.Name = Resources.Keys.normalValueRows.Name then
                box rows.Value :?> 'T
            elif key.Name = Resources.Keys.departments.Name then
                box departments :?> 'T
            else
                raise (System.NotImplementedException key.Name)

        member _.GetData() = raise (System.NotImplementedException())
        member _.GetUnitMappings() = raise (System.NotImplementedException())
        member _.GetRouteMappings() = [||]
        member _.GetValidForms() = raise (System.NotImplementedException())
        member _.GetFormRoutes() = raise (System.NotImplementedException())
        member _.GetFormularyProducts() = raise (System.NotImplementedException())
        member _.GetReconstitution() = [||]
        member _.GetParenteralMeds() = raise (System.NotImplementedException())
        member _.GetEnteralFeeding() = raise (System.NotImplementedException())
        member _.GetProducts() = raise (System.NotImplementedException())
        member _.GetDoseRules() = [||]
        member _.GetSolutionRules() = [||]
        member _.GetRenalRules() = [||]
        member _.GetTotals() = raise (System.NotImplementedException())
        member _.GetGStandProvider() = raise (System.NotImplementedException())
        member _.GetResourceInfo() = raise (System.NotImplementedException())
    }


let input: CreateOrderContextInput =
    {
        AgeMonths = Some 24.0
        WeightKg = None
        HeightCm = None
        Sex = Some "male"
        Department = None
        Generic = None
        Indication = None
        Route = None
        Form = None
    }


let grams (vu: ValueUnit) =
    vu
    |> ValueUnit.convertTo Units.Weight.gram
    |> ValueUnit.getValue
    |> Array.head
    |> Informedica.Utils.Lib.BCL.BigRational.ToDouble
    |> int


let cms (vu: ValueUnit) =
    vu
    |> ValueUnit.convertTo Units.Height.centiMeter
    |> ValueUnit.getValue
    |> Array.head
    |> Informedica.Utils.Lib.BCL.BigRational.ToDouble
    |> int


let tests =
    testList
        "the estimate on the server"
        [
            testList
                "the tables as a resource"
                [
                    test "the four sheets load, each with a header and rows" {
                        Resources.normalValueSheets
                        |> Array.map (fun s -> rows.Value |> Map.tryFind s |> Option.map Array.length)
                        |> Array.forall (fun n -> n |> Option.map (fun n -> n > 1) |> Option.defaultValue false)
                        |> Expect.isTrue "every sheet has rows"
                    }

                    test "the rows parse to the four tables the client parses" {
                        let nv = tables.Value

                        [ nv.Weights; nv.Heights; nv.NeoWeights; nv.NeoHeights ]
                        |> List.map (List.isEmpty >> not)
                        |> Expect.allEqual "every table filled" true

                        nv.Weights
                        |> List.map _.Sex
                        |> List.distinct
                        |> List.sort
                        |> Expect.equal "both sexes" [ "F"; "M" ]
                    }

                    test "a sheet absent gives an empty table, not a failure" {
                        (rows.Value |> Map.remove "height neo" |> NormalValues.ofRows).NeoHeights
                        |> Expect.isEmpty "no neo heights"
                    }

                    test "a sheet that does not load fails the loader, naming the sheet" {
                        match Resources.getNormalValueRows "no-such-workbook" with
                        | Ok _ -> failtest "should not load"
                        | Error msgs -> msgs |> List.length |> Expect.equal "one message" 1
                    }
                ]

            testList
                "a patient read from the platform"
                [
                    test "an age alone gets the weight and height the client shows, as estimates" {
                        let read = Patient.patient |> Patient.setAge [ Years 2 ] |> Patient.setGender Male

                        let pat = read |> Mappers.Patient.estimated tables.Value
                        let w, h = clientShows (Some 2) None Shared.Types.Male

                        (pat.Weight |> Option.map grams, pat.Height |> Option.map cms)
                        |> Expect.equal "the client's numbers" (w |> Option.map int, h |> Option.map int)

                        (pat.WeightMeasured, pat.HeightMeasured)
                        |> Expect.equal "estimated, not measured" (false, false)
                    }

                    test "a measured weight is untouched, the height estimated beside it" {
                        let read =
                            Patient.patient
                            |> Patient.setAge [ Years 2 ]
                            |> Patient.setWeight (Some(Kilogram 20m))

                        let pat = read |> Mappers.Patient.estimated tables.Value

                        (pat.Weight |> Option.map grams, pat.WeightMeasured)
                        |> Expect.equal "twenty kilograms, measured" (Some 20000, true)

                        (pat.Height |> Option.isSome, pat.HeightMeasured)
                        |> Expect.equal "estimated" (true, false)
                    }

                    test "the department, the gender and the access devices survive the round trip" {
                        let read =
                            { Patient.patient with
                                Department = Some "NEO"
                                Access = [ CVL ]
                            }
                            |> Patient.setAge [ Weeks 2 ]
                            |> Patient.setGender Female

                        let pat = read |> Mappers.Patient.estimated tables.Value

                        (pat.Department, pat.Gender, pat.Access)
                        |> Expect.equal "kept" (Some "NEO", Female, [ CVL ])
                    }

                    test "a reading that is no patient stays as it was" {
                        let read = Patient.patient

                        read
                        |> Mappers.Patient.estimated tables.Value
                        |> Expect.equal "unchanged" read
                    }

                    test "the port wrapped: a reading arrives estimated, and unestimated while the tables are not loaded" {
                        let read = Patient.patient |> Patient.setAge [ Years 5 ]
                        let port: ServerApi.PatientDataPort = { read = fun _ -> Some read }

                        (port |> Mappers.Patient.estimating (fun () -> Some tables.Value)).read "any"
                        |> Option.map _.Weight.IsSome
                        |> Expect.equal "estimated" (Some true)

                        (port |> Mappers.Patient.estimating (fun () -> None)).read "any"
                        |> Expect.equal "as read" (Some read)
                    }
                ]

            testList
                "the MCP host"
                [
                    test "an age and a sex alone build a patient with the weight and height the client shows" {
                        let pat = input |> GenOrderTools.buildPatient provider
                        let w, h = clientShows None (Some 24) Shared.Types.Male

                        (pat.Weight |> Option.map grams, pat.Height |> Option.map cms)
                        |> Expect.equal "the client's numbers" (w |> Option.map int, h |> Option.map int)

                        (pat.WeightMeasured, pat.HeightMeasured)
                        |> Expect.equal "estimated, not measured" (false, false)
                    }

                    test "a measure given stays measured, the other estimated" {
                        let pat =
                            { input with WeightKg = Some 14.0 }
                            |> GenOrderTools.buildPatient provider

                        (pat.Weight |> Option.map grams, pat.WeightMeasured)
                        |> Expect.equal "fourteen kilograms, measured" (Some 14000, true)

                        (pat.Height |> Option.isSome, pat.HeightMeasured)
                        |> Expect.equal "estimated" (true, false)
                    }

                    test "both measures given, nothing is estimated" {
                        let pat =
                            { input with
                                WeightKg = Some 14.0
                                HeightCm = Some 90.0
                            }
                            |> GenOrderTools.buildPatient provider

                        (pat.WeightMeasured, pat.HeightMeasured) |> Expect.equal "measured" (true, true)
                    }

                    test "no sex: the estimate is the average of the two, as the client's" {
                        let pat = { input with Sex = None } |> GenOrderTools.buildPatient provider
                        let w, _ = clientShows None (Some 24) Shared.Types.UnknownGender

                        pat.Weight
                        |> Option.map grams
                        |> Expect.equal "the client's number" (w |> Option.map int)
                    }

                    test "without an age nothing is estimated" {
                        let pat = { input with AgeMonths = None } |> GenOrderTools.buildPatient provider

                        (pat.Weight, pat.Height) |> Expect.equal "none" (None, None)
                    }

                    test "the gate: an age alone is a patient, measures alone are, nothing is not" {
                        input |> GenOrderTools.requireAgeOrMeasures |> Expect.isOk "age alone"

                        { input with
                            AgeMonths = None
                            WeightKg = Some 14.0
                            HeightCm = Some 90.0
                        }
                        |> GenOrderTools.requireAgeOrMeasures
                        |> Expect.isOk "measures alone"

                        { input with
                            AgeMonths = None
                            WeightKg = Some 14.0
                        }
                        |> GenOrderTools.requireAgeOrMeasures
                        |> Expect.isError "one measure and no age"

                        { input with AgeMonths = None }
                        |> GenOrderTools.requireAgeOrMeasures
                        |> Expect.isError "nothing"
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
