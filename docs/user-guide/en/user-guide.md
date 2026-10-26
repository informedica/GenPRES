# GenPRES User Guide (English)

> **⚠️ Medical Disclaimer**
> GenPRES is not intended for direct clinical use without appropriate validation and regulatory approval.
> See [SUPPORT.md](../../../SUPPORT.md) for the full disclaimer.

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Accessing the Application](#2-accessing-the-application)
3. [Basic Navigation](#3-basic-navigation)
4. [Prescribing Medication](#4-prescribing-medication)
5. [Emergency List and Infusion Pumps](#5-emergency-list-and-infusion-pumps)
6. [Testing Without Patient Data](#6-testing-without-patient-data)
7. [Unit Conversion Testing](#7-unit-conversion-testing)
8. [Common Use Cases](#8-common-use-cases)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Introduction

GenPRES (Generic Prescribing System) is an open-source Clinical Decision Support System (CDSS) designed to assist clinical staff in:

- Looking up evidence-based dosing rules and constraints
- Performing safe medication calculations
- Verifying the correct application of clinical protocols

GenPRES supports pediatric (including neonatal) and adult patients. It was developed in an intensive care setting but can be adapted to any medical environment.

The live system runs at <http://genpres.nl>.

---

## 2. Accessing the Application

### With Patient Data (EHR Integration)

In a clinical setting GenPRES is typically launched from an Electronic Health Record (EHR; in Dutch EPD) with patient parameters pre-filled in the URL, for example:

```url
https://genpres.nl/#patient?pag=pr&dsc=n&lan=en&agd=730&wgt=12000&hgt=87
```

The URL uses hash-based routing (`/#patient?...`). Supported query parameters:

**Patient parameters:**

| Parameter | Description | Unit / Values |
|-----------|-------------|---------------|
| `agd` | Age | Days (e.g., 730 ≈ 2 years) |
| `byr` | Birth year | YYYY |
| `bmo` | Birth month | 1–12 |
| `bdy` | Birth day | 1–31 |
| `wgt` | Weight | Grams (e.g., 12000 = 12 kg) |
| `hgt` | Height | Centimeters |
| `gaw` | Gestational age weeks | Weeks |
| `gad` | Gestational age days | Days |
| `cvl` | Central venous line | `y` = yes |
| `dep` | Department | Text |

> Use either `agd` (age in days) or `byr`/`bmo`/`bdy` (birth date), not both.

**Medication parameters:**

| Parameter | Description | Unit / Values |
|-----------|-------------|---------------|
| `med` | Medication | Generic name |
| `rte` | Route | e.g., `oraal`, `intraveneus` |
| `ind` | Indication | Text |
| `dst` | Dose type | Text |
| `frm` | Form | Text |

**UI parameters:**

| Parameter | Description | Unit / Values |
|-----------|-------------|---------------|
| `pag` | Page | `pr`, `el`, `cm`, `fm`, `pe`, `nu`, `op`, `ia` |
| `lan` | Language | `en`, `nl`, `fr`, `de`, `es`, `it` |
| `dsc` | Disclaimer | `n` = hide |

Example patients using query parameters:

> **Some of these links do not set `hgt` (height), and a few set neither `wgt` nor `hgt`.** A patient
> needs an age, or a weight and a height: with an age, GenPRES estimates the weight and height it
> does not have and says so; with a weight alone, no patient exists yet and the panel says what is
> missing — fill in the height to continue. Links that carry an age, or `wgt` and `hgt`, go straight
> to a dose.

| Age (years) | Age (days) | GA (weeks) | Weight (kg) | Height (cm) | Medication | Route | Indication | Link |
|---|---|---|---|---|---|---|---|---|
| 1 | | | 10 | | paracetamol | oraal | Milde tot matige pijn; koorts | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=paracetamol&rte=oraal&ind=Milde%20tot%20matige%20pijn%3B%20koorts) |
| | 2 | 35 | 1.2 | 45 | paracetamol | oraal | Pijn, acuut/post-operatief | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=2&gaw=35&wgt=1200&hgt=45&med=paracetamol&rte=oraal&ind=Pijn%2C%20acuut%2Fpost-operatief) |
| 1 | | | 10 | | gentamicine | intraveneus | Ernstige infectie, gram negatieve microorganismen | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=gentamicine&rte=intraveneus&ind=Ernstige%20infectie%2C%20gram%20negatieve%20microorganismen) |
| | 2 | 35 | 1.2 | 45 | gentamicine | intraveneus | Ernstige infectie, gram negatieve microorganismen | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=2&gaw=35&wgt=1200&hgt=45&med=gentamicine&rte=intraveneus&ind=Ernstige%20infectie%2C%20gram%20negatieve%20microorganismen) |
| 1 | | | 10 | | adrenaline | intraveneus | Circulatoire insufficientie | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=adrenaline&rte=intraveneus&ind=Circulatoire%20insufficientie) |
| | 2 | 35 | 1.2 | 45 | adrenaline | intraveneus | Circulatoire insufficientie | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=2&gaw=35&wgt=1200&hgt=45&med=adrenaline&rte=intraveneus&ind=Circulatoire%20insufficientie) |
| 1 | | | 10 | | trimethoprim/sulfametrol | intraveneus | Bacteriele infecties | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=trimethoprim%2Fsulfametrol&rte=intraveneus&ind=Bacteriele%20infecties) |
| 1 | | | 10 | | trimethoprim/sulfametrol | intraveneus | Behandeling Pneumocystis Jiroveci Pneumonie (PCP) | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=trimethoprim%2Fsulfametrol&rte=intraveneus&ind=Behandeling%20Pneumocystis%20Jiroveci%20Pneumonie%20%28PCP%29) |
| 16 | | | 60 | | trimethoprim/sulfamethoxazol | intraveneus | Behandeling Pneumocystis Jiroveci Pneumonie | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=5856&wgt=60000&med=trimethoprim%2Fsulfamethoxazol&rte=intraveneus&ind=Behandeling%20Pneumocystis%20Jiroveci%20Pneumonie) |
| | 2 | 35 | 1.2 | 45 | coffeine 0-water | intraveneus | Neonatale apneu | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=2&gaw=35&wgt=1200&hgt=45&med=coffeine%200-water&rte=intraveneus&ind=Neonatale%20apneu) |
| | 2 | 35 | 1.2 | 45 | coffeine citraat | intraveneus | Neonatale apneu | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=2&gaw=35&wgt=1200&hgt=45&med=coffeine%20citraat&rte=intraveneus&ind=Neonatale%20apneu) |
| 1 | | | 10 | | tramadol | oraal | Pijn | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=tramadol&rte=oraal&ind=Pijn) |
| | 21 | | 3.8 | 50 | benzylpenicilline | intraveneus | Infecties, sepsis | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=21&wgt=3800&hgt=50&med=benzylpenicilline&rte=intraveneus&ind=Infecties%2C%20sepsis) |
| 1 | | | 10 | | benzylpenicilline | intraveneus | Infecties, sepsis | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=366&wgt=10000&med=benzylpenicilline&rte=intraveneus&ind=Infecties%2C%20sepsis) |
| | 2 | 35 | 1.2 | 45 | benzylpenicilline | intraveneus | Infecties, sepsis | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=2&gaw=35&wgt=1200&hgt=45&med=benzylpenicilline&rte=intraveneus&ind=Infecties%2C%20sepsis) |
| 5 | | | 20 | 100 | midazolam | intraveneus | Status epilepticus | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=1830&wgt=20000&hgt=100&med=midazolam&rte=intraveneus&ind=Status%20epilepticus) |
| | | | | | aciclovir | intraveneus | | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=0&med=aciclovir&rte=intraveneus&ind=) |
| | 3 | 29 | 1.05 | 45 | amoxicilline | intraveneus | (Ernstige) waarschijnlijke bacteriële infecties bij pasgeborenen | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=3&gaw=29&wgt=1050&hgt=45&med=amoxicilline&rte=intraveneus&ind=%28Ernstige%29%20waarschijnlijke%20bacteri%C3%ABle%20infecties%20bij%20pasgeborenen) |
| 13 | | | | | rituximab | intraveneus | Granulomatose met polyangiitis (GPA/ziekte van Wegener), microscopische polyangiitis (MPA) | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=4758&med=rituximab&rte=intraveneus&ind=Granulomatose%20met%20polyangiitis%20%28GPA%2Fziekte%20van%20Wegener%29%2C%20microscopische%20polyangiitis%20%28MPA%29) |
| 5 | | | 20 | 109 | ceftazidim/avibactam | intraveneus | Gecompliceerde intra-abdominale of urineweg infecties, nosocomiale pneumonie, andere ernstige infecties door gevoelige verwekkers wanneer andere behandelopties beperkt zijn. | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=1830&wgt=20000&hgt=109&med=ceftazidim%2Favibactam&rte=intraveneus&ind=Gecompliceerde%20intra-abdominale%20of%20urineweg%20infecties%2C%20nosocomiale%20pneumonie%2C%20andere%20ernstige%20infecties%20door%20gevoelige%20verwekkers%20wanneer%20andere%20behandelopties%20beperkt%20zijn.) |
| | 30 | | 2.77 | | piperacilline/tazobactam | intraveneus | | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=30&wgt=2770&med=piperacilline%2Ftazobactam&rte=intraveneus&ind=) |
| 10 | | | | | dantroleen | oraal | | [GenPRES](https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=3660&med=dantroleen&rte=oraal) |

### Without Patient Data (Demo / Testing)

The application can be used **without patient data** in the query string. Open the application directly:

```url
http://localhost:5173
```

or on the production server:

```url
http://genpres.nl
```

When no patient context is provided the application starts in demo mode. You can enter patient details manually in the interface before selecting a medication.

---

## 3. Basic Navigation

After opening the application you will see the main screen divided into functional areas:

### Patient Panel (top)

Displays patient parameters (age, weight, gender, height). If these are not provided via the URL, you can enter them manually here.

### Medication Selection (main area)

Narrow the medication down with the selection lists — indication, generic, route,
pharmaceutical form and dose type. Each list only offers values that are still valid given
what you have already chosen, so a combination with no matching rule cannot be selected.

### Dosing Panel

Shows the calculated dose range based on the patient parameters and the selected dosing protocol. Fields include:

- **Dose per kg** – weight-adjusted dose
- **Total dose** – calculated absolute dose
- **Frequency** – number of doses per day
- **Route** – administration route (oral, IV, etc.)
- **Concentration / Volume** – for infusion preparations

---

## 4. Prescribing Medication

### Step-by-step workflow

1. **Enter patient details** in the patient panel. A patient needs **an age, or a weight and a
   height** before doses are calculated — below that, the panel stays open and says what is
   missing. With an age alone the weight and height are estimated, and the panel shows them as
   such; a measured value replaces the estimate.
2. **Choose the indication and generic** from the selection lists.
3. **Choose the route, form and dose type**. Only combinations for which a dose rule exists are
   offered.
4. **Review the resulting scenarios.** Each is a complete, valid way to prescribe the
   medication; the values shown already satisfy every applicable rule.
5. **Adjust dose or frequency** using the stepping controls. These move between allowed values
   rather than accepting free text, so an out-of-range dose cannot be entered.
6. **Print** the order if a paper record is needed.

> GenPRES prevents unsafe values rather than flagging them after entry: an option that violates
> a rule is not offered in the first place. Values are still color-coded against the applicable
> rules and the G-Standaard dose check (blue = caution, orange = warning, red = alert), in the
> order view as well as in the Formulary; see [Troubleshooting](#9-troubleshooting).

Leaving the page with unsigned work — a medication being prescribed, a signature under way, or
an order in the plan that is not yet signed — makes the browser ask first, whether you press
back, reload or close the tab. What you leave behind is not kept: the next visit opens on the
version last signed.

---

## 5. Emergency List and Infusion Pumps

The emergency list provides quick access to standard infusion pump settings for critical medications (e.g., adrenaline, dopamine, noradrenaline). It is designed for use in emergency and ICU scenarios.

### Opening the Emergency List

1. Open the application.
2. Navigate to **Emergency** or **Noodlijst** in the main menu.
3. Enter or confirm the patient's weight.
4. The system generates the standard infusion concentrations and pump rates for each medication.

### Standard Infusion Pumps

Each entry on the emergency list shows:

- **Medication name**
- **Recommended concentration** (e.g., 1 mg/mL)
- **Starting dose** (mcg/kg/min or mL/h)
- **Dose range** (minimum – maximum)

---

## 6. Testing Without Patient Data

You can run a complete end-to-end workflow without real patient data, which is useful for:

- Developer onboarding
- QA testing
- Training and demonstrations

### Procedure

1. Start the application locally:

   ```bash
   dotnet run
   ```

   Open <http://localhost:5173> in your browser.

2. Leave the URL query string empty (no query parameters).

3. On the main screen, **manually enter test patient data**:
   - Age: e.g., `2` years
   - Weight: e.g., `12` kg
   - Height: e.g., `87` cm (with the weight, the alternative to an age; estimated when an age is given)
   - Gender: `Male`

4. Select a medication, e.g., `paracetamol`.

5. Review the calculated dosing information.

6. Optionally step the dose up or down and observe how the other values follow.

### Demo cache

The repository contains a demo cache file with sample medication data. This is sufficient for all testing workflows above. No live internet connection or proprietary data files are required.

---

## 7. Unit Conversion Testing

GenPRES internally uses `BigRational` arithmetic for exact unit-safe calculations via **Informedica.GenUNITS.Lib**. The following procedure lets you verify unit conversions in the UI.

### Verifying dose units

1. Select a medication with a known dose (e.g., *paracetamol* oral).
2. Observe the **dose per kg** field — it should show the value in `mg/kg`.
3. Change the patient weight and confirm that the **total dose** field updates correspondingly.

### Verifying infusion concentrations

1. Select an IV medication (e.g., *morphine*).
2. Observe the **concentration** field (mg/mL) and the **rate** field (mL/h).
3. Modify the desired dose and confirm the pump rate recalculates correctly.

### Example: Paracetamol oral

| Patient weight | Dose/kg | Expected total dose |
|---------------|---------|-------------------|
| 10 kg | 15 mg/kg | 150 mg |
| 20 kg | 15 mg/kg | 300 mg |
| 30 kg | 15 mg/kg | 450 mg |

---

## 8. Common Use Cases

### Use case 1: Oral paracetamol for a toddler

1. Enter: age `2` years, weight `12` kg, height `87` cm, gender `Male`.
2. Select the generic `paracetamol` and an oral route.
3. Observe the recommended dose range (typically 10–15 mg/kg, 4–6 times daily).
4. Confirm the maximum daily dose is not exceeded.

### Use case 2: IV morphine infusion for a child

1. Enter: age `5` years, weight `20` kg, height `110` cm, gender `Female`.
2. Select the generic `morfine`, an intravenous route, and the continuous dose type.
3. Observe the starting dose (e.g., 10–40 mcg/kg/h) and the calculated pump rate.
4. Step the dose up or down; confirm the rate updates.

### Use case 3: Parenteral nutrition

1. Enter the patient parameters, including weight and height.
2. Open the **Nutrition** view.
3. Review the calculated macronutrient totals against the intake targets.
4. Adjust individual components if clinically indicated.
5. Print the order for pharmacy.

---

## 9. Troubleshooting

### Application does not start

- Make sure you have the required prerequisites installed (.NET SDK, Node.js, npm). See [DEVELOPMENT.md](../../../DEVELOPMENT.md#toolchain-requirements).
- Run `dotnet run` from the repository root.
- Check that port `5173` is not occupied by another process.

### No medication data shown

- The application requires a cache file. The demo cache (`*.demo`) included in the repository is sufficient for testing.
- Ensure the `GENPRES_PROD` environment variable is set to `0` (demo mode). See [DEVELOPMENT.md](../../../DEVELOPMENT.md#environment-configuration).

### Dose values appear incorrect

- Verify that patient weight and age are entered correctly.
- Check whether the correct administration route is selected.
- Review the safety color coding — a red alert indicates a value outside the permitted range.

### Further help

- GitHub Issues: <https://github.com/informedica/GenPRES/issues>
- Slack workspace: <https://genpresworkspace.slack.com>

---

*Language: English*
*[🇳🇱 Nederlandse versie](../nl/gebruikershandleiding.md)*
