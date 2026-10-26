/// The GenPRES client as the tests see it: the parts of its screen a test acts on or reads, one
/// module per part. Every part is found by what does not change with the language: an element
/// id, a role, the test id MUI gives an icon, a value attribute, or the names and numbers the
/// server puts in a text. The texts themselves come from a sheet that changes without a release.
module Informedica.GenPRES.Integration.Tests.Client

open System
open System.Threading.Tasks

open PuppeteerSharp


/// The title bar: whose Session it is and whom it is for.
[<RequireQualifiedAccess>]
module TitleBar =

    /// The session button of an open Session: the user's name, then the role in the user's
    /// language, behind a menu. Selected on the name, which does not depend on the language.
    let sessionButton (displayName: string) = $"button[aria-haspopup=\"menu\"][title^=\"%s{displayName} (\"]"


    /// The session button of an open Session of anyone.
    let anySessionButton = "button[aria-haspopup=\"menu\"]:has(svg[data-testid=\"PersonIcon\"])"


    /// The line in the middle that names the launched patient: the id, the name and the
    /// birthdate, in that order.
    let patientLine (patientId: string) (name: string) (birthDate: string) =
        $"header [title=\"%s{patientId} · %s{name} · %s{birthDate}\"]"


    /// The line that names a launched patient, whoever it is.
    let anyPatientLine = "header [title*=\" · \"]"


    /// Waits until the title bar shows the user's open Session.
    let waitOpen (displayName: string) (page: IPage) = page |> Browser.waitVisible (sessionButton displayName)


    /// Closes the Session from the one item of the session menu, and waits until the title bar
    /// no longer shows it.
    let closeSession (displayName: string) (page: IPage) =
        task {
            do! page.ClickAsync(sessionButton displayName)
            do! Browser.clickOpenMenuItem page
            do! page |> Browser.waitGone (sessionButton displayName)
        }


/// The session gate: the modal over the app while a launch is presented or resumed, after a
/// refusal, after a Session ended, and while a launch waits on a PIN.
[<RequireQualifiedAccess>]
module Gate =

    /// The gate.
    let selector = "[role=\"alertdialog\"][aria-labelledby=\"session-gate-title\"]"


    /// The gate once it has settled: not presenting a launch or resuming a Session, which it
    /// shows as busy, but telling what happened.
    let settled = $"%s{selector}:not(:has(.MuiCircularProgress-root))"


    /// The actions the gate offers: retry, or continue without a launch.
    let actions = $"%s{selector} .MuiCardActions-root button"


    /// Waits until the gate has settled on what it tells.
    let waitShown (page: IPage) = page |> Browser.waitVisible settled


    /// Waits until the gate is gone.
    let waitGone (page: IPage) = page |> Browser.waitGone selector


    /// How many actions the gate offers.
    let actionCount (page: IPage) = page |> Browser.count actions


    /// Continues without the launch: the one action of a gate that offers it, which a refusal
    /// no-role and an ended Session do. The gate goes, and GenPRES is open anonymously.
    let continueWithoutLaunch (page: IPage) =
        task {
            do! page.ClickAsync actions
            do! waitGone page
        }


    /// The enrolment form the gate is while a launch waits on a PIN.
    [<RequireQualifiedAccess>]
    module Enrolment =

        /// The field of the mailed confirmation code.
        let code = "#session-enrolment-code"


        /// The field of the PIN.
        let pin = "#session-enrolment-pin"


        /// The field that repeats the PIN.
        let repeat = "#session-enrolment-repeat"


        /// The form's error, under the last field: the client's own check, or the server's
        /// answer to the last submission.
        let error = "#session-enrolment-repeat-helper-text"


        /// The button that sends the form.
        let submit = $"%s{selector} .MuiCardActions-root button.MuiButton-contained"


        /// Waits until the gate is the enrolment form.
        let waitShown (page: IPage) = page |> Browser.waitVisible code


        /// Fills in the code and the PIN, twice, and sends them.
        let supply (confirmationCode: string) (newPin: string) (page: IPage) =
            task {
                do! page |> Browser.fill code confirmationCode
                do! page |> Browser.fill pin newPin
                do! page |> Browser.fill repeat newPin
                do! page.ClickAsync submit
            }


/// The disclaimer GenPRES shows first when it opens without a Session: a card in a modal, which
/// takes the clicks until it is accepted.
[<RequireQualifiedAccess>]
module Disclaimer =

    /// The disclaimer's card: a card in a modal that is not the gate.
    let selector = ".MuiModal-root .MuiCard-root:not([role=\"alertdialog\"])"


    /// Waits until the disclaimer is shown, accepts it, and waits until it is gone for good. On an
    /// anonymous open the client shows it at once, hides it behind the gate while it asks for a
    /// Session, and shows it again when there is none; a click that lands in between does nothing,
    /// so the disclaimer is accepted again whenever it is back, as a user does.
    let accept (page: IPage) =
        task {
            let button = $"%s{selector} .MuiCardActions-root button.MuiButton-contained"
            let deadline = DateTime.UtcNow.AddMilliseconds(float Browser.timeout)
            let mutable accepted = false

            do! page |> Browser.waitVisible selector

            while not accepted do
                let! shown = page |> Browser.isVisible selector

                if shown then
                    try
                        do! page.ClickAsync button
                    with :? PuppeteerException ->
                        // the card went as the click came: the next round finds it back or gone
                        ()

                // longer than the card takes to fade out
                do! Task.Delay 500

                let! shown = page |> Browser.isVisible selector
                let! gated = page |> Browser.isVisible Gate.selector
                accepted <- not shown && not gated

                if not accepted && DateTime.UtcNow > deadline then
                    raise (
                        TimeoutException $"the disclaimer was not accepted within %i{Browser.timeout / 1000} seconds"
                    )
        }


/// The side menu of pages, each item found by its icon.
[<RequireQualifiedAccess>]
module SideMenu =

    /// The item whose icon has the test id.
    let item (iconTestId: string) =
        $".MuiDrawer-paper li:has(svg[data-testid=\"%s{iconTestId}\"]) .MuiListItemButton-root"


    /// The item of the order plan.
    let orderPlan = item "SummarizeIcon"


/// The order plan page: the orders of the plan, the button that signs it, and the bar that tells
/// the record moved on.
[<RequireQualifiedAccess>]
module OrderPlan =

    /// The button that signs the plan, which only a Prescriber's open Session with a patient
    /// and an order is offered.
    let signButton = "button:has(svg[data-testid=\"AssignmentIcon\"])"


    /// The button that removes the selected orders, there whenever the page shows a plan.
    let deleteButton = "button:has(svg[data-testid=\"DeleteIcon\"])"


    /// One order of the plan: a row of its table.
    let row = ".MuiDataGrid-row"


    /// The bar that tells a newer version was signed: a warning with the action that opens it.
    let movedOnBar = ".MuiAlert-root.MuiAlert-colorWarning:has(.MuiAlert-action button)"


    /// Opens the order plan page from the side menu, and waits until it shows a plan.
    let show (page: IPage) =
        task {
            do! page.ClickAsync SideMenu.orderPlan
            do! page |> Browser.waitVisible deleteButton
        }


    /// How many orders the plan shows.
    let rowCount (page: IPage) = page |> Browser.count row


    /// Waits until the plan shows the number of orders.
    let waitRows (n: int) (page: IPage) =
        page.WaitForFunctionAsync("(s, n) => document.querySelectorAll(s).length === n", row, n) :> Task


    /// Whether the plan offers to sign.
    let offersSign (page: IPage) = page |> Browser.isVisible signButton


    /// Waits until the plan can be signed: the button shown and the plan at rest.
    let waitSignable (page: IPage) = page |> Browser.waitVisible $"%s{signButton}:not([disabled])"


    /// Opens the newest version from the bar that tells it was signed, and waits until the bar
    /// is gone.
    let openNewestVersion (page: IPage) =
        task {
            do! page.ClickAsync $"%s{movedOnBar} .MuiAlert-action button"
            do! page |> Browser.waitGone movedOnBar
        }


/// The prescribing page, opened on a medication through the url Workflow 3 of the testing
/// workflows documents.
[<RequireQualifiedAccess>]
module Prescribe =

    /// A medication as the url names it: the generic, the route and the indication, as the
    /// demo data spells them.
    type Medication =
        {
            /// The generic name.
            Generic: string
            /// The route.
            Route: string
            /// The indication.
            Indication: string
        }


    /// Paracetamol by mouth for mild pain and fever, from the demo data: scenarios for the stub
    /// patient, a ten-year-old of 32 kg.
    let paracetamol =
        {
            Generic = "paracetamol"
            Route = "ORAAL"
            Indication = "Milde tot matige pijn; koorts"
        }


    /// The url parameters of the stub patient, for a GenPRES opened without a launch: born on
    /// 15 March 2016, 32 kg (in grams) and 140 cm. Inside a Session the patient is the
    /// Session's, and the url's patient counts for nothing.
    let stubPatient = "by=2016&bm=3&bd=15&wt=32000&ht=140"


    /// The button that prescribes a scenario.
    let prescribeButton = "button:has(svg[data-testid=\"AddIcon\"])"


    /// The url of the prescribing page on the medication, with the patient parameters if any.
    let url (baseUrl: string) (patient: string option) (medication: Medication) =
        [
            yield! patient |> Option.toList
            "pg=pr"
            $"md=%s{Uri.EscapeDataString medication.Generic}"
            $"rt=%s{Uri.EscapeDataString medication.Route}"
            $"in=%s{Uri.EscapeDataString medication.Indication}"
        ]
        |> String.concat "&"
        |> fun query -> $"%s{baseUrl}/#patient?%s{query}"


    /// Goes to the prescribing page on the medication, which asks the server to compute it. Every
    /// such request carries what the Session opened with, so it is also the next request at
    /// which the server tells what happened to the Session meanwhile.
    let navigate (baseUrl: string) (patient: string option) (medication: Medication) (page: IPage) =
        task {
            let target = url baseUrl patient medication

            // the same url again is no navigation: by way of the session route it is one
            if page.Url = target then
                do! page.GoToAsync(baseUrl + "/#/session") :> Task

            do! page.GoToAsync target :> Task
        }


    /// Opens the prescribing page on the medication, and waits until a scenario can be
    /// prescribed.
    let openOn (baseUrl: string) (patient: string option) (medication: Medication) (page: IPage) =
        task {
            do! page |> navigate baseUrl patient medication
            do! page |> Browser.waitVisible $"%s{prescribeButton}:not([disabled])"
        }


    /// Prescribes the first scenario on show; the client adds it to the plan and shows the
    /// order plan page.
    let prescribeFirst (page: IPage) =
        task {
            do! page.ClickAsync $"%s{prescribeButton}:not([disabled])"
            do! page |> Browser.waitVisible OrderPlan.deleteButton
        }


    /// Prescribes the first scenario of the medication for the Session's patient, and waits
    /// until the order plan page shows; the caller waits for the number of orders it expects.
    let prescribe (baseUrl: string) (medication: Medication) (page: IPage) =
        task {
            do! page |> openOn baseUrl None medication
            do! prescribeFirst page
        }


/// The signing dialog: the orders as they will be signed and the PIN, or, before a challenge,
/// the data notice.
[<RequireQualifiedAccess>]
module SignDialog =

    /// The dialog.
    let selector = "[role=\"dialog\"][aria-labelledby=\"sign-dialog-title\"]"


    /// The PIN field, which the data notice does not have.
    let pinField = "#sign-dialog-pin"


    /// The PIN field's error: the client's own check, or the server's answer to the last PIN.
    let error = "#sign-dialog-pin-helper-text"


    /// The dialog's primary action: Sign, or Continue over a data notice.
    let primary = $"%s{selector} .MuiDialogActions-root button:last-child"


    /// The dialog's cancel action.
    let cancelButton = $"%s{selector} .MuiDialogActions-root button:first-child"


    /// The dialog once the server answered the request to sign: it opens busy while the
    /// challenge is asked for, its PIN field disabled and its orders not yet listed.
    let settled = $"%s{selector}:not(:has(.MuiCircularProgress-root))"


    /// Presses Sign on the order plan, and waits until the dialog has settled: asking the PIN
    /// over the orders as they will be signed, or telling the data notice first.
    let request (page: IPage) =
        task {
            do! OrderPlan.waitSignable page
            do! page.ClickAsync OrderPlan.signButton
            do! page |> Browser.waitVisible settled
        }


    /// Waits until the dialog asks the PIN, and answers it with the primary action.
    let sign (pin: string) (page: IPage) =
        task {
            do! page |> Browser.waitVisible $"%s{pinField}:not([disabled])"
            do! page |> Browser.fill pinField pin
            do! page.ClickAsync $"%s{primary}:not([disabled])"
        }


    /// Continues over the data notice, and waits until the dialog asks the PIN.
    let proceed (page: IPage) =
        task {
            do! page.ClickAsync $"%s{primary}:not([disabled])"
            do! page |> Browser.waitVisible pinField
        }


    /// Cancels, and waits until the dialog is gone.
    let cancel (page: IPage) =
        task {
            do! page.ClickAsync $"%s{cancelButton}:not([disabled])"
            do! page |> Browser.waitGone selector
        }


    /// Waits until the dialog is gone.
    let waitClosed (page: IPage) = page |> Browser.waitGone selector


    /// Whether the dialog asks the PIN: not while it tells the data notice.
    let asksPin (page: IPage) = page |> Browser.isVisible pinField


    /// The names of the orders the dialog lists: those new, changed or removed since the
    /// version last opened or signed, without the tag that says which.
    let orders (page: IPage) =
        page.EvaluateFunctionAsync<string[]>(
            "s => [...document.querySelectorAll(s + ' .MuiListItemText-primary')]"
            + ".map(e => ((e.querySelector('.MuiBox-root') || e).firstChild.textContent || '').trim())",
            selector
        )


/// The snackbar: the one message the client shows at the foot of the page. A message that went
/// well hides after three seconds, so it is read as it comes.
[<RequireQualifiedAccess>]
module Snackbar =

    /// The snackbar.
    let selector = ".MuiSnackbar-root"


    /// Waits until the snackbar says a message with every word, and returns it.
    let waitSaying (words: string list) (page: IPage) = page |> Browser.waitWords selector words


/// The patient panel: the patient data the plan is computed on.
[<RequireQualifiedAccess>]
module PatientPanel =

    /// The panel's fields that are selects, in the order the panel shows them.
    [<RequireQualifiedAccess>]
    type Field =
        /// The age in years.
        | Years
        /// The weight in kg.
        | Weight
        /// The height in cm.
        | Height


    /// The position of the field among the panel's selects: years, months, weeks, days,
    /// weight, height, renal function, department.
    let position field =
        match field with
        | Field.Years -> 0
        | Field.Weight -> 4
        | Field.Height -> 5


    /// The panel.
    let selector = "#patient"


    /// The button that folds the panel open and shut.
    let toggle = "#patient-details"


    /// The radio button of a gender: male, female or other.
    let genderRadio (value: string) = $"input[type=\"radio\"][value=\"%s{value}\"]"


    /// The question an attempt to change held patient data asks: why it cannot, with the ways
    /// out that drop the new and changed orders. Its actions are told apart by kind: keep the
    /// orders (outlined), remove them (the error colour) and refresh from the EHR (contained).
    let heldQuestion =
        "[role=\"dialog\"]:has(button.MuiButton-colorError):has(button.MuiButton-contained):has(button.MuiButton-outlined)"


    /// The gender the panel holds: male, female or other.
    let gender (page: IPage) =
        page.EvaluateExpressionAsync<string>(
            "(document.querySelector('input[type=\"radio\"]:checked') || { value: '' }).value"
        )


    /// Opens the panel when it is folded shut, and waits until it has unfolded.
    let unfold (page: IPage) =
        task {
            let! folded =
                page.EvaluateFunctionAsync<bool>(
                    "s => document.querySelector(s).getAttribute('aria-expanded') === 'false'",
                    toggle
                )

            if folded then
                do! page.ClickAsync toggle

            do! page |> Browser.waitVisible $".MuiCollapse-entered %s{selector}"
        }


    /// Clicks the radio button of a gender, once the panel is at rest and takes the click.
    let clickGender (value: string) (page: IPage) =
        task {
            do! page |> unfold

            do!
                page.WaitForFunctionAsync(
                    "s => { const e = document.querySelector(s); return !!e && !e.disabled }",
                    genderRadio value
                )
                :> Task

            do! page.ClickAsync(genderRadio value)
        }


    /// Waits until the panel holds the gender.
    let waitGender (value: string) (page: IPage) =
        page.WaitForFunctionAsync(
            "v => (document.querySelector('input[type=\"radio\"]:checked') || {}).value === v",
            value
        )
        :> Task


    /// Chooses a value of one of the panel's selects, as a user picks it from the list, once the
    /// panel is at rest and the select takes the click.
    let choose (field: Field) (value: string) (page: IPage) =
        task {
            let fields = $"%s{selector} [role=\"combobox\"]"
            do! page |> unfold

            do!
                page.WaitForFunctionAsync(
                    "(s, i) => { const f = document.querySelectorAll(s)[i]; return !!f && f.getAttribute('aria-disabled') !== 'true' }",
                    fields,
                    position field
                )
                :> Task

            let! all = page.QuerySelectorAllAsync fields
            do! all[position field].ClickAsync()
            do! page |> Browser.waitVisible "[role=\"listbox\"] [role=\"option\"]"

            let! chosen =
                page.EvaluateFunctionAsync<bool>(
                    "v => { const o = [...document.querySelectorAll('[role=\"listbox\"] [role=\"option\"]')]"
                    + ".find(o => o.innerText.trim() === v); if (o) { o.click(); return true } return false }",
                    value
                )

            if not chosen then
                invalidOp $"the panel offers no %A{field} of %s{value}"

            do! page |> Browser.waitGone "[role=\"listbox\"]"
        }


    /// Answers the question of held patient data by removing the new and changed orders.
    let removeNewOrders (page: IPage) =
        task {
            do! page.ClickAsync $"%s{heldQuestion} button.MuiButton-colorError"
            do! page |> Browser.waitGone heldQuestion
        }


    /// Answers the question of held patient data by reading the EHR again, which drops the new
    /// and changed orders and reopens the last signed version.
    let refreshFromEhr (page: IPage) =
        task {
            do! page.ClickAsync $"%s{heldQuestion} button.MuiButton-contained"
            do! page |> Browser.waitGone heldQuestion
        }


    /// Answers the question of held patient data by keeping the orders.
    let keepOrders (page: IPage) =
        task {
            do! page.ClickAsync $"%s{heldQuestion} button.MuiButton-outlined"
            do! page |> Browser.waitGone heldQuestion
        }
