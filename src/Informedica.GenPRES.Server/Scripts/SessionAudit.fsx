// The session store on SQLite (plan 516), step 9: the audit of what was done, by whom and to
// which Session. `Sql/005-audit.sql` is migration 5 and ships with this script.
//
// Script-first draft (script-only policy) of what `module SqlSessions` of
// `ServerApi.SqlAdapters.fs` gains. Build first (`dotnet run Build`), then run
// `dotnet fsi SessionAudit.fsx` from this directory.
//
// The design this drafts, and the reason for it: an entry is **derived from the writes a
// request already returned**, not asked for separately. The machine says what it did once, in
// values the store runs in one transaction; an audit written from those values cannot describe
// an act that did not land, cannot be forgotten by a member that returns writes, and cannot
// drift into a second vocabulary for the same facts. The plan called for an `AppendAudit` case
// the members return; this is the same audit with one place to write it instead of a case to
// remember at every call site.

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

open System
open Shared.Types
open ServerApi


module SqlAudit =

    open Microsoft.Data.Sqlite


    /// One line of the audit: when, to which Session, by whom, what was done and how it came
    /// out, with what the action needs beside its name.
    type Entry =
        {
            At: DateTime
            SessionId: string option
            Actor: string option
            Action: string
            Outcome: string
            Detail: string option
        }


    let private entry at action outcome =
        {
            At = at
            SessionId = None
            Actor = None
            Action = action
            Outcome = outcome
            Detail = None
        }


    /// <summary>
    /// The audit of one write. Every case matched, so that a write nobody thought to audit
    /// fails to compile rather than passing unseen. The writes that only carry a fact already
    /// audited by the act they belong to — the heartbeat, what a Session opened with — are
    /// audited as nothing, said here rather than left out.
    /// </summary>
    let ofWrite (write: Session.Persist) : Entry option =
        match write with
        | Session.RecordLaunch r ->
            Some
                { entry r.Expiry "launched" "ok" with
                    Detail = Some $"""{{"patient":"%s{r.PatientId}"}}"""
                }
        | Session.RecordLaunchOutcome(_, outcome, at) ->
            match outcome with
            | LaunchResult.Opened(sid, opened) ->
                Some
                    { entry at "opened" "ok" with
                        SessionId = Some sid
                        Actor = opened.User |> Option.map _.UserId
                    }
            | LaunchResult.Refused refusal ->
                Some
                    { entry at "launch" "refused" with
                        Detail = Some $"""{{"refusal":"%s{Session.refusalWord refusal}"}}"""
                    }
            | LaunchResult.Enrolling attempt ->
                Some
                    { entry at "enrolling" "ok" with
                        Detail = Some $"""{{"attempt":"%s{attempt}"}}"""
                    }
            // a redirect is no outcome: it is what a launch is answered with until one
            | LaunchResult.RedirectTo _ -> None
        | Session.OpenSession(sid, session) ->
            Some
                { entry session.Seen "session-opened" "ok" with
                    SessionId = Some sid
                    Actor = session.Opened.User |> Option.map _.UserId
                }
        | Session.EndSession(sid, ending, at) ->
            let word =
                match ending with
                | Session.StoredEnding.Closed -> "closed"
                | Session.StoredEnding.Ended e -> $"%A{e}"

            Some
                { entry at "session-ended" "ok" with
                    SessionId = Some sid
                    Detail = Some $"""{{"ending":"%s{word}"}}"""
                }
        | Session.WriteVersion v ->
            Some
                { entry v.SignedAt "signed" "ok" with
                    Actor = Some v.SignedBy.UserId
                    Detail = Some $"""{{"version":"%s{v.Id}","no":%i{v.No},"patient":"%s{v.PatientId}"}}"""
                }
        | Session.WriteCredential(userId, event, _, at) ->
            Some
                { entry at $"credential-%s{event}" "ok" with
                    Actor = Some userId
                }
        | Session.WriteCode(code, at) ->
            Some
                { entry at "code-mailed" "ok" with
                    Actor = Some code.UserId
                }
        | Session.CountCodeTry(userId, _, at) ->
            Some
                { entry at "code-entered" "refused" with
                    Actor = Some userId
                }
        | Session.RememberAnswer(sid, _, outcome, at) ->
            match outcome with
            // the signature itself is audited by the version it wrote
            | SigningOutcome.Submitted _ -> None
            | SigningOutcome.Refused refusal ->
                Some
                    { entry at "sign" "refused" with
                        SessionId = Some sid
                        Detail = Some $"""{{"refusal":"%A{refusal}"}}"""
                    }
            | _ -> None
        // facts of an act that is audited by another of its writes
        | Session.RecordOpenedWith _
        | Session.RecordSeen _
        | Session.AcknowledgeEnding _
        | Session.SpendCode _
        | Session.WriteEnrolment _
        | Session.DropEnrolmentWrite _
        | Session.DropEnrolmentsOf _
        | Session.WriteNotice _
        | Session.WriteChallenge _
        | Session.SpendChallenge _ -> None


    let ofWrites (writes: Session.Persist list) = writes |> List.choose ofWrite


    let private nullable (value: 'a option) =
        value |> Option.map box |> Option.defaultValue (box DBNull.Value)


    /// The entries of a request, in the transaction that carries its writes.
    let append (conn: SqliteConnection) (tx: SqliteTransaction) (entries: Entry list) =
        for e in entries do
            use cmd =
                SqlSessions.command
                    conn
                    tx
                    """
                    insert into audit_entry (at, session_id, actor, action, outcome, detail)
                    values ($at, $sid, $actor, $action, $outcome, $detail)
                    """
                    [
                        "$at", box (SqlSessions.ms e.At)
                        "$sid", nullable e.SessionId
                        "$actor", nullable e.Actor
                        "$action", box e.Action
                        "$outcome", box e.Outcome
                        "$detail", nullable e.Detail
                    ]

            cmd.ExecuteNonQuery() |> ignore


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlAuditTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests


let now = DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)


let sessionOf sid login : Session.SessionRecord =
    {
        Opened =
            {
                User =
                    Some
                        {
                            UserId = login
                            DisplayName = "Stub Prescriber"
                            Role = UserRole.Prescriber
                        }
                PatientId = Some "stub-patient"
                Patient = None
                OpenedToken = Some(OpenedToken "opened-1")
                KeyThumbprint = None
                Head = None
            }
        Login = Some login
        OpenedWith = None
        Seen = now
    }


let launchOf nonce : Session.LaunchRecord =
    {
        Nonce = nonce
        State = $"state-%s{nonce}"
        PatientId = "stub-patient"
        PublicKey = keyA
        Expiry = now.AddMinutes 2.0
        Outcome = None
    }


let tests =
    testList
        "the audit of what a request did"
        [
            test "an open is audited once, by the act and not by its facts" {
                let session = sessionOf "s-1" "prescriber"

                let entries =
                    SqlAudit.ofWrites
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, now)
                            Session.RecordSeen("s-1", now)
                        ]

                entries |> List.map _.Action |> Expect.equal "the open alone" [ "session-opened" ]
                entries.Head.Actor |> Expect.equal "by whom" (Some "prescriber")
                entries.Head.SessionId |> Expect.equal "to which Session" (Some "s-1")
            }

            test "a launch and what it came to are audited, honoured or refused" {
                SqlAudit.ofWrites [ Session.RecordLaunch(launchOf "n-1") ]
                |> List.map _.Action
                |> Expect.equal "the launch" [ "launched" ]

                SqlAudit.ofWrites
                    [ Session.RecordLaunchOutcome("n-1", LaunchResult.Refused LaunchRefusal.WrongActivePatient, now) ]
                |> List.map (fun e -> e.Action, e.Outcome, e.Detail)
                |> Expect.equal
                    "the refusal, by its word"
                    [ "launch", "refused", Some """{"refusal":"wrong-patient"}""" ]

                SqlAudit.ofWrites [ Session.RecordLaunchOutcome("n-1", LaunchResult.Enrolling "a-1", now) ]
                |> List.map _.Action
                |> Expect.equal "the launch suspended into enrolment" [ "enrolling" ]
            }

            test "a signature is audited by the version it wrote, and a refusal by itself" {
                let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0

                SqlAudit.ofWrites
                    [
                        Session.WriteVersion v1
                        Session.RememberAnswer("s-1", "k-1", SigningOutcome.Submitted(v1, OpenedToken "t"), now)
                    ]
                |> List.map (fun e -> e.Action, e.Actor)
                |> Expect.equal "the signature, once" [ "signed", Some "prescriber" ]

                SqlAudit.ofWrites
                    [
                        Session.RememberAnswer("s-1", "k-1", SigningOutcome.Refused SigningRefusal.PinLimit, now)
                    ]
                |> List.map (fun e -> e.Action, e.Outcome)
                |> Expect.equal "the refusal" [ "sign", "refused" ]
            }

            test "a PIN set, a code mailed and a code entered wrongly each say who" {
                let credential = Credential.withPin salts "1234"

                SqlAudit.ofWrites
                    [
                        Session.WriteCredential("no-pin", "pin-set", credential, now)
                        Session.WriteCode(
                            {
                                UserId = "no-pin"
                                MailAddress = "n***@stub.example"
                                CodeMac = [| 1uy |]
                                Expiry = now
                                Tries = 0
                            },
                            now
                        )
                        Session.CountCodeTry("no-pin", [| 1uy |], now)
                    ]
                |> List.map (fun e -> e.Action, e.Outcome, e.Actor)
                |> Expect.equal
                    "each act, by the person it was for"
                    [
                        "credential-pin-set", "ok", Some "no-pin"
                        "code-mailed", "ok", Some "no-pin"
                        "code-entered", "refused", Some "no-pin"
                    ]
            }

            test "the end of a Session is audited with the ending it was" {
                SqlAudit.ofWrites
                    [
                        Session.EndSession("s-1", Session.StoredEnding.Ended SessionEnding.WrongPinLimit, now)
                        Session.AcknowledgeEnding("s-1", now)
                    ]
                |> List.map (fun e -> e.Action, e.Detail)
                |> Expect.equal
                    "the ending, once"
                    [ "session-ended", Some """{"ending":"WrongPinLimit"}""" ]
            }
        ]


runTestsWithCLIArgs [] [||] tests
