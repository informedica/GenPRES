# Dutch Regulations: In-Hospital Medication Prescribing Software

This document summarizes the Dutch regulatory and liability framework for **in-hospital developed medication prescribing software**, with a focus on **ownership, accountability, and insurance**.

---

## 1. When Is Prescribing Software a Medical Device?

- Software with a **diagnostic or therapeutic purpose** (e.g., dose calculators, contraindication alerts) qualifies as **Medical Device Software (MDSW)** under the **EU MDR**, typically under **Rule 11** (Class IIa or higher)  
  → [IGJ – Registration of a medical device / IVD](https://english.igj.nl/medical-technology/market-authorisation/registration-and-deregistration)  
  → [Business.gov.nl – Requirements for medical devices in Netherlands](https://business.gov.nl/regulation/medical-devices/)  

---

## 2. In-House (In-Hospital) Development & Use

- Hospitals may rely on the **Article 5(5) MDR “in-house exemption”** if:
  1. The software is used only within the **same legal entity**  
  2. Manufacture is on a **non-industrial scale**  
  3. A documented **justification of unmet needs** exists  
  4. **Annex I GSPR compliance** and a **QMS** are in place  
  5. A **public declaration** is made available  

  → [Business.gov.nl – Self-made medical devices / self-made devices (Netherlands)](https://business.gov.nl/regulation/medical-devices/#section-self-made-medical-devices)  
  → (Note: the MDCG 2023-1 guidance is EU-level; no exact NL version found in this review.)

- If the exemption’s conditions aren’t met (e.g. software is shared with another legal entity), then **full MDR conformity assessment and CE-marking** are required.  
  → [Business.gov.nl – Requirements for medical devices in Netherlands](https://business.gov.nl/regulation/medical-devices/)  

---

## 3. Ownership and Accountability

### a) Regulatory Manufacturer (MDR)

- The **hospital** is legally responsible as the **manufacturer** under MDR if it develops and uses the software in-house.

### b) Civil Law Liability

- **Product liability (BW 6:185 ff.)**: the hospital may be treated as the “producer” of a defective software product under Dutch law.  
- **Central hospital liability (BW 7:462)**: hospitals have central liability for acts of their professionals.

(Note: I did **not** locate a clean, authoritative Dutch government web page explaining BW 6:185 and BW 7:462 in this domain in the quick check; this is legal doctrine.)

### c) IP / Data Ownership

- **Employer typically retains IP** for code developed by employees (unless contractually otherwise).  
- **Data governance & security**: hospitals must comply with **NEN 7510** (Dutch healthcare information security standard).  
  → [IGJ – Questions about NEN 7510](https://www.igj.nl/onderwerpen/ehealth/vraag-en-antwoord/vragen-over-nen-7510)  

---

## 4. Insurance / Financial Cover

- **Wkkgz (Healthcare Quality, Complaints and Disputes Act)** does not itself mandate liability insurance, but imposes duties of quality, complaints handling, and incident reporting.  
  → [Government.nl – Wkkgz Act (healthcare quality, complaints & disputes)](https://www.government.nl/topics/quality-of-healthcare/laws-and-regulations-of-healthcare/healthcare-quality-complaints-and-disputes-act-wkkgz)  
  → [Business.gov.nl – Regulation: Wkkgz](https://business.gov.nl/regulation/quality-complaints-and-disputes-care-sector-wkkgz/)  

- In practice, hospitals maintain **liability / professional insurance** to cover:
  - **Central hospital liability**
  - **Product liability risks** from in-house developed devices

- **Research setting (WMO)**: for software used in clinical research, **subject-insurance is mandatory** under the **Compulsory Insurance Decree in Medical Research**.  
  → [CCMO – Compulsory Insurance Decree in Medical Research Involving Human Subjects](https://english.ccmo.nl/investigators/legal-framework-for-medical-scientific-research/decrees-and-ministerial-regulations/compulsory-insurance-decree-in-medical-research-involving-human-subjects-2015)  
  → [Decree 24 November 2014 – rules for compulsory insurance (PDF)](https://english.ccmo.nl/binaries/ccmo-en/documenten/publications/2020/08/12/decree-2014-containing-rules-for-compulsory-insurance-in-medical-research-involving-human-subjects-and-explanatory-memorandum/Decree%2B2014%2Bcontaining%2Brules%2Bfor%2Bcompulsory%2Binsurance%2Bin%2Bmedical%2Bresearch%2Binvolving%2Bhuman%2Bsubjects%2Band%2Bexplanatory%2Bmemorandum.pdf)  

---

## 5. Other Dutch Requirements

- **Information security**: compliance with **NEN 7510** is required in Dutch healthcare; hospitals must also implement audit logging (e.g. NEN 7513).  
  → [IGJ – Questions about NEN 7510](https://www.igj.nl/onderwerpen/ehealth/vraag-en-antwoord/vragen-over-nen-7510)  
- **Medication / interoperability standards**: in practice, Dutch hospitals adopt **Nictiz standards** for medication exchange / e-prescribing workflows. (No single NL government page located in this quick check.)  
- **IGJ supervision / registration duties**: IGJ oversees medical device compliance and registration in NL.  
  → [IGJ – Registration of a medical device or IVD](https://english.igj.nl/medical-technology/market-authorisation/registration-and-deregistration)  

---

# ✅ Internal Checklist (Hospital Use)

### Legal / Governance

- [ ] Confirm that the prescribing software is subject to **MDR Rule 11**  
- [ ] Document **justification** for using the in-house exemption (lack of adequate CE devices)  
- [ ] Prepare and publish a **public declaration** as required under Article 5(5) MDR  
- [ ] Assess **central hospital liability** under Dutch civil law (BW 7:462)  
- [ ] Ensure contracts / employment agreements clarify **IP ownership of the software code**

### Quality & Regulatory Affairs

- [ ] Establish a **QMS** aligned with Annex I GSPR  
- [ ] Maintain **technical documentation** (risk analysis, verification, validation)  
- [ ] Set up **post-market surveillance / vigilance** for software incidents  
- [ ] Report incidents / calamities to **IGJ** (in line with Wkkgz)  

### IT / Security

- [ ] Comply with **NEN 7510** for information security  
- [ ] Implement **audit trails / logging** (e.g. NEN 7513)  
- [ ] Ensure interoperability / standards for medication data (e.g. Nictiz standards)  

### Insurance / Finance

- [ ] Review hospital liability / product insurance to ensure coverage of in-house devices  
- [ ] For research setting under WMO, ensure **subject-insurance** meets statutory minima (e.g. per Decree)  

---

**Summary:**  
Hospitals in the Netherlands that develop in-house prescribing software act as **manufacturers** under MDR with associated liability exposures. While **Wkkgz** does not impose a direct insurance mandate, liability and product risks justify having insurance. In research contexts (WMO), **subject-insurance is legally required** under the Compulsory Insurance Decree. Compliance with **NEN 7510** is mandatory for health information security, and **IGJ** supervises medical device regulation and registration in NL.
