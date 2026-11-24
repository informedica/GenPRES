# Implementation plan for issue #1999: move the emergency list calculations that are not medication to GenCORE

2026-11-24. Line numbers are from `master` at `7937f195`.

## Problem description

The emergency list shows rows that are not medication: the tube size, the tube lengths, the umbilical
line size and lengths, and the energy for defibrillation and cardioversion. The client calculates these
rows in `float`, in `EmergencyTreatment.calculate` (`src/Informedica.GenPRES.Shared/Models.fs:541`), with
an age in years that `PatientRead.getAgeInYears` adds up in the client
(`src/Informedica.GenPRES.Client.Core/Models/PatientRead.fs:22`). The texts of the rows are Dutch and
fixed in the code. Issue #1999 lists the formulas.

Plan 1210-1211 moves the medication rows of the list to the server and leaves these rows to a plan of
their own; this is that plan.

## Approaches considered

1. Leave the rows in the client. The clinical calculation stays outside the core and loses its only
   test when part 8 of plan 1210-1211 removes the emergency baseline tests.
2. Calculate the rows in the server, in `EmergencyListCommand`. The server is a composition root and
   maps between the contract and the core; a formula there is outside the core, as it is now.
3. Calculate the rows in `Calculations` of GenCORE, where the age, the body surface area and the renal
   function are calculated, and let the server send the result with the medication rows.

## Chosen approach

Approach 3, with the decisions of the maintainer in the issue (2026-11-24):

- **The rows go in the emergency list:** in the answer of `getEmergencyList`, not in a call of their own.
- **The numbers** are `decimal` with units of measure.
- **The texts:** GenCORE returns which intervention it is, which formula applies and the value with its
  unit. It returns no text. The client maps these to terms.
- **The split by weight stays 3 kg**, so every row keeps the result it has today.

What the code offers, checked on 2026-11-24:

- `List.findNearestMax` (`src/Informedica.GenPRES.Shared/Utils.fs:124`) returns the lowest energy of the
  list at or above the energy asked, and the highest energy of the list when more is asked. It does not
  return the nearest energy.
- `Math.roundBy0_5` rounds with `round`, which on .NET rounds a value halfway between two numbers to the
  even number: 3.75 and 4.25 both give 4.
- A patient weight in GenCORE is at least 0.2 kg (`src/Informedica.GenCORE.Lib/Patient.fs:1012`), so the
  natural logarithm of the oral tube length always has a result.
- The server does not need a Location to answer `getEmergencyList`: a rule without a location applies
  to every patient (`RuleSet.forPatient`, `src/Informedica.GenFORM.Lib/RuleSet.fs:214`). That the list
  is asked only for a patient with a Location is a condition of the client in part 8 of plan 1210-1211,
  which is not built yet.
- The client does not call `getEmergencyList` yet; part 8 of plan 1210-1211 adds the call.
- The body surface area takes an optional precision for its `float` formula
  (`BSA.calcBSA`, `src/Informedica.GenCORE.Lib/Calculations.fs:96`).

## To decide

The issue leaves three points to the plan. Each has the options and a recommendation; the maintainer
decides.

1. **The precision of the two formulas without a rational result.** The oral tube length under 3 kg
   takes a natural logarithm and the nasal tube length a square root. Calculated in `float` and
   converted to `decimal`, the result has 15 significant digits, such as 7.89491416298022 cm at 2 kg.
   The client shows a number with 3 significant digits today (`toStringNumberNLWithoutTrailingZeros`).
   - a. GenCORE returns the 15 digits and the client rounds for display, as today.
   - b. GenCORE rounds to a fixed number of decimals, and the number is part of the formula.
   - Recommendation: b, with 1 decimal, the precision a tube length can be read at. A value from the
     core then needs no rounding rule in the client. The number of decimals is a clinical choice.
   - Decided by the maintainer on 2026-11-24: GenCORE rounds to 3 significant digits
     (`Decimal.fixPrecision 3`), the precision the client shows today. This holds for the four lengths
     from a weight under 3 kg: the oral and nasal tube length and the umbilical artery and vein line
     lengths.
2. **The typed values in the contract.** `Intervention` (`src/Informedica.GenPRES.Shared/Types.fs:635`)
   has only text and `float` fields.
   - a. The answer of `getEmergencyList` becomes a record with two lists: the calculated interventions
     as contract types of their own, and the medication rows as `Intervention` records.
   - b. `Intervention` gets an optional field for the calculated intervention, next to `OrderContext`.
   - Recommendation: a. A calculated intervention uses none of the other fields of `Intervention`, and
     the page already shows the two groups one after the other.
3. **A patient without a Location.**
   - a. The client asks for the list when the patient has an age or a weight. The calculated
     interventions always come back; the medication rows follow the rule sets, as now.
   - b. The client asks only for a patient with a Location, so a patient without a Location loses the
     rows that are shown today.
   - Recommendation: a. The formulas need no Location, and the server already answers without one.

## Confidence

Medium. Step 1 is high: the formulas are copied one for one and tested at their limits. Steps 2 and 3
depend on the three decisions above, and step 3 depends on part 8 of plan 1210-1211.

## Steps

### Step 1: the calculations in GenCORE

- `Calculations.Emergency` in `src/Informedica.GenCORE.Lib/Calculations.fs` gets a function for each
  formula of the issue, in `decimal` with units of measure, and `calculate`, which takes the optional age
  in days and the optional weight in kg and returns the interventions, each with its formula:
  - a weight under 3 kg gives the tube size, the two tube lengths, the umbilical line size and the two
    umbilical line lengths, all from the weight;
  - any other weight, or no weight, gives the two tube sizes and the two tube lengths from the age,
    when there is an age, and the two energies from the weight, when there is a weight.
- The age in years is the age in days divided by 365, the year GenCORE counts with.
- `Measures.fs` gets the measures `mm`, `french` and `joule`.
- The four lengths from a weight under 3 kg follow decision 1.
- Tests in `tests/Informedica.GenCORE.Tests`: each formula at its limits (1 kg, 1.5 kg, 3 kg, the
  largest tube size of 7 mm, a value halfway between two tube sizes, both ends of the energy list), and
  `calculate` for a weight under 3 kg, a weight from 3 kg with and without an age, an age without a
  weight, and neither.
- Script: `src/Informedica.GenCORE.Lib/Scripts/Issue1999Emergency.fsx`.
- Nothing calls the new code yet, and the client keeps its own calculation until step 3.
- Estimate: 200 to 260 lines, most of them the `///` lines of the union cases. Over the limit the step
  splits in two pull requests: the functions of the formulas first, then the types and `calculate`.

### Step 2: the server sends the calculated interventions

- The contract gets the types for a calculated intervention: which intervention, which formula, the
  value and its unit. The answer of `getEmergencyList` carries them as decision 2 says.
- `EmergencyListCommand.processCmd` (`src/Informedica.GenPRES.Server/ServerApi.Commands.fs:477`) takes
  the age in days and the calculation weight, measured or estimated, from the core patient data, calls
  `Emergency.calculate` and maps the result to the contract types. The function is pure, so the server
  needs no port for it.
- Tests in the server tests: the mapping of every intervention and every formula, and the answer for a
  patient without an age, without a weight and without both.
- The client does not read the answer yet.
- Estimate: 80 to 120 lines.

### Step 3: the client shows the calculated interventions

After part 8 of plan 1210-1211, which makes the client ask the server for the list.

Part 8 of plan 1210-1211 shows only the server rows, asks for patient data the server accepts, and makes
the removals below from Shared and the client. The server writes the Dutch texts of the rows (step 2), so
the terms of this step are left open.

- The client asks for the list as decision 3 says.
- `Terms` (`src/Informedica.GenPRES.Client.Core/Models/Terms.fs`) gets a term for each intervention, for
  each formula and for the category of the rows. The fallback of each term is its English text.
- The page maps a calculated intervention to a row: the term of the intervention, the term of the
  formula as the advice, and the value with its unit. A tube size from the age shows with the sizes
  0.5 mm below and above it, as today; the client derives the two from the value.
- Removed from Shared, with the tests that cover them: the rows that are not medication in
  `EmergencyTreatment.calculate`, `calcIntervention`, `calcTube`, `calcOralLength`, `calcNasalLength`,
  `joules`, `calcDefib`, `calcCardioVersion`, `Math.roundBy0_5` and `List.findNearestMax` of
  `Shared/Utils.fs`.
- Removed from the client: the age and the weight that `calculateInterventions`
  (`src/Informedica.GenPRES.Client/App.fs:533`) reads for the emergency list.
- `PatientRead.getAgeInYears` stays while the trail describes the patient with it
  (`src/Informedica.GenPRES.Client.Core/Trail.fs:131`); #1719 decides about it.
- Estimate: 100 to 160 lines, the removed lines included.

## Issues that touch the steps

- #1210 and plan 1210-1211: part 8 comes before step 3. Part 8 removes the emergency baseline tests,
  the only tests of the rows in the client.
- #1719, the age in years of the emergency list as GenCORE counts it: step 3 resolves it for the list;
  the trail is left.
- #1896, an ADR on how GenPRES performs calculations: decision 1 is an example for it.
