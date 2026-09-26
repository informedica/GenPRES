namespace ServerApi

open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types


/// The session mapper: a signed order plan as the record's version and back, a data notice's
/// patient, and what the client keeps of an open Session from the parts the state will hold.
module SessionMapper =

    /// Who signed, as the version records it. The role is not recorded: only a Prescriber
    /// signs.
    let signer (user: UserContext) : Signer.Dto.Dto =
        {
            UserId = user.UserId
            DisplayName = user.DisplayName
        }


    let signerBack (dto: Signer.Dto.Dto) : UserContext =
        {
            UserId = dto.UserId
            DisplayName = dto.DisplayName
            Role = UserRole.Prescriber
        }


    /// Total: a signed order plan from the wire as the version's Dto. The contract model
    /// carries the contexts and the patient; the version stores an order plan, so the filter
    /// is empty and the totals are none.
    let ofSigned (signed: SignedOrderPlan) : OrderPlanVersion.Dto.Dto =
        {
            Id = signed.Head.Id
            No = signed.Head.No
            PatientId = signed.PatientId
            Base = signed.Base
            SignedBy = signed.Head.By |> signer
            SignedAt = signed.Head.SignedAt
            Plan =
                {
                    Patient = signed.Patient |> Patient.ofModel
                    Filtered = [||]
                    Contexts = signed.OrderContexts |> Array.map OrderContextMapper.ofModel
                    Totals = Shared.Models.Totals.empty |> OrderContextMapper.totals
                }
            Verified = signed.Verified
        }


    /// The version's Dto as the signed order plan the wire carries.
    let toSigned (demo: bool) (dto: OrderPlanVersion.Dto.Dto) : SignedOrderPlan =
        {
            Head =
                {
                    Id = dto.Id
                    No = dto.No
                    By = dto.SignedBy |> signerBack
                    SignedAt = dto.SignedAt
                }
            PatientId = dto.PatientId
            Base = dto.Base
            OrderContexts = dto.Plan.Contexts |> Array.map (OrderContextMapper.toModel demo)
            Patient = dto.Plan.Patient |> Patient.toModel
            Verified = dto.Verified
        }


    /// The patient data a notice is about, as the Dto; none when the reading could not be
    /// read.
    let noticeData (notice: DataNotice) = notice.Data |> Option.map Patient.ofModel


    let notice (token: string) (data: Informedica.GenForm.Lib.Patient.Dto.Dto option) : DataNotice =
        {
            Data = data |> Option.map Patient.toModel
            Token = token
        }


    /// The identity on the wire, beside the id the context carries: the name and the birthdate
    /// as three integers.
    let identity (id: Informedica.GenCore.Lib.Patients.PatientIdentity) : NameAndBirthDate =
        {
            Name = id.Name
            BirthYear = int id.BirthDate.Year
            BirthMonth = int id.BirthDate.Month
            BirthDay = int id.BirthDate.Day
        }


    /// What the client keeps of an open Session, from what the store holds: the head as the
    /// signed order plan on the wire when it can be read, none when it cannot, the identity
    /// when the EHR data has one, the patient data as the contract model with what the user
    /// measured on it; the demo flag on every context.
    let toOpened (demo: bool) (opened: OpenedSession) : SessionOpened =
        {
            User = opened.User
            PatientContext =
                opened.PatientId
                |> Option.map (fun id ->
                    {
                        PatientId = id
                        Identity =
                            opened.EhrData
                            |> Option.bind Informedica.GenForm.Lib.EhrPatientData.identity
                            |> Option.map identity
                        Patient =
                            opened.Patient
                            |> Option.map (
                                Informedica.GenForm.Lib.Patient.Dto.toDto
                                >> Patient.toModel
                                >> Measurements.apply opened.Measured
                            )
                    }
                )
            OpenedToken = opened.OpenedToken
            KeyThumbprint = opened.KeyThumbprint
            Head =
                opened.Head
                |> Option.bind (fun head ->
                    match head with
                    | StoredVersion.Readable v -> v |> OrderPlanVersion.Dto.toDto |> toSigned demo |> Some
                    | StoredVersion.Unreadable _ -> None
                )
        }
