open System
open System.IO
open System.Text.RegularExpressions

let commitMsgFile = fsi.CommandLineArgs[1]

let types = "feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert"

let scopes =
    "agents|logging|nlp|genunits|gensolver|gencore|zindex|zform|nkf|ftk|genform|genorder|geninteract|mcp|utils|client|server|api|ui|config|deps|deps-dev|docker|github|deploy"

let pattern = $"^(?:%s{types})(?:\((?:%s{scopes})\))?(?::) "
let msgHeading = commitMsgFile |> File.ReadAllLines |> Array.head

let toList (s: string) =
    s.Split('|') |> Array.map (fun s -> $"- {s}") |> String.concat "\n"

if Regex.IsMatch(msgHeading, pattern) then
    exit 0
else
    Console.Error.WriteLine "Invalid commit message"
    printfn "Valid examples: 'feat(gensolver): add something', 'docs: update README.md'"
    printfn "See guidelines in .github/instructions/commit-message.instructions.md"
    printfn ""
    printfn $"Valid types:\n{types |> toList}"
    printfn ""
    printfn $"Valid scopes:\n{scopes |> toList}"
    exit 1
