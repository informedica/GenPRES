// CHANGELOG.md section parsing for the release tooling.
// Loaded by scripts/ReleaseNotes.fsx and scripts/ChangelogTests.fsx.
//
// A small changelog grammar: any line starting `## ` ends a section, including one inside a fenced
// code block. ShipIt-generated bodies never contain one.

module Changelog

open System


[<RequireQualifiedAccess>]
type SectionError =
    // No heading names the version
    | Missing
    // The heading exists but the section under is blank
    | Empty
    // More than one heading names the version, so the body is ambiguous
    | Duplicate of count: int


/// The version named by a `## ...` heading, if the line is one. ShipIt writes
/// `## 0.1.2-alpha.4 - 2026-08-17`; hand-written entries predating it use
/// `## [0.1.2-alpha.1] - 2026-03-23`, hence the bracket strip.
let headingVersion (line: string) =
    if not (line.StartsWith("## ", StringComparison.Ordinal)) then
        None
    else
        line.Substring 3
        |> _.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryHead
        |> Option.map _.Trim([| '['; ']' |])
        |> Option.filter (String.IsNullOrWhiteSpace >> not)


/// Everything between a version's own heading and the next `## ` heading, 
/// surrounding blank lines trimmed. This is the GitHub Release body.
let sectionFor version (lines: string[]) =
    let isBlank (line: string) = String.IsNullOrWhiteSpace line

    let headings =
        lines
        |> Array.indexed
        |> Array.filter (fun (_, line) -> headingVersion line = Some version)

    match headings with
    | [||] -> Error SectionError.Missing
    | [| (start, _) |] ->
        let rest = lines |> Array.skip (start + 1)

        let body =
            match rest |> Array.tryFindIndex (headingVersion >> Option.isSome) with
            | Some next -> rest |> Array.take next
            | None -> rest

        match body |> Array.skipWhile isBlank |> Array.rev |> Array.skipWhile isBlank with
        | [||] -> Error SectionError.Empty
        | trimmed -> trimmed |> Array.rev |> String.concat "\n" |> Ok
    | duplicates -> duplicates.Length |> SectionError.Duplicate |> Error


/// Message for a failed lookup, naming which of the three ways it failed.
let describeError version error =
    match error with
    | SectionError.Missing -> $"No CHANGELOG.md section found for version %s{version}"
    | SectionError.Empty -> $"The CHANGELOG.md section for version %s{version} is empty"
    | SectionError.Duplicate count -> $"CHANGELOG.md has %i{count} sections for version %s{version}"
