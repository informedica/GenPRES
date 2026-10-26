/// What the server answered a browser, read off its network: where the launch and the identity
/// hop sent it, and the answers of the members a launch, a Session, a signature and a computing
/// request go to. The screen says these in the user's language; the wire says them in the case
/// names of the contract (src/Informedica.GenPRES.Shared), which do not change with it.
module Informedica.GenPRES.Integration.Tests.Wire

open System
open System.Collections.Concurrent
open System.Threading.Tasks

open PuppeteerSharp


/// One answer of a member of the server api.
type Answer =
    {
        /// The member: processSession, processSigning, ...
        Member: string
        /// The body as sent: the answer in the JSON of Fable.Remoting.
        Body: string
    }


/// One redirect the server answered.
type Redirect =
    {
        /// The url that answered with the redirect.
        From: string
        /// Where it sent the browser.
        To: string
    }


/// What a page was answered while the recording ran.
type Recording =
    {
        /// The answers of the members, in the order their bodies were read.
        Answers: ConcurrentQueue<Answer>
        /// The redirects, in the order they arrived.
        Redirects: ConcurrentQueue<Redirect>
    }


/// The members whose answers are kept: those that a launch, a Session, a signature or a
/// computing request goes to. The answers of the formulary, the parenteralia and the
/// interactions say nothing of a Session.
let members =
    set
        [
            "processLaunch"
            "processSession"
            "processSigning"
            "processOrderContext"
            "processOrderPlan"
        ]


/// Starts recording what the page is answered.
let record (page: IPage) =
    let recording =
        {
            Answers = ConcurrentQueue()
            Redirects = ConcurrentQueue()
        }

    page.Response.Add(fun e ->
        let response = e.Response
        let status = int response.Status

        if status >= 300 && status < 400 then
            match response.Headers.TryGetValue "location" with
            | true, location ->
                recording.Redirects.Enqueue
                    {
                        From = response.Url
                        To = location
                    }
            | _ -> ()
        else
            match (Uri response.Url).AbsolutePath.Split('/') with
            | [| ""; "api"; "IServerApi"; name |] when members.Contains name ->
                // read as it arrives: the body is gone once the page has navigated away
                response
                    .TextAsync()
                    .ContinueWith(fun (body: Task<string>) ->
                        if body.IsCompletedSuccessfully then
                            recording.Answers.Enqueue
                                {
                                    Member = name
                                    Body = body.Result
                                }
                    )
                |> ignore
            | _ -> ()
    )

    recording


/// A case of the contract as it appears in an answer: quoted, as a value (a case without
/// fields) and as a key (a case with them) alike.
let case (name: string) = $"\"%s{name}\""


/// The bodies a member answered so far.
let answersOf (memberName: string) (recording: Recording) =
    recording.Answers
    |> Seq.filter (fun a -> a.Member = memberName)
    |> Seq.map _.Body
    |> List.ofSeq


/// Whether a member answered so far with a body that holds every fragment.
let answered (memberName: string) (fragments: string list) (recording: Recording) =
    recording
    |> answersOf memberName
    |> List.exists (fun body -> fragments |> List.forall body.Contains)


/// Waits until a member answers with a body that holds every fragment, and returns the first
/// such body.
let waitForAnswer (memberName: string) (fragments: string list) (recording: Recording) =
    task {
        let what =
            let quoted = fragments |> String.concat " and "
            $"%s{memberName} to answer %s{quoted}"

        do! Browser.waitUntil what (fun () -> recording |> answered memberName fragments)

        return
            recording
            |> answersOf memberName
            |> List.find (fun body -> fragments |> List.forall body.Contains)
    }


/// The redirects from a path so far, oldest first.
let redirectsFrom (path: string) (recording: Recording) =
    recording.Redirects
    |> Seq.filter (fun r -> (Uri r.From).AbsolutePath = path)
    |> List.ofSeq


/// Waits until the path has answered more redirects than it had, and returns the newest.
let waitForRedirect (path: string) (seen: int) (recording: Recording) =
    task {
        do!
            Browser.waitUntil
                $"%s{path} to redirect the browser"
                (fun () -> (recording |> redirectsFrom path).Length > seen)

        return recording |> redirectsFrom path |> List.last
    }


/// The path the identity hop returns through: the server's callback, which opens the Session
/// or names the refusal in the url it redirects to.
let callbackPath = "/callback"


/// The refusal word a callback redirect names, from #/session?refused=word; None for the
/// redirect that opened a Session.
let refusalOf (redirect: Redirect) =
    match redirect.To.Split("refused=") with
    | [| _; word |] -> Some(word.Split('&')[0])
    | _ -> None
