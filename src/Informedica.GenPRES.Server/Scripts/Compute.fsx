// The interaction member (plan 654, step 7): `processInteraction`, a `Request<InteractionCommand>`
// answered with a `Reply<InteractionResponse>` through `Compute.bound`. This family carries the
// one gate that differs per command: the drug names come from the interaction source and are
// served while the formulary is not loaded, a check over a plan's drugs needs it. With this
// family gone from `Command`, every command left there needs the formulary, and `Command.gate`
// says so without a match.
//
// Script-first draft (script-only policy) of:
//   - `InteractionCommand` and `InteractionResponse` as qualified-access types with
//     `InteractionCommand.toString`, and `processInteraction` on `IServerApi` → `Shared/Api.fs`
//     (restated here under `Api654`: a script cannot add or qualify cases of `Shared.Api`);
//   - `InteractionCommand.gate` and `processCmd` → new `ServerApi.InteractionCommand.fs`,
//     compiled after `FormularyCommand.fs` (fsproj, both loaders);
//   - `Command.gate` constant, the two arms gone → `ServerApi.Command.fs`;
//   - the member in `compose` → `CompositionRoot.fs`;
//   - the client's `applyInteraction` and the two loads on the member (in the patch).
//
// Run: `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Types
open Shared.Api
open ServerApi


// ---------------------------------------------------------------------------------------------
// The wire (→ Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

module Api654 =

    /// The interaction family: the drug names of the interaction source, and a check over the
    /// drugs of a plan.
    [<RequireQualifiedAccess>]
    type InteractionCommand =
        | CheckInteractions of string list
        // from the interaction source, not the formulary: served while the formulary is not loaded
        | GetDrugNames


    [<RequireQualifiedAccess>]
    type InteractionResponse =
        | InteractionsChecked of DrugInteraction[]
        | DrugNamesLoaded of string[]


    module InteractionCommand =

        /// For the log: never the drug list.
        let toString cmd =
            match cmd with
            | InteractionCommand.CheckInteractions _ -> "CheckInteractions"
            | InteractionCommand.GetDrugNames -> "GetDrugNames"


    // on `IServerApi`:
    //
    //     processInteraction: Request<InteractionCommand> -> Async<Result<Reply<InteractionResponse>, string[]>>


// ---------------------------------------------------------------------------------------------
// The member (→ ServerApi.InteractionCommand.fs)
// ---------------------------------------------------------------------------------------------

module InteractionCommand =

    open Api654

    /// The drug names come from the interaction source, not the formulary; a check over a
    /// plan's drugs needs the formulary loaded.
    let gate =
        function
        | InteractionCommand.GetDrugNames -> Gate.Open
        | InteractionCommand.CheckInteractions _ -> Gate.RequiresLoaded


    let processCmd (env: AppEnv) (cmd: InteractionCommand) =
        match cmd with
        | InteractionCommand.GetDrugNames ->
            async {
                let! result = env.interaction.getDrugNames ()
                return result |> Result.map (List.toArray >> InteractionResponse.DrugNamesLoaded)
            }
        | InteractionCommand.CheckInteractions drugs ->
            async {
                let! result = env.interaction.checkInteractions drugs
                return result |> Result.map (List.toArray >> InteractionResponse.InteractionsChecked)
            }


// and (→ ServerApi.Command.fs), once no open command is left in Command:
//
//     /// Every command left here needs the formulary loaded. Applied by Compute.bound.
//     let gate (_: Command) = Gate.RequiresLoaded
//
// and in `compose` (→ CompositionRoot.fs):
//
//     processInteraction =
//         Compute.bound env cookie InteractionCommand.toString InteractionCommand.gate (InteractionCommand.processCmd env)


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Api654


let cookieOf (id: string option) : SessionCookie =
    {
        read = fun () -> id
        write = ignore
        delete = ignore
    }


/// An env over a provider whose load failed, the interaction port stubbed and counted.
let envWith (loaded: bool) =
    let asked = ref 0

    let env =
        Adapters.makeAppEnv (
            Resources.CachedResourceProvider(
                (fun () -> Error [ Informedica.GenForm.Lib.Types.ErrorMsg("load failed", None) ]),
                None
            )
        )

    asked,
    { env with
        requireLoaded =
            fun () ->
                asked.Value <- asked.Value + 1
                if loaded then None else env.requireLoaded ()
        interaction =
            {
                checkInteractions = fun drugs -> async { return Ok [] }
                getDrugNames = fun () -> async { return Ok [ "paracetamol"; "ibuprofen" ] }
            }
    }


let processInteraction env cookie (request: Request<InteractionCommand>) =
    Compute.bound env cookie InteractionCommand.toString InteractionCommand.gate (InteractionCommand.processCmd env) request
    |> Async.RunSynchronously


let tests =
    testList
        "processInteraction"
        [
            test "the drug names are served while the formulary is not loaded, and the provider is not asked" {
                let asked, env = envWith false

                match processInteraction env (cookieOf None) { Opened = None; Command = InteractionCommand.GetDrugNames } with
                | Ok reply ->
                    reply.Response
                    |> Expect.equal "the names" (InteractionResponse.DrugNamesLoaded [| "paracetamol"; "ibuprofen" |])
                | Error errs -> failtest $"expected Ok, got {errs}"

                asked.Value |> Expect.equal "never asked: asking may load" 0
            }

            test "a check needs the formulary: refused while not loaded, computed when loaded" {
                let asked, env = envWith false

                processInteraction env (cookieOf None) { Opened = None; Command = InteractionCommand.CheckInteractions [ "a"; "b" ] }
                |> Result.isError
                |> Expect.isTrue "refused"

                asked.Value |> Expect.equal "asked once" 1

                let _, env = envWith true

                match processInteraction env (cookieOf None) { Opened = None; Command = InteractionCommand.CheckInteractions [ "a"; "b" ] } with
                | Ok reply -> reply.Response |> Expect.equal "checked" (InteractionResponse.InteractionsChecked [||])
                | Error errs -> failtest $"expected Ok, got {errs}"
            }

            test "the gate per command" {
                InteractionCommand.gate InteractionCommand.GetDrugNames |> Expect.equal "open" Gate.Open
                InteractionCommand.gate (InteractionCommand.CheckInteractions []) |> Expect.equal "gated" Gate.RequiresLoaded
            }

            test "the log names the command, never the drugs" {
                let line = InteractionCommand.toString (InteractionCommand.CheckInteractions [ "secret-drug" ])
                line |> Expect.equal "name" "CheckInteractions"
                InteractionCommand.toString InteractionCommand.GetDrugNames |> Expect.equal "name" "GetDrugNames"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
