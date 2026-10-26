/// UC-5, someone else takes over the workstation: User B, at User A's open Session, can explore
/// but attest nothing; three wrong PINs end A's Session, mail A, and lock A's signing
/// (docs/scenarios/integration/uc-05-workstation-takeover.md).
///
/// The wrong entries count on A's credential and outlive the Session, so the test is A as the
/// identity no other test signs with, prescriber-other-patient on its own patient. It ends by
/// waiting out the lock of a minute and signing with the right PIN, which zeroes the count, so
/// that the next run finds the credential as this one did.
module Informedica.GenPRES.Integration.Tests.WorkstationTakeoverTests

open System
open System.Globalization
open System.Text.RegularExpressions
open System.Threading.Tasks

open PuppeteerSharp

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The longest lock the test waits out: the first lock is a minute, and each wrong entry after
/// it doubles it.
let longestWait = TimeSpan.FromMinutes 3.0


/// How many signing answers so far hold the fragment.
let signingAnswers (fragment: string) (wire: Wire.Recording) =
    wire
    |> Wire.answersOf "processSigning"
    |> List.filter _.Contains(fragment)
    |> List.length


/// The time the newest answer that refused a PIN as locked says signing is locked until.
let lockedUntil (wire: Wire.Recording) =
    wire
    |> Wire.answersOf "processSigning"
    |> List.rev
    |> List.tryPick (fun body ->
        match Regex.Match(body, "\"Locked\":\"([^\"]+)\"") with
        | m when m.Success ->
            Some(DateTime.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
        | _ -> None
    )


/// Waits until the lock the server answered has passed, when it passes within the longest wait.
let waitOutLock (wire: Wire.Recording) =
    task {
        match lockedUntil wire with
        | None -> failtest "no answer said until when signing is locked"
        | Some until ->
            let wait = until.ToUniversalTime() - DateTime.UtcNow + TimeSpan.FromSeconds 2.0

            if wait > longestWait then
                failtest
                    $"""signing of prescriber-other-patient is locked until %s{until.ToString "u"}, longer than the test waits: an earlier run left wrong PINs; run again after it"""
            elif wait > TimeSpan.Zero then
                do! Task.Delay wait
    }


/// Signs the plan in the open dialog with the right PIN. A lock an earlier run left is waited
/// out first, and the signature asked for again, since its challenge may not outlive the wait.
let signThroughLock (wire: Wire.Recording) (page: IPage) =
    task {
        let submitted () = wire |> signingAnswers (Wire.case "Submitted")
        let locked () = wire |> signingAnswers (Wire.case "Locked")
        let submittedBefore = submitted ()
        let lockedBefore = locked ()

        do! page |> SignDialog.sign seededPin

        do!
            Browser.waitUntil
                "the signature to be answered"
                (fun () -> submitted () > submittedBefore || locked () > lockedBefore)

        if locked () > lockedBefore then
            do! waitOutLock wire
            do! SignDialog.cancel page
            do! SignDialog.request page
            do! page |> SignDialog.sign seededPin
            do! Browser.waitUntil "the signature to be submitted" (fun () -> submitted () > submittedBefore)

        do! SignDialog.waitClosed page
    }


/// The PINs B tries, none of them A's.
let guesses = [ "1111"; "2222"; "3333" ]


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-5 workstation takeover"
        [
            testTask
                "three wrong PINs at A's screen end A's Session, mail A and lock A's signing, until the lock passes" {
                do!
                    Browser.withPages
                        "takeover"
                        (fun baseUrl screen elsewhere ->
                            task {
                                let wire = Wire.record screen
                                let a = Identity.PrescriberOtherPatient
                                let address = Identity.mailAddress a

                                // A's Session, with work signed; the right PIN also zeroes a
                                // count an earlier run may have left on the credential
                                do! screen |> Steps.openSession baseUrl otherPatient a
                                do! screen |> Prescribe.prescribe Prescribe.paracetamol
                                do! SignDialog.request screen
                                do! signThroughLock wire screen

                                let! mails = StubMail.read baseUrl screen
                                let told = mails |> StubMail.about StubMail.signingLocked address |> List.length

                                // B signs at A's screen with PINs of B's own: whose Session it is
                                // comes off the record, so they are checked against A's PIN
                                do! SignDialog.request screen

                                for guess, left in List.zip (List.take 2 guesses) [ 2; 1 ] do
                                    do! screen |> SignDialog.sign guess

                                    let! _ = wire |> Wire.waitForAnswer "processSigning" [ $"\"PinWrong\":%i{left}" ]

                                    let! _ = screen |> Browser.waitWords SignDialog.error [ $"%i{left}" ]
                                    ()

                                // the third ends A's Session, and the gate says so
                                do! screen |> SignDialog.sign (List.last guesses)
                                let! _ = wire |> Wire.waitForAnswer "processSigning" [ Wire.case "PinLimit" ]
                                do! Gate.waitShown screen

                                // the client acknowledges the ending at once
                                let! _ = wire |> Wire.waitForAnswer "processSession" [ Wire.case "SessionClosed" ]

                                let! opened = screen |> Browser.isVisible TitleBar.anySessionButton
                                opened |> Expect.isFalse "A's Session is ended"

                                // the mail is the notice that reaches A
                                let! _ = screen |> StubMail.waitFor baseUrl StubMail.signingLocked address told

                                // A's next launch, at a workstation of A's own, opens a Session as
                                // usual; only signing waits
                                let wire = Wire.record elsewhere
                                do! elsewhere |> Steps.openSession baseUrl otherPatient a
                                do! OrderPlan.show elsewhere
                                do! SignDialog.request elsewhere

                                do! elsewhere |> SignDialog.sign seededPin
                                let! _ = wire |> Wire.waitForAnswer "processSigning" [ Wire.case "Locked" ]

                                do! elsewhere |> Browser.waitVisible SignDialog.error
                                let! stays = elsewhere |> Browser.isVisible SignDialog.selector
                                stays
                                |> Expect.isTrue "the dialog stays: a right PIN while locked counts nothing"

                                // the lock passes, and the right PIN signs again
                                do! waitOutLock wire
                                do! SignDialog.cancel elsewhere
                                do! SignDialog.request elsewhere
                                do! signThroughLock wire elsewhere

                                let! _ = elsewhere |> Snackbar.waitSaying [ Identity.displayName a ]
                                ()
                            }
                        )
            }
        ]
