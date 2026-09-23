# Project Governance

## Overview

This document describes the governance model for GenPRES, a medical decision support system for medication safety in pediatric and adult care.

At this moment, GenPRES is not MDR certified and should not be used for clinical use. We are working on MDR certification.

## Project Vision

GenPRES aims to provide safe, accurate, and reliable medication dosing calculations for pediatric and adult patients, reducing medication errors and improving patient outcomes through evidence-based decision support.

## Governance Principles

1. **Patient Safety First**: All decisions prioritize patient safety and clinical accuracy
2. **Working towards MDR certification**: Development is aimed at meeting the EU Medical Device Regulation (MDR) and the applicable ISO / NEN standards, which will be added when the regulatory strategy is settled
3. **Transparency**: Open decision-making processes and clear documentation
4. **Evidence-Based**: Clinical decisions grounded in scientific literature and guidelines
5. **Community Inclusion**: Welcoming contributions while maintaining quality standards
6. **Quality Over Speed**: Thorough review and validation before releases

## Roles and Responsibilities

### Project Lead

- Overall project direction and priorities
- Final authority on releases and major changes
- Oversight of the MDR certification process
- Conflict resolution
- Community health and growth

### Technical Lead

- Architecture and technical design decisions
- Code quality, code review and testing standards
- Build, release and deployment tooling
- Technical direction of the libraries, server and client

### UI/UX Designer

- Design of the user interface and user workflows
- Usability research and testing with clinical users
- Accessibility of the user interface

### Implementation Lead

- Planning and coordination of the introduction of GenPRES in healthcare organizations
- Onboarding and training of users

### Clinical Advisors

- Provide clinical context for requirements
- Review risk assessments

### Maintainers

- Develop features and fix bugs
- Review and approve pull requests
- Triage issues
- Write tests and maintain documentation

### Contributors

- Submit pull requests, bug reports and feature ideas
- Comment on issues and pull requests
- Take part in GitHub Discussions
- Are credited in the release notes

Who holds each role is recorded in the proprietary MDR documentation.

## Decision-Making Process

All changes related to clinical safety will be described in the proprietary MDR repository.

### Routine Decisions

**Examples**: Bug fixes, minor features, documentation updates, refactoring

**Process:**

1. Contributor submits PR
2. Automated checks pass (tests, formatting, linting)
3. Code review by at least one maintainer
4. Maintainer approval and merge

### Significant Changes

**Examples**: New major features, API changes, architectural changes, dependency updates

Significant changes follow the [pull request process in CONTRIBUTING.md](CONTRIBUTING.md#pull-request-process): an issue, an agreed implementation plan in `docs/implementation-plans/`, then small implementation PRs.

## Consensus Building

We use **lazy consensus** for most decisions:

- Proposals are assumed accepted if no objections within reasonable timeframe
- Explicit approval not always required
- Objections must be raised with reasoning
- Attempt to address concerns and find compromise
- Project lead makes final decision if consensus cannot be reached

## Conflict Resolution

1. **Direct Communication**: Contributors are encouraged to resolve conflicts directly
2. **Maintainer Mediation**: If unresolved, maintainers help facilitate discussion
3. **Project Lead Decision**: Final authority rests with project lead
4. **Code of Conduct**: All conflicts handled per [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

## Pull Request Review Process

### Standard PRs

- **Required**: 1 maintainer approval
- **Optional**: Additional reviews welcome
- **Automated**: Tests, linting, formatting checks must pass

## Release Authority

**Release Manager**: Project Lead or designated maintainer

**Release Process** (automated by EasyBuild.ShipIt, see [DEVELOPMENT.md](DEVELOPMENT.md#changelog--release-automation-easybuildshipit)):

1. All tests passing on `master`
2. ShipIt derives the version from conventional commits and opens a release PR with the `CHANGELOG.md` section and the `Directory.Build.props` bump
3. Release manager reviews and merges the release PR
4. `tag-release.yml` tags the merge commit, publishes the GitHub Release and the Docker image

**Release Schedule:** there is no calendar cadence. ShipIt keeps a release PR current on every push to `master`; the release manager merges it when a release is warranted (see [ROADMAP.md](ROADMAP.md#release-cadence)).

## Medical Device Governance

GenPRES is not MDR certified yet. We are working on MDR certification.

How a certified version is frozen and maintained is proposed in [ADR-0006](docs/adr/0006-accredited-scope-and-maintenance-lines.md).

**Documentation:**

- Design History File (DHF), risk management, requirements and validation records are maintained in the separate, proprietary MDR documentation repository.
- Architecture Decision Records for this code base: `docs/adr/`

## Communication Channels

### Decision Making

- **GitHub Issues**: Feature requests, bugs, small decisions
- **GitHub Discussions**: Design discussions, questions
- **Pull Requests**: Code review and implementation discussion

### Community

- **GitHub Discussions**: General questions and community support
- **Documentation**: User guides and API references
- **README.md**: Project overview and getting started

### Internal

- Signal, for the project team

## Amendments to Governance

This governance document can be amended by:

1. Proposal via GitHub Discussion or issue
2. Discussion period
3. Consensus among maintainers
4. Final approval by project lead
5. Update document and announce changes

## Code of Conduct

All participants must follow the [Code of Conduct](CODE_OF_CONDUCT.md). Violations will be handled per the enforcement guidelines in that document.

## License

GenPRES is licensed under the GNU General Public License v3.0 (see [LICENSE](LICENSE)). All contributions are made under this license.

## Acknowledgments

This governance model is inspired by:

- Apache Software Foundation governance
- Rust language governance
- Open source medical software projects
