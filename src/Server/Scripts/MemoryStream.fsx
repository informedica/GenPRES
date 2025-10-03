
#load "load.fsx"

#r "nuget: Microsoft.IO.RecyclableMemoryStream, 2.1.2"



open Microsoft.IO
open Newtonsoft.Json

let manager = RecyclableMemoryStreamManager()


open Informedica.GenForm.Lib

module FilePath = Informedica.ZIndex.Lib.FilePath

let provider : Resources.IResourceProvider = Api.getCachedProviderWithDataUrlId Logging.noOp ""

// Serialize all dose rules as a single JSON array
let doseRulesJson =
    provider.GetDoseRules ()
    |> JsonConvert.SerializeObject

// Write the JSON string to the stream
let buffer = System.Text.Encoding.UTF8.GetBytes(doseRulesJson)
let stream = manager.GetStream("doserules")
stream.Write(buffer, 0, buffer.Length)

// return the doserule objects from the stream
let doserules =
    // Reset stream position to beginning
    stream.Position <- 0L
    // Read the entire stream content as string
    use reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, -1, true)
    let json = reader.ReadToEnd()
    // Deserialize the JSON string to DoseRule array
    JsonConvert.DeserializeObject<DoseRule[]>(json)