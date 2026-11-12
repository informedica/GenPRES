namespace Informedica.GenPRES.Client.Core.Models

/// Compile-time-safe enumeration of all localizable UI strings.
/// Add a new case here whenever a new UI label is introduced, and update the
/// Localization sheet accordingly. New cases are drafted first in a script.
/// The translations live in data/localization/localization.tsv, the shape of the sheet;
/// scripts/CheckLocalization.fsx fails when a case has no row there.
type Terms =
    | ``Patient enter patient data``
    // what a draft that is no patient yet is missing: an age, or a weight and a height
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
    | ``Emergency List Catagory``
    | ``Emergency List Intervention``
    | ``Emergency List Calculated``
    | ``Emergency List Preparation``
    | ``Emergency List Advice``
    | ``Continuous Medication List``
    | ``Continuous Medication List show when patient data``
    | ``Continuous Medication Catagory``
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
    // the patient has an age but no weight and height, measured or estimated: the rules gate on both
    | ``Prescribe Weight and height unknown``
    // one of the two missing, the other measured or estimated
    | ``Prescribe Weight unknown``
    | ``Prescribe Height unknown``
    // no dose can be shown: the title of the notice
    | ``Prescribe Refusal``
    // one body per refusal, the picks filled into {0}
    | ``Prescribe Refusal No dose rules``
    | ``Prescribe Refusal Patient``
    | ``Prescribe Refusal No products``
    // every rule for the picks needs a weight, or a weight and a height, the patient does not have
    | ``Prescribe Refusal Measurement``
    // every rule for the picks needs a diluent the formulary does not have
    | ``Prescribe Refusal No diluent``
    // a change in the dose dialog that the order cannot be calculated with
    | ``Prescribe Refusal Not solved``
    // a change in the dose dialog to a dose per kg or per m2 that the order does not have
    | ``Prescribe Refusal No adjusted dose``
    // whom to tell, one sentence for every site
    | ``Prescribe Refusal Contact``
    | ``Order``
    | ``Order Frequency``
    | ``Order Dose``
    | ``Order Adjusted dose``
    | ``Order Quantity``
    | ``Order Concentration``
    | ``Order Drip rate``
    | ``Order Administration time``
    // the argumentation for a dose outside what the rules allow: the field's label, and the
    // line under it that says what to write
    | ``Order Argumentation``
    | ``Order Argumentation Helper``
    | ``Nutrition``
    | ``Order Plan``
    // the frequency column of the order plan table: a header, capitalized, where the order
    // dialog's field label is lowercase
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
    // one word for putting a page's choices back as they were, wherever that is offered
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
    // what an empty value shows while there are values to pick from
    | ``Pick a value``
    // what a click on a range, or Enter or Space on it, does
    | ``Pick the median``
    // the hover texts of the quantity field's buttons: to the bounds and one value down or up
    // where a field picks from a list, a large or a small step where a field steps
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
    // the switch that shows whether the patient is an adult, and marks one of 16 to 18 years or without an age
    | ``Patient Adult``
    // the panel's reset: the question asked before the patient's data is discarded
    | ``Patient Reset Dialog Title``
    | ``Patient Reset Dialog Text``
    // the reset of an identified patient: the age is the platform's and stays
    | ``Patient Reset Dialog Text Identified``
    // the question before an edit taken as a new patient, when orders or a selection are lost
    | ``Patient New Dialog Title``
    | ``Patient New Dialog Text``
    // the department in force, and where it came from
    | ``Patient Department``
    | ``Patient Department Default``
    | ``Patient Department Chosen``
    | ``Patient Department Launched``
    | ``Session Open Last Signed``
    // the question before a url with a patient or a medication: it drops the work not signed,
    // or leaves the launched Session
    | ``Url Leave Title``
    | ``Url Leave Text``
    | ``Url Leave``
    | ``Url Leave Session Title``
    | ``Url Leave Session Text``
    | ``Url Leave Launch Text``
    // a launch that came during a signature is not opened
    | ``Url Launch Signing``
    // the summary of a change of patient data: the orders updated, those with a notification
    | ``Patient Data Changed``
    // the panel's button that reads the patient from the EHR again
    | ``Patient Refresh``
    // what needs a patient on a page that works without one, said with what is missing
    | ``Patient Needed Plan Medication``
    // what a patient's calculations go on without, said on every page that calculates: the
    // age, and the gestational age of a patient younger than 28 days
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
    // the rules for the choice give more than one scenario, so the slot has no order to show
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
    // the Session ended because the store holds it in a form this release cannot read
    | ``Session Ending Unreadable``
    // the Session ended after an hour, or the site's lifetime, without a request
    | ``Session Ending Idle``
    // the session stands on the token of another tab
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
    // the orders stand on other patient data than the panel, after a failed recalculation
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
    // an order the current patient data could not be calculated on: locked, and why
    | ``Plan Context Locked``
    // an order calculated on the current patient data, locked because another order of its group was not
    | ``Plan Context Group Locked``
    // the warning before the PIN, with {0} the orders not calculated on the current patient data
    | ``Signing Not Recalculated``
    // the record moved on, told once per version; the button that takes the version up;
    // what is told once it is open
    | ``Session Newer Version``
    | ``Session Open Newest``
    | ``Session Version Opened``
    // a refresh from the EHR that did not happen
    | ``Session Refresh Failed``
    // the start-up gate: while the application starts, and when a load it cannot be used
    // without failed, with {0} the loads named
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
