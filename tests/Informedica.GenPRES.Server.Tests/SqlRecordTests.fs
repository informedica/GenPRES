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


/// The stored fixture of structure version 12, the last version before the link on every plan
/// context and the rename of its intake to totals, copied to the test output.
let v12FixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v12.json"))


/// The same version as this release writes it, structure version 13: the version 12 fixture with
/// the intake of every plan context renamed to totals and no link on any of them.
let currentFixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v13.json"))


let parse (text: string) =
    text
    |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
    |> OrderPlanVersion.Dto.fromDto


/// The stored form of a readable entry; None when it is unreadable.
let storedForm =
    function
    | StoredVersion.Readable(v, _) -> Some(SqlDatabase.toJson v)
    | StoredVersion.Unreadable _ -> None


let reasonOf =
    function
    | StoredVersion.Unreadable u -> Some u.Reason
    | StoredVersion.Readable _ -> None


[<Tests>]
let tests =
    testList
        "SqlDatabase"
        [
            test "whom a version names is in its columns, both cases; a row from before names none; half is a reason" {
                withRecord (fun cs ->
                    let whom =
                        StubPatientData.data "stub-patient"
                        |> Informedica.GenForm.Lib.EhrPatientData.identity

                    SqlDatabase.persist cs [ Session.WriteVersion(v1.Value, whom) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    let identityOf () =
                        SqlDatabase.loadRecords cs "stub-patient" |> List.map StoredVersion.identity

                    identityOf () |> Expect.equal "the identity, on the readable version" [ whom ]

                    let run (sql: string) =
                        use conn = new SqliteConnection(cs)
                        conn.Open()
                        use cmd = conn.CreateCommand()
                        cmd.CommandText <- sql
                        cmd.ExecuteNonQuery() |> ignore

                    // the same row under a structure version this release does not know keeps
                    // naming its patient
                    run "update order_plan set json_version = 14"

                    (SqlDatabase.loadRecords cs "stub-patient"
                     |> List.map reasonOf
                     |> List.map Option.isSome,
                     identityOf ())
                    |> Expect.equal "unreadable, and still the identity" ([ true ], [ whom ])

                    // a row from before the columns: none
                    run
                        "update order_plan set json_version = 13, patient_name = null, birth_year = null, birth_month = null, birth_day = null"
                    identityOf () |> Expect.equal "none" [ None ]

                    // the columns half present, and a date the calendar does not have: a reason
                    run "update order_plan set patient_name = 'Stub'"

                    SqlDatabase.loadRecords cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "not all present" [ Some "the patient's name and birthdate are not all present" ]

                    run "update order_plan set birth_year = 2016, birth_month = 2, birth_day = 30"

                    SqlDatabase.loadRecords cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "no date" [ Some "the birthdate 2016-2-30 is no date" ]

                    // a year too wide for the calendar, as SQLite lets a column hold: a reason,
                    // not a failed load of the whole record
                    run "update order_plan set birth_year = 9999999999, birth_day = 28"

                    SqlDatabase.loadRecords cs "stub-patient"
                    |> List.map reasonOf
                    |> Expect.equal "too wide" [ Some "the birthdate 9999999999-2-28 is no date" ]

                    // all four present but the name blank: nobody is named so
                    run "update order_plan set birth_year = 2016, patient_name = ' '"

                    SqlDatabase.loadRecords cs "stub-patient"
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

                    insertRow cs "plan-1" 1 13 (currentFixtureText ())

                    SqlSchema.apply cs |> Expect.equal "migration 9" [ 9 ]

                    match SqlDatabase.loadRecords cs "stub-patient" with
                    | [ StoredVersion.Readable(v, whom) ] ->
                        (v.Id, whom) |> Expect.equal "readable, naming none" ("plan-1", None)
                    | other -> failtest $"expected one readable row, got %A{other}"
                )
            }

            test "a persisted order plan version loads back as written" {
                withRecord (fun cs ->
                    SqlDatabase.persist cs [ Session.WriteVersion(v1.Value, None) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    // compared in the stored form: a DateTime reads back as the same instant in UTC
                    SqlDatabase.loadRecords cs "stub-patient"
                    |> List.map storedForm
                    |> Expect.equal "the one version, readable" [ Some(SqlDatabase.toJson v1.Value) ]

                    SqlDatabase.loadRecords cs "another-patient"
                    |> Expect.isEmpty "another patient has none"
                )
            }

            test "a second version with the same number is a conflict with the head" {
                withRecord (fun cs ->
                    SqlDatabase.persist cs [ Session.WriteVersion(v1.Value, None) ] |> ignore

                    let rival =
                        { v1.Value with
                            Id = "plan-rival"
                            SignedBy =
                                {
                                    UserId = other.UserId
                                    DisplayName = other.DisplayName
                                }
                        }

                    match SqlDatabase.persist cs [ Session.WriteVersion(rival, None) ] with
                    | Session.StoreOutcome.Conflict winner ->
                        winner
                        |> storedForm
                        |> Expect.equal "the row that won" (Some(SqlDatabase.toJson v1.Value))
                    | other -> failtest $"expected Conflict, got %A{other}"
                )
            }

            test "a second version with the same id and a new number fails, it is no conflict" {
                withRecord (fun cs ->
                    SqlDatabase.persist cs [ Session.WriteVersion(v1.Value, None) ] |> ignore

                    match SqlDatabase.persist cs [ Session.WriteVersion({ v1.Value with No = 2 }, None) ] with
                    | Session.StoreOutcome.Failed reason ->
                        reason.Contains "order_plan.version_id"
                        |> Expect.isTrue "the reason names the column"
                    | other -> failtest $"expected Failed, got %A{other}"
                )
            }

            test "a read-only database fails with the reason" {
                withRecord (fun cs ->
                    let readOnly = SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly).ToString()

                    match SqlDatabase.persist readOnly [ Session.WriteVersion(v1.Value, None) ] with
                    | Session.StoreOutcome.Failed reason -> reason |> Expect.isNotEmpty "a reason"
                    | other -> failtest $"expected Failed, got %A{other}"

                    SqlDatabase.loadRecords cs "stub-patient" |> Expect.isEmpty "nothing written"
                )
            }

            test "rows this release cannot read load as unreadable, and the newest is the head" {
                withRecord (fun cs ->
                    let text = currentFixtureText ()
                    insertRow cs "plan-1" 1 13 text
                    // a structure version newer than the release knows
                    insertRow cs "plan-2" 2 14 text
                    // the identity in the JSON (plan-1, number 1) disagrees with the columns
                    insertRow cs "plan-3" 3 13 text

                    // a Dto the domain refuses: the plan is missing
                    let noPlan =
                        { Canonical.deserialize<OrderPlanVersion.Dto.Dto> text with
                            Id = "plan-4"
                            No = 4
                            Plan = Unchecked.defaultof<_>
                        }
                        |> Canonical.serialize

                    insertRow cs "plan-4" 4 13 noPlan

                    let loaded = SqlDatabase.loadRecords cs "stub-patient"

                    loaded
                    |> List.map StoredVersion.id
                    |> Expect.equal "newest first" [ "plan-4"; "plan-3"; "plan-2"; "plan-1" ]

                    loaded
                    |> List.map reasonOf
                    |> List.map Option.isSome
                    |> Expect.equal "three unreadable, the first readable" [ true; true; true; false ]

                    loaded[2]
                    |> reasonOf
                    |> Option.defaultValue ""
                    |> fun r -> r.Contains "newer than this release knows" |> Expect.isTrue $"the reason: %s{r}"

                    loaded[1]
                    |> reasonOf
                    |> Option.defaultValue ""
                    |> fun r -> r.Contains "identity" |> Expect.isTrue $"the reason: %s{r}"

                    loaded[0]
                    |> reasonOf
                    |> Option.defaultValue ""
                    |> fun r -> r.Contains "does not parse" |> Expect.isTrue $"the reason: %s{r}"

                    match loaded[0] with
                    | StoredVersion.Unreadable u ->
                        (u.No, u.SignedBy.UserId, u.SignedAt)
                        |> Expect.equal "the identity from the columns" (4, "prescriber", t0)
                    | StoredVersion.Readable _ -> failtest "the head should be unreadable"
                )
            }

            test "a malformed value in the JSON leaves the row unreadable" {
                withRecord (fun cs ->
                    // a BigRational that is no number: the converter throws, not the JSON reader
                    currentFixtureText().Replace("\"WeightKg\":\"32/1\"", "\"WeightKg\":\"x/y\"")
                    |> insertRow cs "plan-1" 1 13

                    match SqlDatabase.loadRecords cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason.Contains "does not read" |> Expect.isTrue $"the reason: %s{u.Reason}"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a signed_at out of range leaves the row unreadable at the earliest time" {
                withRecord (fun cs ->
                    insertRowAt cs Int64.MaxValue "plan-1" 1 14 (currentFixtureText ())

                    match SqlDatabase.loadRecords cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.SignedAt |> Expect.equal "the earliest time" DateTime.MinValue
                        u.Reason.Contains "out of range" |> Expect.isTrue $"the reason: %s{u.Reason}"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "this release reads and writes structure version 13; 0 to 12 and 14 are reasons" {
                let json = currentFixtureText ()

                (SqlDatabase.jsonVersionOldest, SqlDatabase.jsonVersionRead, SqlDatabase.jsonVersionWritten)
                |> Expect.equal "oldest, read and written" (13, 13, 13)

                SqlDatabase.upgrade 13 json |> Expect.equal "13 is current" (Ok json)

                for version in 0..12 do
                    SqlDatabase.upgrade version json
                    |> Expect.equal
                        $"%i{version}"
                        (Error $"JSON structure version %i{version} is older than this release reads")

                SqlDatabase.upgrade 14 json
                |> Expect.equal "14" (Error "JSON structure version 14 is newer than this release knows")
            }

            test "a stored version 12 row, with an intake and without links, loads as unreadable" {
                let text = v12FixtureText ()
                text.Contains "\"Intake\"" |> Expect.isTrue "the intake in the row"

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 12 text

                    match SqlDatabase.loadRecords cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason.Contains "older than this release reads"
                        |> Expect.isTrue $"the reason: %s{u.Reason}"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a signed version keeps the mark of an order that could not be calculated" {
                let text = (currentFixtureText ()).Replace("\"NotRecalculated\":false", "\"NotRecalculated\":true")

                match parse text with
                | Ok v ->
                    v.Plan.Contexts
                    |> Array.collect _.Context.Scenarios
                    |> Expect.all "marked" _.NotRecalculated

                    v |> SqlDatabase.toJson |> Expect.equal "written as read" text
                | other -> failtest $"expected a parsed version, got %A{other}"
            }

            test "a row with an argumentation loads back with it; the current fixture with none" {
                let text = "Sepsis, hogere dosis in overleg met de apotheek"

                let argued =
                    match parse (currentFixtureText ()) with
                    | Ok v ->
                        // the row's columns are authoritative: the JSON names the same version
                        { v with
                            Id = "plan-2"
                            No = 2
                            Plan =
                                { v.Plan with
                                    Contexts =
                                        v.Plan.Contexts
                                        |> Array.mapi (fun i pc ->
                                            if i = 0 then
                                                { pc with Context = { pc.Context with Argumentation = Some text } }
                                            else
                                                pc
                                        )
                                }
                        }
                    | Error e -> failtest $"%A{e}"

                argued.Plan.Contexts
                |> Array.isEmpty
                |> Expect.isFalse "the fixture has contexts"

                withRecord (fun cs ->
                    insertRow cs "plan-1" 1 13 (currentFixtureText ())
                    insertRow cs "plan-2" 2 13 (argued |> SqlDatabase.toJson)

                    let texts (v: OrderPlanVersion) =
                        v.Plan.Contexts |> Array.map _.Context.Argumentation |> Array.choose id

                    SqlDatabase.loadRecords cs "stub-patient"
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
                | Ok v ->
                    v
                    |> OrderPlanVersion.Dto.toDto
                    |> Canonical.serialize
                    |> Expect.equal "the same text" text
                | Error errs -> failtest $"%A{errs}"
            }

            test "the write snapshot: the current fixture, read and written as this release writes, is its text" {
                let text = currentFixtureText ()

                match text |> SqlDatabase.upgrade SqlDatabase.jsonVersionWritten |> Result.map parse with
                | Ok(Ok v) -> v |> SqlDatabase.toJson |> Expect.equal "the same text" text
                | other -> failtest $"%A{other}"
            }
        ]
