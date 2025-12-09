# GenPRES TimeLine and Architecture 2026

- [GenPRES TimeLine and Architecture 2026](#genpres-timeline-and-architecture-2026)
  - [Classification of deliverables](#classification-of-deliverables)
  - [Enabling Technologies](#enabling-technologies)
    - [- OTS / Data Platform](#--ots--data-platform)
      - [**Q1.** ZIndex-OTS / ZForm-OTS](#q1-zindex-ots--zform-ots)
        - [*- Extraction of all Z-Index registry*](#--extraction-of-all-z-index-registry)
        - [*- OTS as storage and version control*](#--ots-as-storage-and-version-control)
        - [**Deliverables**](#deliverables)
      - [**Q2.** GenFORM-OTS](#q2-genform-ots)
        - [*- Complete National Dutch Pediatric Formulary in GenFORM*](#--complete-national-dutch-pediatric-formulary-in-genform)
        - [*- All Local Hospital Medication Dose Rules in GenFORM*](#--all-local-hospital-medication-dose-rules-in-genform)
        - [*- All Local Hospital Parenteralia/Oralia Rules in GenFORM*](#--all-local-hospital-parenteraliaoralia-rules-in-genform)
        - [*- Part of National Farmacotherapeutic Formulary in GenFORM*](#--part-of-national-farmacotherapeutic-formulary-in-genform)
        - [*- Part of National Antibiotic Guidelines in GenFORM*](#--part-of-national-antibiotic-guidelines-in-genform)
        - [*- Part of Renal Adjustment Guidelines in GenFORM*](#--part-of-renal-adjustment-guidelines-in-genform)
        - [*- OTS as storage and version control*](#--ots-as-storage-and-version-control-1)
        - [*- Import and Export in spreadsheet format*](#--import-and-export-in-spreadsheet-format)
        - [*- Formal Validation Pipeline*](#--formal-validation-pipeline)
        - [**Deliverables**](#deliverables-1)
      - [**Q3.** GenORDER-DataPlatform](#q3-genorder-dataplatform)
        - [*- Data Platform*](#--data-platform)
        - [**Deliverables**](#deliverables-2)
    - [- Cloud Program](#--cloud-program)
      - [**Q1.** GenSERVER-Cloud](#q1-genserver-cloud)
        - [*- Provides the API for the UI and Interface*](#--provides-the-api-for-the-ui-and-interface)
        - [**Deliverables**](#deliverables-3)
      - [**Q2.** GenFORM-Cloud](#q2-genform-cloud)
        - [*- Runs in the Cloud as a Docker Agent*](#--runs-in-the-cloud-as-a-docker-agent)
        - [**Deliverables**](#deliverables-4)
      - [**Q3.** GenORDER-Cloud](#q3-genorder-cloud)
        - [*- Runs in the Cloud as a Docker Agent*](#--runs-in-the-cloud-as-a-docker-agent-1)
        - [**Deliverables**](#deliverables-5)
      - [**Q4.** MCP-Cloud](#q4-mcp-cloud)
        - [**MCP-Cloud**](#mcp-cloud)
        - [*- Runs in the Cloud as an MCP Agent on Docker*](#--runs-in-the-cloud-as-an-mcp-agent-on-docker)
        - [**Deliverables**](#deliverables-6)
  - [Product](#product)
    - [- Engine](#--engine)
      - [**Q1.** GenSOLVER-Medication](#q1-gensolver-medication)
        - [*- Performs all Calculations*](#--performs-all-calculations)
        - [*- Can handle Values with Unit*](#--can-handle-values-with-unit)
        - [*- Logs all Calculations*](#--logs-all-calculations)
        - [**Deliverables**](#deliverables-7)
      - [**Q2.** GenSOLVER-Nutrition](#q2-gensolver-nutrition)
        - [**Deliverables**](#deliverables-8)
      - [**Q3.** GenSOLVER-Treatment](#q3-gensolver-treatment)
        - [**Deliverables**](#deliverables-9)
    - [- UI Desktop / Mobile](#--ui-desktop--mobile)
    - [Desktop](#desktop)
      - [**Q1**. Emergency Medication](#q1-emergency-medication)
        - [*- Emergency List*](#--emergency-list)
        - [*- Continuous Medication*](#--continuous-medication)
        - [**Deliverables**](#deliverables-10)
      - [**Q2**. Operational Rule Sets / Medication Prescription](#q2-operational-rule-sets--medication-prescription)
        - [**Operational Rule Sets**](#operational-rule-sets)
        - [*- Formularies*](#--formularies)
        - [*- Parenteralia*](#--parenteralia)
        - [**Medication Prescription**](#medication-prescription)
        - [**Deliverables**](#deliverables-11)
      - [**Q3**. Nutrition Prescription / Treatment Prescription / Configuration](#q3-nutrition-prescription--treatment-prescription--configuration)
        - [**Nutrition Prescription**](#nutrition-prescription)
        - [**Treatment Prescription**](#treatment-prescription)
        - [**Configuration**](#configuration)
        - [**Deliverables**](#deliverables-12)
    - [Mobile](#mobile)
      - [**Q1**. Emergency Medication](#q1-emergency-medication-1)
        - [*- Emergency List*](#--emergency-list-1)
        - [*- Continuous Medication*](#--continuous-medication-1)
        - [**Deliverables**](#deliverables-13)
      - [**Q2**. Medication Prescription](#q2-medication-prescription)
        - [**Deliverables**](#deliverables-14)
      - [**Q3**. Treatment View](#q3-treatment-view)
        - [**Deliverables**](#deliverables-15)
    - [- Integration](#--integration)
      - [**Q4.** HIX Connect](#q4-hix-connect)
        - [*- Provides Authentication and Authorization*](#--provides-authentication-and-authorization)
        - [*- Gets Eligible Patients*](#--gets-eligible-patients)
        - [*- Get Complete Order State*](#--get-complete-order-state)
        - [*- Returns Complete Order Set*](#--returns-complete-order-set)
  - [Adoption](#adoption)
    - [- PICU/NICU](#--picunicu)
      - [**Q1**. Emergency Medication](#q1-emergency-medication-2)
      - [**Q2**. Medication Validation](#q2-medication-validation)
      - [**Q3**. Treatment Validation](#q3-treatment-validation)
      - [**Q4**. Treatment Prescription](#q4-treatment-prescription)
    - [- Outpatient Clinic](#--outpatient-clinic)
      - [**Q3**. Treatment Validation](#q3-treatment-validation-1)
      - [**Q4**. Treatment Prescription](#q4-treatment-prescription-1)
  - [Key Milestones](#key-milestones)
  - [**Addendum 1**. GenPRES Shopping Cart](#addendum-1-genpres-shopping-cart)
  - [Addendum 2. Architecture](#addendum-2-architecture)
  - [Addendum 3. GenPRES Libraries](#addendum-3-genpres-libraries)
  - [**Addendum 4.** Tech Stack](#addendum-4-tech-stack)
    - [Core Technologies](#core-technologies)
      - [Development Platform](#development-platform)
      - [Frontend Technologies](#frontend-technologies)
      - [Backend Technologies](#backend-technologies)
    - [Testing \& Quality Assurance](#testing--quality-assurance)
      - [Testing Frameworks](#testing-frameworks)
      - [Quality Tools](#quality-tools)
    - [Infrastructure \& Deployment](#infrastructure--deployment)
      - [Containerization](#containerization)
      - [Cloud \& Hosting](#cloud--hosting)
    - [Data \& Integration](#data--integration)
      - [Data Storage](#data-storage)
      - [Integration Standards](#integration-standards)
      - [Medication Databases](#medication-databases)
    - [Advanced Features](#advanced-features)
      - [Artificial Intelligence](#artificial-intelligence)
      - [Agent Architecture](#agent-architecture)
    - [Development Tools](#development-tools)
      - [Version Control \& CI/CD](#version-control--cicd)
      - [Development Environment](#development-environment)
      - [Code Quality](#code-quality)
    - [Security \& Compliance](#security--compliance)
      - [Medical Device Compliance](#medical-device-compliance)
    - [Mathematical \& Domain Libraries](#mathematical--domain-libraries)
      - [Core Domain Libraries](#core-domain-libraries)
      - [Supporting Libraries](#supporting-libraries)
    - [Browser Compatibility](#browser-compatibility)
      - [Supported Browsers](#supported-browsers)
  - [Addendum 5. TimeLine 2026](#addendum-5-timeline-2026)

## Classification of deliverables

- **GUARANTEED:     prototype,    tested in production**  
- **HIGHLY LIKELY:  prototype,    not in production**  
- **NOT GUARANTEED: no prototype, established technology**  
- **NOT SURE:       no prototype, innovation required**

## Enabling Technologies

### - OTS / Data Platform

#### **Q1.** ZIndex-OTS / ZForm-OTS

##### *- Extraction of all Z-Index registry*

- Medication products,
- dose monitoring rules,
- interactions rules,
- duplicate medication rules

all are provided by the Z-Index registry. These come in the form of flat text files that have to be parsed.

##### *- OTS as storage and version control*

The resulting product and rule sets have to be versioned and stored in the OTS (Ontology Terminology SERVER) Knowledge Platform. This tool can also be used to export and import sets of rules as spreadsheets (enabling efficient bulk updates).

##### **Deliverables**

- **Informedica.ZIndex.Lib:** Performs the primary parsing steps.  
- **Informedica.ZForm.Lib**: Processes dose and interaction rules.  
- **Informedica.OTS.Lib**: Uses OTS as a repository to store products and rules from the Z-Index registry.

#### **Q2.** GenFORM-OTS

GenFORM combines the medication products, drug monitoring rules and interaction rules from the Z-Index registry with specific dose rules from various national and local registries. These dose rules are mostly semi-structured and in free text.

GenFORM can handle the following rules:

- Dosing rules  
- Reconstitution rules  
- Dilution rules  
- Renal adjustment rules  
- Interaction rules  
- Duplicate medication rules

Extraction can be performed:

1. Manually  
2. Parsing semi-structured dose rules  
3. Using LLMs to extract the rules

##### *- Complete National Dutch Pediatric Formulary in GenFORM*

The complete set of dose rules of the National Dutch Pediatric Formulary (Kinderformularium). This is a semistructured set of rules that can be parsed with additional help from a NLP parser for the free text parts.

##### *- All Local Hospital Medication Dose Rules in GenFORM*

Dose rules as unstructured free text. Have to be manually extracted or parsed using a LLM based NLP parser.

##### *- All Local Hospital Parenteralia/Oralia Rules in GenFORM*

Reconstitution and dilution rules. Unstructured free text. Have to be manually extracted or parsed using a LLM based NLP parser.

##### *- Part of National Farmacotherapeutic Formulary in GenFORM*

All the dose rules that have a counterpart in the National Dutch Formulary (Farmacotherapeutisch Kompas), i.e. the adult versions of those rules. Have to be manually extracted or parsed using a LLM based NLP parser.

##### *- Part of National Antibiotic Guidelines in GenFORM*

All the dose rules in the National Antibiotic Guidelines (SWAB guidelines) which have a counterpart in the National Dutch Pediatric Formulary. Have to be manually extracted or parsed using a LLM based NLP parser.

##### *- Part of Renal Adjustment Guidelines in GenFORM*

All the renal adjustment rules in the Renal Adjustment Guidelines which have a counterpart in the National Dutch Pediatric Formulary. Have to be manually extracted or parsed using a LLM based NLP parser.

##### *- OTS as storage and version control*

Use OTS for persistence and version control. Rules sets can be incrementally added, increasing the version for each addition.

##### *- Import and Export in spreadsheet format*

All knowledge rules will have the option to be exported in spreadsheet format. These spreadsheets can be updated and imported again creating a bulk update.

##### *- Formal Validation Pipeline*

GenFORM will provide a validation pipeline that will distinguish between valid and invalid rules. Only valid rules will be available for further usage. Invalid rules can be printed out for correction.

##### **Deliverables**

- **Informedica.NKF.Lib:** Parses the National Dutch Pediatric Formulary (KinderFormularium).  
- **Informedica.NLP.Lib**: A library that uses Natural Language Processing to convert free text to (validated) structured output  
- **Informedica.GenFORM.Lib**: Processes all product and rules resources providing a formal validation pipeline and bulk export and insert.  
- **Informedica.OTS.Lib**: Extend this library to be able to store the GenFORM rules.

**NOTE**: The NLP library is not an absolute requirement for GenFORM. The implication is manual extraction.

#### **Q3.** GenORDER-DataPlatform

GenORDER creates all the specific medication and nutrition orders for a patient using the rules from GenFORM.

##### *- Data Platform*

Can read and write all generated orders to the Data Platform.

##### **Deliverables**

- **Informedica.GenORDER.Lib**: Creates all the orders.  
- **Informedica.FHIR.Lib**: Transforms the GenORDER orders to a FHIR compliant format.  
- **Informedica.DataPlatform.Lib:** Store and Retrieve all medication and nutrition orders from GenORDER.

### - Cloud Program

#### **Q1.** GenSERVER-Cloud

The GenSERVER runs as a docker container and is thus easy to scale up according to demand. This also enables an easy workflow as a guaranteed working environment is provided.

This also provides zero downtime and immediate fallback in case of bugs or problems.

##### *- Provides the API for the UI and Interface*

The GenSERVER running as a Docker container will provide the API and the UI to the hospital EHR system.

##### **Deliverables**

- **Informedica.GenPRES.Server:** Runs the whole GenPRES application as a Docker Container in the Cloud.

#### **Q2.** GenFORM-Cloud

##### *- Runs in the Cloud as a Docker Agent*

GenFORM will run as a Docker agent providing a service and stand-alone functionality.

##### **Deliverables**

- **Informedica.GenFORM.Lib:** Is extended with the capability to run as an independent Docker service agent.

#### **Q3.** GenORDER-Cloud

##### *- Runs in the Cloud as a Docker Agent*

GenORDER will run as a Docker agent providing a service and stand-alone functionality.

##### **Deliverables**

- **Informedica.GenORDER.Lib:** Is extended with the capability to run as an independent Docker service agent.

#### **Q4.** MCP-Cloud

##### **MCP-Cloud**

##### *- Runs in the Cloud as an MCP Agent on Docker*

A Model Context Protocol service will be provided to facilitate direct interaction and as an integration point facilitating LLM and AI based solutions.

##### **Deliverables**

- **Informedica.MCP.Lib:** Runs as an MCP GenSERVER for both GenFORM and GenORDER.

## Product

### - Engine

#### **Q1.** GenSOLVER-Medication

GenSOLVER can solve a set of product and sum equations where each variable is either a specific set of numbers or a range of possible numbers. This allows bidirectional calculation of all possible options for each variable.

##### *- Performs all Calculations*

Can calculate both product and sum equations. Solves the problem of combinatory explosion of possible solutions by using min, max and incremental values. The individual values will be exact, meaning no rounding is involved excluding rounding errors or imprecision.

##### *- Can handle Values with Unit*

Calculations have to include units where specific unit expressions can be handled like:

- **1 x/48 hour gentamicin 7 mg = 4.67 mg/kg/48 hour**

Furthermore, values are exact, meaning that 1/3 x 3 = 1 and

- **1/3_000_000_000_000_000 x 3_000_000_000_000_000 = also 1.**

##### *- Logs all Calculations*

All calculations are logged and can be reviewed in human-readable output.

##### **Deliverables**

- **Informedica.GenUNITS.Lib**: Enables calculation with values that have units and provide exact values.  
- **Informedica.GenSOLVER.Lib**: The core calculation engine that can evaluate sets of equations.

#### **Q2.** GenSOLVER-Nutrition

GenSOLVER is optimized to handle more complex order calculation scenarios like nutrition which can have multiple components where an optimal ratio has to be calculated as well.

##### **Deliverables**

- **Informedica.GenSOLVER.Lib:** Can also handle more complex scenarios like nutrition with multiple components.

#### **Q3.** GenSOLVER-Treatment

GenSOLVER is further optimized to handle a complete set of orders. This is mostly a performance requirement.

##### **Deliverables**

- **Informedica.GenSOLVER.Lib:** Can also handle a complete set of orders.

### - UI Desktop / Mobile

### Desktop

A full web based client that uses material UI to provide the optimal user experience. The aim is to provide a UI that requires little to no user instructions. The user should immediately recognize the user specific workflows where the UI follows the user instead of the user having to follow the UI.

#### **Q1**. Emergency Medication

##### *- Emergency List*

Can show and select all acute patient specific ad hoc medication and interventions. These orders (when medication) can be selected and added to the treatment plan as regular medication orders.

##### *- Continuous Medication*

Can show all patient specific continuous medication orders. These orders can be selected and added to the treatment plan as regular medication orders.

##### **Deliverables**

- **Informedica.GenPRES.Client: Desktop** - Has an Emergency List view and Continuous Medication view with the ability to filter and select specific orders that can be immediately added to the treatment plan.

#### **Q2**. Operational Rule Sets / Medication Prescription

##### **Operational Rule Sets**

##### *- Formularies*

Give a full view on all patient specific dose rules sets with the applicable medication products from the Z-Index registry.

Shows a dose monitoring rule provided by the Z-Index registry that checks the GenFORM dose rule.

##### *- Parenteralia*

Give a view on all patient specific reconstitution- and dilution rule sets based on the local hospital parenteralia.

##### **Medication Prescription**

Allow adding or changing all possible medication orders, which then can be transferred to the Treatment.

##### **Deliverables**

- **Informedica.GenPRES.Client: Desktop** - Has a Formularies and Parenteralia view to show the exact dosing-, reconstitution- and dilution rules along with dose rule check by the Z-Index registry.  
- **Informedica.GenPRES.Client: Desktop** - Can add or adjust all order settings, shows the validity of the order against all rule sets.

#### **Q3**. Nutrition Prescription / Treatment Prescription / Configuration

##### **Nutrition Prescription**

Allow adding or changing enteral and/or parenteral nutrition orders and setting the individual nutrition components.

##### **Treatment Prescription**

Provide a full medication and nutrition order overview along with calculations of totals. Provides direct access to change or delete orders.

##### **Configuration**

Being able to configure the app to specific hospital and/or department settings.

##### **Deliverables**

- **Informedica.GenPRES.Client: Desktop** - Can add or adjust nutrition by composing orders from components and setting those components using a totals view.  
- **Informedica.GenPRES.Client: Desktop** - Can view, remove, add or adjust orders to a Treatment plan and shows the totals of all orders.  
- **Informedica.GenPRES.Client: Desktop** - Shows the configuration view.

### Mobile

A full web based client that uses material UI to provide the optimal user experience responsive and optimized for mobile use as well. The aim is to provide a UI that requires little to no user instructions. The user should immediately recognize the user specific workflows where the UI follows the user instead of the user having to follow the UI.

#### **Q1**. Emergency Medication

##### *- Emergency List*

Can show and select all acute patient specific ad hoc medication and interventions. These orders can be selected and added to the treatment plan.

##### *- Continuous Medication*

Can show all patient specific continuous medication orders. These orders can be selected and added to the treatment plan.

##### **Deliverables**

- **Informedica.GenPRES.Client: Mobile** - Has an Emergency List view and Continuous Medication list view with the ability to filter and select specific orders that can be immediately added to the treatment plan.

#### **Q2**. Medication Prescription

Allow adding or changing all possible medication orders.

##### **Deliverables**

- **Informedica.GenPRES.Client: Mobile** - Can add or adjust all order settings, shows the validity of the order against all rule sets.

#### **Q3**. Treatment View

Provide a full medication and nutrition order overview along with calculations of totals.

##### **Deliverables**

- **Informedica.GenPRES.Client: Mobile** - Can view add or remove orders to a Treatment plan.

### - Integration

#### **Q4.** HIX Connect

State transfer from GenSERVER to an outside party should follow the FHIR requirements.

##### *- Provides Authentication and Authorization*

Can differentiate between unauthenticated unauthorized and authenticated authorized use resulting in different usage patterns.

##### *- Gets Eligible Patients*

With authenticated authorized use can get all eligible patients, i.e. patients that the user is authorized to access.

##### *- Get Complete Order State*

Get the complete set of running orders for that patient.

##### *- Returns Complete Order Set*

Return the updated version of the running order set.

**Deliverables**

- **Informedica.GenPRES.Server**: The main entrypoint of GenPRES, can handle all GenPRES requests.  
- **Informedica.FHIR.Lib**: Transforms the GenORDER orders to a FHIR compliant format.  
- **Informedica.HIXConnect.Lib**: Used by the GenSERVER to pass on requests from and to HIXConnect. Handles authentication/authorization, session identification and transfers state from and to the hospital EHR.

## Adoption

### - PICU/NICU

#### **Q1**. Emergency Medication

GenPRES can be used as an Emergency Medication app. The app works both on the desktop as on mobile devices. There is a direct connection from the patient to the application. The application shows all the emergency interventions along with the standard solutions for continuous medication. From the app a printout can be made for bedside use.

**Deliverable: GUARANTEED**

#### **Q2**. Medication Validation

For every medication order an exact dose advice can be provided according to the patient context following all the rule sets available in GenFORM.

This means that all medication in the

- National Dutch Pediatric Formulary (Kinderformularium) can be prescribed and where applicable
- Local Guidelines for dosing, reconstitution and dilution and
- Adult dose rules from the National Adult Formulary (Farmacotherapeutisch Kompas) and
- Antibiotic dose rules from the (SWAB) guidelines.  
- Additionally renal dose adjustment can be applied according to renal function in the patient context.

So, for every medication order, the dose, reconstitution, dilution and administration can be validated.

**Deliverable: GUARANTEED**

#### **Q3**. Treatment Validation

Every medication and or nutrition order can be generated according to the National Dutch Pediatric Formulary (Kinderformularium) and where applicable to the local and National Adult (Farmacotherapeutisch Kompas) and antibiotics (SWAB). These can be added to a treatment plan. The treatment plan is directly accessible to remove or change orders.

The treatment plan shows a full overview of totals relevant to the patient and PICU/NICU setting.

These orders can thus be validated according to the hospital's established guidelines.

**Deliverable: GUARANTEED**

#### **Q4**. Treatment Prescription

The complete treatment plan can be parsed from and converted to FHIR format.

**Deliverable: HIGHLY LIKELY**

HIX-Connect can provide a full roundtrip with session management, authorization and authentication and can send and accept a full medication treatment plan.

**Deliverable: NOT GUARANTEED**

### - Outpatient Clinic

#### **Q3**. Treatment Validation

Every medication and or nutrition order can be generated according to the National Dutch Pediatric Formulary (Kinderformularium) and where applicable to the local and National Adult (Farmacotherapeutisch Kompas) and antibiotics (SWAB). These can be added to a treatment plan. The treatment plan is directly accessible to remove or change orders.

The treatment plan shows a full overview of totals relevant to the patient and outpatient clinic setting.

These orders can thus be validated according to the hospital's established guidelines.

**Deliverable: GUARANTEED**

#### **Q4**. Treatment Prescription

The complete treatment plan can be parsed from and converted to FHIR format.

**Deliverable: HIGHLY LIKELY**

HIX-Connect can provide a full roundtrip with session management, authorization and authentication and can send and accept a full medication treatment plan for patients attending the outpatient clinic.

**Deliverable: NOT GUARANTEED**

## Key Milestones

- **End Q1 2026:** OTS Registry operational, GenSERVER deployed, Emergency App operational (Desktop/Mobile)  
- **End Q2 2026**: All Rule Registries available, Medication Prescription, Medication Validation available  
- **End Q3 2026:** Full UI suite completed, clinical validation ongoing  
- **End Q4 2026:** PICU/NICU and outpatient clinic full prescription production deployment  
- **2027:** WKZ & Prinses Máxima Centrum expansion  
- **2028:** Full UMCU deployment

## **Addendum 1**. GenPRES Shopping Cart

![image1](https://docs.google.com/drawings/d/e/2PACX-1vRMQL0Fu4JARtrFgzyZ3fa9ZXUL4ITQvD9miASN_P4g9x1W0vCrh3MX18YqGwSbHaio-i4HjAy6MttN/pub?w=961&h=584)

**GenPRES** will handle all medication and nutrition order management acting as a shopping cart. This means that GenPRES will be able to:

1. Interpret and structure Expert Knowledge (all guidelines/rules applicable to medication and nutrition).  
2. **GenFORM**: Parse and structure Expert Knowledge to Operational Knowledge Rules.  
3. **GenPROD**: Creating validated medication/nutrition Orders using GenSOLVER such that  
4. *Orders* are Validated (dose, reconstitution, dilution, interaction, duplication, renal adjustment)  
5. Can be Planned  
6. *Preparation* is fully specified  
7. *Administration* is fully specified.

GenPRES as a shopping cart will be able to show the totals, interactions and duplications so the full medication order set can be evaluated.

The shopping cart mechanism means that GenPRES can handle:

1. An initial state of the shopping cart where the shopping cart is filled with existing orders  
2. Changes to the set of orders in the shopping cart, performing all calculation and validation.  
3. Return the changed set of orders in the shopping cart along with the state of the orders (added, removed or changed).

## Addendum 2. Architecture

![image2](https://docs.google.com/drawings/d/e/2PACX-1vSaYqHT1k3G9Dg53o8gUwVB6eSgfTrsy___FXvTuVNhR3opF3dOO-wwtbv31LD3PBoSUnxEnh-Xnhnw/pub?w=1369&h=759)

## Addendum 3. GenPRES Libraries

Classification:

- **Utility Library**  
- **Domain Library**  
- **Application**

Libraries:  
List of libraries and applications along with capabilities and dependencies. Note that only top level dependencies are shown. Transitive dependencies are not listed.

1. **Informedica.Utils.Lib:** Utility library.  
   - Capabilities:  
     - Basic common functionality (string manipulation, collection extensions, file I/O)  
     - Shared primitives and helper functions  

2. **Informedica.Agents.Lib:** Library using the FSharp MailboxProcessor to create message based agents.
   - Capabilities:
     - Message-based agent abstraction using F# MailboxProcessor  
     - Asynchronous, concurrent API execution model  
     - Specific low level agent implementations  
   - Depends on:  
     - **Informedica.Utils.Lib**  

3. **Informedica.Logging.Lib:** Library with advanced printing capabilities to enable human readable logging for analysis.
   - Capabilities:  
     - Human-readable logging and structured output for analysis  
     - Advanced printing and formatting capabilities  
     - Agent-based logging service  
   - Depends on:  
     - **Informedica.Agents.Lib**  

4. **Informedica.NLP.Lib:** Library using NLP to extract structured rules from free text.
   - Capabilities:  
     - Natural language processing for rule extraction  
     - Conversion of free text to structured typed records  
   - Depends on:  
     - **Informedica.Utils.Lib**

5. **Informedica.OTS.Lib:** Library to retrieve and send rules sets to the OTS server for storage and version control. Can also export and import rules sets as spreadsheets.  
   - Capabilities:  
     - Integration with OTS server for product and rule set storage and version control  
     - Rule set import/export to spreadsheet formats  
     - Can log import and export  
     - Can run as an agent based microservice  
   - Depends on:  
     - **Informedica.Logging.Lib**

6. **Informedica.GenUNITS.Lib:** Domain library handling complex value unit combinations and calculation.  
   - Capabilities:  
     - Complex value-unit combinations and dimensional analysis  
     - Unit conversion and validation  
     - Arithmetic operations on values with units  
   - Depends on:  
     - **Informedica.Utils.Lib**

7. **Informedica.GenSOLVER.Lib:** Domain library for the calculation engine. Can solve product and sum equations using value unit combinations as ranges.
   - Capabilities:  
     - Constraint-solving calculation engine  
     - Product and sum equation solving with value-unit ranges  
     - Variable propagation and constraint satisfaction  
     - Can log all messages and calculation steps  
     - Can run as an agent microservice  
   - Depends on:  
     - **Informedica.Logging.Lib**  
     - **Informedica.GenUNITS.Lib**

8. **Informedica.GenCORE.Lib:** Domain library handling domain common domain concepts like Patient.
   - Capabilities:  
     - Core domain concepts (Patient, measurements, calculations)  
     - Shared domain primitives and value objects  
     - Clinical calculation utilities  
   - Depends on:  
     - **Informedica.GenSOLVER.Lib**

9. **Informedica.ZIndex.Lib:** Performs first parsing of products and rules from the ZIndex registry. Products can be used by GenFORM.
   - Capabilities:  
     - Parsing of pharmaceutical products from ZIndex registry  
     - Initial rule extraction from ZIndex data  
     - Product catalog and formulary management  
   - Depends on:  
     - **Informedica.GenCORE.Lib**

10. **Informedica.ZForm.Lib:** Additional processing of rules to more generic constructs. These rules can be used by GenFORM to check specific dose rules.  
    - Capabilities:  
      - Advanced rule processing and normalization  
      - Conversion of ZIndex rules to generic constructs  
      - Can store and retrieve products and rules in OTS  
      - Can log extraction messages  
      - Can run as an agent microservice  
      - Can be exposed via MCP host  
    - Depends on:  
      - **Informedica.OTS.Lib**  
      - **Informedica.ZIndex.Lib**

11. **Informedica.NKF.Lib:** Library used to parse structured content from the Kinderformularium containing pediatric dosing rules.
    - Capabilities:  
      - Parsing of pediatric dosing guidelines from Kinderformularium  
      - Structured pediatric dosing rules  
      - Can log extraction messages  
      - Can run as an agent based microservice  
    - Depends on:  
      - **Informedica.ZForm.Lib**  

12. **Informedica.FTK.Lib:** Library used to parse structured content from the Farmacotherapeutisch Kompas containing pediatric dosing rules.
    - Capabilities:  
      - Parsing of adult dosing guidelines from Farmacotherapeutisch Kompas  
      - Structured adult dosing rules  
      - Can log extraction messages  
      - Can run as an agent based microservice  
    - Depends on:  
      - **Informedica.ZForm.Lib**

13. **Informedica.GenFORM.Lib:** Handles all rule sets and combines those with products creating specific patient context prescription rules.
    - Capabilities:  
      - Unified rule set management across all sources  
      - Patient-context-specific prescription rule generation  
      - Product-rule combination and validation  
      - Can store and retrieve products and rules in OTS  
      - Can log all rule retrieval messages  
      - Can run as an agent based microservice  
      - Can be accessed as a MCP service  
    - Depends on:  
      - **Informedica.ZForm.Lib**

14. **Informedica.GenORDER.Lib:** Domain library providing a structured representation of orders (medication/nutrition) and turning orders into equations for calculation by GenSOLVER.
    - Capabilities:  
      - Structured medication and nutrition order representation  
      - Conversion of orders to equations for GenSOLVER  
      - Order validation and constraint generation  
      - Can log all order processing messages  
      - Can run as an agent based microservice  
      - Can be exposed via MCP host  
    - Depends on:  
      - **Informedica.GenFORM.Lib**

15. **Informedica.MCP.Lib:** Library enabling a MCP implementation and using LLMs.
    - Capabilities:  
      - Model Context Protocol (MCP) framework implementation in F#  
      - LLM-powered extraction of structured typed records from free text  
      - Agent-based MCP service execution  
      - Can host ZForm, GenFORM, GenORDER  
    - Depends on:  
      - **Informedica.ZForm.Lib**  
      - **Informedica.GenFORM.Lib**  
      - **Informedica.GenORDER.Lib**

16. **Informedica.FHIR.Lib:** Library to convert orders to FHIR format for integration.  
    - Capabilities:  
      - FHIR resource serialization and deserialization  
      - Order-to-FHIR conversion for interoperability  
      - FHIR validation and compliance  
      - Can log all FHIR conversion messages  
      - Can run as an agent based microservice  
    - Depends on:  
      - **Informedica.GenORDER.Lib**

17. **Informedica.DataPlatform.Lib:** Library to send and retrieve patient order sets to and from the Data Platform.  
    - Capabilities:  
      - Data Platform integration for patient order persistence  
      - Order set storage, retrieval, and synchronization  
      - Can log all storage retrieval messages  
      - Can run as an agent based microservice  
    - Depends on:  
      - **Informedica.FHIR.Lib**

18. **Informedica.HIXConnect.Lib:** Library to send and retrieve patient order sets to and from EHR, along with session and authentication/authorization.
    - Capabilities:  
      - EHR (HIX) integration for order management  
      - Session management and authentication/authorization  
      - Bi-directional order synchronization with EHR systems  
      - Can log all storage retrieval messages  
      - Can run as an agent based microservice  
    - Depends on:  
      - **Informedica.FHIR.Lib**

19. **Informedica.GenPRES.Server:** Service application that handles all messaging to and from GenPRES.
    - Capabilities:  
      - Backend service for GenPRES messaging and orchestration  
      - API endpoints for client applications  
      - Integration hub for EHR, Data Platform, and business logic  
      - Can use MCP hosted services  
    - Depends on:  
      - **Informedica.HIXConnect.Lib**  
      - **Informedica.DataPlatform.Lib**

20. **Informedica.GenPRES.Client:** Client application that handles the desktop and mobile views for GenPRES.
    - Capabilities:  
      - Desktop and mobile user interface for prescription management  
      - User interaction layer for GenPRES functionality  
      - Client-side workflow orchestration  
    - Depends on:  
      - **Informedica.GenPRES.Server**

## **Addendum 4.** Tech Stack

### Core Technologies

#### Development Platform

- **.NET 10.0**: Primary runtime and SDK  
- **F# 10.0**: Functional-first programming language for all libraries and applications  
- **SAFE Stack**: Full-stack F# development framework  
  - **Server**: F# with Saturn framework  
  - **Client**: F# with Fable (F# to JavaScript compiler) and Elmish (MVU architecture)

For the canonical and up-to-date development toolchain requirements
(.NET SDK, Node.js, npm), see the **Toolchain Requirements** section in
[`DEVELOPMENT.md`](../../DEVELOPMENT.md#toolchain-requirements).

#### Frontend Technologies

- **Fable 4.x**: F# to JavaScript transpiler  
- **Elmish**: Model-View-Update architecture  
- **Node.js**: 18.x, 22.x, or 23.x (LTS versions)  
- **npm**: 10.x or later for package management  
- **Material UI**: User interface components library  
- **Web Technologies**: HTML5, CSS3, JavaScript (generated from F#)

#### Backend Technologies

- **Saturn**: F# web framework built on ASP.NET Core  
- **ASP.NET Core 10.0**: Web server and API framework  
- **Giraffe**: Functional ASP.NET Core micro web framework  
- **Suave.IO**: Lightweight web server (if applicable)

### Testing & Quality Assurance

#### Testing Frameworks

- **Expecto**: Unit testing framework for F#  
- **FsCheck**: Property-based testing library  
- **Fable.Mocha**: Client-side testing

#### Quality Tools

- **Paket**: Dependency management  
- **FAKE**: F# build automation system  
- **Fantomas**: F# code formatter

### Infrastructure & Deployment

#### Containerization

- **Docker**: Container platform for deployment  
- **Multi-stage Builds**: Optimized container images  
  - Build stage: .NET SDK 10.0  
  - Runtime stage: .NET ASP.NET 10.0

#### Cloud & Hosting

- **Cloud-agnostic**: Designed to run on major cloud providers  
- **Docker Orchestration**: Kubernetes-ready architecture  
- **Zero-downtime Deployment**: Blue-green deployment capability

### Data & Integration

#### Data Storage

- **OTS (Ontology Terminology Server)**: Knowledge Platform for products and rules  
  - Version control for rule sets  
  - Import/export via spreadsheets  
- **Data Platform**: Patient order persistence  
- **Google Sheets**: External data source integration (development/demo)

#### Integration Standards

- **FHIR R4**: Healthcare interoperability standard  
  - MedicationRequest  
  - MedicationDispense  
  - MedicationAdministration  
- **HIX Connect**: Dutch hospital EHR integration standard  
- **HL7**: Healthcare data exchange  
- **G-Standard**: Dutch pharmaceutical product database standard

#### Medication Databases

- **Z-Index**: Dutch pharmaceutical product registry  
  - Medication products  
  - Dose monitoring rules  
  - Interaction rules  
  - Duplicate medication rules

### Advanced Features

#### Artificial Intelligence

- **Model Context Protocol (MCP)**: LLM integration framework  
- **Natural Language Processing (NLP)**: Rule extraction from free text  
- **LLM Integration**: Structured data extraction

#### Agent Architecture

- **F# MailboxProcessor**: Message-based concurrency  
- **Asynchronous Agents**: Microservice-style architecture  
- **Event-driven**: Message-passing patterns

### Development Tools

#### Version Control & CI/CD

- **Git**: Source control  
- **GitHub**: Repository hosting and collaboration  
- **GitHub Actions**: Continuous Integration/Continuous Deployment

#### Development Environment

- **VS Code**: Recommended IDE with Ionide extension  
- **Visual Studio**: Full IDE support  
- **JetBrains Rider**: Alternative IDE  
- **direnv**: Environment variable management (optional)

#### Code Quality

- **F# Compiler**: Strong type system and safety guarantees  
- **Static Analysis**: Built-in F# type checking  
- **XML Documentation**: API documentation  
- **Conventional Commits**: Structured commit messages

### Security & Compliance

#### Medical Device Compliance

- **MDR Certification Ready**: Medical Device Regulation compliance  
- **Audit Logging**: Comprehensive activity logging  
- **Traceability**: Full calculation and decision audit trails  
- **Validation Pipeline**: Formal validation for all rules

### Mathematical & Domain Libraries

#### Core Domain Libraries

- **Informedica.GenUNITS.Lib**: Unit-of-measure calculations with exact arithmetic  
- **Informedica.GenSolver.Lib**: Constraint satisfaction solver  
- **Informedica.GenCore.Lib**: Core domain concepts (Patient, measurements)  
- **Informedica.GenForm.Lib**: Prescription rules and formularies  
- **Informedica.GenOrder.Lib**: Order management and calculations

#### Supporting Libraries

- **Informedica.Utils.Lib**: Common utilities and helpers  
- **Informedica.Agents.Lib**: Agent-based concurrency patterns  
- **Informedica.Logging.Lib**: Structured logging with human-readable output  
- **Informedica.ZIndex.Lib**: Z-Index data parsing  
- **Informedica.ZForm.Lib**: Dose rule processing  
- **Informedica.FHIR.Lib**: FHIR resource serialization  
- **Informedica.MCP.Lib**: Model Context Protocol implementation

### Browser Compatibility

#### Supported Browsers

- **Chrome/Chromium**: Latest version  
- **Firefox**: Latest version  
- **Safari**: Latest version  
- **Edge**: Latest version  
- **Mobile Browsers**: iOS Safari, Chrome Mobile

---

**Note**: This tech stack is designed for:

- **Safety**: Medical-grade reliability and accuracy  
- **Scalability**: Cloud-ready architecture with Docker  
- **Maintainability**: Functional programming with strong types  
- **Interoperability**: Standards-based integration (FHIR, HL7)  
- **Compliance**: MDR certification readiness

## Addendum 5. TimeLine 2026

| GenPRES TimeLine 2026 |  | Q1 | Q2 | Q3 | Q4 |
| ----- | :---- | ----- | ----- | ----- | ----- |
| ENABLING TECHNOLOGIES | OTS / Data Platform | ZIndex OTS |  |  |  |
|  |  | ZForm OTS |  |  |  |
|  |  |  | GenFORM OTS |  |  |
|  |  |  |  | GenORDER DataPlatform |  |
|  | Cloud Program | GenSERVER Cloud |  |  |  |
|  |  |  | GenFORM Cloud |  |  |
|  |  |  |  | GenORDER Cloud |  |
|  |  |  |  |  | MCP Cloud |
| PRODUCT | Engine | GenSOLVER Medication |  |  |  |
|  |  |  | GenSOLVER Nutrition |  |  |
|  |  |  |  | GenSOLVER Treatment |  |
|  | UI Desktop | Emergency Medication |  |  |  |
|  |  |  | Operational Rule Sets |  |  |
|  |  |  | Medication Prescription |  |  |
|  |  |  |  | Nutrition Prescription |  |
|  |  |  |  | Treatment Prescription |  |
|  |  |  |  | Configuration |  |
|  | UI Mobile | Emergency Medication |  |  |  |
|  |  |  | Medication Prescription |  |  |
|  |  |  |  | Treatment View |  |
|  | Integration |  |  |  | HIX Connect |
| ADOPTION | PICU/NICU | Emergency Medication |  |  |  |
|  |  |  | Medication Validation |  |  |
|  |  |  |  | Treatment Validation |  |
|  |  |  |  |  | Treatment Prescription |
|  | Outpatient Clinic |  |  | Treatment Validation |  |
|  |  |  |  |  | Treatment Prescription |
|  |  |  |  |  |  |
| Delivery Classification: |  | GUARANTEED | HIGHLY LIKELY | NOT GUARANTEED | NOT SURE |
|  |  |  |  |  |  |
