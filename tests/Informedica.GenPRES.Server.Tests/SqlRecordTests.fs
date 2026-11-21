/// The record on SQLite: order plan versions persisted and loaded on a real file, the rows a
/// release cannot read kept as unreadable entries, and the stored fixture of the current JSON
/// structure version read and written as this release reads and writes.
module Informedica.GenPRES.Server.Tests.SqlRecordTests

open System
open System.IO
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Informedica.GenPRES.Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.SessionStoreTests
open Informedica.GenPRES.Server.Tests.SqlSchemaTests


/// A fresh database file with the schema applied.
let withRecord (f: string -> 'a) =
    withDb (fun cs ->
        SqlSchema.apply cs |> ignore
        f cs
    )


open Informedica.GenPRES.Server.Tests.TestDates


let v1 = lazy (versionOf 1 prescriber t0 domainPlan.Value)


/// Inserts a row as SQL, bypassing `persist`, for rows no release of this code would write.
let insertRowAt (cs: string) (signedAt: int64) (versionId: string) (no: int) (jsonVersion: int) (plan: string) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()

    cmd.CommandText <-
        """
        insert into order_plan
            (version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
             signed_at, verified, json_version, plan)
        values ($id, $no, 'stub-patient', null, 'prescriber', 'Stub Prescriber', $at, 1, $jv, $plan)
        """

    cmd.Parameters.AddWithValue("$id", versionId) |> ignore
    cmd.Parameters.AddWithValue("$no", no) |> ignore

    cmd.Parameters.AddWithValue("$at", signedAt) |> ignore

    cmd.Parameters.AddWithValue("$jv", jsonVersion) |> ignore
    cmd.Parameters.AddWithValue("$plan", plan) |> ignore
    cmd.ExecuteNonQuery() |> ignore


let insertRow cs =
    insertRowAt cs (DateTimeOffset(t0, TimeSpan.Zero).ToUnixTimeMilliseconds())


/// The stored fixture of structure version 13, the last version that held the whole version in
/// the plan column, copied to the test output.
let v13FixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v13.json"))


/// The stored fixture of structure version 14, the last version with the GenFORM patient.
let v14FixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v14.json"))


/// The stored fixture of structure version 15, the last version that named the patient data
/// Patient.
let v15FixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v15.json"))


/// The stored fixture of structure version 16, the last version without the component quantity in the
/// orderable per adjust unit.
let v16FixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v16.json"))


/// The plan column as this release writes it, structure version 17. It is the order plan of the
/// version 16 fixture with the component quantity in the orderable per adjust unit.
let currentFixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v17.json"))


let parse (text: string) =
    text |> Canonical.deserialize<OrderPlan.Dto.Dto> |> OrderPlan.Dto.fromDto readAt


/// The stored form of a readable entry; None when it is unreadable.
let storedForm =
    function
    | StoredVersion.Readable(v, _) -> Some(SqlDatabase.toJson v)
    | StoredVersion.Unreadable _ -> None


let reasonOf =
    function
    | StoredVersion.Unreadable u -> Some u.Reason
    | StoredVersion.Readable _ -> None


/// Runs one SQL statement on the database.
let run (cs: string) (sql: string) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- sql
    cmd.ExecuteNonQuery() |> ignore


[<Tests>]
let tests =
    testList
        "SqlDatabase"
        [
            test "whom a version names is in its columns, both cases; a row from before names none; half is a reason" {
                withRecord (fun cs ->
                    let whom = StubEhrPatient.patient StubEhrPatient.measuredOn "stub-patient" |> _.Identity

                    SqlDatabase.persist readAt cs [ Session.WriteVersion(v1.Value, whom) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    let identityOf () =
                        SqlDatabase.loadRecords readAt cs "stub-patient"
                        |> List.map StoredVersion.identity

                    identityOf () |> Expect.equal "the identity, on the readable version" [ whom ]

                    let run = run cs

                    // the same row under a structure version this release does not know keeps
                    // naming its patient
                    run "update order_plan set json_version = 18"

                    (SqlDatabase.loadRecords readAt cs "stub-patient"
                     |> List.map reasonOf
                     |> List.map Option.isSome,
                     identityOf ())
                    |> Expect.equal "unreadable, and still the identity" ([ true ], [ whom ])

                    // a row from before the columns: none
                    run
                        "update order_plan set json_version = 17, patient_name = null, birth_year = null, birth_month = null, birth_day = null"
                    identityOf () |> Expect.equal "none" [ None ]

                    // the columns half present, and a date the calendar does not have: a reason
                    run "update order_plan set patient_name = 'Stub'"

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "not all present" [ Some "the patient's name and birthdate are not all present" ]

                    run "update order_plan set birth_year = 2016, birth_month = 2, birth_day = 30"

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "no date" [ Some "the birthdate 2016-2-30 is no date" ]

                    // a year too wide for the calendar, as SQLite lets a column hold: a reason,
                    // not a failed load of the whole record
                    run "update order_plan set birth_year = 9999999999, birth_day = 28"

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "too wide" [ Some "the birthdate 9999999999-2-28 is no date" ]

                    // all four present but the name blank: nobody is named so
                    run "update order_plan set birth_year = 2016, patient_name = ' '"

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "empty name" [ Some "the patient's name is empty" ]
                )
            }

            test "a row written before the identity columns loads, naming none, once they are added" {
                withDb (fun cs ->
                    SqlSchema.embedded typeof<SqlSchema.Migration>.Assembly
                    |> List.filter (fun m -> m.Number <= 7)
                    |> SqlSchema.applyAll cs
                    |> Expect.equal "the file at the version before the columns" [ 1; 2; 3; 4; 5; 6; 7 ]

                    SqlSchema.apply cs |> Expect.equal "migrations 9 and 10" [ 9; 10 ]

                    insertRow cs "plan-1" 1 17 (currentFixtureText ())

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Readable(v, whom) ] ->
                        (v.Id, whom) |> Expect.equal "readable, naming none" ("plan-1", None)
                    | other -> failtest $"expected one readable row, got %A{other}"
                )
            }

            test "a persisted order plan version loads back as written" {
                withRecord (fun cs ->
                    SqlDatabase.persist readAt cs [ Session.WriteVersion(v1.Value, None) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    // compared in the stored form: a DateTime reads back as the same instant in UTC
                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> List.map storedForm
                    |> Expect.equal "the one version, readable" [ Some(SqlDatabase.toJson v1.Value) ]

                    SqlDatabase.loadRecords readAt cs "another-patient"
                    |> Expect.isEmpty "another patient has none"
                )
            }

            test "a version with EHR data, not verified, loads back as written" {
                withRecord (fun cs ->
                    let v =
                        { v1.Value with
                            Verified = false
                            EhrPatient = Some(StubEhrPatient.patient StubEhrPatient.measuredOn "stub-patient")
                        }

                    SqlDatabase.persist readAt cs [ Session.WriteVersion(v, None) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> Expect.equal "the version" [ StoredVersion.Readable(v, None) ]
                )
            }

            test "the column plan holds the order plan Dto alone, under structure version 17" {
                withRecord (fun cs ->
                    SqlDatabase.persist readAt cs [ Session.WriteVersion(v1.Value, None) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    scalar cs "select plan from order_plan"
                    |> string
                    |> Expect.equal "the order plan" (v1.Value.Plan |> OrderPlan.Dto.toDto |> Canonical.serialize)

                    scalar cs "select json_version from order_plan"
                    |> Expect.equal "the structure version" (box 17L)
                )
            }

            test "EHR data that do not read, or one EHR column without the other, leave the row unreadable" {
                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 17 (currentFixtureText ())

                    let run = run cs

                    let reason () =
                        SqlDatabase.loadRecords readAt cs "stub-patient"
                        |> List.exactlyOne
                        |> reasonOf
                        |> Option.defaultValue ""

                    run
                        $"update order_plan set ehr_json_version = %i{SqlEhrPatient.ehrPatientJsonVersion}, ehr_data = 'no json'"
                    reason () |> Expect.stringContains "the reason" "the JSON does not read"

                    run "update order_plan set ehr_data = null"

                    reason ()
                    |> Expect.equal "half" "the EHR data and its JSON structure version are not both present"
                )
            }

            test "the time of signing reads back to the millisecond" {
                withRecord (fun cs ->
                    SqlDatabase.persist
                        readAt
                        cs
                        [ Session.WriteVersion({ v1.Value with SignedAt = t0.AddTicks 12345L }, None) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Readable(v, _) ] ->
                        v.SignedAt |> Expect.equal "whole milliseconds" (t0.AddMilliseconds 1.0)
                    | other -> failtest $"expected one readable row, got %A{other}"
                )
            }

            test "a second version with the same number is a conflict with the head" {
                withRecord (fun cs ->
                    SqlDatabase.persist readAt cs [ Session.WriteVersion(v1.Value, None) ] |> ignore

                    let rival =
                        { v1.Value with
                            Id = "plan-rival"
                            SignedBy =
                                {
                                    UserId = other.UserId
                                    DisplayName = other.DisplayName
                                }
                        }

                    match SqlDatabase.persist readAt cs [ Session.WriteVersion(rival, None) ] with
                    | Session.StoreOutcome.Conflict winner ->
                        winner
                        |> storedForm
                        |> Expect.equal "the row that won" (Some(SqlDatabase.toJson v1.Value))
                    | other -> failtest $"expected Conflict, got %A{other}"
                )
            }

            test "a second version with the same id and a new number fails, it is no conflict" {
                withRecord (fun cs ->
                    SqlDatabase.persist readAt cs [ Session.WriteVersion(v1.Value, None) ] |> ignore

                    match SqlDatabase.persist readAt cs [ Session.WriteVersion({ v1.Value with No = 2 }, None) ] with
                    | Session.StoreOutcome.Failed reason ->
                        reason
                        |> Expect.stringContains "the reason names the column" "order_plan.version_id"
                    | other -> failtest $"expected Failed, got %A{other}"
                )
            }

            test "a read-only database fails with the reason" {
                withRecord (fun cs ->
                    let readOnly = SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly).ToString()

                    match SqlDatabase.persist readAt readOnly [ Session.WriteVersion(v1.Value, None) ] with
                    | Session.StoreOutcome.Failed reason -> reason |> Expect.isNotEmpty "a reason"
                    | other -> failtest $"expected Failed, got %A{other}"

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> Expect.isEmpty "nothing written"
                )
            }

            test "rows this release cannot read load as unreadable, and the newest is the head" {
                withRecord (fun cs ->
                    let text = currentFixtureText ()
                    insertRow cs "plan-1" 1 17 text
                    // a structure version newer than the release knows
                    insertRow cs "plan-2" 2 18 text

                    // a Dto the domain refuses: the totals are missing
                    let noTotals =
                        { Canonical.deserialize<OrderPlan.Dto.Dto> text with Totals = Unchecked.defaultof<_> }
                        |> Canonical.serialize

                    insertRow cs "plan-3" 3 17 noTotals

                    let loaded = SqlDatabase.loadRecords readAt cs "stub-patient"

                    loaded
                    |> List.map StoredVersion.id
                    |> Expect.equal "newest first" [ "plan-3"; "plan-2"; "plan-1" ]

                    loaded
                    |> List.map reasonOf
                    |> List.map Option.isSome
                    |> Expect.equal "two unreadable, the first readable" [ true; true; false ]

                    loaded[1]
                    |> reasonOf
                    |> Option.defaultValue ""
                    |> Expect.stringContains "the reason" "newer than this release knows"

                    loaded[0]
                    |> reasonOf
                    |> Option.defaultValue ""
                    |> Expect.stringContains "the reason" "does not parse"

                    match loaded[0] with
                    | StoredVersion.Unreadable u ->
                        (u.No, u.SignedBy.UserId, u.SignedAt)
                        |> Expect.equal "the identity from the columns" (3, "prescriber", t0)
                    | StoredVersion.Readable _ -> failtest "the head should be unreadable"
                )
            }

            test "a malformed value in the JSON leaves the row unreadable" {
                withRecord (fun cs ->
                    // a BigRational that is no number: the converter throws, not the JSON reader
                    currentFixtureText().Replace("\"Value\":[\"30/1\"", "\"Value\":[\"x/y\"")
                    |> insertRow cs "plan-1" 1 17

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] -> u.Reason |> Expect.stringContains "the reason" "does not read"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a signed_at out of range leaves the row unreadable at the earliest time" {
                withRecord (fun cs ->
                    insertRowAt cs Int64.MaxValue "plan-1" 1 17 (currentFixtureText ())

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.SignedAt |> Expect.equal "the earliest time" DateTime.MinValue
                        u.Reason |> Expect.stringContains "the reason" "out of range"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "this release reads and writes structure version 17; 0 to 16 and 18 are reasons" {
                let json = currentFixtureText ()

                SqlDatabase.jsonVersion |> Expect.equal "the version read and written" 17

                SqlDatabase.upgrade 17 json |> Expect.equal "17 is current" (Ok json)

                for version in 0..16 do
                    SqlDatabase.upgrade version json
                    |> Expect.equal
                        $"%i{version}"
                        (Error $"JSON structure version %i{version} is older than this release reads")

                SqlDatabase.upgrade 18 json
                |> Expect.equal "18" (Error "JSON structure version 18 is newer than this release knows")
            }

            test "a stored version 13 row, with the whole version in the plan column, loads as unreadable" {
                let text = v13FixtureText ()
                text |> Expect.stringContains "the signer in the row" "\"SignedBy\""

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 13 text

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason |> Expect.stringContains "the reason" "older than this release reads"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a stored version 14 row, with the GenFORM patient, loads as unreadable" {
                let text = v14FixtureText ()
                text |> Expect.stringContains "the GenFORM patient in the row" "\"WeightKg\""

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 14 text

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason |> Expect.stringContains "the reason" "older than this release reads"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a stored version 15 row, with the patient data named Patient, loads as unreadable" {
                let text = v15FixtureText ()
                text |> Expect.stringContains "the old name in the row" "\"Patient\":"

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 15 text

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason |> Expect.stringContains "the reason" "older than this release reads"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a stored version 16 row, without the component quantity per adjust unit, loads as unreadable" {
                let text = v16FixtureText ()
                text.Contains "_orb_qty_adj"
                |> Expect.isFalse "no quantity per adjust unit in the row"

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 16 text

                    match SqlDatabase.loadRecords readAt cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason |> Expect.stringContains "the reason" "older than this release reads"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a signed version keeps the mark of an order that could not be calculated" {
                let text = (currentFixtureText ()).Replace("\"NotRecalculated\":false", "\"NotRecalculated\":true")

                match parse text with
                | Ok plan ->
                    plan.Contexts
                    |> Array.collect _.Context.Scenarios
                    |> Expect.all "marked" _.NotRecalculated

                    { v1.Value with Plan = plan }
                    |> SqlDatabase.toJson
                    |> Expect.equal "written as read" text
                | other -> failtest $"expected a parsed order plan, got %A{other}"
            }

            test "a row with an argumentation loads back with it; the current fixture with none" {
                let text = "Sepsis, hogere dosis in overleg met de apotheek"

                let argued =
                    match parse (currentFixtureText ()) with
                    | Ok plan ->
                        { plan with
                            Contexts =
                                plan.Contexts
                                |> Array.mapi (fun i pc ->
                                    if i = 0 then
                                        { pc with Context = { pc.Context with Argumentation = Some text } }
                                    else
                                        pc
                                )
                        }
                    | Error e -> failtest $"%A{e}"

                argued.Contexts |> Array.isEmpty |> Expect.isFalse "the fixture has contexts"

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 17 (currentFixtureText ())
                    insertRow cs "plan-2" 2 17 (argued |> OrderPlan.Dto.toDto |> Canonical.serialize)

                    let texts (v: OrderPlanVersion) =
                        v.Plan.Contexts |> Array.map _.Context.Argumentation |> Array.choose id

                    SqlDatabase.loadRecords readAt cs "stub-patient"
                    |> List.choose (
                        function
                        | StoredVersion.Readable(v, _) -> Some(v.Id, texts v)
                        | StoredVersion.Unreadable _ -> None
                    )
                    |> List.sortBy fst
                    |> Expect.equal
                        "the current fixture with none, the argued row with the text"
                        [ "plan-1", [||]; "plan-2", [| text |] ]
                )
            }

            test "the read snapshot: the current fixture parses and serializes back to its text" {
                let text = currentFixtureText ()

                match parse text with
                | Ok plan ->
                    plan
                    |> OrderPlan.Dto.toDto
                    |> Canonical.serialize
                    |> Expect.equal "the same text" text
                | Error errs -> failtest $"%A{errs}"
            }

            test "the write snapshot: the current fixture, read and written as this release writes, is its text" {
                let text = currentFixtureText ()

                match text |> SqlDatabase.upgrade SqlDatabase.jsonVersion |> Result.map parse with
                | Ok(Ok plan) ->
                    { v1.Value with Plan = plan }
                    |> SqlDatabase.toJson
                    |> Expect.equal "the same text" text
                | other -> failtest $"%A{other}"
            }
        ]
