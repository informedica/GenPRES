# The GenPRES project

The GenPRES project is an open source software initiative to enable a generic medication order entry solution for **Safe and Efficient** medication prescriptions, preparation and administration.

This project is initially aimed at the Dutch medical setting, but can easily be applied to any medical setting.
>
> **Open Source And [FAIR](https://www.go-fair.org/fair-principles/): because Safe and Efficient Healthcare is not a luxury**
>

**IMPORTANT**: This repository contains the source code and documentation for GenPRES. It does not include professional support services. Also, the repository is not intended for direct clinical use without appropriate validation and regulatory approval.

---

![genpresdemo](docs/pcm%20example.gif)

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

Before starting the application set the following environment variables for a demo version of GenPRES:

**Unix/Linux/macOS (bash/zsh):**

```bash
export GENPRES_URL_ID=1xhFPiF-e5rMkk7BRSfbOF-XGACeHInWobxRbjYU0_w4
export GENPRES_LOG=1
export GENPRES_PROD=0
export GENPRES_DEBUG=1
```

**Windows PowerShell:**

```powershell
$env:GENPRES_URL_ID="1xhFPiF-e5rMkk7BRSfbOF-XGACeHInWobxRbjYU0_w4"
$env:GENPRES_LOG="1"
$env:GENPRES_PROD="0"
$env:GENPRES_DEBUG="1"
```

**Windows Command Prompt:**

```cmd
set GENPRES_URL_ID=1xhFPiF-e5rMkk7BRSfbOF-XGACeHInWobxRbjYU0_w4
set GENPRES_LOG=1
set GENPRES_PROD=0
set GENPRES_DEBUG=1
```

**NOTE** `GENPRES_PROD=0` is mandatory to run the demo version. Otherwise the application will look for production files (that do not exist on the repository version).

## Starting the application

Starting the application in developer mode is now super easy, just `dotnet run` spins up the entire application. Look for different targets by `dotnet run list`.

Open a browser to <http://localhost:5173> to view the site.

Additionally, an environment variable can be set to use a different GenPRES data Excel URL:
`export GENPRES_URL_ID=<some url id>`. After starting the application, the url that is used will be
printed to the terminal. If no env is set, the default url will be used.

For Windows users, see the environment variable setup section above for PowerShell and Command Prompt syntax.

### Deployment using Docker

This will create a production ready Docker image:

```bash
docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" -t [USERNAME]/genpres .
```

**Note**: this will build using the local processor architecture.

To build on macOS (M1/M2/Apple Silicon) and still want to publish for AMD64 (x86_64):

```bash
docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" --platform linux/amd64 -t [USERNAME]/genpres .
```

To run the Docker image locally:

```bash
docker run -it -p 8080:8085 [USERNAME]/genpres
```

Open a browser to <http://localhost:8080> to view the site.

## SAFE Stack Documentation

This project is based on the SAFE Stack template. This template can be used to generate a full-stack web application using the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

If you want to know more about the full Azure Stack and all of its components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

You will find more documentation about the used F# components at the following places:

- [Saturn](https://saturnframework.org/)
- [Fable](https://fable.io/docs/)
- [Elmish](https://elmish.github.io/elmish/)

## Collaboration

Any help or collaboration is welcome! You can fork this repository, post issues, ask questions or get on [slack](https://genpresworkspace.slack.com).

Some specifics, for more detailed information look at the [CONTRIBUTING.md](CONTRIBUTING.md):

- **An opt-in strategy is used** in the `.gitignore` file, i.e. you have to specifically define what should be included instead or the other way around.
- commits are tagged with
  - **feat**: A new feature for the user
  - **fix**: A bug fix, note that this should not be used for documentation fixes or refactoring
  - **docs**: Documentation only changes
  - **style**: Changes that do not affect the meaning of the code (white-space, formatting, etc.)
  - **refactor**: A code change that neither fixes a bug nor adds a feature
  - **perf**: A code change that improves performance
  - **test**: Adding missing tests or correcting existing tests
  - **build**: Changes that affect the build system or external dependencies
  - **ci**: Changes to CI configuration files and scripts
  - **chore**: Other changes that don't modify src or test files
  - **revert**: Reverts a previous commit

For detailed commit message guidelines and conventions, see [commit-message.instructions.md](.github/instructions/commit-message.instructions.md).
