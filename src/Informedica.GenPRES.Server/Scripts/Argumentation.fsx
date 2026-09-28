// Step 7 of the plan for #985, the server's half: the argumentation on the contract, through
// the mappers, capped where the server parses a context, and into the stored plan JSON under
// structure version 2. The domain's half is src/Informedica.GenORDER.Lib/Scripts/Argumentation.fsx.
//
// The modules below are the files as they become:
//
// - Shared/Types.fs: the contract's OrderContext gains `Argumentation: string option`, last
//   field; Shared/Models.fs sets it to none on `OrderContext.empty`. Every other construction
//   in the client and Client.Core is a `with` copy and needs no change.
// - ServerApi.Mappers.OrderContext.fs: `ofModel` and `toModel` copy the field both ways, so a
//   signed version carries it in and out through the session and plan mappers unchanged.
// - ServerApi.Services.fs: `OrderContextService.Argumentation.check`, the cap of 1000
//   characters with the server's words, and `parse` refusing before it maps;
//   ServerApi.OrderPlanCommand.fs: `parsePlan` checks every context the same way, so a plan
//   submitted for signing is held to it too. A stored row can hold no longer text, since every
//   write passes one of the two.
// - ServerApi.SqlAdapters.fs: `jsonVersionRead` and `jsonVersionWritten` go to 2, and
//   `upgrade` gains its first step, 1 to 2, which leaves the JSON as it is: the field reads as
//   none when absent, and no read path recomputes a digest of a stored version, the signing
//   digest being taken over the plan submitted, at the challenge and at the signature.
//
// What the script checks: the cap; the mapper round trip with a text and with none; the plan
// context JSON a version 1 row holds today, without the field, reads as none; the upgrade's
// answers per version; the parse refusing a text over the cap and taking one under it.
//
// Run: `dotnet fsi Argumentation.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"

open Expecto
open Expecto.Flip

open Informedica.GenOrder.Lib
open ServerApi


/// Shared/Types.fs as it becomes: the contract's order context with its argumentation.
module Contract =

    open Shared.Types


    type OrderContext =
        {
            Id: string
            Category: OrderCategory
            DemoVersion: bool
            Filter: Filter
            Patient: Patient
            Scenarios: OrderScenario[]
            Intake: Totals
            /// What the clinician wrote when the dose left what the rules allow; none until
            /// written.
            Argumentation: string option
        }


    /// Today's contract context with the field added, none until written.
    let ofToday (ctx: Shared.Types.OrderContext) : OrderContext =
        {
            Id = ctx.Id
            Category = ctx.Category
            DemoVersion = ctx.DemoVersion
            Filter = ctx.Filter
            Patient = ctx.Patient
            Scenarios = ctx.Scenarios
            Intake = ctx.Intake
            Argumentation = None
        }


    /// Today's contract context, the field left behind.
    let toToday (ctx: OrderContext) : Shared.Types.OrderContext =
        {
            Id = ctx.Id
            Category = ctx.Category
            DemoVersion = ctx.DemoVersion
            Filter = ctx.Filter
            Patient = ctx.Patient
            Scenarios = ctx.Scenarios
            Intake = ctx.Intake
        }


/// The domain's Dtos as they become, the shapes the GenORDER script gives them: the context
/// Dto with the field, and the plan context Dto around it.
module DomainDto =

    type ContextDto =
        {
            Filter: Filter.Dto.Dto
            Patient: Informedica.GenForm.Lib.Patient.Dto.Dto
            Scenarios: OrderScenario.Dto.Dto[]
            Argumentation: string option
        }


    type PlanContextDto =
        {
            Id: string
            Category: string
            Context: ContextDto
            Intake: Totals.Dto.Dto
        }


    let ofToday (argumentation: string option) (dto: PlanContext.Dto.Dto) : PlanContextDto =
        {
            Id = dto.Id
            Category = dto.Category
            Context =
                {
                    Filter = dto.Context.Filter
                    Patient = dto.Context.Patient
                    Scenarios = dto.Context.Scenarios
                    Argumentation = argumentation
                }
            Intake = dto.Intake
        }


    let toToday (dto: PlanContextDto) : PlanContext.Dto.Dto =
        {
            Id = dto.Id
            Category = dto.Category
            Context =
                {
                    Filter = dto.Context.Filter
                    Patient = dto.Context.Patient
                    Scenarios = dto.Context.Scenarios
                }
            Intake = dto.Intake
        }


/// ServerApi.Mappers.OrderContext.fs as it becomes: the field copied both ways, nothing else
/// touched. Written here over today's mapper, the field carried around it.
module OrderContextMapper =

    /// Total: the contract model's order context as the plan context's Dto, nothing dropped,
    /// the argumentation as written.
    let ofModel (ctx: Contract.OrderContext) : DomainDto.PlanContextDto =
        ctx
        |> Contract.toToday
        |> ServerApi.OrderContextMapper.ofModel
        |> DomainDto.ofToday ctx.Argumentation


    /// The plan context's Dto as the contract model's order context, built from the Dto alone;
    /// the demo flag is the server's. The argumentation is the Dto's, none when it has none.
    let toModel (demo: bool) (dto: DomainDto.PlanContextDto) : Contract.OrderContext =
        let today =
            dto
            |> DomainDto.toToday
            |> ServerApi.OrderContextMapper.toModel demo
            |> Contract.ofToday

        { today with
            Argumentation = dto.Context.Argumentation
        }


/// ServerApi.Services.fs as it becomes: the cap, and the parse that holds a context to it.
module OrderContextService =

    /// The argumentation as the server takes it in: at most this many characters. The client
    /// normalises the text; the server only refuses what is too long to store.
    module Argumentation =

        let maxLength = 1000


        /// The text as sent, or the reason it is refused in the server's words: longer than
        /// the cap. None passes.
        let check (text: string option) : Result<string option, string[]> =
            match text with
            | Some s when s.Length > maxLength ->
                Error [| $"De argumentatie is te lang: %i{s.Length} tekens, ten hoogste %i{maxLength}" |]
            | _ -> Ok text


    /// The contract model's context parsed: the plan context the mapper makes of it, or the
    /// reasons it is none in the server's words; a text over the cap is refused before the
    /// mapping.
    let parse (ctx: Contract.OrderContext) : Result<PlanContext, string[]> =
        ctx.Argumentation
        |> Argumentation.check
        |> Result.bind (fun _ ->
            ctx
            |> OrderContextMapper.ofModel
            |> DomainDto.toToday
            |> PlanContext.Dto.fromDto
            |> Result.mapError (List.map ServerApi.OrderContextMapper.words >> List.toArray)
        )


/// ServerApi.SqlAdapters.fs as it becomes: the structure version and its first upgrade step.
module SqlDatabase =

    /// The highest JSON structure version this release reads.
    let jsonVersionRead = 2

    /// The JSON structure version this release writes.
    let jsonVersionWritten = 2


    /// Brings plan JSON written under a structure version to the structure this release
    /// reads, one pure step per version on raw JSON. Version 1 to 2 added the argumentation
    /// on every order context: the field reads as none when absent, so the step leaves the
    /// JSON as it is, and no read path recomputes a digest of a stored version.
    let upgrade (version: int) (json: string) : Result<string, string> =
        if version < 1 then
            Error $"JSON structure version %i{version} does not exist"
        elif version > jsonVersionRead then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Shared.Types


module Fixtures =

    /// A drug context of the plan, on the stub patient, as the contract carries it today.
    let today: Shared.Types.OrderContext =
        { Shared.Models.OrderContext.empty with
            Id = "c-1"
            DemoVersion = false
            Filter =
                { Shared.Models.OrderContext.filter with
                    Generics = [| "paracetamol" |]
                    Generic = Some "paracetamol"
                }
            Patient = StubPatientData.patient
        }


    let text = "Sepsis, hogere dosis in overleg met de apotheek"


    let argued: Contract.OrderContext =
        let ctx = today |> Contract.ofToday

        { ctx with
            Argumentation = Some text
        }


open Fixtures


let tests =
    testList
        "the argumentation on the server"
        [
            testList
                "the cap"
                [
                    test "none and a text within the cap pass as sent" {
                        OrderContextService.Argumentation.check None |> Expect.equal "none" (Ok None)

                        OrderContextService.Argumentation.check (Some text)
                        |> Expect.equal "the text" (Ok(Some text))

                        let atCap = String.replicate 1000 "a"

                        OrderContextService.Argumentation.check (Some atCap)
                        |> Expect.equal "exactly the cap" (Ok(Some atCap))
                    }

                    test "a text over the cap is refused in the server's words" {
                        OrderContextService.Argumentation.check (Some(String.replicate 1001 "a"))
                        |> Expect.equal
                            "one over"
                            (Error [| "De argumentatie is te lang: 1001 tekens, ten hoogste 1000" |])
                    }
                ]

            testList
                "the mapper"
                [
                    test "to the Dto and back keeps the text" {
                        let back = argued |> OrderContextMapper.ofModel |> OrderContextMapper.toModel false

                        back.Argumentation |> Expect.equal "the text" (Some text)
                        back |> Contract.toToday |> Expect.equal "the rest as it was" today
                    }

                    test "to the Dto and back keeps none" {
                        let back =
                            today
                            |> Contract.ofToday
                            |> OrderContextMapper.ofModel
                            |> OrderContextMapper.toModel false

                        back.Argumentation |> Expect.isNone "none stays none"
                    }

                    test "the plan context JSON a version 1 row holds, without the field, reads as none" {
                        // the JSON as this release writes it for a plan context, the canonical form
                        let json =
                            today
                            |> ServerApi.OrderContextMapper.ofModel
                            |> Canonical.serialize

                        json.Contains "Argumentation" |> Expect.isFalse "no such field today"

                        let read = json |> Canonical.deserialize<DomainDto.PlanContextDto>

                        read.Context.Argumentation |> Expect.isNone "absent reads as none"

                        (read |> OrderContextMapper.toModel false |> Contract.toToday)
                        |> Expect.equal "the rest as it was" today
                    }
                ]

            testList
                "the parse"
                [
                    test "a context with a text within the cap parses to the plan context" {
                        match OrderContextService.parse argued with
                        | Ok pc -> pc.Id |> Expect.equal "the id" "c-1"
                        | Error e -> failtest $"refused: %A{e}"
                    }

                    test "a context with a text over the cap is refused before it is mapped" {
                        { argued with
                            Argumentation = Some(String.replicate 1001 "a")
                        }
                        |> OrderContextService.parse
                        |> Result.map (fun _ -> ())
                        |> Expect.equal
                            "refused"
                            (Error [| "De argumentatie is te lang: 1001 tekens, ten hoogste 1000" |])
                    }
                ]

            testList
                "the structure version"
                [
                    test "this release reads and writes version 2" {
                        SqlDatabase.jsonVersionRead |> Expect.equal "read" 2
                        SqlDatabase.jsonVersionWritten |> Expect.equal "written" 2
                    }

                    test "the upgrade: a version 1 row is left as it is, 2 as well, 0 and 3 are reasons" {
                        let json = """{"Plan":{}}"""

                        SqlDatabase.upgrade 1 json |> Expect.equal "1 to 2 leaves the JSON" (Ok json)
                        SqlDatabase.upgrade 2 json |> Expect.equal "2 is current" (Ok json)

                        SqlDatabase.upgrade 0 json
                        |> Expect.equal "0" (Error "JSON structure version 0 does not exist")

                        SqlDatabase.upgrade 3 json
                        |> Expect.equal "3" (Error "JSON structure version 3 is newer than this release knows")
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
