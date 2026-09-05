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

GenPRES targets pediatric and neonatal intensive care settings, but can be adapted to any medical environment.

The live system runs at <http://genpres.nl>.

---

## 2. Accessing the Application

### With Patient Data (EHR Integration)

In a clinical setting GenPRES is typically launched from an Electronic Health Record (EHR; in Dutch EPD) with patient parameters pre-filled in the URL, for example:

```url
https://genpres.nl/#patient?pg=pr&dc=n&la=en&ad=730&wt=12000&ht=87
```

The URL uses hash-based routing (`/#patient?...`). Supported query parameters:

**Patient parameters:**

| Parameter | Description | Unit / Values |
|-----------|-------------|---------------|
| `ad` | Age | Days (e.g., 730 ≈ 2 years) |
| `by` | Birth year | YYYY |
| `bm` | Birth month | 1–12 |
| `bd` | Birth day | 1–31 |
| `wt` | Weight | Grams (e.g., 12000 = 12 kg) |
| `ht` | Height | Centimeters |
| `gw` | Gestational age weeks | Weeks |
| `gd` | Gestational age days | Days |
| `cv` | Central venous line | `y` = yes |
| `dp` | Department | Text |

> Use either `ad` (age in days) or `by`/`bm`/`bd` (birth date), not both.

**Medication parameters:**

| Parameter | Description | Unit / Values |
|-----------|-------------|---------------|
| `md` | Medication | Generic name |
| `rt` | Route | e.g., `oraal`, `intraveneus` |
| `in` | Indication | Text |
| `dt` | Dose type | Text |
| `fr` | Form | Text |

**UI parameters:**

| Parameter | Description | Unit / Values |
|-----------|-------------|---------------|
| `pg` | Page | `pr`, `el`, `cm`, `fm`, `pe` |
| `la` | Language | `en`, `du`, `fr`, `gr`, `sp`, `it` |
| `dc` | Disclaimer | `n` = hide |

Example patients using query parameters:

> **Some of these links do not set `ht` (height), and a few set neither `wt` nor `ht`.** GenPRES
> calculates a dose only once both weight and height are present, so opening such a link lands on a
> populated patient panel with the calculation still withheld — fill in the missing measurement to
> continue. Links that already carry `wt` and `ht` go straight to a dose.

| Age (years) | Age (days) | GA (weeks) | Weight (kg) | Height (cm) | Medication | Route | Indication | Link |
|---|---|---|---|---|---|---|---|---|
| 1 | | | 10 | | paracetamol | oraal | Milde tot matige pijn; koorts | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=paracetamol&rt=oraal&in=Milde%20tot%20matige%20pijn%3B%20koorts) |
| | 2 | 35 | 1.2 | 45 | paracetamol | oraal | Pijn, acuut/post-operatief | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=2&gw=35&wt=1200&ht=45&md=paracetamol&rt=oraal&in=Pijn%2C%20acuut%2Fpost-operatief) |
| 1 | | | 10 | | gentamicine | intraveneus | Ernstige infectie, gram negatieve microorganismen | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=gentamicine&rt=intraveneus&in=Ernstige%20infectie%2C%20gram%20negatieve%20microorganismen) |
| | 2 | 35 | 1.2 | 45 | gentamicine | intraveneus | Ernstige infectie, gram negatieve microorganismen | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=2&gw=35&wt=1200&ht=45&md=gentamicine&rt=intraveneus&in=Ernstige%20infectie%2C%20gram%20negatieve%20microorganismen) |
| 1 | | | 10 | | adrenaline | intraveneus | Circulatoire insufficientie | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=adrenaline&rt=intraveneus&in=Circulatoire%20insufficientie) |
| | 2 | 35 | 1.2 | 45 | adrenaline | intraveneus | Circulatoire insufficientie | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=2&gw=35&wt=1200&ht=45&md=adrenaline&rt=intraveneus&in=Circulatoire%20insufficientie) |
| 1 | | | 10 | | trimethoprim/sulfametrol | intraveneus | Bacteriele infecties | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=trimethoprim%2Fsulfametrol&rt=intraveneus&in=Bacteriele%20infecties) |
| 1 | | | 10 | | trimethoprim/sulfametrol | intraveneus | Behandeling Pneumocystis Jiroveci Pneumonie (PCP) | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=trimethoprim%2Fsulfametrol&rt=intraveneus&in=Behandeling%20Pneumocystis%20Jiroveci%20Pneumonie%20%28PCP%29) |
| 16 | | | 60 | | trimethoprim/sulfamethoxazol | intraveneus | Behandeling Pneumocystis Jiroveci Pneumonie | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=5856&wt=60000&md=trimethoprim%2Fsulfamethoxazol&rt=intraveneus&in=Behandeling%20Pneumocystis%20Jiroveci%20Pneumonie) |
| | 2 | 35 | 1.2 | 45 | coffeine 0-water | intraveneus | Neonatale apneu | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=2&gw=35&wt=1200&ht=45&md=coffeine%200-water&rt=intraveneus&in=Neonatale%20apneu) |
| | 2 | 35 | 1.2 | 45 | coffeine citraat | intraveneus | Neonatale apneu | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=2&gw=35&wt=1200&ht=45&md=coffeine%20citraat&rt=intraveneus&in=Neonatale%20apneu) |
| 1 | | | 10 | | tramadol | oraal | Pijn | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=tramadol&rt=oraal&in=Pijn) |
| | 21 | | 3.8 | 50 | benzylpenicilline | intraveneus | Infecties, sepsis | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=21&wt=3800&ht=50&md=benzylpenicilline&rt=intraveneus&in=Infecties%2C%20sepsis) |
| 1 | | | 10 | | benzylpenicilline | intraveneus | Infecties, sepsis | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=366&wt=10000&md=benzylpenicilline&rt=intraveneus&in=Infecties%2C%20sepsis) |
| | 2 | 35 | 1.2 | 45 | benzylpenicilline | intraveneus | Infecties, sepsis | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=2&gw=35&wt=1200&ht=45&md=benzylpenicilline&rt=intraveneus&in=Infecties%2C%20sepsis) |
| 5 | | | 20 | 100 | midazolam | intraveneus | Status epilepticus | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=1830&wt=20000&ht=100&md=midazolam&rt=intraveneus&in=Status%20epilepticus) |
| | | | | | aciclovir | intraveneus | | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=0&md=aciclovir&rt=intraveneus&in=) |
| | 3 | 29 | 1.05 | 45 | amoxicilline | intraveneus | (Ernstige) waarschijnlijke bacteriële infecties bij pasgeborenen | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=3&gw=29&wt=1050&ht=45&md=amoxicilline&rt=intraveneus&in=%28Ernstige%29%20waarschijnlijke%20bacteri%C3%ABle%20infecties%20bij%20pasgeborenen) |
| 13 | | | | | rituximab | intraveneus | Granulomatose met polyangiitis (GPA/ziekte van Wegener), microscopische polyangiitis (MPA) | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=4758&md=rituximab&rt=intraveneus&in=Granulomatose%20met%20polyangiitis%20%28GPA%2Fziekte%20van%20Wegener%29%2C%20microscopische%20polyangiitis%20%28MPA%29) |
| 5 | | | 20 | 109 | ceftazidim/avibactam | intraveneus | Gecompliceerde intra-abdominale of urineweg infecties, nosocomiale pneumonie, andere ernstige infecties door gevoelige verwekkers wanneer andere behandelopties beperkt zijn. | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=1830&wt=20000&ht=109&md=ceftazidim%2Favibactam&rt=intraveneus&in=Gecompliceerde%20intra-abdominale%20of%20urineweg%20infecties%2C%20nosocomiale%20pneumonie%2C%20andere%20ernstige%20infecties%20door%20gevoelige%20verwekkers%20wanneer%20andere%20behandelopties%20beperkt%20zijn.) |
| | 30 | | 2.77 | | piperacilline/tazobactam | intraveneus | | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=30&wt=2770&md=piperacilline%2Ftazobactam&rt=intraveneus&in=) |
| 10 | | | | | dantroleen | oraal | | [GenPRES](https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=3660&md=dantroleen&rt=oraal) |

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

1. **Enter patient details** in the patient panel. Both **weight and height** are required
   before doses are calculated — with either missing, the panel stays open and no dose appears.
2. **Choose the indication and generic** from the selection lists.
3. **Choose the route, form and dose type**. Only combinations for which a dose rule exists are
   offered.
4. **Review the resulting scenarios.** Each is a complete, valid way to prescribe the
   medication; the values shown already satisfy every applicable rule.
5. **Adjust dose or frequency** using the stepping controls. These move between allowed values
   rather than accepting free text, so an out-of-range dose cannot be entered.
6. **Print** the order if a paper record is needed.

> GenPRES prevents unsafe values rather than flagging them after entry: an option that violates
> a rule is not offered in the first place. There is no separate "warning" state to read.

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
- **Starting dose** (µg/kg/min or mL/h)
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
   - Height: e.g., `87` cm (required — no dose is calculated without it)
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
3. Observe the starting dose (e.g., 10–40 µg/kg/h) and the calculated pump rate.
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
- Review the safety colour coding — a red alert indicates a value outside the permitted range.

### Further help

- GitHub Issues: <https://github.com/informedica/GenPRES/issues>
- Slack workspace: <https://genpresworkspace.slack.com>

---

*Version: 1.0 — March 2026*
*Language: English*
*[🇳🇱 Nederlandse versie](../nl/gebruikershandleiding.md)*
