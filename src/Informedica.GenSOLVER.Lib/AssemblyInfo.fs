namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Informedica.GenSolver.Lib")>]
[<assembly: AssemblyProductAttribute("Informedica.GenSolver.Lib")>]
[<assembly: AssemblyCompanyAttribute("halcwb")>]
[<assembly: AssemblyDescriptionAttribute("A solver that solves a set of product and sum equations in which variables are discrete sets of rational numbers or ranges limited by an minimum or maximum, or an increment")>]
[<assembly: AssemblyVersionAttribute("0.2.2")>]
[<assembly: AssemblyFileVersionAttribute("0.2.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.2.2"
