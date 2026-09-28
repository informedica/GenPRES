namespace ServerApi

open Shared.Types


/// The formulary view's member: the formulary port over the request's filter.
module FormularyCommand =

    /// For the log: the record is long and says nothing a log needs.
    let toString (_: Formulary) = "Formulary"


    /// The filter's patient mapped, where it has one; a filter without one stays the formulary
    /// unfiltered.
    let patients (f: Patient -> Patient) (form: Formulary) : Formulary =
        { form with Patient = form.Patient |> Option.map f }


    /// The patient the request edits: the filter's, where it has one.
    let patientOf (form: Formulary) = form.Patient


    /// The filter's patient, where it has one, made at the inbound boundary; a draft that is
    /// none is refused, no patient is the formulary unfiltered.
    let processCmd (env: AppEnv) (form: Formulary) =
        match Patient.patientOption form.Patient with
        | Ok _ -> env.formulary.getFormulary form
        | Error errs -> async { return Error errs }


/// The parenteralia view's member, over the same port.
module ParenteraliaCommand =

    let toString (_: Parenteralia) = "Parenteralia"


    /// No patient to map.
    let patients (_: Patient -> Patient) (par: Parenteralia) = par


    let patientOf (_: Parenteralia) : Patient option = None


    let processCmd (env: AppEnv) (par: Parenteralia) = env.formulary.getParenteralia par
