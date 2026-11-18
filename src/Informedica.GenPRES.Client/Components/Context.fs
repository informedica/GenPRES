namespace Components


module Context =


    open Feliz


    [<ReactComponent>]
    let Context (context: Global.Context) el = Global.context.Provider(context, React.Fragment [ el ])


    /// Provides the step click counting state of the quantity fields to the element.
    [<ReactComponent>]
    let Counting (counting: Global.Counting) el = Global.counting.Provider(counting, React.Fragment [ el ])
