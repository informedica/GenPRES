module Informedica.GenPRES.Client.Core.Tests.Helpers.UrlTests

open Informedica.GenPRES.Client.Core.Helpers
open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Url


let now = DateTime(2026, 10, 9)

let parse = Url.parse now

let patient query = parse [ "patient"; query ]

let age (url: UrlParts) =
    url.Patient
    |> Option.bind _.Age
    |> Option.map (fun a -> int a.Years, int a.Months, int a.Weeks, int a.Days)


[<Tests>]
let tests =
    testList
        "Url"
        [
            test "the query is split into decoded name and value pairs" {
                query "?a=1&b=x%20y&c=p+q&d&&e="
                |> Expect.equal "pairs" [ "a", "1"; "b", "x y"; "c", "p q"; "d", ""; "e", "" ]

                query "?md=a%3Db"
                |> Expect.equal "an escaped = stays in the value" [ "md", "a=b" ]
                query "?md=%E0" |> Expect.equal "a malformed escape is kept" [ "md", "%E0" ]
                query "?" |> Expect.isEmpty "nothing"
            }

            test "a url without segments carries nothing" { parse [] |> Expect.equal "none" Url.none }

            test "an age in days gives the patient's age" {
                patient "?agd=400"
                |> age
                |> Expect.equal "1 year, 1 month, 5 days" (Some(1, 1, 0, 5))
                patient "?agd=400" |> _.NotParsed |> Expect.isEmpty "parsed"
            }

            test "a birth date gives the age at now, january and the first day by default" {
                patient "?byr=2020&bmo=4&bdy=15"
                |> age
                |> Option.map (fun (y, m, _, _) -> y, m)
                |> Expect.equal "6 years, 5 months" (Some(6, 5))

                patient "?byr=2025"
                |> age
                |> Option.map (fun (y, m, _, _) -> y, m)
                |> Expect.equal "born on 1 january: 1 year, 9 months" (Some(1, 9))
            }

            test "the birth year goes before the age in days; a year that is no number does not" {
                patient "?byr=2025&agd=10"
                |> age
                |> Option.map (fun (y, _, _, _) -> y)
                |> Expect.equal "the birth year" (Some 1)

                patient "?byr=x&agd=10"
                |> age
                |> Expect.equal "the age in days" (Some(0, 0, 1, 3))
            }

            test "weight, height, gestational age, department and central line are read" {
                let p =
                    (patient "?agd=10&wgt=3500&hgt=50&gaw=30&gad=3&dep=NEO&cvl=y").Patient
                    |> Option.get

                p.Weight.Measured |> Option.map int |> Expect.equal "weight in gram" (Some 3500)
                p.Height.Measured |> Option.map int |> Expect.equal "height in cm" (Some 50)

                p.GestationalAge
                |> Option.map (fun ga -> int ga.Weeks, int ga.Days)
                |> Expect.equal "30 weeks 3 days" (Some(30, 3))

                p.Department |> Expect.equal "department" (Some "NEO")
                p.Access |> Expect.equal "central line" [ CVL ]
                p.Gender |> Expect.equal "gender unknown" UnknownGender
            }

            test "the central line only for y; weeks without days give 0 days" {
                let p = (patient "?agd=10&cvl=n&gaw=30").Patient |> Option.get

                p.Access |> Expect.isEmpty "no central line"

                p.GestationalAge
                |> Option.map (fun ga -> int ga.Weeks, int ga.Days)
                |> Expect.equal "30 weeks 0 days" (Some(30, 0))
            }

            test "a patient url without a birth year or an age names its parameters, not their values" {
                let url = patient "?pag=pr&wgt=3500"

                url.Patient |> Expect.isNone "no patient"
                url.Page |> Expect.equal "the page still read" (Some Page.Page.Prescribe)

                url.NotParsed |> Expect.equal "the names" [ UrlPart.Patient [ "pag"; "wgt" ] ]
            }

            testList
                "the page codes"
                [
                    for code, page in
                        [
                            "el", Some Page.Page.LifeSupport
                            "cm", Some Page.Page.ContinuousMeds
                            "pr", Some Page.Page.Prescribe
                            "fm", Some Page.Page.Formulary
                            "pe", Some Page.Page.Parenteralia
                            "nu", Some Page.Page.Nutrition
                            "op", Some Page.Page.OrderPlan
                            "ia", Some Page.Page.Interactions
                            "xx", None
                        ] do
                        test $"pag=%s{code}" { (patient $"?agd=10&pag=%s{code}").Page |> Expect.equal code page }
                ]

            test "the language codes" {
                (patient "?agd=10&lan=en").Language
                |> Expect.equal "en" (Some Localization.English)

                (patient "?agd=10&lan=nl").Language
                |> Expect.equal "nl" (Some Localization.Dutch)

                (patient "?agd=10&lan=du").Language |> Expect.isNone "the earlier du"

                (patient "?agd=10&lan=xx").Language |> Expect.isNone "unknown"
                (patient "?agd=10").Language |> Expect.isNone "not given"
            }

            test "the disclaimer is shown unless dsc=n" {
                (patient "?agd=10").Disclaimer |> Expect.isTrue "not given"
                (patient "?agd=10&dsc=n").Disclaimer |> Expect.isFalse "n"
                (patient "?agd=10&dsc=y").Disclaimer |> Expect.isTrue "y"
            }

            test "a medication of one part, and none when no part is given" {
                let med = (patient "?agd=10&med=paracetamol").Medication |> Option.get

                med.medication |> Expect.equal "the medication" (Some "paracetamol")
                med.indication |> Expect.isNone "no indication"
                med.route |> Expect.isNone "no route"
                med.form |> Expect.isNone "no form"
                med.dosetype |> Expect.isNone "no dose type"

                (patient "?agd=10").Medication |> Expect.isNone "no part given"
            }

            test "every part of a medication" {
                let med =
                    (patient "?agd=10&ind=pijn&med=paracetamol&rte=oraal&frm=tablet&dst=timed").Medication
                    |> Option.get

                med.indication |> Expect.equal "indication" (Some "pijn")
                med.route |> Expect.equal "route" (Some "oraal")
                med.form |> Expect.equal "form" (Some "tablet")
                med.dosetype |> Expect.equal "dose type" (Some(Timed ""))
            }

            test "the two-letter keys of the earlier scheme are not read" {
                let url = patient "?ad=10&wt=3500&pg=pr&la=en&dc=n&md=paracetamol"

                url.Patient |> Expect.isNone "no patient"
                url.Page |> Expect.isNone "no page"
                url.Language |> Expect.isNone "no language"
                url.Disclaimer |> Expect.isTrue "the disclaimer shown"
                url.Medication |> Expect.isNone "no medication"

                url.NotParsed
                |> Expect.equal "the names" [ UrlPart.Patient [ "ad"; "dc"; "la"; "md"; "pg"; "wt" ] ]
            }

            test "a launch url carries its token and nothing else" {
                let url = parse [ "session"; "?launch=abc" ]

                url.Launch |> Expect.equal "the launch" (Some(LaunchUrl.Launch(Launch "abc")))
                { url with Launch = None } |> Expect.equal "nothing else" Url.none
            }

            testList
                "each refusal reason, and an unknown one as invalid"
                [
                    for reason, refusal in
                        [
                            "expired", LaunchRefusal.LaunchExpired
                            "spent", LaunchRefusal.LaunchSpent
                            "invalid", LaunchRefusal.LaunchInvalid
                            "no-identity", LaunchRefusal.NoBrowserIdentity
                            "no-role", LaunchRefusal.NoRole
                            "wrong-patient", LaunchRefusal.WrongActivePatient
                            "enrolment", LaunchRefusal.EnrolmentRequired
                            "other", LaunchRefusal.LaunchInvalid
                        ] do
                        test $"refused=%s{reason}" {
                            (parse [ "session"; $"?refused=%s{reason}" ]).Launch
                            |> Expect.equal reason (Some(LaunchUrl.Refused refusal))
                        }
                ]

            test "a session url without a launch or a refusal carries nothing" {
                parse [ "session" ] |> Expect.equal "no query" Url.none
                parse [ "session"; "?x=1" ] |> Expect.equal "no launch" Url.none
            }

            test "a url that is neither names its first segment" {
                parse [ "other"; "?agd=10" ]
                |> Expect.equal "the route" { Url.none with NotParsed = [ UrlPart.Route "other" ] }

                parse [ "patient" ]
                |> Expect.equal
                    "a patient url without a query"
                    { Url.none with NotParsed = [ UrlPart.Route "patient" ] }
            }
        ]
