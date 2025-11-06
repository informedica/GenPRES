# Contributing to GenPRES

Thank you for your interest in contributing to the GenPRES project! This document provides guidelines for contributing to our open source medication order entry solution.

## About GenPRES

GenPRES is an open source software initiative to enable a **Safe and Efficient** medication prescriptions, preparation and administration workflow. The project aims to reduce medication errors through Clinical Decision Support Software (CDSS) that handles:

1. Looking up rules and constraints
2. Calculations
3. Verification of correct applications of rules and constraints and subsequent calculations

**Important**: GenPRES is being developed to comply with Medical Device Regulation (MDR) certification guidelines. Documentation and processes related to MDR compliance will be added to the project as development progresses. Contributors should be aware that all code changes must adhere to medical device software development standards and quality requirements.

## How to Contribute

### Ways to Contribute

- **Report bugs** by creating detailed issue reports
- **Suggest features** through feature request issues
- **Improve documentation** by fixing typos, adding examples, or clarifying instructions
- **Submit code changes** via pull requests
- **Help with testing** by running the application and reporting issues
- **Join discussions** in our [Slack workspace](https://genpresworkspace.slack.com)

## Issue Reporting

Fill in information according to the appropriate template:

- [Bug reports](./.github/ISSUE_TEMPLATE/bug_report.md)
- [Feature requests](./.github/ISSUE_TEMPLATE/feature_request.md)

## Pull Request Process

Maintaining open-source software is difficult, and it's easy to be inefficient with maintainers' and contributors' time. Our pull request process is intended to reduce the amount of time lost to inefficiencies such as slow code reviews and reimplementing (or even rejecting) contributions, and to reduce the likelihood of accepting substandard contributions just because a lot of time has been spent on them.

We acknowledge that the process does place some up-front burden on contributors, but this early communication will help everyone to spot and correct potential problems before a lot of time has been spent. In exchange for contributors following the process, reviewers and maintainers will endeavour to be prompt with responses and reviews.

The process is meant to support productive develpoment, not be a hindrance to it, and maintainers will be pragmatic. In addition, we will monitor its effectiveness, and tweak as necessary; we want to keep the process as light as possible while still achieving the underlying aims.

### Steps

- Before making changes, describe the desired change in an issue.
- If maintainers agree that the change would be valuable, propose a high-level implementation plan in a PR to a markdown file in [./docs/implementation-plans/](./docs/implementation-plans/). The filename should contain the issue number and a short title, e.g. 54-multilingual-user-guide.md.
- Once an implementation plan is agreed, changes can be submitted via implementation PRs. Each implementation PR should have no more than 200 lines of changed code, and [ideally 25-100 lines](https://graphite.dev/blog/the-ideal-pr-is-50-lines-long). Use feature flags if necessary to prevent incomplete changes from being surfaced in the application.
- Reviewers may request that complex changes are split into smaller PRs, even if the original PR's diff is less than 200
lines.
- Certain changes can go straight to implementation PR, without needing an issue and implementation plan:
  - Code changes less than 25 lines.
  - Documentation-only changes.
- Reviewers will ask authors to rework or resubmit PRs that don't meet the above guidelines.

### Before Submission

1. **Fork and Branch**: Create a feature branch from `master`
2. **Follow Coding Standards**: Ensure your code follows our F# and commit message guidelines
3. **Write Tests**: Include comprehensive tests for new functionality
4. **Update Documentation**: Update relevant documentation and comments
5. **Test Locally**: Run all tests and ensure the application builds successfully

### Submission

Documented in [the pull request template](./.github/PULL_REQUEST_TEMPLATE.md).

### Responsibilities

#### Authors

Authors must ensure that code works and meets requirements (including what was agreed in the implementation plan) before submitting a PR. In particular, they should be confident that there are no bugs or security vulnerabilities.

Before marking PRs as ready for review, authors should review changes themselves, adding comments that highlight important changes or providing justification for something that a reviewer would be likely to ask about.

#### Reviewers

Reviewers should review PRs promptly, or select a new reviewer if they are unable to do so.

Reviewers must check that code changes: work; meet guidelines and standards; are not more complex than necessary.

Suggestions should be clearly categorised as follows: blocking (must be addressed before the PR can be merged); required but not blocking (must be addressed, but could be in a follow-up PR); optional (don't ever have to be addressed).

## Community and Communication

### Slack Workspace

Join our [Slack workspace](https://genpresworkspace.slack.com) for:

- **Questions**: Ask questions about the codebase or medical domain
- **Discussions**: Participate in design discussions
- **Collaboration**: Coordinate with other contributors
- **Support**: Get help with setup or development issues

### Code of Conduct

- **Be Respectful**: Treat all contributors with respect and kindness
- **Be Patient**: Remember that contributors have varying levels of experience
- **Be Constructive**: Provide helpful feedback and suggestions
- **Medical Focus**: Keep discussions focused on improving medication safety and efficiency

## Getting Help

- **Documentation**: Check the [README.md](README.md) for setup instructions
- **Issues**: Search existing issues before creating new ones
- **Slack**: Join our [workspace](https://genpresworkspace.slack.com) for real-time help
- **Code Examples**: Look at existing code in the libraries for patterns and examples

## Development

Documented in [./DEVELOPMENT.md](./DEVELOPMENT.md).

## Recognition

All contributors will be recognized in our project documentation. We appreciate every contribution, whether it's code, documentation, testing, or community support!

## License

By contributing to GenPRES, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to GenPRES and helping improve medication safety and efficiency! 🏥💊
