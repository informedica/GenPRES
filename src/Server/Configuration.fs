namespace GenPres.Server

open GenPres.Shared

module Configuration =
    open System.IO
    open Thoth.Json.Net
    open Types.Configuration

    let dataPath = Path.GetFullPath "./../../data/config/genpres.config.json"

    let createSettings dep mina maxa minw maxw =
        {
            Department = dep
            MinAge = mina
            MaxAge = maxa
            MinWeight = minw
            MaxWeight = maxw
        }

    let getSettings () =
        Path.GetFullPath dataPath
        |> File.ReadAllText
        |> Decode.Auto.unsafeFromString<Configuration>