/// Reads what a url carries. The router gives the url as its segments; a "#/patient?..." url
/// carries an anonymous patient, a page, a language, the disclaimer and a medication, a
/// "#/session?..." url a launch or the refusal of one.
module Url

open System
open Shared
open Shared.Types
open Shared.Models


/// What a "#/session?..." url carries: the Launch MainEHR opened GenPRES with, or the
/// reason the return from the IdentityProvider refused it.
[<RequireQualifiedAccess>]
type LaunchUrl =
    | Launch of Launch
    | Refused of LaunchRefusal


/// The medication a "#/patient?..." url carries, each part when given.
type UrlMedication =
    {|
        indication: string option
        medication: string option
        route: string option
        form: string option
        dosetype: DoseType option
    |}


/// A part of the url that did not parse; it names no value, since the values are patient data.
[<RequireQualifiedAccess>]
type UrlPart =
    /// A "#/patient?..." url with neither a birth year nor an age in days, with the names of the
    /// parameters it did give.
    | Patient of parameters: string list
    /// A url that is neither a patient nor a session url, with its first segment.
    | Route of segment: string


/// What a url carries: a "#/patient?..." url its patient, page, language, disclaimer and
/// medication, a "#/session?..." url its launch or refusal.
type UrlParts =
    {
        /// The patient, when the url gives a birth date or an age.
        Patient: Patient option
        /// The page, when the url names one.
        Page: Page.Page option
        /// The language, when the url names one.
        Language: Localization.Locales option
        /// Whether the disclaimer shows.
        Disclaimer: bool
        /// The medication, when the url gives any part of it.
        Medication: UrlMedication option
        /// The launch, or the refusal of one, of a "#/session?..." url.
        Launch: LaunchUrl option
        /// The parts that did not parse.
        NotParsed: UrlPart list
    }


/// A url that carries nothing: no patient, page, language, medication or launch, and the
/// disclaimer shown.
let none =
    {
        Patient = None
        Page = None
        Language = None
        Disclaimer = true
        Medication = None
        Launch = None
        NotParsed = []
    }


/// The name and value pairs of a query string segment, "?a=1&b=2", decoded as the browser
/// decodes a query: a plus is a space, a pair without "=" has an empty value.
let query (segment: string) =
    // a malformed escape is kept as it is
    let decode (s: string) =
        let s = s.Replace("+", " ")

        try
            Uri.UnescapeDataString s
        with _ ->
            s

    let segment =
        if segment.StartsWith "?" then
            segment.Substring 1
        else
            segment

    segment.Split '&'
    |> Array.filter (String.IsNullOrEmpty >> not)
    |> Array.map (fun pair ->
        match pair.IndexOf '=' with
        | -1 -> decode pair, ""
        | i -> decode (pair.Substring(0, i)), decode (pair.Substring(i + 1))
    )
    |> List.ofArray


/// The whole number of a parameter, when it is given and is one.
let tryInt key parameters =
    match parameters |> Map.tryFind key with
    | Some(s: string) ->
        match Int32.TryParse s with
        | true, value -> Some value
        | _ -> None
    | None -> None


/// The fixed vocabulary of "#/session?refused={reason}"; an unknown reason is invalid.
let parseRefusal reason =
    match reason with
    | "expired" -> LaunchRefusal.LaunchExpired
    | "spent" -> LaunchRefusal.LaunchSpent
    | "invalid" -> LaunchRefusal.LaunchInvalid
    | "no-identity" -> LaunchRefusal.NoBrowserIdentity
    | "no-role" -> LaunchRefusal.NoRole
    | "wrong-patient" -> LaunchRefusal.WrongActivePatient
    | "enrolment" -> LaunchRefusal.EnrolmentRequired
    | _ -> LaunchRefusal.LaunchInvalid


/// The launch token of a "#/session?launch={token}" url, opaque to the client, or the
/// refusal of a "#/session?refused={reason}" url. Both are erased the same way.
let parseLaunch segments =
    match segments with
    | [ "session"; segment ] ->
        let parameters = segment |> query |> Map.ofList

        match parameters |> Map.tryFind "launch", parameters |> Map.tryFind "refused" with
        | Some token, _ -> Some(LaunchUrl.Launch(Launch token))
        | None, Some reason -> Some(LaunchUrl.Refused(parseRefusal reason))
        | None, None -> None
    | _ -> None


/// The age a "#/patient?..." url gives, from a birth year or else an age in days; now is the
/// moment the age of a birth date is taken at.
let parseAge now parameters =
    match parameters |> tryInt "by", parameters |> tryInt "ad" with
    | Some year, _ ->
        // january and the first day of the month when not given
        let month = parameters |> tryInt "bm" |> Option.defaultValue 1
        let day = parameters |> tryInt "bd" |> Option.defaultValue 1

        Patient.Age.fromBirthDate now (DateTime(year, month, day)) |> Some
    | _, Some days -> Patient.Age.fromDays days |> Some
    | _ -> None


/// The anonymous patient of a "#/patient?..." url of this age.
let parsePatient (age: Patient.Age) parameters =
    let cvl =
        match parameters |> Map.tryFind "cv" with
        | Some "y" -> [ CVL ]
        | _ -> []

    Patient.create
        (Some age.Years)
        (Some age.Months)
        (Some age.Weeks)
        (Some age.Days)
        (parameters |> tryInt "wt")
        (parameters |> tryInt "ht")
        (parameters |> tryInt "gw" |> Option.map Measures.toWeek)
        (parameters |> tryInt "gd" |> Option.map Measures.toDay)
        UnknownGender
        cvl
        None
        (parameters |> Map.tryFind "dp")


/// The page of a "pg" code.
let parsePage code =
    match code with
    | "el" -> Some Page.Page.LifeSupport
    | "cm" -> Some Page.Page.ContinuousMeds
    | "pr" -> Some Page.Page.Prescribe
    | "fm" -> Some Page.Page.Formulary
    | "pe" -> Some Page.Page.Parenteralia
    | _ -> None


/// What the url of these segments carries; now is the moment the age of a birth date is taken
/// at. A "#/session..." url carries none of the patient's parts: the session supplies the
/// patient. A patient url, as in http://localhost:8080/#/patient?by=2&bm=0&bd=1, takes:
///
/// - pg: the page, el (emergency list), cm (continuous medication), pr (prescribe),
///   fm (formulary) or pe (parenteralia)
/// - ad: age in days
/// - by: birth year
/// - bm: birth month
/// - bd: birth day
/// - wt: weight (gram)
/// - ht: height (cm)
/// - gw: gestational age weeks
/// - gd: gestational age days
/// - la: language (en; du; fr; ge; sp; it; ch)
/// - dc: show disclaimer (n;_)
/// - cv: central venous line (y;_)
/// - dp: department
/// - md: medication
/// - rt: route
/// - fr: form
/// - in: indication
/// - dt: dosetype
let parse now segments =
    match segments with
    | [] -> none
    | "session" :: _ -> { none with Launch = parseLaunch segments }
    | [ "patient"; segment ] ->
        let parameters = segment |> query |> Map.ofList

        let age = parameters |> parseAge now

        let medication =
            {|
                indication = parameters |> Map.tryFind "in"
                medication = parameters |> Map.tryFind "md"
                route = parameters |> Map.tryFind "rt"
                form = parameters |> Map.tryFind "fr"
                dosetype = parameters |> Map.tryFind "dt" |> Option.map DoseType.doseTypeFromString
            |}

        // no medication when the url gives no part of it
        let given =
            medication.indication.IsSome
            || medication.medication.IsSome
            || medication.route.IsSome
            || medication.form.IsSome
            || medication.dosetype.IsSome

        {
            Patient = age |> Option.bind (fun age -> parameters |> parsePatient age)
            Page = parameters |> Map.tryFind "pg" |> Option.bind parsePage
            // ISO code, display name or the legacy codes (du, gr, sp): one parser with the server
            Language = parameters |> Map.tryFind "la" |> Option.bind Localization.tryParse
            Disclaimer = parameters |> Map.tryFind "dc" <> Some "n"
            Medication = if given then Some medication else None
            Launch = None
            NotParsed =
                match age with
                | Some _ -> []
                | None -> [ UrlPart.Patient(parameters |> Map.toList |> List.map fst) ]
        }
    | segment :: _ -> { none with NotParsed = [ UrlPart.Route segment ] }
