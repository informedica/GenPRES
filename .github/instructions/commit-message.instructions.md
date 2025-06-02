# Git Commit Message Instructions

## General Guidelines

Be clear, concise, and specific in your commit messages. Each message should provide enough context for other developers to understand the change without needing to look at the code.
Commit messages should follow a consistent format to ensure clarity and maintainability across the GenPRES project. This document outlines the conventions and best practices for writing commit messages.

### Commit Message Format
Use the conventional commits format for all commit messages:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types
- **feat**: A new feature for the user
- **fix**: A bug fix
- **docs**: Documentation only changes
- **style**: Changes that do not affect the meaning of the code (white-space, formatting, etc.)
- **refact**: A code change that neither fixes a bug nor adds a feature
- **perf**: A code change that improves performance
- **test**: Adding missing tests or correcting existing tests
- **build**: Changes that affect the build system or external dependencies
- **ci**: Changes to CI configuration files and scripts
- **chore**: Other changes that don't modify src or test files
- **revert**: Reverts a previous commit

### Scope Guidelines
Use specific scopes relevant to the GenPRES project:

#### Library Scopes
- **gensolver**: Changes to GenSolver.Lib (constraint solving, equations, variables)
- **genorder**: Changes to GenOrder.Lib (medical orders, prescriptions)
- **genunits**: Changes to GenUnits.Lib (units of measure, calculations)
- **zindex**: Changes to ZIndex.Lib (medication database, drug information)
- **utils**: Changes to Utils.Lib (shared utilities, common functions)

#### Application Scopes
- **client**: Changes to the client-side application
- **server**: Changes to the server-side application
- **api**: Changes to API endpoints or contracts
- **ui**: Changes to user interface components
- **config**: Configuration changes

#### Infrastructure Scopes
- **deps**: Dependency updates
- **docker**: Docker configuration changes
- **github**: GitHub Actions, workflows, or repository configuration
- **build**: Build system changes
- **deploy**: Deployment configuration

### Description Guidelines
- Use the imperative mood ("add", "fix", "update", not "added", "fixed", "updated")
- Keep the first line under 50 characters
- Do not end with a period
- Be specific about what changed
- Reference the domain when relevant (e.g., "dosage calculation", "constraint solving")

### Body Guidelines
- Wrap at 72 characters
- Explain what and why, not how
- Include context for the change
- Reference relevant issues or tickets
- Explain breaking changes

### Footer Guidelines
- Include `BREAKING CHANGE:` for breaking changes
- Reference issues: `Fixes #123`, `Closes #456`
- Include co-authors: `Co-authored-by: Name <email@example.com>`

## Examples

### Feature Examples
```
feat(genorder): add pediatric dosage calculation

Implement weight-based dosage calculations for pediatric patients.
Includes validation for age ranges and maximum dose limits.

Fixes #42
```

```
feat(ui): add medication search component

Add autocomplete search for medications with filtering by:
- Generic name
- Brand name  
- ATC code
- Therapeutic class
```

### Fix Examples
```
fix(gensolver): resolve infinite loop in constraint propagation

Fix edge case where circular constraints caused the solver to loop
indefinitely. Added cycle detection and proper error handling.

Fixes #78
```

```
fix(genunits): correct mg/ml to mmol/L conversion for NaCl

Fixed conversion factor calculation that was causing incorrect
concentration values for sodium chloride solutions.
```

### Documentation Examples
```
docs(readme): update installation instructions

Add requirements for .NET 8 and update package manager commands
for both npm and dotnet CLI.
```

```
docs(api): add examples for dosage calculation endpoints

Include request/response examples and error scenarios for
/api/calculate-dosage endpoint.
```

### Refactor Examples
```
refactor(genorder): extract validation logic to separate module

Move order validation functions from Order.fs to OrderValidation.fs
to improve separation of concerns and testability.
```

### Test Examples
```
test(genunits): add property-based tests for unit conversions

Add FsCheck tests to verify conversion properties:
- Reflexivity: convert to same unit
- Transitivity: A->B->C equals A->C
- Precision: maintain accuracy within tolerance
```

### Build Examples
```
build(deps): update @mui packages to v6

Update Material-UI packages:
- @mui/material: 5.14.1 -> 6.0.0
- @mui/icons-material: 5.14.1 -> 6.0.0
- @mui/x-data-grid: 6.9.0 -> 7.0.0

BREAKING CHANGE: Theme API changes require component updates
```

### CI Examples
```
ci(github): add automated testing for PR branches

Add workflow to run F# tests and linting on pull requests.
Include test coverage reporting and build artifact generation.
```

## Project-Specific Guidelines

### Medical Domain Context
- Be specific about medical concepts (dosage, concentration, prescription)
- Include safety implications when relevant
- Reference medical standards or guidelines when applicable
- Mention patient safety considerations for critical changes

### Mathematical Context
- Specify the type of mathematical operation (solving, optimization, calculation)
- Include precision or accuracy considerations
- Mention performance implications for complex calculations
- Reference mathematical properties being maintained

### Units of Measure
- Always specify the units involved in calculations
- Mention conversion accuracy and precision
- Include validation of unit compatibility
- Reference physical quantities and their relationships

### Error Messages
- Include the specific error being addressed
- Mention user experience improvements
- Reference validation rules or business logic
- Include examples of error scenarios

## Anti-Patterns to Avoid

### Bad Examples
```
// Too vague
fix: bug fix

// Wrong mood
feat: added new feature

// Too long first line
feat(genorder): implement comprehensive pediatric dosage calculation system with age-based validation

// Missing scope for library changes
feat: add unit conversion

// Non-descriptive
update stuff

// Wrong type
feat: fix typo in documentation
```

### Good Corrections
```
// Specific and clear
fix(genorder): resolve null reference in order validation

// Correct mood
feat(genorder): add dosage calculation validation

// Concise first line with detailed body
feat(genorder): add pediatric dosage calculation

Implement weight-based dosage calculations for pediatric patients
including age range validation and maximum dose limits.

// Proper scope
feat(genunits): add mL to L conversion

// Descriptive
refactor(genorder): extract order validation logic

// Correct type
docs: fix typo in installation guide
```

## Tools and Automation

### Commit Message Templates
Use this template for consistent commit messages:

```
# <type>[optional scope]: <description>
# |<----  Using a Maximum Of 50 Characters  ---->|

# Explain why this change is being made
# |<----   Try To Limit Each Line to a Maximum Of 72 Characters   ---->|

# Provide links or keys to any relevant tickets, articles or other resources
# Example: Fixes #23

# --- COMMIT END ---
# Type can be 
#    feat     (new feature)
#    fix      (bug fix)
#    refact   (refactoring production code)
#    style    (formatting, missing semi colons, etc; no code change)
#    docs     (changes to documentation)
#    test     (adding or refactoring tests; no production code change)
#    chore    (updating grunt tasks etc; no production code change)
#    perf     (performance improvements)
#    build    (build system changes)
#    ci       (CI configuration changes)
#    revert   (reverts a previous commit)
#
# Remember to
#    - Use the imperative mood in the subject line
#    - Do not end the subject line with a period
#    - Separate subject from body with a blank line
#    - Use the body to explain what and why vs. how
#    - Can use multiple lines with "-" for bullet points in body
```