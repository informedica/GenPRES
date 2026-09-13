// The computing envelope, generic (plan 654, step 5): `Request<'cmd>` and `Reply<'resp>` in
// place of the `Request`/`Reply` records over `Command`/`Response`, so that every computing
// member can carry its own command and answer inside the same envelope. Abbreviations keep the
// client compiling unchanged until the families move.
//
// Script-first draft of what goes to `Shared/Api.fs`. The one risk is the wire: no generic
// record crosses it yet. Both sides serialize JSON, the server through Fable.Remoting.Json, so
// the round trip below runs `Request<Formulary>` and `Result<Reply<Formulary>, string[]>`
// through that converter, both ways.
//
// Run: `dotnet fsi Api.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Fable.Remoting.Json, 3.0"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"
#load "../Api.fs"

open Shared.Types


/// → `Shared/Api.fs`, in place of `Request` and `Reply`.
module Api654 =

    open Shared.Api

    /// Every computing request: the command and the OpenedToken the Session holds. `None`
    /// where there is none to send: no Session, an anonymous one, or a client acting before
    /// its first token arrived.
    type Request<'cmd> =
        {
            Opened: OpenedToken option
            Command: 'cmd
        }


    /// Every computing reply: the answer, and what the Session is told with it (the record
    /// moved on, or the Session ended).
    type Reply<'resp> =
        {
            Response: 'resp
            Notice: RecordNotice option
        }


    // until the families move, processCommand keeps its shape under these names:
    type Request = Request<Command>
    type Reply = Reply<Response>


open Expecto
open Expecto.Flip
open Newtonsoft.Json
open Fable.Remoting.Json
open Api654


let converters = [| FableJsonConverter() :> JsonConverter |]
let toJson (v: 'a) = JsonConvert.SerializeObject(v, converters)
let ofJson<'a> (json: string) = JsonConvert.DeserializeObject<'a>(json, converters)


let tests =
    testList
        "the generic envelope on the wire"
        [
            test "Request<Formulary> round-trips and reads as today's Request" {
                let request: Request<Formulary> =
                    {
                        Opened = Some(OpenedToken "opened-1")
                        Command = { Shared.Models.Formulary.empty with Generics = [| "paracetamol" |] }
                    }

                let json = toJson request
                (json.Contains "\"Opened\"" && json.Contains "\"Command\"") |> Expect.isTrue "the same two fields"
                ofJson<Request<Formulary>> json |> Expect.equal "the same request back" request
            }

            test "Result<Reply<Formulary>, string[]> round-trips with and without a notice" {
                let reply: Result<Reply<Formulary>, string[]> =
                    Ok
                        {
                            Response = { Shared.Models.Formulary.empty with Generics = [| "paracetamol" |] }
                            Notice = Some(RecordNotice.Ended SessionEnding.SupersededByLaunch)
                        }

                reply |> toJson |> ofJson<Result<Reply<Formulary>, string[]>> |> Expect.equal "with a notice" reply

                let quiet: Result<Reply<Formulary>, string[]> =
                    Ok
                        {
                            Response = Shared.Models.Formulary.empty
                            Notice = None
                        }

                quiet |> toJson |> ofJson<Result<Reply<Formulary>, string[]>> |> Expect.equal "without" quiet

                let refused: Result<Reply<Formulary>, string[]> = Error [| "not loaded" |]
                refused |> toJson |> ofJson<Result<Reply<Formulary>, string[]>> |> Expect.equal "an error" refused
            }

            test "the abbreviation is today's Request over Command" {
                let request: Request =
                    {
                        Opened = None
                        Command = Shared.Api.FormularyCmd Shared.Models.Formulary.empty
                    }

                request |> toJson |> ofJson<Request> |> Expect.equal "back" request
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
