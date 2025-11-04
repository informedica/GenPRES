# GenPRES Roadmap

## Vision

GenPRES aims to be the leading open-source medication decision support system for pediatric care, providing safe, accurate, and evidence-based dosing calculations that reduce medication errors and improve patient outcomes.

## Current Status (v2.0 Development)

🚧 **Active Development** - Moving toward production-ready release

**Current Phase**: Foundation Building & Documentation
- Core libraries implemented (GenSolver, GenUnits, GenOrder, GenForm)
- SAFE Stack architecture in place
- MDR compliance documentation in progress
- Test coverage expanding

## Release Schedule

### v2.0.0 - Production Release (Target: Q4 2026)

First production-ready release with MDR compliance and clinical validation.

**Status**: In Development

**Major Milestones**:
- 12 structured workshops (see [Production Plan](docs/roadmap/genpres-production-plan-2026-v3.md))
- Complete MDR documentation package
- Clinical validation studies
- Regulatory compliance verification
- Production deployment infrastructure

### Development Phases

#### Phase 1: Foundation & Governance (Q1 2026)
**Workshop W1-W3**

- ✅ W1: Project Structure & Governance (In Progress)
  - Community health files
  - Governance model
  - Quality gates
  - CI/CD foundation

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

### v2.1.0 - Enhanced Clinical Features (Target: Q1 2027)

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

### v2.2.0 - Advanced Calculations (Target: Q2 2027)

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

### v2.3.0 - Workflow Integration (Target: Q3 2027)

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

### v3.0.0 - AI/ML Integration (Target: 2028)

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

We follow [Semantic Versioning](https://semver.org/):

- **Major (x.0.0)**: Breaking changes, major features, architectural changes
- **Minor (2.x.0)**: New features, non-breaking enhancements
- **Patch (2.0.x)**: Bug fixes, security patches, minor improvements

### Release Cadence

- **Major releases**: Annually or as needed
- **Minor releases**: Quarterly
- **Patch releases**: As needed (especially for critical bugs)

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

## Detailed Planning

For detailed workshop planning and task breakdown, see:
- [GenPRES Production Plan 2026 v3](docs/roadmap/genpres-production-plan-2026-v3.md)
- [W1: Project Structure & Governance](docs/roadmap/w1-project-structure-and-governance.md)

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

**Document Version**: 1.0
**Last Updated**: 2025-10-25
**Next Review**: 2026-01-25

For the most up-to-date information, see the [project GitHub repository](https://github.com/informedica/GenPRES).
