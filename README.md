# The GenPRES project

The GenPRES project is an open source software initiative to enable a generic medication order entry solution for **Safe and Efficient** medication prescriptions, preparation and administration.

This project is initially aimed at the Dutch medical setting, but can easily be applied to any medical setting.


---

![](docs/pcm%20example.gif)

---


## Background

Medication errors are one of the most common sources of medical complications. However, the medication process, prescribing, preparing and administration of medication is als one of the most thoroughly protocolized medical processes.

In order to achieve a safe and efficient medication workflow the following human error prone activities can be solved by Clinical Decision Support Software (CDSS):

1. Looking up rules and constraints
2. Calculations
3. Verification of correct applications of rules and constraints and subsequent calculations


With the assumption that software will not err in basic lookup and calculation activities, given the correct implementation, it can be assumed that such CDSS can achieve a significant reduction in medical errors and increase efficiency of workflow.

The current solution runs at: http://genpres.nl.

Some more background information can be found at:

- https://github.com/informedica/Informedica.GenPres.Lib/wiki/Informedica.GenOrder.Lib
- https://medicatieveiligensnel.nl (website in Dutch, with a language banner!)


## Build

|                                                                        GitHub Actions                                                                        |
|:------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| [![GitHub Actions](https://github.com/halcwb/GenPres2/workflows/Build%20master/badge.svg)](https://github.com/halcwb/GenPres2/actions?query=branch%3Amaster) |
|          [![Build History](https://buildstats.info/github/chart/halcwb/GenPres2)](https://github.com/halcwb/GenPres2/actions?query=branch%3Amaster)          |




## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications.

Current known build configuration

* dotnet: 9.0.0
* npm: 10.9.0
* node: v22.11.0

For the full application to run a proprietary cache file is needed containing medication product information. Collaborators can request these cache files by contacting the owner of this repository. These cache files cannot be freely distributed!

A demo cache file with medication product data is include in this repository. This contains some sample medication data from a much larger drug formulary database.

## Starting the application

Starting the application in developper mode is now super easy, just `dotnet run` spins up the entire application. Look for different targets by `dotnet run list`.

Open a browser to `http://localhost:5173` to view the site.

Additionally, an environmental variable can be set to use a different GenPRES data excel url:
`export GENPRES_URL_ID=<some url id>`. After starting the application, the url that is used will be
printed to the terminal. If no env is set, the default url will be used.

### Deployment using Docker

This will create a production ready docker image:

```bash
docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" -t halcwb/genpres .
```
**Note**: this will build using the local processor architecture.

To build on a MacOs M1 and still want to publish for an ARM64

```bash
docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" --platform linux/amd64 -t halcwb/genpres .
```

To run the docker image locally:

```
docker run -it -p 8080:8085 [USERNAME]/genpres
```


## SAFE Stack Documentation

This project is based on the SAFE Stack template. This template can be used to generate a full-stack web application using the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

If you want to know more about the full Azure Stack and all of its components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

You will find more documentation about the used F# components at the following places:

* [Saturn](https://saturnframework.org/)
* [Fable](https://fable.io/docs/)
* [Elmish](https://elmish.github.io/elmish/)


## Collaboration

Any help or collaboration is welcome! You can fork this repository, post issues, ask questions or get on [slack](https://genpresworkspace.slack.com).

Some specifics:

- An opt-in strategy is used in the `.gitignore` file, i.e. you have to specifically define what should be included instead or the other way around.
- commits are tagged with
    - chore: something that needs to be done
    - feat: a new feature
    - refact: a refactoring
    - fix: a bug fix, or otherwise
    - docs: documentation
    - tests: testing code