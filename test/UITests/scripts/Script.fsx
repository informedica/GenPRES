#load "./../../../.paket/load/net472/UITests/uitests.group.fsx"

open System.IO
open canopy.runner.classic
open canopy.configuration
open canopy.classic
open canopy.types

let filepath = __SOURCE_DIRECTORY__
let driverpath =
    @"./../../../packages/uitests/Selenium.WebDriver.ChromeDriver/driver/win32"
let chromepath = Path.Combine(filepath, driverpath)

type PageIds =
    | Parent of string * PageIds list
    | Child of string

module Literals =
    // starting div for the elmish app
    [<Literal>]
    let appId = "#elmish-app"

    [<Literal>]
    let homePageId = "#homepage"

    [<Literal>]
    let appBarId = "#appbar"

    [<Literal>]
    let bottomBarId = "#bottombar"

    [<Literal>]
    let bodyId = "#body"

    [<Literal>]
    let title = "GenPRES"

let checkPageDivs ids =
    let rec check previd ids =
        match ids with
        | [] -> ()
        | p :: cs ->
            match p with
            | Child id ->
                if previd = "" then
                    sprintf "checking single: %s" id |> describe
                    displayed id
                else
                    sprintf "checking child: %s of parent: %s" id previd
                    |> describe
                    (element previd |> elementWithin id) |> displayed
            | Parent(p, cs_) ->
                if previd = "" then
                    sprintf "checking main: %s" p |> describe
                    displayed p
                else
                    sprintf "checking child: %s of parent: %s" p previd
                    |> describe
                    (element previd |> elementWithin p) |> displayed
                check p cs_
            check previd cs
    check "" ids

chromeDir <- chromepath
suites <- [ suite() ]
start chrome
"check if the app is correctly loaded" &&& fun _ ->
    // go to the url
    url "http://localhost:8080/"
    describe "checking loaded page ids for the home page"
    [ Parent(Literals.appId,
             [ Parent(Literals.homePageId,
                      [ Child Literals.appBarId
                        Child Literals.bodyId
                        Child Literals.bottomBarId ]) ]) ]
    |> checkPageDivs
    describe "check if the page title is GenPRES"
    "GenPRES" === title()
    describe "check if the appbar title is GenPRES"
    let title = (element "#appbar" |> elementWithin "#title")
    "GenPRES" === (read title)
run()
quit()
