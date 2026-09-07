---
category: Reference
categoryindex: 1
index: 1
---

# GenPRES API Reference

This site is the generated API reference for the `Informedica.*.Lib` F# libraries that
make up GenPRES. It is built from the `///` XML documentation comments in the source by
[fsdocs](https://fsprojects.github.io/FSharp.Formatting/) and republished on every push to
`master` (see `.github/workflows/docs.yml`).

It documents the **public API surface** only. For the reasoning behind the design, read the
hand-written documentation in the repository instead:

- [Core Domain Model](https://github.com/informedica/GenPRES/blob/master/docs/domain/core-domain.md)
  — the transformation pipeline and the vocabulary the libraries share.
- [GenFORM](https://github.com/informedica/GenPRES/blob/master/docs/domain/genform-free-text-to-operational-rules.md),
  [GenORDER](https://github.com/informedica/GenPRES/blob/master/docs/domain/genorder-operational-rules-to-orders.md),
  [GenSOLVER](https://github.com/informedica/GenPRES/blob/master/docs/domain/gensolver-from-orders-to-quantitative-solutions.md)
  — the three systems that drive the pipeline.
- [Architecture Decision Records](https://github.com/informedica/GenPRES/tree/master/docs/adr).

## Libraries

Every project in `GenPRES.sln` that emits an XML documentation file is included. In rough
dependency order:

| Library | Responsibility |
| ------- | -------------- |
| `Informedica.Utils.Lib` | Shared primitives and helpers |
| `Informedica.Agents.Lib` | Agent-based execution (`MailboxProcessor`) |
| `Informedica.Logging.Lib` | The `Logger` port and concurrent log writer |
| `Informedica.NLP.Lib` | Free-text rule extraction helpers |
| `Informedica.GenUNITS.Lib` | Unit-safe `ValueUnit` arithmetic over `BigRational` |
| `Informedica.GenSOLVER.Lib` | Quantitative constraint solver |
| `Informedica.GenCORE.Lib` | Core domain model |
| `Informedica.ZIndex.Lib` | G-Standaard medication and product database |
| `Informedica.ZForm.Lib` | G-Standaard dosing reference data |
| `Informedica.NKF.Lib` | Nederlands Kinderformularium dose rules |
| `Informedica.FTK.Lib` | Farmacotherapeutisch Kompas dose rules |
| `Informedica.GenFORM.Lib` | Operational Knowledge Rules (OKRs) |
| `Informedica.GenORDER.Lib` | Clinical order scenarios and execution |
| `Informedica.GenINTERACT.Lib` | Drug interaction rules |
| `Informedica.MCP.Lib` | Model Context Protocol integration |
| `Informedica.GenPRES.Shared` | Client/server contract types and pure clinical formulas |

Use the **API Reference** link in the menu, or start from a namespace below once the site
is built.
