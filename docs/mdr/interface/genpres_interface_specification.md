# GenPRES-EHR Treatment Plan Interface Specification - FHIR R4 Compliant

**Version:** 1.3
**Date:** September 2025  
**Document Type:** Technical Interface Specification  
**Target Audience:** Solution Architects, Integration Engineers, Clinical Informaticists  
**FHIR Version:** R4 (4.0.1)  
**G-Standard Compliance:** Required  
**IHE Profile Compliance:** IHE Pharmacy (PHARM), IHE Audit Trail and Node Authentication (ATNA), IHE Internet User Authorization (IUA)

## Table of Contents

- [1. Executive Summary](#1-executive-summary)
  - [Key Features](#key-features)
  - [Interface Benefits](#interface-benefits)
- [2. Document Scope and Objectives](#2-document-scope-and-objectives)
  - [2.1 Scope](#21-scope)
  - [2.2 Objectives](#22-objectives)
  - [2.3 Out of Scope](#23-out-of-scope)
- [3. Core Architecture Principles](#3-core-architecture-principles)
  - [3.1 FHIR-First Design](#31-fhir-first-design)
  - [3.2 Treatment Plan as CarePlan](#32-treatment-plan-as-careplan)
  - [3.3 G-Standard Through FHIR Coding](#33-g-standard-through-fhir-coding)
  - [3.4 Stateless GenPRES with FHIR Persistence](#34-stateless-genpres-with-fhir-persistence)
- [4. System Overview](#4-system-overview)
  - [4.1 System Context Diagram](#41-system-context-diagram)
  - [4.2 Data Flow Overview](#42-data-flow-overview)
  - [4.3 Integration Patterns](#43-integration-patterns)
- [5. Data Exchange Specifications](#5-data-exchange-specifications)
  - [5.1 Patient Context](#51-patient-context)
    - [5.1.1 Age and Development Data Structures](#511-age-and-development-data-structures)
    - [5.1.2 Physical Measurement Structure](#512-physical-measurement-structure)
    - [5.1.3 Clinical Status Enumerations](#513-clinical-status-enumerations)
    - [5.1.4 Laboratory Data](#514-laboratory-data)
  - [5.2 Treatment Plan Request (EHR → GenPRES)](#52-treatment-plan-request-ehr--genpres)
- [6. Example Treatment Plan Scenarios](#6-example-treatment-plan-scenarios)
  - [6.1 Single-Product Once-Scenario](#61-single-product-once-scenario)
  - [6.2 Single-Product Once-Timed Scenario](#62-single-product-once-timed-scenario)
  - [6.3 Single-Product Discontinuous Scenario](#63-single-product-discontinuous-scenario)
  - [6.4 Single-Product Discontinuous-Timed Scenario](#64-single-product-discontinuous-timed-scenario)
  - [6.5 Single-Product Continuous Scenario](#65-single-product-continuous-scenario)
  - [6.6 Multi-Product Continuous Scenario](#66-multi-product-continuous-scenario)
  - [6.7 Multi-Product Once-Timed Scenario](#67-multi-product-once-timed-scenario)
  - [6.8 Multi-Product with Reconstitution Discontinuous-Timed Scenario](#68-multi-product-with-reconstitution-discontinuous-timed-scenario)

## 1. Executive Summary

This interface specification defines the complete FHIR R4-compliant data exchange protocol between GenPRES (treatment plan entry  and management system with full checking and validation services) and hospital-wide EHR systems. The interface ensures G-Standard compliance for medication identification while enabling comprehensive treatment plan management through FHIR-based order scenarios with IHE Pharmacy profile compliance.

### Key Features

- **FHIR R4 Compliance**: Full adherence to FHIR R4 standard with proper resource usage
- **IHE Pharmacy Integration**: Compliance with IHE Pharmacy profile for medication management
- **G-Standard Compliance**: Full adherence to Dutch medication standards with GPK-based product identification
- **Stateless GenPRES Operation**: EHR maintains all persistent data while GenPRES provides full treatment plan management
- **OAuth2/SMART-on-FHIR Security**: Industry-standard healthcare authentication and authorization

### Interface Benefits

- **Interoperability**: FHIR-based communication ensuring standards compliance
- **Regulatory Compliance**: Full G-Standard and IHE profile adherence
- **Scalability**: Support for high-volume clinical environments
- **Maintainability**: Clear separation of concerns between systems

## 2. Document Scope and Objectives

### 2.1 Scope

This specification covers:

- FHIR R4 resource structures for complete treatment plan communication
- G-Standard compliance requirements integrated with FHIR coding systems
- OAuth2/SMART-on-FHIR authentication and authorization
- IHE Pharmacy profile compliance
- Clinical workflow integration patterns
- Session management protocols
- Performance and scalability requirements

### 2.2 Objectives

- Define complete FHIR-compliant interface for treatment plan management
- Ensure G-Standard compliance through proper FHIR coding and extensions
- Establish OAuth2/SMART-on-FHIR security integration
- Enable safe, efficient clinical workflows
- Support complex pediatric and critical care scenarios
- Maintain IHE profile compliance

### 2.3 Out of Scope

- Internal EHR system architecture
- GenPRES internal calculation algorithms
- Network infrastructure requirements
- Specific vendor implementation details
- Clinical training and change management

## 3. Core Architecture Principles

### 3.1 FHIR-First Design

All data exchanges use FHIR R4 resources with proper resource relationships:

- **Resource Integrity**: Complete FHIR resource definitions with required elements
- **Reference Consistency**: Proper use of resource references and contained resources
- **Extension Usage**: G-Standard specific data through FHIR extensions
- **Bundle Management**: Appropriate use of Bundle resources for transactions

### 3.2 Treatment Plan as CarePlan

Treatment plans are represented as FHIR CarePlan resources with:

- **Clinical Context Preservation**: Orders evaluated within complete care context
- **Activity Coordination**: MedicationRequest activities linked to CarePlan
- **Goal Alignment**: Treatment goals expressed as Goal resources
- **Workflow Integration**: Proper status management through CarePlan lifecycle

### 3.3 G-Standard Through FHIR Coding

G-Standard compliance achieved through FHIR coding systems:

- **GPK Integration**: GPK codes in Medication.code.coding with G-Standard system URI
- **Product Identification**: Complete product information in Medication resources
- **Product Compounding**: Product compound quanties with G-Standard compliant unit specification
- **Order Scheduling**: Order scheduling using G-Standard compliant time units

### 3.4 Stateless GenPRES with FHIR Persistence

EHR maintains FHIR resources while GenPRES provides calculations, validation, checking:

- **Resource Sovereignty**: EHR controls all FHIR resource persistence
- **Calculation Services**: GenPRES provides read-only calculation and validation
- **Session Management**: Temporary treatment plan state management in GenPRES, persistent data in EHR
- **FHIR Compliance**: All data exchanges through standard FHIR operations

## 4. System Overview

### 4.1 System Context Diagram

```text
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│                 │    │                  │    │                 │
│   EHR System    │◄──►│   GenPRES        │◄──►│  G-Standard     │
│                 │    │   Service        │    │  Database       │
│  - Patient Data │    │  - Calculations  │    │  - Products     │
│  - Orders       │    │  - Validation    │    │  - Dose Rules   │
│  - Persistence  │    │  - Instructions  │    │  - Interactions │
│  - Auth/Authz   │    │  - Safety Checks │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                        │                        │
         │                        │                        │
         ▼                        ▼                        ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Clinical      │    │  Treatment Plan  │    │  Medication     │
│   Workstation   │    │  Session         │    │  Safety Rules   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### 4.2 Data Flow Overview

- Session Initiation: EHR provides complete patient context and existing treatments
- Treatment Planning: GenPRES calculates and handles all possible order scenarios
- Clinical Review: Clinician selects and modifies scenarios as needed
- Validation: Complete treatment plan validated against G-Standard rules and Dutch Pharmaceutical Formularies
- Finalization: EHR receives complete treatment plan with implementation instructions
- Session Closure: All temporary data discarded, persistent data remains in EHR

### 4.3 Integration Patterns

- RESTful API: Standard HTTP-based interface for maximum interoperability
- Event-Driven Updates: Real-time notifications for clinical alerts and status changes
- Session-Based Communication: Stateful sessions for complex treatment planning workflows
- Bulk Data Transfer: Efficient transfer of complete treatment plans and scenarios

## 5. Data Exchange Specifications

### 5.1 Patient Context

The patient context provides all clinical information necessary for accurate dose calculations and safety checking.

```fsharp
type PatientContext = {
    // Patient Demographics
    EhrPatientId: string                    // EHR internal patient ID (required)
    Gender: Gender                          // Male | Female | UnknownGender (required)

    // Age Information
    BirthDate: DateTime                     // Birtdate (required)
    GestationalAge: GestationalAge option   // For neonatal/pediatric patients

    // Physical Measurements
    Weight: PhysicalMeasurement             // Weight with percentile ranges (required)
    Height: PhysicalMeasurement             // Height with percentile ranges (required)

    // Clinical Context
    Department: string option               // ICK, NEO, ICU, HCK, etc. (required for filtering)
    Diagnoses: string[]                     // ICD-10 or institutional diagnosis codes
    Allergies: string[]                     // Known allergies and contraindications

    // Access & Clinical Status
    Access: VascularAccess[]                // Available vascular access options
    RenalFunction: RenalFunction option     // Renal function status for dose adjustment
    HepaticFunction: HepaticFunction option // Hepatic function for drug metabolism

    // Monitoring Requirements
    LaboratoryValues: LaboratoryValue[]     // Recent relevant lab values
}
```

#### 5.1.1 Age and Development Data Structures

```fsharp
type Gender = Male | Female | UnknownGender

type GestationalAge = {
    Weeks: int                            // Gestational weeks (20-45)
    Days: int                             // Additional days (0-6)
}
```

#### 5.1.2 Physical Measurement Structure

```fsharp
type PhysicalMeasurement = {
    Estimated: int option                // Mean estimate (age-appropriate)
    Measured: int option                 // Actual measured value (preferred)
}
```

- Weight Units: Always in grams
- Height Units: Always in centimeters

#### 5.1.3 Clinical Status Enumerations

```fsharp
type VascularAccess =
    | CVL                                  // Central venous line
    | PVL                                  // Peripheral venous line
    | IO                                   // Intraosseous access
    | Peripheral                           // Peripheral IV
    | Arterial                             // Arterial line
    | Other of string                      // Institution-specific access

type RenalFunction =
    | EGFR of min: int option * max: int option    // eGFR range in mL/min/1.73m²
    | IntermittentHemoDialysis                     // Intermittent hemodialysis
    | ContinuousHemoDialysis                       // Continuous hemodialysis
    | PeritonealDialysis                           // Peritoneal dialysis
    | AcuteKidneyInjury of stage: int              // AKI stage 1-3

type HepaticFunction =
    | Normal                              // Normal hepatic function
    | ChildPughA                          // Child-Pugh Class A
    | ChildPughB                          // Child-Pugh Class B
    | ChildPughC                          // Child-Pugh Class C
    | AcuteLiverFailure                   // Acute liver failure
```

#### 5.1.4 Laboratory Data

```fsharp
type LaboratoryValue = {
    TestName: string                      // Laboratory test name
    Value: float                          // Numeric result
    Unit: string                          // Unit of measure
    ReferenceRange: string option         // Normal range for patient
    Timestamp: DateTime                   // When sample was collected
    CriticalFlag: bool                    // Critical/panic value flag
}
```

### 5.2 Treatment Plan Request (EHR → GenPRES)

The treatment plan request initiates a clinical session and with the overall intent that the entire treatment plan can be modified.

```fsharp
type TreatmentPlanRequest = {
    // Session Management
    SessionId: string                               // EHR-generated session identifier (required)
    RequestTimestamp: DateTime                      // When request was initiated (required)

    // Patient context
    PatientContext: PatientContext                  // Patient identification and relevant patient data

    // New Order Requests
    TreatmentPaln: OrderScenario[]                  // Requested new medications

    // Clinical Constraints
    GlobalConstraints: TreatmentConstraint[]        // Plan-wide limitations
    InstitutionalPolicies: Policy[]                 // Hospital-specific requirements

    // Authorization Context
    PrescriberContext: PrescriberContext            // Prescriber permissions and limitations (required)
}
```

```fsharp
type OrderScenario =
    {
        Indication: string
        Medication: string
        Route: string
        Shape: string
        DoseType: DoseType
        DoseText: string
    }
and DoseType =
    | Continuous
    | Once
    | OnceTimed
    | Discontinuous
    | Timed

type OrderType =
    | Medication
    | ParenteralFeeding
    | EnteralFeeding

type Order =
    {
        Type : OrderType
        StartDate: DateTime 
        StopDate: DateTime option
        Name: string
        Compounds : Compound[]
        Schema: Schema
    }
and Compound =
    {
        Id: string
        Quantity: decimal
        Unit: string
    }
and Schema =
    | Pattern
    | ExactTimes
```

## 6. Example Treatment Plan Scenarios

## 6.1 Single-Product Once-Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 19,5 kg
Height: 109 cm

Scenario Context
Indicaties: Pijn, acuut/post-operatief
Medicatie: paracetamol
Routes: RECTAAL
Vorm: zetpil
Doseer types: Once|Startdosering

Voorschrift
paracetamol 750 mg = 38,5 mg/kg (40 mg/kg/dosis)
Bereiding
zetpil 1 stuk paracetamol 750 mg/stuk
Toediening
1 stuk paracetamol 750 mg in 1 stuk
Planning
Start: direct
Einde: n.v.t.
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 19500 # grams
    height: 109   # cm

indication: "Pijn, acuut/post-operatief"
route: "RECTAAL"
shape: "zetpil"
dose-type: "Once|Startdosering"

order: "paracetamol"
start-date: "2025-09-22T10:11:00Z"
stop-date: null

products:
    - gpk: "2345678" # gpk code for paracetamol 750 mg/stuk zetpil
        quantity: 1
        unit: "stuk"

administration:
    quantity: 1
    unit: "stuk"

schema:
    - pattern: # eenmalig
        frequency: 1
        time: null
        unit: null
    - rate:
        quantity: null
        unit1: null
        unit2: null
    - exact-times: []
```

## 6.2 Single-Product Once-Timed Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Pijn, acuut/post-operatief
Medicatie: paracetamol
Routes: INTRAVENEUS
Vorm: infusievloeistof
Doseer types: Once|Startdosering

Voorschrift
paracetamol 220 mg = 20 mg/kg (20 mg/kg/dosis)
Bereiding
infusievloeistof 22 mL paracetamol 10 mg/mL
Toediening
22 mL paracetamol 220 mg in 22 mL = 85 mL/uur ( 16 min )
Start: 2025-09-22 10:11
Einde: n.v.t.
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Pijn, acuut/post-operatief"
route: "INTRAVENEUS"
shape: "infusievloeistof"
dose-type: "Once|Startdosering"

order: "paracetamol"
start-date: "2025-09-22T10:11:00Z"
stop-date: null

products:
    - gpk: "3456789" # gpk code for paracetamol 10 mg/mL infusievloeistof
        quantity: 22
        unit: "mL"

administration:
    quantity: 22
    unit: "mL"

schema:
    - pattern: # eenmalig
        frequency: 1
        time: null
        unit: null
    - rate:
        quantity: 85
        unit1: "mL"
        unit2: "uur"
    - exact-times: []
```

## 6.3 Single-Product Discontinuous Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Milde tot matige pijn; koorts
Medicatie: paracetamol
Routes: RECTAAL
Vorm: zetpil
Doseer types: Discontinuous|onderhoud

Voorschrift
4 x/dag paracetamol 180 mg = 65,5 mg/kg/dag (10 - 20 mg/kg/dosis)
Bereiding
zetpil 1 stuk paracetamol 180 mg/stuk
Toediening
4 x/dag 1 stuk paracetamol 180 mg in 1 stuk
Planning
Start: 2025-09-22 10:11
Einde: 2025-09-29 10:11
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Milde tot matige pijn; koorts"
route: "RECTAAL"
shape: "zetpil"
dose-type: "Discontinuous|onderhoud"

order: "paracetamol"
start-date: "2025-09-22T10:11:00Z"
stop-date: null

products:
    - gpk: "2345678" # gpk code for paracetamol 180 mg/stuk zetpil
        quantity: 1
        unit: "stuk"

administration:
    quantity: 1
    unit: "stuk"

schema:
    - pattern: # discontinu 4x per dag
        frequency: 4
        time: 1
        unit: "dag"
    - rate:
        quantity: null
        unit1: null
        unit2: null
    - exact-times: []
```

## 6.4 Single-Product Discontinuous-Timed Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Pijn, acuut/post-operatief
Medicatie: paracetamol
Routes: INTRAVENEUS
Vorm: infusievloeistof
Doseer types: Timed|onderhoud

Voorschrift
4 x/dag paracetamol 175 mg = 63,6 mg/kg/dag (60 mg/kg/dag)
Bereiding
infusievloeistof 17,5 mL paracetamol 10 mg/mL
Toediening
4 x/dag 17,5 mL paracetamol 175 mg in 17,5 mL = 70 mL/uur (15 min)
Planning
Start: 2025-09-22 08:11
Einde: 2025-09-29 10:11
Exacte tijden: 10:00, 16:00, 22:00, 04:00
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Pijn, acuut/post-operatief"
route: "INTRAVENEUS"
shape: "infusievloeistof"

order: "paracetamol"
start-date: 2025-09-22T10:08:00Z
stop-date: 2025-09-29T10:11:00Z
dose-type: "Timed|onderhoud"

products:
    - gpk: "2345678" # gpk code for paracetamol 10 mg/mL infusievloeistof
        quantity: 17.5
        unit: "mL"

administration:
    quantity: 17.5
    unit: "mL"

schema:
    - pattern: # discontinu 4x per dag 
        frequency: 4
        time: 1
        unit: "dag"
    - rate: # 70 mL/uur (15 min)
        quantity: 70
        unit1: "mL"
        unit2: "uur"
    - exact-times:
        - "10:00"
        - "16:00"
        - "22:00"
        - "04:00"
```

## 6.5 Single-Product Continuous Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Sedatie
Medicatie: propofol
Routes: INTRAVENEUS
Vorm: emulsie voor injectie
Doseer types: Continuous|continu

Voorschrift
propofol 3 mg/kg/uur (1 - 4 mg/kg/uur)
Bereiding
emulsie voor injectie 50 mL propofol 10 mg/mL
Toediening
propofol 500 mg in 50 mL = 3,3 mL/uur ( 15 uur )
Planning
Start: 2025-09-22 10:11
Einde: 
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Sedatie"
route: "INTRAVENEUS"
shape: "emulsie voor injectie"
dose-type: "Continuous|continu"

order: "propofol"
start-date: 2025-09-22T10:08:00Z
stop-date: null

products:
    - gpk: "2345678" # gpk code for propofol 10 mg/mL emulsie voor injectie
        quantity: 50
        unit: "mL"

administration:
    quantity: 50
    unit: "mL"

schema:
    - pattern: 
        frequency: null
        time: null
        unit: null
    - rate: # 3,3 mL/uur (15 uur)
        quantity: 3.3
        unit1: "mL"
        unit2: "uur"
    - exact-times: []
```

## 6.6 Multi-Product Continuous Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Circulatoire insufficiëntie
Medicatie: noradrenaline
Routes: INTRAVENEUS
Vorm: concentraat voor oplossing voor infusie
Verdunningsvorm: gluc 10%
Doseer types: Continuous|continu

Voorschrift
noradrenaline 0,152 microg/kg/min (0,05 - 2 microg/kg/min)
Bereiding
concentraat voor oplossing voor infusie 5 mL noradrenaline 1 mg/mL
vloeistof (gluc 10%) 45 mL  
Toediening
noradrenaline 5 mg in 50 mL = 1 mL/uur ( 50 uur )
Planning
Start: 2025-09-22 10:11
Einde: 
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Circulatoire insufficiëntie"
route: "INTRAVENEUS"
shape: "concentraat voor oplossing voor infusie"
dose-type: "Continuous|continu"

order: "noradrenaline"
start-date: 2025-09-22T10:08:00Z
stop-date: null

products:
    - gpk: "2345678" # gpk code for noradrenaline 1 mg/mL concentraat voor oplossing voor infusie
        quantity: 5
        unit: "mL"
    - gpk: "3456789" # gpk code for gluc 10% vloeistof
        quantity: 45
        unit: "mL"

administration:
    quantity: 50
    unit: "mL"

schema:
    - pattern: 
        frequency: null
        time: null
        unit: null
    - rate: # 1 mL/uur (15 uur)
        quantity: 1
        unit1: "mL"
        unit2: "uur"
    - exact-times: []
```

## 6.7 Multi-Product Once-Timed Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Ernstige therapieresistente hartritmestoornissen
Medicatie: amiodaron
Routes: INTRAVENEUS
Vorm: injectievloeistof
Verdunningsvorm: gluc 10%
Doseer types: Once|Startdosering

Voorschrift
amiodaron 54,6 mg = 4,96 mg/kg (5 mg/kg/dosis)
Bereiding
injectievloeistof 6 mL amiodaron 50 mg/mL
vloeistof (gluc 10%) 44 mL  
Toediening
9,1 mL amiodaron 300 mg in 50 mL = 18 mL/uur ( 30 min )
Planning
Start: 2025-09-22 10:11
Einde: n.v.t.
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Ernstige therapieresistente hartritmestoornissen"
route: "INTRAVENEUS"
shape: "injectievloeistof"
dose-type: "Once|Startdosering"

order: "amiodaron"
start-date: "2025-09-22T10:11:00Z"
stop-date: null

products:
    - gpk: "2345678" # gpk code for amiodaron 50 mg/mL injectievloeistof
        quantity: 6
        unit: "mL"
    - gpk: "3456789" # gpk code for gluc 10% vloeistof  
        quantity: 44
        unit: "mL"

administration:
    quantity: 9.1 #9,1 mL amiodaron 300 mg in 50 mL = 54,6 mg (5 mg/kg/dosis)
    unit: "mL"

schema:
    - pattern: # eenmalig
        frequency: 1
        time: null
        unit: null
    - rate:
        quantity: 18 # 18 mL/uur (30 min)
        unit1: "mL"
        unit2: "uur"
    - exact-times: []
```

## 6.8 Multi-Product with Reconstitution Discontinuous-Timed Scenario

```text
Patient Context
Geslacht: mannelijk
Weight: 11 kg
Height: 79 cm

Scenario Context
Indicaties: Bacteriële infecties
Medicatie: vancomycine
Routes: INTRAVENEUS
Vorm: poeder voor oplossing voor infusie
Verdunningsvorm: gluc 5%
Doseer types: Timed|onderhoud

Voorschrift
4 x/dag vancomycine 149 mg = 54,2 mg/kg/dag (60 mg/kg/dag)
Bereiding
poeder voor oplossing voor infusie 10 mL vancomycine 1000 mg/20 mL
vloeistof (gluc 5%) 40 mL  
Toediening
4 x/dag 14,9 mL vancomycine 500 mg in 50 mL = 59 mL/uur ( 15 min )
```

```yaml
patient:
    ehr-patient-id: "123456"
    gender: "male"
    weight: 1100 # grams
    height: 79   # cm

indication: "Bacteriële infecties"
route: "INTRAVENEUS"
shape: "poeder voor oplossing voor infusie"
dose-type: "Timed|onderhoud"

order: "vancomycine"
start-date: "2025-09-22T10:11:00Z"
stop-date: null

reconstitution:
    - gpk: "2345678" # gpk code for vancomycine 1000 mg poeder voor oplossing voor infusie
        quantity: 1
        unit: "stuk"
    - gpk: "89879" # gpk code for water voor oplossing  
        quantity: 20
        unit: "mL"

products:
    - gpk: "2345678" # gpk code for vancomycine 50 mg/mL poeder voor oplossing voor infusie
        quantity: 10
        unit: "mL"
    - gpk: "3456789" # gpk code for gluc 10% vloeistof  
        quantity: 40
        unit: "mL"

administration:
    quantity: 14.9 #14,9 mL vancomycine 500 mg in 50 mL = 149 mg (54,2 mg/kg/dag)
    unit: "mL"

schema:
    - pattern: # eenmalig
        frequency: 4
        time: 1
        unit: "dag"
    - rate:
        quantity: 59 # 59 mL/uur (15 min)
        unit1: "mL"
        unit2: "uur"
    - exact-times: []
```
