namespace Informedica.Utils.Lib


/// Reads the sheets of an xlsx workbook as rows of text.
module Xlsx =

    open System
    open System.Globalization
    open System.IO
    open System.IO.Compression
    open System.Xml.Linq
    open Informedica.Utils.Lib.BCL


    /// The namespace of the worksheet, workbook and shared strings elements.
    let mainNs = XNamespace.Get "http://schemas.openxmlformats.org/spreadsheetml/2006/main"


    /// The namespace of the relationship id attribute on a sheet element of the workbook.
    let docRelNs = XNamespace.Get "http://schemas.openxmlformats.org/officeDocument/2006/relationships"


    /// The namespace of the elements in a relationships part.
    let packageRelNs = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/relationships"


    /// Returns the zero-based column index of a cell reference: A1 gives 0, AB12 gives 27.
    let columnIndex (cellRef: string) =
        cellRef
        |> Seq.takeWhile Char.IsLetter
        |> Seq.fold (fun acc c -> acc * 26 + int (Char.ToUpperInvariant c) - int 'A' + 1) 0
        |> fun n -> n - 1


    /// Returns a stored number as a plain decimal with at most 15 significant digits, the precision of a
    /// spreadsheet number: 30.0 gives 30, 2.4E7 gives 24000000, 9.129E-5 gives 0.00009129, and the floating
    /// point result 0.16699999999999998 gives 0.167. Text that is not a number is returned unchanged.
    let numberText (s: string) =
        match s |> Double.tryParse with
        | Some d ->
            let digits = d.ToString("G15", CultureInfo.InvariantCulture)

            // A decimal is written without an exponent. A number beyond the range of a decimal keeps the
            // exponent.
            digits
            |> Decimal.tryParse
            |> Option.map _.ToString(CultureInfo.InvariantCulture)
            |> Option.defaultValue digits
        | None -> s


    /// Joins the text elements of a string item, leaving out the phonetic hints.
    let itemText (item: XElement) =
        item.Descendants(mainNs + "t")
        |> Seq.filter (fun t -> t.Parent.Name <> mainNs + "rPh")
        |> Seq.map _.Value
        |> String.concat ""


    /// Returns the text of a cell. A shared or inline string is the string, a boolean is TRUE or FALSE, a
    /// number is written by numberText, and an error cell or a cell without a value is empty.
    let cellText (shared: string[]) (cell: XElement) =
        let value = cell.Element(mainNs + "v")

        let cellType =
            match cell.Attribute "t" with
            | null -> ""
            | t -> t.Value

        match cellType, value with
        | "inlineStr", _ ->
            match cell.Element(mainNs + "is") with
            | null -> ""
            | item -> itemText item
        | _, null
        | "e", _ -> ""
        | "s", v -> shared[int v.Value]
        | "b", v -> if v.Value = "1" then "TRUE" else "FALSE"
        | "str", v -> v.Value
        | _, v -> numberText v.Value


    /// Reads the rows of one worksheet, the header row first, every row as wide as the widest row. Rows marked
    /// hidden and rows without any text are left out, and the cells are trimmed.
    let readSheet shared (doc: XDocument) =
        let isHidden (row: XElement) =
            match row.Attribute "hidden" with
            | null -> false
            | a -> a.Value = "1" || a.Value = "true"

        let cells (row: XElement) =
            row.Elements(mainNs + "c")
            |> Seq.mapi (fun i c ->
                // A cell without a reference is in the column of its place in the row.
                let index =
                    match c.Attribute "r" with
                    | null -> i
                    | r -> columnIndex r.Value

                index, (cellText shared c).Trim()
            )
            |> Seq.toArray

        let visible =
            doc.Descendants(mainNs + "row")
            |> Seq.toArray
            |> Array.filter (isHidden >> not)
            |> Array.map cells
            |> Array.filter (Array.exists (snd >> String.notEmpty))

        let width = visible |> Array.collect (Array.map (fst >> (+) 1)) |> Array.fold max 0

        visible
        |> Array.map (fun cs ->
            let row = Array.create width ""
            cs |> Array.iter (fun (i, s) -> row[i] <- s)
            row
        )


    /// Returns the folder of an entry name: xl/workbook.xml gives xl, a name without a folder gives the empty text.
    let folderOf (path: string) =
        match path.LastIndexOf '/' with
        | -1 -> ""
        | i -> path[.. i - 1]


    /// Returns the entry name of the relationships of a part: xl/workbook.xml gives xl/_rels/workbook.xml.rels,
    /// and the package itself, the empty text, gives _rels/.rels.
    let relationshipsPath (part: string) =
        match part.LastIndexOf '/' with
        | -1 -> $"_rels/%s{part}.rels"
        | i -> $"%s{part[.. i - 1]}/_rels/%s{part[i + 1 ..]}.rels"


    /// Resolves the target of a relationship against the folder of the part that has the relationship. A target
    /// that starts with a slash starts at the package root. Returns the entry name without a leading slash.
    let resolveTarget (folder: string) (target: string) =
        let segments (s: string) =
            s.Split('/', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

        let start = if target.StartsWith "/" then [] else segments folder

        target
        |> segments
        |> List.fold
            (fun acc segment ->
                match segment with
                | "." -> acc
                | ".." -> acc |> List.truncate (acc.Length - 1)
                | s -> acc @ [ s ]
            )
            start
        |> String.concat "/"


    /// Reads the relationships of a part from its relationships document as (id, type, entry name) triples.
    let relationships part (doc: XDocument) =
        doc.Root.Elements(packageRelNs + "Relationship")
        |> Seq.map (fun r ->
            r.Attribute("Id").Value,
            r.Attribute("Type").Value,
            r.Attribute("Target").Value |> resolveTarget (folderOf part)
        )
        |> Seq.toList


    /// Reads every sheet of an xlsx workbook, by sheet name. The workbook is the office document that the
    /// package relationships in _rels/.rels name. Returns an error when the bytes are not an xlsx workbook or
    /// the workbook cannot be read.
    let read (bytes: byte[]) =
        try
            use zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read)

            let load (path: string) =
                match zip.GetEntry path with
                | null -> None
                | entry ->
                    use s = entry.Open()
                    XDocument.Load s |> Some

            let relationshipsOf part =
                load (relationshipsPath part)
                |> Option.map (relationships part)
                |> Option.defaultValue []

            // The type of a relationship is a uri; its last segment names the kind of part.
            let ofType (kind: string) =
                List.tryPick (fun (_, relType: string, path) ->
                    if relType.EndsWith $"/%s{kind}" then Some path else None
                )

            match relationshipsOf "" |> ofType "officeDocument" with
            | None -> Error "The bytes are not an xlsx workbook: _rels/.rels names no office document."
            | Some workbookPath ->
                match load workbookPath with
                | None -> Error $"The bytes are not an xlsx workbook: the office document %s{workbookPath} is missing."
                | Some workbook ->
                    let workbookRelationships = relationshipsOf workbookPath

                    let shared =
                        workbookRelationships
                        |> ofType "sharedStrings"
                        |> Option.bind load
                        |> Option.map (fun doc -> doc.Root.Elements(mainNs + "si") |> Seq.map itemText |> Seq.toArray)
                        |> Option.defaultValue [||]

                    let targets = workbookRelationships |> List.map (fun (id, _, path) -> id, path) |> Map.ofList

                    // A sheet whose relationship or part is missing is left out, so asking for it by name fails.
                    workbook.Descendants(mainNs + "sheet")
                    |> Seq.choose (fun s ->
                        targets
                        |> Map.tryFind (s.Attribute(docRelNs + "id").Value)
                        |> Option.bind load
                        |> Option.map (fun doc -> s.Attribute("name").Value, readSheet shared doc)
                    )
                    |> Map.ofSeq
                    |> Ok
        with
        | :? InvalidDataException as e -> Error $"The bytes are not an xlsx workbook: %s{e.Message}"
        | e -> Error $"The xlsx workbook could not be read.\nException: %s{e.GetType().Name}: %s{e.Message}"
