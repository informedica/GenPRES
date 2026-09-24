namespace Components


/// A section that folds to a summary carrying its result, and unfolds to its content. It opens
/// and closes when the user says so, and never by itself: a section that closed on a timer
/// took the controls away from under the hand. What the summary says is the caller's; a
/// finished step reads its outcome in it, so the section can stay folded.
module Disclosure =

    open Fable.Core


    /// The section: open or folded, what toggling it does, the summary shown either way, the
    /// content shown when open, and the spacing and ids its caller gives it.
    type Props =
        {|
            isOpen: bool
            onToggle: unit -> unit
            summary: JSX.Element
            children: JSX.Element
            isMobile: bool
            detailsPaddingTop: int option
            ariaControls: string option
            summaryId: string option
        |}


    [<JSX.Component>]
    let View (props: Props) =
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


        <Accordion expanded={props.isOpen} onChange={onChange}>
            <AccordionSummary
            sx={sx}
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
