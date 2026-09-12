namespace ServerApi


module AdminCommand =

    open System
    open System.Security.Cryptography
    open System.Text
    open Shared.Api


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
        | Some expected ->
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes password, Encoding.UTF8.GetBytes expected)


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


    /// The admin commands over the admin port: the password buys a token, the token opens the
    /// log and the reload. Not behind `requireLoaded`: the reload is what makes a failed load
    /// loadable again.
    let processCmd (env: AppEnv) (cmd: AdminCommand) : Async<Result<AdminResponse, string[]>> =
        let admin = env.admin

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
