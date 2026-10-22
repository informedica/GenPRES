# Implementation plan for issue #598: browser tests

## Problem description

[The order machines simplified](1224-the-order-machines-simplified.md) (#1224) moved the client's
rules into `Client.Core`, where 774 Expecto tests prove them. What no test reaches is the wiring
from those rules to the screen: whether a page is really disabled while a request is out, whether
the menu, the title bar and the patient panel wait, whether a url change, a Back press or a
signature does what the plan decided. Every step of that plan was therefore checked by hand in the
browser, with the trail, from the list in its section "Verification, per step". Several of those
checks are still open, and some are hard to bring about by hand at all, such as the Session ended
by the server while a submission is out.

[#598](https://github.com/informedica/GenPRES/issues/598) asks for client-side testing. Its last
comment narrows it to the browser-dependent half: React components, rendering, hooks and user
interaction in `src/Informedica.GenPRES.Client`. This plan proposes browser tests that run the
real client against the real server with its stubs, and that cover every browser check that
the plan for #1224 needed, and those of the plan after it, [the App as
wiring](1224-the-app-as-wiring.md), which moves the rest of the App's
decisions into `Client.Core` and lists its own checks for the loads, the lists, the error banner,
the admin and the url.

What exists today:

- **No browser test tooling.** The client's `package.json` has no test packages; `Fable.Mocha` is
  in `paket.references` but no file uses it; there is no `tests/Client`. The FAKE target
  `TestHeadless` is now only `dotnet test GenPRES.sln`.
- **A server that runs without live patient systems.** With `GENPRES_PROD=0` (the `.env.example`
  default) the server mounts the stub launch (`/stub/launch`), the stub mail outbox
  (`/stub/mail`), stub identities (`prescriber`, `no-pin`, `prescriber-other-patient`, ...), the
  stub PIN `1234`, and a fixed stub patient for every patient id but `no-data`. The medication data
  has a demo set (`data/cache/*.demo`) that is part of the repository.
- **The trail.** In a development build with `GENPRES_LOG` set, every machine step is one line,
  `#<n> <time> <machine> <message> -> <effects> | <state>`, and `window.genpresTrail()` returns the
  last 500 lines. The trail has no patient data, tokens or PINs.
- **A disabled page has a marker.** `Components/Disabled.fs` sets `inert` on the page's content box
  and shows a backdrop with a spinner, around `#page-box`.
- **Few stable selectors.** There is no `data-testid`. Some ids are stable (`page-box`,
  `sign-dialog-pin`, `session-gate-title`, the enrolment fields, `order-argumentation`); others
  are built from translated labels.
- **The manual scripts.** `docs/user-guide/testing-workflows.md` holds the manual workflows, and
  the #1224 plan holds the per-step browser list.

## Approaches considered

1. **Puppeteer for .NET (PuppeteerSharp), driven from an Expecto test project in F#.** One
   language and one test framework, as in every other test project. PuppeteerSharp starts a real
   Chrome and talks to it over the DevTools protocol, with no Node process in between. With request
   interception on (`page.SetRequestInterceptionAsync` and the `page.Request` event) it can hold,
   delay, fail or count any request to the server. It reads the page's trail through
   `page.EvaluateExpressionAsync` and handles Back, reloads, several browser contexts (two Sessions)
   and native dialogs. Puppeteer is also what has already been used to write browser tests
   alongside this work, and it was found sufficient.
2. **Playwright for .NET** in the same Expecto project. It can do everything the checks need, and
   it adds locators that wait by themselves and a route API that is easier to use than
   interception. It drives the browser through a bundled Node driver, though, and it would be a
   second browser tool next to the Puppeteer tests that already exist.
3. **Playwright Test or Puppeteer in TypeScript.** The same browsers, but a second language, a
   second test runner and npm test packages beside the F# ones, outside `dotnet test`.
4. **Fable.Mocha component tests** in a browser or a simulated DOM. They test one component at a
   time. They cannot check what the #1224 plan needed: the server's timing, the busy rules across
   pages, the url, Back and the Session.
5. **Canopy or Selenium.** F#, but without built-in request interception, which most checks need,
   and known to be flaky on timing.
6. **Keep the manual list.** No cost now; every later change to the client repeats the hand checks,
   and the hard cases stay unchecked.

## Chosen approach

Approach 1: **PuppeteerSharp in an Expecto project**, `tests/Informedica.GenPRES.Browser.Tests`,
run by its own FAKE target against the development server.

- **The real client and the real server.** The target starts the server (`GENPRES_PROD=0`,
  `GENPRES_LOG=d`, the demo data) and the Vite development server, waits until both answer, runs
  the tests and stops both. The trail exists only in the development build, so the tests run
  against Vite, not against a bundle.
- **Holding a request instead of waiting.** A check like "the page is disabled while the filter
  request is out" holds that one request at the browser's network layer, asserts what the page
  shows, releases the request and asserts again. Interception is on for every page. One handler
  per page lets every request through at once, except one the test has asked to hold for
  `/api/<type>/processOrderContext`. That request waits on a task the test completes, and then
  goes on with `ContinueAsync`. No test sleeps: where Playwright would wait by itself, a helper
  waits explicitly, with `WaitForSelectorAsync` or `WaitForFunctionAsync`. The same mechanism fails a request (a transport failure, or a
  server error answer), counts requests (a dose step fetches neither page; two page loads never
  run at once), and holds one request while the test does something else (a url change, a second
  pick, a superseding launch).
- **Two oracles.**
  - The page: the `inert` marker of the page box, whether the menu, the title bar and the panel
    are enabled, what a field or the snackbar shows.
  - The trail: `window.genpresTrail()` after each scenario. It shows which command went out,
    which answer landed and which message a machine left untouched, which the screen alone
    cannot tell.
- **Selectors.** Roles and existing stable ids first. Where a control has none, or only an id
  from a translated label, the client gets a `data-testid`, added in the step that first needs
  it. Every test opens the client in English (`lan=en`), so a label never changes under a test.
- **Two Sessions without a server change.** A second browser context launching the same identity
  supersedes the first Session, and the notice rides on the first context's next reply. That is
  "the Session ended by the server", made on demand. A data notice comes from editing the panel
  over the stub's reading before the sign, as the manual workflow does.
- **Not part of `ServerTests`.** The project builds with the solution, so formatting and the
  compiler check it on every run. Its tests run only from the new target, since they need a
  running server, Vite and an installed browser.
- **Fable.Mocha goes.** It is unused, and this plan does not use it. `TestHeadless` stays as it
  is: it no longer refers to a browser.

## Confidence

Medium. The interception approach and the oracles are sound; four things are unknown until
step 1 has run:

- **Where the rules come from in a test run.** The demo medication files are in the repository,
  but the rule sheets are read from the demo Google Sheet. Tests must not depend on a live sheet,
  since that makes CI flaky. Step 1 establishes whether the server can run from a local copy of
  the demo sheet, and adds one if it cannot.
- **Start-up time of Vite and the server** on a CI runner, which decides whether a CI job is
  practical.
- **How many selectors are missing**, which decides the size of the client changes per step.
- **The cost of interception under Vite.** With interception on, every request goes through the
  handler, including the many module requests of the development server, and the browser cache is
  bypassed. Step 1 measures the page load with interception on and off. If the difference matters,
  interception is switched on only around the requests a test holds or fails.

## Coverage

Every check from the #1224 plan's "Verification, per step", with the suite that covers it and how
the test brings it about. "Hold" means the named request is held at the network layer; "trail"
means the assertion reads the trail.

| #1224 step | Check | Suite | How |
| ---------- | ----- | ----- | --- |
| Every step | Each request disables its page until the answer; the menu, the title bar and the panel wait; no message dropped | Busy | Hold each request in turn; assert the page is inert and the menu, title bar and panel are disabled; release; assert all enabled; trail |
| Every step | The requests: a filter pick, a scenario choice, picks and clears, a reopen closed without a pick, a patient edit followed by the next field, a formulary and a parenteralia page change, an emergency and a continuous list item, a resource reload, a prescription, a removal, a row filter, the plan dialog, the nutrition slot, a signature, the patient refresh, the plan refresh, a url change during a request | Busy | One data-driven test per request kind |
| 2a | A filter answer that picks a choice itself shows it on the formulary page | Pages follow | Pick a medication with one route in the demo data |
| 2a | A patient change with a filter set: both pages end on the filter for the new patient | Pages follow | Panel edit; count and inspect the page loads |
| 2a | A dose step in the dialog fetches neither page | Pages follow | Count `processFormulary` and `processParenteralia` |
| 2a | A second filter pick while the first page load runs: the first lands, then one load of each page, never two at once | Pages follow | Hold the formulary load; pick again; count concurrent loads |
| 2b | A filter whose answer narrows route and form to one option each shows both picked, no further command | Pages follow | Trail: one command |
| 2b | A department list of one shows that department picked | Pages follow | Patient with one department |
| 3 | A prescription, a new nutrition context, a removal and a row filter each send one plan call over the plan held | Plan | Trail: one `CallPlan` each |
| 3 | A dialog command into a removed context sends nothing | Plan | Trail |
| 4a | A filter pick, then the menu at once: the menu waits for the filter and its two page loads | Busy | Hold the second page load |
| 4a | The held dialog's refresh, the sign click, the challenge and the submission disable every page | Busy, Signing | Hold each request |
| 4a | The newer-version notice's button waits with the title bar | Signing | Two contexts: the second signs a newer plan |
| 4a | A patient refresh on the parenteral nutrition page greys the slot and sends no intake move | Busy | Hold the refresh; trail |
| 4a | A formulary page load: the page and the menu wait, the panel too | Busy | Hold the load |
| 4a | The drug names failing: the Interactions page retries; the menu, title bar and panel stay usable | Busy | Fail `processInteraction` for the names, then let it through |
| 4b | Every page greys one way only; the reload's backdrop does not lift on a workbench answer | Busy | Assert the one marker; hold the reload |
| 4c | The gate stays until every start-up load and the resume have answered; the hospital menu has its options | Start-up | Hold each start-up request in turn |
| 4c | The products unreachable: the gate names them and says to reload | Start-up | Fail the products load |
| 13 | A url with a patient, and a resumed Session with saved orders: the gate stays until the plan has answered | Start-up | Hold the plan's open |
| 5 | The patient refresh after a weight change keeps the orders and recalculates | Refresh | Panel edit, then refresh |
| 5 | The plan refresh shows the last signed orders; new and changed ones gone | Refresh | Sign, change, refresh |
| 5 | Without a signed plan: no plan refresh, the held dialog offers remove alone | Refresh | Fresh launch |
| 5 | The patient refresh with a new order opens the held dialog | Refresh | Add, then refresh |
| 6 | A prescription empties the workbench and shows the plan page at once, greyed until the order lands | Plan | Hold `processOrderPlan` |
| 6 | A failed prescription leaves an empty workbench and tells the error | Plan | Fail `processOrderPlan` |
| 7 | Anonymous: a url with another medication during a filter request starts over; the first answer lands nowhere | Url | Hold the filter request; change the url; release; trail |
| 7 | Launched: the same url asks; yes shows the url's patient with no Session | Url | Launch; change the url; answer the question |
| 7 | A url with the page alone during a request is put back; after the answer Back changes the page alone | Url | Hold; change the url; `GoBack` |
| 7 | A reload on a url with a patient does not resume the Session | Url | Launch; reload with a patient url |
| 7 | A url with a patient while a launch is presented: the launch is ignored, the url's patient shows | Url | Hold `processLaunch`; change the url |
| 7 | A launch url in a tab with a Session and a new order: the question; yes shows the new launch over an empty plan | Url | Two launches in one context |
| 7 | Back with unsigned work asks, and no leaves the page as it was, in both modes | Url | `GoBack`; answer no |
| 7 | Back during a signature puts the url back | Url | Hold the submission; `GoBack` |
| 7 | Back to a patient url during a patient refresh shows that patient with no Session | Url | Hold the refresh; `GoBack` |
| 7 | Back after a signature shows the signed plan as the last signed order plan | Url | Sign; `GoBack` |
| 8 | A data notice accepted, the patient context not held: the plan stays until the signature, then the panel takes the new data and the plan recalculates | Signing | Panel edit over the stub reading; sign; accept; trail |
| 8 | The Session closed during a filter request: no patient on the workbench and the plan; the answer lands nowhere; the trail shows the reset | Signing | Hold the filter; close; release |
| 8 | The close item waits from the sign click to the answer | Signing | Hold the challenge, then the submission |
| 8 | The Session ended by the server while a submission is out: the sign dialog stays, the outcome is told, then the gate | Signing | Hold the submission; a second context supersedes; a reply carries the notice; release |
| 9a | A field counting step clicks holds every page but Nutrition and the menu; the spinner shows over the plan table during a count | Dialog | Click a step button; assert before the debounce fires |
| 9a | The Nutrition page's controls wait while a field counts | Dialog | Same, on the Nutrition page |
| 9b | A step on a plan cell: every other control, the menu and the title bar wait until it lands | Dialog | Hold the plan call |
| 9b | Ok closes the dialog in one click and the argumentation is in the order, from the Prescribe page and from the OrderPlan page | Dialog | Type, Ok; trail shows the text sent, then the close |
| 9b | Escape and a backdrop click while a dose step is out leave the dialog open until the answer | Dialog | Hold the step's call |
| 10, 11 | The workbench and the plan behave as before | Busy, Plan | Covered by the suites above |

### The App as wiring

Every check from the "Verification, per step" of [the App as wiring](1224-the-app-as-wiring.md),
the plan after the order machines. Its first list repeats the order machines' list for every code
step; those rows are above. The rows here are the ones it adds. A trail assertion names the
messages as they are after its step 7a: `SetPatientData` and `PatientDataChanged`.

| App step | Check | Suite | How |
| -------- | ----- | ----- | --- |
| Every step | A page load with and without a url patient: the gate goes once every start-up load has answered | Start-up | Open `#/patient?...` and `#/`; assert the gate goes; trail |
| Every step | The emergency list and continuous medication items: with a patient the workbench is seeded and the Prescribe page shows; without a patient the item is dropped and the snackbar says so | Lists | Choose an item on each list, with and without a url patient; trail shows the seed or nothing |
| Every step | The server stopped and started again: the error banner shows, the server check asks again, and the next answer clears it | Banner | Fail `testApi`, then let it through; assert the banner, then its absence |
| Every step | A failed call of a source raises the error banner and the snackbar; the next success of that source clears the banner, a success of another source does not; the user can dismiss it | Banner | Fail `processFormulary`, then succeed `processParenteralia` and `processFormulary` in turn |
| Every step | The admin login, a wrong password, the log listing | Admin | Log in with `GENPRES_PASSWORD`; once with a wrong one; list the log files |
| Every step | An expired token logs out and leaves the settings page | Admin | Answer `processAdmin` with "Invalid token"; assert the settings page is gone and the login shows |
| Every step | The url question answered yes and no | Url | Launch; change to a patient url; answer each way |
| Every step | The launch url is erased from the history | Url | Launch; assert the address reads `#/session`; Back does not return to the launch url |
| Every step, 8a | A language picked in the title bar changes the labels on the page | Language | Open with `lan=en`; pick Nederlands; assert a known label in Dutch |
| Every step, 8a | A language change shows the disclaimer again | Language | Open with `dsc=n`; pick another language; assert the disclaimer shows; accept it |
| Every step, 8a | The pick holds: a url change without `lan=` keeps it, and the server default never replaces it; a later url with `lan=` sets that language, the last choice winning | Language | Pick a language; change the url without `lan=`; assert the pick; change the url with another `lan=`; assert that one |
| 1a | A patient url with every parameter gives the patient, page, language and medication it names; a launch url and a refused url give the launch and the refusal | Url | Open each; read the panel, the page, the language and the Prescribe filter; trail |
| 1b | A url with the three-letter keys opens the patient, page, language and medication it names | Url | Open `#/patient?byr=...&wgt=...&pag=pr&lan=en&med=...`; read the panel, the page, the language and the Prescribe filter |
| 1b | Each page code opens its page: `el`, `cm`, `pr`, `fm`, `pe`, and the new `nu`, `op` and `ia`; there is no code for Settings | Url | One test per code; assert the page shown |
| 1b | A url with the two-letter keys opens no patient, no page and no medication; the log names the parameters, not their values | Url | Open `#/patient?ad=10&wt=3500&pg=pr`; assert the panel is empty and the default page shows |
| 1b | Each ISO language code sets its language; `du`, `gr`, `sp` and a display name set none | Url | One test per code with `lan=`; assert the language the title bar shows |
| 2 | Each start-up load is asked once; the hospital menu has the hospitals of the bolus medication | Start-up | Count the start-up requests; open the hospital menu |
| 2 | A failed start-up load raises no error banner and no snackbar; the gate names it | Start-up | Fail the normal values load; assert no banner, no snackbar, the gate's text |
| 2 | The trail shows a `Loader` line for each start-up load once the settings have landed, the settings landing included | Trail | Open `#/`; read the trail from the first line |
| 3 | The server down: the error banner with the server sentence and no snackbar; the check asks again after five seconds; the next answer clears the banner | Banner | Fail `testApi` twice, then let it through; count the checks; trail |
| 3 | The drug names failing once or twice: asked again after three seconds, loaded, no snackbar | Busy | Fail the names twice, then let them through; open the Interactions page |
| 3 | The drug names failing three times: no fourth request; the snackbar warns "Interactie medicatie namen konden niet worden geladen" | Busy | Fail the names three times; count the requests; read the snackbar |
| 3 | The drug names loaded stay shown while they are asked again | Busy | Hold a later names request; assert the Interactions page keeps its list |
| 4 | Two filter picks during a held formulary load: when it lands, the page is asked again once, with the second pick | Pages follow | Hold `processFormulary`; pick twice; release; count the loads and read the generic sent |
| 4 | A patient edit after a filter pick during a held page load: both pages land on the new patient, without the earlier choices | Pages follow | Hold both page loads; pick; edit the weight; release; trail shows no load with the earlier generic |
| 4 | A patient set reloads both pages, the formulary for that patient, and clears the two list filters | Pages follow | Set a list filter; edit the patient; count the page loads; assert the filters empty |
| 4 | A formulary page change puts its generic, route and form on the parenteralia page and seeds the workbench with all five choices | Pages follow | Pick on the Formulary page; read the Parenteralia page and the Prescribe filter; trail `SeedWorkbench` |
| 4 | A parenteralia page change clears the formulary's indication and dose type and seeds the workbench without them | Pages follow | Pick on the Parenteralia page; read the Formulary page and the Prescribe filter; trail |
| 4 | A resource reload with a patient seeds the workbench again; without a patient it reloads both pages | Pages follow | Reload resources from Settings, with and without a url patient; trail |
| 5 | Two interacting drugs on the Interactions page, and a plan with interacting orders: the snackbar warns "Er zijn n interactie(s) gevonden" with the count | Interactions | Pick two drugs; prescribe two interacting orders; read the snackbar |
| 5 | A drug removed until fewer than two are left, or a check with no interactions: the notice is withdrawn and the rows cleared | Interactions | Remove a drug; assert the snackbar closed and no rows; trail `WithdrawInteractionsFound` |
| 5 | Another message shown after the notice is left alone by a later withdrawal | Interactions | Show the notice; raise another snackbar message; remove a drug; assert the other message stays |
| 5 | Two checks out: only the answer to the later check is shown | Interactions | Hold the first `processInteraction`; check again; release both; trail shows the earlier answer dropped |
| 5 | The rows shown stay while the drugs are checked again | Interactions | Hold a second check; assert the rows stay |
| 5 | A failed check raises the error banner under the interactions; the next good answer clears it | Banner | Fail `processInteraction`, then let it through |
| 6 | An analysis of a log file shows its report; the Settings page waits while it runs | Admin | Log in; list the log files; analyze one; assert the report and the page inert until it lands |
| 6 | A resource reload ends its busy state and seeds the workbench again, or reloads both pages without a patient | Admin | Log in; reload, with and without a url patient; trail `ReloadDone` and the loader's `ResourcesReloaded` |
| 7a | A patient edit's trail reads `SetPatientData` and `PatientDataChanged`, and nothing else in it changes | Trail | Panel edit; compare the trail lines with the names |
| 7b | A close with the server unreachable: the snackbar says the session could not be closed, the session stays open | Signing | Launch; fail `processSession` for the close; assert the snackbar and the open session |
| 7b | A PIN that does not reach the server: the snackbar says it could not be sent, the form comes back as it was | Signing | Enrol; fail `processSession` for the PIN; assert the snackbar and the form |
| 7b | A failed plan command raises the error banner; the next answer the plan waited for clears it, a late answer does not | Banner | Fail `processOrderPlan`, then let one through; hold a second and send a third, release the second late |
| 7c | A signature leaves the workbench and the plan as they were: no second evaluation; a weight edit still recalculates; a weight cleared before the signature is still cleared after it | Signing | Launch; sign; count `processOrderContext` and `processOrderPlan` after the signature; edit and clear the weight |
| 8a | Each menu page shows when chosen; the Settings page shows only after a login, and a logout from it leaves it | Shell | Choose every page from the menu; choose Settings without a login; log in; log out; assert the page shown each time |
| 8a | A page chosen reaches its loads: the Formulary and Parenteralia pages load once when first shown | Shell | Open `#/`; choose each page; count `processFormulary` and `processParenteralia`; trail `PageShown` |
| 8a | A hospital picked in the title bar shows the disclaimer again and the bolus lists of that hospital | Shell | Open with `dsc=n`; pick another hospital; assert the disclaimer; accept it; open the emergency list |
| 8a | An alert on screen follows a language switch | Shell | Raise the interactions notice; pick another language; assert the snackbar text in that language |
| 8a | The signing alerts read as before: plan signed, signature refused, signed plan opened, newer signed plan | Signing | Launch; sign; refuse once; open a signed plan; let a newer one arrive; read each snackbar |
| 8b | A launch url at a page load is presented once: the router's first report reads `#/session` and changes nothing | Url | Open a launch url; count `processSession` presents; trail shows one `PresentLaunch` and no `StartOver` |
| 8b | A page-only url while a request is out is put back; once nothing is out it is applied | Url | Hold `processFormulary`; change `pag=`; assert the address bar put back; release; change it again; assert the page |
| 8b | A launch url during a signature is erased and put back, and the snackbar says to open the patient again from the EHR | Url | Sign; hold `processSigning`; open a launch url; assert the address, the snackbar and the session kept |
| 8b | A refused launch from the identity provider shows the refusal on the gate | Url | Open `#/session?refused=no-role`; assert the gate's text and the address `#/session` |
| 8b | A url with a medication and no patient: the medication is dropped and the snackbar says so; with a patient the Prescribe filter is seeded | Url | Open `#/patient?med=paracetamol` and `#/patient?agd=10&med=paracetamol`; read the snackbar and the Prescribe filter |
| 8b | A start-over on a patient url shows no frame with the old patient: the new patient, an empty workbench and plan come in one render | Url | With a patient and an order, change to another patient url; record the frames; assert none shows the old patient beside the new url |
| 8b | A page load's trail: the shell's `PageLoaded` with `ResumeSession` or `LeaveSession`, then `PatientFromUrl` | Trail | Open `#/` and a patient url; read the trail from the first line |
| 9b | A page load's trail shows every step in one sequence, the loads included | Trail | Open the client; read the trail from the first line |
| 9b | With the patient held, a session open with a changed order plan, the patient panel takes no edit and the trail shows no patient message; a change that still arrives shows as `Client PanelChanged ... dropped` | Signing | Launch; change an order; try a panel edit; assert disabled; trail |
| 9b | A Session ended by a notice on a page landing: the gate shows the end, and the next page request goes out without the ended Session's token | Signing | Launch; a second context supersedes; let a formulary load land with the notice; assert the gate; read the next request's token |
| 9b | An emergency list item the list does not hold shows in the trail as dropped, and the console logs no warning | Lists | Choose an item after a hospital change removed it; trail; console |
| 10 | The disclaimer shows for anonymous use, and once accepted not again on a page change; a launched, resuming or refused Session never shows it | Shell | Open `#/` with `dsc=y`; accept; change the page; launch; reload with the Session; open `#/session?refused=no-role` |
| 10 | The patient panel shows the estimated weight and height from the normal values once they have loaded; before that it shows the draft as entered | Patient | Hold the normal values load; enter an age; assert no estimate; release; assert the estimates |

## Steps

Every step is one pull request. Tests, scripts and docs do not count toward the 200-line limit;
the client and server changes in each step are small. Each step runs the new target locally and
lists its result in the pull request.

0. **This plan.**
1. **The harness.**
   - Rules: establish where the rules come from in a test run (see Confidence) and add a local
     copy of the demo sheet if the server cannot run without the live one.
   - Project: `tests/Informedica.GenPRES.Browser.Tests` (Expecto, `PuppeteerSharp`), in the
     solution and in the dependency rule's ring map, with `scripts/ProjectGraph.fsx --write`.
   - Target: `dotnet run BrowserTests`. It starts the server and Vite with the test settings and
     waits until both answer. It downloads Chrome with PuppeteerSharp's `BrowserFetcher` when it is
     missing (Chrome only), runs the tests, and stops both whatever the outcome. `ServerTests`
     filters the project out.
   - Helpers:
     - open the client at a url, in English, and wait for the start-up gate to go;
     - read the trail;
     - whether the page, the menu, the title bar and the panel are disabled;
     - hold, release, fail and count a request by its api method;
     - launch through the stub form in a browser context.
   - One smoke test: a page load ends with the gate gone and trail lines present.
2. **Test hooks in the client.** `data-testid` on the controls that have no stable id: the menu
   button, the title bar, the patient panel and its fields, the Prescribe filter fields, the plan
   table and its cells, the order dialog's Ok, the sign button and the close item. Client UI code
   only; no behaviour changes. Later steps add a hook where they find one missing.
3. **Start-up and the busy wiring, workbench pages.** The start-up suite (4c, 13); the busy suite
   for the Prescribe, emergency, continuous, formulary and parenteralia pages and the patient
   edit.
4. **The busy wiring, plan pages.** The OrderPlan, Nutrition and Interactions pages, the resource
   reload (with `GENPRES_PASSWORD` set for the test run), the drug names failing.
5. **Pages follow the answer, and the plan commands.** Steps 2a, 2b, 3 and 6.
6. **Signing and the Session.** Step 8 and the signing half of 4a, with the launch helper and two
   browser contexts.
7. **The url.** Step 7, every row above.
8. **Refreshes, the dialog and counting.** Steps 5, 9a and 9b, including the 9a spinner check
   still open.
9. **The App as wiring.** The rows of its table not covered by steps 3 to 8: the lists suite, the
   language suite, the error banner suite and the admin suite (with `GENPRES_PASSWORD` set), the
   trail suite, and the url, start-up and signing rows of that plan in their suites. A row of a step of that plan
   that has not landed yet is added with the step's pull request instead, so each lands with the
   change it guards.
10. **Clean-up and documentation.**
    - `Fable.Mocha` leaves `paket.references` and `paket.dependencies`.
    - `DEVELOPMENT.md`: how to run the target and add a test.
    - `docs/user-guide/testing-workflows.md`: which manual workflows are now automated.
    - The two #1224 plans' "Verification, per step" point to the suites.
11. **A CI job, after agreement.** A separate job, on Ubuntu only, Chrome only, running
    `dotnet run BrowserTests` and uploading the trail and a screenshot of a failing test. Changes
    to the workflows are agreed first, and the job follows the general approach #598 settles.
    Until then the target runs locally, before a pull request that touches the client.

## Verification, per step

- **Every step:** `dotnet run BrowserTests` passes locally three times in a row with no retry, so a
  test that depends on timing shows before it lands. `dotnet run servertests`, Fantomas and
  `scripts/CheckDependencyRule.fsx` pass.
- **A test proves itself once.** Each new test is seen failing against the code it guards: the
  author breaks the wiring locally (for example, `isDisabled = false` in `Disabled.fs`) and sees
  the test fail, then restores it. The pull request says which break was tried.
- **Steps 3 to 9:** each row of the coverage tables that the step covers has a test, named in the
  pull request; a row the step cannot cover says why.

## Out of scope

- Visual comparison of screenshots, accessibility audits and tests on several browsers.
- Tests of the production bundle: the trail exists only in the development build.
- Unit tests of single React components; the client's rules stay tested in `Client.Core`.
- Live patient systems and the production medication data.
- New issues: the work hangs under #598.
