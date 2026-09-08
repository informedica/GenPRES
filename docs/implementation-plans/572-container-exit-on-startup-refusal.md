# Implementation plan for issue #572

## Problem description

[#572](https://github.com/informedica/GenPRES/issues/572): when the server refuses to start (for
example `GENPRES_PROD=1` without a `GENPRES_PASSWORD`), the container logs the
`Unhandled exception. System.InvalidOperationException: GENPRES_PROD=1 but GENPRES_PASSWORD is
not set ...` message but never exits. `docker inspect` keeps reporting `running`, the `dotnet`
process sits in state `R`, and `docker compose up -d` or an orchestrator sees a healthy container
that serves nothing. Reproduced on `informedica/genpres:0.1.2-alpha.13` and on the #568 branches.

Cause: `ENTRYPOINT [ "dotnet", "Informedica.GenPRES.Server.dll" ]` makes the .NET runtime PID 1.
After an unhandled exception the runtime terminates itself with `abort()`, that is `SIGABRT` to
its own process. Linux ignores the default action of a signal for PID 1 unless a handler is
installed, so the signal is dropped and the runtime never dies. Running the same image with
Docker's init shim (`docker run --init ...`) makes it exit 134 after a few seconds, which confirms
the diagnosis.

A second, smaller cause: the start-up guards in `main` (`src/Informedica.GenPRES.Server/Server.fs`)
report a refused configuration by throwing `invalidOp`. Even with a working PID 1 that ends the
process through an abort with a stack trace and exit code 134, where a configuration error should
be a one-line message and a conventional non-zero exit code.

## Approaches considered

1. **`init: true` in `compose.yaml` and document `--init` for a bare `docker run`.** Smallest
   change, but it only helps the launch paths that pass the flag. The `publish-docker-image`
   smoke test, the Docker Desktop "Run" button and any orchestrator that does not add an init
   process keep the bug.
2. **Install `tini` in the runtime stage and make it the entry point:**
   `ENTRYPOINT ["tini", "--", "dotnet", "Informedica.GenPRES.Server.dll"]`. Fixes every way the
   image is started. `tini` reaps zombies and forwards signals, so `docker stop` still reaches
   Kestrel for a graceful shutdown, and an abort in the runtime now ends the container with
   exit code 134.
3. **Return a non-zero exit code from `main` for the start-up validation instead of throwing.**
   A cleaner exit for the configuration errors the server checks itself, with a readable message
   instead of a stack trace. On its own it does not cover a genuine crash elsewhere, so it belongs
   *in addition to* option 2, not instead of it.

## Chosen approach

Options 2 and 3 together, in one implementation PR. Option 1 is not applied: once `tini` is in the
image, `init: true` would add a second init process for no gain.

- **`Dockerfile`**: in the runtime stage, `apt-get install --no-install-recommends tini` (the
  base image `mcr.microsoft.com/dotnet/aspnet:10.0` is Debian) and
  `ENTRYPOINT [ "tini", "--", "dotnet", "Informedica.GenPRES.Server.dll" ]`, with a comment that
  names the issue.
- **`Server.fs`**: a pure `Config.validateStartup : Settings -> Result<string, string>` that runs
  the existing `validateProductionPassword` and then requires `GENPRES_URL_ID`, returning the URL
  ID on success. `main` matches on it: on `Error` it writes the message with `writeErrorMessage`
  and returns `1`; on `Ok` it builds the host as before and returns `0`. The two `invalidOp` calls
  go away. A crash anywhere else is deliberately not caught: it still surfaces as an unhandled
  exception and, thanks to `tini`, exits 134.

  This deviates from the [#419 plan](419-do-not-use-failwith.md), whose table maps "missing
  configuration or not initialized" to `invalidOp` with the note "startup guards stay fail-fast".
  The guards stay fail-fast, but through an exit code rather than an exception, because an
  exception is not a reliable exit path for a process that may run as PID 1.
- **Tests**: `validateStartup` gets unit tests in
  `tests/Informedica.GenPRES.Server.Tests/ConfigTests.fs` next to the existing
  `validateProductionPassword` tests.
- **Release workflow**: `.github/workflows/tag-release.yml` gets a second container check after
  the existing smoke test. It starts the built image with `GENPRES_PROD=1` and an empty
  `GENPRES_PASSWORD`, waits at most 30 seconds with `docker wait`, and fails the job when the
  container is still running or exited 0. The happy-path smoke test cannot see this bug, because
  a container that never exits looks the same as one that is still starting.
- **Docs**: a short paragraph in `DEVELOPMENT.md`'s Docker section explaining that the image
  runs `tini` as PID 1, that a refused start-up exits 1 and a crash exits 134, and that no
  `--init` is needed.

## Confidence

High. The issue already demonstrates that an init process as PID 1 makes the container exit, and
the exit-code change only touches the two guards that `main` already performs before any listener
binds.

## Steps

1. Add `tini` to the runtime stage of `Dockerfile` and switch the `ENTRYPOINT`.
2. Add `Config.validateStartup` to `Server.fs` and make `main` return `1` on `Error`.
3. Add `validateStartup` tests to `ConfigTests.fs`.
4. Add the refused-start-up check to `tag-release.yml` between the smoke test and the push.
5. Document the PID 1 behaviour and exit codes in `DEVELOPMENT.md`.
6. Verify locally. `dotnet run DockerBuild` only builds `informedica/genpres`, and `DockerRun`
   insists on both secrets, so the refusal cases need plain `docker run`:

   ```bash
   dotnet run DockerBuild

   # Refused: production without a password. Expect exit 1 within seconds, the
   # "GENPRES_PROD=1 but GENPRES_PASSWORD is not set" line once, and no stack trace.
   c=$(docker run -d -e GENPRES_PROD=1 -e GENPRES_PASSWORD= informedica/genpres)
   docker wait "$c"; docker logs "$c"; docker rm "$c"

   # Refused: no URL ID. Expect exit 1 and "No GENPRES_URL_ID (or value is empty)".
   c=$(docker run -d -e GENPRES_URL_ID= informedica/genpres)
   docker wait "$c"; docker logs "$c"; docker rm "$c"

   # Demo run: / answers 200, tini is PID 1 with dotnet as its child, and
   # `docker stop` returns promptly with exit 0 (SIGTERM was forwarded).
   c=$(docker run -d -p 8090:8085 informedica/genpres)
   until curl -sf -o /dev/null http://localhost:8090/; do sleep 2; done
   docker top "$c" -o pid,ppid,comm
   docker stop "$c"; docker inspect -f '{{.State.ExitCode}}' "$c"; docker rm "$c"
   ```

   On `master` the first `docker wait` never returns, which is the bug. Bound it with
   `timeout 30 docker wait "$c"` on Linux; macOS ships no `timeout`, so poll
   `docker inspect -f '{{.State.Status}}'` instead, or just watch `docker ps`.
