
#load "load.fsx"

#r "nuget: Microsoft.IO.RecyclableMemoryStream, 2.1.2"



open Microsoft.IO
open Newtonsoft.Json

let manager = RecyclableMemoryStreamManager()


open Informedica.GenForm.Lib
module FilePath = Informedica.ZIndex.Lib.FilePath


DoseRule.get ()
|> Array.map JsonConvert.SerializeObject
|> Array.map System.Text.Encoding.UTF8.GetBytes
|> Array.iter (fun buffer ->
    manager.GetStream("doserules").Write(buffer, 0, buffer.Length)
)

// return the doserule objects from the steam
let doserules =
    manager.GetStream("doserules")
    |> JsonConvert.DeserializeObject<DoseRule[]>



