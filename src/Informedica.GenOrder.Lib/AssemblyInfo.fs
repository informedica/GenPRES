namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Informedica.GenOrder.Lib")>]
[<assembly: AssemblyProductAttribute("Informedica.GenOrder.Lib")>]
[<assembly: AssemblyCompanyAttribute("halcwb")>]
[<assembly: AssemblyDescriptionAttribute("A library that models medical orders allowing calculation and planning")>]
[<assembly: AssemblyVersionAttribute("0.0.4")>]
[<assembly: AssemblyFileVersionAttribute("0.0.4")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.4"
    let [<Literal>] InformationalVersion = "0.0.4"
