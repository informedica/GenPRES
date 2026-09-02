// Centralized NuGet dependencies for all F# scripts.
// Pin versions here to avoid design-time conflicts (e.g. FSharp.Data type providers).
// Versions match paket.lock.

// note: FSharp.Data should match the packet.lock version
#r "nuget: FSharp.Data, 8.1.10"

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Markdig"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharpPlus, 1.7.0"
#r "nuget: HtmlAgilityPack"
#r "nuget: ConsoleTables"
#r "nuget: Validus"
#r "nuget: IcedTasks"
#r "nuget: Unquote"
#r "nuget: NJsonSchema"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
