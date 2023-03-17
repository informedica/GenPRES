module App

open Fable.Core
open Browser
open Fable.React


module private Elmish =

    open Elmish
    open Feliz
    open Feliz.Router
    open Fable.Remoting.Client
    open Shared
    open Types
    open Global
    open Utils
    open System


    type Model =
        {
            Patient: Patient option
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            Scenarios: Deferred<ScenarioResult>
        }


    type Msg =
        | UpdatePatient of Patient option
        | UrlChanged of string list
        | LoadBolusMedication of
            AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of
            AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>
        | LoadScenarios of AsyncOperationStatus<Result<ScenarioResult, string>>
        | UpdateScenarios of ScenarioResult


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    // url needs to be in format: http://localhost:8080/#pat?ay=2&am=0&ad=1
    // * by: birth year
    // * bm: birth month
    // * bd: birth day
    // * wt: weight (kg)
    // * ln: length (cm)
    let parseUrlToPatient sl =

        match sl with
        | [] -> None
        | [ "pat"
            Route.Query [
                "by", Route.Int by
                "bm", Route.Int bm
                "bd", Route.Int bd
                "wt", Route.Number wt ] ] ->
            Logging.log "query params:" $"by: {by}"

            let age =
                Patient.Age.fromBirthDate DateTime.Now (DateTime(by, bm, bd))

            Patient.create
                (Some age.Years)
                (Some age.Months)
                (Some age.Weeks)
                (Some age.Days)
                (wt |> Some)
                None
            |> fun p ->
                Logging.log "parsed: " p
                p
        | _ ->
            sl
            |> String.concat ""
            |> Logging.warning "could not parse url"

            None


    let pages =
        [
            LifeSupport
            ContinuousMeds
            Prescribe
            Formulary
            Parenteralia
        ]


    let init () : Model * Cmd<Msg> =

        let initialState =
            {
                Patient =
                    Router.currentUrl ()
                    |> parseUrlToPatient
                BolusMedication = HasNotStartedYet
                ContinuousMedication = HasNotStartedYet
                Products = HasNotStartedYet
                Scenarios = HasNotStartedYet
            }

        let cmds =
            Cmd.batch [
                Cmd.ofMsg (LoadBolusMedication Started)
                Cmd.ofMsg (LoadContinuousMedication Started)
                Cmd.ofMsg (LoadProducts Started)
                Cmd.ofMsg (LoadScenarios Started)
            ]

        initialState, cmds


    let update (msg: Msg) (state: Model) =
        match msg with

        | UpdatePatient p ->
            Logging.log "update patient app" p
            { state with Patient = p },
            Cmd.ofMsg (LoadScenarios Started)
        | UrlChanged sl ->
            Logging.log "url changed" sl

            { state with
                Patient = sl |> parseUrlToPatient
            },
            Cmd.none
        | LoadBolusMedication Started ->
            { state with
                BolusMedication = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)
        | LoadBolusMedication (Finished (Ok meds)) ->

            { state with
                BolusMedication = meds |> Resolved
            },
            Cmd.none
        | LoadBolusMedication (Finished (Error s)) ->
            Logging.error "cannot load emergency treatment" s
            state, Cmd.none
        | LoadContinuousMedication Started ->
            { state with
                ContinuousMedication = InProgress
            },
            Cmd.fromAsync (
                GoogleDocs.loadContinuousMedication LoadContinuousMedication
            )
        | LoadContinuousMedication (Finished (Ok meds)) ->

            { state with
                ContinuousMedication = meds |> Resolved
            },
            Cmd.none
        | LoadContinuousMedication (Finished (Error s)) ->
            Logging.error "cannot load continuous medication" s
            state, Cmd.none
        | LoadProducts Started ->
            { state with Products = InProgress },
            Cmd.fromAsync (GoogleDocs.loadProducts LoadProducts)
        | LoadProducts (Finished (Ok prods)) ->

            { state with
                Products = prods |> Resolved
            },
            Cmd.none
        | LoadProducts (Finished (Error s)) ->
            Logging.error "cannot load products" s
            state, Cmd.none

        | LoadScenarios Started ->
            let scenarios =
                match state.Scenarios with
                | Resolved sc when state.Patient.IsSome ->
                    { sc with
                        Age =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getAgeInDays
                            | None -> sc.Age
                        Weight =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getWeight
                            | None -> sc.Weight
                        Height =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getHeight
                            | None -> sc.Height
                    }
                | _ -> ScenarioResult.empty

            let load =
                async {
                    let! result = serverApi.getScenarioResult scenarios
                    return Finished result |> LoadScenarios
                }

            { state with Scenarios = InProgress }, Cmd.fromAsync load

        | LoadScenarios (Finished (Ok result)) ->
            { state with
                Scenarios = Resolved result
            },
            Cmd.none

        | LoadScenarios (Finished (Error msg)) ->
            Logging.log "scenarios" msg
            state, Cmd.none

        | UpdateScenarios sc ->
            let sc =
                { sc with
                    Weight =
                        match state.Patient with
                        | Some pat -> pat |> Patient.getWeight
                        | None -> sc.Weight
                }

            { state with Scenarios = Resolved sc },
            Cmd.ofMsg (LoadScenarios Started)


    let calculatInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> InProgress
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeight
                xs |> calc a w |> Resolved
        )

open Elmish
open Shared

// Entry point must be in a separate file
// for Vite Hot Reload to work

[<JSX.Component>]
let App () =
    let state, dispatch = React.useElmish (init, update, [||])

    let bm =
        calculatInterventions
            EmergencyTreatment.calculate
            state.BolusMedication
            state.Patient

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculatInterventions calc state.ContinuousMedication state.Patient

    Pages.GenPres.View({|
        patient = state.Patient
        updatePatient = UpdatePatient >> dispatch
        bolusMedication = bm
        continuousMedication = cm
        products = state.Products
        scenario = state.Scenarios
        updateScenario = (UpdateScenarios >> dispatch)
    |})

let root = ReactDomClient.createRoot (document.getElementById ("genpres-app"))
root.render (App() |> toReact)
