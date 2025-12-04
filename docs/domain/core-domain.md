# The Core Domain Model

- [The Core Domain Model](#the-core-domain-model)
  - [The Transformation Pipeline](#the-transformation-pipeline)
  - [Core Definitions](#core-definitions)
  - [Domain Boundaries](#domain-boundaries)
  - [The Order Management Cycle](#the-order-management-cycle)
  - [Key Concepts](#key-concepts)
  - [Rule-Based CDS](#rule-based-cds)
  - [AI-Based CDS](#ai-based-cds)
  - [Exposure and Outcome](#exposure-and-outcome)
  - [Operational Knowledge Rules](#operational-knowledge-rules)
    - [Safety and Efficiency through Constraints](#safety-and-efficiency-through-constraints)
    - [Rule Structure](#rule-structure)
    - [Selection and Calculation Constraints](#selection-and-calculation-constraints)
      - [Selection Constraints by Rule Type](#selection-constraints-by-rule-type)
      - [Calculation Constraints by Rule Type](#calculation-constraints-by-rule-type)
    - [Formal Constraint Execution Model](#formal-constraint-execution-model)
      - [Filter Stage (Selection Constraints from GenFORM)](#filter-stage-selection-constraints-from-genform)
      - [Solver Stage (Calculation Constraints via GenSOLVER)](#solver-stage-calculation-constraints-via-gensolver)
    - [Hybrid Architecture](#hybrid-architecture)
    - [Rule Hierarchy](#rule-hierarchy)
    - [Requirements](#requirements)
  - [Related Documents](#related-documents)
    - [Quick Reference by Topic](#quick-reference-by-topic)

The Core Domain Model aims to model the general concept of treating a *Patient* by applying *Orders*, such as medication orders. The model is designed to be extensible to other types of *Orders* (e.g., procedures, therapies) and adaptable to various clinical contexts.

## The Transformation Pipeline

At the heart of the Core Domain Model is a **transformation pipeline** that converts unstructured expert knowledge into safe, computable medication orders:

```text
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                        THE KNOWLEDGE-TO-ORDER PIPELINE                              │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                     │
│   Free Text                    Operational                Selection &               │
│   (Expert Knowledge)    →      Knowledge Rules    →       Calculation      →        │
│   Guidelines, protocols,       (OKRs)                     Constraints               │
│   formularies                  Structured, validated      Categorical +             │
│                                                            Quantitative.            │
│                                                                                     │
│        ↓                            ↓                           ↓                   │
│                                                                                     │
│   Order Scenarios              Quantitative                Executable               │
│   (Valid Alternatives)    →    Order Options      →        Orders                   │
│   Clinically safe choices      Computed values             Ready for administration │
│                                                                                     │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

This pipeline ensures **safety** and **efficiency** in medication order management by:

1. **Safety by Construction**: Every option presented to the user has been validated against all applicable rules. Invalid options are mathematically impossible because they violate the constraint system.

2. **Efficiency through Automation**: Manual lookups, calculations, and cross-referencing are eliminated. The system computes all valid quantitative options automatically.

3. **Traceability**: Each computed option can be traced back through the pipeline to its source rules and expert knowledge.

4. **Completeness**: The constraint solver guarantees that all valid options are preserved—no safe alternative is hidden from the user.

The transformation is implemented by three core systems:

- **GenFORM**: Extracts and structures free-text knowledge into Operational Knowledge Rules (OKRs). Rules are defined in terms of *Patient Categories* (types/ranges) that describe which kinds of patients the rule applies to. → *See [GenFORM: Free Text to Operational Rules](genform-free-text-to-operational-rules.md) for detailed specifications.*
- **GenORDER**: Transforms OKRs into Order Scenarios for a specific *Patient* (instance). At runtime, the Patient's actual attributes (age, weight, etc.) are matched against Patient Category ranges to select applicable rules. → *See [GenORDER: Operational Rules to Orders](genorder-operational-rules-to-orders.md) for detailed specifications.*
- **GenSOLVER**: Solves the equation systems to produce quantitative order options. Performs exact arithmetic with full unit awareness and guarantees soundness, completeness, and monotonic convergence. → *See [GenSOLVER: Order Scenarios to Quantitative Solutions](gensolver-from-orders-to-quantitative-solutions.md) for detailed specifications.*

## Core Definitions

| Term | Definition |
| ----- | ----- |
| *Operational Knowledge Rule (OKR)* | A fully structured, machine-interpretable, constraint-based representation of expert medication (and other clinical) knowledge produced by GenFORM. |
| *Selection Constraint* | A categorical constraint used to determine which OKRs apply to a given Order Context (e.g., indication, generic, route, form, setting). |
| *Calculation Constraint* | A quantitative constraint used to compute numerical values such as dose quantities, rates, volumes, or durations. |
| *Order Context* | The bounded clinical context; composed of a specific Patient (instance), indication(s), and selection constraints from which Order Scenarios are generated. The Patient's attributes are matched against Patient Categories in OKRs. |
| *Order Scenario* | A fully constrained, uniquely identifiable, computable clinical alternative representing one valid way to prescribe, prepare, and administer an order. |
| *Order* | The executable prescription instance derived from an Order Scenario, identified by a unique Id. |
| *Schedule* | The temporal model of an Order defining frequency, administration time, and total duration. |
| *Orderable* | The abstract entity that is ordered (e.g., a medication as prescribed). |
| *Component* | A physical product unit used to realize an Orderable (e.g., vial, ampoule, bag). |
| *Item* | The actual substance delivered to the patient (e.g., a medication substance such as amoxicillin). |
| *Dose Rule* | An OKR that defines qualitative and quantitative constraints for dosing a specific generic in a defined clinical context. |
| *Dilution Rule* | An OKR that defines requirements for the amount and/or concentration of liquid medication. |
| *Reconstitution Rule* | An OKR that defines how medication must be reconstituted to enable administration (e.g., converting powder to liquid). |
| *Renal Rule* | An OKR used to adjust the dose advice according to renal function (GFR). |
| *Patient Category* | A categorical description of the type of patient a rule applies to, defined by ranges for age, weight, BSA, gestational age, post-menstrual age, and gender. Used by GenFORM to define which patients an OKR covers. |
| *Patient* | A specific individual patient instance with concrete values for age, weight, BSA, etc. Used by GenORDER to match against Patient Categories and compute patient-specific Order Scenarios. |
| *Dose Type* | The temporal category of dosing: once, onceTimed, discontinuous, timed, or continuous. |
| *Dose Quantity* | The amount delivered per single administration. |
| *Dose Per Time* | The accumulated dose delivered per unit time (e.g., per day). |
| *Dose Rate* | The continuous delivery speed of a dose (unit per time), typically used for infusions. |
| *Dose Total* | The cumulative dose delivered over the full duration of an Order. |
| *Adjusted Dose* | A dose value normalized to a patient-specific Adjustment Quantity (e.g., kg, m²). |
| *Adjustment Unit* | The patient normalization unit used for dose scaling (e.g., kg for body weight, m² for body surface area). |
| *Adjustment Quantity* | The patient-specific numeric value used to apply an Adjusted Dose (e.g., 12 kg). |
| *Filter Stage* | The stage in which categorical selection constraints are applied to determine which rules and scenarios are applicable. |
| *Solver Stage* | The stage in which quantitative calculation constraints are transformed into equations and solved by GenSOLVER. |
| *Exposure* | The real-world administration of an executable Order to a patient. |
| *Outcome* | The observed clinical effect resulting from Exposure, used for evaluation and feedback. |
| *GenFORM* | The system that transforms free-text medication knowledge into fully computable Operational Knowledge Rules (OKRs). Rules are defined in terms of Patient Categories (types) rather than specific patients. |
| *GenORDER* | The execution-layer system that transforms OKRs into patient-specific, fully computable and executable Order Scenarios. Matches a specific Patient's (instance) attributes against Patient Category ranges to select applicable rules. |
| *GenSOLVER* | The domain-independent quantitative constraint solving engine that computes numerically valid, unit-consistent solutions from the equation systems generated by GenORDER. Guarantees soundness, completeness, and monotonic convergence. |
| *Variable* | A symbolic placeholder representing a numeric value with an associated unit and domain range (used by GenSOLVER). |
| *Domain* | The bounded numerical range or discrete set of values a variable may assume. Domains form a lattice with ⊤ (unrestricted) and ⊥ (empty/infeasible). |
| *Propagation* | The process of reducing variable domains by enforcing constraints through equations. |
| *Soundness* | The property that every computed solution satisfies all constraints. |
| *Completeness* | The property that all valid solutions remain within the computed solution space. |
| *Monotonic Convergence* | The property that each propagation step monotonically reduces the domain until a fixed point is reached. |

## Domain Boundaries

The Core Domain is bounded by the application of *Orders* to *Patients* within a clinical context. The primary aim of applying an *Order* to a specific *Patient* is either conditional or directly consequential to improving the outcome for that *Patient*.

Which *Order* to apply to which *Patient* (and how and when) is governed by *Expert Knowledge* that must be operationalized into *Operational Knowledge Rules* that can be applied to specific *Orders* on a *Patient* by a *Health Professional*.

## The Order Management Cycle

![Core Domain Graph](https://docs.google.com/drawings/d/e/2PACX-1vRmBkfmICA31yM16mYntvYppgCVr5PuZz80urei3J0m0YoZurKSDBtf8mSIH7xzv9sbGoMLIsOxG8kx/pub?w=1440&h=1080)

## Key Concepts

All concepts revolve around a *Patient* context. In addition to the *Patient*, the following stakeholders can be defined:

- *Patients*: Represent the overall context in which a patient is treated, including relevant clinical data, demographics, and care settings.  
- *Scientists*: Representing specific areas of medical knowledge, such as cardiology or oncology, that inform treatment decisions.  
- *Health Professionals*: Represent specific roles of healthcare providers, such as:
  - physicians,
  - nurses, or
  - pharmacists

The *Order Management Cycle* starts with:

- **1. Expert Knowledge**, which must be operationalized into
- **2. Operational Knowledge Rules** that can be applied to specific
- **3. Orders** on a *Patient* by a *Health Professional*.

The *Order Management Cycle* consists of the following stages:

- **3. Orders**: The set of actions taken to treat a *Patient*, such as medication orders, procedures, or therapies.  
- **4. Validation**: The rules and checks that ensure *Orders* are appropriate, safe, and effective for the *Patient*.  
- **5. Planning**: The scheduling and coordination of *Orders* to optimize patient care and resource utilization.  
- **6. Preparation**: The logistics and processes involved in getting *Orders* ready for administration to the *Patient*.  
- **7. Administration**: The actual delivery of *Orders* to the *Patient*, including monitoring and documentation.  
- **8. Evaluation**: The assessment of *Order* outcomes to feed back into the current set of *Orders* and to expand **1. Expert Knowledge** for future **2. Operational Knowledge Rules**.

There are two main types of *Clinical Decision Support (CDS)* rules that can be applied to *Orders*:

- *Rule-Based CDS*: CDS rules that are based on predefined logic and criteria, such as dosage limits or contraindications.  
- *AI-Based CDS*: CDS rules that leverage artificial intelligence and machine learning to provide recommendations based on patient data and clinical patterns.

It is important to note that *AI-Based CDS* can operate within the context of *Rule-Based CDS*, providing an additional layer of decision support where the traditional rules provide the safety rails. *AI-Based CDS* can also analyze [exposure and outcome](#exposure-and-outcome), offering insights and recommendations, and thus contributing back to *Expert Knowledge*.

## Rule-Based CDS

*Rule-Based CDS* depends on the careful modeling, structuring, and translation of **1. Expert Knowledge** into **2. Operational Knowledge Rules** that can be applied to specific **3. Orders** on a *Patient* by a *Health Professional*.

Once the **2. Operational Knowledge Rules** are defined, they can be applied in different stages of the *Order Management Cycle*. Application can be done in a number of ways:

1. *Direct Application*: Rules are applied immediately to each *Order*, automatically processing all required information so the order is valid and ready for **5. Planning**, **6. Preparation**, and **7. Administration**—ensuring safety (protocol- and rule-compliant) and efficiency (no lookups or manual calculations).  
2. *Providing a Bounded Context for AI*: Rules define the constraints and parameters within which *AI-Based CDS* can operate, ensuring that AI recommendations align with established clinical guidelines and safety protocols.

## AI-Based CDS

![AI positioning](https://docs.google.com/drawings/d/e/2PACX-1vTeIgVFS3Vdq97zbiQDR1jcl5kD7J4oVDRRFLDnN2DrJ50DwykO5D1qf3nGfzcXsnj3r6HnJohUBCxW/pub?w=1441&h=898)

When replacing **7. Administration** and *Patient* with the term *Exposure*, and **8. Evaluation** with *Outcome*, the Core Domain Model can also be applied to other domains, such as epidemiology or public health, where exposures and outcomes are studied.

## Exposure and Outcome

![Exposure Outcome Graph](https://docs.google.com/drawings/d/e/2PACX-1vSqjlp9H-KA8dGZMiq9etMjIvse7hd-2ALzg3PNuPjAQuYUNQ69MvsUXla85_7Dfi-iggKarKWSox0O/pub?w=1441&h=898)

The combined exposure of the administration of *Orders* with specific patient characteristics and patient data in relation to outcomes is still largely unexplored territory that can lead to new insights in medical science. Historically, only direct patient-related data have been used to study outcomes, while administered *Orders* are often missing or incomplete.

## Operational Knowledge Rules

An *Operational Knowledge Rule (OKR)* constrains the set of possible **3. Orders** on a specific *Patient* in such a way that a *Health Professional* can narrow down options to the most appropriate ones for that patient context. Constraints can be derived from **4. Validation**, **5. Planning**, and **6. Preparation** stages of the *Order Management Cycle*.

### Safety and Efficiency through Constraints

The constraint-based approach delivers two fundamental properties:

**Safety**: By translating expert knowledge directly into mathematical constraints, the system guarantees that:

- No invalid dose can be computed (violates constraint boundaries)
- No contraindicated combination can be selected (filtered by selection constraints)
- No preparation error can occur (dilution/reconstitution rules enforce correct volumes and concentrations)
- Every presented option satisfies all applicable rules simultaneously

**Efficiency**: By automating the constraint solving process:

- Health professionals select from pre-validated options rather than manually calculating and cross-checking
- Patient-specific adjustments (weight, BSA, renal function) are computed automatically
- Preparation instructions are generated directly from rules
- Order changes propagate correctly through all dependent calculations

The key insight is that **safety and efficiency are not trade-offs**—they are both consequences of the same constraint-based architecture. The more completely rules are encoded as constraints, the safer *and* more efficient the system becomes.

### Rule Structure

Operational Knowledge Rules (OKRs) are implemented as four rule types in `Informedica.GenFORM.Lib`. For complete rule specifications including all fields, data types, and validation requirements, see [GenFORM: Free Text to Operational Rules](genform-free-text-to-operational-rules.md).

1. **Dose Rule**: Defines dosing limits per indication, generic, route, patient, and dose type
2. **Dilution Rule**: Defines preparation constraints (volumes, concentrations, drip rate, administration fraction) per patient and vascular access
3. **Reconstitution Rule**: Defines reconstitution steps (diluent volumes, expansion volumes) per generic product (GPK) and route
4. **Renal Rule**: Defines dose adjustments based on renal function (GFR)

Each rule contains:

- **Selection Constraints**: Source, Generic, Indication, Route, Setting, Patient, Dose Type, Vascular Access, Component, Substance
- **Calculation Constraints**: Schedule, Duration, Dose Limits; Volume, Concentration, Drip Rate, Administration Fraction

### Selection and Calculation Constraints

All OKRs can be translated to either selection or calculation constraints. Selection constraints determine which calculation constraints are available. For every set of calculation constraints there is exactly one set of selection constraints that uniquely identifies the calculation constraints.

For detailed constraint definitions by rule type, see [GenFORM Section 6: Selection and Calculation Constraints](genform-free-text-to-operational-rules.md#6-selection-and-calculation-constraints).

#### Selection Constraints by Rule Type

- **Dose Rule**: Source, Generic, Indication, Route, Setting, Patient, Dose Type, Component, Substance
- **Reconstitution Rule**: Generic, GPK, Form, Route, Setting
- **Dilution Rule**: Generic, Form, Route, Indication, Dose Type, Setting, Vascular Access, Patient, Dose, Substance
- **Renal Rule**: Source, Generic, Indication, Patient, Renal Function

#### Calculation Constraints by Rule Type

- **Dose Rule**: Schedule, Duration, Dose Limits
- **Reconstitution Rule**: Diluent Volume, Expansion Volume
- **Dilution Rule**: Volume, Drip Rate, Administration Fraction, Dose, Concentration
- **Renal Rule**: Schedule, Dose Adjustment (relative or absolute)

**Example**:

- Unconstrained patient context → all Order Scenarios possible
- Patient constrained to Female + specific age → indication "Contraception" becomes available
- Same patient with Gender=Male → "Contraception" removed (no Dose Rule exists for Male + Contraception)

Available options are computed by filtering rules against patient context (Order Context) and collecting matching Order Scenarios.

### Formal Constraint Execution Model

GenORDER operates as the execution engine of a hybrid constraint system implementing a **Constraint Satisfaction Problem (CSP)** following Constraint Logic Programming (CLP) principles.

#### Filter Stage (Selection Constraints from GenFORM)

At this stage, categorical selection constraints originating from GenFORM are applied. See [GenFORM Section 6.1](genform-free-text-to-operational-rules.md#61-selection-constraints) for complete selection constraint definitions and [GenORDER Section 3.1](genorder-operational-rules-to-orders.md#3.1.-filter-stage-(inherited-from-genform)) for execution details.

- Generic
- Indication
- Route
- Pharmaceutical Form
- Setting
- Patient Category (the specific Patient's attributes are matched against Patient Category ranges in rules)
- Dose Type
- Vascular Access

This stage produces a bounded rule domain that is guaranteed to contain only clinically valid rule sets.

**CLP Principles in Practice:**

- **Arc consistency**: When a Patient's attributes are applied (e.g., Age=5 years), Dose Rules with Patient Categories having incompatible age ranges are removed from the domain
- **Forward checking**: After selecting Generic + Indication, system immediately filters Routes, Forms, and Dose Types to detect empty domains early
- **Backtracking with constraint propagation**: If Generic + Route selection leads to no matching Dose Rules (empty domain), user backtracks to revise selection

#### Solver Stage (Calculation Constraints via GenSOLVER)

GenORDER transforms the quantitative calculation constraints into explicit equations. These equations are passed to GenSOLVER, which applies constraint logic programming and monotonic domain refinement to compute valid numerical values. See [GenSOLVER: Formal Constraint Solving Model](gensolver-from-orders-to-quantitative-solutions.md#3.-formal-constraint-solving-model) for the solving algorithm, [GenORDER Section 3.2](genorder-operational-rules-to-orders.md#3.2-solver-stage-(genorder-+-gensolver)) for equation construction, and [GenORDER Appendix D: Equations Table](genorder-operational-rules-to-orders.md#appendix-d.1.-equations-table) for the complete equation system.

**Lattice Theory for Calculation Constraints:**

Quantitative domains (Age, Weight, dose limits) form a lattice (`D`, `⊑`):

```text
- ⊤ = unrestricted range (e.g., Age: 0-150 years)
- ⊥ = empty set (over-constrained/invalid)
- D₁ ⊑ D₂ means D₁ is more constrained (D₁ ⊆ D₂)
```

GenSOLVER applies monotone functions that (see [GenSOLVER Section 3.2: Monotonic Domain Refinement](gensolver-from-orders-to-quantitative-solutions.md#3.2-monotonic-domain-refinement)):

- Tighten minimum/maximum bounds
- Increase increment (coarsening)
- Shrink discrete value sets
- Guarantee convergence (each variable's domain on finite descent chain)
- Preserve completeness (all valid solutions remain)

**The solver guarantees:**

- **Soundness**: all solutions satisfy all constraints
- **Completeness**: all valid solutions remain available
- **Monotonic convergence**: each constraint application reduces the solution space

### Hybrid Architecture

The system uses a two-stage hybrid architecture that transforms Operational Knowledge Rules into Quantitative Order Options:

```text
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                    OKRs → ORDER SCENARIOS → QUANTITATIVE OPTIONS                    │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                     │
│  FILTER STAGE (Selection Constraints)                                               │
│  ─────────────────────────────────────                                              │
│  Input:  All Operational Knowledge Rules                                            │
│  Process:                                                                           │
│    └─ Order Context creation → Patient (instance) + Indication(s) + Selection       │
│       Constraints                                                                   │
│    └─ Patient attributes → match against Patient Category ranges in Dose Rules      │
│    └─ Product selection → filter by Generic, Pharmaceutical Form, Route, Indication,│
│       Dose Type                                                                     │
│  Output: Bounded subset of applicable OKRs (only clinically valid rules remain)     │
│                                      ↓                                              │
│  SOLVER STAGE (Calculation Constraints)                                             │
│  ──────────────────────────────────────                                             │
│  Input:  Filtered OKRs with calculation constraints                                 │
│  Process:                                                                           │
│    └─ Extract Dose Limit ranges, volumes, concentrations from rules                 │
│    └─ Build equation system relating all order variables                            │
│    └─ GenSOLVER applies monotonic constraint refinement                             │
│  Output: Set of valid Order Scenarios with concrete quantitative values             │
│                                      ↓                                              │
│  RESULT: Quantitative Order Options                                                 │
│  ──────────────────────────────────                                                 │
│  Each option is:                                                                    │
│    • Mathematically valid (satisfies all equations)                                 │
│    • Clinically safe (satisfies all rule constraints)                               │
│    • Ready for selection by Health Professional                                     │
│                                                                                     │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

**Selection constraint variables** (defined at rule level): Generic, Pharmaceutical Form, Route, Indication, Dose Type, Setting, Patient Category, Vascular Access

**Note**: Patient Category is a rule-level constraint defining ranges (e.g., age 2-12 years, weight 10-40 kg). At runtime, a specific Patient (instance) is matched against these Patient Category ranges to determine which rules apply.

**Calculation constraint variables**: Schedule, Duration, Dose Limits (Quantity, PerTime, Rate with optional Adjustment); Volumes, Concentrations, Drip Rate, Administration Fraction

**Critical Property**: The user can only select from computed options. Since all options satisfy all constraints by construction, every selection is guaranteed safe. This is fundamentally different from systems that check orders *after* entry—here, invalid orders cannot be constructed in the first place.

### Rule Hierarchy

Rules are organized hierarchically for precedence. For details on rule sources and priority, see [GenFORM Section 3: Sources and Types of Dose Rules](genform-free-text-to-operational-rules.md#3-sources-and-types-of-dose-rules).

```text
Dose Rule (most specific match)
├── Generic + Indication + Route + Dose Type + Patient Category
├── Optional refinements: Brand, Pharmaceutical Form, GPKs, Setting
└── Component
    └── Substance (Item)
        └── Dose Limit
            ├── Dose Quantity, Dose Quantity Adjust (per kg or m²)
            ├── Dose Per Time, Dose Per Time Adjust (total per time period)
            └── Dose Rate, Dose Rate Adjust (continuous infusion)
```

**Dose semantics** (per [GenORDER Section 7: Quantitative Dose Semantics](genorder-operational-rules-to-orders.md#7.-quantitative-dose-semantics)):

- **Dose Quantity**: amount per administration
- **Dose Per Time**: accumulated dose per time unit (= Dose Quantity × Frequency)
- **Dose Rate**: continuous delivery speed (unit per time), typically for infusions
- **Dose Total**: cumulative dose over the entire order duration
- **Adjusted Dose**: dose normalized to Adjustment Unit (kg, m²) × Adjustment Quantity (patient value)

**Mathematical Dose Relations** (forming the equation system for GenSOLVER). See [GenSOLVER Section 6: Relationship to GenORDER Quantitative Semantics](gensolver-from-orders-to-quantitative-solutions.md#6.-relationship-to-genorder-quantitative-semantics) for how these relations are resolved, and [GenORDER Appendix D.1: Equations Table](genorder-operational-rules-to-orders.md#appendix-d.1.-equations-table) for the complete set of 65 equations by dose type:

- Dose PerTime = Dose Quantity × Frequency
- Dose Total = Dose PerTime × Order Duration
- Dose Quantity = Dose Rate × Administration Time
- Base Dose = Adjusted Dose × Adjustment Quantity

Multiple rules may match a patient context. The system:

1. Filters all rules matching selection constraints (creating Order Context)
2. Aggregates calculation constraints (using most restrictive or union of allowed values)
3. Passes aggregated constraints to GenSOLVER
4. Produces valid Order Scenarios

### Requirements

For the constraint-based system to deliver its safety and efficiency guarantees:

1. **Unambiguous Constraints**: Each rule must have a single, precise interpretation. Ambiguity in source knowledge must be resolved during the Free Text → OKR transformation.

2. **Evidence-Based Rules**: All constraints must be traceable to clinical guidelines, formularies, or validated expert sources. The system's safety depends on the correctness of encoded rules.

3. **Complete Coverage**: The constraint system must cover all clinically relevant scenarios. Gaps in rules could allow unsafe options.

4. **Workflow Integration**: The order of constraint application (selection sequence) is determined by the clinical workflow and user preferences, not by the system.

5. **Validity by Construction**: The system guarantees that no invalid option can be presented to the user. This is not post-hoc validation—it is a mathematical property of the constraint solver.

**The Central Guarantee**: If the Operational Knowledge Rules correctly encode expert knowledge, and the constraint solver correctly implements the mathematical model, then every Order Option presented to the user is safe by construction. The transformation pipeline:

```text
Free Text → OKRs → Selection Constraints → Calculation Constraints → Order Scenarios → Quantitative Options
```

preserves the safety properties of the original expert knowledge while making them computationally accessible for efficient clinical decision-making.

## Related Documents

This Core Domain Model document provides a high-level overview. For detailed specifications, refer to:

| Document | Description |
| -------- | ----------- |
| [GenFORM: Free Text to Operational Rules](genform-free-text-to-operational-rules.md) | Complete specification of how free-text expert knowledge is transformed into structured Operational Knowledge Rules (OKRs). Includes rule types, Patient Category definitions, constraint specifications, and data extraction details. |
| [GenORDER: Operational Rules to Orders](genorder-operational-rules-to-orders.md) | Complete specification of how OKRs are transformed into executable Order Scenarios. Includes Order Model (Orderable, Component, Item), dose semantics, equation system, and variable definitions. |
| [GenSOLVER: Order Scenarios to Quantitative Solutions](gensolver-from-orders-to-quantitative-solutions.md) | Complete specification of the quantitative constraint solving engine. Includes variable domains, equation types, unit-aware computation, propagation strategy, and solver guarantees (soundness, completeness, monotonic convergence). |

### Quick Reference by Topic

| Topic | GenFORM Reference | GenORDER Reference | GenSOLVER Reference |
| ----- | ----------------- | ------------------ | ------------------- |
| Rule Types (Dose, Dilution, Reconstitution, Renal) | [Section 3](genform-free-text-to-operational-rules.md#3-sources-and-types-of-dose-rules) | — | — |
| Patient Category Definition | [Appendix C.2](genform-free-text-to-operational-rules.md#addendum-c2-dose-rule-model-table) | [Section 4](genorder-operational-rules-to-orders.md#4.-ordercontext) | — |
| Selection Constraints | [Section 6.1](genform-free-text-to-operational-rules.md#61-selection-constraints) | [Section 3.1](genorder-operational-rules-to-orders.md#3.1.-filter-stage-(inherited-from-genform)) | — |
| Calculation Constraints | [Section 6.2](genform-free-text-to-operational-rules.md#62-calculation-constraints) | [Section 3.2](genorder-operational-rules-to-orders.md#3.2-solver-stage-(genorder-+-gensolver)) | [Section 3](gensolver-from-orders-to-quantitative-solutions.md#3-formal-constraint-solving-model) |
| Order Model (Orderable, Component, Item) | — | [Section 6](genorder-operational-rules-to-orders.md#6.-order-model-(executable-structure)) | — |
| Dose Semantics | — | [Section 7](genorder-operational-rules-to-orders.md#7.-quantitative-dose-semantics) | [Section 6](gensolver-from-orders-to-quantitative-solutions.md#6.-relationship-to-genorder-quantitative-semantics) |
| Equation System | — | [Appendix D.1](genorder-operational-rules-to-orders.md#appendix-d.1.-equations-table) | [Section 4](gensolver-from-orders-to-quantitative-solutions.md#4.-equation-types) |
| Variable Domains & Propagation | — | — | [Section 3.1](gensolver-from-orders-to-quantitative-solutions.md#3.1-variable-domains), [Section 7](gensolver-from-orders-to-quantitative-solutions.md#7.-variable-propagation-and-solving-strategy) |
| Unit-Aware Computation | — | — | [Section 5](gensolver-from-orders-to-quantitative-solutions.md#5.-unit-aware-computation) |
| Logging & Traceability | — | — | [Section 8](gensolver-from-orders-to-quantitative-solutions.md#8.-logging-and-explainability) |
| Library Architecture | [Appendix B.3](genform-free-text-to-operational-rules.md#addendum-b3-genform-libraries) | [Appendix B.3](genorder-operational-rules-to-orders.md#addendum-b.3.-genorder-libraries) | [Section 9](gensolver-from-orders-to-quantitative-solutions.md#9-technical-architecture-and-library-positioning) |
