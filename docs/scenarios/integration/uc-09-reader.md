# A Reader consults a Patient

UC-9. User C holds the Reader Role. They see the version that counts, and are told when it
moves on, but nothing they do can ever be signed.

Precondition: the Patient has a head, User A holds an open Prescriber Session for it, and C
launches. In the demo: the identity `reader`.

```mermaid
sequenceDiagram
    actor C as User C (Reader)
    participant CC as C's Client
    participant S as GenPRES Server
    participant R as UserRegistry (stub)
    participant D as GenPRES Database (stub)

    Note over C,D: uc-01 steps 1 to 4: the code is redeemed

    S->>R: standing
    R-->>S: Reader, the active Patient
    Note over S: a Reader is never asked for a PIN: the credential is not read
    S->>D: headOf (PatientId), openWith
    S-->>CC: 302 #35;/session + cookie
    CC->>S: GetSession
    S-->>CC: SessionResp (Some {User = Reader, Head = the newest version, ...})
    Note over CC: the head's orders in the cart, the person button says Reader, no Ondertekenen

    Note over C,D: step 2, A signs a newer version meanwhile (uc-03)

    C->>CC: prescribes
    CC->>S: processOrderContext {Opened = C's token}
    S-->>CC: Reply {Response, Notice = NewerVersion (A's head)}
    Note over CC: snackbar once, the bar with "Open the newest version": a Reader is told like anyone

    C->>CC: presses the button
    CC->>S: OpenVersion (A's version)
    S-->>CC: SessionResp (Some session with Head = A's version)
    Note over CC: the cart reloaded from A's version, any version of the record may be opened
```

## Reading it

**A Reader prescribes like anyone.** Computing is not gated by Role: C can explore
alternatives freely, on the head's orders or their own. What is gated is signing, and only
signing.

**No PIN is ever read.** Not asked and ignored: the credential stage is skipped whole at the
launch, because the suspension into enrolment binds Prescribers only. The stub directory
seeds no credential for `reader` at all.

**Signing is refused twice over.** The Client renders no Sign button unless the Session's User
is a Prescriber. Were `RequestSignChallenge` or `Submit` sent all the same, the Server would
answer `Refused NotPrescriber`: at the challenge from the Session's Role, at the commit from the
registry's fresh answer.

**The notice does not discriminate.** C is told a newer version exists on the same terms as a
Prescriber, because it rides on any computing reply whose token is the Session's own and gates
nothing. And C's Session is a full Session in every other way: it supersedes C's other
Sessions, it holds an OpenedToken and a key thumbprint, and `OpenVersion` works.

## Not built

The audit of the refusal (Rule 46).

---

Read off `Session.callback` (the credential check), `Session.challenge` and `Session.commit`
in `src/Informedica.GenPRES.Server/ServerApi.Session.fs`, `StubDirectory` and
`StubCredentials` in `ServerApi.StubAdapters.fs`, and `canSign` in `SigningPolicy.fs` in
`src/Informedica.GenPRES.Client/`. The design is UC-9 in [`Integration.fsx`](Integration.fsx).
