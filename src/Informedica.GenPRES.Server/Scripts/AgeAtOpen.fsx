/// The age at the open, and what the challenge compares. The open already projects the EHR
/// data at its date, so an identified patient opens on the birthdate's age at the clock's
/// date, the estimate is the one for that age, and the challenge projects a fresh read at the
/// date of the open; the first half of this script proves that against the source. The
/// challenge, however, still decides whether the data changed by comparing projections, and a
/// data notice is accepted by comparing the projection it was told over: the second half
/// states the rule the plan chose instead, on the EHR data as read, as the functions the
/// Session will call.
///
/// - Session.fs `Notice` and `Challenge`: each carries the EHR data it was told or issued
///   over, as read, beside the projection it keeps for the client and the version.
/// - Session.fs `Reads`: whether a fresh read tells a change, none or other than the Session
///   opened on; whether the notice a token names was told over exactly this read; and the EHR
///   data a Session holds after a commit, the challenge's read, else what it opened on.
///
/// The store's columns for the two records follow in the migration.
///
/// Needs the network for one test, the estimate over the computed age; it is skipped when the
/// normal-value sheets cannot be fetched.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi AgeAtOpen.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
// the contract before the domain, so that the domain's units, measures and patient win unqualified
open Shared.Types
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib
open Informedica.GenCore.Lib.Patients
open Informedica.GenForm.Lib
open ServerApi


// ── Session.fs, the records and the reads a challenge compares ────────────────────────────


/// A data notice as the store holds it: one per Session, the EHR data it was told over as
/// read and its projection (none: unreadable), for two minutes.
type Notice =
    {
        Nonce: string
        /// the EHR data as read when the notice was told; none when it could not be read
        Ehr: EhrPatientData option
        /// its projection, what the client is shown
        Data: Types.Patient option
        Expiry: DateTime
    }


/// A signing challenge as the store holds it: one per Session, the digest of exactly the plan
/// shown, the EHR data as read when it was issued and its projection at the date of the open,
/// for two minutes.
type Challenge =
    {
        Nonce: string
        Digest: string
        /// the EHR data as read at the challenge; none when it could not be read
        Ehr: EhrPatientData option
        /// its projection at the date of the open, the patient the version is signed on
        Reading: Types.Patient option
        Expiry: DateTime
    }


/// What a challenge compares: the EHR data as read, never its projection, whose age moves
/// with the clock.
module Reads =

    /// Whether a fresh read tells a change: none, or other than the EHR data the Session
    /// opened on. A Session that opened on none is told on every read that answers.
    let changed (opened: EhrPatientData option) (current: EhrPatientData option) =
        current.IsNone || current <> opened


    /// The notice the token names, when the Session has one and it was told over exactly the
    /// read at hand; a read that moved since is a fresh notice.
    let accepted (notices: Map<string, Notice>) (sid: string) (token: string option) (current: EhrPatientData option) =
        token
        |> Option.bind (fun token ->
            notices
            |> Map.tryFind sid
            |> Option.filter (fun n -> n.Nonce = token && n.Ehr = current)
        )


    /// The EHR data a Session holds after a commit: the read the challenge was issued over,
    /// so that the next challenge compares against the data just signed on; what it opened on
    /// when the challenge had none.
    let afterCommit (challenge: Challenge) (opened: EhrPatientData option) =
        challenge.Ehr |> Option.orElse opened


// ── The proof ─────────────────────────────────────────────────────────────────────────────


/// The clock of the tests: a September day, half a year past the stub patient's tenth birthday.
let today = DateTime(2026, 9, 26)


let stub = StubPatientData.data "p1"


let ageInDays (pat: Types.Patient option) =
    pat
    |> Option.bind _.Age
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


/// The stub's EHR data with a weight the EHR has since changed.
let heavier =
    let w = WeightAtDate.create today (WeightValue.weightInKg 34m)

    { stub with
        Patient =
            { stub.Patient with
                Weight = Weight.create [ w ] None [] (Some w)
            }
    }


/// The stub's EHR data without any measurement, so that the estimate is the only weight.
let unmeasured =
    { stub with
        Patient =
            { stub.Patient with
                Weight = Weight.unknown
                Height = Height.unknown
            }
    }


let noticeOver nonce ehr : Notice =
    {
        Nonce = nonce
        Ehr = ehr
        Data = ehr |> Option.map (StubPatientData.port.patient today)
        Expiry = today.AddMinutes 2.0
    }


let challengeOver ehr : Challenge =
    {
        Nonce = "c-1"
        Digest = "digest"
        Ehr = ehr
        Reading = ehr |> Option.map (StubPatientData.port.patient today)
        Expiry = today.AddMinutes 2.0
    }


let tests =
    testList
        "the age at the open"
        [
            testList
                "the open, as the source has it"
                [
                    test "an identified patient opens on the birthdate's age at the clock's date" {
                        // born 2016-03-15: ten years, six months, one week and four days
                        Session.sessionPatient today StubPatientData.port (Some stub) None
                        |> ageInDays
                        |> Expect.equal "3650 + 180 + 7 + 4 days" (Some 3841N)
                    }

                    test "the age follows the clock, not the reading: a day later, a day more" {
                        Session.sessionPatient (today.AddDays 1.0) StubPatientData.port (Some stub) None
                        |> ageInDays
                        |> Expect.equal "one day more" (Some 3842N)
                    }

                    test "EHR data with an age value in place of a birthdate opens on that age, whatever the date" {
                        let aged =
                            { stub with
                                Patient =
                                    { stub.Patient with
                                        Age = AgeValue.ten |> PatientAge.ageValue
                                    }
                            }

                        [ today; today.AddYears 1 ]
                        |> List.map (fun dt ->
                            Session.sessionPatient dt StubPatientData.port (Some aged) None |> ageInDays
                        )
                        |> Expect.allEqual "ten years" (Some 3650N)
                    }

                    test "the estimate is the one for the computed age" {
                        match Mapping.getNormalValueRows Mapping.normalValuesUrlId with
                        | Error e -> skiptest $"the normal-value sheets could not be fetched: %A{e}"
                        | Ok rows ->
                            let tables = rows |> Shared.Models.NormalValues.ofRows
                            let port = StubPatientData.port |> Patient.estimating (fun () -> Some tables)

                            let opened = Session.sessionPatient today port (Some unmeasured) None

                            // the contract's own estimate for a patient of that many days
                            let expected =
                                { Shared.Models.Patient.empty with
                                    Age = Some(Shared.Models.Patient.Age.fromDays 3841)
                                }
                                |> Shared.Models.NormalValues.apply tables

                            (opened |> Option.map (fun p -> p.WeightMeasured, p.Weight.IsSome),
                             opened |> Option.bind _.Weight |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram))
                            |> Expect.equal
                                "estimated, and the contract's estimate"
                                (Some(false, true),
                                 expected.Weight.Estimated
                                 |> Option.map (fun g ->
                                     BigRational.fromInt (int g) / 1000N
                                     |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                 ))
                    }
                ]

            testList
                "what the challenge compares"
                [
                    test "an unchanged read is no change, on whatever day it is read" {
                        Reads.changed (Some stub) (Some(StubPatientData.data "p1"))
                        |> Expect.isFalse "the same EHR data, read again"
                    }

                    test "a projection would have said otherwise: a day later, the age differs" {
                        let at dt = StubPatientData.port.patient dt stub

                        (at today = at (today.AddDays 1.0))
                        |> Expect.isFalse "the projection moves with the clock"
                    }

                    test "a changed weight is a change; no read is a change; a Session opened on none is told" {
                        [
                            Reads.changed (Some stub) (Some heavier)
                            Reads.changed (Some stub) None
                            Reads.changed None (Some stub)
                            Reads.changed None None
                        ]
                        |> Expect.allEqual "told, every one" true
                    }

                    test "a notice is accepted over exactly the read it was told over" {
                        let notices = Map.ofList [ "s-1", noticeOver "n-1" (Some heavier) ]

                        (Reads.accepted notices "s-1" (Some "n-1") (Some heavier) |> Option.map _.Nonce,
                         Reads.accepted notices "s-1" (Some "n-1") (Some stub),
                         Reads.accepted notices "s-1" (Some "n-2") (Some heavier),
                         Reads.accepted notices "s-1" None (Some heavier),
                         Reads.accepted notices "s-2" (Some "n-1") (Some heavier))
                        |> Expect.equal
                            "the token over the same read; not another read, token or Session"
                            (Some "n-1", None, None, None, None)
                    }

                    test "a notice over no read is accepted over no read" {
                        let notices = Map.ofList [ "s-1", noticeOver "n-1" None ]

                        Reads.accepted notices "s-1" (Some "n-1") None
                        |> Option.map _.Nonce
                        |> Expect.equal "unverified, accepted as such" (Some "n-1")
                    }

                    test "after a commit the Session holds the challenge's read, else what it opened on" {
                        (Reads.afterCommit (challengeOver (Some heavier)) (Some stub),
                         Reads.afterCommit (challengeOver None) (Some stub))
                        |> Expect.equal "the read just signed on; the one opened on" (Some heavier, Some stub)
                    }

                    test "the notice and the challenge keep the projection beside the read" {
                        ((noticeOver "n-1" (Some stub)).Data |> ageInDays,
                         (challengeOver (Some stub)).Reading |> ageInDays)
                        |> Expect.equal "the projection at the date given" (Some 3841N, Some 3841N)
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
