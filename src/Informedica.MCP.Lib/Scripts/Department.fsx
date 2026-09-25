/// The MCP host's department input checked against the departments the loaded rules name:
/// a department the rules know is taken as they spell it, none leaves the server's default in
/// force, and one they do not know is refused with the names they do, before any rule is read.
///
/// Prototype for the migration into McpTools.GenOrder.fs: checkDepartment is new, and
/// evaluateOrderContext gains the check after the weight-and-height guard, so a call that lacks
/// a measure is still refused without touching the provider.
///
/// Run: cd src/Informedica.MCP.Lib/Scripts && dotnet fsi Department.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Newtonsoft.Json"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FParsec"
#r "nuget: FSharp.Data, 8.0"
#r "nuget: Expecto, 10.2.3"

#r "../bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenSOLVER.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenORDER.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.MCP.Lib.dll"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenOrder.Lib
open Informedica.MCP.Lib


module GenOrderTools =

    open Informedica.MCP.Lib.GenOrderTools


    /// The input with its department one the rules know, spelled as they spell it; none leaves
    /// the default in force. A department the rules do not know is refused with the names they
    /// do and the default, since the department selects the solution rules and reconstitutions,
    /// and a misspelt one would silently select none.
    let checkDepartment
        (departments: Departments)
        (input: CreateOrderContextInput)
        : Result<CreateOrderContextInput, string>
        =
        match input.Department with
        | None -> Ok input
        | Some d ->
            let known =
                departments.Names
                |> Array.tryFind (fun n -> String.Equals(n, d.Trim(), StringComparison.OrdinalIgnoreCase))

            match known with
            | Some n -> Ok { input with Department = Some n }
            | None ->
                let names = departments.Names |> String.concat ", "

                Error
                    $"Unknown department '{d}'. Known departments: {names}. \
                      Omit the department to prescribe for the default, {departments.Default}."


    /// The order context for the input's patient and filter selection, evaluated against the
    /// given provider. Requires both WeightKg and HeightCm (see requireWeightAndHeight), and
    /// then a department the rules know, if one is given (see checkDepartment); shared by
    /// createOrderContext and getOrderScenarios so the guards and the patient/filter/evaluate
    /// pipeline exist in exactly one place.
    let evaluateOrderContext
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderContext, string>
        =
        input
        |> requireWeightAndHeight
        |> Result.bind (fun () -> input |> checkDepartment (provider.Get Keys.departments))
        |> Result.map (fun input ->
            let patient = buildPatient provider input

            OrderContext.create OrderLogging.noOp provider patient
            |> (fun c ->
                match input.Generic with
                | Some g -> c |> OrderContext.setFilterGeneric g
                | None -> c
            )
            |> (fun c ->
                match input.Indication with
                | Some i -> c |> OrderContext.setFilterIndication i
                | None -> c
            )
            |> (fun c ->
                match input.Route with
                | Some r -> c |> OrderContext.setFilterRoute r
                | None -> c
            )
            |> (fun c ->
                match input.Form with
                | Some f -> c |> OrderContext.setFilterForm f
                | None -> c
            )
        )
        |> Result.bind (fun ctx ->
            OrderContext.UpdateOrderContext ctx
            |> OrderContext.evaluate DateTime.UtcNow OrderLogging.noOp provider
            |> Result.mapError (fun e -> $"Failed to evaluate order context: {e}")
        )
        |> Result.map OrderContext.Command.get


open Informedica.MCP.Lib.GenOrderTools
open GenOrderTools


/// The departments the tests prescribe for: two named by rules, the default among them.
let departments = Departments.ofNamed [ Some "NEO"; Some "ICC" ]


/// A provider that answers the departments and nothing else: a test that reaches any rule
/// raises, so a refusal proves that the guard ran before the rules were read.
let departmentsOnly: IResourceProvider =
    { new IResourceProvider with
        member _.Get(key: ResourceKey<'T>) : 'T =
            if key.Name = Keys.departments.Name then
                box departments :?> 'T
            else
                raise (NotImplementedException key.Name)

        member _.GetData() = raise (NotImplementedException())
        member _.GetUnitMappings() = raise (NotImplementedException())
        member _.GetRouteMappings() = raise (NotImplementedException())
        member _.GetValidForms() = raise (NotImplementedException())
        member _.GetFormRoutes() = raise (NotImplementedException())
        member _.GetFormularyProducts() = raise (NotImplementedException())
        member _.GetReconstitution() = raise (NotImplementedException())
        member _.GetParenteralMeds() = raise (NotImplementedException())
        member _.GetEnteralFeeding() = raise (NotImplementedException())
        member _.GetProducts() = raise (NotImplementedException())
        member _.GetDoseRules() = raise (NotImplementedException())
        member _.GetSolutionRules() = raise (NotImplementedException())
        member _.GetRenalRules() = raise (NotImplementedException())
        member _.GetTotals() = raise (NotImplementedException())
        member _.GetGStandProvider() = raise (NotImplementedException())
        member _.GetResourceInfo() = raise (NotImplementedException())
    }


/// A provider that must never be called.
let unusedProvider: IResourceProvider = Unchecked.defaultof<_>


let measured: CreateOrderContextInput =
    {
        AgeMonths = Some 24.0
        WeightKg = Some 12.0
        HeightCm = Some 86.0
        Sex = None
        Department = None
        Generic = Some "paracetamol"
        Indication = None
        Route = None
        Form = None
    }


let tests =
    testList
        "checkDepartment"
        [
            test "the names are the ones the rules name and the default, sorted" {
                departments.Names |> Expect.equal "the names" [| "ICC"; "ICK"; "NEO" |]
            }

            test "no department is Ok and stays none, so the default applies" {
                measured
                |> checkDepartment departments
                |> Expect.equal "unchanged" (Ok measured)
            }

            test "a department the rules name is Ok as given" {
                { measured with Department = Some "NEO" }
                |> checkDepartment departments
                |> Result.map _.Department
                |> Expect.equal "NEO" (Ok(Some "NEO"))
            }

            test "a department in another case is Ok as the rules spell it" {
                { measured with Department = Some "neo" }
                |> checkDepartment departments
                |> Result.map _.Department
                |> Expect.equal "NEO" (Ok(Some "NEO"))
            }

            test "a department with spaces around it is Ok trimmed" {
                { measured with Department = Some " icc " }
                |> checkDepartment departments
                |> Result.map _.Department
                |> Expect.equal "ICC" (Ok(Some "ICC"))
            }

            test "the default itself is Ok as given" {
                { measured with Department = Some "ICK" }
                |> checkDepartment departments
                |> Result.map _.Department
                |> Expect.equal "ICK" (Ok(Some "ICK"))
            }

            test "a department the rules do not name is refused, naming the ones they do" {
                match { measured with Department = Some "PICU" } |> checkDepartment departments with
                | Ok _ -> failtest "should refuse"
                | Error msg ->
                    msg |> Expect.stringContains "names the input" "'PICU'"
                    msg |> Expect.stringContains "names the known" "ICC, ICK, NEO"
                    msg |> Expect.stringContains "names the default" "ICK"
            }

            test "an empty department is refused, not taken as none" {
                { measured with Department = Some "" }
                |> checkDepartment departments
                |> Expect.isError "should refuse"
            }

            test "the checked department is the one the patient is built with" {
                { measured with Department = Some "neo" }
                |> checkDepartment departments
                |> Result.map (buildPatient departmentsOnly >> Patient.getDepartment)
                |> Expect.equal "NEO on the patient" (Ok(Some "NEO"))
            }

            test "evaluateOrderContext refuses an unknown department before reading any rule" {
                { measured with Department = Some "PICU" }
                |> evaluateOrderContext departmentsOnly
                |> Expect.isError "should refuse without evaluating"
            }

            test "evaluateOrderContext refuses a missing measure before touching the provider" {
                { measured with
                    HeightCm = None
                    Department = Some "PICU"
                }
                |> evaluateOrderContext unusedProvider
                |> Result.mapError (fun msg -> msg.Contains "HeightCm")
                |> Expect.equal "the measure, not the department" (Error true)
            }
        ]


runTestsWithCLIArgs [] [||] tests
