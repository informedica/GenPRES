namespace ServerApi

open System
open Shared.Types


module PublicKey =

    open System.Text
    open System.Text.Json
    open System.Security.Cryptography


    let private base64Url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')


    let private sha256 (s: string) =
        s |> Encoding.UTF8.GetBytes |> SHA256.HashData


    /// The required members of a JWK per key type, in the lexicographic order RFC 7638
    /// section 3 prescribes. Empty for a key type the thumbprint does not cover.
    let private requiredMembers kty =
        match kty with
        | "EC" -> [ "crv"; "kty"; "x"; "y" ]
        | "RSA" -> [ "e"; "kty"; "n" ]
        | "OKP" -> [ "crv"; "kty"; "x" ]
        | _ -> []


    /// The string members RFC 7638 hashes, or None when the text is not such a JWK.
    let private jwkMembers (text: string) =
        try
            use doc = JsonDocument.Parse text
            let root = doc.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                None
            else
                let get (name: string) =
                    match root.TryGetProperty name with
                    | true, v when v.ValueKind = JsonValueKind.String -> Some(name, v.GetString())
                    | _ -> None

                match get "kty" with
                | None -> None
                | Some(_, kty) ->
                    let members = requiredMembers kty |> List.map get

                    if members.IsEmpty || members |> List.exists Option.isNone then
                        None
                    else
                        members |> List.choose id |> Some
        with :? JsonException ->
            None


    /// RFC 7638 thumbprint of a public JWK: SHA-256 over the required members serialised
    /// without whitespace in lexicographic order, base64url without padding. Text that is
    /// not a JWK of a covered key type gets the hash of the text itself, so the stub can
    /// correlate any key the client sends; the client computes the same value only for a
    /// real JWK.
    let thumbprint (PublicKey text) =
        match jwkMembers text with
        | Some members ->
            members
            |> List.map (fun (name, value) -> $"\"{name}\":{JsonSerializer.Serialize value}")
            |> String.concat ","
            |> fun body -> "{" + body + "}"
            |> sha256
            |> base64Url
        | None -> text |> sha256 |> base64Url


    /// A random, unguessable id for a session cookie: 256 bits from the CSPRNG, base64url.
    let randomId () =
        RandomNumberGenerator.GetBytes 32 |> base64Url


module LaunchSeal =

    /// The key the Launch is sealed under. 32 bytes from a CSPRNG (`newKey`); shared with the
    /// LaunchScript in the real integration, made per host start for the stub.
    type Key = Key of byte[]


    /// What a Launch says once the seal is verified.
    type Claims =
        {
            PatientId: string
            Nonce: string
            Expiry: DateTime
        }


    /// The sealed payload on the wire. Field names are the contract; keep them short.
    type Payload =
        {
            pid: string
            nonce: string
            // unix seconds, UTC
            exp: int64
        }


    let keyLength = 32


    let newKey (random: int -> byte[]) = Key(random keyLength)


    let toBase64Url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')


    let fromBase64Url (s: string) =
        try
            let padded = s.Replace('-', '+').Replace('_', '/')

            let padded =
                match padded.Length % 4 with
                | 2 -> padded + "=="
                | 3 -> padded + "="
                | 0 -> padded
                | _ -> raise (FormatException "bad length")

            Some(Convert.FromBase64String padded)
        with _ ->
            None


    let mac (Key key) (data: byte[]) =
        System.Security.Cryptography.HMACSHA256.HashData(key, data)


    let private json = System.Text.Json.JsonSerializerOptions()


    /// Seals the claims: `base64url(json) + "." + base64url(HMAC-SHA256(key, json))`.
    let mint (key: Key) (claims: Claims) : Launch =
        let payload =
            {
                pid = claims.PatientId
                nonce = claims.Nonce
                exp = DateTimeOffset(claims.Expiry, TimeSpan.Zero).ToUnixTimeSeconds()
            }

        let bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, json)
        Launch $"{toBase64Url bytes}.{toBase64Url (mac key bytes)}"


    /// Verifies the seal (constant-time), then the lifetime (Rule 3). Anything that is not a
    /// Launch sealed under the key is `LaunchInvalid`; a Launch past its expiry is
    /// `LaunchExpired`.
    let verify (now: DateTime) (key: Key) (Launch text) : Result<Claims, LaunchRefusal> =
        let parts = if isNull text then [||] else text.Split('.')

        match parts with
        | [| payload; signature |] ->
            match fromBase64Url payload, fromBase64Url signature with
            | Some bytes, Some given when
                System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(mac key bytes, given)
                ->
                try
                    let p = System.Text.Json.JsonSerializer.Deserialize<Payload>(bytes, json)

                    if isNull p.pid || isNull p.nonce || p.pid = "" || p.nonce = "" then
                        Error LaunchRefusal.LaunchInvalid
                    else
                        let expiry = DateTimeOffset.FromUnixTimeSeconds(p.exp).UtcDateTime

                        if now > expiry then
                            Error LaunchRefusal.LaunchExpired
                        else
                            Ok
                                {
                                    PatientId = p.pid
                                    Nonce = p.nonce
                                    Expiry = expiry
                                }
                with _ ->
                    Error LaunchRefusal.LaunchInvalid
            | _ -> Error LaunchRefusal.LaunchInvalid
        | _ -> Error LaunchRefusal.LaunchInvalid


/// Launch steps 4 and 5 over server-hosted stubs (plan 605): the LaunchRecord keyed by the
/// nonce (4.2), the redirect to the IdentityProvider, the callback (4.5) with the step-5 ladder,
/// the Rule 45 replay and the Rule 40 single act with the Rule 8 closes. Pure over a state
/// record; `makeSessionPort` wraps it in a lock over the three actor ports.
module Hop =

    /// The refusal words of `#/session?refused=<word>`; the client's `parseRefusal` reads them.
    let refusalWord refusal =
        match refusal with
        | LaunchRefusal.LaunchExpired -> "expired"
        | LaunchRefusal.LaunchSpent -> "spent"
        | LaunchRefusal.LaunchInvalid -> "invalid"
        | LaunchRefusal.NoBrowserIdentity -> "no-identity"
        | LaunchRefusal.NoRole -> "no-role"
        | LaunchRefusal.WrongActivePatient -> "wrong-patient"
        | LaunchRefusal.EnrolmentRequired -> "enrolment"


    let openedUrl = "/#/session"

    let refusedUrl refusal =
        $"/#/session?refused={refusalWord refusal}"


    /// One record per Launch (4.2), keyed by the nonce (Rule 2) and found by the `state` at the
    /// callback. The outcome is appended once (Rule 45); the record is dropped whole at expiry.
    type LaunchRecord =
        {
            Nonce: string
            State: string
            PatientId: string
            PublicKey: PublicKey
            Expiry: DateTime
            Outcome: LaunchResult option
        }


    /// A Session as the store holds it: what the client learns, plus the login it belongs to
    /// (Rule 8: a User has at most one open Session).
    type SessionRecord =
        {
            Session: SessionOpened
            Login: string option
        }


    /// Why a Session ended other than by the User closing it (Rule 11). One case now.
    [<RequireQualifiedAccess>]
    type SessionEnding = SupersededByLaunch


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            // sessions ended by the server, told once at the next GetSession (Rule 11, PR 4)
            Endings: Map<string, SessionEnding>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
            Endings = Map.empty
        }


    let private dropExpired (now: DateTime) (state: State) =
        { state with Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry) }


    /// The answer `present` gives for a record: the recorded outcome, else the redirect again.
    let private answerOf (authorizeUrl: string -> string) (record: LaunchRecord) =
        match record.Outcome with
        | Some outcome -> outcome
        | None -> LaunchResult.RedirectTo(authorizeUrl record.State, record.State)


    /// Step 4.1 and 4.2. Pure over the state; the clock, the id source, the verifier and the
    /// IdentityProvider's url are parameters.
    ///
    /// - not sealed under the key, or past its expiry: refused, nothing recorded (Rules 3, 29);
    /// - a record under the nonce: the same public key gets the recorded answer, or the redirect
    ///   again while the hop is still open (Rule 2, uc-01 Retries); another key is LaunchSpent;
    /// - a new nonce appends the record and sends the browser to the IdentityProvider.
    let present
        (now: DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (authorizeUrl: string -> string)
        (state: State)
        (launch, key)
        : State * LaunchResult
        =
        let state = dropExpired now state

        match verify launch with
        | Error refusal -> state, LaunchResult.Refused refusal
        | Ok claims ->
            match state.Launches |> Map.tryFind claims.Nonce with
            | Some record when record.PublicKey = key -> state, answerOf authorizeUrl record
            | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent
            | None ->
                let record =
                    {
                        Nonce = claims.Nonce
                        State = newId ()
                        PatientId = claims.PatientId
                        PublicKey = key
                        Expiry = claims.Expiry
                        Outcome = None
                    }

                { state with Launches = state.Launches |> Map.add claims.Nonce record }, answerOf authorizeUrl record


    /// Step 5.7, one act (Rule 40): the Session is written, the login's other Sessions are
    /// closed and marked (Rule 8), the outcome is appended to the record.
    let private openSession
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (record: LaunchRecord)
        (standing: UserStanding)
        (state: State)
        =
        let id = newId ()

        let session =
            {
                User = Some standing.User
                PatientContext =
                    Some
                        {
                            PatientId = record.PatientId
                            // ext 6a: no imported data is not a refusal
                            Patient = patientData record.PatientId |> Option.defaultValue Shared.Models.Patient.empty
                        }
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint record.PublicKey)
            }

        let login = Some standing.User.UserId

        let superseded =
            state.Sessions
            |> Map.filter (fun sid s -> sid <> id && s.Login = login)
            |> Map.toList
            |> List.map fst

        let outcome = LaunchResult.Opened(id, session)

        {
            Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome }
            Sessions =
                superseded
                |> List.fold (fun m sid -> Map.remove sid m) state.Sessions
                |> Map.add
                    id
                    {
                        Session = session
                        Login = login
                    }
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid SessionEnding.SupersededByLaunch m) state.Endings
        },
        CallbackResult.Opened(id, openedUrl)


    let private refuse (record: LaunchRecord) refusal (state: State) =
        { state with
            Launches =
                state.Launches
                |> Map.add record.Nonce { record with Outcome = Some(LaunchResult.Refused refusal) }
        },
        CallbackResult.Refused(refusal, refusedUrl refusal)


    /// Step 4.5 and step 5. The ladder, in order: the state cookie must match (the browser that
    /// started the hop), the record must exist and be within its lifetime, an outcome already
    /// recorded is answered again (Rule 45), the IdentityProvider must have said who is there
    /// (ext 3c), the UserRegistry must know them (5.3, ext 5a) with the Launch's Patient active
    /// (ext 5b), and a Prescriber must have a PIN (5.4, Rule 25, ext 5d; a Reader needs none,
    /// ext 5c). Then the Session opens in one act.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (redeem: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> Patient option)
        (state: State)
        (cb: Callback)
        : State * CallbackResult
        =
        let state = dropExpired now state

        let byState =
            state.Launches
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.tryFind (fun r -> r.State = cb.State)

        let invalid =
            CallbackResult.Refused(LaunchRefusal.LaunchInvalid, refusedUrl LaunchRefusal.LaunchInvalid)

        match cb.StateCookie, byState with
        | Some cookie, Some record when cookie = cb.State && cb.State <> "" ->
            match record.Outcome with
            | Some(LaunchResult.Opened(id, _)) when state.Sessions |> Map.containsKey id ->
                state, CallbackResult.Opened(id, openedUrl)
            // the recorded Session was replaced by a newer launch of the same login (Rule 8):
            // answering its id would put a dead cookie over the live one
            | Some(LaunchResult.Opened _) -> state, CallbackResult.Superseded openedUrl
            | Some(LaunchResult.Refused refusal) -> state, CallbackResult.Refused(refusal, refusedUrl refusal)
            | Some(LaunchResult.RedirectTo _)
            | None ->
                let identity =
                    match cb.Error, cb.Code with
                    | None, Some code -> redeem code
                    | _ -> None

                match identity with
                | None -> refuse record LaunchRefusal.NoBrowserIdentity state
                | Some identity ->
                    match standing identity with
                    | None -> refuse record LaunchRefusal.NoRole state
                    | Some standing when standing.ActivePatientId <> Some record.PatientId ->
                        refuse record LaunchRefusal.WrongActivePatient state
                    | Some standing when standing.User.Role = UserRole.Prescriber && not standing.PinSet ->
                        refuse record LaunchRefusal.EnrolmentRequired state
                    | Some standing -> openSession newId patientData record standing state
        | _, None ->
            // no record: expired and dropped (Rule 29), or never presented
            state, invalid
        | _ -> state, invalid


    /// The port over a single mutable state guarded by a lock, over the three actor ports.
    let makeSessionPort
        (now: unit -> DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (patientData: PatientDataPort)
        =
        let gate = obj ()
        let mutable state = emptyState

        let update f =
            lock
                gate
                (fun () ->
                    let next, result = f state
                    state <- next
                    result
                )

        {
            present =
                fun launch -> async { return update (fun s -> present (now ()) newId verify idp.authorizeUrl s launch) }
            callback =
                fun cb ->
                    async {
                        return
                            update (fun s -> callback (now ()) newId idp.redeem registry.standing patientData.read s cb)
                    }
            find =
                fun id ->
                    async { return lock gate (fun () -> state.Sessions |> Map.tryFind id |> Option.map _.Session) }
            close = fun id -> async { return update (fun s -> { s with Sessions = s.Sessions |> Map.remove id }, ()) }
        }


/// The IdentityProvider and the UserRegistry as one stub directory over an identity choice made
/// on the stub launch page: `prescriber`, `reader`, `prescriber-other-patient`, `no-pin`,
/// `unknown` (a login the registry does not know), `none` (no identity: the stub IdP's
/// `/authorize` never issues a code for it). The active Patient of a choice is the one the
/// launch page was given, except for `prescriber-other-patient`.
module StubDirectory =

    let choices =
        [
            "prescriber"
            "reader"
            "prescriber-other-patient"
            "no-pin"
            "unknown"
            "none"
        ]


    let identityOf choice =
        {
            Login = choice
            DisplayName =
                match choice with
                | "prescriber" -> "Stub Prescriber"
                | "reader" -> "Stub Reader"
                | "prescriber-other-patient" -> "Stub Prescriber (other patient)"
                | "no-pin" -> "Stub Prescriber (no PIN)"
                | other -> $"Stub {other}"
        }


    /// The registry's answer for a login, given the Patient the launch page made active.
    let standingOf (activePatientId: string) (identity: BrowserIdentity) : UserStanding option =
        let user role =
            {
                UserId = identity.Login
                DisplayName = identity.DisplayName
                Role = role
            }

        match identity.Login with
        | "prescriber" ->
            Some
                {
                    User = user UserRole.Prescriber
                    ActivePatientId = Some activePatientId
                    PinSet = true
                }
        | "reader" ->
            Some
                {
                    User = user UserRole.Reader
                    ActivePatientId = Some activePatientId
                    PinSet = false
                }
        | "prescriber-other-patient" ->
            Some
                {
                    User = user UserRole.Prescriber
                    ActivePatientId = Some "other-patient"
                    PinSet = true
                }
        | "no-pin" ->
            Some
                {
                    User = user UserRole.Prescriber
                    ActivePatientId = Some activePatientId
                    PinSet = false
                }
        | _ -> None


    /// The directory: one-time codes (the stub IdP) and the active Patient per login (the stub
    /// registry), behind a lock. `issue` is what `/authorize` calls once it read the identity
    /// choice and the active Patient from the stub cookie.
    type Directory =
        {
            idp: IdentityProviderPort
            registry: UserRegistryPort
            issue: string -> string -> string
        }


    /// A code lives as long as a Launch (Rule 29); older ones are pruned on the next issue.
    let codeLifetime = TimeSpan.FromMinutes 2.0


    /// One active Patient per login, as MainEHR has: a later launch of the same login for
    /// another Patient makes an earlier, still open launch wrong-patient (ext 5b).
    let make (now: unit -> DateTime) (newCode: unit -> string) : Directory =
        let gate = obj ()
        let codes = Collections.Generic.Dictionary<string, BrowserIdentity * DateTime>()
        let active = Collections.Generic.Dictionary<string, string>()

        let prune () =
            let cutoff = now () - codeLifetime

            for stale in
                codes
                |> Seq.filter (fun kv -> snd kv.Value < cutoff)
                |> Seq.map _.Key
                |> Seq.toList do
                codes.Remove stale |> ignore

        {
            idp =
                {
                    authorizeUrl = fun state -> $"/authorize?state={Uri.EscapeDataString state}"
                    redeem =
                        fun code ->
                            lock
                                gate
                                (fun () ->
                                    match codes.TryGetValue code with
                                    | true, (identity, issued) when now () - issued <= codeLifetime ->
                                        codes.Remove code |> ignore
                                        Some identity
                                    | _ -> None
                                )
                }
            registry =
                {
                    standing =
                        fun identity ->
                            lock
                                gate
                                (fun () ->
                                    match active.TryGetValue identity.Login with
                                    | true, pid -> standingOf pid identity
                                    | _ -> standingOf "" identity
                                )
                }
            issue =
                fun choice activePatientId ->
                    lock
                        gate
                        (fun () ->
                            prune ()
                            let code = newCode ()
                            codes[code] <- identityOf choice, now ()
                            active[choice] <- activePatientId
                            code
                        )
        }


/// The PatientDataPlatform stub: nothing to import, except that `no-data` has no record at all
/// (ext 6a).
module StubPatientData =

    let port: PatientDataPort =
        {
            read =
                fun pid ->
                    if pid = "no-data" then
                        None
                    else
                        Some Shared.Models.Patient.empty
        }


/// The stub LaunchScript (uc-01 step 1) as a page the server serves in full scope: it mints a
/// sealed Launch for a chosen PatientId and opens the client on it. Pure here; `Server.fs`
/// mounts `GET /stub/launch` (the page) and `POST /stub/launch` (mint + redirect).
module StubLaunch =

    let path = "/stub/launch"


    /// The Launch lifetime (Rule 29): a page load, the identity round trip, a retry or two.
    let lifetime = TimeSpan.FromMinutes 2.0


    /// The form. No inline script or style, so the CSP (`default-src 'self'`) holds.
    let page =
        $"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>GenPRES stub launch</title></head>
<body>
<h1>Stub LaunchScript</h1>
<p>Stands in for the MainEHR LaunchScript (uc-01 step 1): mints a sealed Launch for the patient
below and opens GenPRES on it. Development and test servers only.</p>
<form method="post" action="{path}">
  <label>PatientId <input name="pid" value="stub-patient" required></label>
  <button type="submit">Launch</button>
</form>
</body>
</html>"""


    /// Where the browser goes after minting: the hash form of decision D1.
    let launchUrl (launch: Launch) =
        match launch with
        | Launch text -> $"/#/session?launch={Uri.EscapeDataString text}"


    /// Mints the Launch for a posted PatientId; blank falls back to the stub patient.
    let mint (now: DateTime) (newNonce: unit -> string) (key: LaunchSeal.Key) (pid: string) =
        let pid =
            if String.IsNullOrWhiteSpace pid then
                "stub-patient"
            else
                pid.Trim()

        LaunchSeal.mint
            key
            {
                PatientId = pid
                Nonce = newNonce ()
                Expiry = now + lifetime
            }


    let choices = StubDirectory.choices

    let identityCookieName = "genpres_stub_identity"


    /// The stub identity cookie value for a choice and the Patient the page made active.
    let identityCookie (choice: string) (pid: string) =
        $"{Uri.EscapeDataString choice}.{Uri.EscapeDataString pid}"


    /// The choice and the Patient back from the cookie; None for anything else.
    let parseIdentityCookie (value: string option) : (string * string) option =
        match value with
        | Some v when not (String.IsNullOrWhiteSpace v) ->
            match v.Split('.') with
            | [| choice; pid |] when choices |> List.contains (Uri.UnescapeDataString choice) ->
                Some(Uri.UnescapeDataString choice, Uri.UnescapeDataString pid)
            | _ -> None
        | _ -> None


module Adapters =

    open Informedica.GenForm.Lib


    let private interactionJsonCache =
        lazy
            (let path =
                System.IO.Path.Combine(Informedica.Utils.Lib.AppPath.interactionsDir (), "Data.JSON")
                |> System.IO.Path.GetFullPath

             if System.IO.File.Exists(path) then
                 System.IO.File.ReadAllText(path) |> Some
             else
                 None)


    let loadInteractionJson () = interactionJsonCache.Value


    let toSharedDrugInteraction (di: Informedica.GenInteract.Lib.DrugInteraction) : Shared.Types.DrugInteraction =
        {
            Name = di.Name
            Drug1 = di.Drug1
            Drug2 = di.Drug2
        }


    let private resolveLogger () =
        match Logging.loggingLevel with
        | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
        | Some level ->
            let agent = Logging.getLogger level Logging.OrderLogger
            (Some agent, agent.Logger)


    let private setComponentName name agent =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        }


    let private makeFormularyPort (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get provider }

            getParenteralia =
                fun par -> async { return par |> ParenteraliaService.get provider |> Result.mapError Array.singleton }
        }


    let private makeOrderContextPort agent logger (provider: Resources.IResourceProvider) : OrderContextPort =
        {
            evaluate =
                fun ctxCmd ctx ->
                    async {
                        do! setComponentName "OrderContext" agent

                        return ctx |> OrderContextService.evaluate logger provider ctxCmd
                    }
        }


    let private makeOrderPlanPort
        agent
        (provider: Resources.IResourceProvider)
        (orderCtxPort: OrderContextPort)
        : OrderPlanPort
        =
        {
            updateOrderPlan =
                fun tp cmdOpt ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        let! updated = OrderPlanService.updateOrderPlan orderCtxPort tp cmdOpt
                        let totals = provider.GetTotals()
                        return updated |> OrderPlanService.calculateTotals totals |> Ok
                    }

            filterOrderPlan =
                fun tp ->
                    async {
                        let totals = provider.GetTotals()
                        return tp |> OrderPlanService.calculateTotals totals |> Ok
                    }
        }


    let private makeNutritionPlanPort
        (orderCtxPort: OrderContextPort)
        logger
        (provider: Resources.IResourceProvider)
        : NutritionPlanPort
        =
        {
            initNutritionPlan =
                fun patient ->
                    async {
                        let totals = provider.GetTotals()
                        return NutritionPlanService.initNutritionPlan logger totals patient
                    }

            addNutritionContext =
                fun (plan, category) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.addNutritionContext totals orderCtxPort (plan, category)

            removeNutritionContext =
                fun (plan, id) ->
                    async {
                        let totals = provider.GetTotals()
                        return NutritionPlanService.removeNutritionContext totals (plan, id)
                    }

            updateNutritionOrderContext =
                fun (plan, label, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.updateNutritionOrderContext totals orderCtxPort (plan, label, ctx)

            selectNutritionOrderScenario =
                fun (plan, label, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.selectNutritionOrderScenario totals orderCtxPort (plan, label, ctx)

            navigateNutritionOrderContext =
                fun (plan, label, ctxCmd, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.navigateNutritionOrderContext totals orderCtxPort (plan, label, ctxCmd, ctx)
        }


    /// The session port of a server that does not launch: every Launch is refused as
    /// invalid and no session is ever found. Used in production until the scope switch (#580)
    /// decides what a production server exposes.
    let sessionDisabled: SessionPort =
        {
            present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
            callback =
                fun _ ->
                    async {
                        return
                            CallbackResult.Refused(
                                LaunchRefusal.LaunchInvalid,
                                Hop.refusedUrl LaunchRefusal.LaunchInvalid
                            )
                    }
            find = fun _ -> async { return None }
            close = fun _ -> async { return () }
        }


    let makeAppEnvWith
        (launchKey: LaunchSeal.Key)
        (directory: StubDirectory.Directory)
        (provider: Resources.IResourceProvider)
        : AppEnv
        =
        let agent, logger = resolveLogger ()
        let orderCtxPort = makeOrderContextPort agent logger provider

        {
            formulary = makeFormularyPort provider
            orderContext = orderCtxPort
            orderPlan = makeOrderPlanPort agent provider orderCtxPort
            nutritionPlan = makeNutritionPlanPort orderCtxPort logger provider
            interaction =
                {
                    checkInteractions =
                        fun drugs ->
                            async {
                                try
                                    let result =
                                        Informedica.GenInteract.Lib.Api.checkInteractions (loadInteractionJson ()) drugs
                                        |> List.map toSharedDrugInteraction

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }

                    getDrugNames =
                        fun () ->
                            async {
                                try
                                    let result = Informedica.GenInteract.Lib.Api.getDrugNames (loadInteractionJson ())

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            logAnalyzer =
                {
                    listLogFiles =
                        fun () ->
                            async {
                                try
                                    return Ok(LogAnalyzer.listLogFiles ())
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                    analyzeLogFile = fun fileName -> async { return LogAnalyzer.analyzeFile fileName }
                }
            requireLoaded =
                fun () ->
                    let info = provider.GetResourceInfo()

                    if info.IsLoaded then
                        None
                    else
                        info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some
            // plan 409 step 2: an in-memory stub with a two-minute Launch lifetime (Rule 29);
            // its sessions live as long as this AppEnv
            session =
                Hop.makeSessionPort
                    (fun () -> DateTime.UtcNow)
                    PublicKey.randomId
                    (fun launch -> LaunchSeal.verify DateTime.UtcNow launchKey launch)
                    directory.idp
                    directory.registry
                    StubPatientData.port
        }


    /// An env with its own seal key and stub directory: what tests and the MCP host build. The
    /// server builds `makeAppEnvWith` so that its stub pages share the key and the directory.
    let makeAppEnv (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        makeAppEnvWith
            (LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes)
            (StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId)
            provider
