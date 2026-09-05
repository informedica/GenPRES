# GenPRES – MainEHR integration

The design document for this integration is modeled here as an executable F# script.
`Integration.fsx` runs standalone and checks itself:

```bash
dotnet fsi Integration.fsx
```

It prints a trace for every use case to `Integration.run.txt` and ends with a count of
self-checks. Everything else in this directory is drawn from that trace. The trace
itself is not tracked: it appears beside the script when you run it, so a fresh clone
has the script but not yet its output.

The design document these artifacts implement is
[GenPRES-MainEHR-Integration-V8.md](GenPRES-MainEHR-Integration-V8.md), here in this
directory. Where a page cites a Rule or a Concept by number, that is the document's
numbering.

## The use cases

| | | |
|---|---|---|
| UC-1 | [User launches GenPRES](uc-01-launch.md) | the Launch, the identity, the Role and the active Patient |
| UC-2 | [First launch as a Prescriber](uc-02-enrolment.md) | no PIN yet: the launch suspends into enrolment |
| UC-3 | [Prescribe and sign](uc-03-prescribe-and-sign.md) | the only way a TreatmentPlan comes into being |
| UC-4 | [Two Users, one Patient](uc-04-two-users.md) | the first to sign wins; the other is told |
| UC-5 | [Someone else takes over the workstation](uc-05-workstation-takeover.md) | look and explore, attest nothing |
| UC-6 | [A User forgets their PIN](uc-06-forgotten-pin.md) | replaced, never removed |
| UC-7 | [User opens GenPRES directly](uc-07-direct-open.md) | decision support without a launch |
| UC-8 | [A Session ends out from under the User](uc-08-session-ends.md) | told once, at the next launch |
| UC-9 | [A Reader consults a Patient](uc-09-reader.md) | reads the plan that counts, signs nothing |
| UC-10 | [User closes GenPRES](uc-10-close.md) | no stray Session, no notice |
| UC-11 | [A User's authority is withdrawn](uc-11-authority-withdrawn.md) | anonymous decision support, and nothing more |

Four extensions have diagrams of their own, where the order of messages is the point:
two launches racing (UC-1), the signing modal (UC-3), both Users signing at once (UC-4),
and the request that ends its own Session (UC-8).

## The model, from other angles

- [Actors and edges](actors-and-edges.md) — who may talk to whom, and nothing else is
  possible.
- [How a Session ends, and who gets told](session-endings.md) — the six endings and the
  notice each one owes.

## Reading the diagrams

Each use case starts where the document's precondition starts. Most begin with a launch,
which UC-1 already draws; rather than redraw it, those diagrams open with a note citing
UC-1. Extensions are described in prose under *What it leaves out*, except the four above.

The messages are read off `Integration.run.txt`, not sketched. If a diagram and the trace
disagree, the trace is right.
