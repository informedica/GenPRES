namespace ServerApi

open Shared.Types


/// The formulary view's member: the formulary port over the request's filter.
module FormularyCommand =

    /// For the log: the record is long and says nothing a log needs.
    let toString (_: Formulary) = "Formulary"


    /// The filter's patient, where it has one, made at the ingress; a draft that is none is
    /// refused, no patient is the formulary unfiltered.
    let processCmd (env: AppEnv) (form: Formulary) =
        match Ingress.patientOption form.Patient with
        | Ok _ -> env.formulary.getFormulary form
        | Error errs -> async { return Error errs }


/// The parenteralia view's member, over the same port.
module ParenteraliaCommand =

    let toString (_: Parenteralia) = "Parenteralia"


    let processCmd (env: AppEnv) (par: Parenteralia) = env.formulary.getParenteralia par
