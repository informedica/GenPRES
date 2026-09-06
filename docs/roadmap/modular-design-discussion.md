# Discussion: GenPRES as separately deployed modules

> Discussion, not a decision. Nothing here is decided until it becomes an ADR (ADR-0000 §2). It
> collects the points that a cross-check of the amended [ADR-0001](../adr/0001-system-architecture.md)
> (2026-09-06) raised against a modular design draft ("GenPRES as modules: GenPRES, GenPREP and
> the rest"). The draft itself is not in this repository.

## Why this is not in ADR-0001

ADR-0001 decides "a client-server web application". Several separately deployed module servers
changes that sentence, and brings decisions of its own: one database per module and a shared
credential store (storage), module-to-module reads over contracts and a platform for publication
(integration), and a shared hosting component. That is a decision in its own right, ADR-0006 when
it is taken, with a one-line amendment to ADR-0001 saying that "the system" is since then each
GenPRES module and that the dependency rule and its ring map apply per module executable.
Folding it into ADR-0001 would make that ADR carry three decisions.

## Where the draft and ADR-0001 disagree, and which wins

Where the draft and the ADR use different words for the same thing, the ADR and
`scripts/CheckDependencyRule.fsx` win. In order of consequence:

1. **ZIndex and ZForm are Infrastructure, not core.** The draft listed them among the pure
   domain libraries; ADR-0001 §2 and the ring map say adapters. The core is `GenSOLVER`,
   `GenORDER`, `GenFORM`, `GenUNITS`, `GenCORE`, `GenINTERACT` (plus `Utils.Lib` and
   `Logging.Lib` as the script has them today, pending the split); `ZIndex`, `ZForm`, `NKF`,
   `FTK` and `Agents` are Infrastructure, referenced only from a module server.
2. **Module contracts are Contract-ring projects, and the ring map decides who may see them.**
   Under the script's rule that only Presentation and Client reference Contract: a module server
   (Presentation) may reference another module's contracts, a module client may reference its
   own module's contracts, and neither Core nor Infrastructure may reference any contract. Two
   consequences: a shared hosting library, being Infrastructure, may not reference any module's
   contracts (it must not know what a treatment plan is); and the adapter that calls another
   module over the network, which needs that module's contract types, lives in the calling
   module's server project, not in the hosting library or a shared Infrastructure library. The
   draft had said "core-ring" for contracts; under the script that is wrong.
3. **A shared hosting library is Infrastructure and never a composition root.** ADR-0001 §3:
   exactly one composition root per executable, and only composition roots construct loggers,
   providers and caches. N module servers means N composition roots. The hosting library provides
   ports and their live adapters (session store, credential store, launch verification, signing,
   audit, mail, registry, platform); each module server's composition root constructs and wires
   them from that server's settings. The hosting library constructs nothing on load, reads no
   setting on its own and holds no singleton — a violation there is exactly the type-initializer
   failure (#523, #526) the dependency rule ends.
4. **The `GENPRES_*` rule needs a form that covers all modules.** ADR-0001 §4 and §5 and the
   script's `settingPrefixes` assume one prefix. With a second module there are either
   per-module prefixes, with the script taking the prefix per executable, or one prefix for all
   of GenPRES. Small, but it has to be decided before the second module exists, because the
   script hard-codes the pattern.
5. **Vocabulary.** The draft used "onion" and "impure outer layer"; ADR-0001 uses rings, core,
   adapters and DMZ. The draft's claim that the FHIR ADR backs a FHIR-facade route is replaced
   by a plain statement that the facade is the hospital's interface and GenPRES has no FHIR code
   on master: [ADR-0004](../adr/0004-fhir-r4-integration.md) is superseded (2026-08-28) and the
   prototype was deleted.

## Open before ADR-0006 can be written

- The draft itself, so that ADR-0006 can state what was decided and what else was considered.
- One prefix or one per executable (point 4).
- Whether the modular decision lands as `Proposed` and what condition makes it `Accepted`.

## Related

- [ADR-0001: System Architecture](../adr/0001-system-architecture.md), § Dependency rule and effects
- [ADR-0000: Documentation Rules](../adr/0000-documentation-rules.md), §2 on what an ADR is for
- [Backlog](backlog.md) items 3–8, which a modular split would reshape
- `scripts/CheckDependencyRule.fsx` — the ring map the discussion is checked against
