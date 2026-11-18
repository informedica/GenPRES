namespace Informedica.GenForm.Lib


module Generic =


    let create lbl frm ids : Generic =
        {
            Label = lbl
            Form = frm
            Products = ids
        }


    let toString gen = gen.Label |> GenericLabel.toString


    /// Returns the generic substance name without form or brand, used for lookups such as the G-Standaard.
    let genericName gen = gen.Label |> GenericLabel.genericName
