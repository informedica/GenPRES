// ═══════════════════════════════════════════════════════════════════════════════
//   GenPRES – MainEHR Integration: the system model, executable
// ═══════════════════════════════════════════════════════════════════════════════
//
//     dotnet fsi Integration.fsx
//
// Standalone — no #load, no #r. It prints a trace per scenario and ends with a count
// of self-checks.
//
// ═══════════════════════════════════════════════════════════════════════════════
//   SECTION 0 — THE SYSTEM MODEL
// ═══════════════════════════════════════════════════════════════════════════════
//
// Actors, Roles, Concepts, Constraints, Consequences, Invariants, Possibilities,
// Rules, Guarantees, Open Questions. Everything below cites these by number.
//
// ── Actors ─────────────────────────────────────────────────────────────────────
// Who takes part. [ours] = under construction. [given] = not ours to change. The User
// is neither: they are who the system is for.
//
//  1. MainEHR Workstation  [given]  the running EHR client.
//  2. MainEHR LaunchScript [ours]   a script behind a button there: reads a key, seals a Launch, opens
//                                   the browser, exits. All its scripting allows.
//  3. GenPRES Client       [ours]   the UI, in a browser carrying the User's hospital sign-on. Two parts:
//                                   a thin launch shell, then the full Client once a Session exists.
//  4. GenPRES Server       [ours]   the backend.
//  5. GenPRES Database     [ours]   two stores, one writer: a clinical store that is copied, a private
//                                   store that is not. Append-only in the document; see the note below
//                                   for what this model does instead.
//  6. PatientDataPlatform  [given]  a shared read-only copy of MainEHR's, GenPRES's and other databases.
//  7. User                          the person who uses MainEHR and GenPRES.
//  8. IdentityProvider     [given]  says who is at a browser. No Role, no Patient.
//  9. UserRegistry         [given]  says who a login is, what they may do, how to mail them, and which
//                                   Patient they have active in MainEHR right now.
// 10. MailService          [given]  sends mail, outside GenPRES and MainEHR both.
//
// ── Roles ──────────────────────────────────────────────────────────────────────
// What authority a User holds. The UserRegistry decides it; MainEHR and GenPRES
// enforce it separately, each within itself.
//
//  1. Prescriber  may read, and may create TreatmentPlans.
//  2. Reader      may prescribe within a Session like anyone, but may sign none of it.
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
//  7. GenPRES UserCredential  one User's optional PIN and wrong-entry count. No Role, no identity of its
//                             own.
//  8. GenPRES Session         a User's dealings with GenPRES, for a Patient or for none; anonymous
//                             without a launch.
//  9. GenPRES SessionRecord   what binds a SessionId to a User and a Patient, and whether it is open.
//                             Holds the last address the registry gave, for Rule 27's fallback.
//                             Kept after it ends.
// 10. OrderContext            a PatientContext with its OrderScenarios, keeping its identity across plans
//                             and carrying the stamp of whoever last changed it.
// 11. OrderScenario           one proposed Order with the prescribing information that gives it meaning.
// 12. GenPRES PatientRecord   a Patient's append-only history in GenPRES: a sequence of TreatmentPlans,
//                             every one of them signed.
// 13. TreatmentPlan           the plan as it stood when signed: orders, author, Session, base, the Patient
//                             Data it was built on, and the rule set it was checked under.
// 14. Submission              submitting is signing. It carries the Session User's PIN, and it is the only
//                             way a TreatmentPlan comes into being.
// 15. Prescribing             changing the WorkPlan within a Session; nothing reaches the record until a
//                             TreatmentPlan is signed.
// 16. WorkPlan                the plan under the User's hands in the Client. It dies with the browser
//                             unless it is signed.
// 17. Token                   a note the Server signs and the Client returns unaltered: the OpenedToken,
//                             the SigningChallenge and the DataNoticeToken.
// 18. KnowledgeRuleSet        the published, versioned set of dose rules and their kin that GenPRES
//                             computes with. Every version is kept.
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
//     whoever holds it can name any Patient. The User it cannot name (Rule 4), and the Patient it names
//     must still be the User's own active one (Rule 6).
//  3. Only the Server knows whether a Launch was used, and it cannot tell the LaunchScript.
//  4. The Launch travels in a URL, so it lands in history and logs: hence single use, short life.
//  5. Workstation, LaunchScript and browser all run on the User's PC, which therefore needs every
//     [given] actor it talks to — and the key.
//  6. The Server cannot reach a Client, so a Client learns anything only at its next request.
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
//      - The spent-mark is written in the same act that opens the Session (Rule 40), so a launch cannot
//        spend a nonce and then fail to open.
//      - A launch may check first, but that check is a read: it can refuse early, and it cannot decide.
//   3. A Launch is accepted only within its lifetime.
//   4. The Session's User is the BrowserIdentity, never anything the Launch says.
//   5. The Server takes the Role from the UserRegistry at every launch, never from the Launch.
//   6. The Server asks the registry, at every launch, which Patient the User has active in MainEHR, and
//      opens the Session only for that Patient.
//   7. A launch that cannot be honoured opens no Session; at most the Client offers an anonymous one.
//      - A missing PIN is not that: it suspends the launch into enrolment (Rule 25, UC-2) rather than
//        refusing it. Only an enrolment abandoned or failed leaves no Session.
//
// Session
//   8. A User has at most one open Session, and so has a browser; opening one closes the rest.
//   9. Every request from the Client refreshes its Session's idle clock.
//  10. A Session ends by being closed, idling out, its absolute lifetime, the wrong-PIN limit, or being
//      replaced by the same User or browser.
//  11. An ending the User did not cause is told at their next launch and acknowledged there, once; a
//      Client still holding the SessionId is only refused, with the reason.
//  12. The SessionId rides in an HttpOnly, Secure, SameSite=Strict cookie, and a changing request needs
//      GenPRES's own Origin.
//  13. A Session without a PatientId may prescribe, but may open or create no TreatmentPlan.
//  14. A launchless Session is anonymous — no User, no Role, no Patient — capped in number and ended by
//      an absolute limit.
//
// Record
//  15. Every TreatmentPlan is created under one User's credentials, and every OrderContext changed in
//      the Session carries that stamp.
//  16. A TreatmentPlan never changes: it is corrected only by a newer one whose base it is, and every
//      later version of GenPRES must still open it.
//  17. Only the most recent TreatmentPlan counts clinically.
//  18. Every TreatmentPlan is open to read, but only the most recent can be built on.
//  19. A User starts from the most recent TreatmentPlan; where none exists, from nothing.
//  20. A User may submit unless a TreatmentPlan newer than the one they opened with exists; opening that
//      one lifts the block.
//
// Notification
//  21. With every response the Server compares the record's head against the plan the request's
//      OpenedToken names, and says so if a newer one exists: whose it is, and when it was signed.
//  22. The notice informs and gates nothing — no acknowledgement, no token. Rule 20 is the only guard.
//
// Signing
//  23. The Server alone verifies a UserCredential; the PIN never leaves GenPRES.
//  24. Every launch checks whether a PIN is set for the login.
//  25. A Prescriber with no PIN sets one before the launch goes on, and only once the registry knows them.
//  26. A Reader is never asked for a PIN: they never create a TreatmentPlan.
//  27. Every PIN set or replaced is mailed to the User and recorded — as is reaching the wrong-PIN limit.
//      - The address comes from the UserRegistry, freshly, on the request that sends the mail.
//      - The audit names the address the mail went to.
//      - A notice may fall back on the last address the registry gave; a confirmation code never does.
//  28. Wrong PIN entries count per credential across Sessions; at the limit the Session ends and signing
//      locks for a delay that doubles with each further guess and decays with time.
//
// Configuration
//  29. A Launch lives long enough for one launch: a page load, the identity round trip, a retry or two.
//  30. A Session spans a clinician's pauses and no more than a shift; the Client sends nothing unprompted.
//  31. The wrong-PIN limit forgives mistyping and no more; the PIN is memorable, its space large.
//
// State — where Session state lives, chosen so the Server keeps none of it.
//  32. The Server holds no Session state between requests: the WorkPlan is the Client's, the standing is
//      the SessionRecord's.
//  33. The Server takes a request's User and Patient from the SessionRecord, never from the request.
//  34. The plan a Session opened with travels as the OpenedToken, with every request, and is spent by the
//      Submission that lands and re-issued over the new baseline.
//  35. Stamps are computed by the Server against the base plan; one arriving from the Client is refused.
//  36. The Rule 20 check and the append are one act at the Database, which is what arbitrates between
//      Servers.
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
//  44. Before the challenge the Patient Data is re-read and the current KnowledgeRuleSet is taken; changed
//      or unreadable data means no challenge until the User accepts it, and the signed plan records both.
//  45. A changing request can take effect only once: the Submission by an idempotency key, where a retry
//      returns the first result, and every other change by a conditional write answered from the state.
//
// Audit — the record of the acts around the record.
//  46. The Server appends every launch, opening, ending, Submission, signature, PIN change and refusal
//      to the audit, anonymous refusals by count.
//
// ── Guarantees ─────────────────────────────────────────────────────────────────
// What the Rules add up to. Derived, not asserted, and checked at the end of the run.
//
//  1. One constant. In a PatientRecord the PatientId is the only thing that never changes, and no hand
//     ever sets it (Rules 13, 14).
//  2. One version. Exactly one plan is the visible version and the only place to build from: the most
//     recent (Rules 17-20). Reading is wider: the whole history is open (Rule 18).
//  3. Carts and one checkout. Each User has a private cart in their own Client, and signing is the only
//     checkout: the first to sign wins, and every other cart is rebuilt on top (Rules 19, 20, 32, 36).
//  4. Audit. The record keeps every version with its author and its base, the clinical store copy names
//     nothing private, and the security audit stands beside it (Rules 15, 46; Actors 5, 6). What a
//     signature attests is a person at the launch and a credential holder at the signature — per
//     credential, not per person. Non-repudiation is not claimed (Open Question 3).
//  5. A stolen Launch steals no authority. Whoever presents one is identified as themselves (Rule 4),
//     gets their own Role (Rule 5), and gets only the Patient they themselves have active (Rule 6). The
//     thief gains nothing; the victim loses a spent Launch and one relaunch.
//
// ── Open Questions ─────────────────────────────────────────────────────────────
// Decisions not yet made. Listed because they explain why parts of the design are
// shaped as they are; none of them is modelled here.
//
//  1. Mail deliverability. Rules 27 and 37 hold only if the registry's address is current and the
//     MailService delivers. Neither can be checked from here.
//  2. Payload. Under Rule 32 the whole WorkPlan travels with every request. Whether that is acceptable
//     is a measurement, not a judgement.
//  3. Step-up signing. If the hospital's sign-on can re-authenticate for a single action, the PIN goes
//     and non-repudiation can be claimed. This is what Rules 23-28 and 37 are waiting on.
//  4. Finer patient authorisation. Rule 6 says this User has this Patient open in MainEHR right now.
//     Care relationships and co-sign requirements are MainEHR's alone.
//  5. A tamper-resistant audit. The stores only add rows, so a changed row is tampering by definition —
//     but an administrator can still change one, and nothing outside GenPRES would notice.
//  6. Proof under concurrency. Rules 40-45 are stated, not proven. This file tests crafted
//     interleavings, which is not a proof.
//  7. Patient identity across systems. The PatientId never changes (Guarantee 1), so a MainEHR patient
//     merge cannot be reflected.
//  8. Interrupted work. Signing is the only way anything persists, so a half-finished plan lives only in
//     its browser. Whether practice can live without parking one is for user testing.
//
// ── What this model does not carry ─────────────────────────────────────────────
// Deployment, deliberately:
//
//   * Rule 12's cookie and Origin check: the SessionId is simply held and sent.
//   * Rule 4's mechanism: `BrowserState.BrowserIdentity` is a value the browser presents, not a sign-on
//     exchange. The rule is carried; the protocol is not.
//   * Rule 30's second sentence: every request here follows a `UserAct`, which states that discipline
//     rather than enforcing it.
//   * Rule 37's last sentence: there is no change-PIN act, only the one gated by a confirmation code.
//   * Rule 39's second half: caching, referrers and third-party script belong to serving the Client.
//   * Rule 14's rate limit: the standing cap and the absolute lifetime are here, a rate is not.
//   * Concept 7's login. `UserCredential` is keyed by UserId and carries no login: the registry is asked
//     for one at every launch (Rule 5), and it holds the mail address too (Rule 27).
//   * Concept 18's content. A KnowledgeRuleSet is a version number here and nothing else. What publishing
//     one does to a WorkPlan — which orders no longer fit — is not modelled.
//   * The cryptography: keys are strings and macs are string equality — placeholders that make forgery
//     tests possible, not security properties.
//   * Time: the clock advances one tick per handled message, so lifetimes are counted in messages.
//   * Exhaustive concurrency: Rules 40-45 are checked over crafted interleavings, not the state space.
//   * Rule 40's shape, and Actor 5's "append-only" with it. The document makes a Session a chain of
//     events, so that two invariants hold by construction: an ended Session never reopens, and there is
//     one open Session per chain. Here `PrivateStore.Sessions` is a list of current records, rewritten
//     in place, and those two invariants hold because every branch that writes one is guarded — checked
//     at `Rule 40 an Ended record can never come back open` rather than made impossible. The clinical
//     store, the spent-marks, the answered keys and the audit are append-only as the document says; the
//     Sessions and the UserCredentials are not.
//   * Rule 16's second clause: that every later version of GenPRES must still open every plan ever
//     signed. One version runs here, so there is nothing to be compatible with.
//   * UC-8 ext 1b, the upgrade. Draining one version while another starts is deployment, and this model
//     has no notion of a version to drain.
//   * Concept 6, the MainEHR PatientRecord: `[given]`, and nothing in the protocol reads it.
//   * Concept 11, the OrderScenario: `OrderContext.Content` stands in for all clinical content, so an
//     OrderScenario has nothing to be here.
//   * Actor 3's two-part delivery — a thin launch shell, then the full Client — is how the Client is
//     served, not what it does. One `BrowserState` stands for both parts.
//
// ── Where a mail address comes from ────────────────────────────────────────────
// Rule 27's three bullets say what the design is. This section says how the model
// arranges it, which the Rule does not.
//
//   * Rule 27, first bullet. Each mail is addressed from a fresh registry answer, fetched on the request
//     that sends it. The two acts that will need one — mailing a confirmation code, replacing a PIN —
//     ask before the act, so nothing is done that cannot then be told. Three in-flight stages carry the
//     answer from the lookup to the mail: `AwaitingResetAddress`, `AwaitingPinAddress`, and
//     `AwaitingEnrolAddress` on the launch side.
//   * Rule 27, third bullet. The wrong-PIN limit cannot ask first, because it is only discovered inside
//     the commit (Rule 42). `AwaitingLimitAddress` asks afterwards, and the Session has already ended by
//     then, so only the mail waits on the answer.
//   * `SessionRecord.Mail` is the fallback the third bullet needs. It is written at the launch and again
//     by `NoteMailUsed` whenever a mail goes out, and read only when the registry cannot be asked.

// The rest of the file is in three parts:
//   1. types      — the vocabulary: identities, concepts, messages, actor state
//   2. modules    — the edge table, the Record rules, the tokens, and the reducer
//   3. scenarios  — the harness, UC-1 .. UC-11, and the derived assertions
//
// ── [ships] and [model] ────────────────────────────────────────────────────────
// Every section below is tagged, and so is anything inside one that breaks the tag:
//
//   [ships]  the design itself — types and rule logic meant to be carried into the
//            source, in this shape.
//   [model]  scaffolding that exists so this file can run alone: the message plumbing
//            that stands in for HTTP and process boundaries, the other actors'
//            insides, the clock, the tracing, and the crypto placeholders.

// ═══════════════════════════════════════════════════════════════════════════════
//   SECTION 0B — THE TECHNICAL VOCABULARY
// ═══════════════════════════════════════════════════════════════════════════════
//
// Section 0's Concepts are the domain's words. These are the engineering ones the
// Rules rest on: Rule 34 needs unforgeability, Rule 42 atomicity, Rule 45 idempotency.
// Each entry says where it appears in this file.
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
//                     a different regime (Rule 28). Not modelled: PINs are held as
//                     typed, and that is a placeholder.
//
// ── Credentials and sessions ───────────────────────────────────────────────────
//
//   Bearer            anything whose mere possession grants use, with no proof of who
//                     holds it — hence single use, short life, never in a URL.
//                     Here: `Launch`, `SessionId` (Rules 2, 3, 12). A Launch is a
//                     bearer value for the Patient it names and for nothing else: it
//                     names no User, so holding it buys at most a Session of the
//                     holder's own (Rules 4, 6; Guarantee 5).
//   Token             in this design (Concept 17): a note the Server writes to itself,
//                     hands to the Client, and refuses to believe unless it comes back
//                     with its mac intact — the Server's memory where it keeps none.
//   Replay            re-sending a valid message to get its effect twice. The defence
//                     is a spent-mark: the nonce is consumed by the act that honours
//                     it. Here: the commit, Rules 2, 34 and 43. Rule 2
//                     is the exception that proves it: the same browser re-presenting
//                     a spent Launch in time is answered as the first time, because a
//                     retry of one launch is not a second launch.
//   TTL, expiry       a lifetime signed *into* the claim, so it cannot be extended by
//                     editing the token. Here: `Claim.ExpiresAt`, `launchTtl`,
//                     `tokenTtl`, `confirmationCodeTtl`, `anonymousLifetime`.
//   PKCE              proof key for code exchange: the browser invents a secret, sends
//                     only its hash when starting the flow and reveals the secret when
//                     redeeming, so a stolen code is useless to anyone else. Absent
//                     here: the Launch is an unbound bearer value (Rule 4).
//   HttpOnly cookie   a cookie no script can read, only the browser attaches. Rule
//                     12's intended transport; not modelled.
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
//                     lost. Rule 40 removes the race by making each change one act. In
//                     the document it also removes the shape, by making a Session a
//                     chain of events; this model keeps the shape and gets the
//                     atomicity from running inside one handler (see the note below).
//   Conditional write no lock: write only if the state still matches what was seen,
//                     otherwise fail. Also called compare-and-swap, or optimistic
//                     concurrency. Here: `EndSessionIfOpen`, `TouchIfOpen`,
//                     `OpenSessionClosingOthers` (conditional on the nonce, Rule 2),
//                     and the commit's "nothing newer than my base".
//   Idempotency key   a client-minted id on a request that changes something; the
//                     Database records key -> result and replays it, so a retry after
//                     a lost reply cannot act twice.
//                     Here: `IdemKey`, `PrivateStore.Answered` — on the Submission.
//                     Rule 45's other half is the conditional write, which lets a
//                     change take effect once without a key: `EndSessionIfOpen`,
//                     `ReplacePinIfCode`, `TouchIfOpen`, `NoteMailUsed` and
//                     `OpenSessionClosingOthers`. A retried
//                     `ResetPin` is answered from the state — while a confirmation
//                     code stands it is refused with `ResetPending`, and a fresh one
//                     is minted only once the standing one is void or expired.
//   Monotonic id      every new value greater than every value issued before — what
//                     "newer than" needs (Rules 20, 21, 36). Here:
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
type MailAddress      = MailAddress of string       // from the UserRegistry (Rule 27)
type PatientId        = PatientId of string
type BrowserId        = BrowserId of int            // [model]: one browser, named
type SessionId        = SessionId of string         // Rule 12: bearer, never in a URL
type SessionNo        = SessionNo of int            // traces and ui only, never a key
type TreatmentPlanId  = TreatmentPlanId of string
type TreatmentPlanNo  = TreatmentPlanNo of int      // ordering within one PatientRecord
type OrderContextId   = OrderContextId of string    // Concept 10: persists across plans
type AttemptId        = AttemptId of int            // [model]: one launch, mid-flight
/// [model] Ties the legs of one request together, standing in for a call stack.
/// Dropped with the reply (Rule 32). It names one exchange, never a User.
type RequestId        = RequestId of int
type Pin              = Pin of string               // Concept 7. Never leaves GenPRES.
/// Rule 37. The one-time code mailed to set or replace a PIN. Short-lived.
type ConfirmationCode        = ConfirmationCode of string
/// Rule 45. One key per Submission, minted by the Client. A retry carries the same
/// key, so the Database answers it instead of committing twice.
type IdemKey          = IdemKey of string
/// Concept 18. Which published KnowledgeRuleSet a computation ran under. Every set is
/// kept, so a signed plan can be explained from the one it names.
type RuleSetVersion   = RuleSetVersion of int

/// Roles. The UserRegistry decides which.
type Role =
    | Prescriber
    | Reader

/// The ten Actors, plus Environment. Environment is not an Actor: it is the world
/// they run in — the clock, and starting and stopping infrastructure.
type ActorId =
    | User                              // Actor 7
    | MainEhrWorkstation                // Actor 1  [given]
    | MainEhrLaunchScript               // Actor 2  [ours]
    | GenPresClient of BrowserId        // Actor 3  [ours]
    | GenPresServer                     // Actor 4  [ours]
    | GenPresDatabase                   // Actor 5  [ours]
    | PatientDataPlatform               // Actor 6  [given]
    | IdentityProvider                  // Actor 8  [given]
    | UserRegistry                      // Actor 9  [given]
    | MailService                       // Actor 10 [given]
    | Environment

// ───────────────────────────── the concepts ─────────────────────────────  [ships]

/// Concept 1. Identification and Role, and nothing else. The Role is the registry's
/// answer (Rule 5), never the launch's.
type UserContext =
    {
        UserId : UserId
        Login  : LoginName
        Role   : Role
    }

/// The Patient Data GenPRES needs. Opaque here: this models the protocol, not the
/// clinical content.
type PatientData = PatientData of string

/// Concept 13. Where the Patient Data came from, and when. Coarser than the Concept:
/// `PatientData` is one value here, so one source and one time cover all of it.
type DataSource =
    | FromPlatform of at: int
    | ByHand of at: int

/// Concept 2. Only a launch can supply the PatientId; the User can enter the data by
/// hand. Read from the platform once, at the launch, and not refreshed after that.
type PatientContext =
    {
        Patient : PatientId option
        Data    : PatientData option
    }

/// Concept 3. The active Patient, sealed by the LaunchScript under the key it shares
/// with the Server. It names no User: that is the browser's to prove (Rule 4). The
/// Patient it names is checked against the registry at the launch (Rule 6).
type Launch =
    {
        Patient  : PatientId option
        Nonce    : string
        IssuedAt : int
        Mac      : string
    }

/// Concept 7. Carries no Role of its own. Keyed by UserId and not by login, because a
/// login can be renamed and the wrong-entry count is per person (Rule 28).
type UserCredential =
    {
        User         : UserId
        Pin          : Pin option
        AttemptCount : int              // Rule 28: counts across Sessions
        /// Rule 28. A delay, not a latch: it passes on its own.
        LockedUntil  : int option
    }

/// Rule 37. A reset in flight, holding the code as a mac. `Wrong` is the code's own
/// count, kept apart from the credential's: guessing a code must not lock a good PIN.
type PinReset =
    {
        User    : UserId
        CodeMac : string
        Expires : int
        Wrong   : int
    }

/// Concept 10. `Content` stands in for all the clinical content. All four fields come
/// from the Client, and the Server trusts two of them: `Patient` is checked against
/// the SessionRecord (Rule 33) and `Stamp` is recomputed (Rule 35).
type OrderContext =
    {
        Id      : OrderContextId
        Patient : PatientId option
        Content : string
        Stamp   : UserContext option
    }

/// Concept 16. The cart. No stamps, no identity, no number: none of it is history
/// until it is signed (Concept 14). It travels with every request (Rule 32).
type WorkPlan =
    {
        Data   : PatientData option
        /// Concept 13. Where that data came from, carried so the TreatmentPlan created
        /// from this WorkPlan can record it.
        From   : DataSource option
        Orders : OrderContext list
    }

/// Concept 13. The plan as it stood when signed: the Patient's OrderContexts, by one
/// User (Rule 15), over the plan it was created from, if any.
type TreatmentPlan =
    {
        Id      : TreatmentPlanId
        No      : TreatmentPlanNo
        Patient : PatientId
        By      : UserContext
        Base    : TreatmentPlanId option
        Orders  : OrderContext list
        /// Concept 13. The Patient Data it was built on, and where that came from. A
        /// plan is explained by what it holds, not by asking the platform again.
        Data    : PatientData option
        From    : DataSource option
        /// Concept 18. The KnowledgeRuleSet the plan was checked under (Rule 44).
        RuleSet : RuleSetVersion
        /// Concept 13. The Session the plan was created in.
        Session : SessionId option
        /// When it was signed. Never the ordering: that is `No`, allocated by the
        /// Database at the append. A timestamp cannot say which of two landed first,
        /// so ordering by it would let Rule 20 fail open.
        At      : int
    }

/// Concept 12. Append-only, newest first. The PatientId is the one thing no
/// TreatmentPlan may change (Guarantee 1).
type PatientRecord =
    {
        Patient : PatientId
        Plans   : TreatmentPlan list
    }

// ───────────────────────────── the tokens ─────────────────────────────  [ships]

/// Concept 17. One subkey per purpose, so a token minted for one purpose cannot be
/// spent as another: it fails by key, before any field is compared.
[<RequireQualifiedAccess>]
type TokenPurpose =
    /// Rule 34. The TreatmentPlan a Session opened with.
    | Opened
    /// Rule 43. The exact WorkPlan a signature would attest to.
    | Challenge
    /// Rule 44. The Patient Data as the platform has it now, shown and accepted.
    | DataNotice
    /// Rules 2, 3. The Launch the LaunchScript sealed (Concept 3). Not a Claim — a
    /// Launch has no Session to be bound to.
    | Launch

/// What a token names. How `Names` is read depends on the purpose, and the purpose is
/// signed into the claim, so nothing can be read one way and signed another.
type Claim =
    {
        Purpose   : TokenPurpose
        Sid       : SessionId
        Patient   : PatientId option
        /// What the token names, as text: a plan id, or a digest. Which one depends
        /// on the purpose.
        Names     : string list
        /// Uniqueness, and the key a spent-mark is filed under (Rule 42).
        Nonce     : string
        IssuedAt  : int
        /// Signed in here. The commit is what refuses on it (Rule 42).
        ExpiresAt : int
    }

/// A claim and the Server's word for it. Verifying means recomputing the mac with the
/// subkey of the purpose expected, and comparing.
type Token =
    {
        Claim : Claim
        Mac   : string
    }

/// Rule 34. Re-minted whenever the baseline moves, because Rules 20 and 21 are both
/// measured from it.
type OpenedToken = Token

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
        Challenge : SigningChallenge option
        DataOk    : DataNoticeToken option
        /// Rule 42. Optional in the type so a Submission without one can be built and
        /// refused; there is no way to create a TreatmentPlan without it (Concept 14).
        Pin       : Pin option
        Key       : IdemKey
    }

/// Rule 42. The Submission, plus the one thing only the Server can have found out:
/// the Role it just re-took (Rule 38). The act re-establishes everything else itself.
type Commit =
    {
        Sid  : SessionId
        Req  : Submission
        Role : Role option
    }

// ───────────────────────────── session state ─────────────────────────────  [ships]

/// Rule 10, exactly: the ways a Session ends, and no others. A Server restart is not
/// among them: the Server holds no Session state to lose (Rule 32).
type EndMark =
    | ClosedByUser
    /// Rules 8 and 11. The User did the opening, so this ending owes no notice.
    | ReplacedInBrowser
    | Idle
    | Superseded
    | WrongPinLimit
    /// Rule 10. Its own mark and not `Idle`: Rule 46 records reasons, and this is a
    /// different reason.
    | Expired

/// Two states. `OpenOrGone` also covers a Client that has gone quiet: Rule 10 says a
/// vanished browser looks the same as a silent one, so there is nothing finer to say.
type SessionState =
    | OpenOrGone
    | Ended of mark: EndMark * at: int

/// Rule 11, as a state rather than a timestamp: an `int option` could not tell "none
/// owed" from "owed and not yet given". Not Rule 21's newer-plan notice, which is a
/// different thing and which nobody acknowledges.
type SessionNotice =
    /// The Session is open, or the User closed it themselves. Nothing is owed.
    | NotOwed
    /// Owed until the next launch. Telling a Client that still holds the SessionId
    /// discharges nothing: whoever holds it need not be the User (Rule 11).
    | Owed
    /// Put in front of the User at a launch. The Server cannot know a Client showed
    /// anything (Consequence 6), so an unacknowledged notice may be delivered again.
    | Delivered of at: int
    /// The User said they had seen it. After this it is never shown again.
    | Acknowledged of at: int

/// Concept 9, and all the Server remembers between requests (Rule 32). It carries the
/// whole UserContext because a Session runs on the Role its launch established
/// (signing excepted, Rule 38).
type SessionRecord =
    {
        Id       : SessionId
        No       : SessionNo
        /// None: the Session was anonymous (Rule 14).
        User     : UserContext option
        /// Concept 9, Rule 27. The address the registry last gave for this User: at
        /// the launch, and again whenever a mail went out. It is never what a mail is
        /// addressed from — that is always a fresh answer. It is read only when the
        /// registry cannot be asked, and only for a notice; a confirmation code is
        /// never sent to it (UC-6 ext 1c, 2b).
        Mail     : MailAddress option
        Patient  : PatientId option
        /// Rules 8 and 40. Without it the per-browser limit could only rest on a
        /// Client's word, which is the word of the party the limit is there to bound.
        Browser  : BrowserId option
        /// Rule 2. The nonce of the Launch that opened this Session, which is also
        /// its spent-mark. None means no launch: an anonymous open.
        Launch   : string option
        OpenedAt : int
        /// Rule 10. When this Session stops, whatever happens. Every Session has such
        /// a limit. Rule 9's idle clock forgives a Client that keeps talking; this
        /// does not.
        ExpiresAt : int option
        /// Rule 9: every request refreshes this. The idle clock lives here because
        /// the Server keeps no state of its own (Rule 32).
        LastSeen : int
        State    : SessionState
        /// Rule 11. Set by `endWith`, so the obligation and the ending are made in
        /// the same act and cannot drift apart.
        Notice   : SessionNotice
    }



// ───────────────────────────── failures ─────────────────────────────  [ships]

/// Rule 42. Why a commit changed nothing. Each is one of the rules the act
/// re-establishes, and the act stops at the first that fails. The PIN is checked last,
/// so a Submission that was never going to land costs no attempt (Rule 28).
type CommitRefusal =
    /// Rules 40, 41. The Session is not open, or is past its time.
    | SessionNotOpen of EndMark option
    /// Rules 14, 26, 38. Nobody here may create.
    | RoleRefused
    /// Rules 33, 34, 43, 44. A token that does not verify, or does not name this.
    | TokenRefused of string
    /// Rules 20, 36. Whose work stands in the way, never which TreatmentPlan it is.
    | BlockedBy of UserContext
    /// Rules 23, 28.
    | PinWrong of left: int
    | PinLimitReached
    /// Rule 28. The credential is at the limit, and signing is locked until the tick
    /// named. The delay passes on its own; Rule 37 clears it early.
    | CredentialLocked of until: int


/// Why a Launch bought nothing. The Client is never told which (Rule 7); the audit
/// records it (Rule 46).
type LaunchFailure =
    /// The mac does not verify, so no holder of the launch key wrote it.
    | LaunchForged
    /// Rule 3. Past its lifetime.
    | LaunchExpired
    /// Rule 2. Already spent, and not by a browser entitled to the first answer.
    | LaunchAlreadySpent
    /// Rules 4, 7. The browser proved nobody, so there is no User to open for.
    | NoIdentity
    /// Rule 6. The registry names another active Patient than the Launch's, or none.
    | PatientNotActive

/// Rule 37. Why a code bought nothing. Kept apart because they mean different things
/// to the User: ask again, or look again at the mail.
type ResetFailure =
    | NoResetPending
    /// Rule 37. A reset is already in flight and its code is still good. A second
    /// request would void the code the User is reading, so it is refused.
    | ResetPending
    | ResetExpired
    | WrongCode of left: int
    /// Too many wrong entries. The code is void; a fresh reset mails a fresh one.
    | ResetVoid
    /// Rule 27. The registry could not say where to send the mail, so nothing was
    /// done: a PIN is never set or replaced without the User being told (UC-6 ext 1c).
    | AddressUnavailable

type RegistryFailure =
    | NoRole                            // the registry knows the login, and says no
    | RegistryUnreachable               // the registry cannot say

/// Which exchange a round trip belongs to: a launch in flight, a request in flight,
/// or the idle sweep. Nothing outlives its exchange, which is Rule 32 as a type.
type LegTag =
    | ForLaunch  of AttemptId
    | ForRequest of RequestId
    | ForSweep

// ───────────────────────────── messages ─────────────────────────────  [mixed]
// The payloads ship. The envelope around them is [model]: in a real system that is an
// endpoint and a caller, not a value.

/// What travels inside a Session. All of them arrive as a `SessionRequest`, so Rule
/// 9's refresh has exactly one home.
type SessionCmd =
    /// Concept 15. The Client has already changed its own cart; this sends all of it
    /// to be computed. The answer comes from the payload and the Server keeps none.
    | Compute of OrderContext list
    /// Concept 14. The whole WorkPlan travels, with every token issued about it.
    | SubmitTreatmentPlan of Submission
    /// Rule 43. Asks for the challenge a signature will have to carry. Rule 20 is
    /// answered here, before the User is ever asked for a PIN (UC-3 ext 2a), and the
    /// challenge names the exact WorkPlan it was asked about.
    | RequestSignChallenge of WorkPlan * OpenedToken * DataNoticeToken option
    | OpenTreatmentPlan of TreatmentPlanId        // Rules 18, 19
    /// UC-6. Rule 37: this removes nothing. It asks for a code to be mailed.
    | ResetPin
    /// Rule 37. The mailed code and the new PIN. Verified and replaced in one act, so
    /// there is never a moment without a PIN.
    | SupplyResetCode of ConfirmationCode * Pin
    | CloseSession                      // Rule 10

/// What the User does at the Client. Some of these are purely local. There is no
/// `Proceed`: a Rule 21 notice gates nothing, so there is nothing to proceed past.
type UserAct =
    | Prescribes of OrderContextId      // Concept 15: add or change, in the Client
    | EntersPatientData of PatientData  // Concept 2: the User supplies it by hand
    /// Concept 14: the only way a plan is created. It asks for the challenge and
    /// carries no PIN: UC-3 step 2 sends the WorkPlan, and nothing else.
    | Signs
    /// Rule 43. The second half of signing: `Signs` asks for the challenge, and this
    /// answers it with the PIN the modal asked for (UC-3 step 3). Nothing is
    /// submitted in between.
    | ConfirmsSign of Pin
    /// Rule 43. The User leaves the signature modal without signing.
    | CancelsSign
    | OpensTreatmentPlan of TreatmentPlanId       // Rules 18, 19
    | AsksPinReset                      // UC-6
    /// UC-6 step 2. The User has read the mail and chooses the new PIN.
    | EntersResetCode of ConfirmationCode * Pin
    | ClosesSession                     // Rule 10
    /// Rule 11. The User dismisses the notice that a Session ended.
    | AcknowledgesNotice
    /// UC-8 step 3. The cart survived because it was never in the Server (Rule 32).
    /// The User carries it into the next Session as fresh prescribing. It lasts
    /// exactly as long as the browser does.
    | CarriesOverFrom of BrowserId

type Msg =
    // ── Environment: the clock and the infrastructure ──
    | Tick
    | Start of ActorId
    | Stop of ActorId
    /// Concept 18. A new KnowledgeRuleSet is published. Every computation from here on
    /// runs under it, this Session's included (Rule 44).
    | PublishRuleSet of RuleSetVersion
    // ── U1. User <-> MainEHR Workstation ──
    | LogIn of LoginName
    | SelectPatient of PatientId
    | ClearPatient
    // ── U2. User <-> MainEHR LaunchScript ──
    | TriggerLaunch
    /// UC-1 ext 1b, 2a. The only failures it can report. Both are decided before it
    /// seals anything: after the launch it learns nothing (Consequence 1).
    | LaunchError of string
    // ── C4. MainEHR LaunchScript => GenPRES Client.  One-way: Consequence 1. ──
    /// The LaunchScript seals the Launch and opens the browser with it. It exchanges
    /// nothing with anybody, so nothing here can fail or be reported.
    | OpenUrl of Launch
    // ── U3. User <-> GenPRES Client ──
    | Refresh                           // retry the launch from the page's own memory
    /// Rule 39. The page goes and comes back, so its memory is gone and only the
    /// address bar is left to re-present.
    | ReloadPage
    | OpenDirectly                      // UC-7: no launch, no credential
    | AcceptAnonymousOffer              // Rule 7, UC-1 ext 5a
    /// UC-2 step 2, mid-launch. The User reads the code from their mail and enters it
    /// with the PIN they choose.
    | ChoosePin of ConfirmationCode * Pin
    | Act of UserAct
    | CloseBrowser                      // UC-10 ext 1b: nothing reaches the Server
    // ── C5. GenPRES Client -> GenPRES Server ──
    /// Rule 4. The Launch, the identity the browser proved, and the Session this
    /// browser already holds. No identity opens nothing.
    | RedeemLaunch of Launch * LoginName option * SessionId option
    /// Rules 8, 14. An anonymous open replaces whatever this browser held: one
    /// browser, one Session, and the Database keeps that limit.
    | OpenAnonymous of SessionId option
    /// Rule 14. Bounded in number as well as in lifetime. Above the bound no
    /// SessionRecord is written, and the refusal is counted rather than logged.
    | AnonymousRefused
    /// UC-2, Rule 37. The mailed code and the PIN to set. The launch waits on a human
    /// until both arrive.
    | SupplyPin of AttemptId * ConfirmationCode * Pin
    /// Rule 11. Not a `SessionRequest`: the Session it speaks of has ended. It is an
    /// act of a live launched Session of the same User, never of the stale Client.
    | AckSessionNotice of acknowledging: SessionId * about: SessionId
    /// Rule 34. The OpenedToken travels with every request, so every response can say
    /// whether the record moved on (Rule 21). `None` only where there is none to send:
    /// an anonymous Session, or a Client acting before its first one arrived.
    | SessionRequest of SessionId * OpenedToken option * SessionCmd
    // ── C7. GenPRES Server <-> UserRegistry.  The Launch never reaches here. ──
    | ResolveUser of LegTag * LoginName
    /// Rules 5, 6. The Role, the mail address, and the Patient the User has active
    /// in MainEHR right now — `None` where they have none.
    | UserResolved of LegTag * UserContext * MailAddress * PatientId option
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
    | ReplacePinIfCode of LegTag * UserId * ConfirmationCode * Pin
    | PinReplaced of LegTag * UserCredential
    | ResetRefused of LegTag * ResetFailure
    | ReadRecord of LegTag * PatientId
    | RecordRead of LegTag * PatientRecord
    /// Rule 42, with Rule 36 inside it: the check and the append are one act.
    | CommitTreatmentPlan of LegTag * Commit
    | TreatmentPlanCommitted of LegTag * TreatmentPlan
    | CommitRefused of LegTag * CommitRefusal
    /// Rule 40. The Server never writes back a record it read; it names the change
    /// and the Database decides. Rule 8's two limits are kept in this same act, and so
    /// is Rule 2's spend: the record carries the nonce, and the open is what spends it.
    | OpenSessionClosingOthers of LegTag * SessionRecord * replacing: SessionId option
    | SessionWasOpened of LegTag
    | EndSessionIfOpen of SessionId * EndMark
    | TouchIfOpen of SessionId                    // Rule 9
    /// Rule 27. Records the address a mail was just sent to, so a later notice the
    /// registry cannot answer for has something to fall back on. Conditional like
    /// every SessionRecord change (Rule 40): an ended Session is not touched.
    | NoteMailUsed of SessionId * MailAddress
    | MarkDelivered of SessionId                  // Rule 11, at least once
    /// Rule 11, and then never again. It names which Session is acknowledging, so the
    /// Database can check that it is the User's own and launched.
    | MarkAcknowledged of acknowledging: SessionId * about: SessionId
    | ReadSessionRecord of LegTag * SessionId
    /// Rule 21. The record, and the head of that Session's Patient's PatientRecord.
    /// Read in the same leg, so comparing them costs the Server no second read.
    | SessionRecordRead of LegTag * SessionRecord option * TreatmentPlan option
    | ReadSessionRecords of LegTag
    | SessionRecordsRead of LegTag * SessionRecord list
    /// Rule 2. A read, and nothing more: is this nonce spent, and if so which Session
    /// spent it? The answer is advisory — the nonce is spent by the open (Rule 40),
    /// so another presentation may win the race between this answer and that act.
    | CheckLaunchSpent of LegTag * nonce: string
    | LaunchUnspent of LegTag
    /// Rule 2. Spent, with the Session that spent it: what the replay clause needs.
    /// Answered by the early check and by a refused open alike.
    | LaunchReplayed of LegTag * SessionRecord option
    /// Rule 46. An anonymous open refused above the bound. Counted per source and
    /// nothing more: a line per request would be the same flood by another name.
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
    | PinRequired of AttemptId          // UC-2: choose one, and nothing else is offered
    /// Rule 7. The User is told no reason: forged, expired, spent and wrong-Patient
    /// are one answer to a person. The Client is told one thing only: whether the
    /// Launch is still worth presenting. An unavailable identity is retryable (UC-1
    /// ext 3c); a forged, aged or spent Launch is not (ext 4a).
    | LaunchRefused of retryable: bool
    | NotAuthorised                     // the registry says no; no reason either
    | AuthorityUnavailable              // the registry cannot say
    | ServerUnreachable
    /// Rule 11's one telling. The mark is what ended it.
    | SessionEnded of EndMark option    // None: the Server has no such record
    /// Rule 11. For this screen only. It discharges nothing, so the notice still
    /// stands until a launch.
    | SessionRefused of EndMark option
    /// Rule 11. What ended, and which Session, so the User can acknowledge it. An
    /// ended SessionId opens nothing, so naming it is safe.
    | PriorSessionNotice of (SessionNo * SessionState * SessionId) list
    /// Rule 32. The answer to `Compute`, computed from the payload and kept nowhere.
    | Computed of OrderContext list
    /// Rules 20, 36. Whose work stands in the way. Never which TreatmentPlan it is.
    | SubmissionBlocked of UserContext
    /// Rules 21, 22. A TreatmentPlan newer than the one this Session opened with
    /// exists: whose it is, and when it was signed. It rides along with a response and
    /// gates nothing. Rule 20 is the only guard.
    | NewerPlanNotice of UserContext * int
    /// Rules 33, 34. The payload contradicted the SessionRecord, or a token did not
    /// verify. The reason is for the trace; the Client shows only a refusal.
    | SubmissionRefused of string
    | TreatmentPlanSubmitted of TreatmentPlanId * OpenedToken
    /// Rule 43. The challenge to sign with, over the WorkPlan it was asked about.
    | SignChallengeIssued of SigningChallenge
    /// Rule 44. The Patient Data has changed since the launch read it (Concept 2).
    /// Shown to the User, and accepted by returning the token.
    | PatientDataChanged of PatientData * DataNoticeToken
    /// Rule 44. The platform could not be asked, so the data is unchecked. Accepted
    /// by returning this token, the same way a change is.
    | PatientDataUnverified of DataNoticeToken
    | TreatmentPlanOpened of TreatmentPlanId * OrderContext list * OpenedToken
    | PinRejected of int                // Rule 28: attempts left
    | NoTreatmentPlanHere                    // Rule 13
    | NotPermitted                      // Roles: a Reader never creates a TreatmentPlan
    /// Rule 38. Not `AuthorityUnavailable`, which belongs to a launch and offers an
    /// anonymous open. Here a Session exists already, and it stands.
    | SigningUnavailable
    /// Rule 28. Signing is locked until the delay passes or the PIN is replaced
    /// (Rule 37). Not `PinRejected`, which has attempts left, and not `SessionEnded`,
    /// which is what the attempt at the limit caused.
    | SigningLocked
    /// Rule 37. A code is on its way to the address the registry holds. The PIN in
    /// force is still the old one.
    | ResetCodeMailed
    /// Rule 37. Replaced, in one act (Rules 27, 28).
    | PinChanged
    | ResetDenied of ResetFailure
    // ── any actor -> Environment (standing in for the audit log) ──
    /// An envelope no edge permits. Not merely dropped: a forged or misrouted envelope
    /// is exactly the event worth alerting on.
    | Refused of Envelope
    /// Rule 27's other half: "records the change". The mail is what the User sees;
    /// this is the record.
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
        /// Rule 1. Who may run the LaunchScript. How MainEHR decides is its own
        /// affair; what is ours to state is that a refusal sends nothing.
        MayLaunch     : Set<LoginName>
        /// UC-1 ext 2a. The LaunchScript reads the sealing key from the MainEHR
        /// database (Actor 2), and that read can fail. It is the last thing the script
        /// can report, because after it the script has exited (Consequence 1).
        KeyReadable   : bool
        NextTab       : int
    }

/// Actor 9 [given]. Says who a login belongs to, what that person may do, how to
/// reach them by mail, and which Patient they have active in MainEHR right now. The
/// only source of a Role (Rule 5) and of the active Patient (Rule 6).
type RegistryState =
    {
        Users  : Map<LoginName, UserContext * MailAddress>
        /// Rule 6. What MainEHR has open for each User. The registry is told by
        /// MainEHR, so selecting a Patient at the Workstation is what sets it.
        Active : Map<LoginName, PatientId>
        Up     : bool
    }

/// Actor 6 [given]. Read-only, and read once per launch (Concept 2).
type PlatformState =
    {
        Data : Map<PatientId, PatientData>
        Up   : bool
    }

/// Actor 5, the half that is copied. A type of its own rather than a filter somebody
/// has to remember to apply.
type ClinicalStore =
    {
        /// Concept 12. Every TreatmentPlan, newest first per Patient.
        Signed : Map<PatientId, TreatmentPlan list>
    }

/// Rule 46. One line of the audit: what was done, and when. The tick is the
/// Database's own, so the party that did the act is the party that stamped it.
type AuditEntry =
    {
        At   : int
        What : string
    }

/// Actor 5, the other half. Everything that is GenPRES's own business and no record
/// of care. Never copied anywhere.
type PrivateStore =
    {
        Sessions     : SessionRecord list             // Concept 9
        Credentials  : Map<UserId, UserCredential>    // Concept 7, keyed by the person
        /// Rule 37. Resets in flight. Gone when the code is spent, expires, or is
        /// guessed away.
        Resets       : Map<UserId, PinReset>
        /// Rule 45. What each key has already been answered with.
        Answered     : Map<IdemKey, Result<TreatmentPlan, CommitRefusal>>
        /// Rules 2, 34, 43. Spent nonces, of tokens and of Launches. This is what
        /// makes each work exactly once. A mark past its lifetime can be purged.
        Spent        : Set<string>
        /// Rule 46. Anonymous opens refused above the bound (Rule 14), counted per
        /// source. A count and not a line each, so a flood writes nothing that grows.
        AnonymousRefused : Map<ActorId, int>
        /// Rule 46. What was done, to whom, and when.
        Audit        : AuditEntry list
    }

/// Actor 5. The Server is its only writer. `NextPlan` lives here because the party
/// that decides whether a Submission lands is the party that can order the plans.
type DatabaseState =
    {
        Clinical : ClinicalStore
        Private  : PrivateStore
        NextPlan : int
    }

/// One launch attempt, mid-flight. The Launch is kept for its nonce and its Patient
/// only. Who the User is, is `Identity` (Rule 4).
type LaunchCtx =
    {
        Client    : ActorId
        Launch    : Launch
        /// Rule 4. The identity the browser proved (Actor 8), and the only source of
        /// the Session's User. The Launch carries no login to disagree with it.
        Identity  : LoginName
        /// Rules 8 and 11. The Session this browser held when it presented the
        /// Launch, which opening the new one replaces.
        Replacing : SessionId option
        /// Rule 2's replay clause. Set when the same browser already spent this nonce
        /// within the lifetime. The launch then answers over the SessionRecord the
        /// first presentation opened, instead of opening a second Session.
        Resuming  : SessionRecord option
    }

/// The stages of a launch, in the order Rules 25 and 26 fix. Per-attempt and dropped
/// with the reply, so it is not Session state (Rule 32).
type PendingLaunch =
    /// Rule 2. The nonce is being spent at the Database. Nothing else has happened
    /// yet, so a refusal here costs nothing.
    | AwaitingSpend       of LaunchCtx
    | AwaitingUser        of LaunchCtx
    | AwaitingCredential  of LaunchCtx * UserContext * MailAddress
    /// UC-2 step 1. The code is parked at the Database and the mail goes out on its
    /// answer. The code rides along because the Server mails it and the Database only
    /// ever saw its mac (Rule 37).
    | AwaitingEnrolCode   of LaunchCtx * UserContext * MailAddress * ConfirmationCode
    /// UC-2. The launch waits on a human and may stay here indefinitely. The code it
    /// waits for expires on its own (ext 2a).
    | AwaitingPinChoice   of LaunchCtx * UserContext * MailAddress
    /// Rule 27. The User has answered and the registry is being asked where to send
    /// the notice. The launch-time address rides along as the fallback, and the code
    /// and PIN ride along because the Database act has not happened yet.
    | AwaitingEnrolAddress of LaunchCtx * UserContext * MailAddress * ConfirmationCode * Pin
    | AwaitingPinWritten  of LaunchCtx * UserContext * MailAddress
    | AwaitingPatientData of LaunchCtx * UserContext * MailAddress
    | AwaitingRecord      of LaunchCtx * UserContext * MailAddress * PatientContext
    /// Rule 19. The TreatmentPlan the Session will open from, if the record has one.
    | AwaitingPriors      of
        LaunchCtx * UserContext * MailAddress * PatientContext * TreatmentPlan option
    /// Rules 2, 40. The open is at the Database, which is where the nonce is spent, so
    /// what the Client will be told waits here until it answers. The open can still be
    /// refused: another presentation may have won the race since the early check.
    | AwaitingOpen        of
        LaunchCtx * SessionRecord * PatientContext * TreatmentPlan option * SessionRecord list
    /// Rule 14. The same, for an open with no Launch. Nothing can refuse it — there is
    /// no nonce to be spent — but the Client is told from the same place.
    | AwaitingAnonymousOpen of ActorId * SessionRecord * PatientContext

/// One entry in the Server's launch table.
type PendingEntry =
    {
        Stage : PendingLaunch
        Since : int
    }

/// How far one in-Session request has got. Each stage carries what the earlier legs
/// returned, because there is nowhere else to keep it (Rule 32).
type RequestStage =
    /// Rule 33: before anything else, who and which Patient this Session is.
    | AwaitingSessionRecord
    /// Rules 17 to 21 are decided against the PatientRecord: an open (Rules 18, 19),
    /// and the Rule 20 pre-check a challenge is issued after (Rule 43).
    | AwaitingPatientRecord of SessionRecord
    /// Rule 38. A signature is a fresh act of authority, so the Role is taken from the
    /// registry again — every time, and before the PIN is ever asked for.
    | AwaitingSigningRole   of SessionRecord * Submission
    /// Rule 44. The re-read before the challenge is minted, so the commit that gets
    /// the challenge back needs no second look.
    | AwaitingChallengeData of SessionRecord * WorkPlan * DataNoticeToken option
    /// Rule 42: the Database is deciding the whole Submission, in one act.
    | AwaitingCommit        of SessionRecord
    /// Rule 27. The address is the registry's, so it is asked for before the act that
    /// will need it — not after, so that nothing is done that cannot then be told.
    | AwaitingResetAddress  of SessionRecord * ConfirmationCode
    | AwaitingPinAddress    of SessionRecord * ConfirmationCode * Pin
    /// Rules 10, 27. The exception: the wrong-PIN limit is only discovered inside the
    /// commit, so here the address is asked for after the fact and the mail is what
    /// waits on it.
    | AwaitingLimitAddress  of SessionRecord
    /// UC-6 step 1. The address is in hand and the reset is being parked. The code
    /// rides along because the Server mails it and the Database only saw its mac.
    | AwaitingResetStarted  of SessionRecord * ConfirmationCode * MailAddress
    /// UC-6 step 2. Rule 27 again: the replacement is mailed and recorded.
    | AwaitingPinReplaced   of SessionRecord * MailAddress

/// Rule 32 made visible. One entry per request in flight, made when the request
/// arrives and removed with its reply. Nothing here survives the answer.
type RequestCtx =
    {
        Sid    : SessionId
        Client : ActorId
        /// Rule 34. What the Client says this Session opened with, kept for the life
        /// of the request so Rule 21 can be answered from it.
        Opened : OpenedToken option
        Cmd    : SessionCmd
        Stage  : RequestStage
    }

/// Actor 4. Counters, what is in flight, and whether it is up. Nothing else: that is
/// Rule 32 as a type, with no field a Session could live in.
type ServerState =
    {
        /// One entry per in-Session request, gone with the reply.
        InFlight      : Map<RequestId, RequestCtx>
        /// One entry per launch attempt, gone with the launch.
        Pending       : Map<AttemptId, PendingEntry>
        /// Separate id spaces, so separate counters. All monotonic: an id is never
        /// reissued. The TreatmentPlan counter is the Database's, because Rule 36 makes
        /// the Database the party that orders a PatientRecord.
        NextAttempt   : int
        NextRequest   : int
        NextSessionId : int
        Up            : bool
    }

/// Actor 3. It carries the cart and nothing else does (Rule 32). So a Session's work
/// survives a Server restart here, and dies with the browser.
type BrowserState =
    {
        /// Consequence 4. The Launch arrives in the address bar. Rule 39 erases it
        /// there the moment the Client presents it, so a reload finds nothing.
        UrlLaunch      : Launch option
        /// Rule 39. What is left after the scrub: a copy in the page's own memory,
        /// enough to retry (UC-1 ext 3a), and gone with the page.
        RetryLaunch    : Launch option
        /// Concept 4, Rule 4. The browser's and not the page's, so a reload keeps it
        /// and no Launch can change it.
        BrowserIdentity : LoginName option
        /// Rule 12: a bearer credential, held here and sent in the request.
        Sid            : SessionId option
        /// What the Server said this Session's User and Patient are (Concepts 1, 2).
        /// Shown to the User, and never sent back as an assertion: Rule 33 takes both
        /// from the SessionRecord.
        User           : UserContext option
        Patient        : PatientId option
        /// Concept 16. The WorkPlan travels with every request and lives nowhere else.
        Work           : WorkPlan
        /// Rule 34. Issued by the Server, returned with every request.
        Opened         : OpenedToken option
        /// Rule 43. A signature the User has started: the challenge is on its way,
        /// and then the challenge itself. The PIN is not here — it is asked for at
        /// the modal (UC-3 step 3). While the modal is up the WorkPlan cannot
        /// change, which is what it is for.
        Signing        : bool
        Modal          : SigningChallenge option
        /// Rule 44. The Patient Data notice the User has accepted, returned with the
        /// Submission.
        DataOk         : DataNoticeToken option
        /// Rule 11. Which Sessions the notice in front of the User is about.
        NoticeFor      : SessionId list
        /// The attempt this Client was asked to choose a PIN for (UC-2).
        AwaitingPin    : AttemptId option
        /// Rule 7: whether a fresh anonymous open is on offer.
        AnonymousOffer : bool
        /// Whatever the Client is currently putting in front of the User: a notice
        /// (Rules 11, 21), a prompt (UC-2), an unavailability.
        Showing        : string option
        Closed         : bool
    }

/// The world the participants run in, not a participant's own state. It disappears in
/// production, where real time arrives as Tick.
type EnvState =
    {
        Now : int
        /// Concept 18. The KnowledgeRuleSet currently published. The Server computes
        /// every request under it, so publishing a new one reaches work in progress.
        RuleSet : RuleSetVersion
    }

/// One field per participant, so no branch can read across what will be a process
/// boundary. A convention the branch bodies keep, not something the type enforces.
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
// Rules 29, 30, 31. The unit is one handled message, so a lifetime is measured
// against the length of a cascade (a launch is about twenty), not a count of Ticks.

/// Rule 29. Long enough for one launch: a page load and a retry or two. The
/// LaunchScript and the Server share this as well as the key, because both measure
/// the lifetime against the Launch's own IssuedAt.
let launchTtl = 20

/// Rule 30. Long enough to span the gaps between a clinician's actions. The unit is
/// one handled message, and such a gap is a whole cascade, so this is far larger than
/// `launchTtl`, which spans one page load.
let sessionTtl = 150

/// Rule 14. An anonymous Session has no idle clock: nobody is waiting to be told
/// anything. It does not live for ever either. This is its outright limit, counted
/// from the open.
let anonymousLifetime = 1000

/// Rule 14. An anonymous open is an unauthenticated write, so the lifetime bounds how
/// long each lives and this bounds how many there are.
let anonymousOpenLimit = 8

/// Rule 38. How long the launch's Role stands for a signature when the registry
/// cannot be asked. Short, because it covers a registry that is briefly down and not
/// a Role that may be stale for a shift. Past it, signing fails closed.
let roleGrace = 2 * sessionTtl

/// Rule 10. The outright limit on a launched Session, counted from the open. Rule 9's
/// refresh does not touch it: a Client that keeps talking holds off the idle clock and
/// nothing else. Several times `sessionTtl`, because it bounds a shift.
let sessionMaxLifetime = 8 * sessionTtl

/// How long a launch may sit half-finished before the Server forgets it (UC-2). Not
/// Rule 30's number: this bounds a round trip, not a clinician's gap.
/// `AwaitingPinChoice` waits on a human and is never collected.
let launchAbandonTtl = 25

/// Rule 31. Small enough to make guessing hopeless, large enough to forgive
/// mistyping.
let wrongPinLimit = 3

/// Rule 28. The first lock. Each further wrong entry doubles it: short enough that a
/// User who mistyped waits and carries on, steep enough to price a guesser out.
let pinLockBase = 100

/// Rule 37. Long enough for a User to go and read their mail, and no longer. In the
/// unit used here that is a few attempts at the code and a short walk to the inbox.
let confirmationCodeTtl = 40

/// Rule 37. As with a PIN (Rule 31): small enough to make guessing hopeless, large
/// enough to forgive mistyping. Counted per code, not per credential.
let wrongConfirmationCodeLimit = 3

/// Concept 17. A token is worth nothing once its Session is gone, so it need not
/// outlive one. Signed into every claim, and read at the commit (Rule 42).
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

/// Rules 17 to 21. Small total functions over a TreatmentPlan list held newest first,
/// so "most recent" is `List.tryHead` and "newer than" compares TreatmentPlanNo.
module PatientRecord =

    let empty patient = { Patient = patient; Plans = [] }

    let private no (s: TreatmentPlan) = let (TreatmentPlanNo n) = s.No in n

    /// Rule 36's half of the check: what the Server saw as the head when it decided.
    let head (r: PatientRecord) = r.Plans |> List.tryHead |> Option.map _.Id

    /// "newer than the TreatmentPlan the User opened with". Where the User opened with
    /// nothing, any TreatmentPlan counts as newer.
    let private newerThan (openedWith: TreatmentPlanId option) (s: TreatmentPlan) (r: PatientRecord) =
        match openedWith with
        | None -> true
        | Some id ->
            match r.Plans |> List.tryFind (fun x -> x.Id = id) with
            | Some baseline -> no s > no baseline
            | None -> true              // the baseline is not in this record at all

    /// Rule 17. The only TreatmentPlan that counts clinically: the most recent one.
    /// Nothing is removed to make it so: the record is append-only (Concept 12).
    let latest (r: PatientRecord) = r.Plans |> List.tryHead

    /// Rule 19. A User starts from the most recent TreatmentPlan; where none exists,
    /// from nothing.
    let startsFrom (r: PatientRecord) = latest r

    /// Rule 18. Every TreatmentPlan is open to read. Opening an older one makes it
    /// what the Session opened with, and Rule 20 then blocks the Submission. So
    /// read-only falls out of the baseline rather than being a second mechanism.
    let mayOpen (id: TreatmentPlanId) (r: PatientRecord) =
        r.Plans |> List.tryFind (fun s -> s.Id = id)

    /// Rules 20, 21. The TreatmentPlan that is newer than the one the User opened
    /// with, if there is one. Rule 20 refuses a Submission on it; Rule 21 reports it
    /// with every response and gates nothing.
    let blocking (openedWith: TreatmentPlanId option) (r: PatientRecord) =
        latest r |> Option.filter (fun s -> newerThan openedWith s r)

    /// Concept 12: append-only. The newest TreatmentPlan goes on the front, and no
    /// existing one is ever touched.
    let append (s: TreatmentPlan) (r: PatientRecord) =
        { r with Plans = s :: r.Plans }

// ───────────────────────────── the two stores ─────────────────────────────  [ships]

/// Actor 5. The clinical store holds every TreatmentPlan; the private store holds
/// everything else. Nothing outside this module knows which half a thing came from.
module Database =

    let private no (s: TreatmentPlan) = let (TreatmentPlanNo n) = s.No in n

    let signedOf patient (db: DatabaseState) =
        db.Clinical.Signed |> Map.tryFind patient |> Option.defaultValue []

    /// The whole record, newest first. The Record rules (17 to 21) read this.
    let recordOf patient (db: DatabaseState) =
        {
            Patient = patient
            Plans = signedOf patient db |> List.sortByDescending no
        }

    /// Rule 46. What was done, written by the party that did it, in the same act, and
    /// stamped with the moment. Newest first.
    let note (now: int) (what: string) (db: DatabaseState) =
        { db with Private.Audit = { At = now; What = what } :: db.Private.Audit }

    /// Concept 12: append-only, into the clinical store. `Session` is dropped from the
    /// copy: it is a bearer credential and points into the private store (Guarantee 4).
    let append (plan: TreatmentPlan) (db: DatabaseState) =
        let plan = { plan with Session = None }

        { db with
            Clinical.Signed =
                db.Clinical.Signed |> Map.add plan.Patient (plan :: signedOf plan.Patient db) }

// ───────────────────────────── the credential ─────────────────────────────  [ships]

/// Concept 7 and Rule 27.
module UserCredential =

    let fresh user = { User = user; Pin = None; AttemptCount = 0; LockedUntil = None }

    /// Rules 27 and 37. A PIN that is set or replaced starts with a count of zero, and
    /// lifts any lock that count had earned.
    let setPin pin c = { c with Pin = Some pin; AttemptCount = 0; LockedUntil = None }

    /// Rule 28. Is signing locked at this moment? A moment, not a state: the lock is a
    /// delay, and it passes on its own.
    let isLocked (now: int) (c: UserCredential) =
        match c.LockedUntil with
        | Some until -> now < until
        | None -> false

    /// Rule 28. `pinLockBase * 2^(count - wrongPinLimit)`. The entry that reaches the
    /// limit locks for `pinLockBase`; each one after it doubles that.
    let lockFor (count: int) = pinLockBase * (pown 2 (max 0 (count - wrongPinLimit)))

    /// Rules 23, 28. A locked credential verifies nothing, correct PIN or not, until
    /// the delay passes. After that it verifies as before.
    let verify (now: int) (pin: Pin) (c: UserCredential) =
        let locked = isLocked now c

        match c.Pin with
        // A correct PIN inside the delay still signs nothing: the delay answers what
        // has already happened, and only waiting lifts it. It costs nothing either,
        // because the count is for wrong entries.
        | Some p when p = pin && not locked -> true, { c with AttemptCount = 0; LockedUntil = None }
        | Some p when p = pin -> false, c
        | _ ->
            // Rule 28. A wrong entry counts even while locked, so the delay grows
            // with each guess and not with each guess that waited politely.
            let count = c.AttemptCount + 1
            let until = if count >= wrongPinLimit then Some(now + lockFor count) else None
            false, { c with AttemptCount = count; LockedUntil = until }

    /// Rule 28. A wrong entry at the limit ends the Session (Rule 10) and locks the
    /// credential, so the next Session cannot carry on trying.
    let atLimit c = c.AttemptCount >= wrongPinLimit

    let attemptsLeft c = max 0 (wrongPinLimit - c.AttemptCount)

// ───────────────────────────── the confirmation code ─────────────────────────────  [ships] — with a real mac

/// Rule 37. The Database holds the mac and not the code, so what is stored is not
/// what was sent. Same trick as a token, and the same placeholder.
module Reset =

    let private secret = "reset-key-known-only-to-genpres"

    let macOf (ConfirmationCode c) = $"mac|%s{secret}|reset|%s{c}"

    /// What the mail says. It carries the code and nothing else about the Session.
    /// The same words serve both entrances (Rule 37): at an enrolment there is no PIN
    /// to reset, so the mail does not claim there is.
    let mail (ConfirmationCode c) = $"GenPRES PIN: use confirmation code %s{c} once, and soon"

// ───────────────────────────── the session record ─────────────────────────────  [ships]

module SessionRecord =

    /// Rule 11, on the one axis that decides it: a User who closed was offered the
    /// save, so there is nothing to tell them. Every other ending owes a notice.
    /// Three branches, not four: a Server restart is no longer an ending (Rule 10).
    let owesNotice =
        function
        | ClosedByUser -> false
        | ReplacedInBrowser -> false
        // Rule 10: a launched Session can reach its absolute limit too, and that is
        // worth telling. `endWith` still gates on the record having a User, so an
        // anonymous expiry owes nothing and there is nobody to tell.
        | Expired -> true
        | Idle | Superseded | WrongPinLimit -> true

    /// Ending is idempotent: a record already ended is left alone, so the first mark
    /// stands, and so does the obligation it created.
    let endWith mark now (s: SessionRecord) =
        match s.State with
        | Ended _ -> s
        | OpenOrGone ->
            { s with
                State = Ended(mark, now)
                // Rule 14: an anonymous Session binds to no User, and Rule 11 speaks
                // of the Session's User, so an ending here can owe nobody anything.
                Notice = if s.User.IsSome && owesNotice mark then Owed else NotOwed }

    /// Rule 11. Owed, or delivered and not yet acknowledged: either way the User may
    /// be shown it. The acknowledgement ends the obligation, not the delivery, because
    /// the Server cannot see a screen (Consequence 6).
    let tellsAtNextOpportunity (s: SessionRecord) =
        match s.Notice with
        | Owed -> true
        | Delivered _ -> true
        | NotOwed
        | Acknowledged _ -> false

    /// Whether ending this record now would leave the User owed a notice: either it
    /// is still open, and this launch is about to close it (Rule 8), or it ended
    /// earlier in a way nobody has yet mentioned.
    let wouldOweNotice (s: SessionRecord) =
        match s.State with
        | OpenOrGone -> true
        | Ended _ -> tellsAtNextOpportunity s

    /// Rule 11. Puts the notice in front of the User. At-least-once: a delivery that
    /// is never acknowledged may happen again, and the timestamp moves with it.
    /// Silent where nothing was owed, and silent once acknowledged.
    let delivered now (s: SessionRecord) =
        match s.Notice with
        | Owed
        | Delivered _ -> { s with Notice = Delivered now }
        | NotOwed
        | Acknowledged _ -> s

    /// Rule 11. The User says they have seen it. It is never shown again.
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
    /// so the two can never disagree about what "too long" means. Rule 14: an
    /// anonymous Session has no idle clock at all.
    let hasIdledOut (now: int) (s: SessionRecord) =
        isOpen s && s.User.IsSome && now - s.LastSeen > sessionTtl

    /// Rule 10. The other end a Session cannot outlive: its outright limit, which no
    /// amount of use extends. Every Session carries one. For anonymous Sessions it
    /// bounds the records they leave behind (Rule 14); for launched ones it bounds how
    /// long one launch stands for the person who made it.
    let hasExpired (now: int) (s: SessionRecord) =
        isOpen s && (match s.ExpiresAt with Some at -> now > at | None -> false)

    /// Rules 9 and 41. Both ends, and the mark each earns. A request arriving past
    /// either one ends the Session then and there rather than refreshing it. This is
    /// what an arrival asks, not `hasIdledOut` alone, which a talkative Client can
    /// hold off for ever.
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
    /// secret. Verifying recomputes with the subkey of the purpose expected, so a token
    /// of another purpose fails by key rather than by a field comparison.
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

    /// Rule 34. Minted at the opening of a Session, and re-minted whenever the
    /// baseline moves: an open (Rule 18) or a Submission both make a new TreatmentPlan
    /// the one Rules 20 and 21 are measured from.
    let mintOpened now s p (n: TreatmentPlanId option) : OpenedToken =
        mint TokenPurpose.Opened now s p (n |> Option.toList |> List.map (fun (TreatmentPlanId i) -> i))

    /// Rules 43, 44. Minted after the Rule 20 pre-check, naming the digest of the
    /// WorkPlan the User was shown and the KnowledgeRuleSet it was checked under.
    let mintChallenge now s p (digest: string) (RuleSetVersion v) : SigningChallenge =
        mint TokenPurpose.Challenge now s p [ digest; string v ]

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

    let verifyChallenge (t: SigningChallenge) = verifyAs TokenPurpose.Challenge t

    let verifyDataNotice (t: DataNoticeToken) = verifyAs TokenPurpose.DataNotice t

    /// Rule 34's one name: the TreatmentPlan the Session opened with, if any.
    let plan (t: OpenedToken) = t.Claim.Names |> List.tryHead |> Option.map TreatmentPlanId

    /// Rules 43 and 44: the one digest the token names.
    let digest (t: Token) = t.Claim.Names |> List.tryHead

    /// Rule 44. The KnowledgeRuleSet a challenge was issued under (Concept 18).
    let ruleSet (t: SigningChallenge) =
        t.Claim.Names |> List.tryItem 1 |> Option.map (int >> RuleSetVersion)

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
    /// nonce, the tick, and a mac over the three. No login: Rule 4 takes the User from
    /// the browser, so a Launch has nothing to say about it.
    let mintLaunch (patient: PatientId option) (now: int) : Launch =
        let l = { Patient = patient; Nonce = launchNonceAt patient now; IssuedAt = now; Mac = "" }
        { l with Mac = launchMac l }

    /// Rules 2 and 3's precondition: this really was sealed under the shared key.
    let verifyLaunch (l: Launch) = l.Mac = launchMac { l with Mac = "" }

// ───────────────────────────── the reducer ─────────────────────────────  [mixed]
// The branch bodies are the design: which leg follows which, what each act checks,
// and in what order (Rule 42 above all). The dispatch on (From, To, Msg) is [model],
// standing in for real endpoints, handlers, and the Database's own transaction.

module Hospital =

    let empty =
        {
            Workstation =
                {
                    ActiveUser = None
                    ActivePatient = None
                    MayLaunch = Set.empty
                    KeyReadable = true
                    NextTab = 1
                }
            Registry    = { Users = Map.empty; Active = Map.empty; Up = true }
            Platform    = { Data = Map.empty; Up = true }
            Database    =
                {
                    Clinical = { Signed = Map.empty }
                    Private =
                        {
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
            Env         = { Now = 0; RuleSet = RuleSetVersion 1 }
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
            Signing = false
            Modal = None
            DataOk = None
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
    /// A retry carries the same key, so the Database answers it instead of doing it
    /// twice.
    let private idemKey (BrowserId b) (now: int) = IdemKey $"idem-%i{b}-%04i{now}"

    // ── the in-flight table (Rule 32) ──

    let private putFlight rid ctx (h: Hospital) =
        { h with GenPres.InFlight = h.GenPres.InFlight |> Map.add rid ctx }

    let private dropFlight rid (h: Hospital) =
        { h with GenPres.InFlight = h.GenPres.InFlight |> Map.remove rid }

    /// Rule 27, both halves: the User is mailed, and the change is recorded. Rule 46:
    /// the audit line names the address it went to, so a User who says they never got
    /// it can be answered.
    let private pinChanged (MailAddress a as addr) (what: string) =
        [
            send GenPresServer MailService (SendMail(addr, what))
            send GenPresServer Environment (Noted $"%s{what}, mailed to %s{a}")
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

    /// UC-1 steps 8 and 9, and the last step of the anonymous open. Rule 19 has picked
    /// the TreatmentPlan the Session starts from, if there is one, and Rule 8's other
    /// Sessions of this User have been read back from the Database, because the Server
    /// keeps no copy of them (Rule 32).
    /// UC-1 steps 8 and 9, and the last step of the anonymous open. `ctx` is the
    /// launch this opens for, or None for an anonymous open, which has no Launch and
    /// so no nonce to spend.
    let private openSession
        (att: AttemptId)
        (ctx: LaunchCtx option)
        (client: ActorId)
        (launch: string option)
        (user: UserContext option)
        (mail: MailAddress option)
        (pctx: PatientContext)
        (start: TreatmentPlan option)
        (others: SessionRecord list)
        (replacing: SessionId option)
        (h: Hospital) =

        let sid = SessionId $"sid-%04i{h.GenPres.NextSessionId}"
        let no = SessionNo h.GenPres.NextSessionId

        // Rules 8 and 11 both speak of the Session's User, so neither applies to an
        // anonymous open (Rule 14). Rule 8 is per User, not per Patient: this closes
        // every other Session of *this* User, whichever Patient it was opened for,
        // and closes nobody else's.
        let priors =
            match user with
            | None -> []
            | Some uc ->
                others
                |> List.filter (fun r ->
                    SessionRecord.userId r = Some uc.UserId && SessionRecord.wouldOweNotice r)
                // endWith leaves a record that already ended alone, so a Session that
                // idled out keeps Idle, and keeps the obligation that ending created.
                // This launch is the chance to discharge it.
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

        // Rules 2, 8 and 40. One act: the nonce is spent, the record goes in, and the
        // User's other Sessions close with it. `priors` is what the notice is built
        // from, not what closes them — the closing is the Database's, and its view is
        // what decides. Nothing is said to the Client until the act has answered,
        // because it can still refuse (Rule 2).
        ignore orders

        let h = { h with GenPres.NextSessionId = h.GenPres.NextSessionId + 1 }

        let stage =
            match ctx with
            | Some launchCtx -> AwaitingOpen(launchCtx, record, pctx, start, priors)
            | None -> AwaitingAnonymousOpen(client, record, pctx)

        { h with GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now stage) },
        [ send GenPresServer GenPresDatabase (OpenSessionClosingOthers(ForLaunch att, record, replacing)) ]

    /// Rule 2's replay clause. Nothing is written: the record is there and the nonce
    /// is spent. Only the OpenedToken is fresh, because the first one may be gone.
    let private resumeSession
        (client: ActorId)
        (record: SessionRecord)
        (pctx: PatientContext)
        (start: TreatmentPlan option)
        (h: Hospital) =

        let orders = start |> Option.map _.Orders |> Option.defaultValue []
        let token = Token.mintOpened h.Env.Now record.Id pctx.Patient (start |> Option.map _.Id)

        h,
        [
            send GenPresServer client
                (SessionOpened(record.Id, record.No, record.User, pctx, orders, token))
        ]

    /// UC-1 steps 6 and 7, and where they are skipped. A Reader arrives here
    /// straight from the registry (Rule 26); a Prescriber only once the PIN question
    /// is settled (Rules 24, 25).
    let private afterCredential att (ctx: LaunchCtx) uc mail (h: Hospital) =
        match ctx.Launch.Patient with
        | None ->
            // ext 1a: no Patient, so no data to fetch and no record to read. Rule 8
            // still applies and this User's other Sessions close, so the
            // SessionRecords are still read.
            let pctx = { Patient = None; Data = None }
            { h with
                GenPres.Pending =
                    h.GenPres.Pending
                    |> Map.add att (pend h.Env.Now (AwaitingPriors(ctx, uc, mail, pctx, None))) },
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
            // Rule 46: every launch, honoured or refused. The Client is deliberately
            // told no reason; the audit is where the reason goes.
            send GenPresServer Environment (Noted $"launch refused: %A{reply}")
            send GenPresServer client reply
        ]

    // ── creating a TreatmentPlan: the Server's part, which is small ──

    /// Rule 42. The Server gathers what only it can know (the re-taken Role, the
    /// re-read data) and decides nothing itself.
    let private commit rid (ctx: RequestCtx) (r: SessionRecord) (req: Submission) role (h: Hospital) =
        h |> putFlight rid { ctx with Stage = AwaitingCommit r },
        [
            send GenPresServer GenPresDatabase
                (CommitTreatmentPlan(ForRequest rid, { Sid = r.Id; Req = req; Role = role }))
        ]

    /// The SessionRecord has come back, the Session is open, and Rule 9's clock has
    /// been refreshed. Rule 33 bites here: the User and the Patient are read off the
    /// record and the payload is believed about nothing. Concept 15 says what a User
    /// may do inside a Session, and what they may not.
    let private dispatch rid (ctx: RequestCtx) (r: SessionRecord) (h: Hospital) =
        // Rule 46: every refused request, not only the ones the Database decides. A
        // request turned away here never reaches the Database, so this is the only
        // place it can be recorded.
        let refuse msg =
            let (SessionId sid) = r.Id

            dropFlight rid h,
            [
                send GenPresServer Environment (Noted $"request refused for %s{sid}: %A{msg}")
                send GenPresServer ctx.Client msg
            ]

        // Rule 13: a Session without a PatientId lets the User prescribe, Patient
        // Data included, but a TreatmentPlan cannot be opened or created.
        let withPatient f =
            match r.Patient with
            | None -> refuse NoTreatmentPlanHere
            | Some p -> f p

        // Roles: a Reader may never create a TreatmentPlan. Rule 14: an anonymous Session
        // has no User at all, so there is nobody to create as and nobody to sign as.
        let withPrescriber f =
            match r.User with
            | Some uc when uc.Role = Prescriber -> f uc
            | _ -> refuse NotPermitted

        match ctx.Cmd with
        | Compute orders ->
            // Rule 32. The answer is computed from the payload and nothing is kept.
            // The cart goes home with the reply, as it arrived with the request.
            dropFlight rid h, [ send GenPresServer ctx.Client (Computed orders) ]

        | CloseSession ->
            // Rule 10: closing is an explicit act in the Client. Rule 11 adds
            // nothing, because it speaks only of endings the User did not cause.
            dropFlight rid h, [ send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, ClosedByUser)) ]

        | ResetPin ->
            // UC-6 step 1. Rule 37 removes nothing. A one-time code goes to the
            // address the registry gave (Rule 27), and the PIN in force stands until
            // that code replaces it, so nobody at this workstation gets a window in
            // which to set a PIN of their own.
            //
            // Rule 27. The address is asked for first, and the reset is parked only
            // once it is in hand: a code parked but never sent would sit there until
            // it expired, blocking the next reset for no reason (Rule 37).
            match r.User with
            | None -> refuse NotPermitted
            | Some uc ->
                let code = ConfirmationCode $"code-%04i{h.Env.Now}"
                h |> putFlight rid { ctx with Stage = AwaitingResetAddress(r, code) },
                [ send GenPresServer UserRegistry (ResolveUser(ForRequest rid, uc.Login)) ]

        | SupplyResetCode(code, pin) ->
            // UC-6 step 2. The Server carries the answer no further than the Database:
            // the code is checked and the PIN replaced there, in one act (Rule 37).
            //
            // Rule 27. The address first here too, so that a PIN is never replaced
            // without the User being told it was.
            match r.User with
            | None -> refuse NotPermitted
            | Some uc ->
                h |> putFlight rid { ctx with Stage = AwaitingPinAddress(r, code, pin) },
                [ send GenPresServer UserRegistry (ResolveUser(ForRequest rid, uc.Login)) ]

        | OpenTreatmentPlan _ ->
            withPatient (fun p ->
                match r.User with
                | None -> refuse NotPermitted            // Rule 13
                | Some _ ->
                    h |> putFlight rid { ctx with Stage = AwaitingPatientRecord r },
                    [ send GenPresServer GenPresDatabase (ReadRecord(ForRequest rid, p)) ])

        | RequestSignChallenge _ ->
            // Rule 43, and the order UC-3 ext 2a turns on. Rule 20 is settled against
            // the PatientRecord first, and only then is a challenge issued, so the User
            // is never asked for a PIN they were never going to spend.
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
                        // Rule 38. Signing is a fresh act of authority, so the Role is
                        // taken from the registry again, before anything else. A
                        // signature nobody is entitled to costs no PIN attempt
                        // (Rule 28).
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

        // ── the published knowledge ──

        // Concept 18. Publishing is not an act of any Actor here: the set is the world
        // the Server computes in, and it changes under open Sessions without telling
        // them. What no longer fits shows up at the next computation.
        | Environment, Environment, PublishRuleSet v ->
            { h with Env.RuleSet = v },
            [ send Environment Environment (Noted $"knowledge rule set published: %A{v}") ]

        | _ -> refused h env

    /// Actor 1. Invariant 1: one active Patient at a time.
    let private updateWorkstation (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Actor 1: the MainEHR Workstation ──

        | User, MainEhrWorkstation, LogIn u -> { h with Workstation.ActiveUser = Some u }, []

        // Rule 6. MainEHR is where a Patient is made active and the registry is what
        // GenPRES can ask about it, so the two move together (Invariant 1).
        | User, MainEhrWorkstation, SelectPatient p ->
            { h with
                Workstation.ActivePatient = Some p
                Registry.Active =
                    match h.Workstation.ActiveUser with
                    | Some u -> h.Registry.Active |> Map.add u p
                    | None -> h.Registry.Active }, []

        | User, MainEhrWorkstation, ClearPatient ->
            { h with
                Workstation.ActivePatient = None
                Registry.Active =
                    match h.Workstation.ActiveUser with
                    | Some u -> h.Registry.Active |> Map.remove u
                    | None -> h.Registry.Active }, []

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
            // UC-1 ext 2a. The key cannot be read, so nothing can be sealed. The script
            // reports it and exits; no Launch exists and no browser is opened.
            | Some _ when not h.Workstation.KeyReadable ->
                h, [ send MainEhrLaunchScript User (LaunchError "the launch key could not be read") ]
            | Some _ ->
                // ext 1a: no active Patient is not an error (Rule 13). Concept 3: it
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

        // Rules 5, 6. One question, three answers: who they are, how to mail them,
        // and which Patient they have active in MainEHR.
        | GenPresServer, UserRegistry, ResolveUser(tag, login) ->
            match h.Registry.Users |> Map.tryFind login with
            | Some(uc, mail) ->
                let active = h.Registry.Active |> Map.tryFind login
                h, [ send UserRegistry env.From (UserResolved(tag, uc, mail, active)) ]
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
    /// 40, 41), who may create (Rules 14, 26, 38), every token (Rules 33, 34, 43, 44),
    /// what the record allows (Rules 19, 20, 36), and last of all the PIN (Rules 23,
    /// 28). The PIN is last so that a Submission which was never going to land costs
    /// no attempt. Either everything is written or nothing happened. The Id and the
    /// ordering are minted here, because ordering a record is the same authority as
    /// deciding what may join it.
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
        // Rules 9 and 41, inside the act. A Session past either end — the idle clock
        // or the outright limit — ends here rather than signing anything. Checking
        // only the first would let a talkative Client sign for ever on one launch.
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
            // Rule 14: an anonymous Session has nobody to create as. Rule 13: and
            // a Session without a Patient has nothing to create against.
            | None, _
            | _, None -> refuse h RoleRefused
            | Some uc, Some patient ->

            let pr = h.Database |> Database.recordOf patient

            let openedWith = Token.plan opened

            let basePlan = pr.Plans |> List.tryFind (fun x -> Some x.Id = openedWith)

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

            // Rules 33, 34. The baseline is the Server's own word, handed back.
            elif not (Token.verifyOpened opened) then
                refuse h (TokenRefused "the opened-with token does not verify (Rule 34)")
            elif opened.Claim.Sid <> r.Id || opened.Claim.Patient <> r.Patient then
                refuse h (TokenRefused "the opened-with token is for another Session (Rule 34)")

            // Concept 17 and Rule 34. A token works exactly once and only within its
            // lifetime: the Submission it accompanies consumes it, and a spent or aged
            // one is worth no more than one the Client made up. Settled before the
            // token is read for anything, because an unbelievable token cannot answer
            // Rule 20 either.
            elif opened.Claim.ExpiresAt < h.Env.Now then
                refuse h (TokenRefused "the opened-with token has expired (Rule 34)")
            elif h.Database.Private.Spent.Contains opened.Claim.Nonce then
                refuse h (TokenRefused "the opened-with token was already spent (Rule 34)")

            // Rule 33 and Guarantee 1. The PatientId is the one thing no
            // TreatmentPlan may change, and the payload does not get a vote on it.
            elif req.Work.Orders |> List.exists (fun o -> o.Patient <> None && o.Patient <> Some patient) then
                refuse h (TokenRefused "an OrderContext names another Patient (Rule 33)")

            // Concept 10. An OrderContext has an identity, and a WorkPlan naming
            // one twice says two things about the same thing: Rule 42 refuses the
            // Submission whole rather than choosing between them.
            elif (req.Work.Orders |> List.map _.Id |> List.distinct |> List.length)
                 <> req.Work.Orders.Length then
                refuse h (TokenRefused "an OrderContext appears twice (Concept 10)")

            // Rule 20, and Rule 36 with it: the check and the append are the same
            // act, so there is no window between them to lose.
            elif (PatientRecord.blocking openedWith pr).IsSome then
                let blocker = (PatientRecord.blocking openedWith pr).Value
                refuse h (BlockedBy blocker.By)

            elif not dataTokenStands then
                refuse h (TokenRefused "the Patient Data token does not name this data (Rule 44)")

            // Concept 14. Signing is the only way a TreatmentPlan is created, so a
            // Submission without a PIN creates nothing.
            elif req.Pin.IsNone then
                refuse h (TokenRefused "a Submission is a signature, and carries a PIN (Concept 14)")

            // Rule 43. A signature answers for the exact WorkPlan the User was
            // shown, and for no other.
            elif (match req.Challenge with
                     | Some t ->
                         not (Token.verifyChallenge t)
                         || t.Claim.Sid <> r.Id
                         || t.Claim.ExpiresAt < h.Env.Now
                         || h.Database.Private.Spent.Contains t.Claim.Nonce
                         || Token.digest t <> Some(WorkPlan.signingDigest req.Work)
                     | None -> true) then
                refuse h (TokenRefused "the signing challenge does not name this plan (Rule 43)")

            else
                // Rules 23 and 28, last of all.
                let credential =
                    h.Database.Private.Credentials
                    |> Map.tryFind uc.UserId
                    |> Option.defaultValue (UserCredential.fresh uc.UserId)

                // Rule 28. Reaching the limit ends the Session (Rule 10). An attempt
                // against an already-locked credential is only refused, because this
                // Session did nothing wrong.
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
                        // Rule 10: at the limit the Session ends, here, in the same
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
                            By = uc                                            // Rule 15
                            Base = basePlan |> Option.map _.Id                 // Concept 13
                            Orders = req.Work.Orders |> stampAgainst uc basePlan  // Rule 35
                            // Concept 13: what it was built on, and where that
                            // came from, kept with it.
                            Data = req.Work.Data
                            From = req.Work.From
                            // Rule 44. The KnowledgeRuleSet the challenge was issued
                            // under, so the plan can be explained from it (Concept 18).
                            RuleSet =
                                req.Challenge
                                |> Option.bind Token.ruleSet
                                |> Option.defaultValue h.Env.RuleSet
                            Session = Some r.Id                                // Concept 13
                            At = h.Env.Now
                        }

                    let h = withCredential h

                    let (TreatmentPlanId planId) = plan.Id
                    let (UserId by) = uc.UserId

                    // Concept 17. The tokens this Submission rested on are spent here,
                    // in the same act that honoured them (Rule 42), so it cannot be
                    // replayed with the same word from the Server.
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
                        |> Database.note h.Env.Now $"%s{planId} signed by %s{by}"

                    { h with Database = db }, [ reply (Ok plan) ]

    // Rules 8 and 40, as one act, so no interval exists in which one User or one
    // browser holds two, whichever order two launches arrive in. Both limits are
    // decided from what the Database holds, not from what a Client says.


    /// Rules 8 and 40, as one act, so no interval exists in which one User or one
    /// browser holds two, whichever order two launches arrive in. Both limits are
    /// decided from what the Database holds, not from what a Client says.
    let private dbOpenSession (h: Hospital) (env: Envelope) tag (r: SessionRecord) replacing =
        let now = h.Env.Now
        let (SessionNo sno) = r.No

        let who =
            match r.User with
            | Some uc -> let (UserId u) = uc.UserId in u
            | None -> "anonymous"

        // Two endings, told apart by what they owe. The same browser is the User's
        // own act and owes nothing (Rule 11); other Sessions are Superseded and do.
        // The browser is read off the record and not off `replacing`, so a Client that
        // names nothing cannot keep two. `replacing` is honoured as well, which costs
        // nothing and covers a record without the field.
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

        // Rule 46. The opening, and every Session it ended with it (Rule 8).
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

        // Rule 2, and the act that makes it hold. The nonce is spent here, in the same
        // act that opens the Session, so a launch cannot spend one and then fail to
        // open. Two presentations that both passed the early check cannot both open
        // either: only one finds the nonce unspent at this point.
        match r.Launch with
        | Some nonce when h.Database.Private.Spent.Contains nonce ->
            let opened = h.Database.Private.Sessions |> List.tryFind (fun x -> x.Launch = Some nonce)
            h, [ send GenPresDatabase env.From (LaunchReplayed(tag, opened)) ]
        | _ ->

        let spend (db: DatabaseState) =
            match r.Launch with
            | Some nonce -> { db with Private.Spent = db.Private.Spent |> Set.add nonce }
            | None -> db

        // The SessionId counter never reissues, so an id already present is a replay,
        // and a replay must not resurrect what has since ended.
        if closed |> List.exists (fun x -> x.Id = r.Id) then
            { h with Database.Private.Sessions = closed },
            [ send GenPresDatabase env.From (SessionWasOpened tag) ]
        else
            let db = h.Database
            { h with Database = { db with Private.Sessions = r :: closed } |> spend |> note },
            [ send GenPresDatabase env.From (SessionWasOpened tag) ]

    // Rule 40. Conditional: an already ended record keeps the mark it ended with,
    // and the obligation that ending created (Rule 11).

    /// Actor 5. The Server is its only writer, and every write is one conditional act
    /// guarded by the state it expects (Rule 40).
    let private updateDatabase (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ── Actor 5: the GenPRES Database. The Server is its only writer. ──

        | GenPresServer, GenPresDatabase, ReadCredential(tag, user) ->
            h, [ send GenPresDatabase env.From (CredentialRead(tag, h.Database.Private.Credentials |> Map.tryFind user)) ]

        | GenPresServer, GenPresDatabase, ReadRecord(tag, p) ->
            h, [ send GenPresDatabase env.From (RecordRead(tag, h.Database |> Database.recordOf p)) ]
        | GenPresServer, GenPresDatabase, CommitTreatmentPlan(tag, c) -> dbCommit h env tag c

        | GenPresServer, GenPresDatabase, OpenSessionClosingOthers(tag, r, replacing) ->
            dbOpenSession h env tag r replacing

        // Rule 40. Conditional: an already ended record keeps the mark it ended with,
        // and the obligation that ending created (Rule 11).
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
        | GenPresServer, GenPresDatabase, NoteMailUsed(sid, addr) ->
            { h with
                Database.Private.Sessions =
                    h.Database.Private.Sessions
                    |> List.map (fun x ->
                        if x.Id = sid && SessionRecord.isOpen x then { x with Mail = Some addr } else x) }, []

        | GenPresServer, GenPresDatabase, TouchIfOpen sid ->
            { h with
                Database.Private.Sessions =
                    h.Database.Private.Sessions
                    |> List.map (fun x ->
                        if x.Id = sid && SessionRecord.isOpen x then x |> SessionRecord.seen h.Env.Now
                        else x) }, []

        // Rule 11. The notice went out; whether it was seen is not the Server's to
        // know (Consequence 6), so this may happen more than once.
        | GenPresServer, GenPresDatabase, MarkDelivered sid ->
            { h with
                Database.Private.Sessions =
                    h.Database.Private.Sessions
                    |> List.map (fun x -> if x.Id = sid then x |> SessionRecord.delivered h.Env.Now else x) }, []

        // Rule 11. Honoured only from a Session that is open, launched and the same
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
                        |> Database.note h.Env.Now $"acknowledgement refused: %s{a} may not answer for %s{e} (Rule 11)" },
                []

        | GenPresServer, GenPresDatabase, ReadSessionRecord(tag, sid) ->
            let record = h.Database.Private.Sessions |> List.tryFind (fun x -> x.Id = sid)
            // Rule 21. The head goes back with the record: the Server has no
            // PatientRecord of its own to compare against (Rule 32).
            let head =
                record
                |> Option.bind _.Patient
                |> Option.bind (fun p -> h.Database |> Database.recordOf p |> PatientRecord.latest)
            h, [ send GenPresDatabase env.From (SessionRecordRead(tag, record, head)) ]

        | GenPresServer, GenPresDatabase, ReadSessionRecords tag ->
            h, [ send GenPresDatabase env.From (SessionRecordsRead(tag, h.Database.Private.Sessions)) ]

        // Rule 2, as one conditional operation (Rule 40). Test and mark cannot be two
        // acts, or two browsers at once would both find it unspent. When it was spent
        // already the answer carries the record, which is what the replay needs.
        // Rule 2, the early check: a read and nothing else. It refuses a launch that
        // was plainly spent before anything is fetched for it, which is worth doing,
        // but it is not what makes the spend safe. The open is (Rule 40).
        | GenPresServer, GenPresDatabase, CheckLaunchSpent(tag, nonce) ->
            if h.Database.Private.Spent.Contains nonce then
                let opened = h.Database.Private.Sessions |> List.tryFind (fun x -> x.Launch = Some nonce)
                h, [ send GenPresDatabase env.From (LaunchReplayed(tag, opened)) ]
            else
                h, [ send GenPresDatabase env.From (LaunchUnspent tag) ]

        // Rule 46. A count per source and nothing else: no SessionRecord, and no audit
        // line per refused request, which would be the same flood by another name
        // (Rule 14). All a flood can grow here is one integer.
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
        // both the reset and the credential. A code that verifies replaces the PIN and
        // is spent in the same act; one that does not changes only its own count.
        // Rule 28: a newly set PIN starts at zero.
        | GenPresServer, GenPresDatabase, ReplacePinIfCode(tag, user, code, pin) ->
            let (UserId who) = user

            // Rule 46. A code that bought nothing is an event, at an enrolment (UC-2
            // ext 2b, 2c) as at a reset (UC-6 ext 2a). The audit is where an attempt
            // shows up.
            let db = h.Database

            let refuse (after: DatabaseState) failure =
                { h with
                    Database = after |> Database.note h.Env.Now $"PIN confirmation code refused for %s{who}: %A{failure}" },
                [ send GenPresDatabase env.From (ResetRefused(tag, failure)) ]

            let without = { db with Private.Resets = db.Private.Resets |> Map.remove user }

            match db.Private.Resets |> Map.tryFind user with
            | None -> refuse db NoResetPending
            | Some pending when h.Env.Now > pending.Expires -> refuse without ResetExpired
            | Some pending when pending.CodeMac <> Reset.macOf code ->
                let tried = { pending with Wrong = pending.Wrong + 1 }

                if tried.Wrong >= wrongConfirmationCodeLimit then
                    refuse without ResetVoid
                else
                    refuse { db with Private.Resets = db.Private.Resets |> Map.add user tried }
                           (WrongCode(wrongConfirmationCodeLimit - tried.Wrong))
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
        // the records are in the Database (Rule 32), so the sweep is a read like any
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
            // ext 3c, and Rule 4. The browser proved nobody, and the Launch names no
            // login to fall back on. The nonce is not even spent: nothing happened.
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
                    [ send GenPresServer GenPresDatabase (CheckLaunchSpent(ForLaunch att, launch.Nonce)) ]

        // UC-2 steps 2 and 3. The launch has been suspended on a human, possibly for
        // a long while, and nothing else was offered meanwhile.
        | (GenPresClient _ as sender), GenPresServer, SupplyPin(att, code, pin) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            // The prompt was put to one Client and is that Client's to answer. A
            // second browser answering would set this PIN from another screen; the
            // code is what makes that fail even at the right screen (ext 2c).
            | Some(AwaitingPinChoice(ctx, uc, mail)) when ctx.Client = sender ->
                // Rule 27. The address is asked for again here, not reused from the
                // launch. This stage waited on a human, so the launch's answer may be
                // old by now — the same reason a reset asks on the request that mails
                // (UC-6 step 2).
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending
                        |> Map.add att (pend h.Env.Now (AwaitingEnrolAddress(ctx, uc, mail, code, pin))) },
                [ send GenPresServer UserRegistry (ResolveUser(ForLaunch att, uc.Login)) ]
            | Some(AwaitingPinChoice _) ->
                // Answered by a Client that was never asked. Not merely dropped: an
                // envelope like this is worth alerting on.
                h, [ send GenPresServer Environment (Refused env) ]
            | _ -> h, []

        // Rule 11. Nothing is read and nothing else is decided: the User has seen the
        // notice, and the record stops owing one.
        | GenPresClient _, GenPresServer, AckSessionNotice(acknowledging, about) ->
            h, [ send GenPresServer GenPresDatabase (MarkAcknowledged(acknowledging, about)) ]

        | GenPresClient _, GenPresServer, OpenAnonymous replacing ->
            // Rule 14. An anonymous open costs the Server one SessionRecord and
            // nothing more (Rule 32), so the bound on the cost is how many may stand at
            // once. Above the bound the answer is a refusal that writes nothing.
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
                // Rule 14. An anonymous open has no Launch and so no nonce, but it is
                // still told from the Database's answer, like any other open.
                let att = AttemptId h.GenPres.NextAttempt

                { h with GenPres.NextAttempt = h.GenPres.NextAttempt + 1 }
                |> openSession att None env.From None None None { Patient = None; Data = None } None [] replacing

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 4: the GenPRES Server, in Session — one request, several legs
        // ══════════════════════════════════════════════════════════════════════

        // Rule 32 in one branch. A request arrives with everything but who sent it,
        // and that is in the Database. Rule 9's refresh has one home, and this is it.
        | GenPresClient _, GenPresServer, SessionRequest(sid, opened, cmd) ->
            let rid = RequestId h.GenPres.NextRequest
            let ctx = { Sid = sid; Client = env.From; Opened = opened; Cmd = cmd; Stage = AwaitingSessionRecord }
            { h with GenPres.NextRequest = h.GenPres.NextRequest + 1 } |> putFlight rid ctx,
            [ send GenPresServer GenPresDatabase (ReadSessionRecord(ForRequest rid, sid)) ]

        | _ -> refused h env

    /// Actor 9's answers: a Role at a launch (Rule 5), and one re-taken at a
    /// signature (Rule 38).
    let private updateServerFromRegistry (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // Step 5. Rule 7: no Role, no Session, and no guessing either.
        | UserRegistry, GenPresServer, UserUnresolved(ForLaunch att, failure) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            // Rule 27. The confirmation code has already gone out and been answered
            // correctly, so Rule 37 is settled and only the notice is left. A notice
            // may go to the address this launch already had rather than not go, so the
            // PIN is still written. The audit says the address was the fallback.
            | Some(AwaitingEnrolAddress(ctx, uc, mail, code, pin)) ->
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinWritten(ctx, uc, mail))) },
                [
                    send GenPresServer Environment
                        (Noted "the registry could not be asked: the address this launch had stood (Rule 27)")
                    send GenPresServer GenPresDatabase (ReplacePinIfCode(ForLaunch att, uc.UserId, code, pin))
                ]
            | Some(AwaitingUser ctx) ->
                let reply =
                    match failure with
                    | NoRole -> NotAuthorised
                    | RegistryUnreachable -> AuthorityUnavailable
                refuseLaunch att ctx.Client reply h
            | _ -> h, []

        // UC-2 step 3. Rule 27, the fresh answer. The PIN is written now, and the
        // notice that follows goes to the address the registry gives here. Rule 37,
        // one implementation with two entrances: the code and the PIN go to the same
        // Database act a reset uses (UC-6), which creates the UserCredential if GenPRES
        // holds none and starts the count at zero (Rules 27, 28).
        | UserRegistry, GenPresServer, UserResolved(ForLaunch att, _, addr, _) when
            (match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
             | Some(AwaitingEnrolAddress _) -> true
             | _ -> false)
            ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingEnrolAddress(ctx, uc, _, code, pin)) ->
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinWritten(ctx, uc, addr))) },
                [ send GenPresServer GenPresDatabase (ReplacePinIfCode(ForLaunch att, uc.UserId, code, pin)) ]
            | _ -> h, []

        // Step 5. Rule 5: the Role is the registry's answer, never the launch's,
        // which never carried one (Concept 3). Rule 6: the Patient must be the one the
        // User really has active in MainEHR, which is the registry's answer too. A
        // Launch naming another Patient, or a User with none active, opens nothing
        // (Rule 7). So a stolen Launch gets the thief at most their own Session
        // (Guarantee 5).
        | UserRegistry, GenPresServer, UserResolved(ForLaunch att, uc, mail, active) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingUser ctx) when ctx.Launch.Patient <> None && ctx.Launch.Patient <> active ->
                // Rule 7. Nothing opens, and the Client is told no reason; the audit
                // is where the reason goes (Rule 46). The cure is to activate the right
                // Patient in MainEHR and relaunch (UC-1 ext 5b), so this Launch is not
                // worth presenting again.
                { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att },
                [
                    send GenPresServer Environment (Noted $"launch refused: %A{PatientNotActive} (Rule 6)")
                    send GenPresServer ctx.Client (LaunchRefused false)
                ]
            | Some(AwaitingUser ctx) ->
                match uc.Role with
                // Rule 26: a Reader is never asked for a PIN. Not asked and ignored,
                // but not asked at all: the credential stage is skipped whole.
                | Reader -> afterCredential att ctx uc mail h
                // Rule 24: every launch checks whether a PIN is set for the login.
                | Prescriber ->
                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingCredential(ctx, uc, mail))) },
                    [ send GenPresServer GenPresDatabase (ReadCredential(ForLaunch att, uc.UserId)) ]
            | _ -> h, []

        // Rule 27. The address, asked for at the moment it is needed and kept only
        // for the length of the request (Rule 32). Whatever the registry says now is
        // what the mail goes to, so a change of address takes effect at once and no
        // copy of it can go stale anywhere in GenPRES.
        | UserRegistry, GenPresServer, UserResolved(ForRequest rid, _, addr, _) when
            (match h.GenPres.InFlight |> Map.tryFind rid |> Option.map _.Stage with
             | Some(AwaitingResetAddress _ | AwaitingPinAddress _ | AwaitingLimitAddress _) -> true
             | _ -> false)
            ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            // UC-6 step 1. The address is in hand, so the reset can be parked.
            | Some({ Stage = AwaitingResetAddress(r, code) } as ctx) ->
                match r.User with
                | None -> dropFlight rid h, []
                | Some uc ->
                    h |> putFlight rid { ctx with Stage = AwaitingResetStarted(r, code, addr) },
                    [
                        send GenPresServer GenPresDatabase
                            (StartReset(ForRequest rid, uc.UserId, Reset.macOf code, h.Env.Now + confirmationCodeTtl))
                    ]

            // UC-6 step 2. The address is in hand, so the PIN can be replaced.
            | Some({ Stage = AwaitingPinAddress(r, code, pin) } as ctx) ->
                match r.User with
                | None -> dropFlight rid h, []
                | Some uc ->
                    h |> putFlight rid { ctx with Stage = AwaitingPinReplaced(r, addr) },
                    [ send GenPresServer GenPresDatabase (ReplacePinIfCode(ForRequest rid, uc.UserId, code, pin)) ]

            // Rules 10, 27. The Session ended at the limit and the screen has already
            // been answered; this is the mail catching up.
            | Some { Stage = AwaitingLimitAddress _ } ->
                let (MailAddress a) = addr
                dropFlight rid h,
                [
                    send GenPresServer MailService
                        (SendMail(addr, "GenPRES: the wrong-PIN limit was reached in your session"))
                    send GenPresServer Environment
                        (Noted $"wrong-PIN limit reached — the User was mailed at %s{a}")
                ]
            | _ -> h, []

        // Rule 38. The Role must still be there, and must still belong to the person
        // the SessionRecord names. A login that now resolves to someone else is not
        // this Session's User.
        | UserRegistry, GenPresServer, UserResolved(ForRequest rid, uc, _, _) ->
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
        // kept apart, because one is a withdrawal and the other is a registry that is
        // briefly down, in a Session that may sign again shortly.
        | UserRegistry, GenPresServer, UserUnresolved(ForRequest rid, failure) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            // Rule 38 makes the registry a hard dependency of every signature, so a
            // registry that is down would otherwise stop all signing everywhere.
            // `NoRole` still fails closed, because that is a withdrawal. "Cannot say"
            // is not a withdrawal, so for a bounded while the launch's Role stands for
            // it. Past `roleGrace` it fails closed as before.
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
            // Rule 37. A confirmation code is a credential, not a notice. All of what
            // the rule rests on is the code reaching an address the person at this
            // workstation does not control. So it is never sent to a remembered
            // address — only to one the registry gives now. No answer, no code, and
            // nothing is parked that could not be delivered (UC-6 ext 1c).
            | Some({ Stage = AwaitingResetAddress _ } as ctx) ->
                dropFlight rid h,
                [
                    send GenPresServer ctx.Client (ResetDenied AddressUnavailable)
                    send GenPresServer Environment
                        (Noted "no confirmation code: the registry could not say where to mail (Rules 27, 37)")
                ]

            // Rule 27. A notice is not a credential, so it may go to the address this
            // Session already had from the registry rather than not go at all. The
            // audit says it was a fallback, because it may be older than the registry's
            // answer would have been.
            | Some({ Stage = AwaitingPinAddress(r, code, pin) } as ctx) ->
                match r.User, r.Mail with
                | Some uc, Some addr ->
                    h |> putFlight rid { ctx with Stage = AwaitingPinReplaced(r, addr) },
                    [
                        send GenPresServer Environment
                            (Noted "the registry could not be asked: the address this Session had stood (Rule 27)")
                        send GenPresServer GenPresDatabase (ReplacePinIfCode(ForRequest rid, uc.UserId, code, pin))
                    ]
                // Nothing to fall back on, so the PIN is not replaced: Rule 27 would
                // have nowhere to send the notice it requires.
                | _ ->
                    dropFlight rid h,
                    [
                        send GenPresServer ctx.Client (ResetDenied AddressUnavailable)
                        send GenPresServer Environment
                            (Noted "no PIN change: no address to tell the User at (Rule 27)")
                    ]

            // The same, for the one notice that cannot ask first. The Session has
            // already ended at the limit and the screen has been answered.
            | Some { Stage = AwaitingLimitAddress r } ->
                dropFlight rid h,
                [
                    match r.Mail with
                    | Some(MailAddress a as addr) ->
                        send GenPresServer MailService
                            (SendMail(addr, "GenPRES: the wrong-PIN limit was reached in your session"))
                        send GenPresServer Environment
                            (Noted $"wrong-PIN limit reached — the User was mailed at %s{a}, on the address this Session had (Rule 27)")
                    | None ->
                        send GenPresServer Environment
                            (Noted "wrong-PIN limit reached — the User could not be mailed (Rule 27)")
                ]

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

        // Step 6. Concept 2: read once, at the launch, and not refreshed afterwards.
        // ext 6a: unavailable is not a failure. The PatientContext carries the
        // PatientId and no data, and the User fills the data in by hand.
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
        // changed, or the platform cannot say. The last two issue nothing; the User
        // accepts by returning the token and asks again. Any challenge is issued under
        // the KnowledgeRuleSet published now (Concept 18), so a set published while the
        // User worked is what the signature answers for.
        | PatientDataPlatform, GenPresServer, (PatientDataRead(ForRequest rid, _) | PatientDataUnavailable(ForRequest rid)) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingChallengeData(r, work, dataOk) } as ctx) ->
                let challenge () =
                    send GenPresServer ctx.Client
                        (SignChallengeIssued(
                            Token.mintChallenge
                                h.Env.Now ctx.Sid r.Patient (WorkPlan.signingDigest work) h.Env.RuleSet))

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

        // Rule 33: the User and the Patient of the request come from here. Rule 11:
        // where the Session is gone, this is the next opportunity to say so.
        | GenPresDatabase, GenPresServer, SessionRecordRead(ForRequest rid, record, head) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | None -> h, []
            | Some ctx ->
                match record with
                | None ->
                    dropFlight rid h,
                    [
                        send GenPresServer Environment (Noted "request refused: no such Session (Rule 46)")
                        send GenPresServer ctx.Client (SessionEnded None)
                    ]
                // Rule 11. Refused and told why, but nothing is discharged: whoever
                // holds an ended SessionId may be whoever sat down next, and telling
                // them is not telling the User. Delivery happens at a launch.
                | Some r when not (SessionRecord.isOpen r) ->
                    let mark = match r.State with Ended(m, _) -> Some m | OpenOrGone -> None
                    let (SessionId sid) = r.Id

                    dropFlight rid h,
                    [
                        send GenPresServer Environment
                            (Noted $"request refused for %s{sid}: the Session ended %A{mark} (Rule 46)")
                        send GenPresServer ctx.Client (SessionRefused mark)
                    ]
                // Rules 9, 41. Past either end the request ends the Session then and
                // there, rather than refreshing it back to life. Rule 10 again: the
                // screen is told and the notice is still owed.
                | Some r when (SessionRecord.outOfTime h.Env.Now r).IsSome ->
                    let mark = (SessionRecord.outOfTime h.Env.Now r).Value

                    let (SessionId sid) = r.Id

                    dropFlight rid h,
                    [
                        send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, mark))
                        send GenPresServer Environment
                            (Noted $"request refused for %s{sid}: out of time, %A{mark} (Rules 41, 46)")
                        send GenPresServer ctx.Client (SessionRefused(Some mark))
                    ]
                | Some r ->
                    // Rule 9. Every request refreshes the idle clock, and the clock
                    // is a field of the record, so refreshing it is a write. A guarded
                    // one (Rule 40): a Session that ended meanwhile is not touched.
                    let r = r |> SessionRecord.seen h.Env.Now
                    let refreshed = send GenPresServer GenPresDatabase (TouchIfOpen r.Id)

                    // Rules 21, 22. Two references compared: the head of the record
                    // against the plan the request's OpenedToken names. If the head is
                    // newer the response says whose it is and when it was signed. It
                    // gates nothing (Rule 20 is the only guard), so it rides along with
                    // whatever the dispatch answers.
                    let notice =
                        match ctx.Opened |> Option.filter Token.verifyOpened, head with
                        | Some tok, Some newest when Some newest.Id <> Token.plan tok ->
                            [ send GenPresServer ctx.Client (NewerPlanNotice(newest.By, newest.At)) ]
                        | _ -> []

                    let h, out = dispatch rid ctx r h
                    // CloseSession ends the record itself; anything else gets the
                    // refresh. Doing both would be harmless but noisy.
                    match ctx.Cmd with
                    | CloseSession -> h, out
                    | _ -> h, (refreshed :: out) @ notice

        // The PatientRecord came back for a request part-way through.
        | GenPresDatabase, GenPresServer, RecordRead(ForRequest rid, record) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingPatientRecord r } as ctx) ->
                match ctx.Cmd, r.User with
                // Rules 17 and 18. Opening the most recent TreatmentPlan is also how
                // a blocked User gets unblocked: Rule 34's token is re-minted over it,
                // so it becomes the TreatmentPlan the Session opened with and Rule 20 no
                // longer bites.
                | OpenTreatmentPlan id, Some _ ->
                    match record |> PatientRecord.mayOpen id with
                    | Some s ->
                        dropFlight rid h,
                        [ send GenPresServer ctx.Client
                            (TreatmentPlanOpened(
                                s.Id, s.Orders, Token.mintOpened h.Env.Now ctx.Sid r.Patient (Some s.Id))) ]
                    | None ->
                        dropFlight rid h, [ send GenPresServer ctx.Client NotPermitted ]
                // Rule 43. The pre-check first, and the challenge only if it passes.
                // Rule 20's block is the same answer a Submission would have got,
                // settled here, before any PIN is asked for.
                | RequestSignChallenge(work, opened, dataOk), Some _ ->
                    if not (Token.verifyOpened opened) || opened.Claim.Sid <> ctx.Sid then
                        dropFlight rid h,
                        [
                            send GenPresServer ctx.Client
                                (SubmissionRefused "the opened-with token does not verify (Rule 34)")
                        ]
                    else

                    match PatientRecord.blocking (Token.plan opened) record with
                    // Rule 20. The remedy is to open that TreatmentPlan (Rule 18),
                    // which makes it the one the Session opened with.
                    | Some blocker ->
                        dropFlight rid h, [ send GenPresServer ctx.Client (SubmissionBlocked blocker.By) ]
                    | None ->
                        // Rule 44. Nothing in the record stands in the way, so the
                        // last question is the data. Asked before the challenge is
                        // minted, so the commit needs no second reading.
                        match r.Patient with
                        | Some p ->
                            h |> putFlight rid { ctx with Stage = AwaitingChallengeData(r, work, dataOk) },
                            [ send GenPresServer PatientDataPlatform (ReadPatientData(ForRequest rid, p)) ]
                        | None -> dropFlight rid h, []
                | _ -> dropFlight rid h, []
            | _ -> h, []

        // Rule 42. The one act said yes. Rule 34: the Session now stands on what it
        // just created, so a fresh token goes back with the answer. Rules 20 and 21
        // are measured from this TreatmentPlan from here on.
        | GenPresDatabase, GenPresServer, TreatmentPlanCommitted(ForRequest rid, plan) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingCommit r } as ctx) ->
                dropFlight rid h,
                [ send GenPresServer ctx.Client
                    (TreatmentPlanSubmitted(
                        plan.Id,
                        Token.mintOpened h.Env.Now ctx.Sid r.Patient (Some plan.Id))) ]
            | _ -> h, []

        // Rule 42. The one act said no, and nothing happened. Each refusal is turned
        // into what the Client already understands, and into nothing more: Rule 20's
        // block names whose work stands in the way, never which TreatmentPlan it is.
        | GenPresDatabase, GenPresServer, CommitRefused(ForRequest rid, refusal) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingCommit r } as ctx) ->
                let out =
                    match refusal with
                    // Rule 11, as on the arrival path: refused and told what ended,
                    // and nothing discharged. Whoever is holding this SessionId need
                    // not be the User the notice is owed to.
                    | SessionNotOpen mark -> [ send GenPresServer ctx.Client (SessionRefused mark) ]
                    | RoleRefused -> [ send GenPresServer ctx.Client NotPermitted ]
                    | TokenRefused why -> [ send GenPresServer ctx.Client (SubmissionRefused why) ]
                    | BlockedBy who -> [ send GenPresServer ctx.Client (SubmissionBlocked who) ]
                    | PinWrong left -> [ send GenPresServer ctx.Client (PinRejected left) ]
                    | CredentialLocked _ -> [ send GenPresServer ctx.Client SigningLocked ]
                    | PinLimitReached ->
                        // Rules 10, 11, 27. Telling the screen is telling whoever is
                        // at it, and this is the one ending that means someone was
                        // guessing. So the screen is the last place the notice belongs:
                        // it is refused here and mailed to the address the registry
                        // holds, and the User is told at their next launch.
                        [ send GenPresServer ctx.Client (SessionRefused(Some WrongPinLimit)) ]

                // Rule 27, and the one place the address cannot be asked for first:
                // the limit is only discovered inside the commit (Rule 42), so the
                // screen is answered now and the mail waits on the registry.
                match refusal, r.User with
                | PinLimitReached, Some uc ->
                    h |> putFlight rid { ctx with Stage = AwaitingLimitAddress r },
                    out @ [ send GenPresServer UserRegistry (ResolveUser(ForRequest rid, uc.Login)) ]
                | _ -> dropFlight rid h, out
            | _ -> h, []

        // Rule 37. The reset is parked, so the code can go out to the address on the
        // SessionRecord (Concept 9): there is no Session in memory to hold one. The
        // record says a reset was asked for; it does not say the code.
        | GenPresDatabase, GenPresServer, ResetStarted(ForRequest rid, user) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingResetStarted(r, code, addr) } as ctx) ->
                let (UserId l) = user
                let (MailAddress a) = addr
                dropFlight rid h,
                [
                    send GenPresServer MailService (SendMail(addr, Reset.mail code))
                    // Rule 46 names where it went; Rule 27 records the address on the
                    // Session, so a later notice has something to fall back on.
                    send GenPresServer Environment (Noted $"PIN reset confirmation code sent for %s{l} to %s{a}")
                    send GenPresServer GenPresDatabase (NoteMailUsed(r.Id, addr))
                    send GenPresServer ctx.Client ResetCodeMailed
                ]
            | _ -> h, []

        // UC-6 step 2. Replaced, not removed. Rule 27: mailed and recorded, every
        // replacement as well as every setting.
        | GenPresDatabase, GenPresServer, PinReplaced(ForRequest rid, c) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingPinReplaced(r, addr) } as ctx) ->
                let (UserId l) = c.User
                dropFlight rid h,
                (pinChanged addr $"PIN replaced for %s{l}")
                @ [
                    send GenPresServer GenPresDatabase (NoteMailUsed(r.Id, addr))
                    send GenPresServer ctx.Client PinChanged
                  ]
            | _ -> h, []

        // Rule 37. The code bought nothing, and nothing changed.
        | GenPresDatabase, GenPresServer, ResetRefused(ForRequest rid, failure) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some ctx -> dropFlight rid h, [ send GenPresServer ctx.Client (ResetDenied failure) ]
            | None -> h, []

        // Written, and nothing more to say.
        | GenPresDatabase, GenPresServer, TreatmentPlanCommitted _

        | GenPresDatabase, GenPresServer, CommitRefused _

        | GenPresDatabase, GenPresServer, ResetStarted _

        | GenPresDatabase, GenPresServer, PinReplaced _

        | GenPresDatabase, GenPresServer, ResetRefused _

        | GenPresDatabase, GenPresServer, SessionRecordsRead _

        | GenPresDatabase, GenPresServer, LaunchUnspent _

        | GenPresDatabase, GenPresServer, LaunchReplayed _

        | GenPresDatabase, GenPresServer, SessionWasOpened _

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

        // Step 4 into 5. Rule 2: the nonce is not spent, so far as this read can say.
        // Nothing has been written for it yet — the open is what spends it — so this
        // answer is advisory and the launch carries on. Ask the registry who the
        // browser's identity belongs to. The Launch does not travel there.
        | GenPresDatabase, GenPresServer, LaunchUnspent(ForLaunch att) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingSpend ctx) ->
                { h with GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingUser ctx)) },
                [ send GenPresServer UserRegistry (ResolveUser(ForLaunch att, ctx.Identity)) ]
            | _ -> h, []

        // Rule 2's replay clause, and the one answer both places give. The nonce was
        // spent, so the question is by whom and how long ago. The same browser coming
        // back within the lifetime is a retry of the same launch (UC-1 ext 3a) and gets
        // the first answer: the same Session, not a second one. Anyone else, or too
        // late, gets nothing.
        //
        // Two stages reach here. `AwaitingSpend` is the early check, which refuses
        // before anything is fetched. `AwaitingOpen` is the open itself refusing,
        // because another presentation won the race since that check — and the answer
        // has to be the same one, or a race would be answered differently from a
        // replay that arrived a moment later.
        | GenPresDatabase, GenPresServer, LaunchReplayed(ForLaunch att, opened) ->
            // Rule 2's replay clause is one browser coming back and nothing else. The
            // same login elsewhere would put two browsers on one Session, which Rules 8
            // and 40 spend an act each to prevent.
            let mine (ctx: LaunchCtx) =
                let thisBrowser = match ctx.Client with GenPresClient b -> Some b | _ -> None

                opened
                |> Option.filter (fun r ->
                    SessionRecord.isOpen r
                    && (r.User |> Option.map _.Login) = Some ctx.Identity
                    && r.Browser = thisBrowser
                    && h.Env.Now - ctx.Launch.IssuedAt <= launchTtl)

            let refused (ctx: LaunchCtx) =
                let h, out = refuseLaunch att ctx.Client (LaunchRefused false) h
                h,
                send GenPresServer Environment
                    (Noted $"launch refused: %A{LaunchAlreadySpent} (Rule 2)")
                :: out

            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingSpend ctx) ->
                match mine ctx with
                | None -> refused ctx
                | Some r ->
                    let ctx = { ctx with Resuming = Some r }
                    { h with
                        GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingUser ctx)) },
                    [ send GenPresServer UserRegistry (ResolveUser(ForLaunch att, ctx.Identity)) ]

            // The open was refused. Everything this launch fetched is already in hand,
            // so the resume is answered from here rather than run again.
            | Some(AwaitingOpen(ctx, _, pctx, start, _)) ->
                match mine ctx with
                | None -> refused ctx
                | Some r ->
                    let h, out = resumeSession ctx.Client r pctx start h
                    { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att },
                    send GenPresServer Environment
                        (Noted "the open found the nonce spent: answered as a replay (Rules 2, 40)")
                    :: out
            | _ -> h, []

        // Rules 2, 40. The open committed, so the nonce is spent and the Session
        // stands. Only now is the Client told.
        | GenPresDatabase, GenPresServer, SessionWasOpened(ForLaunch att) ->
            let drop (h: Hospital) = { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att }

            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingOpen(ctx, r, pctx, start, priors)) ->
                let orders = start |> Option.map _.Orders |> Option.defaultValue []
                let token = Token.mintOpened h.Env.Now r.Id pctx.Patient (start |> Option.map _.Id)

                drop h,
                [
                    send GenPresServer ctx.Client (SessionOpened(r.Id, r.No, r.User, pctx, orders, token))
                    if not priors.IsEmpty then
                        send GenPresServer ctx.Client
                            (PriorSessionNotice(priors |> List.map (fun x -> x.No, x.State, x.Id)))
                ]

            | Some(AwaitingAnonymousOpen(client, r, pctx)) ->
                let token = Token.mintOpened h.Env.Now r.Id pctx.Patient None
                drop h, [ send GenPresServer client (SessionOpened(r.Id, r.No, r.User, pctx, [], token)) ]

            | _ -> h, []

        // Step 5, and UC-2 step 1. Rule 25: a Prescriber with no PIN must set one
        // before the launch continues, and only now, once the registry has said who
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
                    let code = ConfirmationCode $"code-%04i{h.Env.Now}"

                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending
                            |> Map.add att (pend h.Env.Now (AwaitingEnrolCode(ctx, uc, mail, code))) },
                    [
                        send GenPresServer GenPresDatabase
                            (StartReset(ForLaunch att, uc.UserId, Reset.macOf code, h.Env.Now + confirmationCodeTtl))
                    ]
            | _ -> h, []

        // UC-2 step 1. The code is parked so it can go out, and only now is the
        // Client asked for anything (Rules 27, 37, 46).
        | GenPresDatabase, GenPresServer, ResetStarted(ForLaunch att, user) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingEnrolCode(ctx, uc, mail, code)) ->
                let (UserId u) = user

                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinChoice(ctx, uc, mail))) },
                [
                    send GenPresServer MailService (SendMail(mail, Reset.mail code))
                    send GenPresServer Environment (Noted $"PIN enrolment confirmation code sent for %s{u}")
                    send GenPresServer ctx.Client (PinRequired att)
                ]
            | _ -> h, []

        // UC-2 step 3. The code verified and the PIN is set. Rule 27: mailed and
        // recorded, the first setting included. Then the launch continues from UC-1
        // step 6.
        | GenPresDatabase, GenPresServer, PinReplaced(ForLaunch att, c) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPinWritten(ctx, uc, mail)) ->
                let (UserId u) = c.User
                let h, out = afterCredential att ctx uc mail h
                h, (pinChanged mail $"PIN set for %s{u}") @ out
            | _ -> h, []

        // UC-2 ext 2b. The code bought nothing and no PIN was set. A wrong one with
        // tries left leaves the launch where it was, so the User can read the mail
        // again. A void or aged one ends the attempt (Rule 7), and the next launch
        // mails a fresh code.
        | GenPresDatabase, GenPresServer, ResetRefused(ForLaunch att, failure) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            // Rule 37, one code at a time. A launch that finds a code already standing
            // does not mail a second one: the User has one in their mailbox, and
            // voiding it to send another is the harm the refusal prevents. The launch
            // carries on and asks for that code.
            | Some(AwaitingEnrolCode(ctx, uc, mail, _)) when failure = ResetPending ->
                let (UserId u) = uc.UserId

                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinChoice(ctx, uc, mail))) },
                [
                    send GenPresServer Environment
                        (Noted $"PIN enrolment confirmation code for %s{u} already sent and still good (Rule 37)")
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
                // `ResetPending` cannot arise here: this stage answers a
                // ReplacePinIfCode, which never refuses for that reason (Rule 37). It
                // is answered rather than left out, because an unreachable case that
                // falls through silently today is a wrong branch after the next edit.
                | NoResetPending
                | ResetExpired
                | ResetVoid
                | ResetPending ->
                    { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att },
                    [ send GenPresServer ctx.Client (ResetDenied failure) ]
            | _ -> h, []

        // Step 7. Rule 19 picks the TreatmentPlan the Session starts from: the most
        // recent one, or nothing where the record is empty. Then Rule 8's other
        // Sessions, which the Server does not mirror and so must read (Rule 32).
        | GenPresDatabase, GenPresServer, RecordRead(ForLaunch att, record) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingRecord(ctx, uc, mail, pctx)) ->
                // Rule 19. The most recent TreatmentPlan, or nothing where the record
                // is empty. It is what Rules 20 and 21 are measured from.
                let start = record |> PatientRecord.startsFrom
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending
                        |> Map.add att (pend h.Env.Now (AwaitingPriors(ctx, uc, mail, pctx, start))) },
                [ send GenPresServer GenPresDatabase (ReadSessionRecords(ForLaunch att)) ]
            | _ -> h, []

        // The last step. Rule 8 closes this User's other Sessions, Rule 11 says so
        // once, and Rule 34 hands the Client the token it will return with every
        // request. From here the Server keeps nothing of the Session but its record.
        | GenPresDatabase, GenPresServer, SessionRecordsRead(ForLaunch att, others) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPriors(ctx, uc, mail, pctx, start)) ->
                let h, out =
                    match ctx.Resuming with
                    // Rule 2's replay clause: the first answer again, not a second
                    // Session. Rule 8's sweep does not run, because nothing new is
                    // opening, and no record is written.
                    | Some r -> resumeSession ctx.Client r pctx start h
                    | None ->
                        openSession
                            att (Some ctx) ctx.Client (Some ctx.Launch.Nonce) (Some uc) (Some mail) pctx start
                            others ctx.Replacing h
                h, out
            | _ -> h, []

        | _ -> updateServerFromDatabaseRequest h env
    /// Actor 4. Its own lifecycle first (a down Server answers its clients and does
    /// nothing else), and then whoever the answer is from.
    let private updateServer (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // Rules 10, 32. A restart ends nothing: there is no Session state to lose.
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

    /// Concept 15 and Rule 32. Prescribing changes the Client's own cart, and all of
    /// it then travels, to be computed on or to be signed. Rule 12: the SessionId
    /// rides in the request and never in a URL, and it is what refreshes the idle
    /// clock (Rule 9).
    let private clientAct (h: Hospital) (env: Envelope) (b: BrowserId) (a: UserAct) =
        let st = clientState b h
        let toServer cmd =
            [ send (GenPresClient b) GenPresServer (SessionRequest(st.Sid.Value, st.Opened, cmd)) ]

        match a, st.NoticeFor with
        // Rule 10. The one act that belongs to Sessions that have already ended.
        | AcknowledgesNotice, [] -> h, []
        // Rule 11. The acknowledgement carries the Session doing it. A Client with no
        // Session of its own has nothing to acknowledge with, which is the point: it is
        // the ended Session's own Client, and whoever holds it need not be the User the
        // notice is for.
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
            | (Prescribes _ | EntersPatientData _) when st.Modal.IsSome || st.Signing ->
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
                // Concept 2: the User can always enter the data by hand, with a
                // Patient or without one (Rule 13).
                h |> onClient b (fun s ->
                    { s with Work.Data = Some d; Work.From = Some(ByHand h.Env.Now) }),
                toServer (Compute st.Work.Orders)

            | Signs ->
                // Rule 43. Signing is two requests: first the challenge, over the
                // WorkPlan as it stands, and then the signature that carries it back.
                // No PIN is asked for here — UC-3 step 2 is the ask, and step 3 is
                // where the modal asks for it, so a User is never asked for a PIN on
                // a Submission that was never going to land (Rule 28).
                match st.Opened with
                | Some tok ->
                    h |> onClient b (fun s -> { s with Signing = true }),
                    toServer (RequestSignChallenge(st.Work, tok, st.DataOk))
                | None -> h, []

            // Rule 43. The other half. The User has read what the modal says the
            // signature would attest to, and signs it as shown. Only now does anything
            // leave the Client, and it carries the challenge it was given, so the
            // commit can check that the plan committed is the plan the User saw.
            | ConfirmsSign pin ->
                match st.Modal, st.Opened with
                | Some challenge, Some opened ->
                    h |> onClient b (fun s -> { s with Modal = None; Signing = false; Showing = None }),
                    toServer (
                        SubmitTreatmentPlan
                            {
                                Work = st.Work
                                Opened = opened
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
                h |> onClient b (fun s -> { s with Signing = false; Modal = None; Showing = None }), []

            // Rule 11. Taken by the match above, whether or not a notice is
            // standing: it belongs to a Session that has ended, and this branch is
            // about one that has not.
            | AcknowledgesNotice -> h, []

            | OpensTreatmentPlan id -> h, toServer (OpenTreatmentPlan id)
            | AsksPinReset -> h, toServer ResetPin
            | EntersResetCode(code, pin) -> h, toServer (SupplyResetCode(code, pin))

            | ClosesSession ->
                // UC-9 ext 1a: the Client can warn that unsigned work is about to
                // be dropped, but closed is closed. It existed only here (Rule 32),
                // so closing is what drops it.
                h |> onClient b (fun s ->
                    { s with Work = WorkPlan.empty; Opened = None }),
                toServer CloseSession

            | CarriesOverFrom src ->
                // UC-8 step 3. Work that outlived its Session because it was
                // never in the Server (Rule 32), arriving as fresh prescribing
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

        // F5. The page is still the page, so the retry comes from its own memory, or
        // from the address bar where the page was never served (ext 5a). Rule 39 is
        // about presenting and not about which branch does it, so either way the bar is
        // scrubbed in the same act.
        | User, GenPresClient b, Refresh ->
            let st = clientState b h
            match st.RetryLaunch |> Option.orElse st.UrlLaunch with
            | Some launch ->
                h |> onClient b (fun s -> { s with UrlLaunch = None; RetryLaunch = Some launch }),
                [ send (GenPresClient b) GenPresServer (RedeemLaunch(launch, st.BrowserIdentity, st.Sid)) ]
            | None -> h, []

        // A full reload: the page and its memory go, and what is re-presented is
        // whatever is in the address bar, which after Rule 39 is nothing.
        | User, GenPresClient b, ReloadPage ->
            let scrubbed = h |> onClient b (fun s -> { s with RetryLaunch = None })
            let st = clientState b scrubbed
            match st.UrlLaunch with
            | Some launch ->
                // Rule 4: the principal is the browser's, so a reload keeps it.
                scrubbed, [ send (GenPresClient b) GenPresServer (RedeemLaunch(launch, st.BrowserIdentity, st.Sid)) ]
            | None -> scrubbed, []

        // UC-7. The Client has no Launch to present, and asks for a Session
        // without one.
        | User, GenPresClient b, OpenDirectly ->
            let st = clientState b h
            h |> onClient b (fun s -> { s with UrlLaunch = None }),
            [ send (GenPresClient b) GenPresServer (OpenAnonymous st.Sid) ]

        // Rule 7, UC-1 ext 5a. The offer carries nothing over from the launch: no
        // User, no Patient. It is made only where relaunching would not cure the
        // failure, such as an unrecognised login or an unreachable registry.
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
            // UC-10 ext 1b: nothing reaches the Server. A vanished browser looks the
            // same as a silent one, so the Session is left to idle out. The cart is
            // gone, because it was only ever here (Rule 32).
            h |> onClient b (fun s ->
                { s with
                    Closed = true
                    Work = WorkPlan.empty
                    Opened = None }), []

        | _ -> refused h env

    /// What the Server answers, which is the only way anything reaches a Client
    /// (Consequence 6): every one of these rides back on a request the Client made.
    let private updateClientFromServer (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        | GenPresServer, GenPresClient b, SessionOpened(sid, _, user, pctx, orders, token) ->
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = None
                    RetryLaunch = None
                    AwaitingPin = None
                    AnonymousOffer = false
                    Sid = Some sid
                    User = user
                    Patient = pctx.Patient
                    // Concept 13. The launch read this from the platform, now
                    // (Concept 2). The WorkPlan carries where it came from, so whatever
                    // is signed from it can record that.
                    Work =
                        {
                            Data = pctx.Data
                            From = pctx.Data |> Option.map (fun _ -> FromPlatform h.Env.Now)
                            Orders = orders
                        }
                    Opened = Some token }), []

        | GenPresServer, GenPresClient b, PinRequired att ->
            h |> onClient b (fun s ->
                { s with
                    AwaitingPin = Some att
                    Showing = Some "choose a PIN — nothing else is offered until you do" }), []

        | GenPresServer, GenPresClient b, LaunchRefused retryable ->
            // ext 3c: the identity could not be had and the Launch is still good, so
            // the page keeps it and a refresh tries again (Rules 3, 39). ext 4a: the
            // Launch itself bought nothing, so only a relaunch will do. Either way the
            // User is told no reason (Rule 7); only the offer differs.
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
            // offer worth making (Rule 7).
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = None
                    AnonymousOffer = true
                    Showing = Some "not authorised — continue anonymously?" }), []

        // A registry that is down is a transient fault, so a relaunch may well cure
        // it. A relaunch mints a fresh Launch, which is the one thing F5 cannot do once
        // this one is spent, so both offers stand. Unlike NotAuthorised above, where
        // the answer would be the same however often it is asked.
        | GenPresServer, GenPresClient b, AuthorityUnavailable ->
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = None
                    AnonymousOffer = true
                    Showing =
                        Some "authorisation could not be checked — relaunch from MainEHR, or continue anonymously?" }), []

        // Consequence 1: no Client is served at all when the Server is down, so the
        // User sees the browser's own error page. Where a Client was already served,
        // the Launch stays in the address bar and a refresh retries for as long as
        // Rule 3 allows. The cart stays too (Rule 32): a Server that is down has ended
        // nothing (Rule 10).
        | GenPresServer, GenPresClient b, ServerUnreachable ->
            h |> onClient b (fun s -> { s with Showing = Some "GenPRES is unavailable" }), []

        // The Session is gone; the work is not. It was never in the Server, so the
        // Client still holds it and may offer to carry it into the next Session as
        // fresh prescribing (Concept 15; UC-8 step 3).
        | GenPresServer, GenPresClient b, SessionEnded mark ->
            let text =
                match mark with
                | Some m -> $"the session ended: %A{m} — relaunch from MainEHR"
                | None -> "no such session — relaunch from MainEHR"
            // Rule 11. The Client is told, and that is all it can do. The ended
            // Session is not added to what this Client may acknowledge, because it has
            // no Session left to acknowledge with. The obligation is discharged at the
            // User's next launch, where `PriorSessionNotice` names it again.
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

        // Rule 32: the answer comes back from the payload and the Client keeps it,
        // because the Client is the only party that keeps anything.
        | GenPresServer, GenPresClient b, Computed orders ->
            h |> onClient b (fun s -> { s with Work.Orders = orders }), []

        // Rule 20. No challenge is coming, so the signature the User started is over,
        // and prescribing is possible again. No PIN was ever typed: the modal that
        // asks for one is exactly what is not going to appear.
        | GenPresServer, GenPresClient b, SubmissionBlocked _ ->
            h |> onClient b (fun s ->
                { s with
                    Signing = false
                    Modal = None
                    Showing = Some "someone signed since you opened — open their plan to build on it" }), []

        // Rules 21, 22. Told, and nothing more: no token to return and nothing to
        // acknowledge. The User keeps working if they choose (UC-4 step 3).
        | GenPresServer, GenPresClient b, NewerPlanNotice(uc, at) ->
            let (LoginName l) = uc.Login
            h |> onClient b (fun s ->
                { s with
                    Showing = Some $"%s{l} signed a newer plan at %i{at} — open it to build on it" }), []

        | GenPresServer, GenPresClient b, SubmissionRefused why ->
            h |> onClient b (fun s ->
                { s with
                    Signing = false
                    Modal = None
                    Showing = Some $"the submission was refused: %s{why}" }), []

        // Rule 43. Nothing is submitted here: the User has not looked at it yet, and
        // a Client that submitted on their behalf would be attesting for them. What
        // goes out goes out on `ConfirmsSign`, carrying this challenge.
        | GenPresServer, GenPresClient b, SignChallengeIssued token ->
            let st = clientState b h
            if st.Signing then
                h |> onClient b (fun s ->
                    { s with
                        Modal = Some token
                        Showing = Some "sign the plan as shown, or cancel and edit" }), []
            // A challenge nobody asked for. Not shown, and certainly not signed.
            else h, []

        // Rule 44. The Patient Data has moved under the Session. The User is shown it
        // and accepts by keeping the token, which the next Submission carries.
        | GenPresServer, GenPresClient b, PatientDataChanged(fresh, token) ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = false
                    DataOk = Some token
                    Work.Data = Some fresh
                    Work.From = Some(FromPlatform h.Env.Now)
                    Showing = Some "the Patient Data has changed — check it and sign again" }), []

        // Rule 44, UC-1 ext 6a. The platform could not be asked. Nothing is refused:
        // the User is told what the signature would stand on, and accepts by signing
        // again, which returns the token.
        | GenPresServer, GenPresClient b, PatientDataUnverified token ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = false
                    DataOk = Some token
                    Showing = Some "the Patient Data could not be checked — sign again to sign on it as it stands" }), []

        | GenPresServer, GenPresClient b, TreatmentPlanSubmitted(_, token) ->
            h |> onClient b (fun s ->
                { s with Opened = Some token; Modal = None }), []

        // Rule 18. The opened plan becomes the cart and the new baseline, so a plan
        // that was blocking under Rule 20 stops blocking once it is opened.
        | GenPresServer, GenPresClient b, TreatmentPlanOpened(_, orders, token) ->
            h |> onClient b (fun s ->
                { s with Work.Orders = orders; Opened = Some token }), []

        | GenPresServer, GenPresClient b, PinRejected left ->
            h |> onClient b (fun s -> { s with Showing = Some $"wrong PIN — %i{left} left" }), []

        | GenPresServer, GenPresClient b, NoTreatmentPlanHere ->
            h |> onClient b (fun s -> { s with Showing = Some "no patient: nothing can be saved" }), []

        | GenPresServer, GenPresClient b, NotPermitted ->
            h |> onClient b (fun s -> { s with Showing = Some "not permitted" }), []

        // Rule 28. Signing is locked for a delay that doubles with each further guess
        // and passes on its own. A correct PIN does not cut it short; waiting does, and
        // so does a Rule 37 replacement.
        | GenPresServer, GenPresClient b, SigningLocked ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = false
                    Showing =
                        Some "signing is locked for a while — wait it out, or reset the PIN to sign now" }), []

        // Rule 38. The Session is untouched; only the signature did not happen.
        | GenPresServer, GenPresClient b, SigningUnavailable ->
            h |> onClient b (fun s ->
                { s with Showing = Some "authorisation could not be checked — nothing was signed" }), []

        | GenPresServer, GenPresClient b, ResetCodeMailed ->
            h |> onClient b (fun s ->
                { s with Showing = Some "a confirmation code has been mailed — the current PIN still stands" }), []

        | GenPresServer, GenPresClient b, PinChanged ->
            h |> onClient b (fun s -> { s with Showing = Some "PIN changed" }), []

        | GenPresServer, GenPresClient b, ResetDenied failure ->
            let what =
                match failure with
                | NoResetPending -> "no reset was asked for"
                | ResetExpired -> "that confirmation code has expired — ask for a new one"
                | WrongCode left -> $"that confirmation code is wrong — %i{left} left before it is void"
                | ResetVoid -> "that confirmation code is void — ask for a new one"
                | ResetPending -> "a confirmation code is already on its way — look for the mail"
                | AddressUnavailable -> "GenPRES could not find out where to mail you — try again"
            h |> onClient b (fun s -> { s with Showing = Some what }), []

        | _ -> refused h env

    /// Actor 3. A closed browser first, since nothing reaches it and nothing leaves
    /// it; then the launch that opens the tab; then whoever the message is from.
    let private updateClient (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        match env.From, env.To, env.Msg with

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 3: the GenPRES Client — and the cart, which lives here (Rule 32)
        // ══════════════════════════════════════════════════════════════════════

        // A closed browser is not there any more. Nothing it might have sent reaches
        // the Server (UC-10 ext 1b), which is why no close can be inferred. The cart
        // went with it, because the cart was only ever here.
        | _, GenPresClient b, _ when
            h.Clients |> Map.tryFind b |> Option.map _.Closed |> Option.defaultValue false ->
            h, []

        // UC-1 ext 2b. A Server that is down serves no Client, so there is nothing of
        // ours to show a message with. Nothing is presented, so nothing is scrubbed
        // (Rule 39), and the Launch stays in the bar for ext 3a to retry from.
        | MainEhrLaunchScript, GenPresClient b, OpenUrl launch when not h.GenPres.Up ->
            h |> onClient b (fun s ->
                { s with
                    UrlLaunch = Some launch
                    Showing = Some "the browser's own error page" }), []

        // Rule 39. The Client presents the Launch and erases it from the address bar
        // in the same act. What is left is a copy in the page's own memory, enough to
        // retry with (UC-1 ext 3a). Not in history, not in the bar, not in a referrer
        // (Consequence 4).
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

    /// The edge table is enforced here, before delivery. That is what makes the
    /// Constraints more than a convention: an unpermitted wire does not exist.
    ///
    /// `depthFirst` runs a cascade to the end before the next inbox item, which is the
    /// readable default. Breadth first interleaves them leg by leg, which is the only
    /// way to put two Submissions in flight at once, and so to exercise Rule 36.
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
            let what = match req.Pin with Some(Pin p) -> $"Sign (pin %s{p})" | None -> "Sign (no pin)"
            let c = match req.Challenge with Some _ -> " +challenge" | None -> ""
            let d = match req.DataOk with Some _ -> " +data" | None -> ""
            let os = req.Work.Orders
            $"%s{what} (%i{os.Length} order contexts, opened-with %s{planName (Token.plan req.Opened)}%s{c}%s{d})"
        | RequestSignChallenge(work, tok, _) ->
            $"RequestSignChallenge (%i{work.Orders.Length} order contexts, opened-with %s{planName (Token.plan tok)})"
        | OpenTreatmentPlan(TreatmentPlanId s) -> $"OpenTreatmentPlan %s{s}"
        | ResetPin -> "ResetPin"
        | SupplyResetCode(ConfirmationCode c, _) -> $"SupplyResetCode %s{c}"
        | CloseSession -> "CloseSession"

    let private actName =
        function
        | Prescribes(OrderContextId o) -> $"Prescribes %s{o}"
        | EntersPatientData(PatientData d) -> $"EntersPatientData \"%s{d}\""
        | Signs -> "Signs"
        | OpensTreatmentPlan(TreatmentPlanId s) -> $"OpensTreatmentPlan %s{s}"
        | ConfirmsSign(Pin p) -> $"ConfirmsSign (pin %s{p})"
        | CancelsSign -> "CancelsSign"
        | AcknowledgesNotice -> "AcknowledgesNotice"
        | AsksPinReset -> "AsksPinReset"
        | EntersResetCode(ConfirmationCode c, _) -> $"EntersResetCode %s{c}"
        | ClosesSession -> "ClosesSession"
        | CarriesOverFrom(BrowserId b) -> $"CarriesOverFrom Client%i{b}"

    let rec describe (m: Msg) =
        match m with
        | Tick -> "Tick"
        | Start a -> $"Start %s{actorName a}"
        | Stop a -> $"Stop %s{actorName a}"
        | PublishRuleSet(RuleSetVersion v) -> $"PublishRuleSet v%i{v}"
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
        | ChoosePin(ConfirmationCode c, Pin p) -> $"ChoosePin %s{c} %s{p}"
        | Act a -> actName a
        | CloseBrowser -> "CloseBrowser"
        | RedeemLaunch(l, identity, _) ->
            let who = match identity with Some(LoginName x) -> x | None -> "nobody"
            $"RedeemLaunch %s{l.Nonce} (the browser proved %s{who})"
        | OpenAnonymous _ -> "OpenAnonymous"
        | AnonymousRefused -> "AnonymousRefused"
        | SupplyPin(AttemptId a, ConfirmationCode c, Pin p) -> $"SupplyPin #%i{a} %s{c} %s{p}"
        | AckSessionNotice(SessionId by, SessionId sid) -> $"AckSessionNotice %s{sid} (from %s{by})"
        | SessionRequest(SessionId s, _, c) -> $"%s{s}: %s{cmdName c}"
        | CheckLaunchSpent(t, nonce) -> $"CheckLaunchSpent %s{tagName t} %s{nonce}"
        | SessionWasOpened t -> $"SessionWasOpened %s{tagName t}"
        | LaunchUnspent t -> $"LaunchUnspent %s{tagName t}"
        | LaunchReplayed(t, r) ->
            let which = match r with Some x -> let (SessionNo n) = x.No in $"ses-%03i{n}" | None -> "(no session)"
            $"LaunchReplayed %s{tagName t} %s{which}"
        | NoteAnonymousRefusal a -> $"NoteAnonymousRefusal %s{actorName a}"
        | ResolveUser(t, LoginName u) -> $"ResolveUser %s{tagName t} %s{u}"
        | UserResolved(t, uc, _, _) ->
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
        | ReplacePinIfCode(t, UserId u, ConfirmationCode c, _) ->
            $"ReplacePinIfCode %s{tagName t} %s{u} %s{c}"
        | PinReplaced(t, _) -> $"PinReplaced %s{tagName t}"
        | ResetRefused(t, f) -> $"ResetRefused %s{tagName t} %A{f}"
        | ReadRecord(t, PatientId p) -> $"ReadRecord %s{tagName t} %s{p}"
        | RecordRead(t, r) -> $"RecordRead %s{tagName t} (%i{r.Plans.Length} plans)"
        | CommitTreatmentPlan(t, c) ->
            let (IdemKey k) = c.Req.Key
            $"CommitTreatmentPlan %s{tagName t} key=%s{k}"
        | TreatmentPlanCommitted(_, s) ->
            let (TreatmentPlanId i) = s.Id
            let (RuleSetVersion v) = s.RuleSet
            $"TreatmentPlanCommitted %s{i} (rule set v%i{v})"
        | CommitRefused(t, r) -> $"CommitRefused %s{tagName t} %A{r}"
        | OpenSessionClosingOthers(t, r, replacing) ->
            let (SessionNo n) = r.No
            let also = match replacing with Some(SessionId o) -> $" (replacing %s{o})" | None -> ""
            $"OpenSessionClosingOthers %s{tagName t} ses-%03i{n}%s{also}"
        | EndSessionIfOpen(SessionId sid, mark) -> $"EndSessionIfOpen %s{sid} %A{mark}"
        | TouchIfOpen(SessionId sid) -> $"TouchIfOpen %s{sid}"
        | NoteMailUsed(SessionId sid, MailAddress a) -> $"NoteMailUsed %s{sid} %s{a}"
        | MarkDelivered(SessionId sid) -> $"MarkDelivered %s{sid}"
        | MarkAcknowledged(SessionId by, SessionId sid) -> $"MarkAcknowledged %s{sid} (by %s{by})"
        | ReadSessionRecord(t, SessionId s) -> $"ReadSessionRecord %s{tagName t} %s{s}"
        | SessionRecordRead(t, r, _) ->
            let what = match r with Some x -> $"%A{x.State}" | None -> "(no such session)"
            $"SessionRecordRead %s{tagName t} %s{what}"
        | ReadSessionRecords t -> $"ReadSessionRecords %s{tagName t}"
        | SessionRecordsRead(t, rs) -> $"SessionRecordsRead %s{tagName t} (%i{rs.Length})"
        | SendMail(MailAddress a, what) -> $"SendMail {a}: \"%s{what}\""
        | SessionOpened(SessionId s, SessionNo n, u, p, os, tok) ->
            let who =
                match u with
                | Some uc -> let (LoginName l) = uc.Login in $"%s{l}/%A{uc.Role}"
                | None -> "anonymous"
            let pat = match p.Patient with Some(PatientId x) -> x | None -> "(no patient)"
            $"SessionOpened %s{s} ses-%03i{n} %s{who} %s{pat} (%i{os.Length} order contexts, opened-with %s{planName (Token.plan tok)})"
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
        | NewerPlanNotice(uc, at) ->
            let (LoginName l) = uc.Login
            $"NewerPlanNotice (%s{l}, signed at %i{at})"
        | SubmissionRefused why -> $"SubmissionRefused \"%s{why}\""
        | TreatmentPlanSubmitted(TreatmentPlanId s, _) -> $"TreatmentPlanSubmitted %s{s}"
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
//  None of this ships as code. The scenarios are the acceptance tests the real system
//  owes, and the checks are what each one has to prove.
// ═══════════════════════════════════════════════════════════════════════════════

// ───────────────────────────── what the world does ─────────────────────────────

let atWorkstation msg = { From = User; To = MainEhrWorkstation; Msg = msg }
let triggerLaunch     = { From = User; To = MainEhrLaunchScript; Msg = TriggerLaunch }
let envt to_ msg      = { From = Environment; To = to_; Msg = msg }
let tick              = envt Environment Tick
let ticks n           = List.replicate n tick
let atClient b msg    = { From = User; To = GenPresClient(BrowserId b); Msg = msg }
let act b a           = atClient b (Act a)

/// A Client putting an envelope on the wire by hand. This is what an attacker has,
/// and what the honest Client's branches deliberately never do.
let fromClient b msg = { From = GenPresClient(BrowserId b); To = GenPresServer; Msg = msg }

/// A Submission built by hand: the fields under test, and defaults for the rest. Rule 45's
/// key is fresh each time, so nothing here is answered out of the table by accident.
let mutable private handKey = 0

let handCreate (work: WorkPlan) (opened: OpenedToken) (pin: Pin option) =
    handKey <- handKey + 1
    SubmitTreatmentPlan
        {
            Work = work
            Opened = opened
            Challenge = None
            DataOk = None
            Pin = pin
            Key = IdemKey $"hand-%04i{handKey}"
        }

/// Rule 43. Signing is two acts of the User's, not one: asking, and then signing what
/// the modal shows. A scenario that means "A signs" wants both, and a scenario testing
/// the gate between them uses `Signs` and `ConfirmsSign` apart.
let signs b pin = [ act b Signs; act b (ConfirmsSign pin) ]

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
let pat2 = PatientId "pat-2"      // head is a TreatmentPlan of A's
let pat3 = PatientId "pat-3"      // head is a TreatmentPlan of B's, over one of A's

let oc id pat by =
    { Id = OrderContextId id; Patient = Some pat; Content = $"%s{id}/as-signed"; Stamp = Some by }

/// A signed plan the run did not make: history the Cast starts with. It carries no
/// Session, which is how a plan from before this record began looks.
let mkPlan n patient by baseOn orders =
    {
        Id = TreatmentPlanId $"plan-%04i{n}"
        No = TreatmentPlanNo n
        Patient = patient
        By = by
        Base = baseOn
        Orders = orders
        Data = Some(PatientData $"as read for %A{patient}")
        From = Some(FromPlatform 0)
        RuleSet = RuleSetVersion 1
        Session = None
        At = 0
    }

let p2Signed = mkPlan 1 pat2 ucA None               [ oc "oc-1" pat2 ucA ]
let p3First  = mkPlan 2 pat3 ucA None               [ oc "oc-2" pat3 ucA ]
let p3Head   = mkPlan 3 pat3 ucB (Some p3First.Id)  [ oc "oc-2" pat3 ucA; oc "oc-3" pat3 ucB ]

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
                    Clinical.Signed =
                        Map.ofList [ pat2, [ p2Signed ]; pat3, [ p3Head; p3First ] ] } }
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
/// Rule 32, structurally: after every scenario step, is the Server empty of requests?
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

/// The Launch the LaunchScript last minted, read off the wire. A scenario cannot make
/// one, because the mac is over a secret only the LaunchScript and the Server hold. So
/// a thief here has what a thief there has: a value seen in passing.
let launchOnTheWire () =
    lastTrace |> List.tryPick (function { Msg = OpenUrl l } -> Some l | _ -> None)

/// Rule 43. The challenges the last step's Server issued, as an attacker or a retry
/// would have them: something a Client was given, and cannot make.
let challengesIssued () =
    lastTrace |> List.choose (function { Msg = SignChallengeIssued t } -> Some t | _ -> None)

let challengeIssued () = challengesIssued () |> List.tryHead

/// Did `first` happen before `second` in the trace? Used where an order is fixed:
/// Rule 24, and UC-3 ext 2a.
let before (first: Msg -> bool) (second: Msg -> bool) =
    let idx p = lastTrace |> List.tryFindIndex (fun e -> p e.Msg)
    match idx first, idx second with
    | Some a, Some b -> a < b
    | _ -> false

// ───────────────────────────── reading the world ─────────────────────────────
// Everything a Session is, is in the Database now (Rule 32), and everything it is
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

/// Rule 11. And said to have been seen, which is what ends the obligation.
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

/// Every Patient the Database holds a TreatmentPlan for.
let patientsInRecord (h: Hospital) =
    h.Database.Clinical.Signed |> Map.toList |> List.map fst |> List.distinct
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
/// Rule 34: the TreatmentPlan the Session opened with, as the Client holds it.
let openedAt b h = clientOf b h |> Option.bind _.Opened |> Option.bind Token.plan

let mailsTo (addr: MailAddress) (h: Hospital) = h.Mail |> List.filter (fst >> (=) addr)

/// Rule 46. What the Database recorded: the private store's audit, and the only copy
/// there is.
let auditOf (h: Hospital) = h.Database.Private.Audit

let audited (what: string) (h: Hospital) = auditOf h |> List.exists (fun a -> a.What.Contains what)
let credentialOf (uc: UserContext) (h: Hospital) = h.Database.Private.Credentials |> Map.tryFind uc.UserId

/// UC-6 step 2. What the User does with the mail: reads the code out of it. Rule 37
/// rests entirely on the code arriving through a channel GenPRES only writes to, and
/// whoever is at the workstation does not read.
let codeInMail (addr: MailAddress) (h: Hospital) =
    mailsTo addr h
    |> List.tryPick (fun (_, body) ->
        body.Split ' ' |> Array.tryFind (fun w -> w.StartsWith "code-"))
    |> Option.map ConfirmationCode

// ───────────────────────────── running a scenario ─────────────────────────────

/// One PatientRecord as a chain, oldest first, each link `id/who`. The arrow is "and
/// then": a PatientRecord is append-only (Concept 12), so every link is still there
/// and each stands on the one to its left.
let private planChain (r: PatientRecord) =
    r.Plans
    |> List.rev
    |> List.map (fun s ->
        let (TreatmentPlanId i) = s.Id
        let (LoginName l) = s.By.Login
        $"%s{i}/%s{l}")
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

/// Rule 32, checked after every step: whatever the Server was doing, it is not doing
/// it any more, and it kept nothing.
let private noteFlight (h: Hospital) =
    if not h.GenPres.InFlight.IsEmpty then everCarriedARequest <- true

/// Every SessionRecord state the Database has ever held, at the end of every step of
/// every scenario. Under Rule 40 a record never travels in an envelope: the Server
/// names a change and the Database decides. So the trace is not where the states are,
/// and this is.
let mutable allRecords : SessionRecord list = []

let private noteRecords (h: Hospital) =
    allRecords <- allRecords @ h.Database.Private.Sessions

/// Rule 16. Every TreatmentPlan the Database held, at the end of every step of every
/// scenario, kept one snapshot at a time. Scenarios all replay from the same world,
/// so a plan id names one plan within a snapshot but not across the run. That is why
/// the snapshots are not flattened.
let mutable allPlans : TreatmentPlan list list = []

/// Rules 8 and 40. Every *set* of SessionRecords the Database has held, kept whole
/// rather than flattened: the limits are about what stands open together, so they can
/// only be tested against one state at a time.
let mutable allDatabases : SessionRecord list list = []

let private noteDatabase (h: Hospital) =
    allDatabases <- h.Database.Private.Sessions :: allDatabases

let private notePlans (h: Hospital) =
    allPlans <- (h.Database.Clinical.Signed |> Map.toList |> List.collect snd) :: allPlans

/// The fuel is a count of handled messages. A scenario measured in ticks spends far
/// more of them than one measured in acts, because a live Session ticking towards a
/// limit costs a sweep per tick. `step` is the ordinary budget; `stepFor` is for the
/// two scenarios that watch a clock run out (Rule 14's anonymous limit, Rule 10's
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

    expect "UC-1 the SessionRecord carries the UserContext (Concept 9)"
        ((newestRecord launched |> Option.bind _.User) = Some ucA)

    // Rule 27. The record does carry the registry's last answer, but only as something
    // to fall back on: no mail is ever addressed from it, and a fresh answer is asked
    // for every time one is sent.
    expect "UC-1 the SessionRecord carries the registry's address, for fallback only (Rule 27)"
        ((newestRecord launched |> Option.bind _.Mail) = Some mailA
         && never (function SendMail _ -> true | _ -> false))

    expect "UC-1 step 4: the Launch carried a Patient, and nothing about who the User is"
        (saw (function
              | OpenUrl l -> l.Patient = Some pat1 && Token.verifyLaunch l
              | _ -> false))

    expect "UC-1 step 4: the nonce is spent once, and the launch went on (Rule 2)"
        (countOf (function CheckLaunchSpent _ -> true | _ -> false) = 1
         && saw (function LaunchUnspent _ -> true | _ -> false)
         && never (function LaunchReplayed _ -> true | _ -> false))

    expect "UC-1 step 3: the login the registry was asked about is the one the browser proved (Rules 4, 5)"
        (saw (function ResolveUser(ForLaunch _, l) -> l = ucA.Login | _ -> false))

    expect "UC-1 step 5: the Role came from the UserRegistry (Rule 5)"
        (saw (function UserResolved(_, uc, _, _) -> uc.Role = Prescriber | _ -> false))

    expect "UC-1 step 5: the registry confirmed the Launch's Patient is the active one (Rule 6)"
        (saw (function UserResolved(ForLaunch _, _, _, active) -> active = Some pat1 | _ -> false))

    expect "UC-1 step 5: a PIN is set, so the launch continues and none is asked for (Rule 24)"
        (saw (function CredentialRead(_, Some c) -> c.Pin.IsSome | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    expect "UC-1 step 6: the PatientContext was read once, at the launch (Concept 2)"
        (saw (function PatientDataRead _ -> true | _ -> false)
         && countOf (function ReadPatientData _ -> true | _ -> false) = 1)

    expect "UC-1 step 7: Patient 1 has no record, so the Session starts from nothing (Rule 19)"
        (openedAt 1 launched = None && workingAt 1 launched = [])

    expect "UC-1 step 8: the SessionRecord was written to the Database (Concept 9)"
        (launched.Database.Private.Sessions.Length = 1)

    // Rule 34. The Client is handed the token it will return with every request, and
    // it could not have made one: the mac is over a secret it never sees.
    expect "UC-1 step 9: the Client holds an opened-with token that verifies (Rule 34)"
        ((clientOf 1 launched |> Option.bind _.Opened |> Option.map Token.verifyOpened) = Some true)

    expect "UC-1 and from here the Server keeps nothing of the Session (Rule 32)"
        (launched.GenPres.InFlight.IsEmpty && launched.GenPres.Pending.IsEmpty)

    // Rule 2. The spent-mark is a nonce and nothing else: GenPRES keeps no copy of
    // the Launch, and the SessionRecord names the nonce that opened it.
    expect "UC-1 the Launch is spent, and all GenPRES keeps of it is the nonce (Rule 2)"
        (launched.Database.Private.Spent
         |> Set.exists (fun n -> (newestRecord launched |> Option.bind _.Launch) = Some n))

    // ── UC-1 ext 1a — no Patient is active in the MainEHR Session ──
    // GenPRES opens and A can prescribe, but a TreatmentPlan cannot be opened or signed.
    let noPatient = step "UC-1 ext 1a — no Patient active" world (launchAs ucA.Login None)

    expect "1a a Session opens without a Patient"
        (openCount noPatient = 1 && (newestRecord noPatient |> Option.bind _.Patient) = None)

    expect "1a steps 6 and 7 are skipped: no data to fetch, no PatientRecord to read"
        (never (function ReadPatientData _ -> true | _ -> false)
         && never (function ReadRecord _ -> true | _ -> false))

    let _ =
        step "UC-1 ext 1a — and a TreatmentPlan cannot be created (Rule 13)" noPatient
             ([ act 1 (Prescribes(OrderContextId "oc-9")) ] @ signs 1 pinA)

    expect "1a prescribing works; signing does not"
        (saw (function Computed _ -> true | _ -> false)
         && saw (function NoTreatmentPlanHere -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── UC-1 ext 1b — the button is not A's to press ──
    // Rule 1. How MainEHR decides is its own affair. What is ours to state is the
    // refusal, and that nothing leaves the workstation when it happens: the script
    // seals nothing, so no Launch ever exists.
    let notTheirButton =
        step "UC-1 ext 1b — the button is not A's to press" { world with Workstation.MayLaunch = Set.empty }
             (launchAs ucA.Login (Some pat1))

    expect "1b the LaunchScript refuses, and nothing leaves the workstation (Rule 1)"
        (saw (function LaunchError _ -> true | _ -> false)
         && never (function OpenUrl _ -> true | _ -> false)
         && notTheirButton.Clients.IsEmpty
         && openCount notTheirButton = 0)

    // ── UC-1 ext 2a — the sealing key cannot be read ──
    // The last thing the LaunchScript can report. It seals nothing, opens no browser
    // and exits, so no Launch exists to be spent, stolen or refused later
    // (Consequence 1).
    let noKey =
        step "UC-1 ext 2a — the LaunchScript cannot read the key" { world with Workstation.KeyReadable = false }
             (launchAs ucA.Login (Some pat1))

    expect "2a the script reports it and nothing leaves the workstation"
        (saw (function LaunchError _ -> true | _ -> false)
         && never (function OpenUrl _ -> true | _ -> false)
         && never (function RedeemLaunch _ -> true | _ -> false)
         && openCount noKey = 0)

    expect "2a and no Launch was minted, so none can be spent or replayed later (Rule 2)"
        (noKey.Database.Private.Spent.IsEmpty)

    // ── UC-1 ext 2b / 3a — the Server is unreachable ──
    // The Client is served by the Server, so a Server that is down serves no Client.
    // There is nothing of ours to show a message with (Consequence 1), and nothing is
    // presented, so nothing is scrubbed (Rule 39). The Launch waits in the address bar
    // for ext 3a to retry from, for as long as Rule 3 allows.
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
         && never (function CheckLaunchSpent _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false)
         && expired |> audited "LaunchExpired")

    // ── Rule 39 — the Launch is erased at its first presentation ──
    // A refresh is the same page retrying from its own memory. A reload is a new page,
    // which can re-present only what is in the address bar: scrubbed, and empty.
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
    // Session of their own. Nothing of A's is taken, and the cost to A is one
    // relaunch. The Launch is parked unpresented, as if the Server had been down, so
    // the thief can get there first.
    let parked =
        step "UC-1 ext 3b — the Launch sits unpresented in the address bar" world
             (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

    let stolenLaunch = (launchOnTheWire ()).Value

    // For anything to open at all, the thief must have Patient 1 active in their own
    // MainEHR Session (Rule 6), which Possibility 2 allows. This is the best case for a
    // thief, and Guarantee 5 says it gains them nothing: the Session is the one their
    // own launch would have given them.
    let thief =
        step "UC-1 ext 3b — a thief presents it first, from their own browser"
             { parked with Registry.Active = parked.Registry.Active |> Map.add ucB.Login pat1 }
             [
                 envt GenPresServer (Start GenPresServer)
                 {
                     From = GenPresClient(BrowserId 99)
                     To = GenPresServer
                     Msg = RedeemLaunch(stolenLaunch, Some ucB.Login, None)
                 }
             ]

    // Rule 4, stated as plainly as the model can state it: the Session's User is the
    // browser's, and the Launch had no say. B's browser, B's Session, B's Patient.
    expect "3b the browser proved B, so a Session opens for B — not for A (Rules 4, 5, 6)"
        (openCount thief = 1
         && (sidAt 99 thief).IsSome
         && (newestRecord thief |> Option.bind _.User |> Option.map _.UserId) = Some ucB.UserId
         && (newestRecord thief |> Option.bind _.Patient) = Some pat1)

    // ── UC-1 ext 3c — the browser proved nobody ──
    // A page opened outside a logged-in workstation. There is no User to open a
    // Session for and the Launch offers none, so nothing opens. Rule 4 is checked
    // before Rule 2, so the nonce is not even spent.
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
         && never (function CheckLaunchSpent _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false)
         && unproven |> audited "NoIdentity")

    // ext 3c, the other half: the identity was missing, not wrong. Nothing was spent,
    // so the Launch is still worth presenting, and the refusal says so. That is the
    // only thing telling this case apart from ext 4a. The User is told no reason
    // either way (Rule 7); what differs is what the Client offers next.
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
    // attacker actually has, and it gains nothing: refused before the lifetime is
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
         && never (function CheckLaunchSpent _ -> true | _ -> false)
         && saw (function LaunchRefused false -> true | _ -> false)
         && forged |> audited "LaunchForged")

    // The Patient it names gains nothing either: a forged Launch never gets far enough
    // for the Patient to matter.
    expect "4a and nothing of the Patient it named was ever read (Rules 2, 6)"
        (never (function ReadPatientData _ -> true | _ -> false)
         && never (function ResolveUser _ -> true | _ -> false))

    // ── UC-1 ext 4a — A's own retry, after the theft ──
    // The nonce is spent, and spent by another browser, so Rule 2's replay clause does
    // not apply and A gets nothing. Theft costs A a relaunch, and never gives anyone a
    // Session in A's name.
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

    // ── Rule 2 — the crash window the early check used to open ──
    // The early check is a read now, and the nonce is spent by the open (Rule 40). So
    // a launch that gets past the check and then dies has spent nothing, and the
    // Launch is still worth presenting. Under the old order the nonce was gone with no
    // Session to show for it, and the User's own retry was refused.
    //
    // The fuel stops the cascade between the two: past `LaunchUnspent`, short of
    // `OpenSessionClosingOthers`.
    let halfWay =
        stepFor 12 "Rule 2 — a launch gets past the early check, and then the Server dies" world
                (launchAs ucA.Login (Some pat1))

    expect "Rule 2 the early check wrote nothing: the nonce is unspent and no Session exists"
        (saw (function LaunchUnspent _ -> true | _ -> false)
         && never (function OpenSessionClosingOthers _ -> true | _ -> false)
         && halfWay.Database.Private.Spent.IsEmpty
         && openCount halfWay = 0)

    let afterCrash =
        step "Rule 2 — the Server restarts and the page retries the same Launch" halfWay
             [
                 envt GenPresServer (Stop GenPresServer)
                 envt GenPresServer (Start GenPresServer)
                 atClient 1 Refresh
             ]

    expect "Rule 2 the retry opens a Session: the launch that died spent nothing (Rules 2, 40)"
        (openCount afterCrash = 1
         && (sidAt 1 afterCrash).IsSome
         && never (function LaunchRefused _ -> true | _ -> false)
         && afterCrash.Database.Private.Spent.Count = 1)

    // ── Rule 2 — two presentations that both pass the early check ──
    // The check is advisory, so two browsers can both be told the nonce is unspent and
    // both run the launch through. The open settles it: the first to reach the
    // Database spends the nonce, and the second is answered as a replay — by
    // BrowserIdentity, exactly as the early check would have answered it (Rules 2, 36,
    // 40). B must have Patient 1 active for their launch to get that far at all
    // (Rule 6; Possibility 2).
    let parked =
        quiet "Rule 2 race — a Launch is minted and left unpresented" 
              { world with Registry.Active = world.Registry.Active |> Map.add ucB.Login pat1 }
              (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

    let contested = (launchOnTheWire ()).Value

    let raced =
        racing "Rule 2 — two browsers present the same Launch at once" parked
               [
                   envt GenPresServer (Start GenPresServer)
                   fromClient 1 (RedeemLaunch(contested, Some ucA.Login, None))
                   fromClient 2 (RedeemLaunch(contested, Some ucB.Login, None))
               ]

    expect "Rule 2 both presentations passed the early check, which writes nothing"
        (countOf (function LaunchUnspent _ -> true | _ -> false) = 2)

    expect "Rule 2 the open decided it: one Session, one spent nonce (Rules 36, 40)"
        (openCount raced = 1
         && raced.Database.Private.Spent.Count = 1
         && countOf (function SessionOpened _ -> true | _ -> false) = 1)

    expect "Rule 2 the loser is answered as a replay, and refused because it is another browser"
        (saw (function LaunchReplayed _ -> true | _ -> false)
         && saw (function LaunchRefused _ -> true | _ -> false)
         && raced |> audited "LaunchAlreadySpent")

    // ── Rule 2's replay clause — the same browser, the same Launch, again ──
    // This is what an F5 during a slow open looks like. The nonce is spent, but it was
    // spent by *this* browser and the Launch is still within its lifetime, so the
    // answer is the first answer: the same Session, not a second one.
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
         && saw (function SessionOpened(sid, _, _, _, _, _) -> sid = firstSid | _ -> false)
         && never (function LaunchRefused _ -> true | _ -> false)
         && recordCount replayed = recordCount openedOnce)

    // And the replayed answer is a whole answer: a fresh OpenedToken over the same
    // TreatmentPlan, because the first one may have been spent by a Submission (Rule 34).
    expect "Rule 2 the replay hands back a fresh, verifying OpenedToken (Rule 34)"
        ((clientOf 1 replayed |> Option.bind _.Opened |> Option.map Token.verifyOpened) = Some true)

    // Past the lifetime it is not a retry any more, whoever presents it (Rule 3).
    let replayedLate =
        step "Rule 2 — but not past the lifetime (Rule 3)" openedOnce
             (ticks 25 @ [ fromClient 1 (RedeemLaunch(firstLaunch, Some ucA.Login, Some firstSid)) ])

    expect "Rule 2 an aged Launch is no retry: refused, and the first Session is untouched"
        (openCount replayedLate = firstCount
         && saw (function LaunchRefused _ -> true | _ -> false)
         && stateOf 1 replayedLate = Some OpenOrGone)

    // ── UC-1 ext 3b — the Launch is stolen before it is presented ──
    // Another browser is not a retry either, however fresh the Launch. Guarantee 5:
    // the thief is identified as themselves (Rule 4), gets their own Role (Rule 5) and
    // only the Patient they have active (Rule 6). B has none active here, so nothing
    // opens at all.
    let replayedByAnother =
        step "UC-1 ext 3b — a thief presents A's Launch in their own browser" openedOnce
             [ fromClient 97 (RedeemLaunch(firstLaunch, Some ucB.Login, None)) ]

    expect "3b a stolen Launch opens nothing: the thief proves the thief (Rules 2, 4, 6)"
        (openCount replayedByAnother = firstCount
         && sidAt 97 replayedByAnother = None
         && saw (function LaunchRefused _ -> true | _ -> false))

    // Guarantee 5, the other half: the login the registry is asked about is the
    // thief's, never A's, so nothing of A's authority is in play at any point.
    expect "3b the registry is asked about the thief, not about A (Rules 4, 5)"
        (lastTrace
         |> List.forall (fun e ->
             match e.Msg with
             | ResolveUser(_, login) -> login = ucB.Login
             | _ -> true))

    // Nor is A's own login from a second browser. The clause is about one browser
    // coming back, not about who is proving what. Handing the first browser's SessionId
    // to a second would put two browsers on one Session, which Rules 8 and 40 spend an
    // act each to prevent. The SessionId is a bearer credential (Rule 12), so it would
    // be handing it to whoever is at that second screen.
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

    expect "5a no launched Session, and rights fail closed (Rules 5, 7)"
        (openCount registryDown = 0
         && saw (function AuthorityUnavailable -> true | _ -> false))

    expect "5a the anonymous open is offered — relaunching would not cure this"
        ((clientOf 1 registryDown |> Option.map _.AnonymousOffer) = Some true)

    let wentAnonymous =
        step "UC-1 ext 5a — A accepts, and gets a fresh anonymous open (Rule 7)" registryDown
             [ atClient 1 AcceptAnonymousOffer ]

    expect "5a it carries nothing over from the launch: no User, no Patient"
        (openCount wentAnonymous = 1
         && (newestRecord wentAnonymous |> Option.bind _.User) = None
         && (newestRecord wentAnonymous |> Option.bind _.Patient) = None)

    // ── UC-1 ext 5b — the registry names another active Patient than the Launch's ──
    // Rule 6. The Launch is honestly sealed and honestly presented; what is wrong is
    // that MainEHR moved on between the sealing and the asking, so the workstation's
    // Patient and the registry's answer differ. Nothing opens, and the cure is to
    // activate the right Patient and relaunch. Seeded rather than raced, because what
    // is under test is the check and not the timing.
    let outOfStep =
        { world with
            Workstation.ActivePatient = Some pat1
            Registry.Active = world.Registry.Active |> Map.add ucA.Login pat2 }

    let wrongPatient =
        step "UC-1 ext 5b — the Launch names Patient 1, the registry says Patient 2" outOfStep
             [ atWorkstation (LogIn ucA.Login); triggerLaunch ]

    expect "5b nothing opens when the Launch's Patient is not the active one (Rules 6, 7)"
        (openCount wrongPatient = 0
         && saw (function LaunchRefused _ -> true | _ -> false))

    expect "5b and it got as far as the registry: this is Rule 6 refusing, not Rule 2 or 3"
        (saw (function UserResolved(ForLaunch _, _, _, p) -> p = Some pat2 | _ -> false))

    expect "5b and the audit says which refusal it was (Rule 46)"
        (wrongPatient |> audited "PatientNotActive")

    // ── UC-1 ext 5c — the launching User is a Reader ──
    let asReader = step "UC-1 ext 5c — C, a Reader, launches for Patient 3" world (launchAs ucC.Login (Some pat3))

    expect "5c a Session opens, with the Reader Role"
        ((newestRecord asReader |> Option.bind _.User |> Option.map _.Role) = Some Reader)

    expect "5c a Reader is never asked for a PIN — not asked and ignored, but not asked (Rule 26)"
        (never (function ReadCredential _ -> true | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    expect "5c and starts from the most recent TreatmentPlan (Rules 18, 19)"
        (openedAt 1 asReader = Some p3Head.Id)

    // ── UC-1 ext 5d — User A has no PIN yet ──
    // First launch as a Prescriber. UC-2 is this case in full: a PIN must be set
    // before the launch continues (Rule 26).

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
    // Rule 8 is per User and not per Patient, so both are the same mechanism: the
    // earlier Session is closed and A is told work in it may have been lost.
    let wrongPatient = step "UC-1 ext 9a — A launched for the wrong Patient" world (launchAs ucA.Login (Some pat1))
    let relaunched =
        step "UC-1 ext 8a/9a — A activates Patient 2 and relaunches" wrongPatient
             (launchAs ucA.Login (Some pat2))

    expect "9a the wrong Session is closed, whichever Patient it was for (Rules 8, 40)"
        (openCount relaunched = 1
         && (newestRecord relaunched |> Option.bind _.Patient) = Some pat2
         && (match stateOf 1 relaunched with Some(Ended(Superseded, _)) -> true | _ -> false))

    expect "8a and A is told, once (Rule 11)"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && wasTold 1 relaunched)

    // ── UC-1 ext 8b — two launches at once ──
    // Rule 8 is a count, and a count read and then written back is a race. Rule 40
    // makes the opening and the closing one act at the Database, so the two orders of
    // arrival have the same answer: one open Session, whichever won.
    //
    // Both launches name the same Patient. Two different ones would not race at all:
    // the registry names one active Patient (Invariant 1), so Rule 6 would refuse the
    // other before Rule 8 ever saw it.
    let racedLaunches =
        racing "UC-1 ext 8b — two of A's launches arrive at once" world
               (launchAs ucA.Login (Some pat2) @ launchAs ucA.Login (Some pat2))

    expect "8b exactly one Session is open, and the other is Superseded (Rules 8, 40)"
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

    // Precondition: UC-1 has reached step 5. The UserContext carries the Prescriber
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
         && asked |> audited "PIN enrolment confirmation code sent"
         && openCount asked = 0
         && never (function SessionOpened _ -> true | _ -> false))

    // Step 2's order, and not merely its content: the mail goes out before anything
    // is asked of the screen, because what the screen is asked for is the code.
    expect "UC-2 step 1: the code is mailed before the Client is asked (Rules 27, 37)"
        (before (function SendMail _ -> true | _ -> false)
                (function PinRequired _ -> true | _ -> false))

    // The order matters twice over: the code's address comes from the UserRegistry, so
    // it cannot even be mailed before the registry has said who the login belongs to.
    expect "UC-2 the PIN is offered only after the registry recognised the login (Rule 25)"
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
    // holding the attempt number is not the User at that screen, and would not get past
    // the code either (ext 2c).
    let intruder =
        let att = asked.GenPres.Pending |> Map.toList |> List.head |> fst
        step "UC-2 — a second browser answers the prompt A was given" asked
             [
                 {
                     From = GenPresClient(BrowserId 99)
                     To = GenPresServer
                     Msg = SupplyPin(att, ConfirmationCode "code-guess", Pin "0000")
                 }
             ]

    expect "UC-2 only the Client the prompt was put to may answer it (Concept 7; Rules 23, 24)"
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

    expect "UC-2 step 3: the change is recorded and A is mailed — the code, then the setting (Rule 27)"
        ((mailsTo mailA enrolled).Length = 2
         && enrolled |> audited "PIN set")

    expect "UC-2 a newly set PIN starts with a count of zero (Rule 28)"
        ((credentialOf ucA enrolled |> Option.map _.AttemptCount) = Some 0)

    expect "UC-2 step 3: the launch continues from UC-1 step 6"
        (openCount enrolled = 1
         && saw (function SessionOpened _ -> true | _ -> false)
         && saw (function PatientDataRead _ -> true | _ -> false))

    // ── Rule 27 — the address is asked for again when the PIN is set ──
    // This stage waited on a human, so the answer the launch got at step 5 may be old.
    // The registry is asked once more before the notice goes out, exactly as a reset
    // asks on the request that mails (UC-6 step 2).
    expect "UC-2 step 3: the registry is asked again before the notice, not reused from step 5 (Rule 27)"
        (saw (function ResolveUser(ForLaunch _, l) -> l = ucA.Login | _ -> false)
         && before (function ResolveUser _ -> true | _ -> false)
                   (function ReplacePinIfCode _ -> true | _ -> false)
         && before (function ResolveUser _ -> true | _ -> false)
                   (function SendMail _ -> true | _ -> false))

    // And it is the new answer that is used. A's address changes at the registry while
    // the launch waits on the human, and the notice follows it.
    let movedWhileWaiting =
        { asked with
            Registry.Users =
                asked.Registry.Users |> Map.add ucA.Login (ucA, MailAddress "a.moved@hospital") }

    let enrolledAfterMove =
        step "Rule 27 — A's address changes while the launch waits, then A answers" movedWhileWaiting
             [ atClient 1 (ChoosePin(enrolCode, Pin "9999")) ]

    expect "Rule 27 the notice goes to the address the registry holds now, not the launch's"
        ((mailsTo (MailAddress "a.moved@hospital") enrolledAfterMove).Length = 1
         && (mailsTo mailA enrolledAfterMove).Length = (mailsTo mailA asked).Length)

    // ── Rule 27 — the registry cannot answer when the PIN is set ──
    // The confirmation code has already gone out and been answered correctly, so
    // Rule 37 is settled. Only the notice is left, and a notice may go to the address
    // this launch already had rather than not go at all.
    let enrolledRegistryDown =
        step "Rule 27 — the registry is down when A answers the prompt" { asked with Registry.Up = false }
             [ atClient 1 (ChoosePin(enrolCode, Pin "9999")) ]

    expect "Rule 27 the PIN is still set, and the notice goes to the launch's address"
        ((credentialOf ucA enrolledRegistryDown |> Option.bind _.Pin) = Some(Pin "9999")
         && (mailsTo mailA enrolledRegistryDown).Length = (mailsTo mailA asked).Length + 1)

    expect "Rule 27 and the audit says the address was the fallback"
        (enrolledRegistryDown |> audited "the address this launch had stood")

    // ── UC-2 ext 2a — A does not answer ──
    // No code comes back, so no PIN is set and the launch is not honoured (Rule 7).
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
             (ticks (confirmationCodeTtl + 4) @ launchAs ucA.Login (Some pat1))

    expect "2a an expired code is no bar: the next launch mails a fresh one (Rule 37)"
        ((mailsTo mailA afterExpiry).Length = 2
         && (codeInMail mailA afterExpiry) <> Some enrolCode)

    // ── UC-2 ext 2b — the code comes back wrong ──
    let guessed =
        step "UC-2 ext 2b — a few wrong codes, and this one is void" asked
             [ for i in 1 .. wrongConfirmationCodeLimit -> atClient 1 (ChoosePin(ConfirmationCode $"code-no%i{i}", Pin "0000")) ]

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
    // The launch runs to step 2 and stalls, because the code went to A's mail, which
    // the other hands do not control (Possibility 1). This is UC-6 ext 1a's gate,
    // applied at enrolment.
    let stranger =
        step "UC-2 ext 2c — another person tries to enrol as A" asked
             [ atClient 1 (ChoosePin(ConfirmationCode "code-stranger", Pin "1234")) ]

    expect "2c no PIN of a stranger's choosing binds to A's credential (Rule 37)"
        ((credentialOf ucA stranger |> Option.bind _.Pin) = None
         && never (function SessionOpened _ -> true | _ -> false))

    expect "2c the code went to A, nobody else was mailed, and the attempt is in the audit (Rules 27, 46)"
        ((mailsTo mailA stranger).Length = 1
         && (mailsTo mailB stranger).Length = 0
         && stranger |> audited "PIN confirmation code refused")

    // A Reader in the same position is never asked at all.
    let readerNoPin =
        step "UC-2 — a Reader with no PIN is never asked (Rule 26)" noPin
             (launchAs ucC.Login (Some pat2))

    expect "UC-2 the Reader's launch is never held up by a PIN"
        (openCount readerNoPin = 1
         && never (function PinRequired _ -> true | _ -> false))

    // No use case asks for this; it is model hygiene. A launch that stalls mid-flight
    // would otherwise sit in the launch table for ever, which is harmless here and a
    // leak in production. Every stage but AwaitingPinChoice waits on a round trip and
    // is collectable; that one waits on a human and is not.
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
//  UC-3  Prescribe and sign
// ═══════════════════════════════════════════════════════════════════════════════

let uc3 () =
    printfn ""
    printfn "############### UC-3  Prescribe and sign ###############"

    // Precondition: UC-1 completed. A has an open Session for Patient 2, started from
    // its head, and holds the Prescriber Role.
    let opened = quiet "UC-3 precondition" world (launchAs ucA.Login (Some pat2))

    expect "UC-3 precondition: the Session started from Patient 2's head (Rule 19)"
        (openedAt 1 opened = Some p2Signed.Id)

    let prescribed =
        step "UC-3 step 1 — A prescribes" opened [ act 1 (Prescribes(OrderContextId "oc-4")) ]

    expect "UC-3 step 1: each change goes to the Server, which answers from the payload (Rules 9, 32)"
        (saw (function Computed _ -> true | _ -> false))

    expect "UC-3 step 1: and nothing of it is in the record — it is only in the browser (Concept 16)"
        (planCount pat2 prescribed = 1)

    let signed = step "UC-3 steps 2 and 3 — A signs" prescribed [ yield! signs 1 pinA ]

    expect "UC-3 step 2: nothing newer exists, so nothing blocks (Rules 20, 21)"
        (never (function SubmissionBlocked _ -> true | _ -> false)
         && never (function NewerPlanNotice _ -> true | _ -> false))

    expect "UC-3 step 3: a TreatmentPlan is appended, carrying A's UserContext (Rule 15)"
        (planCount pat2 signed = 2
         && (headOf pat2 signed |> Option.map _.By) = Some ucA)

    expect "UC-3 step 3: and its base (Concept 13)"
        ((headOf pat2 signed |> Option.bind _.Base) = Some p2Signed.Id)

    expect "UC-3 Rule 15: the OrderContext changed in the Session is stamped"
        (headOf pat2 signed
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.forall (fun o -> o.Stamp = Some ucA))

    expect "UC-3 Rule 34: the Submission carried the opened-with token, and a new one came back"
        (saw (function
              | SessionRequest(_, _, SubmitTreatmentPlan req) -> Token.plan req.Opened = Some p2Signed.Id
              | _ -> false)
         && openedAt 1 signed = (headOf pat2 signed |> Option.map _.Id))

    expect "UC-3 step 3: it is now the most recent TreatmentPlan and counts clinically (Rule 17)"
        ((recordFor pat2 signed |> PatientRecord.latest |> Option.map _.Id)
            = (headOf pat2 signed |> Option.map _.Id))

    // Rule 44. The plan records the KnowledgeRuleSet it was checked under (Concept 18),
    // and it is the one the challenge was issued under, not whatever is current when
    // the record is read later.
    expect "UC-3 step 3: the signed plan names the rule set it was checked under (Rules 43, 44)"
        ((headOf pat2 signed |> Option.map _.RuleSet) = Some(RuleSetVersion 1))

    expect "UC-3 the correct entry reset the wrong-entry count (Rule 28)"
        ((credentialOf ucA signed |> Option.map _.AttemptCount) = Some 0)

    // ── UC-3 ext 1b — a new KnowledgeRuleSet is published while A works ──
    // Concept 18. The Server computes every request under the latest published set, so
    // a set published mid-Session reaches A's WorkPlan at its next computation. Rule
    // 44: the challenge is issued under the current set too, and the signed plan
    // records which one.
    let republished =
        step "UC-3 ext 1b — a new rule set is published while A is working" prescribed
             [
                 envt Environment (PublishRuleSet(RuleSetVersion 2))
                 act 1 (Prescribes(OrderContextId "oc-under-v2"))
             ]

    expect "1b the publish is recorded, and the Server now computes under the new set (Concept 18)"
        (republished.Env.RuleSet = RuleSetVersion 2
         && republished |> audited "knowledge rule set published"
         && saw (function Computed _ -> true | _ -> false))

    let signedUnderV2 = step "UC-3 ext 1b — and A signs under it" republished (signs 1 pinA)

    expect "1b the challenge is issued under the set in force now, not the one A began on (Rule 44)"
        ((headOf pat2 signedUnderV2 |> Option.map _.RuleSet) = Some(RuleSetVersion 2))

    // Concept 18's last word: every published set is kept, so a plan signed under an
    // earlier one still names that one. The record carries both, side by side.
    expect "1b and the plan signed under v1 still names v1 — the record keeps both (Concept 18)"
        ((recordFor pat2 signedUnderV2).Plans
         |> List.map _.RuleSet
         |> List.distinct
         |> List.length = 2)

    // ── UC-3 ext 3a — A gives the wrong PIN ──
    let wrongOnce = step "UC-3 ext 3a — A gives the wrong PIN" prescribed [ yield! signs 1 (Pin "0000") ]

    expect "3a verification fails and no TreatmentPlan is created"
        (planCount pat2 wrongOnce = 1
         && saw (function PinRejected _ -> true | _ -> false))

    // Rule 34. A refused Submission spends nothing, so the Client still holds what it
    // came with and may answer the refusal. A wrong PIN must not cost the User their
    // baseline as well as their attempt.
    expect "3a a refused signature spends neither the opened-with token nor the challenge (Rule 34)"
        (wrongOnce.Database.Private.Spent = prescribed.Database.Private.Spent)

    expect "3a the count is on the UserCredential, not the Session (Rule 28)"
        ((credentialOf ucA wrongOnce |> Option.map _.AttemptCount) = Some 1)

    let atLimit =
        step "UC-3 ext 3a — and at the limit the Session ends (Rules 10, 28)" wrongOnce
             [
                 yield! signs 1 (Pin "0000")
                 yield! signs 1 (Pin "0000")
             ]

    expect "3a the Session ends at the wrong-PIN limit"
        (openCount atLimit = 0
         && (match stateOf 1 atLimit with Some(Ended(WrongPinLimit, _)) -> true | _ -> false)
         && saw (function SessionRefused(Some WrongPinLimit) -> true | _ -> false))

    // Rule 11. The screen is told what ended, and nothing is discharged by telling it:
    // the notice is owed to the User, who hears it at their next launch.
    expect "3a the ending is told to the screen and still owed to the User (Rule 11)"
        (noticeOf 1 atLimit = Some Owed && not (wasTold 1 atLimit))

    expect "3a the count survives the Session, and the credential is locked with it"
        ((credentialOf ucA atLimit |> Option.map _.AttemptCount) = Some wrongPinLimit
         && (credentialOf ucA atLimit |> Option.bind _.LockedUntil).IsSome)

    // Rule 28. What survives is not just a number but the standing of the credential.
    // A fresh Session does not hand back the attempts, and nor does the correct PIN,
    // until the delay has passed.
    let relaunchedAfterLimit =
        quiet "3b — A relaunches after the limit" atLimit (launchAs ucA.Login (Some pat2))

    let stillLocked =
        step "UC-3 ext 3a — a new Session, the right PIN, and signing is still locked" relaunchedAfterLimit
             [ act 2 (Prescribes(OrderContextId "oc-locked")); yield! signs 2 pinA ]

    expect "3a within the delay the correct PIN does not sign either (Rule 28)"
        (saw (function SigningLocked -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 stillLocked = planCount pat2 relaunchedAfterLimit
         && openCount stillLocked = 1)

    // Rule 28. Wait the delay out and the same credential signs again. The wait
    // outlives the Session that was waiting, so what signs is a new Session of the same
    // User: it was the credential that was locked, not the Session.
    let waited =
        let until = (credentialOf ucA stillLocked |> Option.bind _.LockedUntil).Value
        quiet "3b — the delay passes" stillLocked (ticks (until - stillLocked.Env.Now + 4))

    let waitedOut =
        step "UC-3 ext 3a — and the same credential signs again, with no reset at all (Rule 28)" waited
             (launchAs ucA.Login (Some pat2)
              @ [ act 3 (Prescribes(OrderContextId "oc-after-the-wait")); yield! signs 3 pinA ])

    expect "3a a locked credential signs again once the delay passes — no reset, no mail (Rule 28)"
        (planCount pat2 waitedOut = planCount pat2 waited + 1
         && (credentialOf ucA waitedOut |> Option.map _.AttemptCount) = Some 0
         && (credentialOf ucA waitedOut |> Option.bind _.LockedUntil) = None
         && never (function ResetCodeMailed -> true | _ -> false)
         && never (function SigningLocked -> true | _ -> false))

    // And each further wrong entry past the limit costs twice the last.
    expect "3a the delay doubles with every wrong entry past the limit (Rule 28)"
        (UserCredential.lockFor wrongPinLimit = pinLockBase
         && UserCredential.lockFor (wrongPinLimit + 1) = pinLockBase * 2
         && UserCredential.lockFor (wrongPinLimit + 2) = pinLockBase * 4)

    // Rule 28, and the half that makes the doubling worth anything: a wrong entry made
    // *while* the credential is locked counts too. Otherwise a guesser keeps guessing
    // through the delay and pays for one lock however many they try, so the delay would
    // grow with patience rather than with guessing.
    let guessedThrough =
        step "UC-3 ext 3a — more guesses while it is already locked (Rule 28)" stillLocked
             (signs 2 (Pin "0009") @ signs 2 (Pin "0008"))

    expect "3a a guess made while locked counts, and pushes the delay further out (Rule 28)"
        (let before = (credentialOf ucA stillLocked).Value
         let after = (credentialOf ucA guessedThrough).Value

         after.AttemptCount = before.AttemptCount + 2
         && after.LockedUntil > before.LockedUntil
         && saw (function SigningLocked -> true | _ -> false))

    // The correct PIN inside the delay costs nothing: the delay answers what has
    // already happened, and only waiting lifts it (Rule 28).
    let rightPinWhileLocked =
        step "UC-3 ext 3a — and the right PIN inside the delay costs nothing" stillLocked (signs 2 pinA)

    expect "3a the correct PIN inside the delay neither signs nor counts against the User (Rule 28)"
        ((credentialOf ucA rightPinWhileLocked |> Option.map _.AttemptCount)
            = (credentialOf ucA stillLocked |> Option.map _.AttemptCount)
         && (credentialOf ucA rightPinWhileLocked |> Option.bind _.LockedUntil)
            = (credentialOf ucA stillLocked |> Option.bind _.LockedUntil)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // Rule 37 is still a way out, and a faster one: a code by mail, a new PIN, one act.
    let askedForReset = quiet "3b — A asks for a reset" stillLocked [ act 2 AsksPinReset ]

    let unlocked =
        let code = (codeInMail mailA askedForReset).Value
        step "UC-3 ext 3a — or the mailed code replaces the PIN, and signing works at once (Rule 37)" askedForReset
             [ act 2 (EntersResetCode(code, Pin "4242")); yield! signs 2 (Pin "4242") ]

    expect "3a the replacement clears the lock and the count with it, without waiting (Rules 28, 37)"
        ((credentialOf ucA unlocked |> Option.bind _.LockedUntil) = None
         && (credentialOf ucA unlocked |> Option.map _.AttemptCount) = Some 0
         && planCount pat2 unlocked = planCount pat2 askedForReset + 1)

    // ── UC-3 ext 1a and 2a — the record moves on while A works ──
    // One setup, two extensions. B signs while A is open. If A does anything at all
    // first, the response tells A and does not stop them (ext 1a, Rules 21, 22). If
    // nothing told A first, the Submission itself is the notice (ext 2a, Rule 20).
    // UC-4 is the same ground from B's side.
    let bSigned =
        quiet "UC-3 ext 1a setup — B signs while A is open" opened
              (launchAs ucB.Login (Some pat2)
               @ [ act 2 (Prescribes(OrderContextId "oc-5")); yield! signs 2 pinB ])

    let toldFirst =
        step "UC-3 ext 1a — A prescribes, and the response says a newer plan exists" bSigned
             [ act 1 (Prescribes(OrderContextId "oc-told")) ]

    expect "1a A is told whose plan it is and when it was signed (Rule 21)"
        (saw (function NewerPlanNotice(uc, _) -> uc = ucB | _ -> false))

    expect "1a and the telling stops nothing: the request it rode on was answered (Rule 22)"
        (saw (function Computed _ -> true | _ -> false)
         && never (function SubmissionBlocked _ -> true | _ -> false))

    let blocked =
        step "UC-3 ext 2a — A signs, and is blocked before the PIN is asked for" bSigned
             [ act 1 (Prescribes(OrderContextId "oc-6")); yield! signs 1 pinA ]

    expect "2a the block is decided first: no credential is ever read (Rules 20, 22)"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && never (function ReadCredential(ForRequest _, _) -> true | _ -> false))

    expect "2a and nothing was appended"
        (planCount pat2 blocked = planCount pat2 bSigned)

    // ── UC-3 ext 3b — the signature modal ──
    // Rule 43. Asking to sign is one act and signing what the modal shows is another,
    // with nothing sent in between and no change allowed under it. The modal is up
    // because the User asked and the Server answered: the honest path, stopped
    // half way, which is where the rule bites.
    let modalUp =
        step "UC-3 ext 3b — A asks to sign, and is shown what the signature would attest to" signed
             [ act 1 (Prescribes(OrderContextId "oc-shown")); act 1 Signs ]

    expect "3b the challenge is shown and nothing is submitted: the modal gates the signature (Rule 43)"
        (saw (function SignChallengeIssued _ -> true | _ -> false)
         && (clientOf 1 modalUp |> Option.bind _.Modal |> Option.map Token.verifyChallenge) = Some true
         && never (function SessionRequest(_, _, SubmitTreatmentPlan _) -> true | _ -> false)
         && planCount pat2 modalUp = planCount pat2 signed
         && showingOf 1 modalUp = Some "sign the plan as shown, or cancel and edit")

    let heldStill =
        step "UC-3 ext 3b — with the modal up, the WorkPlan cannot change" modalUp
             [ act 1 (Prescribes(OrderContextId "oc-late")); act 1 (EntersPatientData(PatientData "by hand")) ]

    expect "3b the Client refuses locally: nothing is sent, and the WorkPlan is untouched (Rule 43)"
        (workingAt 1 heldStill = workingAt 1 modalUp
         && dataAt 1 heldStill = dataAt 1 modalUp
         && never (function SessionRequest _ -> true | _ -> false))

    let cancelled = step "UC-3 ext 3b — the User leaves the modal" heldStill [ act 1 CancelsSign ]

    expect "3b nothing was signed, and prescribing is possible again"
        (planCount pat2 cancelled = planCount pat2 signed
         && (clientOf 1 cancelled |> Option.bind _.Modal) = None)

    // And cancelling really does drop it: the challenge is gone, so a confirm
    // afterwards — PIN and all — has nothing to answer and sends nothing (Rule 43).
    let confirmedAfterCancel =
        step "UC-3 ext 3b — a confirm after the cancel answers nothing" cancelled [ act 1 (ConfirmsSign pinA) ]

    expect "3b a confirm with no challenge in front of the User sends nothing at all (Rule 43)"
        (never (function SessionRequest _ -> true | _ -> false)
         && planCount pat2 confirmedAfterCancel = planCount pat2 cancelled)

    let signedAfresh =
        step "UC-3 ext 3b — and the next signature asks for a challenge of its own" cancelled
             [ act 1 (Prescribes(OrderContextId "oc-7")); yield! signs 1 pinA ]

    expect "3b the honest path never sees a refusal: a fresh challenge, and the signature lands"
        (saw (function SignChallengeIssued _ -> true | _ -> false)
         && never (function SubmissionRefused _ -> true | _ -> false))

    // The challenge `signedAfresh` issued, read off the wire before the steps below
    // overwrite `lastTrace`. It is what the hand-built Submission at the end replays.
    let stale = (challengeIssued ()).Value

    // ── UC-3 ext 3c — someone else takes the keyboard during the challenge ──
    // A has the modal up and walks away. Whoever sits down cannot edit under it: the
    // Client refuses while the challenge stands, so editing means cancelling first,
    // and cancelling drops the challenge. What they cannot do is change the plan and
    // have the old challenge still answer for it.
    let takenAtTheModal =
        step "UC-3 ext 3c — B takes the keyboard while A's modal is up" modalUp
             [ act 1 (Prescribes(OrderContextId "oc-b-at-the-modal")) ]

    expect "3c editing under the modal is refused locally: nothing is sent and the plan is unchanged"
        (workingAt 1 takenAtTheModal = workingAt 1 modalUp
         && never (function SessionRequest _ -> true | _ -> false)
         && showingOf 1 takenAtTheModal = Some "finish or cancel the signature first")

    let cancelledByB =
        step "UC-3 ext 3c — so B cancels first, and the challenge goes with it" takenAtTheModal
             [ act 1 CancelsSign; act 1 (Prescribes(OrderContextId "oc-b-at-the-modal")) ]

    expect "3c the challenge is dropped, so B's edit is prescribing and nothing is signed"
        ((clientOf 1 cancelledByB |> Option.bind _.Modal) = None
         && saw (function Computed _ -> true | _ -> false)
         && planCount pat2 cancelledByB = planCount pat2 modalUp)

    // And the old challenge cannot be brought back to cover the edit: it names the plan
    // A saw, not the one B made. That is what the hand-built Submission below shows.
    let mismatched =
        let sid = (sidAt 1 signedAfresh).Value
        let opened = (clientOf 1 signedAfresh).Value.Opened.Value
        let changed =
            { workOf 1 signedAfresh with
                Orders =
                    { Id = OrderContextId "oc-slipped"; Patient = Some pat2; Content = "added after"; Stamp = None }
                    :: (workOf 1 signedAfresh).Orders }
        step "UC-3 ext 3c — the challenge is returned over a plan it does not name" signedAfresh
             [
                 fromClient 1
                     (SessionRequest(
                         sid,
                         None,
                         SubmitTreatmentPlan
                             {
                                 Work = changed
                                 Opened = opened
                                 Challenge = Some stale
                                 DataOk = None
                                 Pin = Some pinA
                                 Key = IdemKey "mismatch-1"
                             }))
             ]

    expect "3c the signature is refused, and nothing is appended (Rule 43)"
        (saw (function SubmissionRefused why -> why.Contains "Rule 43" | _ -> false)
         && planCount pat2 mismatched = planCount pat2 signedAfresh)

    // ── UC-3 ext 3d — the Submission arrives late, repeated or out of order ──
    // Rule 45. The retry carries the key of the request it retries, so the Database
    // answers it rather than doing it twice. This is a real signature, asked for the
    // honest way and then put on the wire twice by hand: the challenge has to be the
    // Server's own, or Rule 43 refuses both copies and Rule 45 is never reached.
    let readyToSign =
        quiet "UC-3 ext 3d setup — A asks for a challenge" signedAfresh
              [ act 1 (Prescribes(OrderContextId "oc-retry")); act 1 Signs ]

    let duplicated =
        let sid = (sidAt 1 readyToSign).Value
        let st = (clientOf 1 readyToSign).Value
        let again =
            SessionRequest(
                sid,
                st.Opened,
                SubmitTreatmentPlan
                    {
                        Work = st.Work
                        Opened = st.Opened.Value
                        Challenge = st.Modal
                        DataOk = st.DataOk
                        Pin = Some pinA
                        Key = IdemKey "retry-1"
                    })
        step "UC-3 ext 3d — the same Submission arrives twice" readyToSign
             [ fromClient 1 again; fromClient 1 again ]

    expect "3d one TreatmentPlan, and the same answer both times (Rule 45)"
        (planCount pat2 duplicated = planCount pat2 readyToSign + 1
         && countOf (function TreatmentPlanSubmitted _ -> true | _ -> false) = 2
         && (lastTrace
             |> List.choose (function { Msg = TreatmentPlanSubmitted(id, _) } -> Some id | _ -> None)
             |> List.distinct
             |> List.length) = 1)

    // Rule 45 answers a retry carrying the same key. A signature replayed under a
    // *fresh* key is a different request asking to sign again, and what stops it is the
    // spent challenge (Rule 43), not the idempotency table.
    let replayedChallenge =
        let signedOnce =
            quiet "3e precondition — a signature that landed, and the challenge it used" duplicated
                  [ act 1 (Prescribes(OrderContextId "oc-once")); act 1 Signs ]

        let challenge = (challengeIssued ()).Value
        let landed = quiet "3e precondition — and it lands" signedOnce [ act 1 (ConfirmsSign pinA) ]

        let sid = (sidAt 1 landed).Value
        let opened = (clientOf 1 landed).Value.Opened.Value

        step "UC-3 ext 3d — the spent SigningChallenge is presented again" landed
             [
                 fromClient 1
                     (SessionRequest(
                         sid,
                         None,
                         SubmitTreatmentPlan
                             {
                                 Work = workOf 1 landed
                                 Opened = opened
                                 Challenge = Some challenge
                                 DataOk = None
                                 Pin = Some pinA
                                 Key = IdemKey "replayed-challenge"
                             }))
             ]

    expect "3d a spent SigningChallenge signs nothing a second time (Rules 43, 45)"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── UC-3 ext 3e — A does not sign ──
    // Nothing enters the record: the WorkPlan is only ever in the browser (Concept 16).
    // Either it dies with the browser, as here, or A carries it into the next Session
    // for the same Patient, which is UC-8 step 3.
    let neverSigned =
        step "UC-3 ext 3e — A prescribes, never signs, and closes the browser" signedAfresh
             [ act 1 (Prescribes(OrderContextId "oc-never-signed")); atClient 1 CloseBrowser ]

    expect "3e the record is where it was, and the work went with the browser (Concept 16)"
        (planCount pat2 neverSigned = planCount pat2 signedAfresh
         && (workingAt 1 neverSigned).IsEmpty
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── UC-3 ext 2b, Rule 44 — the Patient Data moved under the Session ──
    // Concept 2 reads the data once, at the launch. A signature is where that stops
    // being good enough. The platform is asked again, and a signature over data it no
    // longer holds does not land until the User has seen what changed.
    let dataMoved =
        { signedAfresh with
            Platform.Data =
                signedAfresh.Platform.Data |> Map.add pat2 (PatientData "pat-2: 7y, 26kg — revised") }

    let stoppedAtData =
        step "UC-3 ext 2b — the platform's Patient Data has changed since the launch" dataMoved
             [ act 1 (Prescribes(OrderContextId "oc-8")); yield! signs 1 pinA ]

    expect "2b the signature does not land: the User is shown what the platform now holds"
        (saw (function PatientDataChanged _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 stoppedAtData = planCount pat2 signedAfresh
         && dataAt 1 stoppedAtData = Some(PatientData "pat-2: 7y, 26kg — revised"))

    let acceptedData =
        step "UC-3 ext 2b — A reads the new data and signs again" stoppedAtData [ yield! signs 1 pinA ]

    expect "2b accepted, the signature lands (Rules 21, 34's pattern, over data)"
        (planCount pat2 acceptedData = planCount pat2 stoppedAtData + 1)

    // Concept 13, and Rule 44's last sentence. The plan records the data the User was
    // shown and accepted, not what the launch read and not what the platform holds now.
    // A signed plan explains itself from its own record.
    expect "2b the signed plan records the data the User accepted, not the launch's (Concept 13)"
        (let head = (headOf pat2 acceptedData).Value

         head.Data = Some(PatientData "pat-2: 7y, 26kg — revised")
         && (match head.From with Some(FromPlatform _) -> true | _ -> false))

    // ── UC-3 ext 2b, Rule 44 — and the branch where the platform cannot be asked ──
    // UC-1 ext 6a is this outage at a launch; this is the same outage at a signature.
    // Nothing is refused: the User is told the data behind the signature is the
    // Session's own and unchecked, and signs on it only by saying so, the same shape as
    // a change. Run from a Session that has accepted nothing, because an acceptance the
    // User already gave stands for the data it names and would forgive the outage
    // rather than test it.
    let platformSilent =
        step "UC-3 ext 2b — the platform cannot be asked when the challenge is due" signedAfresh
             (envt PatientDataPlatform (Stop PatientDataPlatform)
              :: [ act 1 (Prescribes(OrderContextId "oc-unchecked")) ]
              @ signs 1 pinA)

    expect "2b no challenge is issued, and the User is told the data is unverified"
        (saw (function PatientDataUnverified _ -> true | _ -> false)
         && never (function SignChallengeIssued _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 platformSilent = planCount pat2 signedAfresh)

    let signedUnchecked =
        step "UC-3 ext 2b — A says so, and signs on the data as it stands" platformSilent (signs 1 pinA)

    expect "2b accepting the unverified data is what lets the signature land"
        (planCount pat2 signedUnchecked = planCount pat2 platformSilent + 1)

    signed

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-4  Two Users, one Patient
// ═══════════════════════════════════════════════════════════════════════════════

let uc4 () =
    printfn ""
    printfn "############### UC-4  Two Users, one Patient ###############"

    // Precondition: A and B each hold an open Session for Patient 2. Rule 8 permits
    // this: it limits Sessions per User, not per Patient.
    let both =
        step "UC-4 precondition — A and B each open a Session for Patient 2" world
             (launchAs ucA.Login (Some pat2) @ launchAs ucB.Login (Some pat2))

    expect "UC-4 Rule 8 limits Sessions per User, not per Patient: both are open"
        (openCount both = 2
         && (openOfUser ucA both).Length = 1
         && (openOfUser ucB both).Length = 1)

    // Guarantee 3, and the reason it holds by construction: the two carts are in two
    // Browsers, and the Server holds neither (Rule 32).
    expect "UC-4 step 1: the two carts are in the two Clients, and nowhere else (Rule 32, Guarantee 3)"
        (both.GenPres.InFlight.IsEmpty && sidAt 1 both <> sidAt 2 both)

    let aSigned =
        step "UC-4 step 2 — A signs" both
             [
                 act 1 (Prescribes(OrderContextId "oc-a"))
                 yield! signs 1 pinA
             ]

    expect "UC-4 step 2: a TreatmentPlan in A's name, which now counts (Rule 17)"
        (planCount pat2 aSigned = 2
         && (headOf pat2 aSigned |> Option.map _.By) = Some ucA)

    // Rules 21, 22, and Consequence 6: B did not see A's work and was not told of it
    // until B's own next request, which is any request at all.
    let bTold =
        step "UC-4 step 3 — B acts, and the response says a newer plan exists" aSigned
             [ act 2 (Prescribes(OrderContextId "oc-b")) ]

    expect "UC-4 step 3: B is told whose plan it is and when it was signed (Rule 21)"
        (saw (function NewerPlanNotice(uc, _) -> uc = ucA | _ -> false))

    expect "UC-4 step 3: and nothing is blocked by the telling — B keeps working (Rule 22)"
        (never (function SubmissionBlocked _ -> true | _ -> false)
         && saw (function Computed _ -> true | _ -> false))

    let bBlocked =
        step "UC-4 ext 3a — B signs anyway, and the Submission is refused" bTold
             [ yield! signs 2 pinB ]

    expect "3a a TreatmentPlan newer than the one B opened with blocks the Submission (Rule 20)"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && planCount pat2 bBlocked = 2)

    let bTookOver =
        step "UC-4 step 4 — B opens A's TreatmentPlan, which lifts the block (Rule 18)" bBlocked
             [ act 2 (OpensTreatmentPlan (headOf pat2 bBlocked).Value.Id) ]

    expect "UC-4 step 4: opening it re-mints the token, so it is what the Session opened with (Rule 34)"
        (saw (function TreatmentPlanOpened _ -> true | _ -> false)
         && openedAt 2 bTookOver = (headOf pat2 bBlocked |> Option.map _.Id))

    let bReapplied =
        step "UC-4 step 4 — B reapplies their own work and signs" bTookOver
             [
                 act 2 (Prescribes(OrderContextId "oc-b"))
                 yield! signs 2 pinB
             ]

    expect "UC-4 step 4: the signature attests the whole set in B's name (Rule 15)"
        ((headOf pat2 bReapplied |> Option.map _.By) = Some ucB)

    // Rule 15, the half that only shows here, and Rule 35, which is how the Server
    // knows: it diffed the payload against the base TreatmentPlan rather than believing
    // any stamp the Client sent.
    let orders = headOf pat2 bReapplied |> Option.map _.Orders |> Option.defaultValue []

    expect "UC-4 step 4: the OrderContext B changed carries B's stamp"
        (orders |> List.exists (fun o -> o.Id = OrderContextId "oc-b" && o.Stamp = Some ucB))

    expect "UC-4 step 4: the ones B left untouched keep A's stamp (Rules 15, 35)"
        (orders |> List.exists (fun o -> o.Id = OrderContextId "oc-a" && o.Stamp = Some ucA)
         && orders |> List.exists (fun o -> o.Id = OrderContextId "oc-1" && o.Stamp = Some ucA))

    // Nothing signed is ever lost: the PatientRecord is append-only (Concept 12), so
    // A's plan survives B's.
    expect "UC-4 nothing signed is lost: A's TreatmentPlan survives B's (Concept 12)"
        (recordFor pat2 bReapplied |> _.Plans |> List.exists (fun s -> s.By = ucA))

    // ── UC-4 ext 3b — both sign at once ──
    // Two signatures over the same base, in flight together: exactly one can land
    // (Rules 36, 42). Confirming is what leaves the Client, so that is what
    // interleaves. A confirm delivered before its challenge would confirm nothing.
    let bothChallenged =
        quiet "UC-4 ext 3b precondition — both ask to sign, and both are shown a challenge" both
              [
                  act 1 (Prescribes(OrderContextId "oc-a2"))
                  act 2 (Prescribes(OrderContextId "oc-b2"))
                  act 1 Signs
                  act 2 Signs
              ]

    let bothSign =
        racing "UC-4 ext 3b — A and B sign over the same base at once" bothChallenged
               [ act 1 (ConfirmsSign pinA); act 2 (ConfirmsSign pinB) ]

    expect "3b exactly one signature landed, and the record moved once (Rules 36, 42)"
        (countOf (function TreatmentPlanSubmitted _ -> true | _ -> false) = 1
         && planCount pat2 bothSign = planCount pat2 bothChallenged + 1)

    expect "3b the loser is told whose work stands in the way, never which TreatmentPlan (Rule 20)"
        (countOf (function SubmissionBlocked _ -> true | _ -> false) = 1
         && saw (function SubmissionBlocked uc -> uc = ucA || uc = ucB | _ -> false))

    // ── Rule 18 — an older TreatmentPlan is readable, and not a place to build ──
    let history =
        quiet "Rule 18 precondition — a record with several TreatmentPlans" bReapplied
              (launchAs ucA.Login (Some pat2))

    let older = recordFor pat2 history |> _.Plans |> List.skip 1 |> List.tryHead

    let readingHistory =
        step "Rule 18 — A opens an older TreatmentPlan" history
             [ act 3 (OpensTreatmentPlan older.Value.Id) ]

    expect "Rule 18 the whole history is readable by anyone who may see the Patient"
        (saw (function TreatmentPlanOpened _ -> true | _ -> false)
         && openedAt 3 readingHistory = Some older.Value.Id)

    let buildingOnIt =
        step "Rule 18 — and building on it is blocked, because a newer one exists" readingHistory
             ([ act 3 (Prescribes(OrderContextId "oc-from-history")) ] @ signs 3 pinA)

    expect "Rule 20 read-only falls out of the baseline: no second mechanism, and nothing lands"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && planCount pat2 buildingOnIt = planCount pat2 readingHistory)

    bReapplied

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-5  Someone else takes over the workstation
// ═══════════════════════════════════════════════════════════════════════════════

let uc5 () =
    printfn ""
    printfn "############### UC-5  Someone else takes over the workstation ###############"

    // Precondition: A has an open Session for Patient 1 and walks away. Possibility 1:
    // this is not ours to prevent, only to handle.
    let aWalksAway = quiet "UC-5 precondition" world (launchAs ucA.Login (Some pat1))

    let bPrescribes =
        step "UC-5 steps 1 and 2 — B works in A's Session" aWalksAway
             [ act 1 (Prescribes(OrderContextId "oc-8")) ]

    expect "UC-5 step 2: the work carries no attribution and sits in no record (Concept 16)"
        (planCount pat1 bPrescribes = 0
         && saw (function Computed _ -> true | _ -> false))

    // Step 3: signing always names the Session's User, so the Client asks for A's PIN
    // and B does not have it. Supplying their own proves nothing, because the Server
    // verifies against the Session User's credential (Rules 15, 23, 33).
    let bTriesToSign =
        step "UC-5 step 3 — B signs, with the only PIN they have" bPrescribes [ yield! signs 1 pinB ]

    expect "UC-5 step 3: nothing is committed and the record is untouched (Rule 42)"
        (saw (function PinRejected _ -> true | _ -> false)
         && planCount pat1 bTriesToSign = 0
         && (recordFor pat1 bTriesToSign |> PatientRecord.latest).IsNone)

    // Signing always names the Session's User, so verification runs against A's
    // credential whoever is at the keyboard. That is what caps B's guessing in ext 3a,
    // and why it spends A's allowance rather than B's.
    expect "UC-5 the wrong entry counted against the Session's User's credential — A's, not B's (Rules 23, 28, 33)"
        ((credentialOf ucB bTriesToSign |> Option.map _.AttemptCount) = Some 0
         && (credentialOf ucA bTriesToSign |> Option.map _.AttemptCount) = Some 1)

    // ── UC-5 ext 2a — B relaunches as themselves ──
    let bOwnSession =
        step "UC-5 ext 2a — B relaunches from MainEHR as themselves, Patient 1 active" bPrescribes
             (launchAs ucB.Login (Some pat1))

    expect "2a Rule 8 is per User: a Session of B's own opens, and A's is untouched"
        (openCount bOwnSession = 2
         && (openOfUser ucA bOwnSession).Length = 1
         && (openOfUser ucB bOwnSession).Length = 1)

    expect "2a Patient 1 has no record, so it starts from nothing (Rule 19)"
        (openedAt 2 bOwnSession = None)

    let bSignedOwn =
        step "UC-5 ext 2a — B re-enters the work and signs as themselves" bOwnSession
             [
                 act 2 (Prescribes(OrderContextId "oc-8"))
                 yield! signs 2 pinB
             ]

    expect "2a and signs as themselves (Rule 15)"
        ((headOf pat1 bSignedOwn |> Option.map _.By) = Some ucB)

    // ── UC-5 ext 3a — B guesses instead ──
    let guessed =
        step "UC-5 ext 3a — B guesses at A's PIN" bTriesToSign
             [
                 yield! signs 1 (Pin "0001")
                 yield! signs 1 (Pin "0002")
             ]

    expect "3a at the configured number of consecutive wrong entries the Session ends (Rules 10, 28)"
        (openCount guessed = 0
         && (match stateOf 1 guessed with Some(Ended(WrongPinLimit, _)) -> true | _ -> false))

    // Rule 11, and the point of it. The screen B is standing at is refused and told
    // what ended, but B is not A, so nothing is discharged: A's notice is still owed and
    // A hears it at their next launch. Otherwise the guesser could dismiss the very
    // notice that exists to tell A someone was guessing.
    expect "3a nothing was created, and the screen is refused, not told for A (Rule 11)"
        (planCount pat1 guessed = 0
         && saw (function SessionRefused(Some WrongPinLimit) -> true | _ -> false)
         && noticeOf 1 guessed = Some Owed
         && not (wasTold 1 guessed))

    // Rule 11. The screen is where B is standing, so the screen is not where this is
    // told. It goes to the address the registry holds, as a PIN change does (Rule 27).
    expect "3a and it is mailed to A, because the screen is where the guessing happened (Rule 27)"
        ((mailsTo mailA guessed).Length = 1
         && guessed |> audited "wrong-PIN limit reached")

    let relaunchNoHelp =
        step "UC-5 ext 3a — relaunching as A does not reset the count (Rule 28)" guessed
             (launchAs ucA.Login (Some pat1) @ [ yield! signs 2 (Pin "0003") ])

    expect "3a the count belongs to the UserCredential, so guessing is capped outright"
        ((credentialOf ucA relaunchNoHelp |> Option.map _.AttemptCount |> Option.map (fun c -> c >= wrongPinLimit))
            = Some true)

    // Guarantee 2. Nothing of B's work survives anywhere: it was never signed, so it
    // never left the browser (Concept 16). A's relaunch starts from the record, which
    // for Patient 1 is still empty.
    expect "3a nothing of the guesser's work is anywhere: the record is still empty (Concept 16)"
        (planCount pat1 relaunchNoHelp = 0
         && openedAt 2 relaunchNoHelp = None
         && (workingAt 2 relaunchNoHelp).IsEmpty)

    bSignedOwn


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-6  A User forgets their PIN
// ═══════════════════════════════════════════════════════════════════════════════

let uc6 () =
    printfn ""
    printfn "############### UC-6  A User forgets their PIN ###############"

    // Precondition: A has an open Session and a UserCredential with a PIN set but
    // forgotten. Rule 37: the PIN is never removed, only replaced, and what authorises
    // the replacement arrives by mail. So there is no moment in which A's credential is
    // one that anybody at that workstation could claim.
    let opened = quiet "UC-6 precondition" world (launchAs ucA.Login (Some pat2))

    let asked = step "UC-6 step 1 — A asks GenPRES to reset the PIN" opened [ act 1 AsksPinReset ]

    expect "UC-6 step 1: nothing is removed — the PIN in force is still the old one (Rule 37)"
        ((credentialOf ucA asked |> Option.bind _.Pin) = Some pinA
         && saw (function ResetCodeMailed -> true | _ -> false))

    // Rule 27. The address is the registry's, so the registry is asked for it here,
    // on this request. The record of the ask says a code went out; it does not say
    // which, and it does not say where.
    expect "UC-6 step 1: a one-time code goes to the registry's address, and the ask is recorded (Rules 27, 37)"
        ((mailsTo mailA asked).Length = 1
         && (codeInMail mailA asked).IsSome
         && asked |> audited "PIN reset confirmation code sent")

    expect "UC-6 step 1: the address was asked for at the moment it was needed (Rule 27)"
        (before (function ResolveUser(ForRequest _, l) -> l = ucA.Login | _ -> false)
                (function SendMail _ -> true | _ -> false))

    // ── Rule 27 — the address used is the registry's now, not the launch's ──
    // The whole reason nothing keeps a copy. A's address changes at the registry
    // while the Session is open; the next mail goes to the new one, with no relaunch
    // and nothing to invalidate.
    let movedAddress = MailAddress "a.new@hospital"

    let afterMove =
        { opened with
            Registry.Users = opened.Registry.Users |> Map.add ucA.Login (ucA, movedAddress) }

    let mailedToNew = step "Rule 27 — A asks for a reset after the move" afterMove [ act 1 AsksPinReset ]

    expect "Rule 27 the mail goes to the address the registry holds now, not the one the launch saw"
        ((mailsTo movedAddress mailedToNew).Length = 1
         && (mailsTo mailA mailedToNew).IsEmpty)

    // ── UC-6 ext 1c, Rule 27 — the registry cannot say where to send it ──
    // Nothing is parked and nothing is replaced: a code the User can never be given
    // would sit there until it expired, blocking the next reset for no reason.
    let registryDown =
        step "UC-6 ext 1c — the registry is unreachable when the address is needed" { opened with Registry.Up = false }
             [ act 1 AsksPinReset ]

    expect "Rule 27 no mail, no parked reset, and the User is told (UC-6 ext 1c)"
        ((mailsTo mailA registryDown).IsEmpty
         && registryDown.Database.Private.Resets.IsEmpty
         && saw (function ResetDenied AddressUnavailable -> true | _ -> false)
         && never (function StartReset _ -> true | _ -> false))

    expect "Rule 27 the PIN in force is untouched, so A can ask again"
        ((credentialOf ucA registryDown |> Option.bind _.Pin) = Some pinA
         && registryDown |> audited "no confirmation code")

    // Rule 37, and the line the fallback does not cross. The Session has an address
    // from its launch, and a notice would have used it. A confirmation code is a
    // credential, and the whole of Rule 37 is that it reaches an address the person at
    // this workstation does not control. A remembered one may be older than the
    // registry's, so a code is never sent to it.
    expect "Rule 37 a confirmation code is never sent to a remembered address, even though one is there"
        ((newestRecord registryDown |> Option.bind _.Mail) = Some mailA
         && (mailsTo mailA registryDown).IsEmpty)

    // ── UC-6 ext 2b, Rule 27 — a notice falls back on the address this Session had ──
    // The other side of the line. A code cannot be sent to a remembered address, but a
    // notice can: not going at all is worse, because the notice is what tells the User
    // something happened to their credential. A has a code already, so the registry
    // going down now does not stop the replacement — only the freshness of where the
    // notice goes.
    let code = (codeInMail mailA asked).Value

    let replacedWithRegistryDown =
        step "UC-6 ext 2b — A completes the reset while the registry is down" { asked with Registry.Up = false }
             [ act 1 (EntersResetCode(code, Pin "6060")) ]

    expect "UC-6 ext 2b: the PIN is replaced and the notice still goes out, on the fallback address (Rule 27)"
        ((credentialOf ucA replacedWithRegistryDown |> Option.bind _.Pin) = Some(Pin "6060")
         && (mailsTo mailA replacedWithRegistryDown).Length = (mailsTo mailA asked).Length + 1
         && saw (function PinChanged -> true | _ -> false))

    expect "UC-6 ext 2b: and the audit says the address was the Session's, not the registry's (Rule 27)"
        (replacedWithRegistryDown |> audited "the address this Session had stood")

    // Rule 46. Every mail names where it went, so a User who says they never got one
    // can be answered without asking the registry what it used to hold.
    expect "Rule 46 the audit names the address every mail went to"
        (auditOf replacedWithRegistryDown
         |> List.exists (fun a -> a.What.Contains "PIN replaced" && a.What.Contains "a@hospital"))

    // Rule 37, one code at a time. Asking again while the first is still good sends
    // nothing. A second code would void the one A is reading, so anybody able to press
    // the button could keep A from ever completing a reset, and every press would be
    // another mail to an address GenPRES did not choose. Two requests, one mail.
    let askedTwice = step "UC-6 step 1 — A asks a second time, while the first code still stands" asked [ act 1 AsksPinReset ]

    expect "UC-6 two requests, one mail: a standing code is not voided by asking again (Rule 37)"
        ((mailsTo mailA askedTwice).Length = 1
         && saw (function ResetDenied ResetPending -> true | _ -> false)
         && never (function SendMail _ -> true | _ -> false)
         && askedTwice |> audited "one is already pending")

    expect "UC-6 and the code A is holding still works: nothing was taken from them (Rule 37)"
        (let code = (codeInMail mailA askedTwice).Value

         let usedIt =
             quiet "UC-6 — and the standing code still works" askedTwice
                   [ act 1 (EntersResetCode(code, Pin "7777")) ]

         (credentialOf ucA usedIt |> Option.bind _.Pin) = Some(Pin "7777"))

    // ── UC-6 ext 1a — B, at A's open workstation, triggers the reset ──
    // The trigger cannot be prevented, because a launch proves control of a MainEHR
    // Session and not a person (Possibility 1). It gains nothing: the code went to A,
    // and B stalls at it.
    let stalled =
        step "UC-6 ext 1a — whoever is at the screen guesses at the code" asked
             [ act 1 (EntersResetCode(ConfirmationCode "code-guess", Pin "0000")) ]

    expect "1a a guessed code changes nothing, and A's PIN still stands (Rule 37)"
        (saw (function ResetDenied(WrongCode _) -> true | _ -> false)
         && (credentialOf ucA stalled |> Option.bind _.Pin) = Some pinA
         && (mailsTo mailB stalled).Length = 0)

    let code = (codeInMail mailA asked).Value

    let replaced =
        step "UC-6 step 2 — A reads the mail and replaces the PIN in one act" asked
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "UC-6 step 2: replaced, never removed — there is no PIN-less moment (Concept 7, Rule 37)"
        ((credentialOf ucA replaced |> Option.bind _.Pin) = Some(Pin "5555")
         && saw (function PinChanged -> true | _ -> false)
         && never (function ResetDenied _ -> true | _ -> false))

    expect "UC-6 step 2: mailed and recorded, and the new PIN starts at zero (Rules 27, 28)"
        ((mailsTo mailA replaced).Length = 2
         && replaced |> audited "PIN replaced"
         && (credentialOf ucA replaced |> Option.map _.AttemptCount) = Some 0)

    let signedWithNew =
        step "UC-6 step 3 — A signs with the new PIN, in the Session they were already in" replaced
             [ act 1 (Prescribes(OrderContextId "oc-r")); yield! signs 1 (Pin "5555") ]

    expect "UC-6 step 3: the new PIN signs, and no relaunch was needed (Concept 14)"
        (planCount pat2 signedWithNew = planCount pat2 replaced + 1
         && (headOf pat2 signedWithNew |> Option.map _.By) = Some ucA)

    let spent =
        step "UC-6 step 3 — and the code is spent: honoured once, and never again" signedWithNew
             [ act 1 (EntersResetCode(code, Pin "7777")) ]

    expect "UC-6 step 3: a spent code buys nothing, and the PIN it already replaced stands"
        (saw (function ResetDenied NoResetPending -> true | _ -> false)
         && (credentialOf ucA spent |> Option.bind _.Pin) = Some(Pin "5555"))

    // ── UC-6 ext 1b — the code is not used in time ──
    // What expires a code is time, and time here runs in handled messages. Waiting a
    // code out by ticking would idle the Session out first (Rule 10), which is a
    // different scenario, so the code is aged instead: its expiry is moved into the
    // past, which is what the wait would have done to it.
    let aged =
        { asked with
            Database.Private.Resets =
                asked.Database.Private.Resets |> Map.map (fun _ r -> { r with Expires = asked.Env.Now - 1 }) }

    let expiredCode =
        step "UC-6 ext 1b — A leaves the code unused until it dies (Rule 37)" aged
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "1b an aged code replaces nothing, and the old PIN is untouched"
        (saw (function ResetDenied ResetExpired -> true | _ -> false)
         && (credentialOf ucA expiredCode |> Option.bind _.Pin) = Some pinA)

    // ── UC-6 ext 2a — the code is guessed at ──
    // The count is the code's own and not the credential's (Rule 28), so guessing at a
    // code cannot lock a PIN that is still perfectly good.
    let voided =
        step "UC-6 ext 2a — a few wrong codes, and this one is void" asked
             [ for i in 1 .. wrongConfirmationCodeLimit -> act 1 (EntersResetCode(ConfirmationCode $"code-wrong%i{i}", Pin "0000")) ]

    expect "2a the code is void, and the PIN it would have replaced is untouched"
        (saw (function ResetDenied ResetVoid -> true | _ -> false)
         && (credentialOf ucA voided |> Option.bind _.Pin) = Some pinA
         && (credentialOf ucA voided |> Option.map _.AttemptCount) = Some 0)

    let afterVoid =
        step "UC-6 ext 2a — the mailed code is void too: the reset is gone, not merely wrong" voided
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "2a even the right code buys nothing now — a fresh reset means a fresh mail"
        (saw (function ResetDenied NoResetPending -> true | _ -> false)
         && (credentialOf ucA afterVoid |> Option.bind _.Pin) = Some pinA)

    let freshMail = step "UC-6 ext 2a — and A asks again" afterVoid [ act 1 AsksPinReset ]

    expect "2a a second code goes out, and it is not the first one"
        ((mailsTo mailA freshMail).Length = 2
         && (codeInMail mailA freshMail).IsSome
         && (codeInMail mailA freshMail) <> Some code)

    signs


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-7  User opens GenPRES directly
// ═══════════════════════════════════════════════════════════════════════════════

let uc7 () =
    printfn ""
    printfn "############### UC-7  User opens GenPRES directly ###############"

    // GenPRES as Clinical Decision Support, not as order management. No launch means
    // no Launch, and GenPRES cannot know who is at the keyboard.
    let anon = step "UC-7 step 1 — A opens the GenPRES address in a browser" world [ atClient 1 OpenDirectly ]

    expect "UC-7 step 1: an anonymous Session — no User, no Role, no PatientId (Rule 13)"
        (openCount anon = 1
         && (newestRecord anon |> Option.bind _.User) = None
         && (newestRecord anon |> Option.bind _.Patient) = None)

    expect "UC-7 step 1: its SessionRecord binds to no User (Concept 9)"
        ((recNo 1 anon |> Option.bind _.User) = None
         && (recNo 1 anon |> Option.bind _.Launch) = None)

    expect "UC-7 anonymous use needs no Role and no UserRegistry check"
        (never (function ResolveUser _ -> true | _ -> false))

    let prescribing =
        step "UC-7 step 2 — A prescribes: Patient Data and OrderContexts by hand" anon
             [
                 act 1 (EntersPatientData(PatientData "3y, 14kg, by hand"))
                 act 1 (Prescribes(OrderContextId "oc-x"))
             ]

    expect "UC-7 step 2: prescribing works, Patient Data included (Concepts 2, 15)"
        ((dataAt 1 prescribing).IsSome && (workingAt 1 prescribing).Length = 1)

    // Rule 9 stamps `LastSeen` on every request, as it does for any Session, but for
    // an anonymous one nothing ever reads it (Rule 14). What governs this Session is
    // the absolute limit it was opened with.
    expect "UC-7 step 2: requests are served and stamped, but it is the limit that governs (Rules 9, 14)"
        (countOf (function SessionRequest _ -> true | _ -> false) = 2
         && lastSeenOf 1 prescribing > (recNo 1 anon |> Option.map _.LastSeen)
         && (recNo 1 prescribing |> Option.bind _.ExpiresAt).IsSome)

    let noSaving = step "UC-7 step 3 — nothing can be signed" prescribing (signs 1 pinA)

    expect "UC-7 step 3: no TreatmentPlan can be opened or created (Rule 13)"
        (saw (function NoTreatmentPlanHere -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    expect "UC-7 neither the PatientRecord nor the PatientDataPlatform is ever touched"
        (never (function ReadRecord _ -> true | _ -> false)
         && never (function ReadPatientData _ -> true | _ -> false))

    expect "UC-7 step 3: the work exists only in the Client (Rule 32)"
        ((workingAt 1 noSaving).Length = 1 && noSaving.GenPres.InFlight.IsEmpty)

    let idled =
        step "UC-7 — and it need not idle out: keeping it has no consequence (Rule 13)" noSaving
             (ticks (sessionTtl + 5))

    expect "UC-7 an anonymous Session does not idle out: it has no idle clock (Rule 14)"
        (openCount idled = 1 && stateOf 1 idled = Some OpenOrGone)

    // Not for ever, though. Rule 13 says keeping it has no consequence, which is an
    // argument against an idle clock and not against an outright limit: a Session
    // nobody will ever come back to should not sit open until the Server is restarted.
    let outlived =
        stepFor 40000 "UC-7 — but it does not live for ever: the outright limit (Rule 13)" idled
             (ticks (anonymousLifetime + 5))

    expect "UC-7 past its limit the anonymous Session is ended, whatever it was doing"
        (openCount outlived = 0
         && (match stateOf 1 outlived with Some(Ended(Expired, _)) -> true | _ -> false))

    expect "UC-7 and nothing is owed by it: there is no User to tell (Rules 11, 13)"
        (noticeOf 1 outlived = Some NotOwed
         && (recNo 1 outlived |> Option.bind _.User).IsNone)

    // ── Rule 14 — anonymous opens are bounded in number, not only in lifetime ──
    // An anonymous open is an unauthenticated write: one SessionRecord per open, and
    // the lifetime says only how long each lives. Above the bound the answer is a
    // refusal that writes no record.
    let refusals = 4

    let flooded =
        let opens =
            [ 1 .. anonymousOpenLimit + refusals ]
            |> List.collect (fun i -> [ atClient (100 + i) OpenDirectly ])
        step "Rule 14 — many browsers open anonymously at once" world opens

    expect "Rule 14 the standing anonymous Sessions are capped, and the rest are refused"
        (openCount flooded = anonymousOpenLimit
         && saw (function AnonymousRefused -> true | _ -> false))

    expect "Rule 13 and a refused open writes no SessionRecord — which is what the bound is for"
        (recordCount flooded = anonymousOpenLimit)

    // Rule 46. A refusal is an event and the audit is where an attempt shows up, so it
    // is not silence. A line per refused request would be the same flood under another
    // name, so what is kept is a count per source: one integer, however hard anyone
    // leans on it.
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

    // ── UC-7 ext 1a — the browser does present a Launch ──
    // That is a launch: UC-1 from step 3. Covered by UC-1 throughout.

    // ── UC-7 ext 1b — the same Browser later launches properly ──
    // An anonymous Session binds to no User, so it is Rule 8's per-browser half that
    // ends this one. It is the User's own act, so nothing is owed for it.
    let alsoLaunched = step "UC-7 ext 1b — the same person later launches properly" idled (launchAs ucA.Login (Some pat1))

    expect "1b the anonymous Session is not untouched: it is replaced, and owes nothing (Rules 9, 10)"
        (openCount alsoLaunched = 1
         && (match stateOf 1 alsoLaunched with Some(Ended(ReplacedInBrowser, _)) -> true | _ -> false)
         && noticeOf 1 alsoLaunched = Some NotOwed
         && never (function PriorSessionNotice _ -> true | _ -> false))

    // Rules 7 and 40. The replacement and the open are one act at the Database, not
    // two requests with a gap between them: there is no `EndSessionIfOpen` on the wire
    // before the open, and no moment in which this browser holds two Sessions or none.
    expect "1b the replacement and the open are one act, not two (Rules 8, 40)"
        (saw (function OpenSessionClosingOthers(_, _, Some _) -> true | _ -> false)
         && never (function EndSessionIfOpen(_, ReplacedInBrowser) -> true | _ -> false)
         && before
                (function OpenSessionClosingOthers _ -> true | _ -> false)
                (function SessionOpened _ -> true | _ -> false))

    // The limit does not rest on the Client's word either. A Client that names no
    // Session it is replacing (an attacker's would not, and an honest one might not
    // after a reload) still ends up with one Session in its browser, because the
    // Database reads the browser off the record it holds (Rules 8, 40).
    let silentAboutTheOldOne =
        let launch = Token.mintLaunch (Some pat1) idled.Env.Now

        step "Rule 40 — a Client that names no Session to replace still gets only one" idled
             [ fromClient 1 (RedeemLaunch(launch, Some ucA.Login, None)) ]

    expect "Rule 40 the browser limit is the Database's, not the Client's word (Rules 8, 40)"
        (never (function RedeemLaunch(_, _, Some _) -> true | _ -> false)
         && (silentAboutTheOldOne.Database.Private.Sessions
             |> List.filter (fun r -> SessionRecord.isOpen r && r.Browser = Some(BrowserId 1))
             |> List.length) = 1)

    // And the work in the browser goes when the browser does: it was only ever there
    // (Rule 32).
    let browserClosed = step "UC-7 step 3 — and it is gone when the browser goes" idled [ atClient 1 CloseBrowser ]

    expect "UC-7 step 3: the cart dies with the browser (Rule 32)"
        (workingAt 1 browserClosed).IsEmpty

    idled


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-8  A Session ends out from under the User
// ═══════════════════════════════════════════════════════════════════════════════

let uc8 () =
    printfn ""
    printfn "############### UC-8  A Session ends out from under the User ###############"

    // Precondition: UC-3 step 1. A's Session for Patient 2 is open and unsigned work
    // sits on the screen, which is the only place it is (Concept 16).
    let saved =
        quiet "UC-8 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [
                   act 1 (Prescribes(OrderContextId "oc-4"))
                   act 1 (Prescribes(OrderContextId "oc-unsigned"))
                 ])

    expect "UC-8 precondition: unsigned work on screen, and nothing of it in the record"
        (planCount pat2 saved = 1 && (workingAt 1 saved).Length = 3)

    let idled =
        step "UC-8 step 1 — A is called away, and the idle clock runs out (Rules 9, 10)" saved
             (ticks (sessionTtl + 5))

    expect "UC-8 step 1: the Session ends and its record is marked ended"
        (openCount idled = 0
         && (match stateOf 1 idled with Some(Ended(Idle, _)) -> true | _ -> false))

    expect "UC-8 step 1: the ending creates the obligation — a notice is now owed (Rule 11)"
        (noticeOf 1 idled = Some Owed)

    // Step 2: the Server cannot reach the Client, which keeps showing a live-looking
    // screen (Consequence 6). Nothing was sent, and nothing could have been.
    expect "UC-8 step 2: nothing was sent to the Client when the Session ended (Consequence 6)"
        (never (function SessionEnded _ -> true | _ -> false))

    let told = step "UC-8 step 2 — A returns and acts" idled [ act 1 (Prescribes(OrderContextId "oc-later")) ]

    // Rule 11. The request is refused and this screen is told what ended, but the
    // obligation is not discharged by it. Whoever holds this SessionId need not be A:
    // in UC-5's setting it is whoever sat down at the workstation, and telling them is
    // not telling A. Delivery is a launch's business (PriorSessionNotice), where a
    // fresh MainEHR login stands behind the person reading it.
    expect "UC-8 step 2: the request is refused and this screen is told what ended (Rule 11)"
        (saw (function SessionRefused(Some Idle) -> true | _ -> false)
         && showingOf 1 told = Some "the session ended: Idle — relaunch from MainEHR")

    expect "UC-8 step 2: and the notice is still owed — a stale Client is not the User (Rule 11)"
        (noticeOf 1 told = Some Owed
         && not (wasTold 1 told)
         && not (wasAcknowledged 1 told))

    // Rule 11. Dismissing it here would spend the obligation on the word of whoever
    // holds the ended SessionId, which in UC-5's setting is whoever sat down at the
    // workstation. The old Client has no Session of its own to answer with, so the
    // notice stands until a launched Session of A's answers for it.
    let dismissedAtOldClient =
        step "UC-8 step 4 — A's old Client tries to dismiss it, and cannot (Rule 11)" told
             [ act 1 AcknowledgesNotice ]

    expect "UC-8 step 4: the ended Session's own Client cannot spend the obligation (Rule 10)"
        (not (wasAcknowledged 1 dismissedAtOldClient)
         && never (function AckSessionNotice _ -> true | _ -> false))

    let atNextLaunch =
        step "UC-8 step 4 — A launches again, and the notice is there (Rule 11)" dismissedAtOldClient
             (launchAs ucA.Login (Some pat2))

    // Rule 11. This is the *only* place the notice is ever delivered. Every refused
    // request before it left the obligation where the ending put it. The launch is what
    // discharges it, because a fresh MainEHR login stands behind the person about to
    // read it and a stale SessionId does not.
    expect "UC-8 step 4: the launch is where an unacknowledged notice comes back"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && wasTold 1 atNextLaunch
         && not (wasTold 1 dismissedAtOldClient))

    let acked = step "UC-8 step 4 — A dismisses it there" atNextLaunch [ act 2 AcknowledgesNotice ]

    expect "UC-8 step 4: acknowledged from a launched Session of A's own, and now it is spent (Rule 11)"
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

    // Step 3, the change the stateless design makes. The unsigned work was never
    // anywhere but the Client (Rule 32): the ended Session accepts nothing, but the
    // Client still holds it.
    expect "UC-8 step 3: the unsigned work is still in the Client (Rule 32)"
        ((workingAt 1 told) |> List.exists (fun o -> o.Id = OrderContextId "oc-unsigned"))

    expect "UC-8 step 3: and it never reached the record (Concept 16)"
        (planCount pat2 told = 1
         && (recordFor pat2 told).Plans
            |> List.forall (fun s ->
                s.Orders |> List.forall (fun o -> o.Id <> OrderContextId "oc-unsigned")))

    expect "UC-8 step 3: the record is where it was, and the next Session starts there (Rule 19)"
        ((recordFor pat2 told |> PatientRecord.startsFrom |> Option.map _.Id)
            = (headOf pat2 told |> Option.map _.Id))

    let relaunched =
        step "UC-8 step 4 — A relaunches. Acknowledged already, A is not told again (Rule 11)" acked
             (launchAs ucA.Login (Some pat2))

    // A launch always supersedes the Session A was in, and that ending owes a notice of
    // its own (Rules 8, 11). What must never come back is the one A acknowledged.
    expect "UC-8 step 4: the acknowledged Session is never named again (Rule 11, acknowledged once)"
        (let acknowledged = (recNo 1 relaunched).Value.Id

         lastTrace
         |> List.forall (fun e ->
             match e.Msg with
             | PriorSessionNotice priors -> priors |> List.forall (fun (_, _, sid) -> sid <> acknowledged)
             | _ -> true))

    // And the other way round: a notice that was delivered and never acknowledged is
    // shown again, because the alternative is a User who never learns of it at all.
    let unacknowledged =
        step "UC-8 step 4 — but an unacknowledged notice comes back (Rule 11, at least once)" told
             (launchAs ucA.Login (Some pat2))

    expect "UC-8 step 4: delivery is at-least-once; only the acknowledgement ends it"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && openCount unacknowledged = 1)

    // ── Rule 10 — the limit Rule 9's clock cannot put off ──
    // Rule 9 refreshes the idle clock on every request, so a Client that keeps talking
    // (a poll, a tab left computing) never idles out. `sessionMaxLifetime` is the other
    // limit: counted from the open, unaffected by traffic, and the reason a launch
    // cannot stand for the person who made it indefinitely.
    let talking =
        let sid = (sidAt 1 saved).Value
        let poll = fromClient 1 (SessionRequest(sid, None, Compute []))
        // A poll well inside the idle limit, over and over: Rule 8 keeps the Session
        // alive through every one of them, and the absolute limit ends it anyway.
        stepFor 40000 "Rule 10 — a Client that never goes quiet still reaches the outright limit" saved
             ([ 1 .. 20 ] |> List.collect (fun _ -> poll :: ticks 20))

    expect "Rule 10 the Session ends at its absolute limit, though it was never idle (Rules 8, 10)"
        (openCount talking = 0
         && (match stateOf 1 talking with Some(Ended(Expired, _)) -> true | _ -> false)
         && (recNo 1 talking |> Option.bind _.User).IsSome)

    expect "Rule 9 and the User is owed the notice, because there is one to tell (Rule 11)"
        (wasTold 1 talking || noticeOf 1 talking = Some Owed)

    // Step 3: the Client may carry the surviving cart into the next Session as fresh
    // prescribing (Concept 15). Memory to memory, and not a resumed Session.
    let carried =
        step "UC-8 step 3 — A carries the surviving work into the new Session" relaunched
             ([ act 3 (CarriesOverFrom(BrowserId 1)) ] @ signs 3 pinA)

    expect "UC-8 step 3: the unsigned OrderContext from before the idle-out lands in the next TreatmentPlan"
        (headOf pat2 carried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsigned"))

    expect "UC-8 step 3: and it is fresh prescribing — stamped by A in this Session (Rules 15, 35)"
        (headOf pat2 carried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsigned" && o.Stamp = Some ucA))

    // The work lasts exactly as long as the browser does: closed, it is gone.
    let browserGoneFirst =
        step "UC-8 step 3 — but close the browser first, and there is nothing to carry" told
             ([ atClient 1 CloseBrowser ] @ launchAs ucA.Login (Some pat2))

    let nothingCarried =
        step "UC-8 step 3 — the new Session gets only what the record held" browserGoneFirst
             ([ act 2 (CarriesOverFrom(BrowserId 1)) ] @ signs 2 pinA)

    expect "UC-8 step 3: closed is gone — the unsigned work is nowhere"
        (headOf pat2 nothingCarried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsigned")
         |> not)

    // The carry-over is within one User's own work and one Patient's, and no further.
    // Rule 33 takes both from the SessionRecord and Guarantee 1 makes the PatientId the
    // one thing no TreatmentPlan may change, so a cart cannot walk from one User to
    // another, or from one Patient to another.
    let notMine =
        step "UC-8 step 3 — B tries to carry A's surviving work into B's own Session" told
             (launchAs ucB.Login (Some pat2)
              @ [ act 2 (CarriesOverFrom(BrowserId 1)) ]
              @ signs 2 pinB)

    expect "UC-8 step 3: another User's work is not a source — nothing is carried (Rules 15, 33)"
        (headOf pat2 notMine
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsigned")
         |> not)

    let otherPatient =
        step "UC-8 step 3 — A relaunches for another Patient, and the work does not follow" told
             (launchAs ucA.Login (Some pat1)
              @ [ act 2 (CarriesOverFrom(BrowserId 1)) ]
              @ signs 2 pinA)

    expect "UC-8 step 3: work does not cross Patients, and neither record gained it (Guarantee 1)"
        (headOf pat1 otherPatient
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsigned")
         |> not
         && planCount pat2 otherPatient = planCount pat2 told)

    // ── UC-8 ext 2a — nobody swept, and the request itself ends it ──
    // Rule 41. The Session is aged rather than ticked out, so no sweep has run and no
    // Tick has reached the Server. What ends it is the arriving request, which finds a
    // record already past its time and ends it then and there instead of refreshing it
    // back to life (Rule 9).
    let aged =
        { saved with
            Database.Private.Sessions =
                saved.Database.Private.Sessions
                |> List.map (fun r -> { r with LastSeen = r.LastSeen - (sessionTtl + 1) }) }

    let endedOnArrival =
        step "UC-8 ext 2a — A comes back to a Session that is already past its time" aged
             [ act 1 (Prescribes(OrderContextId "oc-late")) ]

    expect "2a the request ends it rather than refreshing it, and says so (Rules 9, 41)"
        (never (function Tick -> true | _ -> false)
         && saw (function SessionRefused(Some Idle) -> true | _ -> false)
         && (match stateOf 1 endedOnArrival with Some(Ended(Idle, _)) -> true | _ -> false)
         && openCount endedOnArrival = 0)

    expect "2a and the notice it created is owed to the User, not to this screen (Rule 11)"
        (noticeOf 1 endedOnArrival = Some Owed && not (wasTold 1 endedOnArrival))

    // Rules 9 and 41, the other end. The idle clock forgives a Client that keeps
    // talking, and one that never stops would never idle out at all, so the outright
    // limit has to be asked on arrival too and not only by a sweep a busy Session
    // outruns. Here the record is aged at the open rather than at the last request, so
    // the idle clock is untouched and only the limit has passed.
    let outlived =
        { saved with
            Database.Private.Sessions =
                saved.Database.Private.Sessions
                |> List.map (fun r ->
                    { r with
                        OpenedAt = r.OpenedAt - (sessionMaxLifetime + 1)
                        ExpiresAt = r.ExpiresAt |> Option.map (fun at -> at - (sessionMaxLifetime + 1)) }) }

    let stoppedOnArrival =
        step "UC-8 ext 2a — and a Session that never went quiet still reaches its limit" outlived
             [ act 1 (Prescribes(OrderContextId "oc-still-talking")) ]

    expect "2a the outright limit is asked on arrival too, not only by the sweep (Rules 10, 41)"
        (never (function Tick -> true | _ -> false)
         && saw (function SessionRefused(Some Expired) -> true | _ -> false)
         && (match stateOf 1 stoppedOnArrival with Some(Ended(Expired, _)) -> true | _ -> false)
         && openCount stoppedOnArrival = 0
         // and it was not the idle clock: the Session had been talking all along
         && not (outlived.Database.Private.Sessions
                 |> List.exists (SessionRecord.hasIdledOut outlived.Env.Now)))

    // ── UC-8 ext 1a — the Server restarts instead ──
    // Nothing ends. The Session's identity and standing are in its SessionRecord, its
    // work is in the Client, and the Server held neither (Rules 10, 32).
    let restarted =
        step "UC-8 ext 1a — the Server restarts instead" saved
             [ envt GenPresServer (Stop GenPresServer); tick; envt GenPresServer (Start GenPresServer) ]

    expect "1a nothing ends: the Session is still open (Rules 10, 32)"
        (openCount restarted = 1 && stateOf 1 restarted = Some OpenOrGone)

    expect "1a the Server settled nothing at the start — there was nothing to settle"
        (never (function ReadSessionRecords ForSweep -> true | _ -> false)
         && never (function EndSessionIfOpen _ -> true | _ -> false))

    expect "1a the Client still holds its cart (Rule 32)"
        ((workingAt 1 restarted).Length = 3)

    let seenBefore = lastSeenOf 1 restarted

    let afterRestart =
        step "1a — and the next request continues the Session (Rules 9, 10)" restarted
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

    // ── UC-8 ext 1c — A opens another Session at another workstation ──
    let elsewhere = step "UC-8 ext 1c — A opens another Session instead" saved (launchAs ucA.Login (Some pat2))

    expect "1c the launch itself ends the old Session, and the notice comes with it (Rules 8, 10, 11)"
        (openCount elsewhere = 1
         && (match stateOf 1 elsewhere with Some(Ended(Superseded, _)) -> true | _ -> false)
         && saw (function PriorSessionNotice _ -> true | _ -> false))

    let ackedElsewhere =
        step "UC-8 ext 1c — A dismisses the notice at the new workstation" elsewhere
             [ act 2 AcknowledgesNotice ]

    let oldTab =
        step "UC-8 ext 1c — the old Client's next request is refused, and not told again (Rule 11)" ackedElsewhere
             [ act 1 (Prescribes(OrderContextId "oc-z")) ]

    expect "1c refused, and the acknowledged notice is not repeated"
        (saw (function SessionRefused _ -> true | _ -> false)
         && never (function SessionEnded _ -> true | _ -> false))

    ignore oldTab
    told

// ═══════════════════════════════════════════════════════════════════════════════
//  UC-9  A Reader consults a Patient
// ═══════════════════════════════════════════════════════════════════════════════

let uc9 () =
    printfn ""
    printfn "############### UC-9  A Reader consults a Patient ###############"

    // Precondition: Patient 2 has a head, A holds an open Prescriber Session for it,
    // and C launches as a Reader (UC-1 ext 5c).
    let aWorking = quiet "UC-9 precondition — A opens a Session for Patient 2" world (launchAs ucA.Login (Some pat2))

    let reading = step "UC-9 step 1 — C launches for Patient 2" aWorking (launchAs ucC.Login (Some pat2))

    expect "UC-9 step 1: the Session opens from the most recent TreatmentPlan (Rules 18, 19)"
        (openedAt 2 reading = Some p2Signed.Id)

    expect "UC-9 step 1: C reads the plan that counts clinically (Rule 17)"
        (workingAt 2 reading = p2Signed.Orders)

    // Step 2. A signs meanwhile, and C hears of it at C's own next action, whatever
    // that action is (Rules 21, 22). Consequence 6: there is no other moment it could
    // arrive.
    let aSignedMeanwhile =
        quiet "UC-9 step 2 setup — A signs a newer plan" reading
              [ act 1 (Prescribes(OrderContextId "oc-4")); yield! signs 1 pinA ]

    let cTold =
        step "UC-9 step 2 — C acts, and is told a newer plan exists" aSignedMeanwhile
             [ act 2 (Prescribes(OrderContextId "oc-what-if")) ]

    expect "UC-9 step 2: C is told whose plan it is and when it was signed (Rule 21)"
        (saw (function NewerPlanNotice(uc, _) -> uc = ucA | _ -> false))

    let cOpenedIt =
        step "UC-9 step 2 — and C opens it (Rule 18)" cTold
             [ act 2 (OpensTreatmentPlan (headOf pat2 cTold).Value.Id) ]

    expect "UC-9 step 2: the whole history is open to a Reader too (Rule 18)"
        (openedAt 2 cOpenedIt = (headOf pat2 cTold |> Option.map _.Id))

    // Step 3. A Reader may prescribe like anyone (Concept 15); what they may never do
    // is create a TreatmentPlan (Roles, Rule 26).
    let exploring =
        step "UC-9 step 3 — C prescribes to explore, and signing is not offered" cOpenedIt
             ([ act 2 (Prescribes(OrderContextId "oc-what-if-2")) ] @ signs 2 (Pin "0000"))

    expect "UC-9 step 3: prescribing works (Concept 15), but signing is not offered"
        (saw (function Computed _ -> true | _ -> false)
         && saw (function NotPermitted -> true | _ -> false)
         && planCount pat2 exploring = planCount pat2 cOpenedIt)

    expect "UC-9 step 3: no PIN is ever asked for, and none is ever read (Rule 26)"
        (never (function PinRequired _ -> true | _ -> false)
         && never (function ReadCredential _ -> true | _ -> false))

    // Rule 46 says every refused request, not only the ones the Database decides. C's
    // signature is turned away in the Server and never reaches the Database, so this is
    // the only place it can be recorded.
    expect "Rule 46 a request refused in the Server is recorded too (Rules 26, 46)"
        (exploring |> audited "request refused"
         && saw (function NotPermitted -> true | _ -> false))

    exploring


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-10  User closes GenPRES
// ═══════════════════════════════════════════════════════════════════════════════

let uc10 () =
    printfn ""
    printfn "############### UC-10  User closes GenPRES ###############"

    // Precondition: UC-3 completed. A has an open Session for Patient 2 and its work
    // is signed, so nothing unsigned remains.
    let signedUp =
        quiet "UC-10 precondition" world
              (launchAs ucA.Login (Some pat2)
               @ [ act 1 (Prescribes(OrderContextId "oc-4")); yield! signs 1 pinA ])

    let closed = step "UC-10 step 1 — A closes the Session in the Client" signedUp [ act 1 ClosesSession ]

    expect "UC-10 step 1: the Session ends, marked closed by the User (Rule 10, Concept 9)"
        (openCount closed = 0
         && (match stateOf 1 closed with Some(Ended(ClosedByUser, _)) -> true | _ -> false))

    expect "UC-10 step 2: and no notice is ever owed — not owed and then skipped (Rule 11)"
        (noticeOf 1 closed = Some NotOwed)

    let nextLaunch = step "UC-10 step 2 — the next launch starts clean" closed (launchAs ucA.Login (Some pat2))

    expect "UC-10 step 2: no notice follows — Rule 11 speaks only of endings other than by the User"
        (never (function PriorSessionNotice _ -> true | _ -> false)
         && noticeOf 1 nextLaunch = Some NotOwed)

    // ── UC-10 ext 1a — unsigned work remains at the close ──
    let withUnsigned =
        quiet "UC-10 ext 1a setup" signedUp [ act 1 (Prescribes(OrderContextId "oc-dangling")) ]

    let closedAnyway =
        step "UC-10 ext 1a — A closes with unsigned work: closed is closed" withUnsigned [ act 1 ClosesSession ]

    expect "1a it existed only in the Client and is gone (Rule 32); what was signed stands (Concept 12)"
        (openCount closedAnyway = 0
         && (workingAt 1 closedAnyway).IsEmpty
         && planCount pat2 closedAnyway = 2
         && (headOf pat2 closedAnyway
             |> Option.map _.Orders
             |> Option.defaultValue []
             |> List.exists (fun o -> o.Id = OrderContextId "oc-dangling")
             |> not))

    // ── UC-10 ext 1b — A closes the browser instead ──
    let browserGone = step "UC-10 ext 1b — A closes the browser instead" signedUp [ atClient 1 CloseBrowser ]

    expect "1b nothing reaches the Server, so no close can be inferred (Rule 10)"
        (openCount browserGone = 1
         && stateOf 1 browserGone = Some OpenOrGone
         && never (function SessionRequest _ -> true | _ -> false))

    let idledOut =
        step "UC-10 ext 1b — the Session idles out instead" browserGone (ticks (sessionTtl + 5))

    expect "1b it idles out, and A is told at the next opportunity (Rule 11; UC-8)"
        (match stateOf 1 idledOut with Some(Ended(Idle, _)) -> true | _ -> false)

    let harmlessNotice = step "UC-10 ext 1b — a harmless notice, the price of the indistinguishability" idledOut (launchAs ucA.Login (Some pat2))

    expect "1b the notice arrives at the next launch"
        (saw (function PriorSessionNotice _ -> true | _ -> false))

    ignore harmlessNotice
    closed


// ═══════════════════════════════════════════════════════════════════════════════
//  UC-11  A User's authority is withdrawn
// ═══════════════════════════════════════════════════════════════════════════════

let uc11 () =
    printfn ""
    printfn "############### UC-11  A User's authority is withdrawn ###############"

    // Precondition: A has an open Session for Patient 2 with unsigned work on screen.
    // Then the UserRegistry stops returning a Role for A's login.
    let aWorking =
        quiet "UC-11 precondition" world
              (launchAs ucA.Login (Some pat2) @ [ act 1 (Prescribes(OrderContextId "oc-4")) ])

    let withdrawn = { aWorking with Registry.Users = aWorking.Registry.Users |> Map.remove ucA.Login }

    let refused = step "UC-11 step 1 — A launches; the registry returns no Role" withdrawn (launchAs ucA.Login (Some pat2))

    // A's Session from the precondition is still open and stays open, which is ext 1a
    // below. What the failed launch must not do is open another one.
    expect "UC-11 step 1: no Role, so the launch opens no Session (Rules 5, 7)"
        (saw (function UserUnresolved(_, NoRole) -> true | _ -> false)
         && saw (function NotAuthorised -> true | _ -> false)
         && never (function SessionOpened _ -> true | _ -> false))

    let cds = step "UC-11 step 1 — A accepts the anonymous open: CDS is all that remains" refused [ atClient 2 AcceptAnonymousOffer ]

    expect "UC-11 step 1: hand-entered patients, no records, nothing signed (UC-7; Rule 14)"
        ((newestRecord cds |> Option.bind _.User) = None
         && (newestRecord cds |> Option.bind _.Patient) = None)

    let againRefused = step "UC-11 step 1 — every later launch ends the same way (Rule 5)" cds (launchAs ucA.Login (Some pat2))

    expect "UC-11 step 1: the Role is taken from the registry at each launch, so the withdrawal stands"
        (saw (function NotAuthorised -> true | _ -> false))

    expect "UC-11 step 2: A's UserCredential remains, but is inert (Concepts 7, 14)"
        ((credentialOf ucA againRefused).IsSome
         && (credentialOf ucA againRefused |> Option.bind _.Pin).IsSome)

    // ── step 3 — the record holds exactly what A signed, and nothing half-done ──
    // Guarantee 2: unsigned work never left A's browser (Concept 16), so there is
    // nothing of A's left pending anywhere for anyone to find or to tidy away.
    let bWorksPast =
        step "UC-11 step 3 — B's next Session starts from the head, untouched by any of this (Rule 19)" againRefused
             (launchAs ucB.Login (Some pat2))

    expect "UC-11 step 3: A's unsigned work is nowhere in the record (Concept 16, Guarantee 2)"
        (openedAt 4 bWorksPast = Some p2Signed.Id
         && planCount pat2 bWorksPast = 1)

    let superseded =
        step "UC-11 step 3 — and B signs over it as normal" bWorksPast
             [ act 4 (Prescribes(OrderContextId "oc-e")); yield! signs 4 pinB ]

    expect "UC-11 step 3: B's TreatmentPlan now counts (Rule 17)"
        ((headOf pat2 superseded |> Option.map _.By) = Some ucB)

    // ── UC-11 ext 1a — the withdrawal happens while A's Session is open ──
    // The Session keeps the Role its launch established, off the SessionRecord (Rules
    // 5, 33). A signature is the one act that does not accept that (Rule 38), so a
    // withdrawal blocks signing at once, while reading and prescribing ride out the
    // Session.
    let stillWorks =
        step "UC-11 ext 1a — the withdrawal lands while A's Session is open: A prescribes" withdrawn
             [ act 1 (Prescribes(OrderContextId "oc-f")) ]

    expect "1a the open Session keeps the Role its launch established, and prescribing works (Concept 9, Rule 33)"
        (saw (function Computed _ -> true | _ -> false)
         && never (function ResolveUser _ -> true | _ -> false))

    let cannotSign =
        step "UC-11 ext 1a — but the signature asks the registry again (Rule 38)" stillWorks [ yield! signs 1 pinA ]

    expect "1a the Role is gone, so the signature is refused — and before the PIN is asked for"
        (saw (function ResolveUser(ForRequest _, _) -> true | _ -> false)
         && saw (function UserUnresolved(_, NoRole) -> true | _ -> false)
         && saw (function NotPermitted -> true | _ -> false)
         && never (function ReadCredential _ -> true | _ -> false)
         && (recordFor pat2 cannotSign |> PatientRecord.latest |> Option.map _.Id) = Some p2Signed.Id)

    expect "1a a signature nobody is entitled to costs no PIN attempt (Rules 28, 38)"
        ((credentialOf ucA cannotSign |> Option.map _.AttemptCount) = Some 0)

    // A registry that is merely down is not a withdrawal. For `roleGrace` after the
    // launch its Role stands instead, and the audit says so. The trade is deliberate:
    // within that window, a withdrawal the registry cannot report does not land.
    let registryDown =
        step "UC-11 ext 1a — the registry cannot be asked at all, and the launch is recent"
             { stillWorks with Registry.Up = false }
             [ yield! signs 1 pinA ]

    expect "1a within the grace the signature lands on the Role the launch took, and it is audited (Rule 38)"
        (saw (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && never (function SigningUnavailable -> true | _ -> false)
         && registryDown |> audited "under grace"
         && openCount registryDown = openCount stillWorks)

    // Past the window it fails closed, exactly as Rule 38 said before.
    let staleRole =
        let aged =
            { stillWorks with
                Registry.Up = false
                Database.Private.Sessions =
                    stillWorks.Database.Private.Sessions
                    |> List.map (fun r -> { r with OpenedAt = r.OpenedAt - (roleGrace + 1) }) }
        step "UC-11 ext 1a — and past the grace, signing fails closed" aged [ yield! signs 1 pinA ]

    expect "1a past the grace no answer means no signature, and the Session is untouched (Rule 38)"
        (saw (function SigningUnavailable -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && openCount staleRole = openCount stillWorks)

    superseded


// ═══════════════════════════════════════════════════════════════════════════════
//  Rules 33 to 36 — the stateless design under attack
// ═══════════════════════════════════════════════════════════════════════════════
//
// The cart is the Client's (Rule 32), so everything the Server would have remembered
// arrives with the request, and counts for nothing unless the Server vouched for it.
// Here: a Client that edits a token, invents one, lies about the Patient or forges a
// stamp, and a Database arbitrating two Servers racing for the same head.

let tokensAndArbitration () =
    printfn ""
    printfn "############### Rules 33-36  The stateless design under attack ###############"

    // ── Rule 34: an opened-with token the Client edited ──
    let both =
        quiet "tokens precondition" world
              (launchAs ucA.Login (Some pat2) @ launchAs ucB.Login (Some pat2))

    let bWon =
        step "Rule 34 setup — B signs, so A's opened-with token is now stale" both
             [ act 2 (Prescribes(OrderContextId "oc-b")); yield! signs 2 pinB ]

    let newestPlan = (recordFor pat2 bWon |> PatientRecord.latest |> Option.map _.Id).Value

    let honestStale =
        step "Rule 34 — A's honest but stale token: blocked, as before (Rule 20)" bWon (signs 1 pinA)

    expect "Rule 34 an honest stale token is believed, and Rule 20 does the refusing"
        (saw (function SubmissionBlocked _ -> true | _ -> false)
         && never (function SubmissionRefused _ -> true | _ -> false)
         && planCount pat2 honestStale = planCount pat2 bWon)

    // Now A edits the token to name the newest TreatmentPlan, which would lift the
    // Rule 20 block, and guesses at the mac.
    let forged =
        let sid = (sidAt 1 bWon).Value
        let tok =
            {
                Claim =
                    {
                        Purpose = TokenPurpose.Opened
                        Sid = sid
                        Patient = Some pat2
                        Names = [ let (TreatmentPlanId i) = newestPlan in i ]
                        Nonce = "guessed"
                        IssuedAt = 0
                        ExpiresAt = 9_999
                    }
                Mac = "mac|guessed"
            }
        step "Rule 34 — A edits the token to name the newest TreatmentPlan" bWon
             [ fromClient 1 (SessionRequest(sid, None, handCreate (workOf 1 bWon) tok None)) ]

    expect "Rule 34 the token does not verify, so the Submission is refused — not merely blocked"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false)
         && planCount pat2 forged = planCount pat2 bWon)

    // ── Concept 17: a genuine token, offered for the wrong purpose ──
    let bOpen = quiet "Concept 17 precondition" world (launchAs ucB.Login (Some pat3))

    let challenged =
        quiet "Concept 17 setup — B asks to sign, and is given a challenge" bOpen
              [ act 1 (Prescribes(OrderContextId "oc-b1")); act 1 Signs ]

    let _ =
        let sid = (sidAt 1 challenged).Value
        let st = (clientOf 1 challenged).Value
        step "Concept 17 — B offers its genuine SigningChallenge as the opened-with token" challenged
             [
                 fromClient 1
                     (SessionRequest(sid, None, handCreate st.Work st.Modal.Value (Some pinB)))
             ]

    expect "Concept 17 a token minted for another purpose fails by key, not by luck"
        (saw (function SubmissionRefused why -> why.Contains "does not verify" | _ -> false)
         && never (function TreatmentPlanSubmitted _ -> true | _ -> false))

    // ── Rules 21, 22: the notice informs and gates nothing ──
    // A signs while B holds an open Session over the same Patient. B is told at B's
    // next request, whatever that request is, and is not stopped by being told.
    let aSignedUnderB =
        quiet "Rule 21 setup — A signs while B is open on the same Patient" bOpen
              (launchAs ucA.Login (Some pat3)
               @ [ act 2 (Prescribes(OrderContextId "oc-a2")); yield! signs 2 pinA ])

    let bToldOnAnyRequest =
        step "Rule 21 — B computes, and the response carries the notice" aSignedUnderB
             [ act 1 (Prescribes(OrderContextId "oc-b2")) ]

    expect "Rule 21 the notice names whose plan it is and when it was signed"
        (saw (function NewerPlanNotice(uc, at) -> uc = ucA && at > 0 | _ -> false))

    expect "Rule 22 and it gates nothing: the request it rode on was answered as normal"
        (saw (function Computed _ -> true | _ -> false)
         && never (function SubmissionBlocked _ -> true | _ -> false))

    // There is no token to return and no acknowledgement to make, so the notice comes
    // again at the next request. Rule 20 is the only thing that ever refuses.
    let bToldAgain =
        step "Rule 22 — B acts again, and is told again: nothing was consumed" bToldOnAnyRequest
             [ act 1 (Prescribes(OrderContextId "oc-b3")) ]

    expect "Rule 22 the notice is not a token: it is repeated, not spent"
        (saw (function NewerPlanNotice _ -> true | _ -> false))

    let bBlockedAtLast =
        step "Rule 20 — and only the Submission is actually refused" bToldAgain (signs 1 pinB)

    expect "Rule 20 the guard is the Submission, not the notice"
        (saw (function SubmissionBlocked uc -> uc = ucA | _ -> false)
         && planCount pat3 bBlockedAtLast = planCount pat3 bToldAgain)

    // ── Rule 33 / Guarantee 1: the payload names another Patient ──
    let aOnPat2 = quiet "Rule 33 precondition" world (launchAs ucA.Login (Some pat2))

    let wrongPatient =
        let sid = (sidAt 1 aOnPat2).Value
        let smuggled =
            [ { Id = OrderContextId "oc-smuggled"; Patient = Some pat3; Content = "elsewhere"; Stamp = None } ]
        let opened = (clientOf 1 aOnPat2).Value.Opened.Value
        step "Rule 33 — the payload names a Patient the SessionRecord does not" aOnPat2
             [ fromClient 1 (SessionRequest(sid, None, handCreate { WorkPlan.empty with Orders = smuggled } opened None)) ]

    expect "Rule 33 the Patient comes from the SessionRecord, and a payload that disagrees is refused"
        (saw (function SubmissionRefused _ -> true | _ -> false)
         && planCount pat2 wrongPatient = planCount pat2 aOnPat2
         && planCount pat3 wrongPatient = planCount pat3 aOnPat2)

    // ── Rule 35: the payload arrives pre-stamped with another User ──
    // The challenge is over ids and contents (Rule 43) and says nothing about stamps,
    // so an honestly obtained challenge still answers for the forged cart. That is what
    // makes this a test of Rule 35 rather than of Rule 43.
    let readyStamped =
        quiet "Rule 35 setup — A asks to sign an honest cart" aOnPat2
              [ act 1 (Prescribes(OrderContextId "oc-new")); act 1 Signs ]

    let preStamped =
        let sid = (sidAt 1 readyStamped).Value
        let st = (clientOf 1 readyStamped).Value
        let claimed = st.Work.Orders |> List.map (fun o -> { o with Stamp = Some ucB })
        step "Rule 35 — the cart arrives stamped with B, in A's Session" readyStamped
             [
                 fromClient 1
                     (SessionRequest(
                         sid,
                         None,
                         SubmitTreatmentPlan
                             {
                                 Work = { st.Work with Orders = claimed }
                                 Opened = st.Opened.Value
                                 Challenge = st.Modal
                                 DataOk = st.DataOk
                                 Pin = Some pinA
                                 Key = IdemKey "stamp-1"
                             }))
             ]

    let stamps = headOf pat2 preStamped |> Option.map _.Orders |> Option.defaultValue []

    expect "Rule 35 the forged stamps are nowhere: the Server recomputed them against the base"
        (stamps |> List.forall (fun o -> o.Stamp <> Some ucB))

    expect "Rule 35 unchanged content keeps the base's stamp; the new one gets the Session's User"
        (stamps |> List.exists (fun o -> o.Id = OrderContextId "oc-1" && o.Stamp = Some ucA)
         && stamps |> List.exists (fun o -> o.Id = OrderContextId "oc-new" && o.Stamp = Some ucA))

    // ── Rules 36 and 42: two signatures in flight at once ──
    // More than one Server may run, and these rules are what make that safe.
    // Interleaving the cascades leg by leg is the only way to put two in flight at
    // once: the same messages in a different order, which is what Rules 36 and 42 are
    // there to survive.
    let bothChallenged =
        quiet "Rules 36, 42 precondition — both ask to sign over the same base" both
              [
                  act 1 (Prescribes(OrderContextId "oc-r1"))
                  act 2 (Prescribes(OrderContextId "oc-r2"))
                  act 1 Signs
                  act 2 Signs
              ]

    let raced =
        racing "Rules 36, 42 — two Sessions on one Patient, two signatures in flight at once" bothChallenged
               [ act 1 (ConfirmsSign pinA); act 2 (ConfirmsSign pinB) ]

    expect "Rule 42 both signatures reached the Database as whole acts"
        (countOf (function CommitTreatmentPlan _ -> true | _ -> false) = 2)

    expect "Rule 36 exactly one landed; the other was refused, and the record moved once"
        (countOf (function TreatmentPlanCommitted _ -> true | _ -> false) = 1
         && countOf (function CommitRefused _ -> true | _ -> false) = 1
         && planCount pat2 raced = planCount pat2 bothChallenged + 1)

    // Rule 20 is what refuses the loser, and it names whose work stands in the way
    // rather than which TreatmentPlan it is.
    expect "Rule 20 the loser is told whose work landed first, and nothing more"
        (countOf (function SubmissionBlocked uc -> uc = ucA || uc = ucB | _ -> false) = 1)

    // ── Rule 40: an Ended record can never come back open ──
    // The interleaving that would do it: something reads the record while the Session
    // is open and writes back what it read after the Session has ended. Under Rule 40
    // there is no such write. There are only named changes, and the Database decides
    // whether the record is still in a state that allows them.
    let closedSession =
        quiet "Rule 40 precondition" world (launchAs ucA.Login (Some pat2) @ [ act 1 ClosesSession ])

    let replayed =
        let stale = (recNo 1 closedSession).Value
        step "Rule 40 — a stale copy of the record is replayed at the Database" closedSession
             [
                 {
                     From = GenPresServer
                     To = GenPresDatabase
                     Msg =
                         OpenSessionClosingOthers(
                             ForSweep, { stale with State = OpenOrGone; Notice = NotOwed }, None)
                 }
                 { From = GenPresServer; To = GenPresDatabase; Msg = TouchIfOpen stale.Id }
             ]

    expect "Rule 40 the Session stays ended, and its idle clock is not refreshed either"
        ((match stateOf 1 replayed with Some(Ended(ClosedByUser, _)) -> true | _ -> false)
         && openCount replayed = 0
         && recordCount replayed = recordCount closedSession
         && lastSeenOf 1 replayed = lastSeenOf 1 closedSession)

    // ── Rules 11, 42: a Submission that arrives open and commits ended ──
    // The window Rule 42 exists to close. The arrival check (Rule 41) found the
    // Session open; by the time the commit re-established it, the User had closed it in
    // the same browser. Interleaving is the only way to sit inside that window.
    //
    // Rule 11 is what is watched here. The commit refuses and the screen is told what
    // ended, but nothing is discharged: whoever holds a SessionId that has just stopped
    // working need not be the User, so the notice waits for a launch. A signature has
    // the widest window, because Rule 38 puts a registry leg between the arrival check
    // and the commit, and that is where the close lands.
    let closedUnderneath =
        let challenged =
            quiet "Rules 11, 42 precondition" world
                  (launchAs ucA.Login (Some pat2)
                   @ [ act 1 (Prescribes(OrderContextId "oc-inflight")); act 1 Signs ])

        racing "Rules 11, 42 — the Session closes while a signature is in flight" challenged
               [ act 1 (ConfirmsSign pinA); act 1 ClosesSession ]

    expect "Rule 42 the Submission is refused at the commit, because the Session ended under it"
        (saw (function CommitRefused(_, SessionNotOpen _) -> true | _ -> false)
         && planCount pat2 closedUnderneath = planCount pat2 world)

    expect "Rule 11 the screen is told what ended, and the ending discharges nothing"
        (saw (function SessionRefused _ -> true | _ -> false)
         && never (function MarkDelivered _ -> true | _ -> false))


// ═══════════════════════════════════════════════════════════════════════════════
//  The adversarial review, answered
// ═══════════════════════════════════════════════════════════════════════════════
//
// Eighteen tests an adversarial review wanted demonstrated, in its order, each with
// the scenario that shows it, and where one cannot be shown, why not. Three are
// answered by the design being different now: 1 in UC-2, 3 in UC-1 ext 8b, 13 in UC-8.

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

        // As in UC-1 ext 3b: the thief must have the same Patient active themselves,
        // or Rule 6 opens nothing at all and there is no Session to inspect.
        step "2 — a browser that proved somebody else presents A's Launch"
             { parked with Registry.Active = parked.Registry.Active |> Map.add ucB.Login pat1 }
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
                         None,
                         RequestSignChallenge(workOf 1 signing, (clientOf 1 signing).Value.Opened.Value, None)))
             ]

    let challenge = (challengeIssued ()).Value

    let commitAfterWithdrawal (h: Hospital) key =
        SessionRequest(
            (sidAt 1 h).Value,
            None,
            SubmitTreatmentPlan
                {
                    Work = workOf 1 h
                    Opened = (clientOf 1 h).Value.Opened.Value
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
    // One honest Client signs once at a time, which is what the modal is for
    // (Rule 43), so the two attempts are put on the wire by hand. The count is read,
    // advanced and written inside the one act (Rule 42), so the two cannot both read
    // the same starting value and write the same answer back.
    let twoChallenges =
        let sid = (sidAt 1 signing).Value
        let ask =
            SessionRequest(
                sid,
                None,
                RequestSignChallenge(workOf 1 signing, (clientOf 1 signing).Value.Opened.Value, None))

        step "8 — two challenges are issued" signing [ fromClient 1 ask; fromClient 1 ask ]

    let twoWrong =
        let sid = (sidAt 1 twoChallenges).Value

        let attempt (t: SigningChallenge) key =
            SessionRequest(
                sid,
                None,
                SubmitTreatmentPlan
                    {
                        Work = workOf 1 twoChallenges
                        Opened = (clientOf 1 twoChallenges).Value.Opened.Value
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

    expect "8 each wrong entry counts exactly once (Rules 28, 42)"
        ((credentialOf ucA twoWrong |> Option.map _.AttemptCount) = Some 2)

    // ── 9, 13. Covered where they belong ──
    // 9 (a credential at its limit cannot be retried through relaunches) is UC-3 ext
    // 3b; 13 (cross-user and cross-patient carry-over) is UC-8 step 5. Both refuse.

    // ── 10, 11. An old baseline token cannot branch, and a token works once ──
    let beforeSaving = quiet "10 setup" world (launchAs ucA.Login (Some pat2))
    let staleOpened = (clientOf 1 beforeSaving).Value.Opened.Value

    let afterSaving =
        quiet "10 setup — A signs, so the baseline moves" beforeSaving
              ([ act 1 (Prescribes(OrderContextId "oc-10")) ] @ signs 1 pinA)

    let replayedToken =
        step "10 — the token the Session opened with is offered again, after the Submission it was spent on" afterSaving
             [ fromClient 1 (SessionRequest((sidAt 1 afterSaving).Value, None, handCreate (workOf 1 afterSaving) staleOpened None)) ]

    expect "10 a spent token is worth no more than one the Client made up (Concept 17, Rule 34)"
        (saw (function SubmissionRefused why -> why.Contains "spent" | _ -> false)
         && planCount pat2 replayedToken = planCount pat2 afterSaving)

    let agedToken =
        let sid = (sidAt 1 afterSaving).Value
        let old =
            Token.mintOpened (afterSaving.Env.Now - tokenTtl - 1) sid (Some pat2) (headOf pat2 afterSaving |> Option.map _.Id)
        step "10 — and a genuine token past its lifetime is refused too" afterSaving
             [ fromClient 1 (SessionRequest(sid, None, handCreate (workOf 1 afterSaving) old None)) ]

    expect "10 an aged token is refused, however genuine its mac (Concept 17)"
        (saw (function SubmissionRefused why -> why.Contains "expired" | _ -> false)
         && planCount pat2 agedToken = planCount pat2 afterSaving)

    // ── 14. One OrderContext, named twice ──
    let twiceNamed =
        let sid = (sidAt 1 afterSaving).Value
        let one = { Id = OrderContextId "oc-dup"; Patient = Some pat2; Content = "first"; Stamp = None }
        let work = { workOf 1 afterSaving with Orders = [ one; { one with Content = "second" } ] }
        step "14 — a WorkPlan that names one OrderContext twice" afterSaving
             [ fromClient 1 (SessionRequest(sid, None, handCreate work (clientOf 1 afterSaving).Value.Opened.Value None)) ]

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
                         None,
                         handCreate (workOf 1 afterSaving) (clientOf 1 afterSaving).Value.Opened.Value None))
             ]

    expect "17 nothing landed and nothing half-landed: the Server holds no intent to lose (Rule 32)"
        (planCount pat2 lostToADownServer = planCount pat2 afterSaving
         && lostToADownServer.GenPres.InFlight.IsEmpty)

    let retriedAfterwards =
        // The Server comes back and A asks for a challenge the honest way; what is
        // under test is the retry, not the forging of a signature.
        let backUp =
            quiet "17 — the Server comes back, and A asks to sign" lostToADownServer
                  [ envt GenPresServer (Start GenPresServer); act 1 Signs ]

        let again =
            SessionRequest(
                (sidAt 1 lostToADownServer).Value,
                None,
                SubmitTreatmentPlan
                    {
                        Work = workOf 1 backUp
                        Opened = (clientOf 1 backUp).Value.Opened.Value
                        Challenge = (clientOf 1 backUp).Value.Modal
                        DataOk = (clientOf 1 backUp).Value.DataOk
                        Pin = Some pinA
                        Key = IdemKey "adv-17"
                    })

        step "17 — and the same act, retried when it comes back, lands once" backUp
             [ fromClient 1 again; fromClient 1 again ]

    expect "17 the retry lands, and the retry of the retry does not (Rule 45)"
        (planCount pat2 retriedAfterwards = planCount pat2 lostToADownServer + 1
         && countOf (function TreatmentPlanSubmitted _ -> true | _ -> false) = 2)

    // ── 18. A restart collides no identifier and loses nothing acknowledged ──
    let restarted =
        step "18 — the Server is restarted, and A launches again" retriedAfterwards
             (envt GenPresServer (Stop GenPresServer)
              :: envt GenPresServer (Start GenPresServer)
              :: launchAs ucA.Login (Some pat2))

    expect "18 the new SessionId is one that has never been used before (Rule 32)"
        (restarted.Database.Private.Sessions
         |> List.map _.Id
         |> fun ids -> ids.Length = (ids |> List.distinct |> List.length))

    expect "18 and everything acknowledged before the restart is still there (Concept 12)"
        (planCount pat2 restarted = planCount pat2 retriedAfterwards
         && (recordFor pat2 restarted).Plans
            |> List.forall (fun x -> (recordFor pat2 retriedAfterwards).Plans |> List.contains x))

    // ── 4, 5, 12, 15, 16. Covered where they belong ──
    // 4 is the Rule 40 replay above; 5 is UC-8 ext 2a; 12 is UC-3 ext 3d; 15 is Rule
    // 44 in UC-3; 16 is the store check under the Guarantees.

    // ── what the Launch settles, and what settles it ──
    // Rule 6 closed the hole this used to record as accepted risk. The Launch's
    // Patient is still the LaunchScript's word (Concept 3), but the word is now
    // checked: the registry must name the same Patient as the User's active one, or
    // nothing opens. Guarantee 5 rests on this.
    let unverifiedPatient =
        step "the Launch's Patient is the script's word, and the registry checks it (Rule 6)" world
             (launchAs ucA.Login (Some pat3))

    expect "a Launch whose Patient the registry confirms opens a Session for exactly that Patient"
        (openCount unverifiedPatient = 1
         && (newestRecord unverifiedPatient |> Option.bind _.Patient) = Some pat3
         && saw (function OpenUrl l -> l.Patient = Some pat3 | _ -> false)
         && saw (function UserResolved(ForLaunch _, _, _, p) -> p = Some pat3 | _ -> false))

    // A Patient the registry does not confirm gains nothing either, however well
    // sealed the Launch is. This is the check UC-1 ext 5b makes, stated here as the
    // answer to the adversarial question.
    let forgedPatient =
        step "a Launch naming a Patient the User does not have active opens nothing (Rules 6, 7)"
             { world with
                 Workstation.ActivePatient = Some pat3
                 Registry.Active = world.Registry.Active |> Map.add ucA.Login pat1 }
             [ atWorkstation (LogIn ucA.Login); triggerLaunch ]

    expect "the key seals the Launch, but the registry decides which Patient it opens"
        (openCount forgedPatient = 0
         && saw (function LaunchRefused _ -> true | _ -> false)
         && forgedPatient |> audited "PatientNotActive")


// ═══════════════════════════════════════════════════════════════════════════════
//  Consequences — derived from the edges, checked over every scenario
// ═══════════════════════════════════════════════════════════════════════════════

let consequences () =
    printfn ""
    printfn "############### Consequences ###############"

    // Consequence 1. The LaunchScript learns nothing after the launch. This is not a
    // discipline the branches keep; it is the shape of edge C4, which is `=>`. The only
    // thing that ever reaches the LaunchScript is the User's own trigger, and it asks
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

    // What makes that true is how little the LaunchScript talks to: one actor, the
    // browser it opens. It follows that it and the Server have nobody in common. A
    // corollary of the shape, and no longer a Consequence of its own.
    let reachableFrom a =
        Edges.table
        |> List.filter (fun (x, _, _) -> x = a)
        |> List.map (fun (_, _, y) -> y)
        |> Set.ofList

    expect "C1 the LaunchScript reaches only the browser it opens, and nothing else"
        (reachableFrom MainEhrLaunchScript = Set.ofList [ GenPresClient(BrowserId 0) ])

    expect "Corollary: the LaunchScript and the Server can reach no party in common"
        (Set.intersect (reachableFrom MainEhrLaunchScript) (reachableFrom GenPresServer)
         |> Set.isEmpty)

    // Constraints: a pair without an edge cannot exchange data at all, and edges do
    // not compose. No component relays on another's behalf unless stated.
    expect "Constraints: a pair without an edge cannot exchange data at all"
        (not (Edges.permits MainEhrWorkstation GenPresServer)
         && not (Edges.permits GenPresDatabase PatientDataPlatform)
         && not (Edges.permits (GenPresClient(BrowserId 1)) GenPresDatabase)
         && not (Edges.permits (GenPresClient(BrowserId 1)) UserRegistry))

    // Consequence 2. The Launch is the only channel from the EHR side, and the key is
    // all that authenticates it: whoever holds it can name any Patient. The User it
    // cannot name, because `type Launch` has no field for one. That half is asserted by
    // the Rule 4 check further down.
    //
    // On the labels: C1..C10 in `actors-and-edges.md` are *edge* names, while C1, C2,
    // C3, C5 and C6 here are *Consequence* names. Doc-C2 is Workstation ->
    // PatientDataPlatform and has nothing to do with this check.
    let ehrSide = [ MainEhrWorkstation; MainEhrLaunchScript ]

    let isGenPres a =
        match a with
        | GenPresClient _
        | GenPresServer
        | GenPresDatabase -> true
        | _ -> false

    // One way in, and it is a `=>`: the table grants the EHR side exactly one edge.
    let ehrCrossings =
        Edges.table
        |> List.filter (fun (x, _, y) -> List.contains x ehrSide && isGenPres y)

    expect "C2 the only edge from the EHR side into GenPRES is C4, and it is one-way"
        (ehrCrossings = [ (MainEhrLaunchScript, EdgeKind.Launch, GenPresClient(BrowserId 0)) ])

    // In the run as well as in the table: nothing but a Launch ever crossed. The
    // non-emptiness is half the check, because a filter that matched nothing would pass
    // whatever the runs did.
    let crossed =
        allTrace |> List.filter (fun e -> List.contains e.From ehrSide && isGenPres e.To)

    expect "C2 Launches did cross from the EHR side, and nothing else ever did"
        (not crossed.IsEmpty
         && crossed |> List.forall (fun e -> match e.Msg with OpenUrl _ -> true | _ -> false))

    // The key being all that authenticates a Launch has two halves. That nothing
    // sealed under the wrong key gets through cannot be re-derived here:
    // `CheckLaunchSpent` carries the nonce and nothing more, and a tampered Launch
    // has the same nonce as the one it was copied from, so a flat trace cannot tell the
    // two apart. It is proved where the forgery is attempted, in UC-1 ext 4a.
    //
    // The half that belongs here is why the mac has to be the whole gate: there is
    // nothing else to check a Launch against. The Server cannot ask the EHR side
    // anything, so a Launch that verifies is honoured for whatever Patient it names.
    // The key is not one factor among several; it is the only one.
    expect "C2 the Server can corroborate a Launch with nobody on the EHR side"
        (not (Edges.permits GenPresServer MainEhrWorkstation)
         && not (Edges.permits GenPresServer MainEhrLaunchScript))

    // Consequence 5. Workstation, LaunchScript and browser all run on the User's PC,
    // so the PC needs a route to every actor those three talk to, and the launch key,
    // which the LaunchScript holds and `Token.mintLaunch` seals with.
    let pcMustReach =
        [ MainEhrWorkstation; MainEhrLaunchScript; GenPresClient(BrowserId 0) ]
        |> List.map reachableFrom
        |> Set.unionMany

    let pcNeeds =
        Set.ofList
            [
                UserRegistry
                PatientDataPlatform
                IdentityProvider
                GenPresClient(BrowserId 0)
                GenPresServer
            ]

    expect "C5 the User's PC must reach exactly these five, and no more" (pcMustReach = pcNeeds)

    // Consequence 3. Nothing can tell the LaunchScript whether the Launch was
    // honoured: it has exited, and it never asked. Every message that decides a
    // Launch's fate is between the Client, the Server and the Database.
    // Consequence 4. The Launch travels in a URL, so it lands in history and logs.
    // What the model can show is the exposure: the URL is the only thing that ever
    // carries a Launch to a Client, and nothing sends one back. History and proxy logs
    // are outside it, which is why the Consequence answers with the two mitigations
    // rather than with a defence — single use (Rule 2) and a short life (Rule 3), each
    // proved in UC-1.
    expect "C4 a Launch reaches a Client only on a URL, and only from the LaunchScript"
        (allTrace |> List.exists (fun e -> match e.Msg with OpenUrl _ -> true | _ -> false)
         && allTrace
            |> List.forall (fun e ->
                match e.Msg with
                | OpenUrl _ -> e.From = MainEhrLaunchScript && (match e.To with GenPresClient _ -> true | _ -> false)
                | _ -> true))

    expect "C3 nothing about a Launch's fate ever goes near the LaunchScript"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | RedeemLaunch _
             | CheckLaunchSpent _
             | LaunchUnspent _
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

    // Rule 12. The SessionId is a bearer credential and never travels in a URL. The
    // only message that is a URL is OpenUrl, and it carries a Launch.
    expect "Rule 11 the only thing that ever travels as a URL is a Launch"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | OpenUrl _ -> e.To |> function GenPresClient _ -> true | _ -> false
             | _ -> true))

    // Rule 23. The PIN never leaves GenPRES. GenPRES is the Client, the Server and the
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
        | Act(ConfirmsSign _) -> true
        | SessionRequest(_, _, SubmitTreatmentPlan { Pin = Some _ }) -> true
        | Act(EntersResetCode _) -> true
        | SessionRequest(_, _, SupplyResetCode _) -> true
        | ReplacePinIfCode _ -> true
        | PinReplaced(_, c) -> c.Pin.IsSome
        | CredentialRead(_, Some c) -> c.Pin.IsSome
        | _ -> false

    expect "Rule 23 no envelope carrying a PIN ever goes outside GenPRES"
        (allTrace
         |> List.filter (fun e -> carriesPin e.Msg)
         |> List.forall (fun e -> not (outsideGenPres.Contains e.To)))

    expect "Rule 23 and the mail that says a PIN changed never carries the PIN itself"
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

    expect "Rule 11 a Session the User closed is never owed a notice"
        (everyRecordWritten
         |> List.forall (fun r ->
             match r.State with
             | Ended(ClosedByUser, _) -> r.Notice = NotOwed
             | _ -> true))

    expect "Rule 10 an open Session owes nothing either: the ending is what creates it"
        (everyRecordWritten
         |> List.forall (fun r -> r.State <> OpenOrGone || r.Notice = NotOwed))

    expect "Rule 11 nothing is ever delivered or acknowledged without an ending that owed it"
        (everyRecordWritten
         |> List.forall (fun r ->
             match r.State, r.Notice with
             | Ended(mark, _), (Delivered _ | Acknowledged _) -> SessionRecord.owesNotice mark
             | _, (Delivered _ | Acknowledged _) -> false
             | _ -> true))

    // Rule 4. The Session's User is the BrowserIdentity, never the Launch. Two halves.
    // The type-level half: a Launch has no login field at all (Concept 3), so nothing
    // in it could have named a User. The trace half: every launched Session that opened
    // was preceded by a RedeemLaunch from that same Client proving that same login, and
    // the Server had no other source to take it from.
    let openedByLaunch =
        allTrace
        |> List.indexed
        |> List.choose (fun (i, e) ->
            match e.Msg with
            | SessionOpened(_, _, Some uc, _, _, _) -> Some(i, e.To, uc)
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

    // ── Rule 16, over every version of every TreatmentPlan the run ever held ──
    // A TreatmentPlan never changes. Scenarios all replay from the same world, so a
    // plan id alone does not name one plan across the run. But a plan written twice
    // with the same id and different content would show up here as two sightings
    // sharing an id, which is what Rule 16 forbids within one record.
    let sightings = allPlans |> List.collect id |> List.distinct

    expect "Rule 16 the run really did write plans, so the claims below are not empty"
        (not sightings.IsEmpty)

    // Within any one state of the Database, a plan id names one plan: two rows sharing
    // an id would be a plan that had been written twice.
    expect "Rule 16 one Patient, one plan id: no snapshot ever holds the same name twice"
        (allPlans
         |> List.forall (fun snapshot ->
             snapshot
             |> List.groupBy (fun s -> s.Patient, s.Id)
             |> List.forall (fun (_, xs) -> xs |> List.distinct |> List.length = 1)))

    // And every snapshot is one ordered sequence per Patient: the numbers are unique,
    // and they are what "newer than" compares (Rules 20, 21). That a plan is never
    // rewritten is proved where it can be: over one scenario's own history, by the
    // append-only suffix check under Guarantee 4.
    expect "Rule 16 every snapshot is one ordered sequence per Patient (Concept 12)"
        (allPlans
         |> List.forall (fun snapshot ->
             snapshot
             |> List.groupBy _.Patient
             |> List.forall (fun (_, plans) ->
                 let nos = plans |> List.map _.No
                 nos |> List.distinct |> List.length = nos.Length)))

    // Rule 5. The Role a Session carries is the registry's answer, never synthesised:
    // every launched Session is preceded by a UserResolved carrying the very same
    // UserContext. Anonymous opens are excluded, because they carry no User at all
    // (Rule 14). The type-level half is that a Launch has no Role field (Concept 3), so
    // a launch could not have supplied one.
    let indexed = allTrace |> List.indexed

    let resolvedBefore (i: int) (uc: UserContext) =
        indexed
        |> List.exists (fun (j, e) ->
            j < i && (match e.Msg with UserResolved(_, uc', _, _) -> uc' = uc | _ -> false))

    expect "Rule 5 every Session's Role came from the registry, and came first"
        (indexed
         |> List.forall (fun (i, e) ->
             match e.Msg with
             | SessionOpened(_, _, Some uc, _, _, _) -> resolvedBefore i uc
             | _ -> true))

    // ── Rule 32, structurally ──
    // The Server carries nothing across requests. This is checked after every step of
    // every scenario, not sampled: `noteFlight` trips a flag the moment a step ends
    // with anything in the in-flight table.
    expect "Rule 32 the in-flight table is empty at the end of every scenario step"
        (not everCarriedARequest)

    // The type says the same. `ServerState` has no field a Session could live in, so
    // Rule 32 is not a discipline the branches keep but something the state cannot
    // express. And nothing of the work stays behind: every Computed is its own
    // request's payload handed back, checked over every one there has ever been.
    let computedIsItsOwnRequest =
        // Requests can be in flight together, from two Sessions or from a scenario
        // that interleaves them, so what is checked is that every answer matches a
        // request still outstanding, and consumes it.
        let rec walk pending trace ok =
            match trace with
            | [] -> ok
            | { Msg = SessionRequest(_, _, Compute os) } :: rest -> walk (os :: pending) rest ok
            | { Msg = Computed os } :: rest ->
                match pending |> List.tryFindIndex ((=) os) with
                | Some i -> walk (pending |> List.indexed |> List.filter (fst >> (<>) i) |> List.map snd) rest ok
                | None -> walk pending rest false
            | _ :: rest -> walk pending rest ok
        walk [] allTrace true

    expect "Rule 32 every Computed is the request's own payload handed back, nothing added"
        computedIsItsOwnRequest

    // What a Submission actually carries, which is a measurement and not a judgement:
    // the whole WorkPlan, its Patient Data included (Concept 16).
    expect "Rule 32 a Submission carries the whole WorkPlan: OrderContexts and Patient Data alike"
        (allTrace
         |> List.exists (fun e ->
             match e.Msg with
             | SessionRequest(_, _, SubmitTreatmentPlan req) ->
                 not req.Work.Orders.IsEmpty && req.Work.Data.IsSome
             | _ -> false))

    // ── Rule 33 ──
    // The payload has no User in it to believe, because a token names a SessionId and
    // not an identity. So a plan's `By` can only have come from a record the Server
    // just read, and every append is preceded by that read.
    let readARecordFor (i: int) (uc: UserContext) =
        indexed
        |> List.exists (fun (j, e) ->
            j < i && (match e.Msg with SessionRecordRead(_, Some r, _) -> r.User = Some uc | _ -> false))

    ignore readARecordFor

    expect "Rule 33 every TreatmentPlan's User came off a SessionRecord, never off the payload"
        (allRecords
         |> List.map _.Id
         |> Set.ofList
         |> fun known ->
             allTrace
             |> List.forall (fun e ->
                 match e.Msg with
                 // Rule 42: the User is read inside the act, off the SessionRecord the
                 // commit names, and never off the payload, which carries no User.
                 | CommitTreatmentPlan(_, c) -> known.Contains c.Sid
                 | _ -> true))

    // ── Rules 21, 22 and 34 ──
    expect "Rule 34 every token the Server ever issued verifies, and every stale one was refused honestly"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | SessionOpened(_, _, _, _, _, t) -> Token.verifyOpened t
             | TreatmentPlanSubmitted(_, t) -> Token.verifyOpened t
             | TreatmentPlanOpened(_, _, t) -> Token.verifyOpened t
             | _ -> true))

    // Rule 22, over the whole run: the notice carries no token and nothing was ever
    // asked to return one. It informs, and the only guard is Rule 20.
    expect "Rule 22 every newer-plan notice named a User and a time, and nothing else was owed"
        (allTrace |> List.exists (fun e -> match e.Msg with NewerPlanNotice _ -> true | _ -> false)
         && allTrace
            |> List.forall (fun e ->
                match e.Msg with
                | NewerPlanNotice(_, at) -> at >= 0
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

    // ── Rule 10 ──
    // Six endings, and a Server restart is not one of them. The type says so, because
    // there is no `EndMark` for it, and UC-8 ext 1a shows what happens instead.
    let marksSeen =
        everyRecordWritten
        |> List.choose (fun r -> match r.State with Ended(m, _) -> Some m | OpenOrGone -> None)
        |> List.distinct

    // ── Rules 7 and 40, as invariants over every state the Database ever held ──
    // Not "the scenarios did not happen to break it": at the end of every step of
    // every scenario, no User held two open Sessions and no browser did either.
    expect "Rule 8 no User ever held two open Sessions at once (Rules 8, 40)"
        (allDatabases
         |> List.forall (fun sessions ->
             sessions
             |> List.filter SessionRecord.isOpen
             |> List.choose SessionRecord.userId
             |> fun users -> users.Length = (users |> List.distinct |> List.length)))

    expect "Rule 8 and no browser ever held two open Sessions at once (Rules 8, 40)"
        (allDatabases
         |> List.forall (fun sessions ->
             sessions
             |> List.filter SessionRecord.isOpen
             |> List.choose _.Browser
             |> fun browsers -> browsers.Length = (browsers |> List.distinct |> List.length)))

    expect "Rule 9 every ending the run produces is one the Rules name, and all of them occur"
        (marksSeen |> List.sort
            = List.sort [ ClosedByUser; ReplacedInBrowser; Idle; Superseded; WrongPinLimit; Expired ])

    // Rule 10. `Expired` is not the anonymous ending alone: a launched Session reaches
    // it too, at `sessionMaxLifetime` counted from the open, and never sooner, whatever
    // Rule 9's clock says. The type cannot keep that discipline, so it is asserted.
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
    let g1 = quiet "G" g0 [ act 1 (Prescribes(OrderContextId "g-1")); yield! signs 1 pinA ]
    let g2 = quiet "G" g1 [ act 1 (Prescribes(OrderContextId "g-2")); yield! signs 1 pinA ]
    let g3 = quiet "G" g2 (launchAs ucB.Login (Some pat2))
    // B is blocked first (Rule 20), takes over A's plan (Rule 18), and then signs.
    let g4a = quiet "G" g3 [ act 2 (Prescribes(OrderContextId "g-3")); yield! signs 2 pinB ]
    let g4b = quiet "G" g4a [ act 2 (OpensTreatmentPlan (headOf pat2 g4a).Value.Id) ]
    let g4 = quiet "G" g4b [ act 2 (Prescribes(OrderContextId "g-3")); yield! signs 2 pinB ]
    // And one Submission that does not land, so the audit has a refusal in it to find.
    let g5 = quiet "G" g4 [ act 2 (Prescribes(OrderContextId "g-4")); yield! signs 2 (Pin "0000") ]

    let record = recordFor pat2 g4

    // ── Guarantee 1: one constant ──
    expect "G1 the PatientId is the one thing no TreatmentPlan may change"
        (record.Plans |> List.forall (fun s -> s.Patient = pat2))

    expect "G1 and only a launch supplies one, so no hand ever set it (Rules 13, 14, 33)"
        (g4
         |> patientsInRecord
         |> List.forall (fun p -> (recordFor p g4).Plans |> List.forall (fun s -> s.Patient = p)))

    // ── Guarantee 2: one version ──
    expect "G2 exactly one TreatmentPlan is the visible version: the most recent (Rules 17, 19)"
        ((PatientRecord.latest record |> Option.map _.Id) = (record.Plans |> List.tryHead |> Option.map _.Id))

    // Reading is wider than building. Every TreatmentPlan is readable (Rule 18), but
    // only the most recent can be built on: opening an older one makes it the
    // Session's baseline and Rule 20 then blocks the Submission.
    expect "G2 reading is wider than building: the whole history is open to read (Rule 18)"
        (record.Plans |> List.forall (fun s -> (record |> PatientRecord.mayOpen s.Id).IsSome))

    expect "G2 and only the newest can be built on (Rules 18, 20)"
        (record.Plans
         |> List.forall (fun s ->
             let isNewest = Some s.Id = (PatientRecord.latest record |> Option.map _.Id)
             (record |> PatientRecord.blocking (Some s.Id)).IsNone = isNewest))

    expect "G2 and every User has the same starting point, because there is only one (Rule 19)"
        ((record |> PatientRecord.startsFrom |> Option.map _.Id)
            = (PatientRecord.latest record |> Option.map _.Id))

    // ── Guarantee 3: carts and one checkout ──
    // The cart is private by construction: it lives in the User's own Client and the
    // Server keeps none of it (Rule 32). The checkout is single by construction too:
    // the Database arbitrates the append (Rule 36).
    expect "G3 signing is the only checkout: every TreatmentPlan in the run was signed by a Prescriber"
        (not allPlans.IsEmpty
         && allPlans |> List.forall (List.forall (fun s -> s.By.Role = Prescriber)))

    expect "G3 a Reader never appears as the creator of anything (Roles)"
        (g4
         |> patientsInRecord
         |> List.forall (fun p -> (recordFor p g4).Plans |> List.forall (fun s -> s.By.Role <> Reader)))

    // Concept 13. Every TreatmentPlan this run created was built on one before it.
    expect "G3 every TreatmentPlan created here stands on a base (Concept 13)"
        (record.Plans
         |> List.filter (fun s -> s.No >= TreatmentPlanNo 10)
         |> List.forall (fun s -> s.Base.IsSome))

    expect "G3 the two carts never met in the Server: it held neither (Rule 32)"
        (g4.GenPres.InFlight.IsEmpty
         && (g4.Clients |> Map.exists (fun _ c -> not c.Work.Orders.IsEmpty)))

    // ── Rule 46: the audit ──
    // The record of what was done lives in the private store, written by the party
    // that did it, in the same act (Rule 42). Every TreatmentPlan in the final
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
                (auditLines
                 |> List.filter (fun a -> a.What.Contains i && a.What.Contains "signed" && a.What.Contains u))
                    .Length = 1))

    expect "Rule 46 refusals are recorded too — a Submission that did not land is an event"
        (auditLines |> List.exists (fun a -> a.What.Contains "refused"))

    // Rule 46 says every refused request, not only the ones the Database decides. A
    // request turned away in the Server never reaches the Database, so it has to be
    // recorded there — over the whole run, and not only in this Patient's audit.
    expect "Rule 46 and so are the Sessions: opened, and ended with the reason"
        (auditLines |> List.exists (fun a -> a.What.Contains "opened")
         && auditLines |> List.exists (fun a -> a.What.Contains "ended"))

    // Rule 46's last word: and when. Every line is stamped by the act that wrote it,
    // so the audit is a sequence of moments and not just a pile of sentences. Written
    // newest first, so the stamps run backwards through it.
    expect "Rule 46 every entry is stamped, in the run's own time, and in the order written"
        (auditLines
         |> List.forall (fun a -> a.At > 0 && a.At <= g5.Env.Now)
         && auditLines |> List.pairwise |> List.forall (fun (newer, older) -> newer.At >= older.At))

    // ── The two stores (Actor 5) ──
    // A copy of the Clinical store is what the PatientDataPlatform is handed (Actor
    // 6). What it holds is signed history and nothing else: no credential, no reset
    // code, no spent key.
    let exported = g5.Database.Clinical

    expect "Actor 5 the Clinical store holds TreatmentPlans, every one of them signed"
        (exported.Signed |> Map.forall (fun _ plans -> not plans.IsEmpty))

    expect "Actor 5 an export of it carries no credential, no code and no key"
        (let text = $"%A{exported}"

         [ "UserCredential"; "Pin "; "ConfirmationCode"; "IdemKey"; "PendingReset" ]
         |> List.forall (fun secret -> not (text.Contains secret)))

    // Actor 5, Guarantee 4 and Rule 12. The copy names nothing in the private store.
    // One reference could reach into it: the SessionId of the Session that created the
    // plan, which is a bearer credential. The append drops it. So a plan records its
    // Session only until it lands: Concept 13 asks for it, and Actor 5 and Guarantee 4
    // forbid carrying it into the copy, which is what ships.
    expect "Actor 5 no exported TreatmentPlan carries a SessionId (Rule 12, Guarantee 4)"
        (exported.Signed |> Map.forall (fun _ plans -> plans |> List.forall (fun s -> s.Session = None)))

    expect "Actor 5 an export of it mentions no SessionId at all, however it is rendered"
        (not (($"%A{exported}").Contains "SessionId"))

    // Concept 13. `Base` does stay: every plan is signed and every plan is in the
    // clinical store, so the chain closes over itself without a second field to
    // follow. That is what collapsing the two stores bought.
    expect "Concept 13 every Base in the clinical store resolves inside the clinical store"
        (let ids =
            exported.Signed |> Map.toList |> List.collect (snd >> List.map _.Id) |> Set.ofList

         exported.Signed
         |> Map.forall (fun _ plans ->
             plans |> List.forall (fun s -> s.Base |> Option.forall ids.Contains)))

    // And it is a real chain, not a field that is always empty: the run built plans on
    // top of plans, so some of them name a base.
    expect "Concept 13 the chain is real: plans in the run were built on plans before them"
        (exported.Signed
         |> Map.exists (fun _ plans -> plans |> List.exists (fun s -> s.Base.IsSome)))

    // Concept 12. The record is one append-only sequence and the clinical store holds
    // all of it. `recordOf` reads one half, so there is no second place a plan could be
    // and no filter anyone has to remember to apply.
    expect "Actor 5 a Patient's whole record is the clinical store's, and nothing else's"
        (g5
         |> patientsInRecord
         |> List.forall (fun p ->
             (recordFor p g5).Plans |> List.sortBy _.No
                = (g5.Database |> Database.signedOf p |> List.sortBy _.No)))

    // ── Guarantee 4: audit ──
    expect "G4 a TreatmentPlan carries the User who signed it (Concepts 13, 14; Rule 15)"
        (record.Plans |> List.forall (fun s -> s.By.UserId = ucA.UserId || s.By.UserId = ucB.UserId))

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

    // Stated as the claim rather than as a count: every TreatmentPlan that existed at
    // any point in the history is still in the record at the end of it.
    let everSigned = history |> List.collect _.Plans |> List.distinct

    expect "G4 nothing signed is ever lost: every TreatmentPlan ever made is still there"
        (everSigned <> []
         && everSigned |> List.forall (fun s -> record.Plans |> List.contains s))

    // What is not protected is unsigned work, which never existed to the record at
    // all: it lived only in its own browser and died with it (Concept 16).
    expect "G4 an older plan is not a place to build: Rule 20 blocks on anything newer"
        (record |> PatientRecord.blocking (Some(TreatmentPlanId "plan-0010"))).IsSome

    // ── Guarantee 5: a stolen Launch steals no authority ──
    // Whoever presents a Launch is identified as themselves (Rule 4), gets their own
    // Role (Rule 5) and only the Patient they themselves have active (Rule 6). Over
    // the whole run: no Session was ever opened for a User the registry had not just
    // answered about, on a Patient the registry had not just named.
    let launchedSessions =
        allTrace
        |> List.indexed
        |> List.choose (fun (i, e) ->
            match e.Msg with
            | SessionOpened(_, _, Some uc, pctx, _, _) when pctx.Patient.IsSome -> Some(i, uc, pctx.Patient)
            | _ -> None)

    expect "G5 the run really did open launched Sessions, so the claim below is not empty"
        (not launchedSessions.IsEmpty)

    expect "G5 every launched Session's User and Patient are the registry's answer, not the Launch's"
        (launchedSessions
         |> List.forall (fun (i, uc, patient) ->
             allTrace
             |> List.indexed
             |> List.exists (fun (j, e) ->
                 j < i
                 && match e.Msg with
                    | UserResolved(ForLaunch _, uc', _, active) -> uc' = uc && active = patient
                    | _ -> false)))


// ═══════════════════════════════════════════════════════════════════════════════
//                                  THE RUN
// ═══════════════════════════════════════════════════════════════════════════════

/// Everything the run accumulates into, put back as it started. A no-op from a
/// terminal, where the process is new. It matters in a live FSI session, such as an
/// IDE's, where a second `runAll ()` would otherwise count on from the first and stay
/// green while doing it, the whole-run checks satisfied by more of the same data.
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

/// Where the run writes itself. Beside the script, so it is found without looking, and
/// in a file rather than a terminal, because the trace is some hundreds of kilobytes.
let runLog = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "Integration.run.txt")

let runAll () =
    reset ()

    // Every scenario prints through `printfn`, which resolves `Console.Out` at each
    // call, so redirecting it here catches the whole run without touching any of the
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
