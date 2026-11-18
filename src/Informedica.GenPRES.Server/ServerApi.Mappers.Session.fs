namespace ServerApi

open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types


/// Maps signed order plans, data notices and open sessions between the contract and the server.
module SessionMapper =

    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    /// Maps a user to the signer of a version. The role is not stored, because only a prescriber
    /// signs.
    let signer (user: UserContext) : Signer =
        {
            UserId = user.UserId
            DisplayName = user.DisplayName
        }


    let signerBack (signer: Signer) : UserContext =
        {
            UserId = signer.UserId
            DisplayName = signer.DisplayName
            Role = UserRole.Prescriber
        }


    /// Maps a signed order plan to a version on the date today. Returns the errors of an invalid
    /// order plan.
    let ofSigned today (signed: SignedOrderPlan) : Result<OrderPlanVersion, Types.DtoError list> =
        ({
            PatientData = signed.PatientData |> Patient.ofModel
            Filtered = [||]
            Contexts = signed.OrderContexts |> Array.map OrderContextMapper.ofModel
            Totals = Informedica.GenPRES.Shared.Models.Totals.empty |> OrderContextMapper.totals
        }
        : OrderPlan.Dto.Dto)
        |> OrderPlan.Dto.fromDto today
        |> Result.map (fun plan ->
            {
                Id = signed.Head.Id
                No = signed.Head.No
                PatientId = signed.PatientId
                Base = signed.Base
                SignedBy = signed.Head.By |> signer
                SignedAt = signed.Head.SignedAt
                Plan = plan
                Verified = signed.Verified
                EhrPatient = None
            }
        )


    /// Maps a patient identity to the contract: the name and the birth date.
    let identity (id: Informedica.GenCore.Lib.Patients.PatientIdentity) : PatientIdentity =
        {
            Name = id.Name
            BirthYear = int id.BirthDate.Year
            BirthMonth = int id.BirthDate.Month
            BirthDay = int id.BirthDate.Day
        }


    /// Maps a version to a signed order plan with the patient identity.
    let toSigned
        (demo: bool)
        (whom: Informedica.GenCore.Lib.Patients.PatientIdentity option)
        (v: OrderPlanVersion)
        : SignedOrderPlan
        =
        let plan = v.Plan |> OrderPlan.Dto.toDto

        {
            Head =
                {
                    Id = v.Id
                    No = v.No
                    By = v.SignedBy |> signerBack
                    SignedAt = v.SignedAt
                }
            PatientId = v.PatientId
            Base = v.Base
            OrderContexts = plan.Contexts |> Array.map (OrderContextMapper.toModel demo)
            PatientData = plan.PatientData |> Patient.toModel
            PatientIdentity = whom |> Option.map identity
            Verified = v.Verified
        }


    let notice (token: string) (data: CorePatientData.Dto.Dto option) : DataNotice =
        {
            PatientData = data |> Option.map Patient.toModel
            Token = token
        }


    /// Maps an open session to the session the client opens.
    let toOpened (demo: bool) sessionPatientData (opened: OpenedSession) : SessionOpened =
        {
            User = opened.User
            PatientContext =
                opened.PatientId
                |> Option.map (fun id ->
                    {
                        PatientId = id
                        PatientIdentity = opened |> OpenedSession.identity |> Option.map identity
                        PatientData = opened |> sessionPatientData |> Option.map Patient.ofPatientData
                    }
                )
            OpenedToken = opened.OpenedToken
            KeyThumbprint = opened.KeyThumbprint
            Head =
                opened.Head
                |> Option.bind (fun head ->
                    match head with
                    | StoredVersion.Readable(v, whom) -> v |> toSigned demo whom |> Some
                    | StoredVersion.Unreadable _ -> None
                )
        }
