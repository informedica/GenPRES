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


    type OrderPlan =
        {
            Patient: Patient
            Selected: OrderScenario option
            Filtered: OrderScenario[]
            // NOTE: maybe use ordercontext to preserve full info
            // so, OrderContexts: OrderContext []
            Scenarios: OrderScenario[]
            Totals: Totals
        }


    [<RequireQualifiedAccess>]
    type NutritionCategory =
        | EnteralFeeding
        | EnteralSupplement
        | TPN
        | Lipid
        | ElectrolyteGlucose


    type NutritionContext =
        {
            Id: string
            Label: string
            Category: NutritionCategory
            Removable: bool
            OrderContext: OrderContext
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


    /// Opaque launch token, sealed by the MainEHR LaunchScript (launch sequence step 1);
    /// the client never reads it.
    type Launch = Launch of string


    /// Public JWK (JSON text) of the browser key pair made at launch step 3.
    type PublicKey = PublicKey of string


    /// Names the TreatmentPlan the Session opened with (Rule 34). Stored now, sent later
    /// inside the signed request of launch step 7.
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


    /// What the client keeps of an open Session (launch step 6). No SessionId: it lives in
    /// the cookie (Rule 12).
    type SessionOpened =
        {
            // None = anonymous session (Rule 14)
            User: UserContext option
            // None = launch without an active patient (ext 1a)
            PatientContext: PatientContext option
            OpenedToken: OpenedToken option
            // RFC 7638 thumbprint of the public key this Session signs with (step 7);
            // the client keeps that private key and prunes the others
            KeyThumbprint: string option
        }


    /// Every refusal ends the same: no Session opens (Rule 7). The case says what the client
    /// offers next (uc-01, Refusals table).
    [<RequireQualifiedAccess>]
    type LaunchRefusal =
        // ext 4a: ask for a relaunch
        | LaunchExpired
        | LaunchSpent
        | LaunchInvalid
        // ext 3c: retry, then relaunch
        | NoBrowserIdentity
        // ext 5a: offer an anonymous open
        | NoRole
        // ext 5b: relaunch after fixing MainEHR
        | WrongActivePatient
        // UC-2, shown as text only for now
        | EnrolmentRequired


    [<RequireQualifiedAccess>]
    type LaunchOutcome =
        | Opened of SessionOpened
        // step 4.2 as a payload: Fable.Remoting's XHR would follow a 302 and try to parse
        // the IdentityProvider's HTML. The stub never returns it.
        | RedirectTo of url: string
        | Refused of LaunchRefusal
