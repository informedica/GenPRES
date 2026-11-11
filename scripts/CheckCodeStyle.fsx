// Check Code Style
//
// Issue #1665: static analysis for the coding rules that Fantomas does not enforce and that
// were checked only by review. This part covers the layout and comment rules of
// .github/instructions/fsharp-coding.instructions.md:
//
//   PARSE-001    the file parses without errors under every combination of its #if symbols
//   BLANK-001    exactly two blank lines between the declarations of a namespace or module
//   DOC-001      a /// comment with text on every public module, type, function, value,
//                member, constructor, property, field, union case, enum case and exception
//   DOC-002      a plain /// block contains no XML tag
//   DOC-003      no backtick and no <c> in a plain /// block
//   DOC-004      an XML /// block is well-formed and has no prose outside its elements
//   COMMENT-001  no (* ... *) comment
//   COMMENT-002  no comment line over 120 columns
//
// Not covered: the param names of an XML block (compiler warning FS3390, on in the libraries
// checked here), and the text rules (DOC-002 to DOC-004, COMMENT-002) can fire on a line inside
// a triple-quoted string that looks like a comment.
//
// The rules read the untyped syntax tree, so no build is needed. The parser is Fantomas.FCS,
// the copy of FSharp.Compiler.Service that Fantomas ships under its own assembly name: the
// FSharp.Compiler.Service package binds to the compiler that dotnet fsi itself loads, whatever
// version the reference names.
//
// Findings already present are listed in CheckCodeStyle.baseline.tsv, keyed by rule, file and
// symbol rather than line, so an edit elsewhere in the file does not move them. The baseline
// is a ratchet: a finding not in it fails the run, and so does an entry that no longer
// matches. Parse errors are never baselined.
//
// Run with: dotnet fsi scripts/CheckCodeStyle.fsx                    check against the baseline
//           dotnet fsi scripts/CheckCodeStyle.fsx --test             run the rule tests only
//           dotnet fsi scripts/CheckCodeStyle.fsx --report           list every finding
//           dotnet fsi scripts/CheckCodeStyle.fsx --prune-baseline   delete the entries that match no finding
//           dotnet fsi scripts/CheckCodeStyle.fsx --write-baseline   write the first baseline; refuses when one exists

#r "nuget: Expecto, 10.2.3"
// Must equal the Fantomas version in .config/dotnet-tools.json; a test checks it.
#r "nuget: Fantomas.FCS, 8.0.0"

open System
open System.IO
open System.Text.RegularExpressions
open Expecto
open Expecto.Flip
open Fantomas.FCS.Syntax
open Fantomas.FCS.SyntaxTrivia
open Fantomas.FCS.Text
open Fantomas.FCS.Xml


/// A rule violation in a file. The key names the symbol or text the finding is about and,
/// with the rule and the file, identifies the finding in the baseline.
type Finding =
    {
        File: string
        Line: int
        Rule: string
        Key: string
        Message: string
    }


let maxCommentWidth = 120


let fantomasFcsVersion = "8.0.0"


let startsWith (prefix: string) (s: string) =
    s.StartsWith(prefix, StringComparison.Ordinal)


let finding file line rule key message =
    {
        File = file
        Line = line
        Rule = rule
        Key = key
        Message = message
    }


let qualify path (name: string) =
    path @ [ name ] |> String.concat "."


// ---------------------------------------------------------------------------
// Parsing, once for every combination of the #if symbols in a file.
// ---------------------------------------------------------------------------

let rec directiveSymbols expr =
    match expr with
    | IfDirectiveExpression.Ident s -> [ s ]
    | IfDirectiveExpression.Not e -> directiveSymbols e
    | IfDirectiveExpression.And(a, b)
    | IfDirectiveExpression.Or(a, b) -> directiveSymbols a @ directiveSymbols b


let rec subsets xs =
    match xs with
    | [] -> [ [] ]
    | x :: rest ->
        let r = subsets rest
        r @ (r |> List.map (fun s -> x :: s))


let maxSymbols = 4


let parseWith defines source =
    Fantomas.FCS.Parse.parseFile false (SourceText.ofString source) defines


let symbolsOf tree =
    match tree with
    | ParsedInput.ImplFile(ParsedImplFileInput(trivia = trivia)) ->
        trivia.ConditionalDirectives
        |> List.collect (fun d ->
            match d with
            | ConditionalDirectiveTrivia.If(expr, _) -> directiveSymbols expr
            | _ -> []
        )
        |> List.distinct
    | ParsedInput.SigFile _ -> []


/// The trees of a source under every combination of its #if symbols, and the parse errors.
let parse file source =
    let first, _ = parseWith [] source
    let symbols = symbolsOf first

    if List.length symbols > maxSymbols then
        [],
        [
            finding file 1 "PARSE-001" "" $"%i{List.length symbols} #if symbols, at most %i{maxSymbols} are checked"
        ]
    else
        let parsed = subsets symbols |> List.map (fun defines -> parseWith defines source)

        let errors =
            parsed
            |> List.collect snd
            |> List.filter (fun d -> d.Severity = Fantomas.FCS.Diagnostics.FSharpDiagnosticSeverity.Error)
            |> List.map (fun d ->
                let line = d.Range |> Option.map _.StartLine |> Option.defaultValue 1
                finding file line "PARSE-001" "" $"parse error: %s{d.Message}"
            )
            |> List.distinct

        List.map fst parsed, errors


// ---------------------------------------------------------------------------
// BLANK-001
// ---------------------------------------------------------------------------

/// Whether a declaration takes part in the blank-line rule; opens, hash directives and
/// module abbreviations are grouped by convention and are left out.
let isSpaced decl =
    match decl with
    | SynModuleDecl.Open _
    | SynModuleDecl.HashDirective _
    | SynModuleDecl.ModuleAbbrev _ -> false
    | _ -> true


/// The number of blank lines above the comment, attribute, doc and #if lines that lead a
/// declaration starting at startLine, not counting lines at or above prevEndLine.
let blankLinesAbove (lines: string[]) prevEndLine startLine =
    let isBlank i = String.IsNullOrWhiteSpace lines[i - 1]

    let isLead i =
        let t = lines[i - 1].TrimStart()
        startsWith "//" t || startsWith "[<" t || startsWith "#" t

    let mutable i = startLine - 1

    while i > prevEndLine && not (isBlank i) && isLead i do
        i <- i - 1

    let mutable n = 0

    while i > prevEndLine && isBlank i do
        n <- n + 1
        i <- i - 1

    n


/// The name of what a binding pattern binds, if any: a tuple binds several.
let rec bindingNames pat =
    match pat with
    | SynPat.Named(ident = SynIdent(id, _); accessibility = access) -> [ id, access ]
    | SynPat.LongIdent(longDotId = SynLongIdent(id = ids); accessibility = access) ->
        ids |> List.tryLast |> Option.map (fun id -> id, access) |> Option.toList
    | SynPat.Tuple(elementPats = ps) -> ps |> List.collect bindingNames
    | SynPat.Paren(pat = p)
    | SynPat.Typed(pat = p)
    | SynPat.Attrib(pat = p) -> bindingNames p
    | _ -> []


/// A name for a declaration that does not depend on its line: the symbol it declares, or else
/// its first line of text.
let declName (lines: string[]) decl =
    let names =
        match decl with
        | SynModuleDecl.Let(bindings = bs) ->
            bs
            |> List.collect (fun (SynBinding(headPat = p)) -> bindingNames p)
            |> List.map (fun (id, _) -> id.idText)
        | SynModuleDecl.Types(typeDefns = ts) -> ts |> List.map (fun (SynTypeDefn(typeInfo = info)) -> (List.last info.LongIdent).idText)
        | SynModuleDecl.NestedModule(moduleInfo = info) -> [ (List.last info.LongIdent).idText ]
        | SynModuleDecl.Exception(exnDefn = SynExceptionDefn(exnRepr = SynExceptionDefnRepr(caseName = SynUnionCase(ident = SynIdent(id, _))))) ->
            [ id.idText ]
        | _ -> []

    match names with
    | [] -> lines[decl.Range.StartLine - 1].Trim()
    | ns -> String.concat ", " ns


let rec blankLineFindings file lines path (decls: SynModuleDecl list) =
    let pairs =
        decls
        |> List.filter isSpaced
        |> List.pairwise
        |> List.choose (fun (a, b) ->
            let n = blankLinesAbove lines a.Range.EndLine b.Range.StartLine

            if n = 2 then
                None
            else
                Some(
                    finding
                        file
                        b.Range.StartLine
                        "BLANK-001"
                        (qualify path (declName lines b))
                        $"%i{n} blank lines before this declaration, expected 2"
                )
        )

    let nested =
        decls
        |> List.collect (fun d ->
            match d with
            | SynModuleDecl.NestedModule(moduleInfo = info; decls = ds) ->
                blankLineFindings file lines (path @ [ (List.last info.LongIdent).idText ]) ds
            | _ -> []
        )

    pairs @ nested


// ---------------------------------------------------------------------------
// DOC-001
// ---------------------------------------------------------------------------

let isHidden access =
    match access with
    | Some(SynAccess.Private _)
    | Some(SynAccess.Internal _) -> true
    | _ -> false


let isHiddenSig access =
    match access with
    | SynValSigAccess.Single a
    | SynValSigAccess.GetSet(a, _, _) -> isHidden a


let tagPattern = Regex(@"<[^>]*>")


/// Whether a doc comment has text once its XML tags are removed: an empty summary is no doc.
let hasDoc (doc: PreXmlDoc) =
    let text = doc.ToXmlDoc(false, None).UnprocessedLines |> String.concat " "
    not (String.IsNullOrWhiteSpace(tagPattern.Replace(text, "")))


let missing file path (id: Ident) what =
    finding file id.idRange.StartLine "DOC-001" $"%s{what} %s{qualify path id.idText}" $"no /// comment on %s{what} %s{id.idText}"


let bindingFindings file path what binding =
    match binding with
    | SynBinding(accessibility = access; xmlDoc = doc; headPat = pat; valData = SynValData(memberFlags = flags); trivia = trivia) ->
        // An override or default implements a slot documented where it is declared.
        let isImpl =
            match trivia.LeadingKeyword with
            | SynLeadingKeyword.Override _
            | SynLeadingKeyword.Default _ -> true
            | _ -> flags |> Option.exists _.IsOverrideOrExplicitImpl

        if isHidden access || isImpl || hasDoc doc then
            []
        else
            bindingNames pat
            |> List.filter (fun (id, patAccess) -> not (isHidden patAccess || startsWith "_" id.idText))
            |> List.map (fun (id, _) -> missing file path id (if id.idText = "new" then "constructor" else what))


let fieldFindings file path what fields =
    fields
    |> List.choose (fun field ->
        match field with
        | SynField(idOpt = Some id; xmlDoc = doc; accessibility = access) when not (isHidden access || hasDoc doc) ->
            Some(missing file path id what)
        | _ -> None
    )


let memberFindings file path (members: SynMemberDefn list) =
    members
    |> List.collect (fun m ->
        match m with
        | SynMemberDefn.Member(memberDefn = b) -> bindingFindings file path "member" b
        | SynMemberDefn.GetSetMember(memberDefnForGet = get; memberDefnForSet = set) ->
            match get, set with
            | Some b, _
            | None, Some b -> bindingFindings file path "member" b
            | None, None -> []
        | SynMemberDefn.AbstractSlot(slotSig = SynValSig(ident = SynIdent(id, _); xmlDoc = doc)) when not (hasDoc doc) ->
            [ missing file path id "abstract member" ]
        | SynMemberDefn.AutoProperty(ident = id; xmlDoc = doc; accessibility = access) when
            not (isHiddenSig access || hasDoc doc)
            ->
            [ missing file path id "property" ]
        | SynMemberDefn.ValField(fieldInfo = f) -> fieldFindings file path "field" [ f ]
        | _ -> []
    )


let typeFindings file path typeDefn =
    match typeDefn with
    | SynTypeDefn(typeInfo = SynComponentInfo(xmlDoc = doc; accessibility = access) as info; typeRepr = repr; members = extra) ->
        let id = List.last info.LongIdent

        if isHidden access then
            []
        else
            let self = if hasDoc doc then [] else [ missing file path id "type" ]
            let inner = path @ [ id.idText ]

            let reprFindings =
                match repr with
                | SynTypeDefnRepr.Simple(simpleRepr = SynTypeDefnSimpleRepr.Record(accessibility = ra; recordFieldsAndSpreads = fs)) when
                    not (isHidden ra)
                    ->
                    fs
                    |> List.choose (fun f ->
                        match f with
                        | SynFieldOrSpread.Field field -> Some field
                        | SynFieldOrSpread.Spread _ -> None
                    )
                    |> fieldFindings file inner "record field"
                | SynTypeDefnRepr.Simple(simpleRepr = SynTypeDefnSimpleRepr.Union(accessibility = ua; unionCases = cs)) when
                    not (isHidden ua)
                    ->
                    cs
                    |> List.choose (fun (SynUnionCase(ident = SynIdent(cid, _); xmlDoc = cdoc)) ->
                        if hasDoc cdoc then None else Some(missing file inner cid "union case")
                    )
                | SynTypeDefnRepr.Simple(simpleRepr = SynTypeDefnSimpleRepr.Enum(cases = cs)) ->
                    cs
                    |> List.choose (fun (SynEnumCase(ident = SynIdent(cid, _); xmlDoc = cdoc)) ->
                        if hasDoc cdoc then None else Some(missing file inner cid "enum case")
                    )
                // The Invoke slot of a delegate is the delegate type itself.
                | SynTypeDefnRepr.ObjectModel(kind = SynTypeDefnKind.Delegate _) -> []
                | SynTypeDefnRepr.ObjectModel(members = ms) -> memberFindings file inner ms
                | _ -> []

            self @ reprFindings @ memberFindings file inner extra


let rec docFindings file path (decls: SynModuleDecl list) =
    decls
    |> List.collect (fun d ->
        match d with
        | SynModuleDecl.NestedModule(moduleInfo = SynComponentInfo(xmlDoc = doc; accessibility = access) as info; decls = ds) ->
            let id = List.last info.LongIdent

            if isHidden access then
                []
            else
                let self = if hasDoc doc then [] else [ missing file path id "module" ]
                self @ docFindings file (path @ [ id.idText ]) ds
        | SynModuleDecl.Let(bindings = bs) -> bs |> List.collect (bindingFindings file path "value or function")
        | SynModuleDecl.Types(typeDefns = ts) -> ts |> List.collect (typeFindings file path)
        | SynModuleDecl.Exception(
            exnDefn = SynExceptionDefn(exnRepr = SynExceptionDefnRepr(caseName = c; xmlDoc = doc; accessibility = access); members = ms)) ->
            match c with
            | SynUnionCase(ident = SynIdent(id, _)) when not (isHidden access) ->
                let self = if hasDoc doc then [] else [ missing file path id "exception" ]
                self @ memberFindings file (path @ [ id.idText ]) ms
            | _ -> []
        | _ -> []
    )


/// DOC-001 for the top-level modules and namespaces of a file. A namespace has no doc comment
/// of its own; a named top-level module does.
let topLevelDocFindings file (contents: SynModuleOrNamespace list) =
    contents
    |> List.collect (fun (SynModuleOrNamespace(longId = ids; kind = kind; decls = ds; xmlDoc = doc; accessibility = access)) ->
        let path = ids |> List.map _.idText

        let self =
            match kind with
            | SynModuleOrNamespaceKind.NamedModule when not (isHidden access || hasDoc doc) ->
                [ missing file (List.take (List.length ids - 1) path) (List.last ids) "module" ]
            | _ -> []

        if isHidden access then [] else self @ docFindings file path ds
    )


// ---------------------------------------------------------------------------
// DOC-002 to DOC-004, read from the text: a /// block is a run of consecutive /// lines.
// ---------------------------------------------------------------------------

/// An XML start, end or empty tag. A < right after a letter, digit, underscore or quote opens
/// a generic argument list, as in Result<T, E>, and is not a tag.
let xmlTag =
    Regex(@"(?<![\w'])</?([A-Za-z][A-Za-z0-9]*)(\s+[A-Za-z]+\s*=\s*(""[^""]*""|'[^']*'))*\s*/?>")


let tagNames (text: string) =
    xmlTag.Matches text
    |> Seq.map (fun m -> m.Groups[1].Value)
    |> Seq.distinct
    |> Seq.toList


/// The /// blocks of a file as (first line number, text lines without the ///).
let docBlocks (lines: string[]) =
    let isDoc (l: string) =
        let t = l.TrimStart()
        startsWith "///" t && not (startsWith "////" t)

    let body (l: string) = l.TrimStart().Substring(3).TrimStart()

    lines
    |> Array.indexed
    |> Array.fold
        (fun (blocks, current) (i, l) ->
            match isDoc l, current with
            | true, Some(start, ls) -> blocks, Some(start, ls @ [ body l ])
            | true, None -> blocks, Some(i + 1, [ body l ])
            | false, Some b -> blocks @ [ b ], None
            | false, None -> blocks, None
        )
        ([], None)
    |> fun (blocks, current) -> blocks @ Option.toList current


/// What is wrong with an XML block, if anything: malformed XML, or prose outside its elements.
let xmlBlockProblem (text: string) =
    try
        let root = Xml.Linq.XElement.Parse($"<doc>%s{text}</doc>", Xml.Linq.LoadOptions.None)

        root.Nodes()
        |> Seq.exists (fun n ->
            match n with
            | :? Xml.Linq.XText as t -> not (String.IsNullOrWhiteSpace t.Value)
            | _ -> false
        )
        |> function
            | true -> Some "prose outside the XML elements of the block"
            | false -> None
    with :? Xml.XmlException as e ->
        Some $"malformed XML: %s{e.Message}"


let docStyleFindings file lines =
    docBlocks lines
    |> List.collect (fun (start, ls) ->
        let text = String.Join("\n", ls)
        let key = List.head ls

        if startsWith "<" (text.TrimStart()) then
            xmlBlockProblem text
            |> Option.map (finding file start "DOC-004" key)
            |> Option.toList
        else
            let tags = tagNames text

            let mixed =
                match tags |> List.filter (fun t -> t <> "c") with
                | [] -> []
                | ts -> [ finding file start "DOC-002" key $"""plain /// block uses XML: %s{String.concat ", " ts}""" ]

            let markup =
                if text.Contains "`" || List.contains "c" tags then
                    [ finding file start "DOC-003" key "backtick or <c> in a plain /// block" ]
                else
                    []

            mixed @ markup
    )


// ---------------------------------------------------------------------------
// COMMENT-001 and COMMENT-002, read from the comment trivia and the text.
// ---------------------------------------------------------------------------

let commentFindings file (lines: string[]) (comments: CommentTrivia list) =
    let block =
        comments
        |> List.choose (fun c ->
            match c with
            | CommentTrivia.BlockComment r ->
                Some(finding file r.StartLine "COMMENT-001" (lines[r.StartLine - 1].Trim()) "(* ... *) comment")
            | CommentTrivia.LineComment _ -> None
        )

    let trailing =
        comments
        |> List.choose (fun c ->
            match c with
            | CommentTrivia.LineComment r -> Some r.StartLine
            | CommentTrivia.BlockComment _ -> None
        )

    let commentLines =
        lines
        |> Array.indexed
        |> Array.choose (fun (i, l) -> if startsWith "//" (l.TrimStart()) then Some(i + 1) else None)
        |> Array.toList

    let wide =
        trailing @ commentLines
        |> List.distinct
        |> List.sort
        |> List.filter (fun n -> lines[n - 1].TrimEnd().Length > maxCommentWidth)
        |> List.map (fun n ->
            let line = lines[n - 1].TrimEnd()
            finding file n "COMMENT-002" (line.Trim()) $"comment line of %i{line.Length} columns, at most %i{maxCommentWidth}"
        )

    block @ wide


// ---------------------------------------------------------------------------
// All rules over one file.
// ---------------------------------------------------------------------------

let treeFindings file lines tree =
    match tree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = contents; trivia = trivia)) ->
        let blank =
            contents
            |> List.collect (fun (SynModuleOrNamespace(longId = ids; decls = ds)) ->
                blankLineFindings file lines (ids |> List.map _.idText) ds
            )

        blank @ topLevelDocFindings file contents @ commentFindings file lines trivia.CodeComments
    | ParsedInput.SigFile _ -> []


let checkSource file (source: string) =
    let source = source.Replace("\r\n", "\n")
    let lines = source.Split '\n'
    let trees, errors = parse file source

    // A finding under several define sets is one finding.
    let fromTrees =
        trees
        |> List.collect (treeFindings file lines)
        |> List.distinctBy (fun f -> f.Rule, f.Line, f.Key)

    errors @ fromTrees @ docStyleFindings file lines
    |> List.sortBy (fun f -> f.Line, f.Rule, f.Key)


// ---------------------------------------------------------------------------
// The libraries under the process. Others are added as they follow.
// ---------------------------------------------------------------------------

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))


let baselinePath = Path.Combine(__SOURCE_DIRECTORY__, "CheckCodeStyle.baseline.tsv")


let projects =
    [
        "src/Informedica.GenUNITS.Lib/Informedica.GenUNITS.Lib.fsproj"
        "src/Informedica.GenSOLVER.Lib/Informedica.GenSOLVER.Lib.fsproj"
        "src/Informedica.GenORDER.Lib/Informedica.GenORDER.Lib.fsproj"
    ]


/// The compiled .fs files of a project, relative to the repository root.
let sourceFiles (project: string) =
    let dir = Path.GetDirectoryName project
    let doc = Xml.Linq.XDocument.Load(Path.Combine(root, project))

    doc.Descendants()
    |> Seq.filter (fun e -> e.Name.LocalName = "Compile")
    |> Seq.map (fun e -> e.Attribute(Xml.Linq.XName.Get "Include").Value)
    |> Seq.filter (fun f -> f.EndsWith(".fs", StringComparison.Ordinal))
    |> Seq.map (fun f -> Path.Combine(dir, f).Replace('\\', '/'))
    |> Seq.toList


let checkRepository () =
    projects
    |> List.collect sourceFiles
    |> List.collect (fun f -> checkSource f (File.ReadAllText(Path.Combine(root, f))))


// ---------------------------------------------------------------------------
// The baseline: one line per finding, rule, file and key separated by tabs.
// ---------------------------------------------------------------------------

let entry f =
    $"%s{f.Rule}\t%s{f.File}\t%s{f.Key.Replace('\t', ' ')}"


let isBaselinable f = f.Rule <> "PARSE-001"


let readBaseline path =
    if File.Exists path then
        File.ReadAllLines path
        |> Array.filter (fun l -> not (String.IsNullOrWhiteSpace l || startsWith "#" l))
        |> Array.toList
    else
        []


/// Compares findings with baseline entries as multisets, since a key can occur twice in a file.
/// Returns the findings not in the baseline and the entries that match no finding.
let compareBaseline (baseline: string list) (findings: Finding list) =
    let counts xs =
        xs |> List.countBy id |> Map.ofList

    let known = counts baseline

    let current =
        findings |> List.filter isBaselinable |> List.map entry |> counts

    let excess (a: Map<string, int>) (b: Map<string, int>) =
        a
        |> Map.toList
        |> List.choose (fun (k, n) ->
            let m = b |> Map.tryFind k |> Option.defaultValue 0
            if n > m then Some(k, n - m) else None
        )
        |> Map.ofList

    let added = excess current known
    let stale = excess known current

    // Report every finding under a key that has more findings than baseline entries.
    let fresh =
        findings
        |> List.filter (fun f -> not (isBaselinable f) || added.ContainsKey(entry f))

    fresh, stale |> Map.toList |> List.collect (fun (k, n) -> List.replicate n k)


let baselineHeader =
    [
        "# Findings of scripts/CheckCodeStyle.fsx present when the check was adopted (issue #1665)."
        "# Rule, file and key, tab separated. Fix a finding and run --prune-baseline to delete its line."
        "# Never add a line: a new finding is fixed, not baselined."
    ]


let writeBaseline path entries =
    let lines = List.sort entries
    File.WriteAllLines(path, baselineHeader @ lines)
    List.length lines


/// The baseline entries that still match a finding, each as often as both list it.
let prune (baseline: string list) (findings: Finding list) =
    let current =
        findings |> List.filter isBaselinable |> List.map entry |> List.countBy id |> Map.ofList

    baseline
    |> List.countBy id
    |> List.collect (fun (k, n) ->
        let m = current |> Map.tryFind k |> Option.defaultValue 0
        List.replicate (min n m) k
    )


// ---------------------------------------------------------------------------
// Tests of the rules on small sources.
// ---------------------------------------------------------------------------

let rules source =
    checkSource "Test.fs" source |> List.map (fun f -> f.Rule, f.Line)


/// The findings of one rule only, for tests of a rule on sources that break others.
let only rule source =
    rules source |> List.filter (fun (r, _) -> r = rule)


let keys rule source =
    checkSource "Test.fs" source
    |> List.filter (fun f -> f.Rule = rule)
    |> List.map _.Key


let src (lines: string list) = String.Join("\n", lines)


let tests =
    testList
        "CheckCodeStyle"
        [
            testList
                "PARSE-001"
                [
                    test "a parse error is a finding" {
                        src [ "/// M"; "module M"; "/// a"; "let a = ("; "" ]
                        |> only "PARSE-001"
                        |> Expect.isNonEmpty "parse error found"
                    }

                    test "code under #if is checked" {
                        src [ "/// M"; "module M"; "#if DEBUG"; "let a = 1"; "#endif" ]
                        |> keys "DOC-001"
                        |> Expect.equal "a found" [ "value or function M.a" ]
                    }

                    test "code under #else of a negated symbol is checked" {
                        src [ "/// M"; "module M"; "#if !FABLE"; "/// a"; "let a = 1"; "#else"; "let b = 1"; "#endif" ]
                        |> keys "DOC-001"
                        |> Expect.equal "b found" [ "value or function M.b" ]
                    }

                    test "a block comment under #if is found" {
                        src [ "/// M"; "module M"; "#if DEBUG"; "(* c *)"; "#endif"; "/// a"; "let a = 1" ]
                        |> only "COMMENT-001"
                        |> Expect.equal "found" [ "COMMENT-001", 4 ]
                    }
                ]

            testList
                "BLANK-001"
                [
                    test "two blank lines between functions pass" {
                        src [ "module M"; ""; "/// a"; "let a = 1"; ""; ""; "/// b"; "let b = 2" ]
                        |> only "BLANK-001"
                        |> Expect.isEmpty "no finding"
                    }

                    test "one blank line between functions fails" {
                        src [ "module M"; ""; "/// a"; "let a = 1"; ""; "/// b"; "let b = 2" ]
                        |> only "BLANK-001"
                        |> Expect.equal "finding on b" [ "BLANK-001", 6 ]
                    }

                    test "three blank lines fail" {
                        src [ "module M"; "/// a"; "let a = 1"; ""; ""; ""; "/// b"; "let b = 2" ]
                        |> only "BLANK-001"
                        |> Expect.equal "finding on b" [ "BLANK-001", 7 ]
                    }

                    test "a leading comment and attribute belong to the declaration" {
                        src
                            [
                                "module M"
                                "/// a"
                                "let a = 1"
                                ""
                                ""
                                "// why"
                                "/// b"
                                "[<Literal>]"
                                "let b = 2"
                            ]
                        |> only "BLANK-001"
                        |> Expect.isEmpty "no finding"
                    }

                    test "a section comment with two blank lines on both sides passes" {
                        src
                            [
                                "module M"
                                "/// a"
                                "let a = 1"
                                ""
                                ""
                                "// ---- section"
                                ""
                                ""
                                "/// b"
                                "let b = 2"
                            ]
                        |> only "BLANK-001"
                        |> Expect.isEmpty "no finding"
                    }

                    test "an #if line leads the declaration it guards" {
                        src [ "module M"; "/// a"; "let a = 1"; ""; ""; "#if DEBUG"; "/// b"; "let b = 2"; "#endif" ]
                        |> only "BLANK-001"
                        |> Expect.isEmpty "no finding"
                    }

                    test "opens are not spaced" {
                        src [ "module M"; "open System"; "open System.IO"; ""; ""; "/// a"; "let a = 1" ]
                        |> only "BLANK-001"
                        |> Expect.isEmpty "no finding"
                    }

                    test "the rule applies inside a nested module and the key names the path" {
                        src [ "module M"; "/// N"; "module N ="; "    /// a"; "    let a = 1"; ""; "    /// b"; "    let b = 2" ]
                        |> keys "BLANK-001"
                        |> Expect.equal "finding on b" [ "M.N.b" ]
                    }
                ]

            testList
                "DOC-001"
                [
                    test "an undocumented module, function and type are found" {
                        src [ "module M"; "let a = 1"; ""; ""; "type T = int" ]
                        |> rules
                        |> Expect.equal "three findings" [ "DOC-001", 1; "DOC-001", 2; "DOC-001", 5 ]
                    }

                    test "private, internal and underscore names are not checked" {
                        src [ "/// M"; "module M"; "let private a = 1"; ""; ""; "let internal b = 1"; ""; ""; "let _c = 1" ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }

                    test "each name of a tuple binding is checked" {
                        src [ "/// M"; "module M"; "let a, b = 1, 2" ]
                        |> keys "DOC-001"
                        |> Expect.equal "a and b" [ "value or function M.a"; "value or function M.b" ]
                    }

                    test "an empty summary is no doc" {
                        src [ "/// M"; "module M"; "/// <summary></summary>"; "let a = 1" ]
                        |> only "DOC-001"
                        |> Expect.equal "a found" [ "DOC-001", 4 ]
                    }

                    test "record fields and union cases are checked" {
                        src
                            [
                                "/// M"
                                "module M"
                                "/// R"
                                "type R = { A: int; /// b"
                                "           B: int }"
                                ""
                                ""
                                "/// U"
                                "type U ="
                                "    | X"
                                "    /// Y"
                                "    | Y"
                            ]
                        |> keys "DOC-001"
                        |> Expect.equal "field A and case X" [ "record field M.R.A"; "union case M.U.X" ]
                    }

                    test "the fields of a private representation are not checked" {
                        src [ "/// M"; "module M"; "/// R"; "type R = private { A: int }" ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }

                    test "members are checked, overrides and interface implementations are not" {
                        src
                            [
                                "/// M"
                                "module M"
                                "/// T"
                                "type T() ="
                                "    member _.A = 1"
                                "    override _.ToString() = \"\""
                                "    interface System.IDisposable with"
                                "        member _.Dispose() = ()"
                            ]
                        |> rules
                        |> Expect.equal "member A" [ "DOC-001", 5 ]
                    }

                    test "val fields and constructors are checked" {
                        src
                            [
                                "/// M"
                                "module M"
                                "/// T"
                                "type T ="
                                "    val X: int"
                                "    new(x) = { X = x }"
                            ]
                        |> keys "DOC-001"
                        |> Expect.equal "X and new" [ "field M.T.X"; "constructor M.T.new" ]
                    }

                    test "exception members are checked" {
                        src [ "/// M"; "module M"; "/// E"; "exception E of string with"; "    member _.Text = 1" ]
                        |> keys "DOC-001"
                        |> Expect.equal "Text" [ "member M.E.Text" ]
                    }

                    test "a delegate has one finding" {
                        src [ "/// M"; "module M"; "type D = delegate of int -> int" ]
                        |> keys "DOC-001"
                        |> Expect.equal "D" [ "type M.D" ]
                    }

                    test "a namespace needs no comment, its contents do" {
                        src [ "namespace N"; ""; "type T = int" ]
                        |> keys "DOC-001"
                        |> Expect.equal "type T" [ "type N.T" ]
                    }
                ]

            testList
                "DOC-002 to DOC-004"
                [
                    test "a plain block and an all-XML block pass" {
                        src
                            [
                                "/// M"
                                "module M"
                                "/// Adds one."
                                "let a x = x + 1"
                                ""
                                ""
                                "/// <summary>Adds two.</summary>"
                                "/// <param name=\"x\">The value.</param>"
                                "let b x = x + 2"
                            ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }

                    test "prose followed by a tag is mixed" {
                        src [ "/// M"; "module M"; "/// Adds one."; "/// <param name=\"x\">The value.</param>"; "let a x = x + 1" ]
                        |> rules
                        |> Expect.equal "mixed block" [ "DOC-002", 3 ]
                    }

                    test "any tag in a plain block is found" {
                        for tag in [ "<br/>"; "<b>x</b>"; "<a href=\"u\">x</a>"; "<Summary>x</Summary>" ] do
                            src [ "/// M"; "module M"; $"/// Adds %s{tag} one."; "let a x = x + 1" ]
                            |> only "DOC-002"
                            |> Expect.equal $"%s{tag} found" [ "DOC-002", 3 ]
                    }

                    test "a generic type in prose is not a tag" {
                        src [ "/// M"; "module M"; "/// Returns a Result<int, string> or an Option<T>."; "let a x = Ok x" ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }

                    test "a backtick or <c> in a plain block is found" {
                        src [ "/// M"; "module M"; "/// Adds `one`."; "let a x = x + 1"; ""; ""; "/// Adds <c>two</c>."; "let b x = x + 2" ]
                        |> rules
                        |> Expect.equal "two findings" [ "DOC-003", 3; "DOC-003", 7 ]
                    }

                    test "a backtick in an XML block is not checked" {
                        src [ "/// M"; "module M"; "/// <summary>Adds `one`.</summary>"; "let a x = x + 1" ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }

                    test "prose after the elements of an XML block is found" {
                        src [ "/// M"; "module M"; "/// <summary>Adds.</summary>"; "/// More prose."; "let a x = x + 1" ]
                        |> only "DOC-004"
                        |> Expect.equal "found" [ "DOC-004", 3 ]
                    }

                    test "an unescaped angle bracket in an XML block is found" {
                        src [ "/// M"; "module M"; "/// <summary>Returns Result<int, string>.</summary>"; "let a x = Ok x" ]
                        |> only "DOC-004"
                        |> Expect.equal "found" [ "DOC-004", 3 ]
                    }
                ]

            testList
                "COMMENT-001 and COMMENT-002"
                [
                    test "a block comment is found" {
                        src [ "/// M"; "module M"; "(* old *)"; "/// a"; "let a = 1" ]
                        |> rules
                        |> Expect.equal "block comment" [ "COMMENT-001", 3 ]
                    }

                    test "the multiplication operator is not a comment" {
                        src [ "/// M"; "module M"; "/// a"; "let a = (*) 2 3" ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }

                    test "a comment over 120 columns is found, code is not" {
                        let wide = String.replicate 120 "x"

                        src [ "/// M"; "module M"; $"// %s{wide}"; "/// a"; $"let a = \"%s{wide}\" // %s{wide}"; "" ]
                        |> rules
                        |> Expect.equal "two findings" [ "COMMENT-002", 3; "COMMENT-002", 5 ]
                    }

                    test "a comment of exactly 120 columns passes" {
                        let exact = String.replicate 117 "x"

                        src [ "/// M"; "module M"; $"// %s{exact}"; "/// a"; "let a = 1" ]
                        |> rules
                        |> Expect.isEmpty "no finding"
                    }
                ]

            testList
                "baseline"
                [
                    let f key =
                        {
                            File = "A.fs"
                            Line = 1
                            Rule = "DOC-001"
                            Key = key
                            Message = ""
                        }

                    test "findings in the baseline pass" {
                        compareBaseline [ entry (f "a") ] [ f "a" ]
                        |> Expect.equal "nothing new, nothing stale" ([], [])
                    }

                    test "a new finding fails even when another one was fixed" {
                        compareBaseline [ entry (f "a") ] [ f "b" ]
                        |> Expect.equal "b new, a stale" ([ f "b" ], [ entry (f "a") ])
                    }

                    test "a second finding under the same key is new" {
                        compareBaseline [ entry (f "a") ] [ f "a"; { f "a" with Line = 2 } ]
                        |> fst
                        |> List.length
                        |> Expect.equal "both reported" 2
                    }

                    test "pruning deletes stale entries and never adds one" {
                        prune [ entry (f "a"); entry (f "b") ] [ f "a"; f "c" ]
                        |> Expect.equal "a kept, b deleted, c not added" [ entry (f "a") ]
                    }

                    test "a parse error is never covered by the baseline" {
                        let p =
                            { f "" with
                                Rule = "PARSE-001"
                            }

                        compareBaseline [ entry p ] [ p ]
                        |> fst
                        |> Expect.equal "reported" [ p ]
                    }
                ]

            test "the Fantomas.FCS version equals the Fantomas tool version" {
                let manifest =
                    File.ReadAllText(Path.Combine(root, ".config", "dotnet-tools.json"))
                    |> Text.Json.JsonDocument.Parse

                let tool =
                    manifest.RootElement.GetProperty("tools").GetProperty("fantomas").GetProperty("version").GetString()

                let loaded =
                    typeof<SynExpr>.Assembly.GetName().Version |> fun v -> $"%i{v.Major}.%i{v.Minor}.%i{v.Build}"

                (tool, loaded)
                |> Expect.equal "tool, loaded" (fantomasFcsVersion, fantomasFcsVersion)
            }
        ]


// ---------------------------------------------------------------------------
// Entry point.
// ---------------------------------------------------------------------------

let print (f: Finding) =
    printfn $"%s{f.File}(%i{f.Line}): %s{f.Rule} %s{f.Message}"


let summary (findings: Finding list) =
    findings
    |> List.countBy _.Rule
    |> List.sortBy fst
    |> List.iter (fun (rule, n) -> printfn $"%s{rule}: %i{n}")

    printfn $"total: %i{List.length findings}"


let args = fsi.CommandLineArgs |> Array.tail


let testResult = runTestsWithCLIArgs [] [| "--summary" |] tests


if testResult <> 0 || Array.contains "--test" args then
    exit testResult
elif Array.contains "--report" args then
    let findings = checkRepository ()
    findings |> List.iter print
    printfn ""
    summary findings
    exit (if List.isEmpty findings then 0 else 1)
elif Array.contains "--write-baseline" args then
    if File.Exists baselinePath then
        printfn $"%s{baselinePath} exists; fix new findings instead, or use --prune-baseline"
        exit 1
    else
        let findings = checkRepository ()
        let n = findings |> List.filter isBaselinable |> List.map entry |> writeBaseline baselinePath
        let errors = findings |> List.filter (isBaselinable >> not)
        errors |> List.iter print
        printfn $"wrote %i{n} entries to %s{baselinePath}"
        exit (if List.isEmpty errors then 0 else 1)
elif Array.contains "--prune-baseline" args then
    let baseline = readBaseline baselinePath
    let kept = prune baseline (checkRepository ())
    writeBaseline baselinePath kept |> ignore
    printfn $"deleted %i{List.length baseline - List.length kept} entries, %i{List.length kept} remain"
    exit 0
else
    let findings = checkRepository ()
    let fresh, stale = compareBaseline (readBaseline baselinePath) findings

    if not (List.isEmpty fresh) then
        printfn "Findings not in the baseline; fix them:"
        fresh |> List.iter print

    if not (List.isEmpty stale) then
        printfn "Baseline entries that no longer match a finding; run with --prune-baseline to delete them:"
        stale |> List.iter (printfn "  %s")

    printfn $"""%i{List.length findings} findings, %i{List.length fresh} new, %i{List.length stale} stale"""
    exit (if List.isEmpty fresh && List.isEmpty stale then 0 else 1)
