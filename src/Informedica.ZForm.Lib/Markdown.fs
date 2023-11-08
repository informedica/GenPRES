namespace Informedica.ZForm.Lib


module Markdown =

    open System
    open Markdig
    open Informedica.Utils.Lib


    /// Converts a markdown string to an HTML string.
    let toHtml (s : string) = Markdown.ToHtml(s)


    /// Opens an HTML string in the default browser.
    let htmlToBrowser html =
        let proc = new System.Diagnostics.Process()
        proc.EnableRaisingEvents <- false

        let tmp = IO.Path.GetTempPath() + "/temp.html"

        html
        |> File.writeTextToFile tmp

        proc.StartInfo.FileName <- tmp

        proc.Start() |> ignore
        proc.Close()


    /// Converts a markdown string to an HTML string and opens it in the default browser.
    let toBrowser s =
        s
        |> toHtml
        |> htmlToBrowser