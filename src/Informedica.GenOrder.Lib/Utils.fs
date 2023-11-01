namespace Informedica.GenOrder.Lib


[<AutoOpen>]
module Utils =

    open System
    open System.IO
    open System.Net.Http

    open Informedica.Utils.Lib.Web


    module Web =



        //https://docs.google.com/spreadsheets/d/1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g/edit?usp=sharing
        let [<Literal>] constraints = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        //https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
        let [<Literal>] genpres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"



        /// <summary>
        /// Get data from a google sheet containing constraints
        /// </summary>
        /// <param name="sheet">The sheet to get</param>
        /// <returns>A array table of string arrays</returns>
        let getDataFromConstraints sheet = GoogleSheets.getDataFromSheet constraints sheet


        /// <summary>
        /// Get data from a google sheet containing data for GenPres
        /// </summary>
        /// <param name="sheet">The sheet to get</param>
        /// <returns>A array table of string arrays</returns>
        let getDataFromGenPres sheet = GoogleSheets.getDataFromSheet genpres sheet


