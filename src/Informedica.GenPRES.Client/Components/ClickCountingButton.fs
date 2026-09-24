namespace Components


module ClickCountingButton =


    open Fable.Core
    open Feliz


    [<JSX.Component>]
    let View
        (props:
            {|
                disabled: bool
                onClick: int -> unit
                onStep: unit -> unit
                icon: JSX.Element
            |})
        =

        let count, setCount = React.useState 0
        let debounceRef = React.useRef (None: int option)
        let intervalRef = React.useRef (None: int option)
        let countRef = React.useRef 0

        // Cleanup timers on unmount
        React.useEffect (
            (fun () ->
                fun () ->
                    debounceRef.current |> Option.iter JS.clearTimeout
                    intervalRef.current |> Option.iter JS.clearInterval
            ),
            [||]
        )

        let clearDebounce () =
            debounceRef.current |> Option.iter JS.clearTimeout
            debounceRef.current <- None

        let clearHoldInterval () =
            intervalRef.current |> Option.iter JS.clearInterval
            intervalRef.current <- None

        let resetCount () =
            countRef.current <- 0
            setCount 0

        let startDebounce () =
            clearDebounce ()

            let id =
                JS.setTimeout
                    (fun () ->
                        let n = countRef.current

                        if n > 0 then
                            props.onClick n

                        resetCount ()
                        debounceRef.current <- None
                    )
                    700

            debounceRef.current <- Some id

        let increment () =
            countRef.current <- countRef.current + 1
            setCount countRef.current
            // notify the consumer of each click so the displayed value can follow
            // the live count (same instant as the badge updates)
            props.onStep ()

        let handleClick =
            fun (_: Browser.Types.MouseEvent) ->
                increment ()
                startDebounce ()

        let handleHoldStart =
            fun (_: Browser.Types.Event) ->
                clearHoldInterval ()

                let id =
                    JS.setInterval
                        (fun () ->
                            increment ()
                            startDebounce ()
                        )
                        150

                intervalRef.current <- Some id

        let handleHoldEnd = fun (_: Browser.Types.Event) -> clearHoldInterval ()

        let badgeContent = if count > 1 then count |> box else null

        JSX.jsx
            $"""
        import IconButton from "@mui/material/IconButton";
        import Badge from "@mui/material/Badge";

        <IconButton
            size="small"
            sx={Mui.Styles.stepButtonSx}
            disabled={props.disabled}
            onClick={handleClick}
            onMouseDown={handleHoldStart}
            onMouseUp={handleHoldEnd}
            onMouseLeave={handleHoldEnd}
            onTouchStart={handleHoldStart}
            onTouchEnd={handleHoldEnd}
        >
            <Badge badgeContent={badgeContent} color="primary" max={999}>
                {props.icon}
            </Badge>
        </IconButton>
        """
