namespace Components


module Context =


    open Feliz


    [<ReactComponent>]
    let Context (context: Global.Context) el = Global.context.Provider(context, React.Fragment [ el ])
