namespace ServerApi

open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Informedica.GenPRES.Shared.Types


/// The session mapper: a signed order plan as the record's version and back, a data notice's
/// patient, and what the client keeps of an open Session from the parts the state will hold.
module SessionMapper =

    /// The signer of a version. A version does not store the role: only a Prescriber signs.
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


    /// The version of a signed order plan from the client, or the errors of its order plan. The
    /// client sends no filter, no totals and no EHR data, so the version has none.
    let ofSigned (signed: SignedOrderPlan) : Result<OrderPlanVersion, Types.DtoError list> =
        ({
            Patient = signed.Patient |> Patient.ofModel
            Filtered = [||]
            Contexts = signed.OrderContexts |> Array.map OrderContextMapper.ofModel
            Totals = Informedica.GenPRES.Shared.Models.Totals.empty |> OrderContextMapper.totals
        }
        : OrderPlan.Dto.Dto)
        |> OrderPlan.Dto.fromDto
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
                EhrData = None
            }
        )


    /// The identity on the wire, beside the id the context carries: the name and the birthdate
    /// as three integers.
    let identity (id: Informedica.GenCore.Lib.Patients.PatientIdentity) : NameAndBirthDate =
        {
            Name = id.Name
            BirthYear = int id.BirthDate.Year
            BirthMonth = int id.BirthDate.Month
            BirthDay = int id.BirthDate.Day
        }


    /// The signed order plan the client gets for a version, with the identity of the patient.
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
            Patient = plan.Patient |> Patient.toModel
            Identity = whom |> Option.map identity
            Verified = v.Verified
        }


    /// The patient data a notice is about, as the Dto; none when the reading could not be
    /// read.
    let noticeData (notice: DataNotice) = notice.Data |> Option.map Patient.ofModel


    let notice (token: string) (data: Informedica.GenForm.Lib.Patient.Dto.Dto option) : DataNotice =
        {
            Data = data |> Option.map Patient.toModel
            Token = token
        }


    /// What the client keeps of an open Session, from what the store holds: the head as the
    /// signed order plan on the wire when it can be read, none when it cannot, whom the
    /// Session is for, the patient of the Session, computed by sessionPatient, as the contract
    /// model; the demo flag on every context.
    let toOpened (demo: bool) sessionPatient (opened: OpenedSession) : SessionOpened =
        {
            User = opened.User
            PatientContext =
                opened.PatientId
                |> Option.map (fun id ->
                    {
                        PatientId = id
                        Identity = opened |> OpenedSession.identity |> Option.map identity
                        Patient =
                            opened
                            |> sessionPatient
                            |> Option.map (Informedica.GenForm.Lib.Patient.Dto.toDto >> Patient.toModel)
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
