namespace ServerApi

open Shared.Types


/// The formulary view's member: the formulary port over the request's filter.
module FormularyCommand =

    /// For the log: the record is long and says nothing a log needs.
    let toString (_: Formulary) = "Formulary"


    let processCmd (env: AppEnv) (form: Formulary) = env.formulary.getFormulary form


/// The parenteralia view's member, over the same port.
module ParenteraliaCommand =

    let toString (_: Parenteralia) = "Parenteralia"


    let processCmd (env: AppEnv) (par: Parenteralia) = env.formulary.getParenteralia par
