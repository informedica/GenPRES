

#r "nuget: Newtonsoft.Json"


/// Utility methods to use ollama
/// https://github.com/ollama/ollama/blob/main/docs/api.md#generate-a-chat-completion
module Ollama =

    open System
    open System.Net.Http
    open System.Text
    open Newtonsoft.Json


    type Configuration() =
        member val num_keep: Nullable<int> = Nullable() with get, set
        member val seed: Nullable<int> = Nullable() with get, set
        member val num_predict: Nullable<int> = Nullable() with get, set
        member val top_k: Nullable<int> = Nullable() with get, set
        member val top_p: Nullable<float> = Nullable() with get, set
        member val tfs_z: Nullable<float> = Nullable() with get, set
        member val typical_p: Nullable<float> = Nullable() with get, set
        member val repeat_last_n: Nullable<int> = Nullable() with get, set
        member val temperature: Nullable<float> = Nullable() with get, set
        member val repeat_penalty: Nullable<float> = Nullable() with get, set
        member val presence_penalty: Nullable<float> = Nullable() with get, set
        member val frequency_penalty: Nullable<float> = Nullable() with get, set
        member val mirostat: Nullable<int> = Nullable() with get, set
        member val mirostat_tau: Nullable<float> = Nullable() with get, set
        member val mirostat_eta: Nullable<float> = Nullable() with get, set
        member val penalize_newline: Nullable<bool> = Nullable() with get, set
        member val stop: string[] = [||] with get, set
        member val numa: Nullable<bool> = Nullable() with get, set
        member val num_ctx: Nullable<int> = Nullable() with get, set
        member val num_batch: Nullable<int> = Nullable() with get, set
        member val num_gqa: Nullable<int> = Nullable() with get, set
        member val num_gpu: Nullable<int> = Nullable() with get, set
        member val main_gpu: Nullable<int> = Nullable() with get, set
        member val low_vram: Nullable<bool> = Nullable() with get, set
        member val f16_kv: Nullable<bool> = Nullable() with get, set
        member val vocab_only: Nullable<bool> = Nullable() with get, set
        member val use_mmap: Nullable<bool> = Nullable() with get, set
        member val use_mlock: Nullable<bool> = Nullable() with get, set
        member val rope_frequency_base: Nullable<float> = Nullable() with get, set
        member val rope_frequency_scale: Nullable<float> = Nullable() with get, set
        member val num_thread: Nullable<int> = Nullable() with get, set


    let options =
        let opts = Configuration()
        opts.seed <- 101
        opts.temperature <- 0.
        opts.repeat_last_n <- 64
        opts.num_ctx <- 2048
        opts.mirostat <- 0

        opts


    module Roles =

        let user = "user"
        let system = "system"
        let assistent = "assistent"


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


    type Response =
        | Success of ModelResponse
        | Error of string
    and ModelResponse = {
        model: string
        created_at: string
        response: string
        message: Message
        ``done``: bool
        context: int list
        total_duration: int64
        load_duration: int64
        prompt_eval_duration: int64
        eval_count: int
        eval_duration: int64
    }


    type ModelDetails = {
        format: string
        family: string
        families : string []
        parameter_size: string
        quantization_level: string
    }

    type Model = {
        name: string
        modified_at: string
        size: int64
        digest: string
        details: ModelDetails
    }

    type Models = {
        models: Model list
    }


    type Embedding = {
        embedding : float []
    }


    type Details = {
        format: string
        family: string
        families: string list
        parameter_size: string
        quantization_level: string
    }

    type ModelConfig = {
        modelfile: string
        parameters: string
        template: string
        details: Details
    }

    module EndPoints =

        [<Literal>]
        let generate = "http://localhost:11434/api/generate"

        [<Literal>]
        let pull = "http://localhost:11434/api/pull"

        [<Literal>]
        let chat = "http://localhost:11434/api/chat"

        [<Literal>]
        let tags = "http://localhost:11434/api/tags"

        [<Literal>]
        let embeddings = "http://localhost:11434/api/embeddings"

        [<Literal>]
        let show = "http://localhost:11434/api/show"



    // Create an HTTP client
    let client = new HttpClient()

    let pullModel name =

        let pars =
            {|
                name = name
            |}
            |> JsonConvert.SerializeObject

        let content = new StringContent(pars, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(EndPoints.pull, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return responseBody
        }


    let generate model prompt =

        let pars =
            {|
                model = model
                prompt = prompt
                options = options
                    (*
                    {|
                        seed = 101
                        temperature = 0.
                    |}
                    *)
                stream = false
            |}
            |> JsonConvert.SerializeObject

        let content = new StringContent(pars, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(EndPoints.generate, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelResponse =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<ModelResponse>
                    |> Success
                with
                | e -> e.ToString() |> Error
            return modelResponse
        }


    let chat model messages (message : Message) =
        let map msg =
            {|
                role = msg.Role
                content = msg.Content
            |}

        let messages =
            {|
                model = model
                messages =
                    [ message |> map ]
                    |> List.append (messages |> List.map map)
                options = options
                stream = false
            |}
            |> JsonConvert.SerializeObject

        let content = new StringContent(messages, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(EndPoints.chat, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelResponse =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<ModelResponse>
                    |> Success
                with
                | e ->
                    e.ToString() |> Error

            return modelResponse
        }


    let listModels () =

        // Asynchronous API call
        async {
            let! response = client.GetAsync(EndPoints.tags) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let models =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<Models>
                with
                | e -> e.ToString() |> failwith

            return models
        }
        |> Async.RunSynchronously


    let showModel model =
        let prompt =
            {|
                name = model
            |}
            |> JsonConvert.SerializeObject

        let content = new StringContent(prompt, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(EndPoints.show, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelConfig =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<ModelConfig>
                with
                | e -> e.ToString() |> failwith
            return modelConfig
        }


    let embeddings model prompt =
        let prompt =
            {|
                model = model
                prompt = prompt
            |}
            |> JsonConvert.SerializeObject

        let content = new StringContent(prompt, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(EndPoints.embeddings, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let models =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<Embedding>
                with
                | e -> e.ToString() |> failwith

            return models
        }



    let run (model : string) messages message =
        message
        |> chat model messages
        |> Async.RunSynchronously
        |> function
            | Success response ->
                [message; response.message]
                |> List.append messages
            | Error s ->
                printfn $"oops: {s}"
                messages


    module Models =

        let llama2 = "llama2"

        let ``llama2:13b-chat`` = "llama2:13b-chat"

        let gemma = "gemma"

        let ``gemma:7b-instruct`` = "gemma:7b-instruct"

        let mistral = "mistral"

        let ``mistral:7b-instruct`` =  "mistral:7b-instruct"

        let ``openchat:7b`` = "openchat:7b"



    let runLlama2  = run Models.llama2


    let runLlama2_13b_chat  = run Models.``llama2:13b-chat``


    let runGemma  = run Models.gemma


    let runGemma_7b_instruct = run Models.``gemma:7b-instruct``


    let runMistral  = run Models.mistral


    let runMistral_7b_instruct  = run Models.``mistral:7b-instruct``


    module Operators =

        let init model msg =
            printfn $"""Starting conversation with {model}

Options:
{options |> JsonConvert.SerializeObject}
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


        let (>>?) (conversation : Conversation) msg  =

            let rec loop tryAgain conversation msg =
                let msg = msg |> Message.user

                msg
                |> run conversation.Model (conversation.Messages |> List.map _.Question)
                |> fun msgs ->
                        let answer = msgs |> List.last

                        match answer.Content |> msg.Validator with
                        | Ok _ ->
                            { conversation with
                                Messages =
                                    [{
                                        Question = msg
                                        Answer = answer
                                    }]
                                    |> List.append conversation.Messages
                            }
                        | Result.Error err ->
                            if not tryAgain then
                                { conversation with
                                    Messages =
                                        [{
                                            Question = msg
                                            Answer = answer
                                        }]
                                        |> List.append conversation.Messages
                                }

                            else
                                $"""
It seems the answer was not correct because: {err}
Can you try again answering?
{msg.Content}
"""
                                |> loop false conversation

            loop true conversation msg


open Ollama.Operators


Ollama.options.temperature <- 0.5
Ollama.options.seed <- 101
Ollama.options.penalize_newline <- false
Ollama.options.top_k <- 50
Ollama.options.top_p <- 0.5


"""
You are an empathic medical professional and translate medical topics to parents
that have a child admitted to a pediatric critical care unit.
"""
|> init Ollama.Models.``openchat:7b``
>>? """
Explain to the parents that there child as to be put on a ventilator and has to
be intubated.
"""
//>>? "translate the previous message to Dutch"
|> Ollama.Conversation.print

