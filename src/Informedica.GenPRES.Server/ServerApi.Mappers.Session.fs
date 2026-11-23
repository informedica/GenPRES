namespace ServerApi

open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types


/// Maps a nutrition category to its name in the "RuleSets" sheet and back.
module NutritionCategoryMapper =

    // Opened here, so that the case names refer to the GenORDER category and not the contract category.
    open Informedica.GenOrder.Lib.Types

    /// The name of a nutrition category in the "RuleSets" sheet.
    let toName =
        function
        | NutritionCategory.EnteralFeeding -> "EnteralFeeding"
        | NutritionCategory.EnteralSupplement -> "EnteralSupplement"
        | NutritionCategory.TPN -> "TPN"
        | NutritionCategory.Lipid -> "Lipid"
        | NutritionCategory.ElectrolyteGlucose -> "ElectrolyteGlucose"


    /// Returns the nutrition category of a name in the "RuleSets" sheet, or None for another name.
    let ofName name =
        NutritionRuleSet.categories |> Array.tryFind (toName >> (=) name)


/// Maps an order plan of the contract to the order plan Dto of GenORDER and back.
module OrderPlanMapper =

    /// Maps an order plan to the Dto.
    let ofModel (plan: OrderPlan) : OrderPlan.Dto.Dto =
        {
            PatientData = plan.PatientData |> Patient.ofModel
            Filtered = plan.Filtered
            Contexts = plan.OrderContexts |> Array.map OrderContextMapper.ofModel
            Totals = plan.Totals |> OrderContextMapper.totals
        }


    /// Maps an order plan Dto to an order plan with the demo flag of the server.
    let toModel (demo: bool) (dto: OrderPlan.Dto.Dto) : OrderPlan =
        {
            PatientData = dto.PatientData |> Patient.toModel
            Filtered = dto.Filtered
            OrderContexts = dto.Contexts |> Array.map (OrderContextMapper.toModel demo)
            Totals = dto.Totals |> OrderContextMapper.totalsBack
        }


    /// Returns the message for an order plan error.
    let rec words =
        function
        | OrderPlanError.NoSuchContext id -> $"The plan holds no context %s{id}"
        | OrderPlanError.NotNarrowed n -> $"Choose one order first: %i{n} candidates remain"
        | OrderPlanError.OrderHeld _ -> "The plan already holds this order"
        | OrderPlanError.NoRuleSet category ->
            $"RuleSets: no rule set for %s{category |> NutritionCategoryMapper.toName}"
        | OrderPlanError.BrokenLink e -> e |> OrderContextMapper.linkWords
        | OrderPlanError.OrderRefused(generic, e) -> $"%s{generic}: %s{words e}"


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


    /// Maps an open session to the session the client opens. The patient data gets the options of the resources.
    let toOpened (demo: bool) resources sessionPatientData (opened: OpenedSession) : SessionOpened =
        {
            User = opened.User
            PatientContext =
                opened.PatientId
                |> Option.map (fun id ->
                    {
                        PatientId = id
                        PatientIdentity = opened |> OpenedSession.identity |> Option.map identity
                        PatientData =
                            opened
                            |> sessionPatientData
                            |> Option.map (Patient.ofPatientData >> Patient.withOptions resources)
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
