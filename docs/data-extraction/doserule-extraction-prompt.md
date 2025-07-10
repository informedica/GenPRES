### 9. DoseRules Sheet

**Purpose**: Defines clinical dosing rules and limits for medications across different patient populations, routes, and clinical scenarios.

**Required Columns**:

#### Basic Identification
- `SortNo` - The order of the dose rule according the appearance in the source schedule text
- `Source` - Data source identifier (e.g., "NKF", "FK") (required)
- `Generic` - Generic medication name (required)
- `Shape` - Medication form/shape (optional)
- `Brand` - Brand name (optional)
- `GPKs` - Generic Product Codes (semicolon-separated list)
- `Indication` - Clinical indication for the medication (required)
- `Route` - Administration route (required)
- `ScheduleText` - Dosing schedule description (original source text) (required)

#### Patient Demographics
- `Dep` - Department/ward (optional)
- `Gender` - Patient gender (optional)
- `MinAge` - Minimum age (numeric, optional)
- `MaxAge` - Maximum age (numeric, optional)
- `MinWeight` - Minimum weight (numeric, optional)
- `MaxWeight` - Maximum weight (numeric, optional)
- `MinBSA` - Minimum body surface area (numeric, optional)
- `MaxBSA` - Maximum body surface area (numeric, optional)
- `MinGestAge` - Minimum gestational age (numeric, optional)
- `MaxGestAge` - Maximum gestational age (numeric, optional)
- `MinPMAge` - Minimum post-menstrual age (numeric, optional)
- `MaxPMAge` - Maximum post-menstrual age (numeric, optional)

#### Dose Configuration
- `DoseType` - Type of dose (can only be either "discontinuous", "continuous", "once", "timed" or "onceTimed")
- `DoseText` - Dose type description text (can be empty)
- `Component` - Component name for combination products (required)
- `Substance` - Active substance name (required)
- `Freqs` - Frequencies (semicolon-separated numeric values) (optional)
- `DoseUnit` - Base dose unit (required)
- `AdjustUnit` - Adjustment unit (e.g., "kg", "m2") (optional)
- `FreqUnit` - Frequency unit (e.g., "day", "hour") (optional)
- `RateUnit` - Rate unit (e.g., "hour", "min") (optional)

#### Timing Parameters
- `MinTime` - Minimum administration time (numeric, optional)
- `MaxTime` - Maximum administration time (numeric, optional)
- `TimeUnit` - Time unit for administration
- `MinInt` - Minimum interval (numeric, optional)
- `MaxInt` - Maximum interval (numeric, optional)
- `IntUnit` - Interval unit (optional)
- `MinDur` - Minimum duration (numeric, optional)
- `MaxDur` - Maximum duration (numeric, optional)
- `DurUnit` - Duration unit (optional)

#### Dose Limits
- `MinQty` - Minimum quantity per dose (numeric, optional)
- `MaxQty` - Maximum quantity per dose (numeric, optional)
- `NormQtyAdj` - Normal adjusted quantity (numeric, optional)
- `MinQtyAdj` - Minimum adjusted quantity (numeric, optional)
- `MaxQtyAdj` - Maximum adjusted quantity (numeric, optional)
- `MinPerTime` - Minimum dose per time (numeric, optional)
- `MaxPerTime` - Maximum dose per time (numeric, optional)
- `NormPerTimeAdj` - Normal adjusted dose per time (numeric, optional)
- `MinPerTimeAdj` - Minimum adjusted dose per time (numeric, optional)
- `MaxPerTimeAdj` - Maximum adjusted dose per time (numeric, optional)
- `MinRate` - Minimum rate (numeric, optional)
- `MaxRate` - Maximum rate (numeric, optional)
- `MinRateAdj` - Minimum adjusted rate (numeric, optional)
- `MaxRateAdj` - Maximum adjusted rate (numeric, optional)


**Example Data**:

| SortNo | Source | Generic | Shape | Brand | Route | GPKs | Indication | ScheduleText | Dep | Gender | MinAge | MaxAge | MinWeight | MaxWeight | MinBSA | MaxBSA | MinGestAge | MaxGestAge | MinPMAge | MaxPMAge | DoseType | DoseText | Component | Substance | Freqs | DoseUnit | AdjustUnit | FreqUnit | RateUnit | MinTime | MaxTime | TimeUnit | MinInt | MaxInt | IntUnit | MinDur | MaxDur | DurUnit | MinQty | MaxQty | NormQtyAdj | MinQtyAdj | MaxQtyAdj | MinPerTime | MaxPerTime | NormPerTimeAdj | MinPerTimeAdj | MaxPerTimeAdj | MinRate | MaxRate | MinRateAdj | MaxRateAdj |
|--------|--------|---------|-------|-------|-------|------|------------|--------------|-----|--------|--------|--------|-----------|-----------|--------|--------|------------|------------|----------|----------|----------|----------|-----------|-----------|-------|----------|------------|----------|----------|---------|---------|----------|--------|--------|---------|--------|--------|---------|--------|--------|------------|-----------|-----------|------------|------------|----------------|---------------|---------------|---------|---------|------------|------------|
| 211 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | < 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses | | | | 7 | | 2000 | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 2 | mg | kg | dag | | | | | | | | | | | | | | | 50 | | | | | | | | | |
| 212 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | < 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses | | | | 7 | | 2000 | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 2 | mg | kg | dag | | | | | | | | | | | | | | | 5 | | | | | | | | | |
| 213 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | < 1 week en geboortegewicht ≥ 2000 gr Amoxicilline/clavulaanzuur 10:1 : 75 / 7,5 mg/kg/dag in 3 doses | | | | 7 | 2000 | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | | | | | 75 | | | | | | | | | |
| 214 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | < 1 week en geboortegewicht ≥ 2000 gr Amoxicilline/clavulaanzuur 10:1 : 75 / 7,5 mg/kg/dag in 3 doses | | | | 7 | 2000 | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | | | | | 7.5 | | | | | | | | | |
| 215 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | 1 week tot 4 weken en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 75 / 7,5 mg/kg/dag in 3 doses | | | 7 | 28 | | 2000 | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | | | | | 75 | | | | | | | | | |
| 216 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | 1 week tot 4 weken en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 75 / 7,5 mg/kg/dag in 3 doses | | | 7 | 28 | | 2000 | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | | | | | 7.5 | | | | | | | | | |
| 217 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | 1 week tot 4 weken en geboortegewicht ≥ 2000 gr Amoxicilline/clavulaanzuur 10:1 100/10 mg/kg/dag in 3 doses | | | 7 | 28 | 2000 | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | | | | | 100 | | | | | | | | | |
| 218 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | 1 week tot 4 weken en geboortegewicht ≥ 2000 gr Amoxicilline/clavulaanzuur 10:1 100/10 mg/kg/dag in 3 doses | | | 7 | 28 | 2000 | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | | | | | 10 | | | | | | | | | |
| 219 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | 1 maand tot 18 jaar Amoxicilline/clavulaanzuur 10:1 100/10 mg/kg/dag in 3 doses, max 12.000 mg / 600 mg per dag. Indien de maximale dosering clavulaanzuur hogere doseringen voor amoxicilline in de weg staat: overweeg amoxicilline+ clavulaanzuur af te wisselen met amoxicilline alleen (om en om). | | | 30 | 6574 | | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | 6000 | | | 100 | | | | | | | | | | |
| 220 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Ernstige bacteriele infecties | 1 maand tot 18 jaar Amoxicilline/clavulaanzuur 10:1 100/10 mg/kg/dag in 3 doses, max 12.000 mg / 600 mg per dag. Indien de maximale dosering clavulaanzuur hogere doseringen voor amoxicilline in de weg staat: overweeg amoxicilline+ clavulaanzuur af te wisselen met amoxicilline alleen (om en om). | | | 30 | 6574 | | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | 600 | | | 10 | | | | | | | | | | |
| 225 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Verdenking neonatale bacteriele infectie (in afwezigheid van positieve kweek) | Zwangerschapsduur 34 weken tot 41 weken. Postnatale leeftijd 0-7 dagen: amoxicilline/clavulaanzuur 10:1  50/5 mg/kg/dag in 2 doses | | | 0 | 7 | | | | | 238 | 287 | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 2 | mg | kg | dag | | | | | | | | | | | | | | | 50 | | | | | | | | | |
| 226 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Verdenking neonatale bacteriele infectie (in afwezigheid van positieve kweek) | Zwangerschapsduur 34 weken tot 41 weken. Postnatale leeftijd 0-7 dagen: amoxicilline/clavulaanzuur 10:1  50/5 mg/kg/dag in 2 doses | | | 0 | 7 | | | | | 238 | 287 | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 2 | mg | kg | dag | | | | | | | | | | | | | | | 5 | | | | | | | | | |
| 225 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Verdenking neonatale bacteriele infectie (in afwezigheid van positieve kweek) | Zwangerschapsduur 34 weken tot 41 weken. Postnatale leeftijd 8-28 dagen: amoxicilline/clavulaanzuur 10:1  75/7,5 mg/kg/dag in 3 doses | | | 8 | 28 | | | | | 238 | 287 | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | | | | | 75 | | | | | | | | | |
| 226 | NKF | amoxicilline/clavulaanzuur | | | INTRAVENEUS | | Verdenking neonatale bacteriele infectie (in afwezigheid van positieve kweek) | Zwangerschapsduur 34 weken tot 41 weken. Postnatale leeftijd 8-28 dagen: amoxicilline/clavulaanzuur 10:1  75/7,5 mg/kg/dag in 3 doses | | | 8 | 28 | | | | | 238 | 287 | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | | | | | 7.5 | | | | | | | | | |
| 226 | NKF | amoxicilline/clavulaanzuur | | | ORAAL | | Verdenking neonatale bacteriele infectie (in afwezigheid van positieve kweek) | Postnatale leeftijd 0-28 dagen Zwangerschapsduur ≥ 34 weken. Na initiele behandeling met IV antibiotica: Amoxicilline/clavulaanzuur 4:1: 60/15 mg/kg/dag in 2 doses of Amoxicilline/clavulaanzuur 8:1: 60/7.5 mg/kg/dag in 2 doses | | | 0 | 28 | | | | | 238 | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 2 | mg | kg | dag | | | | | | | | | | | | | | | 60 | | | | | | | | | |
| 226 | NKF | amoxicilline/clavulaanzuur | | | ORAAL | | Verdenking neonatale bacteriele infectie (in afwezigheid van positieve kweek) | Postnatale leeftijd 0-28 dagen Zwangerschapsduur ≥ 34 weken. Na initiele behandeling met IV antibiotica: Amoxicilline/clavulaanzuur 4:1: 60/15 mg/kg/dag in 2 doses of Amoxicilline/clavulaanzuur 8:1: 60/7.5 mg/kg/dag in 2 doses | | | 0 | 28 | | | | | 238 | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 2 | mg | kg | dag | | | | | | | | | | | | | | | | 6.75 | 16.5 | | | | | | | | |
| 209 | NKF | amoxicilline/clavulaanzuur | | | ORAAL | | Bacteriele infecties | 1 maand tot 18 jaar Amoxicilline /clavulaanzuur 8:1 50/6,25 mg/kg/dag in 3 doses. Max: 1500/187,5mg/dag. Range: 40/5 - 60/7,5 mg/kg/dag amoxicilline/clavulaanzuurAmoxicilline/clavulaanzuur 4:1 50/12,5 mg/kg/dag in 3 doses. Max: 1500/375mg/dag.Range: 40/10-60/15 mg/kg/dag amoxicilline/clavulaanzuur | | | 30 | 6574 | | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | 1500 | | | 40 | 60 | | | | | | | | | |
| 210 | NKF | amoxicilline/clavulaanzuur | | | ORAAL | | Bacteriele infecties | 1 maand tot 18 jaar Amoxicilline /clavulaanzuur 8:1 50/6,25 mg/kg/dag in 3 doses. Max: 1500/187,5mg/dag. Range: 40/5 - 60/7,5 mg/kg/dag amoxicilline/clavulaanzuurAmoxicilline/clavulaanzuur 4:1 50/12,5 mg/kg/dag in 3 doses. Max: 1500/375mg/dag.Range: 40/10-60/15 mg/kg/dag amoxicilline/clavulaanzuur | | | 30 | 6574 | | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | | | | | 15 | | | | | | | | | |
| 221 | NKF | amoxicilline/clavulaanzuur | | | ORAAL | | Ernstige bacteriele infecties | 1 maand tot 18 jaar Amoxicilline/clavulaanzuur 8:1 : 80/10 – 90/11,25 mg/kg/dag in 3 doses. Max: 6000 mg/375 mg per dag.Amoxicilline/clavulaanzuur 4:1 80/20– 90/22,5mg/kg/dag in 3 doses . Max: 6000 mg/ 375 mg per dag.Indien de maximale dosering clavulaanzuur hogere doseringen voor amoxicilline in de weg staat: overweeg amoxicilline+ clavulaanzuur af te wisselen met amoxicilline alleen (om en om). | | | 30 | 6574 | | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | amoxicilline | 3 | mg | kg | dag | | | | | | | | | | | 6000 | | | 80 | 90 | | | | | | | | | |
| 222 | NKF | amoxicilline/clavulaanzuur | | | ORAAL | | Ernstige bacteriele infecties | 1 maand tot 18 jaar Amoxicilline/clavulaanzuur 8:1 : 80/10 – 90/11,25 mg/kg/dag in 3 doses. Max: 6000 mg/375 mg per dag.Amoxicilline/clavulaanzuur 4:1 80/20– 90/22,5mg/kg/dag in 3 doses . Max: 6000 mg/ 375 mg per dag.Indien de maximale dosering clavulaanzuur hogere doseringen voor amoxicilline in de weg staat: overweeg amoxicilline+ clavulaanzuur af te wisselen met amoxicilline alleen (om en om). | | | 30 | 6574 | | | | | | | | | discontinuous | | amoxicilline/clavulaanzuur | clavulaanzuur | 3 | mg | kg | dag | | | | | | | | | | | 375 | | | | 22.5 | | | | | | | | | |
| 3158 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | Premature neonaten Postmenstruele leeftijd < 32 weken Startdosering: 12 mg/kg/dosis, éénmalig. Onderhoudsdosering: 24 mg/kg/dag in 4 doses.De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus. Het minimum interval tussen 2 toedieningen is 4 uur, maximaal aantal toedieningen per dag is 4. | | | | 182 | | | | | | 258 | | 224 | once | startdosering | paracetamol | paracetamol | | mg | kg | | | | | | | | | | | | | 12 | | | | | | | | | | | | | |
| 3159 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | Premature neonaten Postmenstruele leeftijd < 32 weken Startdosering: 12 mg/kg/dosis, éénmalig. Onderhoudsdosering: 24 mg/kg/dag in 4 doses.De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus. Het minimum interval tussen 2 toedieningen is 4 uur, maximaal aantal toedieningen per dag is 4. | | | | 182 | | | | | | 258 | | 224 | discontinuous | | paracetamol | paracetamol | 4 | mg | kg | dag | | | | | | | | | | | | | | | 24 | | | | | | | | | |
| 3160 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | Premature neonaten Postmenstruele leeftijd 32 weken tot 44 weken Startdosering: 20 mg/kg/dosis, éénmalig. Onderhoudsdosering: 40 mg/kg/dag in 4 doses.De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus.Het minimum interval tussen 2 toedieningen is 4 uur; maximaal aantal toedieningen per dag is 4. | | | | 182 | | | | | | 258 | 224 | 308 | once | startdosering | paracetamol | paracetamol | | mg | kg | | | | | | | | | | | | | 20 | | | | | | | | | | | | | |
| 3161 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | Premature neonaten Postmenstruele leeftijd 32 weken tot 44 weken Startdosering: 20 mg/kg/dosis, éénmalig. Onderhoudsdosering: 40 mg/kg/dag in 4 doses.De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus.Het minimum interval tussen 2 toedieningen is 4 uur; maximaal aantal toedieningen per dag is 4. | | | | 182 | | | | | | 258 | 224 | 308 | discontinuous | | paracetamol | paracetamol | 4 | mg | kg | dag | | | | | | | | | | | | | | | 40 | | | | | | | | | |
| 3162 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | a terme neonaat Startdosering: 20 mg/kg/dosis, éénmalig. Onderhoudsdosering: 40 mg/kg/dag in 4 doses.De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus. Het minimum interval tussen iedere toediening dient 4 uur te zijn. Maximaal aantal toedieningen per dag is 4. | | | | 30 | | | | | 259 | | | | once | startdosering | paracetamol | paracetamol | | mg | kg | | | | | | | | | | | | | 20 | | | | | | | | | | | | | |
| 3163 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | a terme neonaat Startdosering: 20 mg/kg/dosis, éénmalig. Onderhoudsdosering: 40 mg/kg/dag in 4 doses.De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus. Het minimum interval tussen iedere toediening dient 4 uur te zijn. Maximaal aantal toedieningen per dag is 4. | | | | 30 | | | | | 259 | | | | discontinuous | | paracetamol | paracetamol | 4 | mg | kg | dag | | | | | | | | | | | | | | | 40 | | | | | | | | | |
| 3164 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | 1 maand tot 18 jaar Startdosering: 20 mg/kg/dosis, éénmalig. Maximale dosering per gift: 1 g/dosis. Onderhoudsdosering: 60 mg/kg/dag in 4 doses. Max: 4 g/dag. Maximale dosering per gift: 1 g/dosis. De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus. Het minimum interval tussen iedere toediening dient 4 uur te zijn. Het maximum aantal toedieningen per dag is 4. | | | 30 | 6574 | | | | | | | | | once | startdosering | paracetamol | paracetamol | | mg | kg | | | | | | | | | | | 1000 | | | 20 | | | | | | | | | | | | |
| 3165 | NKF | paracetamol | | | INTRAVENEUS | | Pijn, acuut/post-operatief | 1 maand tot 18 jaar Startdosering: 20 mg/kg/dosis, éénmalig. Maximale dosering per gift: 1 g/dosis. Onderhoudsdosering: 60 mg/kg/dag in 4 doses. Max: 4 g/dag. Maximale dosering per gift: 1 g/dosis. De paracetamol intraveneuze oplossing wordt toegediend als een 15-minuten intraveneus infuus. Het minimum interval tussen iedere toediening dient 4 uur te zijn. Het maximum aantal toedieningen per dag is 4. | | | 30 | 6574 | | | | | | | | | discontinuous | | paracetamol | paracetamol | 4 | mg | kg | dag | | | | | | | | | | | 1000 | | | | | 4000 | 60 | | | | | | | | |
| 3166 | NKF | paracetamol | | | ORAAL | | Chronische pijn | 1 maand tot 18 jaar 60 mg/kg/dag in 3 - 4 doses. Max: 3 g/dag. Maximale dosering per gift: 1 g/dosis. Bij chronische pijn wordt overleg met een pijnspecialist aanbevolen. | | | 30 | 6574 | | | | | | | | | discontinuous | | paracetamol | paracetamol | 3;4 | mg | kg | dag | | | | | | | | | | | 1000 | | | | | 3000 | 60 | | | | | | | | |
| 3139 | NKF | paracetamol | | | ORAAL | | Milde tot matige pijn; koorts | a terme neonaat 10 - 15 mg/kg/dosis, zo nodig max 3 dd. Max: 45 mg/kg/dag. Let op: sommige vloeibare doseervormenbevatten toxische hulpstoffen en zijn daarom niet geschikt voor toepassing bij neonaten (zie rubriek toedieningvormen en hulpstoffen) | | | | 30 | | | | | 259 | | | | discontinuous | | paracetamol | paracetamol | 3 | mg | kg | dag | | | | | | | | | | | | | | 10 | 15 | | | 45 | | | | | | | | |
| 3140 | NKF | paracetamol | | | ORAAL | | Milde tot matige pijn; koorts | 1 maand tot 18 jaar 10 - 15 mg/kg/dosis, zo nodig max 4dd. Max: 60mg/kg/dag, maar niet hoger dan 4 g/dag. | | | 30 | 6574 | | | | | | | | | discontinuous | | paracetamol | paracetamol | 1;2;3;4 | mg | kg | dag | | | | | | | | | | | | | | 10 | 15 | | 4000 | | | | | | | | | |
| 3143 | NKF | paracetamol | | | ORAAL | | Pijn, acuut/post-operatief | Prematuren Postconceptionele leeftijd 28 weken tot 33 weken 30 mg/kg/dag in 3 doses.Behandelduur: Kortdurend gebruik, maximaal 2-3 dagenLet op: sommige vloeibare doseervormen bevatten toxische hulpstoffen en zijn daarom niet geschikt voor toepassing bij neonaten (zie rubriek toedieningvormen en hulpstoffen) | | | | 182 | | | | | | | 196 | 231 | discontinuous | | paracetamol | paracetamol | 3 | mg | kg | dag | | | | | | | | | | | | | | | 30 | | | | | | | | | |
| 3144 | NKF | paracetamol | | | ORAAL | | Pijn, acuut/post-operatief | Prematuren Postconceptionele leeftijd 33 weken tot 37 weken 45 mg/kg/dag in 3 doses.Behandelduur: kortdurend gebruik, maximaal 2-3 dagenLet op: sommige vloeibare doseervormen bevatten toxische hulpstoffen en zijn daarom niet geschikt voor toepassing bij neonaten (zie rubriek toedieningvormen en hulpstoffen)Omwille van de eenvoud van het dosisadvies en het gebrek aan wetenschappelijke bewijsvoering naar de effectiviteit van een orale oplaaddosering, wordt er geen oplaaddosering aanbevolen. | | | | 182 | | | | | | | 231 | 259 | discontinuous | | paracetamol | paracetamol | 3 | mg | kg | dag | | | | | | | | | | | | | | | 45 | | | | | | | | | |
| 3145 | NKF | paracetamol | | | ORAAL | | Pijn, acuut/post-operatief | a terme neonaat 60 mg/kg/dag in 4 doses.Behandelduur: kortdurend gebruik, maximaal 2-3 dagen. Indien na deze periode nog pijnstilling nodig is, de dosering verlagen naar dosis voor milde tot matige pijn. Let op: sommige vloeibare doseervormen bevatten toxische hulpstoffen en zijn daarom niet geschikt voor toepassing bij neonaten (zie rubriek toedieningvormen en hulpstoffen)Omwille van de eenvoud van het dosisadvies en het gebrek aan wetenschappelijke bewijsvoering naar de effectiviteit van een orale oplaaddosering, wordt er geen oplaaddosering aanbevolen. | | | | 30 | | | | | 259 | | | | discontinuous | | paracetamol | paracetamol | 4 | mg | kg | dag 

---

# PROMP INSTRUCTIONS

When asked, extract the above described columns for the dose rule sheet in the **exact** order from the supplied text. Present the extracted data as tab delimited rows. Use the original dose text language and localization. Do not change the original text.

