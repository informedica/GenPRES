// This reads the files that determine which components a release SBOM may list and compares the SBOM with them.
//
// These functions accept parsed JSON or lines of text so SbomComponentsTests.fsx can test them with small samples.
// SbomComponents.fsx contains the command line that stages input for sbom-tool and verifies its output.

open System
open System.IO
open System.Text.Json.Nodes


/// Raises a FormatException when an input file does not have the shape this script expects.
let formatError message = raise (FormatException(message: string))


/// A package identified by its name and version.
type PackageId = { Name: string; Version: string }


module PackageId =

    /// Creates a package identity from a name and version, preserving the name's original casing.
    let create name version = { Name = name; Version = version }

    /// Returns a package name in lowercase, as NuGet compares package names without regard to case.
    let normalizeName (name: string) = name.ToLowerInvariant()

    /// Returns the identity with its name in lowercase so packages from two sources can be compared.
    let normalize id =
        { id with
            Name = normalizeName id.Name
        }


/// Describes the kind of library represented by an entry in deps.json.
[<RequireQualifiedAccess>]
type LibraryKind =
    /// The library is a project in the solution.
    | Project
    /// The library is a NuGet package.
    | Package
    /// The library has another type, recorded as written.
    | Other of string


/// Represents a library entry in deps.json.
type Library = { Id: PackageId; Kind: LibraryKind }


/// Represents a package in paket.lock and the Paket group it belongs to.
type PaketEntry = { Group: string; PackageName: string }


/// Represents a package in package-lock.json and whether npm marks it as development-only.
type NpmEntry = { Package: PackageId; IsDev: bool }


/// Identifies the package ecosystem named by a package URL in an SBOM.
[<RequireQualifiedAccess>]
type Ecosystem =
    | Nuget
    | Npm
    /// The package URL names another ecosystem, recorded as written.
    | Other of string


/// Represents an SBOM package and the ecosystem named by its package URL.
type SpdxPackage = { Ecosystem: Ecosystem; Component: PackageId }


/// Reports how an SBOM differs from the software included in a release.
type ScopeReport =
    {
        /// NuGet packages in the server's deps.json that the SBOM does not list.
        Missing: Set<PackageId>
        /// NuGet packages listed in the SBOM that appear only in Paket groups that do not ship.
        LeakedNuget: Set<PackageId>
        /// npm packages listed in the SBOM that the client lockfile marks as development-only.
        LeakedNpm: Set<PackageId>
    }


module ScopeReport =

    /// Returns true when the SBOM has no missing or leaked packages.
    let isClean report =
        Set.isEmpty report.Missing && Set.isEmpty report.LeakedNuget && Set.isEmpty report.LeakedNpm


/// Reads a JSON file and parses its contents.
let readJson (path: string) = JsonNode.Parse(File.ReadAllText path)


/// Returns a property when the JSON object contains it.
let tryProp (name: string) (node: JsonNode) = node[name] |> Option.ofObj


/// Returns a property of a JSON object or raises a FormatException when it is missing.
let prop name node =
    match tryProp name node with
    | Some value -> value
    | None -> formatError $"missing property %s{name}"


/// Returns the string value of a JSON object property.
let stringProp name node =
    node |> prop name |> (fun value -> value.GetValue<string>())


/// Returns true when an npm lockfile entry is marked as development-only.
let isDev node =
    node |> tryProp "dev" |> Option.exists (fun value -> value.GetValue<bool>())


/// Returns the library entries from parsed deps.json and raises a FormatException if a key is not a name and version.
let parseDeps deps =
    [
        for entry in (deps |> prop "libraries").AsObject() do
            match entry.Key.Split '/' with
            | [| name; version |] ->
                let kind =
                    match entry.Value |> stringProp "type" with
                    | "project" -> LibraryKind.Project
                    | "package" -> LibraryKind.Package
                    | other -> LibraryKind.Other other

                { Id = PackageId.create name version
                  Kind = kind }
            | _ -> formatError $"unexpected library key %s{entry.Key}"
    ]


/// Returns the package lines from paket.lock with their groups. Lines before the first group header belong to Main.
/// Package lines have four spaces of indentation, while dependency lines have more.
let parsePaketLock (lines: string seq) =
    let isGroupHeader (line: string) = line.StartsWith("GROUP ", StringComparison.Ordinal)

    let isPackageLine (line: string) =
        line.StartsWith("    ", StringComparison.Ordinal)
        && not (line.StartsWith("     ", StringComparison.Ordinal))

    lines
    |> Seq.scan
        (fun (group, _) (line: string) ->
            if isGroupHeader line then line.Substring(6).Trim(), None
            elif isPackageLine line then group, Some(line.Trim().Split(' ')[0])
            else group, None
        )
        ("Main", None)
    |> Seq.choose (fun (group, name) ->
        name
        |> Option.map (fun name ->
            { Group = group
              PackageName = name }
        )
    )
    |> Seq.toList


/// Returns the packages from parsed package-lock.json without the root entry. A key without a node_modules segment
/// raises a FormatException, and a link entry without a version gets an empty version.
let parseLockfile lockfile =
    let segment = "node_modules/"

    [
        for entry in (lockfile |> prop "packages").AsObject() do
            if entry.Key <> "" then
                match entry.Key.LastIndexOf(segment, StringComparison.Ordinal) with
                | -1 -> formatError $"lockfile key %s{entry.Key} has no node_modules segment"
                | at ->
                    let version =
                        entry.Value
                        |> tryProp "version"
                        |> Option.map (fun value -> value.GetValue<string>())
                        |> Option.defaultValue ""

                    { Package = PackageId.create (entry.Key.Substring(at + segment.Length)) version
                      IsDev = isDev entry.Value }
    ]


/// Removes entries marked as development-only and the root entry's devDependencies from parsed package-lock.json.
/// This lets an npm detector see only what the bundle can contain, and the function returns the number of entries removed.
let removeDevEntries lockfile =
    let packages = (lockfile |> prop "packages").AsObject()
    let devKeys = [ for entry in packages do if isDev entry.Value then entry.Key ]

    devKeys |> List.iter (fun key -> packages.Remove key |> ignore)

    packages
    |> tryProp ""
    |> Option.iter (fun root -> root.AsObject().Remove "devDependencies" |> ignore)

    devKeys.Length


/// Removes devDependencies from parsed package.json.
let removeDevDependencies manifest =
    (manifest: JsonNode).AsObject().Remove "devDependencies" |> ignore


/// Returns packages with a package URL from parsed SPDX and raises a FormatException if a package URL has no ecosystem.
let parseSpdx sbom =
    [
        for spdxPackage in (sbom |> prop "packages").AsArray() do
            match spdxPackage |> tryProp "externalRefs" with
            | None -> ()
            | Some refs ->
                for reference in refs.AsArray() do
                    let locator = reference |> stringProp "referenceLocator"

                    if
                        (reference |> stringProp "referenceType") = "purl"
                        && locator.StartsWith("pkg:", StringComparison.Ordinal)
                    then
                        let ecosystem =
                            match locator.Substring(4).Split('/', 2) with
                            | [| "nuget"; _ |] -> Ecosystem.Nuget
                            | [| "npm"; _ |] -> Ecosystem.Npm
                            | [| other; _ |] -> Ecosystem.Other other
                            | _ -> formatError $"package URL %s{locator} has no ecosystem"

                        { Ecosystem = ecosystem
                          Component =
                            PackageId.create
                                (spdxPackage |> stringProp "name")
                                (spdxPackage |> stringProp "versionInfo") }
    ]


/// Compares the packages in an SBOM with the software included in a release. Shipping groups are the Paket groups
/// whose packages end up in the release. NuGet names compare without regard to case, while Paket packages compare by name only.
let checkScope shippingGroups libraries paketEntries npmEntries spdxPackages =
    let sbomIds ecosystem =
        spdxPackages
        |> List.filter (fun package -> package.Ecosystem = ecosystem)
        |> List.map (fun package -> PackageId.normalize package.Component)
        |> Set.ofList

    let sbomNuget = sbomIds Ecosystem.Nuget
    let sbomNpm = sbomIds Ecosystem.Npm

    let shipped =
        libraries
        |> List.filter (fun library -> library.Kind = LibraryKind.Package)
        |> List.map (fun library -> PackageId.normalize library.Id)
        |> Set.ofList

    let isShipping entry = Set.contains entry.Group shippingGroups

    let namesOf entries =
        entries
        |> List.map (fun entry -> PackageId.normalizeName entry.PackageName)
        |> Set.ofList

    let nonShippingOnly =
        Set.difference
            (paketEntries |> List.filter (isShipping >> not) |> namesOf)
            (paketEntries |> List.filter isShipping |> namesOf)

    let npmIds isDevEntry =
        npmEntries
        |> List.filter (fun entry -> entry.IsDev = isDevEntry)
        |> List.map (fun entry -> PackageId.normalize entry.Package)
        |> Set.ofList

    {
        Missing = Set.difference shipped sbomNuget
        LeakedNuget = sbomNuget |> Set.filter (fun id -> Set.contains id.Name nonShippingOnly)
        LeakedNpm = Set.intersect sbomNpm (Set.difference (npmIds true) (npmIds false))
    }
