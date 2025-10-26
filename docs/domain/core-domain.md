# The Core Domain Model

The core domain model aims to model the general concept of treatment of a patient by applying interventions, such as medication orders. This domain is independent of specific medical specialties or care settings. It provides the foundational abstractions and entities that other specialized domains can build upon.

## The Intervention Cycle

![Core Domain Graph](https://docs.google.com/drawings/d/e/2PACX-1vRmBkfmICA31yM16mYntvYppgCVr5PuZz80urei3J0m0YoZurKSDBtf8mSIH7xzv9sbGoMLIsOxG8kx/pub?w=1440&h=1080)

## Key Concepts

All concepts revolve around a patient context. Next to the patient the following stackeholders can be defined:

- *Patients* : Represents the overall context in which a patient is treated, including relevant clinical data, demographics, and care settings.
- *Scientists* : A specific area of medical knowledge, such as cardiology or oncology, that informs treatment decisions.
- *Health Professionals* : A specific role of a healthcare provider, such as a physician, nurse, or pharmacist, that performs interventions on patients.

The intervention cycle starts with **1. Expert Knowledge** that has to be operationalized into **2. Operational Knowledge Rules** that can be applied to specific *3. Interventions* on a *Patient* by a *Health Professional*.

The intervention cycle consists of the following stages:

- **3. Interventions** : The set of actions taken to treat a patient, such as medication orders, procedures, or therapies.
- **4. Validation** : The rules and checks that ensure interventions are appropriate, safe, and effective for the patient.
- **5. Planning** : The scheduling and coordination of interventions to optimize patient care and resource utilization.
- **6. Preparation** : The logistics and processes involved in getting interventions ready for administration to the patient.
- **7. Administration** : The actual delivery of interventions to the patient, including monitoring and documentation.
- **8. Evaluation** : The assessment of intervention outcomes to feedback into the current set of intervantions and to expand **1. Expert Knowledge** for future **2. Operational Knowledge Rules**.

There are two main types of Clinical Decision Support (CDS) rules that can be applied to interventions:

- *Rule Based CDS* : CDS rules that are based on predefined logic and criteria, such as dosage limits or contraindications.
- *AI Based CDS* : CDS rules that leverage artificial intelligence and machine learning to provide recommendations based on patient data and clinical patterns.

It is important to note that AI Based CDS can operate within the context of Rule Based CDS, providing an additional layer of decision support that complements traditional rules. And AI Based CDS can also function independently, offering insights and recommendations and thus contributing back to expert knowledge.

## Rule Based CDS

Rule Based CDS depends on careful modeling, structuring and translation of **1. Expert Knowledge** into **2. Operational Knowledge Rules** that can be applied to specific *3. Interventions* on a *Patient* by a *Health Professional*.

Once the **2. Operational Knowledge Rules** are defined they can be applied in the different stages of the intervention cycle. Application can be done in a number of ways:

1. *Direct Application* : Rules are directly applied to interventions during **4. Validation**, **5. Planning**, **6. Preparation**, **7. Administration**, and **8. Evaluation** stages to ensure safety and efficacy.
2. *Providing a bounded context for AI* : Rules define the constraints and parameters within which AI Based CDS can operate, ensuring that AI recommendations align with established clinical guidelines and safety protocols.

## AI Based CDS

![AI positioning](https://docs.google.com/drawings/d/e/2PACX-1vTeIgVFS3Vdq97zbiQDR1jcl5kD7J4oVDRRFLDnN2DrJ50DwykO5D1qf3nGfzcXsnj3r6HnJohUBCxW/pub?w=1441&h=898)

When replacing the **7. Administration** and the *Patient* with the term *Exposure* and the **8. Evaluation** with *Outcome*, the core domain model can also be applied to other domains, such as epidemiology or public health, where exposures and outcomes are studied.

![Exposure Outcome Graph](https://docs.google.com/drawings/d/e/2PACX-1vSqjlp9H-KA8dGZMiq9etMjIvse7hd-2ALzg3PNuPjAQuYUNQ69MvsUXla85_7Dfi-iggKarKWSox0O/pub?w=1441&h=898)

*Note*. The combined exposure of administration of interventions with specific patient characteristics and patient data in relation to outcome is still largely unexplored territory that can lead to new insights in medical science. Historically only direct patient related data has been used to study outcomes, while admistrered interventions are often missing or incomplete. By combining both patient data and intervention data new types of studies can be performed that can lead to new insights in medical science.

