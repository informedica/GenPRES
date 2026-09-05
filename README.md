# The GenPRES project

The GenPRES project is an open source software initiative to enable a generic medication order entry solution for **Safe and Efficient** medication prescriptions, preparation and administration.

This project is initially aimed at the Dutch medical setting, but can easily be applied to any medical setting.

>
> **Open Source And [FAIR](https://www.go-fair.org/fair-principles/): because Safe and Efficient Healthcare is not a luxury**

**IMPORTANT**: GenPRES is not intended for direct clinical use without appropriate validation and regulatory approval, and this repository does not include professional support services. For the full clinical and support disclaimer, see [SUPPORT.md](SUPPORT.md#medical-advice-disclaimer).

---

![genpresdemo](docs/readme.gif)

---

## Background

Medication errors are one of the most common sources of medical complications. However, the medication process, prescribing, preparing and administration of medication is als one of the most thoroughly protocolized medical processes.

In order to achieve a safe and efficient medication workflow the following human error prone activities can be solved by Clinical Decision Support Software (CDSS):

1. Looking up rules and constraints
2. Calculations
3. Verification of correct applications of rules and constraints and subsequent calculations

With the assumption that software will not err in basic lookup and calculation activities, given the correct implementation, it can be assumed that such CDSS can achieve a significant reduction in medical errors and increase efficiency of workflow.

The current solution runs at: <http://genpres.nl>.

Some more background information can be found at:

- <https://medicatieveiligensnel.nl> (website in Dutch, with a language banner!)

## Build

|                                                                        GitHub Actions                                                                        |
|:------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| [![GitHub Actions](https://github.com/informedica/GenPRES/workflows/Build%20master/badge.svg)](https://github.com/informedica/GenPRES/actions?query=branch%3Amaster) |

## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications:

- **.NET SDK**, **Node.js**, and **npm**

For the canonical list of supported versions, see the
**Toolchain Requirements** section in [`DEVELOPMENT.md`](DEVELOPMENT.md#toolchain-requirements).

For the full application to run a proprietary cache file is needed containing medication product information. Collaborators can request these cache files by contacting the owner of this repository. These cache files cannot be freely distributed!

A demo cache file with medication product data is included in this repository. This contains some sample medication data from a much larger drug formulary database.

For demo and development environment variables, see `DEVELOPMENT.md#environment-configuration`.

## Starting the application

Starting the application in developer mode is now super easy, just `dotnet run` spins up the entire application. Look for different targets by `dotnet run list`.

Open a browser to <http://localhost:5173> to view the site.

Additionally, an environment variable can be set to use a different GenPRES data Excel URL:
`export GENPRES_URL_ID=<some url id>`. After starting the application, the url that is used will be
printed to the terminal. If no env is set, the default url will be used.

For Windows users, see the environment variable setup section above for PowerShell and Command Prompt syntax.

### Deployment using Docker

This will create a production ready Docker image. **The proprietary
`GENPRES_URL_ID` is no longer baked into the image** — it must be injected
at container runtime.

```bash
docker build -t [USERNAME]/genpres .
```

**Note**: this will build using the local processor architecture.

To build on macOS (M1/M2/Apple Silicon) and still want to publish for AMD64 (x86_64):

```bash
docker build --platform linux/amd64 -t [USERNAME]/genpres .
```

To run the Docker image locally, inject `GENPRES_URL_ID` and (for admin
operations) `GENPRES_PASSWORD` at runtime:

```bash
docker run -it -p 8080:8085 \
  -e GENPRES_URL_ID="your_url_id" \
  -e GENPRES_PASSWORD="your_admin_password" \
  [USERNAME]/genpres
```

For production deployments use a Docker / Kubernetes secret rather than
passing the value on the command line. Open a browser to
<http://localhost:8080> to view the site.

> **Tip**: If you find yourself typing these commands often, see
> [Helper Shell Scripts](DEVELOPMENT.md#helper-shell-scripts) in
> DEVELOPMENT.md for ready-to-paste templates of `docker-local.sh`,
> `docker-amd64.sh`, and `docker-run.sh`. These are *local-only*
> convenience wrappers — they are not committed to the repo, and the
> opt-in `.gitignore` strategy deliberately keeps them untracked so
> each developer can customize them.

## User Documentation

For guidance on using and testing the application, see the [User Guide](docs/user-guide/README.md):

- [Getting Started](docs/user-guide/getting-started.md) — accessing the app without patient data, URL parameter reference, navigating views
- [Testing Workflows](docs/user-guide/testing-workflows.md) — reproducible QA procedures: no-patient-context testing, unit conversion testing, emergency list, neonate scenarios

External functional walkthroughs (with annotated screenshots and animations):

- [Emergency List & Standard Infusion Pumps](https://picuwkz.nl/de-genpres-noodlijst/)
- [Prescribing & Drug Dosing](https://picuwkz.nl/genpres-medicatie-controle/)

## SAFE Stack Documentation

This project is based on the SAFE Stack template. This template can be used to generate a full-stack web application using the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

If you want to know more about the full Azure Stack and all of its components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

You will find more documentation about the used F# components at the following places:

- [Saturn](https://saturnframework.org/)
- [Fable](https://fable.io/docs/)
- [Elmish](https://elmish.github.io/elmish/)

For an overview of the GenPRES system architecture, see `ARCHITECTURE.md`, which serves as the stable entry point and index for the architecture decision records under `docs/adr/` and the domain documentation under `docs/domain/` (see `docs/README.md`).

## User Guide

A multilingual user guide is available in [`docs/user-guide/`](docs/user-guide/README.md):

| Language | Guide |
|----------|-------|
| 🇬🇧 English | [User Guide](docs/user-guide/en/user-guide.md) |
| 🇳🇱 Nederlands | [Gebruikershandleiding](docs/user-guide/nl/gebruikershandleiding.md) |

The guide covers basic navigation, prescribing medication, the emergency list, testing without patient data, and unit conversion testing.

## Collaboration

Any help or collaboration is welcome! You can fork this repository, post issues, ask questions or get on [slack](https://genpresworkspace.slack.com).

Some specifics, for more detailed information look at the [CONTRIBUTING.md](CONTRIBUTING.md):

- **An opt-in strategy is used** in the `.gitignore` file, i.e. you have to specifically define what should be included instead or the other way around.
- Commits follow [conventional commit format](.github/instructions/commit-message.instructions.md) with types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
