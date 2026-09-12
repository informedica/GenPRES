// The admin command family (plan 654, step 2): the settings page's four operations as one
// `IServerApi` member, `processAdmin`, token-authenticated and never Session-bound.
//
// Script-first draft of what goes to `Shared/Api.fs`: `AdminCommand`, `AdminResponse`,
// `AdminCommand.toString` and the `IServerApi` member. A script cannot add cases to
// `Shared.Api` or a field to `IServerApi`, so the types are proposed here under `Api654` and
// the member is stated as a signature. `ReloadResources` carries the token the login issued,
// not the password: the password travels once, at `ValidatePassword`.
//
// Run: `dotnet fsi Api.fsx` from this directory, or via the FSI MCP after
// `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"
#load "../Api.fs"

open Shared.Types


/// → `Shared/Api.fs`, after `SigningCommand`.
module Api654 =

    /// The admin command family: the password once, then the token it bought. Never
    /// cookie-authenticated, never behind the formulary being loaded, so a failed load can be
    /// retried from the settings page.
    [<RequireQualifiedAccess>]
    type AdminCommand =
        // answered with a token when the password is the server's; a token nobody can forge
        // when the server has no password
        | ValidatePassword of password: string
        | ListLogFiles of token: string
        | AnalyzeLogFile of token: string * fileName: string
        // reloads the formulary and everything else the resource provider holds
        | ReloadResources of token: string


    [<RequireQualifiedAccess>]
    type AdminResponse =
        // isValid false comes with an empty token
        | PasswordValidated of isValid: bool * token: string
        | LogFilesListed of LogFileInfo[]
        | LogFileAnalyzed of string
        | ResourcesReloaded


    module AdminCommand =

        /// For the log. Never the password or the token; the file name is not a secret.
        let toString cmd =
            match cmd with
            | AdminCommand.ValidatePassword _ -> "ValidatePassword"
            | AdminCommand.ListLogFiles _ -> "ListLogFiles"
            | AdminCommand.AnalyzeLogFile(_, f) -> $"AnalyzeLogFile %s{f}"
            | AdminCommand.ReloadResources _ -> "ReloadResources"


    // `IServerApi` gains, next to `processSigning`:
    //
    //     processAdmin: AdminCommand -> Async<Result<AdminResponse, string[]>>
    //
    // No `Request`/`Reply` envelope: an admin request has no OpenedToken to send and no
    // record notice to receive.


open Expecto
open Expecto.Flip
open Api654


let tests =
    testList
        "AdminCommand.toString"
        [
            test "names the command, never the password" {
                AdminCommand.ValidatePassword "hunter2-hunter2-1"
                |> AdminCommand.toString
                |> Expect.equal "name only" "ValidatePassword"
            }

            test "names the command, never the token" {
                let token = "eyJwYXlsb2FkIn0=.c2lnbmF0dXJl"

                AdminCommand.ListLogFiles token
                |> AdminCommand.toString
                |> Expect.equal "name only" "ListLogFiles"

                AdminCommand.ReloadResources token
                |> AdminCommand.toString
                |> Expect.equal "name only" "ReloadResources"

                let logged = AdminCommand.AnalyzeLogFile(token, "server.log") |> AdminCommand.toString
                logged |> Expect.equal "name and file" "AnalyzeLogFile server.log"
                logged.Contains token |> Expect.isFalse "no token"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
