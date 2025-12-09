# Project Governance

## Overview

This document describes the governance model for GenPRES, a medical decision support system for medication safety in pediatrics. As medical device software, GenPRES requires robust governance to ensure patient safety, regulatory compliance, and sustainable development.

## Project Vision

GenPRES aims to provide safe, accurate, and reliable medication dosing calculations for pediatric patients, reducing medication errors and improving patient outcomes through evidence-based decision support.

## Governance Principles

1. **Patient Safety First**: All decisions prioritize patient safety and clinical accuracy
2. **Regulatory Compliance**: Adherence to MDR, ISO 14971, IEC 62304, and applicable standards
3. **Transparency**: Open decision-making processes and clear documentation
4. **Evidence-Based**: Clinical decisions grounded in scientific literature and guidelines
5. **Community Inclusion**: Welcoming contributions while maintaining quality standards
6. **Quality Over Speed**: Thorough review and validation before releases

## Roles and Responsibilities

### Project Lead

**Current**: [INSERT PROJECT LEAD NAME AND CONTACT]

**Responsibilities:**

- Overall project direction and strategic decisions
- Final authority on releases and major changes
- Regulatory compliance oversight
- Conflict resolution
- Community health and growth
- Medical device quality management

**Authority:**

- Merge rights on main/master branch
- Release authority
- Design decision authority for safety-critical features
- Contributor access management

### Core Maintainers

**Current Core Maintainers:**

- [INSERT MAINTAINER NAMES AND AREAS]

**Responsibilities:**

- Code review and approval
- Issue triage and management
- Technical design decisions within area of expertise
- Documentation maintenance
- Community support and mentoring
- Test coverage and quality assurance

**Requirements:**

- Deep understanding of F#, functional programming, and SAFE Stack
- Knowledge of medical device software requirements
- Consistent contributions over 6+ months
- Understanding of clinical safety requirements
- Commitment to code quality and testing

**Authority:**

- Approve pull requests in their area
- Create and manage issues
- Guide technical discussions
- Mentor contributors

### Medical/Clinical Advisors

**Current Advisors:**

- [INSERT CLINICAL ADVISOR NAMES]

**Responsibilities:**

- Review clinical accuracy of calculations and algorithms
- Validate medication dosing logic
- Review safety-related changes
- Provide clinical context for requirements
- Review risk assessments

**Authority:**

- Required approval for clinical/safety-critical changes
- Veto power on changes that compromise patient safety
- Input on user requirements and clinical scenarios

### Contributors

**Anyone who contributes to GenPRES through:**

- Code contributions (pull requests)
- Documentation improvements
- Bug reports and issue triage
- Testing and validation
- Community support

**Rights:**

- Submit pull requests
- Comment on issues and PRs
- Participate in discussions
- Recognition in CONTRIBUTORS.md

Current maintainers are listed in [MAINTAINERS.md](MAINTAINERS.md).

## Decision-Making Process

### Routine Decisions

**Examples**: Bug fixes, minor features, documentation updates, refactoring

**Process:**

1. Contributor submits PR
2. Automated checks pass (tests, formatting, linting)
3. Code review by at least one maintainer
4. Clinical review if safety-related
5. Maintainer approval and merge

**Timeline**: Typically 3-7 days

### Significant Changes

**Examples**: New major features, API changes, architectural changes, dependency updates

**Process:**

1. RFC (Request for Comments) discussion in GitHub Discussions or issue
2. Design review with core maintainers
3. Clinical review if applicable
4. Consensus among maintainers (not unanimous, but no strong objections)
5. Implementation via PR with standard review process

**Timeline**: Typically 1-4 weeks

### Safety-Critical Changes

**Examples**: Dosing algorithms, unit conversions, constraint solving, risk control measures

**Process:**

1. RFC with clinical justification and literature references
2. Technical design review
3. **Mandatory clinical advisor review**
4. Risk assessment update
5. Enhanced testing requirements
6. Multiple maintainer approvals
7. Validation testing before merge

**Timeline**: Typically 2-8 weeks (may require additional validation)

### Major Strategic Decisions

**Examples**: Regulatory strategy, major technology changes, licensing changes

**Process:**

1. Proposal document by project lead or maintainer
2. Discussion period (minimum 2 weeks)
3. Input from all stakeholders
4. Final decision by project lead
5. Documentation in Architecture Decision Record (ADR)

**Timeline**: Varies based on complexity

## Consensus Building

We use **lazy consensus** for most decisions:

- Proposals are assumed accepted if no objections within reasonable timeframe
- Explicit approval not always required
- Objections must be raised with reasoning
- Attempt to address concerns and find compromise
- Project lead makes final decision if consensus cannot be reached

For safety-critical changes, **explicit approval** is required from:

- At least one core maintainer
- At least one clinical advisor
- Project lead (for high-risk changes)

## Conflict Resolution

1. **Direct Communication**: Contributors are encouraged to resolve conflicts directly
2. **Maintainer Mediation**: If unresolved, maintainers help facilitate discussion
3. **Project Lead Decision**: Final authority rests with project lead
4. **Code of Conduct**: All conflicts handled per [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

## Becoming a Maintainer

### Criteria

- Sustained contributions over 6+ months
- Deep technical knowledge of relevant areas
- Demonstrated understanding of medical device requirements
- High-quality code reviews
- Positive community engagement
- Commitment to project values and patient safety

### Process

1. Nomination by existing maintainer or self-nomination
2. Discussion among core team
3. Consensus approval
4. Onboarding and access provisioning

### Maintainer Emeritus

Maintainers who step back retain emeritus status:

- Recognition of past contributions
- Advisory capacity if desired
- Can return to active maintainer status

## Pull Request Review Process

### Standard PRs

- **Required**: 1 maintainer approval
- **Optional**: Additional reviews welcome
- **Automated**: Tests, linting, formatting checks must pass
- **Clinical review**: Required if safety-related

### Clinical/Safety PRs

- **Required**: 1 maintainer + 1 clinical advisor approval
- **Risk assessment**: Update if risk profile changes
- **Testing**: Enhanced test requirements
- **Documentation**: Update MDR documentation if needed

### Breaking Changes

- **RFC required**: Discussion before implementation
- **Deprecation policy**: Follow semantic versioning
- **Migration guide**: Document upgrade path
- **Major version bump**: Required for breaking changes

## Release Authority

**Release Manager**: Project Lead or designated maintainer

**Release Process:**

1. All tests passing
2. MDR documentation updated
3. CHANGELOG.md updated
4. Version number per semantic versioning
5. Risk management review
6. Release notes prepared
7. Tag and publish

**Release Schedule:**

- **Patch releases**: As needed for critical bugs
- **Minor releases**: Monthly (if changes warrant)
- **Major releases**: Quarterly or as needed

## Medical Device Governance

### Quality Management System (QMS)

GenPRES operates under a Quality Management System per ISO 13485:

**Quality Objectives:**

- Software reliability and correctness
- Clinical accuracy and safety
- Regulatory compliance
- Continuous improvement

**QMS Documentation:**

- Design History File (DHF): `docs/mdr/design-history/`
- Risk Management: `docs/mdr/risk-analysis/`
- Requirements: `docs/mdr/requirements/`
- Validation: `docs/mdr/validation/`

### Change Control

All changes follow change control process:

1. Change request documentation
2. Impact assessment (risk, validation, documentation)
3. Approval by appropriate authorities
4. Implementation and verification
5. Documentation update
6. Change log entry

**See**: `docs/mdr/design-history/change-log.md`

### Post-Market Surveillance

Active monitoring includes:

- User feedback collection
- Adverse event monitoring
- Performance monitoring
- Regulatory vigilance reporting

**See**: `docs/mdr/post-market/`

## Communication Channels

### Decision Making

- **GitHub Issues**: Feature requests, bugs, small decisions
- **GitHub Discussions**: RFCs, design discussions, questions
- **Pull Requests**: Code review and implementation discussion

### Community

- **GitHub Discussions**: General questions and community support
- **Documentation**: User guides and API references
- **README.md**: Project overview and getting started

### Internal

- [INSERT TEAM COMMUNICATION CHANNELS IF APPLICABLE]

## Amendments to Governance

This governance document can be amended by:

1. Proposal via GitHub Discussion or issue
2. Discussion period (minimum 2 weeks)
3. Consensus among maintainers
4. Final approval by project lead
5. Update document and announce changes

## Code of Conduct

All participants must follow the [Code of Conduct](CODE_OF_CONDUCT.md). Violations will be handled per the enforcement guidelines in that document.

## License

GenPRES is licensed under [INSERT LICENSE - check LICENSE file]. All contributions are made under this license.

## Acknowledgments

This governance model is inspired by:

- Apache Software Foundation governance
- Rust language governance
- Open source medical software projects
- ISO 13485 quality management principles

---

**Document Version**: 1.0
**Last Updated**: 2025-10-25
**Next Review**: 2026-04-25 (or sooner if significant changes occur)
