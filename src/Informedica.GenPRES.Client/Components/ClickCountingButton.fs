namespace Components


module ClickCountingButton =


    open Fable.Core
    open Feliz


    /// An arrow that steps on each click and repeats while held. It sends nothing itself: the
    /// field adds up the clicks of all its arrows and sends them as one command. The badge shows
    /// the clicks the field holds in this arrow's direction.
    [<JSX.Component>]
    let View
        (props:
            {|
                disabled: bool
                count: int
                onStep: unit -> unit
                icon: JSX.Element
            |})
        =

        let intervalRef = React.useRef (None: int option)

        // Cleanup the hold timer on unmount
        React.useEffect ((fun () -> fun () -> intervalRef.current |> Option.iter JS.clearInterval), [||])

        let clearHoldInterval () =
            intervalRef.current |> Option.iter JS.clearInterval
            intervalRef.current <- None

        let handleClick = fun (_: Browser.Types.MouseEvent) -> props.onStep ()

        let handleHoldStart =
            fun (_: Browser.Types.Event) ->
                clearHoldInterval ()
                let id = JS.setInterval (fun () -> props.onStep ()) 150
                intervalRef.current <- Some id

        let handleHoldEnd = fun (_: Browser.Types.Event) -> clearHoldInterval ()

        let badgeContent = if props.count > 1 then props.count |> box else null

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
