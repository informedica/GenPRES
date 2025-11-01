# The Core Domain Model

- [The Core Domain Model](#the-core-domain-model)
  - [Domain Boundaries](#domain-boundaries)
  - [The Order Management Cycle](#the-order-management-cycle)
  - [Key Concepts](#key-concepts)
  - [Rule-Based CDS](#rule-based-cds)
  - [AI-Based CDS](#ai-based-cds)
  - [Exposure and Outcome](#exposure-and-outcome)
  - [Operational Knowledge Rules](#operational-knowledge-rules)
    - [Rule Structure](#rule-structure)
    - [Constraint Application](#constraint-application)
    - [Formal Constraint Logic Programming Paradigm](#formal-constraint-logic-programming-paradigm)
    - [Hybrid Architecture](#hybrid-architecture)
    - [Rule Hierarchy](#rule-hierarchy)
    - [Requirements](#requirements)

The Core Domain Model aims to model the general concept of treating a *Patient* by applying *Orders*, such as medication orders. The model is designed to be extensible to other types of *Orders* (e.g., procedures, therapies) and adaptable to various clinical contexts.

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

An *Operational Knowledge Rule* constrains the set of possible **3. Orders** on a specific *Patient* in such way that a *Health Professional* can narrow down options to the most appropriate ones for that patient context. Constraints can be derived from **4. Validation**, **5. Planning**, and **6. Preparation** stages of the *Order Management Cycle*.

### Rule Structure

Operational knowledge rules are implemented as three rule types in `Informedica.GenForm.Lib`:

1. **DoseRule**: Defines dosing limits per indication, generic, route, patient category, and dose type
2. **SolutionRule**: Defines preparation constraints (volumes, concentrations, diluents) per patient category and vascular access
3. **ReconstitutionRule**: Defines reconstitution steps (diluent volumes, expansion volumes) per product and route

Each rule contains:

- **Categorical constraints**: Generic, Shape, Route, Indication, Gender, DoseType, Department, Vascular Access (PVL/CVL)
- **Quantitative constraints**: Age, Weight, BSA, GestAge, PMAge ranges; dose limits (quantity, per-time, rate); volume limits; concentration limits

### Constraint Application

Rules structure constraints with a "belong to" or "match with" relationship:

**Example**:

- Unconstrained patient context → all orders possible
- Patient constrained to Female + specific age → indication "Contraception" becomes available
- Same patient with Gender=Male → "Contraception" removed (no dose rule exists for Male + Contraception)

Available options are computed by filtering rules against patient context and collecting matching options.

### Formal Constraint Logic Programming Paradigm

The system implements a **Constraint Satisfaction Problem (CSP)** following Constraint Logic Programming (CLP) principles:

**Monotonic Constraint Refinement:**

- Start with all rules (maximum `set ⊤`)
- Apply patient constraints to filter matching rules
- Extract valid options from remaining rules
- Each constraint application only narrows the set (monotonic function `f: D → D where D₁ ⊑ D₂ ⟹ f(D₁) ⊑ f(D₂)`)
- No valid solutions are lost (soundness)
- Guaranteed convergence to fixed point

**CLP Principles in Practice:**

- **Arc consistency**: When PatientCategory constraint is applied (e.g., Age=5 years), DoseRules with incompatible age ranges are removed from the domain
- **Forward checking**: After selecting Generic + Indication, system immediately filters Routes, Shapes, and DoseTypes to detect empty domains early
- **Backtracking with constraint propagation**: If Generic + Route selection leads to no matching DoseRules (empty domain), user backtracks to revise selection

**Lattice Theory for Quantitative Constraints:**

Quantitative domains (Age, Weight, dose limits) form a lattice (`D`, `⊑`):

```text
- ⊤ = unrestricted range (e.g., Age: 0-150 years)
- ⊥ = empty set (over-constrained/invalid)
- D₁ ⊑ D₂ means D₁ is more constrained (D₁ ⊆ D₂)
```

GenSolver applies monotone functions that:

- Tighten minimum/maximum bounds
- Increase increment (coarsening)
- Shrink discrete value sets
- Guarantee convergence (each variable's domain on finite descent chain)
- Preserve completeness (all valid solutions remain)

### Hybrid Architecture

The system uses a two-stage hybrid architecture combining categorical and quantitative constraint solving:

```text
Filter Stage (Categorical CSP)
  └─ Patient attributes → filter DoseRules by PatientCategory
  └─ Product selection → filter by Generic, Shape, Route, Indication and DoseType
  └─ Result: Subset of applicable rules
       ↓
Solver Stage (Quantitative Constraints)
  └─ Extract DoseLimit ranges from filtered rules
  └─ GenSolver applies equations with min/max/increment constraints
  └─ Result: Valid solution set with concrete values
```

**Categorical variables**: Generic, Shape, Route, Indication, Gender, DoseType, Department, Location (PVL/CVL)

**Quantitative variables**: Age, Weight, BSA, GestAge, PMAge (ranges); Dose quantities, volumes, concentrations (min/max with units)

### Rule Hierarchy

Rules are organized hierarchically for precedence:

```text
DoseRule (most specific match)
├── Generic + Indication + Route + DoseType + PatientCategory
├── Optional refinements: Brand, Shape, GPKs, Department
└── ComponentLimits
    └── SubstanceLimits (DoseLimit per substance)
        ├── Quantity, QuantityAdjust (per kg or m²)
        ├── PerTime, PerTimeAdjust (total per time period)
        └── Rate, RateAdjust (continuous infusion)
```

Multiple rules may match a patient. The system:

1. Filters all rules matching categorical constraints
2. Aggregates quantitative limits (using most restrictive or union of allowed values)
3. Passes aggregated constraints to solver

### Requirements

- Constraints must be unambiguous (single interpretation per rule)
- Constraints must be evidence-based (sourced from clinical guidelines)
- Application must fit clinical workflow (order of constraint application is user-determined)
- System guarantees validity (no invalid option is presented)
