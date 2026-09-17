// The session store on SQLite (plan 516), step 3: the record and the first stored fixture.
//
// Script-first draft (script-only policy) of the module `SqlDatabase` of
// `ServerApi.SqlAdapters.fs`, and of the tests of `SqlRecordTests.fs`. The tests use the
// builders of `SessionStoreTests.fs` and the `withDb` helper of `SqlSchemaTests.fs`, so this
// script references the built server and server test assemblies instead of loading `load.fsx`:
// a type loaded from source would not be the type those assemblies name. Build first
// (`dotnet run Build`), then run `dotnet fsi SqlRecord.fsx` from this directory.
//
// The first run writes the stored fixture `order_plan_v1.json` when it does not exist yet;
// after that it is never regenerated.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Microsoft.Data.Sqlite, 10.0.12"
#I "../../../tests/Informedica.GenPRES.Server.Tests/bin/Debug/net10.0"
#r "Newtonsoft.Json.dll"
#r "Informedica.Utils.Lib.dll"
#r "Informedica.GenUNITS.Lib.dll"
#r "Informedica.GenSOLVER.Lib.dll"
#r "Informedica.GenCORE.Lib.dll"
#r "Informedica.GenFORM.Lib.dll"
#r "Informedica.GenORDER.Lib.dll"
#r "Informedica.GenPRES.Shared.dll"
#r "Informedica.GenPRES.Server.dll"
#r "Informedica.GenPRES.Server.Tests.dll"


// ---------------------------------------------------------------------------------------------
// The record (→ ServerApi.SqlAdapters.fs, module SqlDatabase)
// ---------------------------------------------------------------------------------------------

open ServerApi


/// <summary>
/// The record on SQLite: the order plan versions of a patient loaded from `order_plan`, and
/// the write of a commit inserted there. The plan is stored as the canonical JSON of
/// `OrderPlanVersion.Dto` under a JSON structure version; a row this release cannot read
/// loads as an unreadable entry built from the identity columns, never dropped.
/// </summary>
module SqlDatabase =

    open System
    open Microsoft.Data.Sqlite
    open Informedica.GenOrder.Lib


    /// The highest JSON structure version this release reads.
    let jsonVersionRead = 1

    /// The JSON structure version this release writes.
    let jsonVersionWritten = 1


    /// Brings plan JSON written under a structure version to the structure this release
    /// reads, one pure step per version on raw JSON. There are no steps yet.
    let upgrade (version: int) (json: string) : Result<string, string> =
        if version < 1 then
            Error $"JSON structure version %i{version} does not exist"
        elif version > jsonVersionRead then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


    /// The JSON this release writes for an order plan version.
    let toJson (v: Types.OrderPlanVersion) =
        v |> OrderPlanVersion.Dto.toDto |> Canonical.serialize


    /// An `order_plan` row as read, before its JSON is parsed.
    type Row =
        {
            VersionId: string
            No: int
            PatientId: string
            Base: string option
            SignedByUserId: string
            SignedByDisplayName: string
            SignedAt: int64
            JsonVersion: int
            Plan: string
        }


    /// <summary>
    /// A row as the record holds it: upgraded, parsed with `fromDto`, and checked against its
    /// identity columns; else unreadable with the reason, its identity from the columns, which
    /// are authoritative.
    /// </summary>
    let readRow (row: Row) : StoredVersion =
        let parsed =
            upgrade row.JsonVersion row.Plan
            |> Result.bind (fun json ->
                try
                    json
                    |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                    |> OrderPlanVersion.Dto.fromDto
                    |> Result.mapError (fun errs -> $"the order plan version does not parse: %A{errs}")
                with :? Newtonsoft.Json.JsonException as e ->
                    Error $"the JSON does not read: %s{e.Message}"
            )
            |> Result.bind (fun v ->
                if v.Id = row.VersionId && v.No = row.No && v.PatientId = row.PatientId && v.Base = row.Base then
                    Ok v
                else
                    Error "the identity in the JSON disagrees with the columns"
            )

        match parsed with
        | Ok v -> StoredVersion.Readable v
        | Error reason ->
            StoredVersion.Unreadable
                {
                    Id = row.VersionId
                    No = row.No
                    PatientId = row.PatientId
                    Base = row.Base
                    SignedBy =
                        {
                            UserId = row.SignedByUserId
                            DisplayName = row.SignedByDisplayName
                        }
                    SignedAt = DateTimeOffset.FromUnixTimeMilliseconds(row.SignedAt).UtcDateTime
                    Reason = reason
                }


    /// Every order plan version of a patient, newest first.
    let loadRecords (connectionString: string) (patientId: string) : StoredVersion list =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        use cmd = conn.CreateCommand()

        cmd.CommandText <-
            """
            select version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                   signed_at, json_version, plan
            from order_plan where patient_id = $patient order by no desc
            """

        cmd.Parameters.AddWithValue("$patient", patientId) |> ignore
        use r = cmd.ExecuteReader()

        [
            while r.Read() do
                readRow
                    {
                        VersionId = r.GetString 0
                        No = r.GetInt32 1
                        PatientId = r.GetString 2
                        Base = if r.IsDBNull 3 then None else Some(r.GetString 3)
                        SignedByUserId = r.GetString 4
                        SignedByDisplayName = r.GetString 5
                        SignedAt = r.GetInt64 6
                        JsonVersion = r.GetInt32 7
                        Plan = r.GetString 8
                    }
        ]


    let private insert (connectionString: string) (v: Types.OrderPlanVersion) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        use cmd = conn.CreateCommand()

        cmd.CommandText <-
            """
            insert into order_plan
                (version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                 signed_at, verified, json_version, plan)
            values ($id, $no, $patient, $base, $user, $name, $at, $verified, $json_version, $plan)
            """

        let add (name: string) (value: obj) =
            cmd.Parameters.AddWithValue(name, value) |> ignore

        add "$id" v.Id
        add "$no" v.No
        add "$patient" v.PatientId
        add "$base" (v.Base |> Option.map box |> Option.defaultValue DBNull.Value)
        add "$user" v.SignedBy.UserId
        add "$name" v.SignedBy.DisplayName
        add "$at" (DateTimeOffset(v.SignedAt, TimeSpan.Zero).ToUnixTimeMilliseconds())
        add "$verified" (if v.Verified then 1 else 0)
        add "$json_version" jsonVersionWritten
        add "$plan" (toJson v)
        cmd.ExecuteNonQuery() |> ignore


    /// <summary>
    /// Runs the write of a commit: inserts the order plan version. A violated
    /// `unique (patient_id, no)` is another server's sign of the same number, answered with
    /// the head as it stands now; any other failure, other constraints included, is `Failed`
    /// with the reason, so that the caller keeps its state and the next Submission retries.
    /// </summary>
    let persist (connectionString: string) (write: Session.Persist) : Session.StoreOutcome =
        let (Session.WriteVersion v) = write

        try
            insert connectionString v
            Session.StoreOutcome.Written
        with
        | :? SqliteException as e when
            e.SqliteExtendedErrorCode = 2067
            && e.Message.Contains "order_plan.patient_id, order_plan.no"
            ->
            try
                match loadRecords connectionString v.PatientId with
                | head :: _ -> Session.StoreOutcome.Conflict head
                | [] -> Session.StoreOutcome.Failed e.Message
            with reread ->
                Session.StoreOutcome.Failed reread.Message
        | e -> Session.StoreOutcome.Failed e.Message


// ---------------------------------------------------------------------------------------------
// The stored fixture, written once
// ---------------------------------------------------------------------------------------------

open System
open System.IO
open Informedica.GenPRES.Server.Tests.SessionStoreTests

let fixtures =
    Path.Combine(__SOURCE_DIRECTORY__, "../../../tests/Informedica.GenPRES.Server.Tests/fixtures")

let fixtureV1 = Path.Combine(fixtures, "order_plan_v1.json")

if not (File.Exists fixtureV1) then
    File.WriteAllText(fixtureV1, versionOf 1 prescriber t0 domainPlan.Value |> SqlDatabase.toJson)
    printfn $"wrote %s{fixtureV1}"


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlRecordTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenOrder.Lib
open Informedica.GenPRES.Server.Tests.SqlSchemaTests


/// A fresh database file with the schema applied.
let withRecord (f: string -> 'a) =
    withDb (fun cs ->
        SqlSchema.apply cs |> ignore
        f cs
    )


let v1 = lazy (versionOf 1 prescriber t0 domainPlan.Value)


/// Inserts a row as SQL, bypassing `persist`, for rows no release of this code would write.
let insertRow (cs: string) (versionId: string) (no: int) (jsonVersion: int) (plan: string) =
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
    cmd.Parameters.AddWithValue("$at", DateTimeOffset(t0, TimeSpan.Zero).ToUnixTimeMilliseconds()) |> ignore
    cmd.Parameters.AddWithValue("$jv", jsonVersion) |> ignore
    cmd.Parameters.AddWithValue("$plan", plan) |> ignore
    cmd.ExecuteNonQuery() |> ignore


let fixtureText () =
    File.ReadAllText fixtureV1


let parse (text: string) =
    text
    |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
    |> OrderPlanVersion.Dto.fromDto


let reasonOf =
    function
    | StoredVersion.Unreadable u -> Some u.Reason
    | StoredVersion.Readable _ -> None


let tests =
    testList
        "SqlDatabase"
        [
            test "a persisted order plan version loads back as written" {
                withRecord (fun cs ->
                    SqlDatabase.persist cs (Session.WriteVersion v1.Value)
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    SqlDatabase.loadRecords cs "stub-patient"
                    |> Expect.equal "the one version, readable" [ StoredVersion.Readable v1.Value ]

                    SqlDatabase.loadRecords cs "another-patient"
                    |> Expect.isEmpty "another patient has none"
                )
            }

            test "a second version with the same number is a conflict with the head" {
                withRecord (fun cs ->
                    SqlDatabase.persist cs (Session.WriteVersion v1.Value) |> ignore

                    let rival =
                        { v1.Value with
                            Id = "plan-rival"
                            SignedBy =
                                {
                                    UserId = other.UserId
                                    DisplayName = other.DisplayName
                                }
                        }

                    SqlDatabase.persist cs (Session.WriteVersion rival)
                    |> Expect.equal "the row that won" (Session.StoreOutcome.Conflict(StoredVersion.Readable v1.Value))
                )
            }

            test "a second version with the same id and a new number fails, it is no conflict" {
                withRecord (fun cs ->
                    SqlDatabase.persist cs (Session.WriteVersion v1.Value) |> ignore

                    match SqlDatabase.persist cs (Session.WriteVersion { v1.Value with No = 2 }) with
                    | Session.StoreOutcome.Failed reason ->
                        reason.Contains "order_plan.version_id"
                        |> Expect.isTrue "the reason names the column"
                    | other -> failtest $"expected Failed, got %A{other}"
                )
            }

            test "a read-only database fails with the reason" {
                withRecord (fun cs ->
                    let readOnly =
                        SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly).ToString()

                    match SqlDatabase.persist readOnly (Session.WriteVersion v1.Value) with
                    | Session.StoreOutcome.Failed reason -> reason |> Expect.isNotEmpty "a reason"
                    | other -> failtest $"expected Failed, got %A{other}"

                    SqlDatabase.loadRecords cs "stub-patient"
                    |> Expect.isEmpty "nothing written"
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

                    loaded[2] |> reasonOf |> Option.defaultValue ""
                    |> fun r -> r.Contains "newer than this release knows" |> Expect.isTrue $"the reason: %s{r}"

                    loaded[1] |> reasonOf |> Option.defaultValue ""
                    |> fun r -> r.Contains "identity" |> Expect.isTrue $"the reason: %s{r}"

                    loaded[0] |> reasonOf |> Option.defaultValue ""
                    |> fun r -> r.Contains "does not parse" |> Expect.isTrue $"the reason: %s{r}"

                    match loaded[0] with
                    | StoredVersion.Unreadable u ->
                        (u.No, u.SignedBy.UserId, u.SignedAt)
                        |> Expect.equal "the identity from the columns" (4, "prescriber", t0)
                    | StoredVersion.Readable _ -> failtest "the head should be unreadable"
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
                | Ok v -> v |> OrderPlanVersion.Dto.toDto |> Canonical.serialize |> Expect.equal "the same text" text
                | Error errs -> failtest $"%A{errs}"
            }

            test "the write snapshot: the fixture, read and written as this release writes, is its text" {
                let text = fixtureText ()

                match text |> SqlDatabase.upgrade SqlDatabase.jsonVersionWritten |> Result.map parse with
                | Ok(Ok v) -> v |> SqlDatabase.toJson |> Expect.equal "the same text" text
                | other -> failtest $"%A{other}"
            }
        ]


runTestsWithCLIArgs [] [||] tests
