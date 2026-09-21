namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib.Types

open Informedica.Utils.Lib
open Informedica.GenUnits.Lib.Core


/// Classification of a Unit into a Group (Mass, Volume, Time, ...). The type
/// lives at namespace level so the Group module below can shadow it
/// (type-first convention, like List type + List module).
type Group =
    | NoGroup
    | ZeroGroup
    | GeneralGroup of string
    | CountGroup
    | MassGroup
    | DistanceGroup
    | VolumeGroup
    | TimeGroup
    | MolarGroup
    | InterNatUnitGroup
    | WeightGroup
    | HeightGroup
    | BSAGroup
    | EnergyGroup
    | CombiGroup of (Group * Operator * Group)


module Group =


    module Constants =


        [<Literal>]
        let General = "General"

        [<Literal>]
        let NoGroup = "NoGroup"

        [<Literal>]
        let Count = "Count"

        [<Literal>]
        let Mass = "Mass"

        [<Literal>]
        let Distance = "Distance"

        [<Literal>]
        let Volume = "Volume"

        [<Literal>]
        let Time = "Time"

        [<Literal>]
        let Molar = "Molar"

        [<Literal>]
        let InterNatUnit = "InterNatUnit"

        [<Literal>]
        let Weight = "Weight"

        [<Literal>]
        let Height = "Height"

        [<Literal>]
        let BSA = "BSA"

        [<Literal>]
        let Energy = "Energy"


    /// Get the corresponding group for a unit
    /// Example: unitToGroup (Mass (KiloGram 1N)) = MassGroup
    let unitToGroup u =
        let rec get u =
            match u with
            | NoUnit
            | ZeroUnit -> Group.NoGroup
            | General(n, _) -> Group.GeneralGroup n
            | Count _ -> Group.CountGroup
            | Mass _ -> Group.MassGroup
            | Distance _ -> Group.DistanceGroup
            | Volume _ -> Group.VolumeGroup
            | Time _ -> Group.TimeGroup
            | Molar _ -> Group.MolarGroup
            | International _ -> Group.InterNatUnitGroup
            | Weight _ -> Group.WeightGroup
            | Height _ -> Group.HeightGroup
            | BSA _ -> Group.BSAGroup
            | Energy _ -> Group.EnergyGroup
            | CombiUnit(ul, op, ur) -> (get ul, op, get ur) |> Group.CombiGroup

        get u


    /// <summary>
    /// Check whether a group g1
    /// contains group g2, i.e.
    /// g1 |> contains g2 checks
    /// whether group g1 contains g2
    /// </summary>
    /// <example>
    /// CombiGroup(MassGroup, OpPer, VolumeGroup) |> contains MassGroup = true
    /// </example>
    let contains g2 g1 =
        let rec cont g =
            match g with
            | Group.GeneralGroup _
            | Group.NoGroup
            | Group.ZeroGroup
            | Group.CountGroup
            | Group.MassGroup
            | Group.DistanceGroup
            | Group.VolumeGroup
            | Group.TimeGroup
            | Group.MolarGroup
            | Group.InterNatUnitGroup
            | Group.WeightGroup
            | Group.HeightGroup
            | Group.EnergyGroup
            | Group.BSAGroup -> g = g2
            | Group.CombiGroup(gl, _, gr) -> cont gl || cont gr

        cont g1


    /// Get a list of the groups in a group g
    let rec getGroups g =
        match g with
        | Group.CombiGroup(gl, _, gr) -> gl |> getGroups |> List.prepend (gr |> getGroups)
        | _ -> [ g ]


    /// Separate numerators from denominators of a group.
    /// The recursion is always entered at the numerator, so the
    /// isNum flag (true = numerator, false = denominator) is an
    /// internal concern hidden behind the one-argument numDenom.
    let numDenom g =
        let rec loop isNum g =
            match g with
            | Group.CombiGroup(gl, OpTimes, gr) ->
                let lns, lds = gl |> loop isNum
                let rns, rds = gr |> loop isNum
                lns @ rns, lds @ rds
            | Group.CombiGroup(gl, OpPer, gr) ->
                if isNum then
                    let lns, lds = gl |> loop true
                    let rns, rds = gr |> loop false
                    lns @ rns, lds @ rds
                else
                    let lns, lds = gr |> loop true
                    let rns, rds = gl |> loop false
                    lns @ rns, lds @ rds
            | _ -> if isNum then (g |> getGroups, []) else ([], g |> getGroups)

        loop true g


    /// <summary>
    /// Checks whether u1 contains
    /// the same unit groups as u2
    /// </summary>
    /// <example>
    /// eqsGroup (Mass (KiloGram 1N)) (Mass (Gram 1N)) = true
    /// // also (ml/kg)/hour = (ml/hour)/kg = true!
    /// let un1 =
    ///     CombiUnit
    ///      (CombiUnit (Volume (MilliLiter 1N), OpPer, Weight (WeightKiloGram 1N)), OpPer,
    ///        Time (Hour 1N))
    /// let un2 =
    ///     CombiUnit
    ///       (CombiUnit (Volume (MilliLiter 1N), OpPer, Time (Hour 1N)), OpPer,
    ///        Weight (WeightKiloGram 1N))
    /// eqsGroup un1 un2 = true
    /// </example>
    /// <remarks>
    /// Compares the flattened num/den groups: handles commutativity and
    /// associativity, but does not cancel common factors or drop Count.
    /// Reduction to the lowest terms is left to simplifyUnit upstream.
    /// </remarks>
    let eqsGroup u1 u2 =
        match u1, u2 with
        | ZeroUnit, NoUnit
        | NoUnit, ZeroUnit
        | _, _ when u1 = u2 -> true
        | _ ->
            let g1Num, g1Den = u1 |> unitToGroup |> numDenom
            let g2Num, g2Den = u2 |> unitToGroup |> numDenom

            g1Num |> List.sort = (g2Num |> List.sort)
            && g1Den |> List.sort = (g2Den |> List.sort)


    /// Returns group g as a string
    let toStringWithGeneralString b g =
        let rec str g s =
            match g with
            | Group.NoGroup -> ""
            | Group.ZeroGroup -> "Zero"
            | Group.GeneralGroup s -> if not b then "General" else $"General({s})"
            | Group.CountGroup -> "Count"
            | Group.MassGroup -> "Mass"
            | Group.DistanceGroup -> "Distance"
            | Group.VolumeGroup -> "Volume"
            | Group.TimeGroup -> "Time"
            | Group.MolarGroup -> "Molar"
            | Group.InterNatUnitGroup -> "InternationalUnit"
            | Group.WeightGroup -> "Weight"
            | Group.HeightGroup -> "Height"
            | Group.BSAGroup -> "BSA"
            | Group.EnergyGroup -> "Energy"
            | Group.CombiGroup(gl, op, gr) ->
                let gls = str gl s
                let grs = str gr s

                gls + (op |> opToStr) + grs

        str g ""


    /// Returns group g as a string with
    /// the specific General string specifier
    let toStringLong = toStringWithGeneralString true


    /// Returns group g as a string
    let toString = toStringWithGeneralString false


    let toStringDutch g =
        let rec str g s =
            match g with
            | Group.NoGroup -> ""
            | Group.ZeroGroup -> "Zero"
            | Group.GeneralGroup s -> s
            | Group.CountGroup -> "Aantal"
            | Group.MassGroup -> "Massa"
            | Group.DistanceGroup -> "Afstand"
            | Group.VolumeGroup -> "Volume"
            | Group.TimeGroup -> "Tijd"
            | Group.MolarGroup -> "Molair"
            | Group.InterNatUnitGroup -> "InternationalUnit"
            | Group.WeightGroup -> "Gewicht"
            | Group.HeightGroup -> "Lengte"
            | Group.BSAGroup -> "BSA"
            | Group.EnergyGroup -> "Energie"
            | Group.CombiGroup(gl, op, gr) ->
                let gls = str gl s
                let grs = str gr s

                gls + (op |> opToStr) + grs

        str g ""


    /// Get all the units that belong to a group in a list.
    /// Example: getGroupUnits MassGroup = [Mass (KiloGram 1N); Mass (Gram 1N); ...]
    let getGroupUnits =
        function
        | Group.NoGroup -> [ NoUnit ]
        | Group.ZeroGroup -> [ ZeroUnit ]
        | Group.GeneralGroup n -> [ (n, 1N) |> General ]
        | Group.CountGroup -> [ 1N |> Times |> Count ]
        | Group.MassGroup ->
            [
                1N |> KiloGram |> Mass
                1N |> Gram |> Mass
                1N |> MilliGram |> Mass
                1N |> MicroGram |> Mass
                1N |> NanoGram |> Mass
            ]
        | Group.DistanceGroup ->
            [
                1N |> Meter |> Distance
                1N |> CentiMeter |> Distance
                1N |> MilliMeter |> Distance
            ]
        | Group.VolumeGroup ->
            [
                1N |> Liter |> Volume
                1N |> DeciLiter |> Volume
                1N |> MilliLiter |> Volume
                1N |> MicroLiter |> Volume
            ]
        | Group.TimeGroup ->
            [
                1N |> Year |> Time
                1N |> Month |> Time
                1N |> Week |> Time
                1N |> Day |> Time
                1N |> Hour |> Time
                1N |> Minute |> Time
                1N |> Second |> Time
            ]
        | Group.MolarGroup -> [ 1N |> Mole |> Molar; 1N |> MilliMole |> Molar ]
        | Group.InterNatUnitGroup -> [ 1N |> MIU |> International; 1N |> IU |> International ]
        | Group.WeightGroup -> [ 1N |> WeightKiloGram |> Weight; 1N |> WeightGram |> Weight ]
        | Group.HeightGroup -> [ 1N |> HeightMeter |> Height; 1N |> HeightCentiMeter |> Height ]
        | Group.BSAGroup -> [ 1N |> M2 |> BSA ]
        | Group.EnergyGroup -> [ 1N |> Calorie |> Energy; 1N |> KiloCalorie |> Energy ]
        | Group.CombiGroup _ -> []


    /// <summary>
    /// Get all the units that belong to group
    /// or a combination of groups.
    /// </summary>
    /// <example>
    /// <code>
    /// getUnitCombinations (CombiGroup (MassGroup, OpPer, VolumeGroup))
    /// returns:
    ///   [CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N));
    ///    CombiUnit (Mass (KiloGram 1N), OpPer, Volume (DeciLiter 1N));
    ///     ...
    ///    CombiUnit (Mass (NanoGram 1N), OpPer, Volume (MilliLiter 1N));
    ///    CombiUnit (Mass (NanoGram 1N), OpPer, Volume (MicroLiter 1N))]
    /// </code>
    /// </example>
    let getUnitCombinations g =
        let rec get g =
            match g with
            | Group.CombiGroup(gl, op, gr) ->
                [
                    for ul in gl |> get do
                        for ur in gr |> get do
                            (ul, op, ur) |> CombiUnit
                ]
            | _ -> g |> getGroupUnits

        get g


    module internal GroupItem =

        type GroupItem =
            | GroupItem of Group
            | OperatorItem of Operator


        let toList g =
            let rec parse g acc =
                match g with
                | Group.CombiGroup(gl, op, gr) ->
                    let gll = parse gl acc
                    let grl = parse gr acc

                    gll @ [ (op |> OperatorItem) ] @ grl
                | _ -> (g |> GroupItem) :: acc

            parse g []
