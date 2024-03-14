namespace Informedica.OpenAI.Lib



module Roles =


    [<Literal>]
    let user = "user"

    [<Literal>]
    let system = "system"

    [<Literal>]
    let assistant = "assistant"



module Conversation =

    let print (conversation : Conversation) =
        for qAndA in conversation.Messages do
            match qAndA.Answer with
            | Some answer ->
                printfn $"""
## Question:
{qAndA.Question.Content.Trim()}

## Answer:
{answer.Content.Trim()}
"""
            | _ ->
                printfn $"""
## System:
{qAndA.Question.Content.Trim()}
"""


module Message =

    let create validator role content =
        {
            Role = role
            Content = content
            Validator = validator
        }

    let assistant = create Result.Ok Roles.assistant

    let user = create Result.Ok Roles.user

    let system = create Result.Ok Roles.system

    let okMessage = create Ok

    let userWithValidator validator = create validator Roles.user


    let print (msg : Message) =
        match msg.Role with
        | Roles.user ->
            printfn $"""
## Question:
{msg.Content.Trim()}
"""
        | Roles.assistant ->
            printfn $"""
## Answer:
{msg.Content.Trim()}
"""
        | Roles.system ->
            printfn $"""
## System:
{msg.Content.Trim()}
"""
        | _ ->
            printfn $"""
## Unknown role:
{msg.Content.Trim()}
"""

