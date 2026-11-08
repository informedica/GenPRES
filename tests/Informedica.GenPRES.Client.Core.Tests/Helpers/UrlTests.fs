module Informedica.GenPRES.Client.Core.Tests.Helpers.UrlTests

open Informedica.GenPRES.Client.Core.Helpers
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Url


let parse = Url.parse

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

            test "the age parts give the patient's age as given" {
                patient "?yrs=6&mos=3&wks=2&dys=4"
                |> age
                |> Expect.equal "6 years, 3 months, 2 weeks, 4 days" (Some(6, 3, 2, 4))

                patient "?yrs=6&mos=3&wks=2&dys=4" |> _.NotParsed |> Expect.isEmpty "parsed"
            }

            test "an age part not given counts as zero" {
                patient "?yrs=6" |> age |> Expect.equal "6 years" (Some(6, 0, 0, 0))
                patient "?wks=3" |> age |> Expect.equal "3 weeks" (Some(0, 0, 3, 0))
                patient "?dys=0" |> age |> Expect.equal "a newborn" (Some(0, 0, 0, 0))
            }

            test "the age parts are not counted over: 14 months stays 14 months" {
                patient "?mos=14&dys=40" |> age |> Expect.equal "as given" (Some(0, 14, 0, 40))
            }

            test "the birth date and the age in days give no age" {
                [ "?byr=2020&bmo=4&bdy=15"; "?byr=2025"; "?agd=400" ]
                |> List.map (patient >> age)
                |> Expect.equal "no age" [ None; None; None ]
            }

            test "an age part that is no whole number of zero or more gives no age" {
                [ "?yrs=x"; "?yrs=6&mos=-1"; "?dys=1.5"; "?yrs=" ]
                |> List.map (patient >> age)
                |> Expect.equal "no age" [ None; None; None; None ]
            }

            test "weight, height, gestational age, department and central line are read" {
                let p =
                    (patient "?wks=1&dys=3&wgt=3500&hgt=50&gaw=30&gad=3&dep=NEO&cvl=y").Patient
                    |> Option.get

                p.Weight.Measured |> Option.map int |> Expect.equal "weight in gram" (Some 3500)
                p.Height.Measured |> Option.map int |> Expect.equal "height in cm" (Some 50)

                // held as optional until the server answers with the need
                p.GestationalAge
                |> Expect.equal
                    "30 weeks 3 days"
                    (GestationalAge.OptionalGestationalAge(
                        Some
                            {
                                Weeks = 30<week>
                                Days = 3<day>
                            }
                    ))

                p.Department |> Expect.equal "department" (Some "NEO")
                p.Access |> Expect.equal "central line" [ CVL ]
                p.Gender |> Expect.equal "gender unknown" UnknownGender
            }

            test "the central line only for y; weeks without days give 0 days" {
                let p = (patient "?wks=1&dys=3&cvl=n&gaw=30").Patient |> Option.get

                p.Access |> Expect.isEmpty "no central line"

                p.GestationalAge
                |> Expect.equal
                    "30 weeks 0 days"
                    (GestationalAge.OptionalGestationalAge(
                        Some
                            {
                                Weeks = 30<week>
                                Days = 0<day>
                            }
                    ))
            }

            test "a patient url without an age part names its parameters, not their values" {
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
                        test $"pag=%s{code}" { (patient $"?wks=1&dys=3&pag=%s{code}").Page |> Expect.equal code page }
                ]

            test "the language codes" {
                (patient "?wks=1&dys=3&lan=en").Language
                |> Expect.equal "en" (Some Localization.English)

                (patient "?wks=1&dys=3&lan=nl").Language
                |> Expect.equal "nl" (Some Localization.Dutch)

                (patient "?wks=1&dys=3&lan=du").Language |> Expect.isNone "the earlier du"

                (patient "?wks=1&dys=3&lan=xx").Language |> Expect.isNone "unknown"
                (patient "?wks=1&dys=3").Language |> Expect.isNone "not given"
            }

            test "the disclaimer is shown unless dsc=n" {
                (patient "?wks=1&dys=3").Disclaimer |> Expect.isTrue "not given"
                (patient "?wks=1&dys=3&dsc=n").Disclaimer |> Expect.isFalse "n"
                (patient "?wks=1&dys=3&dsc=y").Disclaimer |> Expect.isTrue "y"
            }

            test "a medication of one part, and none when no part is given" {
                let med = (patient "?wks=1&dys=3&med=paracetamol").Medication |> Option.get

                med.medication |> Expect.equal "the medication" (Some "paracetamol")
                med.indication |> Expect.isNone "no indication"
                med.route |> Expect.isNone "no route"
                med.form |> Expect.isNone "no form"
                med.dosetype |> Expect.isNone "no dose type"

                (patient "?wks=1&dys=3").Medication |> Expect.isNone "no part given"
            }

            test "every part of a medication" {
                let med =
                    (patient "?wks=1&dys=3&ind=pijn&med=paracetamol&rte=oraal&frm=tablet&dst=timed").Medication
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
                parse [ "other"; "?wks=1&dys=3" ]
                |> Expect.equal "the route" { Url.none with NotParsed = [ UrlPart.Route "other" ] }

                parse [ "patient" ]
                |> Expect.equal
                    "a patient url without a query"
                    { Url.none with NotParsed = [ UrlPart.Route "patient" ] }
            }
        ]
