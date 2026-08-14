namespace Informedica.GenPRES.Server.Tests


open Expecto
open Expecto.Flip

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources


module Helpers =


    let errMsg s : Message = ErrorMsg(s, None)


    /// Stub registry: every resource resolves to a typed empty value. The typed
    /// empties matter — the boxed value's runtime type must match the key's `'T`
    /// for the engine's downcast to succeed.
    let okRegistry: ResourceRegistry =
        Map
            [
                Keys.unitMappings.Name, ofResult (fun () -> Ok([||]: UnitMapping[]))
                Keys.routeMappings.Name, ofResult (fun () -> Ok([||]: RouteMapping[]))
                Keys.validForms.Name, ofResult (fun () -> Ok([||]: string[]))
                Keys.formRoutes.Name, ofResult (fun () -> Ok([||]: FormRoute[]))
                Keys.formularyProducts.Name, ofResult (fun () -> Ok([||]: FormularyProduct[]))
                Keys.genPresProducts.Name, ofResult (fun () -> Ok([||]: Informedica.ZIndex.Lib.Types.GenPresProduct[]))
                Keys.reconstitution.Name, ofResult (fun () -> Ok([||]: Reconstitution[]))
                Keys.parenteralMeds.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
                Keys.enteralFeeding.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
                Keys.doseRuleData.Name, ofResult (fun () -> Ok([||]: DoseRuleData[]))
                Keys.solutionRuleData.Name, ofResult (fun () -> Ok([||]: SolutionRuleData[]))
                Keys.renalRuleData.Name, ofResult (fun () -> Ok([||]: RenalRuleData[]))
                Keys.totalsData.Name, ofResult (fun () -> Ok([||]: TotalsData[]))
                Keys.products.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
                Keys.doseRules.Name, ofResult (fun () -> Ok([||]: DoseRule[]))
                Keys.solutionRules.Name, ofResult (fun () -> Ok([||]: SolutionRule[]))
                Keys.renalRules.Name, ofResult (fun () -> Ok([||]: RenalRule[]))
                Keys.gStandProvider.Name, derive (fun r -> Check.gStandProvider (r.Get Keys.routeMappings))
            ]


    let emptyFormulary: Shared.Types.Formulary =
        {
            Generics = [||]
            Indications = [||]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
            PatientCategories = [||]
            Products = [||]
            Generic = None
            Indication = None
            Route = None
            Form = None
            DoseType = None
            PatientCategory = None
            Patient = None
            Markdown = ""
            DoseCheck = [||]
        }


    let emptyParenteralia: Shared.Types.Parenteralia =
        {
            Generics = [||]
            Forms = [||]
            Routes = [||]
            PatientCategories = [||]
            Generic = None
            Form = None
            Route = None
            PatientCategory = None
            Markdown = ""
        }


module ResourceErrorTests =


    open Helpers


    let errorPropagationTests =
        testList
            "loadAllResourcesWithRegistry error propagation"
            [

                test "leaf loader (unitMappings) returns Error propagates" {
                    okRegistry
                    |> Map.add Keys.unitMappings.Name (fun _ -> Error [ errMsg "unit mapping failed" ])
                    |> loadAllResourcesWithRegistry
                    |> Result.isError
                    |> Expect.isTrue "should be Error"
                }

                test "dependent loader (formRoutes) returns Error propagates" {
                    okRegistry
                    |> Map.add Keys.formRoutes.Name (fun _ -> Error [ errMsg "form routes failed" ])
                    |> loadAllResourcesWithRegistry
                    |> Result.isError
                    |> Expect.isTrue "should be Error"
                }

                test "derived loader (renalRules) returns Error propagates" {
                    okRegistry
                    |> Map.add Keys.renalRules.Name (fun _ -> Error [ errMsg "renal rules failed" ])
                    |> loadAllResourcesWithRegistry
                    |> Result.isError
                    |> Expect.isTrue "should be Error"
                }

                test "loader throws exception is caught and returned as Error" {
                    let result =
                        okRegistry
                        |> Map.add Keys.unitMappings.Name (fun _ -> failwith "unexpected crash")
                        |> loadAllResourcesWithRegistry

                    result |> Result.isError |> Expect.isTrue "should be Error"

                    match result with
                    | Error msgs ->
                        msgs
                        |> List.exists (fun m ->
                            match m with
                            | ErrorMsg(s, _) -> s.Contains("Failed to load resources")
                            | _ -> false
                        )
                        |> Expect.isTrue "should contain 'Failed to load resources' message"
                    | Ok _ -> failwith "expected Error"
                }
            ]


    let successPathTests =
        testList
            "loadAllResourcesWithRegistry success path"
            [

                test "all loaders succeed returns Ok with IsLoaded = true" {
                    let result = okRegistry |> loadAllResourcesWithRegistry

                    result |> Result.isOk |> Expect.isTrue "should be Ok"

                    match result with
                    | Ok loaded ->
                        loaded.State.IsLoaded |> Expect.isTrue "IsLoaded should be true"

                        loaded.State.Messages |> Expect.equal "Messages should be empty" [||]
                    | Error _ -> failwith "expected Ok"
                }
            ]


    let cachedProviderErrorStateTests =
        testList
            "CachedResourceProvider error state"
            [

                test "loader returns Error, GetResourceInfo shows IsLoaded = false" {
                    let provider =
                        CachedResourceProvider((fun () -> Error [ errMsg "load failed" ]), None)

                    let info = (provider :> IResourceProvider).GetResourceInfo()

                    info.IsLoaded |> Expect.isFalse "IsLoaded should be false"

                    info.Messages |> Array.isEmpty |> Expect.isFalse "Messages should not be empty"
                }

                test "all resource getters return empty arrays when loader failed" {
                    let provider =
                        CachedResourceProvider((fun () -> Error [ errMsg "load failed" ]), None)

                    (provider :> IResourceProvider).GetUnitMappings()
                    |> Expect.equal "UnitMappings should be empty" [||]

                    (provider :> IResourceProvider).GetDoseRules()
                    |> Expect.equal "DoseRules should be empty" [||]

                    (provider :> IResourceProvider).GetProducts()
                    |> Expect.equal "Products should be empty" [||]

                    (provider :> IResourceProvider).GetRenalRules()
                    |> Expect.equal "RenalRules should be empty" [||]
                }
            ]


    let cachingBehaviorTests =
        testList
            "CachedResourceProvider caching behavior"
            [

                test "after error, second call does NOT re-invoke loader" {
                    let mutable callCount = 0

                    let provider =
                        CachedResourceProvider(
                            (fun () ->
                                callCount <- callCount + 1
                                Error [ errMsg "load failed" ]
                            ),
                            None
                        )

                    (provider :> IResourceProvider).GetResourceInfo() |> ignore

                    callCount |> Expect.equal "loader should be called once" 1

                    (provider :> IResourceProvider).GetResourceInfo() |> ignore

                    callCount |> Expect.equal "loader should still be called once" 1
                }

                test "ReloadCache re-invokes loader" {
                    let mutable callCount = 0

                    let provider =
                        CachedResourceProvider(
                            (fun () ->
                                callCount <- callCount + 1
                                Error [ errMsg "load failed" ]
                            ),
                            None
                        )

                    (provider :> IResourceProvider).GetResourceInfo() |> ignore

                    callCount |> Expect.equal "loader called once after first access" 1

                    provider.ReloadCache()

                    callCount |> Expect.equal "loader called twice after ReloadCache" 2
                }

                test "loader fails first then succeeds, after ReloadCache IsLoaded = true" {
                    let mutable callCount = 0

                    let provider =
                        CachedResourceProvider(
                            (fun () ->
                                callCount <- callCount + 1

                                if callCount = 1 then
                                    Error [ errMsg "first attempt failed" ]
                                else
                                    okRegistry |> loadAllResourcesWithRegistry
                            ),
                            None
                        )

                    let info1 = (provider :> IResourceProvider).GetResourceInfo()

                    info1.IsLoaded |> Expect.isFalse "should not be loaded after first attempt"

                    provider.ReloadCache()

                    let info2 = (provider :> IResourceProvider).GetResourceInfo()

                    info2.IsLoaded |> Expect.isTrue "should be loaded after ReloadCache"
                }
            ]


    let processCmdGuardTests =
        testList
            "processCmd IsLoaded guard"
            [

                test "FormularyCmd returns Error when provider IsLoaded = false" {
                    let provider =
                        CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                    let cmd = Shared.Api.FormularyCmd emptyFormulary

                    let result =
                        ServerApi.Command.processCmd (ServerApi.Adapters.makeAppEnv provider) cmd
                        |> Async.RunSynchronously

                    result
                    |> Result.isError
                    |> Expect.isTrue "should return Error for FormularyCmd when not loaded"
                }

                test "ParenteraliaCmd returns Error when provider IsLoaded = false" {
                    let provider =
                        CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                    let cmd = Shared.Api.ParenteraliaCmd emptyParenteralia

                    let result =
                        ServerApi.Command.processCmd (ServerApi.Adapters.makeAppEnv provider) cmd
                        |> Async.RunSynchronously

                    result
                    |> Result.isError
                    |> Expect.isTrue "should return Error for ParenteraliaCmd when not loaded"
                }
            ]


    let agentAdapterGuardTests =
        testList
            "processCmd IsLoaded guard (AgentAdapters)"
            [

                test "FormularyCmd returns Error when provider IsLoaded = false (agent)" {
                    let provider =
                        CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                    let cmd = Shared.Api.FormularyCmd emptyFormulary

                    let result =
                        ServerApi.Command.processCmd (ServerApi.AgentAdapters.makeAppEnv provider) cmd
                        |> Async.RunSynchronously

                    result
                    |> Result.isError
                    |> Expect.isTrue "should return Error for FormularyCmd when not loaded (agent)"
                }

                test "ParenteraliaCmd returns Error when provider IsLoaded = false (agent)" {
                    let provider =
                        CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                    let cmd = Shared.Api.ParenteraliaCmd emptyParenteralia

                    let result =
                        ServerApi.Command.processCmd (ServerApi.AgentAdapters.makeAppEnv provider) cmd
                        |> Async.RunSynchronously

                    result
                    |> Result.isError
                    |> Expect.isTrue "should return Error for ParenteraliaCmd when not loaded (agent)"
                }
            ]


    [<Tests>]
    let tests =
        testList
            "Resource Error Handling Tests"
            [
                errorPropagationTests
                successPathTests
                cachedProviderErrorStateTests
                cachingBehaviorTests
                processCmdGuardTests
                agentAdapterGuardTests
            ]


module StubAdapterTests =

    open Shared
    open Shared.Types
    open ServerApi


    /// Stub adapters for isolated application-layer testing.
    /// No IResourceProvider, no network, no data loading.
    module StubAdapters =

        let private notStubbed _ = failwith "not stubbed"


        let formularyAlwaysOk (returnForm: Formulary) : FormularyPort =
            {
                getFormulary = fun _ -> async { return Ok returnForm }
                getParenteralia = fun _ -> async { return Ok Helpers.emptyParenteralia }
            }


        let formularyAlwaysFails (msgs: string[]) : FormularyPort =
            {
                getFormulary = fun _ -> async { return Error msgs }
                getParenteralia = fun _ -> async { return Error msgs }
            }


        let orderContextAlwaysOk (returnCtx: OrderContext) : OrderContextPort =
            { evaluate = fun _ _ -> async { return Ok returnCtx } }


        let orderContextAlwaysFails (msgs: string[]) : OrderContextPort =
            { evaluate = fun _ _ -> async { return Error msgs } }


        let orderPlanAlwaysOk (returnPlan: OrderPlan) : OrderPlanPort =
            {
                updateOrderPlan = fun _ _ -> async { return Ok returnPlan }
                filterOrderPlan = fun _ -> async { return Ok returnPlan }
            }


        let nutritionPlanAlwaysOk (returnPlan: NutritionPlan) : NutritionPlanPort =
            {
                initNutritionPlan = fun _ -> async { return Ok returnPlan }
                addNutritionContext = fun _ -> async { return Ok returnPlan }
                removeNutritionContext = fun _ -> async { return Ok returnPlan }
                updateNutritionOrderContext = fun _ -> async { return Ok returnPlan }
                selectNutritionOrderScenario = fun _ -> async { return Ok returnPlan }
                navigateNutritionOrderContext = fun _ -> async { return Ok returnPlan }
            }


        let makeEnv
            (formulary: FormularyPort)
            (orderContext: OrderContextPort)
            (orderPlan: OrderPlanPort)
            (nutritionPlan: NutritionPlanPort)
            : AppEnv
            =
            {
                formulary = formulary
                orderContext = orderContext
                orderPlan = orderPlan
                nutritionPlan = nutritionPlan
                interaction =
                    {
                        checkInteractions = fun _ -> async { return Ok [] }
                        getDrugNames = fun () -> async { return Ok [] }
                    }
                logAnalyzer =
                    {
                        listLogFiles = fun () -> async { return Ok [||] }
                        analyzeLogFile = fun _ -> async { return Ok "" }
                    }
                requireLoaded = fun () -> None
            }


        let makeEnvNotLoaded (msgs: string[]) : AppEnv =
            {
                formulary = formularyAlwaysFails [| "not loaded" |]
                orderContext = orderContextAlwaysFails [| "not loaded" |]
                orderPlan =
                    {
                        updateOrderPlan = fun _ _ -> async { return Error [| "not loaded" |] }
                        filterOrderPlan = fun _ -> async { return Error [| "not loaded" |] }
                    }
                nutritionPlan =
                    {
                        initNutritionPlan = fun _ -> async { return Error [| "not loaded" |] }
                        addNutritionContext = fun _ -> async { return Error [| "not loaded" |] }
                        removeNutritionContext = fun _ -> async { return Error [| "not loaded" |] }
                        updateNutritionOrderContext = fun _ -> async { return Error [| "not loaded" |] }
                        selectNutritionOrderScenario = fun _ -> async { return Error [| "not loaded" |] }
                        navigateNutritionOrderContext = fun _ -> async { return Error [| "not loaded" |] }
                    }
                interaction =
                    {
                        checkInteractions = fun _ -> async { return Error [| "not loaded" |] }
                        getDrugNames = fun () -> async { return Error [| "not loaded" |] }
                    }
                logAnalyzer =
                    {
                        listLogFiles = fun () -> async { return Ok [||] }
                        analyzeLogFile = fun _ -> async { return Ok "" }
                    }
                requireLoaded = fun () -> Some msgs
            }


    open StubAdapters

    let emptyCtx = Models.OrderContext.empty

    // Copied to TotalsTests. One more copy/paste, and by the rule of three we should consider deduplication.
    // An OrderPlan.empty value seems reasonable.
    let emptyPlan: OrderPlan =
        {
            Patient = Models.Patient.empty
            Scenarios = [||]
            Selected = None
            Filtered = [||]
            Totals = Models.Totals.empty
        }

    let emptyNutritionPlan = Models.NutritionPlan.create Models.Patient.empty [||]


    let commandRoutingTests =
        testList
            "Stub adapter command routing"
            [

                testAsync "FormularyCmd dispatches to formulary.getFormulary" {
                    let returnForm = { Helpers.emptyFormulary with Markdown = "stubbed" }

                    let env =
                        makeEnv
                            (formularyAlwaysOk returnForm)
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                    match result with
                    | Ok(Api.FormularyResp f) -> f.Markdown |> Expect.equal "should return stubbed formulary" "stubbed"
                    | other -> failtest $"expected Ok FormularyResp, got {other}"
                }

                testAsync "ParenteraliaCmd dispatches to formulary.getParenteralia" {
                    let env =
                        makeEnv
                            (formularyAlwaysOk Helpers.emptyFormulary)
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.ParenteraliaCmd Helpers.emptyParenteralia)

                    match result with
                    | Ok(Api.ParenteraliaResp _) -> ()
                    | other -> failtest $"expected Ok ParenteraliaResp, got {other}"
                }

                testAsync "OrderContextCmd dispatches to orderContext.evaluate" {
                    let env =
                        makeEnv
                            (formularyAlwaysOk Helpers.emptyFormulary)
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.OrderContextCmd(Api.UpdateOrderContext, emptyCtx))

                    match result with
                    | Ok(Api.OrderContextResp(Api.OrderContextResult _)) -> ()
                    | other -> failtest $"expected Ok OrderContextResp, got {other}"
                }

                testAsync "NutritionPlanCmd InitNutritionPlan dispatches to nutritionPlan port" {
                    let env =
                        makeEnv
                            (formularyAlwaysOk Helpers.emptyFormulary)
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result =
                        Command.processCmd env (Api.NutritionPlanCmd(Api.InitNutritionPlan Models.Patient.empty))

                    match result with
                    | Ok(Api.NutritionPlanResp(Api.NutritionPlanInitialised _)) -> ()
                    | other -> failtest $"expected Ok NutritionPlanInitialised, got {other}"
                }

                testAsync "OrderPlanCmd FilterOrderPlan dispatches to orderPlan port" {
                    let env =
                        makeEnv
                            (formularyAlwaysOk Helpers.emptyFormulary)
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.OrderPlanCmd(Api.FilterOrderPlan emptyPlan))

                    match result with
                    | Ok(Api.OrderPlanResp(Api.OrderPlanFiltered _)) -> ()
                    | other -> failtest $"expected Ok OrderPlanFiltered, got {other}"
                }
            ]


    let errorPropagationTests =
        testList
            "Stub adapter error propagation"
            [

                testAsync "FormularyCmd propagates port error" {
                    let env =
                        makeEnv
                            (formularyAlwaysFails [| "test error" |])
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                    match result with
                    | Error msgs -> msgs |> Expect.equal "should propagate error messages" [| "test error" |]
                    | Ok _ -> failtest "expected Error, got Ok"
                }

                testAsync "OrderContextCmd propagates port error" {
                    let env =
                        makeEnv
                            (formularyAlwaysOk Helpers.emptyFormulary)
                            (orderContextAlwaysFails [| "ctx error" |])
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.OrderContextCmd(Api.UpdateOrderContext, emptyCtx))

                    match result with
                    | Error msgs -> msgs |> Expect.equal "should propagate order context error" [| "ctx error" |]
                    | Ok _ -> failtest "expected Error, got Ok"
                }
            ]


    let requireLoadedTests =
        testList
            "Stub adapter requireLoaded guard"
            [

                testAsync "requireLoaded returns Error when not loaded" {
                    let env = makeEnvNotLoaded [| "not ready" |]

                    let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                    match result with
                    | Error msgs -> msgs |> Expect.equal "should return requireLoaded error" [| "not ready" |]
                    | Ok _ -> failtest "expected Error from requireLoaded, got Ok"
                }

                testAsync "requireLoaded passes when loaded" {
                    let env =
                        makeEnv
                            (formularyAlwaysOk Helpers.emptyFormulary)
                            (orderContextAlwaysOk emptyCtx)
                            (orderPlanAlwaysOk emptyPlan)
                            (nutritionPlanAlwaysOk emptyNutritionPlan)

                    let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                    match result with
                    | Ok _ -> ()
                    | Error msgs -> failtest $"expected Ok, got Error {msgs}"
                }
            ]


    [<Tests>]
    let tests =
        testList
            "Stub Adapter Tests"
            [
                commandRoutingTests
                errorPropagationTests
                requireLoadedTests
            ]


module DoseCheckTests =

    open Shared.Types
    open ServerApi.FormularyService

    module Check = Check


    /// Minimal TextItem parser used by the build function under test.
    /// Avoids pulling in the Mappers module-level formatting state.
    let parseTextItem (s: string) =
        if System.String.IsNullOrWhiteSpace s then
            [||]
        else
            [| Normal s |]


    let tab (target: string) (route: string) (pat: string) (msg: string) =
        $"%s{target}\t%s{route}\t%s{pat}\t%s{msg}"


    let ctorName =
        function
        | Valid _ -> "Valid"
        | Caution _ -> "Caution"
        | Warning _ -> "Warning"
        | Alert _ -> "Alert"


    /// A graded dose-check signal (Severity, raw tab-separated line).
    let sigOf sev target route pat msg = sev, tab target route pat msg


    let doseCheckTests =
        testList
            "DoseCheck.build severity classification"
            [

                test "no check lines → single Valid 'Ok!'" {
                    let result = [||] |> DoseCheck.build parseTextItem true

                    result.Length |> Expect.equal "one block" 1
                    result[0] |> ctorName |> Expect.equal "Valid" "Valid"
                }

                test "only 'geen doseer bewaking' sentinel → Caution (blue info)" {
                    let sentinel = Check.NoMonitoring, "geen doseer bewaking gevonden voor paracetamol"

                    let result = [| sentinel |] |> DoseCheck.build parseTextItem true

                    result
                    |> Array.forall (fun tb -> ctorName tb = "Caution")
                    |> Expect.isTrue "sentinel signals 'no rules to check', must be Caution not Valid"
                }

                test "multiple 'geen doseer bewaking' sentinels → all Caution" {
                    let lines =
                        [|
                            Check.NoMonitoring, "geen doseer bewaking gevonden voor aciclovir"
                            Check.NoMonitoring, "geen doseer bewaking gevonden voor paracetamol"
                        |]

                    let result = lines |> DoseCheck.build parseTextItem false

                    result.Length |> Expect.equal "two blocks" 2

                    result
                    |> Array.forall (fun tb -> ctorName tb = "Caution")
                    |> Expect.isTrue "both Caution"
                }

                test "frequency mismatch → Warning" {
                    let lines =
                        [|
                            sigOf Check.FrequencyMismatch "paracetamol" "oraal" "0-1 jaar" "frequenties tekst 24"
                        |]

                    let result = lines |> DoseCheck.build parseTextItem false

                    result
                    |> Array.forall (fun tb -> ctorName tb = "Warning")
                    |> Expect.isTrue "all Warning"
                }

                test "advisory norm-max breach → Warning (orange)" {
                    let lines =
                        [|
                            sigOf Check.AdvisoryOverNorm "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"
                        |]

                    let result = lines |> DoseCheck.build parseTextItem true

                    result
                    |> Array.forall (fun tb -> ctorName tb = "Warning")
                    |> Expect.isTrue "advisory is orange, not red"
                }

                test "absolute-max breach → Alert (red)" {
                    let lines =
                        [|
                            sigOf Check.OverAbsolute "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"
                        |]

                    let result = lines |> DoseCheck.build parseTextItem true

                    result
                    |> Array.forall (fun tb -> ctorName tb = "Alert")
                    |> Expect.isTrue "all Alert"
                }

                test "unit mismatch → Caution (blue)" {
                    let lines =
                        [|
                            sigOf Check.UnitMismatch "paracetamol" "oraal" "0-1 jaar" "eenheden verschillen (kg vs m2)"
                        |]

                    let result = lines |> DoseCheck.build parseTextItem true

                    result
                    |> Array.forall (fun tb -> ctorName tb = "Caution")
                    |> Expect.isTrue "all Caution"
                }

                test "mixed advisory + absolute → graded per line (Warning and Alert)" {
                    let lines =
                        [|
                            sigOf Check.AdvisoryOverNorm "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"
                            sigOf Check.OverAbsolute "paracetamol" "oraal" "0-1 jaar" "dosering per kg niet in bereik"
                        |]

                    let result = lines |> DoseCheck.build parseTextItem false

                    result
                    |> Array.map ctorName
                    |> Array.sort
                    |> Expect.equal "one Warning, one Alert" [| "Alert"; "Warning" |]
                }

                test "violation alongside sentinel → sentinel dropped" {
                    let sentinel = Check.NoMonitoring, "geen doseer bewaking gevonden voor paracetamol"

                    let breach =
                        sigOf Check.OverAbsolute "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"

                    let result = [| sentinel; breach |] |> DoseCheck.build parseTextItem false

                    result.Length |> Expect.equal "sentinel dropped, one block left" 1
                    result[0] |> ctorName |> Expect.equal "Alert" "Alert"
                }

                test "isFrequency detects 'frequenties' in the 4th tab field" {
                    let freqLine = tab "x" "y" "z" "frequenties 4 x per dag niet gelijk aan 6 x per dag"

                    let doseLine = tab "x" "y" "z" "keer dosering per dag niet in bereik"

                    DoseCheck.isFrequency freqLine |> Expect.isTrue "frequency"
                    DoseCheck.isFrequency doseLine |> Expect.isFalse "not frequency"
                }
            ]


    [<Tests>]
    let tests = testList "DoseCheck Tests" [ doseCheckTests ]

module TotalsTests =
    open System
    open Shared
    open Shared.Types
    open Swensen.Unquote

    type TotalsSpy() =
        let mutable callCount = 0
        member this.CallCount = callCount

        interface Resources.IResourceProvider with
            member _.Get _ = raise (NotImplementedException())
            member _.GetData() = raise (NotImplementedException())
            member _.GetDoseRules() = raise (NotImplementedException())
            member _.GetEnteralFeeding() = raise (NotImplementedException())
            member _.GetFormRoutes() = raise (NotImplementedException())
            member _.GetFormularyProducts() = raise (NotImplementedException())
            member _.GetGStandProvider() = raise (NotImplementedException())
            member _.GetParenteralMeds() = raise (NotImplementedException())
            member _.GetProducts() = raise (NotImplementedException())
            member _.GetReconstitution() = raise (NotImplementedException())
            member _.GetRenalRules() = raise (NotImplementedException())
            member _.GetResourceInfo() = raise (NotImplementedException())
            member _.GetRouteMappings() = raise (NotImplementedException())
            member _.GetSolutionRules() = raise (NotImplementedException())

            member _.GetTotals() =
                callCount <- callCount + 1
                [||]

            member _.GetUnitMappings() = raise (NotImplementedException())
            member _.GetValidForms() = raise (NotImplementedException())

    // Copied from StubAdapterTests. One more copy/paste, and by the rule of three we should consider deduplication.
    // An OrderPlan.empty value seems reasonable.
    let emptyPlan: OrderPlan =
        {
            Patient = Models.Patient.empty
            Scenarios = [||]
            Selected = None
            Filtered = [||]
            Totals = Models.Totals.empty
        }

    [<Tests>]
    let tests =
        testList
            "Totals Tests"
            [
                testAsync "updateOrderPlan doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ = sut.orderPlan.updateOrderPlan emptyPlan None

                    let countAfter = spy.CallCount
                    countBefore <! countAfter
                }

                testAsync "filterOrderPlan doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ = sut.orderPlan.filterOrderPlan emptyPlan

                    let countAfter = spy.CallCount
                    countBefore <! countAfter
                }
            ]
