# A User forgets their PIN

UC-6. **Not built.** The design has a User get a new PIN inside their Session, by a mailed
confirmation code, and learn from a second mail if somebody else tried; the old PIN stands
until the new one replaces it. The code has no such path.

## What the code does today

A PIN is set once, at enrolment ([uc-02](uc-02-enrolment.md)), and only while the credential
has none: the callback suspends into enrolment when the Role is Prescriber and no PIN is set,
and nowhere else is `Credential.withPin` called. `SessionCommand` has `GetSession`,
`CloseSession`, `SupplyPin` and `OpenVersion`; there is no reset. `AdminCommand` covers the log
files and the resource reload; it cannot clear a credential.

A User who has forgotten their PIN can therefore sign nothing, and every guess counts against
the credential: three wrong entries end the Session and lock signing for one minute, doubling
with each further wrong entry up to a day ([uc-03](uc-03-prescribe-and-sign.md)).

The one thing that resets a PIN today is a restart of the demo server. Credentials live in the
same in-memory state as the Sessions and the record, and a restart forgets all of it: the
seeded `1234` of the stub Prescribers is back, and an enrolled identity such as `no-pin` has to
enrol again.

## What a build would need

The design's sequence, in words: the User asks for a reset from within the Session; the Server
asks the registry for the address, fresh, parks a confirmation code as a mac, mails the code,
and answers that it was mailed, the old PIN still standing; the User returns the code with a
new PIN; the Server asks the registry again, verifies the code and replaces the PIN in one act
with a wrong-count of zero, and mails that the PIN was replaced. The second mail is the point:
if the User did not ask for it, it is how they learn somebody else did. A wrong code counts
against the code, not the credential, and a few wrong codes void it.

The pieces are in place from enrolment and would be reused: `PendingCode` (one live code per
person, its mac, expiry and tries), `Session.newCode` and `Session.codeMac`, `Credential.withPin`,
`Mails.confirmationCode` and `Mails.pinSet`, the `MailPort` and its stub outbox at `/stub/mail`.
What is missing is a command on the wire, the transition over a Session that already has a
PIN, and the lock being lifted by the replacement. The MVP overview lists it as an issue to
file, together with the decay of the wrong-count
([mvpap2019-gap-overview.md](../../roadmap/mvpap2019-gap-overview.md), row 2.1.7).

---

Read off `Session.supplyPin` and `Credential` in
`src/Informedica.GenPRES.Server/ServerApi.Session.fs` and `SessionCommand` in
`src/Informedica.GenPRES.Shared/Api.fs`. The design is UC-6 in
[`Integration.fsx`](Integration.fsx), whose `ResetPin` and `SupplyResetCode` have no
counterpart in the code.
