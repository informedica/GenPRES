namespace Informedica.Agents.Lib


module FileWriterAgent =

    open System
    open System.IO
    open System.Text
    open System.Collections.Generic


    // Messages for the file writer agent
    type FileWriterMsg =
        | Append of path: string * lines: string[]
        | Flush of AsyncReplyChannel<unit>
        | Stop of AsyncReplyChannel<unit>
        | Clear of path: string


    type State = { Writers: Dictionary<string, StreamWriter> }

    // ---- Encoding helpers ----------------------------------------------------


    let utf8NoBom : Encoding = new UTF8Encoding(false)


    /// Try to detect BOM-based encoding. Fallback to UTF-8 (no BOM).
    let detectEncoding (path: string) : Encoding =
        try
            if File.Exists path then
                use fs =
                    new FileStream(
                        path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite ||| FileShare.Delete
                    )
                if fs.Length >= 3L then
                    let b1 = fs.ReadByte()
                    let b2 = fs.ReadByte()
                    let b3 = fs.ReadByte()
                    match b1, b2, b3 with
                    // UTF-8 BOM
                    | 0xEF, 0xBB, 0xBF -> utf8NoBom
                    | _ ->
                        // maybe UTF-16?
                        fs.Position <- 0L
                        if fs.Length >= 2L then
                            let b1 = fs.ReadByte()
                            let b2 = fs.ReadByte()
                            match b1, b2 with
                            | 0xFF, 0xFE -> Encoding.Unicode              // UTF-16 LE
                            | 0xFE, 0xFF -> Encoding.BigEndianUnicode     // UTF-16 BE
                            | _          -> utf8NoBom
                        else utf8NoBom
                else
                    // short/empty file: pick UTF-8 (no BOM)
                    utf8NoBom
            else utf8NoBom
        with _ ->
            // If detection fails, write UTF-8 (no BOM) to avoid BOM pollution
            utf8NoBom


    /// Open a new StreamWriter with sharing that allows editors to truncate/replace.
    let openWriter (path: string) =
        // If the file already exists with a BOM indicating UTF-16, honor it
        let enc = detectEncoding path
        let fs =
            new FileStream(
                path,
                FileMode.Append,              // create if missing, append otherwise
                FileAccess.Write,
                FileShare.ReadWrite ||| FileShare.Delete,
                4096,
                FileOptions.SequentialScan
            )
        let sw = new StreamWriter(fs, enc)
        sw.AutoFlush <- false
        sw


    // ---- State management ----------------------------------------------------

    let removeWriter (st: State) (path: string) =
        match st.Writers.TryGetValue path with
        | true, w ->
            try w.Flush() with _ -> ()
            try w.Dispose() with _ -> ()
            st.Writers.Remove path |> ignore
        | _ -> ()


    let getWriter (st: State) (path: string) =
        match st.Writers.TryGetValue path with
        | true, w -> w
        | _ ->
            let sw = openWriter path
            st.Writers[path] <- sw
            sw


    /// Write lines; if a failure happens (file deleted, replaced, truncated), reopen once and retry.
    let writeLines (st: State) (path: string) (lines: string[]) =
        let rec go (reopened: bool) =
            let w = getWriter st path
            try
                // Always write complete lines
                for i in 0 .. lines.Length - 1 do
                    w.WriteLine(lines[i])
            with
            | :? ObjectDisposedException
            | :? IOException
            | :? UnauthorizedAccessException as ex ->
                if reopened then
                    // give up: surface error but don't crash the agent loop
                    eprintfn "FileWriterAgent: write failed after reopen on '%s': %s" path ex.Message
                else
                    // Attempt to heal by reopening with permissive share & detected encoding
                    removeWriter st path
                    // If file was deleted, FileMode.Append will create it again
                    let _ = getWriter st path
                    go true
        go false


    let create () : Agent<_> =
        Agent.Start(fun inbox ->
            let rec loop (state: State) = async {
                let! msg = inbox.Receive()

                match msg with
                | Append (path, lines) ->
                    try writeLines state path lines
                    with ex -> eprintfn "FileWriterAgent: unexpected error: %s" ex.Message
                    return! loop state
                | Flush reply ->
                    for w in state.Writers.Values do
                        try w.Flush() with _ -> ()
                    reply.Reply(())
                    return! loop state
                | Clear path ->
                    try
                        removeWriter state path
                        use _ = new FileStream(
                            path,
                            FileMode.Create,
                            FileAccess.Write, 
                            FileShare.ReadWrite ||| FileShare.Delete
                        )
                        ()
                    with ex ->
                        eprintfn $"FileWriterAgent: clear failed for {path}\n{ex.Message}"
                    return! loop state
                | Stop reply ->
                    // final flush + dispose all writers
                    for w in state.Writers.Values do
                        try
                            w.Flush()
                            w.Dispose()
                        with _ -> ()
                    state.Writers.Clear()
                    reply.Reply(())
                    // exit
            }
            
            loop { Writers = Dictionary() }
        )


    let append path lines (writer : Agent<_>) = 
        (path, lines)
        |> Append
        |> writer.Post
        writer


    let flush (writer: Agent<_>) = 
        writer.PostAndReply(fun rc -> Flush rc)
        writer


    let clear path (writer: Agent<_>)  =
        writer.Post (Clear path)
        writer


    let stop (writer: Agent<_>) = 
        writer.PostAndReply(fun rc -> Stop rc)
        writer


    let flushAsync (writer: Agent<FileWriterMsg>) =
        writer.PostAndAsyncReply(fun rc -> Flush rc)


    let stopAsync (writer: Agent<FileWriterMsg>) =
        writer.PostAndAsyncReply(fun rc -> Stop rc)

