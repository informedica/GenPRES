# The GenPRES project

The GenPRES project is an open source software initiative to enable a generic medication order entry solution for **Safe and Efficient** medication prescriptions, preparation and administration.

This project is initially aimed at the Dutch medical setting, but can easily be applied to any medical setting.


---

![](docs/pcm%20example.gif)

---


## Background

Medication errors are one of the most common sources of medical complications. However, the medication proces, prescribing, preparing and adminstration of medication is als one of the most thoroughly protocolized medical processes.

In order to achief a safe and efficient medication workflow the following human error prone activities can be solved by Clinical Decision Support Software (CDSS):

1. Looking up rules and constraints
2. Calculations
3. Vefification of correct applications of rules and constraints and subsequent calculations


With the assumption that software will not err in basic lookup and calculation activities, given the correct implementation, it can be assumed that such CDSS can achieve a significant reduction in medical errors and increase efficiency of workflow.

The current solution runs at: http://genpres.nl.

Some more background information can be found at:

- https://github.com/informedica/Informedica.GenPres.Lib/wiki/Informedica.GenOrder.Lib
- https://medicatieveiligensnel.nl (website in Dutch)


## Build

GitHub Actions |
:---: |
[![GitHub Actions](https://github.com/halcwb/GenPres2/workflows/Build%20master/badge.svg)](https://github.com/halcwb/GenPres2/actions?query=branch%3Amaster) |
[![Build History](https://buildstats.info/github/chart/halcwb/GenPres2)](https://github.com/halcwb/GenPres2/actions?query=branch%3Amaster) |




## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications.

Current known build configuration

* dotnet: 6.0.403
* npm: 8.4.1
* node: v17.5.0

For the full application to run a propreitary cache file is needed containing medication product information. Collaborators can request these cache files by contacting the owner of this repository. These cache files cannot be freely distributed!

A demo cache file with medication product data is include in this repository. This contains some sample medication data from a much larger drug formulary database.

## Starting the application

Currently, the project is moving to using Vite and the current build system has been updated using package.json scripts and plain commands.

For the time being the application can be started by the following procedure:

1. `npm install` will perform a tool restore and install all dependencies
2. `npm run watch-server` will start the server
3. `npm start` will start the fable compilation and the Vite development server
4. `npm run build` will create a deployment to a `deploy` folder

Open a browser to `http://localhost:5173` to view the site.

### Deployment using Docker

This will create a production ready docker image:

```bash
docker build -t [USERNAME]/genpres .
```
**Note**: this will build using the local processor architecture.

To build on a MacOs M1 and still want to publish for an ARM64
```
docker build --platform linux/amd64 -t halcwb/genpres .
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

- An opt-in strategy is used in the `.gitignore` file, i.e. you have to specifically define what should be included instead of the otherway around.
- commits are tagged with
    - chore: something that needs to be done
    - feat: a new feature
    - refact: a refactoring
    - fix: a bug fix, or otherwise
    - docs: documentation
    - tests: testing code
