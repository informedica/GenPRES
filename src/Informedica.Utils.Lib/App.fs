namespace Informedica.Utils.Lib

module App = 

    open System
    
    let dataDir = 
        if AppDomain.CurrentDomain |> isNull ||  
           AppDomain.CurrentDomain.GetData("DataDirectory") |> isNull then ""
        else
            AppDomain.CurrentDomain.GetData("DataDirectory").ToString()

