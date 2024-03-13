namespace Informedica.OpenAI.Lib




module Roles =

    let user = "user"
    let system = "system"
    let assistent = "assistent"



module Conversation =

    let print (conversation : Conversation) =
        for qAndA in conversation.Messages do
            printfn $"""
## Question:
{qAndA.Question.Content.Trim()}

## Answer:
{qAndA.Answer.Content.Trim()}

"""


module Message =

    let create validator role content =
        {
            Role = role
            Content = content
            Validator = validator
        }

    let user = create Result.Ok Roles.user

    let system = create Result.Ok Roles.system

    let okMessage = create Ok

    let userWithValidator validator = create validator Roles.user

