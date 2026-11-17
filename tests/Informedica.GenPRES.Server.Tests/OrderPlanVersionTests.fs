/// The order plan version and the signer: the Dto of a version reads back as the version, in
/// canonical form, and reports what does not parse.
module Informedica.GenPRES.Server.Tests.OrderPlanVersionTests

open System
open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open ServerApi

module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types


module Fixtures =

    let child: GenForm.Patient =
        { Informedica.GenForm.Lib.Patient.patient with
            Department = Some "ICK"
            Gender = Male
            Age = Some(ValueUnit.singleWithUnit Units.Time.day 3650N)
            Weight = Some(ValueUnit.singleWithUnit Units.Weight.kiloGram 32N)
            Height = Some(ValueUnit.singleWithUnit Units.Height.centiMeter 140N)
            Access = [ PVL ]
        }

    let filter: GenOrder.Filter =
        {
            Indications = [| "koorts"; "pijn" |]
            Generics = [| "paracetamol" |]
            Routes = [| "rect" |]
            Forms = [| "zetpil" |]
            DoseTypes = [| GenForm.Discontinuous "3-4 x/dag" |]
            Diluents = [||]
            Components = [| "paracetamol" |]
            DoseRuleComponents = [||]
            Indication = Some "koorts"
            Generic = Some "paracetamol"
            Route = Some "rect"
            Form = Some "zetpil"
            DoseType = Some(GenForm.Discontinuous "3-4 x/dag")
            Diluent = None
            SelectedComponents = GenOrder.ComponentSelection.Selected [ "paracetamol" ]
            PickedProducts = [||]
        }

    let context: GenOrder.OrderContext =
        {
            Filter = filter
            Patient = child
            Scenarios = [||]
            Argumentation = None
        }

    let totals: GenOrder.Totals =
        {
            Volume = Some "100 ml"
            Energy = Some "50 kcal"
            Protein = None
            Carbohydrate = None
            Fat = None
            Sodium = Some "3 mmol"
            Potassium = None
            Chloride = None
            Calcium = None
            Phosphate = None
            Magnesium = None
            Iron = None
            VitaminD = None
            Ethanol = None
            Propyleenglycol = None
            BenzylAlcohol = None
            BoricAcid = None
        }

    let planContext: GenOrder.PlanContext =
        {
            Id = "ctx-1"
            Category = GenOrder.OrderCategory.Medication
            Context = context
            Totals = totals
            LinkedTo = None
        }

    let feeding: GenOrder.PlanContext =
        { planContext with
            Id = "ctx-2"
            Category = GenOrder.OrderCategory.Nutrition GenOrder.NutritionCategory.EnteralFeeding
        }

    let plan: GenOrder.OrderPlan =
        {
            Patient = child
            Filtered = [| "ctx-1" |]
            Contexts = [| planContext; feeding |]
            Totals = totals
        }

    let version: OrderPlanVersion =
        {
            Id = "v-1"
            No = 1
            PatientId = "stub-patient"
            Base = None
            SignedBy =
                {
                    UserId = "u-1"
                    DisplayName = "Stub Prescriber"
                }
            SignedAt = DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc)
            Plan = plan
            Verified = true
            EhrData = None
        }


let missing name = VersionDtoError.SignerOrPlan(GenOrder.DtoError.Missing name)

let toDto = OrderPlanVersion.Dto.toDto

let fromDto = OrderPlanVersion.Dto.fromDto

let read = Canonical.deserialize<OrderPlanVersion.Dto.Dto>


[<Tests>]
let tests =
    testList
        "Order plan version Dto"
        [
            test "a version reads back as the same value" {
                Fixtures.version
                |> toDto
                |> fromDto
                |> Expect.equal "the same value" (Ok Fixtures.version)
            }

            test "null nested Dtos are missing, null arrays empty, never a crash" {
                let dto = Fixtures.version |> toDto

                { dto with
                    SignedBy = Unchecked.defaultof<_>
                    Plan =
                        { dto.Plan with
                            Totals = Unchecked.defaultof<_>
                            Filtered = null
                        }
                }
                |> fromDto
                |> Expect.equal "both named" (Error [ missing "SignedBy"; missing "Totals" ])

                match
                    { dto with
                        Plan =
                            { dto.Plan with
                                Filtered = null
                                Contexts = null
                            }
                    }
                    |> fromDto
                with
                | Ok v ->
                    v.Plan.Filtered |> Expect.isEmpty "no filter"
                    v.Plan.Contexts |> Expect.isEmpty "no contexts"
                | Error e -> failtest $"expected a version, got %A{e}"
            }

            test "a version read back parses to the version that was written" {
                Fixtures.version
                |> toDto
                |> Canonical.serialize
                |> read
                |> fromDto
                // in canonical form: a local DateTime reads back as the same instant in UTC
                |> Result.map (toDto >> Canonical.serialize)
                |> Expect.equal "the same version" (Ok(Fixtures.version |> toDto |> Canonical.serialize))
            }

            test "a null root and a null signer are missing by their names, never a crash" {
                "null"
                |> read
                |> fromDto
                |> Expect.equal "a stored null" (Error [ missing "OrderPlanVersion" ])

                Unchecked.defaultof<Signer.Dto.Dto>
                |> Signer.Dto.fromDto
                |> Expect.equal "signer" (Error [ GenOrder.DtoError.Missing "Signer" ])
            }

            test "two versions equal as values digest equal, a re-ordered plan does not" {
                let a = Fixtures.version |> toDto |> Canonical.serialize

                // built apart from the fixture, from the same values
                let equal: OrderPlanVersion =
                    {
                        Id = "v-1"
                        No = 1
                        PatientId = "stub-patient"
                        Base = None
                        SignedBy =
                            {
                                UserId = "u-1"
                                DisplayName = "Stub Prescriber"
                            }
                        SignedAt = DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc)
                        Plan =
                            {
                                Patient = Fixtures.child
                                Filtered = [| "ctx-1" |]
                                Contexts = [| Fixtures.planContext; Fixtures.feeding |]
                                Totals = Fixtures.totals
                            }
                        Verified = true
                        EhrData = None
                    }

                let b =
                    { Fixtures.version with
                        Plan = { Fixtures.plan with Contexts = [| Fixtures.feeding; Fixtures.planContext |] }
                    }
                    |> toDto
                    |> Canonical.serialize

                equal |> toDto |> Canonical.serialize |> Expect.equal "equal" a

                a |> Expect.notEqual "a different order plan" b
            }

            test "EHR data that do not parse are an EHR data error, with the reason" {
                let ehr: EhrPatientData.Dto.Dto =
                    {
                        Patient = Informedica.GenCore.Lib.Patients.Patient.Dto.dto ()
                        RenalFunction = Some "no renal function"
                        Access = [||]
                    }

                match { (Fixtures.version |> toDto) with EhrData = Some ehr } |> fromDto with
                | Error es ->
                    es
                    |> List.exists (
                        function
                        | VersionDtoError.EhrData _ -> true
                        | VersionDtoError.SignerOrPlan _ -> false
                    )
                    |> Expect.isTrue "an EHR data error"
                | Ok _ -> failtest "expected an error"
            }
        ]
