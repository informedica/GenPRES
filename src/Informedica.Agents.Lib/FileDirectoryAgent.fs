namespace Informedica.Agents.Lib

module FileDirectoryAgent =

    open System
    open System.IO
    open System.Collections.Generic

    /// Messages for the directory maintenance agent
    type FileDirectoryMsg =
        | SetPolicy of dir: string * maxFiles: int * searchPattern: string option
        | RemovePolicy of dir: string
        | Prune of dir: string * AsyncReplyChannel<Result<int, string>>
        | Enforce of dir: string * maxFiles: int * searchPattern: string option * AsyncReplyChannel<Result<int, string>>
        | Stop of AsyncReplyChannel<unit>

    type Policy = { MaxFiles: int; Pattern: string }

    type State = { Policies: Dictionary<string, Policy> }

    let private defaultPattern = "*"

    let private normalizeDir (dir: string) =
        if String.IsNullOrWhiteSpace dir then 
            invalidArg (nameof dir) "Directory must not be null or empty"
        Path.GetFullPath dir

    /// Return number of deleted files; oldest by LastWriteTimeUtc
    let private pruneOnce (dir: string) (pattern: string) (maxFiles: int) : Result<int, string> =
        try
            let fullDir = normalizeDir dir

            if maxFiles < 0 then invalidArg (nameof maxFiles) "maxFiles must be >= 0"

            if not (Directory.Exists fullDir) then
                // Nothing to do; consider creating? We'll no-op with 0 deletions.
                Ok 0
            else
                let files =
                    try Directory.EnumerateFiles(fullDir, pattern, SearchOption.TopDirectoryOnly)
                    with _ -> seq { }

                // materialize and sort by LastWriteTimeUtc ascending (oldest first)
                let fileInfos =
                    files
                    |> Seq.map (fun p ->
                        let ts =
                            try File.GetLastWriteTimeUtc p
                            with _ -> DateTime.MinValue
                        struct (p, ts)
                    )
                    |> Seq.toArray

                let count = fileInfos.Length
                if count <= maxFiles then Ok 0
                else
                    let toDelete = count - maxFiles
                    // sort by timestamp ascending; then take first N
                    Array.sortInPlaceBy (fun (struct (_, ts)) -> ts) fileInfos
                    let mutable deleted = 0
                    let mutable errors : string list = []
                    for i in 0 .. toDelete - 1 do
                        let struct (fp, _) = fileInfos[i]
                        try
                            File.SetAttributes(fp, FileAttributes.Normal)
                        with _ -> ()
                        try
                            File.Delete fp
                            deleted <- deleted + 1
                        with ex ->
                            errors <- (sprintf "Failed delete %s: %s" fp ex.Message) :: errors
                    if errors.IsEmpty then Ok deleted
                    else
                        // Return partial success but include first error for visibility
                        Ok deleted
        with ex -> Error ex.Message

    let private getPolicy (st: State) (dir: string) : Policy option =
        match st.Policies.TryGetValue dir with
        | true, p -> Some p
        | _ -> None

    let private setPolicyInternal (st: State) (dir: string) (maxFiles: int) (patternOpt: string option) =
        let dirFull = normalizeDir dir
        let pat = defaultArg patternOpt defaultPattern
        if maxFiles < 0 then invalidArg (nameof maxFiles) "maxFiles must be >= 0"
        st.Policies[dirFull] <- { MaxFiles = maxFiles; Pattern = pat }

    let private removePolicyInternal (st: State) (dir: string) =
        let dirFull = normalizeDir dir
        st.Policies.Remove dirFull |> ignore

    let private pruneWithPolicy (st: State) (dir: string) : Result<int, string> =
        let dirFull = normalizeDir dir
        match getPolicy st dirFull with
        | Some pol -> pruneOnce dirFull pol.Pattern pol.MaxFiles
        | None -> Ok 0

    let create () : Agent<FileDirectoryMsg> =
        Agent.Start(fun inbox ->
            let rec loop (state: State) = async {
                let! msg = inbox.Receive()
                match msg with
                | SetPolicy (dir, maxFiles, pattern) ->
                    try setPolicyInternal state dir maxFiles pattern with ex -> eprintfn "FileDirectoryAgent SetPolicy error: %s" ex.Message
                    return! loop state

                | RemovePolicy dir ->
                    try removePolicyInternal state dir with _ -> ()
                    return! loop state

                | Prune (dir, reply) ->
                    let res = pruneWithPolicy state dir
                    reply.Reply res
                    return! loop state

                | Enforce (dir, maxFiles, pattern, reply) ->
                    let res = pruneOnce dir (defaultArg pattern defaultPattern) maxFiles
                    reply.Reply res
                    return! loop state

                | Stop reply ->
                    reply.Reply(())
                    // exit
            }

            loop { Policies = Dictionary() }
        )

    // Convenience API ---------------------------------------------------------

    let setPolicy dir maxFiles (agent: Agent<FileDirectoryMsg>) =
        SetPolicy (dir, maxFiles, None) |> agent.Post; agent

    let setPolicyWithPattern dir maxFiles pattern (agent: Agent<FileDirectoryMsg>) =
        SetPolicy (dir, maxFiles, Some pattern) |> agent.Post; agent

    let removePolicy dir (agent: Agent<FileDirectoryMsg>) =
        RemovePolicy dir |> agent.Post; agent

    let prune dir (agent: Agent<FileDirectoryMsg>) =
        agent.PostAndReply(fun rc -> Prune (dir, rc))

    let pruneAsync dir (agent: Agent<FileDirectoryMsg>) =
        agent.PostAndAsyncReply(fun rc -> Prune (dir, rc))

    let enforce dir maxFiles (agent: Agent<FileDirectoryMsg>) =
        agent.PostAndReply(fun rc -> Enforce (dir, maxFiles, None, rc))

    let enforceWithPattern dir maxFiles pattern (agent: Agent<FileDirectoryMsg>) =
        agent.PostAndReply(fun rc -> Enforce (dir, maxFiles, Some pattern, rc))

    let stop (agent: Agent<FileDirectoryMsg>) =
        agent.PostAndReply(fun rc -> Stop rc)

    let stopAsync (agent: Agent<FileDirectoryMsg>) =
        agent.PostAndAsyncReply(fun rc -> Stop rc)
