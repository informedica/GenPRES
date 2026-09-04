# A Reader consults a Patient

UC-9. User C holds the Reader Role. They see the plan that counts, and are told when it
moves on — but nothing they do can ever be signed.

Precondition: Patient 2 has a head, User A holds an open Prescriber Session for it, and C
launches (UC-1 ext 5c).

```mermaid
sequenceDiagram
    actor C as User C (Reader)
    participant CC as C's Client
    participant S as GenPRES Server
    participant R as UserRegistry
    participant D as GenPRES Database

    Note over C,D: UC-1 steps 1-4: the Launch is verified

    S->>R: ResolveUser
    R-->>S: UserResolved (Reader)
    Note over S: Rule 26: a Reader is never asked for a PIN,<br/>so the credential is not even read

    S->>D: ReadRecord
    D-->>S: RecordRead
    S->>D: OpenSessionClosingOthers
    D-->>S: SessionWasOpened
    S-->>CC: SessionOpened (from the most recent plan, Rules 18, 19)

    Note over C,D: step 2 - A signs a newer plan meanwhile

    C->>CC: Prescribes
    CC->>S: Compute
    S-->>CC: Computed
    S-->>CC: NewerPlanNotice (dr.a, signed at 62)
    Note over CC: Rule 21 reaches a Reader like anyone else

    C->>CC: OpensTreatmentPlan
    CC->>S: OpenTreatmentPlan
    S-->>CC: TreatmentPlanOpened
    Note over CC: Rule 18: the whole history is open to read

    C->>CC: Signs
    CC->>S: RequestSignChallenge
    S-->>CC: NotPermitted
    Note over S: Roles: a Reader never creates a TreatmentPlan
```

## Reading it

**A Reader prescribes like anyone.** Concept 15 is not gated by Role — C can explore
alternatives freely. What is gated is signing, and only signing.

**No PIN is ever read.** Not asked and ignored: the credential stage is skipped whole at
the launch, because a Reader has nothing to prove (Rule 26).

**Rule 21 does not discriminate.** C is told a newer plan exists on exactly the same terms
as a Prescriber, because the notice rides on any response and gates nothing.

## What it leaves out

- **The refusal is audited** (Rule 46), like every refused request.

---

Drawn from UC-9 in [`Integration.fsx`](Integration.fsx). The full trace of all eleven use
cases is written to `Integration.run.txt` beside it when the script runs.
