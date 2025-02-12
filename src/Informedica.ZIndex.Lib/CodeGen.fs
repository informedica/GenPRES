namespace Informedica.ZIndex.Lib


/// Contains the code that generates the
/// ZIndex file containing all the base tables
///
/// Use the code like:
/// CodeGen.generateZIndex (CodeGen.tableList)
///|> printfn "%s"
module CodeGen =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    [<Literal>]
    let codeString = """

    {comment}
    module {n} =

        let name = "{n}"

        {record}

        {create}

        let posl = BST001T.getPosl name

        let pickList = {pickList}

        let _records _ =
            Parser.getData name posl pickList
            |> Array.map {map}

        let records : unit -> {n} [] = Memoization.memoize _records

    """

    [<Literal>]
    let zindexCode = """
namespace Informedica.ZIndex.Lib

{comment}
module Zindex =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib

    {code}

    """


    /// <summary>
    /// Generates the code for a single table.
    /// </summary>
    /// <param name="n">The name of the table.</param>
    /// <param name="pl">The picklist of the table.</param>
    let generate n pl =
        let cm = BST000T.commentString n pl
        let rc = BST001T.recordString n pl
        let cr = BST001T.createString n pl
        let mp =
            let pl = if pl = [] then [0..(BST001T.columnCount n - 1)] else [0..((pl |> List.length) - 1)]
            let args =
                pl
                |> List.fold (fun s p -> s + " (xs |> Array.item " + string p + ")") ""
            "(fun xs -> create {args})"
            |> String.replace "{args}" args

        codeString
        |> String.replace "{n}" n
        |> String.replace "{comment}" cm
        |> String.replace "{pickList}" (pl |> List.toString)
        |> String.replace "{record}" rc
        |> String.replace "{create}" cr
        |> String.replace "{map}" mp


    /// <summary>
    /// Table list of Z-Index files.
    /// </summary>
    /// <remarks>
    /// Files that no longer (will) exist:
    /// <list type="bullet">
    /// <item>BST025T</item>
    /// <item>BST050T</item>
    /// <item>BST051T</item>
    /// <item>BST059T</item>
    /// <item>BST681T</item>
    /// </list>
    /// </remarks>
    let tableList =
        [
            ("BST004T", [1;2;3;4;11;12])
            ("BST020T", [1..6])
            ("BST031T", [1;2;3;5;6;7;10;18;19;20;21;22;30;38])
            ("BST052T", [1;2;3;4;7;8;9;10;11])
            ("BST200T", [1;2;3;4;5])
            ("BST360T", [1;2;4])
            ("BST380T", [1;2;4])
            ("BST640T", [1;2;6;8])
            ("BST641T", [1..7])
            ("BST642T", [1;2;3;5;6;8;11;12;13])
            ("BST643T", [1..13])
            ("BST649T", [1..14])
            ("BST701T", [1..11])
            ("BST711T", [1;2;3;5;7;8;11;12;17;20;22])
            ("BST715T", [1..7])
            ("BST720T", [1..3])
            ("BST725T", [1..4])
            ("BST750T", [1..15])
            ("BST760T", [1;2;5])
            ("BST801T", [1..5])
            ("BST902T", [1;2;3;7;8])
            ("BST922T", [1..9])
        ]


    /// <summary>
    /// Generates the Z-Index file.
    /// </summary>
    /// <param name="tl">The table list.</param>
    let generateZIndex tl =
        let code =
            tl
            |> List.fold (fun s (tb, pl) ->
                    s + "\n" + generate tb pl
                ) ""

        let comment =
            let s = "/// <summary>\n"

            (tl
            |> List.fold (fun s (n, _) ->
                let t = BST000T.table n
                s + "/// <para>" + t.MDBST + ": " + t.MDOBST + "</para>\n"
            ) s) + "/// </summary>"

        zindexCode
        |> String.replace "{code}" code
        |> String.replace "{comment}" comment



