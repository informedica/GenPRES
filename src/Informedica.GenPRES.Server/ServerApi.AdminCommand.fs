namespace ServerApi


module AdminCommand =

    open System
    open System.Security.Cryptography
    open System.Text
    open Informedica.GenPRES.Shared.Api
    open Informedica.Utils.Lib.BCL


    let tokenLifetime = TimeSpan.FromHours 1.0


    /// Treats a blank secret as no secret, so an empty password never matches an empty setting.
    let private nonBlank (secret: string option) = secret |> Option.filter String.notEmpty


    /// Compares the password with the secret in constant time. False without a secret.
    /// SECURITY: a length mismatch returns early; that leak is accepted because production
    /// requires at least 16 characters.
    let validatePassword (secret: string option) (password: string) =
        match nonBlank secret with
        | None -> false
        | Some expected ->
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes password, Encoding.UTF8.GetBytes expected)


    /// Returns the signing key for the secret and the server mode, so a demo token never verifies
    /// in production.
    let tokenKey (demo: bool) (secret: string) =
        let mode = if demo then "demo" else "production"
        Encoding.UTF8.GetBytes $"genpres-admin-token:%s{mode}:%s{secret}"


    /// Returns a signed token that expires, in the form base64(expiresAt:nonce).base64(hmac).
    /// Returns an empty string without a secret.
    let generateToken (demo: bool) (secret: string option) (now: DateTimeOffset) =
        match nonBlank secret with
        | None -> ""
        | Some secret ->
            let expiresAt = now.Add(tokenLifetime).ToUnixTimeSeconds()
            let nonce = RandomNumberGenerator.GetBytes 32 |> Convert.ToBase64String
            let payload = Encoding.UTF8.GetBytes $"%d{expiresAt}:%s{nonce}"
            use hmac = new HMACSHA256(tokenKey demo secret)
            let signature = hmac.ComputeHash payload
            $"%s{Convert.ToBase64String payload}.%s{Convert.ToBase64String signature}"


    /// Checks that a token was signed with the secret and the mode, and has not expired.
    let validateToken (demo: bool) (secret: string option) (now: DateTimeOffset) (token: string) =
        match nonBlank secret with
        | None -> false
        | Some secret ->
            if token |> String.isNullOrWhiteSpace then
                false
            else
                match token.Split '.' with
                | [| payload; signature |] ->
                    try
                        let payload = Convert.FromBase64String payload
                        let provided = Convert.FromBase64String signature
                        use hmac = new HMACSHA256(tokenKey demo secret)
                        let expected = hmac.ComputeHash payload

                        CryptographicOperations.FixedTimeEquals(provided, expected)
                        && (
                            match (Encoding.UTF8.GetString payload).Split(':', 2) with
                            | [| expiresAt; _ |] ->
                                match Int64.TryParse expiresAt with
                                | true, expiresAt -> now.ToUnixTimeSeconds() <= expiresAt
                                | false, _ -> false
                            | _ -> false
                        )
                    with _ ->
                        false
                | _ -> false


    /// Runs an admin command. A valid password returns a token; the other commands need the token.
    /// Works before the resources are loaded, so a reload can repair a failed load.
    let processCmd (env: AppEnv) (cmd: AdminCommand) : Async<Result<AdminResponse, string[]>> =
        let admin = env.admin

        let withToken token (run: unit -> Async<Result<AdminResponse, string[]>>) =
            if validateToken admin.demo (admin.secret ()) (admin.now ()) token then
                run ()
            else
                async { return Error [| "Invalid token" |] }

        match cmd with
        | AdminCommand.ValidatePassword password ->
            async {
                let secret = admin.secret ()

                return
                    if validatePassword secret password then
                        Ok(AdminResponse.PasswordValidated(true, generateToken admin.demo secret (admin.now ())))
                    else
                        Ok(AdminResponse.PasswordValidated(false, ""))
            }
        | AdminCommand.ListLogFiles token ->
            withToken
                token
                (fun () ->
                    async {
                        let! files = admin.listLogFiles ()
                        return files |> Result.map AdminResponse.LogFilesListed
                    }
                )
        | AdminCommand.AnalyzeLogFile(token, fileName) ->
            withToken
                token
                (fun () ->
                    async {
                        let! report = admin.analyzeLogFile fileName
                        return report |> Result.map AdminResponse.LogFileAnalyzed
                    }
                )
        | AdminCommand.ReloadResources token ->
            withToken
                token
                (fun () ->
                    async {
                        let! reloaded = admin.reloadResources ()
                        return reloaded |> Result.map (fun () -> AdminResponse.ResourcesReloaded)
                    }
                )
