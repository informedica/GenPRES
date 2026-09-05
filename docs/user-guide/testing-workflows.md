# Testing Workflows

> ⚠️ **Clinical Disclaimer**: GenPRES is a Clinical Decision Support System (CDSS). It is **not** intended for direct clinical use without appropriate validation, regulatory approval, and institutional governance. Always apply independent clinical judgment. See [SUPPORT.md](../../SUPPORT.md#medical-advice-disclaimer).

This document describes reproducible testing procedures for developers and QA testers. All workflows use the **demo data** bundled with the repository — no proprietary cache file or live Google Sheets connection is required.

---

## Prerequisites

1. Start the application: `dotnet run`
2. Open a browser to <http://localhost:5173>

---

## Workflow 1 — Basic Navigation Without Patient Data

**Goal**: Verify the application loads and prompts for patient data correctly.

**Steps:**

1. Open <http://localhost:5173> with no URL parameters.
2. **Expected**: The Patient panel is expanded and displays a prompt to enter patient data.
3. Verify the **Formulary** (`fm`) page is accessible without patient data.
4. Verify that **Prescribe** (`pr`) and **Emergency list** (`el`) are either unavailable or empty until patient data is entered.

**Pass criteria**: Application loads without errors; no calculations are triggered before patient data is provided.

---

## Workflow 2 — Manual Patient Data Entry

**Goal**: Verify that manually entering patient data enables dose calculations.

**Steps:**

1. Open <http://localhost:5173>.
2. In the **Patient** panel, enter:
   - Age: `10` years (the panel takes years, months, weeks and days, not a birth date)
   - Weight: `20` kg
   - Height: `115` cm
3. **Expected**: The Patient panel collapses automatically once weight and height are set.
4. Navigate to the **Prescribe** view.
5. Select a medication (e.g., *paracetamol*).
6. **Expected**: Recommended dose range and calculation results are displayed.

**Pass criteria**: Calculated dosage is shown; no errors or blank result panels.

---

## Workflow 3 — Patient Data via URL Parameters

**Goal**: Verify URL-based patient context loading.

**Steps:**

1. Open the following URL directly in the browser:

   ```
   http://localhost:5173/#patient?by=2015&bm=6&bd=1&wt=20000&ht=115&pg=pr
   ```

2. **Expected**: The application loads with the patient context already set (age ≈ 10 years, weight 20 kg, height 115 cm) and the Prescribe view is active.
3. Confirm no patient data entry prompt is shown.
4. Select a medication and verify a dose calculation is produced.

**Pass criteria**: Patient context is loaded from URL; prescribing view is active on load.

---

## Workflow 4 — Unit Conversion Testing

**Goal**: Verify that the application correctly handles automatic unit conversions.

GenPRES performs all calculations using exact rational arithmetic (BigRationals). Units are converted automatically — users are not required to enter values in specific units.

### 4a — Weight-Based Dose Calculation

1. Open:

   ```
   http://localhost:5173/#patient?by=2010&bm=1&bd=1&wt=30000&ht=130&pg=pr
   ```

2. Select **paracetamol** (or another weight-based medication).
3. Observe the calculated dose.
4. **Change the weight** (in the Patient panel) from 30 kg to 60 kg.
5. **Expected**: The calculated dose updates proportionally (doubles for linear weight-based dosing).

**Pass criteria**: Dose scales correctly with weight; no manual unit entry required.

### 4b — Dose Unit Display

1. With a patient loaded and paracetamol selected in the **Prescribe** view:
2. Observe the dose presented in the results (e.g., mg, mg/kg, mg/dose).
3. Adjust the dose or frequency using the available steppers.
4. **Expected**: All displayed values remain consistent across units (e.g., total daily dose = dose per administration × frequency).

**Pass criteria**: Unit relationships are internally consistent; changing one value updates dependent values correctly.

### 4c — Concentration and Volume

1. Open:

   ```
   http://localhost:5173/#patient?by=2015&bm=1&bd=1&wt=20000&ht=115&pg=cm
   ```

2. Select a continuous infusion medication (e.g., *morfine* / morphine).
3. Observe concentration (mg/mL), rate (mL/hr), and total dose (mg/hr or mcg/kg/min).
4. Modify the infusion rate and verify the dependent dose rate updates accordingly.

**Pass criteria**: Concentration × rate = dose rate; values update consistently.

---

## Workflow 5 — Emergency List

**Goal**: Verify the emergency medication list loads with correct weight-adjusted doses.

**Steps:**

1. Open:

   ```
   http://localhost:5173/#patient?by=2018&bm=1&bd=1&wt=12000&ht=85&pg=el
   ```

2. **Expected**: A list of emergency medications is displayed with pre-calculated doses based on the patient weight (12 kg).
3. Verify that doses are shown in an appropriate unit (e.g., mg or mL).
4. **Change the weight** to 15 kg and verify the list updates.

**Pass criteria**: Emergency list populates; doses scale with weight.

---

## Workflow 6 — Neonate / Gestational Age

**Goal**: Verify correct handling of neonates with gestational age.

**Steps:**

1. Open:

   ```
   http://localhost:5173/#patient?ad=5&wt=3200&gw=39&gd=2&pg=pr
   ```

   (Age: 5 days, weight: 3200 g, gestational age: 39 weeks + 2 days)

2. **Expected**: Patient is recognized as a neonate; gestational-age-specific dosing rules apply where applicable.
3. Select a medication and verify the dose recommendation reflects neonatal constraints.

**Pass criteria**: Application correctly identifies neonate patient type; dosing constraints are applied.

---

## Workflow 7 — Language Selection

**Goal**: Verify UI language switching.

**Steps:**

1. Open:

   ```
   http://localhost:5173/#patient?by=2010&bm=1&bd=1&wt=30000&ht=130&pg=pr&la=en
   ```

2. **Expected**: UI labels and instructions are displayed in English.
3. Compare with:

   ```
   http://localhost:5173/#patient?by=2010&bm=1&bd=1&wt=30000&ht=130&pg=pr&la=du
   ```

4. **Expected**: UI labels switch to Dutch.

**Pass criteria**: Language parameter is respected; UI text changes accordingly.

---

## Additional Resources

- [Getting Started](getting-started.md) — full parameter reference and setup instructions
- [External User Guides](README.md#external-user-guides) — annotated walkthroughs for clinical workflows
