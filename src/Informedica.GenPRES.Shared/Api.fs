namespace Shared


module Api =


    open Types


    type Command =
        | OrderContextCmd of OrderContextCommand * OrderContext
        | OrderPlanCmd of OrderPlanCommand
        | FormularyCmd of Formulary
        | ParenteraliaCmd of Parenteralia
        | NutritionPlanCmd of NutritionPlanCommand
        | InteractionCmd of InteractionCommand
        | LogAnalyzerCmd of LogAnalyzerCommand

    and OrderContextCommand =
        | UpdateOrderContext
        | SelectOrderScenario
        | UpdateOrderScenario
        | ResetOrderScenario
        | ReloadResources of password: string
        // Frequency property commands
        | DecreaseScheduleFrequencyProperty
        | IncreaseScheduleFrequencyProperty
        | SetMinScheduleFrequencyProperty
        | SetMaxScheduleFrequencyProperty
        | SetMedianScheduleFrequencyProperty
        // DoseQuantity property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseQuantityProperty of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseQuantityProperty of ntimes: int * useCalc: bool
        | SetMinOrderableDoseQuantityProperty
        | SetMaxOrderableDoseQuantityProperty
        | SetMedianOrderableDoseQuantityProperty
        // DoseRate property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseRateProperty of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseRateProperty of ntimes: int * useCalc: bool
        | SetMinOrderableDoseRateProperty
        | SetMaxOrderableDoseRateProperty
        | SetMedianOrderableDoseRateProperty
        // Component Quantity property commands (cmp = component, ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseComponentOrderableQuantityProperty of cmp: string * ntimes: int * useCalc: bool
        | IncreaseComponentOrderableQuantityProperty of cmp: string * ntimes: int * useCalc: bool
        | SetMinComponentOrderableQuantityProperty of cmp: string
        | SetMaxComponentOrderableQuantityProperty of cmp: string
        | SetMedianComponentOrderableQuantityProperty of cmp: string

    and OrderPlanCommand =
        | UpdateOrderPlan of OrderPlan * (OrderContextCommand * OrderContext) option
        | FilterOrderPlan of OrderPlan

    and NutritionPlanCommand =
        | InitNutritionPlan of Patient
        | UpdateNutritionOrderContext of NutritionPlan * string * OrderContext
        | SelectNutritionOrderScenario of NutritionPlan * string * OrderContext
        | NavigateNutritionOrderContext of NutritionPlan * string * OrderContextCommand * OrderContext
        | AddNutritionContext of NutritionPlan * NutritionCategory
        | RemoveNutritionContext of NutritionPlan * string

    and LogAnalyzerCommand =
        | ValidatePassword of password: string
        | ListLogFiles of token: string
        | AnalyzeLogFile of token: string * fileName: string

    and InteractionCommand =
        | CheckInteractions of string list
        | GetDrugNames

    type Response =
        | OrderContextResp of OrderContextResponse
        | OrderPlanResp of OrderPlanResponse
        | FormularyResp of Formulary
        | ParenteraliaResp of Parenteralia
        | NutritionPlanResp of NutritionPlanResponse
        | InteractionResp of InteractionResponse
        | LogAnalyzerResp of LogAnalyzerResponse

    and OrderContextResponse = OrderContextResult of OrderContext

    and OrderPlanResponse =
        | OrderPlanFiltered of OrderPlan
        | OrderPlanUpdated of OrderPlan

    and NutritionPlanResponse =
        | NutritionPlanInitialised of NutritionPlan
        | NutritionPlanUpdated of NutritionPlan

    and LogAnalyzerResponse =
        | PasswordValidated of isValid: bool * token: string
        | LogFilesListed of LogFileInfo[]
        | LogFileAnalyzed of string

    and InteractionResponse =
        | InteractionsChecked of DrugInteraction[]
        | DrugNamesLoaded of string[]


    module Command =

        let toString =
            function
            | OrderContextCmd(UpdateOrderContext, _) -> "UpdateOrderContext"
            | OrderContextCmd(SelectOrderScenario, _) -> "SelectOrderScenario"
            | OrderContextCmd(UpdateOrderScenario, _) -> "UpdateOrderScenario"
            | OrderContextCmd(ResetOrderScenario, _) -> "ResetOrderScenario"
            | OrderContextCmd(ReloadResources _, _) -> "ReloadResources"
            | OrderContextCmd(DecreaseScheduleFrequencyProperty, _) -> "DecreaseScheduleFrequencyProperty"
            | OrderContextCmd(IncreaseScheduleFrequencyProperty, _) -> "IncreaseScheduleFrequencyProperty"
            | OrderContextCmd(SetMinScheduleFrequencyProperty, _) -> "SetMinScheduleFrequencyProperty"
            | OrderContextCmd(SetMaxScheduleFrequencyProperty, _) -> "SetMaxScheduleFrequencyProperty"
            | OrderContextCmd(SetMedianScheduleFrequencyProperty, _) -> "SetMedianScheduleFrequencyProperty"
            | OrderContextCmd(DecreaseOrderableDoseQuantityProperty(ntimes, useCalc), _) ->
                $"DecreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(IncreaseOrderableDoseQuantityProperty(ntimes, useCalc), _) ->
                $"IncreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(SetMinOrderableDoseQuantityProperty, _) -> "SetMinOrderableDoseQuantityProperty"
            | OrderContextCmd(SetMaxOrderableDoseQuantityProperty, _) -> "SetMaxOrderableDoseQuantityProperty"
            | OrderContextCmd(SetMedianOrderableDoseQuantityProperty, _) -> "SetMedianOrderableDoseQuantityProperty"
            | OrderContextCmd(DecreaseOrderableDoseRateProperty(ntimes, useCalc), _) ->
                $"DecreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(IncreaseOrderableDoseRateProperty(ntimes, useCalc), _) ->
                $"IncreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(SetMinOrderableDoseRateProperty, _) -> "SetMinOrderableDoseRateProperty"
            | OrderContextCmd(SetMaxOrderableDoseRateProperty, _) -> "SetMaxOrderableDoseRateProperty"
            | OrderContextCmd(SetMedianOrderableDoseRateProperty, _) -> "SetMedianOrderableDoseRateProperty"
            | OrderContextCmd(DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc), _) ->
                $"DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc), _) ->
                $"IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(SetMinComponentOrderableQuantityProperty cmp, _) ->
                $"SetMinComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd(SetMaxComponentOrderableQuantityProperty cmp, _) ->
                $"SetMaxComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd(SetMedianComponentOrderableQuantityProperty cmp, _) ->
                $"SetMedianComponentQuantityProperty cmp={cmp}"

            | OrderPlanCmd(UpdateOrderPlan _) -> "UpdatedOrderPlan"
            | OrderPlanCmd(FilterOrderPlan _) -> "FilterOrderPlan"
            | FormularyCmd _ -> "FormularyCmd"
            | ParenteraliaCmd _ -> "ParenteraliaCmd"
            | NutritionPlanCmd(InitNutritionPlan _) -> "InitNutritionPlan"
            | NutritionPlanCmd(UpdateNutritionOrderContext _) -> "UpdateNutritionOrderContext"
            | NutritionPlanCmd(SelectNutritionOrderScenario _) -> "SelectNutritionOrderScenario"
            | NutritionPlanCmd(NavigateNutritionOrderContext _) -> "NavigateNutritionOrderContext"
            | NutritionPlanCmd(AddNutritionContext _) -> "AddNutritionContext"
            | NutritionPlanCmd(RemoveNutritionContext _) -> "RemoveNutritionContext"
            | InteractionCmd(CheckInteractions _) -> "CheckInteractions"
            | InteractionCmd GetDrugNames -> "GetDrugNames"
            | LogAnalyzerCmd(ValidatePassword _) -> "ValidatePassword"
            | LogAnalyzerCmd(ListLogFiles _) -> "ListLogFiles"
            | LogAnalyzerCmd(AnalyzeLogFile(_, f)) -> $"AnalyzeLogFile %s{f}"


    /// Defines how routes are generated on server and mapped from the client
    let routerPaths typeName method = $"/api/%s{typeName}/%s{method}"


    /// A type that specifies the communication protocol between client and server
    /// to learn more read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            processCommand: Command -> Async<Result<Response, string[]>>
            testApi: unit -> Async<string>
        }
