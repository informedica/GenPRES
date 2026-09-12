// The first two members peeled off processCommand (plan 654, step 6): `processFormulary` and
// `processParenteralia`, each a `Request<_>` of its own command answered with a `Reply<_>` of
// its own answer, built through `Compute.bound` like every computing member. The smallest
// families first, so the generic envelope is seen on the browser's wire before the big ones
// move. `FormularyCmd`, `ParenteraliaCmd`, `FormularyResp` and `ParenteraliaResp` leave
// `Command` and `Response` with the migration.
//
// Script-first draft (script-only policy) of:
//   - `FormularyCommand` and `ParenteraliaCommand` (`toString`, `processCmd`) → new
//     `ServerApi.FormularyCommand.fs`, compiled after `Compute.fs` (fsproj, both loaders);
//   - `processFormulary` and `processParenteralia` on `IServerApi` → `Shared/Api.fs`, the four
//     cases and their `toString` arms deleted there and the two arms in `Command.fs`;
//   - the two members in `compose` → `CompositionRoot.fs`;
//   - the client: `createApiMsg` over the member, `Answer<'r>`/`ApiResponse<'r>`,
//     `processApiMsg` with an `apply` per family, `LoadFormulary`/`LoadParenteralia` on the new
//     members (in the migration patch, since the client cannot compile against members that do
//     not exist yet).
//
// Run: `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Types
open Shared.Api
open ServerApi


// ---------------------------------------------------------------------------------------------
// The members (→ ServerApi.FormularyCommand.fs)
// ---------------------------------------------------------------------------------------------

module FormularyCommand =

    /// For the log: the record is long and says nothing a log needs.
    let toString (_: Formulary) = "Formulary"

    let processCmd (env: AppEnv) (form: Formulary) = env.formulary.getFormulary form


module ParenteraliaCommand =

    let toString (_: Parenteralia) = "Parenteralia"

    let processCmd (env: AppEnv) (par: Parenteralia) = env.formulary.getParenteralia par


// and in `compose` (→ CompositionRoot.fs), next to processCommand:
//
//     processFormulary =
//         Compute.bound env cookie FormularyCommand.toString (fun _ -> Gate.RequiresLoaded) (FormularyCommand.processCmd env)
//
//     processParenteralia =
//         Compute.bound env cookie ParenteraliaCommand.toString (fun _ -> Gate.RequiresLoaded) (ParenteraliaCommand.processCmd env)
//
// on `IServerApi` (→ Shared/Api.fs):
//
//     processFormulary: Request<Formulary> -> Async<Result<Reply<Formulary>, string[]>>
//     processParenteralia: Request<Parenteralia> -> Async<Result<Reply<Parenteralia>, string[]>>


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib


let cookieOf (id: string option) : SessionCookie =
    {
        read = fun () -> id
        write = ignore
        delete = ignore
    }


/// An env over a provider whose load failed, the formulary port stubbed, the Session's answer given.
let envWith (loaded: bool) (told: RecordNotice option) =
    let env =
        Adapters.makeAppEnv (
            Resources.CachedResourceProvider(
                (fun () -> Error [ Informedica.GenForm.Lib.Types.ErrorMsg("load failed", None) ]),
                None
            )
        )

    { env with
        requireLoaded = if loaded then (fun () -> None) else env.requireLoaded
        formulary =
            {
                getFormulary = fun f -> async { return Ok { f with Markdown = "stubbed" } }
                getParenteralia = fun p -> async { return Ok { p with Generic = Some "stubbed" } }
            }
        session =
            { env.session with
                seen = fun _ _ -> async { return told }
            }
    }


let processFormulary env cookie (request: Request<Formulary>) =
    Compute.bound env cookie FormularyCommand.toString (fun _ -> Gate.RequiresLoaded) (FormularyCommand.processCmd env) request
    |> Async.RunSynchronously


let processParenteralia env cookie (request: Request<Parenteralia>) =
    Compute.bound env cookie ParenteraliaCommand.toString (fun _ -> Gate.RequiresLoaded) (ParenteraliaCommand.processCmd env) request
    |> Async.RunSynchronously


let tests =
    testList
        "processFormulary and processParenteralia"
        [
            test "the formulary answers its own envelope, typed" {
                let env = envWith true None

                match processFormulary env (cookieOf None) { Opened = None; Command = Shared.Models.Formulary.empty } with
                | Ok reply ->
                    // no family to match on: the answer is a Formulary
                    reply.Response.Markdown |> Expect.equal "computed" "stubbed"
                    reply.Notice |> Expect.isNone "nothing told"
                | Error errs -> failtest $"expected Ok, got {errs}"
            }

            test "the parenteralia answers its own envelope, typed" {
                let env = envWith true None

                match processParenteralia env (cookieOf None) { Opened = None; Command = Shared.Models.Parenteralia.empty } with
                | Ok reply -> reply.Response.Generic |> Expect.equal "computed" (Some "stubbed")
                | Error errs -> failtest $"expected Ok, got {errs}"
            }

            test "with a cookie: what the Session is told rides on the reply" {
                let env = envWith true (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))

                match processFormulary env (cookieOf (Some "s-1")) { Opened = None; Command = Shared.Models.Formulary.empty } with
                | Ok reply ->
                    reply.Notice |> Expect.equal "the ending" (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))
                    reply.Response.Markdown |> Expect.equal "still computed" "stubbed"
                | Error errs -> failtest $"expected Ok, got {errs}"
            }

            test "both are behind the formulary being loaded" {
                let env = envWith false None

                processFormulary env (cookieOf None) { Opened = None; Command = Shared.Models.Formulary.empty }
                |> Result.isError
                |> Expect.isTrue "formulary refused"

                processParenteralia env (cookieOf None) { Opened = None; Command = Shared.Models.Parenteralia.empty }
                |> Result.isError
                |> Expect.isTrue "parenteralia refused"
            }

            test "the log names the family, never the record" {
                FormularyCommand.toString Shared.Models.Formulary.empty |> Expect.equal "name" "Formulary"
                ParenteraliaCommand.toString Shared.Models.Parenteralia.empty |> Expect.equal "name" "Parenteralia"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
