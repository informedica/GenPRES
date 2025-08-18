namespace Informedica.Agents.Lib


module FileWriterAgent =

    open System.IO
    open System.Collections.Generic


    // Messages for the file writer agent
    type FileWriterMsg =
        | Append of path: string * lines: string[]
        | Flush of AsyncReplyChannel<unit>
        | Stop of AsyncReplyChannel<unit>


    type State = { Writers: Dictionary<string, StreamWriter> }


    let getWriter (state: State) (path: string) =
        match state.Writers.TryGetValue path with
        | true, w -> w
        | _ ->
            // Append, allow readers; UTF8 without BOM by default
            let fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)
            try
                let sw = new StreamWriter(fs)
                sw.AutoFlush <- false
                state.Writers[path] <- sw
                sw
            with _ -> 
                fs.Dispose()
                reraise ()


    let create () : Agent<FileWriterMsg> =
        Agent<FileWriterMsg>.Start(fun inbox ->
            let rec loop (st: State) = async {
                let! msg = inbox.Receive()
                match msg with
                | Append (path, lines) ->
                    try
                        let w = getWriter st path
                        for i = 0 to lines.Length - 1 do
                            w.WriteLine(lines[i])
                    with ex ->
                        eprintfn "File write error: %s" ex.Message
                    return! loop st

                | Flush reply ->
                    for w in st.Writers.Values do
                        try w.Flush() with _ -> ()
                    reply.Reply(())
                    return! loop st

                | Stop reply ->
                    // final flush + dispose all writers
                    for w in st.Writers.Values do
                        try
                            w.Flush()
                            w.Dispose()
                        with _ -> ()
                    st.Writers.Clear()
                    reply.Reply(())
                    // exit
            }
            
            loop { Writers = Dictionary() }
        )

    let append path lines (writer : Agent<_>) = 
        (path, lines)
        |> Append
        |> writer.Post


    let flush writer = writer |> Agent.post Flush


    let stop writer = writer |> Agent.post Stop


    let flushAsync (writer: Agent<FileWriterMsg>) =
        writer.PostAndAsyncReply(fun rc -> Flush rc)


    let stopAsync (writer: Agent<FileWriterMsg>) =
        writer.PostAndAsyncReply(fun rc -> Stop rc)