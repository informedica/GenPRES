# GenPRES Roadmap

## Vision

GenPRES aims to be the leading open-source medication decision support system for pediatric and adult care, providing safe, accurate, and evidence-based dosing calculations that reduce medication errors and improve patient outcomes.

## Current Status

🚧 **Active Development** - Moving toward production-ready release

**Current Phase**: Foundation Building & Documentation
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
- 12 structured workshops (W1–W12, below)
- Complete MDR documentation package
- Clinical validation studies
- Regulatory compliance verification
- Production deployment infrastructure

### Development Phases

#### Phase 1: Foundation & Governance (Q1 2026)
**Workshop W1-W3**

- ✅ W1: Project Structure & Governance (Complete)
  - ✅ Community health files (CODE_OF_CONDUCT, CONTRIBUTING, GOVERNANCE, SECURITY, SUPPORT)
  - ✅ Governance model (GOVERNANCE.md, MAINTAINERS.md)
  - ✅ Quality gates (Fantomas formatting, Expecto test suite, CI on push)
  - ✅ CI/CD foundation (GitHub Actions: build, test, Docker image workflow)

- ⏳ W2: Core Architecture Review
  - Domain model validation
  - Constraint solver optimization
  - Unit of measure framework
  - Performance benchmarking

- ⏳ W3: Requirements & Traceability
  - Requirements review and validation
  - Traceability matrix completion
  - Test coverage analysis
  - Gap identification

#### Phase 2: Clinical Validation (Q2 2026)
**Workshop W4-W6**

- ⏳ W4: Clinical Scenarios & Testing
  - Expand scenario coverage
  - Clinical accuracy validation
  - Literature review
  - Expert consultation

- ⏳ W5: Risk Management
  - Complete hazard analysis
  - Risk control implementation
  - Residual risk assessment
  - Safety testing

- ⏳ W6: Usability Engineering
  - Usability testing
  - User interface refinement
  - Critical task analysis
  - User documentation

#### Phase 3: Integration & Interfaces (Q3 2026)
**Workshop W7-W9**

- ⏳ W7: FHIR/HL7 Integration
  - ADR-0004: FHIR R4 integration architecture designed, then superseded — the prototype was never compiled and was deleted; integration restarts from the MainEHR integration model in `docs/scenarios/integration/`
  - Interface implementation
  - EHR integration testing
  - Interoperability validation
  - Integration documentation

- ⏳ W8: Data Management
  - Resource management
  - Data versioning
  - Update procedures
  - Data validation

- ⏳ W9: Security & Privacy
  - Security hardening
  - GDPR compliance
  - Audit logging
  - Penetration testing

#### Phase 4: Production Readiness (Q4 2026)
**Workshop W10-W12**

- ⏳ W10: Performance & Scalability
  - Load testing
  - Performance optimization
  - Scaling infrastructure
  - Monitoring setup

- ⏳ W11: Deployment & Operations
  - Deployment automation
  - Operations procedures
  - Backup/recovery
  - Support processes

- ⏳ W12: Documentation & Training
  - User documentation
  - Training materials
  - Administrator guides
  - Release preparation

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

**Document Version**: 1.3
**Last Updated**: 2026-08-28
**Next Review**: 2026-11-28

For the most up-to-date information, see the [project GitHub repository](https://github.com/informedica/GenPRES).
