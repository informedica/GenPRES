namespace Components


module Accordion =

    open Fable.Core


    [<JSX.Component>]
    let View
        (props:
            {|
                expanded: bool
                onChange: unit -> unit
                summary: JSX.Element
                children: JSX.Element
                isMobile: bool
                detailsPaddingTop: int option
                ariaControls: string option
                summaryId: string option
            |})
        =
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

        JSX.jsx
            $"""
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';


        <Accordion expanded={props.expanded} onChange={fun _ -> props.onChange ()}>
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
