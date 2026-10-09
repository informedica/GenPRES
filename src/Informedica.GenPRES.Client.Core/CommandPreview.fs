/// The context as an order command changes it, shown while the command's request is under way.
module CommandPreview

open Shared.Api


/// The context as the command changes it; the context itself when the command cannot change it.
let shown cmd ctx =
    match OrderViewCommand.preview cmd ctx with
    | Ok changed -> changed
    | Error _ -> ctx
