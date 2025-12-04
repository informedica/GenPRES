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

- **GenFORM**: Extracts and structures free-text knowledge into Operational Knowledge Rules (OKRs)
- **GenORDER**: Transforms OKRs into Order Scenarios by applying selection constraints and building equation systems
- **GenSOLVER**: Solves the equation systems to produce quantitative order options

## Core Definitions

| Term | Definition |
| ----- | ----- |
| *Operational Knowledge Rule (OKR)* | A fully structured, machine-interpretable, constraint-based representation of expert medication (and other clinical) knowledge. |
| *Selection Constraint* | A categorical constraint used to determine which calculation constraints apply (e.g., indication, generic, route, form, setting). |
| *Calculation Constraint* | A quantitative constraint used to compute numerical values such as dose quantities, rates, volumes, or durations. |
| *Order Context* | The bounded clinical context; composed of patient data, indication(s), and selection constraints from which Order Scenarios are generated. |
| *Order Scenario* | A fully constrained, uniquely identifiable, computable clinical alternative representing one valid way to prescribe, prepare, and administer an order. |
| *Order* | The executable prescription instance derived from an Order Scenario. |
| *Dose Rule* | An OKR that defines qualitative and quantitative constraints for dosing a specific generic in a defined clinical context. |
| *Dilution Rule* | An OKR that defines requirements for the amount and/or concentration of liquid medication. |
| *Reconstitution Rule* | An OKR that defines how medication must be reconstituted to enable administration (e.g., converting powder to liquid). |
| *Renal Rule* | An OKR used to adjust the dose advice according to renal function (GFR). |
| *Dose Type* | The temporal category of dosing: once, onceTimed, discontinuous, timed, or continuous. |
| *Adjustment Unit* | A patient normalization unit used to scale doses (e.g., kg for weight, m² for BSA). |
| *Filter Stage* | The stage in which categorical selection constraints are applied to determine which rules and scenarios are applicable. |
| *Solver Stage* | The stage in which quantitative calculation constraints are transformed into equations and solved by GenSOLVER. |
| *GenFORM* | The system that transforms free-text medication knowledge into fully computable Operational Knowledge Rules (OKRs). |
| *GenORDER* | The execution-layer system that transforms OKRs into patient-specific, fully computable and executable Order Scenarios. |
| *GenSOLVER* | The constraint solving engine that resolves the equation systems generated by GenORDER into numerically valid solutions. |

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

Operational Knowledge Rules (OKRs) are implemented as four rule types in `Informedica.GenFORM.Lib`:

1. **Dose Rule**: Defines dosing limits per indication, generic, route, patient category, and dose type
2. **Dilution Rule**: Defines preparation constraints (volumes, concentrations, solutions) per patient category and vascular access
3. **Reconstitution Rule**: Defines reconstitution steps (diluent volumes, expansion volumes) per product and route
4. **Renal Rule**: Defines dose adjustments based on renal function (GFR)

Each rule contains:

- **Selection Constraints**: Generic, Form, Route, Indication, Gender, DoseType, Setting (Location/Department), Vascular Access (PVL/CVL)
- **Calculation Constraints**: Age, Weight, BSA, GestAge, PMAge ranges; dose limits (quantity, per-time, rate); volume limits; concentration limits

### Selection and Calculation Constraints

All OKRs can be translated to either selection or calculation constraints. Selection constraints determine which calculation constraints are available. For every set of calculation constraints there is exactly one set of selection constraints that uniquely identifies the calculation constraints.

#### Selection Constraints by Rule Type

- **Dose Rule**: Source, Generic (Form, Brand, GPKs), Indication, Route, Setting (Location, Department), Patient (Gender, Age, Weight, BSA, GestAge, PMAge), DoseType, Substance
- **Reconstitution Rule**: Generic, Form, Route, Setting
- **Dilution Rule**: Generic, Form, Route, Indication, DoseType, Vascular Access (CVL/PVL), Setting, Patient (Age, Weight), Substance
- **Renal Rule**: Source, Generic, Indication, Route, Renal Function

#### Calculation Constraints by Rule Type

- **Dose Rule**: Schedule (Frequencies, Time, Interval, Duration), Dose Limits (Quantity, PerTime, Rate, with optional Adjustment)
- **Reconstitution Rule**: Diluent Volume, Expansion Volume, Diluents
- **Dilution Rule**: Volume, Concentration, Solutions
- **Renal Rule**: Schedule, Dose Adjustment (relative or absolute)

**Example**:

- Unconstrained patient context → all Order Scenarios possible
- Patient constrained to Female + specific age → indication "Contraception" becomes available
- Same patient with Gender=Male → "Contraception" removed (no Dose Rule exists for Male + Contraception)

Available options are computed by filtering rules against patient context (Order Context) and collecting matching Order Scenarios.

### Formal Constraint Execution Model

GenORDER operates as the execution engine of a hybrid constraint system implementing a **Constraint Satisfaction Problem (CSP)** following Constraint Logic Programming (CLP) principles.

#### Filter Stage (Selection Constraints from GenFORM)

At this stage, categorical selection constraints originating from GenFORM are applied:

- Generic (Form, Brand, GPKs)
- Indication
- Route
- Setting (Location, Department)
- Patient Category (Gender, Age, Weight, BSA, GestAge, PMAge)
- Dose Type
- Vascular Access

This stage produces a bounded rule domain that is guaranteed to contain only clinically valid rule sets.

**CLP Principles in Practice:**

- **Arc consistency**: When Patient constraint is applied (e.g., Age=5 years), Dose Rules with incompatible age ranges are removed from the domain
- **Forward checking**: After selecting Generic + Indication, system immediately filters Routes, Forms, and Dose Types to detect empty domains early
- **Backtracking with constraint propagation**: If Generic + Route selection leads to no matching Dose Rules (empty domain), user backtracks to revise selection

#### Solver Stage (Calculation Constraints via GenSOLVER)

GenORDER transforms the quantitative calculation constraints into explicit equations. These equations are passed to GenSOLVER, which applies constraint logic programming and monotonic domain refinement to compute valid numerical values.

**Lattice Theory for Calculation Constraints:**

Quantitative domains (Age, Weight, dose limits) form a lattice (`D`, `⊑`):

```text
- ⊤ = unrestricted range (e.g., Age: 0-150 years)
- ⊥ = empty set (over-constrained/invalid)
- D₁ ⊑ D₂ means D₁ is more constrained (D₁ ⊆ D₂)
```

GenSOLVER applies monotone functions that:

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
│    └─ Order Context creation → Patient + Indication(s) + Selection Constraints      │
│    └─ Patient attributes → filter Dose Rules by Patient Category                    │
│    └─ Product selection → filter by Generic, Form, Route, Indication, Dose Type     │
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

**Selection constraint variables**: Generic, Form, Route, Indication, Gender, Dose Type, Setting (Location/Department), Vascular Access (PVL/CVL)

**Calculation constraint variables**: Age, Weight, BSA, GestAge, PMAge (ranges); Dose Quantity, Dose Per Time, Dose Rate, Dose Total (min/max with units); Volumes, Concentrations

**Critical Property**: The user can only select from computed options. Since all options satisfy all constraints by construction, every selection is guaranteed safe. This is fundamentally different from systems that check orders *after* entry—here, invalid orders cannot be constructed in the first place.

### Rule Hierarchy

Rules are organized hierarchically for precedence:

```text
Dose Rule (most specific match)
├── Generic + Indication + Route + Dose Type + Patient Category
├── Optional refinements: Brand, Form, GPKs, Setting (Location/Department)
└── Component
    └── Substance
        └── Dose Limit
            ├── Dose Quantity, Dose Quantity Adjust (per kg or m²)
            ├── Dose Per Time, Dose Per Time Adjust (total per time period)
            └── Dose Rate, Dose Rate Adjust (continuous infusion)
```

**Dose semantics** (per GenORDER):

- **Dose Quantity**: amount per administration
- **Dose Per Time**: accumulated dose per time unit (= Dose Quantity × Frequency)
- **Dose Rate**: continuous delivery rate
- **Dose Total**: cumulative dose over the entire order duration
- **Adjusted Dose**: dose normalized to Adjustment Unit (kg, m²) × Adjustment Quantity (patient value)

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
