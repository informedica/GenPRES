# Support

## Getting Help with GenPRES

Thank you for using GenPRES! This document provides guidance on how to get help and support. The current document is a first draft and will be expanded over time to include more details on support processes, contact points, and best practices.

**Important:** Before using GenPRES in any context, please review the canonical clinical use and support disclaimer in the **Medical Advice Disclaimer** section below.

## Before Asking for Help

1. **Check the documentation**
   - [README.md](README.md) - Getting started and overview
   - [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture
   - [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guidelines
   - `docs/` - Detailed documentation

2. **Search existing resources**
   - [GitHub Issues](https://github.com/informedica/GenPRES/issues) - Known bugs and feature requests
   - [GitHub Discussions](https://github.com/informedica/GenPRES/discussions) - Past questions and discussions
   - [Closed Issues](https://github.com/informedica/GenPRES/issues?q=is%3Aissue+is%3Aclosed) - Previously resolved problems

3. **Verify your setup**
   - Ensure you have the correct .NET version (see `global.json`)
   - Check that all dependencies are installed
   - Review error messages carefully

## How to Ask Questions

### GitHub Discussions (Preferred)

Use [GitHub Discussions](https://github.com/informedica/GenPRES/discussions) for:

- **General questions** about using GenPRES
- **Design discussions** and architectural questions
- **Feature ideas** and brainstorming
- **Clinical use cases** and scenarios
- **Best practices** and implementation advice

**Create a new discussion** in the appropriate category:

- **Q&A**: Questions about using or developing GenPRES
- **Ideas**: Feature requests and suggestions
- **Show and Tell**: Share your implementations or extensions
- **General**: Other discussions

**Include in your question:**

- Clear, descriptive title
- What you're trying to accomplish
- What you've tried so far
- Relevant code snippets or configuration
- Error messages (if applicable)
- Environment details (OS, .NET version, browser)

### GitHub Issues

Use [GitHub Issues](https://github.com/informedica/GenPRES/issues) **only** for:

- **Bug reports** - Something is broken or incorrect
- **Feature requests** - Specific, well-defined new functionality
- **Documentation improvements** - Errors or gaps in documentation

**Do NOT use issues for:**

- General questions (use Discussions instead)
- Support requests (use Discussions instead)
- Debugging help (use Discussions first)

When creating an issue, use the appropriate template and provide:

- Clear reproduction steps for bugs
- Expected vs actual behavior
- Environment information
- Screenshots or error messages
- Relevant code snippets

## Clinical and Safety Questions

For questions related to:

- **Clinical accuracy** of dosing calculations
- **Safety concerns** with medication recommendations
- **Medical literature** references or validation
- **Regulatory compliance** questions

Please:

1. Review existing clinical documentation in `docs/scenarios/` and `docs/mdr/`
2. Search for related issues with `safety` or `clinical` labels
3. Create a discussion in the Q&A category with `[Clinical]` prefix
4. Be prepared to provide clinical context and references

**For urgent safety concerns**, see [SECURITY.md](SECURITY.md) for responsible disclosure process.

## Contributing Back

If you've solved a problem or learned something useful:

- **Answer questions** in Discussions
- **Improve documentation** via pull request
- **Share your experience** in Show and Tell
- **Report bugs** you discover
- **Contribute code** following [CONTRIBUTING.md](CONTRIBUTING.md)

## Types of Support

### Community Support (Free)

- GitHub Discussions for questions
- GitHub Issues for bugs and features
- Community-driven responses
- Best-effort support from maintainers and community

**Response time**: Variable, typically 1-7 days depending on complexity and community activity

### Professional Support

For organizations requiring:

- SLA-based support
- Priority bug fixes
- Custom feature development
- Regulatory consulting
- Integration assistance
- Training and onboarding

**Contact**: [INSERT PROFESSIONAL SUPPORT CONTACT]

## What to Include in Support Requests

### For General Questions

```
## What I'm trying to do
[Clear description of your goal]

## What I've tried
[Steps you've already taken]

## Environment
- OS: [macOS/Windows/Linux]
- .NET version: [output of `dotnet --version`]
- GenPRES version: [version or commit hash]
- Browser: [if UI-related]

## Additional context
[Any other relevant information]
```

### For Bug Reports

```
## Description
[What's wrong]

## Steps to Reproduce
1. [First step]
2. [Second step]
3. [...]

## Expected Behavior
[What should happen]

## Actual Behavior
[What actually happens]

## Error Messages
[Full error messages or stack traces]

## Environment
- OS: [macOS/Windows/Linux]
- .NET version: [output of `dotnet --version`]
- GenPRES version: [version or commit hash]
- Browser: [if UI-related]

## Screenshots
[If applicable]
```

### For Feature Requests

```text
## Problem Statement
[What problem does this solve?]

## Proposed Solution
[How should it work?]

## Alternatives Considered
[What else could solve this problem?]

## Clinical/Safety Implications
[Any patient safety considerations?]

## Additional Context
[Links to literature, guidelines, or examples]
```

## Response Expectations

### Community Discussions

- **Simple questions**: Often answered within 1-3 days
- **Complex questions**: May take 1-2 weeks
- **No guarantee** of response time
- Community members and maintainers respond when available

### Bug Reports

- **Critical safety bugs**: Within 24-48 hours
- **Major bugs**: Within 1 week
- **Minor bugs**: As time permits
- Priority based on severity and impact

### Feature Requests

- **Initial response**: Within 1-2 weeks
- **Implementation**: Based on roadmap and priorities
- May be marked as "help wanted" for community contribution

## Code of Conduct

All interactions must follow our [Code of Conduct](CODE_OF_CONDUCT.md). We expect:

- **Respectful communication**
- **Professional behavior**
- **Patience** with volunteers and community members
- **Clear, constructive** feedback

## Language

Primary language for support is **English**. This ensures:

- Broader community participation
- Better searchability of past discussions
- Consistency in documentation

## Medical Advice Disclaimer

**Canonical disclaimer:** This repository contains the source code and documentation for GenPRES. It does not include professional support services, and it is not intended for direct clinical use without appropriate validation and regulatory approval.

⚠️ **IMPORTANT**: GenPRES is a decision support tool, not a replacement for clinical judgment.

- **Do not** use GitHub for urgent clinical questions
- **Do not** rely solely on software for patient care decisions
- **Always** verify calculations and recommendations with appropriate clinical expertise
- **Follow** your institution's policies and protocols

For medical emergencies or urgent clinical questions, contact appropriate medical professionals.

## Privacy and Confidentiality

When asking questions:

- **Never** share actual patient data
- **Never** include PHI (Protected Health Information)
- **Use** anonymized or synthetic examples
- **Respect** patient privacy and GDPR/HIPAA requirements

Violations will be removed and may result in access restrictions.

## Useful Resources

### Documentation

- [README.md](README.md) - Project overview
- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture
- [CONTRIBUTING.md](CONTRIBUTING.md) - How to contribute
- `docs/mdr/` - Medical device regulatory documentation
- `docs/scenarios/` - Clinical scenario examples

### External Resources

- [F# Documentation](https://docs.microsoft.com/en-us/dotnet/fsharp/)
- [SAFE Stack Documentation](https://safe-stack.github.io/)
- [MDR Resources](docs/mdr/mdr-regulations.md)

### Community

- [GitHub Discussions](https://github.com/informedica/GenPRES/discussions)
- [GitHub Issues](https://github.com/informedica/GenPRES/issues)
- [Contribution Guidelines](CONTRIBUTING.md)

## Feedback on Support Process

We welcome feedback on how to improve our support process:

- Open a discussion in the General category
- Suggest documentation improvements
- Share what worked well or could be better

---

**Last updated**: 2025-12-09

Thank you for being part of the GenPRES community! 🚀
