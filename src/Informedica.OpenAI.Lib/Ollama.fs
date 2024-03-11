namespace Informedica.OpenAI.Lib


/// Utility methods to use ollama
/// https://github.com/ollama/ollama/blob/main/docs/api.md#generate-a-chat-completion
module Ollama =

    open System
    open System.Net.Http
    open System.Text
    open NJsonSchema
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


    type Tool =
        {
            ``type`` : string
            ``function`` : Function
        }
    and Function = {
        name : string
        description : string
        parameters : Parameters
    }
    and Parameters  = {
        ``type`` : string
        properties : obj
        required : string list
    }


    module Tool =


        let create name descr req props =
            {
                ``type`` = "function"
                ``function`` = {
                    name = name
                    description = descr
                    parameters = {
                        ``type`` = "object"
                        properties = props
                        required = req
                    }
                }
            }


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


    type Response =
        | Success of ModelResponse
        | Error of string
    and ModelResponse = {
        error: string
        model: string
        created_at: string
        response: string
        message: {| role : string; content : string |}
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

        [<Literal>]
        let openAI = "http://localhost:11434/v1/chat/completions"



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
                    let resp =
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>

                    match resp.error with
                    | s when s |> String.IsNullOrEmpty ->
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>
                        |> Success
                    | s ->
                        s |> Error
                with
                | e ->
                    e.ToString() |> Error

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
                    let resp =
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>

                    match resp.error with
                    | s when s |> String.IsNullOrEmpty ->
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>
                        |> Success
                    | s ->
                        s |> Error
                with
                | e ->
                    e.ToString() |> Error

            return modelResponse
        }


    let json<'ReturnType> model messages (message : Message) =
        let map msg =
            {|
                role = msg.Role
                content = msg.Content
            |}

        let schema = JsonSchema.FromType<'ReturnType>()
        let content =
            {|
                model = model
                format = "json"
                response_format = {|
                    ``type`` = "json_object"
                    schema = schema
                |}
                messages =
                    [ message |> map ]
                    |> List.append (messages |> List.map map)
                options = options
                stream = false
            |}
            |> JsonConvert.SerializeObject

        let content = new StringContent(content, Encoding.UTF8, "application/json")
        // Asynchronous API call
        async {
            let! response = client.PostAsync(EndPoints.chat, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            printfn $"responseBody: {responseBody}"

            let modelResponse =
                try
                    let resp =
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>

                    match resp.error with
                    | s when s |> String.IsNullOrEmpty ->
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>
                        |> Success
                    | s ->
                        s |> Error
                with
                | e ->
                    e.ToString() |> Error

            return modelResponse
        }



    let extract tools model messages (message : Message) =
        let map msg =
            {|
                role = msg.Role
                content = msg.Content
            |}

        let messages =
            {|
                tools = tools
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
                    let resp =
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>

                    match resp.error with
                    | s when s |> String.IsNullOrEmpty ->
                        responseBody
                        |> JsonConvert.DeserializeObject<ModelResponse>
                        |> Success
                    | s ->
                        s |> Error
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

                [ message; Message.okMessage response.message.role response.message.content ]
                |> List.append messages
            | Error s ->
                printfn $"oops: {s}"
                messages


    module Models =

        let llama2 = "llama2"

        let medllama2 = "medllama2"

        let ``llama2:13b-chat`` = "llama2:13b-chat"

        let gemma = "gemma"

        let ``gemma:7b-instruct`` = "gemma:7b-instruct"

        let mistral = "mistral"

        let ``mistral:7b-instruct`` =  "mistral:7b-instruct"

        let ``openchat:7b`` = "openchat:7b"

        let meditron = "meditron"

        let ``joefamous/firefunction-v1:q3_k`` = "joefamous/firefunction-v1:q3_k"



    let runLlama2  = run Models.llama2


    let runLlama2_13b_chat  = run Models.``llama2:13b-chat``


    let runGemma  = run Models.gemma


    let runGemma_7b_instruct = run Models.``gemma:7b-instruct``


    let runMistral  = run Models.mistral


    let runMistral_7b_instruct  = run Models.``mistral:7b-instruct``


    module Operators =

        open Newtonsoft.Json


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