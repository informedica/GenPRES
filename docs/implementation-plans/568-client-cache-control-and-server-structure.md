# Implementation plan for issue #568

## Problem description

[#568](https://github.com/informedica/GenPRES/issues/568): after a Docker container update the
browser keeps showing the previous client; the same URL in an incognito window shows the fix.

`index.html` is served by Saturn `use_static "public"` (`src/Informedica.GenPRES.Server/Server.fs`)
through ASP.NET `StaticFileMiddleware`. The response carries `ETag` and `Last-Modified` but no
`Cache-Control`; nothing under `src/` sets one. Without `Cache-Control` a browser applies heuristic
freshness (RFC 9111 §4.2.2, typically 10 % of `now − Last-Modified`), so the older the built
`index.html` is, the longer the browser reuses its copy without asking the server. That copy points
at the previous content-hashed bundle (`assets/index-<oldhash>.js`), so the old client keeps loading.

Everything else in the chain is already right: Vite emits content-hashed asset names by default
(`vite.config.js` has no `rollupOptions.output` override), `vite build --emptyOutDir` and the fresh
Docker build stage leave no stale files in the image, there is no service worker, and `compose.yaml`
maps the port straight to Kestrel. Only the entry document lacks a caching policy.

The same file, `Server.fs` (419 lines), also mixes environment loading, a banner print and the
production password check as top-level side effects with configuration values, HTTP handlers,
middleware, a hosted service and the Saturn application builder. `//module Server` is commented out
and there is no `[<EntryPoint>]`. Since the fix touches its middleware, the issue also asks for the
file to be sectioned.

## Approaches considered

1. **Set `StaticFileOptions.OnPrepareResponse`.** The standard ASP.NET hook for static-file headers.
   Rejected: Saturn's `use_static` constructs the `StaticFileOptions` itself and exposes no way to
   set the callback, so this would mean replacing `use_static` with a hand-rolled
   `UseDefaultFiles().UseStaticFiles(...)` in `app_config`; and it would cover static files only,
   leaving API responses without a policy.
2. **Fingerprint `index.html` as well.** Rejected: the entry document must keep a fixed URL; that is
   the whole reason it needs a revalidation policy rather than a hash.
3. **`Cache-Control: no-store` on `index.html`.** Rejected: `no-store` forbids keeping any copy, so
   every load is a full transfer. `no-cache` lets the browser keep a copy but forces revalidation;
   the `ETag` that `StaticFileMiddleware` already emits then answers with a cheap 304 when nothing
   changed and a full fetch after a deploy.
4. **Send `Cache-Control` from the existing `securityHeadersMiddleware`.** It already hooks
   `Response.OnStarting` on every response, static files and Fable.Remoting routes alike, and is the
   one place that owns response-header policy. A pure `path -> Cache-Control` function keeps the
   policy unit-testable. Chosen.

For the file structure, the alternatives were splitting `Server.fs` into `Config.fs`, `Http.fs` and
`Server.fs`, or sectioning the single file into nested modules. Sectioning was chosen: same
readability gain, no `.fsproj` change, and the whole entry point stays on one screen of `git log -p`.

## Chosen approach

Option 4 for the fix, in two pull requests so the user-visible fix ships first:

- **PR 1, the fix.** A public pure function `cacheControlFor (path: string) : string` returning
  `public, max-age=31536000, immutable` for `/assets/*` (Vite's content-hashed output, matched
  case-insensitively) and `no-cache` for everything else (`/`, `index.html`, icons, `/api/*`).
  `securityHeadersMiddleware` sets `Cache-Control` from it. Unit tests for the function in a new
  `HttpTests.fs` in `tests/Informedica.GenPRES.Server.Tests`. Changelog entry via the commit body.
- **PR 2, the structure.** `Server.fs` becomes `module Server` with nested modules `Config` (pure
  environment parsing: a `Settings` record, `fromEnv` over an injected `string -> string option`
  getter, `parseTrustedProxies`, `validateProductionPassword` returning `Result`, `redactUrlId`,
  `displayPassword`, `banner`), `Http` (`getClientIP`, `cacheControlFor`, `securityHeadersMiddleware`,
  `addRateLimiting`, `logClientIP`, `safeWebApi` taking the inner handler as a parameter,
  `LoggerShutdown`) and `Host` (`build settings provider` wrapping the Saturn `application` block),
  plus `[<EntryPoint>] main` that loads `.env`, builds settings, validates, prints the banner,
  constructs the resource provider and runs the host. Every side effect moves into `main`; no
  top-level `let` value performs IO any more (see AGENTS.md, "Never Perform IO in a Top-Level
  `let` Value"). The security-review tag comments (B2, B3, A2, L1, L2/B5) stay with the code they
  annotate. Pure functions get unit tests in `ConfigTests.fs`. No behaviour change.

Deployment caveats, recorded in `DEVELOPMENT.md` and in the PR 1 description:

- A browser that already holds a heuristically fresh `index.html` needs one hard reload after this
  ships. From then on a plain reload picks up new deploys.
- On genpres.nl the Plesk nginx front serves `index.html` straight from disk when "Serve static
  files directly by nginx" includes `html` (see `docs/security/2026-04-10-security-review.md`,
  investigation step 1 and 3). In that mode Kestrel's header never reaches the browser. Either take
  `html`/`htm` out of that list, or add to the per-site nginx directives:

  ```nginx
  location = / { add_header Cache-Control "no-cache"; }
  location = /index.html { add_header Cache-Control "no-cache"; }
  ```

  Verify with `curl -sI https://genpres.nl/ | grep -i cache-control`.

## Confidence

High for the fix: the cause is reproduced by the response headers alone, the header policy is the
textbook one for a hashed-assets SPA, and the middleware already runs on every response.

Medium for the restructure: it is a pure move, but it turns implicit top-level execution into an
explicit `[<EntryPoint>]`, which changes when the banner, `.env` loading and the password check run
relative to each other. Verification step 5 covers that.

## Steps

1. This document (`docs/568-cache-control-plan`).
2. PR 1 (`fix/568-cache-control`):
   1. Add `cacheControlFor` above `securityHeadersMiddleware` in `Server.fs`; set
      `h["Cache-Control"]` inside its `OnStarting` callback; extend the comment block above the
      middleware to say it owns caching headers too and why.
   2. Add `tests/Informedica.GenPRES.Server.Tests/HttpTests.fs` (registered before `Program.fs`)
      with data-driven cases: `/`, `/index.html`, `/genpres.png`,
      `/api/IServerApi/processCommand` → `no-cache`; `/assets/index-abc123.js`,
      `/assets/index-abc123.css`, `/ASSETS/x.js` → `public, max-age=31536000, immutable`.
   3. Add a "Browser caching" paragraph to `DEVELOPMENT.md` (Docker section) with the header policy
      and the Plesk caveat.
   4. Commit `fix(server): send Cache-Control so a deploy replaces the cached client` with a
      `=== changelog ===` body block, `Refs #568`.
3. PR 2 (`refactor/568-server-structure`, stacked on PR 1):
   1. Restructure `Server.fs` as described under *Chosen approach*; run `dotnet fantomas` on it.
   2. Add `ConfigTests.fs`: `parseTrustedProxies` (`None` → loopback pair; `"10.0.0.5, bad, 10.0.0.6"`
      → two addresses; whitespace only → loopback pair), `validateProductionPassword` (prod + `None`
      → `Error` mentioning "not set"; prod + 15 characters → `Error` mentioning 16; prod + 16
      characters → `Ok`; demo + `None` → `Ok`), `redactUrlId` (`None` → `NOT SET`; 5 characters or
      fewer → `***<redacted>`; longer → `***` + last 5), `fromEnv` over a `Map`-backed getter.
   3. Commit `refactor(server): section Server.fs into Config, Http and Host with an entry point`,
      `Fixes #568`. The diff will exceed the 200-line guideline because it is a move; the PR
      description says so and offers to split the `Config` extraction out if asked.
4. Verification, for each PR:
   1. `dotnet run build`, `dotnet run servertests`, `dotnet fantomas --check .`.
   2. `dotnet fsi scripts/CheckDependencyRule.fsx` (the server is in the outer ring, so the
      `"GENPRES_` literals stay allowed).
   3. `dotnet run DockerBuild`, then `dotnet run DockerRun` with `.env` sourced, and:

      ```bash
      curl -sI http://localhost:8080/ | grep -i cache-control            # no-cache
      curl -sI "http://localhost:8080/assets/$(ls deploy/public/assets | grep '\.js$' | head -1)" \
          | grep -i cache-control                                         # immutable
      curl -sI http://localhost:8080/ | grep -i x-frame-options           # security headers intact
      ```

   4. End to end: open the app, make a visible client change, rebuild the image, restart the
      container, and confirm a plain reload shows the change.
   5. After PR 2 only: the demo image still starts with no variables set, the banner still prints
      redacted values, and `GENPRES_PROD=1` without a password still refuses to start with the same
      message as before.
