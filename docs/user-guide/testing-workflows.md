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

   ```url
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

   ```url
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

   ```url
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

   ```url
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

   ```url
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

   ```url
   http://localhost:5173/#patient?by=2010&bm=1&bd=1&wt=30000&ht=130&pg=pr&la=en
   ```

2. **Expected**: UI labels and instructions are displayed in English.
3. Compare with:

   ```url
   http://localhost:5173/#patient?by=2010&bm=1&bd=1&wt=30000&ht=130&pg=pr&la=du
   ```

4. **Expected**: UI labels switch to Dutch.

**Pass criteria**: Language parameter is respected; UI text changes accordingly.

---

## Workflow 8 — Launch Sequence

**Goal**: Verify the launch from the hospital EHR, simulated with the server's demo stand-ins.

In production a user reaches GenPRES from the hospital EHR: a launch script opens the browser on a sealed launch token, the browser is signed on at the identity provider, and the server opens a session for the launched patient ([uc-01](../scenarios/integration/uc-01-launch.md)). In demo mode (`GENPRES_PROD=0`, the `.env.example` default) the server hosts stand-ins for every party outside GenPRES, so the whole sequence runs on one machine. A production server answers 404 on the stub routes.

**Steps:**

1. Open <http://localhost:5173/stub/launch>. The page has two fields:
   - **PatientId**, default `stub-patient` (a ten-year-old of 32 kg). `no-data` stands for a patient the platform has no record for: the session then opens on the last signed data, or asks for patient data.
   - **Identity at the browser**: who the stub identity provider says is signed on (table below).
2. Press **Launch**. The server mints a launch token valid for two minutes and redirects to `#/session?launch=<token>`. The client erases the token from the address bar and presents it.
3. **Expected**: the server redirects through `/authorize` and `/callback`, asks the stub user registry for the role and active patient, reads the patient data, and opens the session. The browser lands on `#/session`; the title bar shows the user and role, and the session menu offers **Close session**.

| Identity | Stands for | Ends in |
|---|---|---|
| `prescriber` | a prescriber whose active patient is the launched one | an open session as prescriber |
| `prescriber-b` | a second prescriber on the same patient, for two-browser tests | an open session as Stub Prescriber B |
| `reader` | a reader; no PIN needed | an open session as reader |
| `prescriber-other-patient` | a prescriber with another patient active in the EHR | the gate: wrong patient, relaunch |
| `no-pin` | a prescriber without a PIN | the enrolment form (Workflow 9) |
| `unknown` | a login the user registry does not know | the gate: no role, "continue without launch" |
| `none` | nobody signed on at the browser | the gate: no browser identity, relaunch only |

A refusal arrives as `#/session?refused=<word>` with the words `expired`, `spent`, `invalid`, `no-identity`, `no-role`, `wrong-patient` and `enrolment`.

**Further checks:**

- **Reload after the launch**: the session resumes from the `genpres_session` cookie.
- **Replay the launch**: open the `#/session?launch=…` URL from the Network tab in another browser profile within two minutes: `spent`. After two minutes: `expired`. A token from an earlier server run: `invalid`, because the sealing key is new at every start.
- **Two launches of the same user**: the second session is open; the first is told a newer launch ended it on its next request.
- **Production**: `GENPRES_PROD=1 GENPRES_PASSWORD=<16+ chars> dotnet run`; `/stub/launch` and `/authorize` are 404, `/callback` redirects to `refused=invalid`.

**Pass criteria**: Each identity ends where the table says; a replayed or stale token is refused with the right word.

---

## Workflow 9 — Enrolment: the First Launch of a Prescriber Without a PIN

**Goal**: Verify that a prescriber without a PIN sets one before prescribing ([uc-02](../scenarios/integration/uc-02-enrolment.md)). The demo stands in for the mail service too.

**Steps:**

1. Launch with the identity `no-pin` (Workflow 8).
2. **Expected**: the browser lands on the gate **Set a PIN to continue** and a six-digit confirmation code is mailed.
3. Open <http://localhost:5173/stub/mail> in another tab: the stub outbox, newest mail first. Copy the code from "GenPRES: your confirmation code".
4. Enter the code, a PIN of four to six digits, and the PIN again, then press **Set PIN**.
5. **Expected**: the session opens and the outbox shows a second mail, "GenPRES: your PIN was set".
6. Launch `no-pin` again.
7. **Expected**: the session opens directly. The PIN lives as long as the server runs, or across restarts on SQLite (Workflow 11). The seeded prescribers start with the PIN `1234`.

**Further checks:**

- A wrong code keeps the form and shows the tries left; the third wrong code voids it.
- The code lives fifteen minutes; after that a fresh launch mails a new one.
- **Close session** is not offered while enrolling.

**Pass criteria**: The PIN is set once and used at the next launch; wrong codes are counted and limited.

---

## Workflow 10 — Signing an Order Plan

**Goal**: Verify that a prescriber signs the order plan with the PIN and that signing is the only way anything reaches the record ([uc-03](../scenarios/integration/uc-03-prescribe-and-sign.md)). The demo keeps the record in memory unless SQLite is configured (Workflow 11).

**Steps:**

1. Launch with the identity `prescriber` (Workflow 8).
2. Open **Voorschrijven**, pick a medication, route, form and indication (paracetamol, oral, tablet, mild pain will do), and press **Voorschrijven** on a scenario.
3. Open **Order Plan** and press **Ondertekenen**.
4. **Expected**: the dialog lists the orders as they will be signed and asks the PIN.
5. Enter a wrong PIN. **Expected**: the dialog stays and says two tries are left.
6. Enter `1234`. **Expected**: the snackbar says order plan version 1 was signed. Sign again: version 2.

**Further checks:**

- **Three wrong PINs**: the session ends at the PIN limit. A new launch within the next minute is refused as locked; every further wrong entry doubles the delay, up to a day.
- **Launch again**: the cart opens on the order plan version just signed. A hand edit of the patient panel over a platform reading is not kept, and the next sign says the data changed.
- **Two browsers on one patient** ([uc-04](../scenarios/integration/uc-04-two-users.md)): launch `prescriber-b` in another browser profile, prescribe and sign there. The first browser's next server call shows a snackbar that B signed a newer version, and the **Order Plan** page a bar with **Open the newest version**. Signing without opening it is refused with the same words.
- **The same identity twice** in another profile: the first browser's next action shows the gate.
- **The patient without data** (`no-data`): the first **Ondertekenen** is a notice that the data could not be verified; **Doorgaan** signs the version as unverified.
- **A reader** sees no Sign button.
- **Restart the server**: on the in-memory store the record is gone; on SQLite it survives.

**Pass criteria**: Only a right PIN signs; the version number increases per signature; a newer version signed elsewhere is reported before it can be overwritten.

---

## Workflow 11 — Sessions, PINs and the Record Across a Restart (SQLite)

**Goal**: Verify that the SQLite session store keeps everything a session stands on across a server restart ([ADR-0007](../adr/0007-session-persistence.md)): launches, sessions and their endings, credentials, confirmation codes, enrolment attempts, signed order plan versions, and what a session holds in flight. Every act that writes is audited in the same transaction. Nothing is ever deleted; a row past its lifetime loads as absent, so the file only grows.

**Steps:**

1. Set `GENPRES_DB_CONNECTION=Data Source=data/db/genpres.db` in `.env` or on the command line. A relative path is rooted at the folder holding `.env` (or `GENPRES_ROOT`); the folder is created and migrations run at start-up.
2. **Expected**: the start-up banner says `set (SQLite session store)`.
3. Launch, prescribe and sign (Workflow 10). Stop and start the server and reload the tab.
4. **Expected**: the session is still there and the next signature is version 2.
5. Enrol `no-pin` with a PIN of your own (Workflow 9), restart the server, and launch `no-pin` again.
6. **Expected**: the session opens on the PIN you chose. A lock after three wrong PINs survives too.
7. Read the audit trail:

   ```bash
   sqlite3 data/db/genpres.db "select at, action, outcome, session_id, actor from audit_entry order by id"
   ```

8. To start from nothing, stop the server and delete `data/db/genpres.db`.

The demo credentials are seeded once per login and never overwrite a PIN a user set. Production refuses the key: with `GENPRES_PROD=1` the server refuses to start and names the setting. The file is never tracked.

**Pass criteria**: Sessions, PINs, locks and signed versions survive a restart; the audit table lists every act.

---

## Additional Resources

- [Getting Started](getting-started.md) — full parameter reference and setup instructions
- [Cookies and the development proxy](../../DEVELOPMENT.md#cookies-and-the-development-proxy) — what the launch sets in the browser
- [External User Guides](README.md#external-user-guides) — annotated walkthroughs for clinical workflows
