namespace Informedica.ZIndex.Lib


module Names =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL

    open Types.Names


    /// Map item to TSNR, i.e. the item number in BST902T
    let mapItem = function
        | Form -> 6
        | Route -> 7
        | GenericUnit -> 2
        | FormUnit -> 2
        | PrescriptionContainer -> 73
        | ConsumerContainer -> 4


    /// <summary>
    /// Get the name of a record in BST020T
    /// </summary>
    /// <param name="id">The id of the record</param>
    /// <param name="nm">The name type</param>
    let getName id nm =
        match
            Zindex.BST020T.records ()
            |> Array.tryFind (fun r ->
                r.MUTKOD <> 1 &&
                r.NMNR = id
            ) with
        | Some r ->
            match nm with
            | Full  -> r.NMNAAM
            | Short  -> r.NMNM40
            | Memo  -> r.NMMEMO
            | Label -> r.NMETIK
        | None -> ""


    /// <summary>
    /// Get the name of a record in BST902T
    /// </summary>
    /// <param name="id">The id of the record</param>
    /// <param name="it">The item type</param>
    /// <param name="ln">The name length</param>
    let getThes id it ln =
        match
            Zindex.BST902T.records ()
            |> Array.tryFind (fun r ->
                r.MUTKOD <> 1 &&
                r.TSITNR = id &&
                it |> mapItem = r.TSNR
            ) with
        | Some r -> match ln with | TwentyFive -> r.THNM25 | Fifty -> r.THNM50
        | None -> ""


    /// <summary>
    /// Get all the name lengths for a record in BST020T
    /// </summary>
    /// <param name="itm">The item type</param>
    /// <param name="ln">The name length</param>
    let getItems itm ln =
            Zindex.BST902T.records()
            |> Array.filter (fun r ->
                itm |> mapItem = r.TSNR
            )
            |> Array.map (fun r ->
                r.TSITNR,
                match ln with | TwentyFive -> r.THNM25 | Fifty -> r.THNM50
            )
            |> Array.distinct
            |> Array.sort


    /// All the route names in the thesaurus.
    let getRoutes =
        fun () ->
            getItems Route TwentyFive
            |> Array.collect (
                snd
                >> String.splitAt ','
                >> Array.map String.trim
            )
            |> Array.distinct
            |> Array.sort
        |> Memoization.memoize


    /// All the pharmaceutical form names in the thesaurus.
    let getForms =
        fun () ->
            getItems Form Fifty
            |> Array.map snd
        |> Memoization.memoize


    /// All the generic unit names in the thesaurus.
    let getGenericUnits =
        fun () ->
            getItems GenericUnit Fifty
            |> Array.map snd
        |> Memoization.memoize


    /// All the pharmaceutical form unit names in the thesaurus.
    let getFormUnits =
        fun () ->
            getItems FormUnit TwentyFive
            |> Array.map snd
        |> Memoization.memoize


    /// All the prescription container names in the thesaurus.
    let getPrescriptionContainers =
        fun () ->
            getItems PrescriptionContainer TwentyFive
            |> Array.map snd
        |> Memoization.memoize


    /// All the consumer container names in the thesaurus.
    let getConsumerContainers =
        fun () ->
            getItems ConsumerContainer TwentyFive
            |> Array.map snd
        |> Memoization.memoize