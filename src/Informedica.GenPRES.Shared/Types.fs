namespace Shared


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


    type DataType =
        | StringData
        | FloatData
        | FloatOptionData


    type Configuration = Setting list

    and Setting =
        {
            Department: string
            MinAge: int
            MaxAge: int
            MinWeight: float
            MaxWeight: float
        }


    type NormalValue =
        {
            Sex: string
            Age: float
            P3: float
            Mean: float
            P97: float
        }

    type NormalValues =
        {
            Weights: NormalValue list
            Heights: NormalValue list
            NeoWeights: NormalValue list
            NeoHeights: NormalValue list
        }


    module Patient =

        type Age =
            {
                Years: int<year>
                Months: int<month>
                Weeks: int<week>
                Days: int<day>
            }

        type GestationalAge =
            {
                Weeks: int<week>
                Days: int<day>
            }


    type Age = Patient.Age
    type GestAge = Patient.GestationalAge

    /// Patient model for calculations
    type Patient =
        {
            Age: Age option
            GestationalAge: GestAge option
            Weight: Weight
            Height: Height
            Gender: Gender
            Access: Access list
            RenalFunction: RenalFunction option
            Location: string option
            Department: string option
        }

    /// Weight in gram!!
    and Weight =
        {
            EstimatedP3: int<gram> option
            Estimated: int<gram> option
            EstimatedP97: int<gram> option
            Measured: int<gram> option
        }

    /// Length in cm
    and Height =
        {
            EstimatedP3: int<cm> option
            Estimated: int<cm> option
            EstimatedP97: int<cm> option
            Measured: int<cm> option
        }

    and Gender =
        | Male
        | Female
        | UnknownGender

    and Access =
        | CVL
        | PVL
        | EnteralTube

    and RenalFunction =
        | EGFR of int option * int option
        | IntermittentHemodialysis // intermittent hemodialysis
        | ContinuousHemodialysis
        | PeritonealDialysis


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
            // The effective "bigger" per-click increment used by the outer (first/last)
            // navigation buttons, as computed by the server. Optional: only order
            // variables whose outer step differs from the defined increment emit one.
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
            ComponentConcentration: OrderVariable
            OrderableConcentration: OrderVariable
            Dose: Dose
            IsAdditional: bool
        }


    type Component =
        {
            Id: string
            Name: string
            Form: string
            ComponentQuantity: OrderVariable
            OrderableQuantity: OrderVariable
            OrderableCount: OrderVariable
            OrderQuantity: OrderVariable
            OrderCount: OrderVariable
            OrderableConcentration: OrderVariable
            Dose: Dose
            Items: Item[]
        }


    type Orderable =
        {
            Name: string
            OrderableQuantity: OrderVariable
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


    type OrderLoader =
        {
            Component: string option
            Item: string option
            Order: Order
        }


    type LoadedOrder =
        {
            UseAdjust: bool
            Component: string option
            Item: string option
            Order: Order
        }


    type Medication =
        | Bolus of BolusMedication
        | Continuous of ContinuousMedication

    and BolusMedication =
        {
            Hospital: string
            Category: string
            Generic: string
            MinWeight: float
            MaxWeight: float
            NormDose: float
            MinDose: float
            MaxDose: float
            Concentration: float
            Unit: string
            Remark: string
            TemplateGeneric: string
            TemplateRoute: string
            TemplateDoseType: string
            TemplateIndication: string
        }

    and ContinuousMedication =
        {
            Hospital: string
            Category: string
            Indication: string
            DoseType: string
            Medication: string
            Generic: string
            Unit: string
            Quantity: float
            Total: float
            DoseUnit: string
            MinWeight: float
            MaxWeight: float
            MinDose: float
            MaxDose: float
            AbsMax: float
            MinConc: float
            MaxConc: float
            Solution: string
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


    type Intervention =
        {
            Hospital: string
            // The intervention category
            // == Intervention ==
            // Indication for the intervention
            Category: string
            // Name of the intervention
            Name: string
            // == Patient --
            // minWeight
            MinWeightKg: float option
            // maxWeight
            MaxWeightKg: float option
            // == Product ==
            // Substance quantity
            Quantity: float option
            // Quantity unit
            QuantityUnit: string
            // Name of the solution
            Solution: string
            // Total quantity
            Total: float option
            // Total unit
            TotalUnit: string
            // == Dose ==
            // Intervention dose
            InterventionDose: float option
            // Intervention dose unit
            InterventionDoseUnit: string
            // Text representation
            InterventionDoseText: string
            // Dose of the substance
            SubstanceDose: float option
            // Min dose of the substance
            SubstanceMinDose: float option
            // Max dose of the substance
            SubstanceMaxDose: float option
            // Dose unit
            SubstanceDoseUnit: string
            // Adjusted dose
            SubstanceDoseAdjust: float option
            // Norm adjusted dose
            SubstanceNormDoseAdjust: float option
            // Min adjusted dose
            SubstanceMinDoseAdjust: float option
            // Max adjusted dose
            SubstanceMaxDoseAdjust: float option
            // Adjusted dose unit
            SubstanceDoseAdjustUnit: string
            // Dose remarks
            SubstanceDoseText: string
            Text: string
        }


    type Product =
        {
            Indication: string
            Medication: string
            Concentration: float
            Unit: string
        }


    /// Possible Dose Types.
    type DoseType =
        | Once of string
        | Discontinuous of string
        | Continuous of string
        | Timed of string
        | OnceTimed of string
        | NoDoseType


    type Filter =
        {
            Indications: string[]
            Generics: string[]
            Routes: string[]
            Forms: string[]
            DoseTypes: DoseType[]
            Diluents: string[]
            Components: string[]
            Indication: string option
            Generic: string option
            Form: string option
            Route: string option
            DoseType: DoseType option
            Diluent: string option
            SelectedComponents: string[]
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
            ProductIds: string[]
        }


    type OrderContext =
        {
            DemoVersion: bool
            Filter: Filter
            Patient: Patient
            Scenarios: OrderScenario[]
            Intake: Totals
        }


    type DrugInteraction =
        {
            Name: string * string
            Drug1: string
            Drug2: string
        }


    [<RequireQualifiedAccess>]
    type NutritionCategory =
        | EnteralFeeding
        | EnteralSupplement
        | TPN
        | Lipid
        | ElectrolyteGlucose


    /// A nutrition workbench of the plan: one per category added, an order context narrowed
    /// down to the product and dose; once it holds exactly one scenario, that scenario is an
    /// order of the plan.
    type NutritionContext =
        {
            Id: string
            Label: string
            Category: NutritionCategory
            Removable: bool
            OrderContext: OrderContext
        }


    /// The one plan: every order for the patient, nutrition included, which is what is signed.
    type OrderPlan =
        {
            Patient: Patient
            Selected: OrderScenario option
            Filtered: OrderScenario[]
            // every order in the plan, the nutrition orders included
            Scenarios: OrderScenario[]
            // the nutrition workbenches; a context narrowed to one scenario has it in Scenarios
            NutritionContexts: NutritionContext[]
            Totals: Totals
        }


    type NutritionPlan =
        {
            Patient: Patient
            NutritionContexts: NutritionContext[]
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
            Patient: Patient option
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
    type Launch = Launch of string


    /// Public JWK (JSON text) of the browser key pair made at the launch.
    type PublicKey = PublicKey of string


    /// Names the OrderPlan the Session opened with. Sent with every computing request and
    /// checked at every signature; later it travels inside the signed request of launch
    /// step 7, which is not built yet.
    type OpenedToken = OpenedToken of string


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


    type PatientContext =
        {
            PatientId: string
            Patient: Patient
        }


    /// What identifies a signed version of an order plan: its id, its place in the patient's
    /// record (`No` orders the record: a clock cannot say which of two landed first), who
    /// signed it and when. Enough for the notice that the record moved on: whose, and when.
    type OrderPlanHead =
        {
            Id: string
            No: int
            By: UserContext
            SignedAt: DateTime
        }


    /// A signed version of an order plan, as the record holds it: the head, the patient, the
    /// version it was signed over (`Base`, `None` for the first), the orders as shown at the
    /// signature, the patient data the User saw and whether it was the platform's reading at
    /// the challenge.
    type SignedOrderPlan =
        {
            Head: OrderPlanHead
            PatientId: string
            Base: string option
            Scenarios: OrderScenario[]
            Patient: Patient
            Verified: bool
        }


    /// What the client keeps of an open Session. No SessionId: it lives in the cookie, a
    /// bearer credential that never reaches script. `Head` is the version of the record it
    /// opened with: its orders go into the cart, and a Submission is refused when a newer
    /// version than it exists; `None` from nothing.
    type SessionOpened =
        {
            // None = anonymous session: opened without a launch, no User, no Role
            User: UserContext option
            // None = launch without an active patient
            PatientContext: PatientContext option
            OpenedToken: OpenedToken option
            // RFC 7638 thumbprint of the public key this Session will sign requests with
            // (launch step 7, not built yet); the client keeps that private key and prunes
            // the others
            KeyThumbprint: string option
            Head: SignedOrderPlan option
        }


    /// Every refusal ends the same: no Session opens. The case says what the client offers
    /// next.
    [<RequireQualifiedAccess>]
    type LaunchRefusal =
        // ask for a relaunch
        | LaunchExpired
        | LaunchSpent
        | LaunchInvalid
        // retry, then relaunch
        | NoBrowserIdentity
        // offer an anonymous open
        | NoRole
        // relaunch after activating the right patient in MainEHR
        | WrongActivePatient
        // the enrolment attempt is gone (UC-2); shown as text only for now
        | EnrolmentRequired


    /// Why a Session ended other than by the User closing it. The server says it once, at
    /// the next request, and the client shows it. The idle and absolute lifetimes (Rule 10)
    /// are not built yet.
    [<RequireQualifiedAccess>]
    type SessionEnding =
        | SupersededByLaunch
        // the third wrong PIN at a signature
        | WrongPinLimit


    /// What the client learns when its launch is waiting on a PIN: whom to greet and
    /// where the confirmation code went, hinted so that a shoulder cannot read the address.
    type EnrolmentPending =
        {
            DisplayName: string
            MailHint: string
        }


    /// Why a supplied PIN opened no Session. `WrongCode` leaves the form open with the tries
    /// left; `PinFormat` spends no try; `CodeVoid` and `AttemptExpired` are terminal: the
    /// launch has to start over. `WrongActivePatient` is terminal too, and the PIN is set:
    /// the registry no longer has the launch's Patient active.
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
        // the redirect to the IdentityProvider as a payload: Fable.Remoting's XHR would
        // follow a 302 and try to parse the IdentityProvider's HTML. The stub never returns it.
        | RedirectTo of url: string
        | Refused of LaunchRefusal


    /// Why a signature did not proceed, at the challenge request or at the Submission. The
    /// first seven end the signing and are told once; `PinWrong` and `Locked` keep the PIN
    /// dialog open; `PinLimit` ends the Session. Changed patient data is not a refusal but a
    /// `DataNotice`. The PIN cases arrive with the Submission.
    [<RequireQualifiedAccess>]
    type SigningRefusal =
        // no Session for the cookie, or none at all
        | NoSession
        // the Session has no Patient: nothing to sign for
        | NoPatient
        // nobody to sign as, or the Role, re-taken from the registry, is not Prescriber
        | NotPrescriber
        // the record moved on since the Session opened its version; whose version, and when
        | Blocked of OrderPlanHead
        // not the OpenedToken this Session holds
        | StaleToken
        // not the plan the challenge was issued over, or no challenge
        | ChallengeMismatch
        | ChallengeExpired
        // the wrong-PIN count: tries left, the limit reached (the Session ends), or locked
        // until a moment
        | PinWrong of attemptsLeft: int
        | PinLimit
        | Locked of until: DateTime


    /// The patient data as it stands, told before a challenge is issued when it is not what
    /// the Session opened with. `Data = None`: the platform cannot be read, the data is
    /// unverified. The User proceeds by returning the token with the next request.
    type DataNotice =
        {
            Data: Patient option
            Token: string
        }


    /// The signature: the plan as shown, the OpenedToken the Session holds, the challenge it
    /// was issued, the PIN, and a key of the client's own so that the commit takes effect
    /// once. Never logged.
    type Submission =
        {
            Plan: OrderPlan
            Opened: OpenedToken
            Challenge: string
            Pin: string
            IdemKey: string
        }


    /// The answer to a signing command. A payload like `LaunchOutcome`: the session port
    /// answers it, so it lives with the types, not the api.
    [<RequireQualifiedAccess>]
    type SigningResponse =
        // the challenge over exactly this plan; comes back with the PIN
        | ChallengeIssued of challenge: string
        // no challenge yet; the data as it stands, to show and to accept or not
        | DataNotice of DataNotice
        // the version committed, and a fresh OpenedToken over it
        | Submitted of SignedOrderPlan * OpenedToken
        | Refused of SigningRefusal


    /// What a reply says about the Session next to its result. `NewerVersion`: a version
    /// newer than the one the request's OpenedToken names exists, whose and when; it gates
    /// nothing. `Ended`: the server ended this Session, told at the next request. Lives here,
    /// like `SigningResponse`: the session port answers it.
    [<RequireQualifiedAccess>]
    type RecordNotice =
        | NewerVersion of OrderPlanHead
        | Ended of SessionEnding
