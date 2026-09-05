namespace Informedica.GenForm.Lib


module Source =

    open Informedica.Utils.Lib.BCL


    /// One Nederlands Kinder Formularium (NKF) entry: the generic name and the id
    /// its URL is built from.
    type NKFMedication =
        {
            // The generic name as the NKF spells it: trimmed, lower case, and with
            // combination preparations joined by "+".
            Generic: string
            // The NKF's own id, the first path segment of a medication URL.
            Id: string
        }


    /// Resolve the external formulary link for a <c>Source</c> / <c>GenericLabel</c>
    /// pair, or <c>None</c> when there is none.
    ///
    /// A function type rather than a data dependency so that <c>DoseRule.Print</c>
    /// stays pure and the NKF fetch can live in the loader that the resource registry
    /// composes. Mirrors <c>Check.GStandProvider</c>.
    type LinkProvider = Source -> GenericLabel -> string option


    let toString =
        function
        | Identified s -> s
        | Other s -> s


    let identified = Identified

    let other = Other


    /// <summary>
    /// Find the external formulary link for a <c>Source</c> / <c>GenericLabel</c> pair.
    /// </summary>
    /// <remarks>
    /// The source is matched first and <c>meds</c> is consulted only by the NKF branch,
    /// which is the only one that needs an id. An empty <c>meds</c> — what the resource
    /// registry yields when kinderformularium.nl is unreachable — therefore drops NKF
    /// links only, and leaves FTK links intact.
    /// </remarks>
    let getLink (meds: NKFMedication list) source gen : string option =
        let gen = gen |> GenericLabel.toString
        let src = source |> toString
        let slug = gen |> String.replace "/" "-"

        match src with
        | _ when src = "NKF" ->
            meds
            |> List.tryFind (fun m ->
                m.Generic
                |> String.split "+"
                |> List.map String.trim
                |> String.concat "/"
                |> String.equalsCapInsens gen
            )
            |> Option.map (fun m ->
                $"[Kinderformularium](https://www.kinderformularium.nl/geneesmiddel/%s{m.Id}/%s{slug})"
            )
        | _ when src = "FK" ->
            // The FK files each monograph under the first letter of its (lower case)
            // generic name: .../preparaatteksten/p/paracetamol. An empty label has no page.
            let slug = slug |> String.toLower

            if slug |> String.isNullOrWhiteSpace then
                None
            else
                $"[Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/%c{slug[0]}/%s{slug}#doseringen)"
                |> Some
        | _ -> None


    /// <summary>
    /// A <c>LinkProvider</c> that never finds a link, so every caller falls back to its
    /// own default.
    /// </summary>
    /// <remarks>
    /// For scripts and tests that want no external links at all. It is deliberately *not*
    /// what the resource registry falls back to when the NKF fetch fails: that degrades to
    /// <c>getLink []</c>, which loses the NKF links but keeps the FTK ones.
    /// </remarks>
    let noLinks: LinkProvider = fun _ _ -> None
