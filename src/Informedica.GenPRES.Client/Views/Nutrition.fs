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


    /// Whether a context is a nutrition order of one of the categories.
    let private isOneOf (categories: NutritionCategory list) (ctx: OrderContext) =
        match OrderContext.nutritionCategory ctx with
        | Some category -> categories |> List.contains category
        | None -> false


    let private enteral = [ NutritionCategory.EnteralFeeding; NutritionCategory.EnteralSupplement ]


    let private parenteral =
        [
            NutritionCategory.TPN
            NutritionCategory.Lipid
            NutritionCategory.ElectrolyteGlucose
        ]


    module private Elmish =


        /// What the slot holds of its own: the component picked, never the order, which is
        /// the one the slot's context has.
        type State = { SelectedComponent: string option }

        type Msg =
            | ChangeComponent of string option
            | ChangeComponentOrderableQuantity of cmp: string * string option
            | ChangeComponentDoseQuantityAdjust of cmp: string * string option
            | ChangeOrderableDoseRate of string option
            | ChangeOrderableQuantity of string option
            | ChangeFrequency of string option
            | UpdateOrderScenario of Order
            | ResetOrderScenario
            // Rate navigation
            | DecreaseDoseRateProperty of ntimes: int * useCalc: bool
            | IncreaseDoseRateProperty of ntimes: int * useCalc: bool
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Dose Quantity navigation
            | ChangeOrderableDoseQuantity of string option
            | DecreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | IncreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | SetMinDoseQuantityProperty
            | SetMaxDoseQuantityProperty
            | SetMedianDoseQuantityProperty
            // Component Quantity navigation (carries component name)
            | DecreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | IncreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | SetMinComponentQuantityProperty of cmp: string
            | SetMaxComponentQuantityProperty of cmp: string
            | SetMedianComponentQuantityProperty of cmp: string
            // Frequency navigation
            | DecreaseFrequencyProperty
            | IncreaseFrequencyProperty
            | SetMinFrequencyProperty
            | SetMaxFrequencyProperty
            | SetMedianFrequencyProperty


        /// The component picked, seeded from the one scenario of the slot's context: its first
        /// component.
        let init (ctx: OrderContext) =
            let cmp =
                match ctx.Scenarios with
                | [| sc |] ->
                    match sc.Order.Orderable.Components with
                    | [||] -> None
                    | cmps -> Some cmps[0].Name
                | _ ->
                    if ctx.Scenarios |> Array.length > 1 then
                        Logging.error "received multiple scenarios" ctx.Scenarios.Length

                    None

            { SelectedComponent = cmp }, Cmd.none


        let update
            updateOrderScenario
            resetOrderScenario
            (stepper:
                {|
                    setRateMin: OrderLoader -> unit
                    setRateDec: int * bool -> OrderLoader -> unit
                    setRateMed: OrderLoader -> unit
                    setRateInc: int * bool -> OrderLoader -> unit
                    setRateMax: OrderLoader -> unit

                    setDoseQtyMin: OrderLoader -> unit
                    setDoseQtyDec: int * bool -> OrderLoader -> unit
                    setDoseQtyMed: OrderLoader -> unit
                    setDoseQtyInc: int * bool -> OrderLoader -> unit
                    setDoseQtyMax: OrderLoader -> unit

                    setComponentQtyMin: OrderLoader -> unit
                    setComponentQtyDec: int * bool -> OrderLoader -> unit
                    setComponentQtyMed: OrderLoader -> unit
                    setComponentQtyInc: int * bool -> OrderLoader -> unit
                    setComponentQtyMax: OrderLoader -> unit

                    setFreqMin: OrderLoader -> unit
                    setFreqDec: OrderLoader -> unit
                    setFreqMed: OrderLoader -> unit
                    setFreqInc: OrderLoader -> unit
                    setFreqMax: OrderLoader -> unit
                |})
            (shown: Order option)
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =
            let setOvar = OrderVariable.setOvar

            // every change and every step is over the order shown, the slot's context's; the
            // plan lane holds it, the slot holds none
            let handleNav nav =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent None ord |> nav
                    state, Cmd.none

            let handleNavWithCmp cmpName nav =
                match shown with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create (Some cmpName) None ord |> nav
                    state, Cmd.none

            match msg with

            | UpdateOrderScenario ord ->
                OrderLoader.create state.SelectedComponent None ord |> updateOrderScenario

                state, Cmd.none

            | ResetOrderScenario ->
                match shown with
                | Some ord -> OrderLoader.create state.SelectedComponent None ord |> resetOrderScenario
                | None -> ()

                state, Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedComponent = cmp }, Cmd.none

            | ChangeComponentOrderableQuantity(cmpName, s) ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with
                            Order.Orderable.Components =
                                ord.Orderable.Components
                                |> Array.map (fun cmp ->
                                    if cmp.Name = cmpName then
                                        { cmp with OrderableQuantity = cmp.OrderableQuantity |> setOvar s }
                                    else
                                        cmp
                                )
                        }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeComponentDoseQuantityAdjust(cmpName, s) ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with
                            Order.Orderable.Components =
                                ord.Orderable.Components
                                |> Array.map (fun cmp ->
                                    if cmp.Name = cmpName then
                                        { cmp with
                                            Component.Dose.QuantityAdjust = cmp.Dose.QuantityAdjust |> setOvar s
                                        }
                                    else
                                        cmp
                                )
                        }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseRate s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableQuantity s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.OrderableQuantity = ord.Orderable.OrderableQuantity |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeFrequency s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Schedule.Frequency = ord.Schedule.Frequency |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseQuantity s ->
                match shown with
                | Some ord ->
                    let msg =
                        { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> setOvar s }
                        |> UpdateOrderScenario

                    state, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            // Rate navigation
            | SetMinDoseRateProperty -> handleNav stepper.setRateMin
            | DecreaseDoseRateProperty(n, uc) -> handleNav (stepper.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleNav stepper.setRateMed
            | IncreaseDoseRateProperty(n, uc) -> handleNav (stepper.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleNav stepper.setRateMax

            // Dose Quantity navigation
            | SetMinDoseQuantityProperty -> handleNav stepper.setDoseQtyMin
            | DecreaseDoseQuantityProperty(n, uc) -> handleNav (stepper.setDoseQtyDec (n, uc))
            | SetMedianDoseQuantityProperty -> handleNav stepper.setDoseQtyMed
            | IncreaseDoseQuantityProperty(n, uc) -> handleNav (stepper.setDoseQtyInc (n, uc))
            | SetMaxDoseQuantityProperty -> handleNav stepper.setDoseQtyMax

            // Component Quantity navigation
            | SetMinComponentQuantityProperty cmp -> handleNavWithCmp cmp stepper.setComponentQtyMin
            | DecreaseComponentQuantityProperty(cmp, n, uc) -> handleNavWithCmp cmp (stepper.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty cmp -> handleNavWithCmp cmp stepper.setComponentQtyMed
            | IncreaseComponentQuantityProperty(cmp, n, uc) -> handleNavWithCmp cmp (stepper.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty cmp -> handleNavWithCmp cmp stepper.setComponentQtyMax

            // Frequency navigation
            | SetMinFrequencyProperty -> handleNav stepper.setFreqMin
            | DecreaseFrequencyProperty -> handleNav stepper.setFreqDec
            | SetMedianFrequencyProperty -> handleNav stepper.setFreqMed
            | IncreaseFrequencyProperty -> handleNav stepper.setFreqInc
            | SetMaxFrequencyProperty -> handleNav stepper.setFreqMax


    open Elmish


    /// What a message does: change a value, step it, send or reset the order, or change the slot alone.
    [<RequireQualifiedAccess>]
    type private Kind =
        | Local
        | Change
        | Step of string
        | Update
        | Reset


    let private kindOf msg =
        match msg with
        | ChangeComponent _ -> Kind.Local
        | UpdateOrderScenario _ -> Kind.Update
        | ResetOrderScenario -> Kind.Reset
        | SetMinDoseRateProperty
        | SetMinDoseQuantityProperty
        | SetMinComponentQuantityProperty _
        | SetMinFrequencyProperty -> Kind.Step "min"
        | DecreaseDoseRateProperty _
        | DecreaseDoseQuantityProperty _
        | DecreaseComponentQuantityProperty _
        | DecreaseFrequencyProperty -> Kind.Step "dec"
        | SetMedianDoseRateProperty
        | SetMedianDoseQuantityProperty
        | SetMedianComponentQuantityProperty _
        | SetMedianFrequencyProperty -> Kind.Step "med"
        | IncreaseDoseRateProperty _
        | IncreaseDoseQuantityProperty _
        | IncreaseComponentQuantityProperty _
        | IncreaseFrequencyProperty -> Kind.Step "inc"
        | SetMaxDoseRateProperty
        | SetMaxDoseQuantityProperty
        | SetMaxComponentQuantityProperty _
        | SetMaxFrequencyProperty -> Kind.Step "max"
        | _ -> Kind.Change


    /// The field a message moves, a component field with its component.
    let private fieldOf msg =
        match msg with
        | ChangeComponentOrderableQuantity(cmp, _)
        | SetMinComponentQuantityProperty cmp
        | DecreaseComponentQuantityProperty(cmp, _, _)
        | SetMedianComponentQuantityProperty cmp
        | IncreaseComponentQuantityProperty(cmp, _, _)
        | SetMaxComponentQuantityProperty cmp -> Some $"compOrdQty %s{cmp}"
        | ChangeComponentDoseQuantityAdjust(cmp, _) -> Some $"compDoseQtyAdj %s{cmp}"
        | ChangeOrderableDoseRate _
        | SetMinDoseRateProperty
        | DecreaseDoseRateProperty _
        | SetMedianDoseRateProperty
        | IncreaseDoseRateProperty _
        | SetMaxDoseRateProperty -> Some "ordDoseRate"
        | ChangeOrderableDoseQuantity _
        | SetMinDoseQuantityProperty
        | DecreaseDoseQuantityProperty _
        | SetMedianDoseQuantityProperty
        | IncreaseDoseQuantityProperty _
        | SetMaxDoseQuantityProperty -> Some "ordDoseQty"
        | ChangeOrderableQuantity _ -> Some "ordQty"
        | ChangeFrequency _
        | SetMinFrequencyProperty
        | DecreaseFrequencyProperty
        | SetMedianFrequencyProperty
        | IncreaseFrequencyProperty
        | SetMaxFrequencyProperty -> Some "frequency"
        | ChangeComponent _
        | UpdateOrderScenario _
        | ResetOrderScenario -> None


    /// The message for the trail: its case, what it carries, and its field.
    let private describeMsg msg =
        let value (s: string option) = s |> Option.defaultValue "none"

        let steps (n: int, useCalc: bool) = if useCalc then $"%i{n} calc" else $"%i{n}"

        let case =
            match msg with
            | ChangeComponent s -> $"ChangeComponent %s{value s}"
            | ChangeComponentOrderableQuantity(_, s) -> $"ChangeComponentOrderableQuantity %s{value s}"
            | ChangeComponentDoseQuantityAdjust(_, s) -> $"ChangeComponentDoseQuantityAdjust %s{value s}"
            | ChangeOrderableDoseRate s -> $"ChangeOrderableDoseRate %s{value s}"
            | ChangeOrderableQuantity s -> $"ChangeOrderableQuantity %s{value s}"
            | ChangeFrequency s -> $"ChangeFrequency %s{value s}"
            | ChangeOrderableDoseQuantity s -> $"ChangeOrderableDoseQuantity %s{value s}"
            | UpdateOrderScenario ord -> $"UpdateOrderScenario %s{Trail.Part.shortId ord.Id}"
            | ResetOrderScenario -> "ResetOrderScenario"
            | DecreaseDoseRateProperty(n, uc) -> $"DecreaseDoseRateProperty %s{steps (n, uc)}"
            | IncreaseDoseRateProperty(n, uc) -> $"IncreaseDoseRateProperty %s{steps (n, uc)}"
            | SetMinDoseRateProperty -> "SetMinDoseRateProperty"
            | SetMaxDoseRateProperty -> "SetMaxDoseRateProperty"
            | SetMedianDoseRateProperty -> "SetMedianDoseRateProperty"
            | DecreaseDoseQuantityProperty(n, uc) -> $"DecreaseDoseQuantityProperty %s{steps (n, uc)}"
            | IncreaseDoseQuantityProperty(n, uc) -> $"IncreaseDoseQuantityProperty %s{steps (n, uc)}"
            | SetMinDoseQuantityProperty -> "SetMinDoseQuantityProperty"
            | SetMaxDoseQuantityProperty -> "SetMaxDoseQuantityProperty"
            | SetMedianDoseQuantityProperty -> "SetMedianDoseQuantityProperty"
            | DecreaseComponentQuantityProperty(_, n, uc) -> $"DecreaseComponentQuantityProperty %s{steps (n, uc)}"
            | IncreaseComponentQuantityProperty(_, n, uc) -> $"IncreaseComponentQuantityProperty %s{steps (n, uc)}"
            | SetMinComponentQuantityProperty _ -> "SetMinComponentQuantityProperty"
            | SetMaxComponentQuantityProperty _ -> "SetMaxComponentQuantityProperty"
            | SetMedianComponentQuantityProperty _ -> "SetMedianComponentQuantityProperty"
            | DecreaseFrequencyProperty -> "DecreaseFrequencyProperty"
            | IncreaseFrequencyProperty -> "IncreaseFrequencyProperty"
            | SetMinFrequencyProperty -> "SetMinFrequencyProperty"
            | SetMaxFrequencyProperty -> "SetMaxFrequencyProperty"
            | SetMedianFrequencyProperty -> "SetMedianFrequencyProperty"

        match fieldOf msg with
        | Some field -> $"%s{case} field %s{field}"
        | None -> case


    /// The call a message makes, given the order shown and whether a reopen is under way.
    let private effectsOf (shown: Order option) (reopening: bool) msg =
        let shownId = shown |> Option.map (_.Id >> Trail.Part.shortId)

        match kindOf msg, shownId with
        | Kind.Local, _ -> []
        | Kind.Update, _ ->
            match msg with
            | UpdateOrderScenario ord ->
                let call = if reopening then "CallReopen" else "CallUpdate"
                [ $"%s{call} %s{Trail.Part.shortId ord.Id}" ]
            | _ -> []
        | _, None -> []
        | Kind.Reset, Some id -> [ $"CallReset %s{id}" ]
        | Kind.Change, Some _ -> [ "ofMsg UpdateOrderScenario" ]
        | Kind.Step step, Some _ ->
            let field = fieldOf msg |> Option.defaultValue "?"
            [ $"CallStep %s{field} %s{step}" ]


    /// The slot's own state; a step names the component it acts on in its message.
    let private describeState (state: State) =
        let cmp = state.SelectedComponent |> Option.defaultValue "none"
        $"selected %s{cmp}"


    let private halfSize =
        {|
            xs = 12
            md = 6
        |}

    let private cellSx =
        {|
            minWidth = 350
            ``& .MuiFormControl-root`` = {| width = "100%" |}
        |}

    let private flexEndSx = {| alignItems = "flex-end" |}

    let private alignCenterSx = {| alignItems = "center" |}

    let private boldCellSx = {| fontWeight = "bold" |}

    let private autoMarginSx = {| marginLeft = "auto" |}

    // the summary wraps onto more lines rather than clip a value at the header's edge
    let private flexOverflowSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            alignItems = "center"
            width = "100%"
        |}

    let private printSectionHeaderSx =
        {|
            fontWeight = "bold"
            backgroundColor = "#f5f5f5"
            padding = "4px 8px"
            borderRadius = 1
        |}

    let private printSectionHeaderMb1Sx =
        {|
            fontWeight = "bold"
            backgroundColor = "#f5f5f5"
            padding = "4px 8px"
            borderRadius = 1
            marginBottom = 1
        |}

    let private printSectionHeaderMb2Sx =
        {|
            fontWeight = "bold"
            backgroundColor = "#f5f5f5"
            padding = "4px 8px"
            borderRadius = 1
            marginBottom = 2
        |}

    let private removeButtonSx =
        {|
            marginLeft = "auto"
            display = "inline-flex"
            alignItems = "center"
            cursor = "pointer"
            padding = "4px"
            borderRadius = "50%"
            ``&:hover`` = {| backgroundColor = "rgba(0, 0, 0, 0.04)" |}
        |}

    let private addButtonSx =
        {|
            marginTop = 1
            marginBottom = 1
        |}

    let private dividerSx =
        {|
            marginTop = 2
            marginBottom = 2
        |}


    // the administration of a scenario as pills: one per value the server printed, the
    // frequency, the dose, the rate, the time, each coloured by its own severity; a row per
    // item, the rows told apart by a plus between them
    let private renderAdminSummary (key: string) (name: string) (rows: TextBlock[][]) =
        let typoSx =
            {|
                display = "inline"
                color = "text.secondary"
                marginRight = 1
            |}

        let boxSx =
            {|
                display = "inline-flex"
                alignItems = "center"
                flexWrap = "wrap"
                marginLeft = 1
            |}

        let textOf (block: TextBlock) =
            block
            |> Severity.items
            |> Array.map (
                function
                | Normal s
                | Bold s
                | Italic s -> s
            )
            |> String.concat ""
            |> String.trim

        // a block with a number in it is a value and becomes a chip; one without, an item's
        // name or a word between values such as "in" or "=", stays text between the chips
        let isValue (text: string) = text |> Seq.exists System.Char.IsDigit

        let text (key: string) (s: string) =
            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';
            <Typography key={key} variant="body2" sx={typoSx}>{s}</Typography>
            """

        // each block keeps its own severity through the filtering: the text and the block
        // travel together
        let ofRow (r: int) (row: TextBlock[]) =
            row
            |> Array.map (fun block -> textOf block, block)
            |> Array.filter (fst >> String.notEmpty)
            |> Array.mapi (fun i (t, block) ->
                if t |> isValue then
                    Components.ValueChip.View
                        {|
                            value = t
                            severity = block |> Severity.ofTextBlock
                            label = None
                        |}
                else
                    text $"{r}-{i}" t
            )

        let chips =
            rows
            |> Array.map (fun row -> row |> Array.map textOf |> Array.exists String.notEmpty, row)
            |> Array.filter fst
            |> Array.mapi (fun r (_, row) ->
                if r = 0 then
                    ofRow r row
                else
                    Array.append [| text $"sep-{r}" "+" |] (ofRow r row)
            )
            |> Array.concat
            |> unbox<seq<ReactElement>>
            |> React.Fragment

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box key={key} sx={boxSx}>
            <Typography variant="body2" sx={typoSx}>
                {name}:
            </Typography>
            {chips}
        </Box>
        """


    [<JSX.Component>]
    let private ParenteralPrintView
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
                let label = OrderContext.label nc

                match scenario with
                | None ->
                    let mb2Sx = {| marginBottom = 2 |}

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';

                    <Box key={nc.Id} sx={mb2Sx}>
                        <Typography variant="subtitle1" sx={printSectionHeaderSx}>
                            {label}
                        </Typography>
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
                            let cmpQty = cmp.OrderableQuantity |> OrderVariable.displayString
                            let fixPrec2 = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2

                            let doseAdj = cmp.Dose.QuantityAdjust |> OrderVariable.displayStringFormatted fixPrec2

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

                    let totalVolume = ord.Orderable.OrderableQuantity |> OrderVariable.displayString

                    let rateDisplay =
                        if ord.Schedule.IsContinuous || ord.Schedule.IsTimed then
                            let rate = ord.Orderable.Dose.Rate |> OrderVariable.displayString
                            let fixPrec2 = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2
                            let time = ord.Schedule.Time |> OrderVariable.displayStringFormatted fixPrec2

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

                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    import Typography from '@mui/material/Typography';
                    import Table from '@mui/material/Table';
                    import TableBody from '@mui/material/TableBody';
                    import TableRow from '@mui/material/TableRow';
                    import TableCell from '@mui/material/TableCell';
                    import TableHead from '@mui/material/TableHead';

                    <Box key={nc.Id} sx={ {| marginBottom = 3 |} }>
                        <Typography variant="subtitle1" sx={printSectionHeaderMb1Sx}>
                            {label} - {orderableName}
                        </Typography>
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
            let rows = Totals.intakeRows

            let activeRows =
                rows
                |> Array.filter (fun cells ->
                    let name = cells |> Array.head
                    let items = Totals.substanceToField intake name
                    items |> Array.length >= 2
                )

            let totalsRows =
                activeRows
                |> Array.map (fun cells ->
                    let name = cells[0]
                    let unit = cells[2]
                    let items = Totals.substanceToField intake name

                    let value =
                        if items.Length >= 2 then
                            items[0 .. items.Length - 2]
                            |> Array.map (fun item ->
                                match item with
                                | Normal s
                                | Bold s
                                | Italic s -> s
                            )
                            |> String.concat " "
                        else
                            ""

                    let normal =
                        if items.Length >= 1 then
                            let s =
                                match items[items.Length - 1] with
                                | Normal s
                                | Bold s
                                | Italic s -> s

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
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Typography from '@mui/material/Typography';
                import Table from '@mui/material/Table';
                import TableBody from '@mui/material/TableBody';

                <Box sx={ {| marginTop = 2 |} }>
                    <Typography variant="subtitle1" sx={printSectionHeaderMb1Sx}>
                        Totalen
                    </Typography>
                    <Table size="small" sx={tableSx}>
                        <TableBody>
                            {totalsRows |> unbox<seq<ReactElement>> |> React.Fragment}
                        </TableBody>
                    </Table>
                </Box>
                """

        let printContent =
            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';

            <React.Fragment>
                <Typography variant="subtitle1" sx={printSectionHeaderMb2Sx}>
                    INFUUS AFSPRAKEN CENTRAAL VENEUZE CATHETERS
                </Typography>
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


    [<JSX.Component>]
    let private NutritionSlot
        (props:
            {|
                nutritionContext: OrderContext
                plan: OrderPlan
                planCommand: Api.OrderPlanCommand -> unit
                // a clear from a field's arrow, and the list of such a reopen closed without a pick
                planReopen: Api.OrderPlanCommand -> unit
                planRestore: unit -> unit
                localizationTerms: Deferred<string[][]>
                onRemove: (unit -> unit) option
                wrapInAccordion: bool
                isRecalculating: bool
            |})
        =
        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let ctx = props.nutritionContext
        let ncId = props.nutritionContext.Id

        // Monotonic counter bumped whenever a new server response replaces the order
        // context. Passed into stepped selects so they reset their optimistic step value
        // even when the server returns the SAME value as before (e.g. a no-op step when
        // already at the maximum), where the displayed value never changes and the
        // value-based reset alone would leave the stale optimistic value on screen.
        let revisionRef = React.useRef 0
        let prevCtxRef = React.useRef ctx

        if not (obj.ReferenceEquals(prevCtxRef.current, ctx)) then
            prevCtxRef.current <- ctx
            revisionRef.current <- revisionRef.current + 1

        let revision = revisionRef.current

        let label =
            let name = OrderContext.label ctx

            match OrderContext.nutritionCategory ctx with
            | Some NutritionCategory.EnteralFeeding -> Terms.``Nutrition Enteral Feeding`` |> getTerm name
            | Some NutritionCategory.EnteralSupplement -> Terms.``Nutrition Enteral Supplement`` |> getTerm name
            | Some NutritionCategory.TPN -> Terms.``Nutrition TPN`` |> getTerm name
            | Some NutritionCategory.Lipid -> Terms.``Nutrition Lipids`` |> getTerm name
            | Some NutritionCategory.ElectrolyteGlucose -> Terms.``Nutrition Electrolytes Glucose`` |> getTerm name
            | None -> name

        // Use a ref for the plan so that closures captured by useElmish
        // always read the latest plan, even when useElmish doesn't re-initialize
        // (its deps only include ctx, not the plan).
        let planRef = React.useRef props.plan
        planRef.current <- props.plan

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let markOf = ViewHelpers.markOf

        let genericChange s =
            ctx
            |> OrderContext.medicationChange s
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.UpdateOrderContext, updCtx)
            |> props.planCommand

        let indicationChange s =
            ctx
            |> OrderContext.indicationChange s
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.UpdateOrderContext, updCtx)
            |> props.planCommand

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString

            ctx
            |> OrderContext.doseTypeChange dt
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.UpdateOrderContext, updCtx)
            |> props.planCommand

        // set by a field's arrow just before it clears its value, so that the change goes out as
        // a reopen rather than as a change
        let reopening = React.useRef false

        // a reopen goes without picks: only the workbench keeps them, so the server clears and solves
        let updateOrderScenario (ol: OrderLoader) =
            let isReopen = reopening.current
            reopening.current <- false

            let cmd =
                if isReopen then
                    Api.OrderContextCommand.ReopenOrderScenario [||]
                else
                    Api.OrderContextCommand.UpdateOrderScenario

            { ctx with
                Scenarios =
                    ctx.Scenarios
                    |> Array.map (fun sc ->
                        if sc.Order.Id <> ol.Order.Id then
                            sc
                        else
                            { sc with
                                Component = ol.Component
                                Item = ol.Item
                                Order = ol.Order
                            }
                    )
            }
            |> fun updCtx ->
                Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd, updCtx)
                |> if isReopen then props.planReopen else props.planCommand

        let resetOrderScenario (_ol: OrderLoader) =
            Api.OrderPlanCommand.Navigate(planRef.current, ncId, Api.OrderContextCommand.ResetOrderScenario, ctx)
            |> props.planCommand

        let stepper =
            let create nav =
                fun (ol: OrderLoader) ->
                    let updCtx =
                        { ctx with
                            Scenarios =
                                ctx.Scenarios
                                |> Array.map (fun sc ->
                                    if sc.Order.Id <> ol.Order.Id then
                                        sc
                                    else
                                        { sc with
                                            Component = ol.Component
                                            Item = ol.Item
                                            Order = ol.Order
                                        }
                                )
                        }

                    nav updCtx

            let createWithN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    let updCtx =
                        { ctx with
                            Scenarios =
                                ctx.Scenarios
                                |> Array.map (fun sc ->
                                    if sc.Order.Id <> ol.Order.Id then
                                        sc
                                    else
                                        { sc with
                                            Component = ol.Component
                                            Item = ol.Item
                                            Order = ol.Order
                                        }
                                )
                        }

                    nav (updCtx, n, uc)

            let createWithCmp nav =
                fun (ol: OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp ->
                        let updCtx =
                            { ctx with
                                Scenarios =
                                    ctx.Scenarios
                                    |> Array.map (fun sc ->
                                        if sc.Order.Id <> ol.Order.Id then
                                            sc
                                        else
                                            { sc with
                                                Component = ol.Component
                                                Item = ol.Item
                                                Order = ol.Order
                                            }
                                    )
                            }

                        nav (updCtx, cmp)

            let createWithCmpN nav =
                fun (n, uc) (ol: OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp ->
                        let updCtx =
                            { ctx with
                                Scenarios =
                                    ctx.Scenarios
                                    |> Array.map (fun sc ->
                                        if sc.Order.Id <> ol.Order.Id then
                                            sc
                                        else
                                            { sc with
                                                Component = ol.Component
                                                Item = ol.Item
                                                Order = ol.Order
                                            }
                                    )
                            }

                        nav (updCtx, cmp, n, uc)

            let navRate cmd =
                fun updCtx ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd, updCtx)
                    |> props.planCommand

            let navRateN cmd =
                fun (updCtx, n, uc) ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd (n, uc), updCtx)
                    |> props.planCommand

            let navCmpQty cmd =
                fun (updCtx, cmp) ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd cmp, updCtx)
                    |> props.planCommand

            let navCmpQtyN cmd =
                fun (updCtx, cmp, n, uc) ->
                    Api.OrderPlanCommand.Navigate(planRef.current, ncId, cmd (cmp, n, uc), updCtx)
                    |> props.planCommand

            {|
                // Dose Rate
                setRateMin = create (navRate Api.OrderContextCommand.SetMinOrderableDoseRateProperty)
                setRateDec = createWithN (navRateN Api.OrderContextCommand.DecreaseOrderableDoseRateProperty)
                setRateMed = create (navRate Api.OrderContextCommand.SetMedianOrderableDoseRateProperty)
                setRateInc = createWithN (navRateN Api.OrderContextCommand.IncreaseOrderableDoseRateProperty)
                setRateMax = create (navRate Api.OrderContextCommand.SetMaxOrderableDoseRateProperty)
                // Dose Quantity
                setDoseQtyMin = create (navRate Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty)
                setDoseQtyDec = createWithN (navRateN Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty)
                setDoseQtyMed = create (navRate Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty)
                setDoseQtyInc = createWithN (navRateN Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty)
                setDoseQtyMax = create (navRate Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty)
                // Component Quantity
                setComponentQtyMin =
                    createWithCmp (navCmpQty Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty)
                setComponentQtyDec =
                    createWithCmpN (navCmpQtyN Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty)
                setComponentQtyMed =
                    createWithCmp (navCmpQty Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty)
                setComponentQtyInc =
                    createWithCmpN (navCmpQtyN Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty)
                setComponentQtyMax =
                    createWithCmp (navCmpQty Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty)
                // Frequency
                setFreqMin = create (navRate Api.OrderContextCommand.SetMinScheduleFrequencyProperty)
                setFreqDec = create (navRate Api.OrderContextCommand.DecreaseScheduleFrequencyProperty)
                setFreqMed = create (navRate Api.OrderContextCommand.SetMedianScheduleFrequencyProperty)
                setFreqInc = create (navRate Api.OrderContextCommand.IncreaseScheduleFrequencyProperty)
                setFreqMax = create (navRate Api.OrderContextCommand.SetMaxScheduleFrequencyProperty)
            |}

        // the order shown: the one scenario's of the slot's context
        let shownOrder = ctx.Scenarios |> Array.tryExactlyOne |> Option.map _.Order

        // the reopen flag is read before the update, which resets it
        let updateTraced msg state =
            let reopen = reopening.current
            let next, cmd = update updateOrderScenario resetOrderScenario stepper shownOrder msg state

            StepTrail.record (fun no at ->
                Trail.step "Nutrition" describeMsg id describeState no at msg (next, effectsOf shownOrder reopen msg)
            )

            next, cmd

        let state, dispatch = React.useElmish (init ctx, updateTraced, [| box ctx |])

        let isOrderLoading = props.isRecalculating
        let texts = ViewHelpers.quantityFieldTexts getTerm

        let select = ViewHelpers.orderSelect texts true isOrderLoading

        // a field with one value reopens by its arrow; without picks it cannot tell whether the user
        // constrained it
        let reopenOf (ovar: OrderVariable) : ViewHelpers.Reopen =
            {|
                constrained = PickList.constrained None ovar.Name
                reopening = fun () -> reopening.current <- true
                restore = props.planRestore
                busy = isOrderLoading
            |}
        // a value only shown has nothing for a cross to clear
        let display = ViewHelpers.orderFixed texts true isOrderLoading
        let filterSelect = ViewHelpers.filterSelect isOrderLoading isOrderLoading
        let autoComplete = ViewHelpers.autoComplete isOrderLoading isOrderLoading
        let loadingIndicator = ViewHelpers.inlineProgress isOrderLoading

        let displayOrder = shownOrder

        let componentRows =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Components
                |> Array.map (fun cmp ->
                    // Quantity control (bereiding)
                    let qtyVals = cmp.OrderableQuantity |> ViewHelpers.ovarValsWithRange string 3

                    let nav =
                        let cmpName = cmp.Name

                        let mode =
                            cmp.OrderableQuantity
                            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.ComponentQuantity ord

                        ViewHelpers.createStepper
                            dispatch
                            revision
                            mode
                            (cmp.OrderableQuantity |> ViewHelpers.hasLargeStep)
                            (SetMinComponentQuantityProperty cmpName)
                            (fun (n, uc) -> DecreaseComponentQuantityProperty(cmpName, n, uc))
                            (SetMedianComponentQuantityProperty cmpName)
                            (fun (n, uc) -> IncreaseComponentQuantityProperty(cmpName, n, uc))
                            (SetMaxComponentQuantityProperty cmpName)
                            (cmp.OrderableQuantity |> ViewHelpers.ovarStep string)
                            (cmp.OrderableQuantity |> ViewHelpers.largeStepText)

                    let qtyWarning = cmp.OrderableQuantity |> markOf

                    let qtyLabel = cmp.OrderableQuantity |> ViewHelpers.ovarLabel cmp.Name

                    let qtyControl =
                        select
                            false
                            qtyLabel
                            None
                            (fun s -> ChangeComponentOrderableQuantity(cmp.Name, s) |> dispatch)
                            nav
                            qtyWarning
                            (reopenOf cmp.OrderableQuantity)
                            (Some 400)
                            qtyVals

                    // Dose display (dosering) - always show with label
                    let doseLabel = cmp.Dose.QuantityAdjust |> ViewHelpers.ovarLabel cmp.Name
                    let doseWarning = cmp.Dose.QuantityAdjust |> markOf
                    let doseVals = cmp.Dose.QuantityAdjust |> ViewHelpers.ovarVals (fixPrecision 3)

                    let doseDisplay =
                        select
                            false
                            doseLabel
                            None
                            (fun s -> ChangeComponentDoseQuantityAdjust(cmp.Name, s) |> dispatch)
                            ViewHelpers.noSteps
                            doseWarning
                            (reopenOf cmp.Dose.QuantityAdjust)
                            (Some 400)
                            doseVals

                    JSX.jsx
                        $"""
                    import Grid from '@mui/material/Grid';
                    import Box from '@mui/material/Box';
                    <Grid container spacing={{2}} sx={flexEndSx}>
                        <Grid size={halfSize}>
                            <Box sx={cellSx}>
                                {qtyControl}
                            </Box>
                        </Grid>
                        <Grid size={halfSize}>
                            <Box sx={cellSx}>
                                {doseDisplay}
                            </Box>
                        </Grid>
                    </Grid>
                    """
                )
            | None -> [| null |]

        let isEnteral = ctx |> isOneOf enteral

        let selectMinWidth = if isEnteral then None else Some 400

        let doseQtyControl =
            match displayOrder with
            | Some ord ->
                let severity = ord.Orderable.Dose.Quantity |> markOf

                let label = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarLabel "toedien hoeveelheid"

                let vals = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarValsWithRange string 3

                let doseQtyNav =
                    ViewHelpers.createDoseQtyStepper
                        dispatch
                        revision
                        ord
                        SetMinDoseQuantityProperty
                        DecreaseDoseQuantityProperty
                        SetMedianDoseQuantityProperty
                        IncreaseDoseQuantityProperty
                        SetMaxDoseQuantityProperty

                select
                    false
                    label
                    None
                    (ChangeOrderableDoseQuantity >> dispatch)
                    doseQtyNav
                    severity
                    (reopenOf ord.Orderable.Dose.Quantity)
                    selectMinWidth
                    vals
            | None -> null

        let dosePerTimeAdjDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Dose.PerTimeAdjust
                |> ViewHelpers.ovarDisplay display "dosering" (fixPrecision 3) selectMinWidth
            | None -> null

        let frequencyControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                let severity = ord.Schedule.Frequency |> markOf
                let label = ord.Schedule.Frequency |> ViewHelpers.ovarLabel "frequentie"
                let freqVals = ord.Schedule.Frequency |> ViewHelpers.ovarValsWithRange string 3

                let freqNav =
                    let mode =
                        ord.Schedule.Frequency
                        |> QuantityModePolicy.decideFor QuantityModePolicy.Field.Frequency ord

                    // a frequency steps one increment per click, so it has no large step
                    ViewHelpers.createStepper
                        dispatch
                        revision
                        mode
                        false
                        SetMinFrequencyProperty
                        (fun _ -> DecreaseFrequencyProperty)
                        SetMedianFrequencyProperty
                        (fun _ -> IncreaseFrequencyProperty)
                        SetMaxFrequencyProperty
                        None
                        None

                select
                    false
                    label
                    None
                    (ChangeFrequency >> dispatch)
                    freqNav
                    severity
                    (reopenOf ord.Schedule.Frequency)
                    selectMinWidth
                    freqVals
            | _ -> null

        let genericFilter =
            let sel = ctx.Filter.Generic
            let items = ctx.Filter.Generics
            let lbl = Terms.Composition |> getTerm "Samenstelling"
            let onChange = genericChange

            if isMobile then
                items |> Array.map (fun s -> s, s) |> filterSelect lbl sel onChange
            else
                items |> autoComplete lbl sel onChange

        let frequencyDoseRow =
            if isEnteral then
                // The row is measured, not the window: the container query below decides, and a
                // field that never gets buttons here drops its hidden slots.
                let containerSx =
                    {| Components.QuantityField.dropUnusedSlotsSx with
                        containerType = "inline-size"
                        width = "100%"
                    |}

                // Every feeding keeps its items on one row while they fit: the filter with the
                // room of a severity mark, frequency with its inner buttons, the dose field with
                // all its buttons, dose per time without, and the gaps. Narrower, all items stand
                // in one column, never wrapped part-way and never scrolled sideways.
                let rowWidth =
                    200
                    + Components.QuantityField.markWidth
                    + Components.QuantityField.fieldWidthInnerSlots
                    + Components.QuantityField.fieldWidth
                    + Components.QuantityField.fieldWidthWithoutSlots
                    + 3 * 16

                let flexSx =
                    createObj
                        [
                            "display" ==> "flex"
                            "flexWrap" ==> "nowrap"
                            "gap" ==> 2
                            "alignItems" ==> "flex-end"
                            $"@container (max-width: %i{rowWidth - 1}px)"
                            ==> {|
                                    flexDirection = "column"
                                    alignItems = "stretch"
                                |}
                        ]

                // the filter keeps the room a field gives its severity mark, so the gaps between
                // all items look the same
                let itemSx =
                    {|
                        flex = "1 1 0%"
                        minWidth = 200
                        paddingRight = $"%i{Components.QuantityField.markWidth}px"
                        ``& .MuiFormControl-root`` = {| width = "100%" |}
                        ``& .MuiAutocomplete-root`` = {| minWidth = "unset" |}
                    |}

                // a quantity field is as wide as its buttons and a readable value need
                let fieldItemSx =
                    {|
                        flex = "1 1 0%"
                        minWidth = "min-content"
                    |}

                // frequency keeps one width whether it is chosen from the dropdown or stepped
                let frequencyItemSx = {| fieldItemSx with minWidth = Components.QuantityField.fieldWidthInnerSlots |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={containerSx}>
                    <Box sx={flexSx}>
                        <Box sx={itemSx}>
                            {genericFilter}
                        </Box>
                        <Box sx={frequencyItemSx}>
                            {frequencyControl}
                        </Box>
                        <Box sx={fieldItemSx}>
                            {doseQtyControl}
                        </Box>
                        <Box sx={fieldItemSx}>
                            {dosePerTimeAdjDisplay}
                        </Box>
                    </Box>
                </Box>
                """
            else
                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                import Box from '@mui/material/Box';
                <Grid container spacing={{2}} sx={flexEndSx}>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {frequencyControl}
                        </Box>
                    </Grid>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {doseQtyControl}
                        </Box>
                    </Grid>
                </Grid>
                """

        let rateControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsTimed || ord.Schedule.IsContinuous ->
                let nav =
                    let mode =
                        ord.Orderable.Dose.Rate
                        |> QuantityModePolicy.decideFor QuantityModePolicy.Field.DoseRate ord

                    ViewHelpers.createStepper
                        dispatch
                        revision
                        mode
                        (ord.Orderable.Dose.Rate |> ViewHelpers.hasLargeStep)
                        SetMinDoseRateProperty
                        DecreaseDoseRateProperty
                        SetMedianDoseRateProperty
                        IncreaseDoseRateProperty
                        SetMaxDoseRateProperty
                        (ord.Orderable.Dose.Rate |> ViewHelpers.ovarStep string)
                        (ord.Orderable.Dose.Rate |> ViewHelpers.largeStepText)

                let severity = ord.Orderable.Dose.Rate |> markOf
                let label = ord.Orderable.Dose.Rate |> ViewHelpers.ovarLabel "infuussnelheid"

                let rateDisplay =
                    ord.Orderable.Dose.Rate
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> select
                        false
                        label
                        None
                        (ChangeOrderableDoseRate >> dispatch)
                        nav
                        severity
                        (reopenOf ord.Orderable.Dose.Rate)
                        (Some 400)

                let timeDisplay =
                    ord.Schedule.Time
                    |> ViewHelpers.ovarDisplay display "looptijd" (fixPrecision 3) (Some 400)

                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                import Box from '@mui/material/Box';
                <Grid container spacing={{2}} sx={flexEndSx}>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {rateDisplay}
                        </Box>
                    </Grid>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {timeDisplay}
                        </Box>
                    </Grid>
                </Grid>
                """
            | _ -> null

        let totalVolumeDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.OrderableQuantity
                |> ViewHelpers.ovarDisplay display "totaal volume" string (Some 400)
            | None -> null

        let onClickReset = fun () -> ResetOrderScenario |> dispatch

        let heading label =
            Components.SectionHeading.View
                {|
                    label = label
                    action = None
                |}

        let administrationDivider = Terms.``Prescribe Administration`` |> getTerm "toediening" |> heading

        let preparationHeading = Terms.``Prescribe Preparation`` |> getTerm "bereiding" |> heading
        let dosingHeading = heading "dosering"

        let headerRow =
            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            <Grid container spacing={{2}}>
                <Grid size={halfSize}>
                    {preparationHeading}
                </Grid>
                <Grid size={halfSize}>
                    {dosingHeading}
                </Grid>
            </Grid>
            """

        let preparationSection =
            if isEnteral then
                null
            else
                JSX.jsx
                    $"""
                import Divider from '@mui/material/Divider';
                <>
                    {headerRow}
                    {componentRows |> unbox<seq<ReactElement>> |> React.Fragment}
                    <Divider />
                    {totalVolumeDisplay}
                </>
                """

        // the changes to the order are discarded: bounded and to the left, so the button is not
        // as wide as the panel it sits in and is not where you click by default
        let resetBar =
            Components.ActionBar.View
                {|
                    actions =
                        [|
                            {|
                                label = Terms.Reset |> getTerm "Reset"
                                kind = Components.ActionBar.Kind.Secondary
                                onClick = onClickReset
                                disabled = isOrderLoading
                                icon = Some Mui.Icons.RefreshIcon
                            |}
                        |]
                |}

        // the spinner lies over the details and takes no room, so the fields stay where they are
        // while the order reloads
        let detailsSx = {| position = "relative" |}

        let progressSx =
            {|
                position = "absolute"
                inset = 0
                display = "flex"
                alignItems = "center"
                justifyContent = "center"
                pointerEvents = "none"
            |}

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            import Divider from '@mui/material/Divider';
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';
            <Box sx={detailsSx}>
                <Stack direction={"column"} spacing={1} >
                    {preparationSection}
                    {if isEnteral then null else administrationDivider}
                    {frequencyDoseRow}
                    {rateControl}
                    {resetBar}
                </Stack>
                <Box sx={progressSx}>{loadingIndicator}</Box>
            </Box>
            """

        let indicationFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indications |> Array.length <= 1 then
                null
            else
                let sel = ctx.Filter.Indication
                let items = ctx.Filter.Indications
                let lbl = Terms.Indication |> getTerm "Indicatie"

                if isMobile then
                    items |> Array.map (fun s -> s, s) |> filterSelect lbl sel indicationChange
                else
                    items |> autoComplete lbl sel indicationChange

        let doseTypeFilter =
            if
                ctx.Filter.Generic.IsNone
                || ctx.Filter.Indication.IsNone
                || ctx.Filter.DoseTypes |> Array.length <= 1
            then
                null
            else
                let sel = ctx.Filter.DoseType |> Option.map DoseType.doseTypeToString
                let items = ctx.Filter.DoseTypes
                let lbl = Terms.``Dose Type`` |> getTerm "Doseer type"

                items
                |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                |> filterSelect lbl sel doseTypeChange

        let filterSx = {| marginBottom = 2 |}

        let filterControls =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction="column" spacing={2} sx={filterSx}>
                {if isEnteral && displayOrder.IsSome then
                     null
                 else
                     genericFilter}
                {indicationFilter}
                {doseTypeFilter}
            </Stack>
            """

        let orderDetails = if displayOrder.IsSome then details else loadingIndicator

        let expanded, setExpanded = React.useState true

        let handleAccordionChange = fun _ -> setExpanded (not expanded)

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                // the plan takes one change at a time: no removal while one is under way
                let handleClick (e: Browser.Types.Event) =
                    e.stopPropagation ()

                    if not props.isRecalculating then
                        onRemove ()

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import DeleteIcon from '@mui/icons-material/Delete';
                <Box
                    component="span"
                    onClick={handleClick}
                    sx={removeButtonSx}
                >
                    <DeleteIcon fontSize="small" />
                </Box>
                """
            | None -> null

        if props.wrapInAccordion then
            let summary =
                let adminSummary =
                    match ctx.Scenarios with
                    | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                        renderAdminSummary (string props.nutritionContext.Id) sc.Order.Orderable.Name sc.Administration
                    | _ -> null

                JSX.jsx
                    $"""
                import React from 'react';
                import Typography from '@mui/material/Typography';
                import Box from '@mui/material/Box';

                <Box sx={flexOverflowSx}>
                    <Typography>{label}</Typography>
                    {adminSummary}
                    <Box sx={autoMarginSx}>
                        {removeButton}
                    </Box>
                </Box>
                """

            let children =
                JSX.jsx
                    $"""
                import React from 'react';

                <React.Fragment>
                    {filterControls}
                    {orderDetails}
                </React.Fragment>
                """

            Components.Disclosure.View
                {|
                    isOpen = expanded
                    onToggle = fun () -> setExpanded (not expanded)
                    summary = summary
                    children = children
                    isMobile = isMobile
                    detailsPaddingTop = if isMobile then None else Some 4
                    ariaControls = None
                    summaryId = None
                |}
        else
            // the section's name on its line, with the button that removes the section beside it
            let heading =
                Components.SectionHeading.View
                    {|
                        label = label
                        action = Some removeButton
                    |}

            JSX.jsx
                $"""
            import React from "react";
            import Box from '@mui/material/Box';

            <Box>
                {heading}
                {filterControls}
                {orderDetails}
            </Box>
            """


    [<JSX.Component>]
    let private AddButton
        (props:
            {|
                label: string
                onClick: unit -> unit
                disabled: bool
            |})
        =
        JSX.jsx
            $"""
        import Button from '@mui/material/Button';
        import AddIcon from '@mui/icons-material/Add';
        <Button
            variant="outlined"
            size="small"
            startIcon={{ <AddIcon /> }}
            disabled={props.disabled}
            onClick={fun _ -> props.onClick ()}
            sx={addButtonSx}
        >
            {props.label}
        </Button>
        """


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        // the one plan: the nutrition workbenches live in the order plan, which is there
        // with the patient
        let envOrderPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv
        let orderPlan = envOrderPlan.OrderPlan
        let planCommand = envOrderPlan.OrderPlanCommand

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

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

        let confirmDeleteTarget, setConfirmDeleteTarget = React.useState<string option> None
        let enteralExpanded, setEnteralExpanded = React.useState true
        let printOpen, setPrintOpen = React.useState false

        let makeSlot wrapInAccordion (plan: OrderPlan) (nc: OrderContext) =
            let onRemove =
                let hasSupplements =
                    nc |> isOneOf [ NutritionCategory.EnteralFeeding ]
                    && plan.OrderContexts
                       |> Array.exists (isOneOf [ NutritionCategory.EnteralSupplement ])

                Some(fun () ->
                    if hasSupplements then
                        setConfirmDeleteTarget (Some nc.Id)
                    else
                        Api.OrderPlanCommand.RemoveOrderContexts(plan, [| nc.Id |]) |> planCommand
                )

            NutritionSlot
                {|
                    nutritionContext = nc
                    plan = plan
                    planCommand = planCommand
                    planReopen = envOrderPlan.Reopen
                    planRestore = envOrderPlan.Restore
                    localizationTerms = localizationTerms
                    onRemove = onRemove
                    wrapInAccordion = wrapInAccordion
                    isRecalculating = isRecalculating
                |}

        let newOrderContext (plan: OrderPlan) category =
            Api.OrderPlanCommand.NewOrderContext(plan, category) |> planCommand

        let content =
            match orderPlan with
            | OrderPlanView.Settled(plan, _)
            | OrderPlanView.Changing(plan, _) ->
                let enteralContexts = plan.OrderContexts |> Array.filter (isOneOf enteral)
                let parenteralContexts = plan.OrderContexts |> Array.filter (isOneOf parenteral)

                let enteralSlots = enteralContexts |> Array.map (makeSlot false plan)

                let parenteralSlots = parenteralContexts |> Array.map (makeSlot true plan)

                // the buttons the plan admits a context for, as the server would rule
                let mayAdd category = plan |> OrderPlan.mayAdd category

                let enteralFeedingAddButton =
                    if mayAdd NutritionCategory.EnteralFeeding then
                        AddButton
                            {|
                                label = Terms.``Nutrition Enteral Feeding`` |> getTerm "Enterale Voeding"
                                onClick = fun () -> newOrderContext plan NutritionCategory.EnteralFeeding
                                disabled = isRecalculating
                            |}
                    else
                        null

                let supplementAddButton =
                    if mayAdd NutritionCategory.EnteralSupplement then
                        AddButton
                            {|
                                label = Terms.``Nutrition Add Supplement`` |> getTerm "Supplement toevoegen"
                                onClick = fun () -> newOrderContext plan NutritionCategory.EnteralSupplement
                                disabled = isRecalculating
                            |}
                    else
                        null

                let parenteralAddButtons =
                    [|
                        if mayAdd NutritionCategory.TPN then
                            AddButton
                                {|
                                    label = Terms.``Nutrition TPN`` |> getTerm "TPN"
                                    onClick = fun () -> newOrderContext plan NutritionCategory.TPN
                                    disabled = isRecalculating
                                |}
                        if mayAdd NutritionCategory.Lipid then
                            AddButton
                                {|
                                    label = Terms.``Nutrition Lipids`` |> getTerm "Vetten"
                                    onClick = fun () -> newOrderContext plan NutritionCategory.Lipid
                                    disabled = isRecalculating
                                |}
                        if mayAdd NutritionCategory.ElectrolyteGlucose then
                            AddButton
                                {|
                                    label = Terms.``Nutrition Electrolytes Glucose`` |> getTerm "Elektrolyten/Glucose"
                                    onClick = fun () -> newOrderContext plan NutritionCategory.ElectrolyteGlucose
                                    disabled = isRecalculating
                                |}
                    |]

                let enteralAccordion =
                    let summary =
                        let adminSummaries =
                            enteralContexts
                            |> Array.choose (fun nc ->
                                match nc.Scenarios with
                                | [| sc |] when sc.Administration |> Array.isEmpty |> not ->
                                    renderAdminSummary (string nc.Id) sc.Order.Orderable.Name sc.Administration
                                    |> Some
                                | _ -> None
                            )

                        JSX.jsx
                            $"""
                        import Typography from '@mui/material/Typography';
                        import Box from '@mui/material/Box';

                        <Box sx={flexOverflowSx}>
                            <Typography>{Terms.``Nutrition Enteral Feeding`` |> getTerm "Enterale Voeding"}</Typography>
                            {adminSummaries |> unbox<seq<ReactElement>> |> React.Fragment}
                        </Box>
                        """

                    let children =
                        JSX.jsx
                            $"""
                        import Stack from '@mui/material/Stack';
                        <Stack direction="column" spacing={{2}}>
                            {enteralSlots |> unbox<seq<ReactElement>> |> React.Fragment}
                            {enteralFeedingAddButton}
                            {supplementAddButton}
                        </Stack>
                        """

                    Components.Disclosure.View
                        {|
                            isOpen = enteralExpanded
                            onToggle = fun () -> setEnteralExpanded (not enteralExpanded)
                            summary = summary
                            children = children
                            isMobile = isMobile
                            detailsPaddingTop = if isMobile then None else Some 4
                            ariaControls = None
                            summaryId = None
                        |}

                let printDisabled = parenteralContexts |> Array.isEmpty

                JSX.jsx
                    $"""
                import Stack from '@mui/material/Stack';
                import Typography from '@mui/material/Typography';
                import Divider from '@mui/material/Divider';
                import IconButton from '@mui/material/IconButton';
                import PrintIcon from '@mui/icons-material/Print';

                <Stack direction="column" spacing={1}>
                    {enteralAccordion}
                    <Divider sx={dividerSx} />
                    <Stack direction="row" sx={alignCenterSx} spacing={1}>
                        <Typography variant="h6">{Terms.``Nutrition Parenteral Section`` |> getTerm "Parenteraal"}</Typography>
                        <Button color="primary" size="small" disabled={printDisabled} onClick={fun _ -> setPrintOpen true} startIcon={{<PrintIcon />}}>
                            {Terms.Print |> getTerm "Print"}
                        </Button>
                    </Stack>
                    {parenteralSlots |> unbox<seq<ReactElement>> |> React.Fragment}
                    <Stack direction="row" spacing={1}>
                        {parenteralAddButtons |> unbox<seq<ReactElement>> |> React.Fragment}
                    </Stack>
                </Stack>
                """
            | OrderPlanView.NoPatient -> null

        let confirmDeleteDialog =
            let isOpen = confirmDeleteTarget.IsSome
            let handleCancel = fun () -> setConfirmDeleteTarget None

            // the plan takes one change at a time: a confirmation while one is under way removes
            // nothing and closes the dialog
            let handleConfirm =
                fun () ->
                    match confirmDeleteTarget, orderPlan with
                    | Some ncId, OrderPlanView.Settled(plan, _) ->
                        Api.OrderPlanCommand.RemoveOrderContexts(plan, [| ncId |]) |> planCommand
                    | None, _
                    | Some _, OrderPlanView.NoPatient
                    | Some _, OrderPlanView.Changing _ -> ()

                    setConfirmDeleteTarget None

            Components.ConfirmDialog.View
                {|
                    isOpen = isOpen
                    title =
                        Terms.``Nutrition Remove Enteral Title``
                        |> getTerm "Enterale voeding verwijderen"
                    text =
                        Terms.``Nutrition Remove Enteral Text``
                        |> getTerm
                            "Als u de enterale voeding verwijdert, worden ook alle bijbehorende supplementen verwijderd. Wilt u doorgaan?"
                    confirmLabel = Terms.Delete |> getTerm "Verwijderen"
                    cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                    onConfirm = handleConfirm
                    onCancel = handleCancel
                |}

        let printDialog =
            match printOpen, orderPlan with
            | true, (OrderPlanView.Settled(plan, _) | OrderPlanView.Changing(plan, _)) ->
                ParenteralPrintView
                    {|
                        plan = plan
                        onClose = fun () -> setPrintOpen false
                    |}
            | false, _
            | true, OrderPlanView.NoPatient -> null

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box>
            {patientNotice}
            {content}
            {confirmDeleteDialog}
            {printDialog}
        </Box>
        """
