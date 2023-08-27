namespace Informedica.Utils.Lib

module App =

    open System
    

    /// Get the data directory of the application
    let dataDir =
        if AppDomain.CurrentDomain |> isNull ||
           AppDomain.CurrentDomain.GetData("DataDirectory") |> isNull then ""
        else
            AppDomain.CurrentDomain.GetData("DataDirectory").ToString()

