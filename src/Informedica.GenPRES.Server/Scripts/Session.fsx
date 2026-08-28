// ═══════════════════════════════════════════════════════════════════════════════
//   GenPRES – MainEHR Integration: the system model, executable
// ═══════════════════════════════════════════════════════════════════════════════
//
// A runnable model of the design document *GenPRES – MainEHR Integration*. The
// document is leading: every type, message and branch below exists to carry one of
// its Actors, Concepts, Constraints or Rules, and cites it by number. Nothing the
// document does not sanction lives here.
//
// The file is standalone — no #load, no #r. Run it with:
//
//     dotnet fsi Session.fsx
//
// It prints a trace per scenario and ends with a count of self-checks.
//
// ═══════════════════════════════════════════════════════════════════════════════
//   SECTION 0 — THE SYSTEM MODEL, AS THE DOCUMENT STATES IT
// ═══════════════════════════════════════════════════════════════════════════════
//
// The document's System Model, in its own order and its own words — abridged only
// where a paragraph runs long, never where it decides something. Everything below
// this section exists to carry one of these and cites it by number.
//
// ── Actors ─────────────────────────────────────────────────────────────────────
// The kinds of participant that appear in the use cases. [ours] = under
// construction. [given] = existing infrastructure, not ours to change. The User is
// neither — they are who the system is for.
//
//  1. MainEHR Workstation   [given]  the running EHR Client
//  2. MainEHR LaunchScript  [ours]   a VB.NET script behind a button in the
//                                    Workstation. Runs on trigger, then exits. The
//                                    only part of MainEHR we control.
//  3. GenPRES Client        [ours]   GenPRES UI running in a Browser
//  4. GenPRES Server        [ours]   GenPRES backend
//  5. GenPRES Database      [ours]   two stores, one writer — the Server. The
//                                    clinical store holds the Signed TreatmentPlans
//                                    with their base references, and is what the
//                                    PatientDataPlatform copies. The private store
//                                    holds everything else — Unsigned TreatmentPlans,
//                                    SessionRecords, UserCredentials, the spent-state
//                                    of Tokens (Concept 17), the audit — and is never
//                                    copied anywhere.
//  6. PatientDataPlatform   [given]  a shared, read-only copy of the databases of
//                                    MainEHR, GenPRES and other applications. How data
//                                    gets there is out of scope.
//  7. User                           person who uses MainEHR and GenPRES
//  8. Broker                [ours]   hands a launch from the LaunchScript to the Server
//  9. UserRegistry          [ours]   says who a login belongs to, what that person
//                                    may do, and how to reach them by mail
// 10. MailService           [given]  sends mail to a person, outside GenPRES and
//                                    outside MainEHR
//
// ── Roles ──────────────────────────────────────────────────────────────────────
// The kinds of authority a User can hold. The UserRegistry decides the Role; MainEHR
// and GenPRES enforce it independently, each within its own application.
//
//  1. Prescriber  may read and write — writing meaning creating TreatmentPlans
//  2. Reader      may never create a TreatmentPlan. Like any User they may prescribe
//                 within their Session (Concept 15), but nothing of it can be saved.
//
// ── Concepts ───────────────────────────────────────────────────────────────────
// The things passed between actors, or held by them, and what each one means.
//
//  1. UserContext        User identification and User Role.
//  2. PatientContext     PatientId and Patient Data relevant for GenPRES. The User
//                        can supply the data by hand; only a launch can supply the
//                        identification. Launched, the data is read from the
//                        PatientDataPlatform once, at the launch, and not refreshed
//                        while the Session lives: that it may go out of date during
//                        the Session is accepted — except at a signature, where it is
//                        read again (Rule 44).
//  3. LaunchAssertion    asserts a MainEHR login, and the Patient if one is active —
//                        no verified identity, no Role. The active Patient is also
//                        MainEHR's word that this User may work on this Patient now:
//                        patient-level authorisation is MainEHR's, and GenPRES
//                        enforces nothing finer.
//  4. LaunchCredential   a single-use reference to a LaunchAssertion, short-lived and
//                        opaque — it reveals nothing about what it refers to.
//  5. MainEHR Session    the period a User is logged in at a Workstation. Many
//                        Patients can be handled in it, one active at a time.
//  6. MainEHR PatientRecord   all patient data maintained by MainEHR.
//  7. GenPRES UserCredential  held by GenPRES for one User and keyed by who they are,
//                        not by their renameable login — holding the login by which
//                        the UserRegistry currently knows that person, a PIN if one is
//                        set, and the count of consecutive wrong PIN entries (Rule
//                        27). The PIN is optional: a UserCredential may hold none —
//                        the User has never set one — and one without a PIN cannot
//                        sign; once set, a PIN is only ever replaced, never merely
//                        removed (Rule 37). It carries no Role and no identity of its
//                        own: it only lets a User prove, during a Session, that they
//                        are the person named in the UserContext.
//  8. GenPRES Session    the interaction of a User with GenPRES — for a Patient if the
//                        launch supplied one, otherwise for no Patient; opened without
//                        a launch, it is anonymous (Rule 13). Only a Session with a
//                        Patient allows opening or creating TreatmentPlans (Rule 12).
//                        A Session has no state in the Server between requests: its
//                        identity and standing live in its SessionRecord, its work in
//                        the Client (Rule 31).
//  9. GenPRES SessionRecord   binds a SessionId to exactly one User — or to no User,
//                        when the Session is anonymous — and to a Patient if it has
//                        one. Records whether the Session is open or ended, when it
//                        last heard from the Client (Rule 8), and whether the User has
//                        acknowledged its ending (Rule 10). Kept after it ends.
// 10. OrderContext      a PatientContext together with the OrderScenarios currently
//                        under consideration for that Patient. It has an identity that
//                        persists across TreatmentPlans, and carries the UserContext of
//                        the User whose Session last changed it — stamped at each save
//                        (Rule 14), so one never saved carries none.
// 11. OrderScenario     one proposed Order together with the prescribing information
//                        that gives it meaning but is not part of the Order itself.
// 12. GenPRES PatientRecord   the append-only history of a Patient in GenPRES — a
//                        sequence of TreatmentPlans, every one carrying that Patient's
//                        PatientId: the one thing no TreatmentPlan may change.
// 13. TreatmentPlan     the Patient's treatment plan as it stood when saved — a set of
//                        their OrderContexts, carrying the UserContext of the User who
//                        created it and a reference to the TreatmentPlan it was created
//                        from — its base — if any. It also records the Patient Data it
//                        was built on: the values, where each came from (the platform,
//                        or entered by hand) and when they were read (Concept 2) — so a
//                        signed plan can be explained from its own record. Either
//                        Signed or Unsigned.
// 14. Saving and Signing   one act — creating a TreatmentPlan. Signing is saving while
//                        supplying the PIN of the Session's User: the TreatmentPlan is
//                        then Signed, otherwise Unsigned. There is no other way one
//                        comes into being — and none is ever changed or saved again:
//                        changing means creating a new one whose base is the old.
// 15. Prescribing       changing, within a Session, the Patient Data of the
//                        PatientContext and adding, removing or changing
//                        OrderContexts. Prescribing touches only the WorkPlan
//                        (Concept 16): nothing reaches the PatientRecord until a
//                        TreatmentPlan is created, and the Server computes on what the
//                        Client sends — Patient Data included — without keeping any of
//                        it.
// 16. WorkPlan          the plan being composed in the Client — the Patient Data and
//                        the OrderContexts under the User's hands (Concept 15). It is
//                        mutable, carries no attribution and sits in no record: it
//                        becomes a TreatmentPlan only by being created (Concept 14),
//                        and otherwise dies with the browser. Held only by its own
//                        Client (Rule 31), it is what the shopping-cart metaphor names
//                        (Guarantee 3).
// 17. Token             a short-lived note the Server writes to itself and hands to
//                        the Client, which returns it unaltered — the Server's memory
//                        across requests, where it keeps none of its own (Rule 31).
//                        Bound to what it names, impossible for a Client to make, and
//                        spent by the create it accompanies. Three exist: the
//                        OpenedToken — which TreatmentPlan the Session opened (Rule
//                        33); the NoticeToken — whose Unsigned work a notice disclosed
//                        (Rule 34); the SigningChallenge — the exact plan a signature
//                        would approve (Rule 43). Every create carries and proves the
//                        OpenedToken: has anything Signed appeared since the User
//                        started (Rule 20)? A signing create carries the challenge
//                        besides: is the plan committed the plan the User last saw
//                        (Rule 43)? One guards where the User began, the other what
//                        the User reviewed; between them the Server needs no memory of
//                        the Session at all. The Client holds a token just long enough
//                        to return it; the Server holds only the key that verifies any
//                        of them, and the spent-marks of those already used (Actor 5).
//
// ── Constraints ────────────────────────────────────────────────────────────────
// Notation — how to read the edges below. Not itself a constraint.
//   X ->  Y   X initiates a connection to Y and receives Y's response on it. Grants
//             initiation in that direction only; the reverse is never implied.
//   X =>  Y   X launches Y with initial parameters. One-way: no response, no error
//             path back.
//   X <-> Y   interaction, not request–response: a User can read what Y shows and
//             act on it.
// Any pair without an edge cannot exchange data at all. Edges do not compose — no
// component relays on another's behalf unless stated.
//
// User Interaction — which components a User can read and act on, or start.
//   U1. Any User <-> MainEHR Workstation
//   U2. Any User <-> MainEHR LaunchScript — the User starts it; while it runs it can
//                                            report its own acts back (the Broker
//                                            exchange, the launching of the browser),
//                                            and it exits at once, so nothing later
//                                            ever comes from it.
//   U3. Any User <-> GenPRES Client
//
// Communication — which components may reach which, and nothing else is possible.
// Edges touching a [given] component are what the deployment allows; edges between
// [ours] components are what we choose to build.
//   C1.  MainEHR Workstation  -> UserRegistry
//   C2.  MainEHR Workstation  -> PatientDataPlatform
//   C3.  MainEHR LaunchScript -> Broker
//   C4.  MainEHR LaunchScript => GenPRES Client
//   C5.  GenPRES Client       -> GenPRES Server
//   C6.  GenPRES Server       -> Broker
//   C7.  GenPRES Server       -> UserRegistry
//   C8.  GenPRES Server       -> PatientDataPlatform
//   C9.  GenPRES Server       -> GenPRES Database
//   C10. GenPRES Server       -> MailService
//
// ── Consequences ───────────────────────────────────────────────────────────────
// Derived from the edges above — not new assertions, and not negotiable without
// changing an edge.
//
//  1. The LaunchScript learns nothing after the launch. What it can report to the
//     User (User Interaction 2) ends with its own acts: the Broker exchange (UC-1
//     ext 3a) and the launching of the browser. Expired credential, Server down,
//     wrong patient — none of it reaches it. Error handling falls to the Client,
//     except when the Server is unreachable: the Client is served by the Server, so
//     then no Client is served either and the User is left with the browser's error
//     page.
//  2. The Broker is the only party both the LaunchScript and the Server can reach,
//     so it is the sole channel between the EHR side and GenPRES.
//  3. Only the Broker knows whether a credential was redeemed, and it cannot tell
//     the LaunchScript, which has exited.
//  4. The credential travels in a URL, so it lands in browser history, the address
//     bar, and possibly referrer and proxy logs — hence single use, short lifetime.
//  5. Both the Workstation and the LaunchScript run on the User's PC, so their calls
//     originate there. Every workstation needs network access to the UserRegistry,
//     the PatientDataPlatform and the Broker, plus whatever secret authenticates it.
//  6. The Server cannot reach a Client (edge C5 goes one way only), so a Client only
//     learns its Session ended at its next request. Until then it shows a
//     live-looking screen.
//
// ── Invariants ─────────────────────────────────────────────────────────────────
// Always true of the environment. Given: not ours to change.
//  1. A User has at most one active Patient at any moment in a MainEHR Session.
//
// ── Possibilities ──────────────────────────────────────────────────────────────
// May occur in the environment. Given: not ours to prevent, only to handle.
//  1. Users can leave a logged in MainEHR Session open and another User can act in it.
//  2. Multiple Users can have the same Patient active each in their own MainEHR Session.
//
// ── Rules ──────────────────────────────────────────────────────────────────────
// What the [ours] components must enforce. Chosen, and changeable by decision. One
// assertion each; grouped for reading, numbered straight through for citing.
//
// Launch
//   1. The LaunchScript decides which MainEHR User may run it.
//   2. A LaunchCredential is accepted once; a second presentation is refused.
//   3. A LaunchCredential is accepted only within its lifetime.
//   4. Only the Server may redeem a LaunchCredential at the Broker.
//   5. The Server takes the Role from the UserRegistry at each launch, never from
//      the launch itself.
//   6. If a launch cannot be honoured — no credential, no Role, or a required PIN
//      not set (Rule 24) — no Session is opened by it. There is no silent fallback:
//      at most, the Client offers the User a fresh anonymous open (Rule 13; UC-8),
//      which carries nothing over from the launch — no User, no Patient.
//
// Session
//   7. A User has at most one open Session; opening another closes the rest. The
//      limit is per User, not per Patient: two Users may each hold their own
//      Sessions for the same Patient at once.
//   8. Every request from the Client refreshes its Session's idle clock.
//   9. A Session ends when the User closes it, when it has been idle too long, when
//      the wrong-PIN limit is reached (Rule 27), or when that same User opens another
//      Session (Rule 7). Closing is an explicit act in the Client: a browser that
//      vanishes is indistinguishable from one gone quiet, so the Session is left to
//      idle out. A Server restart ends nothing: the Server holds no Session state to
//      lose (Rule 31).
//  10. When a Session ends other than by the User closing it, the User is told at the
//      next opportunity: through any Client still holding that SessionId, at its next
//      request, or at the User's next launch. The notice stands until the User
//      acknowledges it, and never returns after: acknowledged once — sending alone
//      does not count as telling.
//  11. The SessionId is a bearer credential — whoever holds it can use it — so it
//      never travels in a URL and never sits where script can read it: it rides in a
//      cookie the browser alone handles.
//  12. A Session without a PatientId lets the User prescribe (Concept 15), Patient
//      Data included, but a TreatmentPlan cannot be opened or created.
//  13. A Session opened without a launch is anonymous: it binds to no User and
//      carries no UserContext, no Role, and no PatientId. Rules that speak of the
//      Session's User (7, 10) do not apply to it, and neither does idling: it ends
//      when closed, or at an absolute limit — enough to bound the SessionRecords it
//      leaves behind, which are all it ever amounts to on the Server (Rule 31); its
//      WorkPlan lives and dies with the browser (Concept 16). The Atomicity rules
//      (40-45) have nothing to guard in it — it can commit nothing (Rule 12), so
//      there is no transaction for them to protect.
//
// Record
//  14. Every TreatmentPlan is created under the credentials of exactly one User — the
//      Session's — and carries that User's identity. Within it, every OrderContext
//      changed in the Session is stamped with that same UserContext; an unchanged
//      OrderContext keeps the stamp it had.
//  15. A TreatmentPlan is either Signed or Unsigned.
//  16. Only the most recent Signed TreatmentPlan counts clinically.
//  17. Signed TreatmentPlans are open to every User, to read: any of them may be
//      opened, but only the most recent one can be built upon — opening an older one
//      leaves creating blocked (Rule 20). An Unsigned TreatmentPlan is Rule 18's alone.
//  18. Only the User who created an Unsigned TreatmentPlan can open that TreatmentPlan.
//  19. A User can only start with the most recent TreatmentPlan that is either Signed
//      or Unsigned and their own. Where neither exists, the User works from nothing:
//      the Session's WorkPlan begins with no OrderContexts (Concept 16).
//  20. A User may create a new TreatmentPlan, unless a Signed one exists that is newer
//      than the one the User opened with. Opening that newest Signed TreatmentPlan
//      makes it the one the Session opened with — after that, creating is possible
//      again (UC-6).
//
// Notification
//  21. If a User is about to create a TreatmentPlan and an Unsigned one of another
//      User exists that is newer than the TreatmentPlan the User opened with — any
//      TreatmentPlan at all, where the User opened with nothing — the User is notified
//      — told whose work it is, not its contents — and may choose not to create.
//
// Signing
//  22. The Server is the only party that verifies a UserCredential; the PIN never
//      leaves GenPRES.
//  23. Every launch checks whether a PIN is set for the login.
//  24. A Prescriber with no PIN must set one before the launch continues, and only
//      after the UserRegistry has recognised their login.
//  25. A Reader is never asked for a PIN: a Reader never creates a TreatmentPlan
//      (Roles), so they have nothing to prove.
//  26. The Server mails the User and records the change on every setting of a PIN and
//      every replacement of one, the first setting included. The address comes from
//      the UserRegistry.
//  27. Wrong PIN entries count per UserCredential, across Sessions, the count updated
//      as one conditional operation at the Database (Rule 40): a wrong entry at the
//      configurable limit ends the Session (Rule 9) and suspends signing on the
//      credential until the PIN is replaced (Rule 37). A correct entry resets the
//      count, and a newly set PIN (Rule 26) starts with a count of zero.
//
// Configuration
//  28. A LaunchCredential lives long enough to carry one launch — a page load and a
//      retry or two — and no longer.
//  29. A Session lives long enough to span the gaps between a clinician's actions.
//  30. The wrong-PIN limit is small enough to make guessing hopeless, large enough to
//      forgive mistyping — and the PIN itself short enough to remember, large enough
//      in its space that the limit keeps guessing hopeless.
//
// State — where Session state lives; chosen so that the Server keeps none of it.
//  31. The Server holds no Session state between requests: the WorkPlan (Concept 16)
//      lives in the Client, and a Session's identity and standing live in its
//      SessionRecord in the Database. Two Users' work cannot meet in the Server,
//      because the Server holds neither.
//  32. The Server takes the User and the Patient of a request from the SessionRecord,
//      never from what the request carries — and a create whose OrderContexts name
//      another Patient than the SessionRecord's is refused whole (Guarantee 1).
//  33. The TreatmentPlan a Session opened with travels as the OpenedToken (Concept
//      17) — bound to the Session, the Patient and the TreatmentPlan — returned by the
//      Client with every create and verified then (Rules 19, 20). It works exactly
//      once: consumed by the create it accompanies and re-issued with the new
//      baseline, a spent or expired one is refused.
//  34. A choice to create anyway (Rule 21) travels as the NoticeToken (Concept 17):
//      issued with the notice, naming the Unsigned TreatmentPlans it disclosed,
//      honoured for those and for nothing newer.
//  35. The stamps of Rule 14 are computed by the Server against the base TreatmentPlan;
//      a stamp arriving from the Client is never accepted.
//  36. The Rule 20 check and the append are one act at the Database: a TreatmentPlan
//      lands only if no Signed TreatmentPlan newer than the one its Session opened
//      with has appeared meanwhile — an intervening Unsigned one does not block, it
//      notifies (Rules 20, 21). More than one Server may run; the Database decides
//      which lands. A refusal never names a TreatmentPlan the caller may not open: it
//      says whose, not which (Rules 17, 18, 21).
//
// Security — what [ours] enforces against a hostile environment.
//  37. A PIN is replaced only by its User: a reset mails a one-time code (Rule 26),
//      and returning the code together with the chosen new PIN through the Client
//      replaces the old one in a single act — there is never a moment without a PIN. A
//      code survives its short lifetime and a few wrong entries, then it is void: a
//      fresh reset, with a fresh mail, is the only way on. Changing a PIN without a
//      reset requires the current PIN.
//  38. Every signature re-takes the Role from the UserRegistry: authority withdrawn
//      since the launch blocks the signature at its commit.
//  39. The Client erases the LaunchCredential from the URL and the browser history at
//      first presentation, keeping it only in memory for retries within its lifetime
//      (Rules 3, 28); the Server serves the Client so that nothing of a Session is
//      cached or carried in a referrer, and no script but the Client's own runs in its
//      pages.
//
// Atomicity — what must be one act at the Database.
//  40. Every change to a SessionRecord is one conditional operation, guarded by the
//      state it expects: an ended Session can never return to open, and one open
//      Session per User (Rule 7) is a Database constraint, enforced in the same act
//      that opens the next.
//  41. Expiry is checked when a request arrives, not only by a sweep: a request from a
//      Session past its idle limit ends the Session then and there (Rules 8, 9) — it
//      does not refresh it.
//  42. Creating a TreatmentPlan is one transaction. At its commit the Database
//      re-verifies everything the request rests on — the Session open, unexpired, and
//      for this User and Patient (Rules 40, 41), the Role (Rule 38), the tokens (Rules
//      33, 34), the head (Rule 36), and for a signature the challenge (Rule 43) and
//      the PIN against the UserCredential as it stands at that moment, replaced or
//      suspended included (Rules 22, 27) — and all of it holds together, or nothing
//      lands.
//  43. A signature approves exactly what was shown. The Server issues the
//      SigningChallenge (Concept 17), naming the plan to be signed — content, base,
//      Patient. The Client shows it modally: sign as shown, or cancel and edit. The PIN
//      comes back with the challenge, and the commit checks that the plan submitted is
//      the plan named, then consumes it (Rule 42).
//  44. Within the signing transaction the Server reads the PatientDataPlatform once
//      more: where the Patient Data changed since the launch, the User is told and must
//      choose to proceed before the signature lands — Rule 21's pattern, for data
//      (Concept 2).
//  45. Every request that changes anything carries a key of its own. The Database
//      commits a key once: a retry returns the first result and never repeats the
//      change.
//
// Audit — the record of the acts around the record.
//  46. The Server appends to the audit, in the private store: every launch, honoured
//      or refused; every Session opening and ending, with the reason; every create;
//      every signature and every failed one; every PIN change; every refused request.
//      Append-only; who reads it is out of scope (Guarantee 4).
//
// ── Guarantees ─────────────────────────────────────────────────────────────────
// What the Rules add up to. Derived, not asserted: each holds because the Rules cited
// enforce it. Checked at the end of the run.
//
//  1. One constant. A PatientRecord is a sequence of TreatmentPlans in which the
//     PatientId is the only constant: the Patient Data, the orders and the ordering
//     User may all differ from one to the next (Concepts 12, 13, 15). Only a launch
//     supplies a PatientId (Concept 2) and no Session saves without one (Rules 12,
//     13), so no hand ever changes it.
//  2. One version. At any moment exactly one TreatmentPlan is the visible version and
//     the only starting point for updating it: the most recent Signed one (Rules 16,
//     17) — or, for its creator alone, their own Unsigned one where it is newer (Rules
//     18, 19). Nothing else can be built upon (Rule 20). Reading is wider than
//     building: the Signed history is open to read (Rule 17), and a User may still
//     look into their own superseded Unsigned work (Rule 18) — old versions and dead
//     ends the record keeps, from which nothing grows.
//  3. Carts and one checkout. Changing orders works like a shopping cart per User with
//     a single shared checkout — the cart being the WorkPlan (Concept 16). It is
//     private because of where it lives: in the User's own Client, and the Server keeps
//     none of it (Rule 31), and a User's Unsigned TreatmentPlans are closed to everyone
//     else, existence excepted (Rules 18, 21). Signing is the only checkout, and there
//     is one (Concept 14; Rules 16, 36): the first User to sign wins the version, and
//     every other WorkPlan must be rebuilt on top of it (Rules 19, 20; UC-6).
//  4. Audit. A Signed TreatmentPlan carries the User who signed it (Concepts 13, 14;
//     Rule 14), and every OrderContext in it carries the User whose Session last
//     changed it (Concept 10; Rule 14). The record keeps every version: append-only,
//     each TreatmentPlan with its base (Concepts 12, 13). That is a full audit trail of
//     every signed version of every OrderContext — held in the clinical store, which is
//     what the PatientDataPlatform copies (Actors 5, 6). Unsigned TreatmentPlans,
//     SessionRecords, UserCredentials and tokens live in the private store and are
//     never copied. Beside it stands the security audit (Rule 46). Reading either is
//     out of scope here: no Session shows them (Rule 17). What is guaranteed is that
//     the trail exists, complete, for whatever reads the copy — that nothing secret
//     rides along with it — and what a signature attests, said plainly: the holder of
//     the credential in an authenticated Session (Rules 22, 43), per credential, not
//     per person (UC-5). Non-repudiation is not claimed.
//
// ── Open Questions ─────────────────────────────────────────────────────────────
// Decisions not yet made. Each one blocks something.
//
//  1. Mail deliverability. Rule 26's guarantee — and the tamper evidence UC-7 is
//     built on — holds only if the UserRegistry address is current and the MailService
//     delivers. Neither can be checked from here. Blocks: the failure paths of UC-2
//     step 4 and UC-7.
//  2. Payload. Under Rule 31 the whole WorkPlan travels with every computing request
//     and every create. Whether that is acceptable is a measurement, not a judgement.
//     Blocks: nothing yet — but a bad number would force a server-side cache of the
//     WorkPlan, which must then be built as an optimisation the Rules never depend on,
//     losable without breaking anything.
//  3. A bound launch. The LaunchCredential is an unbound bearer code: nothing ties it
//     to the browser the LaunchScript opened, because the LaunchScript's only channel
//     to that browser is the URL itself. Rule 39 shrinks the theft window to the first
//     page load (UC-1 ext 7b); closing it needs the EHR side to run an authorisation
//     flow that can bind the transaction — SMART App Launch is the shape — and that
//     side is [given]. Blocks: retiring the race that remains in ext 7b.
//  4. Step-up signing. The PIN attests a credential holder, not a person (Guarantee
//     4). Attesting the person needs an authenticator GenPRES does not have — an
//     identity provider, WebAuthn, a smartcard — none of which exists as an actor here.
//     Rules 37, 43 and 27 are the interim. Blocks: claiming non-repudiation; retiring
//     the per-credential caveat of UC-5.
//  5. Finer patient authorisation. The launch is MainEHR's word that this User may
//     work on this Patient now (Concept 3), and GenPRES enforces nothing finer — no
//     care relationship, encounter, or co-sign requirement, because only MainEHR knows
//     them. Blocks: any rule finer than the Prescriber/Reader split.
//  6. A tamper-resistant audit. Rule 46's audit is append-only in the private store,
//     but the same administrator who runs the store could alter it, and its schema is
//     GenPRES's own, not HL7 AuditEvent. Blocks: audit that binds anyone but GenPRES.
//  7. Proof under concurrency. The Atomicity rules (40-45) are stated, not proven:
//     their invariants — once ended always ended, one open Session per User, no commit
//     after revocation or expiry, one result per key — deserve model checking before
//     the Guarantees are claimed under load. Blocks: nothing in the design; everything
//     in the confidence.
//
// ── What this model does not carry ─────────────────────────────────────────────
// The repository's Server is already stateless between requests, which is no longer a
// divergence but the specified design (Rule 31). What the model leaves out is
// deployment, and it leaves it out deliberately:
//
//   * Rule 11's transport. The SessionId is held by the Client and travels in the
//     request; that it rides in a cookie no script can read is a property of the
//     deployed Client, and nothing here turns on it.
//   * Rule 37's last sentence. Changing a PIN while you still know it is not modelled:
//     there is no change-PIN act, only the reset by mailed code.
//   * Rule 39's second half. Caching, referrers and third-party script are the
//     Server's serving of the Client, which this model does not have.
//   * The cryptography. `masterKey` is a string, macs are string equality, SessionIds
//     and plan ids are sequential, and PINs are held as typed. They are placeholders
//     that make forgery tests possible, not security properties — the real thing needs
//     standard, reviewed implementations, key rotation and constant-time comparison.
//   * Time. The clock advances one tick per handled message, so every lifetime here is
//     counted in messages and not in minutes.
//   * Open Question 7. One crafted interleaving is not state-space exploration: the
//     Atomicity invariants want FsCheck over the reducer and TLA+ over the commit
//     protocol before they are claimed under load.
//
// The rest of the file is in three parts:
//   1. types      — the vocabulary: identities, concepts, messages, actor state
//   2. modules    — the edge table, the Record rules, the tokens, and the reducer
//   3. scenarios  — the harness, UC-1 .. UC-13, and the derived assertions


// ═══════════════════════════════════════════════════════════════════════════════
//                                 1. TYPES
// ═══════════════════════════════════════════════════════════════════════════════

// ───────────────────────────── identities ─────────────────────────────

type UserId           = UserId of string            // stable key: what audit keys on
type LoginName        = LoginName of string         // unique today, but renameable
type MailAddress      = MailAddress of string       // from the UserRegistry (Rule 26)
type PatientId        = PatientId of string
type BrowserId        = BrowserId of int
type LaunchCredential = LaunchCredential of string  // Concept 4: opaque to GenPRES
type LaunchNo         = LaunchNo of int             // readable handle, safe to log
type SessionId        = SessionId of string         // Rule 11: bearer, never in a URL
type SessionNo        = SessionNo of int            // traces and ui only, never a key
type TreatmentPlanId  = TreatmentPlanId of string
type TreatmentPlanNo  = TreatmentPlanNo of int      // ordering within one PatientRecord
type OrderContextId   = OrderContextId of string    // Concept 10: persists across plans
type AttemptId        = AttemptId of int            // correlates one launch across ports
/// Rule 31. Correlates the several Database legs of ONE request. Created when the
/// request arrives, dropped with its reply — never carried from one request to the
/// next, which is the whole of what makes the Server stateless.
///
/// Mostly a modelling artifact: this reducer has no call stack, so a request that
/// fans out to several legs needs something to say which answer belongs to which
/// request and how far it had got. In a real Server that is the request handler's own
/// async flow, and this id survives only as a correlation id in the logs and the audit
/// (Rule 46). It is not Rule 45's key — that one the Client mints and the Database
/// keeps — and it is not a SessionId: it names one exchange, never a User.
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

/// The ten Actors of the document, plus Environment — which is not a use case actor
/// but the world they run in: the clock, and starting and stopping infrastructure.
type ActorId =
    | User                              // Actor 7
    | MainEhrWorkstation                // Actor 1  [given]
    | MainEhrLaunchScript               // Actor 2  [ours]
    | GenPresClient of BrowserId        // Actor 3  [ours]
    | GenPresServer                     // Actor 4  [ours]
    | GenPresDatabase                   // Actor 5  [ours]
    | PatientDataPlatform               // Actor 6  [given]
    | Broker                            // Actor 8  [ours]
    | UserRegistry                      // Actor 9  [ours]
    | MailService                       // Actor 10 [given]
    | Environment

// ───────────────────────────── the concepts ─────────────────────────────

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

/// Concept 13. Where a Patient Data value came from and when it was read — so a
/// Signed TreatmentPlan can be explained from its own record rather than from a
/// platform that has since moved on (Concept 2, Rule 44).
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

/// Concept 3. The whole of what crosses the Broker port, and only as trustworthy as
/// whatever wrote it: a login to look up, and a Patient if one was active. No
/// verified identity, no Role.
type LaunchAssertion =
    {
        Login   : LoginName
        Patient : PatientId option
    }

/// Concept 7. Carries no Role of its own. It is keyed by UserId — the stable identity
/// the registry resolves a login to — because a login can be renamed and a credential
/// must not follow the name (Rule 27: the count is per person, across Sessions).
type UserCredential =
    {
        User         : UserId
        Pin          : Pin option
        AttemptCount : int              // Rule 27: counts across Sessions
        /// Rule 27. Reached the limit, and stays there: no PIN signs anything until a
        /// Rule 37 replacement clears it. A count alone could be walked back by
        /// nothing more than time.
        Suspended    : bool
    }

/// Rule 37. A reset in flight. It lives in the Database and nowhere else — the
/// Server holds nothing between requests (Rule 31) — and it holds the code as a mac,
/// so what is stored is not what was mailed. `Wrong` is the code's own count, kept
/// apart from the credential's (Rule 27): guessing at a code must not lock a PIN that
/// is still perfectly good.
type PendingReset =
    {
        User    : UserId
        CodeMac : string
        Expires : int
        Wrong   : int
    }

/// Concept 10. It has an identity that persists across TreatmentPlans, a PatientId it
/// belongs to, whatever the User is putting into it, and the stamp: the UserContext
/// of the User whose Session last changed it (Rule 14).
///
/// Abstracted here: the document's OrderContext is a PatientContext together with the
/// OrderScenarios under consideration (Concepts 10, 11). None of the clinical content
/// is modelled — `Content` stands in for the whole of it, opaque like PatientData.
/// What the Rules turn on is only that it has an identity, names a Patient
/// (Guarantee 1, Rule 32), can be compared with the base to tell changed from
/// unchanged (Rule 35), and carries a stamp.
///
/// The cart is client-held (Rule 31), so all four fields arrive from the Client with
/// every request — and the Server trusts exactly two of them: `Id` and `Content`.
/// `Patient` is checked against the SessionRecord and `Stamp` is recomputed.
type OrderContext =
    {
        Id      : OrderContextId
        Patient : PatientId option
        Content : string
        Stamp   : UserContext option
    }

/// Concept 16. What the User is working on, as one thing: the Patient Data they
/// have entered and the OrderContexts they are putting together. It is mutable and
/// unattributed — no stamps, no identity, no No — it appears in no record, and it
/// dies with the browser. Nothing here is history until a TreatmentPlan is created
/// from it (Concept 14).
///
/// It lives in the Client and nowhere else (Rule 31), so it travels with every
/// request; the Server computes on it and keeps none of it.
type WorkPlan =
    {
        Data   : PatientData option
        /// Concept 13. Where that data came from, carried so the TreatmentPlan created
        /// from this WorkPlan can record it.
        From   : DataSource option
        Orders : OrderContext list
    }

/// Concept 13. Signed or Unsigned (Rule 15), by exactly one User (Rule 14), over the
/// TreatmentPlan it was created from — its base — if any.
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
        Signed  : bool
        At      : int
    }

/// Concept 12. Append-only. Newest first, so the Record rules are `List.tryFind`.
/// The PatientId is the one thing no TreatmentPlan may change (Guarantee 1).
type PatientRecord =
    {
        Patient : PatientId
        Plans   : TreatmentPlan list
    }

// ───────────────────────────── the tokens ─────────────────────────────

/// Concept 17. Every token GenPRES issues is the same object: a claim the Server
/// states, and a mac over it that only the Server can compute. What differs between
/// them is the purpose and what the claim names — never the shape, never the
/// checking. One master secret, one subkey per purpose, so a token minted for one
/// purpose can never be spent as another: it fails by key, before any field is
/// compared.
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

/// What a token names. `Names` is the one field whose reading depends on the
/// purpose, and the purpose is inside the claim, so nothing can be read one way and
/// signed another.
type Claim =
    {
        Purpose   : TokenPurpose
        Sid       : SessionId
        Patient   : PatientId option
        /// What the token names, as text, because what is named differs by purpose.
        /// Opened: the TreatmentPlan the Session opened with, if any. Notice: every
        /// Unsigned TreatmentPlan the notice disclosed. Challenge: the digest of the
        /// WorkPlan being signed. DataNotice: the digest of the Patient Data shown.
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

/// Rule 33. The TreatmentPlan a Session opened with, as something the Client can hold
/// and hand back but cannot make. Minted at the opening of the Session and re-minted
/// whenever the baseline moves — an open (Rule 17) or a create — because Rules 20 and
/// 21 are both measured from it.
type OpenedToken = Token

/// Rule 34. The User's choice to create anyway, as something that names exactly what
/// they were shown: honoured for those Unsigned TreatmentPlans and for nothing newer.
type NoticeToken = Token

/// Rule 43. What the Server states about a signature before it is taken: this
/// Session, this Patient, and this exact WorkPlan. Returned with the PIN, and honoured
/// only for the plan it names — so a signature can never land on work the User was not
/// shown.
type SigningChallenge = Token

/// Rule 44. The Patient Data as the platform had it at the signature, shown to the
/// User and accepted by them. Returned with the create, the same way.
type DataNoticeToken = Token

/// Rule 42. What a create carries: the whole WorkPlan (Rule 31), the tokens that make
/// it worth believing (Rules 33, 34, 43, 44), the PIN if it is a signature (Concept
/// 14) and the key that makes a retry safe (Rule 45). One record, because it is one
/// act — everything in it is decided together or not at all.
type CreateRequest =
    {
        Work      : WorkPlan
        Opened    : OpenedToken
        Notice    : NoticeToken option
        Challenge : SigningChallenge option
        DataOk    : DataNoticeToken option
        Pin       : Pin option
        Key       : IdemKey
    }

/// Rule 42. The create as the Database sees it: the request as it arrived, plus the
/// two things only the Server can have found out — the Role it has just re-taken
/// (Rule 38) and the Patient Data it has just re-read (Rule 44). Everything else is
/// re-established inside the act, from the Database's own state.
type Commit =
    {
        Sid   : SessionId
        Req   : CreateRequest
        Role  : Role option
        Fresh : PatientData option
    }

// ───────────────────────────── session state ─────────────────────────────

/// Rule 9, exactly: the four ways a Session ends, and no others. A Server restart is
/// not among them — the Server holds no Session state to lose (Rule 31).
type EndMark =
    | ClosedByUser
    | Idle
    | Superseded
    | WrongPinLimit

/// Two states. `OpenOrGone` also covers "the Client has gone quiet and the Server
/// cannot yet tell" — Rule 9 says a vanished browser is indistinguishable from a
/// silent one, so there is nothing finer to record.
type SessionState =
    | OpenOrGone
    | Ended of mark: EndMark * at: int

/// Rule 10, as a state rather than a timestamp. `int option` could not tell "no
/// notice is owed" apart from "one is owed and not yet given": a Session the User
/// closed themselves is owed nothing at all (Rule 10 speaks only of endings other
/// than by the User), while one that idled out is. Orthogonal to *how* a Session
/// ended — being told is not a way for a Session to end.
///
/// "Notice" is the document's own noun for this — "the notice comes with the new
/// launch", "the notice is not repeated", "no notice at the next launch", "a harmless
/// notice". Not `Notification`: in the document that word is the heading of the Rules
/// group holding Rule 21 — the notice that another User's Unsigned work exists, which
/// is a different thing entirely and is carried here by `UnsignedWorkNotice`.
type SessionNotice =
    /// The Session is open, or the User closed it themselves. Nothing is owed.
    | NotOwed
    /// It ended in a way the User has not been told about, and will be at the next
    /// opportunity: a Client still holding that SessionId, or the User's next launch.
    | Owed
    /// Put in front of the User. Rule 10 delivers at least once — the Server cannot
    /// know a Client showed anything (Consequence 6) — so a notice that was delivered
    /// and not acknowledged may be delivered again.
    | Delivered of at: int
    /// The User said they had seen it. After this it is never shown again.
    | Acknowledged of at: int

/// Concept 9 — the record of a Session, and now the whole of what GenPRES remembers
/// of one between requests (Rule 31). Lives in the Database, is kept after the
/// Session ends, and the Server is its only writer.
///
/// It carries the UserContext, not merely the UserId: the Role a Session runs under
/// is the one its launch established (UC-13 ext 1a), and Rule 32 takes the User of a
/// request from here rather than from the payload. Signing is the exception — Rule 38
/// re-takes the Role from the registry at every signature — but everything else a
/// Session does runs on the Role recorded here. The mail address rides along for
/// the same reason — Rule 26 has to reach the User with no Session in memory to ask.
type SessionRecord =
    {
        Id       : SessionId
        No       : SessionNo
        /// None: the Session was anonymous (Rule 13).
        User     : UserContext option
        Mail     : MailAddress option
        Patient  : PatientId option
        Launch   : LaunchNo option      // None: no launch — an anonymous open
        OpenedAt : int
        /// Rule 13. When an anonymous Session stops, come what may. `None` for a
        /// Session with a User: those end by Rule 9's four ways, one of which is the
        /// idle clock (Rule 41), and not by the calendar.
        ExpiresAt : int option
        /// Rule 8: every request from the Client refreshes this. The idle clock lives
        /// here because there is nowhere else for it to live.
        LastSeen : int
        State    : SessionState
        /// Rule 10. Set by `endWith`, so the obligation is created by the same act
        /// that creates the ending and cannot drift from it.
        Notice   : SessionNotice
    }



// ───────────────────────────── failures ─────────────────────────────

/// Rule 42. Why a commit changed nothing. Each of these is one of the rules the act
/// re-establishes, and the act stops at the first that fails — the PIN last, so a
/// doomed create never costs an attempt (Rule 27).
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
    /// Rule 44. The platform's Patient Data is not what is being signed over.
    | DataChanged of PatientData
    /// Rules 22, 27.
    | PinWrong of left: int
    | PinLimitReached
    /// Rule 27. The credential reached the limit in some earlier Session and stays
    /// suspended until a Rule 37 replacement.
    | CredentialSuspended


type LaunchFailure =
    | NotFound
    | CredentialExpired                 // Rule 3
    | AlreadyRedeemed                   // Rule 2
    | BrokerUnreachable                 // UC-1 ext 8b

/// Rule 37. Why a code bought nothing. Told apart because they mean different things
/// to the User: ask again, or look again at the mail.
type ResetFailure =
    | NoResetPending
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

// ───────────────────────────── messages ─────────────────────────────

/// What travels from the Client to the Server inside a Session. Every one of these
/// arrives as a `SessionRequest`, so Rule 8's idle-clock refresh has exactly one home
/// — and every one of them is answered out of its own payload plus the SessionRecord,
/// with nothing kept afterwards (Rule 31).
type SessionCmd =
    /// Concept 15. The Client has already changed its own cart; this sends the whole
    /// of it for computing. The answer comes back from the payload, and the Server
    /// keeps none of it.
    | Compute of OrderContext list
    /// Concept 14. Saving and Signing are one act. No PIN saves — the TreatmentPlan is
    /// Unsigned. A PIN signs — Signed, if everything the commit re-establishes holds
    /// (Rule 42). The whole WorkPlan travels, with every token the Server has issued
    /// about it (Rules 33, 34, 43, 44) and the key that makes a retry safe (Rule 45).
    | CreateTreatmentPlan of CreateRequest
    /// Rule 43. Asks for the challenge a signature will have to carry. The Rule 20 and
    /// 21 answers are settled here, before the User is ever asked for a PIN (UC-3 ext
    /// 3c), and the challenge names the exact WorkPlan it was asked about.
    | RequestSignChallenge of WorkPlan * OpenedToken * NoticeToken option
    | OpenTreatmentPlan of TreatmentPlanId        // Rules 17, 18
    /// UC-7. Rule 37: this removes nothing. It asks for a code to be mailed.
    | ResetPin
    /// Rule 37. The code from the mail and the PIN it is to be replaced with —
    /// verified and replaced in one act, so there is never a PIN-less moment.
    | SupplyResetCode of ResetCode * Pin
    | CloseSession                      // Rule 9

/// What the User does at the Client. Distinct from `SessionCmd`: some of these are
/// purely local (the cart is the Client's), and every one that does reach the Server
/// carries the cart with it.
///
/// There is no `Proceed` and no `HoldOff`. Under Rule 34 proceeding is re-sending the
/// create with the token the notice came with, and holding off is not sending it.
type UserAct =
    | Prescribes of OrderContextId      // Concept 15: add or change, in the Client
    | EntersPatientData of PatientData  // Concept 2: the User supplies it by hand
    | Saves                             // Concept 14, Unsigned
    | Signs of Pin                      // Concept 14, Signed if it verifies
    /// Rule 43. The User leaves the signature modal without signing.
    | CancelsSign
    | OpensTreatmentPlan of TreatmentPlanId       // Rules 17, 18
    | AsksPinReset                      // UC-7
    /// UC-7 step 3. The User has read the mail and chooses the new PIN.
    | EntersResetCode of ResetCode * Pin
    | ClosesSession                     // Rule 9
    /// Rule 10. The User dismisses the notice that a Session ended.
    | AcknowledgesNotice
    /// UC-9 step 5. The cart survived the Session because it was never in the Server
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
    /// UC-1 ext 3a. The one launch failure the EHR side can report: the Broker edge is
    /// request-response, and the LaunchScript has not yet exited. Its reporting ends
    /// with its own acts — after the launch it learns nothing (Consequence 1).
    | LaunchError of string
    // ── C3. MainEHR LaunchScript <-> Broker.  No Role: the launch carries no rights. ──
    | PrepareLaunch of LoginName * PatientId option
    | LaunchPrepared of LaunchCredential
    | LaunchNotPrepared
    // ── C4. MainEHR LaunchScript => GenPRES Client.  One-way: Consequence 1. ──
    | OpenUrl of LaunchCredential
    // ── U3. User <-> GenPRES Client ──
    | Refresh                           // retry the launch from the page's own memory
    /// Rule 39. The page goes and comes back: its memory is gone with it, and only
    /// the address bar is left to re-present.
    | ReloadPage
    | OpenDirectly                      // UC-8: no launch, no credential
    | AcceptAnonymousOffer              // Rule 6, UC-1 ext 9a
    | ChoosePin of Pin                  // UC-2 step 3, mid-launch
    | Act of UserAct
    | CloseBrowser                      // UC-12 ext 2a: nothing reaches the Server
    // ── C5. GenPRES Client -> GenPRES Server ──
    | RedeemLaunch of LaunchCredential
    | OpenAnonymous                     // Rule 13
    | SupplyPin of AttemptId * Pin      // UC-2: the launch is suspended on a human
    /// Rule 10. The User says they have seen the notice about an ended Session. Not a
    /// `SessionRequest`: the Session it speaks of has ended, and a request in it would
    /// be refused.
    | AckSessionNotice of SessionId
    | SessionRequest of SessionId * SessionCmd
    // ── C6. GenPRES Server <-> Broker ──
    | ResolveLaunch of AttemptId * LaunchCredential
    | LaunchResolved of AttemptId * LaunchNo * LaunchAssertion
    | LaunchRejected of AttemptId * LaunchNo option * LaunchFailure
    // ── C7. GenPRES Server <-> UserRegistry.  The credential never reaches here. ──
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
    | WriteCredential of LegTag * UserCredential
    | CredentialWritten of LegTag * UserCredential
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
    /// Rule 42. The whole create, as one act at the Database: every rule it turns on
    /// is re-established there, against the state as it stands, and the TreatmentPlan
    /// lands or nothing happens at all. Rule 36 is inside it now — the check and the
    /// append cannot be separated, because they are the same act.
    | CommitTreatmentPlan of LegTag * Commit
    | TreatmentPlanCommitted of LegTag * TreatmentPlan
    | CommitRefused of LegTag * CommitRefusal
    /// Rule 40. The Server never writes back a SessionRecord it read: it names the
    /// change it wants and the Database decides whether the record is still in a state
    /// that allows it. An Ended record can never come back open, whatever raced with
    /// what.
    | OpenSessionClosingOthers of SessionRecord   // Rule 7, in one act
    | EndSessionIfOpen of SessionId * EndMark
    | TouchIfOpen of SessionId                    // Rule 8
    | MarkDelivered of SessionId                  // Rule 10, at least once
    | MarkAcknowledged of SessionId               // Rule 10, and then never again
    | ReadSessionRecord of LegTag * SessionId
    | SessionRecordRead of LegTag * SessionRecord option
    | ReadSessionRecords of LegTag
    | SessionRecordsRead of LegTag * SessionRecord list
    // ── C10. GenPRES Server -> MailService ──
    | SendMail of MailAddress * string
    // ── GenPRES Server -> GenPRES Client (replies only: Consequence 6) ──
    | SessionOpened of
        SessionId * SessionNo * UserContext option * PatientContext * OrderContext list * OpenedToken
    | PinRequired of AttemptId          // UC-2: choose one, and nothing else is offered
    | LaunchRefused                     // carries no reason, deliberately
    | NotAuthorised                     // the registry says no; no reason either
    | AuthorityUnavailable              // the registry cannot say
    | ServerUnreachable
    /// Rule 10's one telling. The mark is what ended it.
    | SessionEnded of EndMark option    // None: the Server has no such record
    /// The request is refused because the Session is not open — but the User has
    /// already been told why, and Rule 10 says never twice.
    | SessionRefused
    /// Rule 10. What ended, and — so the User can say they have seen it — which
    /// Session it was. The SessionId of an ended Session is no longer a bearer
    /// credential for anything (Rule 11 is about what it may open, and this opens
    /// nothing); it only names what is being acknowledged.
    | PriorSessionNotice of (SessionNo * SessionState * SessionId) list
    /// Rule 31. The answer to `Compute`, computed from the payload and kept nowhere.
    | Computed of OrderContext list
    /// Rules 20, 36. Whose work stands in the way — never which TreatmentPlan it is
    /// (Rules 17, 18, 21).
    | CreateBlocked of UserContext
    /// Rule 21: whose work, not its contents. Rule 34: and the token that names what
    /// was disclosed, which is what a choice to create anyway must return.
    | UnsignedWorkNotice of UserContext * NoticeToken
    /// Rules 32, 33. The payload contradicted the SessionRecord, or the token did not
    /// verify. Carries a reason for the trace; the Client shows nothing but a refusal.
    | CreateRefused of string
    | TreatmentPlanCreated of TreatmentPlanId * bool * OpenedToken
    /// Rule 43. The challenge to sign with, over the WorkPlan it was asked about.
    | SignChallengeIssued of SigningChallenge
    /// Rule 44. The Patient Data has moved under the Session (Concept 2 read it once,
    /// at the launch). Shown, and accepted by returning the token.
    | PatientDataChanged of PatientData * DataNoticeToken
    | TreatmentPlanOpened of TreatmentPlanId * OrderContext list * OpenedToken
    | PinRejected of int                // Rule 27: attempts left
    | NoTreatmentPlanHere                    // Rule 12
    | NotPermitted                      // Roles: a Reader never creates a TreatmentPlan
    /// Rule 38. The registry could not be asked, so the Role could not be re-taken and
    /// nothing was signed. Distinct from `AuthorityUnavailable`, which belongs to a
    /// launch and offers an anonymous open: here there is a Session already, and it
    /// stands.
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

// ───────────────────────────── actor state ─────────────────────────────

/// The Broker's own record. It has a lifecycle — issued when, spent or not — that no
/// message carries. GenPRES never sees it, only the LaunchAssertion projected from
/// it, which deliberately drops the credential and the spent flag.
type LaunchRecord =
    {
        Credential : LaunchCredential
        No         : LaunchNo
        Login      : LoginName
        Patient    : PatientId option
        IssuedAt   : int
        Redeemed   : bool
    }

/// Actor 1 [given]. Invariant 1: at most one active Patient at a time.
type WorkstationState =
    {
        ActiveUser    : LoginName option
        ActivePatient : PatientId option
        NextTab       : int
    }

/// Actor 8. Under SMART on FHIR this would be the EHR's authorisation server.
type BrokerState =
    {
        Launches : Map<LaunchCredential, LaunchRecord>
        NextNo   : int
        Up       : bool
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

/// Actor 5, the half of it that is the point: the attested history of a Patient.
/// Signed TreatmentPlans and nothing else — no Session, no credential, no draft. This
/// is the half a copy could be handed to other systems (Open Question 2), which is why
/// it is a type of its own rather than a filter somebody has to remember to apply.
type ClinicalStore =
    {
        /// Concept 12, the Signed part: newest first per Patient.
        Signed : Map<PatientId, TreatmentPlan list>
    }

/// Actor 5, the other half: everything that is GenPRES's own business. Unsigned work
/// belongs to the User who made it and to nobody else (Rule 18); the rest is
/// machinery — who is in a Session, what a credential is, which codes and keys and
/// tokens have been spent — and none of it is a record of care.
type PrivateStore =
    {
        /// Concept 12, the Unsigned part. Newest first per Patient.
        Drafts       : Map<PatientId, TreatmentPlan list>
        Sessions     : SessionRecord list             // Concept 9
        Credentials  : Map<UserId, UserCredential>    // Concept 7, keyed by the person
        /// Rule 37. Resets in flight, gone the moment the code is spent, expires or is
        /// guessed away.
        Resets       : Map<UserId, PendingReset>
        /// Rule 45. What each key has already been answered with.
        Answered     : Map<IdemKey, Result<TreatmentPlan, CommitRefusal>>
        /// Concept 17 and Rules 33, 43. The nonces of tokens already spent — the only
        /// residue of a token the Server side keeps, and the whole of what makes one
        /// work exactly once. Bounded by the token lifetime: a mark older than that
        /// can be purged, because an expired token is refused anyway.
        Spent        : Set<string>
        /// Rule 46. What was done, and to whom.
        Audit        : string list
    }

/// Actor 5. The Server is its only writer.
///
/// `NextPlan` lives here, not in the Server: Rule 42 makes the Database the party that
/// decides whether a create lands, so it is also the party that can hand out an
/// ordering. More than one Server may run; only one Database does.
type DatabaseState =
    {
        Clinical : ClinicalStore
        Private  : PrivateStore
        NextPlan : int
    }

/// One launch attempt, mid-flight. The stages follow UC-1's trace, and the order is
/// the document's, not a convenience:
///   Rule 24  the PIN is offered only after the registry has recognised the login
///   Rule 25  a Reader skips the credential stage entirely
///   ext 1a   no Patient: the platform and record stages are skipped
///   ext 11a  the platform being unreachable is not a failure
/// The credential is handed to the Broker and not kept: after ResolveLaunch, GenPRES
/// holds only the launch number, which is safe to log and safe to store.
type LaunchCtx =
    {
        Client    : ActorId
        Launch    : LaunchNo
        Assertion : LaunchAssertion
    }

/// A launch in flight, with the tick it reached this stage. The tick is what makes an
/// abandoned launch collectable: everything here is waiting on a round trip that
/// should return promptly, except AwaitingPinChoice, which waits on a human (UC-2).
///
/// This table has always had the shape Rule 31 asks for: per-attempt, and nothing
/// retained once the reply goes out. It is not Session state.
type PendingLaunch =
    | AwaitingAssertion   of client: ActorId
    | AwaitingUser        of LaunchCtx
    | AwaitingCredential  of LaunchCtx * UserContext * MailAddress
    /// UC-2. The launch is suspended on a human and may stay here indefinitely.
    | AwaitingPinChoice   of LaunchCtx * UserContext * MailAddress
    | AwaitingPinWritten  of LaunchCtx * UserContext * MailAddress
    | AwaitingPatientData of LaunchCtx * UserContext * MailAddress
    | AwaitingRecord      of LaunchCtx * UserContext * MailAddress * PatientContext
    /// Rule 7 needs the User's other SessionRecords, and the Server no longer mirrors
    /// them — so closing the rest is a Database leg like any other.
    | AwaitingPriors      of LaunchCtx * UserContext * MailAddress * PatientContext * TreatmentPlan option

/// One entry in the Server's launch table.
type PendingEntry =
    {
        Stage : PendingLaunch
        Since : int
    }

/// How far one in-Session request has got through its Database legs. Every stage
/// carries what the earlier legs returned, because there is nowhere else to keep it:
/// the Server holds nothing between requests (Rule 31), and this table is emptied by
/// the reply.
type RequestStage =
    /// Rule 32: before anything else, who and which Patient this Session is.
    | AwaitingSessionRecord
    /// Rules 17 to 21 are decided against the PatientRecord: an open (Rules 17, 18),
    /// and the pre-checks a challenge is issued after (Rule 43).
    | AwaitingPatientRecord of SessionRecord
    /// Rule 38. A signature is a fresh act of authority, so the Role is taken from the
    /// registry again — every time, and before the PIN is ever asked for.
    | AwaitingSigningRole   of SessionRecord * CreateRequest
    /// Rule 44. And the Patient Data is re-read, so a signature cannot attest to data
    /// the platform has since moved on from.
    | AwaitingFreshData     of SessionRecord * CreateRequest * Role
    /// Rule 42: the Database is deciding the whole create, in one act.
    | AwaitingCommit        of SessionRecord
    /// UC-7 step 2. Rule 26 mails the address on the record, so the record is held —
    /// and the code rides along, because it is the Server that mails it and the
    /// Database that only ever saw its mac.
    | AwaitingResetStarted  of SessionRecord * ResetCode
    /// UC-7 step 3. Rule 26 again: the replacement is mailed and recorded.
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

/// Actor 3. Carries no identity of its own: a User is only known through a launch.
/// It does carry the work, though — the cart is here and nowhere else (Rule 31), so
/// this is where a Session's contents survive a Server restart, and where they die
/// when the browser does.
type BrowserState =
    {
        /// Consequence 4: the credential arrives in the address bar. Rule 39: it is
        /// erased there the moment the Client presents it, so a reload finds nothing.
        UrlCredential  : LaunchCredential option
        /// Rule 39. What is left after the scrub: a copy in the page's own memory,
        /// enough for the retry of UC-1 ext 7a, and gone with the page.
        RetryCredential : LaunchCredential option
        /// Rule 11: a bearer credential, held here and sent in the request.
        Sid            : SessionId option
        /// What the Server said this Session's User and Patient are (Concepts 1, 2).
        /// Shown to the User; never sent back as an assertion — Rule 32 takes both
        /// from the SessionRecord.
        User           : UserContext option
        Patient        : PatientId option
        /// Concept 16. The WorkPlan travels with every request and lives nowhere else.
        Work           : WorkPlan
        /// Rule 33. Issued by the Server, returned with every create.
        Opened         : OpenedToken option
        /// Rule 34. Kept from the last UnsignedWorkNotice, returned to create anyway.
        Notice         : NoticeToken option
        /// Rule 43. A signature the User has started: the PIN they typed while the
        /// challenge is being fetched, and then the challenge itself. While the modal
        /// is up the WorkPlan cannot change — that is what it is for.
        Signing        : Pin option
        Modal          : SigningChallenge option
        /// Rule 44. The Patient Data notice the User has accepted, returned with the
        /// create the way a Rule 21 notice is.
        DataOk         : DataNoticeToken option
        /// Rule 10. The Sessions a notice in front of the User is about — one for an
        /// ended Session's own Client, possibly several at a launch that closed
        /// others. They are over, so naming them opens nothing; it only says what the
        /// User is acknowledging.
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

/// Every field is state owned by exactly one participant — Clients per key — except
/// Env, which is the world they all run in. Nothing is shared, so nothing in the
/// model can depend on a memory read across what will be a process boundary. The
/// rule is a convention over the reducer, not something the type enforces: only the
/// branch bodies decide who reads what.
type Hospital =
    {
        Workstation : WorkstationState
        Broker      : BrokerState
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

// ───────────────────────────── configuration ─────────────────────────────
// Rules 28, 29, 30. Owned by the actor named in the comment, not by the model.
// The unit is one handled message: `update` advances the clock on every move, so a
// lifetime has to be read against the length of a cascade, not a count of Ticks — a
// launch from trigger to open Session is some twenty-odd of them.

/// Rule 28. Long enough to carry one launch — a page load and a retry or two.
/// The Broker owns this; GenPRES never sees it.
let credentialTtl = 20

/// Rule 29. Long enough to span the gaps between a clinician's actions. The unit is
/// one handled message, and a clinician's gap here is a whole cascade — a launch, a
/// save, a colleague's Session running alongside — so this is far larger than
/// `credentialTtl`, which spans one page load.
let sessionTtl = 150

/// Rule 13. An anonymous Session has no idle clock — nobody is waiting to be told
/// anything and nothing of theirs is at stake — but it does not live for ever either:
/// this is the outright limit, counted from the open.
let anonymousLifetime = 1000

/// How long a launch may sit half-finished before the Server forgets it (UC-2). Not
/// Rule 29's number: this is a round trip that should return promptly, not a
/// clinician's gap — except `AwaitingPinChoice`, which waits on a human and is never
/// collected.
let launchAbandonTtl = 25

/// Rule 30. Small enough to make guessing hopeless, large enough to forgive
/// mistyping. Owned by GenPRES.
let wrongPinLimit = 3

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

// ───────────────────────────── the edge table ─────────────────────────────

/// The document's notation, as data.
type EdgeKind =
    | Request                           // X ->  Y   initiate, and receive Y's reply
    | Launch                            // X =>  Y   one-way: no response, no error path
    | Interact                          // X <-> Y   read what Y shows, and act on it

module Edges =

    /// The document's Constraints section, verbatim. Anything not here cannot
    /// exchange data at all, and edges do not compose.
    let table : (ActorId * EdgeKind * ActorId) list =
        [
            // User Interaction
            User,                 Interact, MainEhrWorkstation          // U1
            User,                 Interact, MainEhrLaunchScript         // U2
            User,                 Interact, GenPresClient(BrowserId 0)  // U3

            // Communication
            MainEhrWorkstation,   Request,  UserRegistry                // C1
            MainEhrWorkstation,   Request,  PatientDataPlatform         // C2
            MainEhrLaunchScript,  Request,  Broker                      // C3
            MainEhrLaunchScript,  Launch,   GenPresClient(BrowserId 0)  // C4
            GenPresClient(BrowserId 0), Request, GenPresServer          // C5
            GenPresServer,        Request,  Broker                      // C6
            GenPresServer,        Request,  UserRegistry                // C7
            GenPresServer,        Request,  PatientDataPlatform         // C8
            GenPresServer,        Request,  GenPresDatabase             // C9
            GenPresServer,        Request,  MailService                 // C10

            // U2 is `<->` and not `=>`: the LaunchScript reports the one failure it
            // can see — the Broker exchange — before it exits (UC-1 ext 3a). What
            // bounds it is not the edge but its own lifetime: it exits at the launch,
            // so nothing later ever comes from it (Consequence 1). The edge that
            // carries Consequence 1 is C4, which stays `=>`.
        ]

    /// Clients differ by BrowserId; edges do not.
    let private tag =
        function
        | GenPresClient _ -> GenPresClient(BrowserId 0)
        | a -> a

    let private has kind a b =
        table |> List.exists (fun (x, k, y) -> k = kind && x = tag a && y = tag b)

    /// May `from` put this envelope on the wire to `to_`?
    ///
    /// A Request edge permits both the initiation and the reply that comes back on
    /// the same connection. A Launch edge permits the one direction only — which is
    /// what makes Consequence 1 true by construction: nothing can ever be sent back
    /// to the LaunchScript, because no edge carries it.
    let permits from to_ =
        // Environment is not a use case actor: it is the clock and the power switch,
        // and every actor may write to the audit log.
        if from = Environment || to_ = Environment then true
        elif has Interact from to_ || has Interact to_ from then true
        elif has Launch from to_ then true
        elif has Request from to_ then true
        elif has Request to_ from then true          // the reply leg
        else false

// ───────────────────────────── the work plan ─────────────────────────────

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

    /// Rule 44. The same, for the Patient Data alone.
    let dataDigest (d: PatientData option) =
        match d with
        | Some(PatientData x) -> $"sha|%s{x}"
        | None -> "sha|-"

// ───────────────────────────── the record rules ─────────────────────────────

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

    /// Rule 16. The only TreatmentPlan that counts clinically.
    let latestSigned (r: PatientRecord) =
        r.Plans |> List.tryFind _.Signed

    /// Rule 19. Where neither exists, the User works from nothing. A Reader never
    /// creates a TreatmentPlan, so this can only ever hand them the latest Signed one.
    let startsFrom (u: UserId) (r: PatientRecord) =
        r.Plans
        |> List.tryFind (fun s -> s.Signed || s.By.UserId = u)

    /// Rules 17 and 18. Every Signed TreatmentPlan is readable, by anyone who may see
    /// the Patient: it is attested history, and history is what a record is for. An
    /// Unsigned one opens only for the User who created it (Rule 18) — it is nobody
    /// else's work to read.
    ///
    /// Reading an older Signed TreatmentPlan is not a way to build on it: opening one
    /// makes it the TreatmentPlan the Session opened with, and Rule 20 then blocks a
    /// create, because a newer Signed one exists. Read-only falls out of the baseline,
    /// with no second mechanism to keep in step with the first.
    let mayOpen (u: UserId) (id: TreatmentPlanId) (r: PatientRecord) =
        match r.Plans |> List.tryFind (fun s -> s.Id = id) with
        | None -> None
        | Some s when s.Signed -> Some s
        | Some s -> if s.By.UserId = u then Some s else None

    /// Rule 20. A Signed TreatmentPlan newer than the one the User opened with blocks the
    /// create — and opening that Signed TreatmentPlan lifts the block, because it becomes
    /// the one the Session opened with.
    let blocking (openedWith: TreatmentPlanId option) (r: PatientRecord) =
        latestSigned r |> Option.filter (fun s -> newerThan openedWith s r)

    /// Rule 21, and Rule 34's half of it: *every* Unsigned TreatmentPlan of another User
    /// newer than the one opened with, because the notice token names what was
    /// disclosed and is honoured for nothing newer. Newest first.
    let unsignedElsewhere (u: UserId) (openedWith: TreatmentPlanId option) (r: PatientRecord) =
        r.Plans
        |> List.filter (fun s ->
            not s.Signed && s.By.UserId <> u && newerThan openedWith s r)

    /// Concept 12: append-only. The newest TreatmentPlan goes on the front, and no
    /// existing one is ever touched.
    let append (s: TreatmentPlan) (r: PatientRecord) =
        { r with Plans = s :: r.Plans }

// ───────────────────────────── the two stores ─────────────────────────────

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
    /// in the same act. Newest first.
    let note (what: string) (db: DatabaseState) =
        { db with Private.Audit = what :: db.Private.Audit }

    /// Concept 12: append-only, into whichever half the TreatmentPlan belongs to. A
    /// Signed one is history; an Unsigned one is its author's own business (Rule 18).
    let append (plan: TreatmentPlan) (db: DatabaseState) =
        if plan.Signed then
            { db with
                Clinical.Signed =
                    db.Clinical.Signed |> Map.add plan.Patient (plan :: signedOf plan.Patient db) }
        else
            { db with
                Private.Drafts =
                    db.Private.Drafts |> Map.add plan.Patient (plan :: draftsOf plan.Patient db) }

// ───────────────────────────── the credential ─────────────────────────────

/// Concept 7 and Rule 27.
module UserCredential =

    let fresh user = { User = user; Pin = None; AttemptCount = 0; Suspended = false }

    /// Rules 26 and 37: a newly set — or newly replaced — PIN starts with a count of
    /// zero, and lifts the suspension that count may have earned.
    let setPin pin c = { c with Pin = Some pin; AttemptCount = 0; Suspended = false }

    /// Rule 22: verification happens here and nowhere else. Rule 27: a correct entry
    /// resets the count, a wrong one advances it — and a suspended credential verifies
    /// nothing at all, correct PIN or not, until Rule 37 replaces the PIN.
    let verify (pin: Pin) (c: UserCredential) =
        if c.Suspended then false, c
        else
            match c.Pin with
            | Some p when p = pin -> true, { c with AttemptCount = 0 }
            | _ ->
                let tried = { c with AttemptCount = c.AttemptCount + 1 }
                { tried with Suspended = tried.AttemptCount >= wrongPinLimit } |> fun x -> false, x

    /// Rule 27: a wrong entry at the limit ends the Session (Rule 9) — and suspends the
    /// credential, so the next Session cannot simply try again.
    let atLimit c = c.Suspended || c.AttemptCount >= wrongPinLimit

    let attemptsLeft c = max 0 (wrongPinLimit - c.AttemptCount)

// ───────────────────────────── the reset code ─────────────────────────────

/// Rule 37. A PIN is never removed; it is replaced, and what authorises the
/// replacement is a code that went to an address the User controls and GenPRES got
/// from the registry (Rule 26). The Database holds the mac and not the code, so what
/// is stored is not what was sent — the same trick as a token (Concept 17), and the
/// same placeholder for a real one.
module Reset =

    let private secret = "reset-key-known-only-to-genpres"

    let macOf (ResetCode c) = $"mac|%s{secret}|reset|%s{c}"

    /// What the mail says. The code is in it — that is the whole point of the
    /// channel — and nothing else about the Session is.
    let mail (ResetCode c) = $"PIN reset: use code %s{c} once, and soon"

// ───────────────────────────── the session record ─────────────────────────────

module SessionRecord =

    /// Rule 10, on the one axis that decides it: a User who closed was offered the
    /// save, so there is nothing to tell them. Every other ending owes a notice.
    /// Three branches, not four: a Server restart is no longer an ending (Rule 9).
    let owesNotice =
        function
        | ClosedByUser -> false
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

    /// Rule 13. And the other way an anonymous Session ends: its outright limit, which
    /// no amount of use extends. Only anonymous Sessions carry one.
    let hasExpired (now: int) (s: SessionRecord) =
        isOpen s && (match s.ExpiresAt with Some at -> now > at | None -> false)

    let userId (s: SessionRecord) = s.User |> Option.map _.UserId

// ───────────────────────────── the tokens ─────────────────────────────

/// Rules 33 and 34. The Client holds the cart, so anything the Server must be able to
/// trust about a create has to be something the Client cannot forge. Both tokens are
/// the same trick: the Server states a fact, signs it, and refuses to believe the
/// fact unless the signature comes back with it.
///
/// `secret` stands in for the key. It is `private`, so nothing outside this module —
/// no scenario, no forgery test — can compute a mac. That is the point: the tests
/// below can build a token with the right fields and a wrong mac, and watch it fail.
module Token =

    /// The one configured secret. Deployment provides it — a mounted file or an
    /// environment variable — and every Server instance gets the same value, so any
    /// instance can verify any instance's token (Rule 36's several Servers).
    ///
    /// It is `private`, so nothing outside this module — no scenario, no forgery
    /// test — can compute a mac. That is the point: the tests below can build a
    /// token with the right fields and a wrong mac, and watch it fail. As a secret
    /// it is a placeholder and not a security property.
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
    /// baseline moves: an open (Rule 17) or a create both make a new TreatmentPlan the one
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

// ───────────────────────────── the reducer ─────────────────────────────

module Hospital =

    let empty =
        {
            Workstation = { ActiveUser = None; ActivePatient = None; NextTab = 1 }
            Broker      = { Launches = Map.empty; NextNo = 1; Up = true }
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
            UrlCredential = None
            RetryCredential = None
            Sid = None
            User = None
            Patient = None
            Work = WorkPlan.empty
            Opened = None
            Notice = None
            Signing = None
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

    /// Rule 14 says an OrderContext changed in the Session is stamped with the
    /// Session's User and an unchanged one keeps the stamp it had. With the cart in
    /// the Client (Rule 31) there is no Session to ask what changed, and no reason to
    /// believe a Client that says so — so the Server diffs the payload against the
    /// base TreatmentPlan by OrderContextId. Same id and same content: the base's stamp
    /// stands. New id, or changed content: this User's stamp. Whatever stamp arrived
    /// is discarded unread (Rule 35).
    let private stampAgainst (uc: UserContext) (basePlan: TreatmentPlan option) (orders: OrderContext list) =
        let baseline = basePlan |> Option.map _.Orders |> Option.defaultValue []
        orders
        |> List.map (fun o ->
            match baseline |> List.tryFind (fun b -> b.Id = o.Id) with
            | Some b when b.Content = o.Content -> { o with Stamp = b.Stamp }
            | _ -> { o with Stamp = Some uc })

    // ── the launch, and what ends it ──

    /// UC-1 steps 13 and 14, and the last step of the anonymous open. Rule 19 has
    /// already picked the TreatmentPlan the Session starts from, if there is one, and Rule
    /// 7's other Sessions of this User have already been read back from the Database
    /// — the Server keeps no copy of them (Rule 31).
    let private openSession
        (client: ActorId)
        (launch: LaunchNo option)
        (user: UserContext option)
        (mail: MailAddress option)
        (pctx: PatientContext)
        (start: TreatmentPlan option)
        (others: SessionRecord list)
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
                Launch = launch
                OpenedAt = h.Env.Now
                ExpiresAt = if user.IsNone then Some(h.Env.Now + anonymousLifetime) else None
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
            send GenPresServer GenPresDatabase (OpenSessionClosingOthers record)
            send GenPresServer client (SessionOpened(sid, no, user, pctx, orders, token))
            if not priors.IsEmpty then
                send GenPresServer client
                    (PriorSessionNotice(priors |> List.map (fun r -> r.No, r.State, r.Id)))
        ]

    /// UC-1 steps 11 and 12, and where they are skipped. A Reader arrives here
    /// straight from the registry (Rule 25); a Prescriber only once the PIN question
    /// is settled (Rules 23, 24).
    let private afterCredential att (ctx: LaunchCtx) uc mail (h: Hospital) =
        match ctx.Assertion.Patient with
        | None ->
            // ext 1a: no Patient, so no data to fetch and no record to read. Rule 7
            // still applies — this User's other Sessions close — so the SessionRecords
            // are still read.
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
            // Rule 46: every launch, honoured or refused. A refusal carries no reason
            // to the Client (deliberately) — the audit is where the reason goes.
            send GenPresServer Environment (Noted $"launch refused: %A{reply}")
            send GenPresServer client reply
        ]

    // ── creating a TreatmentPlan: the Server's part, which is small ──

    /// Rule 42. The Server gathers what only it can know — the Role it has just
    /// re-taken (Rule 38) and the Patient Data it has just re-read (Rule 44) — and
    /// hands the whole create to the Database as one act. It decides nothing itself:
    /// every rule the create turns on is re-established there, against the state as it
    /// stands (Rules 20, 21, 22, 27, 33, 34, 36, 40, 41, 43, 45).
    let private commit rid (ctx: RequestCtx) (r: SessionRecord) (req: CreateRequest) role fresh (h: Hospital) =
        h |> putFlight rid { ctx with Stage = AwaitingCommit r },
        [
            send GenPresServer GenPresDatabase
                (CommitTreatmentPlan(ForRequest rid, { Sid = r.Id; Req = req; Role = role; Fresh = fresh }))
        ]

    /// The SessionRecord has come back, the Session is open, and Rule 8's clock has
    /// been refreshed. This is where Rule 32 bites: the User and the Patient of the
    /// request are read off the record, and the payload is believed about nothing
    /// else. Concept 15 — what a User may do inside a Session, and what they may not.
    let private dispatch rid (ctx: RequestCtx) (r: SessionRecord) (h: Hospital) =
        let refuse msg = dropFlight rid h, [ send GenPresServer ctx.Client msg ]

        /// Rule 12: a Session without a PatientId lets the User prescribe, Patient
        /// Data included, but a TreatmentPlan cannot be opened or created.
        let withPatient f =
            match r.Patient with
            | None -> refuse NoTreatmentPlanHere
            | Some p -> f p

        /// Roles: a Reader may never create a TreatmentPlan. Rule 13: an anonymous Session
        /// has no User at all, so there is nobody to create as and nobody to sign as.
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
            // UC-7 step 2. Rule 37: nothing is removed. A one-time code goes to the
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
            // UC-7 step 3. The Server carries the answer no further than the Database:
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

        | RequestSignChallenge _ ->
            // Rule 43, and UC-3 ext 3c's order: the Rule 20 and 21 answers are settled
            // against the PatientRecord first, and only then is a challenge issued —
            // so the User is never asked for a PIN they were never going to spend.
            withPatient (fun p ->
                withPrescriber (fun _ ->
                    h |> putFlight rid { ctx with Stage = AwaitingPatientRecord r },
                    [ send GenPresServer GenPresDatabase (ReadRecord(ForRequest rid, p)) ]))

        | CreateTreatmentPlan req ->
            withPatient (fun p ->
                withPrescriber (fun uc ->
                    match req.Pin with
                    | None ->
                        // A save attests to nothing, so it needs neither the Role
                        // re-taken nor the data re-read: straight to the one act.
                        commit rid ctx r req (Some uc.Role) None h
                    | Some _ ->
                        // Rule 38. Signing is a fresh act of authority: the Role is
                        // taken from the registry again, now, and before anything else
                        // — so a signature nobody is entitled to costs no PIN attempt
                        // (Rule 27).
                        ignore p

                        h |> putFlight rid { ctx with Stage = AwaitingSigningRole(r, req) },
                        [ send GenPresServer UserRegistry (ResolveUser(ForRequest rid, uc.Login)) ]))

    // ══════════════════════════════════════════════════════════════════════════
    //  The reducer proper. Dispatch names the sender as well as the recipient, so
    //  every branch states who may send it. Whether the two may exchange anything
    //  at all was already settled by the edge table, in `run`, before we get here.
    // ══════════════════════════════════════════════════════════════════════════

    let rec update (h: Hospital) (env: Envelope) : Hospital * Envelope list =
        // every move takes a tick of time
        let h = { h with Env.Now = h.Env.Now + 1 }

        match env.From, env.To, env.Msg with

        // ── the audit log, and the person ──

        // Recorded, not acted on. Handled first, and never refused itself: refusing a
        // refusal would not terminate.
        | _, Environment, Refused e ->
            let line = $"REFUSED %A{e.From} -> %A{e.To}"
            { h with Database.Private.Audit = line :: h.Database.Private.Audit }, []

        | _, Environment, Noted what -> { h with Database.Private.Audit = what :: h.Database.Private.Audit }, []

        // A person reads what is sent to them; there is no state to change.
        | _, User, _ -> h, []

        // ── the clock ──

        // The clock is advanced by the prefix above, on this envelope like any other,
        // so a Tick adds nothing of its own: it exists to reach the Server, whose
        // sweep runs on nothing else.
        | Environment, Environment, Tick ->
            h, [ send Environment GenPresServer Tick ]

        // ── infrastructure ──

        | Environment, Broker, Stop _ -> { h with Broker.Up = false }, []
        | Environment, Broker, Start _ -> { h with Broker.Up = true }, []
        | Environment, UserRegistry, Stop _ -> { h with Registry.Up = false }, []
        | Environment, UserRegistry, Start _ -> { h with Registry.Up = true }, []
        | Environment, PatientDataPlatform, Stop _ -> { h with Platform.Up = false }, []
        | Environment, PatientDataPlatform, Start _ -> { h with Platform.Up = true }, []

        // Rule 9: a Server restart ends nothing. There is no Session state to lose —
        // identity and standing are in the SessionRecords, the work is in the Clients
        // (Rule 31). What does go is what was in flight at that instant: requests
        // half-way through their Database legs, and launches half-way through theirs.
        // Their Clients see the same silence as any other unreachable Server.
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

        // A Server that is down answers its clients and does nothing else. Ordering
        // matters twice over: this pair must sit above every other Server branch, and
        // the client-facing case must be the narrow one. A reply from the Broker, the
        // registry, the platform or the Database is an in-flight answer to a Server
        // that is gone — dropped, not answered. Ticks are dropped too: a down Server
        // runs no sweeps.
        | _, GenPresServer,
            (RedeemLaunch _ | OpenAnonymous | SupplyPin _ | SessionRequest _) when not h.GenPres.Up ->
            h, [ send GenPresServer env.From ServerUnreachable ]

        | _, GenPresServer, _ when not h.GenPres.Up -> h, []

        // ── Rule 9: the idle sweep ──

        // The clock a Session is swept against is on its SessionRecord (Rule 8), and
        // the records are in the Database (Rule 31), so the sweep is a read like any
        // other rather than a walk over something the Server holds.
        | Environment, GenPresServer, Tick ->
            let now = h.Env.Now

            // A launch nobody is coming back for. Every stage is waiting on a round
            // trip that should return promptly — except AwaitingPinChoice, which the
            // document suspends on a human and which may therefore sit for as long as
            // it likes (UC-2 step 3). Bounded by Rule 29's constant: Rule 28's belongs
            // to the Broker, and GenPRES never sees it.
            let abandoned (p: PendingEntry) =
                match p.Stage with
                | AwaitingPinChoice _ -> false
                | _ -> now - p.Since > launchAbandonTtl

            { h with
                GenPres.Pending =
                    h.GenPres.Pending |> Map.filter (fun _ p -> not (abandoned p)) },
            [ send GenPresServer GenPresDatabase (ReadSessionRecords ForSweep) ]

        | GenPresDatabase, GenPresServer, SessionRecordsRead(ForSweep, rs) ->
            // Rule 13: an anonymous Session need not idle out — keeping it has no
            // consequence — so only a Session bound to a User is swept.
            let now = h.Env.Now
            // Rule 13: an anonymous Session is not swept for idleness — keeping it has
            // no consequence — but it is swept when its outright limit passes.
            let stale =
                rs |> List.filter (fun r -> SessionRecord.hasIdledOut now r || SessionRecord.hasExpired now r)
            ignore now

            h,
            [ for r in stale -> send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, Idle)) ]

        // ── Actor 1: the MainEHR Workstation ──

        | User, MainEhrWorkstation, LogIn u -> { h with Workstation.ActiveUser = Some u }, []
        | User, MainEhrWorkstation, SelectPatient p -> { h with Workstation.ActivePatient = Some p }, []
        | User, MainEhrWorkstation, ClearPatient -> { h with Workstation.ActivePatient = None }, []

        // ── Actor 2: the MainEHR LaunchScript ──

        // Rule 1: the LaunchScript decides which MainEHR User may run it. It is a
        // script behind a button *in* the Workstation, so the login and the active
        // Patient are its own context, not something fetched over an edge — there is
        // no edge between Actors 1 and 2, and none is needed.
        //
        // Note what it does NOT do: it never asks about, decides on, or transmits a
        // Role. The launch carries a login and a Patient (Concept 3), and the Role
        // comes from the UserRegistry at the far end (Rule 5).
        | User, MainEhrLaunchScript, TriggerLaunch ->
            match h.Workstation.ActiveUser with
            | Some u ->
                // ext 1a: no active Patient is not an error. The launch goes without
                // one, and GenPRES opens with no Patient (Rule 12).
                h, [ send MainEhrLaunchScript Broker (PrepareLaunch(u, h.Workstation.ActivePatient)) ]
            | None ->
                h, [ send MainEhrLaunchScript User (LaunchError "no MainEHR login: nobody to launch as") ]

        | Broker, MainEhrLaunchScript, LaunchPrepared cred ->
            let tab = BrowserId h.Workstation.NextTab
            // The launch, and then the LaunchScript exits. Consequence 1 is not a
            // promise made here: edge C4 is `=>`, so nothing can be sent back at all.
            { h with Workstation.NextTab = h.Workstation.NextTab + 1 },
            [ send MainEhrLaunchScript (GenPresClient tab) (OpenUrl cred) ]

        // UC-1 ext 3a. The one launch failure the EHR side can report: the Broker
        // edge is request-response and the LaunchScript has not yet exited.
        | Broker, MainEhrLaunchScript, LaunchNotPrepared ->
            h, [ send MainEhrLaunchScript User (LaunchError "the Broker is unreachable — stay in MainEHR") ]

        // ── Actor 8: the Broker ──

        | MainEhrLaunchScript, Broker, PrepareLaunch _ when not h.Broker.Up ->
            h, [ send Broker env.From LaunchNotPrepared ]

        | GenPresServer, Broker, ResolveLaunch(att, _) when not h.Broker.Up ->
            h, [ send Broker env.From (LaunchRejected(att, None, BrokerUnreachable)) ]

        | MainEhrLaunchScript, Broker, PrepareLaunch(login, patient) ->
            let cred = LaunchCredential $"cred-%04i{h.Broker.NextNo}"
            let record =
                {
                    Credential = cred
                    No = LaunchNo h.Broker.NextNo
                    Login = login
                    Patient = patient
                    IssuedAt = h.Env.Now
                    Redeemed = false
                }
            { h with
                Broker.Launches = h.Broker.Launches |> Map.add cred record
                Broker.NextNo = h.Broker.NextNo + 1 },
            [ send Broker env.From (LaunchPrepared cred) ]

        // Rules 2, 3 and 4. Rule 4 is this branch's shape: only the Server appears in
        // the From position, so no other party can redeem.
        | GenPresServer, Broker, ResolveLaunch(att, cred) ->
            let reject no f = [ send Broker env.From (LaunchRejected(att, no, f)) ]
            match h.Broker.Launches |> Map.tryFind cred with
            | None -> h, reject None NotFound
            | Some l when l.Redeemed -> h, reject (Some l.No) AlreadyRedeemed          // Rule 2
            | Some l when h.Env.Now - l.IssuedAt > credentialTtl ->
                h, reject (Some l.No) CredentialExpired                                // Rule 3
            | Some l ->
                { h with
                    Broker.Launches = h.Broker.Launches |> Map.add cred { l with Redeemed = true } },
                [ send Broker env.From
                    (LaunchResolved(att, l.No, { Login = l.Login; Patient = l.Patient })) ]

        // ── Actor 9: the UserRegistry ──

        | GenPresServer, UserRegistry, ResolveUser(tag, _) when not h.Registry.Up ->
            h, [ send UserRegistry env.From (UserUnresolved(tag, RegistryUnreachable)) ]

        | GenPresServer, UserRegistry, ResolveUser(tag, login) ->
            match h.Registry.Users |> Map.tryFind login with
            | Some(uc, mail) -> h, [ send UserRegistry env.From (UserResolved(tag, uc, mail)) ]
            | None -> h, [ send UserRegistry env.From (UserUnresolved(tag, NoRole)) ]

        // ── Actor 6: the PatientDataPlatform ──

        // Concept 2: read once, at the launch. Whether it is down or simply holds
        // nothing for this Patient makes no difference to the caller (ext 11a).
        | GenPresServer, PatientDataPlatform, ReadPatientData(att, p) ->
            match (if h.Platform.Up then h.Platform.Data |> Map.tryFind p else None) with
            | Some d -> h, [ send PatientDataPlatform env.From (PatientDataRead(att, d)) ]
            | None -> h, [ send PatientDataPlatform env.From (PatientDataUnavailable att) ]

        // ── Actor 10: the MailService ──

        | GenPresServer, MailService, SendMail(addr, what) ->
            { h with Mail = (addr, what) :: h.Mail }, []

        // ── Actor 5: the GenPRES Database. The Server is its only writer. ──

        | GenPresServer, GenPresDatabase, ReadCredential(tag, user) ->
            h, [ send GenPresDatabase env.From (CredentialRead(tag, h.Database.Private.Credentials |> Map.tryFind user)) ]

        | GenPresServer, GenPresDatabase, WriteCredential(tag, c) ->
            { h with Database.Private.Credentials = h.Database.Private.Credentials |> Map.add c.User c },
            [ send GenPresDatabase env.From (CredentialWritten(tag, c)) ]

        | GenPresServer, GenPresDatabase, ReadRecord(tag, p) ->
            h, [ send GenPresDatabase env.From (RecordRead(tag, h.Database |> Database.recordOf p)) ]

        // Rule 36. The Rule 20 check and the append are one act, and this is where it
        // is made one: the Server states the head its check saw, and the TreatmentPlan
        // lands only if that is still the head. More than one Server may run — the
        // arbitration is here, not in an assumption that only one of them writes.
        //
        // Concept 12: append-only. Nothing already in the record is touched. The Id
        // and the ordering are minted here, because ordering a record is the same
        // authority as deciding what may join it.
        // ══════════════════════════════════════════════════════════════════════
        //  Rule 42. The create, as one act.
        //
        //  Everything the create turns on is re-established here, against the state as
        //  it stands and in one go: the Session (Rules 40, 41), who may create (Rules
        //  13, 25, 38), every token (Rules 32, 33, 34, 43, 44), what the record allows
        //  (Rules 19, 20, 21, 36) and last of all the PIN (Rules 22, 27). Last,
        //  because a create that was never going to land must not cost an attempt.
        //  Either a TreatmentPlan is appended and every mark that goes with it is
        //  written, or nothing happened at all.
        // ══════════════════════════════════════════════════════════════════════
        | GenPresServer, GenPresDatabase, CommitTreatmentPlan(tag, c) ->
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

            /// The refusal path, and the only place a refusal is remembered. Rule 46:
            /// a create that did not land is as much an event as one that did.
            let refuse (h: Hospital) refusal =
                let what = if c.Req.Pin.IsSome then "signature" else "save"
                { h with
                    Database =
                        { h.Database with
                            Private.Answered =
                                h.Database.Private.Answered |> Map.add c.Req.Key (Error refusal) }
                        |> Database.note $"%s{what} refused: %A{refusal}" },
                [ reply (Error refusal) ]

            match record with
            | None -> refuse h (SessionNotOpen None)
            | Some r when not (SessionRecord.isOpen r) ->
                let mark = match r.State with Ended(m, _) -> Some m | OpenOrGone -> None
                refuse h (SessionNotOpen mark)
            // Rule 41, inside the act: a Session past its time ends here rather than
            // signing something.
            | Some r when SessionRecord.hasIdledOut h.Env.Now r ->
                let ended =
                    { h with
                        Database.Private.Sessions =
                            h.Database.Private.Sessions
                            |> List.map (fun x ->
                                if x.Id = r.Id then x |> SessionRecord.endWith Idle h.Env.Now else x) }
                refuse ended (SessionNotOpen(Some Idle))
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

                // Rule 44. The Server has just re-read the Patient Data; if the
                // platform has moved on from what is being signed over, the User has
                // to have seen it — and says so by returning the token.
                let dataAccepted =
                    match c.Fresh with
                    | None -> true                                   // ext 11a: unavailable is not a failure
                    | Some fresh when Some fresh = req.Work.Data -> true
                    | Some fresh ->
                        match req.DataOk with
                        | Some t ->
                            Token.verifyDataNotice t
                            && t.Claim.Sid = r.Id
                            && Token.digest t = Some(WorkPlan.dataDigest (Some fresh))
                        | None -> false

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
                // create whole rather than choosing between them.
                elif (req.Work.Orders |> List.map _.Id |> List.distinct |> List.length)
                     <> req.Work.Orders.Length then
                    refuse h (TokenRefused "an OrderContext appears twice (Concept 10)")

                // Rule 20, and Rule 36 with it: the check and the append are the same
                // act, so there is no window between them to lose.
                elif (PatientRecord.blocking openedWith pr).IsSome then
                    let blocker = (PatientRecord.blocking openedWith pr).Value
                    refuse h (BlockedBy blocker.By)

                // Rule 21. Whose work it is, not its contents.
                elif not undisclosed.IsEmpty then
                    refuse h (UnsignedElsewhere(undisclosed.Head.By, outstanding |> List.map _.Id))

                elif not dataAccepted then
                    refuse h (DataChanged c.Fresh.Value)

                // Concept 17 and Rule 33. A token works exactly once and only within
                // its lifetime: the create it accompanies consumes it, and a spent or
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
                             || Token.digest t <> Some(WorkPlan.digest req.Work)
                         | None -> true) then
                    refuse h (TokenRefused "the signing challenge does not name this plan (Rule 43)")

                else
                    // Rules 22 and 27, last of all.
                    let credential =
                        h.Database.Private.Credentials
                        |> Map.tryFind uc.UserId
                        |> Option.defaultValue (UserCredential.fresh uc.UserId)

                    // Rule 27. Whether it was suspended already matters: an attempt
                    // that reaches the limit ends the Session (Rule 9), while one made
                    // against a credential suspended in an earlier Session is simply
                    // refused — this Session did nothing wrong.
                    let wasSuspended = credential.Suspended

                    let pinOk, credential =
                        match req.Pin with
                        | None -> true, credential
                        | Some pin -> UserCredential.verify pin credential

                    let withCredential (st: Hospital) =
                        { st with Database.Private.Credentials = st.Database.Private.Credentials |> Map.add uc.UserId credential }

                    if not pinOk then
                        let h = withCredential h
                        if wasSuspended then refuse h CredentialSuspended
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
                                Signed = req.Pin.IsSome                            // Rules 15, 16
                                At = h.Env.Now
                            }

                        let h = withCredential h

                        let (TreatmentPlanId planId) = plan.Id
                        let (UserId by) = uc.UserId
                        let what = if plan.Signed then "signed" else "saved"

                        // Concept 17. The tokens this create rested on are spent here,
                        // in the same act that honoured them (Rule 42) — so the same
                        // create cannot be replayed with the same word from the Server.
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
                            |> Database.note $"%s{planId} %s{what} by %s{by}"

                        { h with Database = db }, [ reply (Ok plan) ]

        // Rule 40 and Rule 7, as one act: the new record goes in and every other
        // Session of that User closes in the same breath, so there is no interval in
        // which one User holds two — whichever order two launches arrive in.
        | GenPresServer, GenPresDatabase, OpenSessionClosingOthers r ->
            let now = h.Env.Now
            let (SessionNo sno) = r.No

            let who =
                match r.User with
                | Some uc -> let (UserId u) = uc.UserId in u
                | None -> "anonymous"

            let closed =
                h.Database.Private.Sessions
                |> List.map (fun x ->
                    if x.Id <> r.Id
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
                | Some(LaunchNo n) -> $"launch-%03i{n} honoured"
                | None -> "no launch (anonymous)"

            let note (db: DatabaseState) =
                superseded
                |> List.fold
                    (fun acc x ->
                        let (SessionNo n) = x.No
                        acc |> Database.note $"session ses-%03i{n} ended Superseded")
                    (db |> Database.note $"session ses-%03i{sno} opened for %s{who}, %s{how}")

            // The SessionId counter never reissues, so an id already present is a
            // replay — and a replay must not resurrect what has since ended.
            if closed |> List.exists (fun x -> x.Id = r.Id) then
                { h with Database.Private.Sessions = closed }, []
            else
                { h with Database = { h.Database with Private.Sessions = r :: closed } |> note }, []

        // Rule 40. Conditional: an already ended record keeps the mark it ended with,
        // and the obligation that ending created (Rule 10).
        | GenPresServer, GenPresDatabase, EndSessionIfOpen(sid, mark) ->
            let ending =
                h.Database.Private.Sessions
                |> List.exists (fun x -> x.Id = sid && SessionRecord.isOpen x)

            let db =
                { h.Database with
                    Private.Sessions =
                        h.Database.Private.Sessions
                        |> List.map (fun x ->
                            if x.Id = sid then x |> SessionRecord.endWith mark h.Env.Now else x) }

            // Rule 46. Only an ending that happened is recorded; a repeated one is not
            // an event, it is a no-op (Rule 40).
            let (SessionId name) = sid
            { h with Database = if ending then db |> Database.note $"session %s{name} ended %A{mark}" else db }, []

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

        // Rule 10. The User said they had seen it, and that ends it.
        | GenPresServer, GenPresDatabase, MarkAcknowledged sid ->
            { h with
                Database.Private.Sessions =
                    h.Database.Private.Sessions
                    |> List.map (fun x -> if x.Id = sid then x |> SessionRecord.acknowledged h.Env.Now else x) }, []

        | GenPresServer, GenPresDatabase, ReadSessionRecord(tag, sid) ->
            h,
            [ send GenPresDatabase env.From
                (SessionRecordRead(tag, h.Database.Private.Sessions |> List.tryFind (fun x -> x.Id = sid))) ]

        | GenPresServer, GenPresDatabase, ReadSessionRecords tag ->
            h, [ send GenPresDatabase env.From (SessionRecordsRead(tag, h.Database.Private.Sessions)) ]

        // Rule 37. A reset is parked, and nothing else happens: the PIN in force is
        // untouched, so there is no moment in which the credential cannot sign.
        | GenPresServer, GenPresDatabase, StartReset(tag, user, codeMac, expires) ->
            let pending = { User = user; CodeMac = codeMac; Expires = expires; Wrong = 0 }
            { h with Database.Private.Resets = h.Database.Private.Resets |> Map.add user pending },
            [ send GenPresDatabase env.From (ResetStarted(tag, user)) ]

        // Rule 37. The check and the replacement are one act, at the party that holds
        // both the reset and the credential: a code that verifies replaces the PIN and
        // is spent in the same breath, and one that does not changes nothing but its
        // own count. Rule 27: a newly set PIN starts at zero.
        | GenPresServer, GenPresDatabase, ReplacePinIfCode(tag, user, code, pin) ->
            let refuse failure = [ send GenPresDatabase env.From (ResetRefused(tag, failure)) ]

            match h.Database.Private.Resets |> Map.tryFind user with
            | None -> h, refuse NoResetPending
            | Some pending when h.Env.Now > pending.Expires ->
                { h with Database.Private.Resets = h.Database.Private.Resets |> Map.remove user }, refuse ResetExpired
            | Some pending when pending.CodeMac <> Reset.macOf code ->
                let tried = { pending with Wrong = pending.Wrong + 1 }
                if tried.Wrong >= wrongCodeLimit then
                    { h with Database.Private.Resets = h.Database.Private.Resets |> Map.remove user }, refuse ResetVoid
                else
                    { h with Database.Private.Resets = h.Database.Private.Resets |> Map.add user tried },
                    refuse (WrongCode(wrongCodeLimit - tried.Wrong))
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

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 4: the GenPRES Server. A launch, leg by leg — UC-1 steps 7 to 14.
        // ══════════════════════════════════════════════════════════════════════

        // Step 7 into 8. The credential is handed to the Broker and not kept: from
        // here on GenPRES holds only the launch number, which is safe to log.
        | GenPresClient _, GenPresServer, RedeemLaunch cred ->
            let att = AttemptId h.GenPres.NextAttempt
            { h with
                GenPres.NextAttempt = h.GenPres.NextAttempt + 1
                GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingAssertion env.From)) },
            [ send GenPresServer Broker (ResolveLaunch(att, cred)) ]

        // Rule 6. A refusal opens nothing. LaunchRefused carries no reason
        // deliberately: expired, spent and never-existed are one answer to a Client.
        | Broker, GenPresServer, LaunchRejected(att, _, _) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingAssertion client) -> refuseLaunch att client LaunchRefused h
            | _ -> h, []                                  // a late or duplicate answer

        // Step 8 into 9. What the launch asserted — a login and maybe a Patient. Now
        // ask who that login is; the credential does not travel to the registry.
        | Broker, GenPresServer, LaunchResolved(att, no, assertion) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingAssertion client) ->
                let ctx = { Client = client; Launch = no; Assertion = assertion }
                { h with GenPres.Pending = h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingUser ctx)) },
                [ send GenPresServer UserRegistry (ResolveUser(ForLaunch att, assertion.Login)) ]
            | _ -> h, []

        // Step 9. Rule 6: no Role, no Session — and no guessing either.
        | UserRegistry, GenPresServer, UserUnresolved(ForLaunch att, failure) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingUser ctx) ->
                let reply =
                    match failure with
                    | NoRole -> NotAuthorised
                    | RegistryUnreachable -> AuthorityUnavailable
                refuseLaunch att ctx.Client reply h
            | _ -> h, []

        // Step 9 into 10. Rule 5: the Role is the registry's answer, never the
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

        // Step 10, and UC-2 step 1. Rule 24: a Prescriber with no PIN must set one
        // before the launch continues — and only now, once the registry has said who
        // the login belongs to. A login the registry does not recognise never reaches
        // this branch, so it can never enrol.
        | GenPresDatabase, GenPresServer, CredentialRead(ForLaunch att, credential) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingCredential(ctx, uc, mail)) ->
                match credential |> Option.bind _.Pin with
                | Some _ -> afterCredential att ctx uc mail h
                | None ->
                    { h with
                        GenPres.Pending =
                            h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinChoice(ctx, uc, mail))) },
                    [ send GenPresServer ctx.Client (PinRequired att) ]
            | _ -> h, []

        // UC-2 steps 3 and 4. The launch has been suspended on a human, possibly for
        // a long while, and nothing else was offered meanwhile.
        | (GenPresClient _ as sender), GenPresServer, SupplyPin(att, pin) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            // The prompt was put to one Client, and it is that Client's to answer.
            // A PIN is a UserCredential (Concept 7) and the Server is the only party
            // that ever holds one (Rule 22); a second browser answering would set this
            // User's PIN from somebody else's screen.
            | Some(AwaitingPinChoice(ctx, uc, mail)) when ctx.Client = sender ->
                // Creating the UserCredential if GenPRES holds none for that login
                // yet. Rule 26: a newly set PIN starts with a count of zero, so there
                // is nothing on an existing credential worth carrying over.
                let c = UserCredential.fresh uc.UserId |> UserCredential.setPin pin
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending |> Map.add att (pend h.Env.Now (AwaitingPinWritten(ctx, uc, mail))) },
                [ send GenPresServer GenPresDatabase (WriteCredential(ForLaunch att, c)) ]
            | Some(AwaitingPinChoice _) ->
                // Answered by a Client that was never asked. Not merely dropped: this
                // is exactly the envelope worth alerting on.
                h, [ send GenPresServer Environment (Refused env) ]
            | _ -> h, []

        | GenPresDatabase, GenPresServer, CredentialWritten(ForLaunch att, _) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPinWritten(ctx, uc, mail)) ->
                let (LoginName l) = uc.Login
                // Rule 26: mailed and recorded, the first setting included. Then the
                // launch continues from UC-1 step 11.
                let h, out = afterCredential att ctx uc mail h
                h, (pinChanged (Some mail) $"PIN set for %s{l}") @ out
            | _ -> h, []

        // Step 11. Concept 2: read once, at the launch, and not refreshed while the
        // Session lives. ext 11a: unavailable is not a failure — the PatientContext
        // carries the PatientId and no data, and the User fills it in by hand.
        | PatientDataPlatform, GenPresServer,
          (PatientDataRead(ForLaunch att, _) | PatientDataUnavailable(ForLaunch att)) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPatientData(ctx, uc, mail)) ->
                match ctx.Assertion.Patient with
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

        // Step 12. Rule 19 picks the TreatmentPlan the Session starts from: the most recent
        // that is either Signed, by whoever, or Unsigned and this User's own. Where
        // neither exists, the Session starts from nothing. Then Rule 7's other
        // Sessions, which the Server no longer mirrors and so must read (Rule 31).
        | GenPresDatabase, GenPresServer, RecordRead(ForLaunch att, record) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingRecord(ctx, uc, mail, pctx)) ->
                let start = record |> PatientRecord.startsFrom uc.UserId
                { h with
                    GenPres.Pending =
                        h.GenPres.Pending
                        |> Map.add att (pend h.Env.Now (AwaitingPriors(ctx, uc, mail, pctx, start))) },
                [ send GenPresServer GenPresDatabase (ReadSessionRecords(ForLaunch att)) ]
            | _ -> h, []

        // Steps 13 and 14. Rule 7 closes this User's other Sessions, Rule 10 says so
        // once, and Rule 33 hands the Client the token it will return with every
        // create. From here the Server keeps nothing of the Session but its record.
        | GenPresDatabase, GenPresServer, SessionRecordsRead(ForLaunch att, others) ->
            match h.GenPres.Pending |> Map.tryFind att |> Option.map _.Stage with
            | Some(AwaitingPriors(ctx, uc, mail, pctx, start)) ->
                let h, out = openSession ctx.Client (Some ctx.Launch) (Some uc) (Some mail) pctx start others h
                { h with GenPres.Pending = h.GenPres.Pending |> Map.remove att }, out
            | _ -> h, []

        // Rule 13 / UC-8. No launch, so no LaunchCredential, and GenPRES cannot know
        // who is at the keyboard. Neither the PatientRecord nor the
        // PatientDataPlatform is ever touched: with no PatientId there is nothing to
        // read. Rule 7 counts a User's Sessions and this one binds to none, so there
        // is nothing to close and no SessionRecords to read either.
        // Rule 10. No SessionRecord is read and nothing else is decided: the User has
        // seen the notice, and the record stops owing one.
        | GenPresClient _, GenPresServer, AckSessionNotice sid ->
            h, [ send GenPresServer GenPresDatabase (MarkAcknowledged sid) ]

        | GenPresClient _, GenPresServer, OpenAnonymous ->
            openSession env.From None None None { Patient = None; Data = None } None [] h

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 4: the GenPRES Server, in Session — one request, several legs
        // ══════════════════════════════════════════════════════════════════════

        // Rule 31 in one branch: a request arrives with everything it needs except who
        // sent it, and the answer to that is in the Database. Nothing about this
        // Session was in memory a moment ago, and nothing will be a moment after the
        // reply. Rule 8's refresh has one home, here, because every in-Session act
        // travels as this one message shape.
        | GenPresClient _, GenPresServer, SessionRequest(sid, cmd) ->
            let rid = RequestId h.GenPres.NextRequest
            let ctx = { Sid = sid; Client = env.From; Cmd = cmd; Stage = AwaitingSessionRecord }
            { h with GenPres.NextRequest = h.GenPres.NextRequest + 1 } |> putFlight rid ctx,
            [ send GenPresServer GenPresDatabase (ReadSessionRecord(ForRequest rid, sid)) ]

        // Rule 32: the User and the Patient of the request come from here. Rule 10:
        // where the Session is gone, this is the next opportunity to say so.
        | GenPresDatabase, GenPresServer, SessionRecordRead(ForRequest rid, record) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | None -> h, []
            | Some ctx ->
                match record with
                | None ->
                    dropFlight rid h, [ send GenPresServer ctx.Client (SessionEnded None) ]
                | Some r when not (SessionRecord.isOpen r) ->
                    if SessionRecord.tellsAtNextOpportunity r then
                        let mark = match r.State with Ended(m, _) -> Some m | OpenOrGone -> None
                        dropFlight rid h,
                        [
                            send GenPresServer GenPresDatabase (MarkDelivered r.Id)
                            send GenPresServer ctx.Client (SessionEnded mark)
                        ]
                    else
                        // Acknowledged already: the request is still refused, but the
                        // notice is not repeated (Rule 10).
                        dropFlight rid h, [ send GenPresServer ctx.Client SessionRefused ]
                // Rule 41. Expiry is a fact about the record, not about the sweep: a
                // request arriving after the Session should have idled out ends it then
                // and there, rather than refreshing it back to life. The sweep is for
                // Sessions nobody comes back to at all.
                | Some r when SessionRecord.hasIdledOut h.Env.Now r ->
                    dropFlight rid h,
                    [
                        send GenPresServer GenPresDatabase (EndSessionIfOpen(r.Id, Idle))
                        send GenPresServer GenPresDatabase (MarkDelivered r.Id)
                        send GenPresServer ctx.Client (SessionEnded(Some Idle))
                    ]
                | Some r ->
                    // Rule 8. Every request refreshes the idle clock, and the clock is a
                    // field of the record, so refreshing it is a write — a guarded one
                    // (Rule 40): a Session that ended meanwhile is not touched.
                    let r = r |> SessionRecord.seen h.Env.Now
                    let refreshed = send GenPresServer GenPresDatabase (TouchIfOpen r.Id)
                    let h, out = dispatch rid ctx r h
                    // CloseSession writes the ended record itself; anything else gets
                    // the refresh. Writing both would be harmless but noisy.
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
                // Rule 20's block and Rule 21's notice are the same answers a create
                // would have got — settled here, before any PIN is asked for.
                | RequestSignChallenge(work, opened, notice), Some uc ->
                    if not (Token.verifyOpened opened) || opened.Claim.Sid <> ctx.Sid then
                        dropFlight rid h,
                        [
                            send GenPresServer ctx.Client
                                (CreateRefused "the opened-with token does not verify (Rule 33)")
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
                        dropFlight rid h, [ send GenPresServer ctx.Client (CreateBlocked blocker.By) ]
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
                            // Nothing stands in the way, so the User may be asked for a
                            // PIN — and what they will be signing is named now.
                            dropFlight rid h,
                            [
                                send GenPresServer ctx.Client
                                    (SignChallengeIssued(
                                        Token.mintChallenge
                                            h.Env.Now ctx.Sid r.Patient (WorkPlan.digest work)))
                            ]
                | _ -> dropFlight rid h, []
            | _ -> h, []

        // Rule 22: the Server is the only party that verifies a UserCredential, and
        // the PIN never leaves GenPRES. Rule 27: the count is per credential and
        // survives the Session, so guessing is capped outright rather than per visit.
        // Rule 38. The registry has answered for a signature in flight. The Role must
        // still be there, and must still belong to the same person the SessionRecord
        // names (Rule 32) — a login that now resolves to somebody else is not this
        // Session's User.
        | UserRegistry, GenPresServer, UserResolved(ForRequest rid, uc, _) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingSigningRole(r, req) } as ctx) ->
                match r.User, r.Patient with
                | Some sessionUser, Some p when uc.UserId = sessionUser.UserId && uc.Role = Prescriber ->
                    // Rule 44. And now the Patient Data, as the platform has it at the
                    // moment of the signature — not as the launch read it (Concept 2).
                    h |> putFlight rid { ctx with Stage = AwaitingFreshData(r, req, uc.Role) },
                    [ send GenPresServer PatientDataPlatform (ReadPatientData(ForRequest rid, p)) ]
                | _ ->
                    dropFlight rid h, [ send GenPresServer ctx.Client NotPermitted ]
            | _ -> h, []

        // Rule 38. No Role, or no answer: either way nothing is signed. The two are
        // told apart, because one is a withdrawal and the other is a registry that is
        // merely down — and a Session that may sign again in a minute.
        | UserRegistry, GenPresServer, UserUnresolved(ForRequest rid, failure) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some ctx ->
                let reply =
                    match failure with
                    | NoRole -> NotPermitted
                    | RegistryUnreachable -> SigningUnavailable
                dropFlight rid h, [ send GenPresServer ctx.Client reply ]
            | None -> h, []

        // Rule 44. The platform has answered for a signature in flight. Whatever it
        // said — data, or nothing at all when it is unreachable (UC-1 ext 11a) — the
        // create now goes to the Database as one act (Rule 42).
        | PatientDataPlatform, GenPresServer, (PatientDataRead(ForRequest rid, _) | PatientDataUnavailable(ForRequest rid)) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingFreshData(r, req, role) } as ctx) ->
                let fresh =
                    match env.Msg with
                    | PatientDataRead(_, d) -> Some d
                    | _ -> None
                commit rid ctx r req (Some role) fresh h
            | _ -> h, []

        // Rule 42. The one act said yes. Rule 33: the Session now stands on what it
        // just created, so a fresh token goes back with the answer — Rules 20 and 21
        // are measured from this TreatmentPlan from here on out.
        | GenPresDatabase, GenPresServer, TreatmentPlanCommitted(ForRequest rid, plan) ->
            match h.GenPres.InFlight |> Map.tryFind rid with
            | Some({ Stage = AwaitingCommit r } as ctx) ->
                dropFlight rid h,
                [ send GenPresServer ctx.Client
                    (TreatmentPlanCreated(
                        plan.Id,
                        plan.Signed,
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
                    | SessionNotOpen mark ->
                        [
                            send GenPresServer GenPresDatabase (MarkDelivered r.Id)
                            send GenPresServer ctx.Client (SessionEnded mark)
                        ]
                    | RoleRefused -> [ send GenPresServer ctx.Client NotPermitted ]
                    | TokenRefused why -> [ send GenPresServer ctx.Client (CreateRefused why) ]
                    | BlockedBy who -> [ send GenPresServer ctx.Client (CreateBlocked who) ]
                    | UnsignedElsewhere(who, ids) ->
                        // Rule 34. The token is the Server's to mint, because only the
                        // Server has the key — the Database names what was disclosed.
                        [
                            send GenPresServer ctx.Client
                                (UnsignedWorkNotice(who, Token.mintNotice h.Env.Now ctx.Sid r.Patient ids))
                        ]
                    | DataChanged fresh ->
                        [
                            send GenPresServer ctx.Client
                                (PatientDataChanged(
                                    fresh,
                                    Token.mintDataNotice
                                        h.Env.Now ctx.Sid r.Patient (WorkPlan.dataDigest (Some fresh))))
                        ]
                    | PinWrong left -> [ send GenPresServer ctx.Client (PinRejected left) ]
                    | CredentialSuspended -> [ send GenPresServer ctx.Client SigningLocked ]
                    | PinLimitReached ->
                        // Rule 9: the Session ended inside the same act that refused,
                        // and Rule 10: this request is the opportunity to say so.
                        [
                            send GenPresServer GenPresDatabase (MarkDelivered r.Id)
                            send GenPresServer ctx.Client (SessionEnded(Some WrongPinLimit))
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

        // UC-7 step 3. Replaced, not removed. Rule 26: mailed and recorded, every
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
        | GenPresDatabase, GenPresServer, CredentialWritten _
        | GenPresDatabase, GenPresServer, TreatmentPlanCommitted _
        | GenPresDatabase, GenPresServer, CommitRefused _
        | GenPresDatabase, GenPresServer, ResetStarted _
        | GenPresDatabase, GenPresServer, PinReplaced _
        | GenPresDatabase, GenPresServer, ResetRefused _
        | GenPresDatabase, GenPresServer, SessionRecordsRead _
        | GenPresDatabase, GenPresServer, SessionRecordRead _
        | GenPresDatabase, GenPresServer, RecordRead _
        | GenPresDatabase, GenPresServer, CredentialRead _ -> h, []

        // ══════════════════════════════════════════════════════════════════════
        //  Actor 3: the GenPRES Client — and the cart, which lives here (Rule 31)
        // ══════════════════════════════════════════════════════════════════════

        // A closed browser is not there any more. Nothing it might have sent reaches
        // the Server (UC-12 ext 2a), which is exactly why no close can be inferred —
        // and the cart went with it, because the cart was only ever here.
        | _, GenPresClient b, _ when
            h.Clients |> Map.tryFind b |> Option.map _.Closed |> Option.defaultValue false ->
            h, []

        // Consequence 4: the credential travels in a URL, so it lands in the address
        // bar — and stays there, which is what makes a refresh a retry.
        // Rule 39. The Client presents the credential and erases it from the address
        // bar in the same act. What the browser keeps of the launch after that is a
        // copy in the page's own memory — enough to retry with (UC-1 ext 7a), and not
        // in history, not in the bar, not in a referrer (Consequence 4). A browser
        // that is never served never presents and never scrubs (UC-1 ext 5a).
        | MainEhrLaunchScript, GenPresClient b, OpenUrl cred ->
            h |> onClient b (fun s -> { s with UrlCredential = None; RetryCredential = Some cred }),
            [ send (GenPresClient b) GenPresServer (RedeemLaunch cred) ]

        // F5. The page is still the page, so the retry comes from its own memory:
        // after Rule 39's scrub the address bar has nothing left to re-present.
        | User, GenPresClient b, Refresh ->
            let st = clientState b h
            match st.RetryCredential |> Option.orElse st.UrlCredential with
            | Some cred -> h, [ send (GenPresClient b) GenPresServer (RedeemLaunch cred) ]
            | None -> h, []

        // A full reload: the page and its memory go, and what is re-presented is
        // whatever is in the address bar — which, after Rule 39, is nothing.
        | User, GenPresClient b, ReloadPage ->
            let scrubbed = h |> onClient b (fun s -> { s with RetryCredential = None })
            match (clientState b scrubbed).UrlCredential with
            | Some cred -> scrubbed, [ send (GenPresClient b) GenPresServer (RedeemLaunch cred) ]
            | None -> scrubbed, []

        // UC-8. The Client has no LaunchCredential to present, and asks for a Session
        // without one.
        | User, GenPresClient b, OpenDirectly ->
            h |> onClient b (fun s -> { s with UrlCredential = None }),
            [ send (GenPresClient b) GenPresServer OpenAnonymous ]

        // Rule 6 / UC-1 ext 9a. The offer carries nothing over from the launch: no
        // User, no Patient. It is only made where relaunching would not cure the
        // failure — an unrecognised login, or an unreachable registry.
        | User, GenPresClient b, AcceptAnonymousOffer ->
            match h.Clients |> Map.tryFind b with
            | Some s when s.AnonymousOffer ->
                h |> onClient b (fun s -> { s with AnonymousOffer = false; Showing = None }),
                [ send (GenPresClient b) GenPresServer OpenAnonymous ]
            | _ -> h, []

        // UC-2 step 3. Nothing else was on offer until this was answered.
        | User, GenPresClient b, ChoosePin pin ->
            match h.Clients |> Map.tryFind b |> Option.bind _.AwaitingPin with
            | Some att ->
                h |> onClient b (fun s -> { s with AwaitingPin = None; Showing = None }),
                [ send (GenPresClient b) GenPresServer (SupplyPin(att, pin)) ]
            | None -> h, []

        // Concept 15 and Rule 31: prescribing changes the Client's own cart, and the
        // whole of it then travels — to be computed on, or to be saved. Rule 11: the
        // SessionId rides in the request, never in a URL, and it is also what
        // refreshes the idle clock.
        | User, GenPresClient b, Act a ->
            let st = clientState b h
            let toServer cmd = [ send (GenPresClient b) GenPresServer (SessionRequest(st.Sid.Value, cmd)) ]

            match a, st.NoticeFor with
            // Rule 10. The one act that belongs to Sessions that have already ended.
            | AcknowledgesNotice, [] -> h, []
            | AcknowledgesNotice, sids ->
                h |> onClient b (fun s -> { s with NoticeFor = []; Showing = None }),
                [ for sid in sids -> send (GenPresClient b) GenPresServer (AckSessionNotice sid) ]
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
                        // Rule 45. The key is minted here and travels with the create;
                        // a retry of this same create carries this same key.
                        h,
                        toServer (
                            CreateTreatmentPlan
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
                        toServer (RequestSignChallenge(st.Work, tok, st.Notice))
                    | None -> h, []

                | CancelsSign ->
                    // Rule 43. Nothing was signed and nothing was asked for: the
                    // challenge is simply dropped, and the next one is minted fresh.
                    h |> onClient b (fun s -> { s with Signing = None; Modal = None; Showing = None }), []

                // Rule 10. Taken by the match above, whether or not a notice is
                // standing: it belongs to a Session that has ended, and this branch is
                // about one that has not.
                | AcknowledgesNotice -> h, []

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
                    // UC-9 step 5. The unsaved work outlived its Session because it
                    // was never in the Server (Rule 31). It comes into this one as
                    // fresh prescribing (Concept 15) — not as a resumed Session, and
                    // with no claim on the old one's stamps: Rule 35 will decide those.
                    //
                    // It carries over within one User's own work and one Patient's,
                    // and no further. An OrderContext names the Patient it belongs to
                    // (Guarantee 1); rewriting that name here would be the Client
                    // deciding whose record work lands in, which is the Server's to
                    // take from the SessionRecord (Rule 32). So nothing is rewritten,
                    // and what does not belong to this Session is not carried at all —
                    // the create would refuse it in any case.
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

        | User, GenPresClient b, CloseBrowser ->
            // UC-12 ext 2a: nothing reaches the Server. A vanished browser is
            // indistinguishable from a silent one, so the Session is left to idle out
            // — and the cart is gone, because it was only ever here (Rule 31).
            h |> onClient b (fun s ->
                { s with
                    Closed = true
                    Work = WorkPlan.empty
                    Opened = None
                    Notice = None }), []

        | GenPresServer, GenPresClient b, SessionOpened(sid, _, user, pctx, orders, token) ->
            h |> onClient b (fun s ->
                { s with
                    UrlCredential = None
                    RetryCredential = None
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
                    Notice = None }), []

        | GenPresServer, GenPresClient b, PinRequired att ->
            h |> onClient b (fun s ->
                { s with
                    AwaitingPin = Some att
                    Showing = Some "choose a PIN — nothing else is offered until you do" }), []

        | GenPresServer, GenPresClient b, LaunchRefused ->
            // ext 8a: relaunching cures this, so relaunching is what is offered.
            h |> onClient b (fun s ->
                { s with
                    UrlCredential = None
                    Showing = Some "the launch failed — relaunch from MainEHR" }), []

        | GenPresServer, GenPresClient b, NotAuthorised ->
            // ext 9a: relaunching would not help, so the anonymous open is the only
            // offer worth making (Rule 6).
            h |> onClient b (fun s ->
                { s with
                    UrlCredential = None
                    AnonymousOffer = true
                    Showing = Some "not authorised — continue anonymously?" }), []

        // The registry being down is transient, so a relaunch — which mints a fresh
        // credential, the one thing F5 cannot do once this one is spent — plausibly
        // cures it. Both offers stand. Contrast NotAuthorised above, where the answer
        // will be the same however often it is asked.
        | GenPresServer, GenPresClient b, AuthorityUnavailable ->
            h |> onClient b (fun s ->
                { s with
                    UrlCredential = None
                    AnonymousOffer = true
                    Showing =
                        Some "authorisation could not be checked — relaunch from MainEHR, or continue anonymously?" }), []

        // Consequence 1: no Client at all is served when the Server is down, so in
        // practice the User sees the browser's own error page. Where a Client was
        // already served, the credential stays in the address bar and a refresh
        // retries — for as long as Rule 3 allows. The cart stays too (Rule 31): a
        // Server that is down has not ended anything (Rule 9).
        | GenPresServer, GenPresClient b, ServerUnreachable ->
            h |> onClient b (fun s -> { s with Showing = Some "GenPRES is unavailable" }), []

        // The Session is gone; the work is not. It was never in the Server, so the
        // Client still holds it and may offer to carry it into the next Session as
        // fresh prescribing (Concept 15; UC-9 step 5).
        | GenPresServer, GenPresClient b, SessionEnded mark ->
            let text =
                match mark with
                | Some m -> $"the session ended: %A{m} — relaunch from MainEHR"
                | None -> "no such session — relaunch from MainEHR"
            // Rule 10. What the Client holds now is not a Session but a notice, and the
            // Session it names is what an acknowledgement would name.
            h |> onClient b (fun s ->
                { s with
                    NoticeFor = s.NoticeFor @ Option.toList s.Sid
                    Sid = None
                    Opened = None
                    Showing = Some text }), []

        | GenPresServer, GenPresClient b, SessionRefused ->
            h |> onClient b (fun s -> { s with Sid = None; Opened = None }), []

        | GenPresServer, GenPresClient b, PriorSessionNotice priors ->
            h |> onClient b (fun s ->
                { s with
                    NoticeFor = s.NoticeFor @ (priors |> List.map (fun (_, _, sid) -> sid))
                    Showing = Some "work in an earlier session may have been lost" }), []

        // Rule 31: the answer comes back from the payload, and the Client keeps it —
        // because the Client is the only party that keeps anything.
        | GenPresServer, GenPresClient b, Computed orders ->
            h |> onClient b (fun s -> { s with Work.Orders = orders }), []

        | GenPresServer, GenPresClient b, CreateBlocked _ ->
            h |> onClient b (fun s ->
                { s with Showing = Some "someone signed since you opened — take up their version" }), []

        // Rule 34. The token is what a choice to create anyway must return, so the
        // Client keeps it: proceeding is re-sending the create, holding off is not.
        | GenPresServer, GenPresClient b, UnsignedWorkNotice(uc, token) ->
            let (LoginName l) = uc.Login
            h |> onClient b (fun s ->
                { s with
                    Notice = Some token
                    Showing = Some $"unsigned work of %s{l} is newer than yours — create anyway?" }), []

        | GenPresServer, GenPresClient b, CreateRefused why ->
            h |> onClient b (fun s -> { s with Showing = Some $"the save was refused: %s{why}" }), []

        // Rules 33 and 34. The baseline moved, so the old token is spent and a new one
        // arrives with the answer; the notice, having been acted on, is spent too.
        // Rule 43. The challenge names what the User is about to attest to, so the
        // create goes out with it and with the PIN that was waiting for it.
        | GenPresServer, GenPresClient b, SignChallengeIssued token ->
            let st = clientState b h
            match st.Signing, st.Opened with
            | Some pin, Some opened ->
                h |> onClient b (fun s -> { s with Modal = Some token; Signing = None }),
                [
                    send (GenPresClient b) GenPresServer
                        (SessionRequest(
                            st.Sid.Value,
                            CreateTreatmentPlan
                                {
                                    Work = st.Work
                                    Opened = opened
                                    Notice = st.Notice
                                    Challenge = Some token
                                    DataOk = st.DataOk
                                    Pin = Some pin
                                    Key = idemKey b h.Env.Now
                                }))
                ]
            | _ -> h, []

        // Rule 44. The Patient Data has moved under the Session. The User is shown it
        // and accepts by keeping the token, which the next create carries.
        | GenPresServer, GenPresClient b, PatientDataChanged(fresh, token) ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = None
                    DataOk = Some token
                    Work.Data = Some fresh
                    Work.From = Some(FromPlatform h.Env.Now)
                    Showing = Some "the Patient Data has changed — check it and sign again" }), []

        | GenPresServer, GenPresClient b, TreatmentPlanCreated(_, _, token) ->
            h |> onClient b (fun s -> { s with Opened = Some token; Notice = None }), []

        | GenPresServer, GenPresClient b, TreatmentPlanOpened(_, orders, token) ->
            h |> onClient b (fun s ->
                { s with Work.Orders = orders; Opened = Some token; Notice = None }), []

        | GenPresServer, GenPresClient b, PinRejected left ->
            h |> onClient b (fun s -> { s with Showing = Some $"wrong PIN — %i{left} left" }), []

        | GenPresServer, GenPresClient b, NoTreatmentPlanHere ->
            h |> onClient b (fun s -> { s with Showing = Some "no patient: nothing can be saved" }), []

        | GenPresServer, GenPresClient b, NotPermitted ->
            h |> onClient b (fun s -> { s with Showing = Some "not permitted" }), []

        // Rule 27. Signing stays locked until Rule 37 replaces the PIN — a correct PIN
        // does not unlock it, which is the whole point of a limit that outlives the
        // Session it was reached in.
        | GenPresServer, GenPresClient b, SigningLocked ->
            h |> onClient b (fun s ->
                { s with
                    Modal = None
                    Signing = None
                    Showing = Some "signing is locked — reset the PIN to unlock it" }), []

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
            h |> onClient b (fun s -> { s with Showing = Some what }), []

        // ── anything else ──

        // An envelope an edge permits but the recipient does not accept. Recorded
        // rather than swallowed, so a misrouted or forged message shows in the trace.
        | _ -> h, [ send env.To Environment (Refused env) ]

    /// The edge table is enforced here, before anything is delivered: an envelope no
    /// edge permits never reaches its recipient at all. That is what separates the
    /// Constraints section from a convention — no component can relay on another's
    /// behalf, even by accident, because the wire does not exist.
    ///
    /// `depthFirst` is the scheduler. Depth first — a cascade runs to the end before
    /// the next thing in the inbox starts — is the readable default and what every
    /// scenario but one uses. Breadth first interleaves the cascades leg by leg, which
    /// is the only way to put two creates in flight at once and so the only way to
    /// exercise Rule 36. It is the same messages either way; only the order differs,
    /// which is precisely what Rule 36 exists to be safe against.
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


// ───────────────────────────── printing ─────────────────────────────

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
        | Broker -> "Broker"
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
        | CreateTreatmentPlan req ->
            let what = match req.Pin with Some(Pin p) -> $"Sign (pin %s{p})" | None -> "Save"
            let n = match req.Notice with Some _ -> " +notice" | None -> ""
            let c = match req.Challenge with Some _ -> " +challenge" | None -> ""
            let d = match req.DataOk with Some _ -> " +data" | None -> ""
            let os = req.Work.Orders
            $"%s{what} (%i{os.Length} order contexts, opened-with %s{planName (Token.plan req.Opened)}%s{n}%s{c}%s{d})"
        | RequestSignChallenge(work, tok, _) ->
            $"RequestSignChallenge (%i{work.Orders.Length} order contexts, opened-with %s{planName (Token.plan tok)})"
        | OpenTreatmentPlan(TreatmentPlanId s) -> $"OpenTreatmentPlan %s{s}"
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
        | PrepareLaunch(LoginName u, p) ->
            let pat = match p with Some(PatientId x) -> x | None -> "(no patient)"
            $"PrepareLaunch %s{u} %s{pat}"
        | LaunchPrepared(LaunchCredential c) -> $"LaunchPrepared %s{c}"
        | LaunchNotPrepared -> "LaunchNotPrepared"
        | OpenUrl(LaunchCredential c) -> $"GET /genpres?launch=%s{c}"
        | Refresh -> "F5"
        | ReloadPage -> "reload"
        | OpenDirectly -> "OpenDirectly"
        | AcceptAnonymousOffer -> "AcceptAnonymousOffer"
        | ChoosePin(Pin p) -> $"ChoosePin %s{p}"
        | Act a -> actName a
        | CloseBrowser -> "CloseBrowser"
        | RedeemLaunch(LaunchCredential c) -> $"RedeemLaunch %s{c}"
        | OpenAnonymous -> "OpenAnonymous"
        | SupplyPin(AttemptId a, Pin p) -> $"SupplyPin #%i{a} %s{p}"
        | AckSessionNotice(SessionId sid) -> $"AckSessionNotice %s{sid}"
        | SessionRequest(SessionId s, c) -> $"%s{s}: %s{cmdName c}"
        | ResolveLaunch(AttemptId a, LaunchCredential c) -> $"ResolveLaunch #%i{a} %s{c}"
        | LaunchResolved(AttemptId a, LaunchNo n, x) ->
            let (LoginName u) = x.Login
            let pat = match x.Patient with Some(PatientId p) -> p | None -> "(no patient)"
            $"LaunchResolved #%i{a} launch-%03i{n} -> %s{u} / %s{pat}   (a login and a patient: no identity, no role)"
        | LaunchRejected(AttemptId a, no, f) ->
            let tag = match no with Some(LaunchNo n) -> $"launch-%03i{n}" | None -> "launch-???"
            $"LaunchRejected #%i{a} %s{tag} %A{f}"
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
        | WriteCredential(t, _) -> $"WriteCredential %s{tagName t}"
        | CredentialWritten(t, _) -> $"CredentialWritten %s{tagName t}"
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
            $"""TreatmentPlanCommitted %s{i} %s{if s.Signed then "Signed" else "Unsigned"}"""
        | CommitRefused(t, r) -> $"CommitRefused %s{tagName t} %A{r}"
        | OpenSessionClosingOthers r ->
            let (SessionNo n) = r.No
            $"OpenSessionClosingOthers ses-%03i{n}"
        | EndSessionIfOpen(SessionId sid, mark) -> $"EndSessionIfOpen %s{sid} %A{mark}"
        | TouchIfOpen(SessionId sid) -> $"TouchIfOpen %s{sid}"
        | MarkDelivered(SessionId sid) -> $"MarkDelivered %s{sid}"
        | MarkAcknowledged(SessionId sid) -> $"MarkAcknowledged %s{sid}"
        | ReadSessionRecord(t, SessionId s) -> $"ReadSessionRecord %s{tagName t} %s{s}"
        | SessionRecordRead(t, r) ->
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
        | LaunchRefused -> "LaunchRefused"
        | NotAuthorised -> "NotAuthorised"
        | AuthorityUnavailable -> "AuthorityUnavailable"
        | ServerUnreachable -> "ServerUnreachable"
        | SessionEnded m -> $"SessionEnded %A{m}"
        | SessionRefused -> "SessionRefused"
        | PriorSessionNotice ss ->
            let names =
                ss |> List.map (fun (SessionNo i, m, _) -> $"ses-%03i{i}=%A{m}") |> String.concat ", "
            $"PriorSessionNotice [%s{names}]"
        | Computed os -> $"Computed (%i{os.Length} order contexts)"
        | CreateBlocked uc -> let (LoginName l) = uc.Login in $"CreateBlocked by %s{l}"
        | SignChallengeIssued t ->
            $"""SignChallengeIssued over %s{t |> Token.digest |> Option.defaultValue "-"}"""
        | PatientDataChanged(PatientData d, _) -> $"PatientDataChanged %s{d}"
        | UnsignedWorkNotice(uc, t) ->
            let (LoginName l) = uc.Login
            $"UnsignedWorkNotice (%s{l}, disclosing %i{(Token.disclosed t).Length})"
        | CreateRefused why -> $"CreateRefused \"%s{why}\""
        | TreatmentPlanCreated(TreatmentPlanId s, signed, _) ->
            $"""TreatmentPlanCreated %s{s} %s{if signed then "Signed" else "Unsigned"}"""
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
//                         3. SCENARIOS AND ASSERTIONS
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

/// A create built by hand: the fields under test, and defaults for the rest. Rule 45's
/// key is fresh each time, so nothing here is answered out of the table by accident.
let mutable private handKey = 0

let handCreate (work: WorkPlan) (opened: OpenedToken) (notice: NoticeToken option) (pin: Pin option) =
    handKey <- handKey + 1
    CreateTreatmentPlan
        {
            Work = work
            Opened = opened
            Notice = notice
            Challenge = None
            DataOk = None
            Pin = pin
            Key = IdemKey $"hand-%04i{handKey}"
        }

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
// The document's Cast. Their state is the state before the first use case runs;
// later use cases inherit whatever earlier ones left behind.
//
// One Workstation stands in for many. Nothing in the Rules distinguishes them:
// Rule 7 counts a User's Sessions, not a workstation's, and Invariant 1 is about one
// MainEHR Session. So "User B at their own workstation" is modelled by User B
// logging in — User A's GenPRES Session is untouched either way.

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

let mkPlan n patient by signed baseOn orders =
    {
        Id = TreatmentPlanId $"plan-%04i{n}"
        No = TreatmentPlanNo n
        Patient = patient
        By = by
        Base = baseOn
        Orders = orders
        Data = Some(PatientData $"as read for %A{patient}")
        From = Some(FromPlatform 0)
        Signed = signed
        At = 0
    }

let p2Signed   = mkPlan 1 pat2 ucA true  None               [ oc "oc-1" pat2 ucA ]
let p3Signed   = mkPlan 2 pat3 ucB true  None               [ oc "oc-2" pat3 ucB ]
let p3Unsigned = mkPlan 3 pat3 ucA false (Some p3Signed.Id) [ oc "oc-2" pat3 ucB; oc "oc-3" pat3 ucA ]

/// The world the Cast starts in.
let world =
    let h = Hospital.empty
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
                    ucA.UserId, { User = ucA.UserId; Pin = Some pinA; AttemptCount = 0; Suspended = false }
                    ucB.UserId, { User = ucB.UserId; Pin = Some pinB; AttemptCount = 0; Suspended = false }
                ] }
    let h =
        { h with
            Database =
                { h.Database with
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

/// Rule 43. The challenges the last step's Server issued, as an attacker or a retry
/// would have them: something a Client was given, and cannot make.
let challengesIssued () =
    lastTrace |> List.choose (function { Msg = SignChallengeIssued t } -> Some t | _ -> None)

let challengeIssued () = challengesIssued () |> List.tryHead

/// Did `first` happen before `second` in the trace? Used where the document fixes an
/// order — Rule 24, and UC-3 ext 3c.
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

let audited (what: string) (h: Hospital) = auditOf h |> List.exists (fun a -> a.Contains what)
let credentialOf (uc: UserContext) (h: Hospital) = h.Database.Private.Credentials |> Map.tryFind uc.UserId

/// UC-7 step 3. What the User does with the mail: reads the code out of it. That the
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
        $"""%s{i}/%s{l}/%s{if s.Signed then "S" else "U"}""")
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

let step label h inbox =
    printfn ""
    printfn $"== {label} =="
    plansBefore h
    let after, trace, outcome = Hospital.run 4000 h inbox
    lastTrace <- trace
    allTrace <- allTrace @ trace
    trace |> List.filter (Envelope.noise >> not) |> List.iter (Envelope.show >> printfn "%s")
    if outcome <> "completed" then printfn $"    !! {outcome}"
    noteFlight after
    noteRecords after
    dump h after
    after

/// A scenario that runs but whose trace is not worth printing in full.
let quiet label h inbox =
    let h, trace, _ = Hospital.run 4000 h inbox
    lastTrace <- trace
    allTrace <- allTrace @ trace
    noteFlight h
    noteRecords h
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

    expect "UC-1 step 8: the launch asserted a login and a Patient, and no Role"
        (saw (function
              | LaunchResolved(_, _, a) -> a.Login = ucA.Login && a.Patient = Some pat1
              | _ -> false))

    expect "UC-1 step 9: the Role came from the UserRegistry (Rule 5)"
        (saw (function UserResolved(_, uc, _) -> uc.Role = Prescriber | _ -> false))

    expect "UC-1 step 10: a PIN is set, so the launch continues and none is asked for (Rule 23)"
        (saw (function CredentialRead(_, Some c) -> c.Pin.IsSome | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    expect "UC-1 step 11: the PatientContext was read once, at the launch (Concept 2)"
        (saw (function PatientDataRead _ -> true | _ -> false)
         && countOf (function ReadPatientData _ -> true | _ -> false) = 1)

    expect "UC-1 step 12: Patient 1 has no record, so the Session starts from nothing (Rule 19)"
        (openedAt 1 launched = None && workingAt 1 launched = [])

    expect "UC-1 step 13: the SessionRecord was written to the Database (Concept 9)"
        (launched.Database.Private.Sessions.Length = 1)

    // Rule 33. The Client is handed the token it will return with every create, and
    // it could not have made one: the mac is over a secret it never sees.
    expect "UC-1 step 14: the Client holds an opened-with token that verifies (Rule 33)"
        ((clientOf 1 launched |> Option.bind _.Opened |> Option.map Token.verifyOpened) = Some true)

    expect "UC-1 and from here the Server keeps nothing of the Session (Rule 31)"
        (launched.GenPres.InFlight.IsEmpty && launched.GenPres.Pending.IsEmpty)

    expect "UC-1 the credential is spent, and is not kept by GenPRES (Rule 2)"
        (launched.Broker.Launches |> Map.forall (fun _ l -> l.Redeemed))

    // ── UC-1 ext 1a — no Patient is active in the MainEHR Session ──
    // GenPRES opens and A can prescribe, but a TreatmentPlan cannot be opened or created.
    let noPatient = step "UC-1 ext 1a — no Patient active" world (launchAs ucA.Login None)

    expect "1a a Session opens without a Patient"
        (openCount noPatient = 1 && (newestRecord noPatient |> Option.bind _.Patient) = None)

    expect "1a steps 11 and 12 are skipped: no data to fetch, no PatientRecord to read"
        (never (function ReadPatientData _ -> true | _ -> false)
         && never (function ReadRecord _ -> true | _ -> false))

    let _ =
        step "UC-1 ext 1a — and a TreatmentPlan cannot be created (Rule 12)" noPatient
             [ act 1 (Prescribes(OrderContextId "oc-9")); act 1 Saves ]

    expect "1a prescribing works; creating does not"
        (saw (function Computed _ -> true | _ -> false)
         && saw (function NoTreatmentPlanHere -> true | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false))

    // ── UC-1 ext 3a — the Broker is unreachable ──
    // The one launch failure the EHR side can report: its Broker edge is
    // request-response and it has not yet exited.
    let brokerDown =
        step "UC-1 ext 3a — the Broker is unreachable" world
             (envt Broker (Stop Broker) :: launchAs ucA.Login (Some pat1))

    expect "3a no credential exists, nothing was opened, and the LaunchScript says so"
        (saw (function LaunchError _ -> true | _ -> false)
         && never (function LaunchPrepared _ -> true | _ -> false)
         && brokerDown.Clients.IsEmpty
         && openCount brokerDown = 0)

    // ── UC-1 ext 5a / 7a — the Server is unreachable ──
    // In production the Client is served by the Server, so ext 5a shows the browser's
    // own error page and nothing of ours. The model has no notion of the page being
    // served, so both extensions arrive here as the same answer: unavailable. The
    // credential is presented once and scrubbed from the bar in that act (Rule 39),
    // so what a refresh retries with is the page's own memory, for as long as Rule 3
    // allows.
    let serverDown =
        step "UC-1 ext 5a — the Server is down at the launch" world
             (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

    expect "5a nothing opens, and the Client is told GenPRES is unavailable"
        (openCount serverDown = 0
         && sawTo (GenPresClient(BrowserId 1)) (function ServerUnreachable -> true | _ -> false))

    let retried =
        step "UC-1 ext 7a — the Server comes back, and F5 retries within Rule 3's window" serverDown
             (ticks 2 @ [ envt GenPresServer (Start GenPresServer); atClient 1 Refresh ])

    expect "7a the parked credential is still good, and the Session opens"
        (openCount retried = 1 && saw (function SessionOpened _ -> true | _ -> false))

    let expired =
        step "UC-1 ext 7a — but not past credentialTtl (Rule 3, Rule 28)" serverDown
             (ticks 10 @ [ envt GenPresServer (Start GenPresServer); atClient 1 Refresh ])

    expect "7a an aged credential opens nothing"
        (openCount expired = 0
         && saw (function LaunchRejected(_, _, CredentialExpired) -> true | _ -> false)
         && saw (function LaunchRefused -> true | _ -> false))

    // ── Rule 39 — the credential is erased at its first presentation ──
    // A refresh is the same page retrying from its own memory; a reload is a new page,
    // and what it has to re-present is the address bar — scrubbed, and empty.
    expect "Rule 39 nothing of the launch is left in the bar once the Client has presented it"
        ((clientOf 1 retried |> Option.bind _.UrlCredential) = None
         && (clientOf 1 retried |> Option.bind _.RetryCredential) = None)

    let reloaded =
        step "Rule 39 — a full reload after the scrub finds nothing to present" serverDown
             (ticks 2 @ [ envt GenPresServer (Start GenPresServer); atClient 1 ReloadPage ])

    expect "Rule 39 a reload re-presents nothing, so it opens nothing (Consequence 4)"
        (never (function RedeemLaunch _ -> true | _ -> false)
         && openCount reloaded = 0
         && (clientOf 1 reloaded |> Option.bind _.RetryCredential) = None)

    // ── UC-1 ext 7b — the LaunchCredential is stolen before the Client presents it ──
    // The credential is in a URL (Consequence 4): whoever presents it first wins
    // (Rule 2), so within its lifetime a thief gains A's Session. Park it unredeemed —
    // a Server that was down when the browser opened — so the thief can actually get
    // there first. Left to itself the legitimate Client redeems inside the launch
    // cascade and no thief could ever win, which is the race below, not this one.
    let parked =
        step "UC-1 ext 7b — the credential sits unredeemed in the address bar" world
             (envt GenPresServer (Stop GenPresServer) :: launchAs ucA.Login (Some pat1))

    let thief =
        step "UC-1 ext 7b — a thief presents it first, and gains A's Session" parked
             [
                 envt GenPresServer (Start GenPresServer)
                 {
                     From = GenPresClient(BrowserId 99)
                     To = GenPresServer
                     Msg = RedeemLaunch(LaunchCredential "cred-0001")
                 }
             ]

    expect "7b the thief holds a live Session — and it is bound to A (Rules 2, 5)"
        (openCount thief = 1
         && (newestRecord thief |> Option.bind _.User |> Option.map _.UserId) = Some ucA.UserId
         && sidAt 99 thief = (newestRecord thief |> Option.map _.Id))

    let aLocked = step "UC-1 ext 7b — and A's own retry is refused" thief [ atClient 1 Refresh ]

    expect "7b whoever presents it first wins; the loser gets nothing (Rule 2)"
        (openCount aLocked = 1
         && saw (function LaunchRejected(_, _, AlreadyRedeemed) -> true | _ -> false)
         && saw (function LaunchRefused -> true | _ -> false))

    // The damage is bounded: the thief saves at most Unsigned work in A's name.
    let thiefSaved =
        step "UC-1 ext 7b — the thief can save, and it is attributed to A" aLocked
             [
                 act 99 (Prescribes(OrderContextId "oc-stolen"))
                 act 99 Saves
             ]

    expect "7b Unsigned work in A's name — attribution is per credential, not per person (Rules 14, 32)"
        (planCount pat1 thiefSaved = 1
         && (headOf pat1 thiefSaved |> Option.map _.By) = Some ucA
         && (headOf pat1 thiefSaved |> Option.map _.Signed) = Some false)

    let thiefBlocked =
        step "UC-1 ext 7b — but cannot sign: signing needs A's PIN" thiefSaved
             [ act 99 (Signs(Pin "guess")) ]

    expect "7b nothing is Signed, and the guess costs A an attempt (Concept 14; Rules 22, 27)"
        (saw (function PinRejected _ -> true | _ -> false)
         && (recordFor pat1 thiefBlocked |> PatientRecord.latestSigned).IsNone
         && (credentialOf ucA thiefBlocked |> Option.map _.AttemptCount) = Some 1)

    let evicted =
        step "UC-1 ext 7b — A's own next launch evicts them" thiefBlocked (launchAs ucA.Login (Some pat1))

    expect "7b one open Session, the thief's superseded, and A is told something held it (Rules 7, 10)"
        (openCount evicted = 1
         && (match stateOf 1 evicted with Some(Ended(Superseded, _)) -> true | _ -> false)
         && saw (function PriorSessionNotice _ -> true | _ -> false))

    // Single use and short lifetime are the containment, not prevention (Rules 2, 3, 28).
    // The other ordering is legal too, and is what happens when nothing delays the
    // legitimate Client: it redeems inside the launch cascade, and the thief arrives
    // to a credential already spent.
    let lostRace =
        step "UC-1 ext 7b — a thief arriving second gets nothing" world
             (launchAs ucA.Login (Some pat1)
              @ [
                  {
                      From = GenPresClient(BrowserId 99)
                      To = GenPresServer
                      Msg = RedeemLaunch(LaunchCredential "cred-0001")
                  }
                ])

    expect "7b arriving second, the thief is refused and opens nothing (Rule 2)"
        (openCount lostRace = 1
         && saw (function LaunchRejected(_, _, AlreadyRedeemed) -> true | _ -> false)
         && sidAt 99 lostRace = None)

    // ── UC-1 ext 8a — the credential is expired or already redeemed ──
    // Covered by 7a and 7b above: both end in LaunchRefused, which carries no reason.
    // ext 8b — the Server cannot reach the Broker.
    // Starting from ext 5a: the credential is still parked and unredeemed, so there is
    // something for the Server to fail to redeem.
    let _ =
        step "UC-1 ext 8b — the Broker is unreachable at redemption" serverDown
             [
                 envt GenPresServer (Start GenPresServer)
                 envt Broker (Stop Broker)
                 atClient 1 Refresh
             ]

    expect "8b redemption fails and the launch is refused"
        (saw (function LaunchRejected(_, _, BrokerUnreachable) -> true | _ -> false)
         && saw (function LaunchRefused -> true | _ -> false))

    // ── UC-1 ext 9a — the UserRegistry cannot say what the login may do ──
    let registryDown =
        step "UC-1 ext 9a — the registry is unreachable" world
             (envt UserRegistry (Stop UserRegistry) :: launchAs ucA.Login (Some pat1))

    expect "9a no launched Session, and rights fail closed (Rules 5, 6)"
        (openCount registryDown = 0
         && saw (function AuthorityUnavailable -> true | _ -> false))

    expect "9a the anonymous open is offered — relaunching would not cure this"
        ((clientOf 1 registryDown |> Option.map _.AnonymousOffer) = Some true)

    let wentAnonymous =
        step "UC-1 ext 9a — A accepts, and gets a fresh anonymous open (Rule 6)" registryDown
             [ atClient 1 AcceptAnonymousOffer ]

    expect "9a it carries nothing over from the launch: no User, no Patient"
        (openCount wentAnonymous = 1
         && (newestRecord wentAnonymous |> Option.bind _.User) = None
         && (newestRecord wentAnonymous |> Option.bind _.Patient) = None)

    // ── UC-1 ext 9b — the launching User is a Reader ──
    let asReader = step "UC-1 ext 9b — C, a Reader, launches for Patient 3" world (launchAs ucC.Login (Some pat3))

    expect "9b a Session opens, with the Reader Role"
        ((newestRecord asReader |> Option.bind _.User |> Option.map _.Role) = Some Reader)

    expect "9b a Reader is never asked for a PIN — not asked and ignored, but not asked (Rule 25)"
        (never (function ReadCredential _ -> true | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    expect "9b and starts from the most recent Signed TreatmentPlan, not A's Unsigned head (Rules 18, 19)"
        (openedAt 1 asReader = Some p3Signed.Id)

    // ── UC-1 ext 10a — User A has no PIN yet ──
    // First launch as a Prescriber. UC-2 is this case in full: a PIN must be set
    // before the launch continues (Rule 24).

    // ── UC-1 ext 11a — the PatientDataPlatform is unreachable ──
    let noPlatform =
        step "UC-1 ext 11a — the PatientDataPlatform is unreachable" world
             (envt PatientDataPlatform (Stop PatientDataPlatform) :: launchAs ucA.Login (Some pat2))

    expect "11a the launch continues: a PatientId and no data (Concept 2)"
        (openCount noPlatform = 1
         && (newestRecord noPlatform |> Option.bind _.Patient) = Some pat2
         && dataAt 1 noPlatform = None)

    expect "11a TreatmentPlans work as normal — the PatientId is there (Rule 12)"
        (openedAt 1 noPlatform = Some p2Signed.Id)

    // ── UC-1 ext 13a / 14a — A already has an open Session, or the wrong Patient ──
    // Rule 7 is per User, not per Patient, so both are the same mechanism: the
    // earlier Session is closed and A is told work in it may have been lost.
    let wrongPatient = step "UC-1 ext 14a — A launched for the wrong Patient" world (launchAs ucA.Login (Some pat1))
    let relaunched =
        step "UC-1 ext 13a/14a — A activates Patient 2 and relaunches" wrongPatient
             (launchAs ucA.Login (Some pat2))

    expect "14a the wrong Session is closed, whichever Patient it was for (Rule 7)"
        (openCount relaunched = 1
         && (newestRecord relaunched |> Option.bind _.Patient) = Some pat2
         && (match stateOf 1 relaunched with Some(Ended(Superseded, _)) -> true | _ -> false))

    expect "13a and A is told, once (Rule 10)"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && wasTold 1 relaunched)

    // ── UC-1 ext 13b — two launches at once ──
    // Rule 7 is a count, and a count read and then written back is a race. Rule 40
    // makes the opening and the closing one act at the Database, so the two orders of
    // arrival have the same answer: one open Session, whichever won.
    let racedLaunches =
        racing "UC-1 ext 13b — two of A's launches arrive at once" world
               (launchAs ucA.Login (Some pat1) @ launchAs ucA.Login (Some pat2))

    expect "13b exactly one Session is open, and the other is Superseded (Rules 7, 40)"
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

    expect "UC-2 the launch stops and asks for a PIN, and offers nothing else (Rules 23, 24)"
        (saw (function PinRequired _ -> true | _ -> false)
         && openCount asked = 0
         && never (function SessionOpened _ -> true | _ -> false))

    // The order matters: a login the registry does not recognise never gets to enrol.
    expect "UC-2 the PIN is offered only after the registry recognised the login (Rule 24)"
        (before (function UserResolved _ -> true | _ -> false)
                (function PinRequired _ -> true | _ -> false))

    let unknown =
        step "UC-2 — a login the registry does not know never reaches the PIN question" noPin
             (launchAs (LoginName "dr.x") (Some pat1))

    expect "UC-2 an unrecognised login is refused before any PIN is offered (UC-1 ext 9a)"
        (openCount unknown = 0
         && saw (function NotAuthorised -> true | _ -> false)
         && never (function PinRequired _ -> true | _ -> false))

    // The prompt is put to one Client, and that Client answers it. Another browser
    // holding the attempt number is not the same thing as the User at that screen.
    let intruder =
        let att = asked.GenPres.Pending |> Map.toList |> List.head |> fst
        step "UC-2 — a second browser answers the prompt A was given" asked
             [
                 {
                     From = GenPresClient(BrowserId 99)
                     To = GenPresServer
                     Msg = SupplyPin(att, Pin "0000")
                 }
             ]

    expect "UC-2 only the Client the prompt was put to may answer it (Concept 7; Rules 22, 24)"
        (saw (function Refused _ -> true | _ -> false)
         && never (function CredentialWritten _ -> true | _ -> false)
         && (credentialOf ucA intruder |> Option.bind _.Pin) = None
         && openCount intruder = 0)

    let enrolled =
        step "UC-2 steps 3 to 5 — A chooses a PIN and the launch continues" asked
             [ atClient 1 (ChoosePin(Pin "9999")) ]

    expect "UC-2 step 4: the PIN is set on A's UserCredential, created since GenPRES held none"
        ((credentialOf ucA enrolled |> Option.bind _.Pin) = Some(Pin "9999"))

    expect "UC-2 step 4: the change is recorded and A is mailed, the first setting included (Rule 26)"
        ((mailsTo mailA enrolled).Length = 1
         && enrolled |> audited "PIN set")

    expect "UC-2 a newly set PIN starts with a count of zero (Rule 27)"
        ((credentialOf ucA enrolled |> Option.map _.AttemptCount) = Some 0)

    expect "UC-2 step 5: the launch continues from UC-1 step 11"
        (openCount enrolled = 1
         && saw (function SessionOpened _ -> true | _ -> false)
         && saw (function PatientDataRead _ -> true | _ -> false))

    // ── UC-2 ext 3a — A does not set a PIN ──
    let askedAgain =
        step "UC-2 ext 3a — A does not set a PIN; the next launch asks again" asked
             (launchAs ucA.Login (Some pat1))

    expect "3a a required PIN is not set, so no Session is opened (Rule 6) — and it asks again"
        (openCount askedAgain = 0
         && saw (function PinRequired _ -> true | _ -> false))

    // A Reader in the same position is never asked at all.
    let readerNoPin =
        step "UC-2 — a Reader with no PIN is never asked (Rule 25)" noPin
             (launchAs ucC.Login (Some pat2))

    expect "UC-2 the Reader's launch is never held up by a PIN"
        (openCount readerNoPin = 1
         && never (function PinRequired _ -> true | _ -> false))

    // Not a document scenario — model hygiene. A launch that stalls mid-flight would
    // otherwise sit in the launch table forever, which is harmless here and a leak in
    // production. Everything but AwaitingPinChoice is waiting on a round trip and is
    // collectable; that one waits on a human and is not.
    let stalled =
        let ctx =
            {
                Client = GenPresClient(BrowserId 1)
                Launch = LaunchNo 1
                Assertion = { Login = ucA.Login; Patient = Some pat1 }
            }
        { world with
            GenPres.Pending =
                Map.empty
                |> Map.add (AttemptId 90) { Stage = AwaitingUser ctx; Since = 0 }
                |> Map.add (AttemptId 91) { Stage = AwaitingPinChoice(ctx, ucA, mailA); Since = 0 } }

    let swept =
        step "UC-2 — an abandoned launch is collected; one waiting on a human is not" stalled
             (ticks (launchAbandonTtl + 5))

    expect "UC-2 a launch stalled mid-flight is dropped; one suspended on a human is kept (UC-2 step 3)"
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
        (never (function CreateBlocked _ -> true | _ -> false)
         && never (function UnsignedWorkNotice _ -> true | _ -> false))

    expect "UC-3 step 2: an Unsigned TreatmentPlan is appended, carrying A's UserContext (Rule 14)"
        (planCount pat2 saved = 2
         && (headOf pat2 saved |> Option.map _.Signed) = Some false
         && (headOf pat2 saved |> Option.map _.By) = Some ucA)

    expect "UC-3 step 2: and its base (Concept 13)"
        ((headOf pat2 saved |> Option.bind _.Base) = Some p2Signed.Id)

    expect "UC-3 Rule 14: the OrderContext changed in the Session is stamped"
        (headOf pat2 saved
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.forall (fun o -> o.Stamp = Some ucA))

    expect "UC-3 Rule 33: the create carried the opened-with token, and a new one came back"
        (saw (function
              | SessionRequest(_, CreateTreatmentPlan req) -> Token.plan req.Opened = Some p2Signed.Id
              | _ -> false)
         && openedAt 1 saved = (headOf pat2 saved |> Option.map _.Id))

    let signed = step "UC-3 step 3 — A signs" saved [ act 1 (Signs pinA) ]

    expect "UC-3 step 3: a Signed TreatmentPlan in A's name (Concept 14, Rules 14, 15)"
        (planCount pat2 signed = 3
         && (headOf pat2 signed |> Option.map _.Signed) = Some true
         && (headOf pat2 signed |> Option.map _.By) = Some ucA)

    expect "UC-3 step 3: it is now the most recent Signed TreatmentPlan and counts clinically (Rule 16)"
        ((recordFor pat2 signed |> PatientRecord.latestSigned |> Option.map _.Id)
            = (headOf pat2 signed |> Option.map _.Id))

    expect "UC-3 the correct entry reset the wrong-entry count (Rule 27)"
        ((credentialOf ucA signed |> Option.map _.AttemptCount) = Some 0)

    // ── UC-3 ext 2a — the record has moved on since A opened ──
    // If what appeared is Unsigned, A is notified and may create anyway or hold off
    // (Rule 21). If a Signed TreatmentPlan appeared, creating is blocked (Rule 20). UC-6 is
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
    let wrongOnce = step "UC-3 ext 3b — A gives the wrong PIN" saved [ act 1 (Signs(Pin "0000")) ]

    expect "3b verification fails and no TreatmentPlan is created"
        (planCount pat2 wrongOnce = 2
         && saw (function PinRejected _ -> true | _ -> false))

    expect "3b the count is on the UserCredential, not the Session (Rule 27)"
        ((credentialOf ucA wrongOnce |> Option.map _.AttemptCount) = Some 1)

    let atLimit =
        step "UC-3 ext 3b — and at the limit the Session ends (Rules 9, 27)" wrongOnce
             [
                 act 1 (Signs(Pin "0000"))
                 act 1 (Signs(Pin "0000"))
             ]

    expect "3b the Session ends at the wrong-PIN limit"
        (openCount atLimit = 0
         && (match stateOf 1 atLimit with Some(Ended(WrongPinLimit, _)) -> true | _ -> false)
         && saw (function SessionEnded(Some WrongPinLimit) -> true | _ -> false))

    expect "3b the count survives the Session: it is not a fresh start"
        ((credentialOf ucA atLimit |> Option.map _.AttemptCount) = Some wrongPinLimit
         && (credentialOf ucA atLimit |> Option.map _.Suspended) = Some true)

    // Rule 27. What survives is not merely a number but the standing of the
    // credential: a fresh Session does not hand back the attempts, and the correct PIN
    // does not either. Only a Rule 37 replacement does.
    let relaunchedAfterLimit =
        quiet "3b — A relaunches after the limit" atLimit (launchAs ucA.Login (Some pat2))

    let stillLocked =
        step "UC-3 ext 3b — a new Session, the right PIN, and signing is still locked" relaunchedAfterLimit
             [ act 2 (Prescribes(OrderContextId "oc-locked")); act 2 (Signs pinA) ]

    expect "3b the correct PIN does not unlock it: the credential is suspended (Rule 27)"
        (saw (function SigningLocked -> true | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false)
         && planCount pat2 stillLocked = planCount pat2 relaunchedAfterLimit
         && openCount stillLocked = 1)

    // And Rule 37 is the way out: a code by mail, a new PIN, one act.
    let askedForReset = quiet "3b — A asks for a reset" stillLocked [ act 2 AsksPinReset ]

    let unlocked =
        let code = (codeInMail mailA askedForReset).Value
        step "UC-3 ext 3b — the mailed code replaces the PIN, and signing works again (Rule 37)" askedForReset
             [ act 2 (EntersResetCode(code, Pin "4242")); act 2 (Signs(Pin "4242")) ]

    expect "3b the replacement clears the suspension and the count with it (Rules 27, 37)"
        ((credentialOf ucA unlocked |> Option.map _.Suspended) = Some false
         && (credentialOf ucA unlocked |> Option.map _.AttemptCount) = Some 0
         && planCount pat2 unlocked = planCount pat2 askedForReset + 1
         && (headOf pat2 unlocked |> Option.map _.Signed) = Some true)

    // ── UC-3 ext 3c — A signs without saving first ──
    // Steps 2 and 3 become one act, and the block and notification checks run before
    // the PIN is asked for. Set up a block, and watch nothing ask for a credential.
    let bSigned =
        quiet "UC-3 ext 3c setup — B signs while A is open" opened
              (launchAs ucB.Login (Some pat2)
               @ [ act 2 (Prescribes(OrderContextId "oc-5")); act 2 (Signs pinB) ])

    let blocked =
        step "UC-3 ext 3c — A signs without saving, and is blocked before the PIN" bSigned
             [ act 1 (Prescribes(OrderContextId "oc-6")); act 1 (Signs pinA) ]

    expect "3c the block is decided first: no credential is ever read (Rules 20, 22)"
        (saw (function CreateBlocked _ -> true | _ -> false)
         && never (function ReadCredential(ForRequest _, _) -> true | _ -> false))

    expect "3c and nothing was appended"
        (planCount pat2 blocked = planCount pat2 bSigned)

    // ── UC-3 ext 3d — the signature modal ──
    // Rule 43. Between the challenge and the signature the User is looking at exactly
    // what they are about to attest to, and the Client will not let it change under
    // them. Leaving the modal costs nothing: the next signature is asked for afresh.
    let modalUp =
        let sid = (sidAt 1 signed).Value
        { signed with
            Clients =
                signed.Clients
                |> Map.map (fun (BrowserId b) c ->
                    if b = 1 then
                        { c with Modal = Some(Token.mintChallenge signed.Env.Now sid (Some pat2) "sha|shown") }
                    else c) }

    let heldStill =
        step "UC-3 ext 3d — with the modal up, the WorkPlan cannot change" modalUp
             [ act 1 (Prescribes(OrderContextId "oc-late")); act 1 (EntersPatientData(PatientData "by hand")) ]

    expect "3d the Client refuses locally: nothing is sent, and the WorkPlan is untouched (Rule 43)"
        (workingAt 1 heldStill = workingAt 1 signed
         && dataAt 1 heldStill = dataAt 1 signed
         && never (function SessionRequest _ -> true | _ -> false))

    let cancelled = step "UC-3 ext 3d — the User leaves the modal" heldStill [ act 1 CancelsSign ]

    expect "3d nothing was signed, and prescribing is possible again"
        (planCount pat2 cancelled = planCount pat2 signed
         && (clientOf 1 cancelled |> Option.bind _.Modal) = None)

    let signedAfresh =
        step "UC-3 ext 3d — and the next signature asks for a challenge of its own" cancelled
             [ act 1 (Prescribes(OrderContextId "oc-7")); act 1 (Signs pinA) ]

    expect "3d the honest path never sees a refusal: a fresh challenge, and the signature lands"
        (saw (function SignChallengeIssued _ -> true | _ -> false)
         && never (function CreateRefused _ -> true | _ -> false)
         && (headOf pat2 signedAfresh |> Option.map _.Signed) = Some true)

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
                         CreateTreatmentPlan
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
        (saw (function CreateRefused why -> why.Contains "Rule 43" | _ -> false)
         && planCount pat2 mismatched = planCount pat2 signedAfresh)

    // ── UC-3 ext 3f — the reply was lost and the create is sent again ──
    // Rule 45. The retry carries the key of the request it retries, so the Database
    // answers it rather than doing it twice.
    let duplicated =
        let sid = (sidAt 1 signedAfresh).Value
        let opened = (clientOf 1 signedAfresh).Value.Opened.Value
        let again =
            SessionRequest(
                sid,
                CreateTreatmentPlan
                    {
                        Work = workOf 1 signedAfresh
                        Opened = opened
                        Notice = None
                        Challenge = None
                        DataOk = None
                        Pin = None
                        Key = IdemKey "retry-1"
                    })
        step "UC-3 ext 3f — the same create arrives twice" signedAfresh
             [ fromClient 1 again; fromClient 1 again ]

    expect "3f one TreatmentPlan, and the same answer both times (Rule 45)"
        (planCount pat2 duplicated = planCount pat2 signedAfresh + 1
         && countOf (function TreatmentPlanCreated _ -> true | _ -> false) = 2
         && (lastTrace
             |> List.choose (function { Msg = TreatmentPlanCreated(id, _, _) } -> Some id | _ -> None)
             |> List.distinct
             |> List.length) = 1)

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
             [ act 1 (Prescribes(OrderContextId "oc-8")); act 1 (Signs pinA) ]

    expect "Rule 44 the signature does not land: the User is shown what the platform now holds"
        (saw (function PatientDataChanged _ -> true | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false)
         && planCount pat2 stoppedAtData = planCount pat2 signedAfresh
         && dataAt 1 stoppedAtData = Some(PatientData "pat-2: 7y, 26kg — revised"))

    let acceptedData =
        step "Rule 44 — A reads the new data and signs again" stoppedAtData [ act 1 (Signs pinA) ]

    expect "Rule 44 accepted, the signature lands (Rules 21, 34's pattern, over data)"
        (planCount pat2 acceptedData = planCount pat2 stoppedAtData + 1
         && (headOf pat2 acceptedData |> Option.map _.Signed) = Some true)

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

    expect "UC-4 step 2: and the create waits — the User may still choose not to"
        (planCount pat3 warned = 2)

    expect "UC-4 step 2: the notice came with a token naming what it disclosed (Rule 34)"
        ((noticeAt 1 warned |> Option.map Token.disclosed) = Some [ p3Unsigned.Id ])

    // Rule 34: proceeding is re-sending the create with that token. There is no
    // `Proceed` message; holding off is simply not sending this.
    let bSaved = step "UC-4 step 2 — B chooses to create anyway, returning the token" warned [ act 1 Saves ]

    expect "UC-4 step 2: an Unsigned TreatmentPlan of B's own is appended (Rules 14, 34)"
        (planCount pat3 bSaved = 3
         && (headOf pat3 bSaved |> Option.map _.By) = Some ucB
         && (headOf pat3 bSaved |> Option.map _.Signed) = Some false)

    expect "UC-4 step 2: and the notice is spent — the token does not linger (Rule 34)"
        (noticeAt 1 bSaved = None)

    let bSigned = step "UC-4 step 3 — B signs" bSaved [ act 1 (Signs pinB) ]

    expect "UC-4 step 3: a Signed TreatmentPlan in B's name; it now counts clinically (Rules 15, 16)"
        ((headOf pat3 bSigned |> Option.map _.Signed) = Some true
         && (recordFor pat3 bSigned |> PatientRecord.latestSigned |> Option.map _.By) = Some ucB)

    // ── step 4 — A's Unsigned work is superseded ──
    let aReturns = step "UC-4 step 4 — A launches for Patient 3 after B signed" bSigned (launchAs ucA.Login (Some pat3))

    expect "UC-4 step 4: A's Session starts from B's Signed TreatmentPlan (Rule 19)"
        (openedAt 2 aReturns
            = (recordFor pat3 aReturns |> PatientRecord.latestSigned |> Option.map _.Id))

    // "Nobody but User A could ever open it, and now not even User A can act on it."
    // Rule 18 does still let A open their own Unsigned TreatmentPlan — it is unqualified.
    // What has gone is the acting: Rule 20 blocks creating anything from it, because
    // B's Signed TreatmentPlan is newer than the one A would then have opened with.
    let aOnDeadEnd =
        step "UC-4 step 4 — A opens the old work, and can do nothing with it" aReturns
             [
                 act 2 (OpensTreatmentPlan p3Unsigned.Id)
                 act 2 (Signs pinA)
             ]

    expect "UC-4 step 4: A may still open their own Unsigned TreatmentPlan (Rule 18)"
        (saw (function TreatmentPlanOpened(id, _, _) -> id = p3Unsigned.Id | _ -> false))

    expect "UC-4 step 4: but creating anything from it is blocked, for good (Rule 20)"
        (saw (function CreateBlocked _ -> true | _ -> false)
         && (headOf pat3 aOnDeadEnd |> Option.map _.By) = Some ucB)

    // ── UC-4 ext 2a — B holds off at the notification ──
    // There is nothing to send: under Rule 34 the create is only made by returning the
    // token, so holding off is the absence of a message. `warned` is that state.
    expect "2a nothing is created; both TreatmentPlans stand, each usable only by its own User"
        (planCount pat3 warned = 2
         && (headOf pat3 warned |> Option.map _.Id) = Some p3Unsigned.Id)

    // ── UC-4 ext 4a — A launches before B signs ──
    let aBeforeBSigns =
        step "UC-4 ext 4a — A launches for Patient 3 before B signs" bSaved (launchAs ucA.Login (Some pat3))

    expect "4a A starts from A's own Unsigned head: B's is Unsigned too, so it does not supersede (Rule 19)"
        (openedAt 2 aBeforeBSigns = Some p3Unsigned.Id)

    let aSignsFirst =
        step "UC-4 ext 4a — A may sign: no newer Signed TreatmentPlan exists (Rule 20)" aBeforeBSigns
             [ act 2 (Signs pinA) ]

    expect "4a A is notified of B's newer Unsigned work (Rule 21), and nothing is created yet"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucB | _ -> false))

    let aWon = step "UC-4 ext 4a — A re-sends with the token, and signing first blocks B" aSignsFirst [ act 2 (Signs pinA) ]

    expect "4a whichever of the two signs first blocks the other (Rule 20)"
        ((recordFor pat3 aWon |> PatientRecord.latestSigned |> Option.map _.By) = Some ucA)

    let bNowBlocked = step "UC-4 ext 4a — B tries to sign after A did" aWon [ act 1 (Signs pinB) ]

    expect "4a B is blocked by A's Signed TreatmentPlan (Rule 20)"
        (saw (function CreateBlocked _ -> true | _ -> false))

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
        step "UC-5 steps 1 to 4 — B works and saves in A's Session" aWalksAway
             [
                 act 1 (Prescribes(OrderContextId "oc-8"))
                 act 1 Saves
             ]

    expect "UC-5 step 4: the TreatmentPlan is created under the Session's credentials — A's (Rules 14, 32)"
        (planCount pat1 bSaves = 1
         && (headOf pat1 bSaves |> Option.map _.By) = Some ucA)

    expect "UC-5 step 4: and so are the stamps on every OrderContext B changed (Rules 14, 35)"
        (headOf pat1 bSaves
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.forall (fun o -> o.Stamp = Some ucA))

    // Step 5: signing always names the Session's User, so the Client asks for A's PIN.
    // Step 6: B does not have it. Supplying their own proves nothing — the Server
    // verifies against the Session's User's credential (Rules 14, 22, 32).
    let bTriesToSign = step "UC-5 steps 5 and 6 — B signs, with the only PIN they have" bSaves [ act 1 (Signs pinB) ]

    expect "UC-5 step 6: the work stays Unsigned and does not count clinically (Rules 15, 16)"
        (saw (function PinRejected _ -> true | _ -> false)
         && (headOf pat1 bTriesToSign |> Option.map _.Signed) = Some false
         && (recordFor pat1 bTriesToSign |> PatientRecord.latestSigned).IsNone)

    // Signing always names the Session's User, so verification runs against A's
    // credential whoever is at the keyboard — which is exactly what caps B's guessing
    // in ext 6a, and why it costs A their allowance rather than B's.
    expect "UC-5 the wrong entry counted against the Session's User's credential — A's, not B's (Rules 22, 27, 32)"
        ((credentialOf ucB bTriesToSign |> Option.map _.AttemptCount) = Some 0
         && (credentialOf ucA bTriesToSign |> Option.map _.AttemptCount) = Some 1)

    // ── UC-5 ext 5a — B relaunches as themselves ──
    let bOwnSession =
        step "UC-5 ext 5a — B relaunches from MainEHR as themselves, Patient 1 active" bSaves
             (launchAs ucB.Login (Some pat1))

    expect "5a Rule 7 is per User: a Session of B's own opens, and A's is untouched"
        (openCount bOwnSession = 2
         && (openOfUser ucA bOwnSession).Length = 1
         && (openOfUser ucB bOwnSession).Length = 1)

    expect "5a it starts from nothing: no Signed TreatmentPlan, and the Unsigned one is A's (Rules 18, 19)"
        (openedAt 2 bOwnSession = None)

    let bReEnters =
        step "UC-5 ext 5a — B re-enters the work and signs; the notice comes first" bOwnSession
             [
                 act 2 (Prescribes(OrderContextId "oc-8"))
                 act 2 (Signs pinB)
             ]

    expect "5a B is notified of the newer Unsigned TreatmentPlan (Rule 21)"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false))

    let bSignedOwn = step "UC-5 ext 5a — B re-sends with the token (Rule 34)" bReEnters [ act 2 (Signs pinB) ]

    expect "5a and signs as themselves (Rules 14, 15)"
        ((headOf pat1 bSignedOwn |> Option.map _.By) = Some ucB
         && (headOf pat1 bSignedOwn |> Option.map _.Signed) = Some true)

    // ── UC-5 ext 5b — B cannot log in to MainEHR at that workstation ──
    // No path to a Session of B's own. The work stays Unsigned until A opens it in a
    // Session of their own and signs; nobody else can.
    expect "5b the work stays Unsigned, and only A can ever act on it (Rules 18, 19)"
        ((recordFor pat1 bSaves |> PatientRecord.mayOpen ucB.UserId (headOf pat1 bSaves).Value.Id).IsNone
         && (recordFor pat1 bSaves |> PatientRecord.mayOpen ucA.UserId (headOf pat1 bSaves).Value.Id).IsSome)

    // ── UC-5 ext 6a — B guesses instead ──
    let guessed =
        step "UC-5 ext 6a — B guesses at A's PIN" bTriesToSign
             [
                 act 1 (Signs(Pin "0001"))
                 act 1 (Signs(Pin "0002"))
             ]

    expect "6a at the configured number of consecutive wrong entries the Session ends (Rules 9, 27)"
        (openCount guessed = 0
         && (match stateOf 1 guessed with Some(Ended(WrongPinLimit, _)) -> true | _ -> false))

    expect "6a the Unsigned TreatmentPlan stays, and A is told of the ending (Rule 10)"
        (planCount pat1 guessed = 1
         && saw (function SessionEnded(Some WrongPinLimit) -> true | _ -> false))

    let relaunchNoHelp =
        step "UC-5 ext 6a — relaunching as A does not reset the count (Rule 27)" guessed
             (launchAs ucA.Login (Some pat1) @ [ act 2 (Signs(Pin "0003")) ])

    expect "6a the count belongs to the UserCredential, so guessing is capped outright"
        ((credentialOf ucA relaunchNoHelp |> Option.map _.AttemptCount |> Option.map (fun c -> c >= wrongPinLimit))
            = Some true)

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
                 act 1 (Signs pinA)
             ]

    expect "UC-6 step 2: an Unsigned then a Signed TreatmentPlan in A's name"
        (planCount pat2 aSigned = 3
         && (headOf pat2 aSigned |> Option.map _.Signed) = Some true
         && (headOf pat2 aSigned |> Option.map _.By) = Some ucA)

    // Consequence 6: neither User saw the other's work — a Client only learns anything
    // at its own next request.
    let bBlocked =
        step "UC-6 step 3 — B saves, and is blocked" aSigned
             [
                 act 2 (Prescribes(OrderContextId "oc-b"))
                 act 2 Saves
             ]

    expect "UC-6 step 3: a Signed TreatmentPlan newer than the one B opened with blocks the create (Rule 20)"
        (saw (function CreateBlocked _ -> true | _ -> false)
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
                 act 2 (Signs pinB)
             ]

    expect "UC-6 step 4: the signature attests the whole set in B's name (Rules 14, 15)"
        ((headOf pat2 bReapplied |> Option.map _.By) = Some ucB
         && (headOf pat2 bReapplied |> Option.map _.Signed) = Some true)

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
        (never (function CreateBlocked _ -> true | _ -> false)
         && planCount pat2 bSavedFirst = 2)

    let _ =
        step "UC-6 ext 2a — but A is notified when creating (Rule 21)" bSavedFirst
             [ act 1 Saves ]

    expect "2a A is told whose work it is, and may proceed or hold off"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucB | _ -> false))

    // Nothing attested is ever lost: the PatientRecord is append-only (Concept 12), so
    // a Signed TreatmentPlan survives whatever follows. What is not protected is Unsigned
    // work: superseded, it can never be signed (Rules 19, 20).
    expect "UC-6 nothing attested is lost: A's Signed TreatmentPlan survives B's (Concept 12)"
        (recordFor pat2 bReapplied
         |> _.Plans
         |> List.exists (fun s -> s.Signed && s.By = ucA))

    // ── UC-6 ext 2b — both sign at once ──
    // Rule 36's predicate, in the form Rule 42 gave it: a create lands only if no
    // Signed TreatmentPlan newer than its base has arrived. Two signatures over the
    // same base, in flight together — exactly one can be true of both.
    let bothSign =
        racing "UC-6 ext 2b — A and B sign over the same base at once" both
               [
                   act 1 (Prescribes(OrderContextId "oc-a2"))
                   act 2 (Prescribes(OrderContextId "oc-b2"))
                   act 1 (Signs pinA)
                   act 2 (Signs pinB)
               ]

    expect "2b exactly one signature landed, and the record moved once (Rules 36, 42)"
        (countOf (function TreatmentPlanCreated(_, true, _) -> true | _ -> false) = 1
         && planCount pat2 bothSign = planCount pat2 both + 1)

    expect "2b the loser is told whose work stands in the way, and never which TreatmentPlan (Rules 17, 18, 20)"
        (countOf (function CreateBlocked _ -> true | _ -> false) = 1
         && saw (function CreateBlocked uc -> uc = ucA || uc = ucB | _ -> false))

    // ── Rule 17 — an older Signed TreatmentPlan is readable, and not a place to build ──
    let history =
        quiet "Rule 17 precondition — a record with two Signed TreatmentPlans" bReapplied
              (launchAs ucA.Login (Some pat2))

    let older =
        recordFor pat2 history
        |> _.Plans
        |> List.filter _.Signed
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
        (saw (function CreateBlocked _ -> true | _ -> false)
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

    let asked = step "UC-7 steps 1 and 2 — A asks GenPRES to reset the PIN" opened [ act 1 AsksPinReset ]

    expect "UC-7 step 2: nothing is removed — the PIN in force is still the old one (Rule 37)"
        ((credentialOf ucA asked |> Option.bind _.Pin) = Some pinA
         && saw (function ResetCodeMailed -> true | _ -> false))

    // Rule 26 has to reach A with no Session in memory to ask, so the address comes
    // off the SessionRecord (Concept 9). The record of the ask says a code went out;
    // it does not say which.
    expect "UC-7 step 2: a one-time code goes to the registry's address, and the ask is recorded (Rules 26, 37)"
        ((mailsTo mailA asked).Length = 1
         && (codeInMail mailA asked).IsSome
         && asked |> audited "PIN reset code sent")

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
        step "UC-7 step 3 — A reads the mail and replaces the PIN in one act" asked
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "UC-7 step 3: replaced, never removed — there is no PIN-less moment (Concept 7, Rule 37)"
        ((credentialOf ucA replaced |> Option.bind _.Pin) = Some(Pin "5555")
         && saw (function PinChanged -> true | _ -> false)
         && never (function ResetDenied _ -> true | _ -> false))

    expect "UC-7 step 3: mailed and recorded, and the new PIN starts at zero (Rules 26, 27)"
        ((mailsTo mailA replaced).Length = 2
         && replaced |> audited "PIN replaced"
         && (credentialOf ucA replaced |> Option.map _.AttemptCount) = Some 0)

    let signs =
        step "UC-7 step 4 — A signs with the new PIN, in the Session they were already in" replaced
             [ act 1 (Prescribes(OrderContextId "oc-r")); act 1 (Signs(Pin "5555")) ]

    expect "UC-7 step 4: the new PIN signs, and no relaunch was needed (Concept 14)"
        ((headOf pat2 signs |> Option.map _.Signed) = Some true
         && (headOf pat2 signs |> Option.map _.By) = Some ucA)

    let spent =
        step "UC-7 step 4 — and the code is spent: honoured once, and never again" signs
             [ act 1 (EntersResetCode(code, Pin "7777")) ]

    expect "UC-7 step 4: a spent code buys nothing, and the PIN it already replaced stands"
        (saw (function ResetDenied NoResetPending -> true | _ -> false)
         && (credentialOf ucA spent |> Option.bind _.Pin) = Some(Pin "5555"))

    // ── UC-7 ext 2a — the code is not used in time ──
    // What expires a code is time, and time here runs in handled messages: waiting a
    // code out by ticking would idle the Session out first (Rule 9), which is a
    // different scenario. So the code is aged instead — its expiry moved into the
    // past, which is exactly what the wait would have done to it.
    let aged =
        { asked with
            Database.Private.Resets =
                asked.Database.Private.Resets |> Map.map (fun _ r -> { r with Expires = asked.Env.Now - 1 }) }

    let expiredCode =
        step "UC-7 ext 2a — A leaves the code unused until it dies (Rule 37)" aged
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "2a an aged code replaces nothing, and the old PIN is untouched"
        (saw (function ResetDenied ResetExpired -> true | _ -> false)
         && (credentialOf ucA expiredCode |> Option.bind _.Pin) = Some pinA)

    // ── UC-7 ext 3a — the code is guessed at ──
    // The count is the code's own, not the credential's (Rule 27): guessing at a code
    // must not lock a PIN that is still perfectly good.
    let voided =
        step "UC-7 ext 3a — a few wrong codes, and this one is void" asked
             [ for i in 1 .. wrongCodeLimit -> act 1 (EntersResetCode(ResetCode $"code-wrong%i{i}", Pin "0000")) ]

    expect "3a the code is void, and the PIN it would have replaced is untouched"
        (saw (function ResetDenied ResetVoid -> true | _ -> false)
         && (credentialOf ucA voided |> Option.bind _.Pin) = Some pinA
         && (credentialOf ucA voided |> Option.map _.AttemptCount) = Some 0)

    let afterVoid =
        step "UC-7 ext 3a — the mailed code is void too: the reset is gone, not merely wrong" voided
             [ act 1 (EntersResetCode(code, Pin "5555")) ]

    expect "3a even the right code buys nothing now — a fresh reset means a fresh mail"
        (saw (function ResetDenied NoResetPending -> true | _ -> false)
         && (credentialOf ucA afterVoid |> Option.bind _.Pin) = Some pinA)

    let freshMail = step "UC-7 ext 3a — and A asks again" afterVoid [ act 1 AsksPinReset ]

    expect "3a a second code goes out, and it is not the first one"
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
    // LaunchCredential — and GenPRES cannot know who is at the keyboard.
    let anon = step "UC-8 steps 1 to 3 — A opens the GenPRES address in a browser" world [ atClient 1 OpenDirectly ]

    expect "UC-8 step 3: an anonymous Session — no User, no Role, no PatientId (Rule 13)"
        (openCount anon = 1
         && (newestRecord anon |> Option.bind _.User) = None
         && (newestRecord anon |> Option.bind _.Patient) = None)

    expect "UC-8 step 3: its SessionRecord binds to no User (Concept 9)"
        ((recNo 1 anon |> Option.bind _.User) = None
         && (recNo 1 anon |> Option.bind _.Launch) = None)

    expect "UC-8 anonymous use needs no Role and no UserRegistry check"
        (never (function ResolveUser _ -> true | _ -> false))

    let prescribing =
        step "UC-8 step 4 — A prescribes: Patient Data and OrderContexts by hand" anon
             [
                 act 1 (EntersPatientData(PatientData "3y, 14kg, by hand"))
                 act 1 (Prescribes(OrderContextId "oc-x"))
             ]

    expect "UC-8 step 4: prescribing works, Patient Data included (Concepts 2, 15)"
        ((dataAt 1 prescribing).IsSome && (workingAt 1 prescribing).Length = 1)

    expect "UC-8 step 4: each request refreshes the Session's idle clock (Rules 8, 12)"
        (countOf (function SessionRequest _ -> true | _ -> false) = 2
         && lastSeenOf 1 prescribing > (recNo 1 anon |> Option.map _.LastSeen))

    let noSaving = step "UC-8 step 5 — nothing can be saved" prescribing [ act 1 Saves; act 1 (Signs pinA) ]

    expect "UC-8 step 5: no TreatmentPlan can be opened or created (Rule 12)"
        (saw (function NoTreatmentPlanHere -> true | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false))

    expect "UC-8 neither the PatientRecord nor the PatientDataPlatform is ever touched"
        (never (function ReadRecord _ -> true | _ -> false)
         && never (function ReadPatientData _ -> true | _ -> false))

    expect "UC-8 step 5: the work exists only in the Client (Rule 31)"
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
        step "UC-8 — but it does not live for ever: the outright limit (Rule 13)" idled
             (ticks (anonymousLifetime + 5))

    expect "UC-8 past its limit the anonymous Session is ended, whatever it was doing"
        (openCount outlived = 0
         && (match stateOf 1 outlived with Some(Ended(Idle, _)) -> true | _ -> false))

    expect "UC-8 and nothing is owed by it: there is no User to tell (Rules 10, 13)"
        (noticeOf 1 outlived = Some NotOwed
         && (recNo 1 outlived |> Option.bind _.User).IsNone)

    // ── UC-8 ext 2a — the browser does present a LaunchCredential ──
    // That is a launch: UC-1 from step 7. Covered by UC-1 throughout.

    // ── UC-8 ext 2b — the same Browser later launches properly ──
    // The launched Session is another Session; Rule 7 counts only a User's Sessions,
    // and an anonymous Session binds to none. (The model opens the launch in a fresh
    // tab, since a launch always does; nothing in the Rules turns on that.)
    let alsoLaunched = step "UC-8 ext 2b — the same person later launches properly" idled (launchAs ucA.Login (Some pat1))

    expect "2b the anonymous Session is untouched and may simply live on (Rules 7, 13)"
        (openCount alsoLaunched = 2
         && stateOf 1 alsoLaunched = Some OpenOrGone
         && never (function PriorSessionNotice _ -> true | _ -> false))

    // And the work in the browser is gone when the browser goes — it was only ever
    // there (Rule 31).
    let browserClosed = step "UC-8 step 5 — and it is gone when the browser goes" idled [ atClient 1 CloseBrowser ]

    expect "UC-8 step 5: the cart dies with the browser (Rule 31)"
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

    let told = step "UC-9 steps 3 and 4 — A returns and acts" idled [ act 1 (Prescribes(OrderContextId "oc-later")) ]

    expect "UC-9 step 4: the request is refused and A is told (Rule 10)"
        (saw (function SessionEnded(Some Idle) -> true | _ -> false)
         && wasTold 1 told)

    // Rule 10. Delivery is not the end of it: the Server cannot see a screen
    // (Consequence 6), so what spends the obligation is the User saying they have seen
    // it. Until then the notice may be shown again — better twice than never.
    expect "UC-9 step 4: delivered, and not yet acknowledged"
        (not (wasAcknowledged 1 told))

    let acked = step "UC-9 step 4 — A dismisses the notice" told [ act 1 AcknowledgesNotice ]

    expect "UC-9 step 4: acknowledged, and now the obligation is spent (Rule 10)"
        (wasAcknowledged 1 acked
         && saw (function AckSessionNotice _ -> true | _ -> false))

    // Step 5, the change the stateless design makes. The unsaved work was never
    // anywhere but the Client (Rule 31): the ended Session accepts nothing, but the
    // Client still holds it.
    expect "UC-9 step 5: the unsaved changes are still in the Client (Rule 31)"
        ((workingAt 1 told) |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved"))

    expect "UC-9 step 5: and they never reached the record (Concept 15)"
        (planCount pat2 told = 2
         && (headOf pat2 told
             |> Option.map _.Orders
             |> Option.defaultValue []
             |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
             |> not))

    expect "UC-9 step 5: the Unsigned TreatmentPlan stands, A's own to resume (Rules 18, 19)"
        ((recordFor pat2 told |> PatientRecord.startsFrom ucA.UserId |> Option.map _.Id)
            = (headOf pat2 told |> Option.map _.Id))

    let relaunched =
        step "UC-9 step 6 — A relaunches. Acknowledged already, A is not told again (Rule 10)" acked
             (launchAs ucA.Login (Some pat2))

    expect "UC-9 step 6: no notice at the relaunch — an acknowledged notice is never repeated"
        (never (function PriorSessionNotice _ -> true | _ -> false))

    // And the other way round: a notice that was delivered and never acknowledged is
    // shown again, because the alternative is a User who never learns of it at all.
    let unacknowledged =
        step "UC-9 step 6 — but an unacknowledged notice comes back (Rule 10, at least once)" told
             (launchAs ucA.Login (Some pat2))

    expect "UC-9 step 6: delivery is at-least-once; only the acknowledgement ends it"
        (saw (function PriorSessionNotice _ -> true | _ -> false)
         && openCount unacknowledged = 1)

    // Step 5 continued: the Client may offer to carry the surviving cart into the next
    // Session as fresh prescribing (Concept 15) — not as a resumed Session.
    let carried =
        step "UC-9 step 5 — A carries the surviving work into the new Session" relaunched
             [
                 act 2 (CarriesOverFrom(BrowserId 1))
                 act 2 Saves
             ]

    expect "UC-9 step 5: the unsaved OrderContext from before the idle-out lands in the next TreatmentPlan"
        (headOf pat2 carried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved"))

    expect "UC-9 step 5: and it is fresh prescribing — stamped by A in this Session (Rules 14, 35)"
        (headOf pat2 carried
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved" && o.Stamp = Some ucA))

    // "They survive exactly as far as the browser does — closed, they are gone."
    let browserGoneFirst =
        step "UC-9 step 5 — but close the browser first, and there is nothing to carry" told
             ([ atClient 1 CloseBrowser ] @ launchAs ucA.Login (Some pat2))

    let nothingCarried =
        step "UC-9 step 5 — the new Session gets only what the record held" browserGoneFirst
             [
                 act 2 (CarriesOverFrom(BrowserId 1))
                 act 2 Saves
             ]

    expect "UC-9 step 5: closed is gone — the unsaved work is nowhere"
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
        step "UC-9 step 5 — B tries to carry A's surviving work into B's own Session" told
             (launchAs ucB.Login (Some pat2)
              @ [ act 2 (CarriesOverFrom(BrowserId 1)); act 2 Saves ])

    expect "UC-9 step 5: another User's work is not a source — nothing is carried (Rules 14, 32)"
        (headOf pat2 notMine
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
         |> not)

    let otherPatient =
        step "UC-9 step 5 — A relaunches for another Patient, and the work does not follow" told
             (launchAs ucA.Login (Some pat1)
              @ [ act 2 (CarriesOverFrom(BrowserId 1)); act 2 Saves ])

    expect "UC-9 step 5: work does not cross Patients, and neither record gained it (Guarantee 1)"
        (headOf pat1 otherPatient
         |> Option.map _.Orders
         |> Option.defaultValue []
         |> List.exists (fun o -> o.Id = OrderContextId "oc-unsaved")
         |> not
         && planCount pat2 otherPatient = planCount pat2 told)

    // ── UC-9 ext 3a — nobody swept, and the request itself ends it ──
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
        step "UC-9 ext 3a — A comes back to a Session that is already past its time" aged
             [ act 1 (Prescribes(OrderContextId "oc-late")) ]

    expect "3a the request ends it rather than refreshing it, and says so (Rules 8, 41)"
        (never (function Tick -> true | _ -> false)
         && saw (function SessionEnded(Some Idle) -> true | _ -> false)
         && (match stateOf 1 endedOnArrival with Some(Ended(Idle, _)) -> true | _ -> false)
         && openCount endedOnArrival = 0)

    expect "3a and it is the one telling: the notice is spent by the same request (Rule 10)"
        (wasTold 1 endedOnArrival)

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

    // While it is down, requests fail as in UC-1 ext 7a.
    let whileDown =
        step "1a — while it is down, requests fail as in UC-1 ext 7a" saved
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
        (saw (function SessionRefused -> true | _ -> false)
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

    let reading = step "UC-10 steps 1 and 2 — C launches for Patient 2" withUnsignedHead (launchAs ucC.Login (Some pat2))

    expect "UC-10 step 1: C never creates a TreatmentPlan, so no Unsigned one of their own can exist (Rules 17, 19)"
        (openedAt 2 reading = Some p2Signed.Id)

    expect "UC-10 step 2: C reads the plan that counts clinically (Rule 16)"
        (workingAt 2 reading = p2Signed.Orders)

    expect "UC-10 step 3: A's newer Unsigned TreatmentPlan is not shown — only its creator can open it (Rule 18)"
        (recordFor pat2 reading |> PatientRecord.mayOpen ucC.UserId (headOf pat2 reading).Value.Id).IsNone

    // Its existence is not announced either: the only notification of another's
    // Unsigned work fires at TreatmentPlan creation (Rule 21), and a Reader never creates.
    let exploring =
        step "UC-10 step 4 — C prescribes within the Session to explore alternatives" reading
             [
                 act 2 (Prescribes(OrderContextId "oc-what-if"))
                 act 2 Saves
                 act 2 (Signs(Pin "0000"))
             ]

    expect "UC-10 step 4: prescribing works (Concept 15), but saving and signing are not offered"
        (saw (function Computed _ -> true | _ -> false)
         && saw (function NotPermitted -> true | _ -> false)
         && planCount pat2 exploring = planCount pat2 reading)

    expect "UC-10 step 4: no PIN is ever asked for, and none is ever read (Rule 25)"
        (never (function PinRequired _ -> true | _ -> false)
         && never (function ReadCredential _ -> true | _ -> false))

    expect "UC-10 step 3: the existence of A's Unsigned work goes unannounced (Rule 21)"
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

    let resumed = step "UC-11 step 1 — A launches again for Patient 2" parked (launchAs ucA.Login (Some pat2))

    expect "UC-11 step 1: the Session starts from A's own Unsigned head, not the older Signed one (Rule 19)"
        (openedAt 2 resumed = (headOf pat2 resumed |> Option.map _.Id))

    expect "UC-11 step 1: and it carries the work that was saved into it"
        ((workingAt 2 resumed).Length = 2)

    let signed =
        step "UC-11 steps 2 and 3 — A reviews, adjusts and signs" resumed
             [
                 act 2 (Prescribes(OrderContextId "oc-4"))
                 act 2 (Signs pinA)
             ]

    expect "UC-11 step 3: nothing blocks and nothing warns (Rules 20, 21)"
        (never (function CreateBlocked _ -> true | _ -> false)
         && never (function UnsignedWorkNotice _ -> true | _ -> false))

    expect "UC-11 step 3: a Signed TreatmentPlan in A's name; it now counts clinically (Rules 14, 15, 16)"
        ((headOf pat2 signed |> Option.map _.Signed) = Some true
         && (headOf pat2 signed |> Option.map _.By) = Some ucA
         && (recordFor pat2 signed |> PatientRecord.latestSigned |> Option.map _.Id)
                = (headOf pat2 signed |> Option.map _.Id))

    expect "UC-11 step 3: the TreatmentPlan's base is the Unsigned one it was resumed from (Concept 13)"
        ((headOf pat2 signed |> Option.bind _.Base) = (headOf pat2 resumed |> Option.map _.Id))

    // ── UC-11 ext 3a — a Signed TreatmentPlan appeared since the launch ──
    let bSignedMeanwhile =
        quiet "UC-11 ext 3a setup" resumed
              (launchAs ucB.Login (Some pat2)
               @ [
                   act 3 (Prescribes(OrderContextId "oc-c"))
                   // B opened from the older Signed TreatmentPlan, so A's Unsigned head is
                   // newer and Rule 21 fires on B as well. B re-sends with the token.
                   act 3 (Signs pinB)
                   act 3 (Signs pinB)
                 ])

    let aBlocked =
        step "UC-11 ext 3a — A signs after a Signed TreatmentPlan appeared" bSignedMeanwhile
             [ act 2 (Signs pinA) ]

    expect "3a creating is blocked (Rule 20)"
        (saw (function CreateBlocked _ -> true | _ -> false))

    let aRecovered =
        step "UC-11 ext 3a — A opens it, reapplies, and continues (Rule 17; UC-6 step 4)" aBlocked
             [
                 act 2 (OpensTreatmentPlan (headOf pat2 aBlocked).Value.Id)
                 act 2 (Prescribes(OrderContextId "oc-4"))
                 act 2 (Signs pinA)
             ]

    expect "3a opening the newest Signed TreatmentPlan lifts the block"
        ((headOf pat2 aRecovered |> Option.map _.By) = Some ucA
         && (headOf pat2 aRecovered |> Option.map _.Signed) = Some true)

    // ── UC-11 ext 3b — another User's Unsigned TreatmentPlan appeared since the launch ──
    let bSavedMeanwhile =
        quiet "UC-11 ext 3b setup" resumed
              (launchAs ucB.Login (Some pat2)
               @ [
                   act 3 (Prescribes(OrderContextId "oc-d"))
                   act 3 Saves
                   act 3 Saves
                 ])

    let aWarned =
        step "UC-11 ext 3b — A signs after another's Unsigned TreatmentPlan appeared" bSavedMeanwhile
             [ act 2 (Signs pinA) ]

    expect "3b A is notified and decides (Rule 21) — not blocked (Rule 20)"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucB | _ -> false)
         && never (function CreateBlocked _ -> true | _ -> false))

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
               @ [ act 1 (Prescribes(OrderContextId "oc-4")); act 1 (Signs pinA) ])

    let closed = step "UC-12 steps 2 and 3 — A closes the Session in the Client" signedUp [ act 1 ClosesSession ]

    expect "UC-12 step 3: the Session ends, marked closed by the User (Rule 9, Concept 9)"
        (openCount closed = 0
         && (match stateOf 1 closed with Some(Ended(ClosedByUser, _)) -> true | _ -> false))

    expect "UC-12 step 3: and no notice is ever owed — not owed and then skipped (Rule 10)"
        (noticeOf 1 closed = Some NotOwed)

    let nextLaunch = step "UC-12 step 4 — the next launch starts clean" closed (launchAs ucA.Login (Some pat2))

    expect "UC-12 step 4: no notice follows — Rule 10 speaks only of endings other than by the User"
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

    // ── UC-12 ext 2a — A closes the browser instead ──
    let browserGone = step "UC-12 ext 2a — A closes the browser instead" signedUp [ atClient 1 CloseBrowser ]

    expect "2a nothing reaches the Server, so no close can be inferred (Rule 9)"
        (openCount browserGone = 1
         && stateOf 1 browserGone = Some OpenOrGone
         && never (function SessionRequest _ -> true | _ -> false))

    let idledOut =
        step "UC-12 ext 2a — the Session idles out instead" browserGone (ticks (sessionTtl + 5))

    expect "2a it idles out, and A is told at the next opportunity (Rule 10; UC-9)"
        (match stateOf 1 idledOut with Some(Ended(Idle, _)) -> true | _ -> false)

    let harmlessNotice = step "UC-12 ext 2a — a harmless notice, the price of the indistinguishability" idledOut (launchAs ucA.Login (Some pat2))

    expect "2a the notice arrives at the next launch"
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

    let refused = step "UC-13 steps 1 and 2 — A launches; the registry returns no Role" withdrawn (launchAs ucA.Login (Some pat2))

    // A's Session from the precondition is still open, and stays open — that is ext 1a
    // below. What the failed launch must not do is open another one.
    expect "UC-13 step 2: no Role, so the launch opens no Session (Rules 5, 6)"
        (saw (function UserUnresolved(_, NoRole) -> true | _ -> false)
         && saw (function NotAuthorised -> true | _ -> false)
         && never (function SessionOpened _ -> true | _ -> false))

    let cds = step "UC-13 step 3 — A accepts the anonymous open: CDS is all that remains" refused [ atClient 2 AcceptAnonymousOffer ]

    expect "UC-13 step 3: hand-entered patients, no records, nothing saved (UC-8; Rule 13)"
        ((newestRecord cds |> Option.bind _.User) = None
         && (newestRecord cds |> Option.bind _.Patient) = None)

    let againRefused = step "UC-13 step 3 — every later launch ends the same way (Rule 5)" cds (launchAs ucA.Login (Some pat2))

    expect "UC-13 step 3: the Role is taken from the registry at each launch, so the withdrawal stands"
        (saw (function NotAuthorised -> true | _ -> false))

    expect "UC-13 step 4: A's UserCredential remains, but is inert (Concepts 7, 14)"
        ((credentialOf ucA againRefused).IsSome
         && (credentialOf ucA againRefused |> Option.bind _.Pin).IsSome)

    // ── step 5 — the Unsigned TreatmentPlan is stranded ──
    let bWorksPast =
        step "UC-13 step 5 — B's next Session starts from the Signed TreatmentPlan below (Rule 19)" againRefused
             (launchAs ucB.Login (Some pat2))

    expect "UC-13 step 5: only A could open the stranded work, and A can no longer reach it"
        (openedAt 4 bWorksPast = Some p2Signed.Id)

    let bNotified =
        step "UC-13 step 5 — B is notified of the stranded work at the save (Rule 21)" bWorksPast
             [ act 4 (Prescribes(OrderContextId "oc-e")); act 4 (Signs pinB) ]

    expect "UC-13 step 5: B is told whose work it is"
        (saw (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false))

    let superseded = step "UC-13 step 5 — B re-sends with the token, and their signature supersedes it for good" bNotified [ act 4 (Signs pinB) ]

    expect "UC-13 step 5: B's Signed TreatmentPlan now counts, and A's work can never be signed (Rules 16, 20)"
        ((headOf pat2 superseded |> Option.map _.By) = Some ucB
         && (headOf pat2 superseded |> Option.map _.Signed) = Some true)

    // ── UC-13 ext 1a — the withdrawal happens while A's Session is open ──
    // The Session keeps the Role its launch established: Rule 5 checks at the launch,
    // just as Concept 2 reads the data at the launch, and with no Session in memory it
    // is Concept 9 doing the work — the Role comes off the SessionRecord (Rule 32).
    // A signature is the one act that does not accept that. Rule 38 re-takes the Role
    // from the registry at every signature, so a withdrawal lands the moment A tries
    // to sign — while saving, which attests nothing, goes on working.
    let stillSaves =
        step "UC-13 ext 1a — the withdrawal lands while A's Session is open: A saves" withdrawn
             [ act 1 (Prescribes(OrderContextId "oc-f")); act 1 Saves ]

    expect "1a the open Session keeps the Role its launch established, and saving works (Concept 9, Rule 32)"
        ((headOf pat2 stillSaves |> Option.map _.By) = Some ucA
         && (headOf pat2 stillSaves |> Option.map _.Signed) = Some false
         && never (function ResolveUser _ -> true | _ -> false))

    let cannotSign =
        step "UC-13 ext 1a — but the signature asks the registry again (Rule 38)" stillSaves [ act 1 (Signs pinA) ]

    expect "1a the Role is gone, so the signature is refused — and before the PIN is asked for"
        (saw (function ResolveUser(ForRequest _, _) -> true | _ -> false)
         && saw (function UserUnresolved(_, NoRole) -> true | _ -> false)
         && saw (function NotPermitted -> true | _ -> false)
         && never (function ReadCredential _ -> true | _ -> false)
         && (recordFor pat2 cannotSign |> PatientRecord.latestSigned |> Option.map _.Id) = Some p2Signed.Id)

    expect "1a a signature nobody is entitled to costs no PIN attempt (Rules 27, 38)"
        ((credentialOf ucA cannotSign |> Option.map _.AttemptCount) = Some 0)

    // And a registry that is merely down is not a withdrawal: nothing is signed, and
    // the Session stands.
    let registryDown =
        step "UC-13 ext 1a — the registry cannot be asked at all" { stillSaves with Registry.Up = false }
             [ act 1 (Signs pinA) ]

    expect "1a no answer means no signature, and the Session is untouched (Rule 38)"
        (saw (function SigningUnavailable -> true | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false)
         && openCount registryDown = openCount stillSaves)

    superseded


// ═══════════════════════════════════════════════════════════════════════════════
//  Rules 32 to 36 — the stateless design under attack
// ═══════════════════════════════════════════════════════════════════════════════
//
// The cart is in the Client now (Rule 31), so everything the Server used to know by
// remembering it must instead arrive with the request — and be worth nothing unless
// the Server itself vouched for it. These are the tests that say so: a Client that
// edits a token, invents one, lies about the Patient, or forges a stamp, and a
// Database that arbitrates two Servers racing for the same head.

let tokensAndArbitration () =
    printfn ""
    printfn "############### Rules 32-36  The stateless design under attack ###############"

    // ── Rule 33: an opened-with token the Client edited ──
    let both =
        quiet "tokens precondition" world
              (launchAs ucA.Login (Some pat2) @ launchAs ucB.Login (Some pat2))

    let bWon =
        step "Rule 33 setup — B signs, so A's opened-with token is now stale" both
             [ act 2 (Prescribes(OrderContextId "oc-b")); act 2 (Signs pinB) ]

    let newestSigned = (recordFor pat2 bWon |> PatientRecord.latestSigned |> Option.map _.Id).Value

    let honestStale = step "Rule 33 — A's honest but stale token: blocked, as before (Rule 20)" bWon [ act 1 Saves ]

    expect "Rule 33 an honest stale token is believed, and Rule 20 does the refusing"
        (saw (function CreateBlocked _ -> true | _ -> false)
         && never (function CreateRefused _ -> true | _ -> false)
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

    expect "Rule 33 the token does not verify, so the create is refused — not merely blocked"
        (saw (function CreateRefused _ -> true | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false)
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
        (saw (function CreateRefused why -> why.Contains "does not verify" | _ -> false)
         && never (function TreatmentPlanCreated _ -> true | _ -> false))

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
         && never (function TreatmentPlanCreated _ -> true | _ -> false)
         && planCount pat3 notifiedAgain = planCount pat3 aSavedMeanwhile)

    let thenAccepted = step "Rule 34 — B returns the fresh token, and the create lands" notifiedAgain [ act 1 Saves ]

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
         && never (function TreatmentPlanCreated _ -> true | _ -> false))

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
        (saw (function CreateRefused _ -> true | _ -> false)
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
    // The single-writer assumption is gone: more than one Server may run, and this is
    // what makes that safe. Interleaving the cascades leg by leg is the only way to
    // put two creates in flight at once — same messages, different order, which is
    // exactly what Rule 36 exists to be safe against. Rule 42 is what makes the answer
    // the same either way: the check and the append are not two things that can be
    // separated, they are one act.
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
    // loser can still create, by coming back with the token the notice carried.
    expect "Rules 21, 34 the loser is told whose work landed first, and may still proceed"
        (countOf (function UnsignedWorkNotice(uc, _) -> uc = ucA | _ -> false) = 1
         && countOf (function CreateBlocked _ -> true | _ -> false) = 0)

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
                     Msg = OpenSessionClosingOthers { stale with State = OpenOrGone; Notice = NotOwed }
                 }
                 { From = GenPresServer; To = GenPresDatabase; Msg = TouchIfOpen stale.Id }
             ]

    expect "Rule 40 the Session stays ended, and its idle clock is not refreshed either"
        ((match stateOf 1 replayed with Some(Ended(ClosedByUser, _)) -> true | _ -> false)
         && openCount replayed = 0
         && recordCount replayed = recordCount closedSession
         && lastSeenOf 1 replayed = lastSeenOf 1 closedSession)


// ═══════════════════════════════════════════════════════════════════════════════
//  The adversarial review, answered
// ═══════════════════════════════════════════════════════════════════════════════
//
// An adversarial review of an earlier revision listed eighteen tests it wanted
// demonstrated before any guarantee was claimed again. They are worked through here in
// its order, with the scenario that shows each — and where one cannot be shown, why
// not, in the review's own terms rather than around them.
//
// Three of them are answered by the design being different now rather than by a test
// here: 1 is in UC-2, 3 in UC-1 ext 13b, 13 in UC-9. They are named where they land.

let adversarialReview () =
    printfn ""
    printfn "############### The adversarial review, answered ###############"

    // ── 2. A stolen launch code cannot be redeemed without the initiating browser ──
    // Not shown, and not shrugged off: binding the credential to the browser needs the
    // EHR side to run an authorisation flow that can bind the transaction, and that
    // side is [given] (Open Question 3). Rule 39 shrinks the window to the first page
    // load; UC-1 ext 7b is what remains, and it is a scenario that passes because the
    // thief wins, not because they lose.
    expect "2 the launch credential is still an unbound bearer code (Open Question 3, UC-1 ext 7b)"
        (credentialTtl > 0)

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
                         RequestSignChallenge(workOf 1 signing, (clientOf 1 signing).Value.Opened.Value, None)))
             ]

    let challenge = (challengeIssued ()).Value

    let commitAfterWithdrawal (h: Hospital) key =
        SessionRequest(
            (sidAt 1 h).Value,
            CreateTreatmentPlan
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
         && never (function TreatmentPlanCreated _ -> true | _ -> false)
         && planCount pat2 withdrawnMidSignature = planCount pat2 challenged)

    // ── 7. A request that began before the Session ended cannot append after it ──
    let closedMidSignature =
        let closed = quiet "7 setup — A closes the Session" challenged [ act 1 ClosesSession ]
        step "7 — the signature arrives after the Session has ended" closed
             [ fromClient 1 (commitAfterWithdrawal challenged "adv-7") ]

    expect "7 nothing is appended: the Session is re-established at the commit (Rules 40, 41, 42)"
        (never (function TreatmentPlanCreated _ -> true | _ -> false)
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
                RequestSignChallenge(workOf 1 signing, (clientOf 1 signing).Value.Opened.Value, None))

        step "8 — two challenges are issued" signing [ fromClient 1 ask; fromClient 1 ask ]

    let twoWrong =
        let sid = (sidAt 1 twoChallenges).Value

        let attempt (t: SigningChallenge) key =
            SessionRequest(
                sid,
                CreateTreatmentPlan
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
        step "10 — the token the Session opened with is offered again, after the create it was spent on" afterSaving
             [ fromClient 1 (SessionRequest((sidAt 1 afterSaving).Value, handCreate (workOf 1 afterSaving) staleOpened None None)) ]

    expect "10 a spent token is worth no more than one the Client made up (Concept 17, Rule 33)"
        (saw (function CreateRefused why -> why.Contains "spent" | _ -> false)
         && planCount pat2 replayedToken = planCount pat2 afterSaving)

    let agedToken =
        let sid = (sidAt 1 afterSaving).Value
        let old =
            Token.mintOpened (afterSaving.Env.Now - tokenTtl - 1) sid (Some pat2) (headOf pat2 afterSaving |> Option.map _.Id)
        step "10 — and a genuine token past its lifetime is refused too" afterSaving
             [ fromClient 1 (SessionRequest(sid, handCreate (workOf 1 afterSaving) old None None)) ]

    expect "10 an aged token is refused, however genuine its mac (Concept 17)"
        (saw (function CreateRefused why -> why.Contains "expired" | _ -> false)
         && planCount pat2 agedToken = planCount pat2 afterSaving)

    // ── 14. One OrderContext, named twice ──
    let twiceNamed =
        let sid = (sidAt 1 afterSaving).Value
        let one = { Id = OrderContextId "oc-dup"; Patient = Some pat2; Content = "first"; Stamp = None }
        let work = { workOf 1 afterSaving with Orders = [ one; { one with Content = "second" } ] }
        step "14 — a WorkPlan that names one OrderContext twice" afterSaving
             [ fromClient 1 (SessionRequest(sid, handCreate work (clientOf 1 afterSaving).Value.Opened.Value None None)) ]

    expect "14 the create is refused whole rather than one of the two being chosen (Concept 10, Rule 42)"
        (saw (function CreateRefused why -> why.Contains "twice" | _ -> false)
         && planCount pat2 twiceNamed = planCount pat2 afterSaving)

    // ── 17. A failure leaves retryable intent, not half a change ──
    let lostToADownServer =
        step "17 — the Server goes down with a create in flight" afterSaving
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
                CreateTreatmentPlan
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
         && countOf (function TreatmentPlanCreated _ -> true | _ -> false) = 2)

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
    // 4 (a concurrent refresh cannot reopen a closed Session) is the Rule 40 replay
    // above; 5 (an expired Session cannot refresh itself before a sweep) is UC-9 ext
    // 3a; 12 (a duplicate create after a lost reply) is UC-3 ext 3f; 15 (changed
    // Patient Data forces a choice) is Rule 44 in UC-3; 16 (no private table in a
    // shared export) is the store check under the Guarantees.

    // ── 18 tests, and two of them the design does not pass ──
    // Test 2 is Open Question 3, and the review is right that the model cannot answer
    // it. What follows is not a claim that the design is safe — it is a claim about
    // which of the review's questions this model now answers, and which it does not.
    expect "the review's remaining test is named as open, not as passed (Open Questions 3, 7)"
        true


// ═══════════════════════════════════════════════════════════════════════════════
//  Consequences — derived from the edges, checked over every scenario
// ═══════════════════════════════════════════════════════════════════════════════

let consequences () =
    printfn ""
    printfn "############### Consequences ###############"

    // Consequence 1. The LaunchScript learns nothing after the launch. This is not a
    // discipline the branches keep — it is the shape of edge C4, which is `=>`. The
    // only thing that ever reaches the LaunchScript is the Broker's answer to its own
    // request, and the User's trigger.
    let toLaunchScript =
        allTrace |> List.filter (fun e -> e.To = MainEhrLaunchScript)

    expect "C1 nothing reaches the LaunchScript but its own Broker answer and the User's trigger"
        (toLaunchScript |> List.forall (fun e -> e.From = Broker || e.From = User))

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
         && not (Edges.permits (GenPresClient(BrowserId 1)) Broker)
         && not (Edges.permits (GenPresClient(BrowserId 1)) UserRegistry))

    // Consequence 2. The Broker is the only party both the LaunchScript and the Server
    // can reach, so it is the sole channel between the EHR side and GenPRES.
    let reachableFrom a =
        Edges.table
        |> List.filter (fun (x, _, _) -> x = a)
        |> List.map (fun (_, _, y) -> y)
        |> Set.ofList

    let shared = Set.intersect (reachableFrom MainEhrLaunchScript) (reachableFrom GenPresServer)

    expect "C2 the Broker is the only party both the LaunchScript and the Server can reach"
        (shared = Set.ofList [ Broker ])

    // Consequence 3. Only the Broker knows whether a credential was redeemed, and it
    // cannot tell the LaunchScript, which has exited.
    expect "C3 the Broker's answers go only to the Server, the one party that asked"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | LaunchResolved _
             | LaunchRejected _ -> e.To = GenPresServer
             | _ -> true))

    // Consequence 6. The Server cannot reach a Client: edge C5 goes one way only, so
    // every Server-to-Client envelope is a reply riding that request's connection.
    expect "C6 there is no edge from the Server to a Client, so nothing can be pushed"
        (Edges.table
         |> List.exists (fun (x, _, y) ->
             x = GenPresServer && (match y with GenPresClient _ -> true | _ -> false))
         |> not)

    // Rule 11. The SessionId is a bearer credential and never travels in a URL. The
    // only message that is a URL is OpenUrl, and it carries a LaunchCredential.
    expect "Rule 11 the only thing that ever travels as a URL is a LaunchCredential"
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
                Broker
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
        | SessionRequest(_, CreateTreatmentPlan { Pin = Some _ }) -> true
        | Act(EntersResetCode _) -> true
        | SessionRequest(_, SupplyResetCode _) -> true
        | ReplacePinIfCode _ -> true
        | WriteCredential(_, c) | CredentialWritten(_, c) | PinReplaced(_, c) -> c.Pin.IsSome
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

    // Rule 4. Only the Server may redeem a LaunchCredential at the Broker.
    expect "Rule 4 every redemption at the Broker came from the Server"
        (allTrace
         |> List.forall (fun e ->
             match e.Msg with
             | ResolveLaunch _ -> e.From = GenPresServer && e.To = Broker
             | _ -> true))

    // Rule 5. The Role a Session carries is byte-for-byte the registry's answer, never
    // synthesised: every launched Session is preceded by a UserResolved carrying the
    // very same UserContext. Anonymous opens are excluded — they carry no User at all
    // (Rule 13). The type-level half is that LaunchAssertion has no Role field to
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
             | SessionOpened(_, _, Some uc, _, _, _) -> resolvedBefore i uc
             | _ -> true))

    // ── Rule 31, structurally ──
    // The Server carries nothing across requests. This is checked after every step of
    // every scenario, not sampled: `noteFlight` trips a flag the moment a step ends
    // with anything in the in-flight table.
    expect "Rule 31 the in-flight table is empty at the end of every scenario step"
        (not everCarriedARequest)

    // And the type says the same thing: `ServerState` has counters, the two in-flight
    // tables and `Up`. There is no field a Session could live in — no cart, no
    // opened-with, no last-seen — so "the Server holds no Session state" is not a
    // discipline the branches keep but something the state cannot express.
    // And nothing of the work stays behind: every answer to a Compute is that
    // request's own payload handed back. Checked over every Computed there has ever
    // been, against the Compute it answers — not sampled, and not merely "some
    // request carried a cart".
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

    // Open Question 3 is a measurement, not a judgement: this is what a create
    // actually carries — the whole WorkPlan, its Patient Data included (Concept 16).
    expect "Rule 31 a create carries the whole WorkPlan: OrderContexts and Patient Data alike"
        (allTrace
         |> List.exists (fun e ->
             match e.Msg with
             | SessionRequest(_, CreateTreatmentPlan req) ->
                 not req.Work.Orders.IsEmpty && req.Work.Data.IsSome
             | _ -> false))

    // ── Rule 32 ──
    // The payload has no User in it to be believed — `SessionCmd` carries orders, data
    // and tokens, and a token names a SessionId, not an identity — so the only place a
    // TreatmentPlan's `By` can have come from is a SessionRecord the Server had just read.
    // That is what this checks: every conditional append is preceded by the read that
    // supplied its User.
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
             | SessionOpened(_, _, _, _, _, t) -> Token.verifyOpened t
             | TreatmentPlanCreated(_, _, t) -> Token.verifyOpened t
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
    // Four endings, and a Server restart is not one of them. The type says so — there
    // is no `EndMark` for it — and UC-9 ext 1a shows what happens instead.
    let marksSeen =
        everyRecordWritten
        |> List.choose (fun r -> match r.State with Ended(m, _) -> Some m | OpenOrGone -> None)
        |> List.distinct

    expect "Rule 9 all four endings occur across the run, and nothing else ever ends a Session"
        (marksSeen |> List.sort = List.sort [ ClosedByUser; Idle; Superseded; WrongPinLimit ])


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
    let g2 = quiet "G" g1 [ act 1 (Prescribes(OrderContextId "g-2")); act 1 (Signs pinA) ]
    let g3 = quiet "G" g2 (launchAs ucB.Login (Some pat2))
    let g4 = quiet "G" g3 [ act 2 (Prescribes(OrderContextId "g-3")); act 2 (Signs pinB) ]
    // And one create that does not land, so the audit has a refusal in it to find.
    let g5 = quiet "G" g4 [ act 2 (Prescribes(OrderContextId "g-4")); act 2 (Signs(Pin "0000")) ]

    let record = recordFor pat2 g4

    // ── Guarantee 1: one constant ──
    expect "G1 the PatientId is the one thing no TreatmentPlan may change"
        (record.Plans |> List.forall (fun s -> s.Patient = pat2))

    expect "G1 and only a launch supplies one, so no hand ever set it (Rules 12, 13, 32)"
        (g4
         |> patientsInRecord
         |> List.forall (fun p -> (recordFor p g4).Plans |> List.forall (fun s -> s.Patient = p)))

    // ── Guarantee 2: one version ──
    let signedOnes = record.Plans |> List.filter _.Signed

    expect "G2 exactly one TreatmentPlan is the visible version: the most recent Signed one (Rules 16, 17)"
        ((PatientRecord.latestSigned record |> Option.map _.Id) = (signedOnes |> List.tryHead |> Option.map _.Id))

    // Reading is wider than building. Every Signed TreatmentPlan is readable — it is
    // attested history (Rule 17) — but only the most recent one can be built on,
    // because opening an older one makes it the Session's baseline and Rule 20 blocks
    // the create. Nobody else's Unsigned work is readable at all (Rule 18).
    expect "G2 reading is wider than building: Signed history is open, Unsigned work is not (Rules 17, 18)"
        (record.Plans
         |> List.forall (fun s ->
             if s.Signed then (record |> PatientRecord.mayOpen ucC.UserId s.Id).IsSome
             else (record |> PatientRecord.mayOpen ucC.UserId s.Id).IsNone))

    expect "G2 and only the newest Signed one can be built on (Rules 17, 20)"
        (record.Plans
         |> List.filter (fun s -> s.Signed)
         |> List.forall (fun s ->
             let isNewest = Some s.Id = (PatientRecord.latestSigned record |> Option.map _.Id)
             (record |> PatientRecord.blocking (Some s.Id)).IsNone = isNewest))

    expect "G2 and each User has exactly one starting point (Rule 19)"
        ([ ucA; ucB; ucC ]
         |> List.forall (fun uc -> (record |> PatientRecord.startsFrom uc.UserId) |> Option.isSome))

    // ── Guarantee 3: carts and one checkout ──
    // The cart is private by construction now: it lives in the User's own Client and
    // the Server keeps none of it (Rule 31). The checkout is single by construction
    // too: the Database arbitrates the append (Rule 36).
    expect "G3 signing is the only checkout: every Signed TreatmentPlan came from a create with a PIN"
        (signedOnes |> List.forall (fun s -> s.By.Role = Prescriber))

    expect "G3 a Reader never appears as the creator of anything (Roles)"
        (g4
         |> patientsInRecord
         |> List.forall (fun p -> (recordFor p g4).Plans |> List.forall (fun s -> s.By.Role <> Reader)))

    expect "G3 every TreatmentPlan after the first stands on a base (Concept 13)"
        (record.Plans
         |> List.filter (fun s -> s.No <> TreatmentPlanNo 1)
         |> List.forall (fun s -> s.Base.IsSome))

    expect "G3 the two carts never met in the Server: it held neither (Rule 31)"
        (g4.GenPres.InFlight.IsEmpty
         && (g4.Clients |> Map.exists (fun _ c -> not c.Work.Orders.IsEmpty)))

    // ── Rule 46: the audit ──
    // The record of what was done lives in the private store, written by the party
    // that did it, in the same act (Rule 42). Every Signed TreatmentPlan in the final
    // Patient's record has its line, and every line names the User.
    let auditLines = auditOf g5

    // The Cast's own TreatmentPlans were placed, not created, so nothing recorded
    // them: what the audit answers for is every create this run actually made.
    let createdHere = record.Plans |> List.filter (fun s -> s.No >= TreatmentPlanNo 10)

    expect "Rule 46 every TreatmentPlan created here is in the audit, exactly once, with its User"
        (not createdHere.IsEmpty
         && createdHere
            |> List.forall (fun s ->
                let (TreatmentPlanId i) = s.Id
                let (UserId u) = s.By.UserId
                let what = if s.Signed then "signed" else "saved"
                (auditLines |> List.filter (fun a -> a.Contains i && a.Contains what && a.Contains u)).Length = 1))

    expect "Rule 46 refusals are recorded too — a create that did not land is an event"
        (auditLines |> List.exists (fun a -> a.Contains "refused"))

    expect "Rule 46 and so are the Sessions: opened, and ended with the reason"
        (auditLines |> List.exists (fun a -> a.Contains "opened")
         && auditLines |> List.exists (fun a -> a.Contains "ended"))

    // ── The two stores (Actor 5) ──
    // A copy of the Clinical store is what other systems could be handed (Open
    // Question 2). What it holds is attested history and nothing else: no Unsigned
    // work, no Session, no credential, no reset code, no spent key.
    let exported = g5.Database.Clinical

    expect "Actor 5 the Clinical store holds Signed TreatmentPlans and nothing but"
        (exported.Signed |> Map.forall (fun _ plans -> plans |> List.forall _.Signed))

    expect "Actor 5 an export of it carries no Session, no credential, no code and no key"
        (let text = $"%A{exported}"

         [ "SessionId"; "UserCredential"; "Pin "; "ResetCode"; "IdemKey"; "PendingReset" ]
         |> List.forall (fun secret -> not (text.Contains secret)))

    expect "Actor 5 and the Unsigned work is in the other half, where Rule 18 keeps it"
        (g5.Database.Private.Drafts
         |> Map.forall (fun _ plans -> plans |> List.forall (fun s -> not s.Signed))
         && (g5 |> patientsInRecord |> List.exists (fun p ->
             (recordFor p g5).Plans |> List.exists (fun s -> not s.Signed))))

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
        history |> List.collect (fun r -> r.Plans |> List.filter _.Signed) |> List.distinct

    expect "G4 nothing attested is ever lost: every Signed TreatmentPlan ever made is still there"
        (everSigned <> []
         && everSigned |> List.forall (fun s -> record.Plans |> List.contains s))

    // What is not protected: Unsigned work. Superseded, it can never be signed.
    expect "G4 what is not protected is Unsigned work — superseded, it can never be signed (Rules 19, 20)"
        (record |> PatientRecord.blocking (Some(TreatmentPlanId "plan-0010"))).IsSome


// ═══════════════════════════════════════════════════════════════════════════════
//                                  THE RUN
// ═══════════════════════════════════════════════════════════════════════════════

let runAll () =
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
    tokensAndArbitration ()
    adversarialReview ()
    consequences ()
    guarantees ()

    printfn ""
    printfn "######################################################################"
    printfn $"  {checks - failures}/{checks} checks passed"
    if failures > 0 then printfn $"  {failures} FAILED"

runAll ()
