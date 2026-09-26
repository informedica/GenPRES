/// The record on SQLite: order plan versions persisted and loaded on a real file, the rows a
/// release cannot read kept as unreadable entries, and the stored fixture of JSON structure
/// version 1 read and written as this release reads and writes.
module Informedica.GenPRES.Server.Tests.SqlRecordTests

open System
open System.IO
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
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


/// The stored fixture of structure version 1, copied to the test output.
let fixtureText () =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "order_plan_v1.json"))


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
                    let text = fixtureText ()
                    insertRow cs "plan-1" 1 1 text
                    // a structure version newer than the release knows
                    insertRow cs "plan-2" 2 9 text
                    // the identity in the JSON (plan-1, number 1) disagrees with the columns
                    insertRow cs "plan-3" 3 1 text

                    // a Dto the domain refuses: the plan is missing
                    let noPlan =
                        { Canonical.deserialize<OrderPlanVersion.Dto.Dto> text with
                            Id = "plan-4"
                            No = 4
                            Plan = Unchecked.defaultof<_>
                        }
                        |> Canonical.serialize

                    insertRow cs "plan-4" 4 1 noPlan

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
                    fixtureText().Replace("\"WeightKg\":\"32/1\"", "\"WeightKg\":\"x/y\"")
                    |> insertRow cs "plan-1" 1 1

                    match SqlDatabase.loadRecords cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.Reason.Contains "does not read" |> Expect.isTrue $"the reason: %s{u.Reason}"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "a signed_at out of range leaves the row unreadable at the earliest time" {
                withRecord (fun cs ->
                    insertRowAt cs Int64.MaxValue "plan-1" 1 9 (fixtureText ())

                    match SqlDatabase.loadRecords cs "stub-patient" with
                    | [ StoredVersion.Unreadable u ] ->
                        u.SignedAt |> Expect.equal "the earliest time" DateTime.MinValue
                        u.Reason.Contains "out of range" |> Expect.isTrue $"the reason: %s{u.Reason}"
                    | other -> failtest $"expected one unreadable row, got %A{other}"
                )
            }

            test "L5: the stored fixture of structure version 1 upgrades and parses" {
                match fixtureText () |> SqlDatabase.upgrade 1 |> Result.map parse with
                | Ok(Ok v) -> (v.Id, v.No) |> Expect.equal "its identity" ("plan-1", 1)
                | other -> failtest $"expected a parsed version, got %A{other}"
            }

            test "the read snapshot: the fixture parses and serializes back to its text" {
                let text = fixtureText ()

                match parse text with
                | Ok v ->
                    v
                    |> OrderPlanVersion.Dto.toDto
                    |> Canonical.serialize
                    |> Expect.equal "the same text" text
                | Error errs -> failtest $"%A{errs}"
            }

            test "the write snapshot: the fixture, read and written as this release writes, is its text" {
                let text = fixtureText ()

                match text |> SqlDatabase.upgrade SqlDatabase.jsonVersionWritten |> Result.map parse with
                | Ok(Ok v) -> v |> SqlDatabase.toJson |> Expect.equal "the same text" text
                | other -> failtest $"%A{other}"
            }
        ]
