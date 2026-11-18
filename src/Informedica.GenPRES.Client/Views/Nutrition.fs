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
        // The nutrition slots are not in the order plan until they are prescribed. The order plan exists
        // only with patient data.
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let nutrition = AppEnv.asEnv<AppEnv.INutrition> props.appEnv

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        // Without patient data, the notice tells what is missing.
        let patientNotice =
            Components.PatientNotice.View
                {|
                    appEnv = props.appEnv
                    needs = PatientReadinessPolicy.Needs.Calculation
                |}

        // True while a request that changes the page is under way. A disabled page only blocks clicks,
        // so the intake effect of a slot, which sends without a click, checks this value itself. A field
        // that counts its step clicks does not disable the page, because the field is on the page; the
        // controls that send a request wait for the count. A prescribe under way does not disable the
        // page: only the slot being prescribed waits, and the other slots can still be composed.
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
            | OrderPlanView.NoPatientData -> null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box>
            {patientNotice}
            {content}
        </Box>
        """
