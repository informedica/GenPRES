namespace Informedica.OpenAI.Lib

open Informedica.OpenAI.Lib.OpenAI.Chat


module Fireworks =


    open System
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


    let chatJson (input : Chat.ChatInput) =
        // doesn't work well
        //let schema = JsonSchema.FromType<'Schema>().ToJson()

        { input with
            response_format =
                {
                    ``type`` = "json_object"
                    schema = null // this is not well supported
                                  // put the schema in the prompt
                }
        }
        |> JsonConvert.SerializeObject
//        |> String.replace "\"[schema]\"" schema
        |> Utils.post<Chat.ChatResponse>
            EndPoints.chat
            apiKey


    let validate<'ReturnType> (validator : string -> Result<string, string>) (input : Chat.ChatInput) =
        let original = input.messages |> List.last

        async {
            let rec validateLoop reTry (input: Chat.ChatInput) =
                async {
                    let! resp = chatJson (input : Chat.ChatInput)
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


    let validate2<'ReturnType> (validator : string -> Result<string, string>) (input : Chat.ChatInput) =
        let original = input.messages |> List.last

        async {
            let rec validateLoop reTry (input: Chat.ChatInput) =
                async {
                    let! resp = chatJson (input : Chat.ChatInput)
                    match resp with
                    | Ok result ->
                        let answer =
                            result.Response.choices
                            |> List.last
                        let input =
                            { input with
                                messages =
                                    [{|
                                       role = answer.message.role
                                       content = answer.message.content
                                    |}]
                                    |> List.append input.messages
                            }

                        match answer.message.content |> validator with
                        | Ok _ ->
                            let validationResult =
                                answer.message.content
                                |> JsonConvert.DeserializeObject<'ReturnType>
                            return Ok (validationResult, input)
                        | Error err ->
                            if not reTry then
                                return Error (err, input)
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
                            return Error (err, input)
                        else
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


    module WithState =

        open FSharpPlus
        open FSharpPlus.Data


        let inline extract model zero (msg : Message) =
            monad {
                // get the current input
                let! (input : Chat.ChatInput) = State.get
                // get the structured extraction along with
                // the updated input
                let input, res =
                    { input with
                        model = model
                        messages =
                            [{|
                                role = msg.Role
                                content = msg.Content
                            |}]
                            |> List.append input.messages
                    }
                    |> validate2
                        msg.Validator
                    |> Async.RunSynchronously
                    |> function
                        | Ok (result, input) -> input, result
                        | Error (_, input)   -> input, zero
                // refresh the state with the updated list of messages
                do! State.put input
                // return the structured extraction
                return res
            }


    module Extract =

        let doseUnits model text =
            let getJson =
                fun model zero (msg : Message) (input : Chat.ChatInput) ->
                    { input with
                        model = model
                        messages =
                            [{|
                                role = msg.Role
                                content = msg.Content
                            |}]
                            |> List.append input.messages
                    }
                    |> validate2
                        msg.Validator
                    |> Async.RunSynchronously
                    |> function
                        | Ok (result, input) -> input, result
                        | Error (_, input)   -> input, zero

            Extraction.createDoseUnits
                getJson
                getJson
                getJson
                model text
