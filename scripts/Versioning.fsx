// Repo version, read from the single source of truth.
// Loaded by scripts/CheckSolutionVersions.fsx and scripts/ReleaseNotes.fsx so both
// resolve <Version> identically. Parse the XML directly instead of regexing props files.

module Versioning

open System
open System.Xml.Linq


/// Reads the single <Version> element from the repo-root Directory.Build.props file.
/// <param name="propsPath">Path to the props file.</param>
/// <returns>The version string, e.g. <c>0.1.2-alpha.4</c>.</returns>
/// Throws if the file does not contain exactly one <Version> element.
let readVersion (propsPath: string) =
    let doc = XDocument.Load propsPath
    let ns = doc.Root.Name.Namespace

    match doc.Descendants(ns + "Version") |> Seq.map _.Value |> List.ofSeq with
    | [ version ] -> version
    | [] -> failwith $"No <Version> found in %s{propsPath}"
    | versions -> failwith $"Expected exactly one <Version> in %s{propsPath}, found %i{versions.Length}"


/// <summary>Whether a version is a SemVer pre-release.</summary>
/// <param name="version">A SemVer 2.0.0 version string, e.g. <c>0.1.2-alpha.4</c>.</param>
/// <remarks>Checks the core version only; build metadata is ignored. A hyphen in the version
/// indicates a pre-release.</remarks>
let isPreRelease (version: string) =
    version.Split '+' |> Array.head |> _.Contains("-")
