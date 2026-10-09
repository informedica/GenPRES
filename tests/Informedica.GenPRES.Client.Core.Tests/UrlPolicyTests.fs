module Informedica.GenPRES.Client.Core.Tests.UrlPolicyTests

open Expecto
open Expecto.Flip
open Busy
open UrlPolicy


let page = [ "patient"; "?pag=pr" ]
let shown = UrlState.Shown page
let seed = [ "patient"; "?agd=100&med=paracetamol" ]


[<Tests>]
let tests =
    testList
        "UrlPolicy"
        [
            test "the question opens over the url shown and closes back to it" {
                let asked = UrlState.Shown page |> UrlState.ask seed

                asked |> Expect.equal "asked" (UrlState.Asked(page, seed))
                asked |> UrlState.shown |> Expect.equal "the url shown stays" page
                asked |> UrlState.asked |> Expect.equal "the url asked" (Some seed)
                asked |> UrlState.close |> Expect.equal "closed" (UrlState.Shown page)

                asked
                |> UrlState.ask [ "patient"; "?agd=10" ]
                |> Expect.equal "a newer url replaces it" (UrlState.Asked(page, [ "patient"; "?agd=10" ]))

                UrlState.Shown page |> UrlState.asked |> Expect.isNone "no question"
            }

            test "the url shown is unchanged, also while asked; the rest by what it carries" {
                change shown page false |> Expect.equal "unchanged" UrlChange.Unchanged
                change (UrlState.Asked(page, seed)) page false
                |> Expect.equal "the question's url shown" UrlChange.Unchanged

                change shown seed true |> Expect.equal "seed" UrlChange.Seed
                change shown [ "patient"; "?pag=fm" ] false
                |> Expect.equal "page only" UrlChange.PageOnly
            }

            test "an unchanged url does nothing, whatever is out" {
                action UrlChange.Unchanged true [ Request.Plan ] true false
                |> Expect.equal "ignored" UrlAction.Ignore
            }

            test "a signature under way puts any change back, without a question" {
                action UrlChange.Seed true [] true false
                |> Expect.equal "seed" UrlAction.PutBack
                action UrlChange.PageOnly true [] false false
                |> Expect.equal "page" UrlAction.PutBack
            }

            test "a page alone is put back while anything is out, and applied with nothing out" {
                action UrlChange.PageOnly false [ Request.Workbench ] false false
                |> Expect.equal "put back" UrlAction.PutBack

                action UrlChange.PageOnly false [] true false
                |> Expect.equal "applied" UrlAction.ApplyPage

                action UrlChange.PageOnly false [ Request.Load Load.DrugNames ] false false
                |> Expect.equal "the drug names hold nothing" UrlAction.ApplyPage
            }

            test "a patient or a medication always asks first over a launched Session" {
                action UrlChange.Seed false [] false true |> Expect.equal "asked" UrlAction.Ask

                action UrlChange.PageOnly false [] false true
                |> Expect.equal "a page alone leaves no Session" UrlAction.ApplyPage
            }

            test "a patient or a medication asks first with work not signed, and starts over otherwise" {
                action UrlChange.Seed false [] true false |> Expect.equal "asked" UrlAction.Ask

                action UrlChange.Seed false [ Request.Workbench; Request.Plan ] false false
                |> Expect.equal "started over, with requests out" UrlAction.StartOver
            }
        ]
