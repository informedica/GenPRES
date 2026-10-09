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

GenPRES ondersteunt kinderen (inclusief neonaten) en volwassenen. Het is ontwikkeld op een intensivecareafdeling, maar kan worden toegepast in elke medische omgeving.

Het live systeem draait op <http://genpres.nl>.

Aanvullende achtergrondinformatie is beschikbaar op <https://medicatieveiligensnel.nl>.

---

## 2. De applicatie openen

### Met patiëntgegevens (EPD-koppeling)

In een klinische omgeving wordt GenPRES doorgaans gestart vanuit een Elektronisch Patiënten Dossier (EPD) waarbij patiëntparameters vooraf zijn ingevuld in de URL, bijvoorbeeld:

```url
https://genpres.nl/#patient?pag=pr&dsc=n&lan=nl&agd=730&wgt=12000&hgt=87
```

De URL gebruikt hash-based routing (`/#patient?...`). Ondersteunde queryparameters:

**Patiëntparameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `agd` | Leeftijd | Dagen (bijv. 730 ≈ 2 jaar) |
| `byr` | Geboortejaar | JJJJ |
| `bmo` | Geboortemaand | 1–12 |
| `bdy` | Geboortedag | 1–31 |
| `wgt` | Gewicht | Grammen (bijv. 12000 = 12 kg) |
| `hgt` | Lengte | Centimeters |
| `gaw` | Zwangerschapsduur weken | Weken |
| `gad` | Zwangerschapsduur dagen | Dagen |
| `cvl` | Centraal veneuze lijn | `y` = ja |
| `dep` | Afdeling | Tekst |

> Gebruik `agd` (leeftijd in dagen) of `byr`/`bmo`/`bdy` (geboortedatum), niet beide.

**Medicatieparameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `med` | Medicatie | Generieke naam |
| `rte` | Toedieningsweg | bijv. `oraal`, `intraveneus` |
| `ind` | Indicatie | Tekst |
| `dst` | Doseertype | Tekst |
| `frm` | Vorm | Tekst |

**UI-parameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `pag` | Pagina | `pr`, `el`, `cm`, `fm`, `pe`, `nu`, `op`, `ia` |
| `lan` | Taal | `en`, `nl`, `fr`, `de`, `es`, `it` |
| `dsc` | Disclaimer | `n` = verbergen |

Voorbeeldpatiënten via queryparameters:

> **Sommige van deze links zetten geen `hgt` (lengte), en enkele zetten `wgt` noch `hgt`.** Een
> patiënt heeft een leeftijd nodig, of een gewicht en een lengte: met een leeftijd schat GenPRES
> het gewicht en de lengte die het niet heeft en zegt dat erbij; met alleen een gewicht is er nog
> geen patiënt en zegt het paneel wat ontbreekt — vul de lengte aan om verder te gaan. Links met
> een leeftijd, of met `wgt` en `hgt`, komen direct op een dosis uit.

| Leeftijd (jaren) | Leeftijd (dagen) | ZD (weken) | Gewicht (kg) | Lengte (cm) | Medicatie | Toedieningsweg | Indicatie | Link |
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

### Zonder patiëntgegevens (demo / testen)

De applicatie kan worden gebruikt **zonder patiëntgegevens** in de querystring. Open de applicatie direct via:

```url
http://localhost:5173
```

of op de productieserver:

```url
http://genpres.nl
```

Wanneer er geen patiëntcontext is opgegeven, start de applicatie in demomodus. U kunt de patiëntgegevens dan handmatig invoeren in de interface voordat u een medicatie selecteert.

---

## 3. Basisnavigatie

Na het openen van de applicatie ziet u het hoofdscherm met de volgende functionele gebieden:

### Patiëntpaneel (bovenaan)

Toont de patiëntparameters (leeftijd, gewicht, geslacht, lengte). Als deze niet via de URL zijn meegegeven, kunt u ze hier handmatig invullen.

### Medicatiekeuze (hoofdgebied)

Baken de medicatie af met de keuzelijsten — indicatie, generiek, toedieningsweg, farmaceutische
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

1. **Voer patiëntgegevens in** in het patiëntpaneel. Een patiënt heeft **een leeftijd, of een
   gewicht en een lengte** nodig voordat doses worden berekend — daaronder blijft het paneel open
   en zegt het wat ontbreekt. Met alleen een leeftijd worden gewicht en lengte geschat, en het
   paneel toont ze als schatting; een gemeten waarde vervangt de schatting.
2. **Kies de indicatie en het generiek** uit de keuzelijsten.
3. **Kies toedieningsweg, vorm en doseertype.** Alleen combinaties waarvoor een doseerregel
   bestaat, worden aangeboden.
4. **Bekijk de resulterende scenario's.** Elk scenario is een volledige, geldige manier om de
   medicatie voor te schrijven; de getoonde waarden voldoen al aan elke van toepassing zijnde regel.
5. **Pas dosis of frequentie aan** met de stapknoppen. Die springen tussen toegestane waarden in
   plaats van vrije tekst te accepteren, zodat een dosis buiten de range niet in te voeren is.
6. **Druk** het voorschrift af als een papieren vastlegging nodig is.

> GenPRES voorkomt onveilige waarden in plaats van ze achteraf te signaleren: een optie die een
> regel schendt, wordt niet aangeboden. Waarden krijgen wel een kleurcodering ten opzichte van de
> geldende regels en de G-Standaard doseringscontrole (blauw = attentie, oranje = waarschuwing,
> rood = alarm), zowel in de orderweergave als in het Formularium; zie [Probleemoplossing](#9-probleemoplossing).

Wie de pagina verlaat met niet-ondertekend werk — een medicatie in voorbereiding, een
ondertekening die loopt, of een order in het plan die nog niet is ondertekend — krijgt eerst een
vraag van de browser, of dat nu via terug, vernieuwen of het sluiten van het tabblad is. Wat
achterblijft wordt niet bewaard: het volgende bezoek opent op de laatst ondertekende versie.

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
- **Startdosis** (mcg/kg/min of mL/h)
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
   - Lengte: bijv. `87` cm (met het gewicht het alternatief voor een leeftijd; geschat als er een leeftijd is)
   - Geslacht: `Man`

4. Selecteer een medicatie, bijv. `paracetamol`.

5. Bekijk de berekende doseringsinformatie.

6. Stap desgewenst de dosis omhoog of omlaag en zie hoe de overige waarden meebewegen.

### Democache

De repository bevat een democachebestand met voorbeeldmedicatiegegevens. Dit is voldoende voor alle bovenstaande testworkflows. Er is geen liveverbinding met internet of eigendomsbestanden vereist.

---

## 7. Eenheidconversie testen

GenPRES gebruikt intern `BigRational`-rekenkunde voor exacte, eenheidveilige berekeningen via **Informedica.GenUNITS.Lib**. De volgende procedure stelt u in staat eenheidconversies in de gebruikersinterface te verifiëren.

### Doseereenheden verifiëren

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
3. Bekijk de startdosis (bijv. 10–40 mcg/kg/h) en de berekende pompsnelheid.
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

- Zorg ervoor dat de vereiste software is geïnstalleerd (.NET SDK, Node.js, npm). Zie [DEVELOPMENT.md](../../../DEVELOPMENT.md#toolchain-requirements).
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

*Taal: Nederlands*  
*[🇬🇧 English version](../en/user-guide.md)*
