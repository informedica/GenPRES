namespace Informedica.GenForm.Lib


module GenericLabel =

    open Informedica.Utils.Lib.BCL

    let toString =
        function
        | Shorthand s -> s
        | Canonical sl -> sl |> String.concat "/"
        | GenericForm(g, f) -> $"%s{g} (%s{f})"
        | GenericBrand(g, b) -> $"%s{g} (%s{b})"


    /// Returns the generic name without the form or brand, for lookups in sources such as the G-Standaard
    /// that use the substance name only. GenericBrand ("glycopyrronium", "Sialanar") gives "glycopyrronium".
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


    /// Creates a label from the generic g, the form f and the brand b. A brand wins over a form when both
    /// are given; with neither, the label is the generic name alone.
    let toLabel g f b =
        match b |> String.isNullOrWhiteSpace, f |> String.isNullOrWhiteSpace with
        | false, _ -> fromGenericBrand g b
        | _, false -> fromGenericForm g f
        | _ -> fromShorthand g


    /// Creates the label of the generic of a dose rule row from its name, form and brand.
    let ofGenericData (g: GenericData) = toLabel g.Name g.Form g.Brand
