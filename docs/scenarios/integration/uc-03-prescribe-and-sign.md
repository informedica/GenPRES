# Prescribe and sign

UC-3. User A records orders for Patient 2 and takes responsibility for them. Signing is
the only way anything reaches the record — there is no saving — so this diagram is the
whole of how a TreatmentPlan comes into being.

Precondition: UC-1 has left an open Session for Patient 2, started from its head, with the
Prescriber Role.

```mermaid
sequenceDiagram
    actor U as User A
    participant C as GenPRES Client
    participant S as GenPRES Server
    participant R as UserRegistry
    participant P as PatientDataPlatform
    participant D as GenPRES Database

    Note over U,D: UC-1: an open Session for Patient 2, from its head

    rect rgb(245,245,245)
    Note over U,S: step 1 - prescribing, repeated as often as the User likes
    U->>C: Prescribes
    C->>S: Compute (the whole WorkPlan, Rule 32)
    S->>D: ReadSessionRecord
    D-->>S: SessionRecordRead (open)
    S->>D: TouchIfOpen (Rule 9)
    S-->>C: Computed
    Note over S: nothing is kept: the cart goes home with the reply
    end

    Note over U,C: step 2 - the User signs. The PIN waits in the page.
    U->>C: Signs (PIN)
    C->>S: RequestSignChallenge (WorkPlan + OpenedToken)
    S->>D: ReadSessionRecord
    D-->>S: SessionRecordRead (open)
    S->>D: ReadRecord
    D-->>S: RecordRead
    Note over S: Rule 20: nothing newer than the plan this Session opened with
    S->>P: ReadPatientData (Rule 44, re-read)
    P-->>S: PatientDataRead (unchanged)
    S-->>C: SignChallengeIssued (over this exact WorkPlan, under the current rule set)

    Note over U,C: step 3 - the Client shows the challenge modally (Rule 43)
    U->>C: ConfirmsSign
    C->>S: Submission (WorkPlan + OpenedToken + challenge + PIN + key)
    S->>D: ReadSessionRecord
    D-->>S: SessionRecordRead (open)
    S->>R: ResolveUser (Rule 38: the Role, re-taken)
    R-->>S: UserResolved (Prescriber)
    S->>D: CommitTreatmentPlan
    Note over D: one transaction (Rule 42): Session, Role, tokens,<br/>head, challenge and PIN - all of it, or nothing
    D-->>S: TreatmentPlanCommitted (with the rule set it was checked under)
    S-->>C: TreatmentPlanSubmitted (and a fresh OpenedToken over the new baseline)
```

## Reading it

**Signing is two requests, not one.** The first asks for a challenge; the second returns
it with the PIN. Nothing is submitted in between, and while the modal is up the WorkPlan
cannot change — that is what the modal is for. Splitting it this way also means Rule 20's
block is settled before the User is ever asked for a PIN they were never going to spend.

**The Role is taken twice.** Once at the launch (Rule 5), and again here at the signature
(Rule 38). Authority withdrawn since the launch blocks the signature at its commit, which
is UC-11 ext 1a.

**The commit re-establishes everything.** The Server has checked the Session, the token
and the data on the way in, but none of that is what the commit trusts: Rule 42 makes the
Database re-verify all of it inside one transaction. The checks on the way in exist to
fail early and cheaply, not to decide.

**The PIN is last.** A Submission that was never going to land — blocked, or with a bad
token — is refused before the PIN is looked at, so it costs the User no attempt (Rule 28).

## What it leaves out

- **The record moving on** (ext 1a, 2a). If a newer plan appeared while User A worked,
  any response says so and does not stop them (Rules 21, 22); if nothing told them first,
  the Submission itself is refused, and that refusal is the notice (Rule 20). UC-4 is this
  ground from the other side.
- **A new KnowledgeRuleSet** (ext 1b). Published mid-Session, it reaches the next
  computation, and the challenge is issued under it. The signed plan records which set.
- **The Patient Data changing** (ext 2b). No challenge is issued until the User has seen
  the data as it now stands, or been told it could not be checked, and accepted it.
- **The wrong PIN** (ext 3a). Nothing is created and no token is spent. Wrong entries
  count across Sessions; at the limit the Session ends and signing locks for a growing
  delay.
- **Cancelling and editing** (ext 3b), **someone else at the keyboard** (ext 3c), **a late
  or repeated Submission** (ext 3d), and **never signing at all** (ext 3e).
- **The audit.** Every Submission, committed or refused, is appended (Rule 46).

## The modal, drawn out (ext 3b)

Why the two requests are worth separating: between them the User is looking at exactly
what they are about to attest to, and nothing has left the Client.

```mermaid
sequenceDiagram
    actor U as User A
    participant C as GenPRES Client
    participant S as GenPRES Server

    U->>C: Signs (PIN)
    C->>S: RequestSignChallenge
    S-->>C: SignChallengeIssued
    Note over U,C: the modal is up. The PIN sits in the page.

    U->>C: Prescribes
    C-->>U: "finish or cancel the signature first"
    Note over C: refused locally - nothing is sent,<br/>and the WorkPlan cannot change under the challenge

    U->>C: CancelsSign
    Note over C: the challenge is dropped, and the PIN with it.<br/>Nothing was signed and nothing was submitted.

    U->>C: Prescribes
    C->>S: Compute
    S-->>C: Computed
    Note over U,C: editing is possible again, and the next signature<br/>asks for a challenge of its own
```

---

Drawn from UC-3 in [`Integration.fsx`](Integration.fsx). The full trace of all eleven use
cases is written to `Integration.run.txt` beside it when the script runs.
