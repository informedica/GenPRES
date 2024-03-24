namespace Informedica.OpenAI.Lib


module OpenAI =

    open System
    open Newtonsoft.Json

    module EndPoints =


        [<Literal>]
        let chat = "https://api.openai.com/v1/chat/completions"

        [<Literal>]
        let models = "https://api.openai.com/v1/models"

        [<Literal>]
        let completions = "https://api.openai.com/v1/completions"


    type Models =
        {
            data : Model list
        }
    and Model =
        {
            id: string
            object: string
            owned_by: string
            created: string
        }



    type ResponseFormat = {
        ``type``: string
    }


    module Chat =

        type ChatInput = {
            model: string
            messages: {| role : string; content : string |} list
            tools: obj list option
            max_tokens: int
            temperature: float
            top_p: float
            frequency_penalty: float
            presence_penalty: float
            n: int
            stop: string list
            response_format: ResponseFormat
            stream: bool
            user: string
        }


        // Function to create a ModelInput with default values
        let defaultChatInput model (msg : Message) (msgs : Message list) : ChatInput =
            let map msg = {| role = msg.Role; content = msg.Content |}
            let msgs = msgs |> List.map map

            {
                model = model
                messages =
                    [ msg |> map ]
                    |> List.append msgs
                tools = None
                max_tokens = 200
                temperature = 0.
                top_p = 1.0
                frequency_penalty = 0.0
                presence_penalty = 0.0
                n =1
                stop = []
                response_format = { ``type`` = "text" }
                stream = false
                user = "user"
            }


        type Choice = {
            index: int
            message: {| role : string; content : string |}
            finish_reason: string
        }

        type Usage = {
            prompt_tokens: int
            completion_tokens: int
            total_tokens: int
        }

        type ChatResponse = {
            id: string
            object: string
            created: int64
            model: string
            system_fingerprint: string
            choices: Choice list
            usage: Usage
        }


        let print (chat : ChatInput) =
            chat.messages
            |> List.iter (fun m ->
                match m.role with
                | Roles.user ->
                    printfn $"""
## Question:
{m.content.Trim()}
"""
                | Roles.assistant ->
                    printfn $"""
## Answer:
{m.content.Trim()}
"""
                | Roles.system ->
                    printfn $"""
## System:
{m.content.Trim()}
"""
                | _ ->
                    printfn $"""
## Unknown role:
{m.content.Trim()}
"""
            )


    module Completions =


        type CompletionInput = {
            model: string
            prompt: string
            images: string list
            max_tokens: int
            logprobs: int
            echo: bool
            temperature: float
            top_p: float
            top_k: int
            frequency_penalty: float
            presence_penalty: float
            n: int
            stop: string
            response_format: ResponseFormat
            stream: bool
            context_length_exceeded_behavior: string
            user: string
        }

        let defaultCompletionInput model prompt = {
            model = model
            prompt = prompt
            images = []
            max_tokens = 16
            logprobs = 0
            echo = false
            temperature = 0.
            top_p = 1.0
            top_k = 50
            frequency_penalty = 0.0
            presence_penalty = 0.0
            n = 1
            stop = "string"
            response_format = { ``type`` = "text" }
            stream = false
            context_length_exceeded_behavior = "truncate"
            user = "user"
        }


        type LogProbs = {
            tokens: string list
            token_logprobs: float list
            top_logprobs: obj option // As the actual structure isn't defined, using 'obj option' to represent possible null or object
            text_offset: int list
            token_ids: int list
        }

        type Choice = {
            index: int
            text: string
            logprobs: LogProbs
            finish_reason: string
        }

        type Usage = {
            prompt_tokens: int
            total_tokens: int
            completion_tokens: int
        }

        type CompletionResponse = {
            id: string
            object: string
            created: int64 // Using int64 for Unix timestamp
            model: string
            choices: Choice list // Representing array of objects as a list
            usage: Usage
        }


    // Define the API key and endpoint
    let apiKey =
        let var = Environment.GetEnvironmentVariable("OPEN_AI_KEY")
        if var |> String.IsNullOrEmpty then None
        else var |> Some


    let list () =
        Utils.get<Models>
            EndPoints.models
            apiKey
        |> Async.RunSynchronously
        |> function
           | Ok resp ->
                resp.Response.data
                |> List.map _.id
           | Error e -> [ e ]

    let completion (input : Completions.CompletionInput) =
        input
        |> JsonConvert.SerializeObject
        |> Utils.post<Completions.CompletionResponse>
               EndPoints.completions
               apiKey


    let chat (input : Chat.ChatInput) =
        input
        |> JsonConvert.SerializeObject
        |> Utils.post<Chat.ChatResponse>
            EndPoints.chat
            apiKey


    let chatJson<'Schema> (input : Chat.ChatInput) =
        { input with
            response_format =
                {
                    ``type`` = "json_object"
                }
        }
        |> JsonConvert.SerializeObject
        |> Utils.post<Chat.ChatResponse>
            EndPoints.chat
            apiKey


    let validate<'ReturnType> (validator : string -> Result<string, string>) (input : Chat.ChatInput) =
        let original = input.messages |> List.last

        async {
            let rec validateLoop reTry (input: Chat.ChatInput) =
                async {
                    let! resp = chatJson<'ReturnType> (input : Chat.ChatInput)
                    match resp with
                    | Ok result ->
                        let answer =
                            result.Response.choices
                            |> List.last

                        match answer.message.content |> validator with
                        | Ok _ ->
                            let validationResult =
                                answer.message.content
                                |> JsonConvert.DeserializeObject<'ReturnType>

                            input |> Chat.print
                            return Ok validationResult
                        | Error err ->
                            if not reTry then
                                input |> Chat.print
                                return Error err
                            else

                                let updatedInput =
                                    { input with
                                        messages =
                                            [{|
                                                role = "user"
                                                content = $"The answer: {answer.message.content} was not correct because of %s{err}. Please try again answering:\n\n%s{original.content}"
                                            |}]
                                            |> List.append input.messages
                                    }
                                return! validateLoop false updatedInput
                    | Error err ->
                        if not reTry then
                            input |> Chat.print
                            return err |> Error
                        else
                            let msg =
                                input.messages
                                |> List.last
                            let updatedInput =
                                { input with
                                    messages =
                                        [{|
                                            role = "user"
                                            content = $"There was an error: %s{err}. Please try again answering:\n\n%s{original.content}"
                                        |}]
                                        |> List.append input.messages
                                }
                            return! validateLoop false updatedInput
                }
            return! validateLoop true (input : Chat.ChatInput)
        }


    let run (model : string) messages message =
        Chat.defaultChatInput model message messages
        |> chat
        |> Async.RunSynchronously
        |> function
            | Ok response ->
                let response =
                    response.Response.choices
                    |> List.last
                    |> _.message

                [
                    message
                    Message.okMessage response.role response.content
                ]
                |> List.append messages
            | Error s ->
                printfn $"oops: {s}"
                messages



    module Operators =


        let init model msg =
            printfn $"""Starting conversation with {model}
"""

            let msg = msg |> Message.system

            {

                Model = model
                Messages =
                [{
                    Question = msg
                    Answer = None
                }]
            }


        let rec private loop tryAgain conversation msg =
            let msgs =
                conversation.Messages
                |> List.collect (fun m ->
                    match m.Answer with
                    | Some answer -> [m.Question; answer ]
                    | None -> [ m.Question ]
                )

            msg
            |> run conversation.Model msgs
            |> fun msgs ->
                    let answer = msgs |> List.last

                    let newConv =
                        { conversation with
                            Messages =
                                [{
                                    Question = msg
                                    Answer = Some answer
                                }]
                                |> List.append conversation.Messages
                        }


                    match answer.Content |> msg.Validator with
                    | Ok _ -> newConv
                    | Result.Error err ->
                        if not tryAgain then newConv
                        else
                            $"""
It seems the answer was not correct because: {err}
Can you try again answering?
{msg.Content}
"""
                            |> Message.user
                            |> loop false newConv


        let (>>?) conversation msg  =
            let msg = msg |> Message.user
            loop true conversation msg


        let (>>!) conversation msg =
            loop true conversation msg