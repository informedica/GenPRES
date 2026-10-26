---
description: "Conventional commit message format and guidelines for the GenPRES project"
---

# Git Commit Message Instructions

Every commit follows [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

The commit-msg hook (`.husky/scripts/commit-lint.fsx`) rejects a heading whose type or scope is not in the lists below. ShipIt builds `CHANGELOG.md` from the history: only `feat`, `fix` and the other rendering types reach the release notes, and a `=== changelog ===` block in the body adds detail under the entry (see DEVELOPMENT.md).

## Types

| Type | Use for |
|---|---|
| `feat` | a new feature for the user |
| `fix` | a bug fix; not for documentation or refactoring |
| `docs` | documentation only |
| `style` | formatting, white space; no change in meaning |
| `refactor` | a code change that neither fixes a bug nor adds a feature |
| `perf` | a performance improvement |
| `test` | adding or correcting tests |
| `build` | the build system or external dependencies |
| `ci` | CI configuration and scripts |
| `chore` | other changes that touch neither `src/` nor `tests/` |
| `revert` | reverts a previous commit |

## Scopes

Libraries: `agents`, `logging`, `nlp`, `genunits`, `gensolver`, `gencore`, `zindex`, `zform`, `nkf`, `ftk`, `genform`, `genorder`, `geninteract`, `mcp`, `utils`.
Applications: `client`, `server`, `api`, `ui`, `config`.
Infrastructure: `deps`, `deps-dev`, `docker`, `github`, `deploy`.

A library scope is the library's name without the `Informedica.` and `.Lib` parts. A change to a library always carries its scope; `docs` and `chore` commits may omit it.

## Description

- Imperative mood: "add", "fix", "update"; not "added", "fixed"
- Under 50 characters, no period at the end
- Specific: name the thing that changed and, where it matters, the domain (dosage calculation, constraint solving, unit conversion)

## Body and Footers

- Wrap at 72 characters
- Explain what and why, not how; state the safety implication of a dosing or rule change, the units and precision of a calculation change, the error being addressed
- `BREAKING CHANGE:` footer for a breaking change
- `Fixes #123` or `Closes #456` for the issue; `Co-authored-by: Name <email>` for co-authors

## Examples

```
feat(genorder): add pediatric dosage calculation

Implement weight-based dosage calculations for pediatric patients,
with validation of age ranges and maximum dose limits.

Fixes #42
```

```
fix(genunits): correct mg/ml to mmol/L conversion for NaCl

The conversion factor produced wrong concentration values for
sodium chloride solutions.
```

```
build(deps): update @mui packages to v6

BREAKING CHANGE: theme API changes require component updates
```

```
docs: fix typo in installation guide
```

Not acceptable: `fix: bug fix` (vague), `feat: added new feature` (wrong mood), `feat: add unit conversion` (library change without scope), `feat: fix typo in documentation` (wrong type).
