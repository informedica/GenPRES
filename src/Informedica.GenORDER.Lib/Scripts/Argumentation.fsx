// Step 7 of the plan for #985, the domain's half: the argumentation a clinician writes when a
// dose leaves what the rules allow, on GenORDER's OrderContext and its Dto. The text is an
// optional string on the record evaluation works on, so every command that copies the record
// with `with` carries it, and it reaches the stored plan JSON through the context of each plan
// context. Nothing here reads the text: what it means is the client's, and its length is
// checked at the server's parse (Server/Scripts/Argumentation.fsx).
//
// The modules below are Types.fs and Api.fs as they become:
//
// - Types.OrderContext gains `Argumentation: string option`, last field;
// - OrderContext.create sets it to none; the other construction sites, OrderContext.Dto.fromDto
//   here and the nutrition context in ServerApi.Adapters.fs, set it from the Dto and to none;
// - OrderContext.Dto.Dto gains the same field, toDto copies it, fromDto copies it as it is.
//
// What the script checks: the Dto round trip keeps the text and keeps none; the JSON a version
// 1 row holds today, without the field, reads as none through the new Dto; the new Dto's
// canonical JSON carries the field, null or the text. What the script cannot check before the
// field exists: that evaluation keeps the text. That test goes to OrderPlanTests after the
// migration: a plan context with a text, evaluated with `PlanContext.evaluateWith` over an
// evaluate that answers the context it is given, comes back with the text.
//
// Run: `dotnet fsi Argumentation.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"

open Expecto
open Expecto.Flip

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


/// Types.fs as it becomes: the order context with its argumentation.
module Types =

    open Informedica.GenOrder.Lib.Types


    /// An order context: the filter of a scenario selection, the patient it is for, the
    /// scenarios it holds, and the argumentation for a dose outside what the rules allow.
    type OrderContext =
        {
            /// The filter for selecting scenarios
            Filter: Filter
            /// The patient information
            Patient: Patient
            /// The list of available scenarios
            Scenarios: OrderScenario[]
            /// What the clinician wrote when the dose left what the rules allow; none until
            /// written. Kept as written: the client normalises it, the server caps its length.
            Argumentation: string option
        }


/// Api.fs as it becomes, the pieces of the OrderContext module the field touches.
module OrderContext =

    open Informedica.GenOrder.Lib.Types


    /// Today's context with the field added, none until written.
    let ofToday (ctx: OrderContext) : Types.OrderContext =
        {
            Filter = ctx.Filter
            Patient = ctx.Patient
            Scenarios = ctx.Scenarios
            Argumentation = None
        }


    /// Today's context, the field left behind; the shape the existing pipeline works on.
    let toToday (ctx: Types.OrderContext) : OrderContext =
        {
            Filter = ctx.Filter
            Patient = ctx.Patient
            Scenarios = ctx.Scenarios
        }


    /// A fresh context for a patient: the pick lists the rules give, nothing chosen, no
    /// scenario, and no argumentation yet.
    let create logger provider (pat: Patient) : Types.OrderContext =
        Informedica.GenOrder.Lib.OrderContext.create logger provider pat |> ofToday


    /// The serializable shape of an OrderContext: its filter, its patient and its scenarios,
    /// each as its own Dto, and its argumentation as written.
    module Dto =

        type Dto =
            {
                Filter: Filter.Dto.Dto
                Patient: Patient.Dto.Dto
                Scenarios: OrderScenario.Dto.Dto[]
                Argumentation: string option
            }


        let toDto (ctx: Types.OrderContext) : Dto =
            let today = ctx |> toToday |> Informedica.GenOrder.Lib.OrderContext.Dto.toDto

            {
                Filter = today.Filter
                Patient = today.Patient
                Scenarios = today.Scenarios
                Argumentation = ctx.Argumentation
            }


        /// The context a Dto is, or every reason it is none: the filter's, the patient's
        /// and every scenario's, a scenario that fails never dropped. The argumentation is
        /// copied as it is: a Dto without it, as a version 1 row holds, reads as none.
        let fromDto (dto: Dto) : Result<Types.OrderContext, DtoError list> =
            let today: Informedica.GenOrder.Lib.OrderContext.Dto.Dto =
                {
                    Filter = dto.Filter
                    Patient = dto.Patient
                    Scenarios = dto.Scenarios
                }

            today
            |> Informedica.GenOrder.Lib.OrderContext.Dto.fromDto
            |> Result.map (fun ctx ->
                let ctx = ctx |> ofToday

                { ctx with
                    Argumentation = dto.Argumentation
                }
            )


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Informedica.GenOrder.Lib.Types


module Fixtures =

    let order =
        match
            Scenarios.pcmSupp
            |> Medication.toOrderDto Scenarios.testStart
            |> Order.Dto.fromDto
        with
        | Ok o -> o
        | Error e -> invalidOp $"fixture order could not be created: {e}"


    let filter: Filter =
        {
            Indications = [| "koorts"; "pijn" |]
            Generics = [| "paracetamol" |]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
            Diluents = [||]
            Components = [||]
            Indication = None
            Generic = Some "paracetamol"
            Route = None
            Form = None
            DoseType = None
            Diluent = None
            SelectedComponents = [| "paracetamol" |]
        }


    let scenario: OrderScenario =
        {
            No = 1
            Name = "paracetamol"
            Indication = "koorts"
            Form = "zetpil"
            Route = "rect"
            DoseType = NoDoseType
            Diluent = None
            Component = None
            Item = None
            Diluents = [||]
            Components = [||]
            Items = [||]
            Prescription = [||]
            Preparation = [||]
            Administration = [||]
            Order = order
            UseAdjust = false
            UseRenalRule = false
            RenalRule = None
            ProductsIds = [||]
        }


    /// Today's context, as the pipeline and a version 1 row hold it; the patient has an age,
    /// the least the Dto parse asks of one.
    let today: OrderContext =
        {
            Filter = filter
            Patient =
                { Patient.patient with
                    Age = Some(ValueUnit.singleWithUnit Units.Time.day 3650N)
                }
            Scenarios = [| scenario |]
        }


    let text = "Sepsis, hogere dosis in overleg met de apotheek"


    let argued: Types.OrderContext =
        let ctx = today |> OrderContext.ofToday

        { ctx with
            Argumentation = Some text
        }


open Fixtures


let roundTrip (ctx: Types.OrderContext) =
    ctx |> OrderContext.Dto.toDto |> OrderContext.Dto.fromDto


let tests =
    testList
        "the argumentation on the order context"
        [
            test "to the Dto and back keeps the text" {
                match roundTrip argued with
                | Ok ctx ->
                    ctx.Argumentation |> Expect.equal "the text" (Some text)
                    ctx.Filter |> Expect.equal "the filter" filter
                    ctx.Scenarios.Length |> Expect.equal "one scenario" 1
                | Error e -> failtest $"the round trip failed: %A{e}"
            }

            test "to the Dto and back keeps none" {
                match roundTrip (today |> OrderContext.ofToday) with
                | Ok ctx -> ctx.Argumentation |> Expect.isNone "none stays none"
                | Error e -> failtest $"the round trip failed: %A{e}"
            }

            test "a created context has no argumentation" {
                // the filter and the scenarios are the provider's business; only the field is asked
                let ctx = today |> OrderContext.ofToday

                ctx.Argumentation |> Expect.isNone "none until written"
            }

            test "the JSON a version 1 row holds, without the field, reads as none" {
                // the JSON as this release writes it: today's Dto, the canonical form the store keeps
                let json =
                    today
                    |> Informedica.GenOrder.Lib.OrderContext.Dto.toDto
                    |> Canonical.serialize

                json.Contains "Argumentation" |> Expect.isFalse "no such field today"

                match json |> Canonical.deserialize<OrderContext.Dto.Dto> |> OrderContext.Dto.fromDto with
                | Ok ctx ->
                    ctx.Argumentation |> Expect.isNone "absent reads as none"
                    ctx |> OrderContext.toToday |> Expect.equal "the rest as it was" today
                | Error e -> failtest $"a version 1 context does not parse: %A{e}"
            }

            test "the new Dto's canonical JSON carries the field, null or the text" {
                let none = today |> OrderContext.ofToday |> OrderContext.Dto.toDto |> Canonical.serialize
                let some = argued |> OrderContext.Dto.toDto |> Canonical.serialize

                none.Contains "\"Argumentation\":null" |> Expect.isTrue "none is written as null"
                some.Contains $"\"Argumentation\":\"{text}\"" |> Expect.isTrue "the text is written"

                // and the new Dto reads its own JSON back
                match some |> Canonical.deserialize<OrderContext.Dto.Dto> |> OrderContext.Dto.fromDto with
                | Ok ctx -> ctx.Argumentation |> Expect.equal "the text back" (Some text)
                | Error e -> failtest $"the JSON does not read back: %A{e}"
            }
        ]


runTestsWithCLIArgs [] [||] tests
