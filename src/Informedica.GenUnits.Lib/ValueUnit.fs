namespace rec Informedica.GenUnits.Lib

#nowarn "40"

open MathNet.Numerics

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL


module Array =

    let removeBigRationalMultiples xs =
        if xs |> Array.isEmpty then
            xs
        else
            xs
            |> Array.fold
                (fun acc x1 ->
                    acc
                    |> Array.filter (fun x2 -> x1 = x2 || x2 |> BigRational.isMultiple x1 |> not)
                )
                xs


type Unit =
    | NoUnit
    | ZeroUnit
    | CombiUnit of Unit * Operator * Unit
    | General of (string * BigRational)
    | Count of CountUnit
    | Mass of MassUnit
    | Distance of DistanceUnit
    | Volume of VolumeUnit
    | Time of TimeUnit
    | Molar of MolarUnit
    | InterNatUnit of IUnit
    | Weight of WeightUnit
    | Height of HeightUnit
    | BSA of BSAUnit
// TODO: probably can simplify count to unitless without unit n
and CountUnit = Times of BigRational

and MassUnit =
    | KiloGram of BigRational
    | Gram of BigRational
    | MilliGram of BigRational
    | MicroGram of BigRational
    | NanoGram of BigRational

and DistanceUnit =
    | Meter of BigRational
    | CentiMeter of BigRational
    | MilliMeter of BigRational

and VolumeUnit =
    | Liter of BigRational
    | DeciLiter of BigRational
    | MilliLiter of BigRational
    | MicroLiter of BigRational
    | Droplet of BigRational

and TimeUnit =
    | Year of BigRational
    | Month of BigRational
    | Week of BigRational
    | Day of BigRational
    | Hour of BigRational
    | Minute of BigRational
    | Second of BigRational

and MolarUnit =
    | Mol of BigRational
    | MilliMol of BigRational
    | MicroMol of BigRational

and IUnit =
    | MIU of BigRational
    | IU of BigRational
    | MILLIIU of BigRational

and WeightUnit =
    | WeightKiloGram of BigRational
    | WeightGram of BigRational

and HeightUnit =
    | HeightMeter of BigRational
    | HeightCentiMeter of BigRational

and BSAUnit = M2 of BigRational

and Operator =
    | OpTimes
    | OpPer
    | OpPlus
    | OpMinus


type ValueUnit = ValueUnit of BigRational [] * Unit



module Group =


    type Group =
        | NoGroup
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
        | CombiGroup of (Group * Operator * Group)




module Units =


    type Localization =
        | English
        | Dutch


    type Verbal =
        | Long
        | Short


    type Language = { Eng: string; Dut: string }


    let getDutch (lang: Language) = lang.Dut


    let getEnglish (lang: Language) = lang.Eng


    type UnitDetails =
        {
            Unit: Unit
            Group: Group.Group
            Abbreviation: Language
            Name: Language
            Synonyms: string list
        }


    let apply f (ud: UnitDetails) = f ud


    let get = apply id


    let getUnit ud = (ud |> get).Unit


    let per = ValueUnit.per


    let create un gr ab nm sy =
        {
            Unit = un
            Group = gr
            Abbreviation = ab
            Name = nm
            Synonyms = sy
        }


    let createGeneral n v =
        let un = (n, v) |> General
        let ab = { Eng = n; Dut = n }
        let nm = { Eng = n; Dut = n }

        create un (Group.GeneralGroup n) ab nm []


    let getGroup ud = (ud |> get).Group


    let getName ud = (ud |> get).Name


    let getAbbreviation ud = (ud |> get).Abbreviation


    let getEnglishName = getName >> getEnglish


    let getDutchName = getName >> getDutch


    let getEnglishAbbreviation =
        getAbbreviation >> getEnglish


    let getDutchAbbreviation =
        getAbbreviation >> getDutch


    let getUnitString loc verb =
        match loc with
        | English ->
            match verb with
            | Short -> getEnglishAbbreviation
            | Long -> getEnglishName
        | Dutch ->
            match verb with
            | Short -> getDutchAbbreviation
            | Long -> getDutchName


    module General =

        let toGeneral = General
        let general n = (n, 1N) |> toGeneral


    module Count =

        let toCount = Count

        let nTimes n = n |> Times |> toCount

        let times = 1N |> nTimes


    module Mass =

        let toMass = Mass

        let nKiloGram n = n |> KiloGram |> toMass
        let nGram n = n |> Gram |> toMass
        let nMilliGram n = n |> MilliGram |> toMass
        let nMicroGram n = n |> MicroGram |> toMass
        let nNanoGram n = n |> NanoGram |> toMass

        let kiloGram = 1N |> nKiloGram
        let gram = 1N |> nGram
        let milliGram = 1N |> nMilliGram
        let microGram = 1N |> nMicroGram
        let nanoGram = 1N |> nNanoGram


    module Distance =

        let toDistance = Distance

        let nMeter n = n |> Meter |> toDistance
        let nCentiMeter n = n |> CentiMeter |> toDistance
        let nMilliMeter n = n |> MilliMeter |> toDistance

        let meter = 1N |> nMeter
        let centimeter = 1N |> nCentiMeter
        let millimeter = 1N |> nMilliMeter


    module Weight =

        let toWeight = Weight

        let nKiloGram n = n |> WeightKiloGram |> toWeight
        let nGram n = n |> WeightGram |> toWeight

        let kiloGram = 1N |> nKiloGram
        let gram = 1N |> nGram


    module Volume =

        let toVolume = Volume

        let nLiter n = n |> Liter |> toVolume
        let nDeciLiter n = n |> DeciLiter |> toVolume
        let nMilliLiter n = n |> MilliLiter |> toVolume
        let nMicroLiter n = n |> MicroLiter |> toVolume
        let nDroplet n = n |> Droplet |> toVolume

        let liter = 1N |> nLiter
        let deciLiter = 1N |> nDeciLiter
        let milliLiter = 1N |> nMilliLiter
        let microLiter = 1N |> nMicroLiter
        let droplet = 1N |> nDroplet


    module Time =

        let toTime = Time

        let nYear n = n |> Year |> toTime
        let nMonth n = n |> Month |> toTime
        let nWeek n = n |> Week |> toTime
        let nDay n = n |> Day |> toTime
        let nHour n = n |> Hour |> toTime
        let nMinute n = n |> Minute |> toTime
        let nSecond n = n |> Second |> toTime

        let year = 1N |> nYear
        let month = 1N |> nMonth
        let week = 1N |> nWeek
        let day = 1N |> nDay
        let hour = 1N |> nHour
        let minute = 1N |> nMinute
        let second = 1N |> nSecond


    module Molar =

        let toMolar = Molar

        let nMol n = n |> Mol |> toMolar
        let nMilliMol n = n |> MilliMol |> toMolar
        let nMicroMol n = n |> MicroMol |> toMolar

        let mol = 1N |> nMol
        let milliMol = 1N |> nMilliMol
        let microMol = 1N |> nMicroMol


    module InterNatUnit =

        let toInterNatUnit = InterNatUnit

        let nMIU n = n |> MIU |> toInterNatUnit
        let nIU n = n |> IU |> toInterNatUnit
        let nMilliIU n = n |> MILLIIU |> toInterNatUnit

        let mIU = 1N |> nMIU
        let iu = 1N |> nIU
        let milliIU = 1N |> nMilliIU


    module Height =

        let toHeight = Height

        let nMeter n = n |> HeightMeter |> toHeight
        let nCentiMeter n = n |> HeightCentiMeter |> toHeight

        let meter = 1N |> HeightMeter |> toHeight

        let centiMeter =
            1N |> HeightCentiMeter |> toHeight


    module BSA =

        let toBSA = BSA

        let nM2 n = n |> M2 |> toBSA

        let m2 = 1N |> nM2


    let units =
        [
            {
                Unit = Count.times
                Group = Group.NoGroup
                Abbreviation = { Eng = "x"; Dut = "x" }
                Name = { Eng = "times"; Dut = "keer" }
                Synonyms = []
            }

            {
                Unit = Mass.kiloGram
                Group = Group.NoGroup
                Abbreviation = { Eng = "kg"; Dut = "kg" }
                Name = { Eng = "kilogram"; Dut = "kilogram" }
                Synonyms = []
            }
            {
                Unit = Mass.gram
                Group = Group.NoGroup
                Abbreviation = { Eng = "g"; Dut = "g" }
                Name = { Eng = "gram"; Dut = "gram" }
                Synonyms = [ "gr" ]
            }
            {
                Unit = Mass.milliGram
                Group = Group.NoGroup
                Abbreviation = { Eng = "mg"; Dut = "mg" }
                Name = { Eng = "milligram"; Dut = "milligram" }
                Synonyms = [ "millig"; "milligr" ]
            }
            {
                Unit = Mass.microGram
                Group = Group.NoGroup
                Abbreviation = { Eng = "microg"; Dut = "microg" }
                Name = { Eng = "microgram"; Dut = "microgram" }
                Synonyms = [ "mcg"; "µg"; "mcgr" ]
            }
            {
                Unit = Mass.nanoGram
                Group = Group.NoGroup
                Abbreviation = { Eng = "nanog"; Dut = "nanog" }
                Name = { Eng = "nanogram"; Dut = "nanogram" }
                Synonyms = [ "nanogr"; "ng" ]
            }

            {
                Unit = Distance.meter
                Group = Group.NoGroup
                Abbreviation = { Eng = "m"; Dut = "m" }
                Name = { Eng = "meter"; Dut = "meter" }
                Synonyms = []
            }
            {
                Unit = Distance.centimeter
                Group = Group.NoGroup
                Abbreviation = { Eng = "cm"; Dut = "cm" }
                Name =
                    {
                        Eng = "centimeter"
                        Dut = "centimeter"
                    }
                Synonyms = []
            }
            {
                Unit = Distance.millimeter
                Group = Group.NoGroup
                Abbreviation = { Eng = "mm"; Dut = "mm" }
                Name =
                    {
                        Eng = "millimeter"
                        Dut = "millimeter"
                    }
                Synonyms = []
            }


            {
                Unit = Volume.liter
                Group = Group.NoGroup
                Abbreviation = { Eng = "l"; Dut = "l" }
                Name = { Eng = "liter"; Dut = "liter" }
                Synonyms = [ "ltr" ]
            }
            {
                Unit = Volume.deciLiter
                Group = Group.NoGroup
                Abbreviation = { Eng = "dl"; Dut = "dl" }
                Name = { Eng = "deciliter"; Dut = "deciliter" }
                Synonyms = [ "decil" ]
            }
            {
                Unit = Volume.milliLiter
                Group = Group.NoGroup
                Abbreviation = { Eng = "ml"; Dut = "mL" }
                Name =
                    {
                        Eng = "milliliter"
                        Dut = "milliliter"
                    }
                Synonyms = [ "millil" ]
            }
            {
                Unit = Volume.microLiter
                Group = Group.NoGroup
                Abbreviation = { Eng = "microL"; Dut = "microL" }
                Name =
                    {
                        Eng = "microliter"
                        Dut = "microliter"
                    }
                Synonyms = [ "µl" ]
            }
            {
                Unit = Volume.droplet
                Group = Group.NoGroup
                Abbreviation = { Eng = "droplet"; Dut = "druppel" }
                Name = { Eng = "droplet"; Dut = "druppel" }
                Synonyms = [ "drop"; "dr" ]
            }

            {
                Unit = Time.year
                Group = Group.NoGroup
                Abbreviation = { Eng = "yr"; Dut = "jaar" }
                Name = { Eng = "year"; Dut = "jaar" }
                Synonyms = [ "years"; "jaren" ]
            }
            {
                Unit = Time.month
                Group = Group.NoGroup
                Abbreviation = { Eng = "mo"; Dut = "maand" }
                Name = { Eng = "month"; Dut = "maand" }
                Synonyms = [ "months"; "maanden" ]
            }
            {
                Unit = Time.week
                Group = Group.NoGroup
                Abbreviation = { Eng = "week"; Dut = "week" }
                Name = { Eng = "week"; Dut = "week" }
                Synonyms = [ "weeks"; "weken" ]
            }
            {
                Unit = Time.day
                Group = Group.NoGroup
                Abbreviation = { Eng = "day"; Dut = "dag" }
                Name = { Eng = "day"; Dut = "dag" }
                Synonyms = [ "days"; "dagen" ]
            }
            {
                Unit = Time.hour
                Group = Group.NoGroup
                Abbreviation = { Eng = "hr"; Dut = "uur" }
                Name = { Eng = "hour"; Dut = "uur" }
                Synonyms = [ "hours"; "uren" ]
            }
            {
                Unit = Time.minute
                Group = Group.NoGroup
                Abbreviation = { Eng = "min"; Dut = "min" }
                Name = { Eng = "minute"; Dut = "minuut" }
                Synonyms = [ "minutes"; "minuten" ]
            }
            {
                Unit = Time.second
                Group = Group.NoGroup
                Abbreviation = { Eng = "sec"; Dut = "sec" }
                Name = { Eng = "second"; Dut = "seconde" }
                Synonyms = [ "s" ]
            }

            {
                Unit = Molar.mol
                Group = Group.NoGroup
                Abbreviation = { Eng = "mol"; Dut = "mol" }
                Name = { Eng = "mol"; Dut = "mol" }
                Synonyms = []
            }
            {
                Unit = Molar.milliMol
                Group = Group.NoGroup
                Abbreviation = { Eng = "mmol"; Dut = "mmol" }
                Name = { Eng = "millimol"; Dut = "millimol" }
                Synonyms = []
            }
            {
                Unit = Molar.microMol
                Group = Group.NoGroup
                Abbreviation = { Eng = "micromol"; Dut = "micromol" }
                Name = { Eng = "micromol"; Dut = "micromol" }
                Synonyms = [ "umol" ]
            }

            {
                Unit = InterNatUnit.iu
                Group = Group.NoGroup
                Abbreviation = { Eng = "IE"; Dut = "IE" }
                Name = { Eng = "IE"; Dut = "IE" }
                Synonyms = [ "E"; "U"; "IU" ]
            }
            {
                Unit = InterNatUnit.mIU
                Group = Group.NoGroup
                Abbreviation = { Eng = "miljIE"; Dut = "milj-IE" }
                Name =
                    {
                        Eng = "millionIE"
                        Dut = "miljoen-ie"
                    }
                Synonyms = [ "milj.IE"; "milj.E" ]
            }
            {
                Unit = InterNatUnit.milliIU
                Group = Group.NoGroup
                Abbreviation = { Eng = "milliIU"; Dut = "milliIE" }
                Name = { Eng = "milliIU"; Dut = "milliIE" }
                Synonyms =
                    [
                        "milli-internationale eenheid"
                        "mie"
                    ]
            }

            {
                Unit = Weight.kiloGram
                Group = Group.NoGroup
                Abbreviation = { Eng = "kg"; Dut = "kg" }
                Name = { Eng = "kilogram"; Dut = "kilogram" }
                Synonyms = []
            }
            {
                Unit = Weight.gram
                Group = Group.NoGroup
                Abbreviation = { Eng = "g"; Dut = "g" }
                Name = { Eng = "gram"; Dut = "gram" }
                Synonyms = [ "gr" ]
            }

            {
                Unit = BSA.m2
                Group = Group.NoGroup
                Abbreviation = { Eng = "m2"; Dut = "m2" }
                Name =
                    {
                        Eng = "square meter"
                        Dut = "vierkante meter"
                    }
                Synonyms = [ "m^2" ]
            }

        ]
        |> List.map (fun ud ->
            { ud with
                Group = ud.Unit |> ValueUnit.Group.unitToGroup
            }
        )


    /// Get the n value and unit but
    /// cannot do this with a combined unit!
    let nUnit =
        function
        | NoUnit -> (1N, NoUnit)
        | ZeroUnit -> (0N, ZeroUnit)
        | General (n, v) -> (v, ((n, 1N) |> General))
        | Count g ->
            match g with
            | Times n -> (n, Count.times)
        | Mass g ->
            match g with
            | KiloGram n -> (n, Mass.kiloGram)
            | Gram n -> (n, Mass.gram)
            | MilliGram n -> (n, Mass.milliGram)
            | MicroGram n -> (n, Mass.microGram)
            | NanoGram n -> (n, Mass.nanoGram)
        | Distance d ->
            match d with
            | Meter n -> (n, Distance.meter)
            | CentiMeter n -> (n, Distance.centimeter)
            | MilliMeter n -> (n, Distance.millimeter)
        | Volume g ->
            match g with
            | Liter n -> (n, Volume.liter)
            | DeciLiter n -> (n, Volume.deciLiter)
            | MilliLiter n -> (n, Volume.milliLiter)
            | MicroLiter n -> (n, Volume.microLiter)
            | Droplet n -> (n, Volume.droplet)
        | Time g ->
            match g with
            | Year n -> (n, Time.year)
            | Month n -> (n, Time.month)
            | Week n -> (n, Time.week)
            | Day n -> (n, Time.day)
            | Hour n -> (n, Time.hour)
            | Minute n -> (n, Time.minute)
            | Second n -> (n, Time.second)
        | Molar g ->
            match g with
            | Mol n -> (n, Molar.mol)
            | MilliMol n -> (n, Molar.milliMol)
            | MicroMol n -> (n, Molar.microMol)
        | InterNatUnit g ->
            match g with
            | MIU n -> (n, InterNatUnit.mIU)
            | IU n -> (n, InterNatUnit.iu)
            | MILLIIU n -> (n, InterNatUnit.milliIU)
        | Weight g ->
            match g with
            | WeightKiloGram n -> (n, Weight.kiloGram)
            | WeightGram n -> (n, Weight.gram)
        | Height g ->
            match g with
            | HeightMeter n -> (n, Height.meter)
            | HeightCentiMeter n -> (n, Height.centiMeter)
        | BSA g ->
            match g with
            | M2 n -> (n, BSA.m2)
        | CombiUnit (u1, op, u2) ->
            failwith
            <| $"Cannot map combined unit %A{(u1, op, u2) |> CombiUnit}"


    /// Try finds the unit details in
    /// a unit u
    let tryFind u =
        match units |> List.tryFind (fun udt -> udt.Unit = u) with
        | Some udt -> Some udt
        | None -> None


    /// Creates a Unit from a string s, if possible
    /// otherwise returns None. Note doesn't take care of
    /// the n value of a unit! So, for example, the unit
    /// 36 hour cannot be parsed correctly.
    let fromString s =
        match s |> String.splitAt '[' with
        | [| us; gs |] ->
            let gs = gs |> String.replace "]" ""

            let eqsUnit (udt: UnitDetails) =
                udt.Abbreviation.Dut |> String.equalsCapInsens us
                || udt.Abbreviation.Eng |> String.equalsCapInsens us
                || udt.Name.Dut |> String.equalsCapInsens us
                || udt.Name.Eng |> String.equalsCapInsens us
                || udt.Synonyms
                   |> List.exists (String.equalsCapInsens us)

            let eqsGroup (udt: UnitDetails) =
                udt.Group
                |> ValueUnit.Group.toString
                |> String.equalsCapInsens gs

            match units
                  |> List.tryFind (fun udt -> udt |> eqsUnit && udt |> eqsGroup)
                with
            | Some udt -> udt.Unit
            | None -> ValueUnit.generalUnit 1N s
            |> Some

        | _ -> None


    /// Turn a unit u to a string with
    /// localization loc and verbality verb.
    let toString loc verb u =
        let gtost u g =
            u + "[" + (g |> ValueUnit.Group.toString) + "]"

        let rec str u =
            match u with
            | NoUnit -> ""

            | CombiUnit (ul, op, ur) ->
                let uls = str ul
                let urs = str ur

                uls + (op |> ValueUnit.opToStr) + urs

            | General (n, v) ->
                let ustr = n // + "[General]"

                if v > 1N then
                    (1N |> BigRational.toString) + ustr
                else
                    ustr

            | _ ->
                let v, u = u |> nUnit

                match u |> tryFind with
                | Some udt ->
                    match loc with
                    | English ->
                        match verb with
                        | Short -> udt.Group |> gtost udt.Abbreviation.Eng
                        | Long -> udt.Group |> gtost udt.Name.Eng
                    | Dutch ->
                        match verb with
                        | Short -> udt.Group |> gtost udt.Abbreviation.Dut
                        | Long -> udt.Group |> gtost udt.Name.Dut
                | None -> ""
                |> (fun s ->
                    if s = "" then
                        ""
                    else if v = 1N then
                        s
                    else
                        (v |> BigRational.toString) + " " + s
                )

        str u


    let toStringDutchShort =
        toString Dutch Short

    let toStringDutchLong = toString Dutch Long

    let toStringEngShort =
        toString English Short

    let toStringEngLong = toString English Long





module ValueUnit =


    /// Transforms an operator to a string
    let opToStr op =
        match op with
        | OpPer -> "/"
        | OpTimes -> "x"
        | OpPlus -> "+"
        | OpMinus -> "-"


    /// Transforms an operator to a string
    /// (*, /, +, -), throws an error if
    /// no match
    let opFromString s =
        match s with
        | _ when s = "/" -> OpPer
        | _ when s = "*" -> OpPer
        | _ when s = "+" -> OpPer
        | _ when s = "-" -> OpPer
        | _ -> failwith <| $"Cannot parse %s{s} to operand"


    /// Apply a function f to the
    /// value of a unit
    let apply f u =
        let rec app u =
            match u with
            | NoUnit
            | ZeroUnit -> u
            | General (s, n) -> (s, n |> f) |> General
            | Count g ->
                match g with
                | Times n -> n |> f |> Times |> Count
            | Mass g ->
                match g with
                | KiloGram n -> n |> f |> KiloGram
                | Gram n -> n |> f |> Gram
                | MilliGram n -> n |> f |> MilliGram
                | MicroGram n -> n |> f |> MicroGram
                | NanoGram n -> n |> f |> NanoGram
                |> Mass
            | Distance d ->
                match d with
                | Meter n -> n |> f |> Meter
                | CentiMeter n -> n |> f |> CentiMeter
                | MilliMeter n -> n |> f |> MilliMeter
                |> Distance
            | Volume g ->
                match g with
                | Liter n -> n |> f |> Liter
                | DeciLiter n -> n |> f |> DeciLiter
                | MilliLiter n -> n |> f |> MilliLiter
                | MicroLiter n -> n |> f |> MicroLiter
                | Droplet n -> n |> f |> Droplet
                |> Volume
            | Time g ->
                match g with
                | Year n -> n |> f |> Year
                | Month n -> n |> f |> Month
                | Week n -> n |> f |> Week
                | Day n -> n |> f |> Day
                | Hour n -> n |> f |> Hour
                | Minute n -> n |> f |> Minute
                | Second n -> n |> f |> Second
                |> Time
            | Molar g ->
                match g with
                | Mol n -> n |> f |> Mol
                | MilliMol n -> n |> f |> MilliMol
                | MicroMol n -> n |> f |> MicroMol
                |> Molar
            | InterNatUnit g ->
                match g with
                | MIU n -> n |> f |> MIU
                | IU n -> n |> f |> IU
                | MILLIIU n -> n |> f |> MILLIIU
                |> InterNatUnit
            | Weight g ->
                match g with
                | WeightKiloGram n -> n |> f |> WeightKiloGram
                | WeightGram n -> n |> f |> WeightGram
                |> Weight
            | Height g ->
                match g with
                | HeightMeter n -> n |> f |> HeightMeter
                | HeightCentiMeter n -> n |> f |> HeightCentiMeter
                |> Height
            | BSA g ->
                match g with
                | M2 n -> n |> f |> M2 |> BSA
            | CombiUnit (u1, op, u2) -> (app u1, op, app u2) |> CombiUnit

        app u


    /// Change the value of a unit
    /// the the value n
    let setUnitValue n =
        let f = fun _ -> n
        apply f


    /// Get the value of the unit
    let getUnitValue u =
        let rec app u =
            match u with
            | NoUnit
            | ZeroUnit -> None
            | General (_, n) -> n |> Some
            | Count g ->
                match g with
                | Times n -> n |> Some
            | Mass g ->
                match g with
                | Gram n -> n |> Some
                | KiloGram n -> n |> Some
                | MilliGram n -> n |> Some
                | MicroGram n -> n |> Some
                | NanoGram n -> n |> Some
            | Distance d ->
                match d with
                | Meter n -> n |> Some
                | CentiMeter n -> n |> Some
                | MilliMeter n -> n |> Some
            | Volume g ->
                match g with
                | Liter n -> n |> Some
                | DeciLiter n -> n |> Some
                | MilliLiter n -> n |> Some
                | MicroLiter n -> n |> Some
                | Droplet n -> n |> Some
            | Time g ->
                match g with
                | Year n -> n |> Some
                | Month n -> n |> Some
                | Week n -> n |> Some
                | Day n -> n |> Some
                | Hour n -> n |> Some
                | Minute n -> n |> Some
                | Second n -> n |> Some
            | Molar g ->
                match g with
                | Mol n -> n |> Some
                | MilliMol n -> n |> Some
                | MicroMol n -> n |> Some
            | InterNatUnit g ->
                match g with
                | MIU n -> n |> Some
                | IU n -> n |> Some
                | MILLIIU n -> n |> Some
            | Weight g ->
                match g with
                | WeightKiloGram n -> n |> Some
                | WeightGram n -> n |> Some
            | Height g ->
                match g with
                | HeightMeter n -> n |> Some
                | HeightCentiMeter n -> n |> Some
            | BSA g ->
                match g with
                | M2 n -> n |> Some
            | CombiUnit _ -> None

        app u


    module Group =

        /// Transform a unit to the
        /// unit group
        let unitToGroup u =
            let rec get u =
                match u with
                | NoUnit
                | ZeroUnit -> Group.NoGroup
                | General (n, _) -> Group.GeneralGroup n
                | Count _ -> Group.CountGroup
                | Mass _ -> Group.MassGroup
                | Distance _ -> Group.DistanceGroup
                | Volume _ -> Group.VolumeGroup
                | Time _ -> Group.TimeGroup
                | Molar _ -> Group.MolarGroup
                | InterNatUnit _ -> Group.InterNatUnitGroup
                | Weight _ -> Group.WeightGroup
                | Height _ -> Group.HeightGroup
                | BSA _ -> Group.BSAGroup
                | CombiUnit (ul, op, ur) -> (get ul, op, get ur) |> Group.CombiGroup

            get u


        /// Check whether a group g1
        /// contains group g2, i.e.
        /// g1 |> contains g2 checks
        /// whether groupe g1 contains g2
        let contains g2 g1 =
            let rec cont g =
                match g with
                | Group.GeneralGroup _
                | Group.NoGroup
                | Group.CountGroup
                | Group.MassGroup
                | Group.DistanceGroup
                | Group.VolumeGroup
                | Group.TimeGroup
                | Group.MolarGroup
                | Group.InterNatUnitGroup
                | Group.WeightGroup
                | Group.HeightGroup
                | Group.BSAGroup -> g = g2
                | Group.CombiGroup (gl, _, gr) -> cont gl || cont gr

            cont g1


        /// Checks whether u1 contains
        /// the same unit groups as u2
        let eqsGroup u1 u2 =
            if u1 = u2 then
                true
            else
                let g1 = u1 |> unitToGroup
                let g2 = u2 |> unitToGroup

                g1 = g2


        /// Transforms a group g to a string
        let toString g =
            let rec str g s =
                match g with
                | Group.NoGroup -> ""
                | Group.GeneralGroup _ -> "General"
                | Group.CountGroup -> "Count"
                | Group.MassGroup -> "Mass"
                | Group.DistanceGroup -> "Distance"
                | Group.VolumeGroup -> "Volume"
                | Group.TimeGroup -> "Time"
                | Group.MolarGroup -> "Molar"
                | Group.InterNatUnitGroup -> "IUnit"
                | Group.WeightGroup -> "Weight"
                | Group.HeightGroup -> "Height"
                | Group.BSAGroup -> "BSA"
                | Group.CombiGroup (gl, op, gr) ->
                    let gls = str gl s
                    let grs = str gr s

                    gls + (op |> opToStr) + grs

            str g ""


        /// Get all the units that belong to a group in a list
        let getGroupUnits =
            function
            | Group.NoGroup -> [ NoUnit ]
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
            | Group.MolarGroup ->
                [
                    1N |> Mol |> Molar
                    1N |> MilliMol |> Molar
                ]
            | Group.InterNatUnitGroup ->
                [
                    1N |> MIU |> InterNatUnit
                    1N |> IU |> InterNatUnit
                ]
            | Group.WeightGroup ->
                [
                    1N |> WeightKiloGram |> Weight
                    1N |> WeightGram |> Weight
                ]
            | Group.HeightGroup ->
                [
                    1N |> HeightMeter |> Height
                    1N |> HeightCentiMeter |> Height
                ]
            | Group.BSAGroup -> [ 1N |> M2 |> BSA ]
            | Group.CombiGroup _ -> []


        /// Get all the units that belong to group
        /// or a combination of groups
        let getUnits g =
            let rec get g =
                match g with
                | Group.CombiGroup (gl, op, gr) ->
                    [
                        for ul in gl |> get do
                            for ur in gr |> get do
                                (ul, op, ur) |> CombiUnit
                    ]
                | _ -> g |> getGroupUnits

            get g


        module internal GroupItem =

            type Group = Group.Group

            type GroupItem =
                | GroupItem of Group
                | OperatorItem of Operator


            let toList g =
                let rec parse g acc =
                    match g with
                    | Group.CombiGroup (gl, op, gr) ->
                        let gll = parse gl acc
                        let grl = parse gr acc

                        gll @ [ (op |> OperatorItem) ] @ grl
                    | _ -> (g |> GroupItem) :: acc

                parse g []



    module Multipliers =

        let zero = 0N
        let one = 1N
        let kilo = 1000N
        let deci = 1N / 10N
        let centi = deci / 10N
        let milli = 1N / kilo
        let micro = milli / kilo
        let nano = micro / kilo

        let second = 1N
        let minute = 60N * second
        let hour = minute * minute
        let day = 24N * hour
        let week = 7N * day
        let year = (365N + (1N / 4N)) * day
        let month = year / 12N

        let inline toBase m v = v * m
        let inline toUnit m v = v / m


        /// Get the multiplier of a unit
        /// (also when this is a combination of units)
        let getMultiplier u =
            let rec get u m =
                match u with
                | NoUnit
                | ZeroUnit -> one
                | General (_, n) -> n * one
                | Count g ->
                    match g with
                    | Times n -> n * one
                | Mass g ->
                    match g with
                    | KiloGram n -> n * kilo
                    | Gram n -> n * one
                    | MilliGram n -> n * milli
                    | MicroGram n -> n * micro
                    | NanoGram n -> n * nano
                | Distance d ->
                    match d with
                    | Meter n -> n * one
                    | CentiMeter n -> n * centi
                    | MilliMeter n -> n * milli
                | Volume g ->
                    match g with
                    | Liter n -> n * one
                    | DeciLiter n -> n * deci
                    | MilliLiter n -> n * milli
                    | MicroLiter n -> n * micro
                    | Droplet n -> n * (milli / 20N)
                | Time g ->
                    match g with
                    | Year n -> n * year
                    | Month n -> n * month
                    | Week n -> n * week
                    | Day n -> n * day
                    | Hour n -> n * hour
                    | Minute n -> n * minute
                    | Second n -> n * second
                | Molar g ->
                    match g with
                    | Mol n -> n * one
                    | MilliMol n -> n * milli
                    | MicroMol n -> n * micro
                | InterNatUnit g ->
                    match g with
                    | MIU n -> n * kilo * kilo
                    | IU n -> n * one
                    | MILLIIU n -> n * milli
                | Weight g ->
                    match g with
                    | WeightKiloGram n -> n * kilo
                    | WeightGram n -> n * one
                | Height g ->
                    match g with
                    | HeightMeter n -> n * one
                    | HeightCentiMeter n -> n * centi
                | BSA g ->
                    match g with
                    | M2 n -> n * one
                | CombiUnit (u1, op, u2) ->
                    let m1 = get u1 m
                    let m2 = get u2 m

                    match op with
                    | OpTimes -> m1 * m2
                    | OpPer -> m1 / m2
                    | OpMinus
                    | OpPlus -> m

            get u 1N


    /// Create a ValueUnit from a value v
    /// (a bigrational array) and a unit u
    /// Makes sure there are nog duplicates.
    let create u v =
        (v |> Array.distinct |> Array.sort, u)
        |> ValueUnit


    /// An empty ValueUnit that has no value
    /// and no unit, i.e. an empty array with
    /// NoUnit.
    let empty = create NoUnit [||]


    /// Create a a ValueUnit from a single
    /// value v and a unit u
    let createSingle u v = [| v |] |> create u


    /// Creates a ValueUnit with syntax
    /// v |> WithUnit u
    let withUnit u v = v |> create u


    /// Creates a 'single value' ValueUnit with syntax
    /// v |> singleWithUnit u
    let singleWithUnit u v = [| v |] |> withUnit u


    /// create a ValueUnit with syntax like
    /// u |> withValue v
    let withValue v u = create u v


    /// create a 'single value' ValueUnit with syntax like
    /// u |> withSingleValue v
    let withSingleValue v u = [| v |] |> create u


    /// create a general unit with unit value n
    /// and string s
    let generalUnit n s = (s, n) |> General


    /// Create a general ValueUnit with unit value
    /// n general text s and value v
    let generalValueUnit v n s = create (generalUnit n s) v


    /// Create a general 'single' ValueUnit with unit value
    /// n general text s and single value v
    let generalSingleValueUnit v n s = generalValueUnit [| v |] n s


    /// Get the value and the unit of a ValueUnit
    let get (ValueUnit (v, u)) = v, u


    /// Get the value of a ValueUnit
    let getValue (ValueUnit (v, _)) = v


    let setValue v (ValueUnit (_, u)) = v |> create u


    let setSingleValue v = setValue [| v |]


    /// Get the unit of a ValueUnit
    let getUnit (ValueUnit (_, u)) = u


    /// Get the full unit group of a unit.
    /// For example mg[Mass]/ml[Volume] ->
    /// Mass/Volume
    let getGroup = getUnit >> Group.unitToGroup


    /// Check whether the unit is a count unit, i.e.
    /// belongs to the Count group
    let isCountUnit =
        Group.eqsGroup (1N |> Times |> Count)


    /// Checks whether a ValueUnit has an
    /// empty value
    let isEmpty = getValue >> Array.isEmpty


    let hasZeroUnit = getUnit >> ((=) ZeroUnit)


    /// Check whether a ValueUnit is a single value
    let isSingleValue =
        getValue >> Array.length >> ((=) 1)


    /// Convert a value to v to the
    /// base value of unit u.
    /// For example u = mg v = 1 -> 1/1000
    let valueToBase u v =
        v
        |> Multipliers.toBase (u |> Multipliers.getMultiplier)

    /// Get the value of a ValueUnit as
    /// a base value.
    /// For example ValueUnit(1000, mg) -> 1
    let toBaseValue (ValueUnit (v, u)) = v |> Array.map (valueToBase u)


    /// Convert a value to v to the
    /// unit value of unit u.
    /// For example u = mg v = 1 -> 1000
    let valueToUnit u v =
        v
        |> Multipliers.toUnit (u |> Multipliers.getMultiplier)


    /// Get the value of a ValueUnit as
    /// a unit value ValueUnit(1, mg) -> 1000
    let toUnitValue (ValueUnit (v, u)) = v |> Array.map (valueToUnit u)


    /// Transforms a ValueUnit to its base.
    /// For example ValueUnit(1000, mg) -> ValueUnit(1, mg)
    let toBase vu =
        let v, u = vu |> get
        v |> Array.map (valueToBase u) |> create u


    /// Transforms a ValueUnit to its unit.
    /// For example ValueUnit(1, mg) -> ValueUnit(1000, mg)
    let toUnit vu =
        let v, u = vu |> get
        v |> Array.map (valueToUnit u) |> create u


    /// Create a 'zero' with unit u
    let zero u = [| 0N |] |> create u


    /// Create a 'one' with unit u
    let one u = [| 1N |] |> create u


    let setZeroNonNegative vu =
        if vu |> getUnit = NoUnit then ZeroUnit |> zero
        else
            let vu = vu |> filter (fun br -> br > 0N)

            if vu |> isEmpty |> not then
                vu
            else
                vu |> setValue [| 0N |]


    /// A 'count' unit with n = 1
    let count = 1N |> Times |> Count


    /// Checks whether vu1 is of the
    /// same unit group as vu2
    let eqsGroup vu1 vu2 =
        let u1 = vu1 |> getUnit
        let u2 = vu2 |> getUnit
        u1 |> Group.eqsGroup u2



    /// Create a CombiUnit with u1, Operator op and unit u2.
    /// Recalculates the unit n values. Takes care of dividing
    /// by the same unitgroups and multipying with count groups.
    let createCombiUnit (u1, op, u2) = // ToDo: need to check if this is correct!!
        match u1, u2 with
        | NoUnit, NoUnit -> NoUnit
        | ZeroUnit, ZeroUnit -> ZeroUnit
        | _ ->
            match op with
            | OpPer ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup ZeroUnit -> u2
                // this is not enough when u2 is combiunit but
                // contains u1!
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = (u1 |> getUnitValue)
                    let n2 = (u2 |> getUnitValue)

                    match n1, n2 with
                    | Some x1, Some x2 -> count |> setUnitValue (x1 / x2)
                    | _ -> count
                | _ when u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> getUnitValue
                    let n2 = u2 |> getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> setUnitValue (x1 / x2)
                    | _ -> u1
                | _ -> (u1, OpPer, u2) |> CombiUnit
            | OpTimes ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup ZeroUnit -> u2
                | _ when u2 |> Group.eqsGroup ZeroUnit -> u1
                | _ when
                    u1 |> Group.eqsGroup count
                    && u2 |> Group.eqsGroup count
                    ->
                    let n1 = u1 |> getUnitValue
                    let n2 = u2 |> getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> setUnitValue (x1 * x2)
                    | _ -> u1
                | _ when u1 |> Group.eqsGroup count ->
                    let n1 = u1 |> getUnitValue
                    let n2 = u2 |> getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u2 |> setUnitValue (x1 * x2)
                    | _ -> u2
                | _ when u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> getUnitValue
                    let n2 = u2 |> getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> setUnitValue (x1 * x2)
                    | _ -> u1
                | _ -> (u1, OpTimes, u2) |> CombiUnit
            | OpPlus
            | OpMinus ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = u1 |> getUnitValue
                    let n2 = u2 |> getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> setUnitValue (x1 + x2)
                    | _ -> u1
                | _ -> (u1, op, u2) |> CombiUnit


    let per u2 u1 = (u1, OpPer, u2) |> createCombiUnit


    let times u2 u1 = (u1, OpTimes, u2) |> createCombiUnit


    let plus u2 u1 =
        match u2, u1 with
        | ZeroUnit, u
        | u, ZeroUnit -> u
        | _ -> (u1, OpPlus, u2) |> createCombiUnit


    let minus u2 u1 =
        match u2, u1 with
        | ZeroUnit, u
        | u, ZeroUnit -> u
        | _ -> (u1, OpMinus, u2) |> createCombiUnit


    /// Checks wheter u1 has a unit u2
    let hasUnit u2 u1 =
        let rec has u =
            match u with
            | CombiUnit (lu, _, ru) ->
                if lu = u2 || ru = u2 then
                    true
                else
                    has lu || (has ru)
            | _ -> u = u2

        has u1


    /// Checks whether unit u
    /// is not a CombiUnit
    let notCombiUnit u =
        match u with
        | CombiUnit _ -> false
        | _ -> true


    module private UnitItem =

        type UnitItem =
            | UnitItem of Unit
            | OpPlusMinItem of Operator
            | OpMultItem of Operator
            | OpDivItem of Operator


        let listToUnit ul =
            let rec toUnit ul u =
                match ul with
                | [] -> u
                | ui :: rest ->
                    match u with
                    | NoUnit ->
                        match ui with
                        | UnitItem u' -> u'
                        | _ -> NoUnit
                        |> toUnit rest
                    | _ ->
                        match ul with
                        | oi :: ui :: rest ->
                            match oi, ui with
                            | OpDivItem op, UnitItem ur
                            | OpPlusMinItem op, UnitItem ur
                            | OpMultItem op, UnitItem ur -> createCombiUnit (u, op, ur) |> toUnit rest
                            | _ -> u
                        | _ -> u

            toUnit ul NoUnit


    /// Get a list of the units in a unit u
    let rec getUnits u =
        match u with
        | CombiUnit (ul, _, ur) -> ul |> getUnits |> List.append (ur |> getUnits)
        | _ -> [ u ]


    /// Simplify a ValueUnit vu such that
    /// units are algebraically removed or
    /// transformed to count units, where applicable.
    let simplify vu =
        let u = vu |> getUnit

        let simpl u =
            // separate numerators from denominators
            let rec numDenom b u =
                match u with
                | CombiUnit (ul, OpTimes, ur) ->
                    let lns, lds = ul |> numDenom b
                    let rns, rds = ur |> numDenom b
                    lns @ rns, lds @ rds

                | CombiUnit (ul, OpPer, ur) ->
                    if b then
                        let lns, lds = ul |> numDenom true
                        let rns, rds = ur |> numDenom false
                        lns @ rns, lds @ rds
                    else
                        let lns, lds = ur |> numDenom true
                        let rns, rds = ul |> numDenom false
                        lns @ rns, lds @ rds
                | _ ->
                    if b then
                        (u |> getUnits, [])
                    else
                        ([], u |> getUnits)
            // build a unit from a list of numerators and denominators
            let rec build ns ds (b, u) =
                match ns with
                | [] ->
                    match ds with
                    | [] -> (b, u)
                    | _ ->
                        // TODO Was the List.rev needed here (times is commutative?)
                        let d = ds |> List.reduce times

                        if u = NoUnit then
                            Count(Times 1N) |> per d
                        else
                            u |> per d
                        |> fun u -> (b, u)
                | h :: tail ->
                    if ds |> List.exists (Group.eqsGroup h) then
                        build tail (ds |> List.removeFirst (Group.eqsGroup h)) (true, u)
                    else
                        let b =
                            b
                            || ((u |> Group.eqsGroup count)
                                || (h |> Group.eqsGroup count))

                        if u = NoUnit then h else u |> times h
                        |> fun u -> build tail ds (b, u)

            let ns, ds = u |> numDenom true

            (false, NoUnit)
            |> build ns ds
            |> (fun (b, u) ->
                if u = NoUnit then
                    (b, count)
                else
                    (b, u)
            )

        if u = NoUnit then
            vu
        else
            u
            |> simpl
            |> (fun (b, u') ->
                vu
                |> toBaseValue
                |> create (if b then u' else u)
                |> toUnitValue
                |> create (if b then u' else u)
            )


    /// Calculate a ValueUnit by applying an operator op
    /// to ValueUnit vu1 and vu2. The operator can be addition,
    /// subtraction, multiplication or division.
    /// The boolean b results in whether or not the result is
    /// simplified.
    let calc b op vu1 vu2 =

        let (ValueUnit (_, u1)) = vu1
        let (ValueUnit (_, u2)) = vu2
        // calculate value in base
        let v =
            let vs1 = vu1 |> toBaseValue
            let vs2 = vu2 |> toBaseValue

            Array.allPairs vs1 vs2
            |> Array.map (fun (v1, v2) -> v1 |> op <| v2)
        // calculate new combi unit
        let u =
            match op with
            | BigRational.Mult -> u1 |> times u2
            | BigRational.Div -> u1 |> per u2
            | BigRational.Add
            | BigRational.Subtr ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup u2 -> u2
                // Special case when one value is a dimensionless zero
                | ZeroUnit, u
                | u, ZeroUnit -> u
                // Otherwise fail
                | _ ->
                    failwith
                    <| $"cannot add or subtract different units %A{u1} %A{u2}"
        // recreate valueunit with base value and combined unit
        v
        |> create u
        // calculate to the new combiunit
        |> toUnitValue
        // recreate again to final value unit
        |> create u
        |> fun vu -> if b then vu |> simplify else vu


    /// Compare a ValueUnit vu1 with vu2.
    /// Comparison can be:
    /// greater
    /// greater or equal
    /// smaller
    /// smaller or equal
    /// doesn't work for equal!!
    let cmp cp vu1 vu2 =
        // ToDo need better eqsGroup like mg/kg/day = (mg/kg)/day = (mg/kg*day) <> mg/(kg/day) = mg*day/kg
        //if vu1 |> eqsGroup vu2 |> not then false
        //else
        let vs1 = vu1 |> toBaseValue
        let vs2 = vu2 |> toBaseValue

        Array.allPairs vs1 vs2
        |> Array.forall (fun (v1, v2) -> v1 |> cp <| v2)


    let eqs vu1 vu2 =
        let vs1 =
            vu1 |> toBaseValue |> Array.distinct |> Array.sort

        let vs2 =
            vu2 |> toBaseValue |> Array.distinct |> Array.sort

        vs1 = vs2


    /// Apply a function fValue to the Value
    /// of a ValueUnit vu and return the transformed
    /// ValueUnit
    let applyToValue fValue vu =
        let u = vu |> getUnit
        vu |> getValue |> fValue |> create u


    /// Apply a function fValue to the Value
    /// of a ValueUnit vu and return the transformed
    /// ValueUnit. The fValue can use a default value
    /// defVal.
    let applyToValues fArr defVal vu =
        let u = vu |> getUnit
        vu |> getValue |> fArr defVal |> create u


    /// Filter the values in a ValueUnit
    let filterValues =
        applyToValues Array.filter


    /// Map the individual values in a ValueUnit
    let mapValues = applyToValues Array.map


    /// Validates a Value using fValid and return
    /// a result with an errMsg if not valid.
    let validate fValid errMsg vu =
        if vu |> getValue |> fValid then
            vu |> Ok
        else
            errMsg |> Error



    let eq = cmp (=)


    let gt = cmp (>)


    let st = cmp (<)


    let gte = cmp (>=)


    let ste = cmp (<=)


    /// Returns a operator for comparison to a string
    let cmpToStr cp =
        let z = 1N |> Times |> Count |> zero
        let o = 1N |> Times |> Count |> one

        match cp with
        | _ when
            (z |> cp <| z)
            && not (z |> cp <| o)
            && not (o |> cp <| z)
            ->
            "="
        | _ when
            (z |> cp <| z)
            && (z |> cp <| o)
            && not (o |> cp <| z)
            ->
            "<="
        | _ when
            (z |> cp <| z)
            && not (z |> cp <| o)
            && (o |> cp <| z)
            ->
            ">="
        | _ when
            not (z |> cp <| z)
            && (z |> cp <| o)
            && not (o |> cp <| z)
            ->
            "<"
        | _ when
            not (z |> cp <| z)
            && not (z |> cp <| o)
            && (o |> cp <| z)
            ->
            ">"
        | _ -> "unknown comparison"


    /// Convert a ValueUnit vu to
    /// a unit u.
    /// For example 1 gram -> 1000 mg:
    /// ValueUnit(1, Gram) |> convertTo Milligram
    /// Do not convert to no unit or zerounit
    let convertTo u vu =
        let _, oldU = vu |> get

        if u = oldU || u = NoUnit || u = ZeroUnit then
            vu
        else
            vu
            |> toBaseValue
            |> create u
            |> toUnitValue
            |> create u


    let getBaseValue = toBase >> getValue


    let isZero =
        getValue >> Array.forall ((=) 0N)

    let gtZero =
        getValue >> Array.forall ((<) 0N)

    let gteZero =
        getValue >> Array.forall ((<=) 0N)

    let stZero =
        getValue >> Array.forall ((>) 0N)

    let steZero =
        getValue >> Array.forall ((>=) 0N)


    let minElement vu =
        if vu |> isEmpty then None
        else
            vu
            |> applyToValue (Array.min >> Array.singleton)
            |> Some


    let maxElement vu =
        if vu |> isEmpty then None
        else
            vu
            |> applyToValue (Array.max >> Array.singleton)
            |> Some


    let multipleOf f incr vu =
        vu
        |> toBase
        |> applyToValue (fun vs ->
            let incr =
                incr |> getBaseValue |> Set.ofArray

            vs |> Array.map (f incr) |> Array.map snd
        )
        |> toUnit


    let minInclMultipleOf =
        multipleOf BigRational.minInclMultipleOf

    let minExclMultipleOf =
        multipleOf BigRational.minExclMultipleOf


    let maxInclMultipleOf =
        multipleOf BigRational.maxInclMultipleOf

    let maxExclMultipleOf =
        multipleOf BigRational.maxExclMultipleOf


    let denominator =
        getValue >> (Array.map BigRational.denominator)

    let numerator =
        getValue >> (Array.map BigRational.numerator)


    let filter pred =
        toBase
        >> applyToValue (Array.filter pred)
        >> toUnit

    let removeBigRationalMultiples =
        toBase
        >> applyToValue (Array.removeBigRationalMultiples)
        >> toUnit


    let intersect vu1 vu2 =
        vu1
        |> toBase
        |> applyToValue (fun vs ->
            vu2
            |> getBaseValue
            |> Set.ofArray
            |> Set.intersect (vs |> Set.ofArray)
            |> Set.toArray
        )
        |> toUnit


    let isSubset vu1 vu2 =
        let s1 = vu1 |> getBaseValue |> Set.ofArray
        let s2 = vu2 |> getBaseValue |> Set.ofArray
        Set.isSubset s1 s2


    let containsValue vu2 vu1 =
        vu2
        |> toBase
        |> getValue
        |> Array.forall (fun v -> vu1 |> toBase |> getValue |> Array.exists ((=) v))


    let takeFirst n = applyToValue (Array.take n)


    let takeLast n =
        applyToValue (Array.rev >> Array.take n >> Array.rev)


    // ToDo replace with this
    let valueCount = getValue >> Array.length


    let toStr exact =
        if exact then
            toStringDutchShort
            // getValue
            // >> Array.toReadableString
            >> String.removeBrackets
        else
            toReadableDutchStringWithPrec 3


    /// Get the user readable string version
    /// of a unit, i.e. without unit group between
    /// brackets
    let unitToReadableDutchString u =
        u
        |> Units.toString Units.Dutch Units.Short
        |> String.removeBrackets


    /// Turn a ValueUnit vu to a string using
    /// a bigrational to string brf, localization
    /// loc and verbality verb.
    let toString brf loc verb vu =
        let v, u = vu |> get

        $"{v |> Array.map brf |> Array.toReadableString} {Units.toString loc verb u}"


    let toStringDutchShort =
        toString BigRational.toString Units.Dutch Units.Short

    let toStringDutchLong =
        toString BigRational.toString Units.Dutch Units.Long

    let toStringEngShort =
        toString BigRational.toString Units.English Units.Short

    let toStringEngLong =
        toString BigRational.toString Units.English Units.Long

    let toStringFloatDutchShort =
        toString (BigRational.toDecimal >> string) Units.Dutch Units.Short

    let toStringFloatDutchLong =
        toString (BigRational.toDecimal >> string) Units.Dutch Units.Long

    let toStringFloatEngShort =
        toString (BigRational.toDecimal >> string) Units.English Units.Short

    let toStringFloatEngLong =
        toString (BigRational.toDecimal >> string) Units.English Units.Long


    /// Turn a `ValueUnit` `vu` into
    /// a string using precision `prec`.
    let toReadableDutchStringWithPrec prec vu =
        let v, u = vu |> get

        let vs =
            v
            |> Array.map BigRational.toDecimal
            |> Array.map (Decimal.fixPrecision prec)
            |> Array.toReadableString

        let us = u |> unitToReadableDutchString

        vs + " " + us


    /// Parse a string into a ValueUnit
    let fromString s =

        let fs s =
            let dels = "#"

            let ufs s =
                // ToDo doesn't work with units with spaces
                match s |> String.trim |> String.split " " with
                | [ ug ] ->
                    match Units.fromString ug with
                    | Some u' -> u' |> setUnitValue 1N
                    | None -> failwith <| $"Not a valid unit: %s{ug}"

                | [ v; ug ] ->
                    match v |> BigRational.tryParse with
                    | None ->
                        failwith
                        <| $"Cannot parse string: %s{s} with value: %s{v}"
                    | Some v' ->
                        match Units.fromString ug with
                        | Some u' -> u' |> setUnitValue v'
                        | None -> failwith <| $"Not a valid unit: %s{ug}"
                | _ -> failwith <| $"Cannot parse string %s{s}"

                |> UnitItem.UnitItem

            let rec parse ul usl =

                match usl with
                | [] -> ul
                | [ us ] -> ul @ [ us |> ufs ]

                | us :: os :: rest ->
                    let ui = us |> ufs

                    let oi =
                        let o = os |> opFromString

                        match o with
                        | OpPer -> o |> UnitItem.OpDivItem
                        | OpTimes -> o |> UnitItem.OpMultItem
                        | OpPlus
                        | OpMinus -> o |> UnitItem.OpPlusMinItem

                    rest |> parse (ul @ [ ui; oi ])

            s
            |> String.replace "*" (dels + "*" + dels)
            |> String.replace "/" (dels + "/" + dels)
            |> String.replace "+" (dels + "+" + dels)
            |> String.replace "-" (dels + "-" + dels)
            |> String.split dels
            |> parse []
            |> UnitItem.listToUnit

        match s |> String.split " " with
        | vs :: rest ->
            match vs |> BigRational.tryParse with
            | None -> failwith <| $"Cannot parse string %s{s}"
            | Some v ->
                let u =
                    rest |> String.concat " " |> String.trim |> fs

                ([| v |], u) |> ValueUnit
        | _ ->
            if s = "" then
                failwith "Cannot parse empty string"
            else
                failwith <| $"Cannot parse string %s{s}"


    module Operators =

        let (=?) vu1 vu2 = eqs vu1 vu2

        let (>?) vu1 vu2 = cmp (>) vu1 vu2

        let (<?) vu1 vu2 = cmp (<) vu1 vu2

        let (>=?) vu1 vu2 = cmp (>=) vu1 vu2

        let (<=?) vu1 vu2 = cmp (<=) vu1 vu2

        let (==>) vu u = vu |> convertTo u



    module Dto =


        [<Literal>]
        let english = "english"

        [<Literal>]
        let dutch = "dutch"

        type Dto() =
            member val Value = [||] with get, set
            member val Unit = "" with get, set
            member val Group = "" with get, set
            member val Short = true with get, set
            member val Language = "" with get, set

        let dto () = Dto()

        let toString (dto: Dto) = $"%A{dto.Value} %s{dto.Unit}"

        let toDto short lang vu =
            let isLang s l =
                l
                |> String.trim
                |> String.toLower
                |> (fun l -> s |> String.startsWith l)

            let l =
                match lang with
                | _ when lang |> isLang english -> Units.English |> Some
                | _ when lang |> isLang dutch -> Units.Dutch |> Some
                | _ -> None

            match l with
            | None -> None
            | Some l ->
                let s =
                    if short then
                        Units.Short
                    else
                        Units.Long

                let v, u = vu |> get
                let v = v |> Array.map BigRational.toDecimal

                let g =
                    u |> Group.unitToGroup |> Group.toString

                let u =
                    u |> Units.toString l s |> String.removeBrackets

                let dto = dto ()
                dto.Value <- v
                dto.Unit <- u
                dto.Group <- g
                dto.Language <- lang
                dto.Short <- short

                dto |> Some

        let toDtoDutchShort vu = vu |> toDto true dutch |> Option.get
        let toDtoDutchLong vu = vu |> toDto false dutch |> Option.get
        let toDtoEnglishShort vu = vu |> toDto true english |> Option.get
        let toDtoEnglishLong vu = vu |> toDto false english |> Option.get

        let fromDto (dto: Dto) =
            let v =
                dto.Value |> Array.map BigRational.fromDecimal

            if dto.Group |> String.isNullOrWhiteSpace then
                try
                    $"1 {dto.Unit}"
                    |> fromString
                    |> setValue v
                    |> Some
                with
                | _ -> None
            else
                $"%s{dto.Unit}[%s{dto.Group}]"
                |> Units.fromString
                |> function
                    | Some u -> v |> create u |> Some
                    | _ -> None






type ValueUnit with

    static member (*)(vu1, vu2) = ValueUnit.calc true (*) vu1 vu2

    static member (/)(vu1, vu2) = ValueUnit.calc true (/) vu1 vu2

    static member (+)(vu1, vu2) = ValueUnit.calc true (+) vu1 vu2

    static member (-)(vu1, vu2) = ValueUnit.calc true (-) vu1 vu2

    static member (=?)(vu1, vu2) = ValueUnit.eqs vu1 vu2

    static member (>?)(vu1, vu2) = ValueUnit.cmp (>) vu1 vu2

    static member (<?)(vu1, vu2) = ValueUnit.cmp (<) vu1 vu2

    static member (>=?)(vu1, vu2) = ValueUnit.cmp (>=) vu1 vu2

    static member (<=?)(vu1, vu2) = ValueUnit.cmp (<=) vu1 vu2

    static member (==>)(vu, u) = vu |> ValueUnit.convertTo u