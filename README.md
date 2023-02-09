# SAFE Template

This template can be used to generate a full-stack web application using the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications.

Current known build configuration

* dotnet: 6.0.403
* npm: 8.4.1
* node: v17.5.0

## Starting the application

This will start up the application locally for development.

```bash
dotnet tool restore
dotnet run
```

This will create a production ready docker image:

```bash
docker build -t [USERNAME]/genpres .
```

To run the docker image locally:

```
docker run -it -p 8080:8085 [USERNAME]/genpres
```


Open a browser to `http://localhost:8080` to view the site.

## SAFE Stack Documentation

If you want to know more about the full Azure Stack and all of its components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

You will find more documentation about the used F# components at the following places:

* [Saturn](https://saturnframework.org/)
* [Fable](https://fable.io/docs/)
* [Elmish](https://elmish.github.io/elmish/)
