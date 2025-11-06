open System
open System.IO
open System.Text.RegularExpressions

let commitMsgFile = fsi.CommandLineArgs[1]

let types = "feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert"
let scopes = "gensolver|genorder|genunits|zindex|utils|client|server|api|ui|config|deps|docker|github|build|deploy"
let pattern = $"^(?:%s{types})(?:\((?:%s{scopes})\))?(?::) "
let msgHeading = commitMsgFile |> File.ReadAllLines |> Array.head

if Regex.IsMatch(msgHeading, pattern) then
    exit 0
else
    Console.Error.WriteLine "Invalid commit message"
    printfn "Valid examples: 'feat(gensolver): add something', 'docs: update README.md'"
    printfn "See guidelines in .github/instructions/commit-message.instructions.md"

    exit 1
