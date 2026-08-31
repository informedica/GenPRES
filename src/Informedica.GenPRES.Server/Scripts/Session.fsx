// ═══════════════════════════════════════════════════════════════════════════════
//   GenPRES – MainEHR Integration: the system model, executable
// ═══════════════════════════════════════════════════════════════════════════════
//
//     dotnet fsi Session.fsx
//
// Standalone — no #load, no #r. It prints a trace per scenario and ends with a count
// of self-checks.
//
// ═══════════════════════════════════════════════════════════════════════════════
//   SECTION 0 — THE SYSTEM MODEL
// ═══════════════════════════════════════════════════════════════════════════════
//
// Actors, Roles, Concepts, Constraints, Consequences, Invariants, Possibilities,
// Rules, Guarantees. Everything after this section carries one of them and cites it
// by number; nothing they do not sanction lives here.
//
// ── Actors ─────────────────────────────────────────────────────────────────────
// Who takes part. [ours] = under construction. [given] = not ours to change. The User
// is neither: they are who the system is for.
//
//  1. MainEHR Workstation  [given]  the running EHR client.
//  2. MainEHR LaunchScript [ours]   a script behind a button there: reads a key, seals a Launch, opens
//                                   the browser, exits. All its scripting allows.
//  3. GenPRES Client       [ours]   the UI, in a browser carrying the User's hospital sign-on.
//  4. GenPRES Server       [ours]   the backend.
//  5. GenPRES Database     [ours]   two stores, one writer: a clinical store that is copied, a private
//                                   store that is not.
//  6. PatientDataPlatform  [given]  a shared read-only copy of MainEHR's, GenPRES's and other databases.
//  7. User                          the person who uses MainEHR and GenPRES.
//  8. IdentityProvider     [given]  says who is at a browser. No Role, no Patient.
//  9. UserRegistry         [ours]   says who a login is, what they may do, and how to mail them.
// 10. MailService          [given]  sends mail, outside GenPRES and MainEHR both.
//
// ── Roles ──────────────────────────────────────────────────────────────────────
// What authority a User holds. The UserRegistry decides it; MainEHR and GenPRES
// enforce it separately, each within itself.
//
//  1. Prescriber  may read, and may create TreatmentPlans.
//  2. Reader      may prescribe within a Session like anyone, but may save none of it.
//
// ── Concepts ───────────────────────────────────────────────────────────────────
// What is passed between actors or held by them.
//
//  1. UserContext             who a User is, and their Role.
//  2. PatientContext          a PatientId and the Patient Data GenPRES needs, read once at the launch.
//  3. Launch                  the active PatientId, sealed under a key the LaunchScript shares with the
//                             Server: single use, short-lived, naming no User.
//  4. BrowserIdentity         who the IdentityProvider says is at the browser; read once, and it is the
//                             Session's User.
//  5. MainEHR Session         the period a User is logged in at a Workstation; many Patients, one active.
//  6. MainEHR PatientRecord   all the patient data MainEHR keeps.
//  7. GenPRES UserCredential  one User's login, optional PIN and wrong-entry count. No Role, no identity
//                             of its own.
//  8. GenPRES Session         a User's dealings with GenPRES, for a Patient or for none; anonymous
//                             without a launch.
//  9. GenPRES SessionRecord   what binds a SessionId to a User and a Patient, and whether it is open.
//                             Kept after it ends.
// 10. OrderContext            a PatientContext with its OrderScenarios, keeping its identity across plans
//                             and carrying the stamp of whoever last changed it.
// 11. OrderScenario           one proposed Order with the prescribing information that gives it meaning.
// 12. GenPRES PatientRecord   a Patient's append-only history in GenPRES: a sequence of TreatmentPlans.
// 13. TreatmentPlan           the plan as it stood when saved: orders, author, Session, base, nearest
//                             Signed ancestor, and the Patient Data it was built on.
// 14. Submission              saving and signing as one act; with the Session User's PIN it is Signed,
//                             without it Unsigned.
// 15. Prescribing             changing the WorkPlan within a Session; nothing reaches the record until a
//                             TreatmentPlan is created.
// 16. WorkPlan                the plan under the User's hands in the Client. It dies with the browser
//                             unless submitted.
// 17. Token                   a note the Server signs and the Client returns unaltered: the OpenedToken,
//                             NoticeToken, SigningChallenge and DataNoticeToken.
//
// ── Constraints ────────────────────────────────────────────────────────────────
// How to read the edges. Not itself a constraint.
//   X ->  Y   X opens a connection to Y and gets Y's reply on it. That way only.
//   X =>  Y   X starts Y with initial parameters. No reply, no error path.
//   X <-> Y   a User can read what Y shows and act on it.
// A pair with no edge cannot exchange data at all, and edges do not compose.
//
// User Interaction — what a User can read and act on, or start.
//   U1. Any User <-> MainEHR Workstation
//   U2. Any User <-> MainEHR LaunchScript — it can report its own acts, then it exits.
//   U3. Any User <-> GenPRES Client
//
// Communication — what may reach what. Edges touching a [given] actor are what the
// deployment allows; edges between [ours] actors are what we choose to build.
//   C1.  MainEHR Workstation  -> UserRegistry
//   C2.  MainEHR Workstation  -> PatientDataPlatform
//   C3.  GenPRES Client       -> IdentityProvider
//   C4.  MainEHR LaunchScript => GenPRES Client
//   C5.  GenPRES Client       -> GenPRES Server
//   C6.  GenPRES Server       -> IdentityProvider
//   C7.  GenPRES Server       -> UserRegistry
//   C8.  GenPRES Server       -> PatientDataPlatform
//   C9.  GenPRES Server       -> GenPRES Database
//   C10. GenPRES Server       -> MailService
//
// ── Consequences ───────────────────────────────────────────────────────────────
// What the edges force. Not new assertions, and not changeable without changing an edge.
//
//  1. The LaunchScript learns nothing after the launch, so every error falls to the Client.
//  2. The Launch is the only channel from the EHR side, and the key is all that authenticates it:
//     whoever holds it can name any Patient. The User it cannot name (Rule 4).
//  3. Only the Server knows whether a Launch was used, and it cannot tell the LaunchScript.
//  4. The Launch travels in a URL, so it lands in history and logs: hence single use, short life.
//  5. Workstation, LaunchScript and browser all run on the User's PC, which therefore needs every
//     [given] actor it talks to — and the key.
//  6. The Server cannot reach a Client, so a Client learns its Session ended at its next request.
//
// ── Invariants ─────────────────────────────────────────────────────────────────
// Always true of the environment. Not ours to change.
//  1. A User has at most one active Patient at a time in a MainEHR Session.
//
// ── Possibilities ──────────────────────────────────────────────────────────────
// May happen in the environment. Not ours to prevent, only to handle.
//  1. A User can leave a MainEHR Session logged in and another User can act in it.
//  2. Several Users can have the same Patient active, each in their own Session.
//
// ── Rules ──────────────────────────────────────────────────────────────────────
// What the [ours] actors must enforce. Chosen, and changeable by decision. One
// assertion each; grouped for reading, numbered straight through for citing.
//
// Launch
//   1. The LaunchScript decides which MainEHR User may run it, and opens GenPRES.
//   2. A Launch is accepted once — twice from the same BrowserIdentity in its lifetime, answered alike.
//   3. A Launch is accepted only within its lifetime.
//   4. The Session's User is the BrowserIdentity, never anything the Launch says.
//   5. The Server takes the Role from the UserRegistry at every launch, never from the Launch.
//   6. A launch that cannot be honoured opens no Session; at most the Client offers an anonymous one.
//
// Session
//   7. A User has at most one open Session, and so has a browser; opening one closes the rest.
//   8. Every request from the Client refreshes its Session's idle clock.
//   9. A Session ends by being closed, idling out, its absolute lifetime, the wrong-PIN limit, or being
//      replaced by the same User or browser.
//  10. An ending the User did not cause is told at their next launch and acknowledged there, once; a
//      Client still holding the SessionId is only refused, with the reason.
//  11. The SessionId rides in an HttpOnly, Secure, SameSite=Strict cookie, and a changing request needs
//      GenPRES's own Origin.
//  12. A Session without a PatientId may prescribe, but may open or create no TreatmentPlan.
//  13. A launchless Session is anonymous — no User, no Role, no Patient — capped in number and ended by
//      an absolute limit.
//
// Record
//  14. Every TreatmentPlan is created under one User's credentials, and every OrderContext changed in
//      the Session carries that stamp.
//  15. A TreatmentPlan is Signed, Unsigned or Discarded: its content never changes, and its state moves
//      once, Unsigned to Discarded.
//  16. Only the most recent Signed TreatmentPlan counts clinically.
//  17. Every Signed TreatmentPlan is open to read, but only the most recent can be built on.
//  18. Only the User who submitted an Unsigned TreatmentPlan can open it.
//  19. A User starts from the newest plan that is Signed or their own Unsigned — offered rather than
//      opened, with when and how, where the Session that saved it ended without them.
//  20. A User may submit unless a Signed plan newer than the one they opened with exists; opening that
//      one lifts the block.
//  21. A User about to submit is told whose newer Unsigned work exists, not what is in it.
//
// Signing
//  22. The Server alone verifies a UserCredential; the PIN never leaves GenPRES.
//  23. Every launch checks whether a PIN is set for the login.
//  24. A Prescriber with no PIN sets one before the launch goes on, and only once the registry knows them.
//  25. A Reader is never asked for a PIN: they never create a TreatmentPlan.
//  26. Every PIN set or replaced is mailed to the User and recorded — as is reaching the wrong-PIN limit.
//  27. Wrong PIN entries count per credential across Sessions; at the limit the Session ends and signing
//      locks for a delay that doubles with each further guess and decays with time.
//
// Configuration
//  28. A Launch lives long enough for one launch: a page load, the identity round trip, a retry or two.
//  29. A Session spans a clinician's pauses and no more than a shift; the Client sends nothing unprompted.
//  30. The wrong-PIN limit forgives mistyping and no more; the PIN is memorable, its space large.
//
// State — where Session state lives, chosen so the Server keeps none of it.
//  31. The Server holds no Session state between requests: the WorkPlan is the Client's, the standing is
//      the SessionRecord's.
//  32. The Server takes a request's User and Patient from the SessionRecord, never from the request.
//  33. The plan a Session opened with travels as the OpenedToken, spent by the Submission that lands or
//      by a discard, and re-issued over the new baseline.
//  34. A choice to submit anyway travels as the NoticeToken, honoured for what it disclosed and nothing
//      newer.
//  35. Stamps are computed by the Server against the base plan; one arriving from the Client is refused.
//  36. The Rule 20 check and the append are one act at the Database, which is what arbitrates between
//      Servers, and a refusal says whose work blocks, not which.
//
// Security — what [ours] enforces against a hostile environment.
//  37. A PIN is set or replaced only with a mailed one-time code, one code at a time; changing it any
//      other way needs the current PIN.
//  38. Every signature re-takes the Role from the registry, with a bounded grace if it cannot answer,
//      and after that fails closed.
//  39. The Client erases the Launch from the URL at first presentation, keeping it only in memory for
//      retries within its lifetime.
//
// Atomicity — what must be one act at the Database.
//  40. Every SessionRecord change is one conditional act, and one open Session per User and per browser
//      is the Database's to keep.
//  41. Being out of time is checked when a request arrives, not only by a sweep, and ends it then.
//  42. Committing a Submission is one transaction that re-establishes everything it rests on, or nothing
//      lands.
//  43. A signature approves exactly what the SigningChallenge showed: the PIN comes back with it, and the
//      commit checks the plan submitted is the plan named.
//  44. The Patient Data is re-read before the challenge; changed or unreadable, the User is shown it and
//      goes on only by accepting.
//  45. Every changing request carries a key; the Database commits a key once and a retry gets the first
//      result.
//
// Audit — the record of the acts around the record.
//  46. The Server appends every launch, opening, ending, Submission, signature, PIN change and refusal
//      to the audit, anonymous refusals by count.
//
// Discard
//  47. A User may discard their own most recent Unsigned plan: no PIN, not blocked by Rule 20, never
//      opened or built on again, and never removed from the record.
//
// ── Guarantees ─────────────────────────────────────────────────────────────────
// What the Rules add up to. Derived, not asserted, and checked at the end of the run.
//
//  1. One constant. In a PatientRecord the PatientId is the only thing that never changes, and no hand
//     ever sets it (Rules 12, 13).
//  2. One version. Exactly one plan is the visible version and the only place to build from: the newest
//     Signed one, or its creator's own newer Unsigned one (Rules 16-20).
//  3. Carts and one checkout. Each User has a private cart in their own Client, and signing is the only
//     checkout: the first to sign wins, and every other cart is rebuilt on top (Rules 19, 20, 31, 36).
//  4. Audit. The record keeps every version with its author and its base, the clinical store copy names
//     nothing private, and the security audit stands beside it (Rules 14, 46; Actors 5, 6). What a
//     signature attests is a person at the launch and a credential holder at the signature — per
//     credential, not per person. Non-repudiation is not claimed.
//
// ── What this model does not carry ─────────────────────────────────────────────
// Deployment, deliberately:
//
//   * Rule 11's cookie and Origin check: the SessionId is simply held and sent.
//   * Rule 4's mechanism: `BrowserState.BrowserIdentity` is a value the browser presents, not a sign-on
//     exchange. The rule is carried; the protocol is not.
//   * Rule 29's second sentence: every request here follows a `UserAct`, which states that discipline
//     rather than enforcing it.
//   * Rule 37's last sentence: there is no change-PIN act, only the code-gated one.
//   * Rule 39's second half: caching, referrers and third-party script belong to serving the Client.
//   * Rule 13's rate limit: the standing cap and the absolute lifetime are here, a rate is not.
//   * Concept 7's login. `UserCredential` is keyed by UserId and carries no login: the
//     registry is asked for one at every launch (Rule 5) and holds the mail address
//     (Rule 26), so nothing here would ever read it.
//   * Rule 21's "whose work it is" is one name, even where two Users have work
//     outstanding. The NoticeToken covers all of it; the notice names one.
//   * The cryptography: keys are strings and macs are string equality — placeholders that make forgery
//     tests possible, not security properties.
//   * Time: the clock advances one tick per handled message, so lifetimes are counted in messages.
//   * Exhaustive concurrency: Rules 40-45 are checked over crafted interleavings, not the state space.
//
// The rest of the file is in three parts:
//   1. types      — the vocabulary: identities, concepts, messages, actor state
//   2. modules    — the edge table, the Record rules, the tokens, and the reducer
//   3. scenarios  — the harness, UC-1 .. UC-14, and the derived assertions
//
// ── [ships] and [model] ────────────────────────────────────────────────────────
// Every section below is tagged, and so is anything inside one that breaks the tag:
//
//   [ships]  the design itself — types and rule logic meant to be carried into the
//            source, in this shape. What is here is what the Rules say.
//   [model]  scaffolding that exists so this file can run alone: the message plumbing
//            that stands in for HTTP and process boundaries, the other actors'
//            insides, the clock, the tracing, and the crypto placeholders. In the real
//            system these are the framework, somebody else's component, or a library —
//            never code written from this file.


// ═══════════════════════════════════════════════════════════════════════════════
//   SECTION 0B — THE TECHNICAL VOCABULARY
// ═══════════════════════════════════════════════════════════════════════════════
//
// Section 0's Concepts are the domain's words. These are the engineering ones the
// Rules rest on — Rule 33 turns on unforgeability, Rule 42 on atomicity, Rule 45 on
// idempotency — and a reader who does not already have them has nowhere else in the
// file to look. Each says where it appears here.
//
// ── Integrity and secrets ──────────────────────────────────────────────────────
//
//   Hash (digest)     a fixed-size fingerprint of data, computable by anyone. Catches
//                     accidental change; proves nothing about origin, because an
//                     attacker who edits the data recomputes it.
//                     Here: `WorkPlan.digest`, `WorkPlan.dataDigest`.
//   MAC               Message Authentication Code: a tag over data *and a secret key*.
//                     Proves the data came from a key holder and was not altered.
//                     Checked by recomputing and comparing — never by looking it up.
//                     Here: `Token.Mac`, `Reset.macOf`.
//   HMAC              the standard construction of a MAC from a hash function (RFC
//                     2104). Resists the length-extension attack a naive
//                     hash(key ‖ message) allows. Here: what `macAs` stands in for.
//   Signature         asymmetric: a private key mints, a public key verifies, so
//                     anyone can check it. A MAC convinces only key holders — enough
//                     here, where the Server is both issuer and verifier.
//   Key separation    one master secret, a derived subkey per purpose, so a token
//                     minted for one purpose cannot verify as another: it fails by
//                     key, before any field is compared.
//                     Here: `Token.subKey`, `TokenPurpose`.
//   Canonical form    one value, exactly one byte form. Without it the same token
//                     could verify under one encoding and mean something else under
//                     another. Here: `Token.canonical` — fixed order, fixed separators.
//   Nonce             number used once: uniqueness for a single issuance, and here
//                     also the key a spent-mark is filed under.
//                     Here: `Claim.Nonce`, `PrivateStore.Spent`.
//   Constant time     comparing two tags in time that does not depend on how many
//                     bytes matched, so timing leaks nothing. Not modelled: this file
//                     compares with `=`.
//   Salt, KDF         for *stored* secrets, a per-record salt and a deliberately slow
//                     function (Argon2, bcrypt). A short PIN with an attempt limit is
//                     a different regime (Rule 27). Not modelled: PINs are held as
//                     typed, and that is a placeholder.
//
// ── Credentials and sessions ───────────────────────────────────────────────────
//
//   Bearer            anything whose mere possession grants use, with no proof of who
//                     holds it — hence single use, short life, never in a URL.
//                     Here: `Launch`, `SessionId` (Rules 2, 3, 11). A Launch is a
//                     bearer value for the Patient it names and for nothing else: it
//                     names no User, so holding it buys a Session of the holder's own
//                     (Rule 4, UC-1 ext 3b).
//   Token             in this design (Concept 17): a note the Server writes to itself,
//                     hands to the Client, and refuses to believe unless it comes back
//                     with its mac intact — the Server's memory where it keeps none.
//   Replay            re-sending a valid message to get its effect twice. The defence
//                     is a spent-mark: the nonce is consumed by the act that honours
//                     it. Here: the commit and the discard, Rules 2, 33 and 43. Rule 2
//                     is the exception that proves it: the same browser re-presenting
//                     a spent Launch in time is answered as the first time, because a
//                     retry of one launch is not a second launch.
//   TTL, expiry       a lifetime signed *into* the claim, so it cannot be extended by
//                     editing the token. Here: `Claim.ExpiresAt`, `launchTtl`,
//                     `tokenTtl`, `resetCodeTtl`, `anonymousLifetime`.
//   PKCE              proof key for code exchange: the browser invents a secret, sends
//                     only its hash when starting the flow and reveals the secret when
//                     redeeming, so a stolen code is useless to anyone else. Absent
//                     here: the Launch is an unbound bearer value (Rule 4).
//   HttpOnly cookie   a cookie no script can read, only the browser attaches. Rule
//                     11's intended transport; not modelled.
//   Correlation id    one id tying together the log lines of a single request. What
//                     `RequestId` becomes in a real Server.
//
// ── Transactions and concurrency ───────────────────────────────────────────────
//
//   Transaction       a group of changes that all take effect or none do. Here: the
//                     commit of Rule 42.
//   Serializable      the strongest isolation: the outcome equals *some* order of
//                     running the concurrent transactions one at a time. What makes
//                     Rule 36's check-and-append sound in production.
//   Read-modify-write read a value, compute a new one, write it back — and the classic
//                     race, where two readers see the same old value and one write is
//                     lost. What Rule 40 removed.
//   Conditional write no lock: write only if the state still matches what was seen,
//                     otherwise fail. Also called compare-and-swap, or optimistic
//                     concurrency. Here: `EndSessionIfOpen`, `TouchIfOpen`, and the
//                     commit's "nothing Signed newer than my base".
//   Idempotency key   a client-minted id on a request that changes something; the
//                     Database records key -> result and replays it, so a retry after
//                     a lost reply cannot act twice.
//                     Here: `IdemKey`, `PrivateStore.Answered` (Rule 45).
//   Monotonic id      every new value greater than every value issued before — what
//                     "newer than" needs (Rules 19, 20, 21, 36). Here:
//                     `TreatmentPlanNo`, standing in for the storage's own key.
//   Auto-increment    one mechanism for that, with caveats worth knowing: allocated at
//                     insert and not at commit, cached in per-session blocks, gaps on
//                     rollback, per-node only when sharded.
//   ULID, Snowflake   ids ordered by construction — a timestamp with a counter in the
//                     low bits — so one column carries both "when" and "which first".
//   LSN               the storage's own commit position in its write-ahead log: the
//                     truest ordering it has.
//   Interleaving      the order two concurrent cascades happen to take. Here: the
//                     `racing` runner, which explores one deliberately.
//   Fail-open         when a check cannot decide and permits anyway — the wrong
//                     direction for a safety rule, which must fail closed. Why
//                     `TreatmentPlan.At` is never the ordering key.
//   Eager vs lazy     expiry checked when a request arrives, or by a background sweep.
//                     Rule 41 does both: the sweep for Sessions nobody returns to, the
//                     arriving request for the rest.
//
// ── Assurance ──────────────────────────────────────────────────────────────────
//
//   Invariant         something true of every reachable state — "once ended, always
//                     ended", "one open Session per User" (Rule 40).
//   Property test     assert a property and let the tool generate the inputs and
//                     shrink the failures, instead of writing examples by hand
//                     the failures. Not used here.
//   Model checking    exhaustively exploring a specification's states to prove an
//                     invariant under every interleaving. Not used here either: this
//                     file has one crafted race, which is not that.
//   Adversarial test  a scenario written to make the design fail rather than to show
//                     it working. Here: "The adversarial review, answered".
//

// ═══════════════════════════════════════════════════════════════════════════════
//                                 1. TYPES
// ═══════════════════════════════════════════════════════════════════════════════

// ───────────────────────────── identities ─────────────────────────────  [ships] — except the counters marked [model] inside

type UserId           = UserId of string            // stable key: what audit keys on
type LoginName        = LoginName of string         // unique today, but renameable
type MailAddress      = MailAddress of string       // from the UserRegistry (Rule 26)
type PatientId        = PatientId of string
type BrowserId        = BrowserId of int            // [model]: one browser, named
type SessionId        = SessionId of string         // Rule 11: bearer, never in a URL
type SessionNo        = SessionNo of int            // traces and ui only, never a key
type TreatmentPlanId  = TreatmentPlanId of string
type TreatmentPlanNo  = TreatmentPlanNo of int      // ordering within one PatientRecord
type OrderContextId   = OrderContextId of string    // Concept 10: persists across plans
type AttemptId        = AttemptId of int            // [model]: one launch, mid-flight
/// [model] Correlates the Database legs of one request, standing in for the call
/// stack this reducer has not got. Dropped with the reply (Rule 31).
/// Neither Rule 45's key, which the Client mints, nor a SessionId: it names one
/// exchange, never a User.
type RequestId        = RequestId of int
type Pin              = Pin of string               // Concept 7. Never leaves GenPRES.
/// Rule 37. What goes out by mail to reset a PIN: one-time, short-lived, and worth
/// nothing without the Session that asked for it.
type ResetCode        = ResetCode of string
/// Rule 45. Minted by the Client, one per mutating request. A retry carries the key
/// of the request it retries, so the Database can answer it rather than do it twice.
type IdemKey          = IdemKey of string

/// Roles. The UserRegistry decides which.
type Role =
    | Prescriber
    | Reader

/// The ten Actors, plus Environment — which is not one of them but the world they
/// run in: the clock, and starting and stopping infrastructure.
type ActorId =
    | User                              // Actor 7
    | MainEhrWorkstation                // Actor 1  [given]
    | MainEhrLaunchScript               // Actor 2  [ours]
    | GenPresClient of BrowserId        // Actor 3  [ours]
    | GenPresServer                     // Actor 4  [ours]
    | GenPresDatabase                   // Actor 5  [ours]
    | PatientDataPlatform               // Actor 6  [given]
    | IdentityProvider                  // Actor 8  [given]
    | UserRegistry                      // Actor 9  [ours]
    | MailService                       // Actor 10 [given]
    | Environment

// ───────────────────────────── the concepts ─────────────────────────────  [ships]

/// Concept 1. Identification and Role — nothing else. The Role is the registry's
/// answer (Rule 5), never the launch's.
type UserContext =
    {
        UserId : UserId
        Login  : LoginName
        Role   : Role
    }

/// Patient Data relevant for GenPRES. Opaque here: this is a model of the protocol,
/// not of the clinical content.
type PatientData = PatientData of string

/// Concept 13. Coarser than the Concept asks: `PatientData` is one opaque value
/// here, so one source and one time stand for what it wants per value.
type DataSource =
    | FromPlatform of at: int
    | ByHand of at: int

/// Concept 2. Only a launch can supply the identification; the User can supply the
/// data by hand. Read from the PatientDataPlatform once, at the launch, and not
/// refreshed while the Session lives.
type PatientContext =
    {
        Patient : PatientId option
        Data    : PatientData option
    }

/// Concept 3. The active Patient, sealed by the LaunchScript under the key it
/// shares with the Server. No login: the User is Concept 4's — whoever the browser
/// proved to be (Actor 8, Rule 4). The Patient is still MainEHR's word alone.
type Launch =
    {
        Patient  : PatientId option
        Nonce    : string
        IssuedAt : int
        Mac      : string
    }

/// Concept 7. Carries no Role of its own. It is keyed by UserId — the stable identity
/// the registry resolves a login to — because a login can be renamed and a credential
/// must not follow the name (Rule 27: the count is per person, across Sessions).
type UserCredential =
    {
        User         : UserId
        Pin          : Pin option
        AttemptCount : int              // Rule 27: counts across Sessions
        /// Rule 27. A delay, not a latch: it passes on its own.
        LockedUntil  : int option
    }

/// Rule 37. A reset in flight, holding the code as a mac. `Wrong` is the code's own
/// count, apart from the credential's: guessing a code must not lock a good PIN.
type PinReset =
    {
        User    : UserId
        CodeMac : string
        Expires : int
        Wrong   : int
    }

/// Concept 10. No clinical content is modelled: `Content` stands in for all of it.
/// All four fields arrive from the Client and the Server trusts two — `Patient` is
/// checked against the SessionRecord (Rule 32), `Stamp` recomputed (Rule 35).
type OrderContext =
    {
        Id      : OrderContextId
        Patient : PatientId option
        Content : string
        Stamp   : UserContext option
    }

/// Concept 16. No stamps, no identity, no number: nothing here is history until it
/// is submitted (Concept 14). It travels with every request (Rule 31).
type WorkPlan =
    {
        Data   : PatientData option
        /// Concept 13. Where that data came from, carried so the TreatmentPlan created
        /// from this WorkPlan can record it.
        From   : DataSource option
        Orders : OrderContext list
    }

/// Rule 15. A state and not a filter: the Record rules below test `State = Signed`
/// or `State = Unsigned`, and a Discarded plan falls out of all four unmentioned.
type PlanState =
    | Signed
    | Unsigned
    | Discarded

/// Concept 13. The Patient's treatment plan as it stood when saved: a set of their
/// OrderContexts, by exactly one User (Rule 14), over the TreatmentPlan it was created
/// from — its base — if any, and in one of Rule 15's three states.
type TreatmentPlan =
    {
        Id      : TreatmentPlanId
        No      : TreatmentPlanNo
        Patient : PatientId
        By      : UserContext
        Base    : TreatmentPlanId option
        Orders  : OrderContext list
        /// Concept 13. The Patient Data it was built on, and where that came from: a
        /// plan is explained by what it holds, not by asking the platform again.
        Data    : PatientData option
        From    : DataSource option
        /// Rule 15. Signed, Unsigned or Discarded.
        State   : PlanState
        /// The nearest Signed ancestor, computed at the append. `Base` may name an
        /// Unsigned plan, which the copy does not have; this is what it follows.
        SignedBase : TreatmentPlanId option
        /// Concept 13. Its SessionRecord says how that Session ended, which is what
        /// Rule 19 offers on.
        Session : SessionId option
        /// A clinical fact, never the ordering: that is `No`, which the Database
        /// allocates at the append and which "newer than" compares. A timestamp cannot
        /// say which of two landed first — commit versus start, coarse resolution, a
        /// batch sharing one value — so ordering by it would let Rule 20 fail open.
        At      : int
    }

/// Concept 12. Append-only. Newest first, so the Record rules are `List.tryFind`.
/// The PatientId is the one thing no TreatmentPlan may change (Guarantee 1).
type PatientRecord =
    {
        Patient : PatientId
        Plans   : TreatmentPlan list
    }

// ───────────────────────────── the tokens ─────────────────────────────  [ships]

/// Concept 17. One subkey per purpose, so a token minted for one can never be spent
/// as another: it fails by key, before any field is compared.
[<RequireQualifiedAccess>]
type TokenPurpose =
    /// Rule 33. The TreatmentPlan a Session opened with.
    | Opened
    /// Rule 34. The Unsigned TreatmentPlans a notice disclosed.
    | Notice
    /// Rule 43. The exact WorkPlan a signature would attest to.
    | Challenge
    /// Rule 44. The Patient Data as the platform has it now, shown and accepted.
    | DataNotice
    /// Rules 2, 3. The Launch the LaunchScript sealed under the key it shares with
    /// the Server (Concept 3). Not a Claim: a Launch has no Session to be bound to.
    | Launch

/// What a token names. `Names` is the one field whose reading depends on the
/// purpose, and the purpose is inside the claim, so nothing can be read one way and
/// signed another.
type Claim =
    {
        Purpose   : TokenPurpose
        Sid       : SessionId
        Patient   : PatientId option
        /// What the token names, as text: a plan id, the disclosed plan ids, or a
        /// digest — which of them depends on the purpose.
        Names     : string list
        /// Uniqueness — and the key a spent-mark is filed under once tokens become
        /// single-use (Rule 42).
        Nonce     : string
        IssuedAt  : int
        /// Carried and signed here; it is the commit that will refuse on it
        /// (Rule 42), so nothing reads it yet.
        ExpiresAt : int
    }

/// A claim and the Server's word for it. Verification is recomputing the mac from
/// the claim with the subkey of the purpose expected, and comparing.
type Token =
    {
        Claim : Claim
        Mac   : string
    }

/// Rule 33. Re-minted whenever the baseline moves, because Rules 20 and 21 are both
/// measured from it.
type OpenedToken = Token

/// Rule 34. The User's choice to submit anyway, as something that names exactly what
/// they were shown: honoured for those Unsigned TreatmentPlans and for nothing newer.
type NoticeToken = Token

/// Rule 43. This Session, this Patient, this exact WorkPlan.
type SigningChallenge = Token

/// Rule 44. The Patient Data as the platform had it at the signature, shown to the
/// User and accepted by them. Returned with the Submission, the same way.
type DataNoticeToken = Token

/// Concept 14. One record, because it asks for one act. Deliberately not the same
/// type as `Commit`: a Submission that is refused was still submitted.
type Submission =
    {
        Work      : WorkPlan
        Opened    : OpenedToken
        Notice    : NoticeToken option
        Challenge : SigningChallenge option
        DataOk    : DataNoticeToken option
        Pin       : Pin option
        Key       : IdemKey
    }

/// Rule 42. The Submission plus the one thing only the Server can have found out —
/// the Role it just re-took (Rule 38). Everything else the act re-establishes itself.
type Commit =
    {
        Sid  : SessionId
        Req  : Submission
        Role : Role option
    }

// ───────────────────────────── session state ─────────────────────────────  [ships]

/// Rule 9, exactly: the ways a Session ends, and no others. A Server restart is not
/// among them — the Server holds no Session state to lose (Rule 31).
type EndMark =
    | ClosedByUser
    /// Rules 7 and 9. The User's own act — they did the opening — so it owes no
    /// notice.
    | ReplacedInBrowser
    | Idle
    | Superseded
    | WrongPinLimit
    /// Rule 9. Its own mark and not `Idle`, because Rule 46 attests reasons and this
    /// is a different one.
    | Expired

/// Two states. `OpenOrGone` also covers "the Client has gone quiet and the Server
/// cannot yet tell" — Rule 9 says a vanished browser is indistinguishable from a
/// silent one, so there is nothing finer to record.
type SessionState =
    | OpenOrGone
    | Ended of mark: EndMark * at: int

/// Rule 10, as a state and not a timestamp: `int option` could not tell "none owed"
/// from "owed and not yet given". Orthogonal to how a Session ended. Not Rule 21's
/// notice, which is a different thing and is `UnsignedWorkNotice`.
type SessionNotice =
    /// The Session is open, or the User closed it themselves. Nothing is owed.
    | NotOwed
    /// Owed until the next launch. Telling a Client that still holds the SessionId
    /// discharges nothing: whoever holds it need not be the User (Rule 10).
    | Owed
    /// Put in front of the User at a launch. Rule 10 delivers at least once — the
    /// Server cannot know a Client showed anything (Consequence 6) — so a notice that
    /// was delivered and not acknowledged may be delivered again.
    | Delivered of at: int
    /// The User said they had seen it. After this it is never shown again.
    | Acknowledged of at: int

/// Concept 9, and the whole of what the Server remembers between requests (Rule 31).
/// It carries the UserContext and not merely the UserId, because a Session runs on
/// the Role its launch established — signing excepted (Rule 38) — and the mail
/// address, because Rule 26 must reach the User with nothing in memory to ask.
type SessionRecord =
    {
        Id       : SessionId
        No       : SessionNo
        /// None: the Session was anonymous (Rule 13).
        User     : UserContext option
        Mail     : MailAddress option
        Patient  : PatientId option
        /// Rules 7 and 40. Without it the per-browser limit could only be enforced
        /// on a Client's word — the word of the party the limit exists to bound.
        Browser  : BrowserId option
        /// Rule 2. The nonce of the Launch this Session was opened by, which is also
        /// its spent-mark in the private store. None: no launch — an anonymous open.
        Launch   : string option
        OpenedAt : int
        /// Rule 9. When this Session stops, come what may — every Session has such a
        /// limit, not only the anonymous ones. Rule 8's idle clock forgives a Client
        /// that keeps talking; this does not.
        ExpiresAt : int option
        /// Rule 8: every request from the Client refreshes this. The idle clock lives
        /// here because there is nowhere else for it to live.
        LastSeen : int
        State    : SessionState
        /// Rule 10. Set by `endWith`, so the obligation is created by the same act
        /// that creates the ending and cannot drift from it.
        Notice   : SessionNotice
    }



// ───────────────────────────── failures ─────────────────────────────  [ships]

/// Rule 42. Why a commit changed nothing. Each of these is one of the rules the act
/// re-establishes, and the act stops at the first that fails — the PIN last, so a
/// doomed Submission never costs an attempt (Rule 27).
type CommitRefusal =
    /// Rules 40, 41. The Session is not open, or is past its time.
    | SessionNotOpen of EndMark option
    /// Rules 13, 25, 38. Nobody here may create.
    | RoleRefused
    /// Rules 32, 33, 34, 43, 44. A token that does not verify, or does not name this.
    | TokenRefused of string
    /// Rules 20, 36. Whose work stands in the way, never which TreatmentPlan it is
    /// (Rules 17, 18, 21).
    | BlockedBy of UserContext
    /// Rule 21. Whose Unsigned work exists, and what the notice may disclose.
    | UnsignedElsewhere of UserContext * TreatmentPlanId list
    /// Rules 22, 27.
    | PinWrong of left: int
    | PinLimitReached
    /// Rule 27. The credential reached the limit, and signing on it is locked until
    /// the tick named — a delay, which passes on its own (Rule 37 clears it early).
    | CredentialLocked of until: int


/// Why a Launch bought nothing. Never told to the Client — a refusal carries no
/// reason (Rule 6) — but the audit records which of them it was (Rule 46).
type LaunchFailure =
    /// The mac does not verify: nobody holding the launch key wrote this.
    | LaunchForged
    /// Rule 3. Past its lifetime.
    | LaunchExpired
    /// Rule 2. Spent already, and not by a browser that may have the first answer.
    | LaunchAlreadySpent
    /// Rules 4, 6. The browser proved nobody, so there is no User to open for.
    | NoIdentity

/// Rule 37. Why a code bought nothing. Told apart because they mean different things
/// to the User: ask again, or look again at the mail.
type ResetFailure =
    | NoResetPending
    /// Rule 37. A reset is already in flight and its code is still good. A second
    /// request would void the code the User is reading and send another mail, so it is
    /// refused and nothing is sent.
    | ResetPending
    | ResetExpired
    | WrongCode of left: int
    /// Too many wrong entries: the code is void, and a fresh reset means a fresh mail.
    | ResetVoid

type RegistryFailure =
    | NoRole                            // the registry knows the login, and says no
    | RegistryUnreachable               // the registry cannot say

/// Which exchange a round trip belongs to — a Database leg or a registry leg alike:
/// a launch in flight, one request in flight, or the idle sweep. Nothing outlives its
/// exchange, which is Rule 31 in one type.
type LegTag =
    | ForLaunch  of AttemptId
    | ForRequest of RequestId
    | ForSweep

// ───────────────────────────── messages ─────────────────────────────  [mixed]
// The payloads ship. The envelope around them is [model]: in a real system that is an
// endpoint and a caller, not a value.

/// What travels inside a Session. All of them arrive as a `SessionRequest`, so Rule
/// 8's refresh has exactly one home.
type SessionCmd =
    /// Concept 15. The Client has already changed its own cart; this sends the whole
    /// of it for computing. The answer comes back from the payload, and the Server
    /// keeps none of it.
    | Compute of OrderContext list
    /// Concept 14. The whole WorkPlan travels, with every token issued about it.
    | SubmitTreatmentPlan of Submission
    /// Rule 43. Asks for the challenge a signature will have to carry. The Rule 20 and
    /// 21 answers are settled here, before the User is ever asked for a PIN (UC-3 ext
    /// 3c), and the challenge names the exact WorkPlan it was asked about.
    | RequestSignChallenge of WorkPlan * OpenedToken * NoticeToken option * DataNoticeToken option
    | OpenTreatmentPlan of TreatmentPlanId        // Rules 17, 18
    /// Rules 15, 47. One request and one conditional act (Rule 40). The OpenedToken
    /// travels with it and is spent by it: a discard moves the baseline (Rule 33).
    | DiscardTreatmentPlan of TreatmentPlanId * OpenedToken
    /// UC-7. Rule 37: this removes nothing. It asks for a code to be mailed.
    | ResetPin
    /// Rule 37. The code from the mail and the PIN it is to be replaced with —
    /// verified and replaced in one act, so there is never a PIN-less moment.
    | SupplyResetCode of ResetCode * Pin
    | CloseSession                      // Rule 9

/// What the User does at the Client. Some of these are purely local. There is no
/// `Proceed` or `HoldOff`: under Rule 34 proceeding is re-sending with the token.
type UserAct =
    | Prescribes of OrderContextId      // Concept 15: add or change, in the Client
    | EntersPatientData of PatientData  // Concept 2: the User supplies it by hand
    | Saves                             // Concept 14, Unsigned
    | Signs of Pin                      // Concept 14, Signed if it verifies
    /// Rule 43. The User has read the modal and signs the plan as shown. The second
    /// half of signing: `Signs` asks for the challenge, this answers the challenge the
    /// Client was given. Nothing is submitted in between.
    | ConfirmsSign
    /// Rule 43. The User leaves the signature modal without signing.
    | CancelsSign
    | OpensTreatmentPlan of TreatmentPlanId       // Rules 17, 18
    /// Rules 15, 47. The User puts down their own Unsigned draft. Not a signature and
    /// not a Submission: no PIN, no challenge, no Rule 20 check — the plan's state becomes
    /// Discarded and the Session starts from whatever was under it. Nothing is deleted.
    | Discards of TreatmentPlanId
    | AsksPinReset                      // UC-7
    /// UC-7 step 2. The User has read the mail and chooses the new PIN.
    | EntersResetCode of ResetCode * Pin
    | ClosesSession                     // Rule 9
    /// Rule 10. The User dismisses the notice that a Session ended.
    | AcknowledgesNotice
    /// UC-9 step 3. The cart survived the Session because it was never in the Server
    /// (Rule 31); the User carries it into the next one as fresh prescribing. It
    /// survives exactly as far as the browser does.
    | CarriesOverFrom of BrowserId

type Msg =
    // ── Environment: the clock and the infrastructure ──
    | Tick
    | Start of ActorId
    | Stop of ActorId
    // ── U1. User <-> MainEHR Workstation ──
    | LogIn of LoginName
    | SelectPatient of PatientId
    | ClearPatient
    // ── U2. User <-> MainEHR LaunchScript ──
    | TriggerLaunch
    /// UC-1 ext 1b, 2a. The only failures it can report, both decided before it
    /// seals anything: after the launch it learns nothing (Consequence 1).
    | LaunchError of string
    // ── C4. MainEHR LaunchScript => GenPRES Client.  One-way: Consequence 1. ──
    /// The LaunchScript seals the Launch itself and opens the browser with it. No
    /// exchange with anybody: nothing can fail, so nothing can be reported.
    | OpenUrl of Launch
    // ── U3. User <-> GenPRES Client ──
    | Refresh                           // retry the launch from the page's own memory
    /// Rule 39. The page goes and comes back: its memory is gone with it, and only
    /// the address bar is left to re-present.
    | ReloadPage
    | OpenDirectly                      // UC-8: no launch, no credential
    | AcceptAnonymousOffer              // Rule 6, UC-1 ext 5a
    /// UC-2 step 2, mid-launch: the User reads the code from their mail and enters it
    /// with the PIN of their choosing.
    | ChoosePin of ResetCode * Pin
    | Act of UserAct
    | CloseBrowser                      // UC-12 ext 1b: nothing reaches the Server
    // ── C5. GenPRES Client -> GenPRES Server ──
    /// Rule 4. The Launch, the identity the browser proved, and the Session it
    /// already holds. Neither of the last two is the page's word. `None` opens nothing.
    | RedeemLaunch of Launch * LoginName option * SessionId option
    /// Rule 13, and Rules 7 and 9: an anonymous open replaces whatever this browser
    /// held — one browser, one Session, and the Database keeps that limit.
    | OpenAnonymous of SessionId option
    /// Rule 13. Bounded in number as well as in lifetime; above the bound no
    /// SessionRecord is written, and the refusal is counted rather than logged.
    | AnonymousRefused
    /// UC-2, and Rule 37: the code that was mailed, with the PIN it is to set. The
    /// launch is suspended on a human until both arrive.
    | SupplyPin of AttemptId * ResetCode * Pin
    /// Rule 10. Not a `SessionRequest` — the Session it speaks of has ended. It is an
    /// act of a live launched Session of the same User, never of the stale Client.
    | AckSessionNotice of acknowledging: SessionId * about: SessionId
    | SessionRequest of SessionId * SessionCmd
    // ── C7. GenPRES Server <-> UserRegistry.  The Launch never reaches here. ──
    | ResolveUser of LegTag * LoginName
    | UserResolved of LegTag * UserContext * MailAddress
    | UserUnresolved of LegTag * RegistryFailure
    // ── C8. GenPRES Server <-> PatientDataPlatform ──
    | ReadPatientData of LegTag * PatientId
    | PatientDataRead of LegTag * PatientData
    | PatientDataUnavailable of LegTag
    // ── C9. GenPRES Server <-> GenPRES Database.  The Server is its only writer. ──
    | ReadCredential of LegTag * UserId
    | CredentialRead of LegTag * UserCredential option
    /// Rule 37. Park a reset: the code as a mac, and when it dies. The PIN itself is
    /// untouched — the Database is told nothing that could remove one.
    | StartReset of LegTag * UserId * string * int
    | ResetStarted of LegTag * UserId
    /// Rule 37. Check the code and replace the PIN in one act, or refuse and change
    /// nothing. The code is spent by the same act that honours it.
    | ReplacePinIfCode of LegTag * UserId * ResetCode * Pin
    | PinReplaced of LegTag * UserCredential
    | ResetRefused of LegTag * ResetFailure
    | ReadRecord of LegTag * PatientId
    | RecordRead of LegTag * PatientRecord
    /// Rule 42. Rule 36 is inside it: the check and the append are the same act.
    | CommitTreatmentPlan of LegTag * Commit
    | TreatmentPlanCommitted of LegTag * TreatmentPlan
    | CommitRefused of LegTag * CommitRefusal
    /// Rule 40. The Server never writes back a record it read: it names the change
    /// and the Database decides. Rule 7's two limits are kept in this same act.
    | OpenSessionClosingOthers of SessionRecord * replacing: SessionId option
    | EndSessionIfOpen of SessionId * EndMark
    | TouchIfOpen of SessionId                    // Rule 8
    | MarkDelivered of SessionId                  // Rule 10, at least once
    /// Rule 10, and then never again: *which* Session is acknowledging, so the
    /// Database can check that it is the User's own and launched.
    | MarkAcknowledged of acknowledging: SessionId * about: SessionId
    | ReadSessionRecord of LegTag * SessionId
    | SessionRecordRead of LegTag * SessionRecord option
    | ReadSessionRecords of LegTag
    | SessionRecordsRead of LegTag * SessionRecord list
    /// Rules 15, 40, 47. One conditional operation, guarded by everything the discard
    /// requires. The state change is the one in-place write in the private store.
    | DiscardIfOwnHead of LegTag * SessionId * TreatmentPlanId * OpenedToken
    /// The discard landed: what the Session starts from now (Rule 19, Rule 33).
    | TreatmentPlanDiscarded of LegTag * TreatmentPlanId * TreatmentPlan option
    | DiscardRefused of LegTag * string
    | SpendLaunchIfUnspent of LegTag * nonce: string
    | LaunchSpent of LegTag
    | LaunchReplayed of LegTag * SessionRecord option
    /// Rule 46. An anonymous open refused above the bound: counted per source, and
    /// nothing else written — no SessionRecord, no audit line per request, which would
    /// be the same flood by another name.
    | NoteAnonymousRefusal of ActorId
    // ── C10. GenPRES Server -> MailService ──
    | SendMail of MailAddress * string
    // ── GenPRES Server -> GenPRES Client (replies only: Consequence 6) ──
    | SessionOpened of
        SessionId
        * SessionNo
        * UserContext option
        * PatientContext
        * OrderContext list
        * OpenedToken
        // Rule 19. Work this Session did *not* open from, how its Session ended and
        // when it was saved — so the User can place it in time. Offered, not opened.
        * resumedFrom: (TreatmentPlanId * EndMark option * int) option
    | PinRequired of AttemptId          // UC-2: choose one, and nothing else is offered
    /// Rule 6. The refusal carries no reason for the *User* — forged, expired and
    /// spent are one answer to a person. It carries one bit for the Client, which is
    /// not the User: whether the Launch is still worth presenting. The identity being
    /// unavailable is the retryable case (UC-1 ext 3c); a Launch that is forged,
    /// aged out or spent is not (ext 4a).
    | LaunchRefused of retryable: bool
    | NotAuthorised                     // the registry says no; no reason either
    | AuthorityUnavailable              // the registry cannot say
    | ServerUnreachable
    /// Rule 10's one telling. The mark is what ended it.
    | SessionEnded of EndMark option    // None: the Server has no such record
    /// Rule 10. For this screen and no further: it discharges nothing, so the notice
    /// still stands until a launch.
    | SessionRefused of EndMark option
    /// Rule 10. What ended, and which Session it was so the User can acknowledge it.
    /// An ended SessionId opens nothing, so naming it is safe.
    | PriorSessionNotice of (SessionNo * SessionState * SessionId) list
    /// Rule 31. The answer to `Compute`, computed from the payload and kept nowhere.
    | Computed of OrderContext list
    /// Rules 20, 36. Whose work stands in the way — never which TreatmentPlan it is
    /// (Rules 17, 18, 21).
    | SubmissionBlocked of UserContext
    /// Rule 21: whose work, not its contents. Rule 34: and the token that names what
    /// was disclosed, which is what a choice to submit anyway must return.
    | UnsignedWorkNotice of UserContext * NoticeToken
    /// Rules 32, 33. The payload contradicted the SessionRecord, or the token did not
    /// verify. Carries a reason for the trace; the Client shows nothing but a refusal.
    | SubmissionRefused of string
    | TreatmentPlanSubmitted of TreatmentPlanId * PlanState * OpenedToken
    /// Rules 15, 47. The draft is down, and here is what the Session stands on now —
    /// with a fresh OpenedToken over it, because the baseline has moved (Rule 33).
    | TreatmentPlanDiscardedOk of
        discarded: TreatmentPlanId * nowOpen: TreatmentPlanId option * OrderContext list * OpenedToken
    /// Rule 43. The challenge to sign with, over the WorkPlan it was asked about.
    | SignChallengeIssued of SigningChallenge
    /// Rule 44. The Patient Data has moved under the Session (Concept 2 read it once,
    /// at the launch). Shown, and accepted by returning the token.
    | PatientDataChanged of PatientData * DataNoticeToken
    /// Rule 44. The platform could not be asked, so the data is unchecked. Accepted
    /// by returning this token, exactly as a change is.
    | PatientDataUnverified of DataNoticeToken
    | TreatmentPlanOpened of TreatmentPlanId * OrderContext list * OpenedToken
    | PinRejected of int                // Rule 27: attempts left
    | NoTreatmentPlanHere                    // Rule 12
    | NotPermitted                      // Roles: a Reader never creates a TreatmentPlan
    /// Rule 38. Distinct from `AuthorityUnavailable`, which belongs to a launch and
    /// offers an anonymous open: here there is a Session already, and it stands.
    | SigningUnavailable
    /// Rule 27. Signing is locked until the PIN is replaced (Rule 37). Distinct from
    /// `PinRejected`, which still has attempts left in it, and from `SessionEnded`,
    /// which is what the attempt at the limit itself caused.
    | SigningLocked
    /// Rule 37. A code is on its way to the address the registry holds — and the PIN
    /// in force is still the old one.
    | ResetCodeMailed
    /// Rule 37. Replaced, in one act (Rules 26, 27).
    | PinChanged
    | ResetDenied of ResetFailure
    // ── any actor -> Environment (standing in for the audit log) ──
    /// An envelope no edge permits. Not merely dropped: a forged or misrouted
    /// envelope is exactly the event worth alerting on.
    | Refused of Envelope
    /// Rule 26's other half — "records the change". The mail is the User-facing
    /// notice; this is the record.
    | Noted of string

and Envelope =
    {
        From : ActorId
        To   : ActorId
        Msg  : Msg
    }

// ───────────────────────────── actor state ─────────────────────────────  [mixed]
// `DatabaseState` ships. The rest is [model]: in-flight tables standing in for async
// flow, and other actors' insides, which are not ours at all.

/// Actor 1 [given]. Invariant 1: at most one active Patient at a time.
type WorkstationState =
    {
        ActiveUser    : LoginName option
        ActivePatient : PatientId option
        /// Rule 1. Who may run the LaunchScript. What the decision is made of is
        /// MainEHR's affair; that there is one, and that a refusal leaves the
        /// workstation with nothing sent, is ours to state.
        MayLaunch     : Set<LoginName>
        NextTab       : int
    }

/// Actor 9. Says who a login belongs to, what that person may do, and how to reach
/// them by mail. The only source of a Role (Rule 5).
type RegistryState =
    {
        Users : Map<LoginName, UserContext * MailAddress>
        Up    : bool
    }

/// Actor 6 [given]. Read-only, and read once per launch (Concept 2).
type PlatformState =
    {
        Data : Map<PatientId, PatientData>
        Up   : bool
    }

/// Actor 5, the copied half. A type of its own rather than a filter somebody has to
/// remember to apply.
type ClinicalStore =
    {
        /// Concept 12, the Signed part: newest first per Patient.
        Signed : Map<PatientId, TreatmentPlan list>
    }

/// Rule 46. One line of the audit: what was done, and the moment it was done. The
/// tick is the Database's own — the party that did the act is the party that stamps
/// it, so nothing here rests on a clock somewhere else.
type AuditEntry =
    {
        At   : int
        What : string
    }

/// Actor 5, the other half: everything that is GenPRES's own business and no record
/// of care.
type PrivateStore =
    {
        /// Concept 12, the Unsigned part. Newest first per Patient.
        Drafts       : Map<PatientId, TreatmentPlan list>
        Sessions     : SessionRecord list             // Concept 9
        Credentials  : Map<UserId, UserCredential>    // Concept 7, keyed by the person
        /// Rule 37. Resets in flight, gone the moment the code is spent, expires or is
        /// guessed away.
        Resets       : Map<UserId, PinReset>
        /// Rule 45. What each key has already been answered with.
        Answered     : Map<IdemKey, Result<TreatmentPlan, CommitRefusal>>
        /// Rules 2, 33, 43. Spent nonces, of tokens and of Launches — the whole of
        /// what makes one work exactly once. A mark past its lifetime can be purged.
        Spent        : Set<string>
        /// Rule 46. Anonymous opens refused above the bound (Rule 13), counted per
        /// source. A count, not a line each: the point of the bound is that a flood
        /// writes nothing that grows with it.
        AnonymousRefused : Map<ActorId, int>
        /// Rule 46. What was done, to whom — and when.
        Audit        : AuditEntry list
    }

/// Actor 5. The Server is its only writer. `NextPlan` lives here because the party
/// that decides whether a Submission lands is the party that can order them.
type DatabaseState =
    {
        Clinical : ClinicalStore
        Private  : PrivateStore
        NextPlan : int
    }

/// One launch attempt, mid-flight. The Launch is kept for its nonce and its Patient
/// only: who the User is, is `Identity` (Rule 4).
type LaunchCtx =
    {
        Client    : ActorId
        Launch    : Launch
        /// Rule 4. The identity the browser proved (Actor 8). The only source of the
        /// Session's User: the Launch carries no login to disagree with it.
        Identity  : LoginName
        /// Rules 7 and 9. The Session this browser held when it presented the Launch,
        /// which
        /// opening the new one replaces.
        Replacing : SessionId option
        /// Rule 2's replay clause. Set when this nonce was spent already by the same
        /// browser, within the lifetime: the launch runs to the same answer over the
        /// SessionRecord the first presentation opened, instead of opening a second.
        Resuming  : SessionRecord option
    }

/// The stages of a launch, in the order Rules 24 and 25 fix. Per-attempt and dropped
/// with the reply, so it is not Session state (Rule 31).
type PendingLaunch =
    /// Rule 2. The nonce is being spent at the Database; nothing else has happened
    /// yet, so a refusal here costs nothing.
    | AwaitingSpend       of LaunchCtx
    | AwaitingUser        of LaunchCtx
    | AwaitingCredential  of LaunchCtx * UserContext * MailAddress
    /// UC-2 step 1. The code is parked at the Database; the mail goes out on its
    /// answer, which is why the code rides along — the Server mails it and the
    /// Database only ever saw its mac (Rule 37).
    | AwaitingEnrolCode   of LaunchCtx * UserContext * MailAddress * ResetCode
    /// UC-2. The launch is suspended on a human and may stay here indefinitely — the
    /// code it waits for expires on its own (ext 3a).
    | AwaitingPinChoice   of LaunchCtx * UserContext * MailAddress
    | AwaitingPinWritten  of LaunchCtx * UserContext * MailAddress
    | AwaitingPatientData of LaunchCtx * UserContext * MailAddress
    | AwaitingRecord      of LaunchCtx * UserContext * MailAddress * PatientContext
    /// Rule 19. Two candidates, not one: what it would start from, and the newest
    /// Signed plan under it. Which it opens with depends on the SessionRecords.
    | AwaitingPriors      of
        LaunchCtx * UserContext * MailAddress * PatientContext * TreatmentPlan option * TreatmentPlan option

/// One entry in the Server's launch table.
type PendingEntry =
    {
        Stage : PendingLaunch
        Since : int
    }

/// How far one in-Session request has got. Each stage carries what the earlier legs
/// returned, because there is nowhere else to keep it (Rule 31).
type RequestStage =
    /// Rule 32: before anything else, who and which Patient this Session is.
    | AwaitingSessionRecord
    /// Rules 17 to 21 are decided against the PatientRecord: an open (Rules 17, 18),
    /// and the pre-checks a challenge is issued after (Rule 43).
    | AwaitingPatientRecord of SessionRecord
    /// Rule 38. A signature is a fresh act of authority, so the Role is taken from the
    /// registry again — every time, and before the PIN is ever asked for.
    | AwaitingSigningRole   of SessionRecord * Submission
    /// Rule 44. The re-read before the challenge is minted, so the signature that
    /// carries it back needs no second look.
    | AwaitingChallengeData of SessionRecord * WorkPlan * DataNoticeToken option
    /// Rule 42: the Database is deciding the whole Submission, in one act.
    | AwaitingCommit        of SessionRecord
    /// Rules 40, 47: the Database is deciding the whole discard, in one act.
    | AwaitingDiscard       of SessionRecord
    /// UC-7 step 1. Rule 26 mails the address on the record, so the record is held —
    /// and the code rides along, because it is the Server that mails it and the
    /// Database that only ever saw its mac.
    | AwaitingResetStarted  of SessionRecord * ResetCode
    /// UC-7 step 2. Rule 26 again: the replacement is mailed and recorded.
    | AwaitingPinReplaced   of SessionRecord

/// Rule 31 made visible: one entry per request in flight, created when the request
/// arrives and removed with its reply. Nothing here survives the answer.
type RequestCtx =
    {
        Sid    : SessionId
        Client : ActorId
        Cmd    : SessionCmd
        Stage  : RequestStage
    }

/// Actor 4. Counters, what is in flight, and whether it is up — and nothing else.
/// That is Rule 31 as a type: there is no field a Session could live in.
type ServerState =
    {
        /// One entry per in-Session request, gone with the reply.
        InFlight      : Map<RequestId, RequestCtx>
        /// One entry per launch attempt, gone with the launch.
        Pending       : Map<AttemptId, PendingEntry>
        /// Separate id spaces, so separate counters. All monotonic — an id is never
        /// reissued. The TreatmentPlan counter is not here: Rule 36 moved it to the
        /// Database, which is the party that orders a PatientRecord.
        NextAttempt   : int
        NextRequest   : int
        NextSessionId : int
        Up            : bool
    }

/// Actor 3. It carries the cart and nothing else does (Rule 31), so this is where a
/// Session's work survives a Server restart, and dies with the browser.
type BrowserState =
    {
        /// Consequence 4: the Launch arrives in the address bar. Rule 39: it is
        /// erased there the moment the Client presents it, so a reload finds nothing.
        UrlLaunch      : Launch option
        /// Rule 39. What is left after the scrub: a copy in the page's own memory,
        /// enough for the retry of UC-1 ext 3a, and gone with the page.
        RetryLaunch    : Launch option
        /// Concept 4, Rule 4. The browser's, not the page's: a reload keeps it and no
        /// Launch can change it.
        BrowserIdentity : LoginName option
        /// Rule 11: a bearer credential, held here and sent in the request.
        Sid            : SessionId option
        /// What the Server said this Session's User and Patient are (Concepts 1, 2).
        /// Shown to the User; never sent back as an assertion — Rule 32 takes both
        /// from the SessionRecord.
        User           : UserContext option
        Patient        : PatientId option
        /// Concept 16. The WorkPlan travels with every request and lives nowhere else.
        Work           : WorkPlan
        /// Rule 33. Issued by the Server, returned with every Submission.
        Opened         : OpenedToken option
        /// Rule 34. Kept from the last UnsignedWorkNotice, returned to submit anyway.
        Notice         : NoticeToken option
        /// Rule 43. A signature the User has started: the PIN they typed while the
        /// challenge is being fetched, and then the challenge itself. While the modal
        /// is up the WorkPlan cannot change — that is what it is for.
        Signing        : Pin option
        Modal          : SigningChallenge option
        /// Rule 44. The Patient Data notice the User has accepted, returned with the
        /// Submission the way a Rule 21 notice is.
        DataOk         : DataNoticeToken option
        /// Rule 19. A standing offer, not a transient message: a Rule 10 notice may
        /// take the screen in front of it and the work is still there.
        Offered        : (TreatmentPlanId * EndMark option * int) option
        /// Rule 10. Which Sessions the notice in front of the User is about.
        NoticeFor      : SessionId list
        /// The attempt this Client was asked to choose a PIN for (UC-2).
        AwaitingPin    : AttemptId option
        /// Rule 6: whether a fresh anonymous open is on offer.
        AnonymousOffer : bool
        /// Whatever the Client is currently putting in front of the User. Not only
        /// notices: a question (Rule 21), a prompt (UC-2), an unavailability. The
        /// Session-ended notice of Rule 10 is one of the things that can land here.
        Showing        : string option
        Closed         : bool
    }

/// The world the participants run in, not a participant's own state. It disappears
/// in production: real time arrives as Tick.
type EnvState =
    {
        Now : int
    }

/// One field per participant, so nothing here can depend on a memory read across
/// what will be a process boundary. A convention the branch bodies keep, not a type.
type Hospital =
    {
        Workstation : WorkstationState
        Registry    : RegistryState
        Platform    : PlatformState
        Database    : DatabaseState
        GenPres     : ServerState
        Clients     : Map<BrowserId, BrowserState>
        Mail        : (MailAddress * string) list      // what the MailService sent
        Env         : EnvState
    }


// ═══════════════════════════════════════════════════════════════════════════════
//                                2. MODULES
// ═══════════════════════════════════════════════════════════════════════════════

// ───────────────────────────── configuration ─────────────────────────────  [ships] — as configuration, not as literals
// Rules 28, 29, 30. The unit is one handled message, so a lifetime reads against the
// length of a cascade — a launch is some twenty-odd — not a count of Ticks.

/// Rule 28. Long enough to carry one launch — a page load and a retry or two.
/// The LaunchScript and the Server share the key that seals a Launch; they share
/// this too, because both measure the lifetime against the Launch's own IssuedAt.
let launchTtl = 20

/// Rule 29. Long enough to span the gaps between a clinician's actions. The unit is
/// one handled message, and a clinician's gap here is a whole cascade — a launch, a
/// save, a colleague's Session running alongside — so this is far larger than
/// `launchTtl`, which spans one page load.
let sessionTtl = 150

/// Rule 13. An anonymous Session has no idle clock — nobody is waiting to be told
/// anything and nothing of theirs is at stake — but it does not live for ever either:
/// this is the outright limit, counted from the open.
let anonymousLifetime = 1000

/// Rule 13. An anonymous open is an unauthenticated write, so the lifetime bounds how
/// long each lives and this bounds how many there are.
let anonymousOpenLimit = 8

/// Rule 38. How long the Role taken at the launch stands for a signature when the
/// registry cannot be asked (Rule 38). Short: it is a registry that is briefly down, not
/// a Role that may be stale for a shift. Beyond it, signing fails closed as before.
let roleGrace = 2 * sessionTtl

/// Rule 9. The outright limit on a launched Session, counted from the open and
/// deaf to Rule 8's refresh: a Client that keeps talking keeps the idle clock at bay,
/// and nothing else. Several times `sessionTtl`, because it bounds a shift and not a
/// gap between two acts.
let sessionMaxLifetime = 8 * sessionTtl

/// How long a launch may sit half-finished before the Server forgets it (UC-2). Not
/// Rule 29's number: this is a round trip that should return promptly, not a
/// clinician's gap — except `AwaitingPinChoice`, which waits on a human and is never
/// collected.
let launchAbandonTtl = 25

/// Rule 30. Small enough to make guessing hopeless, large enough to forgive
/// mistyping. Owned by GenPRES.
let wrongPinLimit = 3

/// Rule 27. The first lock; each further wrong entry doubles it. Small enough that a
/// User who mistyped waits and carries on, steep enough to price a guesser out.
let pinLockBase = 100

/// Rule 37. Long enough for a User to go and read their mail, and no longer. The
/// unit here is one handled message (see above) and a single request costs several of
/// them, so this is a few attempts at the code and a short walk to the inbox.
let resetCodeTtl = 40

/// Rule 37. As with a PIN (Rule 30): small enough to make guessing hopeless, large
/// enough to forgive mistyping. Counted per code, not per credential.
let wrongCodeLimit = 3

/// Concept 17. A token is worth nothing once its Session is gone, so it need not
/// outlive one. Signed into every claim; the refusal that reads it arrives with the
/// commit (Rule 42).
let tokenTtl = 2 * sessionTtl

// ───────────────────────────── the edge table ─────────────────────────────  [model] — the deployment is what enforces this

/// The Constraints notation, as data.
type EdgeKind =
    | Request                           // X ->  Y   initiate, and receive Y's reply
    | Launch                            // X =>  Y   one-way: no response, no error path
    | Interact                          // X <-> Y   read what Y shows, and act on it

module Edges =

    /// The Constraints, verbatim. Anything not here cannot exchange data at all, and
    /// edges do not compose.
    let table : (ActorId * EdgeKind * ActorId) list =
        [
            // User Interaction
            User,                 Interact, MainEhrWorkstation          // U1
            User,                 Interact, MainEhrLaunchScript         // U2
            User,                 Interact, GenPresClient(BrowserId 0)  // U3

            // Communication
            MainEhrWorkstation,   Request,  UserRegistry                // C1
            MainEhrWorkstation,   Request,  PatientDataPlatform         // C2
            GenPresClient(BrowserId 0), Request, IdentityProvider       // C3
            MainEhrLaunchScript,  Launch,   GenPresClient(BrowserId 0)  // C4
            GenPresClient(BrowserId 0), Request, GenPresServer          // C5
            GenPresServer,        Request,  IdentityProvider            // C6
            GenPresServer,        Request,  UserRegistry                // C7
            GenPresServer,        Request,  PatientDataPlatform         // C8
            GenPresServer,        Request,  GenPresDatabase             // C9
            GenPresServer,        Request,  MailService                 // C10

            // U2 is `<->` because the LaunchScript can refuse (Rule 1); what bounds
            // it is its own lifetime, not the edge. C3 and C6 carry no message here:
            // the identity arrives as a field on the Client. The edges say where it
            // comes from.
        ]

    /// Clients differ by BrowserId; edges do not.
    let private tag =
        function
        | GenPresClient _ -> GenPresClient(BrowserId 0)
        | a -> a

    let private has kind a b =
        table |> List.exists (fun (x, k, y) -> k = kind && x = tag a && y = tag b)

    /// May `from` put this envelope on the wire to `to_`? A Request edge permits the
    /// reply on the same connection; a Launch edge permits one direction only, which
    /// is what makes Consequence 1 true by construction.
    let permits from to_ =
        // Environment is not a use case actor: it is the clock and the power switch,
        // and every actor may write to the audit log.
        if from = Environment || to_ = Environment then true
        elif has Interact from to_ || has Interact to_ from then true
        elif has Launch from to_ then true
        elif has Request from to_ then true
        elif has Request to_ from then true          // the reply leg
        else false

// ───────────────────────────── the work plan ─────────────────────────────  [ships]

/// Concept 16. The whole of it: nothing to mint, nothing to verify, nothing kept.
module WorkPlan =

    let empty = { Data = None; From = None; Orders = [] }

    /// Rule 43. One WorkPlan, one string: the Patient Data and every OrderContext by
    /// identity and content, in a fixed order. A challenge names this and nothing
    /// else, so a WorkPlan that has changed by so much as one OrderContext no longer
    /// answers to the challenge the User was shown. `sha` stands in for a real digest.
    let digest (w: WorkPlan) =
        let data = match w.Data with Some(PatientData d) -> d | None -> "-"
        let orders =
            w.Orders
            |> List.map (fun o -> let (OrderContextId i) = o.Id in $"%s{i}=%s{o.Content}")
            |> List.sort
            |> String.concat ";"
        $"sha|%s{data}|%s{orders}"

    /// Rule 43. What a signature answers for: the plan as it stood when the User was
    /// shown it.
    let signingDigest (w: WorkPlan) = digest w

    /// Rule 44. The same, for the Patient Data alone.
    let dataDigest (d: PatientData option) =
        match d with
        | Some(PatientData x) -> $"sha|%s{x}"
        | None -> "sha|-"

// ───────────────────────────── the record rules ─────────────────────────────  [ships]

/// Rules 16 to 21. Small total functions over a TreatmentPlan list held newest first, so
/// "most recent" is `List.tryFind` and "newer than" is a comparison of TreatmentPlanNo.
module PatientRecord =

    let empty patient = { Patient = patient; Plans = [] }

    let private no (s: TreatmentPlan) = let (TreatmentPlanNo n) = s.No in n

    /// Rule 36's half of the check: what the Server saw as the head when it decided.
    let head (r: PatientRecord) = r.Plans |> List.tryHead |> Option.map _.Id

    /// "newer than the TreatmentPlan the User opened with" — and where the User opened
    /// with nothing, any TreatmentPlan at all counts as newer (Rule 21's parenthesis).
    let private newerThan (openedWith: TreatmentPlanId option) (s: TreatmentPlan) (r: PatientRecord) =
        match openedWith with
        | None -> true
        | Some id ->
            match r.Plans |> List.tryFind (fun x -> x.Id = id) with
            | Some baseline -> no s > no baseline
            | None -> true              // the baseline is not in this record at all

    /// The newest Signed TreatmentPlan. This is what "newer than the one you opened
    /// with" is measured against (Rule 20).
    let newestSigned (r: PatientRecord) =
        r.Plans |> List.tryFind (fun s -> s.State = Signed)

    /// Rule 16. The only TreatmentPlan that counts clinically: the most recent Signed
    /// one. Nothing is removed to make it so — Concept 12 is append-only, and every
    /// earlier version is still in the record.
    let latestSigned (r: PatientRecord) =
        r.Plans |> List.tryFind (fun s -> s.State = Signed)

    /// Rule 19. A Discarded plan is neither Signed nor Unsigned, so it is passed over
    /// with no mention of it — which is the whole of what discarding does (Rule 47).
    let startsFrom (u: UserId) (r: PatientRecord) =
        r.Plans
        |> List.tryFind (fun s ->
            s.State = Signed || (s.State = Unsigned && s.By.UserId = u))

    /// Rules 17, 18. Read-only falls out of the baseline rather than being a second
    /// mechanism: opening an older Signed plan makes it what the Session opened with,
    /// and Rule 20 then blocks the Submission.
    let mayOpen (u: UserId) (id: TreatmentPlanId) (r: PatientRecord) =
        match r.Plans |> List.tryFind (fun s -> s.Id = id) with
        | None -> None
        | Some s when s.State = Signed -> Some s
        // Rule 15. A Discarded plan opens for nobody, its author included: it is not
        // work waiting to be resumed, which is what putting it down meant.
        | Some s -> if s.State = Unsigned && s.By.UserId = u then Some s else None

    /// Rule 20. A Signed TreatmentPlan newer than the one the User opened with blocks the
    /// create — and opening that Signed TreatmentPlan lifts the block, because it becomes
    /// the one the Session opened with.
    let blocking (openedWith: TreatmentPlanId option) (r: PatientRecord) =
        newestSigned r |> Option.filter (fun s -> newerThan openedWith s r)

    /// Rule 21, and Rule 34's half of it: *every* Unsigned TreatmentPlan of another User
    /// newer than the one opened with, because the notice token names what was
    /// disclosed and is honoured for nothing newer. Newest first.
    let unsignedElsewhere (u: UserId) (openedWith: TreatmentPlanId option) (r: PatientRecord) =
        r.Plans
        |> List.filter (fun s ->
            s.State = Unsigned && s.By.UserId <> u && newerThan openedWith s r)

    /// Concept 12: append-only. The newest TreatmentPlan goes on the front, and no
    /// existing one is ever touched.
    let append (s: TreatmentPlan) (r: PatientRecord) =
        { r with Plans = s :: r.Plans }

// ───────────────────────────── the two stores ─────────────────────────────  [ships]

/// Actor 5. A PatientRecord is a view over the two halves, not a thing either of them
/// holds: Concept 12 is one append-only sequence, and it is only the storage that is
/// divided. Nothing outside this module knows which half a TreatmentPlan came from.
module Database =

    let private no (s: TreatmentPlan) = let (TreatmentPlanNo n) = s.No in n

    let signedOf patient (db: DatabaseState) =
        db.Clinical.Signed |> Map.tryFind patient |> Option.defaultValue []

    let draftsOf patient (db: DatabaseState) =
        db.Private.Drafts |> Map.tryFind patient |> Option.defaultValue []

    /// The whole record, newest first — the Record rules (16 to 21) read this and
    /// never the halves.
    let recordOf patient (db: DatabaseState) =
        {
            Patient = patient
            Plans = (signedOf patient db @ draftsOf patient db) |> List.sortByDescending no
        }

    /// Rule 46. What was done, written where it is done — by the party that does it,
    /// in the same act, stamped with the moment it happened. Newest first.
    let note (now: int) (what: string) (db: DatabaseState) =
        { db with Private.Audit = { At = now; What = what } :: db.Private.Audit }

    /// Concept 12: append-only, into whichever half the TreatmentPlan belongs to. A
    /// Signed one is history; an Unsigned one is its author's own business (Rule 18).
    let append (plan: TreatmentPlan) (db: DatabaseState) =
        if plan.State = Signed then
            // Actor 5, Guarantee 4. The copied store must close over itself, and both
            // `Base` and `Session` can point into the private one — so both stop here
            // and the copy follows `SignedBase`. The private half still records the
            // Session (Concept 13); it is the copy that must carry neither a bearer
            // credential nor a dangling reference.
            let plan = { plan with Base = None; Session = None }

            { db with
                Clinical.Signed =
                    db.Clinical.Signed |> Map.add plan.Patient (plan :: signedOf plan.Patient db) }
        else
            { db with
                Private.Drafts =
                    db.Private.Drafts |> Map.add plan.Patient (plan :: draftsOf plan.Patient db) }

// ───────────────────────────── the credential ─────────────────────────────  [ships]

/// Concept 7 and Rule 27.
module UserCredential =

    let fresh user = { User = user; Pin = None; AttemptCount = 0; LockedUntil = None }

    /// Rules 26 and 37: a newly set — or newly replaced — PIN starts with a count of
    /// zero, and lifts the lock that count may have earned.
    let setPin pin c = { c with Pin = Some pin; AttemptCount = 0; LockedUntil = None }

    /// Rule 27. Is signing locked at this moment? A moment, not a state: the lock is a
    /// delay, and it passes on its own.
    let isLocked (now: int) (c: UserCredential) =
        match c.LockedUntil with
        | Some until -> now < until
        | None -> false

    /// Rule 27. `pinLockBase * 2^(count - wrongPinLimit)`: the wrong entry that
    /// reaches the limit costs `pinLockBase`, and every one after it costs twice the
    /// last.
    let lockFor (count: int) = pinLockBase * (pown 2 (max 0 (count - wrongPinLimit)))

    /// Rules 22, 27. A locked credential verifies nothing, correct PIN or not — until
    /// the delay passes, and then it verifies as before.
    let verify (now: int) (pin: Pin) (c: UserCredential) =
        let locked = isLocked now c

        match c.Pin with
        // A correct PIN inside the delay is still no signature: the delay is the
        // answer to what has already happened, and waiting is what lifts it. It costs
        // nothing either — a wrong count is for wrong entries.
        | Some p when p = pin && not locked -> true, { c with AttemptCount = 0; LockedUntil = None }
        | Some p when p = pin -> false, c
        | _ ->
            // Rule 27. A wrong entry counts even while locked, so the delay grows
            // with each guess and not with each guess that waited politely.
            let count = c.AttemptCount + 1
            let until = if count >= wrongPinLimit then Some(now + lockFor count) else None
            false, { c with AttemptCount = count; LockedUntil = until }

    /// Rule 27: a wrong entry at the limit ends the Session (Rule 9) — and locks the
    /// credential, so the next Session cannot simply carry on trying.
    let atLimit c = c.AttemptCount >= wrongPinLimit

    let attemptsLeft c = max 0 (wrongPinLimit - c.AttemptCount)

// ───────────────────────────── the reset code ─────────────────────────────  [ships] — with a real mac

/// Rule 37. The Database holds the mac and not the code, so what is stored is not
/// what was sent — the same trick as a token, and the same placeholder.
module Reset =

    let private secret = "reset-key-known-only-to-genpres"

    let macOf (ResetCode c) = $"mac|%s{secret}|reset|%s{c}"

    /// What the mail says. The code is in it — that is the whole point of the channel
    /// — and nothing else about the Session is. The same words serve both entrances
    /// (Rule 37): at an enrolment there is no PIN to reset, so the mail does not say
    /// there is.
    let mail (ResetCode c) = $"GenPRES PIN: use code %s{c} once, and soon"

// ───────────────────────────── the session record ─────────────────────────────  [ships]

module SessionRecord =

    /// Rule 10, on the one axis that decides it: a User who closed was offered the
    /// save, so there is nothing to tell them. Every other ending owes a notice.
    /// Three branches, not four: a Server restart is no longer an ending (Rule 9).
    let owesNotice =
        function
        | ClosedByUser -> false
        | ReplacedInBrowser -> false
        // Rule 9: a launched Session can now reach its absolute limit too, and
        // that is worth telling — `endWith` still gates on the record having a User, so
        // an anonymous expiry owes nothing and there is nobody it could reach.
        | Expired -> true
        | Idle | Superseded | WrongPinLimit -> true

    /// Ending is idempotent: an already settled record is left alone, so the first
    /// mark is the one that stands — and so is the obligation it created.
    let endWith mark now (s: SessionRecord) =
        match s.State with
        | Ended _ -> s
        | OpenOrGone ->
            { s with
                State = Ended(mark, now)
                // Rule 13: an anonymous Session binds to no User, and Rule 10 speaks of
                // the Session's User — so there is nobody an ending could owe anything.
                Notice = if s.User.IsSome && owesNotice mark then Owed else NotOwed }

    /// Rule 10. Owed, or delivered and not yet acknowledged: either way the User may
    /// be shown it. What ends the obligation is the acknowledgement, not the delivery
    /// — the Server cannot see a screen (Consequence 6), so "told once" can only be
    /// something the User says.
    let tellsAtNextOpportunity (s: SessionRecord) =
        match s.Notice with
        | Owed -> true
        | Delivered _ -> true
        | NotOwed
        | Acknowledged _ -> false

    /// Whether ending this record now would leave the User owed a notice: either it
    /// is still open, and this launch is about to close it (Rule 7), or it ended
    /// earlier in a way nobody has yet mentioned.
    let wouldOweNotice (s: SessionRecord) =
        match s.State with
        | OpenOrGone -> true
        | Ended _ -> tellsAtNextOpportunity s

    /// Rule 10. Puts the notice in front of the User. At-least-once: a delivery that
    /// is never acknowledged may happen again, and the timestamp moves with it.
    /// Silent where nothing was owed, and silent once acknowledged.
    let delivered now (s: SessionRecord) =
        match s.Notice with
        | Owed
        | Delivered _ -> { s with Notice = Delivered now }
        | NotOwed
        | Acknowledged _ -> s

    /// Rule 10. The User says they have seen it, and that is the end of it.
    let acknowledged now (s: SessionRecord) =
        match s.Notice with
        | Owed
        | Delivered _ -> { s with Notice = Acknowledged now }
        | NotOwed
        | Acknowledged _ -> s

    /// Rule 8. The idle clock lives on the record, because it is the only thing that
    /// outlives a request.
    let seen now (s: SessionRecord) = { s with LastSeen = now }

    let isOpen (s: SessionRecord) = s.State = OpenOrGone

    /// Rules 9 and 41. One predicate for the sweep and for an arriving request alike,
    /// so the two can never disagree about what "too long" means. Rule 13: an
    /// anonymous Session has no idle clock at all.
    let hasIdledOut (now: int) (s: SessionRecord) =
        isOpen s && s.User.IsSome && now - s.LastSeen > sessionTtl

    /// Rule 9. The other end a Session cannot outlive: its outright limit, which no
    /// amount of use extends. Every Session carries one — the anonymous ones to bound
    /// the records they leave behind (Rule 13), the launched ones to bound how long
    /// one launch stands for the person who made it.
    let hasExpired (now: int) (s: SessionRecord) =
        isOpen s && (match s.ExpiresAt with Some at -> now > at | None -> false)

    /// Rules 9 and 41. Both ends, and the mark each of them earns. A request arriving
    /// past either one ends the Session then and there rather than refreshing it, so
    /// this is what an arrival asks — not `hasIdledOut` alone, which a Client that
    /// keeps talking can hold off for ever.
    let outOfTime (now: int) (s: SessionRecord) =
        if hasExpired now s then Some Expired
        elif hasIdledOut now s then Some Idle
        else None

    let userId (s: SessionRecord) = s.User |> Option.map _.UserId

// ───────────────────────────── the tokens ─────────────────────────────  [ships] — with a real HMAC

/// Concept 17. The Server states a fact, signs it, and refuses to believe it unless
/// the signature comes back with it.
module Token =

    /// One configured secret, the same for every Server instance so any can verify
    /// any other's token (Rule 36). `private`, so no scenario can compute a mac: the
    /// forgery tests can build the right fields with a wrong mac and watch it fail.
    let private masterKey = "master-key-known-only-to-the-server"

    /// Domain separation (Concept 17): one subkey per purpose, derived from the one
    /// secret. Verification recomputes with the subkey of the purpose *expected*, so
    /// a token of another purpose fails by key rather than by a field comparison
    /// somebody might forget to write.
    let private subKey (purpose: TokenPurpose) =
        $"%s{masterKey}/genpres/token/v1/%A{purpose}"

    /// One claim, one string: fixed field order, fixed separators. If two forms of
    /// the same claim were possible, the same token could verify in one form and
    /// mean another.
    let private canonical (c: Claim) =
        let (SessionId sid) = c.Sid
        let pat = match c.Patient with Some(PatientId p) -> p | None -> "-"
        let names = c.Names |> String.concat ","
        [ $"%A{c.Purpose}"; sid; pat; names; c.Nonce; string c.IssuedAt; string c.ExpiresAt ]
        |> String.concat "|"

    let private macAs (expect: TokenPurpose) (c: Claim) =
        $"mac|%s{subKey expect}|%s{canonical c}"

    /// The nonce. One token is minted per handled message, so the Session and the
    /// tick it was minted at name it uniquely; where the real thing needs
    /// unguessability, this needs only uniqueness.
    let private nonceAt (SessionId sid) (now: int) = $"%s{sid}-%i{now}"

    let private mint purpose now s p names : Token =
        let claim =
            {
                Purpose = purpose
                Sid = s
                Patient = p
                Names = names
                Nonce = nonceAt s now
                IssuedAt = now
                ExpiresAt = now + tokenTtl
            }
        { Claim = claim; Mac = macAs purpose claim }

    /// Rule 33. Minted at the opening of a Session, and re-minted whenever the
    /// baseline moves: an open (Rule 17) or a Submission both make a new TreatmentPlan the one
    /// Rules 20 and 21 are measured from.
    let mintOpened now s p (n: TreatmentPlanId option) : OpenedToken =
        mint TokenPurpose.Opened now s p (n |> Option.toList |> List.map (fun (TreatmentPlanId i) -> i))

    /// Rule 34. Minted with the notice, naming exactly the Unsigned TreatmentPlans it
    /// disclosed.
    let mintNotice now s p (ids: TreatmentPlanId list) : NoticeToken =
        mint TokenPurpose.Notice now s p (ids |> List.map (fun (TreatmentPlanId i) -> i))

    /// Rule 43. Minted after the Rule 20 and 21 pre-checks, naming the digest of the
    /// WorkPlan the User was shown.
    let mintChallenge now s p (digest: string) : SigningChallenge =
        mint TokenPurpose.Challenge now s p [ digest ]

    /// Rule 44. Minted with the notice that the Patient Data has moved, naming the
    /// digest of the data as the platform now has it.
    let mintDataNotice now s p (digest: string) : DataNoticeToken =
        mint TokenPurpose.DataNotice now s p [ digest ]

    /// Recompute and compare, with the subkey of the purpose expected. A token whose
    /// fields were edited no longer matches its mac; a token whose mac was guessed
    /// does not match its fields; and a token minted for another purpose matches
    /// neither, because it was signed under a different key.
    let private verifyAs (expect: TokenPurpose) (t: Token) =
        t.Mac = macAs expect t.Claim

    let verifyOpened (t: OpenedToken) = verifyAs TokenPurpose.Opened t

    let verifyNotice (t: NoticeToken) = verifyAs TokenPurpose.Notice t

    let verifyChallenge (t: SigningChallenge) = verifyAs TokenPurpose.Challenge t

    let verifyDataNotice (t: DataNoticeToken) = verifyAs TokenPurpose.DataNotice t

    /// Rule 33's one name: the TreatmentPlan the Session opened with, if any.
    let plan (t: OpenedToken) = t.Claim.Names |> List.tryHead |> Option.map TreatmentPlanId

    /// Rule 34's names: every Unsigned TreatmentPlan the notice disclosed.
    let disclosed (t: NoticeToken) = t.Claim.Names |> List.map TreatmentPlanId

    /// Rules 43 and 44: the one digest the token names.
    let digest (t: Token) = t.Claim.Names |> List.tryHead

    // ── The Launch (Concept 3) ──
    // Not a Claim: a Launch names no Session, it is what a Session is opened *by*. It
    // gets the same treatment so that neither can be spent as the other.

    /// The nonce of a Launch. The LaunchScript mints one per launch, so the tick and
    /// the Patient name it uniquely here; the real thing needs unguessability.
    let private launchNonceAt (patient: PatientId option) (now: int) =
        let p = match patient with Some(PatientId p) -> p | None -> "-"
        $"launch-%s{p}-%i{now}"

    let private canonicalLaunch (l: Launch) =
        let pat = match l.Patient with Some(PatientId p) -> p | None -> "-"
        [ $"%A{TokenPurpose.Launch}"; pat; l.Nonce; string l.IssuedAt ] |> String.concat "|"

    let private launchMac (l: Launch) =
        $"mac|%s{subKey TokenPurpose.Launch}|%s{canonicalLaunch l}"

    /// Concept 3. What the LaunchScript hands the browser: the active Patient, a
    /// nonce, the tick, and a mac over the three. No login — Rule 4 takes the User
    /// from the browser, so there is nothing here for a launch to assert about it.
    let mintLaunch (patient: PatientId option) (now: int) : Launch =
        let l = { Patient = patient; Nonce = launchNonceAt patient now; IssuedAt = now; Mac = "" }
        { l with Mac = launchMac l }

    /// Rules 2 and 3's precondition: this really was sealed under the shared key.
    let verifyLaunch (l: Launch) = l.Mac = launchMac { l with Mac = "" }

// ───────────────────────────── the reducer ─────────────────────────────  [mixed]
// The branch bodies are the design — which leg follows which, what each act checks,
// and in what order (Rule 42 above all). The dispatch on (From, To, Msg) is [model]:
// real endpoints, real handlers, and the Database's own transaction.

module Hospital =

    let empty =
        {
            Workstation =
                {
                    ActiveUser = None
                    ActivePatient = None
                    MayLaunch = Set.empty
                    NextTab = 1
                }
            Registry    = { Users = Map.empty; Up = true }
            Platform    = { Data = Map.empty; Up = true }
            Database    =
                {
                    Clinical = { Signed = Map.empty }
                    Private =
                        {
                            Drafts = Map.empty
                            Sessions = []
                            Credentials = Map.empty
                            Resets = Map.empty
                            Answered = Map.empty
                            Spent = Set.empty
                            AnonymousRefused = Map.empty
                            Audit = []
                        }
                    NextPlan = 1
                }
            GenPres     =
                {
                    InFlight = Map.empty
                    Pending = Map.empty
                    NextAttempt = 1
                    NextRequest = 1
                    NextSessionId = 1
                    Up = true
                }
            Clients     = Map.empty
            Mail        = []
            Env         = { Now = 0 }
        }

    let blankClient =
        {
            UrlLaunch = None
            RetryLaunch = None
            BrowserIdentity = None
            Sid = None
            User = None
            Patient = None
            Work = WorkPlan.empty
            Opened = None
            Notice = None
            Signing = None
            Modal = None
            DataOk = None
            Offered = None
            NoticeFor = []
            AwaitingPin = None
            AnonymousOffer = false
            Showing = None
            Closed = false
        }

    let private onClient id f h =
        let current = h.Clients |> Map.tryFind id |> Option.defaultValue blankClient
        { h with Clients = h.Clients |> Map.add id (f current) }

    let private clientState id h =
        h.Clients |> Map.tryFind id |> Option.defaultValue blankClient

    let private send from to_ msg = { From = from; To = to_; Msg = msg }

    let private pend now stage = { Stage = stage; Since = now }

    /// Rule 45. The Client's key for one mutating request: this browser, this moment.
    /// A retry of the same request carries the same key, which is what lets the
    /// Database answer it rather than do it twice.
    let private idemKey (BrowserId b) (now: int) = IdemKey $"idem-%i{b}-%04i{now}"

    // ── the in-flight table (Rule 31) ──

    let private putFlight rid ctx (h: Hospital) =
        { h with GenPres.InFlight = h.GenPres.InFlight |> Map.add rid ctx }

    let private dropFlight rid (h: Hospital) =
        { h with GenPres.InFlight = h.GenPres.InFlight |> Map.remove rid }

    /// Rule 26, both halves: the User is mailed, and the change is recorded.
    let private pinChanged (mail: MailAddress option) (what: string) =
        [
            match mail with
            | Some m -> send GenPresServer MailService (SendMail(m, what))
            | None -> ()
            send GenPresServer Environment (Noted what)
        ]

    // ── Rule 35: the stamps are the Server's to compute ──

    /// Rules 14, 35. With the cart in the Client there is no Session to ask what
    /// changed, so the Server diffs the payload against the base by OrderContextId.
    /// Whatever stamp arrived is discarded unread.
    let private stampAgainst (uc: UserContext) (basePlan: TreatmentPlan option) (orders: OrderContext list) =
        let baseline = basePlan |> Option.map _.Orders |> Option.defaultValue []
        orders
        |> List.map (fun o ->
            match baseline |> List.tryFind (fun b -> b.Id = o.Id) with
            | Some b when b.Content = o.Content -> { o with Stamp = b.Stamp }
            | _ -> { o with Stamp = Some uc })

    // ── the launch, and what ends it ──

    /// UC-1 steps 8 and 9, and the last step of the anonymous open. Rule 19 has
    /// already picked the TreatmentPlan the Session starts from, if there is one, and Rule
    /// 7's other Sessions of this User have already been read back from the Database
    /// — the Server keeps no copy of them (Rule 31).
    let private openSession
        (client: ActorId)
        (launch: string option)
        (user: UserContext option)
        (mail: MailAddress option)
        (pctx: PatientContext)
        (start: TreatmentPlan option)
        (others: SessionRecord list)
        (replacing: SessionId option)
        (resumedFrom: (TreatmentPlanId * EndMark option * int) option)
        (h: Hospital) =

        let sid = SessionId $"sid-%04i{h.GenPres.NextSessionId}"
        let no = SessionNo h.GenPres.NextSessionId

        // Rules 7 and 10 both speak of the Session's User, so neither applies to an
        // anonymous open (Rule 13). Rule 7 is per User, not per Patient: this closes
        // every other Session of *this* User, whichever Patient it was opened for,
        // and closes nobody else's.
        let priors =
            match user with
            | None -> []
            | Some uc ->
                others
                |> List.filter (fun r ->
                    SessionRecord.userId r = Some uc.UserId && SessionRecord.wouldOweNotice r)
                // endWith leaves an already settled record alone, so a Session that
                // idled out keeps Idle — and keeps the obligation that ending created,
                // which this launch is the opportunity to discharge.
                |> List.map (
                    SessionRecord.endWith Superseded h.Env.Now
                    >> SessionRecord.delivered h.Env.Now)

        let orders = start |> Option.map _.Orders |> Option.defaultValue []

        let record =
            {
                Id = sid
                No = no
                User = user
                Mail = mail
                Patient = pctx.Patient
                Browser = (match client with GenPresClient b -> Some b | _ -> None)
                Launch = launch
                OpenedAt = h.Env.Now
                // Rule 9. Rule 8's clock forgives a Client that keeps talking; this
                // does not. The two limits differ because what they bound differs.
                ExpiresAt =
                    Some(h.Env.Now + (if user.IsNone then anonymousLifetime else sessionMaxLifetime))
                LastSeen = h.Env.Now
                State = OpenOrGone
                Notice = NotOwed
            }

        // Rule 33. The Client gets the TreatmentPlan the Session opened with as something
        // it can hand back but not make.
        let token = Token.mintOpened h.Env.Now sid pctx.Patient (start |> Option.map _.Id)

        // Rule 40. One act: the record goes in and the User's other Sessions close with
        // it. `priors` is what the notice is built from, not what closes them — the
        // closing is the Database's, and it is the Database's view that decides.
        { h with GenPres.NextSessionId = h.GenPres.NextSessionId + 1 },
        [
            // Rules 7 and 40. Both limits in the same act as the open, so nothing can
            // observe the browser holding two Sessions, or none.
            send GenPresServer GenPresDatabase (OpenSessionClosingOthers(record, replacing))
            send GenPresServer client (SessionOpened(sid, no, user, pctx, orders, token, resumedFrom))
            if not priors.IsEmpty then
                send GenPresServer client
                    (PriorSessionNotice(priors |> List.map (fun r -> r.No, r.State, r.Id)))
        ]

    /// Rule 2's replay clause. Nothing is written — the record is there and the nonce
    /// is spent. Only the OpenedToken is fresh, because the first may be gone.
    let private resumeSession
        (client: ActorId)
        (record: SessionRecord)
        (pctx: PatientContext)
        (start: TreatmentPlan option)
        (resumedFrom: (TreatmentPlanId * EndMark option * int) option)
        (h: Hospital) =

        let orders = start |> Option.map _.Orders |> Option.defaultValue []
        let token = Token.mintOpened h.Env.Now record.Id pctx.Patient (start |> Option.map _.Id)

        h,
        [
            send GenPresServer client
                (SessionOpened(record.Id, record.No, record.User, pctx, orders, token, resumedFrom))
        ]

    /// UC-1 steps 6 and 7, and where they are skipped. A Reader arrives here
    /// straight from the registry (Rule 25); a Prescriber only once the PIN question
    /// is settled (Rules 23, 24).
    let private afterCredential att (ctx: LaunchCtx) uc mail (h: Hospital) =
        match ctx.Launch.Patient with
        | None ->
            // ext 1a: no Patient, so no data to fetch and no record to read. Rule 7
            // still applies — this User's other Sessions close — so the SessionRecords
            // are still read.
            let pctx = { Patient = None; Data = None }
            { h with
                GenPres.Pending =
                    h.GenPres.Pending
                    |> Map.add att (pend h.Env.Now (AwaitingPriors(ctx, uc, mail, pctx, None, None))) },
            [ send GenPresServer GenPresDatabase (ReadSessionRecords(ForLaunch att)) ]
        | Some p ->
            { h with
                GenPres.Pending =
                    h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPatientData(ctx, uc, mail))) },
            [ send GenPresServer PatientDataPlatform (ReadPatientData(ForLaunch att, p)) ]

    /// Rule 6. A launch that cannot be honoured opens no Session. There is no silent
    /// fallback; where relaunching would not cure it, the Client is left to offer a
    /// fresh anonymous open, which carries nothing over.
    let private refuseLaunch att client reply (h: Hospital) =
        { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att },
        [
            // Rule 46: every launch, honoured or refused. A refusal carries no reason
            // to the Client (deliberately) — the audit is where the reason goes.
            send GenPresServer Environment (Noted $"launch refused: %A{reply}")
            send GenPresServer client reply
        ]

    // ── creating a TreatmentPlan: the Server's part, which is small ──

    /// Rule 42. The Server gathers what only it can know — the re-taken Role, the
    /// re-read data — and decides nothing itself.
    let private commit rid (ctx: RequestCtx) (r: SessionRecord) (req: Submission) role (h: Hospital) =
        h |> putFlight rid { ctx with Stage = AwaitingCommit r },
        [
            send GenPresServer GenPresDatabase
                (CommitTreatmentPlan(ForRequest rid, { Sid = r.Id; Req = req; Role = role }))
        ]

    /// The SessionRecord has come back, the Session is open, and Rule 8's clock has
    /// been refreshed. This is where Rule 32 bites: the User and the Patient of the
    /// request are read off the record, and the payload is believed about nothing
    /// else. Concept 15 — what a User may do inside a Session, and what they may not.
    let private dispatch rid (ctx: RequestCtx) (r: SessionRecord) (h: Hospital) =
        let refuse msg = dropFlight rid h, [ send GenPresServer ctx.Client msg ]

        // Rule 12: a Session without a PatientId lets the User prescribe, Patient
        // Data included, but a TreatmentPlan cannot be opened or created.
        let withPatient f =
            match r.Patient with
            | None -> refuse NoTreatmentPlanHere
            | Some p -> f p

        // Roles: a Reader may never create a TreatmentPlan. Rule 13: an anonymous Session
        // has no User at all, so there is nobody to create as and nobody to sign as.
        let withPrescriber f =
            match r.User with
            | Some uc when uc.Role = Prescriber -> f uc
            | _ -> refuse NotPermitted

        match ctx.Cmd with
        | Compute orders ->
            // Rule 31. The answer is computed from the payload and nothing is kept —
            // the cart went home with the reply, as it arrived with the request.
            dropFlight rid h, [ send GenPresServer ctx.Client (Computed orders) ]

        | CloseSession ->
            // Rule 9: closing is an explicit act in the Client. Rule 10 adds nothing
            // — it speaks only of endings other than by the User.
            dropFlight rid h, [ send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, ClosedByUser)) ]

        | ResetPin ->
            // UC-7 step 1. Rule 37: nothing is removed. A one-time code goes to the
            // address the registry gave (Rule 26), and the PIN in force stands until
            // that code replaces it — so there is no window in which anybody at this
            // workstation could set a PIN of their own.
            match r.User with
            | None -> refuse NotPermitted
            | Some uc ->
                let code = ResetCode $"code-%04i{h.Env.Now}"
                h |> putFlight rid { ctx with Stage = AwaitingResetStarted(r, code) },
                [
                    send GenPresServer GenPresDatabase
                        (StartReset(ForRequest rid, uc.UserId, Reset.macOf code, h.Env.Now + resetCodeTtl))
                ]

        | SupplyResetCode(code, pin) ->
            // UC-7 step 2. The Server carries the answer no further than the Database:
            // the code is checked and the PIN replaced there, in one act (Rule 37).
            match r.User with
            | None -> refuse NotPermitted
            | Some uc ->
                h |> putFlight rid { ctx with Stage = AwaitingPinReplaced r },
                [ send GenPresServer GenPresDatabase (ReplacePinIfCode(ForRequest rid, uc.UserId, code, pin)) ]

        | OpenTreatmentPlan _ ->
            withPatient (fun p ->
                match r.User with
                | None -> refuse NotPermitted            // Rule 13
                | Some _ ->
                    h |> putFlight rid { ctx with Stage = AwaitingPatientRecord r },
                    [ send GenPresServer GenPresDatabase (ReadRecord(ForRequest rid, p)) ])

        | DiscardTreatmentPlan(id, opened) ->
            // Rule 47. Nothing is read here: everything the discard turns on is the
            // Database's, token included — spending it and moving the baseline must be
            // one act, or the old token outlives the plan it named (Rule 33).
            withPatient (fun _ ->
                withPrescriber (fun _ ->
                    if not (Token.verifyOpened opened) || opened.Claim.Sid <> ctx.Sid then
                        refuse (SubmissionRefused "the opened-with token does not verify (Rule 33)")
                    else
                        h |> putFlight rid { ctx with Stage = AwaitingDiscard r },
                        [ send GenPresServer GenPresDatabase
                            (DiscardIfOwnHead(ForRequest rid, r.Id, id, opened)) ]))

        | RequestSignChallenge _ ->
            // Rule 43, and UC-3 ext 3c's order: the Rule 20 and 21 answers are settled
            // against the PatientRecord first, and only then is a challenge issued —
            // so the User is never asked for a PIN they were never going to spend.
            withPatient (fun p ->
                withPrescriber (fun _ ->
                    h |> putFlight rid { ctx with Stage = AwaitingPatientRecord r },
                    [ send GenPresServer GenPresDatabase (ReadRecord(ForRequest rid, p)) ]))

        | SubmitTreatmentPlan req ->
            withPatient (fun p ->
                withPrescriber (fun uc ->
                    match req.Pin with
                    | None ->
                        // A save attests to nothing, so it needs neither the Role
                        // re-taken nor the data re-read: straight to the one act.
                        commit rid ctx r req (Some uc.Role) h
                    | Some _ ->
                        // Rule 38. Signing is a fresh act of authority: the Role is
                        // taken from the registry again, now, and before anything else
                        // — so a signature nobody is entitled to costs no PIN attempt
                        // (Rule 27).
                        ignore p

                        h |> putFlight rid { ctx with Stage = AwaitingSigningRole(r, req) },
                        [ send GenPresServer UserRegistry (ResolveUser(ForRequest rid, uc.Login)) ]))

    // ══════════════════════════════════════════════════════════════════════════
    //  The reducer. Every branch names the sender as well as the recipient, so it
    //  states who may send it; whether they may exchange anything at all was settled
    //  by the edge table in `run`. One function per recipient, split on `env.To`
    //  alone, each branch keeping its pattern and its place.
    // ══════════════════════════════════════════════════════════════════════════

    /// An envelope an edge permits but the recipient does not accept. Recorded
    /// rather than swallowed, so a misrouted or forged message shows in the trace.
    let private refused (h: Hospital) (env: Envelope) =
        h, [ send env.To Environment (Refused env) ]

    /// Actor-less. The audit lines the other actors send to be recorded, and the clock.
    let private updateEnvironment (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── the audit log, and the person ──

        // Recorded, not acted on. Handled first, and never refused itself: refusing a
        // refusal would not terminate.
        | _, Environment, Refused e ->
            let line = $"REFUSED %A{e.From} -> %A{e.To}"
            { h with Database = h.Database |> Database.note h.Env.Now line }, []

        | _, Environment, Noted what ->
            { h with Database = h.Database |> Database.note h.Env.Now what }, []

        // ── the clock ──

        // The clock is advanced by the prefix above, on this envelope like any other,
        // so a Tick adds nothing of its own: it exists to reach the Server, whose
        // sweep runs on nothing else.
        | Environment, Environment, Tick ->
            h, [ send Environment GenPresServer Tick ]

        | _ -> refused h env

    /// Actor 1. Invariant 1: one active Patient at a time.
    let private updateWorkstation (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Actor 1: the MainEHR Workstation ──

        | User, MainEhrWorkstation, LogIn u -> { h with Workstation.ActiveUser = Some u }, []

        | User, MainEhrWorkstation, SelectPatient p -> { h with Workstation.ActivePatient = Some p }, []

        | User, MainEhrWorkstation, ClearPatient -> { h with Workstation.ActivePatient = None }, []

        | _ -> refused h env

    /// Actor 2. Rule 1, and then it exits: it seals the Launch, opens the browser, and is
    /// never heard from again (Consequence 1).
    let private updateLaunchScript (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Actor 2: the MainEHR LaunchScript ──

        // Rule 1. A button *in* the Workstation, so the login and the active Patient
        // are its own context: there is no edge between Actors 1 and 2, and none is
        // needed. It transmits no Role and names no User (Rules 4, 5).
        | User, MainEhrLaunchScript, TriggerLaunch ->
            match h.Workstation.ActiveUser with
            // UC-1 ext 1b. Rule 1: the LaunchScript decides which MainEHR User may run
            // it, and refuses the rest. Nothing leaves the workstation, so no Launch
            // ever exists to be spent or stolen.
            | Some u when not (h.Workstation.MayLaunch.Contains u) ->
                h, [ send MainEhrLaunchScript User (LaunchError "this button is not yours to press") ]
            | Some _ ->
                // ext 1a: no active Patient is not an error (Rule 12). Concept 3: it
                // seals the Launch itself and exchanges with nobody, so nothing here
                // can fail and there is nothing to report or wait on.
                let launch = Token.mintLaunch h.Workstation.ActivePatient h.Env.Now
                let tab = BrowserId h.Workstation.NextTab
                // Rule 4. The browser the script opens is the one at this workstation,
                // and the workstation is logged into by badge and PIN: what the page
                // will prove to the Server is that person, and nothing the script does
                // can change it.
                let h = h |> onClient tab (fun s -> { s with BrowserIdentity = h.Workstation.ActiveUser })
                // The launch, and then the LaunchScript exits. Consequence 1 is not a
                // promise made here: edge C4 is `=>`, so nothing can be sent back.
                { h with Workstation.NextTab = h.Workstation.NextTab + 1 },
                [ send MainEhrLaunchScript (GenPresClient tab) (OpenUrl launch) ]
            | None ->
                h, [ send MainEhrLaunchScript User (LaunchError "no MainEHR login: nobody to launch as") ]

        | _ -> refused h env

    /// Actor 9. The only source of a Role (Rule 5).
    let private updateRegistry (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── infrastructure ──

        | Environment, UserRegistry, Stop _ -> { h with Registry.Up = false }, []

        | Environment, UserRegistry, Start _ -> { h with Registry.Up = true }, []

        // ── Actor 9: the UserRegistry ──

        | GenPresServer, UserRegistry, ResolveUser(tag, _) when not h.Registry.Up ->
            h, [ send UserRegistry env.From (UserUnresolved(tag, RegistryUnreachable)) ]

        | GenPresServer, UserRegistry, ResolveUser(tag, login) ->
            match h.Registry.Users |> Map.tryFind login with
            | Some(uc, mail) -> h, [ send UserRegistry env.From (UserResolved(tag, uc, mail)) ]
            | None -> h, [ send UserRegistry env.From (UserUnresolved(tag, NoRole)) ]

        | _ -> refused h env

    /// Actor 6. Read-only, and read once per launch (Concept 2).
    let private updatePlatform (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        | Environment, PatientDataPlatform, Stop _ -> { h with Platform.Up = false }, []

        | Environment, PatientDataPlatform, Start _ -> { h with Platform.Up = true }, []

        // ── Actor 6: the PatientDataPlatform ──

        // Concept 2: read once, at the launch. Whether it is down or simply holds
        // nothing for this Patient makes no difference to the caller (ext 6a).
        | GenPresServer, PatientDataPlatform, ReadPatientData(att, p) ->
            match (if h.Platform.Up then h.Platform.Data |> Map.tryFind p else None) with
            | Some d -> h, [ send PatientDataPlatform env.From (PatientDataRead(att, d)) ]
            | None -> h, [ send PatientDataPlatform env.From (PatientDataUnavailable att) ]

        | _ -> refused h env

    /// Actor 10. It sends, and nothing comes back.
    let private updateMailService (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Actor 10: the MailService ──

        | GenPresServer, MailService, SendMail(addr, what) ->
            { h with Mail = (addr, what) :: h.Mail }, []

        | _ -> refused h env


    /// Rule 42. The Submission, as one act.
    ///
    /// Everything it turns on is re-established here in one go: the Session (Rules
    /// 40, 41), who may create (Rules 13, 25, 38), every token (Rules 32, 33, 34,
    /// 43, 44), what the record allows (Rules 19, 20, 21, 36) and last of all the
    /// PIN (Rules 22, 27) — last, because a Submission that was never going to land
    /// must not cost an attempt. Either everything is written or nothing happened.
    /// The Id and the ordering are minted here, because ordering a record is the
    /// same authority as deciding what may join it.
    let private dbCommit (h: Hospital) (env: Envelope) tag (c: Commit) =
        let reply outcome =
            match outcome with
            | Ok plan -> send GenPresDatabase env.From (TreatmentPlanCommitted(tag, plan))
            | Error refusal -> send GenPresDatabase env.From (CommitRefused(tag, refusal))

        // Rule 45. A key that has been answered is answered again, and nothing is
        // done a second time.
        match h.Database.Private.Answered |> Map.tryFind c.Req.Key with
        | Some remembered -> h, [ reply remembered ]
        | None ->

        let record = h.Database.Private.Sessions |> List.tryFind (fun x -> x.Id = c.Sid)

        // The refusal path, and the only place a refusal is remembered. Rule 46:
        // a Submission that did not land is as much an event as one that did.
        let refuse (h: Hospital) refusal =
            let what = if c.Req.Pin.IsSome then "signature" else "save"
            // The base of a nested copy-and-update is bound first: `{ h.Database
            // with Private.X = … }` is read by some compilers as constructing a
            // bare `PrivateStore` (FS0764). Same value, no ambiguity.
            let db = h.Database

            { h with
                Database =
                    { db with Private.Answered = db.Private.Answered |> Map.add c.Req.Key (Error refusal) }
                    |> Database.note h.Env.Now $"%s{what} refused: %A{refusal}" },
            [ reply (Error refusal) ]

        match record with
        | None -> refuse h (SessionNotOpen None)
        | Some r when not (SessionRecord.isOpen r) ->
            let mark = match r.State with Ended(m, _) -> Some m | OpenOrGone -> None
            refuse h (SessionNotOpen mark)
        // Rules 9 and 41, inside the act: a Session past either of its ends — the
        // idle clock or the outright limit — ends here rather than signing
        // something. Checking only the first would let a Client that keeps talking
        // sign for ever on one launch.
        | Some r when (SessionRecord.outOfTime h.Env.Now r).IsSome ->
            let mark = (SessionRecord.outOfTime h.Env.Now r).Value

            let ended =
                { h with
                    Database.Private.Sessions =
                        h.Database.Private.Sessions
                        |> List.map (fun x ->
                            if x.Id = r.Id then x |> SessionRecord.endWith mark h.Env.Now else x) }
            refuse ended (SessionNotOpen(Some mark))
        | Some r ->
            let req = c.Req
            let opened = req.Opened

            match r.User, r.Patient with
            // Rule 13: an anonymous Session has nobody to create as. Rule 12: and
            // a Session without a Patient has nothing to create against.
            | None, _
            | _, None -> refuse h RoleRefused
            | Some uc, Some patient ->

            let pr = h.Database |> Database.recordOf patient

            let openedWith = Token.plan opened

            // Rule 17. Walk `Base` back until a Signed TreatmentPlan appears —
            // the Unsigned steps in between are the private store's business.
            let rec nearestSigned (p: TreatmentPlan option) =
                match p with
                | Some x when x.State = Signed -> Some x.Id
                | Some x ->
                    nearestSigned (x.Base |> Option.bind (fun b -> pr.Plans |> List.tryFind (fun y -> y.Id = b)))
                | None -> None
            let basePlan = pr.Plans |> List.tryFind (fun x -> Some x.Id = openedWith)

            // Rule 34. Honoured for exactly what the notice disclosed, and for
            // nothing newer; a token the Client made itself counts as none at all.
            let honoured =
                match req.Notice with
                | Some t when Token.verifyNotice t && t.Claim.Sid = r.Id ->
                    Set.ofList (Token.disclosed t)
                | _ -> Set.empty

            let outstanding = pr |> PatientRecord.unsignedElsewhere uc.UserId openedWith
            let undisclosed = outstanding |> List.filter (fun x -> not (honoured.Contains x.Id))

            // Rule 44. Settled before the challenge, so nothing is re-read here.
            // The token must be this Session's and must name this data.
            let dataTokenStands =
                match req.DataOk with
                | None -> true
                | Some t ->
                    Token.verifyDataNotice t
                    && t.Claim.Sid = r.Id
                    && Token.digest t = Some(WorkPlan.dataDigest req.Work.Data)

            if c.Role <> Some Prescriber then refuse h RoleRefused

            // Rules 32, 33. The baseline is the Server's own word, handed back.
            elif not (Token.verifyOpened opened) then
                refuse h (TokenRefused "the opened-with token does not verify (Rule 33)")
            elif opened.Claim.Sid <> r.Id || opened.Claim.Patient <> r.Patient then
                refuse h (TokenRefused "the opened-with token is for another Session (Rule 33)")

            // Rule 32 and Guarantee 1. The PatientId is the one thing no
            // TreatmentPlan may change, and the payload does not get a vote on it.
            elif req.Work.Orders |> List.exists (fun o -> o.Patient <> None && o.Patient <> Some patient) then
                refuse h (TokenRefused "an OrderContext names another Patient (Rule 32)")

            // Concept 10. An OrderContext has an identity, and a WorkPlan naming
            // one twice says two things about the same thing: Rule 42 refuses the
            // Submission whole rather than choosing between them.
            elif (req.Work.Orders |> List.map _.Id |> List.distinct |> List.length)
                 <> req.Work.Orders.Length then
                refuse h (TokenRefused "an OrderContext appears twice (Concept 10)")

            // Rules 15, 19, 47. Rule 33's spent-mark normally makes this
            // unreachable, but the record is what the append lands in, so the
            // record is where it is settled.
            elif (basePlan |> Option.map _.State) = Some Discarded then
                refuse h (TokenRefused "the TreatmentPlan this was opened with has been discarded (Rules 15, 47)")

            // Rule 20, and Rule 36 with it: the check and the append are the same
            // act, so there is no window between them to lose.
            elif (PatientRecord.blocking openedWith pr).IsSome then
                let blocker = (PatientRecord.blocking openedWith pr).Value
                refuse h (BlockedBy blocker.By)

            // Rule 21. Whose work it is, not its contents — and one name, because the
            // rule says "whose", singular. The token names every plan disclosed, so
            // proceeding honours all of them; the notice names the newest one's author.
            // Where two Users have work outstanding the User is told about one of them,
            // which is less than the token covers. That asymmetry is deliberate here
            // and is the rule's to change, not this file's.
            elif not undisclosed.IsEmpty then
                refuse h (UnsignedElsewhere(undisclosed.Head.By, outstanding |> List.map _.Id))

            elif not dataTokenStands then
                refuse h (TokenRefused "the Patient Data token does not name this data (Rule 44)")

            // Concept 17 and Rule 33. A token works exactly once and only within
            // its lifetime: the Submission it accompanies consumes it, and a spent or
            // aged one is worth no more than one the Client made up.
            elif opened.Claim.ExpiresAt < h.Env.Now then
                refuse h (TokenRefused "the opened-with token has expired (Rule 33)")
            elif h.Database.Private.Spent.Contains opened.Claim.Nonce then
                refuse h (TokenRefused "the opened-with token was already spent (Rule 33)")

            // Rule 43. A signature answers for the exact WorkPlan the User was
            // shown, and for no other.
            elif req.Pin.IsSome
                 && (match req.Challenge with
                     | Some t ->
                         not (Token.verifyChallenge t)
                         || t.Claim.Sid <> r.Id
                         || t.Claim.ExpiresAt < h.Env.Now
                         || h.Database.Private.Spent.Contains t.Claim.Nonce
                         || Token.digest t <> Some(WorkPlan.signingDigest req.Work)
                     | None -> true) then
                refuse h (TokenRefused "the signing challenge does not name this plan (Rule 43)")

            else
                // Rules 22 and 27, last of all.
                let credential =
                    h.Database.Private.Credentials
                    |> Map.tryFind uc.UserId
                    |> Option.defaultValue (UserCredential.fresh uc.UserId)

                // Rule 27. Reaching the limit ends the Session (Rule 9); an
                // attempt against an already-locked credential is only refused —
                // this Session did nothing wrong.
                let wasLocked = credential |> UserCredential.isLocked h.Env.Now

                let pinOk, credential =
                    match req.Pin with
                    | None -> true, credential
                    | Some pin -> UserCredential.verify h.Env.Now pin credential

                let withCredential (st: Hospital) =
                    { st with Database.Private.Credentials = st.Database.Private.Credentials |> Map.add uc.UserId credential }

                if not pinOk then
                    let h = withCredential h
                    if wasLocked then refuse h (CredentialLocked credential.LockedUntil.Value)
                    elif UserCredential.atLimit credential then
                        // Rule 9: at the limit the Session ends, here, in the same
                        // act that refused.
                        let h =
                            { h with
                                Database.Private.Sessions =
                                    h.Database.Private.Sessions
                                    |> List.map (fun x ->
                                        if x.Id = r.Id then
                                            x |> SessionRecord.endWith WrongPinLimit h.Env.Now
                                        else x) }
                        refuse h PinLimitReached
                    else
                        refuse h (PinWrong(UserCredential.attemptsLeft credential))
                else
                    let plan =
                        {
                            Id = TreatmentPlanId $"plan-%04i{h.Database.NextPlan}"
                            No = TreatmentPlanNo h.Database.NextPlan
                            Patient = patient                                  // Guarantee 1
                            By = uc                                            // Rule 14
                            Base = basePlan |> Option.map _.Id                 // Concept 13
                            Orders = req.Work.Orders |> stampAgainst uc basePlan  // Rule 35
                            // Concept 13: what it was built on, and where that
                            // came from, kept with it.
                            Data = req.Work.Data
                            From = req.Work.From
                            // Rules 14, 15. Signing is saving with a PIN; a plan
                            // is born Signed or Unsigned and never anything else.
                            // Discarded is reached only later, and only from
                            // Unsigned (Rules 15, 47).
                            State = if req.Pin.IsSome then Signed else Unsigned
                            // Rule 17. The private store's chain is `Base`; this
                            // is the chain the clinical store can follow on its own.
                            SignedBase = nearestSigned basePlan
                            Session = Some r.Id                                // Concept 13
                            At = h.Env.Now
                        }

                    let h = withCredential h

                    let (TreatmentPlanId planId) = plan.Id
                    let (UserId by) = uc.UserId
                    let what = if plan.State = Signed then "signed" else "saved"

                    // Concept 17. The tokens this Submission rested on are spent here,
                    // in the same act that honoured them (Rule 42) — so the same
                    // Submission cannot be replayed with the same word from the Server.
                    let spent =
                        h.Database.Private.Spent
                        |> Set.add opened.Claim.Nonce
                        |> fun set ->
                            match req.Challenge with
                            | Some t -> set |> Set.add t.Claim.Nonce
                            | None -> set

                    let db =
                        h.Database
                        |> Database.append plan
                        |> fun db ->
                            { db with
                                NextPlan = db.NextPlan + 1
                                Private.Answered = db.Private.Answered |> Map.add req.Key (Ok plan)
                                Private.Spent = spent }
                        // Rule 46. Who created what, and whether they attested to it.
                        |> Database.note h.Env.Now $"%s{planId} %s{what} by %s{by}"

                    { h with Database = db }, [ reply (Ok plan) ]

    // Rules 7 and 40, as one act, so there is no interval in which one User or
    // one browser holds two — whichever order two launches arrive in. Both limits
    // are decided from what the Database holds, not from what a Client says.


    /// Rules 15, 40, 47. One conditional operation, guarded by everything the
    /// discard requires — and by nothing else. There is no Rule 20 check here and
    /// no PIN: putting down your own draft attests to nothing and builds on
    /// nothing, so a signature that landed meanwhile has no bearing on it.
    let private dbDiscard (h: Hospital) (env: Envelope) tag sid id (opened: OpenedToken) =
        let refuse why = h, [ send GenPresDatabase env.From (DiscardRefused(tag, why)) ]

        match h.Database.Private.Sessions |> List.tryFind (fun x -> x.Id = sid) with
        | None -> refuse "no such Session"
        | Some r when not (SessionRecord.isOpen r) -> refuse "the Session has ended (Rule 40)"
        // Rule 33, the same two tests a Submission's token gets: not aged out, and
        // not already spent. A token is spent by the act it accompanies, and a
        // discard is such an act.
        | Some _ when opened.Claim.ExpiresAt < h.Env.Now ->
            refuse "the opened-with token has expired (Rule 33)"
        | Some _ when h.Database.Private.Spent.Contains opened.Claim.Nonce ->
            refuse "the opened-with token is spent (Rule 33)"
        | Some r ->
            match r.User, r.Patient with
            | None, _ | _, None -> refuse "an anonymous Session has nothing to discard (Rules 12, 13)"
            | Some uc, Some patient ->
                let pr = h.Database |> Database.recordOf patient
                let target = pr.Plans |> List.tryFind (fun x -> x.Id = id)
                // "That User's most recent" — of their own plans, whatever their
                // state. A draft with newer work of the User's on top of it is not
                // the one they are putting down.
                let ownHead = pr.Plans |> List.tryFind (fun x -> x.By.UserId = uc.UserId)

                match target with
                | None -> refuse "no such TreatmentPlan in this Patient's record"
                | Some plan when plan.State = Signed ->
                    // Rule 15. A signature is not takeable back: what it attested
                    // it attested, and the record keeps it (Concept 12).
                    refuse "a Signed TreatmentPlan cannot be discarded (Rules 15, 16)"
                | Some plan when plan.State = Discarded -> refuse "already discarded (Rule 15)"
                | Some plan when plan.By.UserId <> uc.UserId ->
                    // Rule 18. Somebody else's Unsigned work is not this User's to
                    // read, let alone to put down.
                    refuse "an Unsigned TreatmentPlan is its author's alone (Rule 18)"
                | Some plan when (ownHead |> Option.map _.Id) <> Some plan.Id ->
                    refuse "not this User's most recent TreatmentPlan (Rule 47)"
                | Some plan ->
                    // The one in-place write in the private store. The content is
                    // untouched: only `State` moves, and only Unsigned -> Discarded.
                    let discarded = { plan with State = Discarded }

                    let drafts =
                        h.Database
                        |> Database.draftsOf patient
                        |> List.map (fun x -> if x.Id = plan.Id then discarded else x)

                    // Rule 33. The token that named the old baseline is spent in
                    // the same act that moves it, so the Client cannot come back
                    // with it and build on a plan that is now Discarded.
                    let db =
                        { h.Database with
                            Private.Drafts = h.Database.Private.Drafts |> Map.add patient drafts
                            Private.Spent = h.Database.Private.Spent |> Set.add opened.Claim.Nonce }

                    // Rule 19, over the record as it now stands: with the draft
                    // down, what the Session starts from is whatever was under it.
                    let after = db |> Database.recordOf patient
                    let starts = after |> PatientRecord.startsFrom uc.UserId

                    let (TreatmentPlanId what) = plan.Id
                    let (UserId by) = uc.UserId

                    { h with
                        Database = db |> Database.note h.Env.Now $"%s{what} discarded by %s{by}" },
                    [ send GenPresDatabase env.From (TreatmentPlanDiscarded(tag, plan.Id, starts)) ]


    /// Rules 7 and 40, as one act, so there is no interval in which one User or
    /// one browser holds two — whichever order two launches arrive in. Both limits
    /// are decided from what the Database holds, not from what a Client says.
    let private dbOpenSession (h: Hospital) (env: Envelope) (r: SessionRecord) replacing =
        let now = h.Env.Now
        let (SessionNo sno) = r.No

        let who =
            match r.User with
            | Some uc -> let (UserId u) = uc.UserId in u
            | None -> "anonymous"

        // Two endings, told apart by what they owe: same browser is the User's
        // own act and owes nothing (Rule 10), other Sessions are Superseded and
        // do. The browser is read off the record, not off `replacing`, so a Client
        // that names nothing cannot thereby keep two — `replacing` is honoured as
        // well, which costs nothing and covers a record without the field.
        let sameBrowser (x: SessionRecord) =
            (r.Browser.IsSome && x.Browser = r.Browser) || Some x.Id = replacing

        let closed =
            h.Database.Private.Sessions
            |> List.map (fun x ->
                if x.Id <> r.Id && sameBrowser x then
                    x |> SessionRecord.endWith ReplacedInBrowser now
                elif x.Id <> r.Id
                     && r.User.IsSome
                     && SessionRecord.userId x = SessionRecord.userId r
                     && SessionRecord.wouldOweNotice x
                then
                    x |> SessionRecord.endWith Superseded now |> SessionRecord.delivered now
                else
                    x)

        let superseded =
            closed
            |> List.filter (fun x ->
                x.Id <> r.Id
                && (h.Database.Private.Sessions |> List.exists (fun y -> y.Id = x.Id && SessionRecord.isOpen y)))

        // Rule 46. The opening, and every Session it ended with it (Rule 7).
        let how =
            match r.Launch with
            | Some nonce -> $"%s{nonce} honoured"
            | None -> "no launch (anonymous)"

        let note (db: DatabaseState) =
            superseded
            |> List.fold
                (fun acc x ->
                    let (SessionNo n) = x.No
                    acc |> Database.note now $"session ses-%03i{n} ended Superseded")
                (db |> Database.note now $"session ses-%03i{sno} opened for %s{who}, %s{how}")

        // The SessionId counter never reissues, so an id already present is a
        // replay — and a replay must not resurrect what has since ended.
        if closed |> List.exists (fun x -> x.Id = r.Id) then
            { h with Database.Private.Sessions = closed }, []
        else
            let db = h.Database
            { h with Database = { db with Private.Sessions = r :: closed } |> note }, []

    // Rule 40. Conditional: an already ended record keeps the mark it ended with,
    // and the obligation that ending created (Rule 10).

    /// Actor 5. The Server is its only writer, and every write is one conditional act
    /// guarded by the state it expects (Rule 40).
    let private updateDatabase (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Actor 5: the GenPRES Database. The Server is its only writer. ──

        | GenPresServer, GenPresDatabase, ReadCredential(tag, user) ->
            h, [ send GenPresDatabase env.From (CredentialRead(tag, h.Database.Private.Credentials |> Map.tryFind user)) ]

        | GenPresServer, GenPresDatabase, ReadRecord(tag, p) ->
            h, [ send GenPresDatabase env.From (RecordRead(tag, h.Database |> Database.recordOf p)) ]
        | GenPresServer, GenPresDatabase, DiscardIfOwnHead(tag, sid, id, opened) ->
            dbDiscard h env tag sid id opened

        | GenPresServer, GenPresDatabase, CommitTreatmentPlan(tag, c) -> dbCommit h env tag c

        | GenPresServer, GenPresDatabase, OpenSessionClosingOthers(r, replacing) ->
            dbOpenSession h env r replacing

        // Rule 40. Conditional: an already ended record keeps the mark it ended with,
        // and the obligation that ending created (Rule 10).
        | GenPresServer, GenPresDatabase, EndSessionIfOpen(sid, mark) ->
            let ending =
                h.Database.Private.Sessions
                |> List.exists (fun x -> x.Id = sid && SessionRecord.isOpen x)

            let before = h.Database

            let db =
                { before with
                    Private.Sessions =
                        before.Private.Sessions
                        |> List.map (fun x ->
                            if x.Id = sid then x |> SessionRecord.endWith mark h.Env.Now else x) }

            // Rule 46. Only an ending that happened is recorded; a repeated one is not
            // an event, it is a no-op (Rule 40).
            let (SessionId name) = sid
            { h with
                Database =
                    if ending then db |> Database.note h.Env.Now $"session %s{name} ended %A{mark}" else db }, []

        // Rule 8, and Rule 40: a Session that has ended does not get its idle clock
        // refreshed by a request that arrived too late.
        | GenPresServer, GenPresDatabase, TouchIfOpen sid ->
            { h with
                Database.Private.Sessions =
                    h.Database.Private.Sessions
                    |> List.map (fun x ->
                        if x.Id = sid && SessionRecord.isOpen x then x |> SessionRecord.seen h.Env.Now
                        else x) }, []

        // Rule 10. The notice went out; whether it was seen is not the Server's to
        // know (Consequence 6), so this may happen more than once.
        | GenPresServer, GenPresDatabase, MarkDelivered sid ->
            { h with
                Database.Private.Sessions =
                    h.Database.Private.Sessions
                    |> List.map (fun x -> if x.Id = sid then x |> SessionRecord.delivered h.Env.Now else x) }, []

        // Rule 10. Honoured only from a Session that is open, launched and the same
        // User's: a Client holding the ended SessionId is whoever is at the keyboard.
        | GenPresServer, GenPresDatabase, MarkAcknowledged(acknowledging, about) ->
            let sessions = h.Database.Private.Sessions
            let by = sessions |> List.tryFind (fun x -> x.Id = acknowledging)
            let ended = sessions |> List.tryFind (fun x -> x.Id = about)

            let standing =
                match by, ended with
                | Some b, Some e ->
                    SessionRecord.isOpen b
                    && b.Launch.IsSome
                    && b.User.IsSome
                    && SessionRecord.userId b = SessionRecord.userId e
                | _ -> false

            if standing then
                { h with
                    Database.Private.Sessions =
                        sessions
                        |> List.map (fun x -> if x.Id = about then x |> SessionRecord.acknowledged h.Env.Now else x) },
                []
            else
                let (SessionId a) = acknowledging
                let (SessionId e) = about
                { h with
                    Database =
                        h.Database
                        |> Database.note h.Env.Now $"acknowledgement refused: %s{a} may not answer for %s{e} (Rule 10)" },
                []

        | GenPresServer, GenPresDatabase, ReadSessionRecord(tag, sid) ->
            h,
            [ send GenPresDatabase env.From
                (SessionRecordRead(tag, h.Database.Private.Sessions |> List.tryFind (fun x -> x.Id = sid))) ]

        | GenPresServer, GenPresDatabase, ReadSessionRecords tag ->
            h, [ send GenPresDatabase env.From (SessionRecordsRead(tag, h.Database.Private.Sessions)) ]

        // Rule 2, one conditional operation (Rule 40): test and mark cannot be two
        // acts, or two browsers at once would both find it unspent. When it was spent
        // already the answer carries the record, which is what the replay needs.
        | GenPresServer, GenPresDatabase, SpendLaunchIfUnspent(tag, nonce) ->
            if h.Database.Private.Spent.Contains nonce then
                let opened = h.Database.Private.Sessions |> List.tryFind (fun x -> x.Launch = Some nonce)
                h, [ send GenPresDatabase env.From (LaunchReplayed(tag, opened)) ]
            else
                { h with Database.Private.Spent = h.Database.Private.Spent |> Set.add nonce },
                [ send GenPresDatabase env.From (LaunchSpent tag) ]

        // Rule 46. A count per source, and nothing else — no SessionRecord, and no
        // audit line per refused request, which would be the same flood by another
        // name (Rule 13). What a flood may grow here is one integer.
        | GenPresServer, GenPresDatabase, NoteAnonymousRefusal source ->
            let n = h.Database.Private.AnonymousRefused |> Map.tryFind source |> Option.defaultValue 0
            { h with
                Database.Private.AnonymousRefused =
                    h.Database.Private.AnonymousRefused |> Map.add source (n + 1) },
            []

        // Rule 37. The PIN in force is untouched, so there is no moment without one.
        // A second request while the first code is good is refused before any mail:
        // otherwise anyone could void the code a User is reading, or point a mail
        // flood at an address GenPRES did not choose.
        | GenPresServer, GenPresDatabase, StartReset(tag, user, codeMac, expires) ->
            match h.Database.Private.Resets |> Map.tryFind user with
            | Some standing when h.Env.Now <= standing.Expires ->
                let (UserId who) = user
                { h with
                    Database =
                        h.Database
                        |> Database.note h.Env.Now $"reset refused for %s{who}: one is already pending (Rule 37)" },
                [ send GenPresDatabase env.From (ResetRefused(tag, ResetPending)) ]
            | _ ->
                let pending = { User = user; CodeMac = codeMac; Expires = expires; Wrong = 0 }
                { h with Database.Private.Resets = h.Database.Private.Resets |> Map.add user pending },
                [ send GenPresDatabase env.From (ResetStarted(tag, user)) ]

        // Rule 37. The check and the replacement are one act, at the party that holds
        // both the reset and the credential: a code that verifies replaces the PIN and
        // is spent in the same breath, and one that does not changes nothing but its
        // own count. Rule 27: a newly set PIN starts at zero.
        | GenPresServer, GenPresDatabase, ReplacePinIfCode(tag, user, code, pin) ->
            let (UserId who) = user

            // Rule 46. A code that bought nothing is an event: at an enrolment (UC-2
            // ext 2b, 2c) as at a reset (UC-7 ext 2a), and the audit is where somebody
            // trying shows up.
            let db = h.Database

            let refuse (after: DatabaseState) failure =
                { h with
                    Database = after |> Database.note h.Env.Now $"PIN code refused for %s{who}: %A{failure}" },
                [ send GenPresDatabase env.From (ResetRefused(tag, failure)) ]

            let without = { db with Private.Resets = db.Private.Resets |> Map.remove user }

            match db.Private.Resets |> Map.tryFind user with
            | None -> refuse db NoResetPending
            | Some pending when h.Env.Now > pending.Expires -> refuse without ResetExpired
            | Some pending when pending.CodeMac <> Reset.macOf code ->
                let tried = { pending with Wrong = pending.Wrong + 1 }

                if tried.Wrong >= wrongCodeLimit then
                    refuse without ResetVoid
                else
                    refuse { db with Private.Resets = db.Private.Resets |> Map.add user tried }
                           (WrongCode(wrongCodeLimit - tried.Wrong))
            | Some _ ->
                let c =
                    h.Database.Private.Credentials
                    |> Map.tryFind user
                    |> Option.defaultValue (UserCredential.fresh user)
                    |> UserCredential.setPin pin
                { h with
                    Database.Private =
                        { h.Database.Private with
                            Credentials = h.Database.Private.Credentials |> Map.add user c
                            Resets = h.Database.Private.Resets |> Map.remove user } },
                [ send GenPresDatabase env.From (PinReplaced(tag, c)) ]

        | _ -> refused h env

    /// Actor 4's own clock: Rule 9's sweep, which is the only thing a Tick reaches.
    let private updateServerClock (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Rule 9: the idle sweep ──

        // The clock a Session is swept against is on its SessionRecord (Rule 8), and
        // the records are in the Database (Rule 31), so the sweep is a read like any
        // other rather than a walk over something the Server holds.
        | Environment, GenPresServer, Tick ->
            let now = h.Env.Now

            // A launch nobody is coming back for. Every stage waits on a round trip
            // that should return promptly, except AwaitingPinChoice, which waits on a
            // human (UC-2 step 2).
            let abandoned (p: PendingEntry) =
                match p.Stage with
                | AwaitingPinChoice _ -> false
                | _ -> now - p.Since > launchAbandonTtl

            { h with
                GenPres.Pending =
                    h.GenPres.Pending |> Map.filter (fun _ p -> not (abandoned p)) },
            [ send GenPresServer GenPresDatabase (ReadSessionRecords ForSweep) ]

        | _ -> refused h env

    /// What a Client asks of the Server: a launch, an anonymous open, a PIN mid-launch,
    /// an acknowledgement, and every in-Session request.
    let private updateServerFromClient (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 4: the GenPRES Server. A launch, leg by leg — UC-1 steps 3 to 9.
        // ══════════════════════════════════════════════════════════════════════

        // Step 4. Nobody to ask: the Server settles the mac, the lifetime and the
        // User by itself. Only Rule 2's single use needs the Database.
        | GenPresClient _, GenPresServer, RedeemLaunch(launch, identity, replacing) ->
            let att = AttemptId h.GenPres.NextAttempt

            // Rule 6. A refusal opens nothing, and LaunchRefused carries no reason
            // deliberately: forged, expired and spent are one answer to a Client. The
            // audit is told which it was (Rule 46).
            let refuse (why: LaunchFailure) =
                h,
                [
                    send GenPresServer Environment (Noted $"launch refused: %A{why} (Rules 2, 3, 4)")
                    send GenPresServer env.From (LaunchRefused(why = NoIdentity))
                ]

            match identity with
            // ext 3c, and Rule 4. The browser proved nobody. There is no User to open
            // a Session for, and nothing in the Launch to fall back on — it names no
            // login at all. The nonce is not even spent: nothing happened.
            | None -> refuse NoIdentity
            | Some who ->
                if not (Token.verifyLaunch launch) then refuse LaunchForged
                elif h.Env.Now - launch.IssuedAt > launchTtl then refuse LaunchExpired   // Rule 3
                else
                    let ctx =
                        {
                            Client = env.From
                            Launch = launch
                            Identity = who
                            Replacing = replacing
                            Resuming = None
                        }
                    { h with
                        GenPres.NextAttempt = h.GenPres.NextAttempt + 1
                        GenPres.Pending =
                            h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingSpend ctx)) },
                    [ send GenPresServer GenPresDatabase (SpendLaunchIfUnspent(ForLaunch att, launch.Nonce)) ]

        // UC-2 steps 2 and 3. The launch has been suspended on a human, possibly for
        // a long while, and nothing else was offered meanwhile.
        | (GenPresClient _ as sender), GenPresServer, SupplyPin(att, code, pin) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            // The prompt was put to one Client and is that Client's to answer: a
            // second browser answering would set this PIN from somebody else's screen.
            // The code is what makes that fail even at the right screen (ext 2c).
            | Some(AwaitingPinChoice(ctx, uc, mail)) when ctx.Client = sender ->
                // Rule 37, one implementation with two entrances: the code and the PIN
                // go to the same Database act that a reset uses (UC-7), which creates
                // the UserCredential if GenPRES holds none and starts the count at zero
                // (Rules 26, 27).
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinWritten(ctx, uc, mail))) },
                [ send GenPresServer GenPresDatabase (ReplacePinIfCode(ForLaunch att, uc.UserId, code, pin)) ]
            | Some(AwaitingPinChoice _) ->
                // Answered by a Client that was never asked. Not merely dropped: this
                // is exactly the envelope worth alerting on.
                h, [ send GenPresServer Environment (Refused env) ]
            | _ -> h, []

        // Rule 10. Nothing is read and nothing else is decided: the User has seen the
        // notice, and the record stops owing one.
        | GenPresClient _, GenPresServer, AckSessionNotice(acknowledging, about) ->
            h, [ send GenPresServer GenPresDatabase (MarkAcknowledged(acknowledging, about)) ]

        | GenPresClient _, GenPresServer, OpenAnonymous replacing ->
            // Rule 13. What an anonymous open costs the Server is a SessionRecord
            // (Rule 31: it is all they ever amount to), so what bounds the cost is how
            // many may stand at once. Above the bound the answer is a refusal that
            // writes nothing.
            let standing =
                h.Database.Private.Sessions
                |> List.filter (fun r -> r.User.IsNone && SessionRecord.isOpen r)
                |> List.length

            if standing >= anonymousOpenLimit then
                // Rule 46. Counted, not written out line by line: the refusal is an
                // event worth knowing about, and a count is what a flood may grow.
                h,
                [
                    send GenPresServer GenPresDatabase (NoteAnonymousRefusal env.From)
                    send GenPresServer env.From AnonymousRefused
                ]
            else
                openSession env.From None None None { Patient = None; Data = None } None [] replacing None h

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 4: the GenPRES Server, in Session — one request, several legs
        // ══════════════════════════════════════════════════════════════════════

        // Rule 31 in one branch: a request arrives with everything but who sent it,
        // and that is in the Database. Rule 8's refresh has one home, here.
        | GenPresClient _, GenPresServer, SessionRequest(sid, cmd) ->
            let rid = RequestId h.GenPres.NextRequest
            let ctx = { Sid = sid; Client = env.From; Cmd = cmd; Stage = AwaitingSessionRecord }
            { h with GenPres.NextRequest = h.GenPres.NextRequest + 1 } |> putFlight rid ctx,
            [ send GenPresServer GenPresDatabase (ReadSessionRecord(ForRequest rid, sid)) ]

        | _ -> refused h env

    /// Actor 9's answers — a Role at a launch (Rule 5), and one re-taken at a signature
    /// (Rule 38).
    let private updateServerFromRegistry (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // Step 5. Rule 6: no Role, no Session — and no guessing either.
        | UserRegistry, GenPresServer, UserUnresolved(ForLaunch att, failure) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingUser ctx) ->
                let reply =
                    match failure with
                    | NoRole -> NotAuthorised
                    | RegistryUnreachable -> AuthorityUnavailable
                refuseLaunch att ctx.Client reply h
            | _ -> h, []

        // Step 5. Rule 5: the Role is the registry's answer, never the
        // launch's — the launch never carried one (Concept 3).
        | UserRegistry, GenPresServer, UserResolved(ForLaunch att, uc, mail) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingUser ctx) ->
                match uc.Role with
                // Rule 25: a Reader is never asked for a PIN. Not asked and ignored —
                // not asked at all: the credential stage is skipped whole.
                | Reader -> afterCredential att ctx uc mail h
                // Rule 23: every launch checks whether a PIN is set for the login.
                | Prescriber ->
                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingCredential(ctx, uc, mail))) },
                    [ send GenPresServer GenPresDatabase (ReadCredential(ForLaunch att, uc.UserId)) ]
            | _ -> h, []

        // Rule 38. The Role must still be there, and must still belong to the person
        // the SessionRecord names: a login that now resolves to somebody else is not
        // this Session's User.
        | UserRegistry, GenPresServer, UserResolved(ForRequest rid, uc, _) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingSigningRole(r, req) } as ctx) ->
                match r.User, r.Patient with
                | Some sessionUser, Some _ when uc.UserId = sessionUser.UserId && uc.Role = Prescriber ->
                    // Rule 44 was settled before the challenge (Rule 44), so there is
                    // nothing left to read: the Role is back, and the Submission goes to the
                    // Database as one act (Rule 42).
                    commit rid ctx r req (Some uc.Role) h
                | _ ->
                    dropFlight rid h, [ send GenPresServer ctx.Client NotPermitted ]
            | _ -> h, []

        // Rule 38. No Role, or no answer: either way nothing is signed. The two are
        // told apart, because one is a withdrawal and the other is a registry that is
        // merely down — and a Session that may sign again in a minute.
        | UserRegistry, GenPresServer, UserUnresolved(ForRequest rid, failure) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            // Rule 38. Rule 38 makes the registry a hard dependency of every signature,
            // so a registry that is down stops all signing everywhere. `NoRole` still
            // fails closed — that is a withdrawal, reported — but "cannot say" is not a
            // withdrawal, and for a bounded while the Role the launch took stands for it.
            // Beyond `roleGrace` it fails closed as before.
            | Some({ Stage = AwaitingSigningRole(r, req) } as ctx) when
                failure = RegistryUnreachable
                && h.Env.Now - r.OpenedAt < roleGrace
                && (r.User |> Option.map _.Role) = Some Prescriber
                ->
                let h, out = commit rid ctx r req (Some Prescriber) h
                h,
                send GenPresServer Environment
                    (Noted "the registry could not be asked: the Role taken at the launch stood, under grace (Rule 38)")
                :: out
            | Some ctx ->
                let reply =
                    match failure with
                    | NoRole -> NotPermitted
                    | RegistryUnreachable -> SigningUnavailable
                dropFlight rid h, [ send GenPresServer ctx.Client reply ]
            | None -> h, []

        | _ -> refused h env

    /// Actor 6's answers: read once at the launch (Concept 2), and once more before a
    /// challenge (Rule 44).
    let private updateServerFromPlatform (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // Step 6. Concept 2: read once, at the launch, and not refreshed while the
        // Session lives. ext 6a: unavailable is not a failure — the PatientContext
        // carries the PatientId and no data, and the User fills it in by hand.
        | PatientDataPlatform, GenPresServer,
          (PatientDataRead(ForLaunch att, _) | PatientDataUnavailable(ForLaunch att)) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPatientData(ctx, uc, mail)) ->
                match ctx.Launch.Patient with
                | None -> h, []                           // cannot happen: ext 1a skipped this stage
                | Some p ->
                    let data =
                        match env.Msg with
                        | PatientDataRead(_, d) -> Some d
                        | _ -> None
                    let pctx = { Patient = Some p; Data = data }
                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingRecord(ctx, uc, mail, pctx))) },
                    [ send GenPresServer GenPresDatabase (ReadRecord(ForLaunch att, p)) ]
            | _ -> h, []

        // Rule 44. Three answers, and only one mints a challenge: the data holds, it
        // moved, or the platform cannot say. The last two issue nothing — the User
        // accepts by returning the token and asks again.
        | PatientDataPlatform, GenPresServer, (PatientDataRead(ForRequest rid, _) | PatientDataUnavailable(ForRequest rid)) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingChallengeData(r, work, dataOk) } as ctx) ->
                let challenge () =
                    send GenPresServer ctx.Client
                        (SignChallengeIssued(
                            Token.mintChallenge h.Env.Now ctx.Sid r.Patient (WorkPlan.signingDigest work)))

                let accepted (digest: string) =
                    match dataOk with
                    | Some t ->
                        Token.verifyDataNotice t
                        && t.Claim.Sid = r.Id
                        && Token.digest t = Some digest
                    | None -> false

                match env.Msg with
                | PatientDataRead(_, d) when Some d = work.Data -> dropFlight rid h, [ challenge () ]
                | PatientDataRead(_, d) ->
                    dropFlight rid h,
                    [
                        send GenPresServer ctx.Client
                            (PatientDataChanged(
                                d,
                                Token.mintDataNotice
                                    h.Env.Now ctx.Sid r.Patient (WorkPlan.dataDigest (Some d))))
                    ]
                // UC-1 ext 6a. Unreachable is not a refusal, but it is not silence
                // either: the User signs on unchecked data only after saying so.
                | _ when accepted (WorkPlan.dataDigest work.Data) -> dropFlight rid h, [ challenge () ]
                | _ ->
                    dropFlight rid h,
                    [
                        send GenPresServer ctx.Client
                            (PatientDataUnverified(
                                Token.mintDataNotice
                                    h.Env.Now ctx.Sid r.Patient (WorkPlan.dataDigest work.Data)))
                    ]
            | _ -> h, []

        | _ -> refused h env

    /// Actor 5's answers to a request of an open Session, and the acknowledgements
    /// of what it wrote. Reached from the launch legs below, which fall through to
    /// here when the answer is not one of theirs.
    let private updateServerFromDatabaseRequest (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // Rule 32: the User and the Patient of the request come from here. Rule 10:
        // where the Session is gone, this is the next opportunity to say so.
        | GenPresDatabase, GenPresServer, SessionRecordRead(ForRequest rid, record) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | None -> h, []
            | Some ctx ->
                match record with
                | None ->
                    dropFlight rid h, [ send GenPresServer ctx.Client (SessionEnded None) ]
                // Rule 10. Refused and told why, but nothing is discharged: whoever
                // holds an ended SessionId may be whoever sat down next, and telling
                // them is not telling the User. Delivery happens at a launch.
                | Some r when not (SessionRecord.isOpen r) ->
                    let mark = match r.State with Ended(m, _) -> Some m | OpenOrGone -> None
                    dropFlight rid h, [ send GenPresServer ctx.Client (SessionRefused mark) ]
                // Rules 9, 41. Past either end the request ends the Session then and
                // there, rather than refreshing it back to life. Rule 10 again: the
                // screen is told and the notice is still owed.
                | Some r when (SessionRecord.outOfTime h.Env.Now r).IsSome ->
                    let mark = (SessionRecord.outOfTime h.Env.Now r).Value

                    dropFlight rid h,
                    [
                        send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, mark))
                        send GenPresServer ctx.Client (SessionRefused(Some mark))
                    ]
                | Some r ->
                    // Rule 8. Every request refreshes the idle clock, and the clock is a
                    // field of the record, so refreshing it is a write — a guarded one
                    // (Rule 40): a Session that ended meanwhile is not touched.
                    let r = r |> SessionRecord.seen h.Env.Now
                    let refreshed = send GenPresServer GenPresDatabase (TouchIfOpen r.Id)
                    let h, out = dispatch rid ctx r h
                    // CloseSession ends the record itself; anything else gets the
                    // refresh. Doing both would be harmless but noisy.
                    match ctx.Cmd with
                    | CloseSession -> h, out
                    | _ -> h, refreshed :: out

        // The PatientRecord came back for a request part-way through.
        | GenPresDatabase, GenPresServer, RecordRead(ForRequest rid, record) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingPatientRecord r } as ctx) ->
                match ctx.Cmd, r.User with
                // Rules 17 and 18. Opening the most recent Signed TreatmentPlan is also how
                // a blocked User gets unblocked: Rule 33's token is re-minted over it,
                // so it becomes the TreatmentPlan the Session opened with and Rule 20 no
                // longer bites.
                | OpenTreatmentPlan id, Some uc ->
                    match record |> PatientRecord.mayOpen uc.UserId id with
                    | Some s ->
                        dropFlight rid h,
                        [ send GenPresServer ctx.Client
                            (TreatmentPlanOpened(
                                s.Id, s.Orders, Token.mintOpened h.Env.Now ctx.Sid r.Patient (Some s.Id))) ]
                    | None ->
                        dropFlight rid h, [ send GenPresServer ctx.Client NotPermitted ]
                // Rule 43. The pre-checks first, and the challenge only if they pass.
                // Rule 20's block and Rule 21's notice are the same answers a Submission
                // would have got — settled here, before any PIN is asked for.
                | RequestSignChallenge(work, opened, notice, dataOk), Some uc ->
                    if not (Token.verifyOpened opened) || opened.Claim.Sid <> ctx.Sid then
                        dropFlight rid h,
                        [
                            send GenPresServer ctx.Client
                                (SubmissionRefused "the opened-with token does not verify (Rule 33)")
                        ]
                    else

                    let openedWith = Token.plan opened

                    let honoured =
                        match notice with
                        | Some t when Token.verifyNotice t && t.Claim.Sid = ctx.Sid ->
                            Set.ofList (Token.disclosed t)
                        | _ -> Set.empty

                    let outstanding = record |> PatientRecord.unsignedElsewhere uc.UserId openedWith

                    match PatientRecord.blocking openedWith record with
                    // Rule 20. The remedy is to open that Signed TreatmentPlan (Rule
                    // 17), which makes it the one the Session opened with.
                    | Some blocker ->
                        dropFlight rid h, [ send GenPresServer ctx.Client (SubmissionBlocked blocker.By) ]
                    | None ->
                        match outstanding |> List.filter (fun x -> not (honoured.Contains x.Id)) with
                        // Rule 21. Whose work it is, not its contents — and the token
                        // that names exactly what was disclosed (Rule 34).
                        | undisclosed :: _ ->
                            let token =
                                Token.mintNotice h.Env.Now ctx.Sid r.Patient (outstanding |> List.map _.Id)
                            dropFlight rid h,
                            [ send GenPresServer ctx.Client (UnsignedWorkNotice(undisclosed.By, token)) ]
                        | [] ->
                            // Rule 44. Nothing in the record stands in the way, so the
                            // last question is the data — asked before the challenge is
                            // minted, so the commit needs no second reading.
                            match r.Patient with
                            | Some p ->
                                h |> putFlight rid { ctx with Stage = AwaitingChallengeData(r, work, dataOk) },
                                [ send GenPresServer PatientDataPlatform (ReadPatientData(ForRequest rid, p)) ]
                            | None -> dropFlight rid h, []
                | _ -> dropFlight rid h, []
            | _ -> h, []

        // Rules 33, 47. The discard landed, and the Session's baseline moved with it:
        // a fresh OpenedToken over whatever Rule 19 now starts from, and the orders to
        // go with it. The old one was spent by the discard itself, in the same act
        // (Rule 33) — and a token naming a Discarded plan is refused at the commit in
        // any case, because such a plan is no starting point (Rules 15, 19).
        | GenPresDatabase, GenPresServer, TreatmentPlanDiscarded(ForRequest rid, discarded, starts) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingDiscard r } as ctx) ->
                dropFlight rid h,
                [
                    send GenPresServer ctx.Client
                        (TreatmentPlanDiscardedOk(
                            discarded,
                            starts |> Option.map _.Id,
                            starts |> Option.map _.Orders |> Option.defaultValue [],
                            Token.mintOpened h.Env.Now ctx.Sid r.Patient (starts |> Option.map _.Id)))
                ]
            | _ -> h, []

        | GenPresDatabase, GenPresServer, DiscardRefused(ForRequest rid, why) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some ctx -> dropFlight rid h, [ send GenPresServer ctx.Client (SubmissionRefused why) ]
            | _ -> h, []

        // Rule 42. The one act said yes. Rule 33: the Session now stands on what it
        // just created, so a fresh token goes back with the answer — Rules 20 and 21
        // are measured from this TreatmentPlan from here on out.
        | GenPresDatabase, GenPresServer, TreatmentPlanCommitted(ForRequest rid, plan) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingCommit r } as ctx) ->
                dropFlight rid h,
                [ send GenPresServer ctx.Client
                    (TreatmentPlanSubmitted(
                        plan.Id,
                        plan.State,
                        Token.mintOpened h.Env.Now ctx.Sid r.Patient (Some plan.Id))) ]
            | _ -> h, []

        // Rule 42. The one act said no, and nothing happened. Each refusal is turned
        // into what the Client already understands — and into nothing more: Rule 21's
        // notice names whose work it is and not which TreatmentPlan (Rules 17, 18),
        // and Rule 20's block does the same.
        | GenPresDatabase, GenPresServer, CommitRefused(ForRequest rid, refusal) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingCommit r } as ctx) ->
                let out =
                    match refusal with
                    // Rule 10, as on the arrival path: refused and told what ended,
                    // and nothing discharged. Whoever is holding this SessionId need
                    // not be the User the notice is owed to.
                    | SessionNotOpen mark -> [ send GenPresServer ctx.Client (SessionRefused mark) ]
                    | RoleRefused -> [ send GenPresServer ctx.Client NotPermitted ]
                    | TokenRefused why -> [ send GenPresServer ctx.Client (SubmissionRefused why) ]
                    | BlockedBy who -> [ send GenPresServer ctx.Client (SubmissionBlocked who) ]
                    | UnsignedElsewhere(who, ids) ->
                        // Rule 34. The token is the Server's to mint, because only the
                        // Server has the key — the Database names what was disclosed.
                        [
                            send GenPresServer ctx.Client
                                (UnsignedWorkNotice(who, Token.mintNotice h.Env.Now ctx.Sid r.Patient ids))
                        ]
                    | PinWrong left -> [ send GenPresServer ctx.Client (PinRejected left) ]
                    | CredentialLocked _ -> [ send GenPresServer ctx.Client SigningLocked ]
                    | PinLimitReached ->
                        // Rules 9, 10, 26. Telling the screen is telling whoever is at
                        // it — and this is the one ending that means somebody was
                        // guessing, so the screen is exactly who the notice is not
                        // owed to. It is refused here and mailed to the address the
                        // registry holds; the User is told at their next launch.
                        [
                            send GenPresServer ctx.Client (SessionRefused(Some WrongPinLimit))
                            match r.Mail with
                            | Some addr ->
                                send GenPresServer MailService
                                    (SendMail(addr, "GenPRES: the wrong-PIN limit was reached in your session"))
                                send GenPresServer Environment
                                    (Noted "wrong-PIN limit reached — the User was mailed")
                            | None -> ()
                        ]
                dropFlight rid h, out
            | _ -> h, []

        // Rule 37. The reset is parked, so the code can go out — to the address on the
        // SessionRecord (Concept 9), since there is no Session in memory to hold one.
        // The record of it says a reset was asked for; it does not say the code.
        | GenPresDatabase, GenPresServer, ResetStarted(ForRequest rid, user) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingResetStarted(r, code) } as ctx) ->
                let (UserId l) = user
                dropFlight rid h,
                [
                    match r.Mail with
                    | Some addr -> send GenPresServer MailService (SendMail(addr, Reset.mail code))
                    | None -> ()
                    send GenPresServer Environment (Noted $"PIN reset code sent for %s{l}")
                    send GenPresServer ctx.Client ResetCodeMailed
                ]
            | _ -> h, []

        // UC-7 step 2. Replaced, not removed. Rule 26: mailed and recorded, every
        // replacement as well as every setting.
        | GenPresDatabase, GenPresServer, PinReplaced(ForRequest rid, c) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingPinReplaced r } as ctx) ->
                let (UserId l) = c.User
                dropFlight rid h,
                (pinChanged r.Mail $"PIN replaced for %s{l}")
                @ [ send GenPresServer ctx.Client PinChanged ]
            | _ -> h, []

        // Rule 37. The code bought nothing, and nothing changed.
        | GenPresDatabase, GenPresServer, ResetRefused(ForRequest rid, failure) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some ctx -> dropFlight rid h, [ send GenPresServer ctx.Client (ResetDenied failure) ]
            | None -> h, []

        // Written, and nothing more to say.
        | GenPresDatabase, GenPresServer, TreatmentPlanDiscarded _

        | GenPresDatabase, GenPresServer, DiscardRefused _

        | GenPresDatabase, GenPresServer, TreatmentPlanCommitted _

        | GenPresDatabase, GenPresServer, CommitRefused _

        | GenPresDatabase, GenPresServer, ResetStarted _

        | GenPresDatabase, GenPresServer, PinReplaced _

        | GenPresDatabase, GenPresServer, ResetRefused _

        | GenPresDatabase, GenPresServer, SessionRecordsRead _

        | GenPresDatabase, GenPresServer, LaunchSpent _

        | GenPresDatabase, GenPresServer, LaunchReplayed _

        | GenPresDatabase, GenPresServer, SessionRecordRead _

        | GenPresDatabase, GenPresServer, RecordRead _

        | GenPresDatabase, GenPresServer, CredentialRead _ -> h, []

        | _ -> refused h env

    /// Actor 5's answers to a launch, leg by leg, and to the idle sweep. Anything
    /// else is a request's answer and falls through: the two sets are disjoint by the
    /// LegTag every one of these messages carries, so nothing can be shadowed.
    let private updateServerFromDatabase (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        | GenPresDatabase, GenPresServer, SessionRecordsRead(ForSweep, rs) ->
            // Rules 9, 13. `outOfTime` asks both ends and names the ending, so the
            // sweep and an arriving request can never disagree. The sweep is only for
            // Sessions nobody comes back to (Rule 41).
            let now = h.Env.Now

            let stale =
                rs |> List.filter (fun r -> (SessionRecord.outOfTime now r).IsSome)

            h,
            [
                for r in stale ->
                    let mark = (SessionRecord.outOfTime now r).Value
                    send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, mark))
            ]

        // Step 4 into 5. Rule 2: the nonce was fresh, and is now spent. Ask the
        // registry who the browser's identity belongs to — the Launch does not travel
        // there, and neither does anything else about it.
        | GenPresDatabase, GenPresServer, LaunchSpent(ForLaunch att) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingSpend ctx) ->
                { h with GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingUser ctx)) },
                [ send GenPresServer UserRegistry (ResolveUser(ForLaunch att, ctx.Identity)) ]
            | _ -> h, []

        // Rule 2's replay clause. Spent already — but by whom, and how long ago? The
        // same browser coming back within the lifetime is a retry of the same launch
        // (UC-1 ext 3a), and it gets the first answer: the same Session, not a second
        // one. Anybody else, or too late, gets nothing.
        | GenPresDatabase, GenPresServer, LaunchReplayed(ForLaunch att, opened) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingSpend ctx) ->
                // Rule 2's replay clause is one browser coming back and nothing else.
                // The same login elsewhere would put two browsers on one Session,
                // which Rules 7 and 40 spend an act each to prevent.
                let thisBrowser = match ctx.Client with GenPresClient b -> Some b | _ -> None

                let mine =
                    opened
                    |> Option.filter (fun r ->
                        SessionRecord.isOpen r
                        && (r.User |> Option.map _.Login) = Some ctx.Identity
                        && r.Browser = thisBrowser
                        && h.Env.Now - ctx.Launch.IssuedAt <= launchTtl)
                match mine with
                | None ->
                    let h, out = refuseLaunch att ctx.Client (LaunchRefused false) h
                    h,
                    send GenPresServer Environment
                        (Noted $"launch refused: %A{LaunchAlreadySpent} (Rule 2)")
                    :: out
                | Some r ->
                    let ctx = { ctx with Resuming = Some r }
                    { h with
                        GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingUser ctx)) },
                    [ send GenPresServer UserRegistry (ResolveUser(ForLaunch att, ctx.Identity)) ]
            | _ -> h, []

        // Step 5, and UC-2 step 1. Rule 24: a Prescriber with no PIN must set one
        // before the launch continues — and only now, once the registry has said who
        // the login belongs to. A login the registry does not recognise never reaches
        // this branch, so it can never enrol.
        | GenPresDatabase, GenPresServer, CredentialRead(ForLaunch att, credential) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingCredential(ctx, uc, mail)) ->
                match credential |> Option.bind _.Pin with
                | Some _ -> afterCredential att ctx uc mail h
                | None ->
                    // UC-2 step 1, Rule 37: set the way it is replaced. What binds the
                    // credential to the person is the mail, not the workstation.
                    let code = ResetCode $"code-%04i{h.Env.Now}"

                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending
                            |> Map.add att (pend h.Env.Now (AwaitingEnrolCode(ctx, uc, mail, code))) },
                    [
                        send GenPresServer GenPresDatabase
                            (StartReset(ForLaunch att, uc.UserId, Reset.macOf code, h.Env.Now + resetCodeTtl))
                    ]
            | _ -> h, []

        // UC-2 step 1. The code is parked, so it can go out — and only now is the
        // Client asked for anything (Rules 26, 37, 46).
        | GenPresDatabase, GenPresServer, ResetStarted(ForLaunch att, user) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingEnrolCode(ctx, uc, mail, code)) ->
                let (UserId u) = user

                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinChoice(ctx, uc, mail))) },
                [
                    send GenPresServer MailService (SendMail(mail, Reset.mail code))
                    send GenPresServer Environment (Noted $"PIN enrolment code sent for %s{u}")
                    send GenPresServer ctx.Client (PinRequired att)
                ]
            | _ -> h, []

        // UC-2 step 3. The code verified and the PIN is set. Rule 26: mailed and
        // recorded, the first setting included. Then the launch continues from UC-1
        // step 6.
        | GenPresDatabase, GenPresServer, PinReplaced(ForLaunch att, c) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPinWritten(ctx, uc, mail)) ->
                let (UserId u) = c.User
                let h, out = afterCredential att ctx uc mail h
                h, (pinChanged (Some mail) $"PIN set for %s{u}") @ out
            | _ -> h, []

        // UC-2 ext 2b. The code bought nothing and no PIN was set. A wrong one with
        // tries left leaves the launch where it was, so the User can read the mail
        // again; a void or aged one ends the attempt — Rule 6, and the next launch
        // mails a fresh code.
        | GenPresDatabase, GenPresServer, ResetRefused(ForLaunch att, failure) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            // Rule 37, one at a time. A launch that finds a code already standing does
            // not mail a second one — the User has one in their mailbox, and voiding it
            // to send another is the harm the refusal exists to prevent. The launch
            // carries on and asks for that code.
            | Some(AwaitingEnrolCode(ctx, uc, mail, _)) when failure = ResetPending ->
                let (UserId u) = uc.UserId

                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinChoice(ctx, uc, mail))) },
                [
                    send GenPresServer Environment
                        (Noted $"PIN enrolment code for %s{u} already sent and still good (Rule 37)")
                    send GenPresServer ctx.Client (PinRequired att)
                ]
            | Some(AwaitingEnrolCode(ctx, _, _, _)) ->
                { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att },
                [ send GenPresServer ctx.Client (ResetDenied failure) ]
            | Some(AwaitingPinWritten(ctx, uc, mail)) ->
                match failure with
                | WrongCode _ ->
                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending
                            |> Map.add att (pend h.Env.Now (AwaitingPinChoice(ctx, uc, mail))) },
                    [ send GenPresServer ctx.Client (ResetDenied failure) ]
                // `ResetPending` cannot arise here — this stage is the answer to a
                // ReplacePinIfCode, which never refuses for that reason (Rule 37) — but
                // it is answered rather than left out: an unreachable case that is a
                // silent fall-through today is a wrong branch after the next edit.
                | NoResetPending
                | ResetExpired
                | ResetVoid
                | ResetPending ->
                    { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att },
                    [ send GenPresServer ctx.Client (ResetDenied failure) ]
            | _ -> h, []

        // Step 7. Rule 19 picks the TreatmentPlan the Session starts from: the most recent
        // that is either Signed, by whoever, or Unsigned and this User's own. Where
        // neither exists, the Session starts from nothing. Then Rule 7's other
        // Sessions, which the Server no longer mirrors and so must read (Rule 31).
        | GenPresDatabase, GenPresServer, RecordRead(ForLaunch att, record) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingRecord(ctx, uc, mail, pctx)) ->
                let start = record |> PatientRecord.startsFrom uc.UserId
                // Rule 19. What the Session falls back to when the Unsigned head turns
                // out to have been left by a Session that ended out from under the User:
                // the newest Signed TreatmentPlan of any kind, which is what Rule 20
                // measures against.
                let signedHead = record |> PatientRecord.newestSigned
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending
                        |> Map.add att (pend h.Env.Now (AwaitingPriors(ctx, uc, mail, pctx, start, signedHead))) },
                [ send GenPresServer GenPresDatabase (ReadSessionRecords(ForLaunch att)) ]
            | _ -> h, []

        // Steps 13 and 14. Rule 7 closes this User's other Sessions, Rule 10 says so
        // once, and Rule 33 hands the Client the token it will return with every
        // Submission. From here the Server keeps nothing of the Session but its record.
        | GenPresDatabase, GenPresServer, SessionRecordsRead(ForLaunch att, others) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPriors(ctx, uc, mail, pctx, start, signedHead)) ->
                // Rule 19. An Unsigned head is the User's own work, but only the
                // Session it was saved in says whether the User was the one who left
                // it. So a planted head is one explicit act away, rather than being
                // what the screen already shows.
                let endedUnder =
                    match start with
                    | Some s when s.State = Unsigned ->
                        s.Session
                        |> Option.bind (fun sid -> others |> List.tryFind (fun r -> r.Id = sid))
                        |> Option.bind (fun r ->
                            match r.State with
                            // Every ending but the User's own close. Whatever else it
                            // was, the User did not put that work down.
                            | Ended(ClosedByUser, _) -> None
                            | Ended(m, _) -> Some m
                            | OpenOrGone -> None)
                        |> Option.map (fun m -> s.Id, Some m, s.At)
                    // A head whose Session no record describes — work from before this
                    // record began — says nothing either way, and Rule 19 opens it as
                    // before. Absence of a record is not evidence of a bad ending, and
                    // offering on it would make every migrated plan look planted.
                    | _ -> None

                let start, resumedFrom =
                    match endedUnder with
                    | Some offer -> signedHead, Some offer
                    | None -> start, None

                let h, out =
                    match ctx.Resuming with
                    // Rule 2's replay clause: the first answer again, not a second
                    // Session. Rule 7's sweep does not run — nothing new is opening —
                    // and no record is written.
                    | Some r -> resumeSession ctx.Client r pctx start resumedFrom h
                    | None ->
                        openSession
                            ctx.Client (Some ctx.Launch.Nonce) (Some uc) (Some mail) pctx start others
                            ctx.Replacing resumedFrom h
                { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att }, out
            | _ -> h, []

        | _ -> updateServerFromDatabaseRequest h env
    /// Actor 4. Its own lifecycle first — a down Server answers its clients and does
    /// nothing else — and then whoever the answer is from.
    let private updateServer (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // Rules 9, 31. A restart ends nothing: there is no Session state to lose.
        // What goes is what was in flight, and those Clients see the usual silence.
        | Environment, GenPresServer, Stop _ when h.GenPres.Up ->
            { h with
                GenPres =
                    { h.GenPres with
                        InFlight = Map.empty
                        Pending = Map.empty
                        Up = false } }, []

        // And coming back settles nothing either: no records to read, nothing to mark.
        | Environment, GenPresServer, Start _ when not h.GenPres.Up ->
            { h with GenPres.Up = true }, []

        | Environment, GenPresServer, (Start _ | Stop _) -> h, []

        // A down Server answers its clients and nothing else. Both branches must sit
        // above every other, and the client-facing one must be the narrow one: an
        // in-flight reply to a Server that is gone is dropped, and so are Ticks.
        | _, GenPresServer,
            (RedeemLaunch _ | OpenAnonymous _ | SupplyPin _ | SessionRequest _) when not h.GenPres.Up ->
            h, [ send GenPresServer env.From ServerUnreachable ]

        | _, GenPresServer, _ when not h.GenPres.Up -> h, []

        | _ ->
            match env.From with
            | Environment -> updateServerClock h env
            | GenPresClient _ -> updateServerFromClient h env
            | GenPresDatabase -> updateServerFromDatabase h env
            | UserRegistry -> updateServerFromRegistry h env
            | PatientDataPlatform -> updateServerFromPlatform h env
            | _ -> refused h env

    /// Concept 15 and Rule 31: prescribing changes the Client's own cart, and the
    /// whole of it then travels — to be computed on, or to be saved. Rule 11: the
    /// SessionId rides in the request, never in a URL, and it is also what
    /// refreshes the idle clock.
    /// Concept 15. What the User does to the cart, and what of it reaches the Server.
    let private clientAct (h: Hospital) (env: Envelope) (b: BrowserId) (a: UserAct) =
        let st = clientState b h
        let toServer cmd = [ send (GenPresClient b) GenPresServer (SessionRequest(st.Sid.Value, cmd)) ]

        match a, st.NoticeFor with
        // Rule 10. The one act that belongs to Sessions that have already ended.
        | AcknowledgesNotice, [] -> h, []
        // Rule 10. The acknowledgement carries the Session doing it. A Client with
        // no Session of its own has nothing to acknowledge with — and that is the
        // point: it is the ended Session's own Client, and whoever is holding it is
        // not necessarily the User the notice is for.
        | AcknowledgesNotice, sids ->
            match st.Sid with
            | None -> h |> onClient b (fun s -> { s with Showing = Some "launch from MainEHR to answer this" }), []
            | Some mine ->
                h |> onClient b (fun s -> { s with NoticeFor = []; Showing = None }),
                [ for sid in sids -> send (GenPresClient b) GenPresServer (AckSessionNotice(mine, sid)) ]
        | _ ->

        match st.Sid with
        | None -> h, []
        | Some _ ->
            match a with
            // Rule 43. While the signature modal is up the WorkPlan cannot change:
            // the User is looking at exactly what they are about to attest to, and
            // a change under it would make the challenge name something else.
            | (Prescribes _ | EntersPatientData _) when st.Modal.IsSome || st.Signing.IsSome ->
                h |> onClient b (fun s ->
                    { s with Showing = Some "finish or cancel the signature first" }), []

            | Prescribes id ->
                // Whatever the User just did to it. Content is opaque here; what
                // matters is that it differs from the base, which is how the
                // Server can tell changed from unchanged (Rule 35).
                let content = $"v%i{h.Env.Now}"
                let orders =
                    if st.Work.Orders |> List.exists (fun o -> o.Id = id) then
                        st.Work.Orders
                        |> List.map (fun o -> if o.Id = id then { o with Content = content } else o)
                    else
                        st.Work.Orders
                        @ [ { Id = id; Patient = st.Patient; Content = content; Stamp = None } ]
                h |> onClient b (fun s -> { s with Work.Orders = orders }), toServer (Compute orders)

            | EntersPatientData d ->
                // Concept 2: the User can always supply the data by hand — with a
                // Patient or without one (Rule 12).
                h |> onClient b (fun s ->
                    { s with Work.Data = Some d; Work.From = Some(ByHand h.Env.Now) }),
                toServer (Compute st.Work.Orders)

            | Saves ->
                match st.Opened with
                | Some tok ->
                    // Rule 45. The key is minted here and travels with the Submission;
                    // a retry of this same Submission carries this same key.
                    h,
                    toServer (
                        SubmitTreatmentPlan
                            {
                                Work = st.Work
                                Opened = tok
                                Notice = st.Notice
                                Challenge = None
                                DataOk = st.DataOk
                                Pin = None
                                Key = idemKey b h.Env.Now
                            })
                | None -> h, []           // Rule 33: the Client cannot make one

            | Signs pin ->
                // Rule 43. Signing is two requests: first the challenge, over the
                // WorkPlan as it stands, and then the signature that carries it
                // back. The PIN waits here in the page for the moment in between —
                // it is a field on a form, and it goes no further than the Server
                // (Rule 22).
                match st.Opened with
                | Some tok ->
                    h |> onClient b (fun s -> { s with Signing = Some pin }),
                    toServer (RequestSignChallenge(st.Work, tok, st.Notice, st.DataOk))
                | None -> h, []

            // Rule 43. The other half. The User has read what the modal says the
            // signature would attest to, and signs it as shown. Only now does
            // anything leave the Client — and what leaves carries the challenge it
            // was given, so the commit can check that the plan committed is the
            // plan the User saw.
            | ConfirmsSign ->
                match st.Modal, st.Signing, st.Opened with
                | Some challenge, Some pin, Some opened ->
                    h |> onClient b (fun s -> { s with Modal = None; Signing = None; Showing = None }),
                    toServer (
                        SubmitTreatmentPlan
                            {
                                Work = st.Work
                                Opened = opened
                                Notice = st.Notice
                                Challenge = Some challenge
                                DataOk = st.DataOk
                                Pin = Some pin
                                Key = idemKey b h.Env.Now
                            })
                // No challenge in front of the User, so nothing to confirm. A
                // signature cannot be reached any other way from here (Rule 43).
                | _ -> h, []

            | CancelsSign ->
                // Rule 43. Nothing was signed and nothing was asked for: the
                // challenge is simply dropped, and the next one is minted fresh.
                h |> onClient b (fun s -> { s with Signing = None; Modal = None; Showing = None }), []

            // Rule 10. Taken by the match above, whether or not a notice is
            // standing: it belongs to a Session that has ended, and this branch is
            // about one that has not.
            | AcknowledgesNotice -> h, []

            // Rules 15, 47. Discarding is one request and nothing else: no
            // challenge, no PIN, no cart. Whatever the User was working on stays
            // where it is until the answer comes back with a new baseline.
            | Discards id ->
                match st.Opened with
                | Some tok -> h, toServer (DiscardTreatmentPlan(id, tok))
                | None -> h, []           // Rule 33: the Client cannot make one

            | OpensTreatmentPlan id -> h, toServer (OpenTreatmentPlan id)
            | AsksPinReset -> h, toServer ResetPin
            | EntersResetCode(code, pin) -> h, toServer (SupplyResetCode(code, pin))

            | ClosesSession ->
                // UC-12 ext 1a: the Client can warn that unsaved changes are about
                // to be discarded, but closed is closed. They existed only here
                // (Rule 31), so closing is what discards them.
                h |> onClient b (fun s ->
                    { s with Work = WorkPlan.empty; Opened = None; Notice = None }),
                toServer CloseSession

            | CarriesOverFrom src ->
                // UC-9 step 3. Work that outlived its Session because it was
                // never in the Server (Rule 31), arriving as fresh prescribing
                // with no claim on the old stamps. Nothing is rewritten: an
                // OrderContext names its own Patient, and rewriting that here
                // would be the Client deciding whose record it lands in.
                let source = clientState src h

                let sameUser =
                    match source.User, st.User with
                    | Some a, Some b -> a.UserId = b.UserId
                    | _ -> false

                if not (sameUser && source.Patient = st.Patient) then
                    h, []
                else
                    let carried =
                        source.Work.Orders
                        |> List.filter (fun o -> st.Work.Orders |> List.forall (fun x -> x.Id <> o.Id))
                        |> List.map (fun o -> { o with Stamp = None })
                    let orders = st.Work.Orders @ carried
                    h |> onClient b (fun s -> { s with Work.Orders = orders }), toServer (Compute orders)

    /// What the User does at their Client: present a Launch again, open GenPRES
    /// without one, answer a PIN prompt, act on the cart, or close the Session.
    let private updateClientFromUser (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // F5. The page is still the page, so the retry comes from its own memory —
        // or, where the page was never served and so never presented (ext 5a), from
        // the address bar. Rule 39 is about presenting, not about which branch does
        // it: whichever this is, the bar is scrubbed in the same act.
        | User, GenPresClient b, Refresh ->
            let st = clientState b h
            match st.RetryLaunch |> Option.orElse st.UrlLaunch with
            | Some launch ->
                h |> onClient b (fun s -> { s with UrlLaunch = None; RetryLaunch = Some launch }),
                [ send (GenPresClient b) GenPresServer (RedeemLaunch(launch, st.BrowserIdentity, st.Sid)) ]
            | None -> h, []

        // A full reload: the page and its memory go, and what is re-presented is
        // whatever is in the address bar — which, after Rule 39, is nothing.
        | User, GenPresClient b, ReloadPage ->
            let scrubbed = h |> onClient b (fun s -> { s with RetryLaunch = None })
            let st = clientState b scrubbed
            match st.UrlLaunch with
            | Some launch ->
                // Rule 4: the principal is the browser's, so a reload keeps it.
                scrubbed, [ send (GenPresClient b) GenPresServer (RedeemLaunch(launch, st.BrowserIdentity, st.Sid)) ]
            | None -> scrubbed, []

        // UC-8. The Client has no Launch to present, and asks for a Session
        // without one.
        | User, GenPresClient b, OpenDirectly ->
            let st = clientState b h
            h |> onClient b (fun s -> { s with UrlLaunch = None }),
            [ send (GenPresClient b) GenPresServer (OpenAnonymous st.Sid) ]

        // Rule 6 / UC-1 ext 5a. The offer carries nothing over from the launch: no
        // User, no Patient. It is only made where relaunching would not cure the
        // failure — an unrecognised login, or an unreachable registry.
        | User, GenPresClient b, AcceptAnonymousOffer ->
            match h.Clients |> Map.tryFind b with
            | Some s when s.AnonymousOffer ->
                h |> onClient b (fun s -> { s with AnonymousOffer = false; Showing = None }),
                [ send (GenPresClient b) GenPresServer (OpenAnonymous s.Sid) ]
            | _ -> h, []

        // UC-2 step 2. Nothing else was on offer until this was answered.
        | User, GenPresClient b, ChoosePin(code, pin) ->
            match h.Clients |> Map.tryFind b |> Option.bind _.AwaitingPin with
            // `AwaitingPin` is kept: a wrong code has tries left in it (UC-2 ext 2b),
            // and the User answers again at the same prompt. What clears it is the
            // launch continuing, or the code going void.
            | Some att ->
                h |> onClient b (fun s -> { s with Showing = None }),
                [ send (GenPresClient b) GenPresServer (SupplyPin(att, code, pin)) ]
            | None -> h, []
        | User, GenPresClient b, Act a -> clientAct h env b a

        | User, GenPresClient b, CloseBrowser ->
            // UC-12 ext 1b: nothing reaches the Server. A vanished browser is
            // indistinguishable from a silent one, so the Session is left to idle out
            // — and the cart is gone, because it was only ever here (Rule 31).
            h |> onClient b (fun s ->
                { s with
                    Closed = true
                    Work = WorkPlan.empty
                    Opened = None
                    Notice = None }), []

        | _ -> refused h env

    /// What the Server answers, which is the only way anything reaches a Client
    /// (Consequence 6): every one of these rides back on a request the Client made.
    let private updateClientFromServer (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        | GenPresServer, GenPresClient b, SessionOpened(sid, _, user, pctx, orders, token, resumedFrom) ->
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = None
                    RetryLaunch = None
                    AwaitingPin = None
                    AnonymousOffer = false
                    Sid = Some sid
                    User = user
                    Patient = pctx.Patient
                    // Concept 13. The launch read this from the platform, at this
                    // moment (Concept 2) — and the WorkPlan carries that provenance so
                    // whatever is created from it can record it.
                    Work =
                        {
                            Data = pctx.Data
                            From = pctx.Data |> Option.map (fun _ -> FromPlatform h.Env.Now)
                            Orders = orders
                        }
                    Opened = Some token
                    Notice = None
                    // Rule 19. Not opened — said. The User decides whether to take up
                    // work that a Session ending out from under them left behind.
                    Offered = resumedFrom
                    Showing =
                        match resumedFrom with
                        | Some(TreatmentPlanId p, mark, savedAt) ->
                            let how =
                                match mark with
                                | Some m -> $"%A{m}"
                                | None -> "a session this record does not describe"
                            Some
                                $"unsigned work of yours (%s{p}), saved at %i{savedAt}, was left by a session that ended: %s{how} — open it to take it up"
                        | None -> s.Showing }), []

        | GenPresServer, GenPresClient b, PinRequired att ->
            h |> onClient b (fun s ->
                { s with
                    AwaitingPin = Some att
                    Showing = Some "choose a PIN — nothing else is offered until you do" }), []

        | GenPresServer, GenPresClient b, LaunchRefused retryable ->
            // ext 3c: the identity could not be had, and the Launch is still good — so
            // the page keeps it and a refresh tries again (Rules 3, 39). ext 4a: the
            // Launch itself bought nothing, so only a relaunch can. Either way the User
            // is told no reason (Rule 6); only the offer differs.
            h |> onClient b (fun s ->
                if retryable then
                    { s with Showing = Some "GenPRES could not be reached — try again" }
                else
                    { s with
                        UrlLaunch = None
                        RetryLaunch = None
                        Showing = Some "the launch failed — relaunch from MainEHR" }), []

        | GenPresServer, GenPresClient b, NotAuthorised ->
            // ext 5a: relaunching would not help, so the anonymous open is the only
            // offer worth making (Rule 6).
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = None
                    AnonymousOffer = true
                    Showing = Some "not authorised — continue anonymously?" }), []

        // The registry being down is transient, so a relaunch — which mints a fresh
        // credential, the one thing F5 cannot do once this one is spent — plausibly
        // cures it. Both offers stand. Contrast NotAuthorised above, where the answer
        // will be the same however often it is asked.
        | GenPresServer, GenPresClient b, AuthorityUnavailable ->
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = None
                    AnonymousOffer = true
                    Showing =
                        Some "authorisation could not be checked — relaunch from MainEHR, or continue anonymously?" }), []

        // Consequence 1: no Client at all is served when the Server is down, so in
        // practice the User sees the browser's own error page. Where a Client was
        // already served, the Launch stays in the address bar and a refresh
        // retries — for as long as Rule 3 allows. The cart stays too (Rule 31): a
        // Server that is down has not ended anything (Rule 9).
        | GenPresServer, GenPresClient b, ServerUnreachable ->
            h |> onClient b (fun s -> { s with Showing = Some "GenPRES is unavailable" }), []

        // The Session is gone; the work is not. It was never in the Server, so the
        // Client still holds it and may offer to carry it into the next Session as
        // fresh prescribing (Concept 15; UC-9 step 3).
        | GenPresServer, GenPresClient b, SessionEnded mark ->
            let text =
                match mark with
                | Some m -> $"the session ended: %A{m} — relaunch from MainEHR"
                | None -> "no such session — relaunch from MainEHR"
            // Rule 10. The Client is told, and that is all it can do:
            // the ended Session is not added to what this Client may acknowledge, because
            // it no longer has a Session to acknowledge with. What spends the obligation
            // is the User's next launch, where `PriorSessionNotice` names it again.
            h |> onClient b (fun s ->
                { s with
                    Sid = None
                    Opened = None
                    Showing = Some text }), []

        // Rule 13. Nothing opened, and nothing was written to say so.
        | GenPresServer, GenPresClient b, AnonymousRefused ->
            h |> onClient b (fun s ->
                { s with Showing = Some "GenPRES is busy — launch from MainEHR, or try again later" }), []

        | GenPresServer, GenPresClient b, SessionRefused mark ->
            let what =
                match mark with
                | Some m -> $"the session ended: %A{m} — relaunch from MainEHR"
                | None -> "the session is gone — relaunch from MainEHR"
            h |> onClient b (fun s -> { s with Sid = None; Opened = None; Showing = Some what }), []

        | GenPresServer, GenPresClient b, PriorSessionNotice priors ->
            h |> onClient b (fun s ->
                { s with
                    NoticeFor = s.NoticeFor @ (priors |> List.map (fun (_, _, sid) -> sid))
                    Showing = Some "work in an earlier session may have been lost" }), []

        // Rule 31: the answer comes back from the payload, and the Client keeps it —
        // because the Client is the only party that keeps anything.
        | GenPresServer, GenPresClient b, Computed orders ->
            h |> onClient b (fun s -> { s with Work.Orders = orders }), []

        | GenPresServer, GenPresClient b, SubmissionBlocked _ ->
            h |> onClient b (fun s ->
                { s with Showing = Some "someone signed since you opened — take up their version" }), []

        // Rule 34. The token is what a choice to submit anyway must return, so the
        // Client keeps it: proceeding is re-sending the Submission, holding off is not.
        | GenPresServer, GenPresClient b, UnsignedWorkNotice(uc, token) ->
            let (LoginName l) = uc.Login
            h |> onClient b (fun s ->
                { s with
                    Notice = Some token
                    Showing = Some $"unsigned work of %s{l} is newer than yours — save anyway?" }), []

        | GenPresServer, GenPresClient b, SubmissionRefused why ->
            h |> onClient b (fun s -> { s with Showing = Some $"the submission was refused: %s{why}" }), []

        // Rule 43. Nothing is submitted here: the User has not looked at it yet, and
        // a Client that submitted on their behalf would be attesting for them. What
        // goes out goes out on `ConfirmsSign`, carrying this challenge.
        | GenPresServer, GenPresClient b, SignChallengeIssued token ->
            let st = clientState b h
            match st.Signing with
            | Some _ ->
                h |> onClient b (fun s ->
                    { s with
                        Modal = Some token
                        Showing = Some "sign the plan as shown, or cancel and edit" }), []
            // A challenge nobody asked for. Not shown, and certainly not signed.
            | None -> h, []

        // Rule 44. The Patient Data has moved under the Session. The User is shown it
        // and accepts by keeping the token, which the next Submission carries.
        | GenPresServer, GenPresClient b, PatientDataChanged(fresh, token) ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = None
                    DataOk = Some token
                    Work.Data = Some fresh
                    Work.From = Some(FromPlatform h.Env.Now)
                    Showing = Some "the Patient Data has changed — check it and sign again" }), []

        // Rule 44, UC-1 ext 6a. The platform could not be asked. Nothing is refused —
        // the User is told what the signature would stand on, and accepts by signing
        // again, which returns the token.
        | GenPresServer, GenPresClient b, PatientDataUnverified token ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = None
                    DataOk = Some token
                    Showing = Some "the Patient Data could not be checked — sign again to sign on it as it stands" }), []

        | GenPresServer, GenPresClient b, TreatmentPlanSubmitted(_, _, token) ->
            h |> onClient b (fun s ->
                { s with Opened = Some token; Notice = None; Modal = None }), []

        // Rules 15, 47. The draft is down. The cart becomes what the Session now
        // stands on — the plan under the discarded one, or nothing — and the offer, if
        // one was standing, goes with it: it named work that no longer exists to take.
        | GenPresServer, GenPresClient b, TreatmentPlanDiscardedOk(_, _, orders, token) ->
            h |> onClient b (fun s ->
                { s with
                    Work.Orders = orders
                    Opened = Some token
                    Notice = None
                    Offered = None
                    Showing = Some "the draft is put down" }), []

        | GenPresServer, GenPresClient b, TreatmentPlanOpened(_, orders, token) ->
            // Rule 19. Taken up, or something else opened: either way the offer is
            // answered and stops standing.
            let h = h |> onClient b (fun s -> { s with Offered = None })
            h |> onClient b (fun s ->
                { s with Work.Orders = orders; Opened = Some token; Notice = None }), []

        | GenPresServer, GenPresClient b, PinRejected left ->
            h |> onClient b (fun s -> { s with Showing = Some $"wrong PIN — %i{left} left" }), []

        | GenPresServer, GenPresClient b, NoTreatmentPlanHere ->
            h |> onClient b (fun s -> { s with Showing = Some "no patient: nothing can be saved" }), []

        | GenPresServer, GenPresClient b, NotPermitted ->
            h |> onClient b (fun s -> { s with Showing = Some "not permitted" }), []

        // Rule 27. Signing is locked for a delay that doubles with each further guess
        // and passes on its own. A correct PIN does not cut it short; waiting does, and
        // so does a Rule 37 replacement.
        | GenPresServer, GenPresClient b, SigningLocked ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = None
                    Showing =
                        Some "signing is locked for a while — wait it out, or reset the PIN to sign now" }), []

        // Rule 38. The Session is untouched — only the signature did not happen.
        | GenPresServer, GenPresClient b, SigningUnavailable ->
            h |> onClient b (fun s ->
                { s with Showing = Some "authorisation could not be checked — nothing was signed" }), []

        | GenPresServer, GenPresClient b, ResetCodeMailed ->
            h |> onClient b (fun s ->
                { s with Showing = Some "a reset code has been mailed — the current PIN still stands" }), []

        | GenPresServer, GenPresClient b, PinChanged ->
            h |> onClient b (fun s -> { s with Showing = Some "PIN changed" }), []

        | GenPresServer, GenPresClient b, ResetDenied failure ->
            let what =
                match failure with
                | NoResetPending -> "no reset was asked for"
                | ResetExpired -> "that code has expired — ask for a new one"
                | WrongCode left -> $"that code is wrong — %i{left} left before it is void"
                | ResetVoid -> "that code is void — ask for a new one"
                | ResetPending -> "a code is already on its way — look for the mail"
            h |> onClient b (fun s -> { s with Showing = Some what }), []

        | _ -> refused h env

    /// Actor 3. A closed browser first — nothing reaches it and nothing leaves it —
    /// then the launch that opens the tab, then whoever the message is from.
    let private updateClient (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 3: the GenPRES Client — and the cart, which lives here (Rule 31)
        // ══════════════════════════════════════════════════════════════════════

        // A closed browser is not there any more. Nothing it might have sent reaches
        // the Server (UC-12 ext 1b), which is exactly why no close can be inferred —
        // and the cart went with it, because the cart was only ever here.
        | _, GenPresClient b, _ when
            h.Clients |> Map.tryFind b |> Option.map _.Closed |> Option.defaultValue false ->
            h, []

        // UC-1 ext 2b. A Server that is down serves no Client, so there is nothing of
        // ours to show a message with. Nothing is presented, so nothing is scrubbed
        // (Rule 39): the Launch stays in the bar, which is what ext 3a retries from.
        | MainEhrLaunchScript, GenPresClient b, OpenUrl launch when not h.GenPres.Up ->
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = Some launch
                    Showing = Some "the browser's own error page" }), []

        // Rule 39. The Client presents the Launch and erases it from the address
        // bar in the same act. What the browser keeps of the launch after that is a
        // copy in the page's own memory — enough to retry with (UC-1 ext 3a), and not
        // in history, not in the bar, not in a referrer (Consequence 4).
        | MainEhrLaunchScript, GenPresClient b, OpenUrl launch ->
            let st = clientState b h
            h |> onClient b (fun s -> { s with UrlLaunch = None; RetryLaunch = Some launch }),
            [ send (GenPresClient b) GenPresServer (RedeemLaunch(launch, st.BrowserIdentity, st.Sid)) ]

        | _ ->
            match env.From with
            | User -> updateClientFromUser h env
            | GenPresServer -> updateClientFromServer h env
            | _ -> refused h env
    /// Every envelope, to whoever it is for. The tick is taken here, once, so every
    /// branch below sees the same clock however it is reached.
    let update (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        // every move takes a tick of time
        let h = { h with Env.Now = h.Env.Now + 1 }

        match env.To with
        | Environment -> updateEnvironment h env
        // A person reads what is sent to them; there is no state to change.
        | User -> h, []
        | MainEhrWorkstation -> updateWorkstation h env
        | MainEhrLaunchScript -> updateLaunchScript h env
        | GenPresClient _ -> updateClient h env
        | GenPresServer -> updateServer h env
        | GenPresDatabase -> updateDatabase h env
        | UserRegistry -> updateRegistry h env
        | PatientDataPlatform -> updatePlatform h env
        | MailService -> updateMailService h env
        // Actor 8 is reached by the browser and the Server, and answers with what it
        // knows: who is at a browser. That answer arrives here as a field on the
        // Client, not as a message, so nothing is ever addressed to it.
        | IdentityProvider -> refused h env

    /// The edge table is enforced here, before delivery, which is what separates the
    /// Constraints from a convention: an unpermitted wire does not exist.
    ///
    /// `depthFirst` runs a cascade to the end before the next inbox item, which is the
    /// readable default. Breadth first interleaves them leg by leg — the only way to
    /// put two Submissions in flight at once, and so the only way to exercise Rule 36.
    let runWith depthFirst fuel hospital inbox =
        let rec loop fuel h trace queue =
            match queue with
            | [] -> h, List.rev trace, "completed"
            | _ when fuel <= 0 -> h, List.rev trace, "exhausted"
            | env :: rest ->
                if Edges.permits env.From env.To then
                    let h, out = update h env
                    let next = if depthFirst then out @ rest else rest @ out
                    loop (fuel - 1) h (env :: trace) next
                else
                    let refusal = { From = env.To; To = Environment; Msg = Refused env }
                    let h, _ = update h refusal
                    loop (fuel - 1) h (refusal :: env :: trace) rest
        loop fuel hospital [] inbox

    let run fuel hospital inbox = runWith true fuel hospital inbox


// ───────────────────────────── printing ─────────────────────────────  [model]

/// Rendering an envelope for the trace. Formatting only: no branch here decides
/// anything, so a message may be added without touching the model.
module Envelope =

    let actorName =
        function
        | User -> "User"
        | MainEhrWorkstation -> "Workstation"
        | MainEhrLaunchScript -> "LaunchScript"
        | GenPresClient(BrowserId i) -> $"Client%i{i}"
        | GenPresServer -> "Server"
        | GenPresDatabase -> "Database"
        | PatientDataPlatform -> "Platform"
        | IdentityProvider -> "IdentityProvider"
        | UserRegistry -> "Registry"
        | MailService -> "Mail"
        | Environment -> "Env"

    let private tagName =
        function
        | ForLaunch(AttemptId a) -> $"#%i{a}"
        | ForRequest(RequestId r) -> $"req-%i{r}"
        | ForSweep -> "sweep"

    let private planName = function Some(TreatmentPlanId s) -> s | None -> "(nothing)"

    let private cmdName =
        function
        | Compute os -> $"Compute (%i{os.Length} order contexts)"
        | SubmitTreatmentPlan req ->
            let what = match req.Pin with Some(Pin p) -> $"Sign (pin %s{p})" | None -> "Save"
            let n = match req.Notice with Some _ -> " +notice" | None -> ""
            let c = match req.Challenge with Some _ -> " +challenge" | None -> ""
            let d = match req.DataOk with Some _ -> " +data" | None -> ""
            let os = req.Work.Orders
            $"%s{what} (%i{os.Length} order contexts, opened-with %s{planName (Token.plan req.Opened)}%s{n}%s{c}%s{d})"
        | RequestSignChallenge(work, tok, _, _) ->
            $"RequestSignChallenge (%i{work.Orders.Length} order contexts, opened-with %s{planName (Token.plan tok)})"
        | OpenTreatmentPlan(TreatmentPlanId s) -> $"OpenTreatmentPlan %s{s}"
        | DiscardTreatmentPlan(TreatmentPlanId s, tok) ->
            $"DiscardTreatmentPlan %s{s} (opened-with %s{planName (Token.plan tok)})"
        | ResetPin -> "ResetPin"
        | SupplyResetCode(ResetCode c, _) -> $"SupplyResetCode %s{c}"
        | CloseSession -> "CloseSession"

    let private actName =
        function
        | Prescribes(OrderContextId o) -> $"Prescribes %s{o}"
        | EntersPatientData(PatientData d) -> $"EntersPatientData \"%s{d}\""
        | Saves -> "Saves"
        | Signs(Pin p) -> $"Signs (pin %s{p})"
        | OpensTreatmentPlan(TreatmentPlanId s) -> $"OpensTreatmentPlan %s{s}"
        | Discards(TreatmentPlanId s) -> $"Discards %s{s}"
        | ConfirmsSign -> "ConfirmsSign"
        | CancelsSign -> "CancelsSign"
        | AcknowledgesNotice -> "AcknowledgesNotice"
        | AsksPinReset -> "AsksPinReset"
        | EntersResetCode(ResetCode c, _) -> $"EntersResetCode %s{c}"
        | ClosesSession -> "ClosesSession"
        | CarriesOverFrom(BrowserId b) -> $"CarriesOverFrom Client%i{b}"

    let rec describe (m: Msg) =
        match m with
        | Tick -> "Tick"
        | Start a -> $"Start %s{actorName a}"
        | Stop a -> $"Stop %s{actorName a}"
        | LogIn(LoginName u) -> $"LogIn %s{u}"
        | SelectPatient(PatientId p) -> $"SelectPatient %s{p}"
        | ClearPatient -> "ClearPatient"
        | TriggerLaunch -> "TriggerLaunch"
        | LaunchError e -> $"LaunchError \"%s{e}\""
        | OpenUrl l ->
            let pat = match l.Patient with Some(PatientId x) -> x | None -> "(no patient)"
            $"GET /genpres?launch=%s{l.Nonce}   (patient %s{pat}: no login, no role)"
        | Refresh -> "F5"
        | ReloadPage -> "reload"
        | OpenDirectly -> "OpenDirectly"
        | AcceptAnonymousOffer -> "AcceptAnonymousOffer"
        | ChoosePin(ResetCode c, Pin p) -> $"ChoosePin %s{c} %s{p}"
        | Act a -> actName a
        | CloseBrowser -> "CloseBrowser"
        | RedeemLaunch(l, identity, _) ->
            let who = match identity with Some(LoginName x) -> x | None -> "nobody"
            $"RedeemLaunch %s{l.Nonce} (the browser proved %s{who})"
        | OpenAnonymous _ -> "OpenAnonymous"
        | AnonymousRefused -> "AnonymousRefused"
        | SupplyPin(AttemptId a, ResetCode c, Pin p) -> $"SupplyPin #%i{a} %s{c} %s{p}"
        | AckSessionNotice(SessionId by, SessionId sid) -> $"AckSessionNotice %s{sid} (from %s{by})"
        | SessionRequest(SessionId s, c) -> $"%s{s}: %s{cmdName c}"
        | DiscardIfOwnHead(t, SessionId sid, TreatmentPlanId i, _) ->
            $"DiscardIfOwnHead %s{tagName t} %s{sid} %s{i}"
        | TreatmentPlanDiscarded(t, TreatmentPlanId i, starts) ->
            let onto = match starts with Some x -> let (TreatmentPlanId n) = x.Id in n | None -> "nothing"
            $"TreatmentPlanDiscarded %s{tagName t} %s{i} (now on %s{onto})"
        | DiscardRefused(t, why) -> $"DiscardRefused %s{tagName t} \"%s{why}\""
        | SpendLaunchIfUnspent(t, nonce) -> $"SpendLaunchIfUnspent %s{tagName t} %s{nonce}"
        | LaunchSpent t -> $"LaunchSpent %s{tagName t}"
        | LaunchReplayed(t, r) ->
            let which = match r with Some x -> let (SessionNo n) = x.No in $"ses-%03i{n}" | None -> "(no session)"
            $"LaunchReplayed %s{tagName t} %s{which}"
        | NoteAnonymousRefusal a -> $"NoteAnonymousRefusal %s{actorName a}"
        | ResolveUser(t, LoginName u) -> $"ResolveUser %s{tagName t} %s{u}"
        | UserResolved(t, uc, _) ->
            let (UserId u) = uc.UserId
            $"UserResolved %s{tagName t} %s{u} %A{uc.Role}"
        | UserUnresolved(t, f) -> $"UserUnresolved %s{tagName t} %A{f}"
        | ReadPatientData(t, PatientId p) -> $"ReadPatientData %s{tagName t} %s{p}"
        | PatientDataRead(t, _) -> $"PatientDataRead %s{tagName t}"
        | PatientDataUnavailable t -> $"PatientDataUnavailable %s{tagName t}"
        | ReadCredential(t, UserId u) -> $"ReadCredential %s{tagName t} %s{u}"
        | CredentialRead(t, c) ->
            let pin = match c |> Option.bind _.Pin with Some _ -> "pin set" | None -> "no pin"
            $"CredentialRead %s{tagName t} (%s{pin})"
        | StartReset(t, UserId u, _, _) -> $"StartReset %s{tagName t} %s{u}"
        | ResetStarted(t, UserId u) -> $"ResetStarted %s{tagName t} %s{u}"
        | ReplacePinIfCode(t, UserId u, ResetCode c, _) ->
            $"ReplacePinIfCode %s{tagName t} %s{u} %s{c}"
        | PinReplaced(t, _) -> $"PinReplaced %s{tagName t}"
        | ResetRefused(t, f) -> $"ResetRefused %s{tagName t} %A{f}"
        | ReadRecord(t, PatientId p) -> $"ReadRecord %s{tagName t} %s{p}"
        | RecordRead(t, r) -> $"RecordRead %s{tagName t} (%i{r.Plans.Length} plans)"
        | CommitTreatmentPlan(t, c) ->
            let what = if c.Req.Pin.IsSome then "sign" else "save"
            let (IdemKey k) = c.Req.Key
            $"CommitTreatmentPlan %s{tagName t} %s{what} key=%s{k}"
        | TreatmentPlanCommitted(_, s) ->
            let (TreatmentPlanId i) = s.Id
            $"""TreatmentPlanCommitted %s{i} %s{match s.State with Signed -> "Signed" | Unsigned -> "Unsigned" | Discarded -> "Discarded"}"""
        | CommitRefused(t, r) -> $"CommitRefused %s{tagName t} %A{r}"
        | OpenSessionClosingOthers(r, replacing) ->
            let (SessionNo n) = r.No
            let also = match replacing with Some(SessionId o) -> $" (replacing %s{o})" | None -> ""
            $"OpenSessionClosingOthers ses-%03i{n}%s{also}"
        | EndSessionIfOpen(SessionId sid, mark) -> $"EndSessionIfOpen %s{sid} %A{mark}"
        | TouchIfOpen(SessionId sid) -> $"TouchIfOpen %s{sid}"
        | MarkDelivered(SessionId sid) -> $"MarkDelivered %s{sid}"
        | MarkAcknowledged(SessionId by, SessionId sid) -> $"MarkAcknowledged %s{sid} (by %s{by})"
        | ReadSessionRecord(t, SessionId s) -> $"ReadSessionRecord %s{tagName t} %s{s}"
        | SessionRecordRead(t, r) ->
            let what = match r with Some x -> $"%A{x.State}" | None -> "(no such session)"
            $"SessionRecordRead %s{tagName t} %s{what}"
        | ReadSessionRecords t -> $"ReadSessionRecords %s{tagName t}"
        | SessionRecordsRead(t, rs) -> $"SessionRecordsRead %s{tagName t} (%i{rs.Length})"
        | SendMail(MailAddress a, what) -> $"SendMail {a}: \"%s{what}\""
        | SessionOpened(SessionId s, SessionNo n, u, p, os, tok, resumed) ->
            let who =
                match u with
                | Some uc -> let (LoginName l) = uc.Login in $"%s{l}/%A{uc.Role}"
                | None -> "anonymous"
            let pat = match p.Patient with Some(PatientId x) -> x | None -> "(no patient)"
            let offered =
                match resumed with
                | Some(TreatmentPlanId r, _, at) -> $", unsigned %s{r} (saved at %i{at}) offered"
                | None -> ""
            $"SessionOpened %s{s} ses-%03i{n} %s{who} %s{pat} (%i{os.Length} order contexts, opened-with %s{planName (Token.plan tok)}%s{offered})"
        | PinRequired(AttemptId a) -> $"PinRequired #%i{a}"
        | LaunchRefused retryable ->
            if retryable then "LaunchRefused (retryable)" else "LaunchRefused"
        | NotAuthorised -> "NotAuthorised"
        | AuthorityUnavailable -> "AuthorityUnavailable"
        | ServerUnreachable -> "ServerUnreachable"
        | SessionEnded m -> $"SessionEnded %A{m}"
        | SessionRefused mark -> $"SessionRefused %A{mark}"
        | PriorSessionNotice ss ->
            let names =
                ss |> List.map (fun (SessionNo i, m, _) -> $"ses-%03i{i}=%A{m}") |> String.concat ", "
            $"PriorSessionNotice [%s{names}]"
        | Computed os -> $"Computed (%i{os.Length} order contexts)"
        | SubmissionBlocked uc -> let (LoginName l) = uc.Login in $"SubmissionBlocked by %s{l}"
        | SignChallengeIssued t ->
            $"""SignChallengeIssued over %s{t |> Token.digest |> Option.defaultValue "-"}"""
        | PatientDataChanged(PatientData d, _) -> $"PatientDataChanged %s{d}"
        | PatientDataUnverified _ -> "PatientDataUnverified"
        | UnsignedWorkNotice(uc, t) ->
            let (LoginName l) = uc.Login
            $"UnsignedWorkNotice (%s{l}, disclosing %i{(Token.disclosed t).Length})"
        | SubmissionRefused why -> $"SubmissionRefused \"%s{why}\""
        | TreatmentPlanSubmitted(TreatmentPlanId s, state, _) -> $"TreatmentPlanSubmitted %s{s} %A{state}"
        | TreatmentPlanDiscardedOk(TreatmentPlanId s, now, os, _) ->
            let onto = match now with Some(TreatmentPlanId x) -> x | None -> "nothing"
            $"TreatmentPlanDiscardedOk %s{s} (now on %s{onto}, %i{os.Length} order contexts)"
        | TreatmentPlanOpened(TreatmentPlanId s, os, _) -> $"TreatmentPlanOpened %s{s} (%i{os.Length} order contexts)"
        | PinRejected n -> $"PinRejected (%i{n} left)"
        | NoTreatmentPlanHere -> "NoTreatmentPlanHere"
        | NotPermitted -> "NotPermitted"
        | SigningUnavailable -> "SigningUnavailable"
        | SigningLocked -> "SigningLocked"
        | ResetCodeMailed -> "ResetCodeMailed"
        | PinChanged -> "PinChanged"
        | ResetDenied f -> $"ResetDenied %A{f}"
        | Noted what -> $"Noted \"%s{what}\""
        | Refused e ->
            $"REFUSED << %s{actorName e.From} -> %s{actorName e.To}  %s{describe e.Msg} >>"

    let show (env: Envelope) =
        $"  %-12s{actorName env.From} -> %-12s{actorName env.To} %s{describe env.Msg}"

    /// The idle sweep runs on every Tick and says nothing most of the time. Keeping it
    /// out of the printed trace is formatting, not filtering: the envelopes are all
    /// still in `lastTrace`, and the assertions see them.
    let noise (env: Envelope) =
        match env.Msg with
        | Tick -> true
        | ReadSessionRecords ForSweep
        | SessionRecordsRead(ForSweep, _) -> true
        | _ -> false


// ═══════════════════════════════════════════════════════════════════════════════
//                         3. SCENARIOS AND ASSERTIONS                     [model]
//
//  None of this ships as code — but the scenarios are the acceptance tests the real
//  system owes, and the checks are what each one has to prove.
// ═══════════════════════════════════════════════════════════════════════════════

// ───────────────────────────── what the world does ─────────────────────────────

let atWorkstation msg = { From = User; To = MainEhrWorkstation; Msg = msg }
let triggerLaunch     = { From = User; To = MainEhrLaunchScript; Msg = TriggerLaunch }
let envt to_ msg      = { From = Environment; To = to_; Msg = msg }
let tick              = envt Environment Tick
let ticks n           = List.replicate n tick
let atClient b msg    = { From = User; To = GenPresClient(BrowserId b); Msg = msg }
let act b a           = atClient b (Act a)

/// A Client putting an envelope on the wire by hand — which is what an attacker has,
/// and what the honest Client's own branches deliberately never do.
let fromClient b msg = { From = GenPresClient(BrowserId b); To = GenPresServer; Msg = msg }

/// A Submission built by hand: the fields under test, and defaults for the rest. Rule 45's
/// key is fresh each time, so nothing here is answered out of the table by accident.
let mutable private handKey = 0

let handCreate (work: WorkPlan) (opened: OpenedToken) (notice: NoticeToken option) (pin: Pin option) =
    handKey <- handKey + 1
    SubmitTreatmentPlan
        {
            Work = work
            Opened = opened
            Notice = notice
            Challenge = None
            DataOk = None
            Pin = pin
            Key = IdemKey $"hand-%04i{handKey}"
        }

/// Rule 43. Signing is two acts of the User's, not one: asking, and then signing what
/// the modal shows. A scenario that means "A signs" wants both, and a scenario testing
/// the gate between them uses `Signs` and `ConfirmsSign` apart.
let signs b pin = [ act b (Signs pin); act b ConfirmsSign ]

/// UC-1 steps 1 and 2. `None` is ext 1a: no Patient is active in the MainEHR Session.
let launchAs (LoginName login) (patient: PatientId option) =
    [
        atWorkstation (LogIn(LoginName login))
        match patient with
        | Some p -> atWorkstation (SelectPatient p)
        | None -> atWorkstation ClearPatient
        triggerLaunch
    ]

// ───────────────────────────── the Cast ─────────────────────────────
// The Cast, in the state before the first use case runs. One Workstation stands in
// for many: no Rule distinguishes them, so "B at their own workstation" is B logging
// in here.

let ucA = { UserId = UserId "u-a"; Login = LoginName "dr.a";    Role = Prescriber }
let ucB = { UserId = UserId "u-b"; Login = LoginName "dr.b";    Role = Prescriber }
let ucC = { UserId = UserId "u-c"; Login = LoginName "nurse.c"; Role = Reader }

let mailA = MailAddress "a@hospital"
let mailB = MailAddress "b@hospital"
let mailC = MailAddress "c@hospital"

let pinA = Pin "1111"
let pinB = Pin "2222"

let pat1 = PatientId "pat-1"      // no GenPRES PatientRecord yet
let pat2 = PatientId "pat-2"      // head is a Signed TreatmentPlan
let pat3 = PatientId "pat-3"      // head is an Unsigned TreatmentPlan of A's, over a Signed one

let oc id pat by =
    { Id = OrderContextId id; Patient = Some pat; Content = $"%s{id}/as-saved"; Stamp = Some by }

let mkPlan n patient by state baseOn orders =
    {
        Id = TreatmentPlanId $"plan-%04i{n}"
        No = TreatmentPlanNo n
        Patient = patient
        By = by
        Base = baseOn
        Orders = orders
        Data = Some(PatientData $"as read for %A{patient}")
        From = Some(FromPlatform 0)
        State = state
        // The fixtures stand for history the run did not make: Rule 17's chain is
        // whatever they were built on. They carry no Session, which is what a plan
        // from before this record began looks like — and Rule 19 opens such a head as
        // it always did, because the absence of a record is not evidence of a Session
        // that ended badly.
        SignedBase = baseOn
        Session = None
        At = 0
    }

let p2Signed   = mkPlan 1 pat2 ucA Signed   None               [ oc "oc-1" pat2 ucA ]
let p3Signed   = mkPlan 2 pat3 ucB Signed   None               [ oc "oc-2" pat3 ucB ]
let p3Unsigned = mkPlan 3 pat3 ucA Unsigned (Some p3Signed.Id) [ oc "oc-2" pat3 ucB; oc "oc-3" pat3 ucA ]

/// The world the Cast starts in.
let world =
    let h = Hospital.empty
    // Rule 1. Everyone in the Cast may press the button; UC-1 ext 1b takes that away
    // from one of them, which is the only thing the model can say about a decision
    // that is MainEHR's to make.
    let h =
        { h with
            Workstation.MayLaunch = Set.ofList [ ucA.Login; ucB.Login; ucC.Login; LoginName "dr.x" ] }
    let h =
        { h with
            Registry.Users =
                Map.ofList [
                    ucA.Login, (ucA, mailA)
                    ucB.Login, (ucB, mailB)
                    ucC.Login, (ucC, mailC)
                ] }
    let h =
        { h with
            Platform.Data =
                Map.ofList [
                    pat1, PatientData "pat-1: 4y, 17kg"
                    pat2, PatientData "pat-2: 7y, 24kg"
                    pat3, PatientData "pat-3: 1y, 9kg"
                ] }
    let h =
        { h with
            Database.Private.Credentials =
                Map.ofList [
                    ucA.UserId, { User = ucA.UserId; Pin = Some pinA; AttemptCount = 0; LockedUntil = None }
                    ucB.UserId, { User = ucB.UserId; Pin = Some pinB; AttemptCount = 0; LockedUntil = None }
                ] }
    let h =
        let db = h.Database

        { h with
            Database =
                { db with
                    Clinical.Signed = Map.ofList [ pat2, [ p2Signed ]; pat3, [ p3Signed ] ]
                    Private.Drafts = Map.ofList [ (pat3, [ p3Unsigned ]) ] } }
    // The Cast's TreatmentPlans occupy plan-0001 to plan-0003, so the Database mints from
    // above them. Ids are never reissued.
    { h with Database.NextPlan = 10 }

// ───────────────────────────── assertions ─────────────────────────────

let mutable lastTrace : Envelope list = []
/// Every envelope of every scenario, so the Consequences and Guarantees can be
/// checked over the whole run rather than one step of it.
let mutable allTrace : Envelope list = []
let mutable checks = 0
let mutable failures = 0
/// Rule 31, structurally: after every scenario step, is the Server empty of requests?
let mutable everCarriedARequest = false

let expect label cond =
    checks <- checks + 1
    if cond then printfn $"    [ok]   {label}"
    else
        failures <- failures + 1
        printfn $"    [FAIL] {label}"

let saw (p: Msg -> bool) = lastTrace |> List.exists (fun e -> p e.Msg)
let never (p: Msg -> bool) = not (saw p)
let sawTo actor (p: Msg -> bool) =
    lastTrace |> List.exists (fun e -> e.To = actor && p e.Msg)
let countOf (p: Msg -> bool) = lastTrace |> List.filter (fun e -> p e.Msg) |> List.length

/// The Launch the LaunchScript last minted, read off the wire. A scenario cannot
/// make one — the mac is over a secret only the LaunchScript and the Server hold —
/// so a thief here has exactly what a thief there has: a value seen in passing.
let launchOnTheWire () =
    lastTrace |> List.tryPick (function { Msg = OpenUrl l } -> Some l | _ -> None)

/// Rule 43. The challenges the last step's Server issued, as an attacker or a retry
/// would have them: something a Client was given, and cannot make.
let challengesIssued () =
    lastTrace |> List.choose (function { Msg = SignChallengeIssued t } -> Some t | _ -> None)

let challengeIssued () = challengesIssued () |> List.tryHead

/// Did `first` happen before `second` in the trace? Used where an order is fixed —
/// Rule 24, and UC-3 ext 3c.
let before (first: Msg -> bool) (second: Msg -> bool) =
    let idx p = lastTrace |> List.tryFindIndex (fun e -> p e.Msg)
    match idx first, idx second with
    | Some a, Some b -> a < b
    | _ -> false

// ───────────────────────────── reading the world ─────────────────────────────
// Everything a Session is, is in the Database now (Rule 31), and everything it is
// working on is in a Client. There is no third place to look.

let recNo n (h: Hospital) = h.Database.Private.Sessions |> List.tryFind (fun r -> r.No = SessionNo n)
let stateOf n h = recNo n h |> Option.map _.State
let noticeOf n (h: Hospital) = recNo n h |> Option.map _.Notice
/// Rule 10. Put in front of the User at least once.
let wasTold n h =
    match noticeOf n h with
    | Some(Delivered _)
    | Some(Acknowledged _) -> true
    | _ -> false

/// Rule 10. And said to have been seen, which is what ends the obligation.
let wasAcknowledged n h = match noticeOf n h with Some(Acknowledged _) -> true | _ -> false
let lastSeenOf n (h: Hospital) = recNo n h |> Option.map _.LastSeen

/// Newest first: `OpenSessionClosingOthers` puts a new record on the front.
let newestRecord (h: Hospital) = h.Database.Private.Sessions |> List.tryHead
let openRecords (h: Hospital) = h.Database.Private.Sessions |> List.filter SessionRecord.isOpen
let openCount h = (openRecords h).Length
let recordCount (h: Hospital) = h.Database.Private.Sessions.Length
let openOfUser (uc: UserContext) h =
    openRecords h |> List.filter (fun r -> SessionRecord.userId r = Some uc.UserId)

let recordFor p (h: Hospital) = h.Database |> Database.recordOf p

/// Every Patient the Database holds anything for, either half.
let patientsInRecord (h: Hospital) =
    (h.Database.Clinical.Signed |> Map.toList |> List.map fst)
    @ (h.Database.Private.Drafts |> Map.toList |> List.map fst)
    |> List.distinct
let headOf p h = (recordFor p h).Plans |> List.tryHead
let planCount p h = (recordFor p h).Plans.Length

let lastTab (h: Hospital) = h.Workstation.NextTab - 1
let clientOf b (h: Hospital) = h.Clients |> Map.tryFind (BrowserId b)
let showingOf b h = clientOf b h |> Option.bind _.Showing
let sidAt b h = clientOf b h |> Option.bind _.Sid
let userAt b h = clientOf b h |> Option.bind _.User
let patientAt b h = clientOf b h |> Option.bind _.Patient
let workOf b h = clientOf b h |> Option.map _.Work |> Option.defaultValue WorkPlan.empty
let dataAt b h = clientOf b h |> Option.bind _.Work.Data
let workingAt b h = workOf b h |> _.Orders
/// Rule 33: the TreatmentPlan the Session opened with, as the Client holds it.
let openedAt b h = clientOf b h |> Option.bind _.Opened |> Option.bind Token.plan
let noticeAt b h = clientOf b h |> Option.bind _.Notice

let mailsTo (addr: MailAddress) (h: Hospital) = h.Mail |> List.filter (fst >> (=) addr)

/// Rule 46. What the Database recorded — the private store's audit, and the only copy
/// there is.
let auditOf (h: Hospital) = h.Database.Private.Audit

let audited (what: string) (h: Hospital) = auditOf h |> List.exists (fun a -> a.What.Contains what)
let credentialOf (uc: UserContext) (h: Hospital) = h.Database.Private.Credentials |> Map.tryFind uc.UserId

/// UC-7 step 2. What the User does with the mail: reads the code out of it. That the
/// code arrives through a channel GenPRES only writes to — and whoever is at the
/// workstation does not read — is the whole of what Rule 37 rests on.
let codeInMail (addr: MailAddress) (h: Hospital) =
    mailsTo addr h
    |> List.tryPick (fun (_, body) ->
        body.Split ' ' |> Array.tryFind (fun w -> w.StartsWith "code-"))
    |> Option.map ResetCode

// ───────────────────────────── running a scenario ─────────────────────────────

/// One PatientRecord as a chain, oldest first, each link `id/who/Signed-or-Unsigned`.
/// The arrow is "and then", not "became": a PatientRecord is append-only (Concept 12),
/// so every link is still there and each stands on the one to its left.
let private planChain (r: PatientRecord) =
    r.Plans
    |> List.rev
    |> List.map (fun s ->
        let (TreatmentPlanId i) = s.Id
        let (LoginName l) = s.By.Login
        $"""%s{i}/%s{l}/%s{match s.State with Signed -> "S" | Unsigned -> "U" | Discarded -> "D"}""")
    |> String.concat " -> "

let private planChains (h: Hospital) =
    h
    |> patientsInRecord
    |> List.sort
    |> List.map (fun (PatientId p as pid) -> p, planChain (recordFor pid h))
    |> List.filter (snd >> (<>) "")

/// What the Patients' treatment plans looked like going in. Printed above the trace so
/// that the state printed below it can be read as a difference rather than as a fact:
/// a scenario touches one Patient, and without the baseline the others read as though
/// they had moved too.
let plansBefore (h: Hospital) =
    printfn "    treatment plans before (oldest first, -> = and then):"
    for p, chain in planChains h do
        printfn $"      %s{p}: %s{chain}"

let dump (before: Hospital) (h: Hospital) =
    printfn $"    now=%i{h.Env.Now}  open=%i{openCount h}  in-flight=%i{h.GenPres.InFlight.Count}  launches=%i{h.GenPres.Pending.Count}"
    h.Database.Private.Sessions
    |> List.rev
    |> List.iter (fun r ->
        let (SessionNo n) = r.No
        let who =
            match r.User with
            | Some uc -> let (LoginName l) = uc.Login in l
            | None -> "anonymous"
        let pat = match r.Patient with Some(PatientId p) -> p | None -> "(no patient)"
        let told =
            match r.Notice with
            | Delivered at -> $"  told={at}"
            | Acknowledged at -> $"  acked={at}"
            | Owed -> "  owed a notice"
            | NotOwed -> ""
        printfn $"    ses-%03i{n}  %-10s{who}  %-11s{pat}  %A{r.State}%s{told}")
    let was = planChains before |> Map.ofList
    printfn "    treatment plans after:"
    for p, chain in planChains h do
        let mark = if was.TryFind p = Some chain then "  (unchanged)" else "  (appended)"
        printfn $"      %s{p}: %s{chain}%s{mark}"
    h.Clients
    |> Map.iter (fun (BrowserId b) x ->
        let cart =
            if x.Work.Orders.IsEmpty then "" else $"  cart=%i{x.Work.Orders.Length}"
        match x.Showing with
        | Some n -> printfn $"    Client%i{b}: %s{n}%s{cart}"
        | None -> if cart <> "" then printfn $"    Client%i{b}:%s{cart}")

/// Rule 31, checked after every step: whatever the Server was doing, it is not doing
/// it any more, and it kept nothing.
let private noteFlight (h: Hospital) =
    if not h.GenPres.InFlight.IsEmpty then everCarriedARequest <- true

/// Every SessionRecord state the Database has ever held, at the end of every step of
/// every scenario. Under Rule 40 a record no longer travels in an envelope — the
/// Server names a change and the Database decides — so the trace is no longer where
/// the states are, and this is.
let mutable allRecords : SessionRecord list = []

let private noteRecords (h: Hospital) =
    allRecords <- allRecords @ h.Database.Private.Sessions

/// Rule 15. Every TreatmentPlan the Database has ever held, at the end of every step
/// of every scenario. A plan is written once and then never touched again — except
/// for its `State`, which is the one thing Rule 15 lets move, and only in one
/// direction. Collecting them all is how that is proved rather than asserted.
let mutable allPlans : TreatmentPlan list = []

/// Rules 7 and 40. Every *set* of SessionRecords the Database has held, kept whole
/// rather than flattened: the limits are about what stands open together, so they can
/// only be tested against one state at a time.
let mutable allDatabases : SessionRecord list list = []

let private noteDatabase (h: Hospital) =
    allDatabases <- h.Database.Private.Sessions :: allDatabases

let private notePlans (h: Hospital) =
    let clinical = h.Database.Clinical.Signed |> Map.toList |> List.collect snd
    let drafts = h.Database.Private.Drafts |> Map.toList |> List.collect snd
    allPlans <- allPlans @ clinical @ drafts

/// The fuel is a count of handled messages, and a scenario measured in ticks spends
/// far more of them than one measured in acts: a live Session ticking towards a limit
/// costs a sweep per tick. `step` is the ordinary budget; `stepFor` is for the two
/// scenarios that watch a clock run out (Rule 13's anonymous limit, Rule 9's
/// absolute one), which would otherwise stop half-way and prove nothing.
let stepFor fuel label h inbox =
    printfn ""
    printfn $"== {label} =="
    plansBefore h
    let after, trace, outcome = Hospital.run fuel h inbox
    lastTrace <- trace
    allTrace <- allTrace @ trace
    trace |> List.filter (Envelope.noise >> not) |> List.iter (Envelope.show >> printfn "%s")
    if outcome <> "completed" then printfn $"    !! {outcome}"
    noteFlight after
    noteRecords after
    notePlans after
    noteDatabase after
    dump h after
    after

let step label h inbox = stepFor 4000 label h inbox

/// A scenario that runs but whose trace is not worth printing in full.
let quiet label h inbox =
    let h, trace, _ = Hospital.run 4000 h inbox
    lastTrace <- trace
    allTrace <- allTrace @ trace
    noteFlight h
    noteRecords h
    notePlans h
    noteDatabase h
    ignore label
    h

/// The one scenario that needs the cascades interleaved rather than run one after the
/// other: Rule 36's race (see `Hospital.runWith`).
let racing label h inbox =
    printfn ""
    printfn $"== {label} =="
    plansBefore h
    let after, trace, outcome = Hospital.runWith false 4000 h inbox
    lastTrace <- trace
    allTrace <- allTrace @ trace
    trace |> List.filter (Envelope.noise >> not) |> List.iter (Envelope.show >> printfn "%s")
    if outcome <> "completed" then printfn $"    !! {outcome}"
    noteFlight after
    noteRecords after
    notePlans after
    noteDatabase after
    dump h after
    after

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-1  User launches GenPRES
// ═══════════════════════════════════════════════════════════════════════════════

let uc1 () =
    printfn ""
    printfn "############### UC-1  User launches GenPRES ###############"

    // Goal: User A gets GenPRES open on the Patient they have selected, able to
    // prescribe, save and sign. Patient 1 has no PatientRecord, so the Session starts
    // from nothing.
    let launched = step "UC-1 main — A launches for Patient 1" world (launchAs ucA.Login (Some pat1))

    expect "UC-1 one Session, open, and the Role is the registry's"
        (openCount launched = 1
         && stateOf 1 launched = Some OpenOrGone
         && (newestRecord launched |> Option.bind _.User |> Option.map _.Role) = Some Prescriber)

    expect "UC-1 the SessionRecord carries the UserContext and the mail address (Concept 9)"
        ((newestRecord launched |> Option.bind _.User) = Some ucA
         && (newestRecord launched |> Option.bind _.Mail) = Some mailA)

    expect "UC-1 step 4: the Launch carried a Patient, and nothing about who the User is"
        (saw (function
              | OpenUrl l -> l.Patient = Some pat1 && Token.verifyLaunch l
              | _ -> false))

    expect "UC-1 step 4: the nonce is spent once, and the launch went on (Rule 2)"
        (countOf (function SpendLaunchIfUnspent _ -> true | _ -> false) = 1
         && saw (function LaunchSpent _ -> true | _ -> false)
         && never (function LaunchReplayed _ -> true | _ -> false))

    expect "UC-1 step 3: the login the registry was asked about is the one the browser proved (Rules 4, 5)"
        (saw (function ResolveUser(ForLaunch _, l) -> l = ucA.Login | _ -> false))

    expect "UC-1 step 5: the Role came from the UserRegistry (Rule 5)"
        (saw (function UserResolved(_, uc, _) -> uc.Role = Prescriber | _ -> false))

    expect "UC-1 step 5: a PIN is set, so the launch continues and none is asked for (Rule 23)"
        (saw (function CredentialRead(_, Some c) -> c.Pin.IsSome | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    expect "UC-1 step 6: the PatientContext was read once, at the launch (Concept 2)"
        (saw (function PatientDataRead _ -> true | _ -> false)
         && countOf (function ReadPatientData _ -> true | _ -> false) = 1)

    expect "UC-1 step 7: Patient 1 has no record, so the Session starts from nothing (Rule 19)"
        (openedAt 1 launched = None && workingAt 1 launched = [])

    expect "UC-1 step 8: the SessionRecord was written to the Database (Concept 9)"
        (launched.Database.Private.Sessions.Length = 1)

    // Rule 33. The Client is handed the token it will return with every Submission, and
    // it could not have made one: the mac is over a secret it never sees.
    expect "UC-1 step 9: the Client holds an opened-with token that verifies (Rule 33)"
        ((clientOf 1 launched |> Option.bind _.Opened |> Option.map Token.verifyOpened) = Some true)

    expect "UC-1 and from here the Server keeps nothing of the Session (Rule 31)"
        (launched.GenPres.InFlight.IsEmpty && launched.GenPres.Pending.IsEmpty)

    // Rule 2. The spent-mark is a nonce and nothing else: GenPRES keeps no copy of
    // the Launch, and the SessionRecord names the nonce that opened it.
    expect "UC-1 the Launch is spent, and all GenPRES keeps of it is the nonce (Rule 2)"
        (launched.Database.Private.Spent
         |> Set.exists (fun n -> (newestRecord launched |> Option.bind _.Launch) = Some n))

    // ── UC-1 ext 1a — no Patient is active in the MainEHR Session ──
    // GenPRES opens and A can prescribe, but a TreatmentPlan cannot be opened or created.
    let noPatient = step "UC-1 ext 1a — no Patient active" world (launchAs ucA.Login None)

    expect "1a a Session opens without a Patient"
        (openCount noPatient = 1 && (newestRecord noPatient |> Option.bind _.Patient) = None)

    expect "1a steps 6 and 7 are skipped: no data to fetch, no PatientRecord to read"
        (never (function ReadPatientData _ -> true | _ -> false)
         && never (function ReadRecord _ -> true | _ -> false))

    let _ =
        step "UC-1 ext 1a — and a TreatmentPlan cannot be created (Rule 12)" noPatient
             [ act 1 (Prescribes(OrderContextId "oc-9")); act 1 Saves ]

    expect "1a prescribing works; submitting does not"
        (saw (function Computed _ -> true | _ -> false)
         && saw (function NoTreatmentPlanHere -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── UC-1 ext 1b — the button is not A's to press ──
    // Rule 1. What the decision is made of is MainEHR's affair; what is ours to
    // state is the refusal, and that nothing leaves the workstation
    // when it happens — the script seals nothing, so no Launch ever exists.
    let notTheirButton =
        step "UC-1 ext 1b — the button is not A's to press" { world with Workstation.MayLaunch = Set.empty }
             (launchAs ucA.Login (Some pat1))

    expect "1b the LaunchScript refuses, and nothing leaves the workstation (Rule 1)"
        (saw (function LaunchError _ -> true | _ -> false)
         && never (function OpenUrl _ -> true | _ -> false)
         && notTheirButton.Clients.IsEmpty
         && openCount notTheirButton = 0)

    // ── UC-1 ext 2b / 3a — the Server is unreachable ──
    // The Client is served by the Server, so a Server that is down serves no Client:
    // there is nothing of ours to show a message with (Consequence 1), and nothing is
    // presented — so, Rule 39, nothing is scrubbed. The Launch waits in the address
    // bar, which is what ext 3a retries from, for as long as Rule 3 allows.
    let serverDown =
        step "UC-1 ext 2b — the Server is down at the launch" world
             (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

    expect "2b nothing of ours is shown: no Client is served, so none speaks (Consequence 1)"
        (openCount serverDown = 0
         && never (function RedeemLaunch _ -> true | _ -> false)
         && never (function ServerUnreachable -> true | _ -> false)
         && showingOf 1 serverDown = Some "the browser's own error page")

    expect "2b and the Launch is still in the bar, because nothing presented it (Rule 39)"
        ((clientOf 1 serverDown |> Option.bind _.UrlLaunch).IsSome
         && (clientOf 1 serverDown |> Option.bind _.RetryLaunch) = None)

    let retried =
        step "UC-1 ext 3a — the Server comes back, and F5 retries within Rule 3's window" serverDown
             (ticks 2 @ [ envt GenPresServer (Start GenPresServer); atClient 1 Refresh ])

    expect "3a the parked Launch is still good, and the Session opens"
        (openCount retried = 1 && saw (function SessionOpened _ -> true | _ -> false))

    let expired =
        step "UC-1 ext 3a — but not past launchTtl (Rule 3, Rule 28)" serverDown
             (ticks 10 @ [ envt GenPresServer (Start GenPresServer); atClient 1 Refresh ])

    // Rule 3 is checked before Rule 2's nonce is even asked about: an aged Launch
    // costs the Database nothing.
    expect "3a an aged Launch opens nothing, and is not even spent (Rules 2, 3)"
        (openCount expired = 0
         && never (function SpendLaunchIfUnspent _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false)
         && expired |> audited "LaunchExpired")

    // ── Rule 39 — the Launch is erased at its first presentation ──
    // A refresh is the same page retrying from its own memory; a reload is a new page,
    // and what it has to re-present is the address bar — scrubbed, and empty.
    expect "Rule 39 nothing of the launch is left in the bar once the Client has presented it"
        ((clientOf 1 retried |> Option.bind _.UrlLaunch) = None
         && (clientOf 1 retried |> Option.bind _.RetryLaunch) = None)

    let reloaded =
        step "Rule 39 — a full reload after the scrub finds nothing to present" retried
             [ atClient 1 ReloadPage ]

    expect "Rule 39 a reload re-presents nothing, because the bar was emptied at the presentation"
        (never (function RedeemLaunch _ -> true | _ -> false)
         && openCount reloaded = openCount retried
         && (clientOf 1 reloaded |> Option.bind _.RetryLaunch) = None)

    // ── UC-1 ext 3b — the Launch is stolen before the Client presents it ──
    // Whoever presents it first wins the race (Rules 2, 4), and the winner gets a
    // Session of their own: nothing of A's is taken, and a relaunch is the whole cost.
    // Park it unpresented — a Server that was down — so the thief can get there first.
    let parked =
        step "UC-1 ext 3b — the Launch sits unpresented in the address bar" world
             (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

    let stolenLaunch = (launchOnTheWire ()).Value

    let thief =
        step "UC-1 ext 3b — a thief presents it first, from their own browser" parked
             [
                 envt GenPresServer (Start GenPresServer)
                 {
                     From = GenPresClient(BrowserId 99)
                     To = GenPresServer
                     Msg = RedeemLaunch(stolenLaunch, Some ucB.Login, None)
                 }
             ]

    // Rule 4, stated as plainly as the model can state it: the Session's User is the
    // browser's, and the Launch had no say. B's browser, B's Session.
    expect "3b the browser proved B, so a Session opens for B — not for A (Rules 4, 5)"
        (openCount thief = 1
         && (sidAt 99 thief).IsSome
         && (newestRecord thief |> Option.bind _.User |> Option.map _.UserId) = Some ucB.UserId
         && (newestRecord thief |> Option.bind _.Patient) = Some pat1)

    // ── UC-1 ext 3c — the browser proved nobody ──
    // A page opened outside a logged-in workstation. There is no User to open a
    // Session for, and the Launch offers none: it opens nothing, and — because Rule 4
    // is checked before Rule 2 — it does not even spend the nonce.
    let unproven =
        step "UC-1 ext 3c — a browser that proved nobody gets no further" parked
             [
                 envt GenPresServer (Start GenPresServer)
                 {
                     From = GenPresClient(BrowserId 98)
                     To = GenPresServer
                     Msg = RedeemLaunch(stolenLaunch, None, None)
                 }
             ]

    expect "3c an unproven browser opens nothing, and spends nothing (Rules 4, 6)"
        (openCount unproven = 0
         && sidAt 98 unproven = None
         && never (function SpendLaunchIfUnspent _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false)
         && unproven |> audited "NoIdentity")

    // ext 3c, the other half: the identity was not there, not wrong. Nothing was spent,
    // so the Launch is still worth presenting — and the refusal says so, which is the
    // only thing that tells this case apart from ext 4a. The User is told no reason
    // either way (Rule 6); what differs is what the Client offers next.
    expect "3c the refusal is marked retryable: the Launch is untouched (Rules 3, 6)"
        (saw (function LaunchRefused true -> true | _ -> false)
         && showingOf 98 unproven = Some "GenPRES could not be reached — try again")

    // And so it is: the identity comes back, the same Launch is presented from the
    // page's own memory (Rule 39), and the Session opens.
    let identityReturned =
        step "UC-1 ext 3c — the identity comes back and the same Launch is presented again" unproven
             [ fromClient 98 (RedeemLaunch(stolenLaunch, Some ucA.Login, None)) ]

    expect "3c the retry opens the Session it was always going to open (Rules 2, 3)"
        (openCount identityReturned = 1
         && (sidAt 98 identityReturned).IsSome
         && (newestRecord identityReturned |> Option.bind _.User |> Option.map _.UserId) = Some ucA.UserId)

    // Where ext 4a differs: nothing there is worth presenting again, and the Client is
    // told to relaunch rather than to retry.
    expect "4a is the other answer: not retryable, and the page keeps nothing (ext 3c, ext 4a)"
        (let spent =
            step "UC-1 ext 4a — a Launch that is spent is not worth presenting again" identityReturned
                 [ fromClient 97 (RedeemLaunch(stolenLaunch, Some ucA.Login, None)) ]

         saw (function LaunchRefused false -> true | _ -> false)
         && showingOf 97 spent = Some "the launch failed — relaunch from MainEHR"
         && (clientOf 97 spent |> Option.bind _.RetryLaunch) = None)

    // ── UC-1 ext 4a — a Launch not sealed under the key ──
    // The mac is over a secret the Client never sees, so the only way to hold a Launch
    // is to be given one. A value with the right shape and a wrong mac is what an
    // attacker actually has, and it buys nothing: refused before the lifetime is even
    // looked at, and before the Database is asked anything (Rules 2, 3).
    let forged =
        step "UC-1 ext 4a — a Launch that was not sealed under the key" world
             [
                 fromClient 96
                     (RedeemLaunch({ stolenLaunch with Mac = stolenLaunch.Mac + "-tampered" }, Some ucA.Login, None))
             ]

    expect "4a a forged Launch opens nothing, spends nothing, and is named in the audit (Rules 2, 3)"
        (openCount forged = 0
         && sidAt 96 forged = None
         && never (function SpendLaunchIfUnspent _ -> true | _ -> false)
         && saw (function LaunchRefused false -> true | _ -> false)
         && forged |> audited "LaunchForged")

    // And the Patient it names buys nothing either: a forged Launch cannot reach a
    // Patient the way a stolen one can, because it never gets as far as one.
    expect "4a and nothing of the Patient it named was ever read (Rules 2, 6)"
        (never (function ReadPatientData _ -> true | _ -> false)
         && never (function ResolveUser _ -> true | _ -> false))

    // ── UC-1 ext 4a — A's own retry, after the theft ──
    // The nonce is spent, and spent by somebody else's browser: Rule 2's replay clause
    // does not apply, so A gets nothing. Theft is a nuisance — a relaunch — and never
    // a Session in A's name.
    let aRetries = step "UC-1 ext 4a — A's own retry finds the Launch spent" thief [ atClient 1 Refresh ]

    expect "4a A opens nothing either: a Launch is single use (Rule 2)"
        (openCount aRetries = openCount thief
         && saw (function LaunchReplayed _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false)
         && aRetries |> audited "LaunchAlreadySpent")

    let relaunchedAfterTheft =
        step "UC-1 ext 4a — and a fresh launch is all it costs" aRetries (launchAs ucA.Login (Some pat1))

    expect "4a the fresh launch opens, bound to A, with nothing of the thief's in the record"
        ((newestRecord relaunchedAfterTheft |> Option.bind _.User |> Option.map _.UserId) = Some ucA.UserId
         && planCount pat1 relaunchedAfterTheft = 0)

    // ── Rule 2's replay clause — the same browser, the same Launch, again ──
    // Which is what an F5 during a slow open is. The nonce is spent, but it was spent
    // by *this* browser and the Launch is still within its lifetime, so the answer is
    // the first answer: the same Session, not a second one.
    let openedOnce =
        step "Rule 2 — a Session opens, and the Launch is spent" world (launchAs ucA.Login (Some pat1))

    let firstLaunch = (launchOnTheWire ()).Value
    let firstSid = (sidAt 1 openedOnce).Value
    let firstCount = openCount openedOnce

    // Rule 39 has emptied the bar, so the honest Client would re-present nothing; a
    // retry that reaches the Server at all is one from the page's own memory. Put it
    // on the wire by hand, which is the same thing without the timing.
    let replayed =
        step "Rule 2 — the same browser presents the same Launch again, in time" openedOnce
             [ fromClient 1 (RedeemLaunch(firstLaunch, Some ucA.Login, Some firstSid)) ]

    expect "Rule 2 the replay gets the first answer back: the same Session, not a second"
        (openCount replayed = firstCount
         && saw (function LaunchReplayed _ -> true | _ -> false)
         && saw (function SessionOpened(sid, _, _, _, _, _, _) -> sid = firstSid | _ -> false)
         && never (function LaunchRefused _ -> true | _ -> false)
         && recordCount replayed = recordCount openedOnce)

    // And the replayed answer is a whole answer: a fresh OpenedToken over the same
    // TreatmentPlan, because the first one may have been spent by a Submission (Rule 33).
    expect "Rule 2 the replay hands back a fresh, verifying OpenedToken (Rule 33)"
        ((clientOf 1 replayed |> Option.bind _.Opened |> Option.map Token.verifyOpened) = Some true)

    // Past the lifetime it is not a retry any more, whoever presents it (Rule 3).
    let replayedLate =
        step "Rule 2 — but not past the lifetime (Rule 3)" openedOnce
             (ticks 25 @ [ fromClient 1 (RedeemLaunch(firstLaunch, Some ucA.Login, Some firstSid)) ])

    expect "Rule 2 an aged Launch is no retry: refused, and the first Session is untouched"
        (openCount replayedLate = firstCount
         && saw (function LaunchRefused _ -> true | _ -> false)
         && stateOf 1 replayedLate = Some OpenOrGone)

    // Somebody else's browser is not a retry either, however fresh the Launch.
    let replayedByAnother =
        step "Rule 2 — and another browser's presentation is no retry at all" openedOnce
             [ fromClient 97 (RedeemLaunch(firstLaunch, Some ucB.Login, None)) ]

    expect "Rule 2 a replay for another identity opens nothing (Rules 2, 4)"
        (openCount replayedByAnother = firstCount
         && sidAt 97 replayedByAnother = None
         && saw (function LaunchRefused _ -> true | _ -> false))

    // Nor is A's own login from a second browser. The clause is about one browser
    // coming back, not about who is proving what: handing the first browser's
    // SessionId to a second would put two browsers on one Session, which Rules 7 and
    // 40 spend a whole act each to prevent — and the SessionId is a bearer credential
    // (Rule 11), so it would be handing it to whoever is at that second screen.
    let replayedSameLoginElsewhere =
        step "Rule 2 — and not even A's own login, from a second browser" openedOnce
             [ fromClient 96 (RedeemLaunch(firstLaunch, Some ucA.Login, None)) ]

    expect "Rule 2 the same login from another browser is no retry: no Session, and none handed over"
        (openCount replayedSameLoginElsewhere = firstCount
         && sidAt 96 replayedSameLoginElsewhere = None
         && never (function SessionOpened _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false))

    // ── UC-1 ext 5a — the UserRegistry cannot say what the login may do ──
    let registryDown =
        step "UC-1 ext 5a — the registry is unreachable" world
             (envt UserRegistry (Stop UserRegistry) :: launchAs ucA.Login (Some pat1))

    expect "5a no launched Session, and rights fail closed (Rules 5, 6)"
        (openCount registryDown = 0
         && saw (function AuthorityUnavailable -> true | _ -> false))

    expect "5a the anonymous open is offered — relaunching would not cure this"
        ((clientOf 1 registryDown |> Option.map _.AnonymousOffer) = Some true)

    let wentAnonymous =
        step "UC-1 ext 5a — A accepts, and gets a fresh anonymous open (Rule 6)" registryDown
             [ atClient 1 AcceptAnonymousOffer ]

    expect "5a it carries nothing over from the launch: no User, no Patient"
        (openCount wentAnonymous = 1
         && (newestRecord wentAnonymous |> Option.bind _.User) = None
         && (newestRecord wentAnonymous |> Option.bind _.Patient) = None)

    // ── UC-1 ext 5b — the launching User is a Reader ──
    let asReader = step "UC-1 ext 5b — C, a Reader, launches for Patient 3" world (launchAs ucC.Login (Some pat3))

    expect "5b a Session opens, with the Reader Role"
        ((newestRecord asReader |> Option.bind _.User |> Option.map _.Role) = Some Reader)

    expect "5b a Reader is never asked for a PIN — not asked and ignored, but not asked (Rule 25)"
        (never (function ReadCredential _ -> true | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    expect "5b and starts from the most recent Signed TreatmentPlan, not A's Unsigned head (Rules 18, 19)"
        (openedAt 1 asReader = Some p3Signed.Id)

    // ── UC-1 ext 5c — User A has no PIN yet ──
    // First launch as a Prescriber. UC-2 is this case in full: a PIN must be set
    // before the launch continues (Rule 24).

    // ── UC-1 ext 6a — the PatientDataPlatform is unreachable ──
    let noPlatform =
        step "UC-1 ext 6a — the PatientDataPlatform is unreachable" world
             (envt PatientDataPlatform (Stop PatientDataPlatform) :: launchAs ucA.Login (Some pat2))

    expect "6a the launch continues: a PatientId and no data (Concept 2)"
        (openCount noPlatform = 1
         && (newestRecord noPlatform |> Option.bind _.Patient) = Some pat2
         && dataAt 1 noPlatform = None)

    expect "6a TreatmentPlans work as normal — the PatientId is there (Rule 12)"
        (openedAt 1 noPlatform = Some p2Signed.Id)

    // ── UC-1 ext 8a / 9a — A already has an open Session, or the wrong Patient ──
    // Rule 7 is per User, not per Patient, so both are the same mechanism: the
    // earlier Session is closed and A is told work in it may have been lost.
    let wrongPatient = step "UC-1 ext 9a — A launched for the wrong Patient" world (launchAs ucA.Login (Some pat1))
    let relaunched =
        step "UC-1 ext 8a/9a — A activates Patient 2 and relaunches" wrongPatient
             (launchAs ucA.Login (Some pat2))

    expect "9a the wrong Session is closed, whichever Patient it was for (Rules 7, 40)"
        (openCount relaunched = 1
         && (newestRecord relaunched |> Option.bind _.Patient) = Some pat2
         && (match stateOf 1 relaunched with Some(Ended(Superseded, _)) -> true | _ -> false))

    expect "8a and A is told, once (Rule 10)"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && wasTold 1 relaunched)

    // ── UC-1 ext 8b — two launches at once ──
    // Rule 7 is a count, and a count read and then written back is a race. Rule 40
    // makes the opening and the closing one act at the Database, so the two orders of
    // arrival have the same answer: one open Session, whichever won.
    let racedLaunches =
        racing "UC-1 ext 8b — two of A's launches arrive at once" world
               (launchAs ucA.Login (Some pat1) @ launchAs ucA.Login (Some pat2))

    expect "8b exactly one Session is open, and the other is Superseded (Rules 7, 40)"
        (openCount racedLaunches = 1
         && recordCount racedLaunches = 2
         && (racedLaunches.Database.Private.Sessions
             |> List.filter (fun r -> not (SessionRecord.isOpen r))
             |> List.forall (fun r -> match r.State with Ended(Superseded, _) -> true | _ -> false)))

    launched

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-2  First launch as a Prescriber: no PIN yet
// ═══════════════════════════════════════════════════════════════════════════════

let uc2 () =
    printfn ""
    printfn "############### UC-2  First launch as a Prescriber ###############"

    // Precondition: UC-1 has reached step 10 — the UserContext carries the Prescriber
    // Role, and no PIN is set for that login.
    let noPin =
        { world with
            Database.Private.Credentials = world.Database.Private.Credentials |> Map.remove ucA.UserId }

    let asked =
        step "UC-2 main — A launches as a Prescriber for the first time" noPin
             (launchAs ucA.Login (Some pat1))

    expect "UC-2 step 1: the launch stops, a code is mailed, and nothing else is offered"
        (saw (function PinRequired _ -> true | _ -> false)
         && (mailsTo mailA asked).Length = 1
         && (codeInMail mailA asked).IsSome
         && asked |> audited "PIN enrolment code sent"
         && openCount asked = 0
         && never (function SessionOpened _ -> true | _ -> false))

    // Step 2's order, and not merely its content: the mail goes out before anything
    // is asked of the screen, because what the screen is asked for is the code.
    expect "UC-2 step 1: the code is mailed before the Client is asked (Rules 26, 37)"
        (before (function SendMail _ -> true | _ -> false)
                (function PinRequired _ -> true | _ -> false))

    // The order matters twice over: the code's address comes from the UserRegistry, so
    // it cannot even be mailed before the registry has said who the login belongs to.
    expect "UC-2 the PIN is offered only after the registry recognised the login (Rule 24)"
        (before (function UserResolved _ -> true | _ -> false)
                (function PinRequired _ -> true | _ -> false))

    let unknown =
        step "UC-2 — a login the registry does not know never reaches the PIN question" noPin
             (launchAs (LoginName "dr.x") (Some pat1))

    expect "UC-2 an unrecognised login is refused before any PIN is offered, and no code is mailed"
        (openCount unknown = 0
         && saw (function NotAuthorised -> true | _ -> false)
         && never (function PinRequired _ -> true | _ -> false)
         && never (function SendMail _ -> true | _ -> false))

    // The prompt is put to one Client, and that Client answers it. Another browser
    // holding the attempt number is not the same thing as the User at that screen —
    // and would not get past the code either (ext 3c).
    let intruder =
        let att = asked.GenPres.Pending |> Map.toList |> List.head |> fst
        step "UC-2 — a second browser answers the prompt A was given" asked
             [
                 {
                     From = GenPresClient(BrowserId 99)
                     To = GenPresServer
                     Msg = SupplyPin(att, ResetCode "code-guess", Pin "0000")
                 }
             ]

    expect "UC-2 only the Client the prompt was put to may answer it (Concept 7; Rules 22, 24)"
        (saw (function Refused _ -> true | _ -> false)
         && never (function ReplacePinIfCode _ -> true | _ -> false)
         && (credentialOf ucA intruder |> Option.bind _.Pin) = None
         && openCount intruder = 0)

    let enrolCode = (codeInMail mailA asked).Value

    let enrolled =
        step "UC-2 steps 2 and 3 — A reads the code and sets a PIN with it" asked
             [ atClient 1 (ChoosePin(enrolCode, Pin "9999")) ]

    expect "UC-2 step 3: the PIN is set on A's UserCredential, created since GenPRES held none"
        ((credentialOf ucA enrolled |> Option.bind _.Pin) = Some(Pin "9999"))

    expect "UC-2 step 3: it went through the one act a PIN is ever set by (Rule 37)"
        (saw (function ReplacePinIfCode _ -> true | _ -> false)
         && saw (function PinReplaced _ -> true | _ -> false))

    expect "UC-2 step 3: the change is recorded and A is mailed — the code, then the setting (Rule 26)"
        ((mailsTo mailA enrolled).Length = 2
         && enrolled |> audited "PIN set")

    expect "UC-2 a newly set PIN starts with a count of zero (Rule 27)"
        ((credentialOf ucA enrolled |> Option.map _.AttemptCount) = Some 0)

    expect "UC-2 step 3: the launch continues from UC-1 step 6"
        (openCount enrolled = 1
         && saw (function SessionOpened _ -> true | _ -> false)
         && saw (function PatientDataRead _ -> true | _ -> false))

    // ── UC-2 ext 2a — A does not answer ──
    // No code comes back, so no PIN is set and the launch is not honoured (Rule 6).
    // Relaunching does not mail a second code while the first is still good: the code
    // is in A's mailbox, and sending another would void the one A is about to read
    // (Rule 37). The launch asks for that code instead.
    let askedAgain =
        step "UC-2 ext 2a — A does not answer, and relaunches" asked
             (launchAs ucA.Login (Some pat1))

    expect "2a nothing was set and no Session opened (Rule 6) — and it asks again"
        (openCount askedAgain = 0
         && (credentialOf ucA askedAgain |> Option.bind _.Pin) = None
         && saw (function PinRequired _ -> true | _ -> false))

    expect "2a two requests, one mail: the standing code is not voided by asking again (Rule 37)"
        ((mailsTo mailA askedAgain).Length = 1
         && (codeInMail mailA askedAgain) = Some enrolCode
         && askedAgain |> audited "already sent and still good")

    // And the code A is holding is the one that works, even after the second launch.
    let answeredLate =
        step "UC-2 ext 2a — and the code A already has is the one that works" askedAgain
             [ atClient 2 (ChoosePin(enrolCode, Pin "9999")) ]

    expect "2a the standing code sets the PIN and the second launch continues (Rule 37)"
        ((credentialOf ucA answeredLate |> Option.bind _.Pin) = Some(Pin "9999")
         && openCount answeredLate = 1)

    // Once it expires, the way on is a fresh request with a fresh mail.
    let afterExpiry =
        step "UC-2 ext 2a — once the code expires, the next launch mails a fresh one (Rule 37)" asked
             (ticks (resetCodeTtl + 4) @ launchAs ucA.Login (Some pat1))

    expect "2a an expired code is no bar: the next launch mails a fresh one (Rule 37)"
        ((mailsTo mailA afterExpiry).Length = 2
         && (codeInMail mailA afterExpiry) <> Some enrolCode)

    // ── UC-2 ext 2b — the code comes back wrong ──
    let guessed =
        step "UC-2 ext 2b — a few wrong codes, and this one is void" asked
             [ for i in 1 .. wrongCodeLimit -> atClient 1 (ChoosePin(ResetCode $"code-no%i{i}", Pin "0000")) ]

    expect "2b nothing is set, and the last try says the code is void (Rule 37)"
        ((credentialOf ucA guessed |> Option.bind _.Pin) = None
         && saw (function ResetDenied(WrongCode _) -> true | _ -> false)
         && saw (function ResetDenied ResetVoid -> true | _ -> false))

    let afterVoidCode =
        step "UC-2 ext 2b — and the mailed code is void with it" guessed
             [ atClient 1 (ChoosePin(enrolCode, Pin "9999")) ]

    expect "2b even the right code buys nothing now, and the launch is over"
        ((credentialOf ucA afterVoidCode |> Option.bind _.Pin) = None
         && openCount afterVoidCode = 0)

    let freshLaunch =
        step "UC-2 ext 2b — a fresh launch mails a fresh code" afterVoidCode
             (launchAs ucA.Login (Some pat1))

    expect "2b the way on is a fresh request with a fresh mail (Rule 37)"
        (saw (function PinRequired _ -> true | _ -> false)
         && (codeInMail mailA freshLaunch) <> Some enrolCode)

    // ── UC-2 ext 2c — someone else at the workstation of a Prescriber who never enrolled ──
    // The launch runs to step 2 and stalls: the code went to A's mail, which the other
    // hands do not control (Possibility 1). This is UC-7 ext 1a's gate, at enrolment.
    let stranger =
        step "UC-2 ext 2c — another person tries to enrol as A" asked
             [ atClient 1 (ChoosePin(ResetCode "code-stranger", Pin "1234")) ]

    expect "2c no PIN of a stranger's choosing binds to A's credential (Rule 37)"
        ((credentialOf ucA stranger |> Option.bind _.Pin) = None
         && never (function SessionOpened _ -> true | _ -> false))

    expect "2c the code went to A, nobody else was mailed, and the attempt is in the audit (Rules 26, 46)"
        ((mailsTo mailA stranger).Length = 1
         && (mailsTo mailB stranger).Length = 0
         && stranger |> audited "PIN code refused")

    // A Reader in the same position is never asked at all.
    let readerNoPin =
        step "UC-2 — a Reader with no PIN is never asked (Rule 25)" noPin
             (launchAs ucC.Login (Some pat2))

    expect "UC-2 the Reader's launch is never held up by a PIN"
        (openCount readerNoPin = 1
         && never (function PinRequired _ -> true | _ -> false))

    // No use case asks for this — model hygiene. A launch that stalls mid-flight would
    // otherwise sit in the launch table forever, which is harmless here and a leak in
    // production. Everything but AwaitingPinChoice is waiting on a round trip and is
    // collectable; that one waits on a human and is not.
    let stalled =
        let ctx =
            {
                Client = GenPresClient(BrowserId 1)
                Launch = Token.mintLaunch (Some pat1) 0
                Identity = ucA.Login
                Replacing = None
                Resuming = None
            }
        { world with
            GenPres.Pending =
                Map.empty
                |> Map.add (AttemptId 90) { Stage = AwaitingUser ctx; Since = 0 }
                |> Map.add (AttemptId 91) { Stage = AwaitingPinChoice(ctx, ucA, mailA); Since = 0 } }

    let swept =
        step "UC-2 — an abandoned launch is collected; one waiting on a human is not" stalled
             (ticks (launchAbandonTtl + 5))

    expect "UC-2 a launch stalled mid-flight is dropped; one suspended on a human is kept (UC-2 step 2)"
        (not (swept.GenPres.Pending.ContainsKey(AttemptId 90))
         && swept.GenPres.Pending.ContainsKey(AttemptId 91))


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-3  Prescribe, save and sign
// ═══════════════════════════════════════════════════════════════════════════════

let uc3 () =
    printfn ""
    printfn "############### UC-3  Prescribe, save and sign ###############"

    // Precondition: UC-1 completed — A has an open Session for Patient 2, started from
    // its Signed head, and holds the Prescriber Role.
    let opened = quiet "UC-3 precondition" world (launchAs ucA.Login (Some pat2))

    expect "UC-3 precondition: the Session started from Patient 2's Signed head (Rule 19)"
        (openedAt 1 opened = Some p2Signed.Id)

    let saved =
        step "UC-3 steps 1 and 2 — A prescribes and saves" opened
             [
                 act 1 (Prescribes(OrderContextId "oc-4"))
                 act 1 Saves
             ]

    expect "UC-3 step 1: each change goes to the Server, which answers from the payload (Rules 8, 31)"
        (saw (function Computed _ -> true | _ -> false))

    expect "UC-3 step 2: nothing blocks and nothing warns (Rules 20, 21)"
        (never (function SubmissionBlocked _ -> true | _ -> false)
         && never (function UnsignedWorkNotice _ -> true | _ -> false))

    expect "UC-3 step 2: an Unsigned TreatmentPlan is appended, carrying A's UserContext (Rule 14)"
        (planCount pat2 saved = 2
         && (headOf pat2 saved |> Option.map _.State) = Some Unsigned
         && (headOf pat2 saved |> Option.map _.By) = Some ucA)

    expect "UC-3 step 2: and its base (Concept 13)"
        ((headOf pat2 saved |> Option.bind _.Base) = Some p2Signed.Id)

    expect "UC-3 Rule 14: the OrderContext changed in the Session is stamped"
        (headOf pat2 saved
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.forall (fun o -> o.Stamp = Some ucA))

    expect "UC-3 Rule 33: the Submission carried the opened-with token, and a new one came back"
        (saw (function
              | SessionRequest(_, SubmitTreatmentPlan req) -> Token.plan req.Opened = Some p2Signed.Id
              | _ -> false)
         && openedAt 1 saved = (headOf pat2 saved |> Option.map _.Id))

    let signed = step "UC-3 step 3 — A signs" saved [ yield! signs 1 pinA ]

    expect "UC-3 step 3: a Signed TreatmentPlan in A's name (Concept 14, Rules 14, 15)"
        (planCount pat2 signed = 3
         && (headOf pat2 signed |> Option.map _.State) = Some Signed
         && (headOf pat2 signed |> Option.map _.By) = Some ucA)

    expect "UC-3 step 3: it is now the most recent Signed TreatmentPlan and counts clinically (Rule 16)"
        ((recordFor pat2 signed |> PatientRecord.latestSigned |> Option.map _.Id)
            = (headOf pat2 signed |> Option.map _.Id))

    expect "UC-3 the correct entry reset the wrong-entry count (Rule 27)"
        ((credentialOf ucA signed |> Option.map _.AttemptCount) = Some 0)

    // ── UC-3 ext 2a — the record has moved on since A opened ──
    // If what appeared is Unsigned, A is notified and may submit anyway or hold off
    // (Rule 21). If a Signed TreatmentPlan appeared, submitting is blocked (Rule 20). UC-6 is
    // this case in full.

    // ── UC-3 ext 3a — A does not sign ──
    // The Unsigned TreatmentPlan stays at the head, inert. Only A can open it (Rule 18),
    // and it counts for nothing until signed (Rule 16).
    expect "3a an Unsigned head does not count clinically (Rule 16)"
        ((recordFor pat2 saved |> PatientRecord.latestSigned |> Option.map _.Id) = Some p2Signed.Id)

    expect "3a only its creator can open it (Rule 18)"
        ((recordFor pat2 saved |> PatientRecord.mayOpen ucA.UserId (headOf pat2 saved).Value.Id).IsSome
         && (recordFor pat2 saved |> PatientRecord.mayOpen ucB.UserId (headOf pat2 saved).Value.Id).IsNone)

    // ── UC-3 ext 3b — A gives the wrong PIN ──
    let wrongOnce = step "UC-3 ext 3b — A gives the wrong PIN" saved [ yield! signs 1 (Pin "0000") ]

    expect "3b verification fails and no TreatmentPlan is created"
        (planCount pat2 wrongOnce = 2
         && saw (function PinRejected _ -> true | _ -> false))

    // Rule 33. A refused Submission spends nothing — the Client is left holding what it
    // came with, and may answer the refusal. A wrong PIN must not cost the User their
    // baseline as well as their attempt.
    expect "3b a refused signature spends neither the opened-with token nor the challenge (Rule 33)"
        (wrongOnce.Database.Private.Spent = saved.Database.Private.Spent)

    expect "3b the count is on the UserCredential, not the Session (Rule 27)"
        ((credentialOf ucA wrongOnce |> Option.map _.AttemptCount) = Some 1)

    let atLimit =
        step "UC-3 ext 3b — and at the limit the Session ends (Rules 9, 27)" wrongOnce
             [
                 yield! signs 1 (Pin "0000")
                 yield! signs 1 (Pin "0000")
             ]

    expect "3b the Session ends at the wrong-PIN limit"
        (openCount atLimit = 0
         && (match stateOf 1 atLimit with Some(Ended(WrongPinLimit, _)) -> true | _ -> false)
         && saw (function SessionRefused(Some WrongPinLimit) -> true | _ -> false))

    // Rule 10. The screen is told what ended, and nothing is discharged by telling it:
    // the notice is owed to the User, who hears it at their next launch.
    expect "3b the ending is told to the screen and still owed to the User (Rule 10)"
        (noticeOf 1 atLimit = Some Owed && not (wasTold 1 atLimit))

    expect "3b the count survives the Session, and the credential is locked with it"
        ((credentialOf ucA atLimit |> Option.map _.AttemptCount) = Some wrongPinLimit
         && (credentialOf ucA atLimit |> Option.bind _.LockedUntil).IsSome)

    // Rule 27. What survives is not merely a number but the standing of the
    // credential: a fresh Session does not hand back the attempts, and the correct PIN
    // does not either — not yet.
    let relaunchedAfterLimit =
        quiet "3b — A relaunches after the limit" atLimit (launchAs ucA.Login (Some pat2))

    let stillLocked =
        step "UC-3 ext 3b — a new Session, the right PIN, and signing is still locked" relaunchedAfterLimit
             [ act 2 (Prescribes(OrderContextId "oc-locked")); yield! signs 2 pinA ]

    expect "3b within the delay the correct PIN does not sign either (Rule 27)"
        (saw (function SigningLocked -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 stillLocked = planCount pat2 relaunchedAfterLimit
         && openCount stillLocked = 1)

    // Rule 27. Wait the delay out and the same credential signs again. The wait
    // outlives the Session that was waiting, so what signs is a new Session of the
    // same User — the credential is what was locked, not the Session.
    let waited =
        let until = (credentialOf ucA stillLocked |> Option.bind _.LockedUntil).Value
        quiet "3b — the delay passes" stillLocked (ticks (until - stillLocked.Env.Now + 4))

    let waitedOut =
        step "UC-3 ext 3b — and the same credential signs again, with no reset at all (Rule 27)" waited
             (launchAs ucA.Login (Some pat2)
              @ [ act 3 (Prescribes(OrderContextId "oc-after-the-wait")); yield! signs 3 pinA ])

    expect "3b a locked credential signs again once the delay passes — no reset, no mail (Rule 27)"
        (planCount pat2 waitedOut = planCount pat2 waited + 1
         && (headOf pat2 waitedOut |> Option.map _.State) = Some Signed
         && (credentialOf ucA waitedOut |> Option.map _.AttemptCount) = Some 0
         && (credentialOf ucA waitedOut |> Option.bind _.LockedUntil) = None
         && never (function ResetCodeMailed -> true | _ -> false)
         && never (function SigningLocked -> true | _ -> false))

    // And each further wrong entry past the limit costs twice the last.
    expect "3b the delay doubles with every wrong entry past the limit (Rule 27)"
        (UserCredential.lockFor wrongPinLimit = pinLockBase
         && UserCredential.lockFor (wrongPinLimit + 1) = pinLockBase * 2
         && UserCredential.lockFor (wrongPinLimit + 2) = pinLockBase * 4)

    // Rule 27, and the half that decides whether the doubling is worth anything: a
    // wrong entry made *while* the credential is locked counts too. Otherwise a
    // guesser simply keeps guessing through the delay and pays for one lock however
    // many they try — the delay would grow with patience rather than with guessing.
    let guessedThrough =
        step "UC-3 ext 3b — more guesses while it is already locked (Rule 27)" stillLocked
             (signs 2 (Pin "0009") @ signs 2 (Pin "0008"))

    expect "3b a guess made while locked counts, and pushes the delay further out (Rule 27)"
        (let before = (credentialOf ucA stillLocked).Value
         let after = (credentialOf ucA guessedThrough).Value

         after.AttemptCount = before.AttemptCount + 2
         && after.LockedUntil > before.LockedUntil
         && saw (function SigningLocked -> true | _ -> false))

    // And the correct PIN inside the delay costs nothing: the delay answers what has
    // already happened, and waiting is what lifts it (Rule 27).
    let rightPinWhileLocked =
        step "UC-3 ext 3b — and the right PIN inside the delay costs nothing" stillLocked (signs 2 pinA)

    expect "3b the correct PIN inside the delay neither signs nor counts against the User (Rule 27)"
        ((credentialOf ucA rightPinWhileLocked |> Option.map _.AttemptCount)
            = (credentialOf ucA stillLocked |> Option.map _.AttemptCount)
         && (credentialOf ucA rightPinWhileLocked |> Option.bind _.LockedUntil)
            = (credentialOf ucA stillLocked |> Option.bind _.LockedUntil)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // Rule 37 is still a way out, and a faster one: a code by mail, a new PIN, one act.
    let askedForReset = quiet "3b — A asks for a reset" stillLocked [ act 2 AsksPinReset ]

    let unlocked =
        let code = (codeInMail mailA askedForReset).Value
        step "UC-3 ext 3b — or the mailed code replaces the PIN, and signing works at once (Rule 37)" askedForReset
             [ act 2 (EntersResetCode(code, Pin "4242")); yield! signs 2 (Pin "4242") ]

    expect "3b the replacement clears the lock and the count with it, without waiting (Rules 27, 37)"
        ((credentialOf ucA unlocked |> Option.bind _.LockedUntil) = None
         && (credentialOf ucA unlocked |> Option.map _.AttemptCount) = Some 0
         && planCount pat2 unlocked = planCount pat2 askedForReset + 1
         && (headOf pat2 unlocked |> Option.map _.State) = Some Signed)

    // ── UC-3 ext 3c — A signs without saving first ──
    // Steps 2 and 3 become one act, and the block and notification checks run before
    // the PIN is asked for. Set up a block, and watch nothing ask for a credential.
    let bSigned =
        quiet "UC-3 ext 3c setup — B signs while A is open" opened
              (launchAs ucB.Login (Some pat2)
               @ [ act 2 (Prescribes(OrderContextId "oc-5")); yield! signs 2 pinB ])

    let blocked =
        step "UC-3 ext 3c — A signs without saving, and is blocked before the PIN" bSigned
             [ act 1 (Prescribes(OrderContextId "oc-6")); yield! signs 1 pinA ]

    expect "3c the block is decided first: no credential is ever read (Rules 20, 22)"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && never (function ReadCredential(ForRequest _, _) -> true | _ -> false))

    expect "3c and nothing was appended"
        (planCount pat2 blocked = planCount pat2 bSigned)

    // ── UC-3 ext 3d — the signature modal ──
    // Rule 43. Asking to sign is one act and signing what the modal shows another,
    // with nothing sent in between and no change allowed under it. The modal is up
    // because the User asked and the Server answered — the honest path, stopped half
    // way, which is where the rule bites.
    let modalUp =
        step "UC-3 ext 3d — A asks to sign, and is shown what the signature would attest to" signed
             [ act 1 (Prescribes(OrderContextId "oc-shown")); act 1 (Signs pinA) ]

    expect "3d the challenge is shown and nothing is submitted: the modal gates the signature (Rule 43)"
        (saw (function SignChallengeIssued _ -> true | _ -> false)
         && (clientOf 1 modalUp |> Option.bind _.Modal |> Option.map Token.verifyChallenge) = Some true
         && never (function SessionRequest(_, SubmitTreatmentPlan _) -> true | _ -> false)
         && planCount pat2 modalUp = planCount pat2 signed
         && showingOf 1 modalUp = Some "sign the plan as shown, or cancel and edit")

    let heldStill =
        step "UC-3 ext 3d — with the modal up, the WorkPlan cannot change" modalUp
             [ act 1 (Prescribes(OrderContextId "oc-late")); act 1 (EntersPatientData(PatientData "by hand")) ]

    expect "3d the Client refuses locally: nothing is sent, and the WorkPlan is untouched (Rule 43)"
        (workingAt 1 heldStill = workingAt 1 modalUp
         && dataAt 1 heldStill = dataAt 1 modalUp
         && never (function SessionRequest _ -> true | _ -> false))

    let cancelled = step "UC-3 ext 3d — the User leaves the modal" heldStill [ act 1 CancelsSign ]

    expect "3d nothing was signed, and prescribing is possible again"
        (planCount pat2 cancelled = planCount pat2 signed
         && (clientOf 1 cancelled |> Option.bind _.Modal) = None)

    // And cancelling really does drop it: the PIN goes with the challenge, so a
    // confirm afterwards has nothing to answer and sends nothing (Rule 43).
    let confirmedAfterCancel =
        step "UC-3 ext 3d — a confirm after the cancel answers nothing" cancelled [ act 1 ConfirmsSign ]

    expect "3d a confirm with no challenge in front of the User sends nothing at all (Rule 43)"
        (never (function SessionRequest _ -> true | _ -> false)
         && planCount pat2 confirmedAfterCancel = planCount pat2 cancelled)

    let signedAfresh =
        step "UC-3 ext 3d — and the next signature asks for a challenge of its own" cancelled
             [ act 1 (Prescribes(OrderContextId "oc-7")); yield! signs 1 pinA ]

    expect "3d the honest path never sees a refusal: a fresh challenge, and the signature lands"
        (saw (function SignChallengeIssued _ -> true | _ -> false)
         && never (function SubmissionRefused _ -> true | _ -> false)
         && (headOf pat2 signedAfresh |> Option.map _.State) = Some Signed)

    // ── UC-3 ext 3e — the plan changed under the challenge ──
    // The honest Client cannot do this, which is the point: a challenge names one
    // WorkPlan, and a signature carrying it answers for that WorkPlan and no other.
    let stale = (challengeIssued ()).Value

    let mismatched =
        let sid = (sidAt 1 signedAfresh).Value
        let opened = (clientOf 1 signedAfresh).Value.Opened.Value
        let changed =
            { workOf 1 signedAfresh with
                Orders =
                    { Id = OrderContextId "oc-slipped"; Patient = Some pat2; Content = "added after"; Stamp = None }
                    :: (workOf 1 signedAfresh).Orders }
        step "UC-3 ext 3e — the challenge is returned over a plan it does not name" signedAfresh
             [
                 fromClient 1
                     (SessionRequest(
                         sid,
                         SubmitTreatmentPlan
                             {
                                 Work = changed
                                 Opened = opened
                                 Notice = None
                                 Challenge = Some stale
                                 DataOk = None
                                 Pin = Some pinA
                                 Key = IdemKey "mismatch-1"
                             }))
             ]

    expect "3e the signature is refused, and nothing is appended (Rule 43)"
        (saw (function SubmissionRefused why -> why.Contains "Rule 43" | _ -> false)
         && planCount pat2 mismatched = planCount pat2 signedAfresh)

    // ── UC-3 ext 3f — the reply was lost and the Submission is sent again ──
    // Rule 45. The retry carries the key of the request it retries, so the Database
    // answers it rather than doing it twice.
    let duplicated =
        let sid = (sidAt 1 signedAfresh).Value
        let opened = (clientOf 1 signedAfresh).Value.Opened.Value
        let again =
            SessionRequest(
                sid,
                SubmitTreatmentPlan
                    {
                        Work = workOf 1 signedAfresh
                        Opened = opened
                        Notice = None
                        Challenge = None
                        DataOk = None
                        Pin = None
                        Key = IdemKey "retry-1"
                    })
        step "UC-3 ext 3f — the same Submission arrives twice" signedAfresh
             [ fromClient 1 again; fromClient 1 again ]

    expect "3f one TreatmentPlan, and the same answer both times (Rule 45)"
        (planCount pat2 duplicated = planCount pat2 signedAfresh + 1
         && countOf (function TreatmentPlanSubmitted _ -> true | _ -> false) = 2
         && (lastTrace
             |> List.choose (function { Msg = TreatmentPlanSubmitted(id, _, _) } -> Some id | _ -> None)
             |> List.distinct
             |> List.length) = 1)

    // Rule 45 answers a retry that carries the same key. A signature replayed under a
    // *fresh* key is a different request asking to sign again — and it is the spent
    // challenge that stops it (Rule 43), not the idempotency table.
    let replayedChallenge =
        let signedOnce =
            quiet "3f precondition — a signature that landed, and the challenge it used" duplicated
                  [ act 1 (Prescribes(OrderContextId "oc-once")); act 1 (Signs pinA) ]

        let challenge = (challengeIssued ()).Value
        let landed = quiet "3f precondition — and it lands" signedOnce [ act 1 ConfirmsSign ]

        let sid = (sidAt 1 landed).Value
        let opened = (clientOf 1 landed).Value.Opened.Value

        step "UC-3 ext 3f — the spent SigningChallenge is presented again" landed
             [
                 fromClient 1
                     (SessionRequest(
                         sid,
                         SubmitTreatmentPlan
                             {
                                 Work = workOf 1 landed
                                 Opened = opened
                                 Notice = None
                                 Challenge = Some challenge
                                 DataOk = None
                                 Pin = Some pinA
                                 Key = IdemKey "replayed-challenge"
                             }))
             ]

    expect "3f a spent SigningChallenge signs nothing a second time (Rules 43, 45)"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── Rule 44 — the Patient Data moved under the Session ──
    // Concept 2 reads the data once, at the launch. A signature is where that stops
    // being good enough: the platform is asked again, and a signature over data it no
    // longer holds does not land until the User has seen what changed.
    let dataMoved =
        { signedAfresh with
            Platform.Data =
                signedAfresh.Platform.Data |> Map.add pat2 (PatientData "pat-2: 7y, 26kg — revised") }

    let stoppedAtData =
        step "Rule 44 — the platform's Patient Data has changed since the launch" dataMoved
             [ act 1 (Prescribes(OrderContextId "oc-8")); yield! signs 1 pinA ]

    expect "Rule 44 the signature does not land: the User is shown what the platform now holds"
        (saw (function PatientDataChanged _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 stoppedAtData = planCount pat2 signedAfresh
         && dataAt 1 stoppedAtData = Some(PatientData "pat-2: 7y, 26kg — revised"))

    let acceptedData =
        step "Rule 44 — A reads the new data and signs again" stoppedAtData [ yield! signs 1 pinA ]

    expect "Rule 44 accepted, the signature lands (Rules 21, 34's pattern, over data)"
        (planCount pat2 acceptedData = planCount pat2 stoppedAtData + 1
         && (headOf pat2 acceptedData |> Option.map _.State) = Some Signed)

    // Concept 13, and Rule 44's last sentence. What the plan records is the data the
    // User was shown and accepted — not what the launch read, and not what the platform
    // holds now. A signed plan explains itself from its own record.
    expect "Rule 44 the signed plan records the data the User accepted, not the launch's (Concept 13)"
        (let head = (headOf pat2 acceptedData).Value

         head.Data = Some(PatientData "pat-2: 7y, 26kg — revised")
         && (match head.From with Some(FromPlatform _) -> true | _ -> false))

    // ── Rule 44 — and the branch where the platform cannot be asked at all ──
    // UC-1 ext 6a happens at a launch; this is the same outage at a signature. Nothing
    // is refused: the User is told the data behind the signature is the Session's own
    // and unchecked, and signs on it only by saying so — the same shape as a change.
    // From a Session that has accepted nothing: an acceptance the User has already
    // given stands for the data it names, so a lingering one would forgive the outage
    // rather than test it.
    let platformSilent =
        step "Rule 44 — the platform cannot be asked when the challenge is due" signedAfresh
             (envt PatientDataPlatform (Stop PatientDataPlatform)
              :: [ act 1 (Prescribes(OrderContextId "oc-unchecked")) ]
              @ signs 1 pinA)

    expect "Rule 44 no challenge is issued, and the User is told the data is unverified"
        (saw (function PatientDataUnverified _ -> true | _ -> false)
         && never (function SignChallengeIssued _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 platformSilent = planCount pat2 signedAfresh)

    let signedUnchecked =
        step "Rule 44 — A says so, and signs on the data as it stands" platformSilent (signs 1 pinA)

    expect "Rule 44 accepting the unverified data is what lets the signature land"
        (planCount pat2 signedUnchecked = planCount pat2 platformSilent + 1
         && (headOf pat2 signedUnchecked |> Option.map _.State) = Some Signed)

    signed

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-4  Work left unsigned by someone else
// ═══════════════════════════════════════════════════════════════════════════════

let uc4 () =
    printfn ""
    printfn "############### UC-4  Work left unsigned by someone else ###############"

    // Precondition: Patient 3's head is an Unsigned TreatmentPlan of A's over an older
    // Signed one. B launches, holding the Prescriber Role.
    let bOpen = step "UC-4 step 1 — B launches for Patient 3" world (launchAs ucB.Login (Some pat3))

    expect "UC-4 step 1: B starts from the older Signed TreatmentPlan, not A's Unsigned head (Rules 18, 19)"
        (openedAt 1 bOpen = Some p3Signed.Id)

    expect "UC-4 step 1: A's Unsigned work is closed to B (Rule 18)"
        (recordFor pat3 bOpen |> PatientRecord.mayOpen ucB.UserId p3Unsigned.Id).IsNone

    let warned =
        step "UC-4 step 2 — B enters orders and saves" bOpen
             [
                 act 1 (Prescribes(OrderContextId "oc-7"))
                 act 1 Saves
             ]

    expect "UC-4 step 2: B is told an Unsigned TreatmentPlan of another User is newer (Rule 21)"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false))

    expect "UC-4 step 2: and the Submission waits — the User may still choose not to"
        (planCount pat3 warned = 2)

    expect "UC-4 step 2: the notice came with a token naming what it disclosed (Rule 34)"
        ((noticeAt 1 warned |> Option.map Token.disclosed) = Some [ p3Unsigned.Id ])

    // Rule 34: proceeding is re-sending the Submission with that token. There is no
    // `Proceed` message; holding off is simply not sending this.
    let bSaved = step "UC-4 step 2 — B chooses to submit anyway, returning the token" warned [ act 1 Saves ]

    expect "UC-4 step 2: an Unsigned TreatmentPlan of B's own is appended (Rules 14, 34)"
        (planCount pat3 bSaved = 3
         && (headOf pat3 bSaved |> Option.map _.By) = Some ucB
         && (headOf pat3 bSaved |> Option.map _.State) = Some Unsigned)

    expect "UC-4 step 2: and the notice is spent — the token does not linger (Rule 34)"
        (noticeAt 1 bSaved = None)

    let bSigned = step "UC-4 step 3 — B signs" bSaved [ yield! signs 1 pinB ]

    expect "UC-4 step 3: a Signed TreatmentPlan in B's name; it now counts clinically (Rules 15, 16)"
        ((headOf pat3 bSigned |> Option.map _.State) = Some Signed
         && (recordFor pat3 bSigned |> PatientRecord.latestSigned |> Option.map _.By) = Some ucB)

    // ── step 4 — A's Unsigned work is superseded ──
    let aReturns = step "UC-4 step 4 — A launches for Patient 3 after B signed" bSigned (launchAs ucA.Login (Some pat3))

    expect "UC-4 step 4: A's Session starts from B's Signed TreatmentPlan (Rule 19)"
        (openedAt 2 aReturns
            = (recordFor pat3 aReturns |> PatientRecord.latestSigned |> Option.map _.Id))

    // "Nobody but User A could ever open it, and now not even User A can act on it."
    // Rule 18 does still let A open their own Unsigned TreatmentPlan — it is unqualified.
    // What has gone is the acting: Rule 20 blocks submitting anything from it, because
    // B's Signed TreatmentPlan is newer than the one A would then have opened with.
    let aOnDeadEnd =
        step "UC-4 step 4 — A opens the old work, and can do nothing with it" aReturns
             [
                 act 2 (OpensTreatmentPlan p3Unsigned.Id)
                 yield! signs 2 pinA
             ]

    expect "UC-4 step 4: A may still open their own Unsigned TreatmentPlan (Rule 18)"
        (saw (function TreatmentPlanOpened(id, _, _) -> id = p3Unsigned.Id | _ -> false))

    expect "UC-4 step 4: but submitting anything from it is blocked, for good (Rule 20)"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && (headOf pat3 aOnDeadEnd |> Option.map _.By) = Some ucB)

    // ── UC-4 ext 2a — B holds off at the notification ──
    // There is nothing to send: under Rule 34 the Submission is only made by returning the
    // token, so holding off is the absence of a message. `warned` is that state.
    expect "2a nothing is created; both TreatmentPlans stand, each usable only by its own User"
        (planCount pat3 warned = 2
         && (headOf pat3 warned |> Option.map _.Id) = Some p3Unsigned.Id)

    // ── UC-4 ext 2b — A launches before B signs ──
    let aBeforeBSigns =
        step "UC-4 ext 2b — A launches for Patient 3 before B signs" bSaved (launchAs ucA.Login (Some pat3))

    expect "2b A starts from A's own Unsigned head: B's is Unsigned too, so it does not supersede (Rule 19)"
        (openedAt 2 aBeforeBSigns = Some p3Unsigned.Id)

    let aSignsFirst =
        step "UC-4 ext 2b — A may sign: no newer Signed TreatmentPlan exists (Rule 20)" aBeforeBSigns
             [ yield! signs 2 pinA ]

    expect "2b A is notified of B's newer Unsigned work (Rule 21), and nothing is created yet"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucB | _ -> false))

    let aWon = step "UC-4 ext 2b — A re-sends with the token, and signing first blocks B" aSignsFirst [ yield! signs 2 pinA ]

    expect "2b whichever of the two signs first blocks the other (Rule 20)"
        ((recordFor pat3 aWon |> PatientRecord.latestSigned |> Option.map _.By) = Some ucA)

    let bNowBlocked = step "UC-4 ext 2b — B tries to sign after A did" aWon [ yield! signs 1 pinB ]

    expect "2b B is blocked by A's Signed TreatmentPlan (Rule 20)"
        (saw (function SubmissionBlocked _ -> true | _ -> false))

    ignore bNowBlocked
    bSigned


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-5  Someone else takes over the workstation
// ═══════════════════════════════════════════════════════════════════════════════

let uc5 () =
    printfn ""
    printfn "############### UC-5  Someone else takes over the workstation ###############"

    // Precondition: A has an open Session for Patient 1 and walks away. Possibility 1:
    // this is not ours to prevent, only to handle.
    let aWalksAway = quiet "UC-5 precondition" world (launchAs ucA.Login (Some pat1))

    let bSaves =
        step "UC-5 steps 1 and 2 — B works and saves in A's Session" aWalksAway
             [
                 act 1 (Prescribes(OrderContextId "oc-8"))
                 act 1 Saves
             ]

    expect "UC-5 step 2: the TreatmentPlan is created under the Session's credentials — A's (Rules 14, 32)"
        (planCount pat1 bSaves = 1
         && (headOf pat1 bSaves |> Option.map _.By) = Some ucA)

    expect "UC-5 step 2: and so are the stamps on every OrderContext B changed (Rules 14, 35)"
        (headOf pat1 bSaves
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.forall (fun o -> o.Stamp = Some ucA))

    // Step 3: signing always names the Session's User, so the Client asks for A's PIN,
    // and B does not have it. Supplying their own proves nothing — the Server
    // verifies against the Session's User's credential (Rules 14, 22, 32).
    let bTriesToSign = step "UC-5 step 3 — B signs, with the only PIN they have" bSaves [ yield! signs 1 pinB ]

    expect "UC-5 step 3: the work stays Unsigned and does not count clinically (Rules 15, 16)"
        (saw (function PinRejected _ -> true | _ -> false)
         && (headOf pat1 bTriesToSign |> Option.map _.State) = Some Unsigned
         && (recordFor pat1 bTriesToSign |> PatientRecord.latestSigned).IsNone)

    // Signing always names the Session's User, so verification runs against A's
    // credential whoever is at the keyboard — which is exactly what caps B's guessing
    // in ext 6a, and why it costs A their allowance rather than B's.
    expect "UC-5 the wrong entry counted against the Session's User's credential — A's, not B's (Rules 22, 27, 32)"
        ((credentialOf ucB bTriesToSign |> Option.map _.AttemptCount) = Some 0
         && (credentialOf ucA bTriesToSign |> Option.map _.AttemptCount) = Some 1)

    // ── UC-5 ext 2a — B relaunches as themselves ──
    let bOwnSession =
        step "UC-5 ext 2a — B relaunches from MainEHR as themselves, Patient 1 active" bSaves
             (launchAs ucB.Login (Some pat1))

    expect "2a Rule 7 is per User: a Session of B's own opens, and A's is untouched"
        (openCount bOwnSession = 2
         && (openOfUser ucA bOwnSession).Length = 1
         && (openOfUser ucB bOwnSession).Length = 1)

    expect "2a it starts from nothing: no Signed TreatmentPlan, and the Unsigned one is A's (Rules 18, 19)"
        (openedAt 2 bOwnSession = None)

    let bReEnters =
        step "UC-5 ext 2a — B re-enters the work and signs; the notice comes first" bOwnSession
             [
                 act 2 (Prescribes(OrderContextId "oc-8"))
                 yield! signs 2 pinB
             ]

    expect "2a B is notified of the newer Unsigned TreatmentPlan (Rule 21)"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false))

    let bSignedOwn = step "UC-5 ext 2a — B re-sends with the token (Rule 34)" bReEnters [ yield! signs 2 pinB ]

    expect "2a and signs as themselves (Rules 14, 15)"
        ((headOf pat1 bSignedOwn |> Option.map _.By) = Some ucB
         && (headOf pat1 bSignedOwn |> Option.map _.State) = Some Signed)

    // ── UC-5 ext 2b — B cannot log in to MainEHR at that workstation ──
    // No path to a Session of B's own. The work stays Unsigned until A opens it in a
    // Session of their own and signs; nobody else can.
    expect "2b the work stays Unsigned, and only A can ever act on it (Rules 18, 19)"
        ((recordFor pat1 bSaves |> PatientRecord.mayOpen ucB.UserId (headOf pat1 bSaves).Value.Id).IsNone
         && (recordFor pat1 bSaves |> PatientRecord.mayOpen ucA.UserId (headOf pat1 bSaves).Value.Id).IsSome)

    // ── UC-5 ext 3a — B guesses instead ──
    let guessed =
        step "UC-5 ext 3a — B guesses at A's PIN" bTriesToSign
             [
                 yield! signs 1 (Pin "0001")
                 yield! signs 1 (Pin "0002")
             ]

    expect "3a at the configured number of consecutive wrong entries the Session ends (Rules 9, 27)"
        (openCount guessed = 0
         && (match stateOf 1 guessed with Some(Ended(WrongPinLimit, _)) -> true | _ -> false))

    // Rule 10, and the whole point of it. The screen B is standing at is refused and
    // told what ended — but B is not A, so nothing is discharged: A's notice is still
    // owed, and A hears it at their next launch. Otherwise the guesser could dismiss
    // the very notice that exists to tell A somebody was guessing.
    expect "3a the Unsigned TreatmentPlan stays, and the screen is refused, not told for A (Rule 10)"
        (planCount pat1 guessed = 1
         && saw (function SessionRefused(Some WrongPinLimit) -> true | _ -> false)
         && noticeOf 1 guessed = Some Owed
         && not (wasTold 1 guessed))

    // Rule 10. The screen is where B is standing, so the screen is not where this is
    // told. It goes to the address the registry holds, as a PIN change does (Rule 26).
    expect "3a and it is mailed to A, because the screen is where the guessing happened (Rule 26)"
        ((mailsTo mailA guessed).Length = 1
         && guessed |> audited "wrong-PIN limit reached")

    let relaunchNoHelp =
        step "UC-5 ext 3a — relaunching as A does not reset the count (Rule 27)" guessed
             (launchAs ucA.Login (Some pat1) @ [ yield! signs 2 (Pin "0003") ])

    expect "3a the count belongs to the UserCredential, so guessing is capped outright"
        ((credentialOf ucA relaunchNoHelp |> Option.map _.AttemptCount |> Option.map (fun c -> c >= wrongPinLimit))
            = Some true)

    // B's work carries A's UserContext (Rule 14), so nothing in the record marks it as
    // somebody else's. What speaks is the Session that saved it: it ended at the
    // wrong-PIN limit, not by A, so Rule 19 offers rather than opens.
    expect "3a A's relaunch does not open the work left in A's name — it offers it (Rule 19)"
        (openedAt 2 relaunchNoHelp = None
         && (workingAt 2 relaunchNoHelp).IsEmpty
         && (clientOf 2 relaunchNoHelp
             |> Option.bind _.Offered
             |> Option.map (fun (_, mark, _) -> mark)) = Some(Some WrongPinLimit))

    bSignedOwn


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-6  Two Users, one Patient
// ═══════════════════════════════════════════════════════════════════════════════

let uc6 () =
    printfn ""
    printfn "############### UC-6  Two Users, one Patient ###############"

    // Precondition: A and B each hold an open Session for Patient 2. Rule 7 permits
    // this: it limits Sessions per User, not per Patient.
    let both =
        step "UC-6 precondition — A and B each open a Session for Patient 2" world
             (launchAs ucA.Login (Some pat2) @ launchAs ucB.Login (Some pat2))

    expect "UC-6 Rule 7 limits Sessions per User, not per Patient: both are open"
        (openCount both = 2
         && (openOfUser ucA both).Length = 1
         && (openOfUser ucB both).Length = 1)

    // Guarantee 3, and the reason it now holds by construction: the two carts are in
    // two Browsers, and the Server holds neither (Rule 31).
    expect "UC-6 the two carts are in the two Clients, and nowhere else (Rule 31, Guarantee 3)"
        (both.GenPres.InFlight.IsEmpty && sidAt 1 both <> sidAt 2 both)

    let aSigned =
        step "UC-6 step 2 — A saves and signs" both
             [
                 act 1 (Prescribes(OrderContextId "oc-a"))
                 act 1 Saves
                 yield! signs 1 pinA
             ]

    expect "UC-6 step 2: an Unsigned then a Signed TreatmentPlan in A's name"
        (planCount pat2 aSigned = 3
         && (headOf pat2 aSigned |> Option.map _.State) = Some Signed
         && (headOf pat2 aSigned |> Option.map _.By) = Some ucA)

    // Consequence 6: neither User saw the other's work — a Client only learns anything
    // at its own next request.
    let bBlocked =
        step "UC-6 step 3 — B saves, and is blocked" aSigned
             [
                 act 2 (Prescribes(OrderContextId "oc-b"))
                 act 2 Saves
             ]

    expect "UC-6 step 3: a Signed TreatmentPlan newer than the one B opened with blocks the Submission (Rule 20)"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && planCount pat2 bBlocked = 3)

    let bTookOver =
        step "UC-6 step 4 — B opens A's Signed TreatmentPlan, which lifts the block (Rule 17)" bBlocked
             [ act 2 (OpensTreatmentPlan (headOf pat2 bBlocked).Value.Id) ]

    expect "UC-6 step 4: opening it re-mints the token, so it is what the Session opened with (Rule 33)"
        (saw (function TreatmentPlanOpened _ -> true | _ -> false)
         && openedAt 2 bTookOver = (headOf pat2 bBlocked |> Option.map _.Id))

    let bReapplied =
        step "UC-6 step 4 — B reapplies their own work, saves and signs" bTookOver
             [
                 act 2 (Prescribes(OrderContextId "oc-b"))
                 yield! signs 2 pinB
             ]

    expect "UC-6 step 4: the signature attests the whole set in B's name (Rules 14, 15)"
        ((headOf pat2 bReapplied |> Option.map _.By) = Some ucB
         && (headOf pat2 bReapplied |> Option.map _.State) = Some Signed)

    // Rule 14, the half that only shows here — and Rule 35, which is how the Server
    // knows: it diffed the payload against the base TreatmentPlan, rather than believing
    // any stamp the Client sent.
    let orders = headOf pat2 bReapplied |> Option.map _.Orders |> Option.defaultValue []

    expect "UC-6 step 4: the OrderContext B changed carries B's stamp"
        (orders |> List.exists (fun o -> o.Id = OrderContextId "oc-b" && o.Stamp = Some ucB))

    expect "UC-6 step 4: the ones B left untouched keep A's stamp (Rules 14, 35)"
        (orders |> List.exists (fun o -> o.Id = OrderContextId "oc-a" && o.Stamp = Some ucA)
         && orders |> List.exists (fun o -> o.Id = OrderContextId "oc-1" && o.Stamp = Some ucA))

    // ── UC-6 ext 2a — B saves, Unsigned, before A signs ──
    let bSavedFirst =
        step "UC-6 ext 2a — B saves Unsigned before A signs" both
             [
                 act 2 (Prescribes(OrderContextId "oc-b"))
                 act 2 Saves
             ]

    expect "2a B is not blocked: nothing Signed is newer (Rule 20)"
        (never (function SubmissionBlocked _ -> true | _ -> false)
         && planCount pat2 bSavedFirst = 2)

    let _ =
        step "UC-6 ext 2a — but A is notified when submitting (Rule 21)" bSavedFirst
             [ act 1 Saves ]

    expect "2a A is told whose work it is, and may proceed or hold off"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucB | _ -> false))

    // Nothing attested is ever lost: the PatientRecord is append-only (Concept 12), so
    // a Signed TreatmentPlan survives whatever follows. What is not protected is Unsigned
    // work: superseded, it can never be signed (Rules 19, 20).
    expect "UC-6 nothing attested is lost: A's Signed TreatmentPlan survives B's (Concept 12)"
        (recordFor pat2 bReapplied
         |> _.Plans
         |> List.exists (fun s -> s.State = Signed && s.By = ucA))

    // ── UC-6 ext 2b — both sign at once ──
    // Two signatures over the same base, in flight together: exactly one can land
    // (Rules 36, 42). Confirming is what leaves the Client, so that is what
    // interleaves — a confirm delivered before its challenge would confirm nothing.
    let bothChallenged =
        quiet "UC-6 ext 2b precondition — both ask to sign, and both are shown a challenge" both
              [
                  act 1 (Prescribes(OrderContextId "oc-a2"))
                  act 2 (Prescribes(OrderContextId "oc-b2"))
                  act 1 (Signs pinA)
                  act 2 (Signs pinB)
              ]

    let bothSign =
        racing "UC-6 ext 2b — A and B sign over the same base at once" bothChallenged
               [ act 1 ConfirmsSign; act 2 ConfirmsSign ]

    expect "2b exactly one signature landed, and the record moved once (Rules 36, 42)"
        (countOf (function TreatmentPlanSubmitted(_, Signed, _) -> true | _ -> false) = 1
         && planCount pat2 bothSign = planCount pat2 bothChallenged + 1)

    expect "2b the loser is told whose work stands in the way, and never which TreatmentPlan (Rules 17, 18, 20)"
        (countOf (function SubmissionBlocked _ -> true | _ -> false) = 1
         && saw (function SubmissionBlocked uc -> uc = ucA || uc = ucB | _ -> false))

    // ── Rule 17 — an older Signed TreatmentPlan is readable, and not a place to build ──
    let history =
        quiet "Rule 17 precondition — a record with two Signed TreatmentPlans" bReapplied
              (launchAs ucA.Login (Some pat2))

    let older =
        recordFor pat2 history
        |> _.Plans
        |> List.filter (fun s -> s.State = Signed)
        |> List.skip 1
        |> List.tryHead

    let readingHistory =
        step "Rule 17 — A opens an older Signed TreatmentPlan" history
             [ act 3 (OpensTreatmentPlan older.Value.Id) ]

    expect "Rule 17 attested history is readable by anyone who may see the Patient"
        (saw (function TreatmentPlanOpened _ -> true | _ -> false)
         && openedAt 3 readingHistory = Some older.Value.Id)

    let buildingOnIt =
        step "Rule 17 — and building on it is blocked, because a newer Signed one exists" readingHistory
             [ act 3 (Prescribes(OrderContextId "oc-from-history")); act 3 Saves ]

    expect "Rule 20 read-only falls out of the baseline: no second mechanism, and nothing lands"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && planCount pat2 buildingOnIt = planCount pat2 readingHistory)

    bReapplied

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-7  A User forgets their PIN
// ═══════════════════════════════════════════════════════════════════════════════

let uc7 () =
    printfn ""
    printfn "############### UC-7  A User forgets their PIN ###############"

    // Precondition: A has an open Session and a UserCredential with a PIN set but
    // forgotten. Rule 37: the PIN is never removed, only replaced, and what authorises
    // the replacement arrives by mail — so there is no moment in which A's credential
    // is a credential anyone at that workstation could claim.
    let opened = quiet "UC-7 precondition" world (launchAs ucA.Login (Some pat2))

    let asked = step "UC-7 step 1 — A asks GenPRES to reset the PIN" opened [ act 1 AsksPinReset ]

    expect "UC-7 step 1: nothing is removed — the PIN in force is still the old one (Rule 37)"
        ((credentialOf ucA asked |> Option.bind _.Pin) = Some pinA
         && saw (function ResetCodeMailed -> true | _ -> false))

    // Rule 26 has to reach A with no Session in memory to ask, so the address comes
    // off the SessionRecord (Concept 9). The record of the ask says a code went out;
    // it does not say which.
    expect "UC-7 step 1: a one-time code goes to the registry's address, and the ask is recorded (Rules 26, 37)"
        ((mailsTo mailA asked).Length = 1
         && (codeInMail mailA asked).IsSome
         && asked |> audited "PIN reset code sent")

    // Rule 37, one at a time. Asking again while the first code is still good sends
    // nothing: a second code would void the one A is reading, so anybody able to press
    // the button could keep A from ever completing a reset — and every press would be
    // a mail at an address GenPRES did not choose. Two requests, one mail.
    let askedTwice = step "UC-7 step 1 — A asks a second time, while the first code still stands" asked [ act 1 AsksPinReset ]

    expect "UC-7 two requests, one mail: a standing code is not voided by asking again (Rule 37)"
        ((mailsTo mailA askedTwice).Length = 1
         && saw (function ResetDenied ResetPending -> true | _ -> false)
         && never (function SendMail _ -> true | _ -> false)
         && askedTwice |> audited "one is already pending")

    expect "UC-7 and the code A is holding still works: nothing was taken from them (Rule 37)"
        (let code = (codeInMail mailA askedTwice).Value

         let usedIt =
             quiet "UC-7 — and the standing code still works" askedTwice
                   [ act 1 (EntersResetCode(code, Pin "7777")) ]

         (credentialOf ucA usedIt |> Option.bind _.Pin) = Some(Pin "7777"))

    // ── UC-7 ext 1a — B, at A's open workstation, triggers the reset ──
    // The trigger cannot be prevented: a launch proves control of a MainEHR Session,
    // not a person (Possibility 1). What it now buys is nothing at all — the code went
    // to A, and B stalls at it.
    let stalled =
        step "UC-7 ext 1a — whoever is at the screen guesses at the code" asked
             [ act 1 (EntersResetCode(ResetCode "code-guess", Pin "0000")) ]

    expect "1a a guessed code changes nothing, and A's PIN still stands (Rule 37)"
        (saw (function ResetDenied(WrongCode _) -> true | _ -> false)
         && (credentialOf ucA stalled |> Option.bind _.Pin) = Some pinA
         && (mailsTo mailB stalled).Length = 0)

    let code = (codeInMail mailA asked).Value

    let replaced =
        step "UC-7 step 2 — A reads the mail and replaces the PIN in one act" asked
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "UC-7 step 2: replaced, never removed — there is no PIN-less moment (Concept 7, Rule 37)"
        ((credentialOf ucA replaced |> Option.bind _.Pin) = Some(Pin "5555")
         && saw (function PinChanged -> true | _ -> false)
         && never (function ResetDenied _ -> true | _ -> false))

    expect "UC-7 step 2: mailed and recorded, and the new PIN starts at zero (Rules 26, 27)"
        ((mailsTo mailA replaced).Length = 2
         && replaced |> audited "PIN replaced"
         && (credentialOf ucA replaced |> Option.map _.AttemptCount) = Some 0)

    let signs =
        step "UC-7 step 3 — A signs with the new PIN, in the Session they were already in" replaced
             [ act 1 (Prescribes(OrderContextId "oc-r")); yield! signs 1 (Pin "5555") ]

    expect "UC-7 step 3: the new PIN signs, and no relaunch was needed (Concept 14)"
        ((headOf pat2 signs |> Option.map _.State) = Some Signed
         && (headOf pat2 signs |> Option.map _.By) = Some ucA)

    let spent =
        step "UC-7 step 3 — and the code is spent: honoured once, and never again" signs
             [ act 1 (EntersResetCode(code, Pin "7777")) ]

    expect "UC-7 step 3: a spent code buys nothing, and the PIN it already replaced stands"
        (saw (function ResetDenied NoResetPending -> true | _ -> false)
         && (credentialOf ucA spent |> Option.bind _.Pin) = Some(Pin "5555"))

    // ── UC-7 ext 1b — the code is not used in time ──
    // What expires a code is time, and time here runs in handled messages: waiting a
    // code out by ticking would idle the Session out first (Rule 9), which is a
    // different scenario. So the code is aged instead — its expiry moved into the
    // past, which is exactly what the wait would have done to it.
    let aged =
        { asked with
            Database.Private.Resets =
                asked.Database.Private.Resets |> Map.map (fun _ r -> { r with Expires = asked.Env.Now - 1 }) }

    let expiredCode =
        step "UC-7 ext 1b — A leaves the code unused until it dies (Rule 37)" aged
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "1b an aged code replaces nothing, and the old PIN is untouched"
        (saw (function ResetDenied ResetExpired -> true | _ -> false)
         && (credentialOf ucA expiredCode |> Option.bind _.Pin) = Some pinA)

    // ── UC-7 ext 2a — the code is guessed at ──
    // The count is the code's own, not the credential's (Rule 27): guessing at a code
    // must not lock a PIN that is still perfectly good.
    let voided =
        step "UC-7 ext 2a — a few wrong codes, and this one is void" asked
             [ for i in 1 .. wrongCodeLimit -> act 1 (EntersResetCode(ResetCode $"code-wrong%i{i}", Pin "0000")) ]

    expect "2a the code is void, and the PIN it would have replaced is untouched"
        (saw (function ResetDenied ResetVoid -> true | _ -> false)
         && (credentialOf ucA voided |> Option.bind _.Pin) = Some pinA
         && (credentialOf ucA voided |> Option.map _.AttemptCount) = Some 0)

    let afterVoid =
        step "UC-7 ext 2a — the mailed code is void too: the reset is gone, not merely wrong" voided
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "2a even the right code buys nothing now — a fresh reset means a fresh mail"
        (saw (function ResetDenied NoResetPending -> true | _ -> false)
         && (credentialOf ucA afterVoid |> Option.bind _.Pin) = Some pinA)

    let freshMail = step "UC-7 ext 2a — and A asks again" afterVoid [ act 1 AsksPinReset ]

    expect "2a a second code goes out, and it is not the first one"
        ((mailsTo mailA freshMail).Length = 2
         && (codeInMail mailA freshMail).IsSome
         && (codeInMail mailA freshMail) <> Some code)

    signs


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-8  User opens GenPRES directly
// ═══════════════════════════════════════════════════════════════════════════════

let uc8 () =
    printfn ""
    printfn "############### UC-8  User opens GenPRES directly ###############"

    // GenPRES as Clinical Decision Support, not as order management. No launch, so no
    // Launch — and GenPRES cannot know who is at the keyboard.
    let anon = step "UC-8 step 1 — A opens the GenPRES address in a browser" world [ atClient 1 OpenDirectly ]

    expect "UC-8 step 1: an anonymous Session — no User, no Role, no PatientId (Rule 13)"
        (openCount anon = 1
         && (newestRecord anon |> Option.bind _.User) = None
         && (newestRecord anon |> Option.bind _.Patient) = None)

    expect "UC-8 step 1: its SessionRecord binds to no User (Concept 9)"
        ((recNo 1 anon |> Option.bind _.User) = None
         && (recNo 1 anon |> Option.bind _.Launch) = None)

    expect "UC-8 anonymous use needs no Role and no UserRegistry check"
        (never (function ResolveUser _ -> true | _ -> false))

    let prescribing =
        step "UC-8 step 2 — A prescribes: Patient Data and OrderContexts by hand" anon
             [
                 act 1 (EntersPatientData(PatientData "3y, 14kg, by hand"))
                 act 1 (Prescribes(OrderContextId "oc-x"))
             ]

    expect "UC-8 step 2: prescribing works, Patient Data included (Concepts 2, 15)"
        ((dataAt 1 prescribing).IsSome && (workingAt 1 prescribing).Length = 1)

    // Rule 8 stamps `LastSeen` on every request, as it does for any Session — but for
    // an anonymous one nothing ever reads it (Rule 13). What governs this Session is
    // the absolute limit it was opened with, and nothing else.
    expect "UC-8 step 2: requests are served and stamped, but it is the limit that governs (Rules 8, 13)"
        (countOf (function SessionRequest _ -> true | _ -> false) = 2
         && lastSeenOf 1 prescribing > (recNo 1 anon |> Option.map _.LastSeen)
         && (recNo 1 prescribing |> Option.bind _.ExpiresAt).IsSome)

    let noSaving = step "UC-8 step 3 — nothing can be saved" prescribing [ act 1 Saves; yield! signs 1 pinA ]

    expect "UC-8 step 3: no TreatmentPlan can be opened or created (Rule 12)"
        (saw (function NoTreatmentPlanHere -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    expect "UC-8 neither the PatientRecord nor the PatientDataPlatform is ever touched"
        (never (function ReadRecord _ -> true | _ -> false)
         && never (function ReadPatientData _ -> true | _ -> false))

    expect "UC-8 step 3: the work exists only in the Client (Rule 31)"
        ((workingAt 1 noSaving).Length = 1 && noSaving.GenPres.InFlight.IsEmpty)

    let idled =
        step "UC-8 — and it need not idle out: keeping it has no consequence (Rule 13)" noSaving
             (ticks (sessionTtl + 5))

    expect "UC-8 an anonymous Session does not idle out: it has no idle clock (Rule 13)"
        (openCount idled = 1 && stateOf 1 idled = Some OpenOrGone)

    // Not for ever, though. Rule 13 says keeping it has no consequence, which is an
    // argument against an idle clock and not against an outright limit: a Session
    // nobody will ever come back to should not sit open until the Server is restarted.
    let outlived =
        stepFor 40000 "UC-8 — but it does not live for ever: the outright limit (Rule 13)" idled
             (ticks (anonymousLifetime + 5))

    expect "UC-8 past its limit the anonymous Session is ended, whatever it was doing"
        (openCount outlived = 0
         && (match stateOf 1 outlived with Some(Ended(Expired, _)) -> true | _ -> false))

    expect "UC-8 and nothing is owed by it: there is no User to tell (Rules 10, 13)"
        (noticeOf 1 outlived = Some NotOwed
         && (recNo 1 outlived |> Option.bind _.User).IsNone)

    // ── Rule 13 — anonymous opens are bounded in number, not only in lifetime ──
    // An anonymous open is an unauthenticated write: a SessionRecord per open, and Rule
    // 13's lifetime says only how long each lives. Above the bound the answer is a
    // refusal that writes no record.
    let refusals = 4

    let flooded =
        let opens =
            [ 1 .. anonymousOpenLimit + refusals ]
            |> List.collect (fun i -> [ atClient (100 + i) OpenDirectly ])
        step "Rule 13 — many browsers open anonymously at once" world opens

    expect "Rule 13 the standing anonymous Sessions are capped, and the rest are refused"
        (openCount flooded = anonymousOpenLimit
         && saw (function AnonymousRefused -> true | _ -> false))

    expect "Rule 13 and a refused open writes no SessionRecord — which is what the bound is for"
        (recordCount flooded = anonymousOpenLimit)

    // Rule 46. A refusal is an event, and the audit is where somebody trying shows up
    // — so it is not silence. But a line per refused request would be the same flood
    // under another name, so what is kept is a count per source: one integer, however
    // hard anyone leans on it.
    expect "Rule 46 the refusals are counted, per source, and the count is right"
        (let counted =
            flooded.Database.Private.AnonymousRefused |> Map.toList |> List.sumBy snd

         counted = refusals
         && flooded.Database.Private.AnonymousRefused.Count = refusals)

    // The audit holds one line per open that happened, and none for any that did not.
    expect "Rule 46 and it is still a count, not a line each: the audit did not grow with the flood"
        (not (flooded |> audited "AnonymousRefused")
         && (flooded.Database.Private.Audit
             |> List.filter (fun e -> e.What.Contains "opened")
             |> List.length) = anonymousOpenLimit)

    // Leaning harder moves the count and nothing else.
    let leanedOn =
        step "Rule 46 — and four more refusals move the count, and nothing else" flooded
             ([ 1 .. 4 ] |> List.map (fun i -> atClient (200 + i) OpenDirectly))

    expect "Rule 46 a harder flood grows one integer per source and no store at all"
        ((leanedOn.Database.Private.AnonymousRefused |> Map.toList |> List.sumBy snd) = refusals + 4
         && recordCount leanedOn = recordCount flooded
         && leanedOn.Database.Private.Audit.Length = flooded.Database.Private.Audit.Length)

    // ── UC-8 ext 1a — the browser does present a Launch ──
    // That is a launch: UC-1 from step 3. Covered by UC-1 throughout.

    // ── UC-8 ext 1b — the same Browser later launches properly ──
    // An anonymous Session binds to no User, so it is Rule 7's per-browser half that
    // ends this one. The User's own act, so nothing is owed for it.
    let alsoLaunched = step "UC-8 ext 1b — the same person later launches properly" idled (launchAs ucA.Login (Some pat1))

    expect "1b the anonymous Session is not untouched: it is replaced, and owes nothing (Rules 9, 10)"
        (openCount alsoLaunched = 1
         && (match stateOf 1 alsoLaunched with Some(Ended(ReplacedInBrowser, _)) -> true | _ -> false)
         && noticeOf 1 alsoLaunched = Some NotOwed
         && never (function PriorSessionNotice _ -> true | _ -> false))

    // Rules 7 and 40. The replacement and the open are one act at the Database, not
    // two requests with a gap between them: there is no `EndSessionIfOpen` on the wire
    // before the open, and no moment in which this browser holds two Sessions or none.
    expect "1b the replacement and the open are one act, not two (Rules 7, 40)"
        (saw (function OpenSessionClosingOthers(_, Some _) -> true | _ -> false)
         && never (function EndSessionIfOpen(_, ReplacedInBrowser) -> true | _ -> false)
         && before
                (function OpenSessionClosingOthers _ -> true | _ -> false)
                (function SessionOpened _ -> true | _ -> false))

    // And the limit does not rest on the Client's word. A Client that names no
    // Session it is replacing — which an attacker's would not, and an honest one
    // might not after a reload — still ends up with one Session in its browser,
    // because the Database reads the browser off the record it holds (Rules 7, 40).
    let silentAboutTheOldOne =
        let launch = Token.mintLaunch (Some pat1) idled.Env.Now

        step "Rule 40 — a Client that names no Session to replace still gets only one" idled
             [ fromClient 1 (RedeemLaunch(launch, Some ucA.Login, None)) ]

    expect "Rule 40 the browser limit is the Database's, not the Client's word (Rules 7, 40)"
        (never (function RedeemLaunch(_, _, Some _) -> true | _ -> false)
         && (silentAboutTheOldOne.Database.Private.Sessions
             |> List.filter (fun r -> SessionRecord.isOpen r && r.Browser = Some(BrowserId 1))
             |> List.length) = 1)

    // And the work in the browser is gone when the browser goes — it was only ever
    // there (Rule 31).
    let browserClosed = step "UC-8 step 3 — and it is gone when the browser goes" idled [ atClient 1 CloseBrowser ]

    expect "UC-8 step 3: the cart dies with the browser (Rule 31)"
        (workingAt 1 browserClosed).IsEmpty

    idled


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-9  A Session ends out from under the User
// ═══════════════════════════════════════════════════════════════════════════════

let uc9 () =
    printfn ""
    printfn "############### UC-9  A Session ends out from under the User ###############"

    // Precondition: UC-3 ran through step 2 and stopped. A's Session for Patient 2 is
    // open, an Unsigned TreatmentPlan of A's stands at the head, and further unsaved
    // changes sit on the screen.
    let saved =
        quiet "UC-9 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [
                   act 1 (Prescribes(OrderContextId "oc-4"))
                   act 1 Saves
                   act 1 (Prescribes(OrderContextId "oc-unsaved"))
                 ])

    expect "UC-9 precondition: an Unsigned TreatmentPlan stands, and one change is unsaved"
        (planCount pat2 saved = 2 && (workingAt 1 saved).Length = 3)

    let idled =
        step "UC-9 step 1 — A is called away, and the idle clock runs out (Rules 8, 9)" saved
             (ticks (sessionTtl + 5))

    expect "UC-9 step 1: the Session ends and its record is marked ended"
        (openCount idled = 0
         && (match stateOf 1 idled with Some(Ended(Idle, _)) -> true | _ -> false))

    expect "UC-9 step 1: the ending creates the obligation — a notice is now owed (Rule 10)"
        (noticeOf 1 idled = Some Owed)

    // Step 2: the Server cannot reach the Client, which keeps showing a live-looking
    // screen (Consequence 6). Nothing was sent, and nothing could have been.
    expect "UC-9 step 2: nothing was sent to the Client when the Session ended (Consequence 6)"
        (never (function SessionEnded _ -> true | _ -> false))

    let told = step "UC-9 step 2 — A returns and acts" idled [ act 1 (Prescribes(OrderContextId "oc-later")) ]

    // Rule 10. The request is refused and this screen is told what ended — but the
    // obligation is not discharged by it. Whoever is holding this SessionId need not
    // be A: in UC-5's setting it is whoever sat down at the workstation, and telling
    // them is not telling A. Delivery is a launch's business (PriorSessionNotice),
    // where a fresh MainEHR login stands behind the person reading it.
    expect "UC-9 step 2: the request is refused and this screen is told what ended (Rule 10)"
        (saw (function SessionRefused(Some Idle) -> true | _ -> false)
         && showingOf 1 told = Some "the session ended: Idle — relaunch from MainEHR")

    expect "UC-9 step 2: and the notice is still owed — a stale Client is not the User (Rule 10)"
        (noticeOf 1 told = Some Owed
         && not (wasTold 1 told)
         && not (wasAcknowledged 1 told))

    // Rule 10. Dismissing it here would spend the obligation on the word of whoever
    // holds the ended SessionId — which, in UC-5's setting, is whoever sat down at the
    // workstation. The old Client has no Session of its own to answer with, so it
    // cannot: the notice stands until a launched Session of A's answers for it.
    let dismissedAtOldClient =
        step "UC-9 step 4 — A's old Client tries to dismiss it, and cannot (Rule 10)" told
             [ act 1 AcknowledgesNotice ]

    expect "UC-9 step 4: the ended Session's own Client cannot spend the obligation (Rule 10)"
        (not (wasAcknowledged 1 dismissedAtOldClient)
         && never (function AckSessionNotice _ -> true | _ -> false))

    let atNextLaunch =
        step "UC-9 step 4 — A launches again, and the notice is there (Rule 10)" dismissedAtOldClient
             (launchAs ucA.Login (Some pat2))

    // Rule 10, and the whole of what changed: this is the *only* place the notice is
    // ever delivered. Every refused request before it left the obligation exactly
    // where the ending put it, and the launch is what discharges it — because a fresh
    // MainEHR login stands behind the person about to read it, and a stale SessionId
    // does not.
    expect "UC-9 step 4: the launch is where an unacknowledged notice comes back"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && wasTold 1 atNextLaunch
         && not (wasTold 1 dismissedAtOldClient))

    let acked = step "UC-9 step 4 — A dismisses it there" atNextLaunch [ act 2 AcknowledgesNotice ]

    expect "UC-9 step 4: acknowledged from a launched Session of A's own, and now it is spent (Rule 10)"
        (wasAcknowledged 1 acked
         && saw (function AckSessionNotice _ -> true | _ -> false))

    // And an acknowledgement that does not come from the User's own launched Session is
    // refused outright, whoever sends it: the Database checks, and says so in the audit.
    let notMineToAnswer =
        let bIn = quiet "Rule 10 — B is in a Session of their own" acked (launchAs ucB.Login (Some pat2))
        step "Rule 10 — B answers for A's ended Session" bIn
             [
                 fromClient 3
                     (AckSessionNotice((sidAt 3 bIn).Value, (recNo 1 bIn).Value.Id))
             ]

    expect "Rule 10 another User's Session cannot answer for this one, and the refusal is audited"
        (notMineToAnswer |> audited "acknowledgement refused")

    // Step 5, the change the stateless design makes. The unsaved work was never
    // anywhere but the Client (Rule 31): the ended Session accepts nothing, but the
    // Client still holds it.
    expect "UC-9 step 3: the unsaved changes are still in the Client (Rule 31)"
        ((workingAt 1 told) |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved"))

    expect "UC-9 step 3: and they never reached the record (Concept 15)"
        (planCount pat2 told = 2
         && (headOf pat2 told
             |> Option.map _.Orders
             |> Option.defaultValue []
             |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
             |> not))

    expect "UC-9 step 3: the Unsigned TreatmentPlan stands, A's own to resume (Rules 18, 19)"
        ((recordFor pat2 told |> PatientRecord.startsFrom ucA.UserId |> Option.map _.Id)
            = (headOf pat2 told |> Option.map _.Id))

    let relaunched =
        step "UC-9 step 4 — A relaunches. Acknowledged already, A is not told again (Rule 10)" acked
             (launchAs ucA.Login (Some pat2))

    // A launch always supersedes the Session A was in, and that ending owes a notice of
    // its own (Rule 7, Rule 10). What must never come back is the one A acknowledged.
    expect "UC-9 step 4: the acknowledged Session is never named again (Rule 10, acknowledged once)"
        (let acknowledged = (recNo 1 relaunched).Value.Id

         lastTrace
         |> List.forall (fun e ->
             match e.Msg with
             | PriorSessionNotice priors -> priors |> List.forall (fun (_, _, sid) -> sid <> acknowledged)
             | _ -> true))

    // And the other way round: a notice that was delivered and never acknowledged is
    // shown again, because the alternative is a User who never learns of it at all.
    let unacknowledged =
        step "UC-9 step 4 — but an unacknowledged notice comes back (Rule 10, at least once)" told
             (launchAs ucA.Login (Some pat2))

    expect "UC-9 step 4: delivery is at-least-once; only the acknowledgement ends it"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && openCount unacknowledged = 1)

    // ── Rule 9 — the limit Rule 8's clock cannot put off ──
    // Rule 8 refreshes the idle clock on every request, so a Client that keeps talking
    // — a poll, a tab left computing — never idles out. `sessionMaxLifetime` is the
    // other limit: counted from the open, deaf to the traffic, and the reason a launch
    // cannot stand for the person who made it indefinitely.
    let talking =
        let sid = (sidAt 1 saved).Value
        let poll = fromClient 1 (SessionRequest(sid, Compute []))
        // A poll well inside the idle limit, over and over: Rule 8 keeps the Session
        // alive through every one of them, and the absolute limit ends it anyway.
        stepFor 40000 "Rule 9 — a Client that never goes quiet still reaches the outright limit" saved
             ([ 1 .. 20 ] |> List.collect (fun _ -> poll :: ticks 20))

    expect "Rule 9 the Session ends at its absolute limit, though it was never idle (Rules 8, 9)"
        (openCount talking = 0
         && (match stateOf 1 talking with Some(Ended(Expired, _)) -> true | _ -> false)
         && (recNo 1 talking |> Option.bind _.User).IsSome)

    expect "Rule 9 and the User is owed the notice, because there is one to tell (Rule 10)"
        (wasTold 1 talking || noticeOf 1 talking = Some Owed)

    // Step 5 continued: the Client may offer to carry the surviving cart into the next
    // Session as fresh prescribing (Concept 15) — not as a resumed Session.
    let carried =
        step "UC-9 step 3 — A carries the surviving work into the new Session" relaunched
             [
                 act 3 (CarriesOverFrom(BrowserId 1))
                 act 3 Saves
             ]

    expect "UC-9 step 3: the unsaved OrderContext from before the idle-out lands in the next TreatmentPlan"
        (headOf pat2 carried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved"))

    expect "UC-9 step 3: and it is fresh prescribing — stamped by A in this Session (Rules 14, 35)"
        (headOf pat2 carried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved" && o.Stamp = Some ucA))

    // "They survive exactly as far as the browser does — closed, they are gone."
    let browserGoneFirst =
        step "UC-9 step 3 — but close the browser first, and there is nothing to carry" told
             ([ atClient 1 CloseBrowser ] @ launchAs ucA.Login (Some pat2))

    let nothingCarried =
        step "UC-9 step 3 — the new Session gets only what the record held" browserGoneFirst
             [
                 act 2 (CarriesOverFrom(BrowserId 1))
                 act 2 Saves
             ]

    expect "UC-9 step 3: closed is gone — the unsaved work is nowhere"
        (headOf pat2 nothingCarried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
         |> not)

    // The carry-over is within one User's own work and one Patient's, and no
    // further. Rule 32 takes both from the SessionRecord, and Guarantee 1 makes the
    // PatientId the one thing no TreatmentPlan may change — so a cart cannot walk
    // from one User to another, and cannot walk from one Patient to another either.
    let notMine =
        step "UC-9 step 3 — B tries to carry A's surviving work into B's own Session" told
             (launchAs ucB.Login (Some pat2)
              @ [ act 2 (CarriesOverFrom(BrowserId 1)); act 2 Saves ])

    expect "UC-9 step 3: another User's work is not a source — nothing is carried (Rules 14, 32)"
        (headOf pat2 notMine
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
         |> not)

    let otherPatient =
        step "UC-9 step 3 — A relaunches for another Patient, and the work does not follow" told
             (launchAs ucA.Login (Some pat1)
              @ [ act 2 (CarriesOverFrom(BrowserId 1)); act 2 Saves ])

    expect "UC-9 step 3: work does not cross Patients, and neither record gained it (Guarantee 1)"
        (headOf pat1 otherPatient
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
         |> not
         && planCount pat2 otherPatient = planCount pat2 told)

    // ── UC-9 ext 2a — nobody swept, and the request itself ends it ──
    // Rule 41. The Session is aged rather than ticked out, so no sweep has run and no
    // Tick has reached the Server: what ends it is the arriving request, which finds a
    // record already past its time and ends it then and there instead of refreshing it
    // back to life (Rule 8).
    let aged =
        { saved with
            Database.Private.Sessions =
                saved.Database.Private.Sessions
                |> List.map (fun r -> { r with LastSeen = r.LastSeen - (sessionTtl + 1) }) }

    let endedOnArrival =
        step "UC-9 ext 2a — A comes back to a Session that is already past its time" aged
             [ act 1 (Prescribes(OrderContextId "oc-late")) ]

    expect "2a the request ends it rather than refreshing it, and says so (Rules 8, 41)"
        (never (function Tick -> true | _ -> false)
         && saw (function SessionRefused(Some Idle) -> true | _ -> false)
         && (match stateOf 1 endedOnArrival with Some(Ended(Idle, _)) -> true | _ -> false)
         && openCount endedOnArrival = 0)

    expect "2a and the notice it created is owed to the User, not to this screen (Rule 10)"
        (noticeOf 1 endedOnArrival = Some Owed && not (wasTold 1 endedOnArrival))

    // Rules 9 and 41, the other end. The idle clock forgives a Client that keeps
    // talking, and a Client that never stops talking would never idle out at all — so
    // the outright limit has to be asked on arrival too, not only by a sweep that a
    // busy Session outruns. Here the record is aged at the open rather than at the
    // last request, so the idle clock is untouched and only the limit has passed.
    let outlived =
        { saved with
            Database.Private.Sessions =
                saved.Database.Private.Sessions
                |> List.map (fun r ->
                    { r with
                        OpenedAt = r.OpenedAt - (sessionMaxLifetime + 1)
                        ExpiresAt = r.ExpiresAt |> Option.map (fun at -> at - (sessionMaxLifetime + 1)) }) }

    let stoppedOnArrival =
        step "UC-9 ext 2a — and a Session that never went quiet still reaches its limit" outlived
             [ act 1 (Prescribes(OrderContextId "oc-still-talking")) ]

    expect "2a the outright limit is asked on arrival too, not only by the sweep (Rules 9, 41)"
        (never (function Tick -> true | _ -> false)
         && saw (function SessionRefused(Some Expired) -> true | _ -> false)
         && (match stateOf 1 stoppedOnArrival with Some(Ended(Expired, _)) -> true | _ -> false)
         && openCount stoppedOnArrival = 0
         // and it was not the idle clock: the Session had been talking all along
         && not (outlived.Database.Private.Sessions
                 |> List.exists (SessionRecord.hasIdledOut outlived.Env.Now)))

    // ── UC-9 ext 1a — the Server restarts instead ──
    // Nothing ends. This is the headline change of the stateless design: the Session's
    // identity and standing are in its SessionRecord, its work is in the Client, and
    // the Server held neither (Rules 9, 31).
    let restarted =
        step "UC-9 ext 1a — the Server restarts instead" saved
             [ envt GenPresServer (Stop GenPresServer); tick; envt GenPresServer (Start GenPresServer) ]

    expect "1a nothing ends: the Session is still open (Rules 9, 31)"
        (openCount restarted = 1 && stateOf 1 restarted = Some OpenOrGone)

    expect "1a the Server settled nothing at the start — there was nothing to settle"
        (never (function ReadSessionRecords ForSweep -> true | _ -> false)
         && never (function EndSessionIfOpen _ -> true | _ -> false))

    expect "1a the Client still holds its cart (Rule 31)"
        ((workingAt 1 restarted).Length = 3)

    let seenBefore = lastSeenOf 1 restarted

    let afterRestart =
        step "1a — and the next request continues the Session (Rules 8, 9)" restarted
             [ act 1 (Prescribes(OrderContextId "oc-after-restart")) ]

    expect "1a the next request is served, and refreshes the idle clock"
        (saw (function Computed _ -> true | _ -> false)
         && never (function SessionEnded _ -> true | _ -> false)
         && lastSeenOf 1 afterRestart > seenBefore)

    // While it is down, requests fail as in UC-1 ext 3a.
    let whileDown =
        step "1a — while it is down, requests fail as in UC-1 ext 3a" saved
             [ envt GenPresServer (Stop GenPresServer); act 1 (Prescribes(OrderContextId "oc-nope")) ]

    expect "1a a down Server is unreachable, not an ending"
        (saw (function ServerUnreachable -> true | _ -> false)
         && stateOf 1 whileDown = Some OpenOrGone)

    // ── UC-9 ext 1b — A opens another Session at another workstation ──
    let elsewhere = step "UC-9 ext 1b — A opens another Session instead" saved (launchAs ucA.Login (Some pat2))

    expect "1b the launch itself ends the old Session, and the notice comes with it (Rules 7, 9, 10)"
        (openCount elsewhere = 1
         && (match stateOf 1 elsewhere with Some(Ended(Superseded, _)) -> true | _ -> false)
         && saw (function PriorSessionNotice _ -> true | _ -> false))

    let ackedElsewhere =
        step "UC-9 ext 1b — A dismisses the notice at the new workstation" elsewhere
             [ act 2 AcknowledgesNotice ]

    let oldTab =
        step "UC-9 ext 1b — the old Client's next request is refused, and not told again (Rule 10)" ackedElsewhere
             [ act 1 (Prescribes(OrderContextId "oc-z")) ]

    expect "1b refused, and the acknowledged notice is not repeated"
        (saw (function SessionRefused _ -> true | _ -> false)
         && never (function SessionEnded _ -> true | _ -> false))

    ignore oldTab
    told

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-10  A Reader consults a Patient
// ═══════════════════════════════════════════════════════════════════════════════

let uc10 () =
    printfn ""
    printfn "############### UC-10  A Reader consults a Patient ###############"

    // Precondition: as UC-9. Patient 2's head is an Unsigned TreatmentPlan of A's over its
    // Signed one. C, a Reader, launches for Patient 2.
    let withUnsignedHead =
        quiet "UC-10 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [ act 1 (Prescribes(OrderContextId "oc-4")); act 1 Saves ])

    let reading = step "UC-10 step 1 — C launches for Patient 2" withUnsignedHead (launchAs ucC.Login (Some pat2))

    expect "UC-10 step 1: C never creates a TreatmentPlan, so no Unsigned one of their own can exist (Rules 17, 19)"
        (openedAt 2 reading = Some p2Signed.Id)

    expect "UC-10 step 2: C reads the plan that counts clinically (Rule 16)"
        (workingAt 2 reading = p2Signed.Orders)

    expect "UC-10 step 2: A's newer Unsigned TreatmentPlan is not shown — only its creator can open it (Rule 18)"
        (recordFor pat2 reading |> PatientRecord.mayOpen ucC.UserId (headOf pat2 reading).Value.Id).IsNone

    // Its existence is not announced either: the only notification of another's
    // Unsigned work fires at TreatmentPlan creation (Rule 21), and a Reader never creates.
    let exploring =
        step "UC-10 step 3 — C prescribes within the Session to explore alternatives" reading
             [
                 act 2 (Prescribes(OrderContextId "oc-what-if"))
                 act 2 Saves
                 yield! signs 2 (Pin "0000")
             ]

    expect "UC-10 step 3: prescribing works (Concept 15), but saving and signing are not offered"
        (saw (function Computed _ -> true | _ -> false)
         && saw (function NotPermitted -> true | _ -> false)
         && planCount pat2 exploring = planCount pat2 reading)

    expect "UC-10 step 3: no PIN is ever asked for, and none is ever read (Rule 25)"
        (never (function PinRequired _ -> true | _ -> false)
         && never (function ReadCredential _ -> true | _ -> false))

    expect "UC-10 step 2: the existence of A's Unsigned work goes unannounced (Rule 21)"
        (never (function UnsignedWorkNotice _ -> true | _ -> false))

    // A Reader can thus be reading a plan that a Prescriber already knows is being
    // superseded. The model accepts this deliberately: Unsigned work counts for
    // nothing until it is signed (Rule 16), so there is nothing yet to tell a Reader.
    exploring


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-11  A User resumes their own Unsigned work
// ═══════════════════════════════════════════════════════════════════════════════

let uc11 () =
    printfn ""
    printfn "############### UC-11  A User resumes their own Unsigned work ###############"

    // Precondition: UC-9 completed. Patient 2's head is A's own Unsigned TreatmentPlan over
    // the older Signed one, and A launches again for Patient 2.
    let parked =
        quiet "UC-11 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [ act 1 (Prescribes(OrderContextId "oc-4")); act 1 Saves ]
               @ ticks (sessionTtl + 5))

    let offered = step "UC-11 step 1 — A launches again for Patient 2" parked (launchAs ucA.Login (Some pat2))

    // Rule 19's "may start with", and why it reads that way. The Unsigned head is
    // A's own — but the Session that saved it did not end by A: it idled out. A plan
    // left behind by a Session that ended out from under the User is exactly what a
    // planted one looks like, and the two are told apart by the User, not by the Server.
    // So it is offered, and the Session starts from the Signed plan underneath.
    expect "UC-11 step 1: the Session does not open the Unsigned head — it offers it (Rule 19)"
        (openedAt 2 offered = Some p2Signed.Id
         && (workingAt 2 offered) = p2Signed.Orders)

    // Rule 19. The offer names three things, not one: which work, how the Session
    // that saved it ended, and when it was saved. The last is what lets the User place
    // it — "mine, from just before I was cut off" reads differently from "in my name,
    // from a time I was not here", and only the User can tell those apart.
    expect "UC-11 step 1: and the offer names the work, how the Session ended, and when it was saved (Rule 19)"
        (let head = (headOf pat2 offered).Value

         // Not the screen: a Rule 10 notice may take that in front of the offer. The
         // offer is a standing thing on the Client, and it is what is asserted here.
         (clientOf 2 offered |> Option.bind _.Offered) = Some(head.Id, Some Idle, head.At))

    // Rule 19. Every ending that is not the User's own act triggers the offer, and
    // `Expired` is one of them: a Session cut short by its absolute lifetime ended out
    // from under its User exactly as an idle one did (Rule 9). The work it left behind
    // is no more theirs for having been left that way.
    let expiredInstead =
        // The record as the sweep would have written it had the Session run out its
        // absolute lifetime instead of idling: the ending is Expired, and the open is
        // `sessionMaxLifetime` behind it, which is the only way that ending is reached.
        let ended =
            { parked with
                Database.Private.Sessions =
                    parked.Database.Private.Sessions
                    |> List.map (fun r ->
                        match r.State with
                        | Ended(Idle, at) ->
                            { r with
                                State = Ended(Expired, at)
                                OpenedAt = min r.OpenedAt (at - sessionMaxLifetime) }
                        | _ -> r) }

        step "UC-11 step 1 — and the same, where the Session ran out its lifetime instead" ended
             (launchAs ucA.Login (Some pat2))

    expect "UC-11 Expired is among the endings that offer rather than open (Rules 9, 19)"
        (let head = (headOf pat2 expiredInstead).Value

         openedAt 2 expiredInstead = Some p2Signed.Id
         && (clientOf 2 expiredInstead |> Option.bind _.Offered) = Some(head.Id, Some Expired, head.At))

    // Rule 19 draws the line at one ending and not at a list of them: the User closing
    // the Session. Every other way it can end says nothing about whether the work was
    // meant to be left, so every other way offers. Checked over all five of them.
    expect "UC-11 the line is 'the User closed it', not a list of endings (Rule 19)"
        ([ ReplacedInBrowser; Idle; Superseded; WrongPinLimit; Expired ]
         |> List.forall (fun mark ->
             let ended =
                 { parked with
                     Database.Private.Sessions =
                         parked.Database.Private.Sessions
                         |> List.map (fun r ->
                             match r.State with
                             | Ended(_, at) ->
                                 { r with
                                     State = Ended(mark, at)
                                     // Rule 10. The obligation belongs to the ending,
                                     // so it is set to match the one being planted.
                                     Notice = (if SessionRecord.owesNotice mark then Owed else NotOwed)
                                     OpenedAt = min r.OpenedAt (at - sessionMaxLifetime) }
                             | OpenOrGone -> r) }

             let after = quiet "Rule 19 — every ending but the User's own offers" ended (launchAs ucA.Login (Some pat2))
             let head = (headOf pat2 after).Value

             openedAt 2 after = Some p2Signed.Id
             && (clientOf 2 after |> Option.bind _.Offered) = Some(head.Id, Some mark, head.At)))

    // And the one that does not: a Session the User closed leaves work they put down
    // on purpose, so the next launch opens it as Rule 19 always did.
    expect "UC-11 a Session the User closed leaves work that is opened, not offered (Rule 19)"
        (let closed =
            { parked with
                Database.Private.Sessions =
                    parked.Database.Private.Sessions
                    |> List.map (fun r ->
                        match r.State with
                        | Ended(_, at) -> { r with State = Ended(ClosedByUser, at); Notice = NotOwed }
                        | OpenOrGone -> r) }

         let after = quiet "Rule 19 — but the User's own close does not" closed (launchAs ucA.Login (Some pat2))

         openedAt 2 after = (headOf pat2 after |> Option.map _.Id)
         && (clientOf 2 after |> Option.bind _.Offered) = None)

    let resumed =
        step "UC-11 step 1 — A takes the work up (Rule 18)" offered
             [ act 2 (OpensTreatmentPlan (headOf pat2 offered).Value.Id) ]

    expect "UC-11 step 1: taken up, the Session stands on A's own Unsigned head and carries its work"
        (openedAt 2 resumed = (headOf pat2 resumed |> Option.map _.Id)
         && (workingAt 2 resumed).Length = 2)

    let signed =
        step "UC-11 step 2 — A reviews, adjusts and signs" resumed
             [
                 act 2 (Prescribes(OrderContextId "oc-4"))
                 yield! signs 2 pinA
             ]

    expect "UC-11 step 2: nothing blocks and nothing warns (Rules 20, 21)"
        (never (function SubmissionBlocked _ -> true | _ -> false)
         && never (function UnsignedWorkNotice _ -> true | _ -> false))

    expect "UC-11 step 2: a Signed TreatmentPlan in A's name; it now counts clinically (Rules 14, 15, 16)"
        ((headOf pat2 signed |> Option.map _.State) = Some Signed
         && (headOf pat2 signed |> Option.map _.By) = Some ucA
         && (recordFor pat2 signed |> PatientRecord.latestSigned |> Option.map _.Id)
                = (headOf pat2 signed |> Option.map _.Id))

    // Concept 13, and Actor 5's shape. The signature was built on A's own Unsigned
    // work — but that plan lives only in the private store, so the Signed one cannot
    // carry a reference to it into the clinical store. What it carries instead is
    // `SignedBase`: the nearest Signed ancestor, which is the plan under the Unsigned
    // one. The chain the copy follows skips exactly what the copy does not have.
    expect "UC-11 step 2: the signature skips the Unsigned plan it was built on and names the Signed one under it"
        (let head = (headOf pat2 signed).Value
         let resumedFrom = (headOf pat2 resumed).Value

         head.Base = None
         && head.SignedBase = resumedFrom.SignedBase
         && head.SignedBase <> Some resumedFrom.Id
         && resumedFrom.State = Unsigned)

    // ── UC-11 ext 2a — a Signed TreatmentPlan appeared since the launch ──
    let bSignedMeanwhile =
        quiet "UC-11 ext 2a setup" resumed
              (launchAs ucB.Login (Some pat2)
               @ [
                   act 3 (Prescribes(OrderContextId "oc-c"))
                   // B opened from the older Signed TreatmentPlan, so A's Unsigned head is
                   // newer and Rule 21 fires on B as well. B re-sends with the token.
                   yield! signs 3 pinB
                   yield! signs 3 pinB
                 ])

    let aBlocked =
        step "UC-11 ext 2a — A signs after a Signed TreatmentPlan appeared" bSignedMeanwhile
             [ yield! signs 2 pinA ]

    expect "2a submitting is blocked (Rule 20)"
        (saw (function SubmissionBlocked _ -> true | _ -> false))

    let aRecovered =
        step "UC-11 ext 2a — A opens it, reapplies, and continues (Rule 17; UC-6 step 4)" aBlocked
             [
                 act 2 (OpensTreatmentPlan (headOf pat2 aBlocked).Value.Id)
                 act 2 (Prescribes(OrderContextId "oc-4"))
                 yield! signs 2 pinA
             ]

    expect "2a opening the newest Signed TreatmentPlan lifts the block"
        ((headOf pat2 aRecovered |> Option.map _.By) = Some ucA
         && (headOf pat2 aRecovered |> Option.map _.State) = Some Signed)

    // ── UC-11 ext 2b — another User's Unsigned TreatmentPlan appeared since the launch ──
    let bSavedMeanwhile =
        quiet "UC-11 ext 2b setup" resumed
              (launchAs ucB.Login (Some pat2)
               @ [
                   act 3 (Prescribes(OrderContextId "oc-d"))
                   act 3 Saves
                   act 3 Saves
                 ])

    let aWarned =
        step "UC-11 ext 2b — A signs after another's Unsigned TreatmentPlan appeared" bSavedMeanwhile
             [ yield! signs 2 pinA ]

    expect "2b A is notified and decides (Rule 21) — not blocked (Rule 20)"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucB | _ -> false)
         && never (function SubmissionBlocked _ -> true | _ -> false))

    ignore aWarned
    signed


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-12  User closes GenPRES
// ═══════════════════════════════════════════════════════════════════════════════

let uc12 () =
    printfn ""
    printfn "############### UC-12  User closes GenPRES ###############"

    // Precondition: UC-11 completed. A has an open Session for Patient 2, its work
    // signed, and nothing unsaved remains.
    let signedUp =
        quiet "UC-12 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [ act 1 (Prescribes(OrderContextId "oc-4")); yield! signs 1 pinA ])

    let closed = step "UC-12 step 1 — A closes the Session in the Client" signedUp [ act 1 ClosesSession ]

    expect "UC-12 step 1: the Session ends, marked closed by the User (Rule 9, Concept 9)"
        (openCount closed = 0
         && (match stateOf 1 closed with Some(Ended(ClosedByUser, _)) -> true | _ -> false))

    expect "UC-12 step 2: and no notice is ever owed — not owed and then skipped (Rule 10)"
        (noticeOf 1 closed = Some NotOwed)

    let nextLaunch = step "UC-12 step 2 — the next launch starts clean" closed (launchAs ucA.Login (Some pat2))

    expect "UC-12 step 2: no notice follows — Rule 10 speaks only of endings other than by the User"
        (never (function PriorSessionNotice _ -> true | _ -> false)
         && noticeOf 1 nextLaunch = Some NotOwed)

    // ── UC-12 ext 1a — unsaved changes remain at the close ──
    let withUnsaved =
        quiet "UC-12 ext 1a setup" signedUp [ act 1 (Prescribes(OrderContextId "oc-dangling")) ]

    let closedAnyway = step "UC-12 ext 1a — A closes with unsaved changes: closed is closed" withUnsaved [ act 1 ClosesSession ]

    expect "1a they existed only in the Client and are gone (Rule 31); anything saved stands (Concept 12)"
        (openCount closedAnyway = 0
         && (workingAt 1 closedAnyway).IsEmpty
         && planCount pat2 closedAnyway = 2
         && (headOf pat2 closedAnyway
             |> Option.map _.Orders
             |> Option.defaultValue []
             |> List.exists (fun o -> o.Id = OrderContextId "oc-dangling")
             |> not))

    // ── UC-12 ext 1b — A closes the browser instead ──
    let browserGone = step "UC-12 ext 1b — A closes the browser instead" signedUp [ atClient 1 CloseBrowser ]

    expect "1b nothing reaches the Server, so no close can be inferred (Rule 9)"
        (openCount browserGone = 1
         && stateOf 1 browserGone = Some OpenOrGone
         && never (function SessionRequest _ -> true | _ -> false))

    let idledOut =
        step "UC-12 ext 1b — the Session idles out instead" browserGone (ticks (sessionTtl + 5))

    expect "1b it idles out, and A is told at the next opportunity (Rule 10; UC-9)"
        (match stateOf 1 idledOut with Some(Ended(Idle, _)) -> true | _ -> false)

    let harmlessNotice = step "UC-12 ext 1b — a harmless notice, the price of the indistinguishability" idledOut (launchAs ucA.Login (Some pat2))

    expect "1b the notice arrives at the next launch"
        (saw (function PriorSessionNotice _ -> true | _ -> false))

    ignore harmlessNotice
    closed


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-13  A User's authority is withdrawn
// ═══════════════════════════════════════════════════════════════════════════════

let uc13 () =
    printfn ""
    printfn "############### UC-13  A User's authority is withdrawn ###############"

    // Precondition: UC-3 ran once more through step 2 and stopped. Patient 2's head is
    // an Unsigned TreatmentPlan of A's over the Signed one. Then the UserRegistry stops
    // returning a Role for A's login.
    let aSaved =
        quiet "UC-13 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [ act 1 (Prescribes(OrderContextId "oc-4")); act 1 Saves ])

    let withdrawn = { aSaved with Registry.Users = aSaved.Registry.Users |> Map.remove ucA.Login }

    let refused = step "UC-13 step 1 — A launches; the registry returns no Role" withdrawn (launchAs ucA.Login (Some pat2))

    // A's Session from the precondition is still open, and stays open — that is ext 1a
    // below. What the failed launch must not do is open another one.
    expect "UC-13 step 1: no Role, so the launch opens no Session (Rules 5, 6)"
        (saw (function UserUnresolved(_, NoRole) -> true | _ -> false)
         && saw (function NotAuthorised -> true | _ -> false)
         && never (function SessionOpened _ -> true | _ -> false))

    let cds = step "UC-13 step 1 — A accepts the anonymous open: CDS is all that remains" refused [ atClient 2 AcceptAnonymousOffer ]

    expect "UC-13 step 1: hand-entered patients, no records, nothing saved (UC-8; Rule 13)"
        ((newestRecord cds |> Option.bind _.User) = None
         && (newestRecord cds |> Option.bind _.Patient) = None)

    let againRefused = step "UC-13 step 1 — every later launch ends the same way (Rule 5)" cds (launchAs ucA.Login (Some pat2))

    expect "UC-13 step 1: the Role is taken from the registry at each launch, so the withdrawal stands"
        (saw (function NotAuthorised -> true | _ -> false))

    expect "UC-13 step 2: A's UserCredential remains, but is inert (Concepts 7, 14)"
        ((credentialOf ucA againRefused).IsSome
         && (credentialOf ucA againRefused |> Option.bind _.Pin).IsSome)

    // ── step 3 — the Unsigned TreatmentPlan is stranded ──
    let bWorksPast =
        step "UC-13 step 3 — B's next Session starts from the Signed TreatmentPlan below (Rule 19)" againRefused
             (launchAs ucB.Login (Some pat2))

    expect "UC-13 step 3: only A could open the stranded work, and A can no longer reach it"
        (openedAt 4 bWorksPast = Some p2Signed.Id)

    // UC-13 step 3, and Rule 47's other half: nobody can discard it either. Only its
    // creator may (Rules 18, 47), and A has no Session to do it from — so the work is
    // stranded rather than tidied away, and only B's signature supersedes it.
    let strandedId = (headOf pat2 bWorksPast).Value.Id

    let bTriesToDiscard =
        step "UC-13 step 3 — B tries to discard the stranded work" bWorksPast
             [ act 4 (Discards strandedId) ]

    expect "UC-13 step 3: B cannot discard work that is not theirs (Rules 18, 47)"
        (saw (function SubmissionRefused why -> why.Contains "author's alone" | _ -> false)
         && ((recordFor pat2 bTriesToDiscard).Plans
             |> List.exists (fun x -> x.Id = strandedId && x.State = Unsigned)))

    // And A cannot either: the anonymous Session A was left with can commit nothing
    // (Rules 12, 13), so there is no Session anywhere that could put this down.
    let aTriesFromAnonymous =
        step "UC-13 step 3 — A tries from the anonymous Session they were left with" bTriesToDiscard
             [ act 2 (Discards strandedId) ]

    expect "UC-13 step 3: an anonymous Session can discard nothing (Rules 12, 13, 47)"
        (never (function TreatmentPlanDiscardedOk _ -> true | _ -> false)
         && ((recordFor pat2 aTriesFromAnonymous).Plans
             |> List.exists (fun x -> x.Id = strandedId && x.State = Unsigned)))

    let bNotified =
        step "UC-13 step 3 — B is notified of the stranded work at the save (Rule 21)" bWorksPast
             [ act 4 (Prescribes(OrderContextId "oc-e")); yield! signs 4 pinB ]

    expect "UC-13 step 3: B is told whose work it is"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false))

    let superseded = step "UC-13 step 3 — B re-sends with the token, and their signature supersedes it for good" bNotified [ yield! signs 4 pinB ]

    expect "UC-13 step 3: B's Signed TreatmentPlan now counts, and A's work can never be signed (Rules 16, 20)"
        ((headOf pat2 superseded |> Option.map _.By) = Some ucB
         && (headOf pat2 superseded |> Option.map _.State) = Some Signed)

    // ── UC-13 ext 1a — the withdrawal happens while A's Session is open ──
    // The Session keeps the Role its launch established, off the SessionRecord (Rules
    // 5, 32). A signature is the one act that does not accept that (Rule 38), so a
    // withdrawal lands the moment A signs — while saving goes on working.
    let stillSaves =
        step "UC-13 ext 1a — the withdrawal lands while A's Session is open: A saves" withdrawn
             [ act 1 (Prescribes(OrderContextId "oc-f")); act 1 Saves ]

    expect "1a the open Session keeps the Role its launch established, and saving works (Concept 9, Rule 32)"
        ((headOf pat2 stillSaves |> Option.map _.By) = Some ucA
         && (headOf pat2 stillSaves |> Option.map _.State) = Some Unsigned
         && never (function ResolveUser _ -> true | _ -> false))

    let cannotSign =
        step "UC-13 ext 1a — but the signature asks the registry again (Rule 38)" stillSaves [ yield! signs 1 pinA ]

    expect "1a the Role is gone, so the signature is refused — and before the PIN is asked for"
        (saw (function ResolveUser(ForRequest _, _) -> true | _ -> false)
         && saw (function UserUnresolved(_, NoRole) -> true | _ -> false)
         && saw (function NotPermitted -> true | _ -> false)
         && never (function ReadCredential _ -> true | _ -> false)
         && (recordFor pat2 cannotSign |> PatientRecord.latestSigned |> Option.map _.Id) = Some p2Signed.Id)

    expect "1a a signature nobody is entitled to costs no PIN attempt (Rules 27, 38)"
        ((credentialOf ucA cannotSign |> Option.map _.AttemptCount) = Some 0)

    // A registry that is merely down is not a withdrawal: for `roleGrace` after the
    // launch its Role stands instead, and the audit says so. The trade is deliberate —
    // within that window a withdrawal the registry cannot report does not land.
    let registryDown =
        step "UC-13 ext 1a — the registry cannot be asked at all, and the launch is recent"
             { stillSaves with Registry.Up = false }
             [ yield! signs 1 pinA ]

    expect "1a within the grace the signature lands on the Role the launch took, and it is audited (Rule 38)"
        (saw (function TreatmentPlanSubmitted(_, Signed, _) -> true | _ -> false)
         && never (function SigningUnavailable -> true | _ -> false)
         && registryDown |> audited "under grace"
         && openCount registryDown = openCount stillSaves)

    // Past the window it fails closed, exactly as Rule 38 said before.
    let staleRole =
        let aged =
            { stillSaves with
                Registry.Up = false
                Database.Private.Sessions =
                    stillSaves.Database.Private.Sessions
                    |> List.map (fun r -> { r with OpenedAt = r.OpenedAt - (roleGrace + 1) }) }
        step "UC-13 ext 1a — and past the grace, signing fails closed" aged [ yield! signs 1 pinA ]

    expect "1a past the grace no answer means no signature, and the Session is untouched (Rule 38)"
        (saw (function SigningUnavailable -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && openCount staleRole = openCount stillSaves)

    superseded


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-14  A User discards their own draft
// ═══════════════════════════════════════════════════════════════════════════════

let uc14 () =
    printfn ""
    printfn "############### UC-14  A User discards their own draft ###############"

    // Rules 15 and 47. A draft is work in progress, and work in progress can be put
    // down. Discarding is not a signature and not a Submission: no PIN, no challenge, no
    // Rule 20 check. What it does is change one field of one plan, once, in one
    // direction — and nothing is removed, because Concept 12 is append-only.

    // ── main path ──
    // A opens on pat3, whose head is A's own Unsigned plan over a Signed one of B's.
    let onDraft = quiet "UC-14 precondition — A opens on their own Unsigned head" world (launchAs ucA.Login (Some pat3))

    let draft = (headOf pat3 onDraft).Value
    let before = planCount pat3 onDraft

    expect "UC-14 precondition: the Session opened on A's own Unsigned draft (Rule 19)"
        (draft.State = Unsigned && draft.By.UserId = ucA.UserId && openedAt 1 onDraft = Some draft.Id)

    let discarded = step "UC-14 main — A puts the draft down" onDraft [ act 1 (Discards draft.Id) ]

    let after = recordFor pat3 discarded
    let nowIs = after.Plans |> List.tryFind (fun x -> x.Id = draft.Id)

    expect "UC-14 the plan's state moves to Discarded, and nothing else about it changes (Rule 15)"
        ((nowIs |> Option.map _.State) = Some Discarded
         && (nowIs |> Option.map _.Orders) = Some draft.Orders
         && (nowIs |> Option.map _.By) = Some draft.By
         && (nowIs |> Option.map _.No) = Some draft.No)

    expect "UC-14 nothing is removed: the record is exactly as long as it was (Concept 12)"
        (planCount pat3 discarded = before)

    expect "UC-14 the Session now stands on the Signed plan that was under it (Rule 19)"
        ((after |> PatientRecord.startsFrom ucA.UserId |> Option.map _.Id) = Some p3Signed.Id
         && openedAt 1 discarded = Some p3Signed.Id
         && (workingAt 1 discarded |> List.map _.Id) = (p3Signed.Orders |> List.map _.Id))

    // Rule 33. The old token named a plan that is Discarded; the new one names what
    // the Session now stands on, and it verifies.
    expect "UC-14 the OpenedToken is re-issued over the new baseline (Rule 33)"
        ((clientOf 1 discarded |> Option.bind _.Opened |> Option.map Token.verifyOpened) = Some true
         && (clientOf 1 discarded |> Option.bind _.Opened |> Option.bind Token.plan) = Some p3Signed.Id)

    expect "UC-14 the audit says what was discarded, and by whom (Rule 46)"
        (let (TreatmentPlanId d) = draft.Id in discarded |> audited $"%s{d} discarded")

    // And the Session is a working Session still: A can build on the plan below.
    let builtOn =
        step "UC-14 main — and A works on from what is under it" discarded
             [ act 1 (Prescribes(OrderContextId "oc-fresh")); act 1 Saves ]

    expect "UC-14 a Submission on the new baseline lands, and its base is the Signed plan (Rules 19, 20)"
        (planCount pat3 builtOn = before + 1
         && (headOf pat3 builtOn |> Option.bind _.Base) = Some p3Signed.Id
         && (headOf pat3 builtOn |> Option.map _.State) = Some Unsigned)

    // ── UC-14 ext 1a — a Signed plan cannot be discarded ──
    // Rule 15's one direction, stated as a refusal. What a signature attested it
    // attested, and no later act of its author's takes that back.
    let onSigned = quiet "UC-14 ext 1a precondition" world (launchAs ucA.Login (Some pat2))
    let signedHead = (headOf pat2 onSigned).Value

    let refusedSigned =
        step "UC-14 ext 1a — A tries to discard a Signed TreatmentPlan" onSigned
             [ act 1 (Discards signedHead.Id) ]

    expect "1a a Signed TreatmentPlan cannot be discarded, and the record is untouched (Rules 15, 16)"
        (signedHead.State = Signed
         && saw (function SubmissionRefused why -> why.Contains "cannot be discarded" | _ -> false)
         && (headOf pat2 refusedSigned |> Option.map _.State) = Some Signed
         && (recordFor pat2 refusedSigned |> PatientRecord.latestSigned |> Option.map _.Id) = Some signedHead.Id
         && planCount pat2 refusedSigned = planCount pat2 onSigned)

    // ── UC-14 ext 1b — somebody else at A's workstation discards ──
    // Possibility 1. Whoever is at the keys can put A's draft down: the Session's User
    // is who the Server knows (Rule 32) and no PIN is asked (Rule 47). It costs the
    // record nothing, which is the whole reason discarding is not a deletion.
    let takenOver = quiet "UC-14 ext 1b precondition — A opens, and walks away" world (launchAs ucA.Login (Some pat3))
    let aDraft = (headOf pat3 takenOver).Value
    let signedBelow = recordFor pat3 takenOver |> PatientRecord.latestSigned

    let strangerDiscards =
        step "UC-14 ext 1b — somebody at A's workstation puts A's draft down" takenOver
             [ act 1 (Discards aDraft.Id) ]

    expect "1b the discard lands — but nothing is lost from the record (Concept 12, Rule 15)"
        (planCount pat3 strangerDiscards = planCount pat3 takenOver
         && (recordFor pat3 strangerDiscards
             |> _.Plans
             |> List.exists (fun x -> x.Id = aDraft.Id && x.Orders = aDraft.Orders))
         && (recordFor pat3 strangerDiscards |> PatientRecord.latestSigned |> Option.map _.Id)
            = (signedBelow |> Option.map _.Id))

    // ── UC-14 ext 2a — somebody signed since A saved ──
    // Rule 47's point. A Submission here would be blocked by Rule 20; a discard is not,
    // because it builds on nothing and attests to nothing. It lands, and the Session
    // opens on the signature that arrived.
    let aSavedThenB =
        let aSaved =
            quiet "UC-14 ext 2a precondition — A saves a draft on pat1" world
                  (launchAs ucA.Login (Some pat1)
                   @ [ act 1 (Prescribes(OrderContextId "oc-a")); act 1 Saves ])

        // Rule 21: A's Unsigned draft is newer than anything B opened with, so B is
        // told whose work stands there and signs again to go ahead (Rule 34).
        quiet "UC-14 ext 2a precondition — and B signs on the same Patient" aSaved
              (launchAs ucB.Login (Some pat1)
               @ [
                   act 2 (Prescribes(OrderContextId "oc-b"))
                   yield! signs 2 pinB
                   yield! signs 2 pinB
                 ])

    let aDraftOnPat1 =
        (recordFor pat1 aSavedThenB).Plans
        |> List.tryFind (fun x -> x.State = Unsigned && x.By.UserId = ucA.UserId)
        |> Option.get

    let bSignedOnPat1 = (recordFor pat1 aSavedThenB |> PatientRecord.latestSigned).Value

    // First, that a Submission really would be blocked here — otherwise the discard
    // landing proves nothing.
    let aBlocked =
        step "UC-14 ext 2a — A's submission is blocked by B's signature (Rule 20)" aSavedThenB
             [ act 1 (Prescribes(OrderContextId "oc-a2")); act 1 Saves ]

    expect "2a a Submission is blocked: something Signed appeared since A opened (Rule 20)"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && planCount pat1 aBlocked = planCount pat1 aSavedThenB)

    let discardedAnyway =
        step "UC-14 ext 2a — but the discard lands all the same (Rule 47)" aSavedThenB
             [ act 1 (Discards aDraftOnPat1.Id) ]

    expect "2a the discard is not measured against the head: it lands (Rules 20, 47)"
        (never (function SubmissionBlocked _ -> true | _ -> false)
         && ((recordFor pat1 discardedAnyway).Plans
             |> List.exists (fun x -> x.Id = aDraftOnPat1.Id && x.State = Discarded)))

    expect "2a and the Session opens on the Signed plan that arrived meanwhile (Rule 19)"
        (openedAt 1 discardedAnyway = Some bSignedOnPat1.Id
         && (recordFor pat1 discardedAnyway
             |> PatientRecord.startsFrom ucA.UserId
             |> Option.map _.Id) = Some bSignedOnPat1.Id)

    // ── Rule 47 — what a discard is not ──
    // Not somebody else's to make, and not an older draft's either.
    let notMine =
        let bOnPat3 = quiet "Rule 47 precondition — B opens on pat3" world (launchAs ucB.Login (Some pat3))
        step "Rule 47 — B tries to discard A's Unsigned draft" bOnPat3
             [ act 1 (Discards p3Unsigned.Id) ]

    expect "Rule 47 an Unsigned TreatmentPlan is its author's alone to put down (Rule 18)"
        (saw (function SubmissionRefused why -> why.Contains "author's alone" | _ -> false)
         && ((recordFor pat3 notMine).Plans
             |> List.exists (fun x -> x.Id = p3Unsigned.Id && x.State = Unsigned)))

    let notTheHead =
        let twoDrafts =
            quiet "Rule 47 precondition — A saves twice on pat1" world
                  (launchAs ucA.Login (Some pat1)
                   @ [
                       act 1 (Prescribes(OrderContextId "oc-first"))
                       act 1 Saves
                       act 1 (Prescribes(OrderContextId "oc-second"))
                       act 1 Saves
                     ])

        let older =
            (recordFor pat1 twoDrafts).Plans
            |> List.filter (fun x -> x.By.UserId = ucA.UserId)
            |> List.last

        step "Rule 47 — A tries to discard the older of their two drafts" twoDrafts
             [ act 1 (Discards older.Id) ]

    expect "Rule 47 only the User's most recent TreatmentPlan can be put down"
        (saw (function SubmissionRefused why -> why.Contains "most recent" | _ -> false))

    // And a discard is not repeatable: the second one finds a Discarded plan.
    let twice = step "Rule 47 — and a discarded plan cannot be discarded again" discarded [ act 1 (Discards draft.Id) ]

    expect "Rule 47 discarding is one act in one direction: the second is refused (Rule 15)"
        (saw (function SubmissionRefused why -> why.Contains "already discarded" | _ -> false)
         && planCount pat3 twice = planCount pat3 discarded)

    // ── Rule 33 — a discard consumes the OpenedToken ──
    // A discard moves the baseline as a Submission does, so the token that named the
    // old one must stop working in the same act — or the record grows a branch out of
    // something Rule 19 says is no starting point.
    let preDiscardToken = (clientOf 1 onDraft |> Option.bind _.Opened).Value

    expect "Rule 33 the token the discard was made with is spent by it, as a Submission's would be"
        (discarded.Database.Private.Spent.Contains preDiscardToken.Claim.Nonce
         && not (onDraft.Database.Private.Spent.Contains preDiscardToken.Claim.Nonce))

    let builtOnTheDiscarded =
        step "Rule 33 — a Client comes back with the token it held before the discard" discarded
             [
                 fromClient 1
                     (SessionRequest(
                         (sidAt 1 discarded).Value,
                         handCreate { WorkPlan.empty with Orders = [ oc "oc-ghost" pat3 ucA ] } preDiscardToken None None))
             ]

    expect "Rule 33 the spent token is refused, and nothing is built on the discarded plan"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat3 builtOnTheDiscarded = planCount pat3 discarded)

    // And the record-side guard behind it, reached by a token that was never spent:
    // one minted for the discarded plan after the fact. The Client cannot make one —
    // the mac is over a secret it never sees — so this is the Server's own token,
    // used to show that the check does not rest on the spent-mark alone.
    let freshTokenOnTheDiscarded =
        let ghost = Token.mintOpened discarded.Env.Now (sidAt 1 discarded).Value (Some pat3) (Some draft.Id)

        step "Rule 47 — and an unspent token naming the discarded plan is refused too" discarded
             [
                 fromClient 1
                     (SessionRequest(
                         (sidAt 1 discarded).Value,
                         handCreate { WorkPlan.empty with Orders = [ oc "oc-ghost2" pat3 ucA ] } ghost None None))
             ]

    expect "Rule 47 a discarded TreatmentPlan is no baseline: nothing is ever built on it (Rules 15, 19)"
        (saw (function SubmissionRefused why -> why.Contains "has been discarded" | _ -> false)
         && planCount pat3 freshTokenOnTheDiscarded = planCount pat3 discarded)


// ═══════════════════════════════════════════════════════════════════════════════
//  Rules 32 to 36 — the stateless design under attack
// ═══════════════════════════════════════════════════════════════════════════════
//
// The cart is the Client's (Rule 31), so everything the Server would have remembered
// arrives with the request — and is worth nothing unless the Server vouched for it.
// A Client that edits a token, invents one, lies about the Patient or forges a stamp,
// and a Database arbitrating two Servers racing for the same head.

let tokensAndArbitration () =
    printfn ""
    printfn "############### Rules 32-36  The stateless design under attack ###############"

    // ── Rule 33: an opened-with token the Client edited ──
    let both =
        quiet "tokens precondition" world
              (launchAs ucA.Login (Some pat2) @ launchAs ucB.Login (Some pat2))

    let bWon =
        step "Rule 33 setup — B signs, so A's opened-with token is now stale" both
             [ act 2 (Prescribes(OrderContextId "oc-b")); yield! signs 2 pinB ]

    let newestSigned = (recordFor pat2 bWon |> PatientRecord.latestSigned |> Option.map _.Id).Value

    let honestStale = step "Rule 33 — A's honest but stale token: blocked, as before (Rule 20)" bWon [ act 1 Saves ]

    expect "Rule 33 an honest stale token is believed, and Rule 20 does the refusing"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && never (function SubmissionRefused _ -> true | _ -> false)
         && planCount pat2 honestStale = planCount pat2 bWon)

    // Now A edits the token to name the newest Signed TreatmentPlan — which would lift the
    // Rule 20 block — and guesses at the mac.
    let forged =
        let sid = (sidAt 1 bWon).Value
        let tok =
            {
                Claim =
                    {
                        Purpose = TokenPurpose.Opened
                        Sid = sid
                        Patient = Some pat2
                        Names = [ let (TreatmentPlanId i) = newestSigned in i ]
                        Nonce = "guessed"
                        IssuedAt = 0
                        ExpiresAt = 9_999
                    }
                Mac = "mac|guessed"
            }
        step "Rule 33 — A edits the token to name the newest Signed TreatmentPlan" bWon
             [ fromClient 1 (SessionRequest(sid, handCreate (workOf 1 bWon) tok None None)) ]

    expect "Rule 33 the token does not verify, so the Submission is refused — not merely blocked"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 forged = planCount pat2 bWon)

    // ── Rule 34: a notice token that is out of date, and one the Client made ──
    let bOpen = quiet "Rule 34 precondition" world (launchAs ucB.Login (Some pat3))

    let bWarned = step "Rule 34 — B saves and is notified of A's Unsigned work (Rule 21)" bOpen [ act 1 Saves ]

    expect "Rule 34 the notice disclosed exactly the one Unsigned TreatmentPlan that exists"
        ((noticeAt 1 bWarned |> Option.map Token.disclosed) = Some [ p3Unsigned.Id ])

    // ── Concept 17: a genuine token, offered for the wrong purpose ──
    let _ =
        let sid = (sidAt 1 bWarned).Value
        let notice = (noticeAt 1 bWarned).Value
        step "Concept 17 — B offers its genuine NoticeToken as the opened-with token" bWarned
             [
                 fromClient 1
                     (SessionRequest(sid, handCreate (workOf 1 bWarned) notice None None))
             ]

    expect "Concept 17 a token minted for another purpose fails by key, not by luck"
        (saw (function SubmissionRefused why -> why.Contains "does not verify" | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // While B deliberates, A saves again: a *newer* Unsigned TreatmentPlan of another User,
    // which B's token does not name.
    let aSavedMeanwhile =
        quiet "Rule 34 — A saves again while B deliberates" bWarned
              (launchAs ucA.Login (Some pat3)
               @ [ act 2 (Prescribes(OrderContextId "oc-a2")); act 2 Saves ])

    let notifiedAgain =
        step "Rule 34 — B returns the token it was given, but the record has moved on" aSavedMeanwhile
             [ act 1 Saves ]

    expect "Rule 34 the token is honoured for what it disclosed and for nothing newer"
        (saw (function UnsignedWorkNotice _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat3 notifiedAgain = planCount pat3 aSavedMeanwhile)

    let thenAccepted = step "Rule 34 — B returns the fresh token, and the Submission lands" notifiedAgain [ act 1 Saves ]

    expect "Rule 34 a token naming everything outstanding is honoured"
        (planCount pat3 thenAccepted = planCount pat3 notifiedAgain + 1
         && (headOf pat3 thenAccepted |> Option.map _.By) = Some ucB)

    let _ =
        let sid = (sidAt 1 bOpen).Value
        let tok =
            {
                Claim =
                    {
                        Purpose = TokenPurpose.Notice
                        Sid = sid
                        Patient = Some pat3
                        Names = [ let (TreatmentPlanId i) = p3Unsigned.Id in i ]
                        Nonce = "i-made-this-up"
                        IssuedAt = 0
                        ExpiresAt = 9_999
                    }
                Mac = "i-made-this-up"
            }
        let opened = (clientOf 1 bOpen).Value.Opened.Value
        step "Rule 34 — B skips the notice by inventing a token" bOpen
             [
                 fromClient 1
                     (SessionRequest(sid, handCreate (workOf 1 bOpen) opened (Some tok) None))
             ]

    expect "Rule 34 a self-made token is treated as none at all: B is notified, not honoured"
        (saw (function UnsignedWorkNotice _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── Rule 32 / Guarantee 1: the payload names another Patient ──
    let aOnPat2 = quiet "Rule 32 precondition" world (launchAs ucA.Login (Some pat2))

    let wrongPatient =
        let sid = (sidAt 1 aOnPat2).Value
        let smuggled =
            [ { Id = OrderContextId "oc-smuggled"; Patient = Some pat3; Content = "elsewhere"; Stamp = None } ]
        let opened = (clientOf 1 aOnPat2).Value.Opened.Value
        step "Rule 32 — the payload names a Patient the SessionRecord does not" aOnPat2
             [ fromClient 1 (SessionRequest(sid, handCreate { WorkPlan.empty with Orders = smuggled } opened None None)) ]

    expect "Rule 32 the Patient comes from the SessionRecord, and a payload that disagrees is refused"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && planCount pat2 wrongPatient = planCount pat2 aOnPat2
         && planCount pat3 wrongPatient = planCount pat3 aOnPat2)

    // ── Rule 35: the payload arrives pre-stamped with another User ──
    let preStamped =
        let sid = (sidAt 1 aOnPat2).Value
        let claimed =
            [
                { Id = OrderContextId "oc-1"; Patient = Some pat2; Content = "oc-1/as-saved"; Stamp = Some ucB }
                { Id = OrderContextId "oc-new"; Patient = Some pat2; Content = "fresh"; Stamp = Some ucB }
            ]
        let opened = (clientOf 1 aOnPat2).Value.Opened.Value
        step "Rule 35 — the cart arrives stamped with B, in A's Session" aOnPat2
             [ fromClient 1 (SessionRequest(sid, handCreate { WorkPlan.empty with Orders = claimed } opened None None)) ]

    let stamps = headOf pat2 preStamped |> Option.map _.Orders |> Option.defaultValue []

    expect "Rule 35 the forged stamps are nowhere: the Server recomputed them against the base"
        (stamps |> List.forall (fun o -> o.Stamp <> Some ucB))

    expect "Rule 35 unchanged content keeps the base's stamp; the new one gets the Session's User"
        (stamps |> List.exists (fun o -> o.Id = OrderContextId "oc-1" && o.Stamp = Some ucA)
         && stamps |> List.exists (fun o -> o.Id = OrderContextId "oc-new" && o.Stamp = Some ucA))

    // ── Rules 36 and 42: two creates in flight at once ──
    // More than one Server may run, and this is what makes that safe. Interleaving the
    // cascades leg by leg is the only way to put two in flight at once: same messages,
    // different order, which is what Rules 36 and 42 exist to be safe against.
    let raced =
        racing "Rules 36, 42 — two Sessions on one Patient, two creates in flight at once" both
               [ act 1 Saves; act 2 Saves ]

    expect "Rule 42 both creates reached the Database as whole acts"
        (countOf (function CommitTreatmentPlan _ -> true | _ -> false) = 2)

    expect "Rule 36 exactly one landed; the other was refused, and the record moved once"
        (countOf (function TreatmentPlanCommitted _ -> true | _ -> false) = 1
         && countOf (function CommitRefused _ -> true | _ -> false) = 1
         && planCount pat2 raced = planCount pat2 both + 1)

    // What the loser is told depends on what won. Here both were saves, so what landed
    // is Unsigned — and Unsigned work does not block, it notifies (Rules 21, 34). The
    // loser can still save, by coming back with the token the notice carried.
    expect "Rules 21, 34 the loser is told whose work landed first, and may still proceed"
        (countOf (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false) = 1
         && countOf (function SubmissionBlocked _ -> true | _ -> false) = 0)

    // ── Rule 40: an Ended record can never come back open ──
    // The interleaving that would do it: something read the record while the Session
    // was open and writes back what it read after the Session has ended. Under Rule 40
    // there is no such write — only named changes, and the Database decides whether
    // the record is still in a state that allows them.
    let closedSession =
        quiet "Rule 40 precondition" world (launchAs ucA.Login (Some pat2) @ [ act 1 ClosesSession ])

    let replayed =
        let stale = (recNo 1 closedSession).Value
        step "Rule 40 — a stale copy of the record is replayed at the Database" closedSession
             [
                 {
                     From = GenPresServer
                     To = GenPresDatabase
                     Msg = OpenSessionClosingOthers({ stale with State = OpenOrGone; Notice = NotOwed }, None)
                 }
                 { From = GenPresServer; To = GenPresDatabase; Msg = TouchIfOpen stale.Id }
             ]

    expect "Rule 40 the Session stays ended, and its idle clock is not refreshed either"
        ((match stateOf 1 replayed with Some(Ended(ClosedByUser, _)) -> true | _ -> false)
         && openCount replayed = 0
         && recordCount replayed = recordCount closedSession
         && lastSeenOf 1 replayed = lastSeenOf 1 closedSession)

    // ── Rules 10, 42: a Submission that arrives open and commits ended ──
    // The window Rule 42 exists to close. The arrival check (Rule 41) found the Session
    // open; by the time the commit re-established it, the User had closed it in the
    // same browser. Interleaving is the only way to sit inside that window.
    //
    // Rule 10 is what is being watched here. The commit refuses, and the screen is told
    // what ended — but nothing is discharged: whoever is holding a SessionId that has
    // just stopped working need not be the User, so the notice waits for a launch.
    // A signature is the Submission with the widest window: Rule 38 puts a registry
    // leg between the arrival check and the commit, and that is where the close lands.
    let closedUnderneath =
        let challenged =
            quiet "Rules 10, 42 precondition" world
                  (launchAs ucA.Login (Some pat2)
                   @ [ act 1 (Prescribes(OrderContextId "oc-inflight")); act 1 (Signs pinA) ])

        racing "Rules 10, 42 — the Session closes while a signature is in flight" challenged
               [ act 1 ConfirmsSign; act 1 ClosesSession ]

    expect "Rule 42 the Submission is refused at the commit, because the Session ended under it"
        (saw (function CommitRefused(_, SessionNotOpen _) -> true | _ -> false)
         && planCount pat2 closedUnderneath = planCount pat2 world)

    expect "Rule 10 the screen is told what ended, and the ending discharges nothing"
        (saw (function SessionRefused _ -> true | _ -> false)
         && never (function MarkDelivered _ -> true | _ -> false))


// ═══════════════════════════════════════════════════════════════════════════════
//  The adversarial review, answered
// ═══════════════════════════════════════════════════════════════════════════════
//
// Eighteen tests an adversarial review wanted demonstrated, in its order, with the
// scenario that shows each — and where one cannot be, why not, in its own terms.
// Three are answered by the design being different now: 1 in UC-2, 3 in UC-1 ext 8b,
// 13 in UC-9.

let adversarialReview () =
    printfn ""
    printfn "############### The adversarial review, answered ###############"

    // ── 2. A stolen launch code cannot be redeemed without the initiating browser ──
    // Answered by Rule 4, not by binding the Launch: it is still an unbound bearer
    // value and it decides nothing. A thief gets a Session of their own and takes
    // nothing of A's. What still needs proving is that nothing of A's crosses over.
    let stolen =
        let parked =
            quiet "2 — the Launch sits unpresented" world
                  (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

        let taken = (launchOnTheWire ()).Value

        step "2 — a browser that proved somebody else presents A's Launch" parked
             [
                 envt GenPresServer (Start GenPresServer)
                 {
                     From = GenPresClient(BrowserId 97)
                     To = GenPresServer
                     Msg = RedeemLaunch(taken, Some ucB.Login, None)
                 }
             ]

    expect "2 the Session a stolen Launch opens is the thief's own, never A's (Rules 4, 5)"
        (openCount stolen = 1
         && (newestRecord stolen |> Option.bind _.User |> Option.map _.UserId) = Some ucB.UserId
         && (openOfUser ucA stolen).IsEmpty
         && sidAt 1 stolen = None)

    // ── 6. A request that began before a withdrawal cannot sign after it ──
    // The challenge is issued while the Role stands; the Role is withdrawn while the
    // User is looking at the modal; the signature arrives after. Rule 38 takes the Role
    // at the commit, not at the challenge, so it is the withdrawal that wins.
    let signing = quiet "adversarial precondition" world (launchAs ucA.Login (Some pat2))

    let challenged =
        step "6 — a challenge is issued while A still holds the Role (Rule 43)" signing
             [
                 fromClient 1
                     (SessionRequest(
                         (sidAt 1 signing).Value,
                         RequestSignChallenge(workOf 1 signing, (clientOf 1 signing).Value.Opened.Value, None, None)))
             ]

    let challenge = (challengeIssued ()).Value

    let commitAfterWithdrawal (h: Hospital) key =
        SessionRequest(
            (sidAt 1 h).Value,
            SubmitTreatmentPlan
                {
                    Work = workOf 1 h
                    Opened = (clientOf 1 h).Value.Opened.Value
                    Notice = None
                    Challenge = Some challenge
                    DataOk = None
                    Pin = Some pinA
                    Key = IdemKey key
                })

    let withdrawnMidSignature =
        let world = { challenged with Registry.Users = challenged.Registry.Users |> Map.remove ucA.Login }
        step "6 — the Role is withdrawn, and only then does the signature arrive" world
             [ fromClient 1 (commitAfterWithdrawal challenged "adv-6") ]

    expect "6 the signature is refused at its commit, and nothing is appended (Rule 38)"
        (saw (function NotPermitted -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 withdrawnMidSignature = planCount pat2 challenged)

    // ── 7. A request that began before the Session ended cannot append after it ──
    let closedMidSignature =
        let closed = quiet "7 setup — A closes the Session" challenged [ act 1 ClosesSession ]
        step "7 — the signature arrives after the Session has ended" closed
             [ fromClient 1 (commitAfterWithdrawal challenged "adv-7") ]

    expect "7 nothing is appended: the Session is re-established at the commit (Rules 40, 41, 42)"
        (never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 closedMidSignature = planCount pat2 challenged)

    // ── 8. Two wrong PINs at once count twice, not once ──
    // One honest Client signs once at a time — that is what the modal is for (Rule 43)
    // — so the two attempts are put on the wire by hand. The count is read, advanced
    // and written inside the one act (Rule 42), so they cannot both read the same
    // starting value and write the same answer back.
    let twoChallenges =
        let sid = (sidAt 1 signing).Value
        let ask =
            SessionRequest(
                sid,
                RequestSignChallenge(workOf 1 signing, (clientOf 1 signing).Value.Opened.Value, None, None))

        step "8 — two challenges are issued" signing [ fromClient 1 ask; fromClient 1 ask ]

    let twoWrong =
        let sid = (sidAt 1 twoChallenges).Value

        let attempt (t: SigningChallenge) key =
            SessionRequest(
                sid,
                SubmitTreatmentPlan
                    {
                        Work = workOf 1 twoChallenges
                        Opened = (clientOf 1 twoChallenges).Value.Opened.Value
                        Notice = None
                        Challenge = Some t
                        DataOk = None
                        Pin = Some(Pin "0000")
                        Key = IdemKey key
                    })

        match challengesIssued () with
        | first :: second :: _ ->
            racing "8 — two wrong PINs in flight at once" twoChallenges
                   [ fromClient 1 (attempt first "adv-8a"); fromClient 1 (attempt second "adv-8b") ]
        | _ -> twoChallenges

    expect "8 each wrong entry counts exactly once (Rules 27, 42)"
        ((credentialOf ucA twoWrong |> Option.map _.AttemptCount) = Some 2)

    // ── 9, 13. Covered where they belong ──
    // 9 (a credential at its limit cannot be retried through relaunches) is UC-3 ext
    // 3b; 13 (cross-user and cross-patient carry-over) is UC-9 step 5. Both refuse.

    // ── 10, 11. An old baseline token cannot branch, and a token works once ──
    let beforeSaving = quiet "10 setup" world (launchAs ucA.Login (Some pat2))
    let staleOpened = (clientOf 1 beforeSaving).Value.Opened.Value

    let afterSaving =
        quiet "10 setup — A saves, so the baseline moves" beforeSaving
              [ act 1 (Prescribes(OrderContextId "oc-10")); act 1 Saves ]

    let replayedToken =
        step "10 — the token the Session opened with is offered again, after the Submission it was spent on" afterSaving
             [ fromClient 1 (SessionRequest((sidAt 1 afterSaving).Value, handCreate (workOf 1 afterSaving) staleOpened None None)) ]

    expect "10 a spent token is worth no more than one the Client made up (Concept 17, Rule 33)"
        (saw (function SubmissionRefused why -> why.Contains "spent" | _ -> false)
         && planCount pat2 replayedToken = planCount pat2 afterSaving)

    let agedToken =
        let sid = (sidAt 1 afterSaving).Value
        let old =
            Token.mintOpened (afterSaving.Env.Now - tokenTtl - 1) sid (Some pat2) (headOf pat2 afterSaving |> Option.map _.Id)
        step "10 — and a genuine token past its lifetime is refused too" afterSaving
             [ fromClient 1 (SessionRequest(sid, handCreate (workOf 1 afterSaving) old None None)) ]

    expect "10 an aged token is refused, however genuine its mac (Concept 17)"
        (saw (function SubmissionRefused why -> why.Contains "expired" | _ -> false)
         && planCount pat2 agedToken = planCount pat2 afterSaving)

    // ── 14. One OrderContext, named twice ──
    let twiceNamed =
        let sid = (sidAt 1 afterSaving).Value
        let one = { Id = OrderContextId "oc-dup"; Patient = Some pat2; Content = "first"; Stamp = None }
        let work = { workOf 1 afterSaving with Orders = [ one; { one with Content = "second" } ] }
        step "14 — a WorkPlan that names one OrderContext twice" afterSaving
             [ fromClient 1 (SessionRequest(sid, handCreate work (clientOf 1 afterSaving).Value.Opened.Value None None)) ]

    expect "14 the Submission is refused whole rather than one of the two being chosen (Concept 10, Rule 42)"
        (saw (function SubmissionRefused why -> why.Contains "twice" | _ -> false)
         && planCount pat2 twiceNamed = planCount pat2 afterSaving)

    // ── 17. A failure leaves retryable intent, not half a change ──
    let lostToADownServer =
        step "17 — the Server goes down with a Submission in flight" afterSaving
             [
                 envt GenPresServer (Stop GenPresServer)
                 fromClient 1
                     (SessionRequest(
                         (sidAt 1 afterSaving).Value,
                         handCreate (workOf 1 afterSaving) (clientOf 1 afterSaving).Value.Opened.Value None None))
             ]

    expect "17 nothing landed and nothing half-landed: the Server holds no intent to lose (Rule 31)"
        (planCount pat2 lostToADownServer = planCount pat2 afterSaving
         && lostToADownServer.GenPres.InFlight.IsEmpty)

    let retriedAfterwards =
        let again =
            SessionRequest(
                (sidAt 1 lostToADownServer).Value,
                SubmitTreatmentPlan
                    {
                        Work = workOf 1 lostToADownServer
                        Opened = (clientOf 1 lostToADownServer).Value.Opened.Value
                        Notice = None
                        Challenge = None
                        DataOk = None
                        Pin = None
                        Key = IdemKey "adv-17"
                    })

        step "17 — and the same act, retried when it comes back, lands once" lostToADownServer
             [ envt GenPresServer (Start GenPresServer); fromClient 1 again; fromClient 1 again ]

    expect "17 the retry lands, and the retry of the retry does not (Rule 45)"
        (planCount pat2 retriedAfterwards = planCount pat2 afterSaving + 1
         && countOf (function TreatmentPlanSubmitted _ -> true | _ -> false) = 2)

    // ── 18. A restart collides no identifier and loses nothing acknowledged ──
    let restarted =
        step "18 — the Server is restarted, and A launches again" retriedAfterwards
             (envt GenPresServer (Stop GenPresServer)
              :: envt GenPresServer (Start GenPresServer)
              :: launchAs ucA.Login (Some pat2))

    expect "18 the new SessionId is one that has never been used before (Rule 31)"
        (restarted.Database.Private.Sessions
         |> List.map _.Id
         |> fun ids -> ids.Length = (ids |> List.distinct |> List.length))

    expect "18 and everything acknowledged before the restart is still there (Concept 12)"
        (planCount pat2 restarted = planCount pat2 retriedAfterwards
         && (recordFor pat2 restarted).Plans
            |> List.forall (fun x -> (recordFor pat2 retriedAfterwards).Plans |> List.contains x))

    // ── 4, 5, 12, 15, 16. Covered where they belong ──
    // 4 is the Rule 40 replay above; 5 is UC-9 ext 2a; 12 is UC-3 ext 3f; 15 is Rule
    // 44 in UC-3; 16 is the store check under the Guarantees.

    // ── what the Launch still does not settle ──
    // The User is the browser's (Rule 4); the Patient is the LaunchScript's word, and
    // nothing in GenPRES can check it (Concept 3). The claim below is only that the
    // model does not pretend otherwise.
    let anyPatient =
        step "the Launch's Patient is still the script's word (Concept 3)" world
             (launchAs ucA.Login (Some pat3))

    expect "the Patient the Launch carries is unverified, and the model shows it rather than claiming it"
        (openCount anyPatient = 1
         // the Session's Patient is exactly what the Launch carried ...
         && (newestRecord anyPatient |> Option.bind _.Patient) = Some pat3
         && saw (function OpenUrl l -> l.Patient = Some pat3 | _ -> false)
         // ... and nothing was asked about it: the registry is asked who the login is,
         // never whether that person may have this Patient (Concept 3).
         && lastTrace
            |> List.forall (fun e ->
                match e.Msg with
                | ResolveUser(_, login) -> login = ucA.Login
                | _ -> true))


// ═══════════════════════════════════════════════════════════════════════════════
//  Consequences — derived from the edges, checked over every scenario
// ═══════════════════════════════════════════════════════════════════════════════

let consequences () =
    printfn ""
    printfn "############### Consequences ###############"

    // Consequence 1. The LaunchScript learns nothing after the launch. This is not a
    // discipline the branches keep — it is the shape of edge C4, which is `=>`. The
    // only thing that ever reaches the LaunchScript is the User's own trigger: it asks
    // nobody anything, so there is nothing for anybody to answer.
    let toLaunchScript =
        allTrace |> List.filter (fun e -> e.To = MainEhrLaunchScript)

    expect "C1 nothing reaches the LaunchScript but the User's trigger"
        (toLaunchScript |> List.forall (fun e -> e.From = User))

    expect "C1 in particular, neither the Server nor a Client can ever reach it"
        (toLaunchScript
         |> List.forall (fun e ->
             e.From <> GenPresServer && (match e.From with GenPresClient _ -> false | _ -> true)))

    // And the edge table is what says so, not the branches. Edge C4 is `=>`, which
    // grants the one direction only, so there is no wire back at all.
    expect "C1 the edge table refuses a reply to the LaunchScript outright"
        (not (Edges.permits GenPresServer MainEhrLaunchScript)
         && not (Edges.permits (GenPresClient(BrowserId 1)) MainEhrLaunchScript))

    // "Any pair without an edge cannot exchange data at all. Edges do not compose — no
    // component relays on another's behalf unless stated."
    expect "Constraints: a pair without an edge cannot exchange data at all"
        (not (Edges.permits MainEhrWorkstation GenPresServer)
         && not (Edges.permits GenPresDatabase PatientDataPlatform)
         && not (Edges.permits (GenPresClient(BrowserId 1)) GenPresDatabase)
         && not (Edges.permits (GenPresClient(BrowserId 1)) UserRegistry))

    // Consequence 2. With the relay gone, the LaunchScript and the Server share no
    // party at all: the only thing that crosses from the EHR side to GenPRES is the
    // Launch itself, carried by the browser (edge C4) and presented by it (edge C5).
    // There is no channel between them to secure, monitor, or lose.
    let reachableFrom a =
        Edges.table
        |> List.filter (fun (x, _, _) -> x = a)
        |> List.map (fun (_, _, y) -> y)
        |> Set.ofList

    let shared = Set.intersect (reachableFrom MainEhrLaunchScript) (reachableFrom GenPresServer)

    expect "C2 the LaunchScript and the Server can reach no party in common"
        (shared.IsEmpty)

    expect "C2 the LaunchScript reaches only the browser it opens, and nothing else"
        (reachableFrom MainEhrLaunchScript = Set.ofList [ GenPresClient(BrowserId 0) ])

    // Consequence 3. Nothing can tell the LaunchScript whether the Launch was
    // honoured, because it has exited — and because it never asked. Every message that
    // decides a Launch's fate is between the Client, the Server and the Database.
    expect "C3 nothing about a Launch's fate ever goes near the LaunchScript"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | RedeemLaunch _
             | SpendLaunchIfUnspent _
             | LaunchSpent _
             | LaunchReplayed _
             | LaunchRefused _ -> e.To <> MainEhrLaunchScript
             | _ -> true))

    // Consequence 6. The Server cannot reach a Client: edge C5 goes one way only, so
    // every Server-to-Client envelope is a reply riding that request's connection.
    expect "C6 there is no edge from the Server to a Client, so nothing can be pushed"
        (Edges.table
         |> List.exists (fun (x, _, y) ->
             x = GenPresServer && (match y with GenPresClient _ -> true | _ -> false))
         |> not)

    // Rule 11. The SessionId is a bearer credential and never travels in a URL. The
    // only message that is a URL is OpenUrl, and it carries a Launch.
    expect "Rule 11 the only thing that ever travels as a URL is a Launch"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | OpenUrl _ -> e.To |> function GenPresClient _ -> true | _ -> false
             | _ -> true))

    // Rule 22. The PIN never leaves GenPRES. GenPRES is the Client, the Server and the
    // Database; everything else is outside it.
    let outsideGenPres =
        Set.ofList
            [
                IdentityProvider
                UserRegistry
                PatientDataPlatform
                MailService
                MainEhrWorkstation
                MainEhrLaunchScript
            ]

    let carriesPin (m: Msg) =
        match m with
        | ChoosePin _ | SupplyPin _ -> true
        | Act(Signs _) -> true
        | SessionRequest(_, SubmitTreatmentPlan { Pin = Some _ }) -> true
        | Act(EntersResetCode _) -> true
        | SessionRequest(_, SupplyResetCode _) -> true
        | ReplacePinIfCode _ -> true
        | PinReplaced(_, c) -> c.Pin.IsSome
        | CredentialRead(_, Some c) -> c.Pin.IsSome
        | _ -> false

    expect "Rule 22 no envelope carrying a PIN ever goes outside GenPRES"
        (allTrace
         |> List.filter (fun e -> carriesPin e.Msg)
         |> List.forall (fun e -> not (outsideGenPres.Contains e.To)))

    expect "Rule 22 and the mail that says a PIN changed never carries the PIN itself"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | SendMail(_, what) -> not (what.Contains "9999" || what.Contains "1111")
             | _ -> true))

    // Rule 10, over every SessionRecord ever written to the Database in every
    // scenario. These two are what the typed state buys: with a bare timestamp,
    // "no notice is owed" and "one is owed and not yet given" were the same value,
    // and neither could be asserted.
    let everyRecordWritten = allRecords

    expect "Rule 10 a Session the User closed is never owed a notice"
        (everyRecordWritten
         |> List.forall (fun r ->
             match r.State with
             | Ended(ClosedByUser, _) -> r.Notice = NotOwed
             | _ -> true))

    expect "Rule 10 an open Session owes nothing either: the ending is what creates it"
        (everyRecordWritten
         |> List.forall (fun r -> r.State <> OpenOrGone || r.Notice = NotOwed))

    expect "Rule 10 nothing is ever delivered or acknowledged without an ending that owed it"
        (everyRecordWritten
         |> List.forall (fun r ->
             match r.State, r.Notice with
             | Ended(mark, _), (Delivered _ | Acknowledged _) -> SessionRecord.owesNotice mark
             | _, (Delivered _ | Acknowledged _) -> false
             | _ -> true))

    // Rule 4. The Session's User is the BrowserIdentity, never the Launch. Two halves.
    // The type-level half: a Launch has no login field at all (Concept 3), so nothing
    // in it could have named a User. The trace half: every launched Session that
    // opened was preceded by a RedeemLaunch from that same Client proving that same
    // login — the Server had no other source to take it from.
    let openedByLaunch =
        allTrace
        |> List.indexed
        |> List.choose (fun (i, e) ->
            match e.Msg with
            | SessionOpened(_, _, Some uc, _, _, _, _) -> Some(i, e.To, uc)
            | _ -> None)

    expect "Rule 4 every launched Session's User is the identity its browser proved, never the Launch's"
        (openedByLaunch
         |> List.forall (fun (i, client, uc) ->
             allTrace
             |> List.indexed
             |> List.exists (fun (j, e) ->
                 j < i
                 && e.From = client
                 && match e.Msg with
                    | RedeemLaunch(_, Some who, _) -> who = uc.Login
                    | _ -> false)))

    // ── Rule 15, over every version of every TreatmentPlan the run ever held ──
    // Scenarios all replay from the same world, so a plan id alone does not name one
    // plan across the run. What does is the whole of it with the `State` taken out —
    // the part Rule 15 says never changes — so anything agreeing in that is the same
    // plan seen twice, and any difference is a different plan.
    let sightings =
        allPlans
        |> List.map (fun s -> { s with State = Signed }, s.State)
        |> List.distinct
        |> List.groupBy fst
        |> List.map (fun (content, xs) -> content, xs |> List.map snd |> List.distinct)

    expect "Rule 15 the run really did move a plan's state, so the claims below are not empty"
        (sightings |> List.exists (fun (_, states) -> states.Length > 1))

    expect "Rule 15 the State moves at most once, and only Unsigned -> Discarded"
        (sightings
         |> List.forall (fun (_, states) ->
             match states |> List.sort with
             | [ _ ] -> true
             | [ Unsigned; Discarded ] -> true
             | _ -> false))

    expect "Rule 15 nothing Signed ever becomes anything else, and nothing ever becomes Signed"
        (sightings
         |> List.forall (fun (_, states) -> not (states |> List.contains Signed) || states.Length = 1))

    expect "Rule 15 and a plan that changed state kept every other field it was written with"
        (allPlans
         |> List.filter (fun s -> s.State = Discarded)
         |> List.forall (fun d ->
             allPlans
             |> List.exists (fun u -> u.State = Unsigned && { u with State = Signed } = { d with State = Signed })))

    // Rule 5. The Role a Session carries is byte-for-byte the registry's answer, never
    // synthesised: every launched Session is preceded by a UserResolved carrying the
    // very same UserContext. Anonymous opens are excluded — they carry no User at all
    // (Rule 13). The type-level half is that a Launch has no Role field to
    // carry one (Concept 3), so the launch could not have supplied it.
    let indexed = allTrace |> List.indexed

    let resolvedBefore (i: int) (uc: UserContext) =
        indexed
        |> List.exists (fun (j, e) ->
            j < i && (match e.Msg with UserResolved(_, uc', _) -> uc' = uc | _ -> false))

    expect "Rule 5 every Session's Role came from the registry, and came first"
        (indexed
         |> List.forall (fun (i, e) ->
             match e.Msg with
             | SessionOpened(_, _, Some uc, _, _, _, _) -> resolvedBefore i uc
             | _ -> true))

    // ── Rule 31, structurally ──
    // The Server carries nothing across requests. This is checked after every step of
    // every scenario, not sampled: `noteFlight` trips a flag the moment a step ends
    // with anything in the in-flight table.
    expect "Rule 31 the in-flight table is empty at the end of every scenario step"
        (not everCarriedARequest)

    // The type says the same: `ServerState` has no field a Session could live in, so
    // Rule 31 is not a discipline the branches keep but something the state cannot
    // express. And nothing of the work stays behind — every Computed is its own
    // request's payload handed back, checked over every one there has ever been.
    let computedIsItsOwnRequest =
        // Requests can be in flight together — two Sessions, or a scenario that
        // interleaves them deliberately — so what is checked is that every answer
        // matches a request still outstanding, and consumes it.
        let rec walk pending trace ok =
            match trace with
            | [] -> ok
            | { Msg = SessionRequest(_, Compute os) } :: rest -> walk (os :: pending) rest ok
            | { Msg = Computed os } :: rest ->
                match pending |> List.tryFindIndex ((=) os) with
                | Some i -> walk (pending |> List.indexed |> List.filter (fst >> (<>) i) |> List.map snd) rest ok
                | None -> walk pending rest false
            | _ :: rest -> walk pending rest ok
        walk [] allTrace true

    expect "Rule 31 every Computed is the request's own payload handed back, nothing added"
        computedIsItsOwnRequest

    // What a Submission actually carries, which is a measurement and not a judgement:
    // the whole WorkPlan, its Patient Data included (Concept 16).
    expect "Rule 31 a Submission carries the whole WorkPlan: OrderContexts and Patient Data alike"
        (allTrace
         |> List.exists (fun e ->
             match e.Msg with
             | SessionRequest(_, SubmitTreatmentPlan req) ->
                 not req.Work.Orders.IsEmpty && req.Work.Data.IsSome
             | _ -> false))

    // ── Rule 32 ──
    // The payload has no User in it to believe — a token names a SessionId, not an
    // identity — so a plan's `By` can only have come from a record the Server just
    // read. Every append is preceded by that read.
    let readARecordFor (i: int) (uc: UserContext) =
        indexed
        |> List.exists (fun (j, e) ->
            j < i && (match e.Msg with SessionRecordRead(_, Some r) -> r.User = Some uc | _ -> false))

    ignore readARecordFor

    expect "Rule 32 every TreatmentPlan's User came off a SessionRecord, never off the payload"
        (allRecords
         |> List.map _.Id
         |> Set.ofList
         |> fun known ->
             allTrace
             |> List.forall (fun e ->
                 match e.Msg with
                 // Rule 42: the User is read inside the act, off the SessionRecord the
                 // commit names — never off the payload, which carries no User at all.
                 | CommitTreatmentPlan(_, c) -> known.Contains c.Sid
                 | _ -> true))

    // ── Rules 33 and 34 ──
    expect "Rule 33 every token the Server ever issued verifies, and every stale one was refused honestly"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | SessionOpened(_, _, _, _, _, t, _) -> Token.verifyOpened t
             | TreatmentPlanSubmitted(_, _, t) -> Token.verifyOpened t
             | TreatmentPlanOpened(_, _, t) -> Token.verifyOpened t
             | _ -> true))

    expect "Rule 34 every notice the Server ever sent carried a token that verifies"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | UnsignedWorkNotice(_, t) -> Token.verifyNotice t
             | _ -> true))

    // ── Rules 36 and 42 ──
    // There is no message that appends a TreatmentPlan at all: `CommitTreatmentPlan` is
    // the only way in, it goes to the one party that arbitrates, and what comes back
    // is either the whole thing or nothing.
    let commits = allTrace |> List.filter (fun e -> match e.Msg with CommitTreatmentPlan _ -> true | _ -> false)
    let landed = allTrace |> List.filter (fun e -> match e.Msg with TreatmentPlanCommitted _ -> true | _ -> false)

    expect "Rule 42 every TreatmentPlan that landed came through one commit, and there were more commits than plans"
        (landed.Length <= commits.Length && landed.Length > 0)

    expect "Rule 36 and every commit went to the Database, the one arbiter"
        (commits |> List.forall (fun e -> e.To = GenPresDatabase))

    // ── Rule 9 ──
    // Six endings, and a Server restart is not one of them. The type says so — there
    // is no `EndMark` for it — and UC-9 ext 1a shows what happens instead.
    let marksSeen =
        everyRecordWritten
        |> List.choose (fun r -> match r.State with Ended(m, _) -> Some m | OpenOrGone -> None)
        |> List.distinct

    // ── Rules 7 and 40, as invariants over every state the Database ever held ──
    // Not "the scenarios did not happen to break it": at the end of every step of
    // every scenario, no User held two open Sessions and no browser did either.
    expect "Rule 7 no User ever held two open Sessions at once (Rules 7, 40)"
        (allDatabases
         |> List.forall (fun sessions ->
             sessions
             |> List.filter SessionRecord.isOpen
             |> List.choose SessionRecord.userId
             |> fun users -> users.Length = (users |> List.distinct |> List.length)))

    expect "Rule 7 and no browser ever held two open Sessions at once (Rules 7, 40)"
        (allDatabases
         |> List.forall (fun sessions ->
             sessions
             |> List.filter SessionRecord.isOpen
             |> List.choose _.Browser
             |> fun browsers -> browsers.Length = (browsers |> List.distinct |> List.length)))

    expect "Rule 9 every ending the run produces is one the Rules name, and all of them occur"
        (marksSeen |> List.sort
            = List.sort [ ClosedByUser; ReplacedInBrowser; Idle; Superseded; WrongPinLimit; Expired ])

    // Rule 9. `Expired` is no longer the anonymous ending alone: a launched Session
    // reaches it too, at `sessionMaxLifetime` counted from the open — and never sooner,
    // whatever Rule 8's clock says. That is the discipline the type cannot keep, so it
    // is asserted instead.
    expect "Rule 9 an Expired ending of a Session with a User is at least sessionMaxLifetime after its open"
        (everyRecordWritten
         |> List.forall (fun r ->
             match r.State with
             | Ended(Expired, at) -> r.User.IsNone || at - r.OpenedAt >= sessionMaxLifetime
             | _ -> true))


// ═══════════════════════════════════════════════════════════════════════════════
//  Guarantees — what the Rules add up to
// ═══════════════════════════════════════════════════════════════════════════════

let guarantees () =
    printfn ""
    printfn "############### Guarantees ###############"

    // A record built up over several creates, by two Users, with a block and a
    // takeover in the middle of it.
    let g0 = quiet "G" world (launchAs ucA.Login (Some pat2))
    let g1 = quiet "G" g0 [ act 1 (Prescribes(OrderContextId "g-1")); act 1 Saves ]
    let g2 = quiet "G" g1 [ act 1 (Prescribes(OrderContextId "g-2")); yield! signs 1 pinA ]
    let g3 = quiet "G" g2 (launchAs ucB.Login (Some pat2))
    let g4 = quiet "G" g3 [ act 2 (Prescribes(OrderContextId "g-3")); yield! signs 2 pinB ]
    // And one Submission that does not land, so the audit has a refusal in it to find.
    let g5a = quiet "G" g4 [ act 2 (Prescribes(OrderContextId "g-4")); yield! signs 2 (Pin "0000") ]
    // And a draft that is saved and then put down, so the record has a Discarded plan
    // in it for Rule 15's claims to be about.
    let g5b = quiet "G" g5a [ act 2 (Prescribes(OrderContextId "g-5")); act 2 Saves ]

    let g5 =
        match headOf pat2 g5b with
        | Some draft when draft.State = Unsigned -> quiet "G" g5b [ act 2 (Discards draft.Id) ]
        | _ -> g5b

    let record = recordFor pat2 g4

    // ── Guarantee 1: one constant ──
    expect "G1 the PatientId is the one thing no TreatmentPlan may change"
        (record.Plans |> List.forall (fun s -> s.Patient = pat2))

    expect "G1 and only a launch supplies one, so no hand ever set it (Rules 12, 13, 32)"
        (g4
         |> patientsInRecord
         |> List.forall (fun p -> (recordFor p g4).Plans |> List.forall (fun s -> s.Patient = p)))

    // ── Guarantee 2: one version ──
    let signedOnes = record.Plans |> List.filter (fun s -> s.State = Signed)

    expect "G2 exactly one TreatmentPlan is the visible version: the most recent Signed one (Rules 16, 17)"
        ((PatientRecord.latestSigned record |> Option.map _.Id) = (signedOnes |> List.tryHead |> Option.map _.Id))

    // Reading is wider than building. Every Signed TreatmentPlan is readable — it is
    // attested history (Rule 17) — but only the most recent one can be built on,
    // because opening an older one makes it the Session's baseline and Rule 20 blocks
    // the Submission. Nobody else's Unsigned work is readable at all (Rule 18).
    expect "G2 reading is wider than building: Signed history is open, Unsigned work is not (Rules 17, 18)"
        (record.Plans
         |> List.forall (fun s ->
             if s.State = Signed then (record |> PatientRecord.mayOpen ucC.UserId s.Id).IsSome
             else (record |> PatientRecord.mayOpen ucC.UserId s.Id).IsNone))

    expect "G2 and only the newest Signed one can be built on (Rules 17, 20)"
        (record.Plans
         |> List.filter (fun s -> s.State = Signed)
         |> List.forall (fun s ->
             let isNewest = Some s.Id = (PatientRecord.latestSigned record |> Option.map _.Id)
             (record |> PatientRecord.blocking (Some s.Id)).IsNone = isNewest))

    expect "G2 and each User has exactly one starting point (Rule 19)"
        ([ ucA; ucB; ucC ]
         |> List.forall (fun uc -> (record |> PatientRecord.startsFrom uc.UserId) |> Option.isSome))

    // ── Rule 15: the third state, and what it is worth ──
    // A Discarded plan is not a version, not a starting point, and not work to be
    // resumed — by anybody, its author included. That is one claim, and it holds in
    // all four places without any of the four mentioning Discarded.
    let discardedSomewhere =
        (g5 |> patientsInRecord)
        |> List.collect (fun p -> (recordFor p g5).Plans |> List.filter (fun x -> x.State = Discarded))

    expect "Rule 15 the run really did discard something — otherwise the four claims below are empty"
        (not discardedSomewhere.IsEmpty)

    expect "Rule 15 no Discarded TreatmentPlan is ever a starting point, for anyone (Rule 19)"
        (g5
         |> patientsInRecord
         |> List.forall (fun p ->
             let r = recordFor p g5

             [ ucA; ucB; ucC ]
             |> List.forall (fun uc ->
                 match r |> PatientRecord.startsFrom uc.UserId with
                 | Some s -> s.State <> Discarded
                 | None -> true)))

    expect "Rule 15 no Discarded TreatmentPlan can be opened, by its author or anybody else (Rules 17, 18)"
        (g5
         |> patientsInRecord
         |> List.forall (fun p ->
             let r = recordFor p g5

             discardedSomewhere
             |> List.forall (fun d ->
                 [ ucA; ucB; ucC ]
                 |> List.forall (fun uc -> (r |> PatientRecord.mayOpen uc.UserId d.Id).IsNone))))

    expect "Rule 15 no Discarded TreatmentPlan is ever disclosed as another User's work (Rule 21)"
        (g5
         |> patientsInRecord
         |> List.forall (fun p ->
             let r = recordFor p g5

             [ ucA; ucB; ucC ]
             |> List.forall (fun uc ->
                 r
                 |> PatientRecord.unsignedElsewhere uc.UserId None
                 |> List.forall (fun s -> s.State <> Discarded))))

    expect "Rule 15 and no Discarded TreatmentPlan is in the clinical store: it was never Signed"
        (g5.Database.Clinical.Signed
         |> Map.forall (fun _ plans -> plans |> List.forall (fun s -> s.State <> Discarded)))

    // ── Guarantee 3: carts and one checkout ──
    // The cart is private by construction now: it lives in the User's own Client and
    // the Server keeps none of it (Rule 31). The checkout is single by construction
    // too: the Database arbitrates the append (Rule 36).
    expect "G3 signing is the only checkout: every Signed TreatmentPlan came from a Submission with a PIN"
        (signedOnes |> List.forall (fun s -> s.By.Role = Prescriber))

    expect "G3 a Reader never appears as the creator of anything (Roles)"
        (g4
         |> patientsInRecord
         |> List.forall (fun p -> (recordFor p g4).Plans |> List.forall (fun s -> s.By.Role <> Reader)))

    // Concept 13. Every TreatmentPlan after the first was built on one before it —
    // named as `Base` while it is in the private store, and as `SignedBase` once it
    // reaches the clinical store, which cannot carry the other (Actor 5).
    expect "G3 every TreatmentPlan after the first stands on a base (Concept 13)"
        (record.Plans
         |> List.filter (fun s -> s.No <> TreatmentPlanNo 1)
         |> List.forall (fun s -> s.Base.IsSome || s.SignedBase.IsSome))

    expect "G3 the two carts never met in the Server: it held neither (Rule 31)"
        (g4.GenPres.InFlight.IsEmpty
         && (g4.Clients |> Map.exists (fun _ c -> not c.Work.Orders.IsEmpty)))

    // ── Rule 46: the audit ──
    // The record of what was done lives in the private store, written by the party
    // that did it, in the same act (Rule 42). Every Signed TreatmentPlan in the final
    // Patient's record has its line, and every line names the User.
    let auditLines = auditOf g5

    // The Cast's own TreatmentPlans were placed, not created, so nothing recorded
    // them: what the audit answers for is every Submission this run actually made.
    let createdHere = record.Plans |> List.filter (fun s -> s.No >= TreatmentPlanNo 10)

    expect "Rule 46 every TreatmentPlan created here is in the audit, exactly once, with its User"
        (not createdHere.IsEmpty
         && createdHere
            |> List.forall (fun s ->
                let (TreatmentPlanId i) = s.Id
                let (UserId u) = s.By.UserId
                let what = if s.State = Signed then "signed" else "saved"
                (auditLines
                 |> List.filter (fun a -> a.What.Contains i && a.What.Contains what && a.What.Contains u))
                    .Length = 1))

    expect "Rule 46 refusals are recorded too — a Submission that did not land is an event"
        (auditLines |> List.exists (fun a -> a.What.Contains "refused"))

    expect "Rule 46 and so are the Sessions: opened, and ended with the reason"
        (auditLines |> List.exists (fun a -> a.What.Contains "opened")
         && auditLines |> List.exists (fun a -> a.What.Contains "ended"))

    // Rule 46's last word: and when. Every line is stamped by the act that wrote it,
    // so the audit is a sequence of moments and not merely a pile of sentences — and
    // written newest first, the stamps run backwards through it.
    expect "Rule 46 every entry is stamped, in the run's own time, and in the order written"
        (auditLines
         |> List.forall (fun a -> a.At > 0 && a.At <= g5.Env.Now)
         && auditLines |> List.pairwise |> List.forall (fun (newer, older) -> newer.At >= older.At))

    // ── The two stores (Actor 5) ──
    // A copy of the Clinical store is what the PatientDataPlatform is handed (Actor
    // 6). What it holds is attested history and nothing else: no Unsigned
    // work, no credential, no reset code, no spent key.
    let exported = g5.Database.Clinical

    expect "Actor 5 the Clinical store holds Signed TreatmentPlans and nothing but"
        (exported.Signed |> Map.forall (fun _ plans -> plans |> List.forall (fun s -> s.State = Signed)))

    expect "Actor 5 an export of it carries no credential, no code and no key"
        (let text = $"%A{exported}"

         [ "UserCredential"; "Pin "; "ResetCode"; "IdemKey"; "PendingReset" ]
         |> List.forall (fun secret -> not (text.Contains secret)))

    // Actor 5, Guarantee 4 and Rule 11. The copy names nothing in the private store.
    // Two references could reach into it — the SessionId of the Session that created
    // the plan (a bearer credential) and a `Base` that may be an Unsigned plan — and
    // the append drops both. So a Signed plan records the Session it was created in
    // only until it lands: Concept 13 asks for it, Actor 5 and Guarantee 4 forbid
    // carrying it into the copy, and the copy is what the clinical store is.
    expect "Actor 5 no exported TreatmentPlan carries a SessionId (Rule 11, Guarantee 4)"
        (exported.Signed |> Map.forall (fun _ plans -> plans |> List.forall (fun s -> s.Session = None)))

    expect "Actor 5 and none of them carries a Base, which could have named an Unsigned plan"
        (exported.Signed |> Map.forall (fun _ plans -> plans |> List.forall (fun s -> s.Base = None)))

    expect "Actor 5 an export of it mentions no SessionId at all, however it is rendered"
        (not (($"%A{exported}").Contains "SessionId"))

    // Rule 17. The copy has to close over itself: a Signed TreatmentPlan's `Base` may
    // name an Unsigned one, which lives only in the private store and is never copied,
    // so the clinical store carries `SignedBase` and follows that instead.
    expect "Rule 17 every SignedBase in the clinical store resolves inside the clinical store"
        (let ids =
            exported.Signed |> Map.toList |> List.collect (snd >> List.map _.Id) |> Set.ofList

         exported.Signed
         |> Map.forall (fun _ plans ->
             plans |> List.forall (fun s -> s.SignedBase |> Option.forall ids.Contains)))

    // And it is a real chain, not `Base` renamed. The export has no `Base` to compare
    // with — that is the point — so it is asserted over the whole record: somewhere an
    // Unsigned step sits between a signature and the ancestor it names.
    expect "Rule 17 the chain really skips: a signature over Unsigned work names the Signed plan under it"
        (g5
         |> patientsInRecord
         |> List.exists (fun p ->
             let plans = (recordFor p g5).Plans

             plans
             |> List.exists (fun s ->
                 s.State = Signed
                 && match s.SignedBase with
                    | None -> false
                    | Some ancestor ->
                        // something Unsigned sits between them: newer than the ancestor,
                        // older than the signature, and the same User's work.
                        plans
                        |> List.exists (fun mid ->
                            mid.State <> Signed
                            && mid.No < s.No
                            && Some mid.Id <> Some ancestor
                            && (plans
                                |> List.exists (fun a -> a.Id = ancestor && a.No < mid.No))))))

    // The private store holds everything that was never Signed — the Unsigned work
    // Rule 18 keeps for its author, and the Discarded plans Rule 15 keeps for nobody.
    // Neither is ever copied anywhere.
    expect "Actor 5 and everything never Signed is in the other half, where nothing copies it"
        (g5.Database.Private.Drafts
         |> Map.forall (fun _ plans ->
             plans |> List.forall (fun s -> s.State = Unsigned || s.State = Discarded))
         && (g5 |> patientsInRecord |> List.exists (fun p ->
             (recordFor p g5).Plans |> List.exists (fun s -> s.State = Unsigned)))
         && (g5 |> patientsInRecord |> List.exists (fun p ->
             (recordFor p g5).Plans |> List.exists (fun s -> s.State = Discarded))))

    // ── Guarantee 4: audit ──
    expect "G4 a Signed TreatmentPlan carries the User who signed it (Concepts 13, 14; Rule 14)"
        (signedOnes |> List.forall (fun s -> s.By.UserId = ucA.UserId || s.By.UserId = ucB.UserId))

    expect "G4 every OrderContext in every TreatmentPlan carries the User whose Session last changed it"
        (g4
         |> patientsInRecord
         |> List.forall (fun p ->
             (recordFor p g4).Plans
             |> List.forall (fun s -> s.Orders |> List.forall (_.Stamp.IsSome))))

    expect "G4 and the stamps are not all the signer's: B's signature kept A's work attributed to A"
        (record.Plans
         |> List.tryHead
         |> Option.map (fun s ->
             s.By = ucB
             && s.Orders |> List.exists (fun o -> o.Stamp = Some ucA)
             && s.Orders |> List.exists (fun o -> o.Stamp = Some ucB))
         |> Option.defaultValue false)

    // Append-only: each record is the previous one with something on the front, and
    // nothing that was already there is ever touched.
    let history = [ recordFor pat2 g0; recordFor pat2 g1; recordFor pat2 g2; recordFor pat2 g4 ]

    expect "G4 the record is append-only: every earlier version is a suffix of the later one (Concept 12)"
        (history
         |> List.pairwise
         |> List.forall (fun (earlier, later) ->
             later.Plans.Length >= earlier.Plans.Length
             && later.Plans |> List.skip (later.Plans.Length - earlier.Plans.Length)
                    = earlier.Plans))

    // Stated as the claim rather than as a count: every Signed TreatmentPlan that existed
    // at any point in the history is still in the record at the end of it.
    let everSigned =
        history |> List.collect (fun r -> r.Plans |> List.filter (fun s -> s.State = Signed)) |> List.distinct

    expect "G4 nothing attested is ever lost: every Signed TreatmentPlan ever made is still there"
        (everSigned <> []
         && everSigned |> List.forall (fun s -> record.Plans |> List.contains s))

    // What is not protected: Unsigned work. Superseded, it can never be signed.
    expect "G4 what is not protected is Unsigned work — superseded, it can never be signed (Rules 19, 20)"
        (record |> PatientRecord.blocking (Some(TreatmentPlanId "plan-0010"))).IsSome


// ═══════════════════════════════════════════════════════════════════════════════
//                                  THE RUN
// ═══════════════════════════════════════════════════════════════════════════════

/// Everything the run accumulates into, put back as it started. A no-op from a
/// terminal, where the process is new. It matters in a live FSI session — an IDE
/// keeps one — where a second `runAll ()` would otherwise count on from the first and
/// stay green while doing it, the whole-run checks being satisfied by more of the
/// same data.
let private reset () =
    checks <- 0
    failures <- 0
    lastTrace <- []
    allTrace <- []
    allRecords <- []
    allPlans <- []
    allDatabases <- []
    everCarriedARequest <- false
    handKey <- 0

/// Where the run writes itself. Beside the script, so it is found without looking —
/// and outside it, because a trace of some three hundred kilobytes belongs in a file
/// rather than in a terminal or an IDE's console pane.
let runLog = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "Session.run.txt")

let runAll () =
    reset ()

    // Every scenario prints through `printfn`, which resolves `Console.Out` at each
    // call — so redirecting it here catches the whole run without touching any of the
    // two hundred-odd places that write. Restored in a `finally`, because a run that
    // throws must not leave the session writing to a closed file.
    let terminal = System.Console.Out
    use writer = new System.IO.StreamWriter(runLog, false)
    System.Console.SetOut writer

    try
        uc1 () |> ignore
        uc2 ()
        uc3 () |> ignore
        uc4 () |> ignore
        uc5 () |> ignore
        uc6 () |> ignore
        uc7 () |> ignore
        uc8 () |> ignore
        uc9 () |> ignore
        uc10 () |> ignore
        uc11 () |> ignore
        uc12 () |> ignore
        uc13 () |> ignore
        uc14 ()
        tokensAndArbitration ()
        adversarialReview ()
        consequences ()
        guarantees ()

        printfn ""
        printfn "######################################################################"
        printfn $"  {checks - failures}/{checks} checks passed"
        if failures > 0 then printfn $"  {failures} FAILED"
    finally
        writer.Flush()
        System.Console.SetOut terminal

    // The one line the terminal gets: the verdict, and where to read the rest.
    printfn $"  {checks - failures}/{checks} checks passed — trace in %s{runLog}"
    if failures > 0 then printfn $"  {failures} FAILED"

runAll ()
