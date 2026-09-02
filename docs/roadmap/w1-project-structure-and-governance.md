# W1: Project Structure & Governance - Missing Items Analysis

## Workshop Status: Complete

**Last reviewed**: 2026-08-28 (superseding the 2025-10-25 content below)

W1 is recorded as complete in [ROADMAP.md](../../ROADMAP.md); this document is retained as the
gap analysis that drove it.

### Delivered

The community health and governance baseline is in place: `CODE_OF_CONDUCT.md`, `SECURITY.md`,
`GOVERNANCE.md`, `SUPPORT.md`, `ROADMAP.md`, `CHANGELOG.md`, `MAINTAINERS.md` and
`ARCHITECTURE.md` at the repository root, plus `.editorconfig`, `.github/PULL_REQUEST_TEMPLATE/`,
`.github/ISSUE_TEMPLATE/`, Husky pre-commit hooks (Fantomas formatting and markdown linting),
and a CI matrix in `.github/workflows/build.yml` that runs `dotnet fantomas --check` and the
Expecto suite across Linux, Windows and macOS.

Architecture Decision Records live in `docs/mdr/design-history/`, not the `docs/adr/` this
document originally proposed. That relocation was deliberate — the ADRs are part of the MDR
Design History File.

### Still open

- FSharpLint is not configured or wired into CI (Fantomas formatting is).
- No issue template for MDR change requests (`bug_report`, `feature_request` and `ux_report` exist).
- No `docs/api/` — auto-generated API documentation is tracked as
  [#460](https://github.com/informedica/GenPRES/issues/460).
- No contributor guide beyond `CONTRIBUTING.md`.
- No documented branch-protection rules.
- No code-coverage collection, no security scanning, and no dependency-update automation
  (`.github/dependabot.yml` does not exist).

The detailed per-category analysis below is the original October 2025 assessment. Individual
✅/❌ marks in it have not been re-verified and should be read as historical.

---

- [W1: Project Structure \& Governance - Missing Items Analysis](#w1-project-structure--governance---missing-items-analysis)
  - [Workshop Status: Complete](#workshop-status-complete)
    - [Delivered](#delivered)
    - [Still open](#still-open)
  - [Current Repository State Assessment](#current-repository-state-assessment)
    - [✅ Present](#-present)
      - [Root Level Files](#root-level-files)
      - [Documentation Structure (`docs/`)](#documentation-structure-docs)
        - [Design History \& Architecture (`docs/mdr/design-history/`)](#design-history--architecture-docsmdrdesign-history)
        - [Requirements (`docs/mdr/requirements/`)](#requirements-docsmdrrequirements)
        - [Risk Management (`docs/mdr/risk-analysis/`)](#risk-management-docsmdrrisk-analysis)
        - [Validation \& Testing (`docs/mdr/validation/`)](#validation--testing-docsmdrvalidation)
        - [Interface Specifications (`docs/mdr/interface/`)](#interface-specifications-docsmdrinterface)
        - [Post-Market Surveillance (`docs/mdr/post-market/`)](#post-market-surveillance-docsmdrpost-market)
        - [Usability Engineering (`docs/mdr/usability/`)](#usability-engineering-docsmdrusability)
        - [Other Documentation](#other-documentation)
    - [❌ Missing Items by Category](#-missing-items-by-category)
  - [1. Community Health Files](#1-community-health-files)
    - [✅ Completed (2025-10-25)](#-completed-2025-10-25)
  - [2. Repository Structure \& Documentation](#2-repository-structure--documentation)
    - [Present](#present)
    - [✅ Completed (2025-10-25)](#-completed-2025-10-25-1)
    - [Still Missing](#still-missing)
  - [3. Development Workflow \& Quality Gates](#3-development-workflow--quality-gates)
    - [Missing](#missing)
  - [4. CI/CD Enhancements](#4-cicd-enhancements)
    - [Missing](#missing-1)
  - [5. Issue \& Project Management](#5-issue--project-management)
    - [Missing](#missing-2)
  - [6. Versioning \& Release Management](#6-versioning--release-management)
    - [Missing](#missing-3)
  - [7. Code Quality \& Testing](#7-code-quality--testing)
    - [Missing](#missing-4)
  - [8. Documentation Infrastructure](#8-documentation-infrastructure)
    - [Missing](#missing-5)
  - [9. Medical Device Specific (Critical for GenPRES)](#9-medical-device-specific-critical-for-genpres)
    - [Present](#present-1)
    - [Missing or Needs Enhancement](#missing-or-needs-enhancement)
    - [Recommendations](#recommendations)
  - [10. Community \& Communication](#10-community--communication)
    - [Missing](#missing-6)
  - [11. License \& Legal](#11-license--legal)
    - [Missing](#missing-7)
  - [12. Accessibility \& Internationalization](#12-accessibility--internationalization)
    - [Missing](#missing-8)
  - [Priority Recommendations for W1 Workshop](#priority-recommendations-for-w1-workshop)
    - [✅ Completed (2025-10-25)](#-completed-2025-10-25-2)
    - [🔴 High Priority (Must Have - Next)](#-high-priority-must-have---next)
    - [🟡 Medium Priority (Should Have)](#-medium-priority-should-have)
    - [🟢 Lower Priority (Nice to Have)](#-lower-priority-nice-to-have)
  - [Workshop Deliverables](#workshop-deliverables)
    - [Expected Outputs from W1](#expected-outputs-from-w1)
  - [Recommended Folder Layout](#recommended-folder-layout)
    - [Proposed Repository Structure](#proposed-repository-structure)
    - [Priority Implementation Order](#priority-implementation-order)
      - [Phase 1: Critical Foundation (Week 1)](#phase-1-critical-foundation-week-1)
      - [Phase 2: Documentation Structure (Week 2)](#phase-2-documentation-structure-week-2)
      - [Phase 3: MDR Enhancements (Week 3)](#phase-3-mdr-enhancements-week-3)
      - [Phase 4: Automation \& Quality (Ongoing)](#phase-4-automation--quality-ongoing)
    - [File Naming Conventions](#file-naming-conventions)
    - [Cross-References](#cross-references)

## Current Repository State Assessment

### ✅ Present

#### Root Level Files

- **CONTRIBUTING.md** - Exists but may need enhancement for MDR compliance
- **LICENSE** - Present
- **README.md** - Present with basic setup instructions
- **.github/workflows/build.yml** - Basic build workflow for Windows, macOS, and Ubuntu
- **.github/ISSUE_TEMPLATE/** - Bug report and feature request templates
- **.github/PULL_REQUEST_TEMPLATE.md** - Basic pull request template
- **.github/copilot-instructions.md** - AI coding assistant instructions
- **.github/instructions/** - F# coding and commit message standards

#### Documentation Structure (`docs/`)

##### Design History & Architecture (`docs/mdr/design-history/`)

- ✅ **0000-change-log.md** - Design change history
- ✅ **0001-system-architecture.md** - High-level system architecture (SAFE Stack)
- ✅ **0009-mcp-server-architecture.md** - MCP access path for AI assistants
- ✅ **0014-staged-value-expansion-timed-orders.md** - Bounded value expansion in the solver
- ✅ **0015-security-baseline.md** - Security baseline for the demo deployment
- ✅ **0016-gstand-dose-rule-fallback.md** - G-Standard fallback for missing adult rules
- ✅ **0018-nlp-dose-rule-extraction.md** - LLM-based dose-rule extraction pipeline
- ✅ **0019-shared-clinical-calculations.md** - Clinical formulas shared by server and client
- ✅ **0020-fhir-r4-integration.md** - FHIR R4 as the EHR interface standard

##### Requirements (`docs/mdr/requirements/`)

- ✅ **user-requirements.md** - User requirements (UR-001 through UR-XXX)
- ✅ **software-requirements.md** - Software requirements specification
- ✅ **chemo_specific_requirements.md** - Chemotherapy-specific requirements
- ✅ **informedica.genunits.lib.requirements.md** - Units library requirements
- ✅ **traceability-matrix.xlsx** - Requirements traceability
- ✅ **genpres_traceability_matrix.xlsx** - GenPRES traceability

##### Risk Management (`docs/mdr/risk-analysis/`)

- ✅ **risk-management-plan.md** - ISO 14971 risk management plan
- ✅ **risk-management-report.md** - Risk management report
- ✅ **hazard-analysis.xlsx** - Hazard identification and analysis
- ✅ **genpres_hazard_analysis.xlsx** - GenPRES-specific hazards
- ✅ **genpres_hazard_control.xlsx** - Hazard control measures
- ✅ **risk-control-table.xlsx** - Risk control implementation
- ✅ **hazard_analysis.md** - Hazard analysis documentation

##### Validation & Testing (`docs/mdr/validation/`)

- ✅ **test-strategy.md** - Testing strategy document
- ✅ **unit-test-report.md** - Unit test results
- ✅ **integration-test-report.md** - Integration test results
- ✅ **usability-validation-report.md** - Usability testing results

##### Interface Specifications (`docs/mdr/interface/`)

- ✅ **genpres_interface_specification.md** - GenPRES interface specification
- ✅ **treatmentplan-interface-specification.md** - Treatment plan interface
- ✅ **treatmentplan-interface-specification-FHIR-IHE-revision.md** - FHIR/IHE revision
- ✅ **treatmentplan-interface-specification-merged.md** - Merged specification
- ✅ **merged_fhir_specification.md** - Merged FHIR specifications
- ✅ **merged_fhir_specification_FIXED.md** - Fixed FHIR specification

##### Post-Market Surveillance (`docs/mdr/post-market/`)

- ✅ **feedback-log.md** - User feedback tracking
- ✅ **known-issues.md** - Known issues log
- ✅ **update-plan.md** - Update and maintenance plan
- ✅ **genpres_protocol_draft.md** - Clinical protocol draft

##### Usability Engineering (`docs/mdr/usability/`)

- ✅ **user-profile.md** - User profiles and personas
- ✅ **critical-tasks.md** - Critical task analysis
- ✅ **formative-testing.md** - Formative usability testing
- ✅ **summative-testing.md** - Summative usability testing

##### Other Documentation

- ✅ **docs/roadmap/** - Strategic planning and workshops
  - ✅ **genpres-architecture-and-timeline.md** - Architecture and timeline with 12 workshops
  - ✅ **w1-project-structure-and-governance.md** - This document
- ✅ **docs/scenarios/** - Use cases (4 files)
  - UC-GENPRES-001.md (prescribe & manage patient orders), UC-GENPRES-002.md
    (stand-alone calculation), UC-GENPRES-003.md (pharmacy notification),
    UC-GENPRES-004.md (nutrition ordering)
  - The former age-group scenario documents (Newborn, Infant, Toddler, Child,
    Teenager, Adult) were generated output of
    `src/Informedica.GenORDER.Lib/Scripts/Scenarios.fsx` and are no longer kept
    in the repository. Running that script still writes them to this folder as
    local artefacts; they are gitignored so they do not reappear as untracked
    files.
- ✅ **docs/code-reviews/** - Code review documentation (3 files)
  - genpres-review.md, parseTextItem-refactoring.md, solver-memoization.md
- ✅ **docs/literature/** - EHR prescribing research (4 files)
  - ehr_medication_prescribing_research.md
  - epic_medication_prescribing_research.md
- ✅ **docs/data-extraction/** - Data extraction prompts (1 file)
  - doserule-extraction-prompt.md
- ✅ **docs/mdr/mdr-regulations.md** - MDR regulations overview

### ❌ Missing Items by Category

**Note:** Many MDR-related documents exist but are in draft/incomplete state. Items marked as "missing" below are either completely absent or need significant enhancement for production readiness.

---

## 1. Community Health Files

### ✅ Completed (2025-10-25)

- ✅ **CODE_OF_CONDUCT.md** - Contributor Covenant v2.1 adopted
  - Establishes community standards for professional medical software project
  - Includes enforcement guidelines and procedures
  - Adapted for medical device context with patient safety emphasis
  
- ✅ **SECURITY.md** - Vulnerability disclosure policy implemented
  - Security reporting process defined
  - Response timeline commitments (48 hours for initial response)
  - Severity classification (Critical/High/Medium/Low)
  - Medical device compliance considerations
  - Security best practices for contributors
  
- ✅ **SUPPORT.md** - User support resources documented
  - GitHub Discussions for questions
  - GitHub Issues for bugs and features
  - Clinical and safety question guidelines
  - Professional support options
  - Privacy and confidentiality guidelines
  
- ✅ **GOVERNANCE.md** - Project governance model established
  - Clear roles: Project Lead, Core Maintainers, Clinical Advisors
  - Decision-making processes (routine, significant, safety-critical, strategic)
  - Consensus building approach (lazy consensus + explicit approval for safety)
  - Maintainer requirements and process
  - Quality management system (QMS) integration
  - Change control procedures

---

## 2. Repository Structure & Documentation

### Present

- ✅ **docs/mdr/design-history/0001-system-architecture.md** - Comprehensive SAFE Stack architecture
- ✅ **docs/mdr/design-history/0000-change-log.md** - Design history file exists
- ✅ **docs/scenarios/** - Clinical scenarios documented (6 age groups)
- ✅ **docs/mdr/interface/** - Interface specifications (FHIR, IHE)
- ✅ **docs/code-reviews/** - Some code review documentation

### ✅ Completed (2025-10-25)

- ✅ **ROADMAP.md** - Public roadmap created
  - Version planning (v2.0 through v3.0)
  - Development phases aligned with 12 workshops
  - Feature roadmap for future releases
  - Community input process
  - Transparent planning and priorities
  - References detailed production plan in docs/roadmap/
  
- ✅ **CHANGELOG.md** - Structured release notes at root
  - Follows [Keep a Changelog](https://keepachangelog.com/) format
  - Semantic versioning aligned
  - User-facing changes focus (complementary to design-history/0000-change-log.md)
  - Clear distinction between alpha/beta/stable releases
  - Links to detailed MDR documentation
  
- ✅ **MAINTAINERS.md** - Maintainer roster and responsibilities
  - Role definitions (Project Lead, Area Maintainers, Clinical Advisors)
  - Contact information placeholders
  - Areas of responsibility
  - Becoming a maintainer process
  - Maintainer emeritus recognition

### Still Missing
  
- ❌ **AUTHORS.md** or **CONTRIBUTORS.md** - Recognition file
  - List of contributors
  - Attribution for third-party code
  
- ❌ **docs/ADR/** - Architecture Decision Records directory
  - Template-based decision records (Why we chose X over Y)
  - Critical for MDR traceability
  - Separate from design-history (which is "what" not "why")
  
- ❌ **docs/api/** - Developer-focused API documentation
  - Auto-generated from code (FSharp.Formatting)
  - Integration guides for EHR developers
  - Currently have interface specs but need code-level API reference
  
- ❌ **ARCHITECTURE.md** - Root-level architecture summary
  - High-level overview for quick reference
  - Links to detailed architecture in docs/mdr/design-history/
  - Technology stack summary
  - System context diagram

---

## 3. Development Workflow & Quality Gates

### Missing

- ❌ **Pre-commit hooks** - Automated formatting/linting
  - **Fantomas** for F# formatting
  - **FSharpLint** for code quality
  - **Commit message validation** (conventional commits)
  - Implementation: Use [pre-commit](https://pre-commit.com/) or [Husky](https://typicode.github.io/husky/)
  
- ❌ **Branch protection rules** - Documentation and enforcement
  - Require PR reviews
  - Require status checks
  - Require linear history
  - Restrict force pushes
  
- ❌ **Enhanced PR template** - Should include checklist for:
  - [ ] Tests added/updated
  - [ ] Documentation updated
  - [ ] Breaking changes noted
  - [ ] MDR compliance consideration
  - [ ] Security implications reviewed
  
- ❌ **.editorconfig** - Consistent coding style across IDEs
  - Indentation rules
  - Line endings
  - Charset
  - Trim trailing whitespace
  
- ❌ **Formatting verification in CI** - Fantomas check
  - Fail build on formatting violations
  - Auto-format option for PRs
  
- ❌ **Linting in CI** - FSharpLint integration
  - Code quality checks
  - Code smell detection
  - Configurable rules

---

## 4. CI/CD Enhancements

**Current state:** Basic build workflow exists for multiple platforms

### Missing

- ❌ **Test coverage reporting** - Integration with Codecov or Coveralls
  - Minimum coverage thresholds
  - Coverage trends
  - PR coverage diff
  
- ❌ **Automated dependency updates** - Dependabot or Renovate
  - Security vulnerability alerts
  - Automated PR for updates
  - Grouped updates
  
- ❌ **Security scanning** - SAST tools
  - GitHub Code Scanning (CodeQL)
  - Dependency vulnerability scanning
  - Secret detection
  
- ❌ **Performance benchmarking** - BenchmarkDotNet in CI
  - Performance regression detection
  - Baseline comparisons
  - Critical for solver performance
  
- ❌ **Release automation** - Semantic versioning + automated releases
  - Automated version bumping
  - Changelog generation
  - GitHub Releases creation
  
- ❌ **Docker image publishing** - Automated container builds
  - Multi-arch images
  - Version tagging
  - Registry deployment
  
- ❌ **Documentation deployment** - Auto-deploy docs to GitHub Pages
  - FSharp.Formatting or similar
  - API documentation
  - User guides
  
- ❌ **Multi-stage CI** - Separate jobs for build/test/lint/security
  - Parallel execution
  - Fail fast
  - Clear job separation

---

## 5. Issue & Project Management

**Current state:** Basic issue templates exist

### Missing

- ❌ **Issue labels** - Standardized label taxonomy
  - **Priority:** critical, high, medium, low
  - **Type:** bug, feature, documentation, question
  - **Area:** solver, ui, api, domain, etc.
  - **Status:** needs-triage, in-progress, blocked
  - **Medical:** MDR-relevant, clinical-safety
  
- ❌ **Project boards** - For sprint/release planning
  - Kanban-style boards
  - Sprint planning
  - Release tracking
  
- ❌ **Milestone definitions** - Clear release planning
  - Version milestones
  - Feature milestones
  - Due dates
  
- ❌ **Issue triage process** - Documentation
  - Triage schedule
  - Triage criteria
  - Escalation process
  
- ❌ **Discussion forums** - GitHub Discussions enabled
  - Q&A category
  - Ideas/feature requests
  - General discussion

---

## 6. Versioning & Release Management

### Missing

- ❌ **Semantic versioning policy** - Clear versioning scheme
  - Major.Minor.Patch rules
  - Breaking change policy
  - Pre-release versioning
  
- ❌ **Release process documentation** - Step-by-step release guide
  - Release checklist
  - Testing requirements
  - Deployment steps
  
- ❌ **Deprecation policy** - How to handle breaking changes
  - Deprecation timeline
  - Migration guides
  - Sunset policy
  
- ❌ **MinVer or GitVersion** - Automated version from git tags
  - Git tag conventions
  - CI integration
  - Package versioning
  
- ❌ **Release notes template** - Structured changelog format
  - Breaking changes section
  - New features section
  - Bug fixes section
  - Dependencies updated
  
- ❌ **Package publishing** - NuGet packages for libraries
  - Automated publishing
  - Package metadata
  - Symbol packages

---

## 7. Code Quality & Testing

### Missing

- ❌ **Test coverage requirements** - Minimum coverage thresholds
  - Overall coverage target (e.g., 80%)
  - Critical path coverage (e.g., 95%)
  - PR coverage diff requirements
  
- ❌ **Property-based testing examples** - FsCheck patterns
  - Common property patterns
  - Generator examples
  - Integration with Expecto
  
- ❌ **Integration test suite** - End-to-end scenarios
  - Full workflow tests
  - EHR integration tests
  - Performance tests
  
- ❌ **Performance test suite** - BenchmarkDotNet baselines
  - Solver benchmarks
  - Unit conversion benchmarks
  - Baseline tracking
  
- ❌ **Mutation testing** - Stryker.NET for test quality
  - Mutation score thresholds
  - CI integration
  - Critical path focus
  
- ❌ **Code quality badges** - In README for transparency
  - Build status
  - Test coverage
  - Code quality score
  - Security score

---

## 8. Documentation Infrastructure

### Missing

- ❌ **API documentation generation** - FSharp.Formatting or similar
  - XML doc comments
  - Auto-generated reference docs
  - Code examples
  
- ❌ **User documentation** - Separate from developer docs
  - User guides
  - Tutorials
  - Clinical workflows
  
- ❌ **Contributing guide sections for:**
  - First-time contributors
  - Good first issues
  - Development workflow (branch strategy)
  - Code review process
  - Testing requirements
  
- ❌ **MDR compliance documentation structure** - As noted in CONTRIBUTING.md
  - Requirements traceability
  - Risk management
  - Verification and validation
  
- ❌ **Clinical safety documentation** - For medical device certification
  - Clinical evaluation
  - Safety analysis
  - Intended use

---

## 9. Medical Device Specific (Critical for GenPRES)

### Present

- ✅ **docs/mdr/risk-analysis/risk-management-plan.md** - ISO 14971 compliant plan
- ✅ **docs/mdr/risk-analysis/risk-management-report.md** - Risk management report
- ✅ **docs/mdr/risk-analysis/hazard-analysis.xlsx** - Hazard identification (multiple files)
- ✅ **docs/mdr/risk-analysis/risk-control-table.xlsx** - Risk control measures
- ✅ **docs/mdr/requirements/traceability-matrix.xlsx** - Requirements traceability (2 versions)
- ✅ **docs/mdr/post-market/** - Post-market surveillance structure
- ✅ **docs/mdr/validation/** - Validation documentation structure
- ✅ **docs/mdr/usability/** - Usability engineering files
- ✅ **docs/mdr/requirements/user-requirements.md** - User requirements (UR-XXX)
- ✅ **docs/mdr/requirements/software-requirements.md** - Software requirements
- ✅ **docs/mdr/requirements/chemo_specific_requirements.md** - Specialized requirements

### Missing or Needs Enhancement

- ⚠️ **Software Bill of Materials (SBOM)** - Needs automation
  - Currently manual tracking
  - Need CycloneDX or SPDX format generation
  - CI/CD integration for automatic SBOM generation
  - License information consolidated

**Proposal**: use the microsoft .net tool for SBOM generation: <https://github.com/microsoft/sbom-tool>
  
- ⚠️ **Traceability matrix** - Needs consolidation and automation
  - Two Excel files exist but need unified approach
  - Automated traceability from requirements → tests → code
  - Gap analysis reporting
  - Integration with test frameworks
  
- ⚠️ **Clinical evaluation documentation** - Structure incomplete
  - Literature review framework needed
  - Clinical data collection process
  - Benefit-risk analysis template
  - Post-market clinical follow-up (PMCF) plan
  
- ❌ **Adverse event reporting** - Process documentation missing
  - Event classification criteria
  - Reporting timeline and procedures
  - Investigation workflow
  - Competent authority notification process
  
- ⚠️ **Change control process** - Needs formalization
  - Change request template needed
  - Impact assessment framework
  - Approval workflow documentation
  - Verification requirements per change type
  - Currently informal process

### Recommendations

1. **Automate SBOM generation** - Priority: HIGH
   - Use tools like `dotnet list package` + formatting
   - Include in CI/CD pipeline
   - Track license compatibility

2. **Enhance traceability matrix** - Priority: HIGH
   - Move from Excel to database or structured format
   - Auto-link test results to requirements
   - Generate compliance reports

3. **Formalize change control** - Priority: MEDIUM
   - Create GitHub issue template for change requests
   - Define approval workflow in GOVERNANCE.md
   - Link to risk management re-assessment

4. **Clinical evaluation framework** - Priority: MEDIUM
   - Template for literature search
   - Clinical data logging structure
   - Periodic review schedule

5. **Adverse event process** - Priority: HIGH (regulatory requirement)
   - Define vigilance process
   - Training documentation
   - Contact points for reporting

---

## 10. Community & Communication

### Missing

- ❌ **Communication channels** - Discord/Slack/Matrix
  - Real-time chat
  - Developer channel
  - User support channel
  
- ❌ **Meeting notes** - If regular contributor meetings occur
  - Meeting schedule
  - Notes repository
  - Action items tracking
  
- ❌ **Developer mailing list** - For announcements
  - Release announcements
  - Breaking changes
  - Security advisories
  
- ❌ **Social media presence** - Twitter/Mastodon for updates
  - Project updates
  - Community highlights
  - Event announcements
  
- ❌ **Newsletter** - For stakeholders
  - Monthly updates
  - Feature highlights
  - Community news
  
- ❌ **Office hours** - Regular Q&A sessions
  - Scheduled sessions
  - Video calls
  - Open forum

---

## 11. License & Legal

**Current:** LICENSE exists

### Missing

- ❌ **CLA (Contributor License Agreement) or DCO (Developer Certificate of Origin)** - For IP clarity
  - CLA bot integration
  - DCO sign-off requirements
  - Legal clarity
  
- ❌ **NOTICE file** - Third-party attributions
  - Third-party licenses
  - Copyright notices
  - Patent notices
  
- ❌ **License compatibility check** - For dependencies
  - Automated scanning
  - Compatibility matrix
  - Policy enforcement
  
- ❌ **Export control statement** - If applicable for medical software
  - Export classification
  - Restrictions
  - Compliance statement
  
- ❌ **GDPR/PHI compliance statement** - For data handling
  - Data processing
  - Privacy policy
  - PHI handling guidelines

---

## 12. Accessibility & Internationalization

### Missing

- ❌ **Accessibility guidelines** - WCAG compliance for UI
  - WCAG 2.1 Level AA target
  - Keyboard navigation
  - Screen reader support
  - Color contrast
  
- ❌ **Internationalization (i18n) strategy** - Multi-language support
  - Translation framework
  - Supported languages
  - Resource management
  
- ❌ **Localization (l10n) process** - Translation workflow
  - Translation memory
  - Review process
  - Update workflow

---

## Priority Recommendations for W1 Workshop

### ✅ Completed (2025-10-25)

1. ✅ **CODE_OF_CONDUCT.md** - Contributor Covenant v2.1 adopted
2. ✅ **SECURITY.md** - Security policy and vulnerability disclosure implemented
3. ✅ **GOVERNANCE.md** - Project governance model documented
4. ✅ **SUPPORT.md** - User support resources established
5. ✅ **ROADMAP.md** - Public roadmap created with version planning
6. ✅ **CHANGELOG.md** - Structured release notes following Keep a Changelog
7. ✅ **MAINTAINERS.md** - Maintainer roster and responsibilities defined

### 🔴 High Priority (Must Have - Next)

1. **.editorconfig** - Consistent formatting across IDEs
2. **Pre-commit hooks** - Fantomas + commit message validation
3. **CI enhancements** - Add formatting/linting checks to GitHub Actions
4. **Branch protection rules** - Document and enforce
5. **Enhanced PR template** - With MDR compliance checklist
6. **Semantic versioning** - MinVer/GitVersion setup
7. **SBOM generation** - For medical device compliance
8. **Issue labels** - Standardized taxonomy (priority, type, area, medical)

### 🟡 Medium Priority (Should Have)

1. ~~**ARCHITECTURE.md**~~ - ✅ Completed
2. **ADR directory** - Architecture Decision Records with templates
3. **Test coverage reporting** - Codecov or Coveralls integration
4. **Dependabot** - Automated dependency updates and security alerts
5. **Security scanning** - CodeQL or similar SAST tools
6. **AUTHORS/CONTRIBUTORS.md** - Recognition and attribution
7. **docs/api/** - Auto-generated API documentation

### 🟢 Lower Priority (Nice to Have)

1. **GitHub Discussions** - Enable for community forum
2. **Documentation site** - GitHub Pages with FSharp.Formatting
3. **CLA/DCO** - IP management (if needed for regulatory)
4. **Community channels** - Discord/Slack (evaluate need)
5. **Performance benchmarking** - BenchmarkDotNet in CI

---

## Workshop Deliverables

### Expected Outputs from W1

1. **Decision documents**
   - Monorepo vs multi-repo structure
   - CI/CD platform choice (GitHub Actions confirmed)
   - Documentation tooling
   - Code quality tools configuration

2. **Work packages created**
   - **WP-01:** Repository setup and structure
   - **WP-02:** CI/CD pipeline implementation
   - **WP-03:** Documentation framework setup
   - **WP-04:** Community health files
   - **WP-05:** Code quality automation

3. **Templates and configurations**
   - Enhanced PR template with MDR checklist
   - Issue label taxonomy
   - .editorconfig file
   - Pre-commit hook configuration
   - Fantomas configuration
   - FSharpLint rules

4. **Documentation**
   - CONTRIBUTING.md enhancements
   - CODE_OF_CONDUCT.md
   - SECURITY.md
   - GOVERNANCE.md
   - Branch protection policy

5. **Timeline and assignment**
   - Work package effort estimates
   - Developer skill matching
   - Dependency mapping
   - Implementation schedule

---

## Recommended Folder Layout

### Proposed Repository Structure

This section used to carry a ~200-line annotated tree of every file in the repository, each
marked ✅ present or ❌ missing. It was deleted: the repository is the authoritative record of
its own layout, a hand-maintained copy goes stale silently (it still claimed `.pdf` files that
do not exist), and per [issue #411](https://github.com/informedica/GenPRES/issues/411) that kind
of snapshot is not what a document should be carrying.

For the current layout see the folder structure in
[DEVELOPMENT.md](../../DEVELOPMENT.md#project-folder-structure). What is still genuinely absent
is listed under "Still open" at the top of this document.

### Priority Implementation Order

#### Phase 1: Critical Foundation (Week 1)

```text
Root Level:
  ❌ CODE_OF_CONDUCT.md
  ❌ SECURITY.md
  ❌ GOVERNANCE.md
  ❌ .editorconfig

.github/:
  ⚠️ PULL_REQUEST_TEMPLATE.md (enhance)
  ❌ workflows/format-check.yml
  ❌ workflows/lint.yml
  ❌ ISSUE_TEMPLATE/change_request.md
```

#### Phase 2: Documentation Structure (Week 2)

```text
Root Level:
  ❌ ARCHITECTURE.md
  ❌ MAINTAINERS.md
  ❌ SUPPORT.md

docs/:
  ❌ adr/ (entire directory + first ADRs)
  ❌ api/index.md
  ❌ guides/contributor-guide.md
```

#### Phase 3: MDR Enhancements (Week 3)

```text
docs/mdr/:
  ❌ clinical-evaluation/ (entire directory)
  ❌ change-control/ (entire directory)
  ❌ post-market/adverse-event-procedure.md
  ❌ requirements/traceability-automated.json
```

#### Phase 4: Automation & Quality (Ongoing)

```text
.github/workflows/:
  ❌ test.yml
  ❌ coverage.yml
  ❌ security-scan.yml
  ❌ sbom-generate.yml
  ❌ release.yml

artifacts/:
  ❌ sbom.json (auto-generated)
  
scripts/:
  ❌ pre-commit-hook.sh
  ❌ generate-sbom.sh
```

### File Naming Conventions

- **Root community files**: UPPERCASE.md (e.g., `README.md`, `SECURITY.md`)
- **Documentation**: lowercase-with-hyphens.md (e.g., `user-guide.md`)
- **MDR documents**: lowercase-with-hyphens.md (e.g., `risk-management-plan.md`)
- **ADRs**: `NNNN-short-title.md` (e.g., `0001-use-safe-stack.md`)
- **Configuration**: lowercase or project-standard (e.g., `.editorconfig`, `fantomas-config.json`)

### Cross-References

Many missing root-level files should link to detailed MDR documentation:

- `ARCHITECTURE.md` → `docs/mdr/design-history/0001-system-architecture.md`
- `ROADMAP.md` → `docs/roadmap/genpres-architecture-and-timeline.md`
- `CHANGELOG.md` (user-facing) ≠ `docs/mdr/design-history/0000-change-log.md` (developer)
- ADRs should reference design history files for detailed technical decisions

