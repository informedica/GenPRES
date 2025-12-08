namespace Components


module Router =

    open Feliz
    open Feliz.Router


    [<ReactComponent>]
    let View(props: {| onUrlChanged: string list -> unit |}) =
        React.router [
            router.onUrlChanged props.onUrlChanged
        ]
