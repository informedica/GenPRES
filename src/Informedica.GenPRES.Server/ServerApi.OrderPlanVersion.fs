namespace ServerApi

open System
open FsToolkit.ErrorHandling
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types


/// Who signed an order plan version.
type Signer =
    {
        /// The user id at the identity provider.
        UserId: string
        /// The name shown for the signer.
        DisplayName: string
    }


/// An order plan version: the order plan as the prescriber signed it, as the record holds it. A
/// prescriber creates one by signing; it stores the whole plan, filter, totals and the totals of
/// each context included, so opening the version again shows what the
/// signer saw, nothing recomputed.
type OrderPlanVersion =
    {
        /// The id of the version.
        Id: string
        /// The version number, one above the previous version of the patient.
        No: int
        /// The patient the plan belongs to.
        PatientId: string
        /// The id of the version this one was built on; None for the first.
        Base: string option
        /// The prescriber who signed the version.
        SignedBy: Signer
        /// The time the prescriber signed the version.
        SignedAt: DateTime
        /// The order plan as signed.
        Plan: GenOrder.OrderPlan
        /// Whether the patient data was verified against the reading of the platform at the
        /// challenge.
        Verified: bool
        /// The EHR data as read at the challenge, none when they could not be read. A value the
        /// prescriber typed shows as a difference with the patient of the plan.
        EhrData: GenForm.EhrPatientData option
    }


/// Why the Dto of an order plan version does not parse to a version.
[<RequireQualifiedAccess>]
type VersionDtoError =
    /// The signer or the order plan does not parse, for the reason the domain gives.
    | SignerOrPlan of GenOrder.DtoError
    /// The EHR data of the version do not parse, with the reason.
    | EhrData of string


module Signer =

    /// The serializable shape of a signer.
    module Dto =

        /// The signer as stored: the user id and the name shown.
        type Dto =
            {
                /// The user id at the identity provider.
                UserId: string
                /// The name shown for the signer.
                DisplayName: string
            }


        /// The Dto of a signer. Total: every signer has a Dto.
        let toDto (s: Signer) =
            {
                UserId = s.UserId
                DisplayName = s.DisplayName
            }


        /// The signer a Dto is; a Dto a serializer left null is missing by its name, and a field
        /// left null reads as blank.
        let fromDto (dto: Dto) : Result<Signer, GenOrder.DtoError list> =
            Nested.required
                "Signer"
                dto
                (fun dto ->
                    Ok
                        {
                            UserId = dto.UserId |> DtoResult.orBlank
                            DisplayName = dto.DisplayName |> DtoResult.orBlank
                        }
                )


module OrderPlanVersion =

    /// The serializable shape of an order plan version, the stored root: its identity, the
    /// signer, the time, and the whole order plan. No structure version here; the database
    /// keeps that beside the row.
    module Dto =

        /// The version as stored: the fields of the version, with the signer, the order plan and
        /// the EHR data as their Dtos.
        type Dto =
            {
                /// The id of the version.
                Id: string
                /// The version number.
                No: int
                /// The patient the plan belongs to.
                PatientId: string
                /// The id of the version this one was built on; None for the first.
                Base: string option
                /// The prescriber who signed the version.
                SignedBy: Signer.Dto.Dto
                /// The time the prescriber signed the version.
                SignedAt: DateTime
                /// The order plan as signed.
                Plan: OrderPlan.Dto.Dto
                /// Whether the patient data was verified against the reading of the platform at
                /// the challenge.
                Verified: bool
                /// The EHR data as read at the challenge; none when they could not be read.
                EhrData: EhrPatientData.Dto.Dto option
            }


        /// The Dto of a version. Total: every version has a Dto.
        let toDto (v: OrderPlanVersion) =
            {
                Id = v.Id
                No = v.No
                PatientId = v.PatientId
                Base = v.Base
                SignedBy = v.SignedBy |> Signer.Dto.toDto
                SignedAt = v.SignedAt
                Plan = v.Plan |> OrderPlan.Dto.toDto
                Verified = v.Verified
                EhrData = v.EhrData |> Option.map EhrPatientData.Dto.toDto
            }


        /// The version a Dto is, or every reason it is none, in the order signer, order plan,
        /// EHR data. A Dto a serializer left null is missing by its name.
        let fromDto (dto: Dto) : Result<OrderPlanVersion, VersionDtoError list> =
            let ofDomain r = r |> Result.mapError (List.map VersionDtoError.SignerOrPlan)

            Nested.required "OrderPlanVersion" dto Ok
            |> ofDomain
            |> Result.bind (fun dto ->
                validation {
                    let! signer = Nested.required "SignedBy" dto.SignedBy Signer.Dto.fromDto |> ofDomain
                    and! plan = Nested.required "Plan" dto.Plan OrderPlan.Dto.fromDto |> ofDomain

                    and! ehr =
                        dto.EhrData
                        |> Option.traverseResult EhrPatientData.Dto.fromDto
                        |> Result.mapError (List.map VersionDtoError.EhrData)

                    return
                        {
                            Id = dto.Id |> DtoResult.orBlank
                            No = dto.No
                            PatientId = dto.PatientId |> DtoResult.orBlank
                            Base = dto.Base
                            SignedBy = signer
                            SignedAt = dto.SignedAt
                            Plan = plan
                            Verified = dto.Verified
                            EhrData = ehr
                        }
                }
            )
