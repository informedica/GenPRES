namespace Informedica.Utils.Lib


module Env =

    open System
    open System.IO
    open System.Linq
    open System.Collections.Generic


    /// Recursively tries to find the parent of a file starting from a directory
    let rec findParent (directory: string) (fileToFind: string) =
        let path =
            if Directory.Exists(directory) then directory else Directory.GetParent(directory).FullName

        let files = Directory.GetFiles(path)
        if files.Any(fun file -> Path.GetFileName(file).ToLower() = fileToFind.ToLower())
        then path
        else findParent (DirectoryInfo(path).Parent.FullName) fileToFind


    /// Returns enviroment variables as a dictionary
    let environmentVars() =
        let variables = Dictionary<string, string>()
        let userVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User)
        let processVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
        for pair in userVariables do
            let variable = unbox<Collections.DictionaryEntry> pair
            let key = unbox<string> variable.Key
            let value = unbox<string> variable.Value
            if not (variables.ContainsKey(key)) && key <> "PATH" then variables.Add(key, value)
        for pair in processVariables do
            let variable = unbox<Collections.DictionaryEntry> pair
            let key = unbox<string> variable.Key
            let value = unbox<string> variable.Value
            if not (variables.ContainsKey(key)) && key <> "PATH" then variables.Add(key, value)
        variables

    let getItem s =
        let vars = environmentVars()
        if not (vars.ContainsKey(s)) then None
        else
            vars.Item(s)
            |> Some