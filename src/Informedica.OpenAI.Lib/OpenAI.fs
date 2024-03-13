namespace Informedica.OpenAI.Lib


module OpenAI =


    type ResponseFormat = {
        ``type``: string
        schema: obj option // As an example, using obj to represent arbitrary JSON schema
    }


    module Chat =


        type ChatInput = {
            model: string
            messages: Message list
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
            response_format: ResponseFormat option
            stream: bool
            context_length_exceeded_behavior: string
            user: string
        }

        // Function to create a ModelInput with default values
        let defaultChatInput model msg msgs : ChatInput = {
            model = model
            messages = [ msg ] |> List.append msgs
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
            response_format = Some { ``type`` = "text"; schema = None }
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
            response_format = { ``type`` = "text"; schema = None }
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


    let completion endPoint apiKey input =
        input
        |> Utils.post<Completions.CompletionResponse> endPoint apiKey


    let chat endPoint apiKey (input : Chat.ChatInput) =
        input
        |> Newtonsoft.Json.JsonConvert.SerializeObject
        |> Utils.post<Chat.ChatResponse> endPoint apiKey
