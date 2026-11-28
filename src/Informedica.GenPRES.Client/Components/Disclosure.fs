namespace Components


/// A section that folds to a summary and unfolds to its content. It opens and closes only when
/// the user toggles it, never on a timer, so controls do not disappear while the user works in them.
module Disclosure =

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// Where the summary of a section goes when the page scrolls.
    [<RequireQualifiedAccess>]
    type SummaryPlacement =
        /// The summary scrolls away with the page.
        | Scrolls
        /// The summary sticks to the top of the scrolling page, below the stacked summaries of
        /// the sections before it in the same container.
        | Stacks


    /// Whether the section is open, the toggle handler, the summary shown always, the content
    /// shown when open, where the summary goes when the page scrolls, and the layout options and ids.
    type Props =
        {|
            isOpen: bool
            onToggle: unit -> unit
            summary: JSX.Element
            children: JSX.Element
            summaryPlacement: SummaryPlacement
            isMobile: bool
            detailsPaddingTop: int option
            ariaControls: string option
            summaryId: string option
        |}


    /// The class that marks a summary that stacks.
    let stackedClass = "disclosure-stacked"


    [<Emit("new ResizeObserver($0)")>]
    let private resizeObserver (callback: unit -> unit) : obj = jsNative


    [<Emit("Array.from($0)")>]
    let private toArray (nodes: obj) : obj[] = jsNative


    /// Sets the top of a stacked summary to the total height of the stacked summaries before it
    /// in the container of its section.
    let stack (summary: obj) (container: obj) =
        let tops =
            container?querySelectorAll ($".%s{stackedClass}")
            |> toArray
            |> Array.takeWhile (fun other -> not (obj.ReferenceEquals(other, summary)))
            |> Array.sumBy (fun other -> other?offsetHeight: float)

        summary?style?top <- $"%.0f{tops}px"


    [<JSX.Component>]
    let View (props: Props) =
        let stacks = props.summaryPlacement = SummaryPlacement.Stacks

        let summaryRef = React.useRef null

        // A stacked summary moves when a summary before it changes height, or when a section is
        // added or removed; each of those changes the height of the container, which is observed.
        React.useEffect (
            (fun () ->
                let summary = summaryRef.current

                if stacks && not (isNull summary) then
                    let container = summary?closest(".MuiAccordion-root")?parentElement
                    let observer = resizeObserver (fun () -> stack summary container)
                    observer?observe (container)
                    fun () -> observer?disconnect ()
                else
                    ignore
            ),
            [| box stacks |]
        )

        let sx =
            {|
                bgcolor = Mui.Styles.headerBgColor
                paddingTop = (if props.isMobile then 0 else 1)
                paddingBottom = (if props.isMobile then 0 else 1)
                minHeight = 0
                ``&.Mui-expanded`` = {| minHeight = 0 |}
                ``& .MuiAccordionSummary-content`` =
                    {|
                        margin = 0
                        display = "flex"
                        alignItems = "center"
                    |}
                ``& .MuiAccordionSummary-content.Mui-expanded`` = {| margin = 0 |}
            |}

        // A stacked summary escapes its section: the boxes of the section and of its heading are
        // removed from the layout, so the summary sticks within the container of all sections, and
        // the stacked summaries keep to the top until the end of that container. The z-index keeps a
        // stacked summary above the content, where input labels sit at z-index 1. The border in the
        // background colour leaves room between the stacked summaries and hides the content that
        // scrolls below them; the border is part of the height that stack adds up.
        let stackedSummarySx =
            {|
                position = "sticky"
                top = 0
                zIndex = 2
                borderBottom = "6px solid"
                borderColor = "background.paper"
            |}

        let accordionSx =
            {|
                display = "contents"
                ``&::before`` = {| display = "none" |}
                ``& > .MuiAccordion-heading`` = {| display = "contents" |}
            |}

        let summarySx =
            if stacks then
                [| box sx; box stackedSummarySx |]
            else
                [| box sx |]

        let summaryClass = if stacks then stackedClass else ""

        let rootSx = if stacks then box accordionSx else null

        let detailsPadding = props.detailsPaddingTop |> Option.defaultValue (if props.isMobile then 1 else 2)

        let ariaControls = props.ariaControls |> Option.defaultValue ""

        let summaryId = props.summaryId |> Option.defaultValue ""

        let onChange = fun _ -> props.onToggle ()

        JSX.jsx
            $"""
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';


        <Accordion expanded={props.isOpen} onChange={onChange} sx={rootSx}>
            <AccordionSummary
            ref={summaryRef}
            className={summaryClass}
            sx={summarySx}
            expandIcon={{ <ExpandMoreIcon /> }}
            aria-controls={ariaControls}
            id={summaryId}
            >
            {props.summary}
            </AccordionSummary>
            <AccordionDetails sx={ {| paddingTop = detailsPadding |} }>
                {props.children}
            </AccordionDetails>
        </Accordion>
        """
