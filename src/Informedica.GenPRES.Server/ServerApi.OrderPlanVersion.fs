namespace ServerApi

open System

module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types


/// The GenCORE patient: an identity, none for an anonymous patient, and the patient data.
type CorePatient = Informedica.GenCore.Lib.Patients.Patient


/// The GenCORE patient data: what is known about a patient apart from the identity.
type CorePatientData = Informedica.GenCore.Lib.Patients.PatientData


/// Who signed an order plan version.
type Signer =
    {
        /// The user id at the identity provider.
        UserId: string
        /// The name shown for the signer.
        DisplayName: string
    }


/// An order plan version: the order plan as the prescriber signed it, as the record holds it. A
/// prescriber creates one by signing; it stores the whole plan, filter, totals and the totals of
/// each context included, so opening the version again shows what the
/// signer saw, nothing recomputed.
type OrderPlanVersion =
    {
        /// The id of the version.
        Id: string
        /// The version number, one above the previous version of the patient.
        No: int
        /// The patient the plan belongs to.
        PatientId: string
        /// The id of the version this one was built on; None for the first.
        Base: string option
        /// The prescriber who signed the version.
        SignedBy: Signer
        /// The time the prescriber signed the version.
        SignedAt: DateTime
        /// The order plan as signed.
        Plan: GenOrder.OrderPlan
        /// Whether the patient data was verified against the reading of the platform at the
        /// challenge.
        Verified: bool
        /// The patient as the EHR gave it at the challenge, none when it could not be read. A value the
        /// prescriber typed shows as a difference with the patient of the plan.
        EhrPatient: CorePatient option
    }
