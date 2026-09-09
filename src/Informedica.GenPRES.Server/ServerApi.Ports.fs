namespace ServerApi

open Shared.Types
open Shared.Api


type FormularyPort =
    {
        getFormulary: Formulary -> Async<Result<Formulary, string[]>>
        getParenteralia: Parenteralia -> Async<Result<Parenteralia, string[]>>
    }


type OrderContextPort = { evaluate: OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string[]>> }


type OrderPlanPort =
    {
        updateOrderPlan: OrderPlan -> (OrderContextCommand * OrderContext) option -> Async<Result<OrderPlan, string[]>>
        filterOrderPlan: OrderPlan -> Async<Result<OrderPlan, string[]>>
    }


type NutritionPlanPort =
    {
        initNutritionPlan: Patient -> Async<Result<NutritionPlan, string[]>>
        addNutritionContext: NutritionPlan * NutritionCategory -> Async<Result<NutritionPlan, string[]>>
        removeNutritionContext: NutritionPlan * string -> Async<Result<NutritionPlan, string[]>>
        updateNutritionOrderContext: NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        selectNutritionOrderScenario: NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        navigateNutritionOrderContext:
            NutritionPlan * string * OrderContextCommand * OrderContext -> Async<Result<NutritionPlan, string[]>>
    }


type InteractionPort =
    {
        checkInteractions: string list -> Async<Result<DrugInteraction list, string[]>>
        getDrugNames: unit -> Async<Result<string list, string[]>>
    }


type LogAnalyzerPort =
    {
        listLogFiles: unit -> Async<Result<LogFileInfo[], string[]>>
        analyzeLogFile: string -> Async<Result<string, string[]>>
    }


/// The session adapter's answer to a presentation. The session id is the server's to put in
/// the cookie; the composition root maps this to the client's `LaunchOutcome` without it.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * SessionOpened
    | RedirectTo of url: string
    | Refused of LaunchRefusal


type SessionPort =
    {
        // idempotent per public key within the Launch lifetime (Rule 2, uc-01 Retries)
        present: Launch * PublicKey -> Async<LaunchResult>
        // by session id from the cookie
        find: string -> Async<SessionOpened option>
        // Rule 10: explicit close
        close: string -> Async<unit>
    }


type AppEnv =
    {
        formulary: FormularyPort
        orderContext: OrderContextPort
        orderPlan: OrderPlanPort
        nutritionPlan: NutritionPlanPort
        interaction: InteractionPort
        logAnalyzer: LogAnalyzerPort
        requireLoaded: unit -> string[] option
        session: SessionPort
    }
