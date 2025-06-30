# GenPres2 Architecture Overview

This document describes the architecture of the GenPres2 application, a clinical decision support system (CDSS) for medication prescribing. The system is written entirely in F# and built with the SAFE Stack (Saturn, Azure, Fable, Elmish).

---

## 1. High-Level Architecture

GenPres2 is a **client-server web application**:

- **Server**: Runs in .NET (F#), can be hosted in a Docker container. Exposes a web API.
- **Client**: F# (Fable) compiled to JavaScript, runs in the browser.
- **Configuration**: All configuration and medication rules are maintained in Google Spreadsheets.
- **Local Drug Repository**: Drug data is cached locally in text files for performance and offline access.

---

## 2. Server

### 2.1. Technologies

- **F#** (.NET 9.0)
- **Giraffe** for web server
- **Saturn** for application composition
- **Fable.Remoting** for type-safe API communication with the client

### 2.2. Structure

- **Entry Point**: `src/Server/Server.fs`
- **API Implementation**: `src/Server/ServerApi.fs`
    - Implements protocol in `Shared.Api.IServerApi`
    - Processes commands from the client, performs calculations/validations, and returns results
- **Agents & Domain Logic**: e.g., `src/Server/Scripts/OrderAgent.fsx`
    - Uses F# MailboxProcessor (actor model) for concurrent/isolated processing (e.g., medication order calculations)

### 2.3. Docker Hosting

- **Dockerfile** builds the server, bundles the client, and sets up the environment.
- Environment variables (e.g., `GENPRES_URL_ID`, `GENPRES_PROD`) configure which Google Spreadsheet is used for configuration.
- Entry point: runs `dotnet Server.dll` and exposes port 8085.

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
- **Vite**: Dev/bundle tooling (see `src/Client/vite.config.js`)

### 3.2. Structure

- **Entry Point**: `src/Client/App.fs` and `src/Client/index.html`
- Communicates with the server using Fable.Remoting proxies (`Api.IServerApi`)
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
    - Path: `src/Server/data/cache/README.md` explains the folder usage.

- **Drug Data Types and Logic**:
    - Main types are defined in `src/Informedica.KinderFormularium.Lib/Drug.fs` and `src/Informedica.GenOrder.Lib/Types.fs`
    - Includes types for Drug, Dose, Route, Schedule, and DrugOrder.
    - Drug data can be loaded from local cache or generated from Google Sheets.

---

## 5.1. Order Modeling and Calculation: Informedica.GenOrder.Lib

The [`Informedica.GenOrder.Lib`](Informedica.GenOrder.Lib.md) library is central to the modeling and calculation of medical orders in GenPres2. It provides a domain model and mapping from a clinical `Order` to a set of mathematical `Equations` that can be solved by the `Informedica.GenSolver.Lib`, using `Informedica.Units.Lib` for unit conversions.

### Medication Cycle

A medical order in GenPres2 supports the full medication cycle:
1. **Lookup**: Identify available medication products, dosing, and preparation instructions.
2. **Calculation**: Compute doses and preparation steps, which can be complex and depend on patient-specific factors.
3. **Adjustment**: Support for cyclic updates as administered prescriptions may require additions or changes.

![Medication Cycle](https://docs.google.com/drawings/d/e/2PACX-1vSf_4pUCnI38jM1ad9Bq8Ody8UK5Xrc09jec246ST8JwpSsEROGAXHuVbiInydvBtjseY88lRCSSC1P/pub?w=744&h=520)

### Order Model

The order model is hierarchical and supports a wide range of clinical scenarios:

- **Order**: Identified by an `Id`, represents a specific prescription.
- **Prescription**: Details how the order is prescribed (one per order).
- **Orderable**: The entity being ordered (e.g., a medication or nutrition product).
- **Component**: An orderable can have multiple components.
- **Item**: Each component can contain multiple items, each with possible unit group variations.
- **Dose**: Each orderable, component, and item can have one or more doses, supporting adjustments (e.g., per kg, per BSA).

![Order model](https://docs.google.com/drawings/d/e/2PACX-1vTgBB0m625rx2mrDYibTaQ2moIUVPkJNKTRm8yvvWu5JaOZE-HcyoDIFtfLjYQluqKkl23_p4qRJWQG/pub?w=1222&h=638)

This model allows for flexible expression of doses (e.g., per kg bodyweight, per BSA) and supports multiple unit systems (mass, molar, etc.).

### Calculation Variables and Equations

The library defines a comprehensive set of variables representing all relevant quantities in an order (e.g., dose quantity, concentration, rate, total, adjustments). These are mapped to equations that describe the relationships between them, enabling automated calculation and validation.

**Example variable types:**
- `ItemDoseQuantity`
- `ComponentDoseTotal`
- `OrderableDoseRate`
- `PrescriptionFrequency`
- `OrderAdjust`

**Example equation:**
```
ItemDoseQuantity = ItemComponentConcentration × ComponentDoseQuantity
```

A full list of variables and equations is maintained in [`Informedica.GenOrder.Lib.md`](Informedica.GenOrder.Lib.md).

### Integration

- The server domain logic uses this model to process medication orders, perform calculations, and validate prescriptions.
- The model supports not only medication but also feeding, parenteral nutrition, and maintenance fluids.
- All calculations are unit-safe and leverage the `Informedica.Units.Lib` for conversions.

---

## 5.2. Tackling Core Calculation and Usability Challenges

The GenPres project has addressed several key challenges to enable robust, flexible, and safe medication prescribing:

1. **Order-Independent Calculations:**  
   The system is designed so that calculations can be performed regardless of the order in which quantitative data is entered. For example, if a total dose, frequency, or individual dose is provided, the system can solve for the remaining variables. This is achieved by modeling the relationships as a set of equations, allowing any variable to be solved from any combination of knowns.

2. **Automatic Unit Handling:**  
   All calculations are performed in base units, with automatic conversion between units. Users do not need to specify units explicitly; the system infers and converts as needed, ensuring consistency and reducing the risk of unit-related errors.

3. **Absolute Precision:**  
   Calculations use absolute precision quantities (e.g., BigRationals) to avoid rounding errors. This ensures that all computed values are as precise as possible, which is critical for medication safety.

4. **Range and Restriction Modeling:**  
   The system supports calculations involving ranges and restrictions. For example, a dose can be specified as a range (e.g., 60–80 mg), frequency as a set (e.g., 2, 3, or 4 times per day), and increments or specific allowed values can be enforced. This enables the system to present all valid scenarios to the prescriber.

5. **Performance Considerations:**  
   While supporting flexible calculations and ranges, the system is being optimized for performance, especially for scenarios with large sets of possible values. Mathematical modeling and collaboration with academic partners are ongoing to further improve efficiency.

6. **Comprehensive Data Integration:**  
   The system integrates two major sources of information: product data (which products are available) and dosing/preparation rules. This allows GenPres to calculate all possible valid scenarios for a given clinical context.

7. **Web-Based, Device-Agnostic Interface:**  
   To ensure usability across clinical environments, GenPres is implemented as a web application, supporting use on desktops, tablets, and mobile devices.

These solutions collectively enable GenPres to provide a "one-click" prescription experience, where the system computes all valid options and the clinician simply selects the most appropriate one.

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

- [SAFE Stack Documentation](https://safe-stack.github.io/docs/)
- [Saturn](https://saturnframework.org/)
- [Fable](https://fable.io/docs/)
- [Elmish](https://elmish.github.io/elmish/)
- More background: [GenPres2 README](https://github.com/halcwb/GenPres2/blob/master/README.md)

---

## 9. Notes & Limitations

- Some medication data is proprietary and not included in the public repo.
- Only demo cache files are distributed for development and testing.
- The drug repository logic and calculation code is modular and can be extended for new data sources.

---

## 10. File References

- **See also**:
    - [README.md](https://github.com/halcwb/GenPres2/blob/master/README.md)
    - [Dockerfile](https://github.com/halcwb/GenPres2/blob/master/Dockerfile)
    - [src/Server/ServerApi.fs](https://github.com/halcwb/GenPres2/blob/master/src/Server/ServerApi.fs)
    - [src/Informedica.KinderFormularium.Lib/Drug.fs](https://github.com/halcwb/GenPres2/blob/master/src/Informedica.KinderFormularium.Lib/Drug.fs)

---

> **This is a summary based on code and documentation found in the repository. For more in-depth details, see the full [codebase on GitHub](https://github.com/halcwb/GenPres2) and linked files above. Results are based on a search limited to the top 10 results per query.**