namespace Informedica.OpenAI.Lib


module Fireworks =


    open System
    open System.Net
    open NJsonSchema
    open Newtonsoft.Json
    open Informedica.Utils.Lib.BCL


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


    module EndPoints =


        [<Literal>]
        let chat = "https://api.fireworks.ai/inference/v1/chat/completions"

        [<Literal>]
        let models = "https://api.fireworks.ai/inference/v1/models"

        [<Literal>]
        let completions = "https://api.fireworks.ai/inference/v1/completions"





    type ResponseFormat = {
        ``type``: string
        schema: string
    }


    module Chat =

        type ChatInput = {
            model: string
            messages: {| role : string; content : string |} list
            tools: obj list
            max_tokens: int
            prompt_truncate_len: int
            temperature: float
            top_p: float
            top_k: int
            frequency_penalty: float
            presence_penalty: float
            n: int
            stop: string list
            response_format: ResponseFormat
            stream: bool
            context_length_exceeded_behavior: string
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
                tools = []
                max_tokens = 200
                prompt_truncate_len = 1500
                temperature = 0.
                top_p = 1.0
                top_k = 50
                frequency_penalty = 0.0
                presence_penalty = 0.0
                n =1
                stop = []
                response_format = { ``type`` = "text"; schema = null }
                stream = false
                context_length_exceeded_behavior = "truncate"
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
            response_format = { ``type`` = "text"; schema = null }
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
        let var = Environment.GetEnvironmentVariable("FIREWORKS_API_KEY")
        if var |> String.IsNullOrEmpty then None
        else var |> Some


    let list () =
        Utils.post<Models>
            EndPoints.models
            apiKey
            ""
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
        let schema = JsonSchema.FromType<'Schema>().ToJson()

        { input with
            response_format =
                {
                    ``type`` = "json_object"
                    schema = "[schema]"
                }
        }
        |> JsonConvert.SerializeObject
        |> String.replace "\"[schema]\"" schema
        |> Utils.post<Chat.ChatResponse>
            EndPoints.chat
            apiKey



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

            msg
            |> run model []
            |> fun msgs ->
                    printfn $"Got an answer"

                    {

                        Model = model
                        Messages =
                        [{
                            Question = msg
                            Answer = msgs |> List.last
                        }]
                    }


        let rec private loop tryAgain conversation msg =
            msg
            |> run conversation.Model (conversation.Messages |> List.map (_.Question))
            |> fun msgs ->
                    let answer = msgs |> List.last

                    let newConv =
                        { conversation with
                            Messages =
                                [{
                                    Question = msg
                                    Answer = answer
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


        let (>>!) conversation msg = loop true conversation msg