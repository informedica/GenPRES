namespace Views


#nowarn "1104"

module Nutrition =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines

    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.React
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Order
    open Elmish
    open Utils
    open FSharp.Core
    open OrderPlanMachine
    open NutritionSlot


    let private dividerSx =
        {|
            marginTop = 2
            marginBottom = 2
        |}


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        // the slots live outside the plan until they are prescribed; the plan is there with the patient
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let nutrition = AppEnv.asEnv<AppEnv.INutrition> props.appEnv

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        // no patient: the notice says what is missing
        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadinessPolicy.Needs.Calculation
                |}

        // a request out that changes the page. The page is disabled then, but that stops clicks
        // only: the slots' intake effect sends without one, so it checks this itself. A field counting
        // its step clicks does not disable the page, since the field is on it: the controls that send
        // wait for the count themselves. A prescribe under way leaves the page open: the slot prescribed
        // waits, and the others can be composed meanwhile
        let busy = (AppEnv.asEnv<AppEnv.IBusy> props.appEnv).Page Global.Pages.Nutrition

        let content =
            match orderPlan with
            | OrderPlanView.Settled(plan, _)
            | OrderPlanView.Changing(plan, _) ->
                let sectionProps =
                    {|
                        plan = plan
                        slots = nutrition.Slots
                        nutrition = nutrition
                        planChanging = orderPlan.IsChanging
                        localizationTerms = localizationTerms
                        busy = busy
                    |}

                let enteral = EnteralNutrition.Section sectionProps
                let parenteral = ParenteralNutrition.Section sectionProps

                JSX.jsx
                    $"""
                import Stack from '@mui/material/Stack';
                import Divider from '@mui/material/Divider';

                <Stack direction="column" spacing={1}>
                    {enteral}
                    <Divider sx={dividerSx} />
                    {parenteral}
                </Stack>
                """
            | OrderPlanView.NoPatient -> null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box>
            {patientNotice}
            {content}
        </Box>
        """
