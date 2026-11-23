namespace Informedica.GenPRES.Client.Core.Models

/// The localizable texts of the user interface. A new case needs a row in the Localization sheet and in
/// data/localization/localization.tsv; scripts/CheckLocalization.fsx fails when a case has no row there.
type Terms =
    | ``Patient enter patient data``
    // Asks for patient data with an age, or with a weight and a height.
    | ``Patient enter age or weight and height``
    | ``Patient Age``
    | ``Patient GA Age``
    | ``Patient Age year``
    | ``Patient Age years``
    | ``Patient Age month``
    | ``Patient Age months``
    | ``Patient Age week``
    | ``Patient Age weeks``
    | ``Patient Age day``
    | ``Patient Age days``
    | ``Patient Estimated``
    | ``Patient Years``
    | ``Patient Months``
    | ``Patient Weeks``
    | ``Patient Days``
    | ``Patient Weight``
    | ``Patient Length``
    | ``Patient remove patient data``
    | ``Emergency List``
    | ``Emergency List show when patient data``
    | ``Emergency List Category``
    | ``Emergency List Intervention``
    | ``Emergency List Calculated``
    | ``Emergency List Preparation``
    | ``Emergency List Advice``
    | ``Continuous Medication List``
    | ``Continuous Medication List show when patient data``
    | ``Continuous Medication Category``
    | ``Continuous Medication Medication``
    | ``Continuous Medication Quantity``
    | ``Continuous Medication Solution``
    | ``Continuous Medication Dose``
    | ``Continuous Medication Advice``
    | ``Prescribe``
    | ``Prescribe Scenarios``
    | ``Prescribe Indications``
    | ``Prescribe Medications``
    | ``Prescribe Routes``
    | ``Prescribe Prescription``
    | ``Prescribe Preparation``
    | ``Prescribe Administration``
    // the patient has no age: every dose rule with an age bound is left out
    | ``Prescribe Age unknown``
    // The patient has an age but no measured or estimated weight and height.
    | ``Prescribe Weight and height unknown``
    // The weight or the height is missing, and the other is known.
    | ``Prescribe Weight unknown``
    | ``Prescribe Height unknown``
    // The title of the notice that no dose can be shown.
    | ``Prescribe Refusal``
    // The text of each refusal, with the chosen values in {0}.
    | ``Prescribe Refusal No dose rules``
    | ``Prescribe Refusal Patient``
    | ``Prescribe Refusal No products``
    // Every rule for the chosen values needs a weight, or a weight and a height, that the patient lacks.
    | ``Prescribe Refusal Measurement``
    // Every rule for the chosen values needs a diluent that the formulary does not have.
    | ``Prescribe Refusal No diluent``
    // a change in the dose dialog that the order cannot be calculated with
    | ``Prescribe Refusal Not solved``
    // a change in the dose dialog to a dose per kg or per m2 that the order does not have
    | ``Prescribe Refusal No adjusted dose``
    // Who to contact, one sentence for each site.
    | ``Prescribe Refusal Contact``
    | ``Order``
    | ``Order Frequency``
    | ``Order Dose``
    | ``Order Adjusted dose``
    | ``Order Quantity``
    | ``Order Concentration``
    | ``Order Drip rate``
    | ``Order Administration time``
    // The label and the helper text of the argumentation field for a dose outside the rules.
    | ``Order Argumentation``
    | ``Order Argumentation Helper``
    | ``Nutrition``
    | ``Order Plan``
    // The capitalized header of the frequency column in the order plan; the order dialog label is lowercase.
    | ``Order Plan Frequency``
    | ``Formulary``
    | ``Formulary Medications``
    | ``Formulary Indications``
    | ``Formulary Routes``
    | ``Formulary Patients``
    | ``Parenteralia``
    | ``Interactions``
    | ``Interactions Drug 1``
    | ``Interactions Drug 2``
    | ``Interactions Class``
    | ``Interactions None Found``
    | ``Delete``
    // The button that resets the choices on a page.
    | ``Reset``
    | ``Edit``
    | ``Ok ``
    | ``Sort By``
    | ``Disclaimer``
    | ``Disclaimer text``
    | ``Disclaimer accept``
    | Settings
    | ``Reload resources``
    | ``Enter password``
    | Password
    | ``Invalid password``
    | Cancel
    | Confirm
    // Shared terms (used across multiple views)
    | ``Dose Types``
    | ``Dose Type``
    | Route
    | Indication
    | Composition
    | Form
    | ``Pharmaceutical Form``
    | Diluent
    | Components
    // The placeholder of an empty field that has values to pick from.
    | ``Pick a value``
    // The hint that a click, Enter or Space on a range picks the median.
    | ``Pick the median``
    // The hover texts of the quantity field buttons: to the minimum or maximum and one value lower or
    // higher for a field with a list, a large or a small step for a field with steps.
    | ``Step to minimum``
    | ``Step lower``
    | ``Step higher``
    | ``Step to maximum``
    | ``Step large down``
    | ``Step down``
    | ``Step up``
    | ``Step large up``
    // Patient-specific terms
    | ``Patient Male``
    | ``Patient Female``
    | ``Patient Unknown Gender``
    | ``Patient Gender``
    | ``Patient Access``
    | ``Patient Enteral Tube``
    | ``Patient Renal Function``
    // The switch that shows whether the patient is an adult; it can mark a patient of 16 to 18 years,
    // or without an age, as an adult.
    | ``Patient Adult``
    // The confirmation asked before the patient panel reset discards the patient data.
    | ``Patient Reset Dialog Title``
    | ``Patient Reset Dialog Text``
    // The reset text for an identified patient, whose age comes from the platform and is not reset.
    | ``Patient Reset Dialog Text Identified``
    // The confirmation asked before an edit that starts another patient, when orders or a selection
    // would be lost.
    | ``Patient New Dialog Title``
    | ``Patient New Dialog Text``
    // The current department, and whether it is chosen, set by the launch or not chosen.
    | ``Patient Department``
    | ``Patient Department Chosen``
    | ``Patient Department Launched``
    | ``Patient Department None``
    | ``Session Open Last Signed``
    // The confirmation asked before opening a url with a patient or a medication, which discards the
    // unsigned work or leaves the launched session.
    | ``Url Leave Title``
    | ``Url Leave Text``
    | ``Url Leave``
    | ``Url Leave Session Title``
    | ``Url Leave Session Text``
    | ``Url Leave Launch Text``
    // A launch that arrives during signing is not opened.
    | ``Url Launch Signing``
    // The summary after a patient data update: the updated orders and those with a notification.
    | ``Patient Data Changed``
    // the panel's button that reads the patient from the EHR again
    | ``Patient Refresh``
    // The notice that planning a medication needs patient data, with what is missing.
    | ``Patient Needed Plan Medication``
    // The notice on every page that calculates that the age is unknown, or the gestational age of a
    // patient younger than 28 days.
    | ``Patient Age unknown``
    | ``Patient Gestational age unknown``
    // Shared UI terms
    | Print
    | ``Not Configured``
    // Nutrition-specific terms
    | ``Nutrition Parenteral``
    | ``Nutrition Parenteral Section``
    | ``Nutrition TPN``
    | ``Nutrition Enteral Feeding``
    | ``Nutrition Enteral Supplement``
    | ``Nutrition Add Supplement``
    | ``Nutrition Lipids``
    | ``Nutrition Electrolytes Glucose``
    // The rules for the choice give more than one scenario, so there is no order to show.
    | ``Nutrition Several Rules``
    | ``Nutrition Remove Enteral Title``
    | ``Nutrition Remove Enteral Text``
    // Interactions
    | ``Interactions Medication``
    // Session: the gate and the session menu
    | ``Session Gate Opening``
    | ``Session Gate Opening Text``
    | ``Session Gate Resuming``
    | ``Session Gate Resuming Text``
    | ``Session Gate Unreachable``
    | ``Session Gate Unreachable Text``
    | ``Session Gate Refused``
    | ``Session Gate Try Again Or Relaunch``
    | ``Session Relaunch``
    | ``Session Retry``
    | ``Session Refusal Expired``
    | ``Session Refusal Spent``
    | ``Session Refusal Invalid``
    | ``Session Refusal No Browser Identity``
    | ``Session Refusal No Role``
    | ``Session Refusal Wrong Patient``
    | ``Session Refusal Enrolment``
    | ``Session Try Again``
    | ``Session Continue Without Launch``
    | ``Session Close``
    | ``Session Role Prescriber``
    | ``Session Role Reader``
    // the gate after the server ended the Session
    | ``Session Gate Ended``
    | ``Session Gate Inactive Tab``
    | ``Session Ending Superseded``
    // the Session ended at the third wrong PIN
    | ``Session Ending Pin Limit``
    // The session ended because this release cannot read the stored session.
    | ``Session Ending Unreadable``
    // the Session ended after an hour, or the site's lifetime, without a request
    | ``Session Ending Idle``
    // The session uses the token of another tab.
    | ``Session Ending Inactive Tab``
    // the enrolment form: title, body with {0} the name and {1} the hinted
    // mail address, the three field labels, the button, and one sentence per refusal
    | ``Session Gate Enrolment``
    | ``Session Gate Enrolment Text``
    | ``Session Enrolment Code``
    | ``Session Enrolment Pin``
    | ``Session Enrolment Pin Repeat``
    | ``Session Enrolment Submit``
    | ``Session Enrolment Code Format``
    | ``Session Enrolment Pin Format``
    | ``Session Enrolment Pins Differ``
    | ``Session Enrolment Wrong Code``
    | ``Session Enrolment Code Void``
    | ``Session Enrolment Expired``
    // signing: the button, the dialog, the data notice, the signed
    // sentence with {0} the version and {1} the signer, and one sentence per refusal
    | ``Signing Sign``
    // After a failed recalculation, the orders are calculated on other patient data than the panel shows.
    | ``Signing Plan Behind``
    | ``Signing Plan Behind Retry``
    | ``Signing Dialog Title``
    | ``Signing Dialog Text``
    | ``Signing Pin``
    | ``Signing Cancel``
    | ``Signing Proceed``
    | ``Signing Signed``
    | ``Signing Data Changed``
    | ``Signing Take Ehr Data``
    | ``Signing Keep Plan Data``
    | ``Signing Notice Ehr``
    | ``Signing Data Unverified``
    // the patient of the Session is no longer active in the EHR, told before the PIN
    | ``Signing Ehr Moved On``
    // the same for a patient the EHR did not identify
    | ``Signing Ehr Moved On Unnamed``
    | ``Signing Refusal No Session``
    | ``Signing Refusal No Patient``
    | ``Signing Refusal Not Prescriber``
    | ``Signing Refusal Blocked``
    | ``Signing Refusal Stale Token``
    | ``Signing Refusal Challenge Mismatch``
    | ``Signing Refusal Challenge Expired``
    | ``Signing Refusal Pin Wrong``
    | ``Signing Refusal Pin Limit``
    | ``Signing Refusal Locked``
    | ``Signing Refusal Store Failed``
    | ``Signing Refusal Plan Unreadable``
    | ``Signing Send Failed``
    // the sign dialog lists what differs from the version last opened or signed: the tag of a
    // new, a changed and a removed order, and the line shown when nothing differs
    | ``Signing New``
    | ``Signing Changed``
    | ``Signing Removed``
    | ``Signing No Changes``
    // An order that could not be calculated on the current patient data is locked, with the reason.
    | ``Plan Context Locked``
    // an order calculated on the current patient data, locked because another order of its group was not
    | ``Plan Context Group Locked``
    // the warning before the PIN, with {0} the orders not calculated on the current patient data
    | ``Signing Not Recalculated``
    // A newer version of the session exists, told once per version; the button that opens the newest
    // version; the notice after it is opened.
    | ``Session Newer Version``
    | ``Session Open Newest``
    | ``Session Version Opened``
    // a refresh from the EHR that did not happen
    | ``Session Refresh Failed``
    // The start-up gate while the application starts, and when a required load failed, with the failed
    // loads in {0}.
    | ``Startup Gate Starting``
    | ``Startup Gate Starting Text``
    | ``Startup Gate Failed``
    | ``Startup Gate Failed Text``
    | ``Startup Load Localization``
    | ``Startup Load Bolus Medication``
    | ``Startup Load Continuous Medication``
    | ``Startup Load Products``
    // the snackbar alerts, with {0} the number of interactions
    | ``Alert Interaction Names Not Loaded``
    | ``Alert Interactions Found``
    | ``Alert Close Failed``
    | ``Alert Pin Not Sent``
    | ``Alert Request Failed``
    // a failure the server gave no error text for
    | ``Alert Something Went Wrong``
    // the server error banner, with {0} the errors the server gave
    | ``Server Error Title``
    | ``Server Error Unreachable``
    | ``Server Error Text``
    // the patient line: a child's gender word, the word that joins the last part of an age, the
    // label of the body surface area
    | ``Patient Boy``
    | ``Patient Girl``
    | ``Patient Age And``
    | ``Patient BSA``
    // the renal functions the patient panel offers
    | ``Patient Renal eGFR Above 50``
    | ``Patient Renal eGFR 30 To 50``
    | ``Patient Renal eGFR 10 To 30``
    | ``Patient Renal eGFR Below 10``
    | ``Patient Renal Intermittent Hemodialysis``
    | ``Patient Renal Continuous Hemodialysis``
    | ``Patient Renal Peritoneal Dialysis``
    // the quantity labels of an order, and the text while an order loads
    | ``Order Dose Quantity``
    | ``Order Preparation Quantity``
    | ``Order Product Strength``
    | ``Order Total Quantity``
    | ``Order Total Volume``
    | ``Order Loading``
    // the nutrition slot: the column headings, the hint that the composition is locked, the
    // caption of the intake slider and the share of the total, with {0} the percentage
    | ``Nutrition Component``
    | ``Nutrition Quantity``
    | ``Nutrition Recommended Range``
    | ``Nutrition Composition Locked``
    | ``Nutrition Intake Caption``
    | ``Nutrition Share Of Total``
    // the section header of the parenteral nutrition print
    | ``Nutrition Parenteral Print Header``
    // the shell: the demo mark of the window title, the login and logout buttons, and the close
    // button of a dialog
    | ``Demo Version``
    | Login
    | Logout
    | Close
    // the settings page: the log files and the report of a log file
    | ``Settings Log Files``
    | ``Settings File Name``
    | ``Settings Size``
    | ``Settings Last Modified``
    | ``Settings Refresh``
    | ``Settings No Log Files``
    | ``Settings Analyzing Log File``
    | ``Settings Log Analysis Report``
    | ``Settings No Report``
    // the heading above the enteral supplements of a feeding
    | ``Nutrition Enteral Supplements``
    // the progress text while the resources reload
    | ``Settings Reloading Resources``
