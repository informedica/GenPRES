/// The stub MailService's outbox a demo server shows at /stub/mail, where a test reads the mail
/// a User would read in their mailbox. The mails are English only, whatever the user's language.
module Informedica.GenPRES.Integration.Tests.StubMail

open System
open System.Text.RegularExpressions
open System.Threading.Tasks

open PuppeteerSharp


/// One mail the server sent.
type Mail =
    {
        /// The subject line.
        Subject: string
        /// The address it went to.
        To: string
        /// The text.
        Body: string
    }


/// The subject of the mail with the confirmation code of an enrolment.
let confirmationCode = "GenPRES: your confirmation code"


/// The subject of the mail that tells a PIN was set.
let pinSet = "GenPRES: your PIN was set"


/// The subject of the mail that tells the third wrong PIN locked signing.
let signingLocked = "GenPRES: signing is locked"


/// Where the page is served, under the base url.
let path = "/stub/mail"


/// Every mail the server sent since it started, newest first. Fetched by the page beside what
/// it shows, so that the page stays where it is, as a mailbox in another window.
let read (baseUrl: string) (page: IPage) =
    task {
        let script =
            "async url => {"
            + " const html = await (await fetch(url)).text();"
            + " const doc = new DOMParser().parseFromString(html, 'text/html');"
            + " return [...doc.querySelectorAll('article')].map(a =>"
            + " [a.querySelector('h2').textContent, a.querySelector('code').textContent, a.querySelector('pre').textContent]);"
            + " }"

        let! mails = page.EvaluateFunctionAsync<string[][]>(script, baseUrl + path)

        return
            mails
            |> Array.map (fun mail ->
                {
                    Subject = mail[0]
                    To = mail[1]
                    Body = mail[2]
                }
            )
            |> List.ofArray
    }


/// The mails with the subject to the address, newest first.
let about (subject: string) (address: string) (mails: Mail list) =
    mails |> List.filter (fun m -> m.Subject = subject && m.To = address)


/// Waits until more mails with the subject went to the address than the count seen, and returns
/// the newest of them. The server sends a mail once the act that asked for it has landed, so a
/// test that saw the act answered may still have to wait for its mail.
let waitFor (baseUrl: string) (subject: string) (address: string) (seen: int) (page: IPage) =
    task {
        let deadline = DateTime.UtcNow.AddMilliseconds(float Browser.timeout)
        let! mails = read baseUrl page
        let mutable found = mails |> about subject address

        while found.Length <= seen && DateTime.UtcNow < deadline do
            do! Task.Delay 500
            let! mails = read baseUrl page
            found <- mails |> about subject address

        return
            match found with
            | newest :: _ when found.Length > seen -> newest
            | _ -> raise (TimeoutException $"no new mail '%s{subject}' to %s{address} arrived")
    }


/// The six-digit code a confirmation code mail carries.
let codeOf (mail: Mail) =
    match Regex.Match(mail.Body, @"\b(\d{6})\b") with
    | m when m.Success -> m.Groups[1].Value
    | _ -> invalidArg (nameof mail) $"the mail '%s{mail.Subject}' holds no six-digit code"
