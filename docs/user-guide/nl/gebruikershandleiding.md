# GenPRES Gebruikershandleiding (Nederlands)

> **⚠️ Medisch voorbehoud**  
> GenPRES is niet bedoeld voor direct klinisch gebruik zonder passende validatie en regelgevende goedkeuring.  
> Zie [SUPPORT.md](../../../SUPPORT.md) voor de volledige disclaimer.

---

## Inhoudsopgave

1. [Inleiding](#1-inleiding)
2. [De applicatie openen](#2-de-applicatie-openen)
3. [Basisnavigatie](#3-basisnavigatie)
4. [Medicatie voorschrijven](#4-medicatie-voorschrijven)
5. [Noodlijst en infuuspompen](#5-noodlijst-en-infuuspompen)
6. [Testen zonder patiëntgegevens](#6-testen-zonder-patiëntgegevens)
7. [Eenheidconversie testen](#7-eenheidconversie-testen)
8. [Veelvoorkomende gebruiksscenario's](#8-veelvoorkomende-gebruiksscenarios)
9. [Probleemoplossing](#9-probleemoplossing)

---

## 1. Inleiding

GenPRES (Generic Prescribing System) is een open-source Clinical Decision Support System (CDSS) dat klinisch personeel ondersteunt bij:

- Het opzoeken van evidence-based doseergrenzen en protocollen
- Het uitvoeren van veilige medicatieberekeningen
- Het verifiëren van de juiste toepassing van klinische richtlijnen

GenPRES richt zich op pediatrische en neonatale intensivecareafdeling, maar kan worden toegepast in elke medische omgeving.

Het live systeem draait op <http://genpres.nl>.

Aanvullende achtergrondinformatie is beschikbaar op <https://medicatieveiligensnel.nl>.

---

## 2. De applicatie openen

### Met patiëntgegevens (EPD-koppeling)

In een klinische omgeving wordt GenPRES doorgaans gestart vanuit een Elektronisch Patiënten Dossier (EPD) waarbij patiëntparameters vooraf zijn ingevuld in de URL, bijvoorbeeld:

```
https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=730&wt=12000
```

De URL gebruikt hash-based routing (`/#patient?...`). Ondersteunde queryparameters:

**Patiëntparameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `ad` | Leeftijd | Dagen (bijv. 730 ≈ 2 jaar) |
| `by` | Geboortejaar | JJJJ |
| `bm` | Geboortemaand | 1–12 |
| `bd` | Geboortedag | 1–31 |
| `wt` | Gewicht | Grammen (bijv. 12000 = 12 kg) |
| `ht` | Lengte | Centimeters |
| `gw` | Zwangerschapsduur weken | Weken |
| `gd` | Zwangerschapsduur dagen | Dagen |
| `cv` | Centraal veneuze lijn | `y` = ja |
| `dp` | Afdeling | Tekst |

> Gebruik `ad` (leeftijd in dagen) of `by`/`bm`/`bd` (geboortedatum), niet beide.

**Medicatieparameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `md` | Medicatie | Generieke naam |
| `rt` | Toedieningsweg | bijv. `oraal`, `intraveneus` |
| `in` | Indicatie | Tekst |
| `dt` | Doseertype | Tekst |
| `fr` | Vorm | Tekst |

**UI-parameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `pg` | Pagina | `pr`, `el`, `cm`, `fm`, `pe` |
| `la` | Taal | `en`, `du`, `fr`, `gr`, `sp`, `it` |
| `dc` | Disclaimer | `n` = verbergen |

Voorbeeldpatiënten via queryparameters:

| Leeftijd (jaren) | Leeftijd (dagen) | ZD (weken) | Gewicht (kg) | Lengte (cm) | Medicatie | Toedieningsweg | Indicatie | Link |
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

### Zonder patiëntgegevens (demo / testen)

De applicatie kan worden gebruikt **zonder patiëntgegevens** in de querystring. Open de applicatie direct via:

```
http://localhost:5173
```

of op de productieserver:

```
http://genpres.nl
```

Wanneer er geen patiëntcontext is opgegeven, start de applicatie in demomodus. U kunt de patiëntgegevens dan handmatig invoeren in de interface voordat u een medicatie selecteert.

---

## 3. Basisnavigatie

Na het openen van de applicatie ziet u het hoofdscherm met de volgende functionele gebieden:

### Patiëntpaneel (bovenaan)

Toont de patiëntparameters (leeftijd, gewicht, geslacht, lengte). Als deze niet via de URL zijn meegegeven, kunt u ze hier handmatig invullen.

### Medicatiekeuze (hoofdgebied)

Bak de medicatie af met de keuzelijsten — indicatie, generiek, toedieningsweg, farmaceutische
vorm en doseertype. Elke lijst toont alleen waarden die nog geldig zijn bij wat u al gekozen
hebt, zodat een combinatie zonder bijbehorende doseerregel niet te selecteren is.

### Doseerpaneel

Toont de berekende doseringsrange op basis van de patiëntparameters en het geselecteerde doseringsprotocol. Velden omvatten:

- **Dosis per kg** – gewichtsaangepaste dosis
- **Totale dosis** – berekende absolute dosis
- **Frequentie** – aantal doses per dag
- **Toedieningsweg** – oraal, IV, rectaal, etc.
- **Concentratie / Volume** – voor infuusbereidingen

---

## 4. Medicatie voorschrijven

### Stapsgewijze werkwijze

1. **Voer patiëntgegevens in** in het patiëntpaneel. Zowel **gewicht als lengte** zijn nodig
   voordat doses worden berekend — ontbreekt er één, dan blijft het paneel open en verschijnt
   er geen dosis.
2. **Kies de indicatie en het generiek** uit de keuzelijsten.
3. **Kies toedieningsweg, vorm en doseertype.** Alleen combinaties waarvoor een doseerregel
   bestaat, worden aangeboden.
4. **Bekijk de resulterende scenario's.** Elk scenario is een volledige, geldige manier om de
   medicatie voor te schrijven; de getoonde waarden voldoen al aan elke van toepassing zijnde regel.
5. **Pas dosis of frequentie aan** met de stapknoppen. Die springen tussen toegestane waarden in
   plaats van vrije tekst te accepteren, zodat een dosis buiten de range niet in te voeren is.
6. **Druk** het voorschrift af als een papieren vastlegging nodig is.

> GenPRES voorkomt onveilige waarden in plaats van ze achteraf te signaleren: een optie die een
> regel schendt, wordt niet aangeboden. Er is dus geen aparte waarschuwingsstatus om te lezen.

---

## 5. Noodlijst en infuuspompen

De noodlijst biedt snelle toegang tot standaardinstellingen van infuuspompen voor kritische medicatie (bijv. adrenaline, dopamine, noradrenaline). Deze is ontworpen voor gebruik in nood- en IC-situaties.

### De noodlijst openen

1. Open de applicatie.
2. Navigeer naar **Noodlijst** in het hoofdmenu.
3. Voer het gewicht van de patiënt in of bevestig dit.
4. Het systeem genereert de standaard infuusconcentraties en pompsnelheden voor elk medicament.

### Standaard infuuspompen

Elk item op de noodlijst toont:

- **Medicatienaam**
- **Aanbevolen concentratie** (bijv. 1 mg/mL)
- **Startdosis** (µg/kg/min of mL/h)
- **Doseringsrange** (minimum – maximum)

---

## 6. Testen zonder patiëntgegevens

U kunt een volledige end-to-end workflow uitvoeren zonder echte patiëntgegevens. Dit is nuttig voor:

- Onboarding van ontwikkelaars
- QA-testen
- Training en demonstraties

### Werkwijze

1. Start de applicatie lokaal:

   ```bash
   dotnet run
   ```

   Open <http://localhost:5173> in uw browser.

2. Laat de URL-querystring leeg (geen queryparameters).

3. Voer op het hoofdscherm **handmatig testpatiëntgegevens in**:
   - Leeftijd: bijv. `2` jaar
   - Gewicht: bijv. `12` kg
   - Lengte: bijv. `87` cm (verplicht — zonder lengte wordt geen dosis berekend)
   - Geslacht: `Man`

4. Selecteer een medicatie, bijv. `paracetamol`.

5. Bekijk de berekende doseringsinformatie.

6. Stap desgewenst de dosis omhoog of omlaag en zie hoe de overige waarden meebewegen.

### Democache

De repository bevat een democachebestand met voorbeeldmedicatiegegevens. Dit is voldoende voor alle bovenstaande testworkflows. Er is geen liveverbinding met internet of eigendomsbestanden vereist.

---

## 7. Eenheidconversie testen

GenPRES gebruikt intern `BigRational`-rekenkunde voor exacte, eenheidveilige berekeningen via **Informedica.GenUNITS.Lib**. De volgende procedure stelt u in staat eenheidconversies in de gebruikersinterface te verifiëren.

### Dosisoenheden verifiëren

1. Selecteer een medicament met een bekende dosis (bijv. *paracetamol* oraal).
2. Bekijk het veld **dosis per kg** — dit moet de waarde in `mg/kg` tonen.
3. Wijzig het patiëntgewicht en bevestig dat het veld **totale dosis** dienovereenkomstig bijwerkt.

### Infuusconcentraties verifiëren

1. Selecteer een IV-medicament (bijv. *morfine*).
2. Bekijk het veld **concentratie** (mg/mL) en het veld **pompsnelheid** (mL/h).
3. Wijzig de gewenste dosis en bevestig dat de pompsnelheid correct herberekend wordt.

### Voorbeeld: Paracetamol oraal

| Patiëntgewicht | Dosis/kg | Verwachte totale dosis |
|---------------|---------|----------------------|
| 10 kg | 15 mg/kg | 150 mg |
| 20 kg | 15 mg/kg | 300 mg |
| 30 kg | 15 mg/kg | 450 mg |

---

## 8. Veelvoorkomende gebruiksscenario's

### Scenario 1: Oraal paracetamol voor een peuter

1. Voer in: leeftijd `2` jaar, gewicht `12` kg, lengte `87` cm, geslacht `Man`.
2. Selecteer het generiek `paracetamol` en een orale toedieningsweg.
3. Bekijk de aanbevolen doseringsrange (doorgaans 10–15 mg/kg, 4–6 keer per dag).
4. Bevestig dat de maximale dagdosis niet wordt overschreden.

### Scenario 2: IV morfine-infuus voor een kind

1. Voer in: leeftijd `5` jaar, gewicht `20` kg, lengte `110` cm, geslacht `Vrouw`.
2. Selecteer het generiek `morfine`, een intraveneuze toedieningsweg en het doseertype continu.
3. Bekijk de startdosis (bijv. 10–40 µg/kg/h) en de berekende pompsnelheid.
4. Stap de dosis aan; bevestig dat de snelheid bijwerkt.

### Scenario 3: Parenterale voeding

1. Voer de patiëntparameters in, inclusief gewicht en lengte.
2. Open de **Voeding**-weergave.
3. Bekijk de berekende macronutriënttotalen tegen de inname-doelen.
4. Pas afzonderlijke componenten aan indien klinisch geïndiceerd.
5. Druk de opdracht af voor de apotheek.

---

## 9. Probleemoplossing

### Applicatie start niet op

- Zorg ervoor dat de vereiste vereisten zijn geïnstalleerd (.NET SDK, Node.js, npm). Zie [DEVELOPMENT.md](../../../DEVELOPMENT.md#toolchain-requirements).
- Voer `dotnet run` uit vanuit de root van de repository.
- Controleer of poort `5173` niet bezet is door een ander proces.

### Geen medicatiegegevens zichtbaar

- De applicatie vereist een cachebestand. De democache (`*.demo`) in de repository is voldoende voor testen.
- Zorg ervoor dat de omgevingsvariabele `GENPRES_PROD` is ingesteld op `0` (demomodus). Zie [DEVELOPMENT.md](../../../DEVELOPMENT.md#environment-configuration).

### Dosiswaarden lijken onjuist

- Controleer of patiëntgewicht en leeftijd correct zijn ingevoerd.
- Controleer of de juiste toedieningsweg is geselecteerd.
- Bekijk de veiligheidskleurcodering — een rood signaal geeft een waarde buiten het toegestane bereik aan.

### Verdere hulp

- GitHub Issues: <https://github.com/informedica/GenPRES/issues>
- Slack-werkruimte: <https://genpresworkspace.slack.com>

---

*Versie: 1.0 — maart 2026*  
*Taal: Nederlands*  
*[🇬🇧 English version](../en/user-guide.md)*
