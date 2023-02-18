namespace Informedica.GenForm.Lib


[<AutoOpen>]
module Utils =

    open System
    open System.IO
    open System.Net.Http

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL



    module Web =


        //https://docs.google.com/spreadsheets/d/1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g/edit?usp=sharing
        let [<Literal>] dataUrlId = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"

        //https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
        let [<Literal>] dataUrlId2 = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"



        let getDataFromSheet = Web.GoogleSheets.getDataFromSheet



    module BigRational =

        open MathNet.Numerics


        let toBrs s =
            s
            |> String.splitAt ';'
            |> Array.choose Double.tryParse
            |> Array.choose BigRational.fromFloat


        let toBrOpt brs = brs |> Array.tryHead


        let tupleBrOpt brs1 brs2 =
            brs1 |> Array.tryHead,
            brs2 |> Array.tryHead
