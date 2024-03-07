

#r "nuget: Newtonsoft.Json"


module Texts =

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



    let systemDoseIndicationExpert = """
You are an expert on medication prescribing, preparation and administration.
You have to answer questions about texts that describing a drug dose.
You are asked to extract structured information from that text.

Are the indications for a specific drug. An indication for the drug is the reason to
prescribe the drug with a dose advice.

You answer all questions with ONLY the shortest possible answer to the question.
    """


    let systemDosePatientExpert = """
You are an expert on medication prescribing, preparation and administration.
You have to answer questions about texts that describing a drug dose.
You are asked to extract structured information from that text.

The information you are looking for pertains to the patient category the dose text applies to:
- the gender of the patient
- the minimum age of the patient
- the maximum age of the patient
- the minimum weight of the patient
- the maximum weight of the patient
- the minimum body surface area of the patient
- the maximum body surface area of the patient
- the minimum gestational age of the patient
- the maximum gestational age of the patient
- the minimum post menstrual age of the patient
- the maximum post menstrual age of the patient

If the required answer is not available you return an empty string

You answer all questions with ONLY the shortest possible answer to the question.
    """


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
or is an IU/IE (international unit of measurement).

You answer all questions with ONLY the shortest possible answer to the question.
    """

    let doseRuleStructure = """
    {
      "doseRule": {
        "generic": "acetaminophen/ibuprofen", // "The generic name in all small caps, for multiple substances drugs concatenated with '/'"
        "shape": "tablet", // "The shape name of the generic drug from the Z-index"
        "brand": "BrandX", // "The specific Brand a dose rule applies to"
        "GPKs": ["GPK123", "GPK456"], // "Specific medication identifiers (Generic Product Codes)"
        "route": "oral", // "The route name using the mapping in the Routes sheet"
        "indication": "pain relief", // "The indication of the dose rule from the Kinderformularium or Farmacotherapeutisch Kompas"
        "dep": "pediatrics", // "The department the dose rule applies to"
        "gender": "any", // "The gender the dose rule applies to"
        "ageRange": {
          "minAge": "0 days", // "The minimum age in days the dose rule applies to"
          "maxAge": "6570 days" // "The maximum age in days the dose rule applies to"
        },
        "weightRange": {
          "minWeight": "1000 gram", // "The minimum weight in grams the dose rule applies to"
          "maxWeight": "50000 gram" // "The maximum weight in grams the dose rule applies to"
        },
        "BSARange": {
          "minBSA": "0.5 mˆ2", // "The minimum BSA in m2 the dose rule applies to"
          "maxBSA": "1.5 mˆ2" // "The maximum BSA in m2 the dose rule applies to"
        },
        "gestationalAgeRange": {
          "minGestAge": "0 days", // "The minimum gestational age in days the dose rule applies to"
          "maxGestAge": "280 days" // "The maximum gestational age in days the dose rule applies to"
        },
        "postmenstrualAgeRange": {
          "minPMAge": "0 days", // "The minimum postmenstrual age in days the dose rule applies to"
          "maxPMAge": "294 days" // "The maximum postmenstrual age in days the dose rule applies to"
        },
        "doseType": "continuous", // "The dose type ('once', 'onceTimed', 'discontinuous', 'timed', 'continuous')"
        "doseText": "Continuous intravenous infusion", // "Description of the DoseType"
        "substance": "acetaminophen", // "The substance used, part of the generic name"
        "timeRange": {
          "minTime": 30, // "The minimum time for infusion of a dose"
          "maxTime": 120, // "The maximum time for infusion of a dose"
          "timeUnit": "minutes" // "The time unit to measure the infusion"
        },
        "intervalRange": {
          "minInt": 4, // "The minimum interval between two doses"
          "maxInt": 8, // "The maximum interval between two doses"
          "intUnit": "hours" // "The interval unit"
        },
        "durationRange": {
          "minDur": 1, // "The minimum duration of the dose rule"
          "maxDur": 7, // "The maximum duration of the dose rule"
          "durUnit": "days" // "The duration time unit"
        },
        "quantityRange": {
          "minQty": "10 mg", // "The minimum dose quantity per dose"
          "maxQty": "15 mg", // "The maximum dose quantity per dose"
          "normQtyAdj": "10 mg/kg", // "The normal adjusted quantity per dose"
          "minQtyAdj": "9 mg/kg", // "The minimum adjusted quantity per dose"
          "maxQtyAdj": "11 mg/kg", // "The maximum adjusted quantity per dose"
          "doseUnit": "mg", // "The dose unit of the substance"
          "adjustUnit": "kg", // "The unit to adjust the dose with"
        },
        "frequencyRange": {
          "minPerTime": "40 mg/day", // "The minimum dose quantity per freq time unit"
          "maxPerTime": "60 mg/day", // "The maximum dose quantity per freq time unit"
          "normPerTimeAdj": "40/kg/day", // "The normal adjusted dose quantity per frequency unit"
          "minPerTimeAdj": "36/kg/day", // "The minimum adjusted dose quantity per freq time unit"
          "maxPerTimeAdj": "44/kg/day" // "The maximum dose adjusted quantity per freq time unit"
          "doseUnit": "mg", // "The dose unit of the substance"
          "adjustUnit": "kg", // "The unit to adjust the dose with"
          "freqs": "4;6;8", // "The possible frequencies, i.e. doses per freq unit, number list separated with ';'"
          "freqUnit": "day", // "The time unit of the frequencies"
        },
        "rateRange": {
          "minRate": "2 mg/hour", // "The minimum dose rate"
          "maxRate": "4 mg/hour", // "The maximum dose rate"
          "minRateAdj": "2 mg/kg/hour", // "The minimum adjusted dose rate"
          "maxRateAdj": "4 mg/kg/hour" // "The maximum adjusted dose rate"
          "adjustUnit": "kg", // "The unit to adjust the dose with"
          "rateUnit": "hour", // "The time unit for a dose rate"
        }
      }
    }
    """


    let doseQuantity = """
    {
      "doseQuantity": {
        "timeRange": {
          "minTime": "", // "A Quantity. The minimum time for infusion of a dose"
          "maxTime": "", // "A Quantity. The maximum time for infusion of a dose"
          "timeUnit": "" // "A Unit. The time unit to measure the infusion"
        },
        "intervalRange": {
          "minInt": "", // "A Quantity. The minimum interval between two doses"
          "maxInt": "", // "A Quantity. The maximum interval between two doses"
          "intUnit": "" // "A Unit. The interval unit"
        },
        "durationRange": {
          "minDur": "", // "A Quantity. The minimum duration of the dose rule"
          "maxDur": "", // "A Quantity. The maximum duration of the dose rule"
          "durUnit": "" // "A Unit. The duration time unit"
        },
        "quantityRange": {
          "minQty": "", // "A Quantity. The minimum dose quantity per dose"
          "maxQty": "", // "A Quantity. The maximum dose quantity per dose"
          "normQtyAdj": "", // "A Quantity. The normal adjusted quantity per dose"
          "minQtyAdj": "", // "A Quantity. The minimum adjusted quantity per dose"
          "maxQtyAdj": "", // "A Quantity. The maximum adjusted quantity per dose"
          "doseUnit": "", // "A Unit. The dose unit of the substance"
          "adjustUnit": "" // "A Unit. The unit to adjust the dose with"
        },
        "frequencyRange": {
          "minPerTime": "", // "A Quantity. The minimum dose quantity per freq time unit"
          "maxPerTime": "", // "A Quantity. The maximum dose quantity per freq time unit"
          "normPerTimeAdj": "", // "A Quantity. The normal adjusted dose quantity per frequency unit"
          "minPerTimeAdj": "", // "A Quantity. The minimum adjusted dose quantity per freq time unit"
          "maxPerTimeAdj": "", // "A Quantity. The maximum dose adjusted quantity per freq time unit"
          "doseUnit": "", // "A Unit. The dose unit of the substance"
          "adjustUnit": "", // "A Unit. The unit to adjust the dose with"
          "freqs": "", // "A list of numbers. The possible frequencies, i.e. doses per freq unit, number list separated with ';'"
          "freqUnit": "" // "A Unit. The time unit of the frequencies"
        },
        "rateRange": {
          "minRate": "", // "A Quantity. The minimum dose rate"
          "maxRate": "", // "A Quantity. The maximum dose rate"
          "minRateAdj": "", // "A Quantity. The minimum adjusted dose rate"
          "maxRateAdj": "", // "A Quantity. The maximum adjusted dose rate"
          "doseUnit": "", // "A Unit. The dose unit of the substance"
          "adjustUnit": "", // "A Unit. The unit to adjust the dose with"
          "rateUnit": "" // "A Unit. The time unit for a dose rate"
        }
      }
    }
    """


    let context = $"""
    A dose quantity can be characterized by the following structure:

    {doseQuantity}

    Each field is either
    - a quantity with a number and a unit, example: 40 mg/day
    - or a single unit: example: day
    - or a list of numbers: example: 1;2;3
    """

    let freqRange = """
    "frequencyRange": {
      "minPerTime": ,
      "maxPerTime": ,
      "normPerTimeAdj": ,
      "minPerTimeAdj": ,
      "maxPerTimeAdj": ,
      "doseUnit": ,
      "adjustUnit": ,
      "freqs": ,
      "freqUnit":
    }
    """


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


        let (>>?) (conversation : Conversation) msg  =

            let rec loop tryAgain conversation msg =
                let msg = msg |> Message.user

                msg
                |> run conversation.Model (conversation.Messages |> List.map (_.Question))
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


let extractDoseQuantities model text =
        Texts.systemDoseQuantityExpert
        |> init model
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
Summarize the previous answers as:

- Substance: ?
- SubstanceUnit: ?
- AdjustUnit: ?
- TimeUnit: ?
- MaximumDosePerTime: ?
- AdjustedDosePerTime: ?
- Frequency: ?

"""


let testModel model =

    printfn $"\n\n# Running: {model}\n\n"
    for text in Texts.testTexts do
        extractDoseQuantities model text
        |> Ollama.Conversation.print


let testAll () =

    [
        Ollama.Models.gemma
        Ollama.Models.``gemma:7b-instruct``
        Ollama.Models.llama2
        Ollama.Models.``llama2:13b-chat``
        Ollama.Models.mistral
        Ollama.Models.``mistral:7b-instruct``
    ]
    |> List.iter testModel



Ollama.options.penalize_newline <- true
Ollama.options.top_k <- 10
Ollama.options.top_p <- 0.95


Ollama.Models.``openchat:7b``
|> testModel


module BNFC =

    let paracetamolPO =
        [
            """
paracetamol
Neonate 28 weeks to 32 weeks corrected gestational age
20 mg/ kg for 1 dose, then 10–15 mg/kg every 8–12 hours as required, maximum daily dose to be given in divided doses; maximum 30 mg/kg per day.
"""
            """
Child 1–2 months
30–60 mg every 8 hours as required, maximum daily dose to be given in divided doses; maximum 60 mg/kg per day.
"""
            """
paracetamol
Child 3–5 months
60 mg every 4–6 hours; maximum 4 doses per day.
"""
            """
paracetamol
Child 6–23 months
120 mg every 4–6 hours; maximum 4 doses per day.
"""
        ]



"You are a helpful assistant"
|> init Ollama.Models.``openchat:7b``
>>? "Why is the sky blue?"
|> Ollama.Conversation.print


Ollama.options.temperature <- 0
Ollama.options.seed <- 101
Ollama.options.penalize_newline <- true
Ollama.options.top_k <- 10
Ollama.options.top_p <- 0.95


BNFC.paracetamolPO[0]
|> extractDoseQuantities Ollama.Models.``openchat:7b``
|> Ollama.Conversation.print


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
>>? "translate the previous message to Dutch"
|> Ollama.Conversation.print


"""
Je bent een empathische zorgverlener die ouders uitleg moet geven over hun kind
dat op de kinder IC ligt.

Je geeft alle uitleg en antwoorden in het Nederlands.
"""
|> init Ollama.Models.``openchat:7b``
>>? """
Leg aan ouders uit dat hun kind aan de beademing moet worden gelegd en daarvoor
geintubeerd moet worden.
"""
|> Ollama.Conversation.print
