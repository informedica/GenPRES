namespace Informedica.GenOrder.Lib

// The order plan types' Dtos (ADR-0008, docs/adr/0008-contract-model-dto-mapping-boundary.md),
// after Api.fs since a plan context nests an order context. The order plan rules join them here.

open System
open Informedica.GenForm.Lib


module PlanContext =

    /// The serializable shape of a PlanContext: the category as a string, the context and
    /// the intake as their own Dtos.
    module Dto =

        type Dto =
            {
                Id: string
                Category: string
                Context: OrderContext.Dto.Dto
                Intake: Totals.Dto.Dto
            }


        let toDto (pc: PlanContext) : Dto =
            {
                Id = pc.Id
                Category = pc.Category |> OrderCategoryDto.toString
                Context = pc.Context |> OrderContext.Dto.toDto
                Intake = pc.Intake |> Totals.Dto.toDto
            }


        let fromDto (dto: Dto) : Result<PlanContext, DtoError list> =
            let category =
                dto.Category |> OrderCategoryDto.fromString |> Result.mapError List.singleton

            let context = Nested.required "Context" dto.Context OrderContext.Dto.fromDto
            let intake = Nested.required "Intake" dto.Intake Totals.Dto.fromDto

            match category, context, intake with
            | Ok category, Ok context, Ok intake ->
                Ok
                    {
                        Id = dto.Id |> DtoResult.orBlank
                        Category = category
                        Context = context
                        Intake = intake
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf category @ errorsOf context @ errorsOf intake)


module OrderPlan =

    /// The serializable shape of an OrderPlan: the patient, the filtered ids, every
    /// context and the totals.
    module Dto =

        type Dto =
            {
                Patient: Patient.Dto.Dto
                Filtered: string[]
                Contexts: PlanContext.Dto.Dto[]
                Totals: Totals.Dto.Dto
            }


        let toDto (plan: OrderPlan) : Dto =
            {
                Patient = plan.Patient |> Informedica.GenForm.Lib.Patient.Dto.toDto
                Filtered = plan.Filtered
                Contexts = plan.Contexts |> Array.map PlanContext.Dto.toDto
                Totals = plan.Totals |> Totals.Dto.toDto
            }


        /// The order plan a Dto is, or every reason it is none, over the patient and every
        /// context.
        let fromDto (dto: Dto) : Result<OrderPlan, DtoError list> =
            let patient =
                Nested.required
                    "Patient"
                    dto.Patient
                    (fun p ->
                        p
                        |> Informedica.GenForm.Lib.Patient.Dto.fromDto
                        |> Result.mapError (List.map DtoError.Patient)
                    )

            let contexts =
                dto.Contexts
                |> DtoResult.orEmpty
                |> Array.toList
                |> List.map (fun pc -> Nested.required "Context" pc PlanContext.Dto.fromDto)
                |> DtoResult.sequence
                |> Result.mapError List.concat

            let totals = Nested.required "Totals" dto.Totals Totals.Dto.fromDto

            match patient, contexts, totals with
            | Ok patient, Ok contexts, Ok totals ->
                Ok
                    {
                        Patient = patient
                        Filtered = dto.Filtered |> DtoResult.orEmpty
                        Contexts = contexts |> List.toArray
                        Totals = totals
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf patient @ errorsOf contexts @ errorsOf totals)


module Signer =

    module Dto =

        type Dto =
            {
                UserId: string
                DisplayName: string
            }


        let toDto (s: Signer) : Dto =
            {
                UserId = s.UserId
                DisplayName = s.DisplayName
            }


        let fromDto (dto: Dto) : Result<Signer, DtoError list> =
            Ok
                {
                    UserId = dto.UserId |> DtoResult.orBlank
                    DisplayName = dto.DisplayName |> DtoResult.orBlank
                }


module OrderPlanVersion =

    /// The serializable shape of an order plan version, the stored root: its identity, the
    /// signer, the time, and the whole order plan. No structure version here; the database
    /// keeps that beside the row.
    module Dto =

        type Dto =
            {
                Id: string
                No: int
                PatientId: string
                Base: string option
                SignedBy: Signer.Dto.Dto
                SignedAt: DateTime
                Plan: OrderPlan.Dto.Dto
                Verified: bool
            }


        let toDto (v: OrderPlanVersion) : Dto =
            {
                Id = v.Id
                No = v.No
                PatientId = v.PatientId
                Base = v.Base
                SignedBy = v.SignedBy |> Signer.Dto.toDto
                SignedAt = v.SignedAt
                Plan = v.Plan |> OrderPlan.Dto.toDto
                Verified = v.Verified
            }


        let fromDto (dto: Dto) : Result<OrderPlanVersion, DtoError list> =
            let signer = Nested.required "SignedBy" dto.SignedBy Signer.Dto.fromDto
            let plan = Nested.required "Plan" dto.Plan OrderPlan.Dto.fromDto

            match signer, plan with
            | Ok signer, Ok plan ->
                Ok
                    {
                        Id = dto.Id |> DtoResult.orBlank
                        No = dto.No
                        PatientId = dto.PatientId |> DtoResult.orBlank
                        Base = dto.Base
                        SignedBy = signer
                        SignedAt = dto.SignedAt
                        Plan = plan
                        Verified = dto.Verified
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf signer @ errorsOf plan)
