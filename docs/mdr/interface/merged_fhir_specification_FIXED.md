# GenPRES-EHR Treatment Plan Interface Specification - FHIR R4 Compliant

**Version:** 1.2  
**Date:** January 2026  
**Document Type:** Technical Interface Specification  
**Target Audience:** Solution Architects, Integration Engineers, Clinical Informaticists  
**FHIR Version:** R4 (4.0.1)  
**G-Standard Compliance:** Required  
**IHE Profile Compliance:** IHE Pharmacy (PHARM), IHE Audit Trail and Node Authentication (ATNA), IHE Internet User Authorization (IUA)

## Table of Contents

- [1. Executive Summary](#1-executive-summary)
- [2. Document Scope and Objectives](#2-document-scope-and-objectives)
- [3. Core Architecture Principles](#3-core-architecture-principles)
- [4. FHIR Resource Model](#4-fhir-resource-model)
- [5. Data Exchange Specifications](#5-data-exchange-specifications)
- [6. G-Standard Integration](#6-g-standard-integration)
- [7. Session Management and Workflow](#7-session-management-and-workflow)
- [8. Integration Architecture](#8-integration-architecture)
- [9. Clinical Safety and Compliance](#9-clinical-safety-and-compliance)
- [10. Performance and Scalability](#10-performance-and-scalability)
- [11. Security and Privacy](#11-security-and-privacy)
- [12. Implementation Considerations](#12-implementation-considerations)
- [13. Appendices](#13-appendices)

## 1. Executive Summary

This interface specification defines the complete FHIR R4-compliant data exchange protocol between GenPRES (treatment plan entry system) and hospital-wide EHR systems. The interface ensures G-Standard compliance for medication identification and dosage checking while enabling comprehensive treatment plan management through FHIR-based order scenarios with IHE Pharmacy profile compliance.

### Key Features

- **FHIR R4 Compliance**: Full adherence to FHIR R4 standard with proper resource usage
- **IHE Pharmacy Integration**: Compliance with IHE Pharmacy profile for medication management
- **G-Standard Compliance**: Full adherence to Dutch medication standards with GPK-based product identification
- **Stateless GenPRES Operation**: EHR maintains all persistent data while GenPRES provides calculation services
- **Comprehensive Clinical Decision Support**: Multi-level validation with real-time safety checking
- **OAuth2/SMART-on-FHIR Security**: Industry-standard healthcare authentication and authorization

### Interface Benefits

- **Clinical Safety**: Multi-layered validation preventing medication errors
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
- Validation and safety checking procedures
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
- **Dose Rules**: G-Standard dose rules referenced through extensions
- **Product Identification**: Complete product information in Medication resources
- **Safety Validation**: G-Standard safety rules integrated with FHIR validation

### 3.4 Stateless GenPRES with FHIR Persistence

EHR maintains FHIR resources while GenPRES provides calculations:

- **Resource Sovereignty**: EHR controls all FHIR resource persistence
- **Calculation Services**: GenPRES provides read-only calculation and validation
- **Session Management**: Temporary calculation state in GenPRES, persistent data in EHR
- **FHIR Compliance**: All data exchanges through standard FHIR operations

## 4. FHIR Resource Model

### 4.1 Core Resource Structure

```fsharp
// Core FHIR Resources used in the interface
type TreatmentPlanFhirResources = {
    // Primary Resources
    Patient: Patient                        // FHIR Patient resource
    CarePlan: CarePlan                     // Treatment plan as CarePlan
    RequestGroup: RequestGroup             // Grouped medication requests
    MedicationRequests: MedicationRequest[] // Individual medication orders
    Medications: Medication[]              // Medication definitions with G-Standard
    
    // Supporting Clinical Resources
    Encounter: Encounter option            // Current encounter context
    Conditions: Condition[]                // Relevant diagnoses
    AllergyIntolerances: AllergyIntolerance[] // Known allergies
    Observations: Observation[]            // Labs, vitals, measurements
    Goals: Goal[]                          // Treatment goals
    
    // Practitioner and Organization
    Practitioners: Practitioner[]          // Involved healthcare providers
    PractitionerRoles: PractitionerRole[]  // Provider roles and specialties
    Organizations: Organization[]          // Healthcare organizations
    
    // Nutrition and Monitoring
    NutritionOrders: NutritionOrder[]      // Nutritional therapy orders
    ServiceRequests: ServiceRequest[]      // Monitoring and lab requests
    
    // Safety and Compliance
    DetectedIssues: DetectedIssue[]        // Clinical safety issues
    RiskAssessments: RiskAssessment[]      // Clinical risk assessments
    
    // Audit and Provenance
    Provenances: Provenance[]              // Resource provenance tracking
    AuditEvents: AuditEvent[]              // IHE ATNA compliance
}
```

### 4.2 G-Standard Extensions

```fsharp
// FHIR Extensions for G-Standard integration
type GStandardExtensions = {
    // Medication Extensions
    GPKCode: Extension                     // G-Standard GPK code
    GStandardProductInfo: Extension        // Complete G-Standard product data
    DoseRuleReference: Extension           // Reference to applicable dose rules
    SafetyRuleReference: Extension         // Reference to safety rules
    
    // Dosing Extensions  
    WeightBasedDosing: Extension           // Weight-based dose calculation
    BSABasedDosing: Extension             // BSA-based dose calculation
    RenalAdjustment: Extension            // Renal dose adjustment info
    AgeBasedDosing: Extension             // Age-specific dosing rules
    
    // Safety Extensions
    ContraindicationOverride: Extension    // Approved contraindication overrides
    InteractionAssessment: Extension       // Drug interaction assessment
    AllergyRiskAssessment: Extension      // Allergy risk evaluation
    
    // Quality Extensions
    CalculationConfidence: Extension       // Confidence in dose calculations
    ValidationStatus: Extension            // G-Standard validation status
    ComplianceLevel: Extension            // Overall G-Standard compliance
}
```

### 4.3 Dutch Healthcare Profiles

```fsharp
// Dutch healthcare specific profiles and extensions
type DutchHealthcareProfiles = {
    // Patient Profile Extensions
    BSN: Extension                         // Dutch BSN (social security number)
    DutchAddressing: Extension            // Dutch address formatting
    InsuranceInformation: Extension       // Dutch health insurance data
    
    // Medication Profile Extensions
    GStandardCoding: Extension            // G-Standard coding system
    ThesaurusClassification: Extension    // Dutch medication thesaurus
    ReimbursementInfo: Extension          // Medication reimbursement data
    
    // Clinical Profile Extensions
    DutchClinicalCoding: Extension        // Dutch clinical coding systems
    QualificationRegistry: Extension      // BIG register practitioner info
    InstitutionRegistry: Extension        // Dutch healthcare institution registry
}
```

## 5. Data Exchange Specifications

### 5.1 Patient Context Transfer (EHR → GenPRES)

**FHIR Operation**: `POST /fhir/$bootstrap`  
**Bundle Type**: `collection`  
**Content-Type**: `application/fhir+json`

```json
{
  "resourceType": "Bundle",
  "type": "collection",
  "entry": [
    {
      "resource": {
        "resourceType": "Patient",
        "id": "pat-123",
        "identifier": [
          {
            "system": "http://fhir.nl/fhir/NamingSystem/bsn",
            "value": "123456789"
          }
        ],
        "birthDate": "2010-05-15",
        "extension": [
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/gestational-age",
            "valueQuantity": {
              "value": 40,
              "unit": "weeks",
              "system": "http://unitsofmeasure.org",
              "code": "wk"
            }
          }
        ]
      }
    },
    {
      "resource": {
        "resourceType": "Observation",
        "status": "final",
        "category": [
          {
            "coding": [
              {
                "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                "code": "vital-signs"
              }
            ]
          }
        ],
        "code": {
          "coding": [
            {
              "system": "http://loinc.org",
              "code": "29463-7",
              "display": "Body weight"
            }
          ]
        },
        "subject": {
          "reference": "Patient/pat-123"
        },
        "valueQuantity": {
          "value": 14.2,
          "unit": "kg",
          "system": "http://unitsofmeasure.org",
          "code": "kg"
        },
        "extension": [
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/weight-percentile",
            "valueQuantity": {
              "value": 50,
              "unit": "%",
              "system": "http://unitsofmeasure.org",
              "code": "%"
            }
          }
        ]
      }
    },
    {
      "resource": {
        "resourceType": "AllergyIntolerance",
        "patient": {
          "reference": "Patient/pat-123"
        },
        "code": {
          "coding": [
            {
              "system": "http://gstandard.nl/fhir/CodeSystem/gpk",
              "code": "12345",
              "display": "Penicillin"
            }
          ]
        },
        "reaction": [
          {
            "manifestation": [
              {
                "coding": [
                  {
                    "system": "http://snomed.info/sct",
                    "code": "271807003",
                    "display": "Rash"
                  }
                ]
              }
            ],
            "severity": "moderate"
          }
        ]
      }
    }
  ]
}
```

### 5.2 Treatment Plan Request (EHR → GenPRES)

**FHIR Operation**: `POST /fhir/$treatment-plan-request`  
**Parameters**: FHIR Parameters resource with CarePlan intent

```json
{
  "resourceType": "Parameters",
  "parameter": [
    {
      "name": "careplan-intent",
      "resource": {
        "resourceType": "CarePlan",
        "status": "draft",
        "intent": "plan",
        "subject": {
          "reference": "Patient/pat-123"
        },
        "goal": [
          {
            "reference": "Goal/pain-management"
          }
        ],
        "activity": [
          {
            "detail": {
              "kind": "MedicationRequest",
              "code": {
                "coding": [
                  {
                    "system": "http://gstandard.nl/fhir/CodeSystem/generic",
                    "code": "PARACETAMOL",
                    "display": "Paracetamol"
                  }
                ]
              },
              "status": "not-started",
              "description": "Pain management with paracetamol"
            }
          }
        ],
        "extension": [
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/treatment-priority",
            "valueCode": "routine"
          },
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/clinical-indication",
            "valueString": "Post-operative pain management"
          }
        ]
      }
    },
    {
      "name": "prescriber-context",
      "resource": {
        "resourceType": "PractitionerRole",
        "practitioner": {
          "reference": "Practitioner/dr-smith"
        },
        "organization": {
          "reference": "Organization/hospital-amc"
        },
        "code": [
          {
            "coding": [
              {
                "system": "http://snomed.info/sct",
                "code": "158965000",
                "display": "Medical practitioner"
              }
            ]
          }
        ],
        "specialty": [
          {
            "coding": [
              {
                "system": "http://snomed.info/sct",
                "code": "394537008",
                "display": "Pediatric specialty"
              }
            ]
          }
        ]
      }
    }
  ]
}
```

### 5.3 Complete Treatment Plan Response (GenPRES → EHR)

**FHIR Operation Response**: Bundle with transaction type  
**Bundle Type**: `transaction`  
**Content-Type**: `application/fhir+json`

```json
{
  "resourceType": "Bundle",
  "type": "transaction",
  "entry": [
    {
      "fullUrl": "urn:uuid:careplan-001",
      "resource": {
        "resourceType": "CarePlan",
        "status": "draft",
        "intent": "plan",
        "subject": {
          "reference": "Patient/pat-123"
        },
        "activity": [
          {
            "reference": {
              "reference": "urn:uuid:medreq-001"
            }
          }
        ],
        "extension": [
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/calculation-timestamp",
            "valueDateTime": "2026-01-15T10:30:00Z"
          },
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/gstandard-compliance",
            "extension": [
              {
                "url": "level",
                "valueCode": "full-compliance"
              },
              {
                "url": "validation-timestamp",
                "valueDateTime": "2026-01-15T10:30:05Z"
              }
            ]
          }
        ]
      },
      "request": {
        "method": "POST",
        "url": "CarePlan"
      }
    },
    {
      "fullUrl": "urn:uuid:medreq-001",
      "resource": {
        "resourceType": "MedicationRequest",
        "status": "draft",
        "intent": "plan",
        "medicationReference": {
          "reference": "urn:uuid:med-001"
        },
        "subject": {
          "reference": "Patient/pat-123"
        },
        "dosageInstruction": [
          {
            "text": "10 mg/kg every 6 hours orally",
            "route": {
              "coding": [
                {
                  "system": "http://snomed.info/sct",
                  "code": "26643006",
                  "display": "Oral route"
                }
              ]
            },
            "doseAndRate": [
              {
                "doseQuantity": {
                  "value": 142,
                  "unit": "mg",
                  "system": "http://unitsofmeasure.org",
                  "code": "mg"
                }
              }
            ]
          }
        ],
        "extension": [
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/weight-based-dose",
            "extension": [
              {
                "url": "dose-per-kg",
                "valueQuantity": {
                  "value": 10,
                  "unit": "mg/kg",
                  "system": "http://unitsofmeasure.org",
                  "code": "mg/kg"
                }
              },
              {
                "url": "patient-weight",
                "valueQuantity": {
                  "value": 14.2,
                  "unit": "kg",
                  "system": "http://unitsofmeasure.org",
                  "code": "kg"
                }
              }
            ]
          },
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule",
            "valueString": "PARA-PEDS-001"
          }
        ]
      },
      "request": {
        "method": "POST",
        "url": "MedicationRequest"
      }
    },
    {
      "fullUrl": "urn:uuid:med-001",
      "resource": {
        "resourceType": "Medication",
        "code": {
          "coding": [
            {
              "system": "http://gstandard.nl/fhir/CodeSystem/gpk",
              "code": "12345",
              "display": "Paracetamol tablet 500mg"
            },
            {
              "system": "http://gstandard.nl/fhir/CodeSystem/generic",
              "code": "PARACETAMOL",
              "display": "Paracetamol"
            }
          ]
        },
        "form": {
          "coding": [
            {
              "system": "http://gstandard.nl/fhir/CodeSystem/pharmaceutical-form",
              "code": "TAB",
              "display": "Tablet"
            }
          ]
        },
        "ingredient": [
          {
            "itemCodeableConcept": {
              "coding": [
                {
                  "system": "http://gstandard.nl/fhir/CodeSystem/substance",
                  "code": "PARACETAMOL",
                  "display": "Paracetamol"
                }
              ]
            },
            "strength": {
              "numerator": {
                "value": 500,
                "unit": "mg",
                "system": "http://unitsofmeasure.org",
                "code": "mg"
              },
              "denominator": {
                "value": 1,
                "unit": "tablet",
                "system": "http://unitsofmeasure.org",
                "code": "1"
              }
            }
          }
        ],
        "extension": [
          {
            "url": "http://genpres.nl/fhir/StructureDefinition/gstandard-product-info",
            "extension": [
              {
                "url": "gpk-code",
                "valueString": "12345"
              },
              {
                "url": "manufacturer",
                "valueString": "Generic Pharma BV"
              },
              {
                "url": "package-size",
                "valueString": "100 tablets"
              }
            ]
          }
        ]
      },
      "request": {
        "method": "POST",
        "url": "Medication"
      }
    },
    {
      "fullUrl": "urn:uuid:detected-issue-001",
      "resource": {
        "resourceType": "DetectedIssue",
        "status": "preliminary",
        "category": {
          "coding": [
            {
              "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
              "code": "DRG",
              "display": "Drug interaction"
            }
          ]
        },
        "severity": "moderate",
        "patient": {
          "reference": "Patient/pat-123"
        },
        "detail": "Potential drug interaction between paracetamol and existing medication",
        "mitigation": [
          {
            "action": {
              "coding": [
                {
                  "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                  "code": "MONITOR",
                  "display": "Monitor"
                }
              ]
            },
            "description": "Monitor patient for signs of liver toxicity"
          }
        ]
      },
      "request": {
        "method": "POST",
        "url": "DetectedIssue"
      }
    },
    {
      "fullUrl": "urn:uuid:provenance-001",
      "resource": {
        "resourceType": "Provenance",
        "target": [
          {
            "reference": "urn:uuid:careplan-001"
          },
          {
            "reference": "urn:uuid:medreq-001"
          }
        ],
        "recorded": "2026-01-15T10:30:00Z",
        "agent": [
          {
            "type": {
              "coding": [
                {
                  "system": "http://terminology.hl7.org/CodeSystem/provenance-participant-type",
                  "code": "performer"
                }
              ]
            },
            "who": {
              "display": "GenPRES Calculation Engine v2.1"
            }
          }
        ],
        "activity": {
          "coding": [
            {
              "system": "http://terminology.hl7.org/CodeSystem/v3-DataOperation",
              "code": "CREATE"
            }
          ]
        }
      },
      "request": {
        "method": "POST",
        "url": "Provenance"
      }
    },
    {
      "fullUrl": "urn:uuid:audit-event-001",
      "resource": {
        "resourceType": "AuditEvent",
        "type": {
          "system": "http://terminology.hl7.org/CodeSystem/audit-event-type",
          "code": "rest"
        },
        "action": "C",
        "recorded": "2026-01-15T10:30:00Z",
        "outcome": "0",
        "agent": [
          {
            "type": {
              "coding": [
                {
                  "system": "http://terminology.hl7.org/CodeSystem/extra-security-role-type",
                  "code": "humanuser"
                }
              ]
            },
            "who": {
              "reference": "Practitioner/dr-smith"
            },
            "requestor": true
          },
          {
            "type": {
              "coding": [
                {
                  "system": "http://terminology.hl7.org/CodeSystem/extra-security-role-type",
                  "code": "application"
                }
              ]
            },
            "who": {
              "display": "GenPRES System"
            },
            "requestor": false
          }
        ],
        "source": {
          "observer": {
            "display": "GenPRES Treatment Plan Service"
          },
          "type": [
            {
              "system": "http://terminology.hl7.org/CodeSystem/security-source-type",
              "code": "4"
            }
          ]
        },
        "entity": [
          {
            "what": {
              "reference": "Patient/pat-123"
            },
            "type": {
              "system": "http://terminology.hl7.org/CodeSystem/audit-entity-type",
              "code": "1"
            },
            "role": {
              "system": "http://terminology.hl7.org/CodeSystem/object-role",
              "code": "1"
            }
          }
        ]
      },
      "request": {
        "method": "POST",
        "url": "AuditEvent"
      }
    }
  ]
}
```

## 6. G-Standard Integration

### 6.1 G-Standard FHIR Coding Systems

```fsharp
// G-Standard specific FHIR coding systems
type GStandardFhirCodeSystems = {
    // Product Identification
    GPKCodeSystem: string = "http://gstandard.nl/fhir/CodeSystem/gpk"
    GenericCodeSystem: string = "http://gstandard.nl/fhir/CodeSystem/generic"
    ProductNameSystem: string = "http://gstandard.nl/fhir/CodeSystem/product-name"
    
    // Pharmaceutical Forms and Routes
    PharmaceuticalFormSystem: string = "http://gstandard.nl/fhir/CodeSystem/pharmaceutical-form"
    RouteSystem: string = "http://gstandard.nl/fhir/CodeSystem/route"
    
    // Substances and Ingredients
    SubstanceSystem: string = "http://gstandard.nl/fhir/CodeSystem/substance"
    IngredientSystem: string = "http://gstandard.nl/fhir/CodeSystem/ingredient"
    
    // Dose Rules and Safety
    DoseRuleSystem: string = "http://gstandard.nl/fhir/CodeSystem/dose-rule"
    SafetyRuleSystem: string = "http://gstandard.nl/fhir/CodeSystem/safety-rule"
    InteractionSystem: string = "http://gstandard.nl/fhir/CodeSystem/interaction"
    
    // Clinical Classification
    ATCSystem: string = "http://www.whocc.no/atc"
    ThesaurusSystem: string = "http://gstandard.nl/fhir/CodeSystem/thesaurus"
}
```

### 6.2 G-Standard Extensions Definition

```fsharp
// FHIR Structure Definitions for G-Standard extensions
type GStandardStructureDefinitions = {
    // Base Extension URL
    BaseExtensionUrl: string = "http://genpres.nl/fhir/StructureDefinition/"
    
    // Product Extensions
    GStandardProductInfo: StructureDefinition
    GPKIdentifier: StructureDefinition
    ProductAvailability: StructureDefinition
    ManufacturerInfo: StructureDefinition
    
    // Dosing Extensions
    WeightBasedDosing: StructureDefinition
    BSABasedDosing: StructureDefinition
    AgeBasedDosing: StructureDefinition
    RenalAdjustment: StructureDefinition
    DoseRuleReference: StructureDefinition
    
    // Safety Extensions
    ContraindicationOverride: StructureDefinition
    InteractionAssessment: StructureDefinition
    AllergyRiskAssessment: StructureDefinition
    SafetyRuleReference: StructureDefinition
    
    // Quality Extensions
    CalculationConfidence: StructureDefinition
    ValidationStatus: StructureDefinition
    ComplianceLevel: StructureDefinition
    GStandardVersion: StructureDefinition
}
```

### 6.3 G-Standard Validation Integration

```fsh

// G-Standard validation through FHIR OperationDefinition
OperationDefinition: validate-gstandard-compliance
- name: validate-gstandard-compliance
- status: active
- kind: operation
- code: validate-gstandard-compliance
- resource: [CarePlan, MedicationRequest, Medication]
- system: false
- type: true
- instance: true
- parameter:
  - name: resource
    use: in
    min: 1
    max: 1
    type: Resource
  - name: validation-level
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: validation-result
    use: out
    min: 1
    max: 1
    type: OperationOutcome
  - name: compliance-report
    use: out
    min: 0
    max: 1
    type: Parameters
```

## 7. Session Management and Workflow

### 7.1 FHIR-Based Session Management

```fsharp
// Session management using FHIR Communication resources
type FhirSessionManagement = {
    // Session Resource
    SessionCommunication: Communication    // Session state and metadata
    SessionTask: Task                     // Session workflow management
    SessionList: List                     // Session resources collection
    
    // Session Extensions
    SessionId: Extension                  // Unique session identifier
    SessionStatus: Extension              // Current session status
    SessionTimeout: Extension             // Session expiration time
    SessionMetrics: Extension             // Performance metrics
}

// Session Communication resource structure
let createSessionCommunication (sessionId: string) (patientRef: Reference) =
    {
        ResourceType = "Communication"
        Status = "in-progress"
        Subject = Some patientRef
        Identifier = [
            {
                System = Some "http://genpres.nl/fhir/NamingSystem/session-id"
                Value = Some sessionId
            }
        ]
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/communication-category"
                        Code = Some "treatment-planning-session"
                        Display = Some "Treatment Planning Session"
                    }
                ]
            }
        ]
        Extension = [
            {
                Url = "http://genpres.nl/fhir/StructureDefinition/session-timeout"
                ValueDateTime = Some (DateTime.UtcNow.AddHours(2.0))
            }
            {
                Url = "http://genpres.nl/fhir/StructureDefinition/session-performance"
                Extension = [
                    {
                        Url = "calculation-time"
                        ValueDuration = Some { Value = Some 2.5; Unit = Some "s" }
                    }
                    {
                        Url = "validation-time"
                        ValueDuration = Some { Value = Some 1.2; Unit = Some "s" }
                    }
                ]
            }
        ]
    }
```

### 7.2 Treatment Plan Workflow States

```fsharp
// FHIR Task-based workflow management
type TreatmentPlanWorkflow = {
    // Workflow Tasks
    PlanningTask: Task                    // Overall planning task
    CalculationTasks: Task[]              // Individual calculation tasks
    ValidationTasks: Task[]               // Validation tasks
    ApprovalTasks: Task[]                 // Clinical approval tasks
    
    // Workflow Status Management
    WorkflowStatus: TaskStatus            // Current workflow status
    WorkflowProgress: Extension           // Progress tracking
    WorkflowMetrics: Extension            // Workflow performance metrics
}

// Task status progression for treatment planning
type TreatmentPlanTaskStatus =
    | Draft                               // Initial planning state
    | Requested                           // Planning requested
    | Received                            // Request received by GenPRES
    | Accepted                            // Planning accepted
    | InProgress                          // Calculation in progress
    | OnHold                              // Waiting for clinical input
    | Ready                               // Ready for review
    | Completed                           // Planning completed
    | Cancelled                           // Planning cancelled
    | Failed                              // Planning failed

// FHIR Task resource for treatment plan workflow
let createPlanningTask (sessionId: string) (patientRef: Reference) =
    {
        ResourceType = "Task"
        Status = TaskStatus.Requested
        Intent = TaskIntent.Plan
        Code = Some {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/task-type"
                    Code = Some "treatment-plan-calculation"
                    Display = Some "Treatment Plan Calculation"
                }
            ]
        }
        For = Some patientRef
        AuthoredOn = Some DateTime.UtcNow
        Owner = Some {
            Display = Some "GenPRES Calculation Service"
        }
        Extension = [
            {
                Url = "http://genpres.nl/fhir/StructureDefinition/session-reference"
                ValueString = Some sessionId
            }
            {
                Url = "http://genpres.nl/fhir/StructureDefinition/calculation-priority"
                ValueCode = Some "routine"
            }
        ]
    }
```

### 7.3 Session Event Management

```fsh

// FHIR Subscription for real-time session events
Subscription: treatment-plan-events
- status: active
- reason: "Real-time treatment planning events"
- criteria: "Communication?category=treatment-planning-session"
- channel:
  - type: websocket
  - endpoint: wss://api.genpres.nl/fhir/ws
  - payload: application/fhir+json
  - header: ["Authorization: Bearer {token}"]

// Event notification structure
AuditEvent: session-event
- type: 
  - system: http://terminology.hl7.org/CodeSystem/audit-event-type
  - code: rest
- action: U (Update)
- recorded: {timestamp}
- agent:
  - type:
    - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
    - code: application
  - who:
    - display: "GenPRES Treatment Planning Service"
- entity:
  - what:
    - reference: "Communication/{session-id}"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: 2 (System Object)
```

## 8. Integration Architecture

### 8.1 FHIR API Endpoints

```yaml
## FHIR R4 compliant endpoints with IHE Pharmacy profile
paths:
  # Patient Context Bootstrap
  /fhir/$bootstrap:
    post:
      summary: Initialize patient context for treatment planning
      operationId: bootstrap
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            example:
              resourceType: Bundle
              type: collection
              entry: [
                # Patient, Observations, AllergyIntolerances, etc.
              ]
      responses:
        '200':
          description: Context successfully established
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'
              examples:
                bootstrap-success:
                  summary: Successful bootstrap
                  value:
                    resourceType: OperationOutcome
                    issue:
                      - severity: information
                        code: informational
                        details:
                          text: "Patient context successfully established"
        '400':
          description: Invalid request
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'
        '401':
          $ref: '#/components/responses/Unauthorized'
        '403':
          $ref: '#/components/responses/Forbidden'
        '422':
          description: Validation failed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  /$treatment-plan-request:
    post:
      tags: [Treatment Planning]
      summary: Request treatment plan calculation
      description: |
        Requests calculation of treatment plan based on clinical intent.
        Returns calculated CarePlan with MedicationRequests and validation results.
      operationId: treatmentPlanRequest
      security:
        - OAuth2: [patient/*.write, user/*.read]
      parameters:
        - name: X-Session-ID
          in: header
          description: Treatment planning session identifier
          required: false
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
            examples:
              treatment-request:
                summary: Treatment plan request with clinical intent
                value:
                  resourceType: Parameters
                  parameter:
                    - name: careplan-intent
                      resource:
                        resourceType: CarePlan
                        status: draft
                        intent: plan
                        subject:
                          reference: Patient/pat-123
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'
              examples:
                calculated-plan:
                  summary: Calculated treatment plan
                  value:
                    resourceType: Bundle
                    type: transaction
                    entry:
                      - fullUrl: urn:uuid:careplan-001
                        resource:
                          resourceType: CarePlan
                          status: draft
                          intent: plan
        '422':
          description: Clinical validation failed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  /$finalize:
    post:
      tags: [Treatment Planning]
      summary: Finalize treatment plan for implementation
      description: |
        Finalizes the treatment plan for implementation in the EHR.
        Returns transaction Bundle ready for EHR processing.
      operationId: finalize
      security:
        - OAuth2: [patient/*.write, user/*.write]
      parameters:
        - name: X-Session-ID
          in: header
          description: Treatment planning session identifier
          required: true
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  /$simulate:
    post:
      tags: [Treatment Planning]
      summary: Simulate treatment plan without persistence
      description: |
        Dry-run simulation of treatment plan calculation without creating
        persistent resources. Useful for testing and validation.
      operationId: simulate
      security:
        - OAuth2: [patient/*.read]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  /Medication/$validate-gstandard:
    post:
      tags: [G-Standard Validation]
      summary: Validate medication against G-Standard
      description: |
        Validates medication resources against G-Standard database
        and returns compliance information.
      operationId: validateGStandard
      security:
        - OAuth2: [patient/*.read, system/*.read]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

components:
  securitySchemes:
    OAuth2:
      type: oauth2
      description: OAuth2 with SMART-on-FHIR scopes
      flows:
        authorizationCode:
          authorizationUrl: https://auth.genpres.nl/oauth2/authorize
          tokenUrl: https://auth.genpres.nl/oauth2/token
          scopes:
            patient/*.read: Read patient data
            patient/*.write: Write patient data
            user/*.read: Read user data
            user/*.write: Write user data
            launch: Launch context
            launch/patient: Patient launch context
            fhirUser: FHIR user identity
            openid: OpenID Connect
            profile: User profile access

    BearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT

  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        id:
          type: string
          pattern: '^[A-Za-z0-9\-\.]{1,64}
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Treatment Plan Request
  /fhir/$treatment-plan-request:
    post:
      summary: Request treatment plan calculation
      operationId: treatmentPlanRequest
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # Treatment Plan Finalization
  /fhir/$finalize:
    post:
      summary: Finalize treatment plan for EHR implementation
      operationId: finalize
      parameters:
        - name: session-id
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # G-Standard Validation
  /fhir/Medication/$validate-gstandard:
    post:
      summary: Validate medication against G-Standard
      operationId: validateGStandard
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Simulation (Dry Run)
  /fhir/$simulate:
    post:
      summary: Simulate treatment plan without persistence
      operationId: simulate
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

components:
  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        type:
          type: string
          enum: [collection, transaction, searchset, batch]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'
    
    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'
```

### 8.2 OAuth2/SMART-on-FHIR Security Integration

```fsharp
// SMART-on-FHIR security context
type SmartOnFhirSecurity = {
    // OAuth2 Configuration
    AuthorizationServer: string           // EHR authorization server
    TokenEndpoint: string                 // OAuth2 token endpoint
    IntrospectionEndpoint: string         // Token introspection endpoint
    
    // SMART Scopes
    RequiredScopes: SmartScope[]          // Required SMART scopes
    OptionalScopes: SmartScope[]          // Optional enhanced scopes
    
    // Token Management
    AccessToken: AccessToken              // Current access token
    RefreshToken: RefreshToken option     // Refresh token if available
    TokenExpiration: DateTime             // Token expiration time
    
    // FHIR Security
    FhirSecurityLabels: SecurityLabel[]   // FHIR security labels
    AuditRequirements: AuditRequirement[] // IHE ATNA compliance
}

// SMART-on-FHIR scopes for GenPRES integration
type SmartScope =
    | PatientRead                         // patient/*.read
    | PatientWrite                        // patient/*.write
    | UserRead                            // user/*.read
    | UserWrite                           // user/*.write
    | SystemRead                          // system/*.read
    | SystemWrite                         // system/*.write
    | Launch                              // launch
    | LaunchPatient                       // launch/patient
    | FhirUser                            // fhirUser
    | OpenIdProfile                       // profile openid

// OAuth2 token validation for FHIR requests
let validateFhirToken (token: string) : Async<Result<FhirSecurityContext, SecurityError>> =
    async {
        try
            // Introspect token with EHR authorization server
            let! introspectionResult = introspectToken token
            
            match introspectionResult with
            | Error error -> return Error error
            | Ok tokenInfo ->
                
                // Validate required scopes
                let requiredScopes = [
                    "patient/Patient.read"
                    "patient/CarePlan.write" 
                    "patient/MedicationRequest.write"
                    "patient/Medication.read"
                    "user/Practitioner.read"
                ]
                
                let hasRequiredScopes = 
                    requiredScopes 
                    |> List.forall (fun scope -> tokenInfo.Scopes |> List.contains scope)
                
                if not hasRequiredScopes then
                    return Error (InsufficientScopes requiredScopes)
                else
                    let securityContext = {
                        UserId = tokenInfo.Sub
                        PatientId = tokenInfo.Patient
                        Scopes = tokenInfo.Scopes
                        FhirUser = tokenInfo.FhirUser
                        ExpiresAt = tokenInfo.ExpiresAt
                    }
                    return Ok securityContext
        with
        | ex -> return Error (TokenValidationError ex.Message)
    }
```

### 8.3 IHE Profile Compliance

```fsh

// IHE Pharmacy (PHARM) Profile Compliance
Profile: IHE-PHARM-CarePlan
Parent: CarePlan
Title: "IHE Pharmacy CarePlan Profile"
Description: "CarePlan profile compliant with IHE Pharmacy workflow"
- status 1..1 MS
- intent 1..1 MS
- subject 1..1 MS
- activity
  - detail
    - kind 1..1 MS
    - code 1..1 MS
    - status 1..1 MS
    - medication[x] 0..1 MS

// IHE Audit Trail and Node Authentication (ATNA) Compliance
Profile: IHE-ATNA-AuditEvent
Parent: AuditEvent
Title: "IHE ATNA AuditEvent Profile"
Description: "AuditEvent profile for IHE ATNA compliance"
- type 1..1 MS
- action 1..1 MS
- recorded 1..1 MS
- outcome 1..1 MS
- agent 1..* MS
  - type 1..1 MS
  - who 1..1 MS
  - requestor 1..1 MS
- source 1..1 MS
  - observer 1..1 MS
- entity 0..* MS
  - what 0..1 MS
  - type 0..1 MS
  - role 0..1 MS

// IHE Internet User Authorization (IUA) Profile
Profile: IHE-IUA-AccessToken
Title: "IHE IUA Access Token Profile" 
Description: "OAuth2 access token profile for IHE IUA compliance"
- iss (issuer) 1..1 MS
- sub (subject) 1..1 MS  
- aud (audience) 1..1 MS
- exp (expiration) 1..1 MS
- iat (issued at) 1..1 MS
- scope 1..1 MS
- purpose_of_use 0..1 MS
- homeCommunityId 0..1 MS
- national_provider_identifier 0..1 MS
- subject_organization 0..1 MS
- subject_organization_id 0..1 MS
```

## 9. Clinical Safety and Compliance

### 9.1 FHIR-Based Clinical Decision Support

```fsh

// Clinical Decision Support through FHIR CDS Hooks
CDS Hook: medication-prescribe
- hook: medication-prescribe  
- title: "GenPRES Medication Safety Check"
- description: "G-Standard compliant medication safety validation"
- id: "genpres-medication-safety"
- prefetch:
    patient: "Patient/{{context.patientId}}"
    medications: "MedicationRequest?patient={{context.patientId}}&status=active"
    allergies: "AllergyIntolerance?patient={{context.patientId}}"
    conditions: "Condition?patient={{context.patientId}}&clinical-status=active"
    observations: "Observation?patient={{context.patientId}}&category=vital-signs&_sort=-date&_count=10"

// CDS Card response for safety alerts
CDSCard: safety-alert
- summary: "G-Standard safety alert"
- detail: "Detailed safety information from G-Standard validation"
- indicator: "warning" | "info" | "critical"
- source:
  - label: "G-Standard"
  - url: "http://gstandard.nl"
  - icon: "http://gstandard.nl/icon.png"
- suggestions:
  - label: "Alternative medication"
  - actions:
    - type: "create"
    - description: "Add alternative medication"
    - resource: MedicationRequest

// DetectedIssue resource for clinical alerts
DetectedIssue: clinical-safety-alert
- status: preliminary | final | entered-in-error
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActCode
    - code: DRG | ALLERGY | DOSEAMT | DOSEDUR | DOSECOND
- severity: high | moderate | low
- patient: Reference(Patient)
- detail: "Human readable description of the issue"
- reference: "Reference to related resources"
- mitigation:
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActCode  
      - code: MONITOR | REPLACE | REDUCE | DISCONTINUE
  - description: "Description of mitigation action"
```

### 9.2 G-Standard Safety Validation

```fsharp
// G-Standard safety validation through FHIR operations
type GStandardSafetyValidation = {
    // Validation Operations
    ProductValidation: FhirOperation       // Validate product GPK codes
    DoseRuleValidation: FhirOperation      // Validate dose rules compliance
    InteractionCheck: FhirOperation        // Check drug interactions
    ContraindicationCheck: FhirOperation   // Check contraindications
    
    // Safety Assessment
    RiskAssessment: RiskAssessment         // Overall safety risk assessment
    SafetyProfile: Extension               // Comprehensive safety profile
    
    // Compliance Reporting
    ComplianceReport: Parameters           // G-Standard compliance report
    ValidationOutcome: OperationOutcome    // Validation results
}

// FHIR OperationDefinition for comprehensive safety validation
OperationDefinition: comprehensive-safety-check
- name: comprehensive-safety-check
- status: active
- kind: operation
- code: comprehensive-safety-check
- resource: [CarePlan]
- system: false
- type: false  
- instance: true
- parameter:
  - name: gstandard-version
    use: in
    min: 0
    max: 1
    type: string
  - name: validation-level  
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: safety-report
    use: out
    min: 1
    max: 1 
    type: Parameters
  - name: detected-issues
    use: out
    min: 0
    max: "*"
    type: DetectedIssue
  - name: risk-assessment
    use: out  
    min: 0
    max: 1
    type: RiskAssessment

// RiskAssessment resource for overall safety evaluation  
RiskAssessment: treatment-plan-safety
- status: final
- subject: Reference(Patient)
- encounter: Reference(Encounter) 
- performedDateTime: {assessment-timestamp}
- performer: Reference(Device) # GenPRES system
- code:
  - coding:
    - system: http://snomed.info/sct
    - code: 225338004
    - display: "Risk assessment"
- prediction:
  - outcome:
    - coding:
      - system: http://snomed.info/sct  
      - code: 281647001
      - display: "Adverse reaction"
  - probabilityDecimal: 0.15
  - relativeRisk: 1.2
- mitigation: "Recommended monitoring and precautions"
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/gstandard-risk-factors
    extension:
      - url: age-risk
        valueDecimal: 0.1
      - url: weight-risk  
        valueDecimal: 0.05
      - url: interaction-risk
        valueDecimal: 0.3
```

### 9.3 Pediatric Safety Specializations

```fsh

// Pediatric-specific FHIR profiles and extensions
Profile: PediatricMedicationRequest
Parent: MedicationRequest
Title: "Pediatric Medication Request"
Description: "MedicationRequest profile with pediatric safety requirements"
- subject 1..1 MS
- subject only Reference(PediatricPatient)
- dosageInstruction 1..* MS
  - doseAndRate
    - dose[x] 1..1 MS
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
        extension:
          - url: dose-per-kg
            valueQuantity 1..1 MS
          - url: patient-weight  
            valueQuantity 1..1 MS
          - url: weight-source
            valueCode 1..1 MS # measured | estimated | calculated
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/pediatric-safety-check
    extension:
      - url: age-appropriate
        valueBoolean 1..1 MS
      - url: weight-appropriate
        valueBoolean 1..1 MS  
      - url: concentration-safe
        valueBoolean 1..1 MS
      - url: volume-appropriate
        valueBoolean 1..1 MS

// Age-specific contraindication checking
CodeSystem: pediatric-age-groups
- url: http://genpres.nl/fhir/CodeSystem/pediatric-age-groups
- concept:
  - code: neonate
    display: "Neonate (0-28 days)"
    definition: "Newborn infant up to 28 days of age"
  - code: infant  
    display: "Infant (29 days - 2 years)"
    definition: "Infant from 29 days to 2 years of age"
  - code: child
    display: "Child (2-12 years)" 
    definition: "Child from 2 to 12 years of age"
  - code: adolescent
    display: "Adolescent (12-18 years)"
    definition: "Adolescent from 12 to 18 years of age"

// Pediatric dose rule validation
Extension: pediatric-dose-rule
- url: http://genpres.nl/fhir/StructureDefinition/pediatric-dose-rule
- extension:
  - url: rule-id
    valueString 1..1 MS
  - url: age-group  
    valueCode 1..1 MS # From pediatric-age-groups CodeSystem
  - url: weight-range
    extension:
      - url: min-weight
        valueQuantity 0..1 MS
      - url: max-weight 
        valueQuantity 0..1 MS
  - url: dose-calculation
    extension:
      - url: dose-per-kg
        valueQuantity 0..1 MS
      - url: dose-per-m2
        valueQuantity 0..1 MS  
      - url: max-single-dose
        valueQuantity 0..1 MS
      - url: max-daily-dose
        valueQuantity 0..1 MS
  - url: safety-considerations
    valueString 0..* MS
```

## 10. Performance and Scalability

### 10.1 FHIR Performance Optimization

```fsharp
// FHIR-specific performance optimization strategies
type FhirPerformanceOptimization = {
    // Bundle Optimization
    BundleSize: BundleSizeStrategy         // Optimal bundle sizes
    BundleProcessing: BundleProcessingStrategy // Parallel vs sequential
    
    // Resource Optimization  
    ResourceMinimization: ResourceMinimizationStrategy // Include only necessary elements
    ReferenceOptimization: ReferenceStrategy // Contained vs external references
    
    // Search Optimization
    SearchParameterOptimization: SearchStrategy // Efficient FHIR searches
    IncludeRevIncludeOptimization: IncludeStrategy // Optimal _include/_revinclude usage
    
    // Caching Strategy
    ResourceCaching: CachingStrategy       // Cache frequently accessed resources
    BundleCaching: CachingStrategy         // Cache complete bundles
    OperationCaching: CachingStrategy      // Cache operation results
}

// Bundle size optimization for treatment plans
type BundleSizeStrategy = {
    MaxBundleSize: int                     // Maximum entries per bundle
    ResourceTypeGrouping: bool             // Group similar resources  
    DependencyOrdering: bool               // Order resources by dependencies
    ChunkingStrategy: ChunkingMethod       // How to split large bundles
}

type ChunkingMethod =
    | ByResourceCount of int               // Split by number of resources
    | ByResourceType                       // Split by resource type
    | ByDependencyGraph                    // Split maintaining dependencies
    | ByPayloadSize of int                 // Split by payload size in KB

// FHIR search optimization for patient context
let optimizedPatientContextQuery (patientId: string) =
    $"""
    Patient/{patientId}?
    _include=Patient:organization&
    _include=Patient:general-practitioner&
    _revinclude=Observation:patient&
    _revinclude=AllergyIntolerance:patient&
    _revinclude=Condition:patient&
    _revinclude=MedicationRequest:patient&
    _count=50&
    _sort=-_lastUpdated
    """

// Resource minimization through FHIR _elements parameter  
let minimizePatientResource = "_elements=id,identifier,birthDate,gender,extension"
let minimizeObservationResource = "_elements=id,status,code,subject,value,effective"
let minimizeMedicationRequestResource = "_elements=id,status,intent,medication,subject,dosageInstruction"
```

### 10.2 Scalability Architecture

```fsh

// FHIR server scalability configuration
CapabilityStatement: genpres-capability
- status: active
- date: "2026-01-15"
- publisher: "GenPRES"
- kind: instance
- software:
  - name: "GenPRES FHIR Server"
  - version: "2.1.0"
- implementation:
  - description: "GenPRES Treatment Planning FHIR Server"  
  - url: "https://api.genpres.nl/fhir"
- fhirVersion: 4.0.1
- format: [json, xml]
- rest:
  - mode: server
  - security:
    - cors: true
    - service:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/restful-security-service
        - code: OAuth
        - display: "OAuth2 using SMART-on-FHIR profile"
  - resource:
    - type: Patient
      interaction: [read, search-type]
      searchParam:
        - name: identifier
          type: token
        - name: birthdate  
          type: date
    - type: CarePlan
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      conditionalUpdate: true
      searchParam:
        - name: patient
          type: reference
        - name: status
          type: token
        - name: date
          type: date
    - type: MedicationRequest  
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      searchParam:
        - name: patient
          type: reference
        - name: medication
          type: reference  
        - name: status
          type: token
  - operation:
    - name: bootstrap
      definition: http://genpres.nl/fhir/OperationDefinition/bootstrap
    - name: treatment-plan-request
      definition: http://genpres.nl/fhir/OperationDefinition/treatment-plan-request
    - name: finalize
      definition: http://genpres.nl/fhir/OperationDefinition/finalize
    - name: validate-gstandard  
      definition: http://genpres.nl/fhir/OperationDefinition/validate-gstandard
```

### 10.3 Performance Monitoring

```fsharp
// FHIR-based performance monitoring
type FhirPerformanceMonitoring = {
    // Operation Performance
    OperationMetrics: OperationMetric[]    // Performance per FHIR operation
    ResourceMetrics: ResourceMetric[]      // Performance per resource type
    BundleMetrics: BundleMetric[]          // Bundle processing performance
    
    // Quality Metrics
    ValidationMetrics: ValidationMetric[]  // G-Standard validation performance
    SafetyMetrics: SafetyMetric[]          // Safety check performance
    
    // System Metrics  
    ServerMetrics: ServerMetric[]          // FHIR server performance
    DatabaseMetrics: DatabaseMetric[]     // Database performance
    CacheMetrics: CacheMetric[]           // Cache performance
}

// Performance metrics as FHIR Observation resources
let createPerformanceObservation (operationType: string) (duration: float) =
    {
        ResourceType = "Observation"
        Status = ObservationStatus.Final
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/observation-category"
                        Code = Some "performance-metric"
                        Display = Some "Performance Metric"
                    }
                ]
            }
        ]
        Code = {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                    Code = Some "operation-duration"  
                    Display = Some "Operation Duration"
                }
            ]
        }
        Component = [
            {
                Code = {
                    Coding = [
                        {
                            System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                            Code = Some "operation-type"
                            Display = Some "Operation Type"
                        }
                    ]
                }
                ValueString = Some operationType
            }
        ]
        ValueQuantity = Some {
            Value = Some duration
            Unit = Some "ms"
            System = Some "http://unitsofmeasure.org"
            Code = Some "ms"
        }
        EffectiveDateTime = Some DateTime.UtcNow
        Device = Some {
            Display = Some "GenPRES Performance Monitor"
        }
    }
```

## 11. Security and Privacy

### 11.1 OAuth2/SMART-on-FHIR Security

```fsh

// OAuth2 Authorization Server Metadata (RFC 8414)
OAuth2Metadata: genpres-oauth2
- issuer: "https://auth.genpres.nl"
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"  
- introspection_endpoint: "https://auth.genpres.nl/oauth2/introspect"
- jwks_uri: "https://auth.genpres.nl/.well-known/jwks.json"
- scopes_supported: [
    "patient/*.read",
    "patient/*.write", 
    "user/*.read",
    "user/*.write",
    "system/*.read",
    "system/*.write",
    "launch",
    "launch/patient",
    "fhirUser",
    "openid",
    "profile"
  ]
- response_types_supported: ["code", "token", "id_token"]
- grant_types_supported: ["authorization_code", "client_credentials", "refresh_token"]
- code_challenge_methods_supported: ["S256"]
- token_endpoint_auth_methods_supported: ["client_secret_basic", "client_secret_post", "private_key_jwt"]

// SMART-on-FHIR Well-Known Endpoint
SMARTConfiguration: smart-configuration  
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"
- capabilities: [
  "launch-ehr", 
  "launch-standalone",
  "client-public",
  "client-confidential-symmetric", 
  "sso-openid-connect",
  "context-passthrough-banner",
  "context-passthrough-style",
  "context-ehr-patient",
  "context-ehr-encounter",
  "permission-offline",
  "permission-patient",
  "permission-user"
]
```

### 11.2 FHIR Security Labels and Access Control

```fsh

// Security labels for FHIR resources
CodeSystem: security-labels
- url: http://genpres.nl/fhir/CodeSystem/security-label
- concept:
  - code: PEDS
    display: "Pediatric Data"
    definition: "Data specific to pediatric patients requiring special handling"
  - code: GSTAND
    display: "G-Standard Data" 
    definition: "Data validated against G-Standard requiring compliance tracking"
  - code: CALC
    display: "Calculated Data"
    definition: "Data generated by GenPRES calculations"
  - code: SENS
    display: "Sensitive Clinical Data"
    definition: "Highly sensitive clinical information requiring restricted access"

// Security labeling for CarePlan resources
CarePlan: secure-treatment-plan
- meta:
    security:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
        code: GSTAND
        display: "G-Standard Data"
      - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
        code: N
        display: "Normal"

// Consent resource for treatment plan data usage
Consent: treatment-plan-consent
- status: active
- scope:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentscope
    - code: treatment
    - display: "Treatment"
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentcategorycodes
    - code: medical
    - display: "Medical Consent"
- patient: Reference(Patient)
- dateTime: "2026-01-15T10:00:00Z"
- performer: [Reference(Patient)]
- policy:
  - authority: "https://genpres.nl/privacy-policy"
  - uri: "https://genpres.nl/consent/treatment-planning"
- provision:
  - type: permit
  - purpose:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
    - code: TREAT
    - display: "Treatment"
  - class:
    - system: http://hl7.org/fhir/resource-types
    - code: CarePlan
  - class:
    - system: http://hl7.org/fhir/resource-types  
    - code: MedicationRequest
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: collect
      - display: "Collect"
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: use
      - display: "Use"
```

### 11.3 Audit Trail and Compliance

```fsh

// IHE ATNA compliant audit events
AuditEvent: treatment-plan-access
- type:
  - system: http://dicom.nema.org/resources/ontology/DCM
  - code: "110112"
  - display: "Query"
- action: E # Execute/Perform
- recorded: "2026-01-15T10:30:00Z"
- outcome: "0" # Success
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: humanuser
      - display: "Human User"
  - who:
    - reference: "Practitioner/dr-smith"
    - display: "Dr. John Smith"
  - requestor: true
  - location: Reference(Location/icu-room-101)
  - policy: ["http://genpres.nl/policy/treatment-planning-access"]
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: application
      - display: "Application"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/application-id"
      - value: "genpres-treatment-planner"
    - display: "GenPRES Treatment Planner"
  - requestor: false
- source:
  - observer:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/system-id"  
      - value: "genpres-fhir-server"
    - display: "GenPRES FHIR Server"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/security-source-type
    - code: "4"
    - display: "Application Server"
- entity:
  - what:
    - reference: "Patient/pat-123"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: "1"
    - display: "Person"
  - role:
    - system: http://terminology.hl7.org/CodeSystem/object-role
    - code: "1" 
    - display: "Patient"
  - name: "Treatment plan calculation for pediatric patient"
  - securityLabel:
    - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
    - code: N
    - display: "Normal"

// Provenance tracking for calculated treatment plans
Provenance: treatment-plan-calculation
- target:
  - reference: "CarePlan/calculated-plan-001"
- target:
  - reference: "MedicationRequest/calculated-medreq-001"
- recorded: "2026-01-15T10:30:00Z"
- policy: ["http://genpres.nl/policy/calculation-provenance"]
- location: Reference(Location/genpres-datacenter)
- activity:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-DataOperation
    - code: CREATE
    - display: "Create"
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
      - code: performer
      - display: "Performer"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/calculation-engine"
      - value: "genpres-calc-engine-v2.1"
    - display: "GenPRES Calculation Engine v2.1"
  - onBehalfOf:
    - reference: "Practitioner/dr-smith"
- entity:
  - role: source
  - what:
    - reference: "Bundle/patient-context-bundle"
    - display: "Patient context bundle used for calculation"
  - agent:
    - type:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
        - code: enterer
        - display: "Enterer"  
    - who:
      - reference: "Device/ehr-system"
      - display: "Hospital EHR System"
- signature:
  - type:
    - system: urn:iso-astm:E1762-95:2013
    - code: 1.2.840.10065.1.12.1.1
    - display: "Author's Signature"
  - when: "2026-01-15T10:30:00Z"
  - who:
    - reference: "Device/genpres-system"
  - data: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." # Base64 encoded signature
```

## 12. Implementation Considerations

### 12.1 FHIR Implementation Guide

```fsh

// Implementation Guide definition
ImplementationGuide: genpres-treatment-planning
- url: "http://genpres.nl/fhir/ImplementationGuide/treatment-planning"
- version: "1.2.0" 
- name: "GenPRESTreatmentPlanning"
- title: "GenPRES Treatment Planning Implementation Guide"
- status: active
- publisher: "GenPRES"
- description: "FHIR R4 implementation guide for GenPRES treatment planning integration"
- packageId: "nl.genpres.treatmentplanning"
- license: CC0-1.0
- fhirVersion: [4.0.1]
- dependsOn:
  - uri: "http://hl7.org/fhir"
    version: "4.0.1"
  - uri: "http://hl7.org/fhir/uv/sdc"
    version: "3.0.0"
  - uri: "http://hl7.org/fhir/uv/cpg"  
    version: "2.0.0"
- definition:
  - resource:
    - reference:
      - reference: "StructureDefinition/genpres-patient"
      - name: "GenPRES Patient Profile"
      - description: "Patient profile with Dutch healthcare extensions"
    - reference:
      - reference: "StructureDefinition/genpres-careplan"
      - name: "GenPRES CarePlan Profile" 
      - description: "Treatment plan as FHIR CarePlan"
    - reference:
      - reference: "StructureDefinition/genpres-medicationrequest"
      - name: "GenPRES MedicationRequest Profile"
      - description: "Medication request with G-Standard compliance"
  - page:
    - nameUrl: "index.html"
    - title: "GenPRES Treatment Planning IG"
    - generation: html
    - page:
      - nameUrl: "profiles.html" 
      - title: "FHIR Profiles"
      - generation: html
    - page:
      - nameUrl: "extensions.html"
      - title: "FHIR Extensions"  
      - generation: html
    - page:
      - nameUrl: "terminology.html"
      - title: "Terminology"
      - generation: html
    - page:
      - nameUrl: "examples.html"
      - title: "Examples"
      - generation: html
```

### 12.2 Testing and Validation

```fsharp
// FHIR validation and testing framework
type FhirTestingFramework = {
    // Profile Validation
    ProfileValidation: ProfileValidationConfig // FHIR profile validation
    SchemaValidation: SchemaValidationConfig   // FHIR schema validation
    TerminologyValidation: TerminologyValidationConfig // Terminology validation
    
    // Integration Testing
    OperationTesting: OperationTestConfig      // FHIR operation testing
    WorkflowTesting: WorkflowTestConfig        // End-to-end workflow testing
    PerformanceTesting: PerformanceTestConfig  // Performance testing
    
    // Compliance Testing
    G-StandardTesting: GStandardTestConfig     // G-Standard compliance testing
    SecurityTesting: SecurityTestConfig        // OAuth2/SMART security testing
    IHETesting: IHETestConfig                  // IHE profile compliance testing
}

// FHIR TestScript resources for automated testing
TestScript: bootstrap-operation-test
- url: "http://genpres.nl/fhir/TestScript/bootstrap-operation-test"
- version: "1.0.0"
- name: "BootstrapOperationTest"
- status: active
- title: "Test bootstrap operation with patient context"
- description: "Validates the $bootstrap operation with complete patient context"
- origin:
  - index: 1
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-origin-types"
    - code: "FHIR-Client"
- destination:
  - index: 1  
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-destination-types"
    - code: "FHIR-Server"
- fixture:
  - id: "patient-context-bundle"
  - autocreate: false
  - autodelete: false
  - resource:
    - reference: "Bundle/patient-context-example"
- variable:
  - name: "patientId"
  - defaultValue: "pat-123"
  - sourceId: "patient-context-bundle"
  - expression: "Bundle.entry.where(resource is Patient).resource.id"
- test:
  - id: "bootstrap-success"
  - name: "Bootstrap Operation Success"
  - description: "Test successful bootstrap operation"
  - action:
    - operation:
      - type:
        - system: "http://terminology.hl7.org/CodeSystem/testscript-operation-codes"
        - code: "create"
      - resource: "Bundle"
      - description: "Execute bootstrap operation"  
      - destination: 1
      - origin: 1
      - params: "/$bootstrap"
      - sourceId: "patient-context-bundle"
    - assert:
      - description: "Confirm that the returned HTTP status is 200(OK)"
      - direction: response
      - responseCode: "200"
    - assert:
      - description: "Confirm that the response is an OperationOutcome"
      - direction: response
      - resource: "OperationOutcome"
    - assert:
      - description: "Confirm successful outcome"
      - direction: response
      - expression: "OperationOutcome.issue.where(severity='information').exists()"

// Example test data for FHIR testing
Bundle: patient-context-test-data
- resourceType: Bundle
- type: collection
- entry:
  - resource:
    - resourceType: Patient
    - id: test-patient-001
    - identifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
      - value: "999999999"
    - birthDate: "2015-03-15"
    - gender: male
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
        valueQuantity:
          value: 39
          unit: weeks
          system: http://unitsofmeasure.org
          code: wk
  - resource:
    - resourceType: Observation
    - status: final
    - category:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/observation-category
        - code: vital-signs
    - code:
      - coding:
        - system: http://loinc.org
        - code: "29463-7"
        - display: "Body weight"
    - subject:
      - reference: "Patient/test-patient-001"
    - valueQuantity:
      - value: 18.5
      - unit: kg
      - system: http://unitsofmeasure.org  
      - code: kg
```

### 12.3 Data Migration and Integration

```fsharp
// FHIR data migration strategies
type FhirDataMigration = {
    // Legacy Data Transformation
    LegacyToFhir: TransformationConfig     // Transform legacy data to FHIR
    GStandardMapping: MappingConfig        // Map G-Standard data to FHIR
    
    // Data Validation
    MigrationValidation: ValidationConfig  // Validate migrated data
    QualityAssurance: QualityConfig        // Data quality checks
    
    // Incremental Migration
    IncrementalSync: SyncConfig            // Incremental data synchronization
    ConflictResolution: ConflictConfig     // Handle data conflicts
}

// FHIR ConceptMap for G-Standard to FHIR mapping
ConceptMap: gstandard-to-fhir-medication
- url: "http://genpres.nl/fhir/ConceptMap/gstandard-to-fhir-medication"
- version: "1.0.0"
- name: "GStandardToFHIRMedication" 
- title: "G-Standard to FHIR Medication Mapping"
- status: active
- publisher: "GenPRES"
- description: "Mapping G-Standard medication codes to FHIR Medication resource elements"
- sourceUri: "http://gstandard.nl/fhir/CodeSystem/gpk"
- targetUri: "http://hl7.org/fhir/StructureDefinition/Medication"
- group:
  - source: "http://gstandard.nl/fhir/CodeSystem/gpk"
  - target: "http://snomed.info/sct"
  - element:
    - code: "12345" # GPK code
    - display: "Paracetamol tablet 500mg"
    - target:
      - code: "387517004"
      - display: "Paracetamol"
      - equivalence: equivalent
  - element:
    - code: "67890" # GPK code  
    - display: "Amoxicillin capsule 250mg"
    - target:
      - code: "27658006"
      - display: "Amoxicillin"
      - equivalence: equivalent

// FHIR StructureMap for data transformation
StructureMap: legacy-patient-to-fhir
- url: "http://genpres.nl/fhir/StructureMap/legacy-patient-to-fhir"
- version: "1.0.0"
- name: "LegacyPatientToFHIR"
- status: active
- title: "Transform legacy patient data to FHIR Patient"
- description: "Transforms legacy EHR patient data to FHIR Patient resource"
- structure:
  - url: "http://genpres.nl/fhir/StructureDefinition/legacy-patient"
  - mode: source
  - alias: "LegacyPatient"
- structure:
  - url: "http://hl7.org/fhir/StructureDefinition/Patient"  
  - mode: target
  - alias: "FHIRPatient"
- group:
  - name: "patient"
  - typeMode: none
  - input:
    - name: "source"
    - type: "LegacyPatient"
    - mode: source
  - input:
    - name: "target"
    - type: "FHIRPatient" 
    - mode: target
  - rule:
    - name: "patient-id"
    - source:
      - context: "source"
      - element: "patientId"
      - variable: "pid"
    - target:
      - context: "target"
      - contextType: variable
      - element: "id"
      - transform: copy
      - parameter:
        - valueId: "pid"
  - rule:
    - name: "birth-date"
    - source:
      - context: "source"
      - element: "dateOfBirth" 
      - variable: "dob"
    - target:
      - context: "target"
      - contextType: variable
      - element: "birthDate"
      - transform: copy
      - parameter:
        - valueId: "dob"
```

## 13. Appendices

### Appendix A: FHIR Profiles and Extensions

```fsh

// Complete FHIR profile definitions
StructureDefinition: GenPRESPatient
- url: http://genpres.nl/fhir/StructureDefinition/genpres-patient
- version: 1.2.0
- name: GenPRESPatient  
- title: GenPRES Patient Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: Patient
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Patient
- derivation: constraint
- differential:
  - element:
    - id: Patient.identifier
    - path: Patient.identifier
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: Patient.identifier:BSN
    - path: Patient.identifier
    - sliceName: BSN
    - min: 0
    - max: 1
    - type:
      - code: Identifier
    - patternIdentifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
  - element:  
    - id: Patient.birthDate
    - path: Patient.birthDate
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: Patient.extension:gestationalAge
    - path: Patient.extension
    - sliceName: gestationalAge
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gestational-age]

StructureDefinition: GenPRESCarePlan
- url: http://genpres.nl/fhir/StructureDefinition/genpres-careplan
- version: 1.2.0
- name: GenPRESCarePlan
- title: GenPRES CarePlan Profile  
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: CarePlan
- baseDefinition: http://hl7.org/fhir/StructureDefinition/CarePlan
- derivation: constraint
- differential:
  - element:
    - id: CarePlan.status
    - path: CarePlan.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: CarePlan.intent
    - path: CarePlan.intent  
    - min: 1
    - max: 1
    - fixedCode: plan
    - mustSupport: true
  - element:
    - id: CarePlan.subject
    - path: CarePlan.subject
    - min: 1  
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: CarePlan.extension:gstandardCompliance
    - path: CarePlan.extension
    - sliceName: gstandardCompliance
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-compliance]

StructureDefinition: GenPRESMedicationRequest
- url: http://genpres.nl/fhir/StructureDefinition/genpres-medicationrequest
- version: 1.2.0
- name: GenPRESMedicationRequest
- title: GenPRES MedicationRequest Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: MedicationRequest
- baseDefinition: http://hl7.org/fhir/StructureDefinition/MedicationRequest
- derivation: constraint
- differential:
  - element:
    - id: MedicationRequest.status
    - path: MedicationRequest.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.intent
    - path: MedicationRequest.intent
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.medication[x]
    - path: MedicationRequest.medication[x]
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-medication]
    - mustSupport: true
  - element:
    - id: MedicationRequest.subject  
    - path: MedicationRequest.subject
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: MedicationRequest.dosageInstruction
    - path: MedicationRequest.dosageInstruction
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: MedicationRequest.extension:weightBasedDose
    - path: MedicationRequest.extension
    - sliceName: weightBasedDose
    - min: 0
    - max: 1  
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/weight-based-dose]
  - element:
    - id: MedicationRequest.extension:gstandardDoseRule
    - path: MedicationRequest.extension
    - sliceName: gstandardDoseRule
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule]
```

### Appendix B: G-Standard Integration Specifications

```fsh

// G-Standard specific extensions
StructureDefinition: GStandardCompliance
- url: http://genpres.nl/fhir/StructureDefinition/gstandard-compliance
- version: 1.2.0
- name: GStandardCompliance
- title: G-Standard Compliance Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: CarePlan
  - type: element
  - expression: MedicationRequest
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: G-Standard compliance information
    - definition: Detailed information about G-Standard compliance validation
  - element:
    - id: Extension.extension:level
    - path: Extension.extension
    - sliceName: level
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:level.url
    - path: Extension.extension.url
    - fixedUri: level
  - element:
    - id: Extension.extension:level.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/compliance-level
  - element:
    - id: Extension.extension:validationTimestamp
    - path: Extension.extension
    - sliceName: validationTimestamp
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:validationTimestamp.url
    - path: Extension.extension.url
    - fixedUri: validation-timestamp
  - element:
    - id: Extension.extension:validationTimestamp.value[x]
    - path: Extension.extension.value[x]  
    - type:
      - code: dateTime
  - element:
    - id: Extension.extension:gstandardVersion
    - path: Extension.extension
    - sliceName: gstandardVersion
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:gstandardVersion.url
    - path: Extension.extension.url
    - fixedUri: gstandard-version
  - element:
    - id: Extension.extension:gstandardVersion.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: string

StructureDefinition: WeightBasedDose
- url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
- version: 1.2.0
- name: WeightBasedDose
- title: Weight-Based Dose Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: MedicationRequest
  - type: element  
  - expression: Dosage
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: Weight-based dosing information
    - definition: Information about weight-based dose calculation
  - element:
    - id: Extension.extension:dosePerKg
    - path: Extension.extension
    - sliceName: dosePerKg
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:dosePerKg.url
    - path: Extension.extension.url
    - fixedUri: dose-per-kg
  - element:
    - id: Extension.extension:dosePerKg.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:patientWeight
    - path: Extension.extension
    - sliceName: patientWeight
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:patientWeight.url
    - path: Extension.extension.url
    - fixedUri: patient-weight
  - element:
    - id: Extension.extension:patientWeight.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:weightSource
    - path: Extension.extension
    - sliceName: weightSource
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:weightSource.url
    - path: Extension.extension.url
    - fixedUri: weight-source  
  - element:
    - id: Extension.extension:weightSource.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/weight-source
```

### Appendix C: API Documentation

```yaml
## Complete OpenAPI 3.0 specification
openapi: 3.0.3
info:
  title: GenPRES FHIR Treatment Planning API
  description: |
    FHIR R4 compliant API for GenPRES treatment planning integration.
    Supports G-Standard compliance, OAuth2/SMART-on-FHIR security,
    and IHE Pharmacy profile compliance.
  version: 1.2.0
  contact:
    name: GenPRES API Support
    url: https://support.genpres.nl
    email: api-support@genpres.nl
  license:
    name: Proprietary
    url: https://genpres.nl/license

servers:
  - url: https://api.genpres.nl/fhir
    description: Production FHIR server
  - url: https://staging-api.genpres.nl/fhir  
    description: Staging FHIR server
  - url: https://dev-api.genpres.nl/fhir
    description: Development FHIR server

security:
  - OAuth2: [patient/*.read, patient/*.write, user/*.read]
  - BearerAuth: []

paths:
  /$bootstrap:
    post:
      tags: [Treatment Planning]
      summary: Initialize patient context for treatment planning
      description: |
        Bootstrap operation that establishes patient context for treatment planning.
        Accepts a Bundle containing Patient and related clinical resources.
      operationId: bootstrap
      security:
        - OAuth2: [patient/*.read, launch, launch/patient]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            examples:
              patient-context:
                summary: Patient context with vital signs and allergies
                value:
                  resourceType: Bundle
                  type: collection
                  entry:
                    - resource:
                        resourceType: Patient
                        id: pat-123
                        birthDate: "2010-05-15"
                        gender: male
      responses:
        '200':
          description: Bootstrap successful
          content:
            application/fhir+json:: TREAT
        display: "Treatment"
      - system: http://genpres.nl/fhir/CodeSystem/security-label  
        code
        type:
          type: string
          enum: [document, message, transaction, transaction-response, batch, batch-response, history, searchset, collection]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'

    BundleEntry:
      type: object
      properties:
        fullUrl:
          type: string
          format: uri
        resource:
          oneOf:
            - $ref: '#/components/schemas/Patient'
            - $ref: '#/components/schemas/CarePlan'
            - $ref: '#/components/schemas/MedicationRequest'
            - $ref: '#/components/schemas/Medication'
        request:
          $ref: '#/components/schemas/BundleEntryRequest'

    BundleEntryRequest:
      type: object
      required: [method, url]
      properties:
        method:
          type: string
          enum: [GET, POST, PUT, DELETE, PATCH]
        url:
          type: string

    Patient:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Patient]
        id:
          type: string
          pattern: '^[A-Za-z0-9\-\.]{1,64}
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Treatment Plan Request
  /fhir/$treatment-plan-request:
    post:
      summary: Request treatment plan calculation
      operationId: treatmentPlanRequest
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # Treatment Plan Finalization
  /fhir/$finalize:
    post:
      summary: Finalize treatment plan for EHR implementation
      operationId: finalize
      parameters:
        - name: session-id
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # G-Standard Validation
  /fhir/Medication/$validate-gstandard:
    post:
      summary: Validate medication against G-Standard
      operationId: validateGStandard
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Simulation (Dry Run)
  /fhir/$simulate:
    post:
      summary: Simulate treatment plan without persistence
      operationId: simulate
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

components:
  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        type:
          type: string
          enum: [collection, transaction, searchset, batch]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'
    
    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'
```

### 8.2 OAuth2/SMART-on-FHIR Security Integration

```fsharp
// SMART-on-FHIR security context
type SmartOnFhirSecurity = {
    // OAuth2 Configuration
    AuthorizationServer: string           // EHR authorization server
    TokenEndpoint: string                 // OAuth2 token endpoint
    IntrospectionEndpoint: string         // Token introspection endpoint
    
    // SMART Scopes
    RequiredScopes: SmartScope[]          // Required SMART scopes
    OptionalScopes: SmartScope[]          // Optional enhanced scopes
    
    // Token Management
    AccessToken: AccessToken              // Current access token
    RefreshToken: RefreshToken option     // Refresh token if available
    TokenExpiration: DateTime             // Token expiration time
    
    // FHIR Security
    FhirSecurityLabels: SecurityLabel[]   // FHIR security labels
    AuditRequirements: AuditRequirement[] // IHE ATNA compliance
}

// SMART-on-FHIR scopes for GenPRES integration
type SmartScope =
    | PatientRead                         // patient/*.read
    | PatientWrite                        // patient/*.write
    | UserRead                            // user/*.read
    | UserWrite                           // user/*.write
    | SystemRead                          // system/*.read
    | SystemWrite                         // system/*.write
    | Launch                              // launch
    | LaunchPatient                       // launch/patient
    | FhirUser                            // fhirUser
    | OpenIdProfile                       // profile openid

// OAuth2 token validation for FHIR requests
let validateFhirToken (token: string) : Async<Result<FhirSecurityContext, SecurityError>> =
    async {
        try
            // Introspect token with EHR authorization server
            let! introspectionResult = introspectToken token
            
            match introspectionResult with
            | Error error -> return Error error
            | Ok tokenInfo ->
                
                // Validate required scopes
                let requiredScopes = [
                    "patient/Patient.read"
                    "patient/CarePlan.write" 
                    "patient/MedicationRequest.write"
                    "patient/Medication.read"
                    "user/Practitioner.read"
                ]
                
                let hasRequiredScopes = 
                    requiredScopes 
                    |> List.forall (fun scope -> tokenInfo.Scopes |> List.contains scope)
                
                if not hasRequiredScopes then
                    return Error (InsufficientScopes requiredScopes)
                else
                    let securityContext = {
                        UserId = tokenInfo.Sub
                        PatientId = tokenInfo.Patient
                        Scopes = tokenInfo.Scopes
                        FhirUser = tokenInfo.FhirUser
                        ExpiresAt = tokenInfo.ExpiresAt
                    }
                    return Ok securityContext
        with
        | ex -> return Error (TokenValidationError ex.Message)
    }
```

### 8.3 IHE Profile Compliance

```fsh

// IHE Pharmacy (PHARM) Profile Compliance
Profile: IHE-PHARM-CarePlan
Parent: CarePlan
Title: "IHE Pharmacy CarePlan Profile"
Description: "CarePlan profile compliant with IHE Pharmacy workflow"
- status 1..1 MS
- intent 1..1 MS
- subject 1..1 MS
- activity
  - detail
    - kind 1..1 MS
    - code 1..1 MS
    - status 1..1 MS
    - medication[x] 0..1 MS

// IHE Audit Trail and Node Authentication (ATNA) Compliance
Profile: IHE-ATNA-AuditEvent
Parent: AuditEvent
Title: "IHE ATNA AuditEvent Profile"
Description: "AuditEvent profile for IHE ATNA compliance"
- type 1..1 MS
- action 1..1 MS
- recorded 1..1 MS
- outcome 1..1 MS
- agent 1..* MS
  - type 1..1 MS
  - who 1..1 MS
  - requestor 1..1 MS
- source 1..1 MS
  - observer 1..1 MS
- entity 0..* MS
  - what 0..1 MS
  - type 0..1 MS
  - role 0..1 MS

// IHE Internet User Authorization (IUA) Profile
Profile: IHE-IUA-AccessToken
Title: "IHE IUA Access Token Profile" 
Description: "OAuth2 access token profile for IHE IUA compliance"
- iss (issuer) 1..1 MS
- sub (subject) 1..1 MS  
- aud (audience) 1..1 MS
- exp (expiration) 1..1 MS
- iat (issued at) 1..1 MS
- scope 1..1 MS
- purpose_of_use 0..1 MS
- homeCommunityId 0..1 MS
- national_provider_identifier 0..1 MS
- subject_organization 0..1 MS
- subject_organization_id 0..1 MS
```

## 9. Clinical Safety and Compliance

### 9.1 FHIR-Based Clinical Decision Support

```fsh

// Clinical Decision Support through FHIR CDS Hooks
CDS Hook: medication-prescribe
- hook: medication-prescribe  
- title: "GenPRES Medication Safety Check"
- description: "G-Standard compliant medication safety validation"
- id: "genpres-medication-safety"
- prefetch:
    patient: "Patient/{{context.patientId}}"
    medications: "MedicationRequest?patient={{context.patientId}}&status=active"
    allergies: "AllergyIntolerance?patient={{context.patientId}}"
    conditions: "Condition?patient={{context.patientId}}&clinical-status=active"
    observations: "Observation?patient={{context.patientId}}&category=vital-signs&_sort=-date&_count=10"

// CDS Card response for safety alerts
CDSCard: safety-alert
- summary: "G-Standard safety alert"
- detail: "Detailed safety information from G-Standard validation"
- indicator: "warning" | "info" | "critical"
- source:
  - label: "G-Standard"
  - url: "http://gstandard.nl"
  - icon: "http://gstandard.nl/icon.png"
- suggestions:
  - label: "Alternative medication"
  - actions:
    - type: "create"
    - description: "Add alternative medication"
    - resource: MedicationRequest

// DetectedIssue resource for clinical alerts
DetectedIssue: clinical-safety-alert
- status: preliminary | final | entered-in-error
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActCode
    - code: DRG | ALLERGY | DOSEAMT | DOSEDUR | DOSECOND
- severity: high | moderate | low
- patient: Reference(Patient)
- detail: "Human readable description of the issue"
- reference: "Reference to related resources"
- mitigation:
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActCode  
      - code: MONITOR | REPLACE | REDUCE | DISCONTINUE
  - description: "Description of mitigation action"
```

### 9.2 G-Standard Safety Validation

```fsharp
// G-Standard safety validation through FHIR operations
type GStandardSafetyValidation = {
    // Validation Operations
    ProductValidation: FhirOperation       // Validate product GPK codes
    DoseRuleValidation: FhirOperation      // Validate dose rules compliance
    InteractionCheck: FhirOperation        // Check drug interactions
    ContraindicationCheck: FhirOperation   // Check contraindications
    
    // Safety Assessment
    RiskAssessment: RiskAssessment         // Overall safety risk assessment
    SafetyProfile: Extension               // Comprehensive safety profile
    
    // Compliance Reporting
    ComplianceReport: Parameters           // G-Standard compliance report
    ValidationOutcome: OperationOutcome    // Validation results
}

// FHIR OperationDefinition for comprehensive safety validation
OperationDefinition: comprehensive-safety-check
- name: comprehensive-safety-check
- status: active
- kind: operation
- code: comprehensive-safety-check
- resource: [CarePlan]
- system: false
- type: false  
- instance: true
- parameter:
  - name: gstandard-version
    use: in
    min: 0
    max: 1
    type: string
  - name: validation-level  
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: safety-report
    use: out
    min: 1
    max: 1 
    type: Parameters
  - name: detected-issues
    use: out
    min: 0
    max: "*"
    type: DetectedIssue
  - name: risk-assessment
    use: out  
    min: 0
    max: 1
    type: RiskAssessment

// RiskAssessment resource for overall safety evaluation  
RiskAssessment: treatment-plan-safety
- status: final
- subject: Reference(Patient)
- encounter: Reference(Encounter) 
- performedDateTime: {assessment-timestamp}
- performer: Reference(Device) # GenPRES system
- code:
  - coding:
    - system: http://snomed.info/sct
    - code: 225338004
    - display: "Risk assessment"
- prediction:
  - outcome:
    - coding:
      - system: http://snomed.info/sct  
      - code: 281647001
      - display: "Adverse reaction"
  - probabilityDecimal: 0.15
  - relativeRisk: 1.2
- mitigation: "Recommended monitoring and precautions"
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/gstandard-risk-factors
    extension:
      - url: age-risk
        valueDecimal: 0.1
      - url: weight-risk  
        valueDecimal: 0.05
      - url: interaction-risk
        valueDecimal: 0.3
```

### 9.3 Pediatric Safety Specializations

```fsh

// Pediatric-specific FHIR profiles and extensions
Profile: PediatricMedicationRequest
Parent: MedicationRequest
Title: "Pediatric Medication Request"
Description: "MedicationRequest profile with pediatric safety requirements"
- subject 1..1 MS
- subject only Reference(PediatricPatient)
- dosageInstruction 1..* MS
  - doseAndRate
    - dose[x] 1..1 MS
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
        extension:
          - url: dose-per-kg
            valueQuantity 1..1 MS
          - url: patient-weight  
            valueQuantity 1..1 MS
          - url: weight-source
            valueCode 1..1 MS # measured | estimated | calculated
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/pediatric-safety-check
    extension:
      - url: age-appropriate
        valueBoolean 1..1 MS
      - url: weight-appropriate
        valueBoolean 1..1 MS  
      - url: concentration-safe
        valueBoolean 1..1 MS
      - url: volume-appropriate
        valueBoolean 1..1 MS

// Age-specific contraindication checking
CodeSystem: pediatric-age-groups
- url: http://genpres.nl/fhir/CodeSystem/pediatric-age-groups
- concept:
  - code: neonate
    display: "Neonate (0-28 days)"
    definition: "Newborn infant up to 28 days of age"
  - code: infant  
    display: "Infant (29 days - 2 years)"
    definition: "Infant from 29 days to 2 years of age"
  - code: child
    display: "Child (2-12 years)" 
    definition: "Child from 2 to 12 years of age"
  - code: adolescent
    display: "Adolescent (12-18 years)"
    definition: "Adolescent from 12 to 18 years of age"

// Pediatric dose rule validation
Extension: pediatric-dose-rule
- url: http://genpres.nl/fhir/StructureDefinition/pediatric-dose-rule
- extension:
  - url: rule-id
    valueString 1..1 MS
  - url: age-group  
    valueCode 1..1 MS # From pediatric-age-groups CodeSystem
  - url: weight-range
    extension:
      - url: min-weight
        valueQuantity 0..1 MS
      - url: max-weight 
        valueQuantity 0..1 MS
  - url: dose-calculation
    extension:
      - url: dose-per-kg
        valueQuantity 0..1 MS
      - url: dose-per-m2
        valueQuantity 0..1 MS  
      - url: max-single-dose
        valueQuantity 0..1 MS
      - url: max-daily-dose
        valueQuantity 0..1 MS
  - url: safety-considerations
    valueString 0..* MS
```

## 10. Performance and Scalability

### 10.1 FHIR Performance Optimization

```fsharp
// FHIR-specific performance optimization strategies
type FhirPerformanceOptimization = {
    // Bundle Optimization
    BundleSize: BundleSizeStrategy         // Optimal bundle sizes
    BundleProcessing: BundleProcessingStrategy // Parallel vs sequential
    
    // Resource Optimization  
    ResourceMinimization: ResourceMinimizationStrategy // Include only necessary elements
    ReferenceOptimization: ReferenceStrategy // Contained vs external references
    
    // Search Optimization
    SearchParameterOptimization: SearchStrategy // Efficient FHIR searches
    IncludeRevIncludeOptimization: IncludeStrategy // Optimal _include/_revinclude usage
    
    // Caching Strategy
    ResourceCaching: CachingStrategy       // Cache frequently accessed resources
    BundleCaching: CachingStrategy         // Cache complete bundles
    OperationCaching: CachingStrategy      // Cache operation results
}

// Bundle size optimization for treatment plans
type BundleSizeStrategy = {
    MaxBundleSize: int                     // Maximum entries per bundle
    ResourceTypeGrouping: bool             // Group similar resources  
    DependencyOrdering: bool               // Order resources by dependencies
    ChunkingStrategy: ChunkingMethod       // How to split large bundles
}

type ChunkingMethod =
    | ByResourceCount of int               // Split by number of resources
    | ByResourceType                       // Split by resource type
    | ByDependencyGraph                    // Split maintaining dependencies
    | ByPayloadSize of int                 // Split by payload size in KB

// FHIR search optimization for patient context
let optimizedPatientContextQuery (patientId: string) =
    $"""
    Patient/{patientId}?
    _include=Patient:organization&
    _include=Patient:general-practitioner&
    _revinclude=Observation:patient&
    _revinclude=AllergyIntolerance:patient&
    _revinclude=Condition:patient&
    _revinclude=MedicationRequest:patient&
    _count=50&
    _sort=-_lastUpdated
    """

// Resource minimization through FHIR _elements parameter  
let minimizePatientResource = "_elements=id,identifier,birthDate,gender,extension"
let minimizeObservationResource = "_elements=id,status,code,subject,value,effective"
let minimizeMedicationRequestResource = "_elements=id,status,intent,medication,subject,dosageInstruction"
```

### 10.2 Scalability Architecture

```fsh

// FHIR server scalability configuration
CapabilityStatement: genpres-capability
- status: active
- date: "2026-01-15"
- publisher: "GenPRES"
- kind: instance
- software:
  - name: "GenPRES FHIR Server"
  - version: "2.1.0"
- implementation:
  - description: "GenPRES Treatment Planning FHIR Server"  
  - url: "https://api.genpres.nl/fhir"
- fhirVersion: 4.0.1
- format: [json, xml]
- rest:
  - mode: server
  - security:
    - cors: true
    - service:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/restful-security-service
        - code: OAuth
        - display: "OAuth2 using SMART-on-FHIR profile"
  - resource:
    - type: Patient
      interaction: [read, search-type]
      searchParam:
        - name: identifier
          type: token
        - name: birthdate  
          type: date
    - type: CarePlan
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      conditionalUpdate: true
      searchParam:
        - name: patient
          type: reference
        - name: status
          type: token
        - name: date
          type: date
    - type: MedicationRequest  
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      searchParam:
        - name: patient
          type: reference
        - name: medication
          type: reference  
        - name: status
          type: token
  - operation:
    - name: bootstrap
      definition: http://genpres.nl/fhir/OperationDefinition/bootstrap
    - name: treatment-plan-request
      definition: http://genpres.nl/fhir/OperationDefinition/treatment-plan-request
    - name: finalize
      definition: http://genpres.nl/fhir/OperationDefinition/finalize
    - name: validate-gstandard  
      definition: http://genpres.nl/fhir/OperationDefinition/validate-gstandard
```

### 10.3 Performance Monitoring

```fsharp
// FHIR-based performance monitoring
type FhirPerformanceMonitoring = {
    // Operation Performance
    OperationMetrics: OperationMetric[]    // Performance per FHIR operation
    ResourceMetrics: ResourceMetric[]      // Performance per resource type
    BundleMetrics: BundleMetric[]          // Bundle processing performance
    
    // Quality Metrics
    ValidationMetrics: ValidationMetric[]  // G-Standard validation performance
    SafetyMetrics: SafetyMetric[]          // Safety check performance
    
    // System Metrics  
    ServerMetrics: ServerMetric[]          // FHIR server performance
    DatabaseMetrics: DatabaseMetric[]     // Database performance
    CacheMetrics: CacheMetric[]           // Cache performance
}

// Performance metrics as FHIR Observation resources
let createPerformanceObservation (operationType: string) (duration: float) =
    {
        ResourceType = "Observation"
        Status = ObservationStatus.Final
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/observation-category"
                        Code = Some "performance-metric"
                        Display = Some "Performance Metric"
                    }
                ]
            }
        ]
        Code = {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                    Code = Some "operation-duration"  
                    Display = Some "Operation Duration"
                }
            ]
        }
        Component = [
            {
                Code = {
                    Coding = [
                        {
                            System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                            Code = Some "operation-type"
                            Display = Some "Operation Type"
                        }
                    ]
                }
                ValueString = Some operationType
            }
        ]
        ValueQuantity = Some {
            Value = Some duration
            Unit = Some "ms"
            System = Some "http://unitsofmeasure.org"
            Code = Some "ms"
        }
        EffectiveDateTime = Some DateTime.UtcNow
        Device = Some {
            Display = Some "GenPRES Performance Monitor"
        }
    }
```

## 11. Security and Privacy

### 11.1 OAuth2/SMART-on-FHIR Security

```fsh

// OAuth2 Authorization Server Metadata (RFC 8414)
OAuth2Metadata: genpres-oauth2
- issuer: "https://auth.genpres.nl"
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"  
- introspection_endpoint: "https://auth.genpres.nl/oauth2/introspect"
- jwks_uri: "https://auth.genpres.nl/.well-known/jwks.json"
- scopes_supported: [
    "patient/*.read",
    "patient/*.write", 
    "user/*.read",
    "user/*.write",
    "system/*.read",
    "system/*.write",
    "launch",
    "launch/patient",
    "fhirUser",
    "openid",
    "profile"
  ]
- response_types_supported: ["code", "token", "id_token"]
- grant_types_supported: ["authorization_code", "client_credentials", "refresh_token"]
- code_challenge_methods_supported: ["S256"]
- token_endpoint_auth_methods_supported: ["client_secret_basic", "client_secret_post", "private_key_jwt"]

// SMART-on-FHIR Well-Known Endpoint
SMARTConfiguration: smart-configuration  
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"
- capabilities: [
  "launch-ehr", 
  "launch-standalone",
  "client-public",
  "client-confidential-symmetric", 
  "sso-openid-connect",
  "context-passthrough-banner",
  "context-passthrough-style",
  "context-ehr-patient",
  "context-ehr-encounter",
  "permission-offline",
  "permission-patient",
  "permission-user"
]
```

### 11.2 FHIR Security Labels and Access Control

```fsh

// Security labels for FHIR resources
CodeSystem: security-labels
- url: http://genpres.nl/fhir/CodeSystem/security-label
- concept:
  - code: PEDS
    display: "Pediatric Data"
    definition: "Data specific to pediatric patients requiring special handling"
  - code: GSTAND
    display: "G-Standard Data" 
    definition: "Data validated against G-Standard requiring compliance tracking"
  - code: CALC
    display: "Calculated Data"
    definition: "Data generated by GenPRES calculations"
  - code: SENS
    display: "Sensitive Clinical Data"
    definition: "Highly sensitive clinical information requiring restricted access"

// Security labeling for CarePlan resources
CarePlan: secure-treatment-plan
- meta:
    security:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
        code: GSTAND
        display: "G-Standard Data"
      - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
        code: N
        display: "Normal"

// Consent resource for treatment plan data usage
Consent: treatment-plan-consent
- status: active
- scope:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentscope
    - code: treatment
    - display: "Treatment"
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentcategorycodes
    - code: medical
    - display: "Medical Consent"
- patient: Reference(Patient)
- dateTime: "2026-01-15T10:00:00Z"
- performer: [Reference(Patient)]
- policy:
  - authority: "https://genpres.nl/privacy-policy"
  - uri: "https://genpres.nl/consent/treatment-planning"
- provision:
  - type: permit
  - purpose:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
    - code: TREAT
    - display: "Treatment"
  - class:
    - system: http://hl7.org/fhir/resource-types
    - code: CarePlan
  - class:
    - system: http://hl7.org/fhir/resource-types  
    - code: MedicationRequest
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: collect
      - display: "Collect"
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: use
      - display: "Use"
```

### 11.3 Audit Trail and Compliance

```fsh

// IHE ATNA compliant audit events
AuditEvent: treatment-plan-access
- type:
  - system: http://dicom.nema.org/resources/ontology/DCM
  - code: "110112"
  - display: "Query"
- action: E # Execute/Perform
- recorded: "2026-01-15T10:30:00Z"
- outcome: "0" # Success
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: humanuser
      - display: "Human User"
  - who:
    - reference: "Practitioner/dr-smith"
    - display: "Dr. John Smith"
  - requestor: true
  - location: Reference(Location/icu-room-101)
  - policy: ["http://genpres.nl/policy/treatment-planning-access"]
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: application
      - display: "Application"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/application-id"
      - value: "genpres-treatment-planner"
    - display: "GenPRES Treatment Planner"
  - requestor: false
- source:
  - observer:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/system-id"  
      - value: "genpres-fhir-server"
    - display: "GenPRES FHIR Server"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/security-source-type
    - code: "4"
    - display: "Application Server"
- entity:
  - what:
    - reference: "Patient/pat-123"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: "1"
    - display: "Person"
  - role:
    - system: http://terminology.hl7.org/CodeSystem/object-role
    - code: "1" 
    - display: "Patient"
  - name: "Treatment plan calculation for pediatric patient"
  - securityLabel:
    - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
    - code: N
    - display: "Normal"

// Provenance tracking for calculated treatment plans
Provenance: treatment-plan-calculation
- target:
  - reference: "CarePlan/calculated-plan-001"
- target:
  - reference: "MedicationRequest/calculated-medreq-001"
- recorded: "2026-01-15T10:30:00Z"
- policy: ["http://genpres.nl/policy/calculation-provenance"]
- location: Reference(Location/genpres-datacenter)
- activity:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-DataOperation
    - code: CREATE
    - display: "Create"
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
      - code: performer
      - display: "Performer"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/calculation-engine"
      - value: "genpres-calc-engine-v2.1"
    - display: "GenPRES Calculation Engine v2.1"
  - onBehalfOf:
    - reference: "Practitioner/dr-smith"
- entity:
  - role: source
  - what:
    - reference: "Bundle/patient-context-bundle"
    - display: "Patient context bundle used for calculation"
  - agent:
    - type:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
        - code: enterer
        - display: "Enterer"  
    - who:
      - reference: "Device/ehr-system"
      - display: "Hospital EHR System"
- signature:
  - type:
    - system: urn:iso-astm:E1762-95:2013
    - code: 1.2.840.10065.1.12.1.1
    - display: "Author's Signature"
  - when: "2026-01-15T10:30:00Z"
  - who:
    - reference: "Device/genpres-system"
  - data: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." # Base64 encoded signature
```

## 12. Implementation Considerations

### 12.1 FHIR Implementation Guide

```fsh

// Implementation Guide definition
ImplementationGuide: genpres-treatment-planning
- url: "http://genpres.nl/fhir/ImplementationGuide/treatment-planning"
- version: "1.2.0" 
- name: "GenPRESTreatmentPlanning"
- title: "GenPRES Treatment Planning Implementation Guide"
- status: active
- publisher: "GenPRES"
- description: "FHIR R4 implementation guide for GenPRES treatment planning integration"
- packageId: "nl.genpres.treatmentplanning"
- license: CC0-1.0
- fhirVersion: [4.0.1]
- dependsOn:
  - uri: "http://hl7.org/fhir"
    version: "4.0.1"
  - uri: "http://hl7.org/fhir/uv/sdc"
    version: "3.0.0"
  - uri: "http://hl7.org/fhir/uv/cpg"  
    version: "2.0.0"
- definition:
  - resource:
    - reference:
      - reference: "StructureDefinition/genpres-patient"
      - name: "GenPRES Patient Profile"
      - description: "Patient profile with Dutch healthcare extensions"
    - reference:
      - reference: "StructureDefinition/genpres-careplan"
      - name: "GenPRES CarePlan Profile" 
      - description: "Treatment plan as FHIR CarePlan"
    - reference:
      - reference: "StructureDefinition/genpres-medicationrequest"
      - name: "GenPRES MedicationRequest Profile"
      - description: "Medication request with G-Standard compliance"
  - page:
    - nameUrl: "index.html"
    - title: "GenPRES Treatment Planning IG"
    - generation: html
    - page:
      - nameUrl: "profiles.html" 
      - title: "FHIR Profiles"
      - generation: html
    - page:
      - nameUrl: "extensions.html"
      - title: "FHIR Extensions"  
      - generation: html
    - page:
      - nameUrl: "terminology.html"
      - title: "Terminology"
      - generation: html
    - page:
      - nameUrl: "examples.html"
      - title: "Examples"
      - generation: html
```

### 12.2 Testing and Validation

```fsharp
// FHIR validation and testing framework
type FhirTestingFramework = {
    // Profile Validation
    ProfileValidation: ProfileValidationConfig // FHIR profile validation
    SchemaValidation: SchemaValidationConfig   // FHIR schema validation
    TerminologyValidation: TerminologyValidationConfig // Terminology validation
    
    // Integration Testing
    OperationTesting: OperationTestConfig      // FHIR operation testing
    WorkflowTesting: WorkflowTestConfig        // End-to-end workflow testing
    PerformanceTesting: PerformanceTestConfig  // Performance testing
    
    // Compliance Testing
    G-StandardTesting: GStandardTestConfig     // G-Standard compliance testing
    SecurityTesting: SecurityTestConfig        // OAuth2/SMART security testing
    IHETesting: IHETestConfig                  // IHE profile compliance testing
}

// FHIR TestScript resources for automated testing
TestScript: bootstrap-operation-test
- url: "http://genpres.nl/fhir/TestScript/bootstrap-operation-test"
- version: "1.0.0"
- name: "BootstrapOperationTest"
- status: active
- title: "Test bootstrap operation with patient context"
- description: "Validates the $bootstrap operation with complete patient context"
- origin:
  - index: 1
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-origin-types"
    - code: "FHIR-Client"
- destination:
  - index: 1  
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-destination-types"
    - code: "FHIR-Server"
- fixture:
  - id: "patient-context-bundle"
  - autocreate: false
  - autodelete: false
  - resource:
    - reference: "Bundle/patient-context-example"
- variable:
  - name: "patientId"
  - defaultValue: "pat-123"
  - sourceId: "patient-context-bundle"
  - expression: "Bundle.entry.where(resource is Patient).resource.id"
- test:
  - id: "bootstrap-success"
  - name: "Bootstrap Operation Success"
  - description: "Test successful bootstrap operation"
  - action:
    - operation:
      - type:
        - system: "http://terminology.hl7.org/CodeSystem/testscript-operation-codes"
        - code: "create"
      - resource: "Bundle"
      - description: "Execute bootstrap operation"  
      - destination: 1
      - origin: 1
      - params: "/$bootstrap"
      - sourceId: "patient-context-bundle"
    - assert:
      - description: "Confirm that the returned HTTP status is 200(OK)"
      - direction: response
      - responseCode: "200"
    - assert:
      - description: "Confirm that the response is an OperationOutcome"
      - direction: response
      - resource: "OperationOutcome"
    - assert:
      - description: "Confirm successful outcome"
      - direction: response
      - expression: "OperationOutcome.issue.where(severity='information').exists()"

// Example test data for FHIR testing
Bundle: patient-context-test-data
- resourceType: Bundle
- type: collection
- entry:
  - resource:
    - resourceType: Patient
    - id: test-patient-001
    - identifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
      - value: "999999999"
    - birthDate: "2015-03-15"
    - gender: male
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
        valueQuantity:
          value: 39
          unit: weeks
          system: http://unitsofmeasure.org
          code: wk
  - resource:
    - resourceType: Observation
    - status: final
    - category:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/observation-category
        - code: vital-signs
    - code:
      - coding:
        - system: http://loinc.org
        - code: "29463-7"
        - display: "Body weight"
    - subject:
      - reference: "Patient/test-patient-001"
    - valueQuantity:
      - value: 18.5
      - unit: kg
      - system: http://unitsofmeasure.org  
      - code: kg
```

### 12.3 Data Migration and Integration

```fsharp
// FHIR data migration strategies
type FhirDataMigration = {
    // Legacy Data Transformation
    LegacyToFhir: TransformationConfig     // Transform legacy data to FHIR
    GStandardMapping: MappingConfig        // Map G-Standard data to FHIR
    
    // Data Validation
    MigrationValidation: ValidationConfig  // Validate migrated data
    QualityAssurance: QualityConfig        // Data quality checks
    
    // Incremental Migration
    IncrementalSync: SyncConfig            // Incremental data synchronization
    ConflictResolution: ConflictConfig     // Handle data conflicts
}

// FHIR ConceptMap for G-Standard to FHIR mapping
ConceptMap: gstandard-to-fhir-medication
- url: "http://genpres.nl/fhir/ConceptMap/gstandard-to-fhir-medication"
- version: "1.0.0"
- name: "GStandardToFHIRMedication" 
- title: "G-Standard to FHIR Medication Mapping"
- status: active
- publisher: "GenPRES"
- description: "Mapping G-Standard medication codes to FHIR Medication resource elements"
- sourceUri: "http://gstandard.nl/fhir/CodeSystem/gpk"
- targetUri: "http://hl7.org/fhir/StructureDefinition/Medication"
- group:
  - source: "http://gstandard.nl/fhir/CodeSystem/gpk"
  - target: "http://snomed.info/sct"
  - element:
    - code: "12345" # GPK code
    - display: "Paracetamol tablet 500mg"
    - target:
      - code: "387517004"
      - display: "Paracetamol"
      - equivalence: equivalent
  - element:
    - code: "67890" # GPK code  
    - display: "Amoxicillin capsule 250mg"
    - target:
      - code: "27658006"
      - display: "Amoxicillin"
      - equivalence: equivalent

// FHIR StructureMap for data transformation
StructureMap: legacy-patient-to-fhir
- url: "http://genpres.nl/fhir/StructureMap/legacy-patient-to-fhir"
- version: "1.0.0"
- name: "LegacyPatientToFHIR"
- status: active
- title: "Transform legacy patient data to FHIR Patient"
- description: "Transforms legacy EHR patient data to FHIR Patient resource"
- structure:
  - url: "http://genpres.nl/fhir/StructureDefinition/legacy-patient"
  - mode: source
  - alias: "LegacyPatient"
- structure:
  - url: "http://hl7.org/fhir/StructureDefinition/Patient"  
  - mode: target
  - alias: "FHIRPatient"
- group:
  - name: "patient"
  - typeMode: none
  - input:
    - name: "source"
    - type: "LegacyPatient"
    - mode: source
  - input:
    - name: "target"
    - type: "FHIRPatient" 
    - mode: target
  - rule:
    - name: "patient-id"
    - source:
      - context: "source"
      - element: "patientId"
      - variable: "pid"
    - target:
      - context: "target"
      - contextType: variable
      - element: "id"
      - transform: copy
      - parameter:
        - valueId: "pid"
  - rule:
    - name: "birth-date"
    - source:
      - context: "source"
      - element: "dateOfBirth" 
      - variable: "dob"
    - target:
      - context: "target"
      - contextType: variable
      - element: "birthDate"
      - transform: copy
      - parameter:
        - valueId: "dob"
```

## 13. Appendices

### Appendix A: FHIR Profiles and Extensions

```fsh

// Complete FHIR profile definitions
StructureDefinition: GenPRESPatient
- url: http://genpres.nl/fhir/StructureDefinition/genpres-patient
- version: 1.2.0
- name: GenPRESPatient  
- title: GenPRES Patient Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: Patient
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Patient
- derivation: constraint
- differential:
  - element:
    - id: Patient.identifier
    - path: Patient.identifier
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: Patient.identifier:BSN
    - path: Patient.identifier
    - sliceName: BSN
    - min: 0
    - max: 1
    - type:
      - code: Identifier
    - patternIdentifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
  - element:  
    - id: Patient.birthDate
    - path: Patient.birthDate
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: Patient.extension:gestationalAge
    - path: Patient.extension
    - sliceName: gestationalAge
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gestational-age]

StructureDefinition: GenPRESCarePlan
- url: http://genpres.nl/fhir/StructureDefinition/genpres-careplan
- version: 1.2.0
- name: GenPRESCarePlan
- title: GenPRES CarePlan Profile  
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: CarePlan
- baseDefinition: http://hl7.org/fhir/StructureDefinition/CarePlan
- derivation: constraint
- differential:
  - element:
    - id: CarePlan.status
    - path: CarePlan.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: CarePlan.intent
    - path: CarePlan.intent  
    - min: 1
    - max: 1
    - fixedCode: plan
    - mustSupport: true
  - element:
    - id: CarePlan.subject
    - path: CarePlan.subject
    - min: 1  
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: CarePlan.extension:gstandardCompliance
    - path: CarePlan.extension
    - sliceName: gstandardCompliance
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-compliance]

StructureDefinition: GenPRESMedicationRequest
- url: http://genpres.nl/fhir/StructureDefinition/genpres-medicationrequest
- version: 1.2.0
- name: GenPRESMedicationRequest
- title: GenPRES MedicationRequest Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: MedicationRequest
- baseDefinition: http://hl7.org/fhir/StructureDefinition/MedicationRequest
- derivation: constraint
- differential:
  - element:
    - id: MedicationRequest.status
    - path: MedicationRequest.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.intent
    - path: MedicationRequest.intent
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.medication[x]
    - path: MedicationRequest.medication[x]
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-medication]
    - mustSupport: true
  - element:
    - id: MedicationRequest.subject  
    - path: MedicationRequest.subject
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: MedicationRequest.dosageInstruction
    - path: MedicationRequest.dosageInstruction
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: MedicationRequest.extension:weightBasedDose
    - path: MedicationRequest.extension
    - sliceName: weightBasedDose
    - min: 0
    - max: 1  
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/weight-based-dose]
  - element:
    - id: MedicationRequest.extension:gstandardDoseRule
    - path: MedicationRequest.extension
    - sliceName: gstandardDoseRule
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule]
```

### Appendix B: G-Standard Integration Specifications

```fsh

// G-Standard specific extensions
StructureDefinition: GStandardCompliance
- url: http://genpres.nl/fhir/StructureDefinition/gstandard-compliance
- version: 1.2.0
- name: GStandardCompliance
- title: G-Standard Compliance Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: CarePlan
  - type: element
  - expression: MedicationRequest
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: G-Standard compliance information
    - definition: Detailed information about G-Standard compliance validation
  - element:
    - id: Extension.extension:level
    - path: Extension.extension
    - sliceName: level
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:level.url
    - path: Extension.extension.url
    - fixedUri: level
  - element:
    - id: Extension.extension:level.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/compliance-level
  - element:
    - id: Extension.extension:validationTimestamp
    - path: Extension.extension
    - sliceName: validationTimestamp
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:validationTimestamp.url
    - path: Extension.extension.url
    - fixedUri: validation-timestamp
  - element:
    - id: Extension.extension:validationTimestamp.value[x]
    - path: Extension.extension.value[x]  
    - type:
      - code: dateTime
  - element:
    - id: Extension.extension:gstandardVersion
    - path: Extension.extension
    - sliceName: gstandardVersion
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:gstandardVersion.url
    - path: Extension.extension.url
    - fixedUri: gstandard-version
  - element:
    - id: Extension.extension:gstandardVersion.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: string

StructureDefinition: WeightBasedDose
- url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
- version: 1.2.0
- name: WeightBasedDose
- title: Weight-Based Dose Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: MedicationRequest
  - type: element  
  - expression: Dosage
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: Weight-based dosing information
    - definition: Information about weight-based dose calculation
  - element:
    - id: Extension.extension:dosePerKg
    - path: Extension.extension
    - sliceName: dosePerKg
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:dosePerKg.url
    - path: Extension.extension.url
    - fixedUri: dose-per-kg
  - element:
    - id: Extension.extension:dosePerKg.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:patientWeight
    - path: Extension.extension
    - sliceName: patientWeight
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:patientWeight.url
    - path: Extension.extension.url
    - fixedUri: patient-weight
  - element:
    - id: Extension.extension:patientWeight.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:weightSource
    - path: Extension.extension
    - sliceName: weightSource
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:weightSource.url
    - path: Extension.extension.url
    - fixedUri: weight-source  
  - element:
    - id: Extension.extension:weightSource.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/weight-source
```

### Appendix C: API Documentation

```yaml
## Complete OpenAPI 3.0 specification
openapi: 3.0.3
info:
  title: GenPRES FHIR Treatment Planning API
  description: |
    FHIR R4 compliant API for GenPRES treatment planning integration.
    Supports G-Standard compliance, OAuth2/SMART-on-FHIR security,
    and IHE Pharmacy profile compliance.
  version: 1.2.0
  contact:
    name: GenPRES API Support
    url: https://support.genpres.nl
    email: api-support@genpres.nl
  license:
    name: Proprietary
    url: https://genpres.nl/license

servers:
  - url: https://api.genpres.nl/fhir
    description: Production FHIR server
  - url: https://staging-api.genpres.nl/fhir  
    description: Staging FHIR server
  - url: https://dev-api.genpres.nl/fhir
    description: Development FHIR server

security:
  - OAuth2: [patient/*.read, patient/*.write, user/*.read]
  - BearerAuth: []

paths:
  /$bootstrap:
    post:
      tags: [Treatment Planning]
      summary: Initialize patient context for treatment planning
      description: |
        Bootstrap operation that establishes patient context for treatment planning.
        Accepts a Bundle containing Patient and related clinical resources.
      operationId: bootstrap
      security:
        - OAuth2: [patient/*.read, launch, launch/patient]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            examples:
              patient-context:
                summary: Patient context with vital signs and allergies
                value:
                  resourceType: Bundle
                  type: collection
                  entry:
                    - resource:
                        resourceType: Patient
                        id: pat-123
                        birthDate: "2010-05-15"
                        gender: male
      responses:
        '200':
          description: Bootstrap successful
          content:
            application/fhir+json:: TREAT
        display: "Treatment"
      - system: http://genpres.nl/fhir/CodeSystem/security-label  
        code
        identifier:
          type: array
          items:
            $ref: '#/components/schemas/Identifier'
        birthDate:
          type: string
          format: date
        gender:
          type: string
          enum: [male, female, other, unknown]
        extension:
          type: array
          items:
            $ref: '#/components/schemas/Extension'

    CarePlan:
      type: object
      required: [resourceType, status, intent, subject]
      properties:
        resourceType:
          type: string
          enum: [CarePlan]
        id:
          type: string
          pattern: '^[A-Za-z0-9\-\.]{1,64}
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Treatment Plan Request
  /fhir/$treatment-plan-request:
    post:
      summary: Request treatment plan calculation
      operationId: treatmentPlanRequest
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # Treatment Plan Finalization
  /fhir/$finalize:
    post:
      summary: Finalize treatment plan for EHR implementation
      operationId: finalize
      parameters:
        - name: session-id
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # G-Standard Validation
  /fhir/Medication/$validate-gstandard:
    post:
      summary: Validate medication against G-Standard
      operationId: validateGStandard
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Simulation (Dry Run)
  /fhir/$simulate:
    post:
      summary: Simulate treatment plan without persistence
      operationId: simulate
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

components:
  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        type:
          type: string
          enum: [collection, transaction, searchset, batch]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'
    
    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'
```

### 8.2 OAuth2/SMART-on-FHIR Security Integration

```fsharp
// SMART-on-FHIR security context
type SmartOnFhirSecurity = {
    // OAuth2 Configuration
    AuthorizationServer: string           // EHR authorization server
    TokenEndpoint: string                 // OAuth2 token endpoint
    IntrospectionEndpoint: string         // Token introspection endpoint
    
    // SMART Scopes
    RequiredScopes: SmartScope[]          // Required SMART scopes
    OptionalScopes: SmartScope[]          // Optional enhanced scopes
    
    // Token Management
    AccessToken: AccessToken              // Current access token
    RefreshToken: RefreshToken option     // Refresh token if available
    TokenExpiration: DateTime             // Token expiration time
    
    // FHIR Security
    FhirSecurityLabels: SecurityLabel[]   // FHIR security labels
    AuditRequirements: AuditRequirement[] // IHE ATNA compliance
}

// SMART-on-FHIR scopes for GenPRES integration
type SmartScope =
    | PatientRead                         // patient/*.read
    | PatientWrite                        // patient/*.write
    | UserRead                            // user/*.read
    | UserWrite                           // user/*.write
    | SystemRead                          // system/*.read
    | SystemWrite                         // system/*.write
    | Launch                              // launch
    | LaunchPatient                       // launch/patient
    | FhirUser                            // fhirUser
    | OpenIdProfile                       // profile openid

// OAuth2 token validation for FHIR requests
let validateFhirToken (token: string) : Async<Result<FhirSecurityContext, SecurityError>> =
    async {
        try
            // Introspect token with EHR authorization server
            let! introspectionResult = introspectToken token
            
            match introspectionResult with
            | Error error -> return Error error
            | Ok tokenInfo ->
                
                // Validate required scopes
                let requiredScopes = [
                    "patient/Patient.read"
                    "patient/CarePlan.write" 
                    "patient/MedicationRequest.write"
                    "patient/Medication.read"
                    "user/Practitioner.read"
                ]
                
                let hasRequiredScopes = 
                    requiredScopes 
                    |> List.forall (fun scope -> tokenInfo.Scopes |> List.contains scope)
                
                if not hasRequiredScopes then
                    return Error (InsufficientScopes requiredScopes)
                else
                    let securityContext = {
                        UserId = tokenInfo.Sub
                        PatientId = tokenInfo.Patient
                        Scopes = tokenInfo.Scopes
                        FhirUser = tokenInfo.FhirUser
                        ExpiresAt = tokenInfo.ExpiresAt
                    }
                    return Ok securityContext
        with
        | ex -> return Error (TokenValidationError ex.Message)
    }
```

### 8.3 IHE Profile Compliance

```fsh

// IHE Pharmacy (PHARM) Profile Compliance
Profile: IHE-PHARM-CarePlan
Parent: CarePlan
Title: "IHE Pharmacy CarePlan Profile"
Description: "CarePlan profile compliant with IHE Pharmacy workflow"
- status 1..1 MS
- intent 1..1 MS
- subject 1..1 MS
- activity
  - detail
    - kind 1..1 MS
    - code 1..1 MS
    - status 1..1 MS
    - medication[x] 0..1 MS

// IHE Audit Trail and Node Authentication (ATNA) Compliance
Profile: IHE-ATNA-AuditEvent
Parent: AuditEvent
Title: "IHE ATNA AuditEvent Profile"
Description: "AuditEvent profile for IHE ATNA compliance"
- type 1..1 MS
- action 1..1 MS
- recorded 1..1 MS
- outcome 1..1 MS
- agent 1..* MS
  - type 1..1 MS
  - who 1..1 MS
  - requestor 1..1 MS
- source 1..1 MS
  - observer 1..1 MS
- entity 0..* MS
  - what 0..1 MS
  - type 0..1 MS
  - role 0..1 MS

// IHE Internet User Authorization (IUA) Profile
Profile: IHE-IUA-AccessToken
Title: "IHE IUA Access Token Profile" 
Description: "OAuth2 access token profile for IHE IUA compliance"
- iss (issuer) 1..1 MS
- sub (subject) 1..1 MS  
- aud (audience) 1..1 MS
- exp (expiration) 1..1 MS
- iat (issued at) 1..1 MS
- scope 1..1 MS
- purpose_of_use 0..1 MS
- homeCommunityId 0..1 MS
- national_provider_identifier 0..1 MS
- subject_organization 0..1 MS
- subject_organization_id 0..1 MS
```

## 9. Clinical Safety and Compliance

### 9.1 FHIR-Based Clinical Decision Support

```fsh

// Clinical Decision Support through FHIR CDS Hooks
CDS Hook: medication-prescribe
- hook: medication-prescribe  
- title: "GenPRES Medication Safety Check"
- description: "G-Standard compliant medication safety validation"
- id: "genpres-medication-safety"
- prefetch:
    patient: "Patient/{{context.patientId}}"
    medications: "MedicationRequest?patient={{context.patientId}}&status=active"
    allergies: "AllergyIntolerance?patient={{context.patientId}}"
    conditions: "Condition?patient={{context.patientId}}&clinical-status=active"
    observations: "Observation?patient={{context.patientId}}&category=vital-signs&_sort=-date&_count=10"

// CDS Card response for safety alerts
CDSCard: safety-alert
- summary: "G-Standard safety alert"
- detail: "Detailed safety information from G-Standard validation"
- indicator: "warning" | "info" | "critical"
- source:
  - label: "G-Standard"
  - url: "http://gstandard.nl"
  - icon: "http://gstandard.nl/icon.png"
- suggestions:
  - label: "Alternative medication"
  - actions:
    - type: "create"
    - description: "Add alternative medication"
    - resource: MedicationRequest

// DetectedIssue resource for clinical alerts
DetectedIssue: clinical-safety-alert
- status: preliminary | final | entered-in-error
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActCode
    - code: DRG | ALLERGY | DOSEAMT | DOSEDUR | DOSECOND
- severity: high | moderate | low
- patient: Reference(Patient)
- detail: "Human readable description of the issue"
- reference: "Reference to related resources"
- mitigation:
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActCode  
      - code: MONITOR | REPLACE | REDUCE | DISCONTINUE
  - description: "Description of mitigation action"
```

### 9.2 G-Standard Safety Validation

```fsharp
// G-Standard safety validation through FHIR operations
type GStandardSafetyValidation = {
    // Validation Operations
    ProductValidation: FhirOperation       // Validate product GPK codes
    DoseRuleValidation: FhirOperation      // Validate dose rules compliance
    InteractionCheck: FhirOperation        // Check drug interactions
    ContraindicationCheck: FhirOperation   // Check contraindications
    
    // Safety Assessment
    RiskAssessment: RiskAssessment         // Overall safety risk assessment
    SafetyProfile: Extension               // Comprehensive safety profile
    
    // Compliance Reporting
    ComplianceReport: Parameters           // G-Standard compliance report
    ValidationOutcome: OperationOutcome    // Validation results
}

// FHIR OperationDefinition for comprehensive safety validation
OperationDefinition: comprehensive-safety-check
- name: comprehensive-safety-check
- status: active
- kind: operation
- code: comprehensive-safety-check
- resource: [CarePlan]
- system: false
- type: false  
- instance: true
- parameter:
  - name: gstandard-version
    use: in
    min: 0
    max: 1
    type: string
  - name: validation-level  
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: safety-report
    use: out
    min: 1
    max: 1 
    type: Parameters
  - name: detected-issues
    use: out
    min: 0
    max: "*"
    type: DetectedIssue
  - name: risk-assessment
    use: out  
    min: 0
    max: 1
    type: RiskAssessment

// RiskAssessment resource for overall safety evaluation  
RiskAssessment: treatment-plan-safety
- status: final
- subject: Reference(Patient)
- encounter: Reference(Encounter) 
- performedDateTime: {assessment-timestamp}
- performer: Reference(Device) # GenPRES system
- code:
  - coding:
    - system: http://snomed.info/sct
    - code: 225338004
    - display: "Risk assessment"
- prediction:
  - outcome:
    - coding:
      - system: http://snomed.info/sct  
      - code: 281647001
      - display: "Adverse reaction"
  - probabilityDecimal: 0.15
  - relativeRisk: 1.2
- mitigation: "Recommended monitoring and precautions"
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/gstandard-risk-factors
    extension:
      - url: age-risk
        valueDecimal: 0.1
      - url: weight-risk  
        valueDecimal: 0.05
      - url: interaction-risk
        valueDecimal: 0.3
```

### 9.3 Pediatric Safety Specializations

```fsh

// Pediatric-specific FHIR profiles and extensions
Profile: PediatricMedicationRequest
Parent: MedicationRequest
Title: "Pediatric Medication Request"
Description: "MedicationRequest profile with pediatric safety requirements"
- subject 1..1 MS
- subject only Reference(PediatricPatient)
- dosageInstruction 1..* MS
  - doseAndRate
    - dose[x] 1..1 MS
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
        extension:
          - url: dose-per-kg
            valueQuantity 1..1 MS
          - url: patient-weight  
            valueQuantity 1..1 MS
          - url: weight-source
            valueCode 1..1 MS # measured | estimated | calculated
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/pediatric-safety-check
    extension:
      - url: age-appropriate
        valueBoolean 1..1 MS
      - url: weight-appropriate
        valueBoolean 1..1 MS  
      - url: concentration-safe
        valueBoolean 1..1 MS
      - url: volume-appropriate
        valueBoolean 1..1 MS

// Age-specific contraindication checking
CodeSystem: pediatric-age-groups
- url: http://genpres.nl/fhir/CodeSystem/pediatric-age-groups
- concept:
  - code: neonate
    display: "Neonate (0-28 days)"
    definition: "Newborn infant up to 28 days of age"
  - code: infant  
    display: "Infant (29 days - 2 years)"
    definition: "Infant from 29 days to 2 years of age"
  - code: child
    display: "Child (2-12 years)" 
    definition: "Child from 2 to 12 years of age"
  - code: adolescent
    display: "Adolescent (12-18 years)"
    definition: "Adolescent from 12 to 18 years of age"

// Pediatric dose rule validation
Extension: pediatric-dose-rule
- url: http://genpres.nl/fhir/StructureDefinition/pediatric-dose-rule
- extension:
  - url: rule-id
    valueString 1..1 MS
  - url: age-group  
    valueCode 1..1 MS # From pediatric-age-groups CodeSystem
  - url: weight-range
    extension:
      - url: min-weight
        valueQuantity 0..1 MS
      - url: max-weight 
        valueQuantity 0..1 MS
  - url: dose-calculation
    extension:
      - url: dose-per-kg
        valueQuantity 0..1 MS
      - url: dose-per-m2
        valueQuantity 0..1 MS  
      - url: max-single-dose
        valueQuantity 0..1 MS
      - url: max-daily-dose
        valueQuantity 0..1 MS
  - url: safety-considerations
    valueString 0..* MS
```

## 10. Performance and Scalability

### 10.1 FHIR Performance Optimization

```fsharp
// FHIR-specific performance optimization strategies
type FhirPerformanceOptimization = {
    // Bundle Optimization
    BundleSize: BundleSizeStrategy         // Optimal bundle sizes
    BundleProcessing: BundleProcessingStrategy // Parallel vs sequential
    
    // Resource Optimization  
    ResourceMinimization: ResourceMinimizationStrategy // Include only necessary elements
    ReferenceOptimization: ReferenceStrategy // Contained vs external references
    
    // Search Optimization
    SearchParameterOptimization: SearchStrategy // Efficient FHIR searches
    IncludeRevIncludeOptimization: IncludeStrategy // Optimal _include/_revinclude usage
    
    // Caching Strategy
    ResourceCaching: CachingStrategy       // Cache frequently accessed resources
    BundleCaching: CachingStrategy         // Cache complete bundles
    OperationCaching: CachingStrategy      // Cache operation results
}

// Bundle size optimization for treatment plans
type BundleSizeStrategy = {
    MaxBundleSize: int                     // Maximum entries per bundle
    ResourceTypeGrouping: bool             // Group similar resources  
    DependencyOrdering: bool               // Order resources by dependencies
    ChunkingStrategy: ChunkingMethod       // How to split large bundles
}

type ChunkingMethod =
    | ByResourceCount of int               // Split by number of resources
    | ByResourceType                       // Split by resource type
    | ByDependencyGraph                    // Split maintaining dependencies
    | ByPayloadSize of int                 // Split by payload size in KB

// FHIR search optimization for patient context
let optimizedPatientContextQuery (patientId: string) =
    $"""
    Patient/{patientId}?
    _include=Patient:organization&
    _include=Patient:general-practitioner&
    _revinclude=Observation:patient&
    _revinclude=AllergyIntolerance:patient&
    _revinclude=Condition:patient&
    _revinclude=MedicationRequest:patient&
    _count=50&
    _sort=-_lastUpdated
    """

// Resource minimization through FHIR _elements parameter  
let minimizePatientResource = "_elements=id,identifier,birthDate,gender,extension"
let minimizeObservationResource = "_elements=id,status,code,subject,value,effective"
let minimizeMedicationRequestResource = "_elements=id,status,intent,medication,subject,dosageInstruction"
```

### 10.2 Scalability Architecture

```fsh

// FHIR server scalability configuration
CapabilityStatement: genpres-capability
- status: active
- date: "2026-01-15"
- publisher: "GenPRES"
- kind: instance
- software:
  - name: "GenPRES FHIR Server"
  - version: "2.1.0"
- implementation:
  - description: "GenPRES Treatment Planning FHIR Server"  
  - url: "https://api.genpres.nl/fhir"
- fhirVersion: 4.0.1
- format: [json, xml]
- rest:
  - mode: server
  - security:
    - cors: true
    - service:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/restful-security-service
        - code: OAuth
        - display: "OAuth2 using SMART-on-FHIR profile"
  - resource:
    - type: Patient
      interaction: [read, search-type]
      searchParam:
        - name: identifier
          type: token
        - name: birthdate  
          type: date
    - type: CarePlan
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      conditionalUpdate: true
      searchParam:
        - name: patient
          type: reference
        - name: status
          type: token
        - name: date
          type: date
    - type: MedicationRequest  
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      searchParam:
        - name: patient
          type: reference
        - name: medication
          type: reference  
        - name: status
          type: token
  - operation:
    - name: bootstrap
      definition: http://genpres.nl/fhir/OperationDefinition/bootstrap
    - name: treatment-plan-request
      definition: http://genpres.nl/fhir/OperationDefinition/treatment-plan-request
    - name: finalize
      definition: http://genpres.nl/fhir/OperationDefinition/finalize
    - name: validate-gstandard  
      definition: http://genpres.nl/fhir/OperationDefinition/validate-gstandard
```

### 10.3 Performance Monitoring

```fsharp
// FHIR-based performance monitoring
type FhirPerformanceMonitoring = {
    // Operation Performance
    OperationMetrics: OperationMetric[]    // Performance per FHIR operation
    ResourceMetrics: ResourceMetric[]      // Performance per resource type
    BundleMetrics: BundleMetric[]          // Bundle processing performance
    
    // Quality Metrics
    ValidationMetrics: ValidationMetric[]  // G-Standard validation performance
    SafetyMetrics: SafetyMetric[]          // Safety check performance
    
    // System Metrics  
    ServerMetrics: ServerMetric[]          // FHIR server performance
    DatabaseMetrics: DatabaseMetric[]     // Database performance
    CacheMetrics: CacheMetric[]           // Cache performance
}

// Performance metrics as FHIR Observation resources
let createPerformanceObservation (operationType: string) (duration: float) =
    {
        ResourceType = "Observation"
        Status = ObservationStatus.Final
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/observation-category"
                        Code = Some "performance-metric"
                        Display = Some "Performance Metric"
                    }
                ]
            }
        ]
        Code = {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                    Code = Some "operation-duration"  
                    Display = Some "Operation Duration"
                }
            ]
        }
        Component = [
            {
                Code = {
                    Coding = [
                        {
                            System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                            Code = Some "operation-type"
                            Display = Some "Operation Type"
                        }
                    ]
                }
                ValueString = Some operationType
            }
        ]
        ValueQuantity = Some {
            Value = Some duration
            Unit = Some "ms"
            System = Some "http://unitsofmeasure.org"
            Code = Some "ms"
        }
        EffectiveDateTime = Some DateTime.UtcNow
        Device = Some {
            Display = Some "GenPRES Performance Monitor"
        }
    }
```

## 11. Security and Privacy

### 11.1 OAuth2/SMART-on-FHIR Security

```fsh

// OAuth2 Authorization Server Metadata (RFC 8414)
OAuth2Metadata: genpres-oauth2
- issuer: "https://auth.genpres.nl"
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"  
- introspection_endpoint: "https://auth.genpres.nl/oauth2/introspect"
- jwks_uri: "https://auth.genpres.nl/.well-known/jwks.json"
- scopes_supported: [
    "patient/*.read",
    "patient/*.write", 
    "user/*.read",
    "user/*.write",
    "system/*.read",
    "system/*.write",
    "launch",
    "launch/patient",
    "fhirUser",
    "openid",
    "profile"
  ]
- response_types_supported: ["code", "token", "id_token"]
- grant_types_supported: ["authorization_code", "client_credentials", "refresh_token"]
- code_challenge_methods_supported: ["S256"]
- token_endpoint_auth_methods_supported: ["client_secret_basic", "client_secret_post", "private_key_jwt"]

// SMART-on-FHIR Well-Known Endpoint
SMARTConfiguration: smart-configuration  
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"
- capabilities: [
  "launch-ehr", 
  "launch-standalone",
  "client-public",
  "client-confidential-symmetric", 
  "sso-openid-connect",
  "context-passthrough-banner",
  "context-passthrough-style",
  "context-ehr-patient",
  "context-ehr-encounter",
  "permission-offline",
  "permission-patient",
  "permission-user"
]
```

### 11.2 FHIR Security Labels and Access Control

```fsh

// Security labels for FHIR resources
CodeSystem: security-labels
- url: http://genpres.nl/fhir/CodeSystem/security-label
- concept:
  - code: PEDS
    display: "Pediatric Data"
    definition: "Data specific to pediatric patients requiring special handling"
  - code: GSTAND
    display: "G-Standard Data" 
    definition: "Data validated against G-Standard requiring compliance tracking"
  - code: CALC
    display: "Calculated Data"
    definition: "Data generated by GenPRES calculations"
  - code: SENS
    display: "Sensitive Clinical Data"
    definition: "Highly sensitive clinical information requiring restricted access"

// Security labeling for CarePlan resources
CarePlan: secure-treatment-plan
- meta:
    security:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
        code: GSTAND
        display: "G-Standard Data"
      - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
        code: N
        display: "Normal"

// Consent resource for treatment plan data usage
Consent: treatment-plan-consent
- status: active
- scope:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentscope
    - code: treatment
    - display: "Treatment"
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentcategorycodes
    - code: medical
    - display: "Medical Consent"
- patient: Reference(Patient)
- dateTime: "2026-01-15T10:00:00Z"
- performer: [Reference(Patient)]
- policy:
  - authority: "https://genpres.nl/privacy-policy"
  - uri: "https://genpres.nl/consent/treatment-planning"
- provision:
  - type: permit
  - purpose:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
    - code: TREAT
    - display: "Treatment"
  - class:
    - system: http://hl7.org/fhir/resource-types
    - code: CarePlan
  - class:
    - system: http://hl7.org/fhir/resource-types  
    - code: MedicationRequest
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: collect
      - display: "Collect"
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: use
      - display: "Use"
```

### 11.3 Audit Trail and Compliance

```fsh

// IHE ATNA compliant audit events
AuditEvent: treatment-plan-access
- type:
  - system: http://dicom.nema.org/resources/ontology/DCM
  - code: "110112"
  - display: "Query"
- action: E # Execute/Perform
- recorded: "2026-01-15T10:30:00Z"
- outcome: "0" # Success
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: humanuser
      - display: "Human User"
  - who:
    - reference: "Practitioner/dr-smith"
    - display: "Dr. John Smith"
  - requestor: true
  - location: Reference(Location/icu-room-101)
  - policy: ["http://genpres.nl/policy/treatment-planning-access"]
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: application
      - display: "Application"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/application-id"
      - value: "genpres-treatment-planner"
    - display: "GenPRES Treatment Planner"
  - requestor: false
- source:
  - observer:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/system-id"  
      - value: "genpres-fhir-server"
    - display: "GenPRES FHIR Server"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/security-source-type
    - code: "4"
    - display: "Application Server"
- entity:
  - what:
    - reference: "Patient/pat-123"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: "1"
    - display: "Person"
  - role:
    - system: http://terminology.hl7.org/CodeSystem/object-role
    - code: "1" 
    - display: "Patient"
  - name: "Treatment plan calculation for pediatric patient"
  - securityLabel:
    - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
    - code: N
    - display: "Normal"

// Provenance tracking for calculated treatment plans
Provenance: treatment-plan-calculation
- target:
  - reference: "CarePlan/calculated-plan-001"
- target:
  - reference: "MedicationRequest/calculated-medreq-001"
- recorded: "2026-01-15T10:30:00Z"
- policy: ["http://genpres.nl/policy/calculation-provenance"]
- location: Reference(Location/genpres-datacenter)
- activity:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-DataOperation
    - code: CREATE
    - display: "Create"
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
      - code: performer
      - display: "Performer"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/calculation-engine"
      - value: "genpres-calc-engine-v2.1"
    - display: "GenPRES Calculation Engine v2.1"
  - onBehalfOf:
    - reference: "Practitioner/dr-smith"
- entity:
  - role: source
  - what:
    - reference: "Bundle/patient-context-bundle"
    - display: "Patient context bundle used for calculation"
  - agent:
    - type:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
        - code: enterer
        - display: "Enterer"  
    - who:
      - reference: "Device/ehr-system"
      - display: "Hospital EHR System"
- signature:
  - type:
    - system: urn:iso-astm:E1762-95:2013
    - code: 1.2.840.10065.1.12.1.1
    - display: "Author's Signature"
  - when: "2026-01-15T10:30:00Z"
  - who:
    - reference: "Device/genpres-system"
  - data: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." # Base64 encoded signature
```

## 12. Implementation Considerations

### 12.1 FHIR Implementation Guide

```fsh

// Implementation Guide definition
ImplementationGuide: genpres-treatment-planning
- url: "http://genpres.nl/fhir/ImplementationGuide/treatment-planning"
- version: "1.2.0" 
- name: "GenPRESTreatmentPlanning"
- title: "GenPRES Treatment Planning Implementation Guide"
- status: active
- publisher: "GenPRES"
- description: "FHIR R4 implementation guide for GenPRES treatment planning integration"
- packageId: "nl.genpres.treatmentplanning"
- license: CC0-1.0
- fhirVersion: [4.0.1]
- dependsOn:
  - uri: "http://hl7.org/fhir"
    version: "4.0.1"
  - uri: "http://hl7.org/fhir/uv/sdc"
    version: "3.0.0"
  - uri: "http://hl7.org/fhir/uv/cpg"  
    version: "2.0.0"
- definition:
  - resource:
    - reference:
      - reference: "StructureDefinition/genpres-patient"
      - name: "GenPRES Patient Profile"
      - description: "Patient profile with Dutch healthcare extensions"
    - reference:
      - reference: "StructureDefinition/genpres-careplan"
      - name: "GenPRES CarePlan Profile" 
      - description: "Treatment plan as FHIR CarePlan"
    - reference:
      - reference: "StructureDefinition/genpres-medicationrequest"
      - name: "GenPRES MedicationRequest Profile"
      - description: "Medication request with G-Standard compliance"
  - page:
    - nameUrl: "index.html"
    - title: "GenPRES Treatment Planning IG"
    - generation: html
    - page:
      - nameUrl: "profiles.html" 
      - title: "FHIR Profiles"
      - generation: html
    - page:
      - nameUrl: "extensions.html"
      - title: "FHIR Extensions"  
      - generation: html
    - page:
      - nameUrl: "terminology.html"
      - title: "Terminology"
      - generation: html
    - page:
      - nameUrl: "examples.html"
      - title: "Examples"
      - generation: html
```

### 12.2 Testing and Validation

```fsharp
// FHIR validation and testing framework
type FhirTestingFramework = {
    // Profile Validation
    ProfileValidation: ProfileValidationConfig // FHIR profile validation
    SchemaValidation: SchemaValidationConfig   // FHIR schema validation
    TerminologyValidation: TerminologyValidationConfig // Terminology validation
    
    // Integration Testing
    OperationTesting: OperationTestConfig      // FHIR operation testing
    WorkflowTesting: WorkflowTestConfig        // End-to-end workflow testing
    PerformanceTesting: PerformanceTestConfig  // Performance testing
    
    // Compliance Testing
    G-StandardTesting: GStandardTestConfig     // G-Standard compliance testing
    SecurityTesting: SecurityTestConfig        // OAuth2/SMART security testing
    IHETesting: IHETestConfig                  // IHE profile compliance testing
}

// FHIR TestScript resources for automated testing
TestScript: bootstrap-operation-test
- url: "http://genpres.nl/fhir/TestScript/bootstrap-operation-test"
- version: "1.0.0"
- name: "BootstrapOperationTest"
- status: active
- title: "Test bootstrap operation with patient context"
- description: "Validates the $bootstrap operation with complete patient context"
- origin:
  - index: 1
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-origin-types"
    - code: "FHIR-Client"
- destination:
  - index: 1  
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-destination-types"
    - code: "FHIR-Server"
- fixture:
  - id: "patient-context-bundle"
  - autocreate: false
  - autodelete: false
  - resource:
    - reference: "Bundle/patient-context-example"
- variable:
  - name: "patientId"
  - defaultValue: "pat-123"
  - sourceId: "patient-context-bundle"
  - expression: "Bundle.entry.where(resource is Patient).resource.id"
- test:
  - id: "bootstrap-success"
  - name: "Bootstrap Operation Success"
  - description: "Test successful bootstrap operation"
  - action:
    - operation:
      - type:
        - system: "http://terminology.hl7.org/CodeSystem/testscript-operation-codes"
        - code: "create"
      - resource: "Bundle"
      - description: "Execute bootstrap operation"  
      - destination: 1
      - origin: 1
      - params: "/$bootstrap"
      - sourceId: "patient-context-bundle"
    - assert:
      - description: "Confirm that the returned HTTP status is 200(OK)"
      - direction: response
      - responseCode: "200"
    - assert:
      - description: "Confirm that the response is an OperationOutcome"
      - direction: response
      - resource: "OperationOutcome"
    - assert:
      - description: "Confirm successful outcome"
      - direction: response
      - expression: "OperationOutcome.issue.where(severity='information').exists()"

// Example test data for FHIR testing
Bundle: patient-context-test-data
- resourceType: Bundle
- type: collection
- entry:
  - resource:
    - resourceType: Patient
    - id: test-patient-001
    - identifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
      - value: "999999999"
    - birthDate: "2015-03-15"
    - gender: male
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
        valueQuantity:
          value: 39
          unit: weeks
          system: http://unitsofmeasure.org
          code: wk
  - resource:
    - resourceType: Observation
    - status: final
    - category:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/observation-category
        - code: vital-signs
    - code:
      - coding:
        - system: http://loinc.org
        - code: "29463-7"
        - display: "Body weight"
    - subject:
      - reference: "Patient/test-patient-001"
    - valueQuantity:
      - value: 18.5
      - unit: kg
      - system: http://unitsofmeasure.org  
      - code: kg
```

### 12.3 Data Migration and Integration

```fsharp
// FHIR data migration strategies
type FhirDataMigration = {
    // Legacy Data Transformation
    LegacyToFhir: TransformationConfig     // Transform legacy data to FHIR
    GStandardMapping: MappingConfig        // Map G-Standard data to FHIR
    
    // Data Validation
    MigrationValidation: ValidationConfig  // Validate migrated data
    QualityAssurance: QualityConfig        // Data quality checks
    
    // Incremental Migration
    IncrementalSync: SyncConfig            // Incremental data synchronization
    ConflictResolution: ConflictConfig     // Handle data conflicts
}

// FHIR ConceptMap for G-Standard to FHIR mapping
ConceptMap: gstandard-to-fhir-medication
- url: "http://genpres.nl/fhir/ConceptMap/gstandard-to-fhir-medication"
- version: "1.0.0"
- name: "GStandardToFHIRMedication" 
- title: "G-Standard to FHIR Medication Mapping"
- status: active
- publisher: "GenPRES"
- description: "Mapping G-Standard medication codes to FHIR Medication resource elements"
- sourceUri: "http://gstandard.nl/fhir/CodeSystem/gpk"
- targetUri: "http://hl7.org/fhir/StructureDefinition/Medication"
- group:
  - source: "http://gstandard.nl/fhir/CodeSystem/gpk"
  - target: "http://snomed.info/sct"
  - element:
    - code: "12345" # GPK code
    - display: "Paracetamol tablet 500mg"
    - target:
      - code: "387517004"
      - display: "Paracetamol"
      - equivalence: equivalent
  - element:
    - code: "67890" # GPK code  
    - display: "Amoxicillin capsule 250mg"
    - target:
      - code: "27658006"
      - display: "Amoxicillin"
      - equivalence: equivalent

// FHIR StructureMap for data transformation
StructureMap: legacy-patient-to-fhir
- url: "http://genpres.nl/fhir/StructureMap/legacy-patient-to-fhir"
- version: "1.0.0"
- name: "LegacyPatientToFHIR"
- status: active
- title: "Transform legacy patient data to FHIR Patient"
- description: "Transforms legacy EHR patient data to FHIR Patient resource"
- structure:
  - url: "http://genpres.nl/fhir/StructureDefinition/legacy-patient"
  - mode: source
  - alias: "LegacyPatient"
- structure:
  - url: "http://hl7.org/fhir/StructureDefinition/Patient"  
  - mode: target
  - alias: "FHIRPatient"
- group:
  - name: "patient"
  - typeMode: none
  - input:
    - name: "source"
    - type: "LegacyPatient"
    - mode: source
  - input:
    - name: "target"
    - type: "FHIRPatient" 
    - mode: target
  - rule:
    - name: "patient-id"
    - source:
      - context: "source"
      - element: "patientId"
      - variable: "pid"
    - target:
      - context: "target"
      - contextType: variable
      - element: "id"
      - transform: copy
      - parameter:
        - valueId: "pid"
  - rule:
    - name: "birth-date"
    - source:
      - context: "source"
      - element: "dateOfBirth" 
      - variable: "dob"
    - target:
      - context: "target"
      - contextType: variable
      - element: "birthDate"
      - transform: copy
      - parameter:
        - valueId: "dob"
```

## 13. Appendices

### Appendix A: FHIR Profiles and Extensions

```fsh

// Complete FHIR profile definitions
StructureDefinition: GenPRESPatient
- url: http://genpres.nl/fhir/StructureDefinition/genpres-patient
- version: 1.2.0
- name: GenPRESPatient  
- title: GenPRES Patient Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: Patient
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Patient
- derivation: constraint
- differential:
  - element:
    - id: Patient.identifier
    - path: Patient.identifier
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: Patient.identifier:BSN
    - path: Patient.identifier
    - sliceName: BSN
    - min: 0
    - max: 1
    - type:
      - code: Identifier
    - patternIdentifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
  - element:  
    - id: Patient.birthDate
    - path: Patient.birthDate
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: Patient.extension:gestationalAge
    - path: Patient.extension
    - sliceName: gestationalAge
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gestational-age]

StructureDefinition: GenPRESCarePlan
- url: http://genpres.nl/fhir/StructureDefinition/genpres-careplan
- version: 1.2.0
- name: GenPRESCarePlan
- title: GenPRES CarePlan Profile  
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: CarePlan
- baseDefinition: http://hl7.org/fhir/StructureDefinition/CarePlan
- derivation: constraint
- differential:
  - element:
    - id: CarePlan.status
    - path: CarePlan.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: CarePlan.intent
    - path: CarePlan.intent  
    - min: 1
    - max: 1
    - fixedCode: plan
    - mustSupport: true
  - element:
    - id: CarePlan.subject
    - path: CarePlan.subject
    - min: 1  
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: CarePlan.extension:gstandardCompliance
    - path: CarePlan.extension
    - sliceName: gstandardCompliance
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-compliance]

StructureDefinition: GenPRESMedicationRequest
- url: http://genpres.nl/fhir/StructureDefinition/genpres-medicationrequest
- version: 1.2.0
- name: GenPRESMedicationRequest
- title: GenPRES MedicationRequest Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: MedicationRequest
- baseDefinition: http://hl7.org/fhir/StructureDefinition/MedicationRequest
- derivation: constraint
- differential:
  - element:
    - id: MedicationRequest.status
    - path: MedicationRequest.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.intent
    - path: MedicationRequest.intent
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.medication[x]
    - path: MedicationRequest.medication[x]
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-medication]
    - mustSupport: true
  - element:
    - id: MedicationRequest.subject  
    - path: MedicationRequest.subject
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: MedicationRequest.dosageInstruction
    - path: MedicationRequest.dosageInstruction
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: MedicationRequest.extension:weightBasedDose
    - path: MedicationRequest.extension
    - sliceName: weightBasedDose
    - min: 0
    - max: 1  
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/weight-based-dose]
  - element:
    - id: MedicationRequest.extension:gstandardDoseRule
    - path: MedicationRequest.extension
    - sliceName: gstandardDoseRule
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule]
```

### Appendix B: G-Standard Integration Specifications

```fsh

// G-Standard specific extensions
StructureDefinition: GStandardCompliance
- url: http://genpres.nl/fhir/StructureDefinition/gstandard-compliance
- version: 1.2.0
- name: GStandardCompliance
- title: G-Standard Compliance Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: CarePlan
  - type: element
  - expression: MedicationRequest
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: G-Standard compliance information
    - definition: Detailed information about G-Standard compliance validation
  - element:
    - id: Extension.extension:level
    - path: Extension.extension
    - sliceName: level
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:level.url
    - path: Extension.extension.url
    - fixedUri: level
  - element:
    - id: Extension.extension:level.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/compliance-level
  - element:
    - id: Extension.extension:validationTimestamp
    - path: Extension.extension
    - sliceName: validationTimestamp
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:validationTimestamp.url
    - path: Extension.extension.url
    - fixedUri: validation-timestamp
  - element:
    - id: Extension.extension:validationTimestamp.value[x]
    - path: Extension.extension.value[x]  
    - type:
      - code: dateTime
  - element:
    - id: Extension.extension:gstandardVersion
    - path: Extension.extension
    - sliceName: gstandardVersion
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:gstandardVersion.url
    - path: Extension.extension.url
    - fixedUri: gstandard-version
  - element:
    - id: Extension.extension:gstandardVersion.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: string

StructureDefinition: WeightBasedDose
- url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
- version: 1.2.0
- name: WeightBasedDose
- title: Weight-Based Dose Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: MedicationRequest
  - type: element  
  - expression: Dosage
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: Weight-based dosing information
    - definition: Information about weight-based dose calculation
  - element:
    - id: Extension.extension:dosePerKg
    - path: Extension.extension
    - sliceName: dosePerKg
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:dosePerKg.url
    - path: Extension.extension.url
    - fixedUri: dose-per-kg
  - element:
    - id: Extension.extension:dosePerKg.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:patientWeight
    - path: Extension.extension
    - sliceName: patientWeight
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:patientWeight.url
    - path: Extension.extension.url
    - fixedUri: patient-weight
  - element:
    - id: Extension.extension:patientWeight.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:weightSource
    - path: Extension.extension
    - sliceName: weightSource
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:weightSource.url
    - path: Extension.extension.url
    - fixedUri: weight-source  
  - element:
    - id: Extension.extension:weightSource.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/weight-source
```

### Appendix C: API Documentation

```yaml
## Complete OpenAPI 3.0 specification
openapi: 3.0.3
info:
  title: GenPRES FHIR Treatment Planning API
  description: |
    FHIR R4 compliant API for GenPRES treatment planning integration.
    Supports G-Standard compliance, OAuth2/SMART-on-FHIR security,
    and IHE Pharmacy profile compliance.
  version: 1.2.0
  contact:
    name: GenPRES API Support
    url: https://support.genpres.nl
    email: api-support@genpres.nl
  license:
    name: Proprietary
    url: https://genpres.nl/license

servers:
  - url: https://api.genpres.nl/fhir
    description: Production FHIR server
  - url: https://staging-api.genpres.nl/fhir  
    description: Staging FHIR server
  - url: https://dev-api.genpres.nl/fhir
    description: Development FHIR server

security:
  - OAuth2: [patient/*.read, patient/*.write, user/*.read]
  - BearerAuth: []

paths:
  /$bootstrap:
    post:
      tags: [Treatment Planning]
      summary: Initialize patient context for treatment planning
      description: |
        Bootstrap operation that establishes patient context for treatment planning.
        Accepts a Bundle containing Patient and related clinical resources.
      operationId: bootstrap
      security:
        - OAuth2: [patient/*.read, launch, launch/patient]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            examples:
              patient-context:
                summary: Patient context with vital signs and allergies
                value:
                  resourceType: Bundle
                  type: collection
                  entry:
                    - resource:
                        resourceType: Patient
                        id: pat-123
                        birthDate: "2010-05-15"
                        gender: male
      responses:
        '200':
          description: Bootstrap successful
          content:
            application/fhir+json:: TREAT
        display: "Treatment"
      - system: http://genpres.nl/fhir/CodeSystem/security-label  
        code
        status:
          type: string
          enum: [draft, active, on-hold, revoked, completed, entered-in-error, unknown]
        intent:
          type: string
          enum: [proposal, plan, order, option]
        subject:
          $ref: '#/components/schemas/Reference'
        activity:
          type: array
          items:
            $ref: '#/components/schemas/CarePlanActivity'
        extension:
          type: array
          items:
            $ref: '#/components/schemas/Extension'

    CarePlanActivity:
      type: object
      properties:
        reference:
          $ref: '#/components/schemas/Reference'
        detail:
          $ref: '#/components/schemas/CarePlanActivityDetail'

    CarePlanActivityDetail:
      type: object
      properties:
        kind:
          type: string
          enum: [Appointment, CommunicationRequest, DeviceRequest, DiagnosticReport, ImmunizationRecommendation, MedicationRequest, NutritionOrder, Observation, ProcedureRequest, ReferralRequest, RequestGroup, ServiceRequest, SupplyRequest, Task, VisionPrescription]
        code:
          $ref: '#/components/schemas/CodeableConcept'
        status:
          type: string
          enum: [not-started, scheduled, in-progress, on-hold, completed, cancelled, stopped, unknown, entered-in-error]
        description:
          type: string

    MedicationRequest:
      type: object
      required: [resourceType, status, intent, subject]
      properties:
        resourceType:
          type: string
          enum: [MedicationRequest]
        id:
          type: string
          pattern: '^[A-Za-z0-9\-\.]{1,64}
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Treatment Plan Request
  /fhir/$treatment-plan-request:
    post:
      summary: Request treatment plan calculation
      operationId: treatmentPlanRequest
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # Treatment Plan Finalization
  /fhir/$finalize:
    post:
      summary: Finalize treatment plan for EHR implementation
      operationId: finalize
      parameters:
        - name: session-id
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # G-Standard Validation
  /fhir/Medication/$validate-gstandard:
    post:
      summary: Validate medication against G-Standard
      operationId: validateGStandard
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Simulation (Dry Run)
  /fhir/$simulate:
    post:
      summary: Simulate treatment plan without persistence
      operationId: simulate
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

components:
  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        type:
          type: string
          enum: [collection, transaction, searchset, batch]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'
    
    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'
```

### 8.2 OAuth2/SMART-on-FHIR Security Integration

```fsharp
// SMART-on-FHIR security context
type SmartOnFhirSecurity = {
    // OAuth2 Configuration
    AuthorizationServer: string           // EHR authorization server
    TokenEndpoint: string                 // OAuth2 token endpoint
    IntrospectionEndpoint: string         // Token introspection endpoint
    
    // SMART Scopes
    RequiredScopes: SmartScope[]          // Required SMART scopes
    OptionalScopes: SmartScope[]          // Optional enhanced scopes
    
    // Token Management
    AccessToken: AccessToken              // Current access token
    RefreshToken: RefreshToken option     // Refresh token if available
    TokenExpiration: DateTime             // Token expiration time
    
    // FHIR Security
    FhirSecurityLabels: SecurityLabel[]   // FHIR security labels
    AuditRequirements: AuditRequirement[] // IHE ATNA compliance
}

// SMART-on-FHIR scopes for GenPRES integration
type SmartScope =
    | PatientRead                         // patient/*.read
    | PatientWrite                        // patient/*.write
    | UserRead                            // user/*.read
    | UserWrite                           // user/*.write
    | SystemRead                          // system/*.read
    | SystemWrite                         // system/*.write
    | Launch                              // launch
    | LaunchPatient                       // launch/patient
    | FhirUser                            // fhirUser
    | OpenIdProfile                       // profile openid

// OAuth2 token validation for FHIR requests
let validateFhirToken (token: string) : Async<Result<FhirSecurityContext, SecurityError>> =
    async {
        try
            // Introspect token with EHR authorization server
            let! introspectionResult = introspectToken token
            
            match introspectionResult with
            | Error error -> return Error error
            | Ok tokenInfo ->
                
                // Validate required scopes
                let requiredScopes = [
                    "patient/Patient.read"
                    "patient/CarePlan.write" 
                    "patient/MedicationRequest.write"
                    "patient/Medication.read"
                    "user/Practitioner.read"
                ]
                
                let hasRequiredScopes = 
                    requiredScopes 
                    |> List.forall (fun scope -> tokenInfo.Scopes |> List.contains scope)
                
                if not hasRequiredScopes then
                    return Error (InsufficientScopes requiredScopes)
                else
                    let securityContext = {
                        UserId = tokenInfo.Sub
                        PatientId = tokenInfo.Patient
                        Scopes = tokenInfo.Scopes
                        FhirUser = tokenInfo.FhirUser
                        ExpiresAt = tokenInfo.ExpiresAt
                    }
                    return Ok securityContext
        with
        | ex -> return Error (TokenValidationError ex.Message)
    }
```

### 8.3 IHE Profile Compliance

```fsh

// IHE Pharmacy (PHARM) Profile Compliance
Profile: IHE-PHARM-CarePlan
Parent: CarePlan
Title: "IHE Pharmacy CarePlan Profile"
Description: "CarePlan profile compliant with IHE Pharmacy workflow"
- status 1..1 MS
- intent 1..1 MS
- subject 1..1 MS
- activity
  - detail
    - kind 1..1 MS
    - code 1..1 MS
    - status 1..1 MS
    - medication[x] 0..1 MS

// IHE Audit Trail and Node Authentication (ATNA) Compliance
Profile: IHE-ATNA-AuditEvent
Parent: AuditEvent
Title: "IHE ATNA AuditEvent Profile"
Description: "AuditEvent profile for IHE ATNA compliance"
- type 1..1 MS
- action 1..1 MS
- recorded 1..1 MS
- outcome 1..1 MS
- agent 1..* MS
  - type 1..1 MS
  - who 1..1 MS
  - requestor 1..1 MS
- source 1..1 MS
  - observer 1..1 MS
- entity 0..* MS
  - what 0..1 MS
  - type 0..1 MS
  - role 0..1 MS

// IHE Internet User Authorization (IUA) Profile
Profile: IHE-IUA-AccessToken
Title: "IHE IUA Access Token Profile" 
Description: "OAuth2 access token profile for IHE IUA compliance"
- iss (issuer) 1..1 MS
- sub (subject) 1..1 MS  
- aud (audience) 1..1 MS
- exp (expiration) 1..1 MS
- iat (issued at) 1..1 MS
- scope 1..1 MS
- purpose_of_use 0..1 MS
- homeCommunityId 0..1 MS
- national_provider_identifier 0..1 MS
- subject_organization 0..1 MS
- subject_organization_id 0..1 MS
```

## 9. Clinical Safety and Compliance

### 9.1 FHIR-Based Clinical Decision Support

```fsh

// Clinical Decision Support through FHIR CDS Hooks
CDS Hook: medication-prescribe
- hook: medication-prescribe  
- title: "GenPRES Medication Safety Check"
- description: "G-Standard compliant medication safety validation"
- id: "genpres-medication-safety"
- prefetch:
    patient: "Patient/{{context.patientId}}"
    medications: "MedicationRequest?patient={{context.patientId}}&status=active"
    allergies: "AllergyIntolerance?patient={{context.patientId}}"
    conditions: "Condition?patient={{context.patientId}}&clinical-status=active"
    observations: "Observation?patient={{context.patientId}}&category=vital-signs&_sort=-date&_count=10"

// CDS Card response for safety alerts
CDSCard: safety-alert
- summary: "G-Standard safety alert"
- detail: "Detailed safety information from G-Standard validation"
- indicator: "warning" | "info" | "critical"
- source:
  - label: "G-Standard"
  - url: "http://gstandard.nl"
  - icon: "http://gstandard.nl/icon.png"
- suggestions:
  - label: "Alternative medication"
  - actions:
    - type: "create"
    - description: "Add alternative medication"
    - resource: MedicationRequest

// DetectedIssue resource for clinical alerts
DetectedIssue: clinical-safety-alert
- status: preliminary | final | entered-in-error
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActCode
    - code: DRG | ALLERGY | DOSEAMT | DOSEDUR | DOSECOND
- severity: high | moderate | low
- patient: Reference(Patient)
- detail: "Human readable description of the issue"
- reference: "Reference to related resources"
- mitigation:
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActCode  
      - code: MONITOR | REPLACE | REDUCE | DISCONTINUE
  - description: "Description of mitigation action"
```

### 9.2 G-Standard Safety Validation

```fsharp
// G-Standard safety validation through FHIR operations
type GStandardSafetyValidation = {
    // Validation Operations
    ProductValidation: FhirOperation       // Validate product GPK codes
    DoseRuleValidation: FhirOperation      // Validate dose rules compliance
    InteractionCheck: FhirOperation        // Check drug interactions
    ContraindicationCheck: FhirOperation   // Check contraindications
    
    // Safety Assessment
    RiskAssessment: RiskAssessment         // Overall safety risk assessment
    SafetyProfile: Extension               // Comprehensive safety profile
    
    // Compliance Reporting
    ComplianceReport: Parameters           // G-Standard compliance report
    ValidationOutcome: OperationOutcome    // Validation results
}

// FHIR OperationDefinition for comprehensive safety validation
OperationDefinition: comprehensive-safety-check
- name: comprehensive-safety-check
- status: active
- kind: operation
- code: comprehensive-safety-check
- resource: [CarePlan]
- system: false
- type: false  
- instance: true
- parameter:
  - name: gstandard-version
    use: in
    min: 0
    max: 1
    type: string
  - name: validation-level  
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: safety-report
    use: out
    min: 1
    max: 1 
    type: Parameters
  - name: detected-issues
    use: out
    min: 0
    max: "*"
    type: DetectedIssue
  - name: risk-assessment
    use: out  
    min: 0
    max: 1
    type: RiskAssessment

// RiskAssessment resource for overall safety evaluation  
RiskAssessment: treatment-plan-safety
- status: final
- subject: Reference(Patient)
- encounter: Reference(Encounter) 
- performedDateTime: {assessment-timestamp}
- performer: Reference(Device) # GenPRES system
- code:
  - coding:
    - system: http://snomed.info/sct
    - code: 225338004
    - display: "Risk assessment"
- prediction:
  - outcome:
    - coding:
      - system: http://snomed.info/sct  
      - code: 281647001
      - display: "Adverse reaction"
  - probabilityDecimal: 0.15
  - relativeRisk: 1.2
- mitigation: "Recommended monitoring and precautions"
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/gstandard-risk-factors
    extension:
      - url: age-risk
        valueDecimal: 0.1
      - url: weight-risk  
        valueDecimal: 0.05
      - url: interaction-risk
        valueDecimal: 0.3
```

### 9.3 Pediatric Safety Specializations

```fsh

// Pediatric-specific FHIR profiles and extensions
Profile: PediatricMedicationRequest
Parent: MedicationRequest
Title: "Pediatric Medication Request"
Description: "MedicationRequest profile with pediatric safety requirements"
- subject 1..1 MS
- subject only Reference(PediatricPatient)
- dosageInstruction 1..* MS
  - doseAndRate
    - dose[x] 1..1 MS
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
        extension:
          - url: dose-per-kg
            valueQuantity 1..1 MS
          - url: patient-weight  
            valueQuantity 1..1 MS
          - url: weight-source
            valueCode 1..1 MS # measured | estimated | calculated
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/pediatric-safety-check
    extension:
      - url: age-appropriate
        valueBoolean 1..1 MS
      - url: weight-appropriate
        valueBoolean 1..1 MS  
      - url: concentration-safe
        valueBoolean 1..1 MS
      - url: volume-appropriate
        valueBoolean 1..1 MS

// Age-specific contraindication checking
CodeSystem: pediatric-age-groups
- url: http://genpres.nl/fhir/CodeSystem/pediatric-age-groups
- concept:
  - code: neonate
    display: "Neonate (0-28 days)"
    definition: "Newborn infant up to 28 days of age"
  - code: infant  
    display: "Infant (29 days - 2 years)"
    definition: "Infant from 29 days to 2 years of age"
  - code: child
    display: "Child (2-12 years)" 
    definition: "Child from 2 to 12 years of age"
  - code: adolescent
    display: "Adolescent (12-18 years)"
    definition: "Adolescent from 12 to 18 years of age"

// Pediatric dose rule validation
Extension: pediatric-dose-rule
- url: http://genpres.nl/fhir/StructureDefinition/pediatric-dose-rule
- extension:
  - url: rule-id
    valueString 1..1 MS
  - url: age-group  
    valueCode 1..1 MS # From pediatric-age-groups CodeSystem
  - url: weight-range
    extension:
      - url: min-weight
        valueQuantity 0..1 MS
      - url: max-weight 
        valueQuantity 0..1 MS
  - url: dose-calculation
    extension:
      - url: dose-per-kg
        valueQuantity 0..1 MS
      - url: dose-per-m2
        valueQuantity 0..1 MS  
      - url: max-single-dose
        valueQuantity 0..1 MS
      - url: max-daily-dose
        valueQuantity 0..1 MS
  - url: safety-considerations
    valueString 0..* MS
```

## 10. Performance and Scalability

### 10.1 FHIR Performance Optimization

```fsharp
// FHIR-specific performance optimization strategies
type FhirPerformanceOptimization = {
    // Bundle Optimization
    BundleSize: BundleSizeStrategy         // Optimal bundle sizes
    BundleProcessing: BundleProcessingStrategy // Parallel vs sequential
    
    // Resource Optimization  
    ResourceMinimization: ResourceMinimizationStrategy // Include only necessary elements
    ReferenceOptimization: ReferenceStrategy // Contained vs external references
    
    // Search Optimization
    SearchParameterOptimization: SearchStrategy // Efficient FHIR searches
    IncludeRevIncludeOptimization: IncludeStrategy // Optimal _include/_revinclude usage
    
    // Caching Strategy
    ResourceCaching: CachingStrategy       // Cache frequently accessed resources
    BundleCaching: CachingStrategy         // Cache complete bundles
    OperationCaching: CachingStrategy      // Cache operation results
}

// Bundle size optimization for treatment plans
type BundleSizeStrategy = {
    MaxBundleSize: int                     // Maximum entries per bundle
    ResourceTypeGrouping: bool             // Group similar resources  
    DependencyOrdering: bool               // Order resources by dependencies
    ChunkingStrategy: ChunkingMethod       // How to split large bundles
}

type ChunkingMethod =
    | ByResourceCount of int               // Split by number of resources
    | ByResourceType                       // Split by resource type
    | ByDependencyGraph                    // Split maintaining dependencies
    | ByPayloadSize of int                 // Split by payload size in KB

// FHIR search optimization for patient context
let optimizedPatientContextQuery (patientId: string) =
    $"""
    Patient/{patientId}?
    _include=Patient:organization&
    _include=Patient:general-practitioner&
    _revinclude=Observation:patient&
    _revinclude=AllergyIntolerance:patient&
    _revinclude=Condition:patient&
    _revinclude=MedicationRequest:patient&
    _count=50&
    _sort=-_lastUpdated
    """

// Resource minimization through FHIR _elements parameter  
let minimizePatientResource = "_elements=id,identifier,birthDate,gender,extension"
let minimizeObservationResource = "_elements=id,status,code,subject,value,effective"
let minimizeMedicationRequestResource = "_elements=id,status,intent,medication,subject,dosageInstruction"
```

### 10.2 Scalability Architecture

```fsh

// FHIR server scalability configuration
CapabilityStatement: genpres-capability
- status: active
- date: "2026-01-15"
- publisher: "GenPRES"
- kind: instance
- software:
  - name: "GenPRES FHIR Server"
  - version: "2.1.0"
- implementation:
  - description: "GenPRES Treatment Planning FHIR Server"  
  - url: "https://api.genpres.nl/fhir"
- fhirVersion: 4.0.1
- format: [json, xml]
- rest:
  - mode: server
  - security:
    - cors: true
    - service:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/restful-security-service
        - code: OAuth
        - display: "OAuth2 using SMART-on-FHIR profile"
  - resource:
    - type: Patient
      interaction: [read, search-type]
      searchParam:
        - name: identifier
          type: token
        - name: birthdate  
          type: date
    - type: CarePlan
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      conditionalUpdate: true
      searchParam:
        - name: patient
          type: reference
        - name: status
          type: token
        - name: date
          type: date
    - type: MedicationRequest  
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      searchParam:
        - name: patient
          type: reference
        - name: medication
          type: reference  
        - name: status
          type: token
  - operation:
    - name: bootstrap
      definition: http://genpres.nl/fhir/OperationDefinition/bootstrap
    - name: treatment-plan-request
      definition: http://genpres.nl/fhir/OperationDefinition/treatment-plan-request
    - name: finalize
      definition: http://genpres.nl/fhir/OperationDefinition/finalize
    - name: validate-gstandard  
      definition: http://genpres.nl/fhir/OperationDefinition/validate-gstandard
```

### 10.3 Performance Monitoring

```fsharp
// FHIR-based performance monitoring
type FhirPerformanceMonitoring = {
    // Operation Performance
    OperationMetrics: OperationMetric[]    // Performance per FHIR operation
    ResourceMetrics: ResourceMetric[]      // Performance per resource type
    BundleMetrics: BundleMetric[]          // Bundle processing performance
    
    // Quality Metrics
    ValidationMetrics: ValidationMetric[]  // G-Standard validation performance
    SafetyMetrics: SafetyMetric[]          // Safety check performance
    
    // System Metrics  
    ServerMetrics: ServerMetric[]          // FHIR server performance
    DatabaseMetrics: DatabaseMetric[]     // Database performance
    CacheMetrics: CacheMetric[]           // Cache performance
}

// Performance metrics as FHIR Observation resources
let createPerformanceObservation (operationType: string) (duration: float) =
    {
        ResourceType = "Observation"
        Status = ObservationStatus.Final
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/observation-category"
                        Code = Some "performance-metric"
                        Display = Some "Performance Metric"
                    }
                ]
            }
        ]
        Code = {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                    Code = Some "operation-duration"  
                    Display = Some "Operation Duration"
                }
            ]
        }
        Component = [
            {
                Code = {
                    Coding = [
                        {
                            System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                            Code = Some "operation-type"
                            Display = Some "Operation Type"
                        }
                    ]
                }
                ValueString = Some operationType
            }
        ]
        ValueQuantity = Some {
            Value = Some duration
            Unit = Some "ms"
            System = Some "http://unitsofmeasure.org"
            Code = Some "ms"
        }
        EffectiveDateTime = Some DateTime.UtcNow
        Device = Some {
            Display = Some "GenPRES Performance Monitor"
        }
    }
```

## 11. Security and Privacy

### 11.1 OAuth2/SMART-on-FHIR Security

```fsh

// OAuth2 Authorization Server Metadata (RFC 8414)
OAuth2Metadata: genpres-oauth2
- issuer: "https://auth.genpres.nl"
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"  
- introspection_endpoint: "https://auth.genpres.nl/oauth2/introspect"
- jwks_uri: "https://auth.genpres.nl/.well-known/jwks.json"
- scopes_supported: [
    "patient/*.read",
    "patient/*.write", 
    "user/*.read",
    "user/*.write",
    "system/*.read",
    "system/*.write",
    "launch",
    "launch/patient",
    "fhirUser",
    "openid",
    "profile"
  ]
- response_types_supported: ["code", "token", "id_token"]
- grant_types_supported: ["authorization_code", "client_credentials", "refresh_token"]
- code_challenge_methods_supported: ["S256"]
- token_endpoint_auth_methods_supported: ["client_secret_basic", "client_secret_post", "private_key_jwt"]

// SMART-on-FHIR Well-Known Endpoint
SMARTConfiguration: smart-configuration  
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"
- capabilities: [
  "launch-ehr", 
  "launch-standalone",
  "client-public",
  "client-confidential-symmetric", 
  "sso-openid-connect",
  "context-passthrough-banner",
  "context-passthrough-style",
  "context-ehr-patient",
  "context-ehr-encounter",
  "permission-offline",
  "permission-patient",
  "permission-user"
]
```

### 11.2 FHIR Security Labels and Access Control

```fsh

// Security labels for FHIR resources
CodeSystem: security-labels
- url: http://genpres.nl/fhir/CodeSystem/security-label
- concept:
  - code: PEDS
    display: "Pediatric Data"
    definition: "Data specific to pediatric patients requiring special handling"
  - code: GSTAND
    display: "G-Standard Data" 
    definition: "Data validated against G-Standard requiring compliance tracking"
  - code: CALC
    display: "Calculated Data"
    definition: "Data generated by GenPRES calculations"
  - code: SENS
    display: "Sensitive Clinical Data"
    definition: "Highly sensitive clinical information requiring restricted access"

// Security labeling for CarePlan resources
CarePlan: secure-treatment-plan
- meta:
    security:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
        code: GSTAND
        display: "G-Standard Data"
      - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
        code: N
        display: "Normal"

// Consent resource for treatment plan data usage
Consent: treatment-plan-consent
- status: active
- scope:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentscope
    - code: treatment
    - display: "Treatment"
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentcategorycodes
    - code: medical
    - display: "Medical Consent"
- patient: Reference(Patient)
- dateTime: "2026-01-15T10:00:00Z"
- performer: [Reference(Patient)]
- policy:
  - authority: "https://genpres.nl/privacy-policy"
  - uri: "https://genpres.nl/consent/treatment-planning"
- provision:
  - type: permit
  - purpose:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
    - code: TREAT
    - display: "Treatment"
  - class:
    - system: http://hl7.org/fhir/resource-types
    - code: CarePlan
  - class:
    - system: http://hl7.org/fhir/resource-types  
    - code: MedicationRequest
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: collect
      - display: "Collect"
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: use
      - display: "Use"
```

### 11.3 Audit Trail and Compliance

```fsh

// IHE ATNA compliant audit events
AuditEvent: treatment-plan-access
- type:
  - system: http://dicom.nema.org/resources/ontology/DCM
  - code: "110112"
  - display: "Query"
- action: E # Execute/Perform
- recorded: "2026-01-15T10:30:00Z"
- outcome: "0" # Success
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: humanuser
      - display: "Human User"
  - who:
    - reference: "Practitioner/dr-smith"
    - display: "Dr. John Smith"
  - requestor: true
  - location: Reference(Location/icu-room-101)
  - policy: ["http://genpres.nl/policy/treatment-planning-access"]
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: application
      - display: "Application"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/application-id"
      - value: "genpres-treatment-planner"
    - display: "GenPRES Treatment Planner"
  - requestor: false
- source:
  - observer:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/system-id"  
      - value: "genpres-fhir-server"
    - display: "GenPRES FHIR Server"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/security-source-type
    - code: "4"
    - display: "Application Server"
- entity:
  - what:
    - reference: "Patient/pat-123"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: "1"
    - display: "Person"
  - role:
    - system: http://terminology.hl7.org/CodeSystem/object-role
    - code: "1" 
    - display: "Patient"
  - name: "Treatment plan calculation for pediatric patient"
  - securityLabel:
    - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
    - code: N
    - display: "Normal"

// Provenance tracking for calculated treatment plans
Provenance: treatment-plan-calculation
- target:
  - reference: "CarePlan/calculated-plan-001"
- target:
  - reference: "MedicationRequest/calculated-medreq-001"
- recorded: "2026-01-15T10:30:00Z"
- policy: ["http://genpres.nl/policy/calculation-provenance"]
- location: Reference(Location/genpres-datacenter)
- activity:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-DataOperation
    - code: CREATE
    - display: "Create"
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
      - code: performer
      - display: "Performer"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/calculation-engine"
      - value: "genpres-calc-engine-v2.1"
    - display: "GenPRES Calculation Engine v2.1"
  - onBehalfOf:
    - reference: "Practitioner/dr-smith"
- entity:
  - role: source
  - what:
    - reference: "Bundle/patient-context-bundle"
    - display: "Patient context bundle used for calculation"
  - agent:
    - type:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
        - code: enterer
        - display: "Enterer"  
    - who:
      - reference: "Device/ehr-system"
      - display: "Hospital EHR System"
- signature:
  - type:
    - system: urn:iso-astm:E1762-95:2013
    - code: 1.2.840.10065.1.12.1.1
    - display: "Author's Signature"
  - when: "2026-01-15T10:30:00Z"
  - who:
    - reference: "Device/genpres-system"
  - data: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." # Base64 encoded signature
```

## 12. Implementation Considerations

### 12.1 FHIR Implementation Guide

```fsh

// Implementation Guide definition
ImplementationGuide: genpres-treatment-planning
- url: "http://genpres.nl/fhir/ImplementationGuide/treatment-planning"
- version: "1.2.0" 
- name: "GenPRESTreatmentPlanning"
- title: "GenPRES Treatment Planning Implementation Guide"
- status: active
- publisher: "GenPRES"
- description: "FHIR R4 implementation guide for GenPRES treatment planning integration"
- packageId: "nl.genpres.treatmentplanning"
- license: CC0-1.0
- fhirVersion: [4.0.1]
- dependsOn:
  - uri: "http://hl7.org/fhir"
    version: "4.0.1"
  - uri: "http://hl7.org/fhir/uv/sdc"
    version: "3.0.0"
  - uri: "http://hl7.org/fhir/uv/cpg"  
    version: "2.0.0"
- definition:
  - resource:
    - reference:
      - reference: "StructureDefinition/genpres-patient"
      - name: "GenPRES Patient Profile"
      - description: "Patient profile with Dutch healthcare extensions"
    - reference:
      - reference: "StructureDefinition/genpres-careplan"
      - name: "GenPRES CarePlan Profile" 
      - description: "Treatment plan as FHIR CarePlan"
    - reference:
      - reference: "StructureDefinition/genpres-medicationrequest"
      - name: "GenPRES MedicationRequest Profile"
      - description: "Medication request with G-Standard compliance"
  - page:
    - nameUrl: "index.html"
    - title: "GenPRES Treatment Planning IG"
    - generation: html
    - page:
      - nameUrl: "profiles.html" 
      - title: "FHIR Profiles"
      - generation: html
    - page:
      - nameUrl: "extensions.html"
      - title: "FHIR Extensions"  
      - generation: html
    - page:
      - nameUrl: "terminology.html"
      - title: "Terminology"
      - generation: html
    - page:
      - nameUrl: "examples.html"
      - title: "Examples"
      - generation: html
```

### 12.2 Testing and Validation

```fsharp
// FHIR validation and testing framework
type FhirTestingFramework = {
    // Profile Validation
    ProfileValidation: ProfileValidationConfig // FHIR profile validation
    SchemaValidation: SchemaValidationConfig   // FHIR schema validation
    TerminologyValidation: TerminologyValidationConfig // Terminology validation
    
    // Integration Testing
    OperationTesting: OperationTestConfig      // FHIR operation testing
    WorkflowTesting: WorkflowTestConfig        // End-to-end workflow testing
    PerformanceTesting: PerformanceTestConfig  // Performance testing
    
    // Compliance Testing
    G-StandardTesting: GStandardTestConfig     // G-Standard compliance testing
    SecurityTesting: SecurityTestConfig        // OAuth2/SMART security testing
    IHETesting: IHETestConfig                  // IHE profile compliance testing
}

// FHIR TestScript resources for automated testing
TestScript: bootstrap-operation-test
- url: "http://genpres.nl/fhir/TestScript/bootstrap-operation-test"
- version: "1.0.0"
- name: "BootstrapOperationTest"
- status: active
- title: "Test bootstrap operation with patient context"
- description: "Validates the $bootstrap operation with complete patient context"
- origin:
  - index: 1
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-origin-types"
    - code: "FHIR-Client"
- destination:
  - index: 1  
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-destination-types"
    - code: "FHIR-Server"
- fixture:
  - id: "patient-context-bundle"
  - autocreate: false
  - autodelete: false
  - resource:
    - reference: "Bundle/patient-context-example"
- variable:
  - name: "patientId"
  - defaultValue: "pat-123"
  - sourceId: "patient-context-bundle"
  - expression: "Bundle.entry.where(resource is Patient).resource.id"
- test:
  - id: "bootstrap-success"
  - name: "Bootstrap Operation Success"
  - description: "Test successful bootstrap operation"
  - action:
    - operation:
      - type:
        - system: "http://terminology.hl7.org/CodeSystem/testscript-operation-codes"
        - code: "create"
      - resource: "Bundle"
      - description: "Execute bootstrap operation"  
      - destination: 1
      - origin: 1
      - params: "/$bootstrap"
      - sourceId: "patient-context-bundle"
    - assert:
      - description: "Confirm that the returned HTTP status is 200(OK)"
      - direction: response
      - responseCode: "200"
    - assert:
      - description: "Confirm that the response is an OperationOutcome"
      - direction: response
      - resource: "OperationOutcome"
    - assert:
      - description: "Confirm successful outcome"
      - direction: response
      - expression: "OperationOutcome.issue.where(severity='information').exists()"

// Example test data for FHIR testing
Bundle: patient-context-test-data
- resourceType: Bundle
- type: collection
- entry:
  - resource:
    - resourceType: Patient
    - id: test-patient-001
    - identifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
      - value: "999999999"
    - birthDate: "2015-03-15"
    - gender: male
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
        valueQuantity:
          value: 39
          unit: weeks
          system: http://unitsofmeasure.org
          code: wk
  - resource:
    - resourceType: Observation
    - status: final
    - category:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/observation-category
        - code: vital-signs
    - code:
      - coding:
        - system: http://loinc.org
        - code: "29463-7"
        - display: "Body weight"
    - subject:
      - reference: "Patient/test-patient-001"
    - valueQuantity:
      - value: 18.5
      - unit: kg
      - system: http://unitsofmeasure.org  
      - code: kg
```

### 12.3 Data Migration and Integration

```fsharp
// FHIR data migration strategies
type FhirDataMigration = {
    // Legacy Data Transformation
    LegacyToFhir: TransformationConfig     // Transform legacy data to FHIR
    GStandardMapping: MappingConfig        // Map G-Standard data to FHIR
    
    // Data Validation
    MigrationValidation: ValidationConfig  // Validate migrated data
    QualityAssurance: QualityConfig        // Data quality checks
    
    // Incremental Migration
    IncrementalSync: SyncConfig            // Incremental data synchronization
    ConflictResolution: ConflictConfig     // Handle data conflicts
}

// FHIR ConceptMap for G-Standard to FHIR mapping
ConceptMap: gstandard-to-fhir-medication
- url: "http://genpres.nl/fhir/ConceptMap/gstandard-to-fhir-medication"
- version: "1.0.0"
- name: "GStandardToFHIRMedication" 
- title: "G-Standard to FHIR Medication Mapping"
- status: active
- publisher: "GenPRES"
- description: "Mapping G-Standard medication codes to FHIR Medication resource elements"
- sourceUri: "http://gstandard.nl/fhir/CodeSystem/gpk"
- targetUri: "http://hl7.org/fhir/StructureDefinition/Medication"
- group:
  - source: "http://gstandard.nl/fhir/CodeSystem/gpk"
  - target: "http://snomed.info/sct"
  - element:
    - code: "12345" # GPK code
    - display: "Paracetamol tablet 500mg"
    - target:
      - code: "387517004"
      - display: "Paracetamol"
      - equivalence: equivalent
  - element:
    - code: "67890" # GPK code  
    - display: "Amoxicillin capsule 250mg"
    - target:
      - code: "27658006"
      - display: "Amoxicillin"
      - equivalence: equivalent

// FHIR StructureMap for data transformation
StructureMap: legacy-patient-to-fhir
- url: "http://genpres.nl/fhir/StructureMap/legacy-patient-to-fhir"
- version: "1.0.0"
- name: "LegacyPatientToFHIR"
- status: active
- title: "Transform legacy patient data to FHIR Patient"
- description: "Transforms legacy EHR patient data to FHIR Patient resource"
- structure:
  - url: "http://genpres.nl/fhir/StructureDefinition/legacy-patient"
  - mode: source
  - alias: "LegacyPatient"
- structure:
  - url: "http://hl7.org/fhir/StructureDefinition/Patient"  
  - mode: target
  - alias: "FHIRPatient"
- group:
  - name: "patient"
  - typeMode: none
  - input:
    - name: "source"
    - type: "LegacyPatient"
    - mode: source
  - input:
    - name: "target"
    - type: "FHIRPatient" 
    - mode: target
  - rule:
    - name: "patient-id"
    - source:
      - context: "source"
      - element: "patientId"
      - variable: "pid"
    - target:
      - context: "target"
      - contextType: variable
      - element: "id"
      - transform: copy
      - parameter:
        - valueId: "pid"
  - rule:
    - name: "birth-date"
    - source:
      - context: "source"
      - element: "dateOfBirth" 
      - variable: "dob"
    - target:
      - context: "target"
      - contextType: variable
      - element: "birthDate"
      - transform: copy
      - parameter:
        - valueId: "dob"
```

## 13. Appendices

### Appendix A: FHIR Profiles and Extensions

```fsh

// Complete FHIR profile definitions
StructureDefinition: GenPRESPatient
- url: http://genpres.nl/fhir/StructureDefinition/genpres-patient
- version: 1.2.0
- name: GenPRESPatient  
- title: GenPRES Patient Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: Patient
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Patient
- derivation: constraint
- differential:
  - element:
    - id: Patient.identifier
    - path: Patient.identifier
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: Patient.identifier:BSN
    - path: Patient.identifier
    - sliceName: BSN
    - min: 0
    - max: 1
    - type:
      - code: Identifier
    - patternIdentifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
  - element:  
    - id: Patient.birthDate
    - path: Patient.birthDate
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: Patient.extension:gestationalAge
    - path: Patient.extension
    - sliceName: gestationalAge
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gestational-age]

StructureDefinition: GenPRESCarePlan
- url: http://genpres.nl/fhir/StructureDefinition/genpres-careplan
- version: 1.2.0
- name: GenPRESCarePlan
- title: GenPRES CarePlan Profile  
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: CarePlan
- baseDefinition: http://hl7.org/fhir/StructureDefinition/CarePlan
- derivation: constraint
- differential:
  - element:
    - id: CarePlan.status
    - path: CarePlan.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: CarePlan.intent
    - path: CarePlan.intent  
    - min: 1
    - max: 1
    - fixedCode: plan
    - mustSupport: true
  - element:
    - id: CarePlan.subject
    - path: CarePlan.subject
    - min: 1  
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: CarePlan.extension:gstandardCompliance
    - path: CarePlan.extension
    - sliceName: gstandardCompliance
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-compliance]

StructureDefinition: GenPRESMedicationRequest
- url: http://genpres.nl/fhir/StructureDefinition/genpres-medicationrequest
- version: 1.2.0
- name: GenPRESMedicationRequest
- title: GenPRES MedicationRequest Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: MedicationRequest
- baseDefinition: http://hl7.org/fhir/StructureDefinition/MedicationRequest
- derivation: constraint
- differential:
  - element:
    - id: MedicationRequest.status
    - path: MedicationRequest.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.intent
    - path: MedicationRequest.intent
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.medication[x]
    - path: MedicationRequest.medication[x]
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-medication]
    - mustSupport: true
  - element:
    - id: MedicationRequest.subject  
    - path: MedicationRequest.subject
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: MedicationRequest.dosageInstruction
    - path: MedicationRequest.dosageInstruction
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: MedicationRequest.extension:weightBasedDose
    - path: MedicationRequest.extension
    - sliceName: weightBasedDose
    - min: 0
    - max: 1  
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/weight-based-dose]
  - element:
    - id: MedicationRequest.extension:gstandardDoseRule
    - path: MedicationRequest.extension
    - sliceName: gstandardDoseRule
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule]
```

### Appendix B: G-Standard Integration Specifications

```fsh

// G-Standard specific extensions
StructureDefinition: GStandardCompliance
- url: http://genpres.nl/fhir/StructureDefinition/gstandard-compliance
- version: 1.2.0
- name: GStandardCompliance
- title: G-Standard Compliance Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: CarePlan
  - type: element
  - expression: MedicationRequest
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: G-Standard compliance information
    - definition: Detailed information about G-Standard compliance validation
  - element:
    - id: Extension.extension:level
    - path: Extension.extension
    - sliceName: level
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:level.url
    - path: Extension.extension.url
    - fixedUri: level
  - element:
    - id: Extension.extension:level.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/compliance-level
  - element:
    - id: Extension.extension:validationTimestamp
    - path: Extension.extension
    - sliceName: validationTimestamp
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:validationTimestamp.url
    - path: Extension.extension.url
    - fixedUri: validation-timestamp
  - element:
    - id: Extension.extension:validationTimestamp.value[x]
    - path: Extension.extension.value[x]  
    - type:
      - code: dateTime
  - element:
    - id: Extension.extension:gstandardVersion
    - path: Extension.extension
    - sliceName: gstandardVersion
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:gstandardVersion.url
    - path: Extension.extension.url
    - fixedUri: gstandard-version
  - element:
    - id: Extension.extension:gstandardVersion.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: string

StructureDefinition: WeightBasedDose
- url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
- version: 1.2.0
- name: WeightBasedDose
- title: Weight-Based Dose Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: MedicationRequest
  - type: element  
  - expression: Dosage
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: Weight-based dosing information
    - definition: Information about weight-based dose calculation
  - element:
    - id: Extension.extension:dosePerKg
    - path: Extension.extension
    - sliceName: dosePerKg
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:dosePerKg.url
    - path: Extension.extension.url
    - fixedUri: dose-per-kg
  - element:
    - id: Extension.extension:dosePerKg.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:patientWeight
    - path: Extension.extension
    - sliceName: patientWeight
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:patientWeight.url
    - path: Extension.extension.url
    - fixedUri: patient-weight
  - element:
    - id: Extension.extension:patientWeight.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:weightSource
    - path: Extension.extension
    - sliceName: weightSource
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:weightSource.url
    - path: Extension.extension.url
    - fixedUri: weight-source  
  - element:
    - id: Extension.extension:weightSource.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/weight-source
```

### Appendix C: API Documentation

```yaml
## Complete OpenAPI 3.0 specification
openapi: 3.0.3
info:
  title: GenPRES FHIR Treatment Planning API
  description: |
    FHIR R4 compliant API for GenPRES treatment planning integration.
    Supports G-Standard compliance, OAuth2/SMART-on-FHIR security,
    and IHE Pharmacy profile compliance.
  version: 1.2.0
  contact:
    name: GenPRES API Support
    url: https://support.genpres.nl
    email: api-support@genpres.nl
  license:
    name: Proprietary
    url: https://genpres.nl/license

servers:
  - url: https://api.genpres.nl/fhir
    description: Production FHIR server
  - url: https://staging-api.genpres.nl/fhir  
    description: Staging FHIR server
  - url: https://dev-api.genpres.nl/fhir
    description: Development FHIR server

security:
  - OAuth2: [patient/*.read, patient/*.write, user/*.read]
  - BearerAuth: []

paths:
  /$bootstrap:
    post:
      tags: [Treatment Planning]
      summary: Initialize patient context for treatment planning
      description: |
        Bootstrap operation that establishes patient context for treatment planning.
        Accepts a Bundle containing Patient and related clinical resources.
      operationId: bootstrap
      security:
        - OAuth2: [patient/*.read, launch, launch/patient]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            examples:
              patient-context:
                summary: Patient context with vital signs and allergies
                value:
                  resourceType: Bundle
                  type: collection
                  entry:
                    - resource:
                        resourceType: Patient
                        id: pat-123
                        birthDate: "2010-05-15"
                        gender: male
      responses:
        '200':
          description: Bootstrap successful
          content:
            application/fhir+json:: TREAT
        display: "Treatment"
      - system: http://genpres.nl/fhir/CodeSystem/security-label  
        code
        status:
          type: string
          enum: [active, on-hold, cancelled, completed, entered-in-error, stopped, draft, unknown]
        intent:
          type: string
          enum: [proposal, plan, order, original-order, reflex-order, filler-order, instance-order, option]
        medicationReference:
          $ref: '#/components/schemas/Reference'
        subject:
          $ref: '#/components/schemas/Reference'
        dosageInstruction:
          type: array
          items:
            $ref: '#/components/schemas/Dosage'
        extension:
          type: array
          items:
            $ref: '#/components/schemas/Extension'

    Medication:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Medication]
        id:
          type: string
          pattern: '^[A-Za-z0-9\-\.]{1,64}
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Treatment Plan Request
  /fhir/$treatment-plan-request:
    post:
      summary: Request treatment plan calculation
      operationId: treatmentPlanRequest
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # Treatment Plan Finalization
  /fhir/$finalize:
    post:
      summary: Finalize treatment plan for EHR implementation
      operationId: finalize
      parameters:
        - name: session-id
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

  # G-Standard Validation
  /fhir/Medication/$validate-gstandard:
    post:
      summary: Validate medication against G-Standard
      operationId: validateGStandard
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

  # Simulation (Dry Run)
  /fhir/$simulate:
    post:
      summary: Simulate treatment plan without persistence
      operationId: simulate
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

components:
  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        type:
          type: string
          enum: [collection, transaction, searchset, batch]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'
    
    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'
```

### 8.2 OAuth2/SMART-on-FHIR Security Integration

```fsharp
// SMART-on-FHIR security context
type SmartOnFhirSecurity = {
    // OAuth2 Configuration
    AuthorizationServer: string           // EHR authorization server
    TokenEndpoint: string                 // OAuth2 token endpoint
    IntrospectionEndpoint: string         // Token introspection endpoint
    
    // SMART Scopes
    RequiredScopes: SmartScope[]          // Required SMART scopes
    OptionalScopes: SmartScope[]          // Optional enhanced scopes
    
    // Token Management
    AccessToken: AccessToken              // Current access token
    RefreshToken: RefreshToken option     // Refresh token if available
    TokenExpiration: DateTime             // Token expiration time
    
    // FHIR Security
    FhirSecurityLabels: SecurityLabel[]   // FHIR security labels
    AuditRequirements: AuditRequirement[] // IHE ATNA compliance
}

// SMART-on-FHIR scopes for GenPRES integration
type SmartScope =
    | PatientRead                         // patient/*.read
    | PatientWrite                        // patient/*.write
    | UserRead                            // user/*.read
    | UserWrite                           // user/*.write
    | SystemRead                          // system/*.read
    | SystemWrite                         // system/*.write
    | Launch                              // launch
    | LaunchPatient                       // launch/patient
    | FhirUser                            // fhirUser
    | OpenIdProfile                       // profile openid

// OAuth2 token validation for FHIR requests
let validateFhirToken (token: string) : Async<Result<FhirSecurityContext, SecurityError>> =
    async {
        try
            // Introspect token with EHR authorization server
            let! introspectionResult = introspectToken token
            
            match introspectionResult with
            | Error error -> return Error error
            | Ok tokenInfo ->
                
                // Validate required scopes
                let requiredScopes = [
                    "patient/Patient.read"
                    "patient/CarePlan.write" 
                    "patient/MedicationRequest.write"
                    "patient/Medication.read"
                    "user/Practitioner.read"
                ]
                
                let hasRequiredScopes = 
                    requiredScopes 
                    |> List.forall (fun scope -> tokenInfo.Scopes |> List.contains scope)
                
                if not hasRequiredScopes then
                    return Error (InsufficientScopes requiredScopes)
                else
                    let securityContext = {
                        UserId = tokenInfo.Sub
                        PatientId = tokenInfo.Patient
                        Scopes = tokenInfo.Scopes
                        FhirUser = tokenInfo.FhirUser
                        ExpiresAt = tokenInfo.ExpiresAt
                    }
                    return Ok securityContext
        with
        | ex -> return Error (TokenValidationError ex.Message)
    }
```

### 8.3 IHE Profile Compliance

```fsh

// IHE Pharmacy (PHARM) Profile Compliance
Profile: IHE-PHARM-CarePlan
Parent: CarePlan
Title: "IHE Pharmacy CarePlan Profile"
Description: "CarePlan profile compliant with IHE Pharmacy workflow"
- status 1..1 MS
- intent 1..1 MS
- subject 1..1 MS
- activity
  - detail
    - kind 1..1 MS
    - code 1..1 MS
    - status 1..1 MS
    - medication[x] 0..1 MS

// IHE Audit Trail and Node Authentication (ATNA) Compliance
Profile: IHE-ATNA-AuditEvent
Parent: AuditEvent
Title: "IHE ATNA AuditEvent Profile"
Description: "AuditEvent profile for IHE ATNA compliance"
- type 1..1 MS
- action 1..1 MS
- recorded 1..1 MS
- outcome 1..1 MS
- agent 1..* MS
  - type 1..1 MS
  - who 1..1 MS
  - requestor 1..1 MS
- source 1..1 MS
  - observer 1..1 MS
- entity 0..* MS
  - what 0..1 MS
  - type 0..1 MS
  - role 0..1 MS

// IHE Internet User Authorization (IUA) Profile
Profile: IHE-IUA-AccessToken
Title: "IHE IUA Access Token Profile" 
Description: "OAuth2 access token profile for IHE IUA compliance"
- iss (issuer) 1..1 MS
- sub (subject) 1..1 MS  
- aud (audience) 1..1 MS
- exp (expiration) 1..1 MS
- iat (issued at) 1..1 MS
- scope 1..1 MS
- purpose_of_use 0..1 MS
- homeCommunityId 0..1 MS
- national_provider_identifier 0..1 MS
- subject_organization 0..1 MS
- subject_organization_id 0..1 MS
```

## 9. Clinical Safety and Compliance

### 9.1 FHIR-Based Clinical Decision Support

```fsh

// Clinical Decision Support through FHIR CDS Hooks
CDS Hook: medication-prescribe
- hook: medication-prescribe  
- title: "GenPRES Medication Safety Check"
- description: "G-Standard compliant medication safety validation"
- id: "genpres-medication-safety"
- prefetch:
    patient: "Patient/{{context.patientId}}"
    medications: "MedicationRequest?patient={{context.patientId}}&status=active"
    allergies: "AllergyIntolerance?patient={{context.patientId}}"
    conditions: "Condition?patient={{context.patientId}}&clinical-status=active"
    observations: "Observation?patient={{context.patientId}}&category=vital-signs&_sort=-date&_count=10"

// CDS Card response for safety alerts
CDSCard: safety-alert
- summary: "G-Standard safety alert"
- detail: "Detailed safety information from G-Standard validation"
- indicator: "warning" | "info" | "critical"
- source:
  - label: "G-Standard"
  - url: "http://gstandard.nl"
  - icon: "http://gstandard.nl/icon.png"
- suggestions:
  - label: "Alternative medication"
  - actions:
    - type: "create"
    - description: "Add alternative medication"
    - resource: MedicationRequest

// DetectedIssue resource for clinical alerts
DetectedIssue: clinical-safety-alert
- status: preliminary | final | entered-in-error
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActCode
    - code: DRG | ALLERGY | DOSEAMT | DOSEDUR | DOSECOND
- severity: high | moderate | low
- patient: Reference(Patient)
- detail: "Human readable description of the issue"
- reference: "Reference to related resources"
- mitigation:
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActCode  
      - code: MONITOR | REPLACE | REDUCE | DISCONTINUE
  - description: "Description of mitigation action"
```

### 9.2 G-Standard Safety Validation

```fsharp
// G-Standard safety validation through FHIR operations
type GStandardSafetyValidation = {
    // Validation Operations
    ProductValidation: FhirOperation       // Validate product GPK codes
    DoseRuleValidation: FhirOperation      // Validate dose rules compliance
    InteractionCheck: FhirOperation        // Check drug interactions
    ContraindicationCheck: FhirOperation   // Check contraindications
    
    // Safety Assessment
    RiskAssessment: RiskAssessment         // Overall safety risk assessment
    SafetyProfile: Extension               // Comprehensive safety profile
    
    // Compliance Reporting
    ComplianceReport: Parameters           // G-Standard compliance report
    ValidationOutcome: OperationOutcome    // Validation results
}

// FHIR OperationDefinition for comprehensive safety validation
OperationDefinition: comprehensive-safety-check
- name: comprehensive-safety-check
- status: active
- kind: operation
- code: comprehensive-safety-check
- resource: [CarePlan]
- system: false
- type: false  
- instance: true
- parameter:
  - name: gstandard-version
    use: in
    min: 0
    max: 1
    type: string
  - name: validation-level  
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: safety-report
    use: out
    min: 1
    max: 1 
    type: Parameters
  - name: detected-issues
    use: out
    min: 0
    max: "*"
    type: DetectedIssue
  - name: risk-assessment
    use: out  
    min: 0
    max: 1
    type: RiskAssessment

// RiskAssessment resource for overall safety evaluation  
RiskAssessment: treatment-plan-safety
- status: final
- subject: Reference(Patient)
- encounter: Reference(Encounter) 
- performedDateTime: {assessment-timestamp}
- performer: Reference(Device) # GenPRES system
- code:
  - coding:
    - system: http://snomed.info/sct
    - code: 225338004
    - display: "Risk assessment"
- prediction:
  - outcome:
    - coding:
      - system: http://snomed.info/sct  
      - code: 281647001
      - display: "Adverse reaction"
  - probabilityDecimal: 0.15
  - relativeRisk: 1.2
- mitigation: "Recommended monitoring and precautions"
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/gstandard-risk-factors
    extension:
      - url: age-risk
        valueDecimal: 0.1
      - url: weight-risk  
        valueDecimal: 0.05
      - url: interaction-risk
        valueDecimal: 0.3
```

### 9.3 Pediatric Safety Specializations

```fsh

// Pediatric-specific FHIR profiles and extensions
Profile: PediatricMedicationRequest
Parent: MedicationRequest
Title: "Pediatric Medication Request"
Description: "MedicationRequest profile with pediatric safety requirements"
- subject 1..1 MS
- subject only Reference(PediatricPatient)
- dosageInstruction 1..* MS
  - doseAndRate
    - dose[x] 1..1 MS
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
        extension:
          - url: dose-per-kg
            valueQuantity 1..1 MS
          - url: patient-weight  
            valueQuantity 1..1 MS
          - url: weight-source
            valueCode 1..1 MS # measured | estimated | calculated
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/pediatric-safety-check
    extension:
      - url: age-appropriate
        valueBoolean 1..1 MS
      - url: weight-appropriate
        valueBoolean 1..1 MS  
      - url: concentration-safe
        valueBoolean 1..1 MS
      - url: volume-appropriate
        valueBoolean 1..1 MS

// Age-specific contraindication checking
CodeSystem: pediatric-age-groups
- url: http://genpres.nl/fhir/CodeSystem/pediatric-age-groups
- concept:
  - code: neonate
    display: "Neonate (0-28 days)"
    definition: "Newborn infant up to 28 days of age"
  - code: infant  
    display: "Infant (29 days - 2 years)"
    definition: "Infant from 29 days to 2 years of age"
  - code: child
    display: "Child (2-12 years)" 
    definition: "Child from 2 to 12 years of age"
  - code: adolescent
    display: "Adolescent (12-18 years)"
    definition: "Adolescent from 12 to 18 years of age"

// Pediatric dose rule validation
Extension: pediatric-dose-rule
- url: http://genpres.nl/fhir/StructureDefinition/pediatric-dose-rule
- extension:
  - url: rule-id
    valueString 1..1 MS
  - url: age-group  
    valueCode 1..1 MS # From pediatric-age-groups CodeSystem
  - url: weight-range
    extension:
      - url: min-weight
        valueQuantity 0..1 MS
      - url: max-weight 
        valueQuantity 0..1 MS
  - url: dose-calculation
    extension:
      - url: dose-per-kg
        valueQuantity 0..1 MS
      - url: dose-per-m2
        valueQuantity 0..1 MS  
      - url: max-single-dose
        valueQuantity 0..1 MS
      - url: max-daily-dose
        valueQuantity 0..1 MS
  - url: safety-considerations
    valueString 0..* MS
```

## 10. Performance and Scalability

### 10.1 FHIR Performance Optimization

```fsharp
// FHIR-specific performance optimization strategies
type FhirPerformanceOptimization = {
    // Bundle Optimization
    BundleSize: BundleSizeStrategy         // Optimal bundle sizes
    BundleProcessing: BundleProcessingStrategy // Parallel vs sequential
    
    // Resource Optimization  
    ResourceMinimization: ResourceMinimizationStrategy // Include only necessary elements
    ReferenceOptimization: ReferenceStrategy // Contained vs external references
    
    // Search Optimization
    SearchParameterOptimization: SearchStrategy // Efficient FHIR searches
    IncludeRevIncludeOptimization: IncludeStrategy // Optimal _include/_revinclude usage
    
    // Caching Strategy
    ResourceCaching: CachingStrategy       // Cache frequently accessed resources
    BundleCaching: CachingStrategy         // Cache complete bundles
    OperationCaching: CachingStrategy      // Cache operation results
}

// Bundle size optimization for treatment plans
type BundleSizeStrategy = {
    MaxBundleSize: int                     // Maximum entries per bundle
    ResourceTypeGrouping: bool             // Group similar resources  
    DependencyOrdering: bool               // Order resources by dependencies
    ChunkingStrategy: ChunkingMethod       // How to split large bundles
}

type ChunkingMethod =
    | ByResourceCount of int               // Split by number of resources
    | ByResourceType                       // Split by resource type
    | ByDependencyGraph                    // Split maintaining dependencies
    | ByPayloadSize of int                 // Split by payload size in KB

// FHIR search optimization for patient context
let optimizedPatientContextQuery (patientId: string) =
    $"""
    Patient/{patientId}?
    _include=Patient:organization&
    _include=Patient:general-practitioner&
    _revinclude=Observation:patient&
    _revinclude=AllergyIntolerance:patient&
    _revinclude=Condition:patient&
    _revinclude=MedicationRequest:patient&
    _count=50&
    _sort=-_lastUpdated
    """

// Resource minimization through FHIR _elements parameter  
let minimizePatientResource = "_elements=id,identifier,birthDate,gender,extension"
let minimizeObservationResource = "_elements=id,status,code,subject,value,effective"
let minimizeMedicationRequestResource = "_elements=id,status,intent,medication,subject,dosageInstruction"
```

### 10.2 Scalability Architecture

```fsh

// FHIR server scalability configuration
CapabilityStatement: genpres-capability
- status: active
- date: "2026-01-15"
- publisher: "GenPRES"
- kind: instance
- software:
  - name: "GenPRES FHIR Server"
  - version: "2.1.0"
- implementation:
  - description: "GenPRES Treatment Planning FHIR Server"  
  - url: "https://api.genpres.nl/fhir"
- fhirVersion: 4.0.1
- format: [json, xml]
- rest:
  - mode: server
  - security:
    - cors: true
    - service:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/restful-security-service
        - code: OAuth
        - display: "OAuth2 using SMART-on-FHIR profile"
  - resource:
    - type: Patient
      interaction: [read, search-type]
      searchParam:
        - name: identifier
          type: token
        - name: birthdate  
          type: date
    - type: CarePlan
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      conditionalUpdate: true
      searchParam:
        - name: patient
          type: reference
        - name: status
          type: token
        - name: date
          type: date
    - type: MedicationRequest  
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      searchParam:
        - name: patient
          type: reference
        - name: medication
          type: reference  
        - name: status
          type: token
  - operation:
    - name: bootstrap
      definition: http://genpres.nl/fhir/OperationDefinition/bootstrap
    - name: treatment-plan-request
      definition: http://genpres.nl/fhir/OperationDefinition/treatment-plan-request
    - name: finalize
      definition: http://genpres.nl/fhir/OperationDefinition/finalize
    - name: validate-gstandard  
      definition: http://genpres.nl/fhir/OperationDefinition/validate-gstandard
```

### 10.3 Performance Monitoring

```fsharp
// FHIR-based performance monitoring
type FhirPerformanceMonitoring = {
    // Operation Performance
    OperationMetrics: OperationMetric[]    // Performance per FHIR operation
    ResourceMetrics: ResourceMetric[]      // Performance per resource type
    BundleMetrics: BundleMetric[]          // Bundle processing performance
    
    // Quality Metrics
    ValidationMetrics: ValidationMetric[]  // G-Standard validation performance
    SafetyMetrics: SafetyMetric[]          // Safety check performance
    
    // System Metrics  
    ServerMetrics: ServerMetric[]          // FHIR server performance
    DatabaseMetrics: DatabaseMetric[]     // Database performance
    CacheMetrics: CacheMetric[]           // Cache performance
}

// Performance metrics as FHIR Observation resources
let createPerformanceObservation (operationType: string) (duration: float) =
    {
        ResourceType = "Observation"
        Status = ObservationStatus.Final
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/observation-category"
                        Code = Some "performance-metric"
                        Display = Some "Performance Metric"
                    }
                ]
            }
        ]
        Code = {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                    Code = Some "operation-duration"  
                    Display = Some "Operation Duration"
                }
            ]
        }
        Component = [
            {
                Code = {
                    Coding = [
                        {
                            System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                            Code = Some "operation-type"
                            Display = Some "Operation Type"
                        }
                    ]
                }
                ValueString = Some operationType
            }
        ]
        ValueQuantity = Some {
            Value = Some duration
            Unit = Some "ms"
            System = Some "http://unitsofmeasure.org"
            Code = Some "ms"
        }
        EffectiveDateTime = Some DateTime.UtcNow
        Device = Some {
            Display = Some "GenPRES Performance Monitor"
        }
    }
```

## 11. Security and Privacy

### 11.1 OAuth2/SMART-on-FHIR Security

```fsh

// OAuth2 Authorization Server Metadata (RFC 8414)
OAuth2Metadata: genpres-oauth2
- issuer: "https://auth.genpres.nl"
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"  
- introspection_endpoint: "https://auth.genpres.nl/oauth2/introspect"
- jwks_uri: "https://auth.genpres.nl/.well-known/jwks.json"
- scopes_supported: [
    "patient/*.read",
    "patient/*.write", 
    "user/*.read",
    "user/*.write",
    "system/*.read",
    "system/*.write",
    "launch",
    "launch/patient",
    "fhirUser",
    "openid",
    "profile"
  ]
- response_types_supported: ["code", "token", "id_token"]
- grant_types_supported: ["authorization_code", "client_credentials", "refresh_token"]
- code_challenge_methods_supported: ["S256"]
- token_endpoint_auth_methods_supported: ["client_secret_basic", "client_secret_post", "private_key_jwt"]

// SMART-on-FHIR Well-Known Endpoint
SMARTConfiguration: smart-configuration  
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"
- capabilities: [
  "launch-ehr", 
  "launch-standalone",
  "client-public",
  "client-confidential-symmetric", 
  "sso-openid-connect",
  "context-passthrough-banner",
  "context-passthrough-style",
  "context-ehr-patient",
  "context-ehr-encounter",
  "permission-offline",
  "permission-patient",
  "permission-user"
]
```

### 11.2 FHIR Security Labels and Access Control

```fsh

// Security labels for FHIR resources
CodeSystem: security-labels
- url: http://genpres.nl/fhir/CodeSystem/security-label
- concept:
  - code: PEDS
    display: "Pediatric Data"
    definition: "Data specific to pediatric patients requiring special handling"
  - code: GSTAND
    display: "G-Standard Data" 
    definition: "Data validated against G-Standard requiring compliance tracking"
  - code: CALC
    display: "Calculated Data"
    definition: "Data generated by GenPRES calculations"
  - code: SENS
    display: "Sensitive Clinical Data"
    definition: "Highly sensitive clinical information requiring restricted access"

// Security labeling for CarePlan resources
CarePlan: secure-treatment-plan
- meta:
    security:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
        code: GSTAND
        display: "G-Standard Data"
      - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
        code: N
        display: "Normal"

// Consent resource for treatment plan data usage
Consent: treatment-plan-consent
- status: active
- scope:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentscope
    - code: treatment
    - display: "Treatment"
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentcategorycodes
    - code: medical
    - display: "Medical Consent"
- patient: Reference(Patient)
- dateTime: "2026-01-15T10:00:00Z"
- performer: [Reference(Patient)]
- policy:
  - authority: "https://genpres.nl/privacy-policy"
  - uri: "https://genpres.nl/consent/treatment-planning"
- provision:
  - type: permit
  - purpose:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
    - code: TREAT
    - display: "Treatment"
  - class:
    - system: http://hl7.org/fhir/resource-types
    - code: CarePlan
  - class:
    - system: http://hl7.org/fhir/resource-types  
    - code: MedicationRequest
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: collect
      - display: "Collect"
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: use
      - display: "Use"
```

### 11.3 Audit Trail and Compliance

```fsh

// IHE ATNA compliant audit events
AuditEvent: treatment-plan-access
- type:
  - system: http://dicom.nema.org/resources/ontology/DCM
  - code: "110112"
  - display: "Query"
- action: E # Execute/Perform
- recorded: "2026-01-15T10:30:00Z"
- outcome: "0" # Success
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: humanuser
      - display: "Human User"
  - who:
    - reference: "Practitioner/dr-smith"
    - display: "Dr. John Smith"
  - requestor: true
  - location: Reference(Location/icu-room-101)
  - policy: ["http://genpres.nl/policy/treatment-planning-access"]
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: application
      - display: "Application"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/application-id"
      - value: "genpres-treatment-planner"
    - display: "GenPRES Treatment Planner"
  - requestor: false
- source:
  - observer:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/system-id"  
      - value: "genpres-fhir-server"
    - display: "GenPRES FHIR Server"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/security-source-type
    - code: "4"
    - display: "Application Server"
- entity:
  - what:
    - reference: "Patient/pat-123"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: "1"
    - display: "Person"
  - role:
    - system: http://terminology.hl7.org/CodeSystem/object-role
    - code: "1" 
    - display: "Patient"
  - name: "Treatment plan calculation for pediatric patient"
  - securityLabel:
    - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
    - code: N
    - display: "Normal"

// Provenance tracking for calculated treatment plans
Provenance: treatment-plan-calculation
- target:
  - reference: "CarePlan/calculated-plan-001"
- target:
  - reference: "MedicationRequest/calculated-medreq-001"
- recorded: "2026-01-15T10:30:00Z"
- policy: ["http://genpres.nl/policy/calculation-provenance"]
- location: Reference(Location/genpres-datacenter)
- activity:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-DataOperation
    - code: CREATE
    - display: "Create"
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
      - code: performer
      - display: "Performer"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/calculation-engine"
      - value: "genpres-calc-engine-v2.1"
    - display: "GenPRES Calculation Engine v2.1"
  - onBehalfOf:
    - reference: "Practitioner/dr-smith"
- entity:
  - role: source
  - what:
    - reference: "Bundle/patient-context-bundle"
    - display: "Patient context bundle used for calculation"
  - agent:
    - type:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
        - code: enterer
        - display: "Enterer"  
    - who:
      - reference: "Device/ehr-system"
      - display: "Hospital EHR System"
- signature:
  - type:
    - system: urn:iso-astm:E1762-95:2013
    - code: 1.2.840.10065.1.12.1.1
    - display: "Author's Signature"
  - when: "2026-01-15T10:30:00Z"
  - who:
    - reference: "Device/genpres-system"
  - data: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." # Base64 encoded signature
```

## 12. Implementation Considerations

### 12.1 FHIR Implementation Guide

```fsh

// Implementation Guide definition
ImplementationGuide: genpres-treatment-planning
- url: "http://genpres.nl/fhir/ImplementationGuide/treatment-planning"
- version: "1.2.0" 
- name: "GenPRESTreatmentPlanning"
- title: "GenPRES Treatment Planning Implementation Guide"
- status: active
- publisher: "GenPRES"
- description: "FHIR R4 implementation guide for GenPRES treatment planning integration"
- packageId: "nl.genpres.treatmentplanning"
- license: CC0-1.0
- fhirVersion: [4.0.1]
- dependsOn:
  - uri: "http://hl7.org/fhir"
    version: "4.0.1"
  - uri: "http://hl7.org/fhir/uv/sdc"
    version: "3.0.0"
  - uri: "http://hl7.org/fhir/uv/cpg"  
    version: "2.0.0"
- definition:
  - resource:
    - reference:
      - reference: "StructureDefinition/genpres-patient"
      - name: "GenPRES Patient Profile"
      - description: "Patient profile with Dutch healthcare extensions"
    - reference:
      - reference: "StructureDefinition/genpres-careplan"
      - name: "GenPRES CarePlan Profile" 
      - description: "Treatment plan as FHIR CarePlan"
    - reference:
      - reference: "StructureDefinition/genpres-medicationrequest"
      - name: "GenPRES MedicationRequest Profile"
      - description: "Medication request with G-Standard compliance"
  - page:
    - nameUrl: "index.html"
    - title: "GenPRES Treatment Planning IG"
    - generation: html
    - page:
      - nameUrl: "profiles.html" 
      - title: "FHIR Profiles"
      - generation: html
    - page:
      - nameUrl: "extensions.html"
      - title: "FHIR Extensions"  
      - generation: html
    - page:
      - nameUrl: "terminology.html"
      - title: "Terminology"
      - generation: html
    - page:
      - nameUrl: "examples.html"
      - title: "Examples"
      - generation: html
```

### 12.2 Testing and Validation

```fsharp
// FHIR validation and testing framework
type FhirTestingFramework = {
    // Profile Validation
    ProfileValidation: ProfileValidationConfig // FHIR profile validation
    SchemaValidation: SchemaValidationConfig   // FHIR schema validation
    TerminologyValidation: TerminologyValidationConfig // Terminology validation
    
    // Integration Testing
    OperationTesting: OperationTestConfig      // FHIR operation testing
    WorkflowTesting: WorkflowTestConfig        // End-to-end workflow testing
    PerformanceTesting: PerformanceTestConfig  // Performance testing
    
    // Compliance Testing
    G-StandardTesting: GStandardTestConfig     // G-Standard compliance testing
    SecurityTesting: SecurityTestConfig        // OAuth2/SMART security testing
    IHETesting: IHETestConfig                  // IHE profile compliance testing
}

// FHIR TestScript resources for automated testing
TestScript: bootstrap-operation-test
- url: "http://genpres.nl/fhir/TestScript/bootstrap-operation-test"
- version: "1.0.0"
- name: "BootstrapOperationTest"
- status: active
- title: "Test bootstrap operation with patient context"
- description: "Validates the $bootstrap operation with complete patient context"
- origin:
  - index: 1
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-origin-types"
    - code: "FHIR-Client"
- destination:
  - index: 1  
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-destination-types"
    - code: "FHIR-Server"
- fixture:
  - id: "patient-context-bundle"
  - autocreate: false
  - autodelete: false
  - resource:
    - reference: "Bundle/patient-context-example"
- variable:
  - name: "patientId"
  - defaultValue: "pat-123"
  - sourceId: "patient-context-bundle"
  - expression: "Bundle.entry.where(resource is Patient).resource.id"
- test:
  - id: "bootstrap-success"
  - name: "Bootstrap Operation Success"
  - description: "Test successful bootstrap operation"
  - action:
    - operation:
      - type:
        - system: "http://terminology.hl7.org/CodeSystem/testscript-operation-codes"
        - code: "create"
      - resource: "Bundle"
      - description: "Execute bootstrap operation"  
      - destination: 1
      - origin: 1
      - params: "/$bootstrap"
      - sourceId: "patient-context-bundle"
    - assert:
      - description: "Confirm that the returned HTTP status is 200(OK)"
      - direction: response
      - responseCode: "200"
    - assert:
      - description: "Confirm that the response is an OperationOutcome"
      - direction: response
      - resource: "OperationOutcome"
    - assert:
      - description: "Confirm successful outcome"
      - direction: response
      - expression: "OperationOutcome.issue.where(severity='information').exists()"

// Example test data for FHIR testing
Bundle: patient-context-test-data
- resourceType: Bundle
- type: collection
- entry:
  - resource:
    - resourceType: Patient
    - id: test-patient-001
    - identifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
      - value: "999999999"
    - birthDate: "2015-03-15"
    - gender: male
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
        valueQuantity:
          value: 39
          unit: weeks
          system: http://unitsofmeasure.org
          code: wk
  - resource:
    - resourceType: Observation
    - status: final
    - category:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/observation-category
        - code: vital-signs
    - code:
      - coding:
        - system: http://loinc.org
        - code: "29463-7"
        - display: "Body weight"
    - subject:
      - reference: "Patient/test-patient-001"
    - valueQuantity:
      - value: 18.5
      - unit: kg
      - system: http://unitsofmeasure.org  
      - code: kg
```

### 12.3 Data Migration and Integration

```fsharp
// FHIR data migration strategies
type FhirDataMigration = {
    // Legacy Data Transformation
    LegacyToFhir: TransformationConfig     // Transform legacy data to FHIR
    GStandardMapping: MappingConfig        // Map G-Standard data to FHIR
    
    // Data Validation
    MigrationValidation: ValidationConfig  // Validate migrated data
    QualityAssurance: QualityConfig        // Data quality checks
    
    // Incremental Migration
    IncrementalSync: SyncConfig            // Incremental data synchronization
    ConflictResolution: ConflictConfig     // Handle data conflicts
}

// FHIR ConceptMap for G-Standard to FHIR mapping
ConceptMap: gstandard-to-fhir-medication
- url: "http://genpres.nl/fhir/ConceptMap/gstandard-to-fhir-medication"
- version: "1.0.0"
- name: "GStandardToFHIRMedication" 
- title: "G-Standard to FHIR Medication Mapping"
- status: active
- publisher: "GenPRES"
- description: "Mapping G-Standard medication codes to FHIR Medication resource elements"
- sourceUri: "http://gstandard.nl/fhir/CodeSystem/gpk"
- targetUri: "http://hl7.org/fhir/StructureDefinition/Medication"
- group:
  - source: "http://gstandard.nl/fhir/CodeSystem/gpk"
  - target: "http://snomed.info/sct"
  - element:
    - code: "12345" # GPK code
    - display: "Paracetamol tablet 500mg"
    - target:
      - code: "387517004"
      - display: "Paracetamol"
      - equivalence: equivalent
  - element:
    - code: "67890" # GPK code  
    - display: "Amoxicillin capsule 250mg"
    - target:
      - code: "27658006"
      - display: "Amoxicillin"
      - equivalence: equivalent

// FHIR StructureMap for data transformation
StructureMap: legacy-patient-to-fhir
- url: "http://genpres.nl/fhir/StructureMap/legacy-patient-to-fhir"
- version: "1.0.0"
- name: "LegacyPatientToFHIR"
- status: active
- title: "Transform legacy patient data to FHIR Patient"
- description: "Transforms legacy EHR patient data to FHIR Patient resource"
- structure:
  - url: "http://genpres.nl/fhir/StructureDefinition/legacy-patient"
  - mode: source
  - alias: "LegacyPatient"
- structure:
  - url: "http://hl7.org/fhir/StructureDefinition/Patient"  
  - mode: target
  - alias: "FHIRPatient"
- group:
  - name: "patient"
  - typeMode: none
  - input:
    - name: "source"
    - type: "LegacyPatient"
    - mode: source
  - input:
    - name: "target"
    - type: "FHIRPatient" 
    - mode: target
  - rule:
    - name: "patient-id"
    - source:
      - context: "source"
      - element: "patientId"
      - variable: "pid"
    - target:
      - context: "target"
      - contextType: variable
      - element: "id"
      - transform: copy
      - parameter:
        - valueId: "pid"
  - rule:
    - name: "birth-date"
    - source:
      - context: "source"
      - element: "dateOfBirth" 
      - variable: "dob"
    - target:
      - context: "target"
      - contextType: variable
      - element: "birthDate"
      - transform: copy
      - parameter:
        - valueId: "dob"
```

## 13. Appendices

### Appendix A: FHIR Profiles and Extensions

```fsh

// Complete FHIR profile definitions
StructureDefinition: GenPRESPatient
- url: http://genpres.nl/fhir/StructureDefinition/genpres-patient
- version: 1.2.0
- name: GenPRESPatient  
- title: GenPRES Patient Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: Patient
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Patient
- derivation: constraint
- differential:
  - element:
    - id: Patient.identifier
    - path: Patient.identifier
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: Patient.identifier:BSN
    - path: Patient.identifier
    - sliceName: BSN
    - min: 0
    - max: 1
    - type:
      - code: Identifier
    - patternIdentifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
  - element:  
    - id: Patient.birthDate
    - path: Patient.birthDate
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: Patient.extension:gestationalAge
    - path: Patient.extension
    - sliceName: gestationalAge
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gestational-age]

StructureDefinition: GenPRESCarePlan
- url: http://genpres.nl/fhir/StructureDefinition/genpres-careplan
- version: 1.2.0
- name: GenPRESCarePlan
- title: GenPRES CarePlan Profile  
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: CarePlan
- baseDefinition: http://hl7.org/fhir/StructureDefinition/CarePlan
- derivation: constraint
- differential:
  - element:
    - id: CarePlan.status
    - path: CarePlan.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: CarePlan.intent
    - path: CarePlan.intent  
    - min: 1
    - max: 1
    - fixedCode: plan
    - mustSupport: true
  - element:
    - id: CarePlan.subject
    - path: CarePlan.subject
    - min: 1  
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: CarePlan.extension:gstandardCompliance
    - path: CarePlan.extension
    - sliceName: gstandardCompliance
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-compliance]

StructureDefinition: GenPRESMedicationRequest
- url: http://genpres.nl/fhir/StructureDefinition/genpres-medicationrequest
- version: 1.2.0
- name: GenPRESMedicationRequest
- title: GenPRES MedicationRequest Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: MedicationRequest
- baseDefinition: http://hl7.org/fhir/StructureDefinition/MedicationRequest
- derivation: constraint
- differential:
  - element:
    - id: MedicationRequest.status
    - path: MedicationRequest.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.intent
    - path: MedicationRequest.intent
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.medication[x]
    - path: MedicationRequest.medication[x]
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-medication]
    - mustSupport: true
  - element:
    - id: MedicationRequest.subject  
    - path: MedicationRequest.subject
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: MedicationRequest.dosageInstruction
    - path: MedicationRequest.dosageInstruction
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: MedicationRequest.extension:weightBasedDose
    - path: MedicationRequest.extension
    - sliceName: weightBasedDose
    - min: 0
    - max: 1  
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/weight-based-dose]
  - element:
    - id: MedicationRequest.extension:gstandardDoseRule
    - path: MedicationRequest.extension
    - sliceName: gstandardDoseRule
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule]
```

### Appendix B: G-Standard Integration Specifications

```fsh

// G-Standard specific extensions
StructureDefinition: GStandardCompliance
- url: http://genpres.nl/fhir/StructureDefinition/gstandard-compliance
- version: 1.2.0
- name: GStandardCompliance
- title: G-Standard Compliance Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: CarePlan
  - type: element
  - expression: MedicationRequest
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: G-Standard compliance information
    - definition: Detailed information about G-Standard compliance validation
  - element:
    - id: Extension.extension:level
    - path: Extension.extension
    - sliceName: level
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:level.url
    - path: Extension.extension.url
    - fixedUri: level
  - element:
    - id: Extension.extension:level.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/compliance-level
  - element:
    - id: Extension.extension:validationTimestamp
    - path: Extension.extension
    - sliceName: validationTimestamp
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:validationTimestamp.url
    - path: Extension.extension.url
    - fixedUri: validation-timestamp
  - element:
    - id: Extension.extension:validationTimestamp.value[x]
    - path: Extension.extension.value[x]  
    - type:
      - code: dateTime
  - element:
    - id: Extension.extension:gstandardVersion
    - path: Extension.extension
    - sliceName: gstandardVersion
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:gstandardVersion.url
    - path: Extension.extension.url
    - fixedUri: gstandard-version
  - element:
    - id: Extension.extension:gstandardVersion.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: string

StructureDefinition: WeightBasedDose
- url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
- version: 1.2.0
- name: WeightBasedDose
- title: Weight-Based Dose Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: MedicationRequest
  - type: element  
  - expression: Dosage
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: Weight-based dosing information
    - definition: Information about weight-based dose calculation
  - element:
    - id: Extension.extension:dosePerKg
    - path: Extension.extension
    - sliceName: dosePerKg
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:dosePerKg.url
    - path: Extension.extension.url
    - fixedUri: dose-per-kg
  - element:
    - id: Extension.extension:dosePerKg.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:patientWeight
    - path: Extension.extension
    - sliceName: patientWeight
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:patientWeight.url
    - path: Extension.extension.url
    - fixedUri: patient-weight
  - element:
    - id: Extension.extension:patientWeight.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:weightSource
    - path: Extension.extension
    - sliceName: weightSource
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:weightSource.url
    - path: Extension.extension.url
    - fixedUri: weight-source  
  - element:
    - id: Extension.extension:weightSource.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/weight-source
```

### Appendix C: API Documentation

```yaml
## Complete OpenAPI 3.0 specification
openapi: 3.0.3
info:
  title: GenPRES FHIR Treatment Planning API
  description: |
    FHIR R4 compliant API for GenPRES treatment planning integration.
    Supports G-Standard compliance, OAuth2/SMART-on-FHIR security,
    and IHE Pharmacy profile compliance.
  version: 1.2.0
  contact:
    name: GenPRES API Support
    url: https://support.genpres.nl
    email: api-support@genpres.nl
  license:
    name: Proprietary
    url: https://genpres.nl/license

servers:
  - url: https://api.genpres.nl/fhir
    description: Production FHIR server
  - url: https://staging-api.genpres.nl/fhir  
    description: Staging FHIR server
  - url: https://dev-api.genpres.nl/fhir
    description: Development FHIR server

security:
  - OAuth2: [patient/*.read, patient/*.write, user/*.read]
  - BearerAuth: []

paths:
  /$bootstrap:
    post:
      tags: [Treatment Planning]
      summary: Initialize patient context for treatment planning
      description: |
        Bootstrap operation that establishes patient context for treatment planning.
        Accepts a Bundle containing Patient and related clinical resources.
      operationId: bootstrap
      security:
        - OAuth2: [patient/*.read, launch, launch/patient]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            examples:
              patient-context:
                summary: Patient context with vital signs and allergies
                value:
                  resourceType: Bundle
                  type: collection
                  entry:
                    - resource:
                        resourceType: Patient
                        id: pat-123
                        birthDate: "2010-05-15"
                        gender: male
      responses:
        '200':
          description: Bootstrap successful
          content:
            application/fhir+json:: TREAT
        display: "Treatment"
      - system: http://genpres.nl/fhir/CodeSystem/security-label  
        code
        code:
          $ref: '#/components/schemas/CodeableConcept'
        form:
          $ref: '#/components/schemas/CodeableConcept'
        ingredient:
          type: array
          items:
            $ref: '#/components/schemas/MedicationIngredient'
        extension:
          type: array
          items:
            $ref: '#/components/schemas/Extension'

    MedicationIngredient:
      type: object
      properties:
        itemCodeableConcept:
          $ref: '#/components/schemas/CodeableConcept'
        strength:
          $ref: '#/components/schemas/Ratio'
        isActive:
          type: boolean

    Dosage:
      type: object
      properties:
        text:
          type: string
        route:
          $ref: '#/components/schemas/CodeableConcept'
        doseAndRate:
          type: array
          items:
            $ref: '#/components/schemas/DosageDoseAndRate'

    DosageDoseAndRate:
      type: object
      properties:
        doseQuantity:
          $ref: '#/components/schemas/Quantity'
        rateQuantity:
          $ref: '#/components/schemas/Quantity'

    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'

    ParametersParameter:
      type: object
      required: [name]
      properties:
        name:
          type: string
        valueString:
          type: string
        valueBoolean:
          type: boolean
        valueInteger:
          type: integer
        valueDecimal:
          type: number
        valueDateTime:
          type: string
          format: date-time
        valueCode:
          type: string
        resource:
          type: object

    OperationOutcome:
      type: object
      required: [resourceType, issue]
      properties:
        resourceType:
          type: string
          enum: [OperationOutcome]
        issue:
          type: array
          minItems: 1
          items:
            $ref: '#/components/schemas/OperationOutcomeIssue'

    OperationOutcomeIssue:
      type: object
      required: [severity, code]
      properties:
        severity:
          type: string
          enum: [fatal, error, warning, information]
        code:
          type: string
          enum: [invalid, structure, required, value, invariant, security, login, unknown, expired, forbidden, suppressed, processing, not-supported, duplicate, multiple-matches, not-found, deleted, too-long, code-invalid, extension, too-costly, business-rule, conflict, transient, lock-error, no-store, exception, timeout, incomplete, throttled, informational]
        details:
          $ref: '#/components/schemas/CodeableConcept'
        diagnostics:
          type: string
        location:
          type: array
          items:
            type: string
        expression:
          type: array
          items:
            type: string

    CodeableConcept:
      type: object
      properties:
        coding:
          type: array
          items:
            $ref: '#/components/schemas/Coding'
        text:
          type: string

    Coding:
      type: object
      properties:
        system:
          type: string
          format: uri
        version:
          type: string
        code:
          type: string
        display:
          type: string
        userSelected:
          type: boolean

    Reference:
      type: object
      properties:
        reference:
          type: string
        type:
          type: string
        identifier:
          $ref: '#/components/schemas/Identifier'
        display:
          type: string

    Identifier:
      type: object
      properties:
        use:
          type: string
          enum: [usual, official, temp, secondary, old]
        type:
          $ref: '#/components/schemas/CodeableConcept'
        system:
          type: string
          format: uri
        value:
          type: string
        period:
          $ref: '#/components/schemas/Period'
        assigner:
          $ref: '#/components/schemas/Reference'

    Period:
      type: object
      properties:
        start:
          type: string
          format: date-time
        end:
          type: string
          format: date-time

    Quantity:
      type: object
      properties:
        value:
          type: number
        comparator:
          type: string
          enum: ['<', '<=', '>=', '>']
        unit:
          type: string
        system:
          type: string
          format: uri
        code:
          type: string

    Ratio:
      type: object
      properties:
        numerator:
          $ref: '#/components/schemas/Quantity'
        denominator:
          $ref: '#/components/schemas/Quantity'

    Extension:
      type: object
      required: [url]
      properties:
        url:
          type: string
          format: uri
        valueString:
          type: string
        valueBoolean:
          type: boolean
        valueInteger:
          type: integer
        valueDecimal:
          type: number
        valueDateTime:
          type: string
          format: date-time
        valueCode:
          type: string
        valueQuantity:
          $ref: '#/components/schemas/Quantity'
        extension:
          type: array
          items:
            $ref: '#/components/schemas/Extension'

  responses:
    Unauthorized:
      description: Authentication required
      content:
        application/fhir+json:
          schema:
            $ref: '#/components/schemas/OperationOutcome'
          example:
            resourceType: OperationOutcome
            issue:
              - severity: error
                code: login
                details:
                  text: "Authentication required"

    Forbidden:
      description: Insufficient permissions
      content:
        application/fhir+json:
          schema:
            $ref: '#/components/schemas/OperationOutcome'
          example:
            resourceType: OperationOutcome
            issue:
              - severity: error
                code: forbidden
                details:
                  text: "Insufficient permissions for this operation"

    ValidationError:
      description: FHIR validation error
      content:
        application/fhir+json:
          schema:
            $ref: '#/components/schemas/OperationOutcome'
          example:
            resourceType: OperationOutcome
            issue:
              - severity: error
                code: invalid
                details:
                  text: "Resource validation failed"

  examples:
    PatientContextBundle:
      summary: Complete patient context for treatment planning
      value:
        resourceType: Bundle
        type: collection
        entry:
          - resource:
              resourceType: Patient
              id: pat-pediatric-001
              identifier:
                - system: http://fhir.nl/fhir/NamingSystem/bsn
                  value: "123456789"
              birthDate: "2015-03-15"
              gender: male
              extension:
                - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
                  valueQuantity:
                    value: 39
                    unit: weeks
                    system: http://unitsofmeasure.org
                    code: wk
          - resource:
              resourceType: Observation
              status: final
              category:
                - coding:
                    - system: http://terminology.hl7.org/CodeSystem/observation-category
                      code: vital-signs
              code:
                coding:
                  - system: http://loinc.org
                    code: "29463-7"
                    display: Body weight
              subject:
                reference: Patient/pat-pediatric-001
              valueQuantity:
                value: 18.5
                unit: kg
                system: http://unitsofmeasure.org
                code: kg
              extension:
                - url: http://genpres.nl/fhir/StructureDefinition/weight-percentile
                  valueQuantity:
                    value: 75
                    unit: "%"
                    system: http://unitsofmeasure.org
                    code: "%"
```

### Appendix D: Sample Implementation Code

```fsharp
// Complete F# implementation examples for FHIR-based GenPRES integration

module GenPresFhirClient =
    open System
    open System.Net.Http
    open System.Text
    open System.Text.Json
    open Microsoft.Extensions.Logging

    // FHIR resource types
    type FhirResource = 
        | Patient of Patient
        | CarePlan of CarePlan  
        | MedicationRequest of MedicationRequest
        | Medication of Medication
        | Bundle of Bundle
        | OperationOutcome of OperationOutcome
        | Parameters of Parameters

    // OAuth2 token for SMART-on-FHIR
    type SmartToken = {
        AccessToken: string
        TokenType: string
        ExpiresIn: int
        Scope: string
        PatientId: string option
        FhirUser: string option
    }

    // FHIR client configuration
    type FhirClientConfig = {
        BaseUrl: string
        AuthToken: SmartToken
        Timeout: TimeSpan
        RetryPolicy: RetryPolicy
    }

    // GenPRES FHIR operations client
    type GenPresFhirClient(config: FhirClientConfig, logger: ILogger) =
        let httpClient = new HttpClient()
        
        do
            httpClient.BaseAddress <- Uri(config.BaseUrl)
            httpClient.Timeout <- config.Timeout
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.AuthToken.AccessToken}")
            httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json")

        // Bootstrap patient context
        member this.BootstrapAsync(patientContextBundle: Bundle): Async<Result<OperationOutcome, FhirError>> =
            async {
                try
                    let json = JsonSerializer.Serialize(patientContextBundle)
                    let content = new StringContent(json, Encoding.UTF8, "application/fhir+json")
                    
                    let! response = httpClient.PostAsync("/$bootstrap", content) |> Async.AwaitTask
                    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    
                    if response.IsSuccessStatusCode then
                        let outcome = JsonSerializer.Deserialize<OperationOutcome>(responseContent)
                        return Ok outcome
                    else
                        let errorOutcome = JsonSerializer.Deserialize<OperationOutcome>(responseContent)
                        return Error (FhirOperationError errorOutcome)
                        
                with ex ->
                    logger.LogError(ex, "Bootstrap operation failed")
                    return Error (FhirClientError ex.Message)
            }

        // Request treatment plan calculation
        member this.RequestTreatmentPlanAsync(request: Parameters): Async<Result<Bundle, FhirError>> =
            async {
                try
                    let json = JsonSerializer.Serialize(request)
                    let content = new StringContent(json, Encoding.UTF8, "application/fhir+json")
                    
                    let! response = httpClient.PostAsync("/$treatment-plan-request", content) |> Async.AwaitTask
                    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    
                    if response.IsSuccessStatusCode then
                        let bundle = JsonSerializer.Deserialize<Bundle>(responseContent)
                        return Ok bundle
                    else
                        let errorOutcome = JsonSerializer.Deserialize<OperationOutcome>(responseContent)
                        return Error (FhirOperationError errorOutcome)
                        
                with ex ->
                    logger.LogError(ex, "Treatment plan request failed")
                    return Error (FhirClientError ex.Message)
            }

        // Finalize treatment plan
        member this.FinalizeAsync(carePlan: CarePlan, sessionId: string): Async<Result<Bundle, FhirError>> =
            async {
                try
                    let json = JsonSerializer.Serialize(carePlan)
                    let content = new StringContent(json, Encoding.UTF8, "application/fhir+json")
                    
                    httpClient.DefaultRequestHeaders.Add("X-Session-ID", sessionId)
                    
                    let! response = httpClient.PostAsync("/$finalize", content) |> Async.AwaitTask
                    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    
                    if response.IsSuccessStatusCode then
                        let bundle = JsonSerializer.Deserialize<Bundle>(responseContent)
                        return Ok bundle
                    else
                        let errorOutcome = JsonSerializer.Deserialize<OperationOutcome>(responseContent)
                        return Error (FhirOperationError errorOutcome)
                        
                with ex ->
                    logger.LogError(ex, "Finalization failed")
                    return Error (FhirClientError ex.Message)
            }

        // G-Standard medication validation
        member this.ValidateGStandardAsync(medication: Medication): Async<Result<OperationOutcome, FhirError>> =
            async {
                try
                    let parameters = {
                        ResourceType = "Parameters"
                        Parameter = [
                            {
                                Name = "medication"
                                Resource = Some (Medication medication)
                            }
                            {
                                Name = "validation-level"
                                ValueCode = Some "comprehensive"
                            }
                        ]
                    }
                    
                    let json = JsonSerializer.Serialize(parameters)
                    let content = new StringContent(json, Encoding.UTF8, "application/fhir+json")
                    
                    let! response = httpClient.PostAsync("/Medication/$validate-gstandard", content) |> Async.AwaitTask
                    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    
                    if response.IsSuccessStatusCode then
                        let outcome = JsonSerializer.Deserialize<OperationOutcome>(responseContent)
                        return Ok outcome
                    else
                        let errorOutcome = JsonSerializer.Deserialize<OperationOutcome>(responseContent)
                        return Error (FhirOperationError errorOutcome)
                        
                with ex ->
                    logger.LogError(ex, "G-Standard validation failed")
                    return Error (FhirClientError ex.Message)
            }

    // FHIR bundle builder for patient context
    module FhirBundleBuilder =
        
        let createPatientContextBundle (patient: Patient) (observations: Observation[]) (allergies: AllergyIntolerance[]) =
            {
                ResourceType = "Bundle"
                Type = "collection"
                Entry = [
                    yield { Resource = Patient patient; FullUrl = Some $"Patient/{patient.Id}" }
                    for obs in observations do
                        yield { Resource = Observation obs; FullUrl = Some $"Observation/{obs.Id}" }
                    for allergy in allergies do
                        yield { Resource = AllergyIntolerance allergy; FullUrl = Some $"AllergyIntolerance/{allergy.Id}" }
                ]
            }

        let createTreatmentPlanRequest (patientId: string) (clinicalIntent: string) (medications: string[]) =
            {
                ResourceType = "Parameters"
                Parameter = [
                    {
                        Name = "careplan-intent"
                        Resource = Some (CarePlan {
                            ResourceType = "CarePlan"
                            Status = "draft"
                            Intent = "plan"
                            Subject = { Reference = Some $"Patient/{patientId}"; Display = None }
                            Activity = [
                                for medication in medications do
                                    yield {
                                        Detail = Some {
                                            Kind = Some "MedicationRequest"
                                            Code = Some {
                                                Coding = [
                                                    {
                                                        System = Some "http://gstandard.nl/fhir/CodeSystem/generic"
                                                        Code = Some medication
                                                        Display = Some medication
                                                    }
                                                ]
                                                Text = None
                                            }
                                            Status = "not-started"
                                            Description = Some clinicalIntent
                                        }
                                    }
                            ]
                        })
                    }
                    {
                        Name = "clinical-priority"
                        ValueCode = Some "routine"
                    }
                ]
            }

    // G-Standard integration helpers
    module GStandardHelpers =
        
        let createGStandardMedication (gpkCode: string) (genericName: string) (strength: string) =
            {
                ResourceType = "Medication"
                Id = Some $"med-gpk-{gpkCode}"
                Code = Some {
                    Coding = [
                        {
                            System = Some "http://gstandard.nl/fhir/CodeSystem/gpk"
                            Code = Some gpkCode
                            Display = Some $"{genericName} {strength}"
                        }
                        {
                            System = Some "http://gstandard.nl/fhir/CodeSystem/generic"
                            Code = Some genericName
                            Display = Some genericName
                        }
                    ]
                    Text = Some $"{genericName} {strength}"
                }
                Extension = Some [
                    {
                        Url = "http://genpres.nl/fhir/StructureDefinition/gstandard-product-info"
                        Extension = Some [
                            {
                                Url = "gpk-code"
                                ValueString = Some gpkCode
                            }
                            {
                                Url = "validation-status"
                                ValueCode = Some "validated"
                            }
                        ]
                    }
                ]
            }

        let addWeightBasedDosing (medicationRequest: MedicationRequest) (dosePerKg: float) (patientWeight: float) =
            let calculatedDose = dosePerKg * patientWeight
            let weightBasedExtension = {
                Url = "http://genpres.nl/fhir/StructureDefinition/weight-based-dose"
                Extension = Some [
                    {
                        Url = "dose-per-kg"
                        ValueQuantity = Some {
                            Value = Some dosePerKg
                            Unit = Some "mg/kg"
                            System = Some "http://unitsofmeasure.org"
                            Code = Some "mg/kg"
                        }
                    }
                    {
                        Url = "patient-weight"
                        ValueQuantity = Some {
                            Value = Some patientWeight
                            Unit = Some "kg"
                            System = Some "http://unitsofmeasure.org"
                            Code = Some "kg"
                        }
                    }
                ]
            }
            
            { medicationRequest with
                Extension = 
                    match medicationRequest.Extension with
                    | Some existing -> Some (weightBasedExtension :: existing)
                    | None -> Some [weightBasedExtension]
                DosageInstruction = 
                    match medicationRequest.DosageInstruction with
                    | Some dosages ->
                        Some (dosages |> List.map (fun dosage ->
                            { dosage with
                                DoseAndRate = Some [
                                    {
                                        DoseQuantity = Some {
                                            Value = Some calculatedDose
                                            Unit = Some "mg"
                                            System = Some "http://unitsofmeasure.org"
                                            Code = Some "mg"
                                        }
                                    }
                                ]
                            }))
                    | None -> None
            }

    // Usage example
    module UsageExample =
        open Microsoft.Extensions.Logging.Abstractions
        
        let exampleUsage () =
            async {
                // Configure FHIR client with SMART-on-FHIR token
                let token = {
                    AccessToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
                    TokenType = "Bearer"
                    ExpiresIn = 3600
                    Scope = "patient/*.read patient/*.write launch/patient"
                    PatientId = Some "pat-123"
                    FhirUser = Some "Practitioner/dr-smith"
                }
                
                let config = {
                    BaseUrl = "https://api.genpres.nl/fhir"
                    AuthToken = token
                    Timeout = TimeSpan.FromSeconds(30)
                    RetryPolicy = DefaultRetryPolicy
                }
                
                let client = GenPresFhirClient(config, NullLogger.Instance)
                
                // Create patient context bundle
                let patient = {
                    ResourceType = "Patient"
                    Id = Some "pat-pediatric-001"
                    BirthDate = Some "2015-03-15"
                    Gender = Some "male"
                    Identifier = Some [
                        {
                            System = Some "http://fhir.nl/fhir/NamingSystem/bsn"
                            Value = Some "123456789"
                        }
                    ]
                    Extension = Some [
                        {
                            Url = "http://genpres.nl/fhir/StructureDefinition/gestational-age"
                            ValueQuantity = Some {
                                Value = Some 39.0
                                Unit = Some "weeks"
                                System = Some "http://unitsofmeasure.org"
                                Code = Some "wk"
                            }
                        }
                    ]
                }
                
                let weightObservation = {
                    ResourceType = "Observation"
                    Id = Some "obs-weight-001"
                    Status = "final"
                    Code = {
                        Coding = [
                            {
                                System = Some "http://loinc.org"
                                Code = Some "29463-7"
                                Display = Some "Body weight"
                            }
                        ]
                    }
                    Subject = { Reference = Some "Patient/pat-pediatric-001" }
                    ValueQuantity = Some {
                        Value = Some 18.5
                        Unit = Some "kg"
                        System = Some "http://unitsofmeasure.org"
                        Code = Some "kg"
                    }
                }
                
                let patientContextBundle = FhirBundleBuilder.createPatientContextBundle patient [weightObservation] []
                
                // Bootstrap patient context
                let! bootstrapResult = client.BootstrapAsync(patientContextBundle)
                
                match bootstrapResult with
                | Ok outcome ->
                    printfn "Bootstrap successful: %A" outcome
                    
                    // Request treatment plan
                    let treatmentRequest = FhirBundleBuilder.createTreatmentPlanRequest "pat-pediatric-001" "Post-operative pain management" [|"PARACETAMOL"|]
                    let! planResult = client.RequestTreatmentPlanAsync(treatmentRequest)
                    
                    match planResult with
                    | Ok treatmentBundle ->
                        printfn "Treatment plan calculated successfully"
                        
                        // Extract CarePlan from bundle for finalization
                        let carePlan = 
                            treatmentBundle.Entry
                            |> List.tryPick (fun entry ->
                                match entry.Resource with
                                | CarePlan cp -> Some cp
                                | _ -> None)
                        
                        match carePlan with
                        | Some cp ->
                            let! finalizeResult = client.FinalizeAsync(cp, Guid.NewGuid().ToString())
                            
                            match finalizeResult with
                            | Ok finalBundle ->
                                printfn "Treatment plan finalized successfully"
                                return Ok finalBundle
                            | Error err ->
                                printfn "Finalization failed: %A" err
                                return Error err
                        | None ->
                            printfn "No CarePlan found in treatment bundle"
                            return Error (FhirClientError "No CarePlan in response")
                            
                    | Error err ->
                        printfn "Treatment plan request failed: %A" err
                        return Error err
                        
                | Error err ->
                    printfn "Bootstrap failed: %A" err
                    return Error err
            }
```

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | September 2025 | Solution Architecture Team | Initial complete specification |
| 1.1 | January 2026 | FHIR Integration Team | FHIR/IHE compliance revision |
| 1.2 | January 2026 | Integration Team | Merged comprehensive FHIR-compliant specification |

---

## Document Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Solution Architect | [Name] | [Signature] | [Date] |
| Clinical Lead | [Name] | [Signature] | [Date] |
| Integration Lead | [Name] | [Signature] | [Date] |
| Security Officer | [Name] | [Signature] | [Date] |
| FHIR Compliance Officer | [Name] | [Signature] | [Date] |

---

## Summary of Changes in Version 1.2

This merged specification incorporates all three source documents while ensuring full FHIR R4 compliance:

### Key Integration Points

1. **FHIR-First Architecture**: All data exchanges now use proper FHIR R4 resources with complete resource definitions, proper references, and standard operations.

2. **G-Standard Integration**: G-Standard compliance achieved through FHIR coding systems, extensions, and validation operations rather than proprietary structures.

3. **IHE Profile Compliance**: Added full IHE Pharmacy, ATNA, and IUA profile compliance with proper resource profiles and security implementations.

4. **OAuth2/SMART-on-FHIR Security**: Replaced proprietary authentication with industry-standard SMART-on-FHIR security model.

5. **Clinical Safety**: Maintained all clinical safety requirements while expressing them through FHIR DetectedIssue, RiskAssessment, and CDS Hooks integration.

6. **Session Management**: Session management now uses FHIR Communication and Task resources instead of proprietary session structures.

7. **Performance and Scalability**: All performance requirements maintained while ensuring FHIR compliance through proper Bundle management and resource optimization.

The specification preserves all clinical intent from the original documents while providing a standards-compliant, interoperable interface that meets Dutch healthcare requirements and international FHIR standards.

*This document contains confidential and proprietary information. Distribution is restricted to authorized personnel only.*
              schema:
                $ref: '#/components/schemas/OperationOutcome'

## Treatment Plan Request

  /fhir/$treatment-plan-request:
    post:
      summary: Request treatment plan calculation
      operationId: treatmentPlanRequest
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Treatment plan calculated successfully
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

## Treatment Plan Finalization

  /fhir/$finalize:
    post:
      summary: Finalize treatment plan for EHR implementation
      operationId: finalize
      parameters:
        - name: session-id
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/CarePlan'
      responses:
        '200':
          description: Treatment plan finalized
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

## G-Standard Validation

  /fhir/Medication/$validate-gstandard:
    post:
      summary: Validate medication against G-Standard
      operationId: validateGStandard
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Validation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/OperationOutcome'

## Simulation (Dry Run)

  /fhir/$simulate:
    post:
      summary: Simulate treatment plan without persistence
      operationId: simulate
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Parameters'
      responses:
        '200':
          description: Simulation completed
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/Bundle'

```yaml
components:
  schemas:
    Bundle:
      type: object
      required: [resourceType, type]
      properties:
        resourceType:
          type: string
          enum: [Bundle]
        type:
          type: string
          enum: [collection, transaction, searchset, batch]
        entry:
          type: array
          items:
            $ref: '#/components/schemas/BundleEntry'

    Parameters:
      type: object
      required: [resourceType]
      properties:
        resourceType:
          type: string
          enum: [Parameters]
        parameter:
          type: array
          items:
            $ref: '#/components/schemas/ParametersParameter'

```
```

### 8.2 OAuth2/SMART-on-FHIR Security Integration

```
// SMART-on-FHIR security context
type SmartOnFhirSecurity = {
    // OAuth2 Configuration
    AuthorizationServer: string           // EHR authorization server
    TokenEndpoint: string                 // OAuth2 token endpoint
    IntrospectionEndpoint: string         // Token introspection endpoint
    
    // SMART Scopes
    RequiredScopes: SmartScope[]          // Required SMART scopes
    OptionalScopes: SmartScope[]          // Optional enhanced scopes
    
    // Token Management
    AccessToken: AccessToken              // Current access token
    RefreshToken: RefreshToken option     // Refresh token if available
    TokenExpiration: DateTime             // Token expiration time
    
    // FHIR Security
    FhirSecurityLabels: SecurityLabel[]   // FHIR security labels
    AuditRequirements: AuditRequirement[] // IHE ATNA compliance
}

// SMART-on-FHIR scopes for GenPRES integration
type SmartScope =
    | PatientRead                         // patient/*.read
    | PatientWrite                        // patient/*.write
    | UserRead                            // user/*.read
    | UserWrite                           // user/*.write
    | SystemRead                          // system/*.read
    | SystemWrite                         // system/*.write
    | Launch                              // launch
    | LaunchPatient                       // launch/patient
    | FhirUser                            // fhirUser
    | OpenIdProfile                       // profile openid

// OAuth2 token validation for FHIR requests
let validateFhirToken (token: string) : Async<Result<FhirSecurityContext, SecurityError>> =
    async {
        try
            // Introspect token with EHR authorization server
            let! introspectionResult = introspectToken token
            
            match introspectionResult with
            | Error error -> return Error error
            | Ok tokenInfo ->
                
                // Validate required scopes
                let requiredScopes = [
                    "patient/Patient.read"
                    "patient/CarePlan.write" 
                    "patient/MedicationRequest.write"
                    "patient/Medication.read"
                    "user/Practitioner.read"
                ]
                
                let hasRequiredScopes = 
                    requiredScopes 
                    |> List.forall (fun scope -> tokenInfo.Scopes |> List.contains scope)
                
                if not hasRequiredScopes then
                    return Error (InsufficientScopes requiredScopes)
                else
                    let securityContext = {
                        UserId = tokenInfo.Sub
                        PatientId = tokenInfo.Patient
                        Scopes = tokenInfo.Scopes
                        FhirUser = tokenInfo.FhirUser
                        ExpiresAt = tokenInfo.ExpiresAt
                    }
                    return Ok securityContext
        with
        | ex -> return Error (TokenValidationError ex.Message)
    }
```

### 8.3 IHE Profile Compliance

```

// IHE Pharmacy (PHARM) Profile Compliance
```fsh
Profile: IHE-PHARM-CarePlan
Parent: CarePlan
Title: "IHE Pharmacy CarePlan Profile"
Description: "CarePlan profile compliant with IHE Pharmacy workflow"
- status 1..1 MS
- intent 1..1 MS
- subject 1..1 MS
- activity
  - detail
    - kind 1..1 MS
    - code 1..1 MS
    - status 1..1 MS
    - medication[x] 0..1 MS

// IHE Audit Trail and Node Authentication (ATNA) Compliance
Profile: IHE-ATNA-AuditEvent
Parent: AuditEvent
Title: "IHE ATNA AuditEvent Profile"
Description: "AuditEvent profile for IHE ATNA compliance"
- type 1..1 MS
- action 1..1 MS
- recorded 1..1 MS
- outcome 1..1 MS
- agent 1..* MS
  - type 1..1 MS
  - who 1..1 MS
  - requestor 1..1 MS
- source 1..1 MS
  - observer 1..1 MS
- entity 0..* MS
  - what 0..1 MS
  - type 0..1 MS
  - role 0..1 MS

// IHE Internet User Authorization (IUA) Profile
Profile: IHE-IUA-AccessToken
Title: "IHE IUA Access Token Profile" 
Description: "OAuth2 access token profile for IHE IUA compliance"
- iss (issuer) 1..1 MS
- sub (subject) 1..1 MS  
- aud (audience) 1..1 MS
- exp (expiration) 1..1 MS
- iat (issued at) 1..1 MS
- scope 1..1 MS
- purpose_of_use 0..1 MS
- homeCommunityId 0..1 MS
- national_provider_identifier 0..1 MS
- subject_organization 0..1 MS
- subject_organization_id 0..1 MS
```
```

## 9. Clinical Safety and Compliance

### 9.1 FHIR-Based Clinical Decision Support

```

// Clinical Decision Support through FHIR CDS Hooks
CDS Hook: medication-prescribe
- hook: medication-prescribe  
- title: "GenPRES Medication Safety Check"
- description: "G-Standard compliant medication safety validation"
- id: "genpres-medication-safety"
- prefetch:
    patient: "Patient/{{context.patientId}}"
    medications: "MedicationRequest?patient={{context.patientId}}&status=active"
    allergies: "AllergyIntolerance?patient={{context.patientId}}"
    conditions: "Condition?patient={{context.patientId}}&clinical-status=active"
    observations: "Observation?patient={{context.patientId}}&category=vital-signs&_sort=-date&_count=10"

// CDS Card response for safety alerts
CDSCard: safety-alert
- summary: "G-Standard safety alert"
- detail: "Detailed safety information from G-Standard validation"
- indicator: "warning" | "info" | "critical"
- source:
  - label: "G-Standard"
  - url: "http://gstandard.nl"
  - icon: "http://gstandard.nl/icon.png"
- suggestions:
  - label: "Alternative medication"
  - actions:
    - type: "create"
    - description: "Add alternative medication"
    - resource: MedicationRequest

// DetectedIssue resource for clinical alerts
DetectedIssue: clinical-safety-alert
- status: preliminary | final | entered-in-error
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActCode
    - code: DRG | ALLERGY | DOSEAMT | DOSEDUR | DOSECOND
- severity: high | moderate | low
- patient: Reference(Patient)
- detail: "Human readable description of the issue"
- reference: "Reference to related resources"
- mitigation:
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActCode  
      - code: MONITOR | REPLACE | REDUCE | DISCONTINUE
  - description: "Description of mitigation action"
```

### 9.2 G-Standard Safety Validation

```
// G-Standard safety validation through FHIR operations
type GStandardSafetyValidation = {
    // Validation Operations
    ProductValidation: FhirOperation       // Validate product GPK codes
    DoseRuleValidation: FhirOperation      // Validate dose rules compliance
    InteractionCheck: FhirOperation        // Check drug interactions
    ContraindicationCheck: FhirOperation   // Check contraindications
    
    // Safety Assessment
    RiskAssessment: RiskAssessment         // Overall safety risk assessment
    SafetyProfile: Extension               // Comprehensive safety profile
    
    // Compliance Reporting
    ComplianceReport: Parameters           // G-Standard compliance report
    ValidationOutcome: OperationOutcome    // Validation results
}

// FHIR OperationDefinition for comprehensive safety validation
OperationDefinition: comprehensive-safety-check
- name: comprehensive-safety-check
- status: active
- kind: operation
- code: comprehensive-safety-check
- resource: [CarePlan]
- system: false
- type: false  
- instance: true
- parameter:
  - name: gstandard-version
    use: in
    min: 0
    max: 1
    type: string
  - name: validation-level  
    use: in
    min: 0
    max: 1
    type: code
    binding:
      strength: required
      valueSet: http://genpres.nl/fhir/ValueSet/validation-level
  - name: safety-report
    use: out
    min: 1
    max: 1 
    type: Parameters
  - name: detected-issues
    use: out
    min: 0
    max: "*"
    type: DetectedIssue
  - name: risk-assessment
    use: out  
    min: 0
    max: 1
    type: RiskAssessment

// RiskAssessment resource for overall safety evaluation  
RiskAssessment: treatment-plan-safety
- status: final
- subject: Reference(Patient)
- encounter: Reference(Encounter) 
- performedDateTime: {assessment-timestamp}
- performer: Reference(Device) # GenPRES system
- code:
  - coding:
    - system: http://snomed.info/sct
    - code: 225338004
    - display: "Risk assessment"
- prediction:
  - outcome:
    - coding:
      - system: http://snomed.info/sct  
      - code: 281647001
      - display: "Adverse reaction"
  - probabilityDecimal: 0.15
  - relativeRisk: 1.2
- mitigation: "Recommended monitoring and precautions"
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/gstandard-risk-factors
    extension:
      - url: age-risk
        valueDecimal: 0.1
      - url: weight-risk  
        valueDecimal: 0.05
      - url: interaction-risk
        valueDecimal: 0.3
```

### 9.3 Pediatric Safety Specializations

```

// Pediatric-specific FHIR profiles and extensions
Profile: PediatricMedicationRequest
Parent: MedicationRequest
Title: "Pediatric Medication Request"
Description: "MedicationRequest profile with pediatric safety requirements"
- subject 1..1 MS
- subject only Reference(PediatricPatient)
- dosageInstruction 1..* MS
  - doseAndRate
    - dose[x] 1..1 MS
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
        extension:
          - url: dose-per-kg
            valueQuantity 1..1 MS
          - url: patient-weight  
            valueQuantity 1..1 MS
          - url: weight-source
            valueCode 1..1 MS # measured | estimated | calculated
- extension:
  - url: http://genpres.nl/fhir/StructureDefinition/pediatric-safety-check
    extension:
      - url: age-appropriate
        valueBoolean 1..1 MS
      - url: weight-appropriate
        valueBoolean 1..1 MS  
      - url: concentration-safe
        valueBoolean 1..1 MS
      - url: volume-appropriate
        valueBoolean 1..1 MS

// Age-specific contraindication checking
CodeSystem: pediatric-age-groups
- url: http://genpres.nl/fhir/CodeSystem/pediatric-age-groups
- concept:
  - code: neonate
    display: "Neonate (0-28 days)"
    definition: "Newborn infant up to 28 days of age"
  - code: infant  
    display: "Infant (29 days - 2 years)"
    definition: "Infant from 29 days to 2 years of age"
  - code: child
    display: "Child (2-12 years)" 
    definition: "Child from 2 to 12 years of age"
  - code: adolescent
    display: "Adolescent (12-18 years)"
    definition: "Adolescent from 12 to 18 years of age"

// Pediatric dose rule validation
Extension: pediatric-dose-rule
- url: http://genpres.nl/fhir/StructureDefinition/pediatric-dose-rule
- extension:
  - url: rule-id
    valueString 1..1 MS
  - url: age-group  
    valueCode 1..1 MS # From pediatric-age-groups CodeSystem
  - url: weight-range
    extension:
      - url: min-weight
        valueQuantity 0..1 MS
      - url: max-weight 
        valueQuantity 0..1 MS
  - url: dose-calculation
    extension:
      - url: dose-per-kg
        valueQuantity 0..1 MS
      - url: dose-per-m2
        valueQuantity 0..1 MS  
      - url: max-single-dose
        valueQuantity 0..1 MS
      - url: max-daily-dose
        valueQuantity 0..1 MS
  - url: safety-considerations
    valueString 0..* MS
```

## 10. Performance and Scalability

### 10.1 FHIR Performance Optimization

```
// FHIR-specific performance optimization strategies
type FhirPerformanceOptimization = {
    // Bundle Optimization
    BundleSize: BundleSizeStrategy         // Optimal bundle sizes
    BundleProcessing: BundleProcessingStrategy // Parallel vs sequential
    
    // Resource Optimization  
    ResourceMinimization: ResourceMinimizationStrategy // Include only necessary elements
    ReferenceOptimization: ReferenceStrategy // Contained vs external references
    
    // Search Optimization
    SearchParameterOptimization: SearchStrategy // Efficient FHIR searches
    IncludeRevIncludeOptimization: IncludeStrategy // Optimal _include/_revinclude usage
    
    // Caching Strategy
    ResourceCaching: CachingStrategy       // Cache frequently accessed resources
    BundleCaching: CachingStrategy         // Cache complete bundles
    OperationCaching: CachingStrategy      // Cache operation results
}

// Bundle size optimization for treatment plans
type BundleSizeStrategy = {
    MaxBundleSize: int                     // Maximum entries per bundle
    ResourceTypeGrouping: bool             // Group similar resources  
    DependencyOrdering: bool               // Order resources by dependencies
    ChunkingStrategy: ChunkingMethod       // How to split large bundles
}

type ChunkingMethod =
    | ByResourceCount of int               // Split by number of resources
    | ByResourceType                       // Split by resource type
    | ByDependencyGraph                    // Split maintaining dependencies
    | ByPayloadSize of int                 // Split by payload size in KB

// FHIR search optimization for patient context
let optimizedPatientContextQuery (patientId: string) =
    $"""
    Patient/{patientId}?
    _include=Patient:organization&
    _include=Patient:general-practitioner&
    _revinclude=Observation:patient&
    _revinclude=AllergyIntolerance:patient&
    _revinclude=Condition:patient&
    _revinclude=MedicationRequest:patient&
    _count=50&
    _sort=-_lastUpdated
    """

// Resource minimization through FHIR _elements parameter  
let minimizePatientResource = "_elements=id,identifier,birthDate,gender,extension"
let minimizeObservationResource = "_elements=id,status,code,subject,value,effective"
let minimizeMedicationRequestResource = "_elements=id,status,intent,medication,subject,dosageInstruction"
```

### 10.2 Scalability Architecture

```

// FHIR server scalability configuration
CapabilityStatement: genpres-capability
- status: active
- date: "2026-01-15"
- publisher: "GenPRES"
- kind: instance
- software:
  - name: "GenPRES FHIR Server"
  - version: "2.1.0"
- implementation:
  - description: "GenPRES Treatment Planning FHIR Server"  
  - url: "https://api.genpres.nl/fhir"
- fhirVersion: 4.0.1
- format: [json, xml]
- rest:
  - mode: server
  - security:
    - cors: true
    - service:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/restful-security-service
        - code: OAuth
        - display: "OAuth2 using SMART-on-FHIR profile"
  - resource:
    - type: Patient
      interaction: [read, search-type]
      searchParam:
        - name: identifier
          type: token
        - name: birthdate  
          type: date
    - type: CarePlan
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      conditionalUpdate: true
      searchParam:
        - name: patient
          type: reference
        - name: status
          type: token
        - name: date
          type: date
    - type: MedicationRequest  
      interaction: [create, read, update, delete, search-type]
      versioning: versioned
      conditionalCreate: true
      searchParam:
        - name: patient
          type: reference
        - name: medication
          type: reference  
        - name: status
          type: token
  - operation:
    - name: bootstrap
      definition: http://genpres.nl/fhir/OperationDefinition/bootstrap
    - name: treatment-plan-request
      definition: http://genpres.nl/fhir/OperationDefinition/treatment-plan-request
    - name: finalize
      definition: http://genpres.nl/fhir/OperationDefinition/finalize
    - name: validate-gstandard  
      definition: http://genpres.nl/fhir/OperationDefinition/validate-gstandard
```

### 10.3 Performance Monitoring

```
// FHIR-based performance monitoring
type FhirPerformanceMonitoring = {
    // Operation Performance
    OperationMetrics: OperationMetric[]    // Performance per FHIR operation
    ResourceMetrics: ResourceMetric[]      // Performance per resource type
    BundleMetrics: BundleMetric[]          // Bundle processing performance
    
    // Quality Metrics
    ValidationMetrics: ValidationMetric[]  // G-Standard validation performance
    SafetyMetrics: SafetyMetric[]          // Safety check performance
    
    // System Metrics  
    ServerMetrics: ServerMetric[]          // FHIR server performance
    DatabaseMetrics: DatabaseMetric[]     // Database performance
    CacheMetrics: CacheMetric[]           // Cache performance
}

// Performance metrics as FHIR Observation resources
let createPerformanceObservation (operationType: string) (duration: float) =
    {
        ResourceType = "Observation"
        Status = ObservationStatus.Final
        Category = [
            {
                Coding = [
                    {
                        System = Some "http://genpres.nl/fhir/CodeSystem/observation-category"
                        Code = Some "performance-metric"
                        Display = Some "Performance Metric"
                    }
                ]
            }
        ]
        Code = {
            Coding = [
                {
                    System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                    Code = Some "operation-duration"  
                    Display = Some "Operation Duration"
                }
            ]
        }
        Component = [
            {
                Code = {
                    Coding = [
                        {
                            System = Some "http://genpres.nl/fhir/CodeSystem/performance-metric"
                            Code = Some "operation-type"
                            Display = Some "Operation Type"
                        }
                    ]
                }
                ValueString = Some operationType
            }
        ]
        ValueQuantity = Some {
            Value = Some duration
            Unit = Some "ms"
            System = Some "http://unitsofmeasure.org"
            Code = Some "ms"
        }
        EffectiveDateTime = Some DateTime.UtcNow
        Device = Some {
            Display = Some "GenPRES Performance Monitor"
        }
    }
```

## 11. Security and Privacy

### 11.1 OAuth2/SMART-on-FHIR Security

```

// OAuth2 Authorization Server Metadata (RFC 8414)
OAuth2Metadata: genpres-oauth2
- issuer: "https://auth.genpres.nl"
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"  
- introspection_endpoint: "https://auth.genpres.nl/oauth2/introspect"
- jwks_uri: "https://auth.genpres.nl/.well-known/jwks.json"
- scopes_supported: [
    "patient/*.read",
    "patient/*.write", 
    "user/*.read",
    "user/*.write",
    "system/*.read",
    "system/*.write",
    "launch",
    "launch/patient",
    "fhirUser",
    "openid",
    "profile"
  ]
- response_types_supported: ["code", "token", "id_token"]
- grant_types_supported: ["authorization_code", "client_credentials", "refresh_token"]
- code_challenge_methods_supported: ["S256"]
- token_endpoint_auth_methods_supported: ["client_secret_basic", "client_secret_post", "private_key_jwt"]

// SMART-on-FHIR Well-Known Endpoint
SMARTConfiguration: smart-configuration  
- authorization_endpoint: "https://auth.genpres.nl/oauth2/authorize"
- token_endpoint: "https://auth.genpres.nl/oauth2/token"
- capabilities: [
  "launch-ehr", 
  "launch-standalone",
  "client-public",
  "client-confidential-symmetric", 
  "sso-openid-connect",
  "context-passthrough-banner",
  "context-passthrough-style",
  "context-ehr-patient",
  "context-ehr-encounter",
  "permission-offline",
  "permission-patient",
  "permission-user"
]
```

### 11.2 FHIR Security Labels and Access Control

```

// Security labels for FHIR resources
CodeSystem: security-labels
- url: http://genpres.nl/fhir/CodeSystem/security-label
- concept:
  - code: PEDS
    display: "Pediatric Data"
    definition: "Data specific to pediatric patients requiring special handling"
  - code: GSTAND
    display: "G-Standard Data" 
    definition: "Data validated against G-Standard requiring compliance tracking"
  - code: CALC
    display: "Calculated Data"
    definition: "Data generated by GenPRES calculations"
  - code: SENS
    display: "Sensitive Clinical Data"
    definition: "Highly sensitive clinical information requiring restricted access"

// Security labeling for CarePlan resources
CarePlan: secure-treatment-plan
- meta:
    security:
      - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
        code: GSTAND
        display: "G-Standard Data"
      - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
        code: N
        display: "Normal"

// Consent resource for treatment plan data usage
Consent: treatment-plan-consent
- status: active
- scope:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentscope
    - code: treatment
    - display: "Treatment"
- category:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/consentcategorycodes
    - code: medical
    - display: "Medical Consent"
- patient: Reference(Patient)
- dateTime: "2026-01-15T10:00:00Z"
- performer: [Reference(Patient)]
- policy:
  - authority: "https://genpres.nl/privacy-policy"
  - uri: "https://genpres.nl/consent/treatment-planning"
- provision:
  - type: permit
  - purpose:
    - system: http://terminology.hl7.org/CodeSystem/v3-ActReason
    - code: TREAT
    - display: "Treatment"
  - class:
    - system: http://hl7.org/fhir/resource-types
    - code: CarePlan
  - class:
    - system: http://hl7.org/fhir/resource-types  
    - code: MedicationRequest
  - action:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: collect
      - display: "Collect"
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/consentaction
      - code: use
      - display: "Use"
```

### 11.3 Audit Trail and Compliance

```

// IHE ATNA compliant audit events
AuditEvent: treatment-plan-access
- type:
  - system: http://dicom.nema.org/resources/ontology/DCM
  - code: "110112"
  - display: "Query"
- action: E # Execute/Perform
- recorded: "2026-01-15T10:30:00Z"
- outcome: "0" # Success
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: humanuser
      - display: "Human User"
  - who:
    - reference: "Practitioner/dr-smith"
    - display: "Dr. John Smith"
  - requestor: true
  - location: Reference(Location/icu-room-101)
  - policy: ["http://genpres.nl/policy/treatment-planning-access"]
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/extra-security-role-type
      - code: application
      - display: "Application"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/application-id"
      - value: "genpres-treatment-planner"
    - display: "GenPRES Treatment Planner"
  - requestor: false
- source:
  - observer:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/system-id"  
      - value: "genpres-fhir-server"
    - display: "GenPRES FHIR Server"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/security-source-type
    - code: "4"
    - display: "Application Server"
- entity:
  - what:
    - reference: "Patient/pat-123"
  - type:
    - system: http://terminology.hl7.org/CodeSystem/audit-entity-type
    - code: "1"
    - display: "Person"
  - role:
    - system: http://terminology.hl7.org/CodeSystem/object-role
    - code: "1" 
    - display: "Patient"
  - name: "Treatment plan calculation for pediatric patient"
  - securityLabel:
    - system: http://terminology.hl7.org/CodeSystem/v3-Confidentiality
    - code: N
    - display: "Normal"

// Provenance tracking for calculated treatment plans
Provenance: treatment-plan-calculation
- target:
  - reference: "CarePlan/calculated-plan-001"
- target:
  - reference: "MedicationRequest/calculated-medreq-001"
- recorded: "2026-01-15T10:30:00Z"
- policy: ["http://genpres.nl/policy/calculation-provenance"]
- location: Reference(Location/genpres-datacenter)
- activity:
  - coding:
    - system: http://terminology.hl7.org/CodeSystem/v3-DataOperation
    - code: CREATE
    - display: "Create"
- agent:
  - type:
    - coding:
      - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
      - code: performer
      - display: "Performer"
  - who:
    - identifier:
      - system: "http://genpres.nl/fhir/NamingSystem/calculation-engine"
      - value: "genpres-calc-engine-v2.1"
    - display: "GenPRES Calculation Engine v2.1"
  - onBehalfOf:
    - reference: "Practitioner/dr-smith"
- entity:
  - role: source
  - what:
    - reference: "Bundle/patient-context-bundle"
    - display: "Patient context bundle used for calculation"
  - agent:
    - type:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/provenance-participant-type
        - code: enterer
        - display: "Enterer"  
    - who:
      - reference: "Device/ehr-system"
      - display: "Hospital EHR System"
- signature:
  - type:
    - system: urn:iso-astm:E1762-95:2013
    - code: 1.2.840.10065.1.12.1.1
    - display: "Author's Signature"
  - when: "2026-01-15T10:30:00Z"
  - who:
    - reference: "Device/genpres-system"
  - data: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." # Base64 encoded signature
```

## 12. Implementation Considerations

### 12.1 FHIR Implementation Guide

```

// Implementation Guide definition
ImplementationGuide: genpres-treatment-planning
- url: "http://genpres.nl/fhir/ImplementationGuide/treatment-planning"
- version: "1.2.0" 
- name: "GenPRESTreatmentPlanning"
- title: "GenPRES Treatment Planning Implementation Guide"
- status: active
- publisher: "GenPRES"
- description: "FHIR R4 implementation guide for GenPRES treatment planning integration"
- packageId: "nl.genpres.treatmentplanning"
- license: CC0-1.0
- fhirVersion: [4.0.1]
- dependsOn:
  - uri: "http://hl7.org/fhir"
    version: "4.0.1"
  - uri: "http://hl7.org/fhir/uv/sdc"
    version: "3.0.0"
  - uri: "http://hl7.org/fhir/uv/cpg"  
    version: "2.0.0"
- definition:
  - resource:
    - reference:
      - reference: "StructureDefinition/genpres-patient"
      - name: "GenPRES Patient Profile"
      - description: "Patient profile with Dutch healthcare extensions"
    - reference:
      - reference: "StructureDefinition/genpres-careplan"
      - name: "GenPRES CarePlan Profile" 
      - description: "Treatment plan as FHIR CarePlan"
    - reference:
      - reference: "StructureDefinition/genpres-medicationrequest"
      - name: "GenPRES MedicationRequest Profile"
      - description: "Medication request with G-Standard compliance"
  - page:
    - nameUrl: "index.html"
    - title: "GenPRES Treatment Planning IG"
    - generation: html
    - page:
      - nameUrl: "profiles.html" 
      - title: "FHIR Profiles"
      - generation: html
    - page:
      - nameUrl: "extensions.html"
      - title: "FHIR Extensions"  
      - generation: html
    - page:
      - nameUrl: "terminology.html"
      - title: "Terminology"
      - generation: html
    - page:
      - nameUrl: "examples.html"
      - title: "Examples"
      - generation: html
```

### 12.2 Testing and Validation

```
// FHIR validation and testing framework
type FhirTestingFramework = {
    // Profile Validation
    ProfileValidation: ProfileValidationConfig // FHIR profile validation
    SchemaValidation: SchemaValidationConfig   // FHIR schema validation
    TerminologyValidation: TerminologyValidationConfig // Terminology validation
    
    // Integration Testing
    OperationTesting: OperationTestConfig      // FHIR operation testing
    WorkflowTesting: WorkflowTestConfig        // End-to-end workflow testing
    PerformanceTesting: PerformanceTestConfig  // Performance testing
    
    // Compliance Testing
    G-StandardTesting: GStandardTestConfig     // G-Standard compliance testing
    SecurityTesting: SecurityTestConfig        // OAuth2/SMART security testing
    IHETesting: IHETestConfig                  // IHE profile compliance testing
}

// FHIR TestScript resources for automated testing
TestScript: bootstrap-operation-test
- url: "http://genpres.nl/fhir/TestScript/bootstrap-operation-test"
- version: "1.0.0"
- name: "BootstrapOperationTest"
- status: active
- title: "Test bootstrap operation with patient context"
- description: "Validates the $bootstrap operation with complete patient context"
- origin:
  - index: 1
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-origin-types"
    - code: "FHIR-Client"
- destination:
  - index: 1  
  - profile:
    - system: "http://terminology.hl7.org/CodeSystem/testscript-profile-destination-types"
    - code: "FHIR-Server"
- fixture:
  - id: "patient-context-bundle"
  - autocreate: false
  - autodelete: false
  - resource:
    - reference: "Bundle/patient-context-example"
- variable:
  - name: "patientId"
  - defaultValue: "pat-123"
  - sourceId: "patient-context-bundle"
  - expression: "Bundle.entry.where(resource is Patient).resource.id"
- test:
  - id: "bootstrap-success"
  - name: "Bootstrap Operation Success"
  - description: "Test successful bootstrap operation"
  - action:
    - operation:
      - type:
        - system: "http://terminology.hl7.org/CodeSystem/testscript-operation-codes"
        - code: "create"
      - resource: "Bundle"
      - description: "Execute bootstrap operation"  
      - destination: 1
      - origin: 1
      - params: "/$bootstrap"
      - sourceId: "patient-context-bundle"
    - assert:
      - description: "Confirm that the returned HTTP status is 200(OK)"
      - direction: response
      - responseCode: "200"
    - assert:
      - description: "Confirm that the response is an OperationOutcome"
      - direction: response
      - resource: "OperationOutcome"
    - assert:
      - description: "Confirm successful outcome"
      - direction: response
      - expression: "OperationOutcome.issue.where(severity='information').exists()"

// Example test data for FHIR testing
Bundle: patient-context-test-data
- resourceType: Bundle
- type: collection
- entry:
  - resource:
    - resourceType: Patient
    - id: test-patient-001
    - identifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
      - value: "999999999"
    - birthDate: "2015-03-15"
    - gender: male
    - extension:
      - url: http://genpres.nl/fhir/StructureDefinition/gestational-age
        valueQuantity:
          value: 39
          unit: weeks
          system: http://unitsofmeasure.org
          code: wk
  - resource:
    - resourceType: Observation
    - status: final
    - category:
      - coding:
        - system: http://terminology.hl7.org/CodeSystem/observation-category
        - code: vital-signs
    - code:
      - coding:
        - system: http://loinc.org
        - code: "29463-7"
        - display: "Body weight"
    - subject:
      - reference: "Patient/test-patient-001"
    - valueQuantity:
      - value: 18.5
      - unit: kg
      - system: http://unitsofmeasure.org  
      - code: kg
```

### 12.3 Data Migration and Integration

```
// FHIR data migration strategies
type FhirDataMigration = {
    // Legacy Data Transformation
    LegacyToFhir: TransformationConfig     // Transform legacy data to FHIR
    GStandardMapping: MappingConfig        // Map G-Standard data to FHIR
    
    // Data Validation
    MigrationValidation: ValidationConfig  // Validate migrated data
    QualityAssurance: QualityConfig        // Data quality checks
    
    // Incremental Migration
    IncrementalSync: SyncConfig            // Incremental data synchronization
    ConflictResolution: ConflictConfig     // Handle data conflicts
}

// FHIR ConceptMap for G-Standard to FHIR mapping
ConceptMap: gstandard-to-fhir-medication
- url: "http://genpres.nl/fhir/ConceptMap/gstandard-to-fhir-medication"
- version: "1.0.0"
- name: "GStandardToFHIRMedication" 
- title: "G-Standard to FHIR Medication Mapping"
- status: active
- publisher: "GenPRES"
- description: "Mapping G-Standard medication codes to FHIR Medication resource elements"
- sourceUri: "http://gstandard.nl/fhir/CodeSystem/gpk"
- targetUri: "http://hl7.org/fhir/StructureDefinition/Medication"
- group:
  - source: "http://gstandard.nl/fhir/CodeSystem/gpk"
  - target: "http://snomed.info/sct"
  - element:
    - code: "12345" # GPK code
    - display: "Paracetamol tablet 500mg"
    - target:
      - code: "387517004"
      - display: "Paracetamol"
      - equivalence: equivalent
  - element:
    - code: "67890" # GPK code  
    - display: "Amoxicillin capsule 250mg"
    - target:
      - code: "27658006"
      - display: "Amoxicillin"
      - equivalence: equivalent

// FHIR StructureMap for data transformation
StructureMap: legacy-patient-to-fhir
- url: "http://genpres.nl/fhir/StructureMap/legacy-patient-to-fhir"
- version: "1.0.0"
- name: "LegacyPatientToFHIR"
- status: active
- title: "Transform legacy patient data to FHIR Patient"
- description: "Transforms legacy EHR patient data to FHIR Patient resource"
- structure:
  - url: "http://genpres.nl/fhir/StructureDefinition/legacy-patient"
  - mode: source
  - alias: "LegacyPatient"
- structure:
  - url: "http://hl7.org/fhir/StructureDefinition/Patient"  
  - mode: target
  - alias: "FHIRPatient"
- group:
  - name: "patient"
  - typeMode: none
  - input:
    - name: "source"
    - type: "LegacyPatient"
    - mode: source
  - input:
    - name: "target"
    - type: "FHIRPatient" 
    - mode: target
  - rule:
    - name: "patient-id"
    - source:
      - context: "source"
      - element: "patientId"
      - variable: "pid"
    - target:
      - context: "target"
      - contextType: variable
      - element: "id"
      - transform: copy
      - parameter:
        - valueId: "pid"
  - rule:
    - name: "birth-date"
    - source:
      - context: "source"
      - element: "dateOfBirth" 
      - variable: "dob"
    - target:
      - context: "target"
      - contextType: variable
      - element: "birthDate"
      - transform: copy
      - parameter:
        - valueId: "dob"
```

## 13. Appendices

### Appendix A: FHIR Profiles and Extensions

```

// Complete FHIR profile definitions
StructureDefinition: GenPRESPatient
- url: http://genpres.nl/fhir/StructureDefinition/genpres-patient
- version: 1.2.0
- name: GenPRESPatient  
- title: GenPRES Patient Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: Patient
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Patient
- derivation: constraint
- differential:
  - element:
    - id: Patient.identifier
    - path: Patient.identifier
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: Patient.identifier:BSN
    - path: Patient.identifier
    - sliceName: BSN
    - min: 0
    - max: 1
    - type:
      - code: Identifier
    - patternIdentifier:
      - system: http://fhir.nl/fhir/NamingSystem/bsn
  - element:  
    - id: Patient.birthDate
    - path: Patient.birthDate
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: Patient.extension:gestationalAge
    - path: Patient.extension
    - sliceName: gestationalAge
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gestational-age]

StructureDefinition: GenPRESCarePlan
- url: http://genpres.nl/fhir/StructureDefinition/genpres-careplan
- version: 1.2.0
- name: GenPRESCarePlan
- title: GenPRES CarePlan Profile  
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: CarePlan
- baseDefinition: http://hl7.org/fhir/StructureDefinition/CarePlan
- derivation: constraint
- differential:
  - element:
    - id: CarePlan.status
    - path: CarePlan.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: CarePlan.intent
    - path: CarePlan.intent  
    - min: 1
    - max: 1
    - fixedCode: plan
    - mustSupport: true
  - element:
    - id: CarePlan.subject
    - path: CarePlan.subject
    - min: 1  
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: CarePlan.extension:gstandardCompliance
    - path: CarePlan.extension
    - sliceName: gstandardCompliance
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-compliance]

StructureDefinition: GenPRESMedicationRequest
- url: http://genpres.nl/fhir/StructureDefinition/genpres-medicationrequest
- version: 1.2.0
- name: GenPRESMedicationRequest
- title: GenPRES MedicationRequest Profile
- status: active
- fhirVersion: 4.0.1
- kind: resource
- abstract: false
- type: MedicationRequest
- baseDefinition: http://hl7.org/fhir/StructureDefinition/MedicationRequest
- derivation: constraint
- differential:
  - element:
    - id: MedicationRequest.status
    - path: MedicationRequest.status
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.intent
    - path: MedicationRequest.intent
    - min: 1
    - max: 1
    - mustSupport: true
  - element:
    - id: MedicationRequest.medication[x]
    - path: MedicationRequest.medication[x]
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-medication]
    - mustSupport: true
  - element:
    - id: MedicationRequest.subject  
    - path: MedicationRequest.subject
    - min: 1
    - max: 1
    - type:
      - code: Reference
      - targetProfile: [http://genpres.nl/fhir/StructureDefinition/genpres-patient]
    - mustSupport: true
  - element:
    - id: MedicationRequest.dosageInstruction
    - path: MedicationRequest.dosageInstruction
    - min: 1
    - max: "*"
    - mustSupport: true
  - element:
    - id: MedicationRequest.extension:weightBasedDose
    - path: MedicationRequest.extension
    - sliceName: weightBasedDose
    - min: 0
    - max: 1  
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/weight-based-dose]
  - element:
    - id: MedicationRequest.extension:gstandardDoseRule
    - path: MedicationRequest.extension
    - sliceName: gstandardDoseRule
    - min: 0
    - max: 1
    - type:
      - code: Extension
      - profile: [http://genpres.nl/fhir/StructureDefinition/gstandard-dose-rule]
```

### Appendix B: G-Standard Integration Specifications

```

// G-Standard specific extensions
StructureDefinition: GStandardCompliance
- url: http://genpres.nl/fhir/StructureDefinition/gstandard-compliance
- version: 1.2.0
- name: GStandardCompliance
- title: G-Standard Compliance Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: CarePlan
  - type: element
  - expression: MedicationRequest
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: G-Standard compliance information
    - definition: Detailed information about G-Standard compliance validation
  - element:
    - id: Extension.extension:level
    - path: Extension.extension
    - sliceName: level
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:level.url
    - path: Extension.extension.url
    - fixedUri: level
  - element:
    - id: Extension.extension:level.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/compliance-level
  - element:
    - id: Extension.extension:validationTimestamp
    - path: Extension.extension
    - sliceName: validationTimestamp
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:validationTimestamp.url
    - path: Extension.extension.url
    - fixedUri: validation-timestamp
  - element:
    - id: Extension.extension:validationTimestamp.value[x]
    - path: Extension.extension.value[x]  
    - type:
      - code: dateTime
  - element:
    - id: Extension.extension:gstandardVersion
    - path: Extension.extension
    - sliceName: gstandardVersion
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:gstandardVersion.url
    - path: Extension.extension.url
    - fixedUri: gstandard-version
  - element:
    - id: Extension.extension:gstandardVersion.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: string

StructureDefinition: WeightBasedDose
- url: http://genpres.nl/fhir/StructureDefinition/weight-based-dose
- version: 1.2.0
- name: WeightBasedDose
- title: Weight-Based Dose Extension
- status: active
- fhirVersion: 4.0.1
- kind: complex-type
- abstract: false
- context:
  - type: element
  - expression: MedicationRequest
  - type: element  
  - expression: Dosage
- type: Extension
- baseDefinition: http://hl7.org/fhir/StructureDefinition/Extension
- derivation: constraint
- differential:
  - element:
    - id: Extension
    - path: Extension
    - short: Weight-based dosing information
    - definition: Information about weight-based dose calculation
  - element:
    - id: Extension.extension:dosePerKg
    - path: Extension.extension
    - sliceName: dosePerKg
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:dosePerKg.url
    - path: Extension.extension.url
    - fixedUri: dose-per-kg
  - element:
    - id: Extension.extension:dosePerKg.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:patientWeight
    - path: Extension.extension
    - sliceName: patientWeight
    - min: 1
    - max: 1
  - element:
    - id: Extension.extension:patientWeight.url
    - path: Extension.extension.url
    - fixedUri: patient-weight
  - element:
    - id: Extension.extension:patientWeight.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: Quantity
  - element:
    - id: Extension.extension:weightSource
    - path: Extension.extension
    - sliceName: weightSource
    - min: 0
    - max: 1
  - element:
    - id: Extension.extension:weightSource.url
    - path: Extension.extension.url
    - fixedUri: weight-source  
  - element:
    - id: Extension.extension:weightSource.value[x]
    - path: Extension.extension.value[x]
    - type:
      - code: code
    - binding:
      - strength: required
      - valueSet: http://genpres.nl/fhir/ValueSet/weight-source
```

### Appendix C: API Documentation

```
## Complete OpenAPI 3.0 specification
openapi: 3.0.3
info:
  title: GenPRES FHIR Treatment Planning API
  description: |
    FHIR R4 compliant API for GenPRES treatment planning integration.
    Supports G-Standard compliance, OAuth2/SMART-on-FHIR security,
    and IHE Pharmacy profile compliance.
  version: 1.2.0
  contact:
    name: GenPRES API Support
    url: https://support.genpres.nl
    email: api-support@genpres.nl
  license:
    name: Proprietary
    url: https://genpres.nl/license

servers:
  - url: https://api.genpres.nl/fhir
    description: Production FHIR server
  - url: https://staging-api.genpres.nl/fhir  
    description: Staging FHIR server
  - url: https://dev-api.genpres.nl/fhir
    description: Development FHIR server

security:
  - OAuth2: [patient/*.read, patient/*.write, user/*.read]
  - BearerAuth: []

paths:
  /$bootstrap:
    post:
      tags: [Treatment Planning]
      summary: Initialize patient context for treatment planning
      description: |
        Bootstrap operation that establishes patient context for treatment planning.
        Accepts a Bundle containing Patient and related clinical resources.
      operationId: bootstrap
      security:
        - OAuth2: [patient/*.read, launch, launch/patient]
      requestBody:
        required: true
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/Bundle'
            examples:
              patient-context:
                summary: Patient context with vital signs and allergies
                value:
                  resourceType: Bundle
                  type: collection
                  entry:
                    - resource:
                        resourceType: Patient
                        id: pat-123
                        birthDate: "2010-05-15"
                        gender: male
      responses:
        '200':
          description: Bootstrap successful
          content:
            application/fhir+json:: TREAT
        display: "Treatment"
      - system: http://genpres.nl/fhir/CodeSystem/security-label  
        code