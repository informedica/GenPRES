namespace Informedica.GenPRES.Shared


/// This module defines shared types between
/// the client and the server
module Types =

    open System


    [<Measure>]
    type gram

    [<Measure>]
    type cm

    [<Measure>]
    type m

    [<Measure>]
    type kg

    [<Measure>]
    type day

    [<Measure>]
    type week

    [<Measure>]
    type month

    [<Measure>]
    type year


    module PatientData =

        type Age =
            {
                Years: int<year>
                Months: int<month>
                Weeks: int<week>
                Days: int<day>
            }

        /// An age in weeks and days, such as a gestational age.
        type AgeWeeksDays =
            {
                Weeks: int<week>
                Days: int<day>
            }

        /// Whether a patient counts as an adult. The server sets it from the age and the choice of the user.
        [<RequireQualifiedAccess>]
        type AdultMark =
            /// Younger than 16 years: the patient cannot be marked as an adult.
            | NotAdult
            /// From 16 to 18 years, or without an age: an adult when the user marks the patient as one.
            | MarkableAdult of marked: bool
            /// From 18 years: an adult without a mark.
            | Adult

        /// The gestational age and whether it is needed. The server sets the case from the age.
        [<RequireQualifiedAccess>]
        type GestationalAge =
            /// From 1 year, or without an age: no gestational age is given.
            | NoGestationalAge
            /// From 28 days and under 1 year: the gestational age may be given.
            | OptionalGestationalAge of AgeWeeksDays option
            /// Under 28 days: the gestational age is needed.
            | RequiredGestationalAge of AgeWeeksDays option


    type Age = PatientData.Age
    type AgeWeeksDays = PatientData.AgeWeeksDays
    type AdultMark = PatientData.AdultMark
    type GestationalAge = PatientData.GestationalAge

    /// The estimated weights and the measured weight of a patient, in gram.
    type Weight =
        {
            EstimatedP3: int<gram> option
            Estimated: int<gram> option
            EstimatedP97: int<gram> option
            Measured: int<gram> option
        }


    /// The estimated heights and the measured height of a patient, in cm.
    type Height =
        {
            EstimatedP3: int<cm> option
            Estimated: int<cm> option
            EstimatedP97: int<cm> option
            Measured: int<cm> option
        }


    type Gender =
        | Male
        | Female
        | UnknownGender


    type Access =
        | CVL
        | PVL
        | EnteralTube


    type RenalFunction =
        | EGFR of int option * int option
        | IntermittentHemodialysis
        | ContinuousHemodialysis
        | PeritonealDialysis


    /// An eGFR band the user picks from, with the text the user reads.
    type EGFRBandOption =
        {
            /// The renal function of the band, which holds its minimum and maximum eGFR.
            RenalFunction: RenalFunction
            /// The text of the band in the rules, which the user reads.
            Text: string
        }


    /// The values the user picks the patient data from.
    type PatientDataOptions =
        {
            /// The calculation weights in gram.
            Weights: int<gram>[]
            /// The calculation heights in cm.
            Heights: int<cm>[]
            /// The years of an age.
            Years: int<year> list
            /// The months of an age.
            Months: int<month> list
            /// The weeks of an age.
            Weeks: int<week> list
            /// The days of an age.
            Days: int<day> list
            /// The weeks of a gestational age.
            GestationalWeeks: int<week> list
            /// The days of a gestational age.
            GestationalDays: int<day> list
            /// The eGFR bands, each with its text.
            EGFRBands: EGFRBandOption list
            /// The kinds of dialysis.
            Dialyses: RenalFunction list
        }


    /// The patient data the client sends. The data can be incomplete; the server checks it before the
    /// rules use it.
    type PatientData =
        {
            Age: Age option
            /// The gestational age and whether it may or must be given. The server sets the case from the age.
            GestationalAge: GestationalAge
            /// The post-menstrual age the server calculates from the age and the gestational age. The client
            /// never calculates it.
            PostMenstrualAge: AgeWeeksDays option
            /// The body surface area in m2 the server calculates from the weight and the height. The client
            /// never calculates it.
            BodySurfaceArea: decimal option
            Weight: Weight
            Height: Height
            Gender: Gender
            Access: Access list
            RenalFunction: RenalFunction option
            Location: string option
            Department: string option
            /// Whether the patient counts as an adult. An adult is matched to the dose rules for adults.
            AdultMark: AdultMark
            /// The values the user picks the patient data from; None when the server sent no options.
            Options: PatientDataOptions option
        }


    type ValueUnit =
        {
            Value: (string * decimal)[]
            Unit: string
            Group: string
            Short: bool
            Language: string
            Json: string
        }


    type Variable =
        {
            Name: string
            IsNonZeroPositive: bool
            Min: ValueUnit option
            MinIncl: bool
            Incr: ValueUnit option
            Max: ValueUnit option
            MaxIncl: bool
            Vals: ValueUnit option
        }


    type OrderVariable =
        {
            Name: string
            DefinedConstraints: Variable
            CalculatedConstraints: Variable
            Variable: Variable
            /// The increment a large step moves by: the calculated increment, or ten times the
            /// calculated increment when that is 0.1 mL or 0.1 mL/hour. Absent when the order
            /// variable has no calculated increment.
            LargeIncr: ValueUnit option
            Level: Level
        }

    and Level =
        | IsNormal
        | IsCaution
        | IsWarning
        | IsAlert


    type Schedule =
        {
            IsOnce: bool
            IsOnceTimed: bool
            IsContinuous: bool
            IsDiscontinuous: bool
            IsTimed: bool
            Frequency: OrderVariable
            Time: OrderVariable
        }


    type Dose =
        {
            Quantity: OrderVariable
            PerTime: OrderVariable
            Rate: OrderVariable
            Total: OrderVariable
            QuantityAdjust: OrderVariable
            PerTimeAdjust: OrderVariable
            RateAdjust: OrderVariable
            TotalAdjust: OrderVariable
        }


    type Item =
        {
            Name: string
            ComponentQuantity: OrderVariable
            OrderableQuantity: OrderVariable
            /// The quantity of the item in the orderable per adjust unit of the order
            OrderableQuantityAdjust: OrderVariable
            ComponentConcentration: OrderVariable
            OrderableConcentration: OrderVariable
            Dose: Dose
            IsAdditional: bool
        }


    /// The strength of one substance of a product.
    type ProductStrength =
        {
            /// The name of the substance
            Substance: string
            /// The concentration of the substance in the product
            Strength: ValueUnit
        }


    /// The reconstitution a product was made with: the solvent and its volumes.
    type ComponentReconstitution =
        {
            /// The name of the solvent the product is dissolved in.
            Solvent: string
            /// The volume of the solvent.
            SolventVolume: ValueUnit
            /// The volume the dissolved product adds to the solvent volume, when known.
            ExpansionVolume: ValueUnit option
        }


    /// The predilution a product was made with: a product volume diluted with a volume of a diluent.
    type ComponentPredilution =
        {
            /// The volume of the product that is diluted.
            MedVolume: ValueUnit
            /// The volume of the diluent that the product volume is diluted with.
            DiluentVolume: ValueUnit
            /// The name of the diluent.
            Diluent: string
        }


    /// A product a component can be made of, with the strength of each substance.
    type ComponentProduct =
        {
            /// The id of the product: a GPK, PRK or HPK
            Id: string
            /// The form quantities of the product; none when the product has none
            FormQuantities: ValueUnit option
            /// The strength of each substance of the product
            Strengths: ProductStrength[]
            /// The reconstitution the product was made with; none for a product that is not reconstituted.
            Reconstitution: ComponentReconstitution option
            /// The predilution the product was made with; none for a product that is not pre-diluted.
            Predilution: ComponentPredilution option
        }


    type Component =
        {
            Id: string
            Name: string
            Form: string
            ComponentQuantity: OrderVariable
            OrderableQuantity: OrderVariable
            /// The quantity of the component in the orderable per adjust unit of the order
            OrderableQuantityAdjust: OrderVariable
            OrderableCount: OrderVariable
            OrderQuantity: OrderVariable
            OrderCount: OrderVariable
            OrderableConcentration: OrderVariable
            Dose: Dose
            Items: Item[]
            /// The products the component can be made of
            Products: ComponentProduct[]
        }


    type Orderable =
        {
            Name: string
            OrderableQuantity: OrderVariable
            /// The quantity of the orderable per adjust unit of the order
            OrderableQuantityAdjust: OrderVariable
            OrderQuantity: OrderVariable
            OrderCount: OrderVariable
            DoseCount: OrderVariable
            Dose: Dose
            Components: Component[]
        }


    type Order =
        {
            Id: string
            Adjust: OrderVariable
            Orderable: Orderable
            Schedule: Schedule
            Route: string
            Duration: OrderVariable
            Start: DateTime
            Stop: DateTime option
        }


    type TextItem =
        | Normal of string
        | Bold of string
        | Italic of string


    type TextBlock =
        | Valid of TextItem[]
        | Caution of TextItem[]
        | Warning of TextItem[]
        | Alert of TextItem[]


    /// Possible Dose Types.
    type DoseType =
        | Once of string
        | Discontinuous of string
        | Continuous of string
        | Timed of string
        | OnceTimed of string
        | NoDoseType


    /// A value of an order's schedule the user can pick.
    [<RequireQualifiedAccess>]
    type ScheduleProperty =
        | Frequency
        | Time


    /// A value of an order's orderable the user can pick.
    [<RequireQualifiedAccess>]
    type OrderableProperty =
        | Quantity
        | DoseQuantity
        | DoseRate


    /// A value of a component of the orderable the user can pick.
    [<RequireQualifiedAccess>]
    type ComponentProperty =
        | OrderableQuantity
        | DoseQuantityAdjust


    /// A value of an item of a component the user can pick.
    [<RequireQualifiedAccess>]
    type ItemProperty =
        | DoseQuantity
        | DoseQuantityAdjust
        | DosePerTime
        | DosePerTimeAdjust
        | DoseRate
        | DoseRateAdjust
        | ComponentConcentration
        | OrderableConcentration
        | OrderableQuantity


    /// Where the choices of a filter come from when they are set from outside its own fields.
    [<RequireQualifiedAccess>]
    type SeedSource =
        /// The medication in the url, which replaces the choices of the filter.
        | Url
        /// An item of the emergency or continuous medication list, written over an empty filter.
        | MedicationList
        /// The choices of the formulary page.
        | Formulary
        /// The medication, route and form of the parenteralia page.
        | Parenteralia
        /// The rules were reloaded; the filter is evaluated again unchanged.
        | Reload


    /// The components a prescriber selected.
    [<RequireQualifiedAccess>]
    type ComponentSelection =
        /// No selection made: every component stays.
        | AllComponents
        /// The selected optional components; none when the list is empty.
        | Selected of string list


    type Filter =
        {
            Indications: string[]
            Generics: string[]
            Routes: string[]
            Forms: string[]
            DoseTypes: DoseType[]
            Diluents: string[]
            Components: string[]
            /// Every component of the dose rule, the main component first, in the order of the rule; set
            /// from the first scenario, before a selection can leave an additive out
            DoseRuleComponents: string[]
            Indication: string option
            Generic: string option
            Form: string option
            Route: string option
            DoseType: DoseType option
            Diluent: string option
            /// Which optional components the prescriber selected, among the options in Components
            SelectedComponents: ComponentSelection
            PickedProducts: string[]
        }


    type Totals =
        {
            Volume: TextItem[]
            Energy: TextItem[]
            Protein: TextItem[]
            Carbohydrate: TextItem[]
            Fat: TextItem[]
            Sodium: TextItem[]
            Potassium: TextItem[]
            Chloride: TextItem[]
            Calcium: TextItem[]
            Phosphate: TextItem[]
            Magnesium: TextItem[]
            Iron: TextItem[]
            VitaminD: TextItem[]
            Ethanol: TextItem[]
            Propyleenglycol: TextItem[]
            BoricAcid: TextItem[]
            BenzylAlcohol: TextItem[]
        }


    type OrderScenario =
        {
            Name: string
            Indication: string
            Form: string
            Route: string
            DoseType: DoseType
            Diluent: string option
            Component: string option
            Item: string option
            Diluents: string[]
            Components: string[]
            Items: string[]
            Prescription: TextBlock[][]
            Preparation: TextBlock[][]
            Administration: TextBlock[][]
            Order: Order
            UseAdjust: bool
            UseRenalRule: bool
            RenalRule: string option
            /// Whether the order fits its dose rule only when the Divisible of its products is ignored
            FitsOnlyWithoutDivisible: bool
            /// Whether no dose rule applies to the current patient data for the picks of the order, or other
            /// dose rules apply than for the patient data of its context
            RulesNoLongerApply: bool
            /// Whether the order could not be recalculated with the current patient data, so it stays as
            /// calculated with the earlier patient data; the prescriber accepts or removes it
            NotRecalculated: bool
            ProductIds: string[]
            /// The access device the solution rule of the scenario prepares for; none when the preparation
            /// fits any access. This is not the access of the patient.
            Access: Access option
            /// The reconstitution of the product of the first component as text, such as "10 mL NaCl 0,9%";
            /// none when that product is not reconstituted.
            Reconstitution: string option
            /// The predilution of the product of the first component as text, such as "3 mL verdund met 7 mL
            /// gluc 5%"; none when that product is not pre-diluted.
            Predilution: string option
        }


    [<RequireQualifiedAccess>]
    type NutritionCategory =
        | EnteralFeeding
        | EnteralSupplement
        | TPN
        | Lipid
        | ElectrolyteGlucose


    /// What kind of order a context holds: a medication, or a nutrition order of a category. It is stored
    /// with the context, so a signed plan read back knows the kind of each order without guessing from the
    /// generic: KCl, NaCl and glucose are both nutrition and medication generics.
    [<RequireQualifiedAccess>]
    type OrderCategory =
        | Medication
        | Nutrition of NutritionCategory


    type OrderContext =
        {
            /// The id of the context in the plan; empty for a context not in the plan, which the server
            /// gives a new id when it is added
            Id: string
            /// A medication, or the nutrition category the context holds
            Category: OrderCategory
            /// Whether the server that answered serves the demo data
            DemoVersion: bool
            /// The pick lists and the picks that select the scenarios
            Filter: Filter
            /// The patient the context is evaluated for
            PatientData: PatientData
            /// The scenarios that match the picks; a single scenario when the context is narrowed to one order
            Scenarios: OrderScenario[]
            /// The totals of the single scenario when the context is narrowed to one order
            Totals: Totals
            /// The reason the prescriber gives for a dose outside the dose rules; none until given
            Argumentation: string option
            /// The id of the plan context that holds the main order of this order; none for an order
            /// that is not linked
            LinkedTo: string option
        }


    type Intervention =
        {
            Hospital: string
            Category: string
            Name: string
            MinWeightKg: float option
            MaxWeightKg: float option
            /// The quantity of the substance
            Quantity: float option
            QuantityUnit: string
            /// The name of the solution
            Solution: string
            /// The total quantity
            Total: float option
            TotalUnit: string
            InterventionDose: float option
            InterventionDoseUnit: string
            /// The intervention dose as text
            InterventionDoseText: string
            SubstanceDose: float option
            SubstanceMinDose: float option
            SubstanceMaxDose: float option
            SubstanceDoseUnit: string
            SubstanceDoseAdjust: float option
            SubstanceNormDoseAdjust: float option
            SubstanceMinDoseAdjust: float option
            SubstanceMaxDoseAdjust: float option
            SubstanceDoseAdjustUnit: string
            /// Remarks on the substance dose
            SubstanceDoseText: string
            Text: string
            /// The order context of the row, narrowed to the scenario of the row; None for a row that is not
            /// medication.
            OrderContext: OrderContext option
        }


    type DrugInteraction =
        {
            Name: string * string
            Drug1: string
            Drug2: string
        }


    /// The order plan of a patient: every medication and nutrition order. The plan is what the prescriber
    /// signs.
    type OrderPlan =
        {
            PatientData: PatientData
            /// The ids of the contexts the plan totals count; empty for all contexts
            Filtered: string[]
            /// Every order context of the plan, medication and nutrition; the orders of the plan come from
            /// the contexts narrowed to one order
            OrderContexts: OrderContext[]
            Totals: Totals
        }


    type Formulary =
        {
            Generics: string[]
            Indications: string[]
            Routes: string[]
            Forms: string[]
            DoseTypes: DoseType[]
            PatientCategories: string[]
            Products: string[]
            Generic: string option
            Indication: string option
            Route: string option
            Form: string option
            DoseType: DoseType option
            PatientCategory: string option
            PatientData: PatientData option
            Markdown: string
            DoseCheck: TextBlock[]
        }


    type Parenteralia =
        {
            Generics: string[]
            Forms: string[]
            Routes: string[]
            PatientCategories: string[]
            Generic: string option
            Form: string option
            Route: string option
            PatientCategory: string option
            Markdown: string
        }


    type LogFileInfo =
        {
            FileName: string
            SizeBytes: int64
            LastModifiedAt: string
        }


    /// Opaque launch token, sealed by the MainEHR LaunchScript; the client never reads it.
    type Launch = | Launch of string


    /// Public JWK (JSON text) of the browser key pair made at the launch.
    type PublicKey = | PublicKey of string


    /// Names the order plan version the Session opened with. Sent with every computing request and checked
    /// at every signature. It will also travel inside the signed request of launch step 7, not built yet.
    type OpenedToken = | OpenedToken of string


    [<RequireQualifiedAccess>]
    type UserRole =
        | Prescriber
        | Reader


    type UserContext =
        {
            UserId: string
            DisplayName: string
            Role: UserRole
        }


    /// The full name and the birth date of the patient of a Session, the birth date as three integers.
    /// Shown in full, because the user works at the EHR that launched the Session.
    type PatientIdentity =
        {
            Name: string
            BirthYear: int
            BirthMonth: int
            BirthDay: int
        }


    /// The patient of a Session: the id, the identity when the EHR gives one, and the patient data read
    /// from the EHR at the launch or else from the last signed version. The data is None when neither
    /// has any; the user then enters it.
    type PatientContext =
        {
            PatientId: string
            /// None in an anonymous Session, where the EHR did not identify the patient
            PatientIdentity: PatientIdentity option
            PatientData: PatientData option
        }


    /// Identifies a signed version of an order plan: the id, the number No that orders the versions in the
    /// record of the patient, who signed it and when. No orders the versions because a clock cannot tell
    /// which of two signatures came first.
    type OrderPlanHead =
        {
            Id: string
            No: int
            By: UserContext
            SignedAt: DateTime
        }


    /// A signed version of an order plan as stored in the record: the head, the patient, the version it
    /// was signed over in Base (None for the first), the order contexts at the signature, the patient
    /// data the user saw, and in Verified whether that data was read from the EHR at the challenge.
    type SignedOrderPlan =
        {
            Head: OrderPlanHead
            PatientId: string
            Base: string option
            /// Every order context of the plan as signed, so a signed plan read back is the plan as it was
            OrderContexts: OrderContext[]
            PatientData: PatientData
            /// The name and birth date of the patient at this version; none for a version signed in an
            /// anonymous Session
            PatientIdentity: PatientIdentity option
            Verified: bool
        }


    /// What the client receives when a Session opens. There is no SessionId: the id is in the cookie, a
    /// bearer credential that scripts cannot read. Head is the signed version the Session opened with:
    /// its orders start the plan, and a Submission is refused when a newer version exists. Head is None
    /// when the record has no signed version.
    type SessionOpened =
        {
            /// None for an anonymous Session, opened without a launch, so without a user or role
            User: UserContext option
            /// None for a launch without an active patient
            PatientContext: PatientContext option
            OpenedToken: OpenedToken option
            /// The RFC 7638 thumbprint of the public key this Session will sign requests with (launch
            /// step 7, not built yet). The client keeps the matching private key and deletes the others.
            KeyThumbprint: string option
            Head: SignedOrderPlan option
        }


    /// Why a launch opened no Session. The case tells the client what to offer next.
    [<RequireQualifiedAccess>]
    type LaunchRefusal =
        /// The launch has expired; the client asks for a relaunch.
        | LaunchExpired
        /// The launch was already used; the client asks for a relaunch.
        | LaunchSpent
        /// The launch is not valid; the client asks for a relaunch.
        | LaunchInvalid
        /// The browser identity is missing; the client retries, then asks for a relaunch.
        | NoBrowserIdentity
        /// The user has no role; the client offers an anonymous Session.
        | NoRole
        /// The patient of the launch is not the active patient in MainEHR; the client asks for a
        /// relaunch after the right patient is activated.
        | WrongActivePatient
        /// The enrolment attempt is gone; the client shows only a text.
        | EnrolmentRequired


    /// Why a Session ended other than by the user closing it. The server tells it once, at the next
    /// request. The absolute lifetime is not built yet.
    [<RequireQualifiedAccess>]
    type SessionEnding =
        /// A new launch replaced the Session.
        | SupersededByLaunch
        /// The third wrong PIN at a signature.
        | WrongPinLimit
        /// This release cannot read the stored working state of the Session; a relaunch opens a new one.
        | Unreadable
        /// No request came within the idle lifetime; a relaunch reads the patient data and age again.
        | Idle
        /// Told to a tab only and never stored: another tab took the Session over with another token.
        /// The Session itself stays open for that tab.
        | InactiveTab


    /// What the client receives when its launch waits on a PIN: the name to greet, and a hint of the mail
    /// address the confirmation code went to, so a bystander cannot read the address.
    type EnrolmentPending =
        {
            DisplayName: string
            MailHint: string
        }


    /// Why a supplied PIN opened no Session. WrongCode allows another try and gives the tries left;
    /// PinFormat does not count as a try. CodeVoid, AttemptExpired and WrongActivePatient end the
    /// launch, which has to start over. After WrongActivePatient the PIN is set, but the patient of the
    /// launch is no longer the active patient.
    [<RequireQualifiedAccess>]
    type PinRefusal =
        | WrongCode of attemptsLeft: int
        | CodeVoid
        | AttemptExpired
        | PinFormat
        | WrongActivePatient


    [<RequireQualifiedAccess>]
    type LaunchOutcome =
        | Opened of SessionOpened
        /// The url of the IdentityProvider, sent as a value because Fable.Remoting would follow a 302
        /// redirect and fail to parse the HTML of the IdentityProvider. The stub never returns it.
        | RedirectTo of url: string
        | Refused of LaunchRefusal


    /// Why a signature did not proceed, at the challenge request or at the Submission. PinWrong and Locked
    /// let the user enter the PIN again, PinLimit ends the Session, and every other case ends the
    /// signing. Changed patient data is not a refusal but a DataNotice. The PIN cases come with the
    /// Submission.
    [<RequireQualifiedAccess>]
    type SigningRefusal =
        /// No Session for the cookie, or no Session at all.
        | NoSession
        /// The Session has no patient, or the plan to sign has none.
        | NoPatient
        /// No user to sign as, or the role read again from the registry is not Prescriber.
        | NotPrescriber
        /// The record has a newer version than the one the Session opened with; the head says whose
        /// and when.
        | Blocked of OrderPlanHead
        /// The OpenedToken is not the one this Session holds.
        | StaleToken
        /// The plan is not the plan the challenge was issued for, or there is no challenge.
        | ChallengeMismatch
        /// The challenge has expired.
        | ChallengeExpired
        /// The PIN is wrong; gives the tries left.
        | PinWrong of attemptsLeft: int
        /// The limit of wrong PINs is reached; the Session ends.
        | PinLimit
        /// Signing is locked until the given time.
        | Locked of until: DateTime
        /// The version could not be stored; nothing changed, so the user signs again.
        | StoreFailed
        /// The plan as sent cannot be read as an order plan; there is nothing to sign.
        | PlanUnreadable


    /// The current patient data, sent before a challenge when it differs from the data the
    /// Session opened with. None when the EHR cannot be read; the data is then not verified. The
    /// user goes on by sending the token back with the next request.
    type DataNotice =
        {
            PatientData: PatientData option
            Token: string
        }


    /// The signature: the plan as shown, the OpenedToken the Session holds, the challenge, the PIN, and an
    /// idempotency key from the client so the commit takes effect once. Never logged.
    type Submission =
        {
            Plan: OrderPlan
            Opened: OpenedToken
            Challenge: string
            Pin: string
            IdemKey: string
        }


    /// The answer to a signing command. Defined here, not in the api, because the session port returns it.
    [<RequireQualifiedAccess>]
    type SigningResponse =
        /// A challenge for exactly this plan, to send back with the PIN. ehrMovedOn is true when the
        /// patient of the Session is no longer the active patient in the EHR; the user learns this before
        /// entering the PIN.
        | ChallengeIssued of challenge: string * ehrMovedOn: bool
        /// No challenge yet: the current patient data, for the user to accept or not.
        | DataNotice of DataNotice
        /// The version is stored. Gives a new OpenedToken and the patient data of the Session after the
        /// signing, with the age calculated again.
        | Submitted of SignedOrderPlan * OpenedToken * PatientData
        /// The signature was refused, with the reason.
        | Refused of SigningRefusal


    /// Why an order context could not be evaluated. The cases match the Refusal type of GenORDER.
    [<RequireQualifiedAccess>]
    type OrderContextRefusal =
        /// No dose rule exists for the picks at all.
        | NoDoseRules
        /// Dose rules exist for the picks, and none of them covers this patient.
        | NoDoseRulesForPatient
        /// Dose rules cover the picks and the patient, and none of them can be prescribed: no
        /// product, or no dose type.
        | NoProducts
        /// Dose rules cover the picks and the patient, and each needs a weight, or a weight and a
        /// height, the patient does not have.
        | MeasurementMissing
        /// Dose rules cover the picks and the patient, and each medication is given undiluted
        /// while its concentration limit allows none of its product concentrations.
        | NoDiluent
        /// The order cannot be solved with the change; the order stays as it was.
        | NotSolved
        /// The change sets or recalculates a dose per kg or per m2, and the order has none. The
        /// order stays as it was.
        | NoAdjustedDose


    /// The answer to an order context command: the evaluated context, or the context as sent with the
    /// refusal. Failures go through the error channel.
    [<RequireQualifiedAccess>]
    type OrderContextResponse =
        /// The evaluated context, with the orders the link rules offer to link to its order: filled for a
        /// context narrowed to one order, empty otherwise. Only the answer carries them, so a request cannot
        /// send them back.
        | Evaluated of OrderContext * linked: OrderContext[]
        /// The context as it was sent, and why it could not be evaluated.
        | Refused of OrderContext * OrderContextRefusal


    /// What a reply says about the Session beside its result. NewerVersion: a version newer than the one
    /// the OpenedToken of the request names exists, with whose and when; it blocks nothing. Ended: the
    /// server ended this Session, told at the next request. Defined here because the session port
    /// returns it.
    [<RequireQualifiedAccess>]
    type RecordNotice =
        | NewerVersion of OrderPlanHead
        | Ended of SessionEnding
