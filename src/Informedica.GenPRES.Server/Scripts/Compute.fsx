// The admin command family (plan 654, step 2): ValidatePassword, ListLogFiles, AnalyzeLogFile
// and ReloadResources as one `IServerApi` member, `processAdmin`, under the HMAC token that the
// password buys. Never Session-bound (no cookie, no OpenedToken, no record notice) and never
// behind the formulary being loaded, so a failed initial load can be retried from the
// settings page. `ReloadResources` no longer carries the password: it carries the token, like
// the log commands.
//
// Script-first draft (script-only policy) of:
//   - `AdminCommand`, `AdminResponse`, `AdminCommand.toString`, `processAdmin` → `Shared/Api.fs`
//     (drafted in `Shared/Scripts/Api.fsx`; restated here under `Api654` because a script
//     cannot extend `Shared.Api`);
//   - `AdminPort`, replacing `LogAnalyzerPort` as `AppEnv.admin` → `Ports.fs`;
//   - the port's adapter: the secret read from GENPRES_PASSWORD, the clock, the reload through
//     `Informedica.GenForm.Lib.Api.reloadCache` → `Adapters.fs`;
//   - `AdminCommand.processCmd` with the password and token functions moved out of
//     `Command.fs`, over explicit values instead of the environment → new
//     `ServerApi.AdminCommand.fs`, compiled before `Command.fs` so the old `LogAnalyzerCmd`
//     arms share them until they go;
//   - `processAdmin` in `compose` → `CompositionRoot.fs`.
//
// The script takes the port where the source takes `AppEnv` (a record cannot gain a field in
// a script). Run: `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types


// ---------------------------------------------------------------------------------------------
// The wire (→ Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

module Api654 =

    /// The admin command family: the password once, then the token it bought.
    [<RequireQualifiedAccess>]
    type AdminCommand =
        | ValidatePassword of password: string
        | ListLogFiles of token: string
        | AnalyzeLogFile of token: string * fileName: string
        | ReloadResources of token: string


    [<RequireQualifiedAccess>]
    type AdminResponse =
        | PasswordValidated of isValid: bool * token: string
        | LogFilesListed of LogFileInfo[]
        | LogFileAnalyzed of string
        | ResourcesReloaded


    module AdminCommand =

        /// For the log. Never the password or the token.
        let toString cmd =
            match cmd with
            | AdminCommand.ValidatePassword _ -> "ValidatePassword"
            | AdminCommand.ListLogFiles _ -> "ListLogFiles"
            | AdminCommand.AnalyzeLogFile(_, f) -> $"AnalyzeLogFile %s{f}"
            | AdminCommand.ReloadResources _ -> "ReloadResources"


// ---------------------------------------------------------------------------------------------
// The port (→ Ports.fs, in place of LogAnalyzerPort; AppEnv.logAnalyzer becomes AppEnv.admin)
// ---------------------------------------------------------------------------------------------

/// What the admin commands need from the edge. The secret and the clock are values the DMZ
/// reads and passes in, so the command module never touches the environment.
type AdminPort =
    {
        // GENPRES_PASSWORD; None when unset, empty or whitespace, so every check fails closed
        secret: unit -> string option
        now: unit -> DateTimeOffset
        listLogFiles: unit -> Async<Result<LogFileInfo[], string[]>>
        analyzeLogFile: string -> Async<Result<string, string[]>>
        // the resource provider reloaded: the formulary and, later, the knowledge sheets
        reloadResources: unit -> Async<Result<unit, string[]>>
    }


// ---------------------------------------------------------------------------------------------
// The command (→ ServerApi.AdminCommand.fs)
// ---------------------------------------------------------------------------------------------

module AdminCommand =

    open System.Security.Cryptography
    open System.Text
    open Api654


    let tokenLifetime = TimeSpan.FromHours 1.0


    /// An empty or whitespace secret is no secret. `Env.getItem` answers `Some ""` for a
    /// setting that is set but empty (the Dockerfile's `ENV GENPRES_PASSWORD=`), and an empty
    /// password compared with an empty secret would match, so blanks fail closed here as well
    /// as at the port.
    let private nonBlank (secret: string option) =
        secret |> Option.filter (String.IsNullOrWhiteSpace >> not)


    /// SECURITY: `FixedTimeEquals` so equal-length comparisons do not leak through per-byte
    /// timing. It short-circuits on a length mismatch; that leak is accepted because production
    /// enforces a 16-character minimum and the password travels only at ValidatePassword.
    let validatePassword (secret: string option) (password: string) =
        match nonBlank secret with
        | None -> false
        | Some expected -> CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes password, Encoding.UTF8.GetBytes expected)


    /// `base64(expiresAt:nonce).base64(hmacsha256(secret, expiresAt:nonce))`; empty without a
    /// secret, so that nothing signed with an empty key can ever verify.
    let generateToken (secret: string option) (now: DateTimeOffset) =
        match nonBlank secret with
        | None -> ""
        | Some secret ->
            let expiresAt = now.Add(tokenLifetime).ToUnixTimeSeconds()
            let nonce = RandomNumberGenerator.GetBytes 32 |> Convert.ToBase64String
            let payload = Encoding.UTF8.GetBytes $"%d{expiresAt}:%s{nonce}"
            use hmac = new HMACSHA256(Encoding.UTF8.GetBytes secret)
            let signature = hmac.ComputeHash payload
            $"%s{Convert.ToBase64String payload}.%s{Convert.ToBase64String signature}"


    let validateToken (secret: string option) (now: DateTimeOffset) (token: string) =
        match nonBlank secret with
        | None -> false
        | Some secret ->
            if String.IsNullOrWhiteSpace token then
                false
            else
                match token.Split '.' with
                | [| payload; signature |] ->
                    try
                        let payload = Convert.FromBase64String payload
                        let provided = Convert.FromBase64String signature
                        use hmac = new HMACSHA256(Encoding.UTF8.GetBytes secret)
                        let expected = hmac.ComputeHash payload

                        CryptographicOperations.FixedTimeEquals(provided, expected)
                        && (match (Encoding.UTF8.GetString payload).Split(':', 2) with
                            | [| expiresAt; _ |] ->
                                match Int64.TryParse expiresAt with
                                | true, expiresAt -> now.ToUnixTimeSeconds() <= expiresAt
                                | false, _ -> false
                            | _ -> false)
                    with _ ->
                        false
                | _ -> false


    /// The source takes `env: AppEnv` and reads `env.admin`.
    let processCmd (admin: AdminPort) (cmd: AdminCommand) : Async<Result<AdminResponse, string[]>> =
        let withToken token (run: unit -> Async<Result<AdminResponse, string[]>>) =
            if validateToken (admin.secret ()) (admin.now ()) token then
                run ()
            else
                async { return Error [| "Invalid token" |] }

        match cmd with
        | AdminCommand.ValidatePassword password ->
            async {
                let secret = admin.secret ()

                return
                    if validatePassword secret password then
                        Ok(AdminResponse.PasswordValidated(true, generateToken secret (admin.now ())))
                    else
                        Ok(AdminResponse.PasswordValidated(false, ""))
            }
        | AdminCommand.ListLogFiles token ->
            withToken token (fun () ->
                async {
                    let! files = admin.listLogFiles ()
                    return files |> Result.map AdminResponse.LogFilesListed
                }
            )
        | AdminCommand.AnalyzeLogFile(token, fileName) ->
            withToken token (fun () ->
                async {
                    let! report = admin.analyzeLogFile fileName
                    return report |> Result.map AdminResponse.LogFileAnalyzed
                }
            )
        | AdminCommand.ReloadResources token ->
            withToken token (fun () ->
                async {
                    let! reloaded = admin.reloadResources ()
                    return reloaded |> Result.map (fun () -> AdminResponse.ResourcesReloaded)
                }
            )


// ---------------------------------------------------------------------------------------------
// The adapter (→ Adapters.fs, in makeAppEnvWith)
// ---------------------------------------------------------------------------------------------
//
//     admin =
//         {
//             secret =
//                 fun () ->
//                     Informedica.Utils.Lib.Env.getItem "GENPRES_PASSWORD"
//                     |> Option.filter (String.IsNullOrWhiteSpace >> not)
//             now = fun () -> DateTimeOffset.UtcNow
//             listLogFiles = (as today)
//             analyzeLogFile = (as today)
//             reloadResources =
//                 fun () ->
//                     async {
//                         try
//                             Informedica.GenForm.Lib.Api.reloadCache logger provider
//                             return Ok()
//                         with ex ->
//                             return Error [| ex.Message |]
//                     }
//         }
//
// and in `compose` (→ CompositionRoot.fs), with the same logging as the session members:
//
//     processAdmin =
//         fun cmd ->
//             async {
//                 writeInfoMessage $"Processing admin: {cmd |> AdminCommand.toString}"
//                 let! response = AdminCommand.processCmd env cmd
//                 writeInfoMessage $"Finished processing admin: {cmd |> AdminCommand.toString}"
//                 return response
//             }
//
// `processAdmin` does not consult `requireLoaded`: the reload is what makes a failed load
// loadable again.


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Api654


let secret = Some "a-sixteen-char-secret!"
let t0 = DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero)


/// A port over a fixed secret and clock that counts its reloads.
let port (secret: string option) (now: DateTimeOffset) =
    let reloads = ref 0

    reloads,
    {
        secret = fun () -> secret
        now = fun () -> now
        listLogFiles = fun () -> async { return Ok [| { FileName = "server.log"; SizeBytes = 12L; LastModifiedAt = "2026-09-12" } |] }
        analyzeLogFile = fun name -> async { return Ok $"report of %s{name}" }
        reloadResources =
            fun () ->
                async {
                    reloads.Value <- reloads.Value + 1
                    return Ok()
                }
    }


let run admin cmd =
    AdminCommand.processCmd admin cmd |> Async.RunSynchronously


let tokenOf admin =
    match run admin (AdminCommand.ValidatePassword (admin.secret ()).Value) with
    | Ok(AdminResponse.PasswordValidated(true, token)) -> token
    | other -> failtest $"expected a token, got {other}"


/// The same payload under a different signature.
let forge (token: string) =
    match token.Split '.' with
    | [| payload; signature |] ->
        let bytes = Convert.FromBase64String signature
        bytes[0] <- bytes[0] ^^^ 1uy
        $"%s{payload}.%s{Convert.ToBase64String bytes}"
    | _ -> failtest "not a token"


let passwordTests =
    testList
        "ValidatePassword"
        [
            test "the server's password buys a token that verifies" {
                let _, admin = port secret t0
                let token = tokenOf admin
                token |> Expect.isNotEmpty "a token"
                AdminCommand.validateToken secret t0 token |> Expect.isTrue "verifies"
            }

            test "a wrong password: not valid, no token" {
                let _, admin = port secret t0

                run admin (AdminCommand.ValidatePassword "wrong")
                |> Expect.equal "refused" (Ok(AdminResponse.PasswordValidated(false, "")))
            }

            test "no password on the server: nothing is valid, not even an empty one" {
                for none in [ None; Some ""; Some "   " ] do
                    let _, admin = port none t0

                    run admin (AdminCommand.ValidatePassword "")
                    |> Expect.equal "refused" (Ok(AdminResponse.PasswordValidated(false, "")))

                    AdminCommand.generateToken none t0 |> Expect.equal "no token" ""
                    AdminCommand.validateToken none t0 "" |> Expect.isFalse "empty never verifies"
            }
        ]


let tokenTests =
    testList
        "the token"
        [
            test "a valid token lists, analyzes and reloads" {
                let reloads, admin = port secret t0
                let token = tokenOf admin

                match run admin (AdminCommand.ListLogFiles token) with
                | Ok(AdminResponse.LogFilesListed files) -> files.Length |> Expect.equal "one file" 1
                | other -> failtest $"expected the files, got {other}"

                run admin (AdminCommand.AnalyzeLogFile(token, "server.log"))
                |> Expect.equal "the report" (Ok(AdminResponse.LogFileAnalyzed "report of server.log"))

                run admin (AdminCommand.ReloadResources token)
                |> Expect.equal "reloaded" (Ok AdminResponse.ResourcesReloaded)

                reloads.Value |> Expect.equal "the port reloaded once" 1
            }

            test "a forged, an expired, a malformed and an empty token are refused, and nothing reloads" {
                let reloads, admin = port secret t0
                let token = tokenOf admin
                let later = t0.Add(AdminCommand.tokenLifetime).AddSeconds 1.0
                let _, expiredAdmin = port secret later

                for admin, token in
                    [
                        admin, forge token
                        expiredAdmin, token
                        admin, "not.a.token"
                        admin, "abc"
                        admin, ""
                    ] do
                    run admin (AdminCommand.ReloadResources token) |> Expect.equal "refused" (Error [| "Invalid token" |])
                    run admin (AdminCommand.ListLogFiles token) |> Expect.equal "refused" (Error [| "Invalid token" |])

                    run admin (AdminCommand.AnalyzeLogFile(token, "server.log"))
                    |> Expect.equal "refused" (Error [| "Invalid token" |])

                reloads.Value |> Expect.equal "nothing reloaded" 0
            }

            test "a token lives an hour" {
                let _, admin = port secret t0
                let token = tokenOf admin
                AdminCommand.validateToken secret (t0.Add AdminCommand.tokenLifetime) token |> Expect.isTrue "at the hour"

                AdminCommand.validateToken secret (t0.Add(AdminCommand.tokenLifetime).AddSeconds 1.0) token
                |> Expect.isFalse "past it"
            }

            test "a token of another secret is refused" {
                let _, other = port (Some "another-secret-of-16!") t0
                let _, admin = port secret t0
                run admin (AdminCommand.ReloadResources(tokenOf other)) |> Expect.equal "refused" (Error [| "Invalid token" |])
            }

            test "a failing reload answers the port's error" {
                let _, admin = port secret t0
                let token = tokenOf admin

                let failing =
                    { admin with
                        reloadResources = fun () -> async { return Error [| "sheet unreachable" |] }
                    }

                run failing (AdminCommand.ReloadResources token) |> Expect.equal "the error" (Error [| "sheet unreachable" |])
            }
        ]


let logTests =
    testList
        "AdminCommand.toString"
        [
            test "never the password or the token" {
                let _, admin = port secret t0
                let token = tokenOf admin

                [
                    AdminCommand.ValidatePassword secret.Value
                    AdminCommand.ListLogFiles token
                    AdminCommand.AnalyzeLogFile(token, "server.log")
                    AdminCommand.ReloadResources token
                ]
                |> List.map AdminCommand.toString
                |> List.iter (fun line ->
                    line.Contains secret.Value |> Expect.isFalse "no password"
                    line.Contains token |> Expect.isFalse "no token"
                )
            }
        ]


runTestsWithCLIArgs [] [||] (testList "Admin" [ passwordTests; tokenTests; logTests ]) |> ignore
