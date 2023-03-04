# The GenPRES project

A project to enable a generic prescription solution for Safe and Efficient medication prescriptions, preparation and administration. 

---

![](docs/pcm%20example.gif)

---

Some more background information can be found at:

- https://github.com/informedica/Informedica.GenPres.Lib/wiki/Informedica.GenOrder.Lib
- https://medicatieveiligensnel.nl (website in Dutch)



## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications.

Current known build configuration

* dotnet: 6.0.403
* npm: 8.4.1
* node: v17.5.0

For the full application to run a propreitary cache file is needed containing medication product information. Collaborators can request these cache files by contacting the owner of this repository. These cache files cannot be freely distributed!

A demo cache file will be made public, but this is work in progress.

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

**Note!**
Currently, the project is moving to using Vite and the current build system hasn't been updated yet. 

For the time being the application can be started by the following procedure:

1. `npm install` will perform a tool restore and install all dependencies
2. `dotnet watch --project src/Server/Server.fsproj` will start the server
3. `npm start` will start the fable compilation and the Vite development server

Open a browser to `http://localhost:8080` to view the site.

## SAFE Stack Documentation

This project is based on the SAFE Stack template. This template can be used to generate a full-stack web application using the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

If you want to know more about the full Azure Stack and all of its components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

You will find more documentation about the used F# components at the following places:

* [Saturn](https://saturnframework.org/)
* [Fable](https://fable.io/docs/)
* [Elmish](https://elmish.github.io/elmish/)
