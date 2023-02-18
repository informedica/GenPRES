namespace Informedica.ZForm.Lib


module Markdown =


    open System

    open Markdig

    open Informedica.Utils.Lib


    let toHtml (s : string) = Markdown.ToHtml(s)


    let htmlToBrowser html =
        let proc = new System.Diagnostics.Process()
        proc.EnableRaisingEvents <- false

        let tmp = IO.Path.GetTempPath() + "/temp.html"

        html
        |> File.writeTextToFile tmp

        proc.StartInfo.FileName <- tmp

        proc.Start() |> ignore
        proc.Close()


    let toBrowser s =
        s
        |> toHtml
        |> htmlToBrowser