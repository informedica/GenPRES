namespace Informedica.GenPRES.Client.Core.Helpers


/// Parses a url from its router segments. A "#/patient?..." url gives an anonymous patient, a page, a
/// language, the disclaimer setting and a medication; a "#/session?..." url gives a launch or its refusal.
module Url =

    open Informedica.GenPRES.Client.Core.Models

    open System
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// The content of a "#/session?..." url: the launch with which MainEHR opened GenPRES, or the reason
    /// the IdentityProvider refused the launch.
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


    /// A part of the url that did not parse. It contains no values, because the values are patient data.
    [<RequireQualifiedAccess>]
    type UrlPart =
        /// A "#/patient?..." url without a valid age, with the names of the parameters it gave.
        | Patient of parameters: string list
        /// A url that is neither a patient nor a session url, with its first segment.
        | Route of segment: string


    /// The parsed content of a url.
    type UrlParts =
        {
            /// The anonymous patient, when the url gives a valid age.
            Patient: PatientData option
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


    /// Returns the integer value of a parameter, or None when it is missing or not an integer.
    let tryInt key parameters =
        match parameters |> Map.tryFind key with
        | Some(s: string) ->
            match Int32.TryParse s with
            | true, value -> Some value
            | _ -> None
        | None -> None


    /// Parses the reason of a "#/session?refused={reason}" url. An unknown reason gives LaunchInvalid.
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


    /// Parses the launch token of a "#/session?launch={token}" url, which the client does not read, or the
    /// refusal of a "#/session?refused={reason}" url. Returns None for any other url.
    let parseLaunch segments =
        match segments with
        | [ "session"; segment ] ->
            let parameters = segment |> query |> Map.ofList

            match parameters |> Map.tryFind "launch", parameters |> Map.tryFind "refused" with
            | Some token, _ -> Some(LaunchUrl.Launch(Launch token))
            | None, Some reason -> Some(LaunchUrl.Refused(parseRefusal reason))
            | None, None -> None
        | _ -> None


    /// Parses the age parts yrs, mos, wks and dys of a "#/patient?..." url; a missing part counts as 0.
    /// Returns None when no part is given, or when a given part is not a whole number of 0 or more.
    let parseAge parameters =
        let part key =
            match parameters |> Map.tryFind key with
            | Some _ -> parameters |> tryInt key |> Option.filter (fun n -> n >= 0) |> Some
            | None -> None

        match part "yrs", part "mos", part "wks", part "dys" with
        | None, None, None, None -> None
        | yrs, mos, wks, dys when [ yrs; mos; wks; dys ] |> List.contains (Some None) -> None
        | yrs, mos, wks, dys ->
            let value p = p |> Option.flatten |> Option.defaultValue 0

            PatientData.Age.create
                (value yrs * 1<year>)
                (Some(value mos * 1<month>))
                (Some(value wks * 1<week>))
                (Some(value dys * 1<day>))
            |> Some


    /// Creates the anonymous patient of a "#/patient?..." url with the given age.
    let parsePatient (age: PatientData.Age) parameters =
        let cvl =
            match parameters |> Map.tryFind "cvl" with
            | Some "y" -> [ CVL ]
            | _ -> []

        PatientData.create
            (Some age.Years)
            (Some age.Months)
            (Some age.Weeks)
            (Some age.Days)
            (parameters |> tryInt "wgt")
            (parameters |> tryInt "hgt")
            (parameters |> tryInt "gaw" |> Option.map Measures.toWeek)
            (parameters |> tryInt "gad" |> Option.map Measures.toDay)
            UnknownGender
            cvl
            None
            (parameters |> Map.tryFind "dep")


    /// Returns the page for a "pag" code. Settings, the admin page, has no code, so that no link opens it.
    let parsePage code =
        match code with
        | "el" -> Some Page.Page.LifeSupport
        | "cm" -> Some Page.Page.ContinuousMeds
        | "pr" -> Some Page.Page.Prescribe
        | "fm" -> Some Page.Page.Formulary
        | "pe" -> Some Page.Page.Parenteralia
        | "nu" -> Some Page.Page.Nutrition
        | "op" -> Some Page.Page.OrderPlan
        | "ia" -> Some Page.Page.Interactions
        | _ -> None


    /// Parses the url segments. A "#/session..." url gives only the launch, because the session
    /// supplies the patient. A patient url, such as
    /// http://localhost:5173/#/patient?yrs=6&mos=3&wgt=20000, takes:
    ///
    /// - pag: the page, el (emergency list), cm (continuous medication), pr (prescribe),
    ///   fm (formulary), pe (parenteralia), nu (nutrition), op (order plan) or ia (interactions)
    /// - yrs: age years
    /// - mos: age months
    /// - wks: age weeks
    /// - dys: age days
    /// - wgt: weight (gram)
    /// - hgt: height (cm)
    /// - gaw: gestational age weeks
    /// - gad: gestational age days
    /// - lan: language, an ISO 639-1 code (en; nl; fr; de; es; it)
    /// - dsc: show disclaimer (n;_)
    /// - cvl: central venous line (y;_)
    /// - dep: department
    /// - med: medication
    /// - rte: route
    /// - frm: form
    /// - ind: indication
    /// - dst: dosetype
    let parse segments =
        match segments with
        | [] -> none
        | "session" :: _ -> { none with Launch = parseLaunch segments }
        | [ "patient"; segment ] ->
            let parameters = segment |> query |> Map.ofList

            let age = parameters |> parseAge

            let medication =
                {|
                    indication = parameters |> Map.tryFind "ind"
                    medication = parameters |> Map.tryFind "med"
                    route = parameters |> Map.tryFind "rte"
                    form = parameters |> Map.tryFind "frm"
                    dosetype = parameters |> Map.tryFind "dst" |> Option.map DoseTypeText.doseTypeFromString
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
                Page = parameters |> Map.tryFind "pag" |> Option.bind parsePage
                // Only an ISO 639-1 code, parsed like the default language of the server.
                Language = parameters |> Map.tryFind "lan" |> Option.bind Localization.tryParse
                Disclaimer = parameters |> Map.tryFind "dsc" <> Some "n"
                Medication = if given then Some medication else None
                Launch = None
                NotParsed =
                    match age with
                    | Some _ -> []
                    | None -> [ UrlPart.Patient(parameters |> Map.toList |> List.map fst) ]
            }
        | segment :: _ -> { none with NotParsed = [ UrlPart.Route segment ] }
