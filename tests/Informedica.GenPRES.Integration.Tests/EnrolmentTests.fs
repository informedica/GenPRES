/// UC-2, the first launch of a Prescriber without a PIN: the launch suspends, a confirmation code
/// is mailed, and the Session opens once the code comes back with a PIN of the User's choosing
/// (docs/scenarios/integration/uc-02-enrolment.md).
///
/// A PIN is set once: once no-pin enrolled, every later launch of it opens straight away. On the
/// in-memory store a restart of the demo server forgets the PIN, so these tests run on a server
/// on which no-pin has not enrolled yet, and skip themselves on any other.
module Informedica.GenPRES.Integration.Tests.EnrolmentTests

open PuppeteerSharp

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The PIN the tests enrol no-pin with.
let newPin = "4321"


/// A six-digit code that is not the one given.
let otherCode (code: string) = ((int code + 1) % 1_000_000).ToString("D6")


/// Launches no-pin on the patient, and waits until the gate is the enrolment form; skips the
/// test when no-pin opens a Session instead, having set its PIN earlier on this server.
let launchEnrolling (baseUrl: string) (patientId: string) (page: IPage) =
    task {
        let opened = TitleBar.sessionButton (Identity.displayName Identity.NoPin)

        do! page |> launch baseUrl patientId Identity.NoPin

        do! page |> Browser.waitVisible $"%s{Gate.Enrolment.code}, %s{opened}"

        let! enrolled = page |> Browser.isVisible opened

        if enrolled then
            skiptest
                "no-pin set its PIN earlier on this server, and a PIN is set once: restart the demo server on the in-memory store to test the enrolment"
    }


/// The confirmation code that stands for no-pin: the newest mailed, since a launch while a code
/// stands mails none, and every new code drops the one before.
let standingCode (baseUrl: string) (page: IPage) =
    task {
        let! mails = StubMail.read baseUrl page

        match
            mails
            |> StubMail.about StubMail.confirmationCode (Identity.mailAddress Identity.NoPin)
        with
        | newest :: _ as all -> return StubMail.codeOf newest, all.Length
        | [] -> return failtest "the launch suspended into enrolment, but no confirmation code was mailed"
    }


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-2 enrolment"
        [
            testTask "three wrong codes void the code, and a relaunch mails a fresh one" {
                do!
                    Browser.withPage
                        "enrolment-code-void"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page
                                let patientId = newPatientId "enrolment"

                                do! page |> launchEnrolling baseUrl patientId
                                let! code, mailed = page |> standingCode baseUrl

                                // the first two wrong codes keep the form and tell the tries left
                                for left in [ 2; 1 ] do
                                    do! page |> Gate.Enrolment.supply (otherCode code) newPin

                                    let! _ = wire |> Wire.waitForAnswer "processSession" [ $"\"WrongCode\":%i{left}" ]

                                    let! _ = page |> Browser.waitWords Gate.Enrolment.error [ $"%i{left}" ]
                                    ()

                                // the third voids the code and ends the enrolment
                                do! page |> Gate.Enrolment.supply (otherCode code) newPin
                                let! _ = wire |> Wire.waitForAnswer "processSession" [ Wire.case "CodeVoid" ]
                                do! page |> Browser.waitGone Gate.Enrolment.code
                                do! Gate.waitShown page

                                let! actions = Gate.actionCount page
                                actions |> Expect.equal "the gate offers nothing but a relaunch" 0

                                // the relaunch mails a fresh code, and the form asks for it
                                do! page |> launchEnrolling baseUrl patientId

                                let! _ =
                                    page
                                    |> StubMail.waitFor
                                        baseUrl
                                        StubMail.confirmationCode
                                        (Identity.mailAddress Identity.NoPin)
                                        mailed

                                ()
                            }
                        )
            }

            testTask "the mailed code sets the PIN and opens the Session, and the next launch asks for none" {
                do!
                    Browser.withPage
                        "enrolment-set-pin"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page
                                let patientId = newPatientId "enrolment"
                                let address = Identity.mailAddress Identity.NoPin

                                do! page |> launchEnrolling baseUrl patientId

                                let! _ = wire |> Wire.waitForAnswer "processSession" [ Wire.case "EnrolmentPending" ]

                                // while enrolling there is no Session, and so nothing to close
                                let! closable = page |> Browser.isVisible TitleBar.anySessionButton
                                closable |> Expect.isFalse "no session button while enrolling"

                                let! code, _ = page |> standingCode baseUrl
                                let! mails = StubMail.read baseUrl page
                                let told = mails |> StubMail.about StubMail.pinSet address |> List.length

                                // the right code with a PIN of the User's choosing
                                do! page |> Gate.Enrolment.supply code newPin
                                do! page |> TitleBar.waitOpen (Identity.displayName Identity.NoPin)
                                do! Gate.waitGone page

                                let! _ =
                                    wire
                                    |> Wire.waitForAnswer
                                        "processSession"
                                        [ Wire.case "SessionResp"; "\"UserId\":\"no-pin\"" ]

                                // the second mail is how a User learns a PIN was set they did
                                // not set themselves
                                let! _ = page |> StubMail.waitFor baseUrl StubMail.pinSet address told

                                // the next launch opens the Session without the form
                                let enrolments =
                                    wire
                                    |> Wire.answersOf "processSession"
                                    |> List.filter _.Contains(Wire.case "EnrolmentPending")
                                    |> List.length

                                do! page |> Steps.openSession baseUrl patientId Identity.NoPin

                                wire
                                |> Wire.answersOf "processSession"
                                |> List.filter _.Contains(Wire.case "EnrolmentPending")
                                |> List.length
                                |> Expect.equal "the launch did not suspend into enrolment again" enrolments
                            }
                        )
            }
        ]
