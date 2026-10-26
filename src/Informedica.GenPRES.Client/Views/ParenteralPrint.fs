namespace Views


#nowarn "1104"

/// The print sheet of the parenteral nutrition orders.
module ParenteralPrint =

    open Informedica.GenPRES.Client.Core.Models
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


    let private boldCellSx = {| fontWeight = "bold" |}


    /// The parenteral orders on paper, for the central venous lines: per order its components
    /// with their volume and dose per kg, the rate and the time, then the totals of the intake.
    [<JSX.Component>]
    let View
        (props:
            {|
                plan: OrderPlan
                onClose: unit -> unit
            |})
        =
        let printLabels = Global.printLabels ()
        let weightKg = ViewHelpers.PrintView.patientWeight printLabels.unknown (props.plan.Patient |> Some)

        let parenteralContexts = props.plan.OrderContexts |> Array.filter (isOneOf parenteral)

        let tableSx =
            {|
                tableLayout = "fixed"
                width = "100%"
            |}

        let contextSections =
            parenteralContexts
            |> Array.map (fun nc ->
                let scenario = nc.Scenarios |> Array.tryExactlyOne
                let label = OrderContextText.label nc

                match scenario with
                | None ->
                    let mb2Sx = {| marginBottom = 2 |}

                    let header =
                        ViewHelpers.PrintView.SectionHeader
                            {|
                                label = label
                                marginBottom = 0
                            |}

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';

                    <Box key={nc.Id} sx={mb2Sx}>
                        {header}
                        <Typography variant="body2" color="text.secondary">
                            niet geconfigureerd
                        </Typography>
                    </Box>
                    """
                | Some sc ->
                    let ord = sc.Order
                    let orderableName = ord.Orderable.Name

                    let componentRows =
                        ord.Orderable.Components
                        |> Array.map (fun cmp ->
                            let cmpQty = cmp.OrderableQuantity |> OrderDisplay.OrderVariable.displayString
                            let fixPrec2 = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2

                            let doseAdj =
                                cmp.Dose.QuantityAdjust
                                |> OrderDisplay.OrderVariable.displayStringFormatted fixPrec2

                            JSX.jsx
                                $"""
                            import TableRow from '@mui/material/TableRow';
                            import TableCell from '@mui/material/TableCell';

                            <TableRow key={cmp.Name}>
                                <TableCell colSpan={2}>{cmp.Name}</TableCell>
                                <TableCell align="right"><strong>{cmpQty}</strong></TableCell>
                                <TableCell align="right">{doseAdj}</TableCell>
                            </TableRow>
                            """
                        )

                    let totalVolume = ord.Orderable.OrderableQuantity |> OrderDisplay.OrderVariable.displayString

                    let rateDisplay =
                        if ord.Schedule.IsContinuous || ord.Schedule.IsTimed then
                            let rate = ord.Orderable.Dose.Rate |> OrderDisplay.OrderVariable.displayString
                            let fixPrec2 = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2
                            let time = ord.Schedule.Time |> OrderDisplay.OrderVariable.displayStringFormatted fixPrec2

                            let rateBoxSx =
                                {|
                                    display = "flex"
                                    gap = 3
                                    marginTop = 2
                                |}

                            JSX.jsx
                                $"""
                            import Typography from '@mui/material/Typography';
                            import Box from '@mui/material/Box';

                            <Box sx={rateBoxSx}>
                                <Typography variant="body2">
                                    Pompsnelheid: <strong>{rate}</strong>
                                </Typography>
                                <Typography variant="body2">
                                    Looptijd: <strong>{time}</strong>
                                </Typography>
                            </Box>
                            """
                        else
                            null

                    let header =
                        ViewHelpers.PrintView.SectionHeader
                            {|
                                label = $"%s{label} - %s{orderableName}"
                                marginBottom = 1
                            |}

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Table from '@mui/material/Table';
                    import TableBody from '@mui/material/TableBody';
                    import TableRow from '@mui/material/TableRow';
                    import TableCell from '@mui/material/TableCell';
                    import TableHead from '@mui/material/TableHead';

                    <Box key={nc.Id} sx={ {| marginBottom = 3 |} }>
                        {header}
                        <Table size="small" sx={tableSx}>
                            <TableHead>
                                <TableRow>
                                    <TableCell colSpan={2}><strong>Component</strong></TableCell>
                                    <TableCell align="right"><strong>Volume</strong></TableCell>
                                    <TableCell align="right"><strong>Dosis/kg</strong></TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {componentRows |> unbox<seq<ReactElement>> |> React.Fragment}
                                <TableRow>
                                    <TableCell colSpan={2} sx={boldCellSx}>Totaal volume</TableCell>
                                    <TableCell align="right" sx={boldCellSx}>{totalVolume}</TableCell>
                                    <TableCell />
                                </TableRow>
                            </TableBody>
                        </Table>
                        {rateDisplay}
                    </Box>
                    """
            )

        let totalsSection =
            let intake = props.plan.Totals
            let rows = TotalsDisplay.intakeRows

            let activeRows =
                rows
                |> Array.filter (fun cells ->
                    let name = cells |> Array.head
                    let items = TotalsDisplay.substanceToField intake name
                    items |> Array.length >= 2
                )

            let totalsRows =
                activeRows
                |> Array.map (fun cells ->
                    let name = cells[0]
                    let unit = cells[2]
                    let items = TotalsDisplay.substanceToField intake name

                    let value =
                        if items.Length >= 2 then
                            items[0 .. items.Length - 2]
                            |> Array.map ViewHelpers.textItemText
                            |> String.concat " "
                        else
                            ""

                    let normal =
                        if items.Length >= 1 then
                            let s = items[items.Length - 1] |> ViewHelpers.textItemText

                            if s = "" then "" else s + " " + unit
                        else
                            ""

                    JSX.jsx
                        $"""
                    import TableRow from '@mui/material/TableRow';
                    import TableCell from '@mui/material/TableCell';

                    <TableRow key={name}>
                        <TableCell colSpan={2}>{name}</TableCell>
                        <TableCell align="right">{value}</TableCell>
                        <TableCell align="right">{normal}</TableCell>
                    </TableRow>
                    """
                )

            if activeRows.Length = 0 then
                null
            else
                let header =
                    ViewHelpers.PrintView.SectionHeader
                        {|
                            label = "Totalen"
                            marginBottom = 1
                        |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Table from '@mui/material/Table';
                import TableBody from '@mui/material/TableBody';

                <Box sx={ {| marginTop = 2 |} }>
                    {header}
                    <Table size="small" sx={tableSx}>
                        <TableBody>
                            {totalsRows |> unbox<seq<ReactElement>> |> React.Fragment}
                        </TableBody>
                    </Table>
                </Box>
                """

        let printContent =
            let header =
                ViewHelpers.PrintView.SectionHeader
                    {|
                        label = "INFUUS AFSPRAKEN CENTRAAL VENEUZE CATHETERS"
                        marginBottom = 2
                    |}

            JSX.jsx
                $"""
            import React from 'react';

            <React.Fragment>
                {header}
                {ViewHelpers.PrintView.PatientHeader
                     {|
                         weightKg = weightKg
                         labels = printLabels
                     |}}
                {contextSections |> unbox<seq<ReactElement>> |> React.Fragment}
                {totalsSection}
                {ViewHelpers.PrintView.PatientSignature {| label = printLabels.signature |}}
            </React.Fragment>
            """
            |> toReact

        ViewHelpers.PrintView.PrintDialog
            {|
                isOpen = true
                onClose = props.onClose
                title = "Parenterale Voeding"
                children = printContent
            |}
