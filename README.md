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

Medication errors are one of the most common sources of medical complications. However, the medication process, prescribing, preparing and administration of medication is also one of the most thoroughly protocolized medical processes.

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

The `GENPRES_URL_ID` environment variable selects the Google Sheet the server reads its rules from;
`.env.example` ships the public demo sheet ID. After starting the application, the sheet ID in use is
printed (masked) to the terminal. See [Environment Configuration](DEVELOPMENT.md#environment-configuration)
for the full variable list, the `.env` priority order, and Windows syntax.

### Deployment using Docker

The published image (`informedica/genpres`) defaults to **demo mode**: `GENPRES_PROD=0` and the
public demo sheet ID baked in, so a bare run starts a working demo with no secrets:

```bash
docker run -it -p 8080:8085 informedica/genpres
```

Open a browser to <http://localhost:8080> to view the site.

Production is an explicit opt-in at container runtime and needs `GENPRES_PROD=1`, the proprietary
`GENPRES_URL_ID`, a `GENPRES_PASSWORD` of at least 16 characters, and a bind mount of `data/cache`.
Neither the URL ID nor the password is ever baked into the image; inject them via a Docker or
Kubernetes secret. The repo-root `compose.yaml` wires all of this from `.env`:

```bash
cp .env.example .env    # once; for production set the secrets
docker compose pull && docker compose up -d
```

To build the image yourself use the FAKE targets `dotnet run DockerBuild` and `dotnet run DockerRun`
(cross-build with `DOCKER_PLATFORM=linux/amd64`). See [Docker](DEVELOPMENT.md#docker-wrappers) in
DEVELOPMENT.md for details.

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

The guide covers basic navigation, prescribing medication, the emergency list, testing without patient data, and unit conversion testing. For developers and testers:

- [Getting Started](docs/user-guide/getting-started.md) — running the app, entering patient data manually or via URL parameters, navigating views
- [Testing Workflows](docs/user-guide/testing-workflows.md) — reproducible QA procedures: no-patient-context testing, unit conversion, emergency list, neonate scenarios

External functional walkthroughs (with annotated screenshots and animations):

- [Emergency List & Standard Infusion Pumps](https://picuwkz.nl/de-genpres-noodlijst/)
- [Prescribing & Drug Dosing](https://picuwkz.nl/genpres-medicatie-controle/)

## Collaboration

Any help or collaboration is welcome! You can fork this repository, post issues, ask questions or get on [slack](https://genpresworkspace.slack.com).

Some specifics, for more detailed information look at the [CONTRIBUTING.md](CONTRIBUTING.md):

- **An opt-in strategy is used** in the `.gitignore` file, i.e. you have to specifically define what should be included instead of the other way around.
- Commits follow [conventional commit format](.github/instructions/commit-message.instructions.md) with types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
