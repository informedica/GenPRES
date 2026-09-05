namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL


module UnitsParse =

    open FParsec
    open Units
    open Combine

    let groupIsGeneralOrNone s =
        let xs = (String.regex "[^\[\]]+(?=\])").Matches(s)
        xs |> Seq.map _.Value |> Seq.forall (String.equalsCapInsens "general")


    /// <summary>
    /// Creates a Unit from a string s, if possible,
    /// otherwise returns None. Note will take care of
    /// the n value of a unit! So, for example, the unit
    /// 36 hours can be parsed correctly.
    /// </summary>
    /// <example>
    /// fromString "36 hours" = Some (Time (Hour 36N))
    /// </example>
    let fromString s =
        if s |> String.isNullOrWhiteSpace then
            None
        else
            s
            |> String.split "/"
            |> function
                | us when us |> List.length >= 1 && (us |> List.length <= 3) ->
                    us
                    |> List.map (fun s ->
                        // need to replace nan as this otherwise will be a float
                        // NOTE: raw substring replace; a name containing "nan" would be mangled.
                        let s = s |> String.replace "nan" "nnn"

                        match s |> run Parser.parseUnit with
                        | Success(u, _, _) -> Some u
                        | Failure _ ->
                            if s |> String.isNullOrWhiteSpace then
                                None
                            else
                                if s |> groupIsGeneralOrNone |> not then
                                    raise (System.FormatException $"invalid unit group {s}")

                                s |> String.removeBrackets |> Units.General.general |> Some
                    )
                    |> fun us ->
                        if us |> List.forall Option.isSome then
                            us |> List.map Option.get |> List.reduce (fun u1 u2 -> u1 |> per u2) |> Some
                        else
                            printfn $"cannot parse {s}"
                            None
                | _ ->
                    printfn $"cannot parse {s}"
                    None


    /// Append a group to a string that represents a unit
    /// Example: stringWithGroup "mg" = "mg[Mass]"
    let stringWithGroup u =
        UnitDetails.units
        |> List.filter (fun ud -> ud.Group <> Group.WeightGroup)
        |> List.tryFind (fun ud ->
            [
                ud.Abbreviation.Dut
                ud.Abbreviation.Eng
                ud.Name.Dut
                ud.Name.Eng
            ]
            |> List.append ud.Synonyms
            |> List.exists (String.equalsCapInsens (u |> String.replaceNumbers ""))
        )
        |> function
            | Some ud -> ud.Group |> Group.toString
            | None -> "General"
        |> sprintf "%s[%s]" u
