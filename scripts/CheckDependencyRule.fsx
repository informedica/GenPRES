// Check Dependency Rule
//
// Architecture fitness test for ADR-0001 (docs/adr/0001-system-architecture.md):
// project references point inward, the core never reaches network, filesystem,
// environment, clock or entropy, and only the DMZ (the server-side outer ring) knows
// configuration and owns entry points. And for ADR-0008 rule R9
// (docs/adr/0008-contract-model-dto-mapping-boundary.md): the contract model stays in
// the server's edge files, no domain library names it, and Shared stays transpilable.
//
// The ring map and the project reader live in scripts/DependencyRule.fsx, shared with
// scripts/ProjectGraph.fsx. The allow-lists (project references there, source-level
// allowances below) are the inventory of today's violations; each entry names a reason
// and is a ratchet: an entry that no longer matches anything fails the run, so the list
// can only shrink. Add an entry only with a reason and an issue.
//
// Run with: dotnet fsi scripts/CheckDependencyRule.fsx
// Prototype per the script-only policy in AGENTS.md; no build is required.

#r "nuget: Expecto"
#load "DependencyRule.fsx"

open System
open System.IO
open System.Text.RegularExpressions
open Expecto
open Expecto.Flip
open DependencyRule


// ---------------------------------------------------------------------------
// Allow-lists: the inventory of today's violations. Every entry must still match.
// ---------------------------------------------------------------------------

/// A core source file that reaches outside. `Token = None` allows the whole file
/// (an IO module awaiting eviction); `Token = Some t` allows one banned token in it.
type Allowance =
    {
        File: string
        Token: string option
        Reason: string
    }


let allowFile file reason =
    {
        File = file
        Token = None
        Reason = reason
    }


let allowToken file token reason =
    {
        File = file
        Token = Some token
        Reason = reason
    }


/// Tokens a core project may not contain outside comments. Matched on a word
/// boundary before the token, so `AppEnv.` does not match `Env.`.
let bannedTokens =
    [
        "System.IO"
        "System.Net"
        "HttpClient"
        "Environment.GetEnvironmentVariable"
        "Environment.SetEnvironmentVariable"
        "Environment.GetEnvironmentVariables"
        "Environment.CurrentDirectory"
        "Environment.ProcessorCount"
        "Environment.MachineName"
        "Environment.UserName"
        "AppDomain.CurrentDomain"
        "AppContext.BaseDirectory"
        "DateTime.Now"
        "DateTime.UtcNow"
        "DateTimeOffset.Now"
        "DateTimeOffset.UtcNow"
        "Guid.NewGuid"
        "Console."
        "ConsoleWriter"
        "writeErrorMessage"
        "writeWarningMessage"
        "writeInfoMessage"
        "writeDebugMessage"
        "printfn"
        "eprintfn"
        "File."
        "Directory."
        "Web."
        "Env."
        "AppPath"
        "StopWatch."
        "Stopwatch"
        "Async.RunSynchronously"
        "Memoization.memoize"
        "MailboxProcessor"
        "FileWriterAgent"
        "AgentLogging"
        // T6: the contract model (ADR-0008 R9); a domain library never sees Shared
        "Shared."
    ]


/// Phase numbers refer to docs/implementation-plans/378-dependency-rule.md.
/// "Permanent" entries are accepted exceptions recorded in ADR-0001.
let allowances =
    let utilsSplit = "IO module in Utils.Lib; leaves the core with the Utils split (Phase 2)"
    let evict = "Google-Sheets/NKF loader in GenFORM; moves to the adapter project (Phase 2)"
    let clock = "ambient clock in the core; becomes a `now` parameter (Phase 4)"
    let chunking = "Permanent: Environment.ProcessorCount only sizes parallel chunks, never a result"

    [
        // Utils.Lib: whole IO modules awaiting the pure/IO split
        allowFile "src/Informedica.Utils.Lib/Directory.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/File.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/Env.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/App.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/AppPath.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/Console.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/StopWatch.fs" utilsSplit
        allowFile "src/Informedica.Utils.Lib/Web.fs" utilsSplit
        allowToken "src/Informedica.Utils.Lib/Path.fs" "System.IO" "Permanent: System.IO.Path string helpers only, no filesystem access"
        allowToken "src/Informedica.Utils.Lib/Memoization.fs" "Stopwatch" "Permanent: timing in an example function, no IO"
        // Logging.Lib
        allowToken "src/Informedica.Logging.Lib/Logging.fs" "DateTime.Now" clock
        allowToken "src/Informedica.Utils.Lib/BCL/DateTime.fs" "DateTime.Now" clock
        // GenCORE.Lib
        allowToken "src/Informedica.GenCORE.Lib/Patient.fs" "DateTime.Now" clock
        // GenSOLVER.Lib
        allowToken "src/Informedica.GenSOLVER.Lib/Utils.fs" "Environment.ProcessorCount" chunking
        // GenFORM.Lib
        allowToken "src/Informedica.GenFORM.Lib/Utils.fs" "Environment.ProcessorCount" chunking
        allowToken "src/Informedica.GenFORM.Lib/Utils.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Utils.fs" "StopWatch." evict
        allowToken "src/Informedica.GenFORM.Lib/Mapping.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "Async.RunSynchronously" evict
        allowToken "src/Informedica.GenFORM.Lib/Product.fs" "StopWatch." evict
        allowToken "src/Informedica.GenFORM.Lib/DoseRuleLoader.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/DoseRuleLoader.fs" "Async.RunSynchronously" evict
        allowFile "src/Informedica.GenFORM.Lib/SourceLoader.fs" evict
        allowToken "src/Informedica.GenFORM.Lib/SolutionRule.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/RenalRule.fs" "Web." evict
        allowToken "src/Informedica.GenFORM.Lib/Resources.fs" "DateTime.UtcNow" "CachedResourceProvider TTL clock; provider moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Export.fs" "File." "cwd-relative export file write; moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Export.fs" "Directory." "cwd-relative export file write; moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Export.fs" "Environment.CurrentDirectory" "cwd-relative export file write; moves to the adapter project (Phase 2)"
        allowToken "src/Informedica.GenFORM.Lib/Api.fs" "Async.RunSynchronously" "parallel rule filtering blocks on Async; keep pure or move to the edge (Phase 2)"
        // GenORDER.Lib
        allowToken "src/Informedica.GenORDER.Lib/Utils.fs" "Env." "dead getDataFromGenPres reads env; delete (Phase 0)"
        allowToken "src/Informedica.GenORDER.Lib/EquationMapping.fs" "Memoization.memoize" "Permanent: memoizes a hard-coded, pure equation list"
        allowToken "src/Informedica.GenORDER.Lib/Medication.fs" "Guid.NewGuid" "ambient entropy in a constructor; becomes a `newId` parameter (Phase 4)"
    ]


/// The token by which server code names the contract model (`Shared.Types.Patient`,
/// `open Shared.Api`); ADR-0008 R9, T5.
let contractToken = "Shared."


/// T5: the server files in which a code line may name the contract model, as file-name
/// globs over `src/Informedica.GenPRES.Server/`: the mappers, the command handlers, the
/// ports, the composition root, the session service, the compute wrapper, the API
/// implementation and the host. Every other server file needs a `contractAllowances` entry.
let contractEdgeFiles =
    [
        "ServerApi.Mappers*.fs"
        "ServerApi.*Command.fs"
        "ServerApi.Ports.fs"
        "ServerApi.CompositionRoot.fs"
        "ServerApi.Session.fs"
        "ServerApi.Compute.fs"
        "ServerApi.ApiImpl.fs"
        "Server.fs"
    ]


/// Server files outside the edge that still name the contract model today. Phase and step
/// numbers refer to docs/implementation-plans/725-contract-model-dto-domain-flow.md. Each
/// entry is a ratchet: one that no longer matches fails the run.
let contractAllowances =
    [
        "src/Informedica.GenPRES.Server/ServerApi.Services.fs",
        "the formulary and parenteralia services typed on contract models, and the order context parse; own issue after plan 725"
        "src/Informedica.GenPRES.Server/ServerApi.Adapters.fs",
        "the formulary, interaction and admin ports typed on contract models, own issue after plan 725; the session port's identity types (UserContext, OpenedToken, the refusals and endings) stay contract by ADR-0008 R6 and ADR-0007 section 3, a contract-free session domain is its own issue"
        "src/Informedica.GenPRES.Server/ServerApi.StubAdapters.fs",
        "the stub session and identity adapters answer the session identity types (UserContext, OpenedToken, the refusals and endings), which stay contract by ADR-0008 R6 and ADR-0007 section 3; a contract-free session domain is its own issue"
        "src/Informedica.GenPRES.Server/ServerApi.SqlAdapters.fs",
        "the session store holds the same session identity types the stub answers (the refusals, the Roles, the tokens), which stay contract by ADR-0008 R6 and ADR-0007 section 3; a contract-free session domain is its own issue"
        "src/Informedica.GenPRES.Server/LogAnalyzer.fs",
        "admin log listing answered as a contract record, no domain behind it; own issue"
    ]


/// T7: the packages Shared may reference, exactly. Shared is transpiled to JavaScript for
/// the client, so a package joins this list only when it is known to be Fable-compatible.
let sharedPackages = [ "FSharp.Core" ]


/// The prefixes under which settings are read, as they appear in source (`"GENPRES_URL_ID"`).
/// One entry today; the single place to change. Whether a second executable would get its own
/// prefix is an open question in `docs/roadmap/modular-design-discussion.md`.
let settingPrefixes = [ "GENPRES_" ]


/// True when a code line names a setting under any known prefix.
let namesSetting (line: string) =
    settingPrefixes |> List.exists (fun p -> line.Contains("\"" + p))


/// Core files that may name a `GENPRES_*` setting today.
let allowedConfigMentions =
    [
        "src/Informedica.Utils.Lib/AppPath.fs", "GENPRES_ROOT root resolution; leaves the core with the Utils split (Phase 2)"
        "src/Informedica.Utils.Lib/Console.fs", "GENPRES_DEBUG read inside the console writer; leaves the core with the Utils split (Phase 2)"
        "src/Informedica.GenORDER.Lib/Utils.fs", "GENPRES_URL_ID constant used only by dead getDataFromGenPres; delete (Phase 0)"
    ]


/// A line that is only a comment does not reach outside.
let isComment (line: string) = line.TrimStart().StartsWith "//"


/// `token` occurs in `line` and is not the tail of a longer identifier.
let containsToken (token: string) (line: string) =
    let rec search from =
        match line.IndexOf(token, from, StringComparison.Ordinal) with
        | -1 -> false
        | i when i = 0 || not (Char.IsLetterOrDigit line[i - 1]) -> true
        | i -> search (i + 1)

    search 0


/// `*` in a file-name glob matches any run of characters; nothing else is special.
let globMatches (pattern: string) (name: string) =
    let re = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$"
    Regex.IsMatch(name, re)


let isContractEdgeFile (rel: string) =
    let name = Path.GetFileName rel
    contractEdgeFiles |> List.exists (fun pattern -> globMatches pattern name)


let codeLines (file: string) =
    File.ReadAllLines file
    |> Array.mapi (fun i l -> i + 1, l)
    |> Array.filter (fun (_, l) -> not (isComment l))


let failWithAll what (violations: string list) =
    if not violations.IsEmpty then
        violations
        |> String.concat Environment.NewLine
        |> sprintf "%s:%s%s" what Environment.NewLine
        |> failtest


// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

let ringMapTests =
    testList
        "ring map"
        [
            test "every src project in GenPRES.sln has a ring" {
                srcProjects ()
                |> List.filter (fun p -> p.Ring.IsNone)
                |> List.map _.Name
                |> failWithAll "projects without a ring"
            }

            test "every ring map entry names a project in GenPRES.sln" {
                let names = srcProjects () |> List.map _.Name |> Set.ofList

                rings
                |> Map.keys
                |> Seq.filter (fun n -> not (names.Contains n))
                |> List.ofSeq
                |> failWithAll "ring map entries without a project"
            }
        ]


let referenceTests =
    testList
        "T1 project references point inward"
        [
            test "no project references an outer ring, except the allow-list" {
                let projects = srcProjects ()
                let ringOf name = rings |> Map.tryFind name

                let allowed =
                    allowedReferences |> List.map (fun (f, t, _) -> f, t) |> Set.ofList

                projects
                |> List.collect (fun p ->
                    p.References
                    |> List.choose (fun dep ->
                        match p.Ring, ringOf dep with
                        | Some from, Some target when not (mayReference from target) ->
                            if allowed.Contains(p.Name, dep) then
                                None
                            else
                                Some $"%s{p.Name} (%A{from}) -> %s{dep} (%A{target})"
                        | _ -> None
                    )
                )
                |> failWithAll "outward references"
            }

            test "every allowed reference still exists (ratchet)" {
                let projects = srcProjects ()

                allowedReferences
                |> List.filter (fun (f, t, _) ->
                    projects
                    |> List.exists (fun p -> p.Name = f && p.References |> List.contains t)
                    |> not
                )
                |> List.map (fun (f, t, _) -> $"%s{f} -> %s{t}")
                |> failWithAll "allow-list entries for references that no longer exist; remove them"
            }
        ]


let coreTests =
    let coreFiles () =
        srcProjects ()
        |> List.filter (fun p -> p.Ring = Some Ring.Core)
        |> List.collect _.SourceFiles

    let isAllowed (file: string) (token: string) =
        allowances
        |> List.exists (fun a -> a.File = file && (a.Token.IsNone || a.Token = Some token))

    testList
        "T2 the core does not reach outside"
        [
            test "no core source line uses a banned token, except the allow-list" {
                coreFiles ()
                |> List.collect (fun file ->
                    let rel = relative file

                    codeLines file
                    |> Array.toList
                    |> List.collect (fun (n, line) ->
                        bannedTokens
                        |> List.filter (fun t -> containsToken t line && not (isAllowed rel t))
                        |> List.map (fun t -> $"%s{rel}:%i{n} %s{t}")
                    )
                )
                |> failWithAll "core files reaching outside"
            }

            test "every allowance still matches something (ratchet)" {
                let files = coreFiles () |> List.map (fun f -> relative f, f) |> Map.ofList

                allowances
                |> List.filter (fun a ->
                    match files |> Map.tryFind a.File with
                    | None -> true
                    | Some full ->
                        let lines = codeLines full |> Array.map snd

                        let tokens =
                            match a.Token with
                            | Some t -> [ t ]
                            | None -> bannedTokens

                        lines
                        |> Array.exists (fun l -> tokens |> List.exists (fun t -> containsToken t l))
                        |> not
                )
                |> List.map (fun a -> $"%s{a.File} %A{a.Token}")
                |> failWithAll "allowances that no longer match; remove them"
            }
        ]


let dmzTests =
    testList
        "T3/T4 only the DMZ knows configuration and owns entry points"
        [
            test "GENPRES_ settings are named only in DMZ projects, except the allow-list" {
                let allowed = allowedConfigMentions |> List.map fst |> Set.ofList

                srcProjects ()
                |> List.filter (fun p ->
                    match p.Ring with
                    | Some r -> not (isDmz r) && r <> Ring.Client
                    | None -> false
                )
                |> List.collect _.SourceFiles
                |> List.collect (fun file ->
                    let rel = relative file

                    if allowed.Contains rel then
                        []
                    else
                        codeLines file
                        |> Array.filter (fun (_, l) -> namesSetting l)
                        |> Array.map (fun (n, _) -> $"%s{rel}:%i{n}")
                        |> Array.toList
                )
                |> failWithAll "configuration named outside the DMZ"
            }

            test "every config allowance still matches something (ratchet)" {
                allowedConfigMentions
                |> List.filter (fun (rel, _) ->
                    let full = Path.Combine(repoRoot, rel)

                    not (File.Exists full)
                    || codeLines full |> Array.exists (fun (_, l) -> namesSetting l) |> not
                )
                |> List.map fst
                |> failWithAll "config allowances that no longer match; remove them"
            }

            test "only Presentation projects declare an entry point" {
                srcProjects ()
                |> List.filter (fun p -> p.Ring <> Some Ring.Presentation)
                |> List.collect _.SourceFiles
                |> List.filter (fun file -> File.ReadAllText file |> _.Contains("[<EntryPoint>]"))
                |> List.map relative
                |> failWithAll "entry points outside Presentation"
            }
        ]


let contractTests =
    let serverProject () =
        srcProjects () |> List.find (fun p -> p.Name = "Informedica.GenPRES.Server")

    let sharedProject () =
        srcProjects () |> List.find (fun p -> p.Name = "Informedica.GenPRES.Shared")

    let namesContract (line: string) = containsToken contractToken line

    testList
        "T5/T7 the contract model stays at the server's edge and Shared stays transpilable"
        [
            test "T5 only edge files of the server name the contract model, except the allow-list" {
                let allowed = contractAllowances |> List.map fst |> Set.ofList

                serverProject().SourceFiles
                |> List.collect (fun file ->
                    let rel = relative file

                    if isContractEdgeFile rel || allowed.Contains rel then
                        []
                    else
                        codeLines file
                        |> Array.filter (fun (_, l) -> namesContract l)
                        |> Array.map (fun (n, _) -> $"%s{rel}:%i{n}")
                        |> Array.toList
                )
                |> failWithAll "contract model named outside the server's edge"
            }

            test "T5 every contract allowance still matches something (ratchet)" {
                let files =
                    serverProject().SourceFiles |> List.map (fun f -> relative f, f) |> Map.ofList

                contractAllowances
                |> List.filter (fun (rel, _) ->
                    match files |> Map.tryFind rel with
                    | None -> true
                    | Some full ->
                        isContractEdgeFile rel
                        || codeLines full |> Array.exists (fun (_, l) -> namesContract l) |> not
                )
                |> List.map fst
                |> failWithAll "contract allowances that no longer match, or name an edge file; remove them"
            }

            test "T7 Shared references exactly the allowed packages and no project" {
                let shared = sharedProject ()
                let dir = Path.GetDirectoryName(Path.Combine(repoRoot, shared.Path))

                let packages =
                    Path.Combine(dir, "paket.references")
                    |> File.ReadAllLines
                    |> Array.map _.Trim()
                    |> Array.filter (fun l -> l <> "" && not (l.StartsWith "//"))
                    |> Array.toList

                packages
                |> Expect.equal "Shared's paket.references must equal the allow-list" sharedPackages

                shared.References
                |> Expect.isEmpty "Shared must reference no project"
            }
        ]


runTestsWithCLIArgs
    []
    [| "--summary" |]
    (testList
        "dependency rule"
        [
            ringMapTests
            referenceTests
            coreTests
            dmzTests
            contractTests
        ])
|> exit
