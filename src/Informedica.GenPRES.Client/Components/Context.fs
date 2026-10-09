namespace Components


module Context =


    open Feliz


    [<ReactComponent>]
    let Context (context: Global.Context) el = Global.context.Provider(context, React.Fragment [ el ])


    /// The quantity fields' count around the element: whether one counts, and how it reports.
    [<ReactComponent>]
    let Counting (counting: Global.Counting) el = Global.counting.Provider(counting, React.Fragment [ el ])
