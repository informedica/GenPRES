# GenPres Architecture Overview

This document describes the architecture of the GenPres application, a clinical decision support system (CDSS) for order management. The system is written entirely in F# and built with the SAFE Stack (Saturn, Azure, Fable, Elmish).

---

## 1. High-Level Architecture

GenPres is a **client-server web application**:

- **Server**: Runs in .NET (F#), can be hosted in a Docker container. Exposes a web API.
- **Client**: F# (Fable) compiled to JavaScript, runs in the browser.
- **Configuration**: All configuration and medication rules are (currently) maintained in Google Spreadsheets.
- **Local Drug Repository**: Drug data is cached locally in text files for performance and offline access.

---

## 2. Server

### 2.1. Technologies

- **F#** (.NET 10.0)
- **Giraffe** for web server
- **Saturn** for application composition
- **Fable.Remoting** for type-safe API communication with the client

For the canonical and up-to-date development toolchain requirements
(.NET SDK, Node.js, npm), see the **Toolchain Requirements** section in
[`DEVELOPMENT.md`](../../../DEVELOPMENT.md#toolchain-requirements).

### 2.2. Structure

- **Entry Point**: `src/Informedica.GenPRES.Server/Server.fs`
- **API Implementation**: `src/Informedica.GenPRES.Server/ServerApi.fs`
  - Implements protocol in `Informedica.GenPRES.Shared.Api.IServerApi`
  - Processes commands from the client, performs calculations/validations, and returns results
- **Domain Logic**: e.g., `src/Informedica.GenORDER.Lib`
  - Uses F# MailboxProcessor (actor model) for concurrent/isolated processing (e.g., medication order calculations). NOT IMPLEMETENTED YET.

### 2.3. Docker Hosting

- **Dockerfile** builds the server, bundles the client, and sets up the environment.
- Environment variables (e.g., `GENPRES_URL_ID`, `GENPRES_PROD`) configure which Google Spreadsheet is used for configuration.
- Entry point: runs `dotnet Informedica.GenPRES.Server.dll` and exposes port 8085.

**Example Docker Usage:**

```bash
docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" -t halcwb/genpres .
docker run -it -p 8080:8085 halcwb/genpres
```

---

## 3. Client

### 3.1. Technologies

- **F# (Fable)**: Compiles to JavaScript
- **Elmish**: Model-View-Update architecture
- **React**: UI rendering
- **Vite**: Dev/bundle tooling (see `src/Informedica.GenPRES.Client/vite.config.js`)

### 3.2. Structure

- **Entry Point**: `src/Informedica.GenPRES.Client/App.fs` and `src/Informedica.GenPRES.Client/index.html`
- Communicates with the server using Fable.Remoting proxies (`Informedica.GenPRES.Shared.Api.IServerApi`)
- Handles application state, dispatches commands, and updates UI reactively

---

## 4. Configuration via Google Spreadsheets

- **All rules, constraints, and medication data** (except local drug cache) are stored in Google Spreadsheets.
  - URLs are constructed dynamically and downloaded as CSV.
  - Example: `https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}`
- **F# Modules** (e.g., `Informedica.Utils.Lib.Web.GoogleSheets`) handle fetching and parsing of spreadsheet data.
- **Which spreadsheet to use** is controlled by the `GENPRES_URL_ID` environment variable.

---

## 5. Local Medication Drug Repository

- **Data Cache**: Proprietary cache files (not distributed publicly) containing medication product information.
  - Used for fast lookup/calculation and offline use.
  - Demo cache files are included for development.
  - Path: `src/Informedica.GenPRES.Server/data/cache/README.md` explains the folder usage.

- **Drug Data Types and Logic**:
  - Main types are defined in `src/Informedica.KinderFormularium.Lib/Drug.fs` and `src/Informedica.GenORDER.Lib/Types.fs`
  - Includes types for Drug, Dose, Route, Schedule, and DrugOrder.
  - Drug data can be loaded from local cache or generated from Google Sheets.

---

## 5.1. Domain Architecture and Transformation Pipeline

GenPres implements a comprehensive domain architecture described in detail in the `docs/domain` folder. For complete architectural specifications, see:

- **[Core Domain Model](../../domain/core-domain.md)**: Defines the transformation pipeline from expert knowledge to executable orders
- **[GenFORM](../../domain/genform-free-text-to-operational-rules.md)**: Transforms free text to Operational Knowledge Rules (OKRs)
- **[GenORDER](../../domain/genorder-operational-rules-to-orders.md)**: Transforms OKRs to Order Scenarios and defines the Order Model (Orderable, Component, Item)
- **[GenSOLVER](../../domain/gensolver-from-orders-to-quantitative-solutions.md)**: Quantitative constraint solving engine

### Key Domain Concepts

The system implements a transformation pipeline:

```text
Free Text → [GenFORM] → OKRs → [GenORDER] → Order Scenarios → [GenSOLVER] → Quantitative Solutions
```

**Operational Knowledge Rules (OKRs)** define:
- Selection Constraints (Generic, Indication, Route, Patient Category, Dose Type, etc.)
- Calculation Constraints (Dose Limits, Schedule, Duration, Volumes, Concentrations)

**Order Model** (hierarchical structure):
- Order → Prescription → Orderable → Component → Item → Dose

For details on:
- Rule types (Dose Rule, Dilution Rule, Reconstitution Rule, Renal Rule), see [GenFORM Section 3](../../domain/genform-free-text-to-operational-rules.md#3-sources-and-types-of-dose-rules)
- Order model structure, see [GenORDER Section 6](../../domain/genorder-operational-rules-to-orders.md#6.-order-model-(executable-structure))
- Dose semantics (Quantity, Per Time, Rate, Total, Adjusted), see [GenORDER Section 7](../../domain/genorder-operational-rules-to-orders.md#7.-quantitative-dose-semantics)
- Equation system (65 equations), see [GenORDER Appendix D.1](../../domain/genorder-operational-rules-to-orders.md#appendix-d.1.-equations-table)
- Constraint solving algorithm, see [GenSOLVER Section 3](../../domain/gensolver-from-orders-to-quantitative-solutions.md#3.-formal-constraint-solving-model)

### Core Capabilities

1. **Order-Independent Calculations**: The constraint-based equation system allows calculations regardless of data entry order
2. **Automatic Unit Handling**: All calculations use base units with automatic conversion
3. **Absolute Precision**: Uses BigRationals to avoid rounding errors
4. **Range and Restriction Modeling**: Supports dose ranges, frequency sets, and value constraints
5. **Safety by Construction**: Invalid options are mathematically impossible to construct
6. **Completeness Guarantee**: All valid options are preserved by the constraint solver

---

## 6. Build and Development

- **Development**: `dotnet run` for full stack, or `dotnet run list` for targets.
- **Client**: Open browser at `http://localhost:5173`.
- **Production**: Deploy via Docker.
- **Environmental Variables**:
  - `GENPRES_URL_ID`: Google Spreadsheet ID for config/data
  - `GENPRES_LOG`, `GENPRES_PROD`, `GENPRES_DEBUG`: Control logging, production/demo mode, etc.

---

## 7. Key Data Flow

1. **Startup**:
    - Server loads configuration from Google Spreadsheets (as CSV).
    - Loads or generates local drug cache.
2. **Runtime**:
    - Client sends commands (e.g., calculate dose) via API.
    - Server processes, applies rules, validates, and returns results.
    - Drug lookup/calculation uses both spreadsheet config and local cache as needed.
3. **Updates**:
    - To update rules or configuration, edit the Google Spreadsheets referenced by the server.

---

## 8. References

### Technical Stack
- [SAFE Stack Documentation](https://safe-stack.github.io/docs/)
- [Saturn](https://saturnframework.org/)
- [Fable](https://fable.io/docs/)
- [Elmish](https://elmish.github.io/elmish/)
- [.NET 10.0](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)

### Domain Architecture
- [Core Domain Model](../../domain/core-domain.md)
- [GenFORM: Free Text to Operational Rules](../../domain/genform-free-text-to-operational-rules.md)
- [GenORDER: Operational Rules to Orders](../../domain/genorder-operational-rules-to-orders.md)
- [GenSOLVER: Order Scenarios to Quantitative Solutions](../../domain/gensolver-from-orders-to-quantitative-solutions.md)

---

## 9. Notes & Limitations

- Some medication data is proprietary and not included in the public repo.
- Only demo cache files are distributed for development and testing.
- The drug repository logic and calculation code is modular and can be extended for new data sources.

---

## 10. Key Files and Libraries

### Core Server Files
- `src/Informedica.GenPRES.Server/Server.fs`: Application entry point
- `src/Informedica.GenPRES.Server/ServerApi.fs`: API implementation
- `src/Informedica.GenPRES.Client/App.fs`: Client application entry
- `Dockerfile`: Container configuration for deployment

### Domain Libraries
- **Informedica.GenFORM.Lib**: Operational Knowledge Rules implementation
- **Informedica.GenORDER.Lib**: Order scenario generation and management
- **Informedica.GenSOLVER.Lib**: Constraint solving engine
- **Informedica.GenUNITS.Lib**: Unit-aware computation
- **Informedica.ZForm.Lib**: Rule extraction from G-Standaard
- **Informedica.NKF.Lib**: Kinderformularium dosing rules
- **Informedica.FTK.Lib**: Farmacotherapeutisch Kompas rules

For complete library specifications, see [GenFORM Appendix B.3](../../domain/genform-free-text-to-operational-rules.md#addendum-b3-genform-libraries).
