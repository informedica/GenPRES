namespace Informedica.GenForm.Lib


module GenericLabel =

    open Informedica.Utils.Lib.BCL

    let toString =
        function
        | Shorthand s -> s
        | Canonical sl -> sl |> String.concat "/"
        | GenericForm(g, f) -> $"%s{g} (%s{f})"
        | GenericBrand(g, b) -> $"%s{g} (%s{b})"


    /// The base generic substance name, without the form/brand qualifier.
    /// Used for lookups against external sources (e.g. the G-Standaard) that
    /// key on the substance name only — e.g. GenericBrand ("glycopyrronium",
    /// "Sialanar") resolves to "glycopyrronium", not "glycopyrronium
    /// (Sialanar)".
    let genericName =
        function
        | Shorthand s -> s
        | Canonical sl -> sl |> String.concat "/"
        | GenericForm(g, _) -> g
        | GenericBrand(g, _) -> g

    let fromShorthand = Shorthand

    let fromCanonical = Canonical

    let fromGenericForm g f = (g, f) |> GenericForm

    let fromGenericBrand g b = (g, b) |> GenericBrand


    /// Build the GenericLabel from the original source restriction.
    /// Brand takes precedence over form when both are present (source data
    /// should not restrict both; that is flagged as a validation warning).
    /// When neither form nor brand restricts, the label is the canonical/
    /// shorthand generic name (form/brand are not shown), even though the
    /// domain Generic.Form still carries the expanded grouping form.
    let toLabel g f b =
        match b |> String.isNullOrWhiteSpace, f |> String.isNullOrWhiteSpace with
        | false, _ -> fromGenericBrand g b
        | _, false -> fromGenericForm g f
        | _ -> fromShorthand g
