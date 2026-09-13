# GenPRES – MainEHR integration

The pages in this directory describe the integration **as the code builds it today**: the
demo server (`GENPRES_PROD=0`) with in-memory stand-ins for every party outside GenPRES.
Every diagram is read off the source files named at the foot of its page, and every page
ends with what the design asks for that the code does not yet do.

The design itself lives in two files here:

- [GenPRES-MainEHR-Integration-V8.md](GenPRES-MainEHR-Integration-V8.md), the document. Where
  a page cites a Rule or a Concept by number, that is its numbering.
- [`Integration.fsx`](Integration.fsx), the design as an executable model. It runs standalone
  (`dotnet fsi Integration.fsx`), prints a trace per use case to `Integration.run.txt` (not
  tracked) and ends with a count of self-checks. It models the design, not the code: it has
  six Session endings, a PIN reset, an anonymous open and an idle clock that the code does not
  have, and it lacks the browser key pair and the LaunchRecord that the code does have.

Where a page and the model disagree, the page describes the code and the model describes the
design; the gap is named under the page's *Not built*.

## The use cases

| | | | Built |
|---|---|---|---|
| UC-1 | [User launches GenPRES](uc-01-launch.md) | the Launch, the identity, the Role and the active Patient | yes, up to step 6; the signed request of step 7 is not |
| UC-2 | [First launch as a Prescriber](uc-02-enrolment.md) | no PIN yet: the launch suspends into enrolment | yes |
| UC-3 | [Prescribe and sign](uc-03-prescribe-and-sign.md) | the only way an OrderPlan comes into being | yes |
| UC-4 | [Two Users, one Patient](uc-04-two-users.md) | the first to sign wins; the other is told | yes |
| UC-5 | [Someone else takes over the workstation](uc-05-workstation-takeover.md) | look and explore, attest nothing | yes, as a consequence of UC-3; the notice to the User is only the mail |
| UC-6 | [A User forgets their PIN](uc-06-forgotten-pin.md) | replaced, never removed | **no** |
| UC-7 | [User opens GenPRES directly](uc-07-direct-open.md) | decision support without a launch | yes, without a Session |
| UC-8 | [A Session ends out from under the User](uc-08-session-ends.md) | told once, at the next launch | partly: two endings, no clocks, told at the next request |
| UC-9 | [A Reader consults a Patient](uc-09-reader.md) | reads the plan that counts, signs nothing | yes |
| UC-10 | [User closes GenPRES](uc-10-close.md) | no stray Session, no notice | yes |
| UC-11 | [A User's authority is withdrawn](uc-11-authority-withdrawn.md) | anonymous decision support, and nothing more | partly: refused at the launch, blocked at the signature |

Three extensions have diagrams of their own, where the order of messages is the point: two
launches racing (UC-1), the signing modal (UC-3) and both Users signing at once (UC-4).

## The model, from other angles

- [Actors and edges](actors-and-edges.md) — who may talk to whom, and which of those edges is
  HTTP and which an in-process port today.
- [How a Session ends, and who gets told](session-endings.md) — the two endings the code has,
  how a Client learns of them, and the six the design asks for.

## Reading the diagrams

Participants are the User, the GenPRES Client, the GenPRES Server, and the parties the Server
asks: the IdentityProvider, the UserRegistry, the PatientDataPlatform, the MailService and the
GenPRES Database. In the demo server all five are stand-ins the Server calls in-process; the
diagrams draw them as participants all the same, since the real adapters will make the same
calls over the wire. What crosses the browser is drawn as HTTP: the routes, the redirects and
the cookies each sets.

Message labels are the code's names: the members of `IServerApi`
(`src/Informedica.GenPRES.Shared/Api.fs`), their command and response cases, and the functions
of the `Session` module (`src/Informedica.GenPRES.Server/ServerApi.Session.fs`). The
walkthroughs that exercise every page by hand are in
[DEVELOPMENT.md](../../../DEVELOPMENT.md#simulating-the-launch-sequence).

Most use cases begin with a launch, which UC-1 draws; rather than redraw it, those diagrams
open with a note citing UC-1.
