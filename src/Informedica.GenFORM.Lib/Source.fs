namespace Informedica.GenForm.Lib


module Source =

    open Informedica.Utils.Lib.BCL


    /// One Nederlands Kinder Formularium (NKF) entry: the generic name and the id its URL is built from.
    type NKFMedication =
        {
            /// The generic name as the NKF writes it, trimmed and in lower case, with the generics of a
            /// combination joined by "+".
            Generic: string
            /// The NKF id, the first path segment of the medication URL.
            Id: string
        }


    /// Returns the external formulary link for a source and a generic label, or None when there is none.
    /// A function type, so DoseRule.Print stays pure and the resources load the NKF index.
    type LinkProvider = Source -> GenericLabel -> string option


    let toString =
        function
        | Identified s -> s
        | Other s -> s


    let identified = Identified

    let other = Other


    /// Returns the Markdown link for the source NKF or FK and the generic label, or None for another
    /// source or a generic without a link. Only the NKF link reads meds, so an empty list, used when
    /// the NKF index could not be fetched, loses only the NKF links.
    let getLink (meds: NKFMedication list) source gen : string option =
        // The generic name without a brand or form, as in "glycopyrronium (Sialanar)", because the
        // NKF index and the FK page names use the generic name only.
        let gen = gen |> GenericLabel.genericName
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
            // The FK places each page under the first letter of the lower case generic name, as in
            // .../preparaatteksten/p/paracetamol. An empty generic name has no page.
            let slug = slug |> String.toLower

            if slug |> String.isNullOrWhiteSpace then
                None
            else
                $"[Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/%c{slug[0]}/%s{slug}#doseringen)"
                |> Some
        | _ -> None


    /// A link provider that never finds a link, for scripts and tests without external links. The
    /// resources use getLink [] instead when the NKF fetch fails, which keeps the FK links.
    let noLinks: LinkProvider = fun _ _ -> None
