# GenPRES Roadmap

## Vision

GenPRES aims to be the leading open-source medication decision support system for pediatric and adult care, providing safe, accurate, and evidence-based dosing calculations that reduce medication errors and improve patient outcomes.

## Current Status

🚧 **Active Development** - Moving toward production-ready release

**Current Phase**: MVPAP2019 — the minimal replacement of the AfsprakenProgramma 2019 (below)
- Core libraries implemented (GenSOLVER, GenUNITS, GenORDER, GenFORM, NLP, MCP)
- SAFE Stack architecture in place (upgraded to Fable 5 / React 19 / Vite 8 in Q1 2026)
- Architecture decisions recorded in `docs/adr/` (pruned under issue #411, numbering is not contiguous); MDR compliance documentation maintained in the separate, proprietary MDR documentation repository
- Test coverage expanding (Expecto property tests for solver, GenUNITS, GenORDER)
- LRU memoization for GenSOLVER prototyped in scripts (pending integration; see the GenSOLVER domain document)
- MCP stdio server (Informedica.MCP.Server) exposing GenFORM/GenORDER tools
- NLP dose-rule extraction pipeline (DoseRuleExtract.fsx) for semi-automated data entry
- G-Standaard dose-rule fallback for medications without GenFORM spreadsheet entries (prototype; implementation plan for #307)
- Shared clinical calculations (BSA, age, renal eGFR) in `Informedica.GenPRES.Shared` for server and client (ADR-0003)

## Release Schedule

### First production release (Target: Q4 2026)

First production-ready (non-pre-release) version with MDR compliance and clinical validation. Version numbers are derived from conventional commits by EasyBuild.ShipIt (see [DEVELOPMENT.md](DEVELOPMENT.md#changelog--release-automation-easybuildshipit)); the current line is `0.1.x-alpha` and the stable number is assigned when the pre-release marker is dropped, not planned here.

**Status**: In Development

**Major Milestones**:
- MVPAP2019 (below)
- Complete MDR documentation package
- Clinical validation studies
- Regulatory compliance verification
- Production deployment infrastructure

### MVPAP2019

The first deliverable. MVPAP2019 is the minimal replacement of the AfsprakenProgramma 2019 (AP2019), the PICU/NICU workflow application at UMC Utrecht / WKZ, on three pillars: **create** TPN and continuous medication orders, **record** them, and **notify the pharmacy** with the calculated preparation instructions. Everything else AP2019 does is post-MVP.

- [MVPAP2019: What Has to Be Built, and What Has to Be Configured](docs/roadmap/mvpap2019-gap-overview.md) sorts every remaining item into software (a GitHub issue in a milestone), configuration of the rule sheets, an arrangement on the hospital side, or out of the MVP.
- [Fit-Gap Analysis: AP2019 vs GenPRES](docs/roadmap/fit-gap-ap2019-vs-genpres.md) is the full comparison the MVP was cut from.
- The [MainEHR integration model](docs/scenarios/integration/) holds the use cases the record pillar is built to; the nutrition order and the pharmacy notification are the next to be written.

The work is tracked in the GitHub milestones, due dates as of 2026-09-22:

| Milestone | Due | Holds |
|---|---|---|
| M1 Improve the overall build system and CI pipeline | 2026-09-06 | closed |
| M2 GenPRES integration in the hospital environment | 2026-09-20 | launch, Session, PIN enrolment, signing and order plan versions; the store and the adapters to the hospital |
| M3 Pharmacy notification | 2026-10-04 | the preparation instruction and its electronic hand-off |
| M4 UI and UX update | 2026-10-18 | the Nutrition view, the totals, the remaining MVP-critical UI |
| M5 Bug fixing and final updates before go live | 2026-11-29 | solver and remaining defects |

MDR and regulatory work runs beside the milestones and is tracked separately.

## Feature Roadmap

### Enhanced Clinical Features (Target: Q1 2027)

**Focus**: Expanded clinical capabilities

- [ ] Additional medication categories
  - Antibiotics dosing
  - Pain management protocols
  - Emergency medications
- [ ] Enhanced chemotherapy support
  - Body surface area calculations
  - Cycle management
  - Dose adjustments
- [ ] Renal dosing adjustments
  - GFR-based adjustments
  - Renal function monitoring
  - Dialysis protocols
- [ ] Drug interaction checking
  - Basic interaction database
  - Severity classification
  - Clinical recommendations

### Advanced Calculations (Target: Q2 2027)

**Focus**: Sophisticated dosing algorithms

- [ ] Pharmacokinetic modeling
  - Vancomycin dosing
  - Aminoglycoside dosing
  - Population PK models
- [ ] Therapeutic drug monitoring
  - Level interpretation
  - Dose adjustment recommendations
  - Sampling time optimization
- [ ] Weight-based protocols
  - Ideal body weight calculations
  - Adjusted body weight
  - Obesity dosing guidelines

### Workflow Integration (Target: Q3 2027)

**Focus**: Clinical workflow optimization

- [ ] Order sets and protocols
  - Pre-defined order sets
  - Protocol templates
  - Customization capability
- [ ] Clinical decision support rules
  - Age-appropriate dosing
  - Weight-based alerts
  - Renal function alerts
- [ ] Enhanced reporting
  - Dose calculation reports
  - Audit trail reports
  - Utilization statistics

### AI/ML Integration (Target: 2028)

**Focus**: Machine learning enhancements

- [ ] Predictive dosing recommendations
  - Historical outcome analysis
  - Patient-specific predictions
  - Continuous learning
- [ ] Natural language processing
  - Order entry via natural language
  - Documentation analysis
  - Literature mining
- [ ] Anomaly detection
  - Unusual dosing patterns
  - Potential errors
  - Safety alerts

## Long-Term Vision (2028+)

### Research & Development
- Integration with pharmacogenomics data
- Real-world evidence collection
- Outcomes research platform
- International expansion (localization)

### Platform Expansion
- Mobile applications (iOS/Android)
- Wearable device integration
- Home care support
- Patient/family engagement tools

### Ecosystem Development
- Plugin architecture for extensions
- Third-party integrations
- API marketplace
- Community contributions

## How to Influence the Roadmap

We welcome community input on our roadmap:

1. **Feature Requests**: Create a GitHub Discussion in the Ideas category
2. **Clinical Needs**: Share use cases and clinical scenarios
3. **Partnerships**: Contact us about collaboration opportunities
4. **Contributions**: Implement features and submit pull requests

### Priority Considerations

Features are prioritized based on:
- **Patient Safety Impact**: Direct impact on medication safety
- **Clinical Need**: Frequency and urgency of clinical scenarios
- **Evidence Base**: Available literature and guidelines
- **Regulatory Compliance**: MDR and regulatory requirements
- **Resource Availability**: Development capacity and expertise
- **Community Interest**: User requests and contributions

## Versioning Strategy

We follow [Semantic Versioning](https://semver.org/). The version is not chosen by hand: EasyBuild.ShipIt derives it from the conventional-commit history (`feat` bumps minor, `fix` bumps patch, a breaking change bumps major) and opens a release PR with the changelog section — see [DEVELOPMENT.md](DEVELOPMENT.md#changelog--release-automation-easybuildshipit). Until the first stable release the line is `0.1.x-alpha.N`.

### Release Cadence

There is no calendar cadence. ShipIt opens or updates the release PR on every push to `master`; a release happens when the release manager merges it, after which `tag-release.yml` tags the merge commit and publishes the GitHub Release and the Docker image.

## Development Principles

Our roadmap is guided by:

1. **Safety First**: Patient safety is paramount in all decisions
2. **Evidence-Based**: Grounded in clinical literature and guidelines
3. **Quality Over Speed**: Thorough validation before release
4. **Community-Driven**: Responsive to user needs and feedback
5. **Regulatory Compliance**: Maintain MDR and quality standards
6. **Open & Transparent**: Public roadmap and decision-making

## Dependencies & Risks

### Key Dependencies
- .NET ecosystem and SAFE Stack stability
- Clinical advisory board availability
- Regulatory landscape changes
- Resource and funding availability

### Known Risks
- Regulatory approval timelines
- Clinical validation complexity
- Resource constraints
- Technical debt management

### Mitigation Strategies
- Incremental delivery approach
- Early regulatory engagement
- Strong testing and validation
- Active community building

## Get Involved

Want to contribute to GenPRES development?

- **Developers**: See [CONTRIBUTING.md](CONTRIBUTING.md)
- **Clinicians**: Share scenarios in GitHub Discussions
- **Researchers**: Collaborate on validation studies
- **Organizations**: Contact us about partnerships

## Questions?

- **Roadmap questions**: [GitHub Discussions](https://github.com/informedica/GenPRES/discussions)
- **Feature requests**: [GitHub Discussions - Ideas](https://github.com/informedica/GenPRES/discussions/categories/ideas)
- **General support**: [SUPPORT.md](SUPPORT.md)

---

**Document Version**: 1.4
**Last Updated**: 2026-09-22
**Next Review**: 2026-11-28

For the most up-to-date information, see the [project GitHub repository](https://github.com/informedica/GenPRES).
