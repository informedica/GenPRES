namespace Informedica.OpenAI.Lib


[<AutoOpen>]
module Types =

    open System


    type Message =
        {
            Role : string
            Content : string
            Validator : string -> Result<string, string>
        }


    type Conversation =
        {
            Model : string
            Messages : QuestionAnswer list
        }
    and QuestionAnswer =
        {
            Question : Message
            Answer : Message
        }


    type Response<'T> =
        {
            Original : string
            Response: 'T
        }