// Extract dose rule rows from free text via a local Ollama JSON-mode LLM,
// feed the parsed JSON through `DoseRule.processDoseRuleData`, and print the
// resulting `DoseRule` array with `DoseRule.Print.toMarkdown`.
//
// Pipeline:
//   1. Load the prompt markdown file at
//      `docs/data-extraction/doserule-extraction-prompt.md`.
//   2. Call the LLM via `Extraction.extractDoseRule` — a recursive
//      validation-retry loop (up to 2 attempts) that asks for one
//      `{ "rules": [ ... ] }` JSON object.
//   3. Map the JSON to `Types.DoseRuleData[]` (GenFORM shape).
//   4. Call `DoseRule.processDoseRuleData prods routeMapping`, then
//      `mapToDoseRule` + `addDoseLimits` — the same chain used inside
//      `DoseRule.get`.
//   5. Print via `DoseRule.Print.toMarkdown` or `Printing.printExtracted`.
//
// The JSON ↔ TSV round-trip (`Conversion.toTsv` / `Conversion.fromTsv`)
// lets extracted output be saved as a TSV row matching
// `data/sources/Rules/doserules.tsv` and parsed back through the same
// `parseTsv` used by `Benchmark` for ground-truth scoring.
//
// Requires:
//   - A local Ollama server on `http://localhost:11434` with
//     `qwen3-coder:30b` (default) or another JSON-capable model pulled.
//   - `GENPRES_URL_ID` for product / route-mapping lookup.
//   - The GenFORM.Lib DLL rebuilt and loaded (see CLAUDE.md note on DLL
//     reloads — the FSI MCP server must be restarted after rebuilds).


#I __SOURCE_DIRECTORY__
#load "../../../scripts/load-dependencies.fsx"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenCORE.Lib/bin/Debug/net10.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.NLP.Lib.dll"


open System
open System.IO
open System.Globalization
open MathNet.Numerics
open Newtonsoft.Json
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.OpenAI.Lib
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Utils


// The flat, one-row-per-dose-limit shape this tool extracts to, serialises to
// the canonical Pass-4 TSV, and re-parses for benchmark scoring. This is the
// pre-v2 GenFORM `DoseRuleData` layout; GenFORM v2 replaced it with a nested
// record (GenericData / PatientCategoryData / ScheduleData), so we keep the flat
// shape locally — it shadows `Informedica.GenForm.Lib.Types.DoseRuleData` for the
// conversion/TSV utilities below. The build path (buildDoseRules) goes through
// the canonical TSV and the v2 GenFORM parser, so it does not use this type.
type DoseRuleData =
    {
        Source: string
        Indication: string
        Generic: string
        Form: string
        Brand: string
        GPKs: string array
        Route: string
        Department: string
        ScheduleText: string
        Gender: Gender
        MinAge: BigRational option
        MaxAge: BigRational option
        MinWeight: BigRational option
        MaxWeight: BigRational option
        MinBSA: BigRational option
        MaxBSA: BigRational option
        MinGestAge: BigRational option
        MaxGestAge: BigRational option
        MinPMAge: BigRational option
        MaxPMAge: BigRational option
        DoseType: string
        DoseText: string
        Frequencies: BigRational array
        DoseUnit: string
        AdjustUnit: string
        FreqUnit: string
        RateUnit: string
        MinTime: BigRational option
        MaxTime: BigRational option
        TimeUnit: string
        MinInterval: BigRational option
        MaxInterval: BigRational option
        IntervalUnit: string
        MinDur: BigRational option
        MaxDur: BigRational option
        DurUnit: string
        Component: string
        Substance: string
        MinQty: BigRational option
        MaxQty: BigRational option
        MinQtyAdj: BigRational option
        MaxQtyAdj: BigRational option
        MinPerTime: BigRational option
        MaxPerTime: BigRational option
        MinPerTimeAdj: BigRational option
        MaxPerTimeAdj: BigRational option
        MinRate: BigRational option
        MaxRate: BigRational option
        MinRateAdj: BigRational option
        MaxRateAdj: BigRational option
        Products: ProductComponent array
    }


// =======================================================
// Config
// =======================================================

module Config =

    Env.loadDotEnv () |> ignore
    Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

    /// The Google Sheets data URL used by the GenFORM resource provider.
    let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")

    /// Default local Ollama model. qwen3-coder:30b is the best-behaved
    /// model for structured JSON extraction at this corpus. Override per
    /// call via `Pipeline.runWithModelAndPrint`.
    let defaultModel = "qwen3-coder:30b"

    // Bump the Ollama context window so the full extraction prompt
    // (~9 KB) plus the free-text input fit comfortably. Also cap the
    // response size and extend the HttpClient timeout (the default
    // 100 s is too short for a 30B model generating a full JSON block).
    do
        Ollama.options.num_ctx <- Nullable 16384
        Ollama.options.temperature <- Nullable 0.0
        Ollama.options.seed <- Nullable 101
        Ollama.options.repeat_penalty <- Nullable 1.3
        Ollama.options.num_predict <- Nullable 2000

        try
            Informedica.OpenAI.Lib.Utils.client.Timeout <- TimeSpan.FromMinutes 10.0
        with :? InvalidOperationException -> ()

    /// Path to the prompt markdown file.
    let promptPath =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "../../../docs/data-extraction/doserule-extraction-prompt.md"
        )
        |> Path.GetFullPath

    /// Load the prompt from the markdown file.
    let loadPrompt () = File.ReadAllText promptPath

    /// Resource provider used by `buildDoseRules`.
    let provider: Resources.IResourceProvider =
        Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


// =======================================================
// Schema — unified DoseRuleExtracted record + TSV header
// =======================================================

module Schema =

    /// One dose-limit inside a `DoseType`. Identifies a `(component,
    /// substance)` pair and carries all quantitative bounds + their units.
    type DoseLimit =
        {|
            ``component``: string
            substance: string
            doseUnit: string
            adjustUnit: string
            rateUnit: string
            minQty: Nullable<float>
            maxQty: Nullable<float>
            minQtyAdj: Nullable<float>
            maxQtyAdj: Nullable<float>
            minPerTime: Nullable<float>
            maxPerTime: Nullable<float>
            minPerTimeAdj: Nullable<float>
            maxPerTimeAdj: Nullable<float>
            minRate: Nullable<float>
            maxRate: Nullable<float>
            minRateAdj: Nullable<float>
            maxRateAdj: Nullable<float>
        |}


    /// One dose-type inside a `DoseRuleExtracted`. Carries the temporal
    /// profile (frequency, time window, interval, duration) shared by all
    /// `doseLimits` in that phase.
    type DoseType =
        {|
            doseType: string
            doseText: string
            freqs: int[]
            freqUnit: string
            minTime: Nullable<float>
            maxTime: Nullable<float>
            timeUnit: string
            minInt: Nullable<float>
            maxInt: Nullable<float>
            intUnit: string
            minDur: Nullable<float>
            maxDur: Nullable<float>
            durUnit: string
            doseLimits: DoseLimit[]
        |}


    /// Shape of one extracted dose rule, as emitted by the LLM. One record
    /// per `scheduleText`, covering one patient group. Numeric fields use
    /// `Nullable<float>` so the JSON parser tolerates `null`. The nested
    /// `doseTypes` may contain 1..N dose-type phases (e.g. start +
    /// maintenance), each with 1..N `doseLimits` (one per
    /// `(component, substance)` pair).
    type DoseRuleExtracted =
        {|
            sortNo: Nullable<int>
            source: string
            generic: string
            form: string
            brand: string
            gpks: string[]
            route: string
            indication: string
            scheduleText: string
            dep: string
            gender: string
            minAge: Nullable<float>
            maxAge: Nullable<float>
            minWeight: Nullable<float>
            maxWeight: Nullable<float>
            minBSA: Nullable<float>
            maxBSA: Nullable<float>
            minGestAge: Nullable<float>
            maxGestAge: Nullable<float>
            minPMAge: Nullable<float>
            maxPMAge: Nullable<float>
            doseTypes: DoseType[]
        |}


    /// Container the LLM wraps its output in so we always deserialize a single
    /// top-level object (Ollama JSON mode emits one JSON object per response).
    /// For a single-`scheduleText` extraction `rules.Length = 1`; corpus
    /// runs may return many.
    type DoseRuleExtractionResult = {| rules: DoseRuleExtracted[] |}


    /// The canonical 50-column TSV header, tab-delimited — identical to line 1
    /// of `data/sources/Rules/doserules.tsv`. Column order must stay aligned
    /// with `Conversion.toTsvRow`.
    let tsvColumns =
        [
            "SortNo"; "Source"; "Generic"; "Form"; "Brand"; "Route"; "GPKs"
            "Indication"; "ScheduleText"; "Dep"; "Gender"; "MinAge"; "MaxAge"
            "MinWeight"; "MaxWeight"; "MinBSA"; "MaxBSA"; "MinGestAge"
            "MaxGestAge"; "MinPMAge"; "MaxPMAge"; "DoseType"; "DoseText"
            "Component"; "Substance"; "Freqs"; "DoseUnit"; "AdjustUnit"
            "FreqUnit"; "RateUnit"; "MinTime"; "MaxTime"; "TimeUnit"; "MinInt"
            "MaxInt"; "IntUnit"; "MinDur"; "MaxDur"; "DurUnit"; "MinQty"
            "MaxQty"; "MinQtyAdj"; "MaxQtyAdj"; "MinPerTime"; "MaxPerTime"
            "MinPerTimeAdj"; "MaxPerTimeAdj"; "MinRate"; "MaxRate"; "MinRateAdj"
            "MaxRateAdj"
        ]

    let tsvHeader = tsvColumns |> String.concat "\t"

    /// Allowed dose-type enum values.
    let validDoseTypes = [ "once"; "onceTimed"; "discontinuous"; "timed"; "continuous"; "" ]

    /// Allowed adjust-unit values.
    let validAdjustUnits = [ "kg"; "m2"; "" ]


// =======================================================
// Prompt — JSON schema example + strict output rules
// =======================================================

module Prompt =

    /// JSON schema shown to the LLM. Hierarchical: one `rules[i]` per
    /// `scheduleText` (one patient group). Each rule has `doseTypes[]`
    /// (start + maintenance, etc.); each dose type has `doseLimits[]`
    /// (one per `component`/`substance` pair). Every field must be
    /// present; numbers default to `null`, strings to `""`, arrays to
    /// `[]`. Minified to save prompt tokens.
    let jsonSchema =
        """{"rules":[{"sortNo":1,"source":"","generic":"","form":"","brand":"","gpks":[],"route":"","indication":"","scheduleText":"","dep":"","gender":"","minAge":null,"maxAge":null,"minWeight":null,"maxWeight":null,"minBSA":null,"maxBSA":null,"minGestAge":null,"maxGestAge":null,"minPMAge":null,"maxPMAge":null,"doseTypes":[{"doseType":"","doseText":"","freqs":[],"freqUnit":"","minTime":null,"maxTime":null,"timeUnit":"","minInt":null,"maxInt":null,"intUnit":"","minDur":null,"maxDur":null,"durUnit":"","doseLimits":[{"component":"","substance":"","doseUnit":"","adjustUnit":"","rateUnit":"","minQty":null,"maxQty":null,"minQtyAdj":null,"maxQtyAdj":null,"minPerTime":null,"maxPerTime":null,"minPerTimeAdj":null,"maxPerTimeAdj":null,"minRate":null,"maxRate":null,"minRateAdj":null,"maxRateAdj":null}]}]}]}"""


    /// Prompt augmentation for the JSON variant. Directs the LLM to emit a
    /// `{ "rules": [...] }` document following the hierarchical schema.
    let jsonOverride =
        $"""
OUTPUT (overrides §3/§7):
- Emit ONE JSON object: {{"rules":[...]}}. No markdown, no fences, no prose.
- One `rules[i]` per scheduleText / patient group (identity + patient fields at top level).
- `doseTypes[]`: one entry per temporal phase (e.g. start + maintenance). Timing fields (freqs, freqUnit, minTime, maxTime, timeUnit, minInt, maxInt, intUnit, minDur, maxDur, durUnit) live on the dose type.
- `doseLimits[]`: one entry per (component, substance) pair within a dose type.
- Every field present. Missing: null (num) / "" (str) / [] (arr).
- component + substance required on every doseLimit; single-substance → both = generic.
- form: fill only if source states pharmaceutical form, else "".
- sortNo: 1,2,3… in emission order of rules.
- doseType ∈ {{once, onceTimed, discontinuous, timed, continuous}}.
- adjustUnit ∈ {{kg, m2, ""}}.
- Units: age/gestAge/pmAge = days (int); weight = grams (int); BSA = m² (float); decimal = `.`.
Schema:
{jsonSchema}"""


    /// Build the system-role message content for one extraction call.
    /// Includes the source banner, the main extraction prompt (loaded from
    /// markdown), and the strict JSON output rules.
    let systemContent (sourceName: string) =
        let banner =
            if String.IsNullOrWhiteSpace sourceName then ""
            else $"Source name: {sourceName}\n\n"

        banner + Config.loadPrompt () + jsonOverride


// =======================================================
// Validation — strict check of the raw JSON reply
// =======================================================

module Validation =

    /// Strip code fences, leading/trailing whitespace, and any commentary
    /// before the first `{` / after the last `}`.
    let cleanup (s: string) =
        if isNull s then ""
        else
            let trimmed = s.Trim()
            let noFence =
                if trimmed.StartsWith("```") then
                    let afterFirst =
                        match trimmed.IndexOf('\n') with
                        | -1 -> trimmed
                        | i -> trimmed.Substring(i + 1)

                    let endFence = afterFirst.LastIndexOf("```")
                    if endFence >= 0 then afterFirst.Substring(0, endFence) else afterFirst
                else
                    trimmed

            let first = noFence.IndexOf('{')
            let last = noFence.LastIndexOf('}')

            if first >= 0 && last > first then noFence.Substring(first, last - first + 1)
            else noFence.Trim()


    /// Validate that a JSON string can be deserialized into
    /// `DoseRuleExtractionResult` and that the hierarchical tree is
    /// well-formed: every rule has a non-empty `generic` and at least
    /// one `doseTypes` entry; every dose type has a recognised
    /// `doseType` value and at least one `doseLimits` entry; every dose
    /// limit has a recognised `adjustUnit` plus non-empty `component`
    /// and `substance`. Errors are reported with a `rule i / doseType j
    /// / doseLimit k` path. Returns the cleaned JSON on success.
    let validateExtractionJson (s: string) : Result<string, string> =
        if String.isNullOrWhiteSpace s then
            Error "Empty response"
        else
            let cleaned = cleanup s

            try
                let parsed =
                    JsonConvert.DeserializeObject<Schema.DoseRuleExtractionResult>(cleaned)

                if isNull (box parsed) || isNull (box parsed.rules) then
                    Error "JSON missing 'rules' array"
                elif parsed.rules.Length = 0 then
                    Error "JSON 'rules' array is empty"
                else
                    let validDoseTypes =
                        Schema.validDoseTypes
                        |> List.filter ((<>) "")
                        |> String.concat ", "

                    let errors =
                        parsed.rules
                        |> Array.mapi (fun i r ->
                            [
                                let rulePath = $"rule {i + 1}"

                                if String.isNullOrWhiteSpace r.generic then
                                    yield $"{rulePath}: generic is empty"

                                if isNull (box r.doseTypes) || r.doseTypes.Length = 0 then
                                    yield $"{rulePath}: doseTypes is empty"
                                else
                                    for j in 0 .. r.doseTypes.Length - 1 do
                                        let dt = r.doseTypes[j]
                                        let dtPath = $"{rulePath} / doseType {j + 1}"

                                        if Schema.validDoseTypes |> List.contains dt.doseType |> not then
                                            yield $"{dtPath}: doseType '{dt.doseType}' is not valid; must be one of: {validDoseTypes}"

                                        if isNull (box dt.doseLimits) || dt.doseLimits.Length = 0 then
                                            yield $"{dtPath}: doseLimits is empty"
                                        else
                                            for k in 0 .. dt.doseLimits.Length - 1 do
                                                let dl = dt.doseLimits[k]
                                                let dlPath = $"{dtPath} / doseLimit {k + 1}"

                                                if Schema.validAdjustUnits |> List.contains dl.adjustUnit |> not then
                                                    yield $"{dlPath}: adjustUnit '{dl.adjustUnit}' is not valid; must be 'kg', 'm2', or empty"

                                                if String.isNullOrWhiteSpace dl.``component`` then
                                                    yield $"{dlPath}: component is empty"

                                                if String.isNullOrWhiteSpace dl.substance then
                                                    yield $"{dlPath}: substance is empty"
                            ]
                        )
                        |> Array.collect List.toArray

                    if errors.Length = 0 then Ok cleaned
                    else errors |> String.concat "; " |> Error
            with e ->
                Error $"JSON parse error: {e.Message}"


// =======================================================
// Extraction — recursive tryExtract loop (ported from
// DoseRuleExtraction.fsx) over a pluggable sendMessages
// function. An Ollama sender is provided.
// =======================================================

module Extraction =

    /// Abstract LLM sender. Given a model name and the full message
    /// history, returns the raw assistant content as a string.
    type Sender =
        string
            -> Informedica.OpenAI.Lib.Types.Message list
            -> Async<Result<string, string>>


    /// Ollama sender: treats the last message as the trailing user turn
    /// expected by `Ollama.chat` and passes everything else as history.
    let ollamaSender: Sender =
        fun model msgs ->
            async {
                match List.rev msgs with
                | [] -> return Error "no messages"
                | last :: revInit ->
                    let history = List.rev revInit
                    let! resp = Ollama.chat model history last
                    return resp |> Result.map _.Response.message.content
            }


    /// Ask the LLM to correct grammar and punctuation in a free-text
    /// dosing schedule. The reply is the corrected text itself — no
    /// JSON, no retries. Clinical content (numbers, units, frequencies,
    /// abbreviations) must be preserved verbatim.
    let sanitizeText
        (sendMessages: Sender)
        (model: string)
        (text: string)
        : Async<Result<string, string>>
        =
        let mkSystem = Informedica.OpenAI.Lib.Message.system
        let mkUser = Informedica.OpenAI.Lib.Message.user

        let systemPrompt =
            "You correct grammar and punctuation in medical dosing schedule text. "
            + "Preserve all clinical content, numbers, units, frequencies, durations, "
            + "and abbreviations exactly. Do not translate. Do not add, remove, "
            + "reorder, or reinterpret any clinical fact. "
            + "Reply with the corrected text only — no quoting, no commentary, no markdown fences."

        let stripFences (s: string) =
            let t = s.Trim()
            let t =
                if t.StartsWith("```") then
                    match t.IndexOf('\n') with
                    | -1 -> t
                    | i ->
                        let rest = t.Substring(i + 1)
                        let endFence = rest.LastIndexOf("```")
                        if endFence >= 0 then rest.Substring(0, endFence) else rest
                else
                    t

            t.Trim().Trim('`').Trim()

        async {
            if String.isNullOrWhiteSpace text then
                return Ok text
            else
                let msgs = [ mkSystem systemPrompt; mkUser text ]
                let! resp = sendMessages model msgs

                match resp with
                | Error e -> return Error e
                | Ok answer -> return Ok (stripFences answer)
        }


    /// Extract a structured `DoseRuleExtractionResult` from free text using
    /// the specified LLM sender. Retries up to 2 times on validation failure,
    /// feeding the validation error back as a user turn.
    ///
    /// Parameters:
    ///   sendMessages - LLM sender (e.g. `ollamaSender`)
    ///   model        - The model identifier (e.g. "qwen3-coder:30b")
    ///   sourceName   - Source name included in the system banner
    ///   text         - The free-text dosage description
    let extractDoseRule
        (sendMessages: Sender)
        (model: string)
        (sourceName: string)
        (text: string)
        : Async<Result<Schema.DoseRuleExtractionResult, string>>
        =
        let mkSystem = Informedica.OpenAI.Lib.Message.system
        let mkUser = Informedica.OpenAI.Lib.Message.user
        let mkAssistant = Informedica.OpenAI.Lib.Message.assistant

        async {
            let baseMsgs =
                [
                    mkSystem (Prompt.systemContent sourceName)
                    mkUser text
                ]

            let rec tryExtract attempt msgs =
                async {
                    let! resp = sendMessages model msgs

                    match resp with
                    | Error e -> return Error e
                    | Ok answer ->
                        match Validation.validateExtractionJson answer with
                        | Error err when attempt < 2 ->
                            let retryMsgs =
                                msgs
                                @ [
                                    mkAssistant answer
                                    mkUser
                                        $"The previous response had issues: {err}. Please correct the JSON and respond only with valid JSON matching the schema."
                                ]

                            return! tryExtract (attempt + 1) retryMsgs
                        | Error err -> return Error err
                        | Ok validJson ->
                            return
                                validJson
                                |> JsonConvert.DeserializeObject<Schema.DoseRuleExtractionResult>
                                |> Ok
                }

            return! tryExtract 0 baseMsgs
        }


// =======================================================
// Conversion — JSON ↔ DoseRuleData, JSON ↔ TSV
// =======================================================

module Conversion =

    let inline private brFromFloat f =
        Informedica.Utils.Lib.BCL.BigRational.fromFloat f

    let inline private nullableToBr (n: Nullable<float>) =
        if n.HasValue then brFromFloat n.Value else None

    let private brOptToNullable (br: BigRational option) : Nullable<float> =
        match br with
        | Some v -> Nullable(BigRational.toFloat v)
        | None -> Nullable()

    let private orEmpty (s: string) = if isNull s then "" else s


    /// Build one flat `DoseRuleData` record from a rule / dose-type /
    /// dose-limit triple. The three layers own disjoint field sets, so
    /// the mapping is a straightforward field-wise copy.
    let private toDoseRuleDataOne
        (r: Schema.DoseRuleExtracted)
        (dt: Schema.DoseType)
        (dl: Schema.DoseLimit)
        : DoseRuleData
        =
        let freqs =
            if isNull dt.freqs then [||]
            else dt.freqs |> Array.choose (float >> brFromFloat)

        {
            Source = orEmpty r.source
            Indication = orEmpty r.indication
            Generic = orEmpty r.generic
            Form = orEmpty r.form
            Brand = orEmpty r.brand
            GPKs =
                if isNull r.gpks then [||]
                else
                    r.gpks
                    |> Array.map String.trim
                    |> Array.filter String.notEmpty
                    |> Array.distinct
            Route = orEmpty r.route
            Department = orEmpty r.dep
            ScheduleText = orEmpty r.scheduleText
            Gender = orEmpty r.gender |> Gender.fromString
            MinAge = nullableToBr r.minAge
            MaxAge = nullableToBr r.maxAge
            MinWeight = nullableToBr r.minWeight
            MaxWeight = nullableToBr r.maxWeight
            MinBSA = nullableToBr r.minBSA
            MaxBSA = nullableToBr r.maxBSA
            MinGestAge = nullableToBr r.minGestAge
            MaxGestAge = nullableToBr r.maxGestAge
            MinPMAge = nullableToBr r.minPMAge
            MaxPMAge = nullableToBr r.maxPMAge
            DoseType = orEmpty dt.doseType
            DoseText = orEmpty dt.doseText
            Frequencies = freqs
            DoseUnit = orEmpty dl.doseUnit
            AdjustUnit = orEmpty dl.adjustUnit
            FreqUnit = orEmpty dt.freqUnit
            RateUnit = orEmpty dl.rateUnit
            MinTime = nullableToBr dt.minTime
            MaxTime = nullableToBr dt.maxTime
            TimeUnit = orEmpty dt.timeUnit
            MinInterval = nullableToBr dt.minInt
            MaxInterval = nullableToBr dt.maxInt
            IntervalUnit = orEmpty dt.intUnit
            MinDur = nullableToBr dt.minDur
            MaxDur = nullableToBr dt.maxDur
            DurUnit = orEmpty dt.durUnit
            Component = orEmpty dl.``component``
            Substance = orEmpty dl.substance
            MinQty = nullableToBr dl.minQty
            MaxQty = nullableToBr dl.maxQty
            MinQtyAdj = nullableToBr dl.minQtyAdj
            MaxQtyAdj = nullableToBr dl.maxQtyAdj
            MinPerTime = nullableToBr dl.minPerTime
            MaxPerTime = nullableToBr dl.maxPerTime
            MinPerTimeAdj = nullableToBr dl.minPerTimeAdj
            MaxPerTimeAdj = nullableToBr dl.maxPerTimeAdj
            MinRate = nullableToBr dl.minRate
            MaxRate = nullableToBr dl.maxRate
            MinRateAdj = nullableToBr dl.minRateAdj
            MaxRateAdj = nullableToBr dl.maxRateAdj
            Products = [||]
        }


    /// Expand one hierarchical record to one flat `DoseRuleData` per
    /// `(doseType, doseLimit)` pair. Enumeration order is preserved.
    let toDoseRuleData (r: Schema.DoseRuleExtracted) : DoseRuleData[] =
        [|
            for dt in (if isNull r.doseTypes then [||] else r.doseTypes) do
                let limits = if isNull dt.doseLimits then [||] else dt.doseLimits
                for dl in limits -> toDoseRuleDataOne r dt dl
        |]


    let applyToDoseRuleExtracted f (de: Schema.DoseRuleExtracted) : Schema.DoseRuleExtracted = f de


    /// Carve one `DoseLimit` out of a flat `DoseRuleData` row.
    let private toDoseLimit (d: DoseRuleData) : Schema.DoseLimit =
        {|
            ``component`` = d.Component
            substance = d.Substance
            doseUnit = d.DoseUnit
            adjustUnit = d.AdjustUnit
            rateUnit = d.RateUnit
            minQty = brOptToNullable d.MinQty
            maxQty = brOptToNullable d.MaxQty
            minQtyAdj = brOptToNullable d.MinQtyAdj
            maxQtyAdj = brOptToNullable d.MaxQtyAdj
            minPerTime = brOptToNullable d.MinPerTime
            maxPerTime = brOptToNullable d.MaxPerTime
            minPerTimeAdj = brOptToNullable d.MinPerTimeAdj
            maxPerTimeAdj = brOptToNullable d.MaxPerTimeAdj
            minRate = brOptToNullable d.MinRate
            maxRate = brOptToNullable d.MaxRate
            minRateAdj = brOptToNullable d.MinRateAdj
            maxRateAdj = brOptToNullable d.MaxRateAdj
        |}


    /// Build one `DoseType` from the subset of rows sharing the same
    /// dose-type identity (doseType + timing fields). Timing fields are
    /// copied from the first row; each row contributes one `DoseLimit`.
    let private toDoseType (rows: DoseRuleData[]) : Schema.DoseType =
        let head = rows[0]

        {|
            doseType = head.DoseType
            doseText = head.DoseText
            freqs = head.Frequencies |> Array.map (BigRational.toFloat >> int)
            freqUnit = head.FreqUnit
            minTime = brOptToNullable head.MinTime
            maxTime = brOptToNullable head.MaxTime
            timeUnit = head.TimeUnit
            minInt = brOptToNullable head.MinInterval
            maxInt = brOptToNullable head.MaxInterval
            intUnit = head.IntervalUnit
            minDur = brOptToNullable head.MinDur
            maxDur = brOptToNullable head.MaxDur
            durUnit = head.DurUnit
            doseLimits = rows |> Array.map toDoseLimit
        |}


    /// Group a flat `DoseRuleData[]` into hierarchical
    /// `DoseRuleExtracted[]`: rows sharing identity + patient fields
    /// collapse into one rule; within a rule, rows sharing dose-type +
    /// timing collapse into one `DoseType`; each row becomes one
    /// `DoseLimit`. Group order follows first-occurrence order of the
    /// input, so a TSV → hierarchy → TSV round-trip preserves row order.
    let doseRuleDataArrayToExtracted
        (ds: DoseRuleData[])
        : Schema.DoseRuleExtracted[]
        =
        let ruleKey (d: DoseRuleData) =
            {|
                source = d.Source
                generic = d.Generic
                form = d.Form
                brand = d.Brand
                gpks = d.GPKs |> Array.toList
                route = d.Route
                indication = d.Indication
                scheduleText = d.ScheduleText
                dep = d.Department
                gender = d.Gender
                minAge = d.MinAge
                maxAge = d.MaxAge
                minWeight = d.MinWeight
                maxWeight = d.MaxWeight
                minBSA = d.MinBSA
                maxBSA = d.MaxBSA
                minGestAge = d.MinGestAge
                maxGestAge = d.MaxGestAge
                minPMAge = d.MinPMAge
                maxPMAge = d.MaxPMAge
            |}

        let doseTypeKey (d: DoseRuleData) =
            {|
                doseType = d.DoseType
                doseText = d.DoseText
                freqs = d.Frequencies |> Array.toList
                freqUnit = d.FreqUnit
                minTime = d.MinTime
                maxTime = d.MaxTime
                timeUnit = d.TimeUnit
                minInterval = d.MinInterval
                maxInterval = d.MaxInterval
                intervalUnit = d.IntervalUnit
                minDur = d.MinDur
                maxDur = d.MaxDur
                durUnit = d.DurUnit
            |}

        ds
        |> Array.groupBy ruleKey
        |> Array.map (fun (_, group) ->
            let head = group[0]

            let doseTypes =
                group
                |> Array.groupBy doseTypeKey
                |> Array.map (fun (_, inner) -> toDoseType inner)

            {|
                sortNo = Nullable()
                source = head.Source
                generic = head.Generic
                form = head.Form
                brand = head.Brand
                gpks = head.GPKs
                route = head.Route
                indication = head.Indication
                scheduleText = head.ScheduleText
                dep = head.Department
                gender = head.Gender |> Gender.toString
                minAge = brOptToNullable head.MinAge
                maxAge = brOptToNullable head.MaxAge
                minWeight = brOptToNullable head.MinWeight
                maxWeight = brOptToNullable head.MaxWeight
                minBSA = brOptToNullable head.MinBSA
                maxBSA = brOptToNullable head.MaxBSA
                minGestAge = brOptToNullable head.MinGestAge
                maxGestAge = brOptToNullable head.MaxGestAge
                minPMAge = brOptToNullable head.MinPMAge
                maxPMAge = brOptToNullable head.MaxPMAge
                doseTypes = doseTypes
            |}
        )


    let private formatNullable (n: Nullable<float>) =
        if n.HasValue then n.Value.ToString(CultureInfo.InvariantCulture)
        else ""

    let private formatInt (n: Nullable<int>) =
        if n.HasValue then string n.Value else "0"


    /// Render one `(rule, doseType, doseLimit)` triple as a `\t`-joined
    /// TSV row matching `Schema.tsvColumns`. One hierarchical rule
    /// expands to `doseTypes.Length * doseLimits.Length` rows.
    let toTsvRow
        (r: Schema.DoseRuleExtracted)
        (dt: Schema.DoseType)
        (dl: Schema.DoseLimit)
        : string
        =
        let lookup =
            [
                "SortNo", formatInt r.sortNo
                "Source", orEmpty r.source
                "Generic", orEmpty r.generic
                "Form", orEmpty r.form
                "Brand", orEmpty r.brand
                "Route", orEmpty r.route
                "GPKs",
                    (if isNull r.gpks then "" else r.gpks |> String.concat ";")
                "Indication", orEmpty r.indication
                "ScheduleText", orEmpty r.scheduleText
                "Dep", orEmpty r.dep
                "Gender", orEmpty r.gender
                "MinAge", formatNullable r.minAge
                "MaxAge", formatNullable r.maxAge
                "MinWeight", formatNullable r.minWeight
                "MaxWeight", formatNullable r.maxWeight
                "MinBSA", formatNullable r.minBSA
                "MaxBSA", formatNullable r.maxBSA
                "MinGestAge", formatNullable r.minGestAge
                "MaxGestAge", formatNullable r.maxGestAge
                "MinPMAge", formatNullable r.minPMAge
                "MaxPMAge", formatNullable r.maxPMAge
                "DoseType", orEmpty dt.doseType
                "DoseText", orEmpty dt.doseText
                "Component", orEmpty dl.``component``
                "Substance", orEmpty dl.substance
                "Freqs",
                    (if isNull dt.freqs then ""
                     else dt.freqs |> Array.map string |> String.concat ";")
                "DoseUnit", orEmpty dl.doseUnit
                "AdjustUnit", orEmpty dl.adjustUnit
                "FreqUnit", orEmpty dt.freqUnit
                "RateUnit", orEmpty dl.rateUnit
                "MinTime", formatNullable dt.minTime
                "MaxTime", formatNullable dt.maxTime
                "TimeUnit", orEmpty dt.timeUnit
                "MinInt", formatNullable dt.minInt
                "MaxInt", formatNullable dt.maxInt
                "IntUnit", orEmpty dt.intUnit
                "MinDur", formatNullable dt.minDur
                "MaxDur", formatNullable dt.maxDur
                "DurUnit", orEmpty dt.durUnit
                "MinQty", formatNullable dl.minQty
                "MaxQty", formatNullable dl.maxQty
                "MinQtyAdj", formatNullable dl.minQtyAdj
                "MaxQtyAdj", formatNullable dl.maxQtyAdj
                "MinPerTime", formatNullable dl.minPerTime
                "MaxPerTime", formatNullable dl.maxPerTime
                "MinPerTimeAdj", formatNullable dl.minPerTimeAdj
                "MaxPerTimeAdj", formatNullable dl.maxPerTimeAdj
                "MinRate", formatNullable dl.minRate
                "MaxRate", formatNullable dl.maxRate
                "MinRateAdj", formatNullable dl.minRateAdj
                "MaxRateAdj", formatNullable dl.maxRateAdj
            ]
            |> Map.ofList

        Schema.tsvColumns
        |> List.map (fun c ->
            match lookup.TryFind c with
            | Some v -> v
            | None -> ""
        )
        |> String.concat "\t"


    /// Expand one hierarchical rule to one TSV row per
    /// `(doseType, doseLimit)` pair.
    let toTsvRows (r: Schema.DoseRuleExtracted) : string list =
        [
            for dt in (if isNull r.doseTypes then [||] else r.doseTypes) do
                let limits = if isNull dt.doseLimits then [||] else dt.doseLimits
                for dl in limits -> toTsvRow r dt dl
        ]


    /// Render a `DoseRuleExtractionResult` as a TSV block with the
    /// canonical header followed by one row per flattened
    /// `(rule, doseType, doseLimit)` triple.
    let toTsv (r: Schema.DoseRuleExtractionResult) : string =
        let header = Schema.tsvHeader

        let rows =
            r.rules
            |> Array.toList
            |> List.collect toTsvRows

        header :: rows |> String.concat "\n"


// =======================================================
// Pipeline — TSV parsing, GenFORM post-processing, and
// synchronous convenience wrappers.
// =======================================================

module Pipeline =

    /// Strip surrounding junk (leading/trailing whitespace, accidental code
    /// fences, stray leading tabs) and split into non-empty lines.
    let private cleanLines (tsv: string) =
        tsv.Split([| '\n' |])
        |> Array.map _.Trim('\r').TrimEnd()
        |> Array.filter (fun l -> not (String.isNullOrWhiteSpace l))
        |> Array.filter (fun l -> not (l.StartsWith("```")))
        |> Array.map (fun l -> if l.StartsWith("\t") then l.Substring(1) else l)


    /// Detect whether a line is the canonical TSV header (first field = "SortNo").
    let private isHeaderLine (line: string) =
        let first = line.Split('\t') |> Array.tryHead |> Option.defaultValue ""
        first.Trim() = "SortNo"


    /// Parse a TSV block into `DoseRuleData[]`. Injects the canonical header
    /// if the input skipped it. Column names and unit semantics follow
    /// `DoseRule.getData` in GenFORM.Lib.
    let parseTsv (tsv: string) : DoseRuleData[] =
        let cleaned = cleanLines tsv

        let lines =
            match cleaned |> Array.tryHead with
            | Some first when isHeaderLine first -> cleaned
            | _ -> Array.append [| Schema.tsvHeader |] cleaned

        if lines.Length < 2 then
            [||]
        else
            let rawRows = lines |> Array.map _.Split('\t')
            let headerLen = rawRows[0].Length

            let pad (row: string[]) =
                if row.Length >= headerLen then row
                else Array.append row (Array.create (headerLen - row.Length) "")

            let rows = rawRows |> Array.map pad
            let columns = rows[0]
            let getColumn = Csv.getStringColumn columns
            let toBrOpt = BigRational.toBrs >> Array.tryHead

            rows
            |> Array.tail
            |> Array.map (fun r ->
                let get name =
                    try
                        getColumn r name
                    with _ ->
                        ""

                {
                    Source = get "Source"
                    Indication = get "Indication"
                    Generic = get "Generic"
                    Form = get "Form"
                    Brand = get "Brand"
                    GPKs =
                        get "GPKs"
                        |> String.splitAt ';'
                        |> Array.map String.trim
                        |> Array.filter String.notEmpty
                        |> Array.distinct
                    Route = get "Route"
                    Department = get "Dep"
                    ScheduleText = get "ScheduleText"
                    Gender = get "Gender" |> Gender.fromString
                    MinAge = get "MinAge" |> toBrOpt
                    MaxAge = get "MaxAge" |> toBrOpt
                    MinWeight = get "MinWeight" |> toBrOpt
                    MaxWeight = get "MaxWeight" |> toBrOpt
                    MinBSA = get "MinBSA" |> toBrOpt
                    MaxBSA = get "MaxBSA" |> toBrOpt
                    MinGestAge = get "MinGestAge" |> toBrOpt
                    MaxGestAge = get "MaxGestAge" |> toBrOpt
                    MinPMAge = get "MinPMAge" |> toBrOpt
                    MaxPMAge = get "MaxPMAge" |> toBrOpt
                    DoseType = get "DoseType"
                    DoseText = get "DoseText"
                    Frequencies = get "Freqs" |> BigRational.toBrs
                    DoseUnit = get "DoseUnit"
                    AdjustUnit = get "AdjustUnit"
                    FreqUnit = get "FreqUnit"
                    RateUnit = get "RateUnit"
                    MinTime = get "MinTime" |> toBrOpt
                    MaxTime = get "MaxTime" |> toBrOpt
                    TimeUnit = get "TimeUnit"
                    MinInterval = get "MinInt" |> toBrOpt
                    MaxInterval = get "MaxInt" |> toBrOpt
                    IntervalUnit = get "IntUnit"
                    MinDur = get "MinDur" |> toBrOpt
                    MaxDur = get "MaxDur" |> toBrOpt
                    DurUnit = get "DurUnit"
                    Component = get "Component"
                    Substance = get "Substance"
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                    MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                    MinPerTime = get "MinPerTime" |> toBrOpt
                    MaxPerTime = get "MaxPerTime" |> toBrOpt
                    MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                    MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                    MinRate = get "MinRate" |> toBrOpt
                    MaxRate = get "MaxRate" |> toBrOpt
                    MinRateAdj = get "MinRateAdj" |> toBrOpt
                    MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                    Products = [||]
                }
            )


    /// Parse a TSV block into `DoseRuleExtracted[]` — the inverse of
    /// `Conversion.toTsv`. Flat rows are grouped into the hierarchical
    /// shape by rule identity + dose-type key. Used for round-trip
    /// checks and for replaying archived TSV rows through the JSON-based
    /// pipeline.
    let fromTsv (tsv: string) : Schema.DoseRuleExtracted[] =
        tsv
        |> parseTsv
        |> Conversion.doseRuleDataArrayToExtracted


    /// One row-level transformation. Multiple `Transform`s are composed
    /// with `>>` so any `DoseRuleExtracted -> DoseRuleExtracted` function
    /// (whether pure or effectful, e.g. `checkGrammar sender model`)
    /// can be added to a pipeline without changing call sites.
    type Transform = Schema.DoseRuleExtracted -> Schema.DoseRuleExtracted


    let applyToExtracted (f: Transform) (extracted: Schema.DoseRuleExtracted[]) =
        extracted
        |> Array.map f


    /// Apply a list of `Transform`s to every row, left-to-right. An empty
    /// list is a no-op.
    let applyAll (transforms: Transform list) (extracted: Schema.DoseRuleExtracted[]) =
        let composed = transforms |> List.fold (>>) id
        extracted |> Array.map composed


    /// Render a `DoseRuleExtracted[]` as the canonical TSV block
    /// (header + one row per rule). Complements `fromTsv`.
    let toTsv (extracted: Schema.DoseRuleExtracted[]) : string =
        {| rules = extracted |} |> Conversion.toTsv


    /// Given `/path/to/file.tsv`, return `/path/to/file.vN.tsv` where
    /// `N` is the next free integer. Existing `.vK.tsv` siblings are
    /// scanned; absent, `N = 2`. Never overwrites.
    let nextVersionedPath (path: string) : string =
        let dir =
            match Path.GetDirectoryName path with
            | null | "" -> "."
            | d -> d

        let baseName = Path.GetFileNameWithoutExtension path
        let ext = Path.GetExtension path

        let versionOf (f: string) =
            let name = Path.GetFileNameWithoutExtension f

            if name.StartsWith(baseName + ".v") then
                let tail = name.Substring((baseName + ".v").Length)
                match Int32.TryParse tail with
                | true, n -> Some n
                | false, _ -> None
            else
                None

        let nextN =
            Directory.GetFiles(dir, $"{baseName}.v*{ext}")
            |> Array.choose versionOf
            |> fun ns -> if Array.isEmpty ns then 1 else Array.max ns
            |> (+) 1

        Path.Combine(dir, $"{baseName}.v{nextN}{ext}")


    /// Read a TSV file, apply each `Transform` in order, write the result
    /// to `outputPath`. Returns the output path for convenience.
    let transformTsvFile
        (transforms: Transform list)
        (inputPath: string)
        (outputPath: string)
        : string
        =
        let tsv =
            inputPath
            |> File.ReadAllText
            |> fromTsv
            |> applyAll transforms
            |> toTsv

        File.WriteAllText(outputPath, tsv)
        outputPath


    /// Same as `transformTsvFile` but writes to an auto-versioned sibling
    /// (`file.v2.tsv`, `file.v3.tsv`, ...) next to the input.
    let transformTsvFileVersioned
        (transforms: Transform list)
        (inputPath: string)
        : string
        =
        transformTsvFile transforms inputPath (nextVersionedPath inputPath)


    /// Strip C0/C1 control characters while preserving newlines, carriage
    /// returns, and tabs. Null input collapses to an empty string.
    let removeNonReadable (s: string) =
        if isNull s then
            ""
        else
            let chars =
                s.ToCharArray()
                |> Array.filter (fun c ->
                    c = '\n' || c = '\r' || c = '\t' || not (Char.IsControl c)
                )

            System.String(chars)


    /// Deterministic whitespace normalization applied after the LLM
    /// grammar pass: normalize line endings, collapse runs of spaces /
    /// tabs (per line, so newlines are preserved), and drop leading /
    /// trailing blank lines. No content rewriting.
    let normalizeWhitespace (s: string) =
        if isNull s then
            ""
        else
            let lf = s.Replace("\r\n", "\n").Replace("\r", "\n")
            let collapseSpaces (line: string) =
                System.Text.RegularExpressions.Regex.Replace(line, @"[ \t]+", " ")

            lf.Split('\n')
            |> Array.map (collapseSpaces >> String.trim)
            |> Array.skipWhile String.isNullOrWhiteSpace
            |> Array.rev
            |> Array.skipWhile String.isNullOrWhiteSpace
            |> Array.rev
            |> String.concat "\n"


    /// Ask the LLM to correct grammar and punctuation in `scheduleText`,
    /// then strip non-readable characters and normalize whitespace. Falls
    /// back to the original text on LLM failure so the pipeline never loses
    /// data. Output may be multi-line — newlines from the LLM are preserved.
    let checkGrammar
        (sender: Extraction.Sender)
        (model: string)
        (de: Schema.DoseRuleExtracted)
        : Schema.DoseRuleExtracted
        =
        let cleaned =
            de.scheduleText
            |> Extraction.sanitizeText sender model
            |> Async.RunSynchronously
            |> Result.defaultValue de.scheduleText
            |> removeNonReadable
            |> normalizeWhitespace

        {| de with scheduleText = cleaned |}


    /// Overwrite `scheduleText` on a rule with the supplied (possibly
    /// multi-line) canonical text. Used after the validate step to
    /// commit the user-approved version.
    let saveScheduleText
        (cleanedText: string)
        (r: Schema.DoseRuleExtracted)
        : Schema.DoseRuleExtracted
        =
        {| r with scheduleText = cleanedText |}


    /// Feed the parsed `DoseRuleData` through the same post-processing
    /// chain as `DoseRule.get` (processDoseRuleData → mapToDoseRule →
    /// addDoseLimits) and return the resulting DoseRule array.
    let buildDoseRules (rules: Schema.DoseRuleExtracted[]) =
        let prods = Config.provider.GetProducts()
        let routeMapping = Config.provider.GetRouteMappings()
        let formRoutes = Config.provider.GetFormRoutes()

        // Serialise the extracted rules to the canonical Pass-4 TSV and run that
        // through the v2 GenFORM build chain (parseDoseRuleData -> addProducts ->
        // mapToDoseRule -> addDoseLimits), exactly as DoseRule.get does. The v2
        // rewrite removed the flat DoseRule.processDoseRuleData entry point, so we
        // go through the TSV/parser path instead.
        let matrix =
            {| rules = rules |}
            |> Conversion.toTsv
            |> _.Split('\n')
            |> Array.map _.Split('\t')

        DoseRule.get (fun () -> DoseRule.parseDoseRuleData matrix) routeMapping formRoutes prods
        |> Result.map fst


    /// End-to-end: free text → `DoseRuleExtractionResult` via Ollama →
    /// `DoseRuleData[]` → `DoseRule[]` → markdown string.
    let extractAndFormat
        (model: string)
        (sourceName: string)
        (freeText: string)
        =
        async {
            let! res =
                Extraction.extractDoseRule Extraction.ollamaSender model sourceName freeText

            match res with
            | Error e -> return Error $"LLM/JSON error: {e}"
            | Ok payload ->
                if Array.isEmpty payload.rules then
                    return Error "LLM returned zero rules"
                else
                    match buildDoseRules payload.rules with
                    | Error msgs -> return Error $"buildDoseRules failed: {msgs}"
                    // No resource provider here, and these rules were just extracted by an
                    // LLM rather than looked up, so there is nothing to link out to.
                    | Ok rules -> return rules |> DoseRule.Print.toMarkdown Source.noLinks |> Ok
        }


    /// Synchronous wrapper: extract and print with the default model.
    let runAndPrint (sourceName: string) (freeText: string) =
        match
            extractAndFormat Config.defaultModel sourceName freeText
            |> Async.RunSynchronously
        with
        | Ok md -> printfn $"{md}"
        | Error e -> eprintfn $"Extraction failed: {e}"


    /// Synchronous wrapper: extract and print with a specified model.
    let runWithModelAndPrint
        (model: string)
        (sourceName: string)
        (freeText: string)
        =
        match extractAndFormat model sourceName freeText |> Async.RunSynchronously with
        | Ok md -> printfn $"{md}"
        | Error e -> eprintfn $"Extraction failed: {e}"


// =======================================================
// RuleValidation — structural checks on one hierarchical
// `DoseRuleExtracted` produced from one input `scheduleText`.
// Patient-group identity is now guaranteed by construction
// (one rule = one group); the auto-checks cover dose-type /
// dose-limit uniqueness only.
// =======================================================

module RuleValidation =

    /// Structural summary of one hierarchical rule.
    type RuleSummary =
        {|
            doseTypes: string[]
            components: string[]
            substances: string[]
            duplicateCombos: (string * string * string * string)[]
        |}


    /// Distinct dose-type labels, components and substances inside the
    /// rule, plus any `(doseType, doseText, component, substance)`
    /// quadruples that repeat across dose-limits. Two dose types may
    /// legitimately share a `doseType` label (e.g. load + maintenance
    /// both `timed`) provided their `doseText` distinguishes them;
    /// duplication is flagged per-limit against the full key. Pure, no IO.
    let summarise (r: Schema.DoseRuleExtracted) : RuleSummary =
        let dts =
            if isNull r.doseTypes then [||] else r.doseTypes

        let dtLabels = dts |> Array.map _.doseType

        let combos =
            [|
                for dt in dts do
                    let limits =
                        if isNull dt.doseLimits then [||] else dt.doseLimits

                    for dl in limits ->
                        dt.doseType, dt.doseText, dl.``component``, dl.substance
            |]

        let dupCombos =
            combos
            |> Array.countBy id
            |> Array.choose (fun (k, n) -> if n > 1 then Some k else None)

        {|
            doseTypes = dtLabels |> Array.distinct
            components =
                combos |> Array.map (fun (_, _, c, _) -> c) |> Array.distinct
            substances =
                combos |> Array.map (fun (_, _, _, s) -> s) |> Array.distinct
            duplicateCombos = dupCombos
        |}


    /// A rule must carry at least one `DoseType`.
    let validateDoseTypesPresent
        (r: Schema.DoseRuleExtracted)
        : Result<unit, string>
        =
        if isNull r.doseTypes || r.doseTypes.Length = 0 then
            Error "doseTypes is empty"
        else
            Ok()


    /// Each `(doseType, doseText, component, substance)` quadruple must
    /// appear at most once across all dose-limits in the rule. Two
    /// dose-type entries may share a `doseType` label if distinguished
    /// by `doseText`.
    let validateUniqueDoseLimits
        (r: Schema.DoseRuleExtracted)
        : Result<unit, string list>
        =
        let dups = (summarise r).duplicateCombos

        if Array.isEmpty dups then
            Ok()
        else
            dups
            |> Array.map (fun (dt, dtx, c, s) ->
                let text = if dtx = "" then "" else $" doseText=%s{dtx}"
                $"duplicate dose-limit combo: doseType=%s{dt}{text}, component=%s{c}, substance=%s{s}"
            )
            |> Array.toList
            |> Error


    /// Combined check: returns `Ok r` on full pass, `Error report` otherwise.
    let validate
        (r: Schema.DoseRuleExtracted)
        : Result<Schema.DoseRuleExtracted, string list>
        =
        let dtPresent =
            match validateDoseTypesPresent r with
            | Ok() -> []
            | Error e -> [ e ]

        let dlUnique =
            match validateUniqueDoseLimits r with
            | Ok() -> []
            | Error es -> es

        match dtPresent @ dlUnique with
        | [] -> Ok r
        | errs -> Error errs


    /// Pretty-print the structural summary for FSI inspection.
    let printCounts (r: Schema.DoseRuleExtracted) =
        let s = summarise r
        let join (xs: string[]) = String.concat ", " xs

        let totalLimits =
            if isNull r.doseTypes then
                0
            else
                r.doseTypes
                |> Array.sumBy (fun dt ->
                    if isNull dt.doseLimits then 0 else dt.doseLimits.Length
                )

        printfn $"""doseTypes:  {s.doseTypes.Length} [{join s.doseTypes}]"""
        printfn $"""components: {s.components.Length} [{join s.components}]"""
        printfn $"""substances: {s.substances.Length} [{join s.substances}]"""
        printfn $"""doseLimits: {totalLimits}"""

        if not (Array.isEmpty s.duplicateCombos) then
            printfn $"duplicate combos: {s.duplicateCombos.Length}"

            for (dt, dtx, comp, sub) in s.duplicateCombos do
                let text = if dtx = "" then "" else $" doseText={dtx}"
                printfn $"  - doseType={dt}{text}, component={comp}, substance={sub}"


// =======================================================
// Printing — pretty-print a DoseRuleExtracted for inspection.
// Ported from DoseRuleExtraction.fsx and remapped to the
// unified record shape (dep / minInt / minDur / freqs).
// =======================================================

module Printing =

    let private nullable (n: Nullable<float>) =
        if n.HasValue then string n.Value else "—"


    /// Pretty-print one dose-limit block — the quantitative payload
    /// attached to a `(doseType, component, substance)` triple.
    let private printDoseLimit
        (dt: Schema.DoseType)
        (dl: Schema.DoseLimit)
        =
        printfn
            $"""  - substance  : %s{dl.substance}
    component  : %s{dl.``component``}
    doseUnit   : %s{dl.doseUnit}
    adjustUnit : %s{dl.adjustUnit}
    rateUnit   : %s{dl.rateUnit}
    Qty        : %s{nullable dl.minQty} — %s{nullable dl.maxQty} %s{dl.doseUnit}
    QtyAdj     : %s{nullable dl.minQtyAdj} — %s{nullable dl.maxQtyAdj} %s{dl.doseUnit}/%s{dl.adjustUnit}
    PerTime    : %s{nullable dl.minPerTime} — %s{nullable dl.maxPerTime} %s{dl.doseUnit}/%s{dt.freqUnit}
    PerTimeAdj : %s{nullable dl.minPerTimeAdj} — %s{nullable dl.maxPerTimeAdj} %s{dl.doseUnit}/%s{dl.adjustUnit}/%s{dt.freqUnit}
    Rate       : %s{nullable dl.minRate} — %s{nullable dl.maxRate} %s{dl.doseUnit}/%s{dl.rateUnit}
    RateAdj    : %s{nullable dl.minRateAdj} — %s{nullable dl.maxRateAdj} %s{dl.doseUnit}/%s{dl.adjustUnit}/%s{dl.rateUnit}"""


    /// Pretty-print one dose-type block: the temporal profile followed
    /// by every nested dose-limit.
    let private printDoseType (dt: Schema.DoseType) =
        let freqs =
            if isNull dt.freqs then ""
            else dt.freqs |> Array.map string |> String.concat ";"

        printfn
            $"""
### Dose type
  doseType   : %s{dt.doseType}
  doseText   : %s{dt.doseText}

### Schedule
  frequencies: %s{freqs} × %s{dt.freqUnit}
  time       : %s{nullable dt.minTime} — %s{nullable dt.maxTime} %s{dt.timeUnit}
  interval   : %s{nullable dt.minInt} — %s{nullable dt.maxInt} %s{dt.intUnit}
  duration   : %s{nullable dt.minDur} — %s{nullable dt.maxDur} %s{dt.durUnit}

### Dose limits"""

        let limits =
            if isNull dt.doseLimits then [||] else dt.doseLimits

        if Array.isEmpty limits then
            printfn "  (none)"
        else
            limits |> Array.iter (printDoseLimit dt)


    /// Pretty-print an extracted `DoseRuleExtracted` record for
    /// inspection: identity + patient at the top, then every nested
    /// `DoseType` / `DoseLimit`.
    let printExtracted (r: Schema.DoseRuleExtracted) =
        printfn
            $"""
## Extracted DoseRule

### Medication
  generic    : %s{r.generic}
  form       : %s{r.form}
  brand      : %s{r.brand}
  gpks       : %s{if isNull r.gpks then "" else r.gpks |> String.concat ", "}
  route      : %s{r.route}
  indication : %s{r.indication}
  department : %s{r.dep}

### Source
  source     : %s{r.source}

### Patient
  gender     : %s{r.gender}
  age        : %s{nullable r.minAge} — %s{nullable r.maxAge} days
  weight     : %s{nullable r.minWeight} — %s{nullable r.maxWeight} g
  BSA        : %s{nullable r.minBSA} — %s{nullable r.maxBSA} m2
  gestAge    : %s{nullable r.minGestAge} — %s{nullable r.maxGestAge} days
  PMAge      : %s{nullable r.minPMAge} — %s{nullable r.maxPMAge} days"""

        let dts =
            if isNull r.doseTypes then [||] else r.doseTypes

        if Array.isEmpty dts then
            printfn ""
            printfn "### Dose type"
            printfn "  (none)"
        else
            dts |> Array.iter printDoseType


    /// Print every rule in a `DoseRuleExtractionResult` via `printExtracted`.
    let printExtractionResult (r: Schema.DoseRuleExtractionResult) =
        if isNull (box r.rules) || r.rules.Length = 0 then
            printfn "(no rules)"
        else
            r.rules |> Array.iter printExtracted


// =======================================================
// Interactive — walk one scheduleText through extract →
// checkGrammar → validate → save, prompting between every
// stage so the user can step through, save the current
// canonical text and exit, or abort entirely.
// =======================================================

module Interactive =

    type StepDecision =
        | Proceed
        | SaveAndExit
        | Exit


    type RunResult =
        | Saved of Schema.DoseRuleExtracted
        | Aborted of stage: string
        | Failed of string


    /// Read one line from stdin and map to a step decision. Defaults to
    /// `Proceed` on empty / unrecognised input. Case-insensitive.
    let promptStep (label: string) : StepDecision =
        printfn ""
        printfn $"[{label}] (P)roceed / (S)ave and exit / (E)xit  [P]:"
        let line = Console.ReadLine()

        let key =
            if isNull line then ""
            else line.Trim().ToUpperInvariant()

        if key.StartsWith "S" then SaveAndExit
        elif key.StartsWith "E" then Exit
        else Proceed


    /// Commit the current `scheduleText` as-is — hierarchical records
    /// carry the text in one place, so this is just an identity pass.
    let private saveCurrent (r: Schema.DoseRuleExtracted) = r


    /// Step the user through extract → checkGrammar → validate → save
    /// for ONE `scheduleText`. The LLM is expected to return exactly
    /// one hierarchical `DoseRuleExtracted` (multiple entries trigger
    /// `Failed`). Each prompt accepts P / S / E (case-insensitive);
    /// empty defaults to Proceed. SaveAndExit returns `Saved` with the
    /// current text committed. Exit returns `Aborted`.
    let runInteractive
        (sender: Extraction.Sender)
        (model: string)
        (sourceName: string)
        (freeText: string)
        : RunResult
        =
        // Stage 1: extract
        let extracted =
            match
                Extraction.extractDoseRule sender model sourceName freeText
                |> Async.RunSynchronously
            with
            | Ok r -> Ok r.rules
            | Error e -> Error e

        match extracted with
        | Error e -> Failed $"extraction failed: {e}"
        | Ok rs when Array.isEmpty rs -> Failed "extraction returned zero rules"
        | Ok rs when rs.Length > 1 ->
            Failed $"extraction returned {rs.Length} rules; expected 1 per scheduleText"
        | Ok rs ->
            let r0 = rs[0]

            printfn "=== Stage 1: extracted ==="
            r0 |> RuleValidation.printCounts

            match promptStep "after extract" with
            | Exit -> Aborted "extract"
            | SaveAndExit -> Saved(saveCurrent r0)
            | Proceed ->
                // Stage 2: grammar check
                printfn ""
                printfn "=== Stage 2: checkGrammar (LLM, may take a while) ==="
                let cleaned = Pipeline.checkGrammar sender model r0

                printfn "scheduleText (cleaned):"
                printfn "%s" cleaned.scheduleText

                match promptStep "after checkGrammar" with
                | Exit -> Aborted "checkGrammar"
                | SaveAndExit -> Saved(saveCurrent cleaned)
                | Proceed ->
                    // Stage 3: validate
                    printfn ""
                    printfn "=== Stage 3: validate ==="

                    match RuleValidation.validate cleaned with
                    | Error errs ->
                        errs |> List.iter (printfn "FAIL: %s")
                        printfn ""
                        printfn "Manually rewrite (and possibly split) the scheduleText, then re-run."
                        Failed(String.concat "; " errs)
                    | Ok r ->
                        printfn "validation OK"

                        match promptStep "after validate" with
                        | Exit -> Aborted "validate"
                        | SaveAndExit
                        | Proceed -> Saved(saveCurrent r)


// =======================================================
// Benchmark — evaluate local Ollama models against the
// authoritative dose-rule TSV. Unchanged in behaviour;
// re-points to Extraction.extractDoseRule +
// Conversion.toDoseRuleData (which now returns an array
// per hierarchical rule — flattened via Array.collect).
// =======================================================

module Benchmark =

    open System.Diagnostics
    open MathNet.Numerics

    /// Canonical TSV path relative to the repo root.
    let tsvPath =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "../../../data/sources/Rules/doserules.tsv"
        )
        |> Path.GetFullPath


    /// Parse the authoritative TSV into `DoseRuleData[]`.
    let loadGroundTruth () : DoseRuleData[] =
        let raw = File.ReadAllText tsvPath
        Pipeline.parseTsv raw


    /// A benchmark sample: one input ScheduleText plus all ground-truth
    /// rows that share it (one per Substance / phase).
    type Sample =
        {
            Label: string
            ScheduleText: string
            Expected: DoseRuleData[]
        }


    /// Pick `count` ScheduleText groups from the ground truth, biased
    /// toward multi-phase paragraphs (more interesting to grade) but with
    /// one single-row group for coverage.
    let pickSamples (count: int) (seed: int) (data: DoseRuleData[]) : Sample[] =
        let rng = Random(seed)

        let groups =
            data
            |> Array.filter (fun d -> String.notEmpty d.ScheduleText)
            |> Array.groupBy _.ScheduleText.Trim()
            |> Array.map (fun (t, rs) -> t, rs)

        let multiPhase =
            groups
            |> Array.filter (fun (_, rs) -> rs.Length >= 2)
            |> Array.sortBy (fun _ -> rng.Next())
            |> Array.truncate (max 1 (count - 1))

        let singlePhase =
            groups
            |> Array.filter (fun (_, rs) -> rs.Length = 1)
            |> Array.sortBy (fun _ -> rng.Next())
            |> Array.truncate 1

        Array.append multiPhase singlePhase
        |> Array.mapi (fun i (text, rows) ->
            let first = rows |> Array.head
            let label =
                $"#{i + 1} {first.Generic} / {first.Route} / {first.Indication}"
                |> fun s -> if s.Length > 80 then s.Substring(0, 80) + "…" else s

            {
                Label = label
                ScheduleText = text
                Expected = rows
            }
        )


    /// Result of grading one extraction.
    type Grade =
        {
            Model: string
            Sample: string
            RowCountExpected: int
            RowCountExtracted: int
            DoseTypesMatch: bool
            DoseTextsMatch: bool
            SubstancesMatch: bool
            NumericFieldMatches: int
            NumericFieldMax: int
            ElapsedMs: int64
            Error: string option
        }

        member this.Score =
            if this.Error.IsSome then 0
            else
                let rowPts = if this.RowCountExpected = this.RowCountExtracted then 2 else 0
                let dtPts = if this.DoseTypesMatch then 2 else 0
                let textPts = if this.DoseTextsMatch then 1 else 0
                let subPts = if this.SubstancesMatch then 1 else 0
                let numPts = this.NumericFieldMatches
                rowPts + dtPts + textPts + subPts + numPts

        member this.MaxScore =
            2 + 2 + 1 + 1 + this.NumericFieldMax


    let private eqBr (a: BigRational option) (b: BigRational option) =
        match a, b with
        | None, None -> true
        | Some x, Some y -> x = y
        | _ -> false


    let private numericFields
        : (DoseRuleData -> BigRational option) list
        =
        [
            _.MinQtyAdj
            _.MaxQtyAdj
            _.MinPerTime
            _.MaxPerTime
            _.MinPerTimeAdj
            _.MaxPerTimeAdj
            _.MinRate
            _.MaxRate
        ]


    let private gradeRows
        (expected: DoseRuleData[])
        (extracted: DoseRuleData[])
        =
        let key (r: DoseRuleData) =
            r.DoseType, r.DoseText.ToLower(), r.Substance.ToLower()

        let byKey = extracted |> Array.map (fun r -> key r, r) |> Map.ofArray

        let mutable matches = 0
        let mutable max = 0

        for exp in expected do
            let k = key exp
            match byKey |> Map.tryFind k with
            | None ->
                for getField in numericFields do
                    if (getField exp).IsSome then
                        max <- max + 1
            | Some act ->
                for getField in numericFields do
                    let e = getField exp
                    if e.IsSome then
                        max <- max + 1
                        if eqBr e (getField act) then
                            matches <- matches + 1

        matches, max


    let private lowerSet xs =
        xs |> Array.map (fun (s: string) -> s.ToLower()) |> Set.ofArray


    let private gradeSample (model: string) (sample: Sample) : Grade =
        let sw = Stopwatch.StartNew()

        let runAsync =
            async {
                try
                    let! res =
                        Extraction.extractDoseRule
                            Extraction.ollamaSender
                            model
                            "benchmark"
                            sample.ScheduleText

                    match res with
                    | Error e -> return Error e
                    | Ok payload ->
                        let rows =
                            payload.rules |> Array.collect Conversion.toDoseRuleData

                        return Ok rows
                with ex ->
                    return Error ex.Message
            }

        let result =
            try
                Async.RunSynchronously(runAsync, timeout = 10 * 60 * 1000)
            with ex ->
                Error ex.Message

        sw.Stop()

        match result with
        | Error e ->
            {
                Model = model
                Sample = sample.Label
                RowCountExpected = sample.Expected.Length
                RowCountExtracted = 0
                DoseTypesMatch = false
                DoseTextsMatch = false
                SubstancesMatch = false
                NumericFieldMatches = 0
                NumericFieldMax = 0
                ElapsedMs = sw.ElapsedMilliseconds
                Error = Some e
            }
        | Ok extracted ->
            let expTypes = sample.Expected |> Array.map _.DoseType |> lowerSet
            let actTypes = extracted |> Array.map _.DoseType |> lowerSet

            let expTexts = sample.Expected |> Array.map _.DoseText |> lowerSet
            let actTexts = extracted |> Array.map _.DoseText |> lowerSet

            let expSubs = sample.Expected |> Array.map _.Substance |> lowerSet
            let actSubs = extracted |> Array.map _.Substance |> lowerSet

            let numMatches, numMax = gradeRows sample.Expected extracted

            {
                Model = model
                Sample = sample.Label
                RowCountExpected = sample.Expected.Length
                RowCountExtracted = extracted.Length
                DoseTypesMatch = expTypes = actTypes
                DoseTextsMatch = expTexts = actTexts
                SubstancesMatch = expSubs = actSubs
                NumericFieldMatches = numMatches
                NumericFieldMax = numMax
                ElapsedMs = sw.ElapsedMilliseconds
                Error = None
            }


    let private printGradeLine (g: Grade) =
        let status =
            match g.Error with
            | Some e -> $"ERR: {e.Substring(0, min 60 e.Length)}"
            | None ->
                let dt = if g.DoseTypesMatch then "✓DT" else "✗DT"
                let dx = if g.DoseTextsMatch then "✓TX" else "✗TX"
                let sub = if g.SubstancesMatch then "✓Sub" else "✗Sub"
                $"rows {g.RowCountExtracted}/{g.RowCountExpected} {dt} {dx} {sub} num {g.NumericFieldMatches}/{g.NumericFieldMax}"

        printfn
            $"  %-28s{g.Model} %-40s{g.Sample.PadRight(40).Substring(0, 40)} score %2d{g.Score}/%-2d{g.MaxScore} %6d{g.ElapsedMs}ms  %s{status}"


    let private printScoreboard (grades: Grade[]) =
        let groups = grades |> Array.groupBy _.Model

        printfn ""
        printfn "=========================================================================="
        printfn "Scoreboard (higher is better)"
        printfn "=========================================================================="
        printfn "%-28s %-10s %-10s %-12s %s" "Model" "Score" "Max" "Avg ms" "Errors"

        for model, gs in
            groups
            |> Array.sortByDescending (fun (_, gs) -> gs |> Array.sumBy _.Score)
            do
            let total = gs |> Array.sumBy _.Score
            let max = gs |> Array.sumBy _.MaxScore
            let avg =
                if gs.Length > 0 then
                    (gs |> Array.sumBy _.ElapsedMs) / int64 gs.Length
                else
                    0L

            let errs = gs |> Array.filter _.Error.IsSome |> Array.length

            printfn $"%-28s{model} %-10d{total} %-10d{max} %-12d{avg} %d{errs}"


    /// Run the full benchmark. Prints progress per (model, sample) and a
    /// final scoreboard.
    let run (models: string list) (sampleCount: int) (seed: int) : Grade[] =
        let gt = loadGroundTruth ()
        printfn $"Loaded %d{gt.Length} ground-truth DoseRuleData rows from %s{tsvPath}"

        let samples = pickSamples sampleCount seed gt
        printfn $"Picked %d{samples.Length} samples (seed=%d{seed})."

        samples
        |> Array.iteri (fun i s ->
            printfn
                $"  Sample %d{i + 1}: %s{s.Label} (%d{s.Expected.Length} expected row(s), %d{s.ScheduleText.Length} chars)"
        )

        printfn ""

        let grades = ResizeArray<Grade>()

        for model in models do
            printfn $"--- %s{model} ---"

            for sample in samples do
                let g = gradeSample model sample
                grades.Add g
                printGradeLine g

            printfn ""

        let arr = grades.ToArray()
        printScoreboard arr
        arr


// =======================================================
// Example / benchmark invocation — comment out when
// loading the script without triggering a network call.
// =======================================================

/// Models available on the local Ollama server (adjust for your machine):
let benchmarkModels =
    [
        Config.defaultModel
    ]


let freeText =
    """
acetylsalicylzuur, oraal, Ziekte van Kawasaki.
1 maand tot 18 jaar Startdosering: Acetylsalicylzuur: 30 - 50 mg/kg/dag in 3 - 4 doses.
Max: 3.000 mg/dag.
Onderhoudsdosering: Nadat temperatuur genormaliseerd is en CRP gedaald:
dosering verlagen tot 3 - 5 mg/kg/dag in 1 dosis.
"""


// Uncomment one of the following to exercise the pipeline interactively:
//
//   Pipeline.runWithModelAndPrint Config.defaultModel "Kinderformularium" freeText
//
//   match
//       Extraction.extractDoseRule
//           Extraction.ollamaSender
//           Config.defaultModel
//           "Kinderformularium"
//           freeText
//       |> Async.RunSynchronously
//   with
//   | Ok r -> Printing.printExtractionResult r
//   | Error e -> eprintfn $"Extraction failed: {e}"


// =======================================================
// Interactive driver — extract → checkGrammar → validate
// → save, with a per-stage prompt (Proceed / Save and exit
// / Exit). On a validate failure, manually rewrite (and
// possibly split) the scheduleText, then re-run.
// =======================================================

//   match
//       Interactive.runInteractive
//           Extraction.ollamaSender
//           Config.defaultModel
//           "Kinderformularium"
//           freeText
//   with
//   | Interactive.Saved rs   -> rs |> Pipeline.toTsv |> printfn "%s"
//   | Interactive.Aborted s  -> printfn "aborted at stage: %s" s
//   | Interactive.Failed e   -> eprintfn "failed: %s" e


// Skip the benchmark when the script is loaded purely to exercise the
// types (TSV round-trip checks, tests, etc). Set `GENPRES_NLP_SKIP_BENCHMARK=1`
// in the environment to suppress the Ollama call.
let skipBenchmark =
    match Environment.GetEnvironmentVariable "GENPRES_NLP_SKIP_BENCHMARK" with
    | null
    | "" -> false
    | _ -> true

if not skipBenchmark then
    Benchmark.run benchmarkModels 100 42 |> ignore
