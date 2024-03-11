namespace Informedica.OpenAI.Lib


module OpenAI =


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

    type ChatCompletion = {
        id: string
        object: string
        created: int64
        model: string
        system_fingerprint: string
        choices: Choice list
        usage: Usage
    }


