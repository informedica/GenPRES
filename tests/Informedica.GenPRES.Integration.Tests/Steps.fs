/// The steps the use cases share, each as a user takes it: a launch that opens a Session, a
/// prescription, a signature.
module Informedica.GenPRES.Integration.Tests.Steps

open PuppeteerSharp

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// Launches GenPRES for the identity on the patient, and waits until the Session is open and
/// the client has done what it does at the open: the requests that compute the plan for the
/// patient. A request of those still under way when the Session ends elsewhere is told the
/// ending, and the client acknowledges it at once, which a test that ends the Session next
/// would not expect.
let openSession (baseUrl: string) (patientId: string) (identity: Identity) (page: IPage) =
    task {
        do! page |> launch baseUrl patientId identity
        do! page |> TitleBar.waitOpen (Identity.displayName identity)
        do! Browser.waitApiQuiet page
    }


/// Prescribes paracetamol for the Session's patient, and waits until the plan holds the number
/// of orders.
let prescribe (baseUrl: string) (orders: int) (page: IPage) =
    task {
        do! page |> Prescribe.prescribe baseUrl Prescribe.paracetamol
        do! page |> OrderPlan.waitRows orders
    }


/// Signs the plan with the right PIN, and waits until the version is told: the snackbar names
/// the version and the signer, and the server answered it Submitted with that number.
let sign (wire: Wire.Recording) (version: int) (signer: Identity) (page: IPage) =
    task {
        do! SignDialog.request page
        do! page |> SignDialog.sign seededPin
        do! SignDialog.waitClosed page

        let! _ = page |> Snackbar.waitSaying [ $"%i{version}"; Identity.displayName signer ]

        let! _ =
            wire
            |> Wire.waitForAnswer "processSigning" [ Wire.case "Submitted"; $"\"No\":%i{version}" ]

        ()
    }
