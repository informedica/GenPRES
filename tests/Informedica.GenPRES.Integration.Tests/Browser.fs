/// The browser the tests drive, and the running GenPRES they drive it against.
module Informedica.GenPRES.Integration.Tests.Browser

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks

open Expecto
open PuppeteerSharp


/// The environment variable that names the GenPRES to test, when it is not the one dotnet run
/// starts.
[<Literal>]
let BaseUrlVariable = "E2E_BASE_URL"


/// The GenPRES dotnet run starts: the Vite dev server, in front of the demo server.
[<Literal>]
let DefaultBaseUrl = "http://localhost:5173"


/// The environment variable that, set to 1, shows the browser instead of running it headless.
[<Literal>]
let HeadfulVariable = "E2E_HEADFUL"


/// The header through which a proxy names the client it forwards a request for. The server
/// believes it only from a proxy on its trusted list, which is loopback unless
/// GENPRES_TRUSTED_PROXIES says otherwise, and so believes it from the Vite dev server.
[<Literal>]
let ForwardedForHeader = "X-Forwarded-For"


/// How long, in milliseconds, a step waits for the app before it fails. Generous, because the
/// first page load from a Vite dev server compiles modules on demand.
let timeout = 30_000


/// Where the GenPRES under test is, and whether the run named it.
[<RequireQualifiedAccess>]
type Target =
    /// The url the run named in E2E_BASE_URL.
    | Named of string
    /// The default url, since the run named none.
    | Default of string


/// Functions over the target.
[<RequireQualifiedAccess>]
module Target =

    /// The root url, without a trailing slash.
    let url target =
        match target with
        | Target.Named url
        | Target.Default url -> url


/// The GenPRES under test: the one E2E_BASE_URL names, else the one dotnet run starts.
let target () =
    match Environment.GetEnvironmentVariable BaseUrlVariable with
    | url when String.IsNullOrWhiteSpace url -> Target.Default DefaultBaseUrl
    | url -> Target.Named(url.Trim().TrimEnd('/'))


/// Whether a demo GenPRES answers at the url: its stub launch page, which a production server
/// does not serve.
let answers (url: string) =
    task {
        use client = new Net.Http.HttpClient(Timeout = TimeSpan.FromSeconds 5.0)

        try
            let! response = client.GetAsync(url + "/stub/launch")
            return response.IsSuccessStatusCode
        with _ ->
            return false
    }


/// Whether the GenPRES under test answers, asked once per run. Lazy, so that nothing is asked
/// until a test runs.
let available = lazy (answers (Target.url (target ())))


/// Whether the run asked to watch the browser.
let headful () = Environment.GetEnvironmentVariable HeadfulVariable = "1"


/// The Chrome for Testing build this PuppeteerSharp version is made for, downloaded on first
/// use beside the test assembly and reused from there: Chrome/, and ChromeHeadlessShell/, which
/// PuppeteerSharp always fetches with it, about 560 MB together. The tests run the full Chrome,
/// headless or not, as the closest match to the browser a user has. Lazy, so that a run that
/// skips the browser tests downloads nothing, and shared, so that tests running at the same
/// time download it once.
let executablePath =
    lazy
        (task {
            let! installed = BrowserFetcher().DownloadAsync()
            return installed.GetExecutablePath()
        })


/// Starts a browser of its own for one test.
let launch () =
    task {
        let! path = executablePath.Value
        let options = LaunchOptions(ExecutablePath = path, Headless = not (headful ()))
        return! Puppeteer.LaunchAsync options
    }


// the number of the next workstation; it starts at random, so that two runs close together do
// not share an address
let private workstationCount = ref (Random.Shared.Next(0, 65_536))


/// The address of one more workstation, from 198.18.0.0/15, the range set aside for
/// benchmarking that no real client has. The server's rate limiter counts the requests of every
/// client address apart, sixty in ten seconds, so a workstation of its own gives every browser
/// of a test, and every test of a run, the allowance of a user at a workstation of their own,
/// rather than one shared by every browser behind the Vite dev server.
let newAddress () =
    let n = Interlocked.Increment(&workstationCount.contents)
    $"198.18.%i{n / 256 % 256}.%i{n % 256}"


/// Saves what the page shows to screenshots/name.png beside the test assembly, and returns the
/// path. Best effort: a page that cannot be captured any more is no reason to hide the failure
/// that asked for the screenshot.
let screenshot (name: string) (page: IPage) =
    task {
        try
            let dir = Path.Combine(AppContext.BaseDirectory, "screenshots")
            Directory.CreateDirectory dir |> ignore
            let file = Path.Combine(dir, $"%s{name}.png")
            do! page.ScreenshotAsync(file, ScreenshotOptions(FullPage = true))
            return Some file
        with _ ->
            return None
    }


/// The client's own trail of its machine steps, one line per step: what each message did and the
/// state it reached. The client keeps it in a debug build with GENPRES_LOG set, against demo data,
/// as window.genpresTrail(); empty anywhere else. No line holds a patient's identity, a name, a
/// PIN or a token.
let trail (page: IPage) =
    task {
        try
            return!
                page.EvaluateExpressionAsync<string>
                    "typeof window.genpresTrail === 'function' ? window.genpresTrail() : ''"
        with _ ->
            return ""
    }


/// Saves the client's trail to screenshots/name.trail.txt beside the test assembly, and returns
/// the path with the last ten steps. Best effort, as the screenshot is; None when the client keeps
/// no trail.
let saveTrail (name: string) (page: IPage) =
    task {
        let! text = trail page

        if String.IsNullOrWhiteSpace text then
            return None
        else
            try
                let dir = Path.Combine(AppContext.BaseDirectory, "screenshots")
                Directory.CreateDirectory dir |> ignore
                let file = Path.Combine(dir, $"%s{name}.trail.txt")
                do! File.WriteAllTextAsync(file, text)
                let lines = text.Split('\n')
                return Some(file, lines |> Array.skip (max 0 (lines.Length - 10)))
            with _ ->
                return None
    }


/// Collects what went wrong in the page while a test ran: responses with an error status,
/// requests that got no answer, exceptions its scripts left uncaught, and errors on its console.
/// Read when the test fails.
let recordTrouble (page: IPage) =
    let trouble = ConcurrentQueue<string>()

    page.Response.Add(fun e ->
        let status = int e.Response.Status

        if status >= 400 then
            trouble.Enqueue $"HTTP %i{status} %O{e.Response.Request.Method} %s{e.Response.Url}"
    )

    page.RequestFailed.Add(fun e -> trouble.Enqueue $"no answer: %O{e.Request.Method} %s{e.Request.Url}")
    page.PageError.Add(fun e -> trouble.Enqueue $"uncaught: %s{e.Message}")

    page.Console.Add(fun e ->
        // the browser's own line for a failed resource repeats a response above, without its url
        if
            e.Message.Type = ConsoleType.Error
            && not (e.Message.Text.StartsWith "Failed to load resource")
        then
            trouble.Enqueue $"console: %s{e.Message.Text}"
    )

    trouble


// the requests to the server's api each page has sent and not yet seen answered
let private pendingApi =
    System.Runtime.CompilerServices.ConditionalWeakTable<IPage, ConcurrentDictionary<IRequest, unit>>()


/// Keeps count of the page's requests to the server's api that are under way, for
/// waitApiQuiet. Only the api's: the client keeps other requests open, so the page as a whole
/// is never quiet.
let trackApi (page: IPage) =
    let pending = ConcurrentDictionary<IRequest, unit>()
    pendingApi.AddOrUpdate(page, pending)

    let isApi (request: IRequest) = (Uri request.Url).AbsolutePath.StartsWith "/api/"

    page.Request.Add(fun e ->
        if isApi e.Request then
            pending[e.Request] <- ()
    )

    page.RequestFinished.Add(fun e -> pending.TryRemove e.Request |> ignore)
    page.RequestFailed.Add(fun e -> pending.TryRemove e.Request |> ignore)

    // a request the document before still had under way is never told finished once the next
    // one is in, as when a launch leaves for the identity hop: it is the old document's alone
    page.FrameNavigated.Add(fun e ->
        if obj.ReferenceEquals(e.Frame, page.MainFrame) then
            pending.Clear()
    )


/// Waits until the page has had no request to the server's api under way for half a second:
/// the client has done what an answer set off.
let waitApiQuiet (page: IPage) =
    task {
        let pending =
            match pendingApi.TryGetValue page with
            | true, pending -> pending
            | _ -> invalidOp "the page's api requests are not tracked: open it with openWorkstation"

        let deadline = DateTime.UtcNow.AddMilliseconds(float timeout)
        let mutable quietSince = DateTime.UtcNow

        while DateTime.UtcNow - quietSince < TimeSpan.FromMilliseconds 500.0 do
            if not pending.IsEmpty then
                quietSince <- DateTime.UtcNow

            if DateTime.UtcNow > deadline then
                let under =
                    pending.Keys
                    |> Seq.map (fun r -> $"%O{r.Method} %s{r.Url}")
                    |> String.concat ", "

                raise (
                    TimeoutException
                        $"the page's api requests did not settle within %i{timeout / 1000} seconds: %s{under}"
                )

            do! Task.Delay 50
    }


/// Answers the prompt a page raises before it unloads with unsigned work by leaving, as a user
/// who chose to does. GenPRES raises no other dialog, so any other is dismissed and recorded as
/// trouble.
let leaveWhenAsked (trouble: ConcurrentQueue<string>) (page: IPage) =
    page.Dialog.Add(fun e ->
        match e.Dialog.DialogType with
        | DialogType.BeforeUnload -> e.Dialog.Accept() |> ignore
        | other ->
            trouble.Enqueue $"dialog: %O{other} %s{e.Dialog.Message}"
            e.Dialog.Dismiss() |> ignore
    )


/// What a failure means when the server answered 429 along the way: its rate limiter, which
/// counts every request from one client address in a fixed window, refused some. Every
/// workstation sends its own address, which the server believes only through a proxy it
/// trusts; through any other, every browser shares one address.
let rateLimited =
    "the server's rate limiter answered 429 (Too Many Requests): the requests of this "
    + "workstation went over its window. When the server does not trust the proxy in between, "
    + "every browser shares one address: wait ten seconds and run again, and close other "
    + "browsers on the same server"


/// One workstation: a browser profile of its own, so that its cookies and its keys are its
/// alone, at a client address of its own.
type Workstation =
    {
        /// What the failure output and the screenshot call it.
        Name: string
        /// The page the test drives.
        Page: IPage
        /// What went wrong in the page, read when the test fails.
        Trouble: ConcurrentQueue<string>
    }


/// Sends every request of the page to the GenPRES at the base url from the address, and every
/// other request as the page made it. Only GenPRES's own: a header of its own on a request to
/// another origin, the Google sheets and fonts the client reads, would make the browser ask that
/// origin first, which refuses it, and the client would go without what it reads there.
let sendFrom (baseUrl: string) (address: string) (page: IPage) =
    task {
        let origin = Uri(baseUrl).GetLeftPart UriPartial.Authority

        let ours (url: string) =
            url = origin || url.StartsWith(origin + "/", StringComparison.OrdinalIgnoreCase)

        do! page.SetRequestInterceptionAsync true

        page.AddRequestInterceptor(fun request ->
            if request.IsInterceptResolutionHandled then
                Task.CompletedTask
            elif ours request.Url then
                let headers = Dictionary<string, string>(request.Headers)
                headers[ForwardedForHeader] <- address
                request.ContinueAsync(Payload(Headers = headers))
            else
                request.ContinueAsync()
        )
    }


/// Opens a workstation in the browser for the GenPRES at the base url: a fresh profile and a
/// page in it, the size of a workstation's screen, at an address of its own.
let openWorkstation (browser: IBrowser) (baseUrl: string) (name: string) =
    task {
        let! context = browser.CreateBrowserContextAsync()
        let! page = context.NewPageAsync()
        page.DefaultTimeout <- timeout
        page.DefaultNavigationTimeout <- timeout
        do! page.SetViewportAsync(ViewPortOptions(Width = 1400, Height = 900))
        do! page |> sendFrom baseUrl (newAddress ())
        trackApi page
        let trouble = recordTrouble page
        page |> leaveWhenAsked trouble

        return
            {
                Name = name
                Page = page
                Trouble = trouble
            }
    }


/// Writes out the url the workstation's page was on, what went wrong in it, and a screenshot
/// named after it.
let report (workstation: Workstation) =
    task {
        let name = workstation.Name
        eprintfn $"%s{name}: the page was at %s{workstation.Page.Url}"

        for line in workstation.Trouble do
            eprintfn $"%s{name}: %s{line}"

        if workstation.Trouble |> Seq.exists _.StartsWith("HTTP 429") then
            eprintfn $"%s{name}: %s{rateLimited}"

        match! screenshot name workstation.Page with
        | Some file -> eprintfn $"%s{name}: the page at the failure is in %s{file}"
        | None -> ()

        match! saveTrail name workstation.Page with
        | Some(file, last) ->
            eprintfn $"%s{name}: the client's trail is in %s{file}; its last steps:"

            for line in last do
                eprintfn $"%s{name}:   %s{line}"
        | None -> ()
    }


/// Runs a test body on fresh workstations, one per name, in a browser of its own, against the
/// GenPRES under test. When it does not answer, the test is skipped if it is the default one,
/// which is only not started yet, and failed if the run named it. The body gets the base url and
/// the pages, in the order of the names. When the body fails, every workstation is reported
/// before the failure is passed on; a test that skips itself is passed on as it is.
let withWorkstations (names: string list) (body: string -> IPage list -> Task<unit>) =
    task {
        let target = target ()
        let url = Target.url target
        let! up = available.Value

        match target with
        | Target.Default _ when not up ->
            skiptest $"no demo GenPRES answers at %s{url}: start it with dotnet run to run the browser tests"
        | Target.Named _ when not up -> failtest $"%s{BaseUrlVariable} names %s{url}, but no demo GenPRES answers there"
        | _ ->
            let! browser = launch ()

            try
                let workstations = ResizeArray<Workstation>()

                for name in names do
                    let! workstation = openWorkstation browser url name
                    workstations.Add workstation

                // caught as a value rather than rethrown from the handler, so that the reports
                // can be awaited before the failure leaves with its own stack trace
                let! failure =
                    task {
                        try
                            do! body url (workstations |> Seq.map _.Page |> List.ofSeq)
                            return None
                        with ex ->
                            return Some(ExceptionDispatchInfo.Capture ex)
                    }

                match failure with
                | None -> ()
                | Some failure when (failure.SourceException :? IgnoreException) -> failure.Throw()
                | Some failure ->
                    for workstation in workstations do
                        do! report workstation

                    failure.Throw()
            finally
                browser.CloseAsync().GetAwaiter().GetResult()
    }


/// Runs a test body on one fresh workstation; see withWorkstations.
let withPage (name: string) (body: string -> IPage -> Task<unit>) =
    withWorkstations
        [ name ]
        (fun url pages ->
            match pages with
            | [ page ] -> body url page
            | _ -> invalidOp "one workstation was asked for"
        )


/// Runs a test body on two fresh workstations, two Users at two screens, whose reports are
/// called name-a and name-b; see withWorkstations.
let withPages (name: string) (body: string -> IPage -> IPage -> Task<unit>) =
    withWorkstations
        [ $"%s{name}-a"; $"%s{name}-b" ]
        (fun url pages ->
            match pages with
            | [ a; b ] -> body url a b
            | _ -> invalidOp "two workstations were asked for"
        )


/// Whether the failure is a wait cut off by the page navigating away from under it.
let isNavigationLoss (ex: exn) =
    match ex with
    // a wait the page's next document ended says only that it failed; one that ran out of time
    // says how long it waited
    | :? WaitTaskTimeoutException when ex.Message.EndsWith "Waiting failed" -> true
    | _ ->
        [ "Execution context was destroyed"; "Cannot find context"; "detached" ]
        |> List.exists ex.Message.Contains


/// Runs a wait again when a navigation cut it off, until the timeout. A launch takes the
/// browser through the identity hop and back, three documents in all, and a wait begun on one
/// of them dies with it.
let acrossNavigations (wait: unit -> Task) =
    task {
        let deadline = DateTime.UtcNow.AddMilliseconds(float timeout)
        let mutable waiting = true

        while waiting do
            try
                do! wait ()
                waiting <- false
            with ex when isNavigationLoss ex && DateTime.UtcNow < deadline ->
                do! Task.Delay 100
    }


/// Waits until an element the selector matches is on screen.
let waitVisible (selector: string) (page: IPage) =
    acrossNavigations (fun () -> page.WaitForSelectorAsync(selector, WaitForSelectorOptions(Visible = true)) :> Task)


/// Waits until no element the selector matches is on screen, or in the page at all.
let waitGone (selector: string) (page: IPage) =
    acrossNavigations (fun () -> page.WaitForSelectorAsync(selector, WaitForSelectorOptions(Hidden = true)) :> Task)


/// The address the page shows, as the page itself reads it.
let address (page: IPage) = page.EvaluateExpressionAsync<string> "location.href"


/// Whether an element the selector matches is on screen now.
let isVisible (selector: string) (page: IPage) =
    page.EvaluateFunctionAsync<bool>(
        "s => [...document.querySelectorAll(s)].some(e => e.checkVisibility({ visibilityProperty: true }))",
        selector
    )


/// How many elements the selector matches now.
let count (selector: string) (page: IPage) =
    page.EvaluateFunctionAsync<int>("s => document.querySelectorAll(s).length", selector)


/// The text of the first element the selector matches, as shown; empty when there is none.
let textOf (selector: string) (page: IPage) =
    page.EvaluateFunctionAsync<string>(
        "s => { const e = document.querySelector(s); return e ? e.innerText : '' }",
        selector
    )


/// The script that tells whether a text holds every fragment as a whole word, so that the
/// version number 1 is not found in the time 01:30.
let holdsWordsScript =
    "(text, words) => words.every(w => new RegExp('(^|[^\\\\p{L}\\\\p{N}])' + "
    + "w.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\\\$&') + '($|[^\\\\p{L}\\\\p{N}])', 'u').test(text))"


/// Waits until the first element the selector matches shows a text that holds every word, and
/// returns that text. The words are the names and numbers a message carries in any language.
let waitWords (selector: string) (words: string list) (page: IPage) =
    task {
        let script =
            $"(s, words) => {{ const e = document.querySelector(s); return !!e && (%s{holdsWordsScript})(e.innerText, words) }}"

        do! acrossNavigations (fun () -> page.WaitForFunctionAsync(script, selector, words |> Array.ofList) :> Task)
        return! textOf selector page
    }


/// Waits until the text field the selector names shows the text, replacing what it held, by
/// the keyboard as a user types it. The keys go to the page in front, so the page is brought to
/// the front first, as a user turns to the screen they type on.
let fill (selector: string) (text: string) (page: IPage) =
    task {
        do! page.BringToFrontAsync()
        do! page.FocusAsync selector
        do! page.EvaluateFunctionAsync("s => document.querySelector(s).select()", selector)
        do! page.Keyboard.PressAsync "Backspace"
        do! page.TypeAsync(selector, text)
    }


/// Polls a check every tenth of a second until it holds, and fails naming what was waited for
/// when it does not hold within the timeout.
let waitUntil (what: string) (check: unit -> bool) =
    task {
        let deadline = DateTime.UtcNow.AddMilliseconds(float timeout)

        while not (check ()) && DateTime.UtcNow < deadline do
            do! Task.Delay 100

        if not (check ()) then
            raise (TimeoutException $"waited %i{timeout / 1000} seconds for %s{what}")
    }


/// The script that finds the menu item of the open menu. MUI keeps a closed menu in the page,
/// hidden, so the item a user can see is the one of the menu that is open.
let openMenuItemScript =
    "() => [...document.querySelectorAll('[role=\"menuitem\"]')]"
    + ".find(e => e.checkVisibility({ visibilityProperty: true }))"


/// Clicks the menu item the open menu shows, once it shows one. For a menu with a single item,
/// such as the session menu, where the item's text depends on the language.
let clickOpenMenuItem (page: IPage) =
    task {
        do! page.WaitForFunctionAsync openMenuItemScript :> Task
        do! page.EvaluateFunctionAsync $"() => (%s{openMenuItemScript})().click()"
    }
