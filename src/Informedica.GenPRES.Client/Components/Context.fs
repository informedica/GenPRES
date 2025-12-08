namespace Components


module Context =


    open Feliz


    [<ReactComponent>]
    let Context (context: Global.Context) el =
        React.contextProvider (
            Global.context,
            context,
            React.fragment [ el ]
        )