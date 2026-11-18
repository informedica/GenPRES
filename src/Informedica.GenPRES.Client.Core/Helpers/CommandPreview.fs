namespace Informedica.GenPRES.Client.Core.Helpers

/// Shows the order context as a command will change it while the request runs.
module CommandPreview =

    open Informedica.GenPRES.Shared.Api


    /// Returns the context as the command changes it, or the unchanged context when the preview fails.
    let shown cmd ctx =
        match OrderViewCommand.preview cmd ctx with
        | Ok changed -> changed
        | Error _ -> ctx
