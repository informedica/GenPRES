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

        // the slots and the buttons rest while a change is under way: the plan takes one change
        // at a time
        let isRecalculating =
            match orderPlan with
            | OrderPlanView.Changing _ -> true
            | OrderPlanView.NoPatient
            | OrderPlanView.Settled _ -> false

        let content =
            match orderPlan with
            | OrderPlanView.Settled(plan, _)
            | OrderPlanView.Changing(plan, _) ->
                let sectionProps =
                    {|
                        plan = plan
                        planCommand = envOrderPlan.OrderPlanCommand
                        planNavigate = envOrderPlan.Navigate
                        planReopen = envOrderPlan.Reopen
                        planRestore = envOrderPlan.Restore
                        localizationTerms = localizationTerms
                        isRecalculating = isRecalculating
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
