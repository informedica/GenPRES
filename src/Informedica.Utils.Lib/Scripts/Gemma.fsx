

#r "nuget: Newtonsoft.Json"


module Texts =


    let systemDoseQuantityExpert = """
You are an expert on medication prescribing, preparation and administration.
You have to answer questions about texts that describing a drug dose.
You are asked to extract structured information from that text.

The information will one of the following:
- a quantity with a number and a unit, example: 40 mg/day
- or a single unit, example: day
- or a list of numbers, example: 1;2;3

An adjust unit (AdjustUnit) can only be 'kg' body weight or 'mˆ2' body surface area.
A substance unit is a unit that belongs to either a mass unit group, molar unit group
or is an international unit of measurement.

You answer all questions with ONLY the shortest possible answer to the question.
    """


    let testTexts = [
        """
alprazolam
6 jaar tot 18 jaar Startdosering: 0,125 mg/dag, éénmalig. Onderhoudsdosering: Op geleide van klinisch beeld verhogen met stappen van 0,125-0,25 mg/dosis tot max 0,05 mg/kg/dag in 3 doses. Max: 3 mg/dag. Advies inname/toediening: De dagdosis indien mogelijk verdelen over 3 doses.Bij plotselinge extreme slapeloosheid: alleen voor de nacht innemen; dosering op geleide van effect ophogen tot max 0,05 mg/kg, maar niet hoger dan 3 mg/dag.De effectiviteit bij de behandeling van acute angst is discutabel.
"""
        """
acetylsalicylzuur
1 maand tot 18 jaar Startdosering:Acetylsalicylzuur: 30 - 50 mg/kg/dag in 3 - 4 doses. Max: 3.000 mg/dag.
"""
        """
paracetamol
Oraal: Bij milde tot matige pijn en/of koorts: volgens het Kinderformularium van het NKFK bij een leeftijd van 1 maand–18 jaar: 10–15 mg/kg lichaamsgewicht per keer, zo nodig 4×/dag, max. 60 mg/kg/dag en max. 4 g/dag.
"""
        """
amitriptyline
6 jaar tot 18 jaar Startdosering: voor de nacht: 10 mg/dag in 1 dosisOnderhoudsdosering: langzaam ophogen met 10 mg/dag per 4-6 weken naar 10 - 30 mg/dag in 1 dosis. Max: 30 mg/dag. Behandeling met amitriptyline mag niet plotseling worden gestaakt vanwege het optreden van ontwenningsverschijnselen; de dosering moet geleidelijk worden verminderd.Uit de studie van Powers (2017) blijkt dat de werkzaamheid van amitriptyline bij migraine profylaxe niet effectiever is t.o.v. placebo. Desondanks menen experts dat in individuele gevallen behandeling met amitriptyline overwogen kan worden.
"""
    ]




/// Utility methods to use ollama
/// https://github.com/ollama/ollama/blob/main/docs/api.md#generate-a-chat-completion
module Ollama =

    open System
    open System.Net.Http
    open System.Text
    open Newtonsoft.Json


    module Roles =

        let user = "user"
        let system = "system"
        let assistent = "assistent"


    type Message =
        {
            role : string
            content : string
        }


    module Message =

        let create role content =
            {
                role = role
                content = content
            }

        let user = create Roles.user

        let system = create Roles.system



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
                options =
                    {|
                        seed = 101
                        temperature = 0.
                    |}
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

        let messages =
            {|
                model = model
                messages =
                    [ message ]
                    |> List.append messages
                options =
                    {|
                        seed = 101
                        temperature = 0.
                    |}
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



    let run llm messages message =
        message
        |> chat llm messages
        |> Async.RunSynchronously
        |> function
            | Success response ->
                [message; response.message]
                |> List.append messages
            | Error s ->
                printfn $"oops: {s}"
                messages


    let runGemma_7b_instruct = run "gemma:7b-instruct"



    module Operators =


        let mutable runModel = runGemma_7b_instruct


        let (>>?) msgs msg  =
            printfn $"## ME:\n{msg}"

            msg
            |> Message.user
            |> runModel msgs
            |> fun msgs ->
                    printfn $"## GEMMA:\n{(msgs |> List.last).content}\n"
                    msgs



open Ollama.Operators



for text in Texts.testTexts do

    Texts.systemDoseQuantityExpert
    |> Ollama.Message.system
    |> runModel []
    >>? $"""
The text between the ''' describes dose quantities for a
substance:

'''{text}'''

For which substance?
Give the answer as Substance : ?
"""
    >>? """
What is the unit used for the substance, the substance unit?
Give the answer as SubstanceUnit : ?
"""
    >>? """
What is the unit to adjust the dose for?
Give the answer as AdjustUnit : ?
"""
    >>? """
What is the time unit for the dose frequency?
Give the answer as TimeUnit : ?
"""
    >>? """
What is the maximum dose per time in SubstanceUnit/TimeUnit?
Give the answer as MaximumDosePerTime: ?
"""
    >>? """
What is the dose, adjusted for weight in SubstanceUnit/AdjustUnit/TimeUnit?
Give the answer as AdjustedDosePerTime: ?
"""
    >>? """
What is the number of doses per TimeUnit?
Give the answer as Frequency: ?
"""
    >>? """
Summarize the previous answers:
Substance: ?
SubstanceUnit: ?
AdjustUnit: ?
TimeUnit: ?
MaximumDosePerTime: ?
AdjustedDosePerTime: ?
Frequency: ?
"""
    |> ignore


