# A Session ends out from under the User

UC-8. The Server cannot reach a Client, so a Session that ends while nobody is looking cannot
be announced. The screen goes on looking alive. User A finds out at their next action.

The code has three endings that happen to a User: the same login opening a Session elsewhere,
the wrong-PIN limit ([uc-05](uc-05-workstation-takeover.md)), and a Session left idle for the
idle lifetime, an hour unless the site sets another. Nothing ends a Session for being old. This
page draws the first; an idle Session is told the same way, at the request that ends it.

Precondition: [uc-03](uc-03-prescribe-and-sign.md) step 1, an open Session with unsigned work
on screen.

```mermaid
sequenceDiagram
    actor A as User A
    participant C1 as A's Client (old tab)
    participant S as GenPRES Server
    participant D as GenPRES Database (stub)
    participant C2 as A's Client (new browser)

    Note over A,C1: uc-03 step 1: unsigned work on screen

    Note over A,C2: step 1, A launches again elsewhere (uc-01)
    A->>C2: launches from MainEHR
    C2->>S: GET /callback
    S->>D: Session.openWith (ses-002, login A): ses-001 removed and marked SupersededByLaunch
    S-->>C2: 302 #35;/session + cookie ses-002
    Note over C1: the old tab cannot be told. The screen still shows what it last heard.

    Note over A,C1: step 2, A comes back to the old tab and acts
    A->>C1: prescribes
    C1->>S: processOrderContext {Opened = ses-001's token} (cookie ses-001)
    S->>D: Session.seen: no Session under ses-001, a mark for it
    S-->>C1: Reply {Response, Notice = Ended SupersededByLaunch}
    Note over C1: the gate: "Your session was ended: a newer launch...", the cart dropped
    C1->>S: processSession CloseSession
    S->>D: the mark dropped, the cookie deleted
    Note over C1: the gate offers a relaunch, or continue without launch

    Note over C1,C2: step 3, the carry-over: refused. The unsigned work of the old tab is gone with it.
```

A reload of the old tab instead of an action asks `GetSession`, which answers
`SessionEnded SupersededByLaunch` and ends the same way.

## Reading it

**Nothing pushes.** The Client-to-Server edge goes one way, so the Session ends silently and
A learns at their own next request, whichever member it is: the ending rides on the reply of a
computing request as `Reply.Notice`, and on `GetSession` as its answer. The computing request
is still answered; the ending is told next to the result.

**Being told is being acknowledged.** The design keeps the notice owed until A, from a fresh
launched Session, says they have seen it, and has the refused old tab discharge nothing. The
code has the Client acknowledge at once: on `Ended` it shows the gate and sends
`CloseSession`, which drops the mark. Whoever is at the old tab, A or the next person at the
workstation, sees the notice and discharges it.

**Nothing is told at the next launch.** The new browser's launch reads no marks, and by the
time A relaunches from the old tab the mark is gone. The notice is the gate on the old tab, and
nothing else.

**There is no carry-over, and none will be built.** The design has the new tab read the old
tab's cart from memory, same User and same Patient only. The code has no such read; the cart of
the superseded Session is dropped when the gate shows. A User who wants the work keeps the old
tab open until it is signed, and the leave-page guard warns before a tab with unsigned work is
closed or left.

The carry-over was refused rather than left pending
([#518](https://github.com/informedica/GenPRES/issues/518)), because a hand-over inside the
browser reaches only the case where no work is lost. A relaunch in the same browser profile
does not end the old tab: the session cookie is shared by every tab of the profile, so the old
tab continues on the new Session and its work stays one tab away. A relaunch in another browser
or profile, as drawn above, does end the old tab, but no channel inside the browser reaches
across a profile, and keeping unsigned work in the Server, the one way that would, is forbidden
(Rule 32; Concept 16). The design's carry-over of step 3 is therefore a divergence of the code
from the design, not a gap.

**A restart forgets the Session, unless the store is set.** The design has a Session's standing
live in its record, so that a restart ends nothing (Rule 32). With
`GENPRES_DB_CONNECTION` set this holds: the cookie still names the Session, the server reads it
back from the file with its ending, its credential and what it opened with, and a second server
reads the same. Without the key the record is the process's memory, and after a restart the
cookie names no Session, `GetSession` answers `SessionResp None`, and the Client opens
anonymously, without a notice, the cart of the old Session gone with the page. The production
engine is still to be chosen ([#516](https://github.com/informedica/GenPRES/issues/516)).

## Not built

- The absolute lifetime (Rule 10), and the request that ends its own Session past it
  (Rule 41) ([#825](https://github.com/informedica/GenPRES/issues/825)). The idle half is
  built: every request but a close first ends a Session not seen for the idle lifetime, and is
  told the ending ([#1061](https://github.com/informedica/GenPRES/issues/1061)).
- The notice at the next launch, and the acknowledgement only A can give (Rule 11;
  [session-endings](session-endings.md)).
- The audit of a Session superseded by a newer launch, and of the refusal it is told (Rule 46).
  A close and the PIN limit are audited on the SQLite store, since each writes an ending; a
  supersession writes none — the newer Session is what tells it — so there is nothing to audit
  it from.
- An upgrade that serves open Sessions by the version they opened on: one version runs.

---

Read off `Session.openWith`, `Session.seen`, `Session.find` and `Session.close` in
`src/Informedica.GenPRES.Server/ServerApi.Session.fs`, `StubDatabase.idleFirst` in
`ServerApi.StubAdapters.fs`, `Compute.bound` in
`ServerApi.Compute.fs`, and `EndedByServer` and `Resumed` in `SessionMachine.fs` in
`src/Informedica.GenPRES.Client/`. The design is UC-8 in [`Integration.fsx`](Integration.fsx),
whose `PriorSessionNotice` and `AckSessionNotice` have no counterpart in the code.
