namespace Views

#nowarn "1104"

module Nutrition =

    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.React
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open Shared.Models.Order
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
        // the one plan: the nutrition workbenches live in the order plan, which is there
        // with the patient
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        // no patient: the notice says what is missing. A patient whose plan is being opened
        // shows as Changing, with the sections' own overlay and greyed slots, so no spinner here
        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadiness.Needs.Calculation
                |}

        // a request out that changes the page. The page is disabled then, but that stops clicks
        // only: the slots' intake effect and their fields' step timers send without one, and the
        // delete confirmation opens outside the page, so these check this themselves
        let busy = (AppEnv.asEnv<AppEnv.IBusy> props.appEnv).Page Global.Pages.Nutrition

        let content =
            match orderPlan with
            | OrderPlanView.Settled(plan, _)
            | OrderPlanView.Changing(plan, _) ->
                let sectionProps =
                    {|
                        plan = plan
                        planNew = envOrderPlan.NewNutrition
                        planRemove = envOrderPlan.Remove
                        planNavigate = envOrderPlan.OrderDialogCommand
                        planReopen = envOrderPlan.ReopenField
                        planRestore = envOrderPlan.RestoreField
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
