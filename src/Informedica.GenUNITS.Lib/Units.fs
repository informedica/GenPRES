namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib.Types

open Informedica.Utils.Lib.BCL


module Units =

    type Localization =
        | English
        | Dutch


    type Verbal =
        | Long
        | Short


    type Language =
        {
            Eng: string
            EngPlural: string
            Dut: string
            DutchPlural: string
        }


    /// <summary>
    /// Get the Dutch language string
    /// </summary>
    /// <param name="lang"></param>
    /// <example>
    /// getDutch { Eng = "abc"; EngPlural = "abcs"; Dut = "def"; DutchPlural = "defs" } = "def"
    /// </example>
    let getDutch (lang: Language) = lang.Dut


    /// <summary>
    /// Get the English language string
    /// </summary>
    /// <param name="lang"></param>
    /// <example>
    /// getEnglish { Eng = "abc"; EngPlural = "abcs"; Dut = "def"; DutchPlural = "defs" } = "abc"
    /// </example>
    let getEnglish (lang: Language) = lang.Eng


    type UnitDetails =
        {
            Unit: Unit
            Group: Group
            Abbreviation: Language
            Name: Language
            Synonyms: string list
        }


    module General =

        /// create a general unit
        let toGeneral = General

        /// create a general unit with unit value = 1
        let general n =
            if n |> String.isNullOrWhiteSpace then
                invalidArg (nameof n) "the name of a General Unit cannot be an empty string"

            (n, 1N) |> toGeneral


    module Count =

        module Constants =

            [<Literal>]
            let Times = "x"

        /// Create a Count unit
        let toCount = Count

        /// Create a Count unit with unit value = n
        let nTimes n = n |> Times |> toCount
        /// Create a Count unit with unit value = 1
        let times = 1N |> nTimes


    module Mass =

        module Constants =

            [<Literal>]
            let KiloGram = "kg"

            [<Literal>]
            let Gram = "g"

            [<Literal>]
            let MilliGram = "mg"

            [<Literal>]
            let MicroGram = "microg"

            [<Literal>]
            let NanoGram = "nanog"


        /// Create a Mass unit
        let toMass = Mass

        /// Create a Mass unit kilogram with unit value = n
        let nKiloGram n = n |> KiloGram |> toMass
        /// Create a Mass unit gram with unit value = n
        let nGram n = n |> Gram |> toMass
        /// Create a Mass unit milligram with unit value = n
        let nMilliGram n = n |> MilliGram |> toMass
        /// Create a Mass unit microgram with unit value = n
        let nMicroGram n = n |> MicroGram |> toMass
        /// Create a Mass unit nanogram with unit value = n
        let nNanoGram n = n |> NanoGram |> toMass

        /// Create a Mass unit kilogram with unit value = 1
        let kiloGram = 1N |> nKiloGram
        /// Create a Mass unit gram with unit value = 1
        let gram = 1N |> nGram
        /// Create a Mass unit milligram with unit value = 1
        let milliGram = 1N |> nMilliGram
        /// Create a Mass unit microgram with unit value = 1
        let microGram = 1N |> nMicroGram
        /// Create a Mass unit nanogram with unit value = 1
        let nanoGram = 1N |> nNanoGram


    module Distance =

        module Constants =

            [<Literal>]
            let Meter = "m"

            [<Literal>]
            let CentiMeter = "cm"

            [<Literal>]
            let MilliMeter = "mm"

        /// Create a Distance unit
        let toDistance = Distance

        /// Create a Distance unit meter with unit value = n
        let nMeter n = n |> Meter |> toDistance
        /// Create a Distance unit centimeter with unit value = n
        let nCentiMeter n = n |> CentiMeter |> toDistance
        /// Create a Distance unit millimeter with unit value = n
        let nMilliMeter n = n |> MilliMeter |> toDistance

        /// Create a Distance unit meter with unit value = 1
        let meter = 1N |> nMeter
        /// Create a Distance unit centimeter with unit value = 1
        let centimeter = 1N |> nCentiMeter
        /// Create a Distance unit millimeter with unit value = 1
        let millimeter = 1N |> nMilliMeter


    module Weight =

        module Constants =

            [<Literal>]
            let KiloGram = "kg"

            [<Literal>]
            let Gram = "g"


        /// Create a Weight unit
        let toWeight = Weight

        /// Create a Weight unit kilogram with unit value = n
        let nKiloGram n = n |> WeightKiloGram |> toWeight
        /// Create a Weight unit gram with unit value = n
        let nGram n = n |> WeightGram |> toWeight

        /// Create a Weight unit kilogram with unit value = 1
        let kiloGram = 1N |> nKiloGram
        /// Create a Weight unit gram with unit value = 1
        let gram = 1N |> nGram


    module Volume =

        module Constants =

            [<Literal>]
            let Liter = "l"

            [<Literal>]
            let DeciLiter = "dl"

            [<Literal>]
            let MilliLiter = "ml"

            [<Literal>]
            let MicroLiter = "microL"

            [<Literal>]
            let Droplet = "droplet"


        /// Create a Volume unit
        let toVolume = Volume

        /// Create a Volume unit liter with unit value = n
        let nLiter n = n |> Liter |> toVolume
        /// Create a Volume unit deciliter with unit value = n
        let nDeciLiter n = n |> DeciLiter |> toVolume
        /// Create a Volume unit milliliter with unit value = n
        let nMilliLiter n = n |> MilliLiter |> toVolume
        /// Create a Volume unit microliter with unit value = n
        let nMicroLiter n = n |> MicroLiter |> toVolume
        /// Create a Volume unit droplet with unit value = n
        let nDroplet n = (n, 20N) |> Droplet |> toVolume

        /// Create a Volume unit liter with unit value = 1
        let liter = 1N |> nLiter
        /// Create a Volume unit deciliter with unit value = 1
        let deciLiter = 1N |> nDeciLiter
        /// Create a Volume unit milliliter with unit value = 1
        let milliLiter = 1N |> nMilliLiter
        /// Create a Volume unit microliter with unit value = 1
        let microLiter = 1N |> nMicroLiter
        /// Create a Volume unit droplet with unit value = 1
        let droplet = 1N |> nDroplet
        /// Default is 20 drops per mL, however this can vary
        let dropletWithDropsPerMl m = (1N, m) |> Droplet |> Volume

        /// Set the multiplier of a droplet unit
        let dropletSetDropsPerMl m dr =
            match dr with
            | Volume(Droplet(n, _)) -> (n, m) |> Droplet |> Volume
            | _ -> dr


    module Time =


        module Constants =

            [<Literal>]
            let Year = "yr"

            [<Literal>]
            let Month = "mo"

            [<Literal>]
            let Week = "week"

            [<Literal>]
            let Day = "day"

            [<Literal>]
            let Hour = "hr"

            [<Literal>]
            let Minute = "min"

            [<Literal>]
            let Second = "sec"


        /// Create a Time unit
        let toTime = Time

        /// Create a Time unit year with unit value = n
        let nYear n = n |> Year |> toTime
        /// Create a Time unit month with unit value = n
        let nMonth n = n |> Month |> toTime
        /// Create a Time unit week with unit value = n
        let nWeek n = n |> Week |> toTime
        /// Create a Time unit day with unit value = n
        let nDay n = n |> Day |> toTime
        /// Create a Time unit hour with unit value = n
        let nHour n = n |> Hour |> toTime
        /// Create a Time unit minute with unit value = n
        let nMinute n = n |> Minute |> toTime
        /// Create a Time unit second with unit value = n
        let nSecond n = n |> Second |> toTime

        /// Create a Time unit year with unit value = 1
        let year = 1N |> nYear
        /// Create a Time unit month with unit value = 1
        let month = 1N |> nMonth
        /// Create a Time unit week with unit value = 1
        let week = 1N |> nWeek
        /// Create a Time unit day with unit value = 1
        let day = 1N |> nDay
        /// Create a Time unit hour with unit value = 1
        let hour = 1N |> nHour
        /// Create a Time unit minute with unit value = 1
        let minute = 1N |> nMinute
        /// Create a Time unit second with unit value = 1
        let second = 1N |> nSecond


    module Molar =

        module Constants =

            [<Literal>]
            let Mole = "mol"

            [<Literal>]
            let MilliMole = "mmol"

            [<Literal>]
            let MicroMole = "micromol"


        /// Create a Molar unit
        let toMolar = Molar

        /// Create a Molar unit mole with unit value = n
        let nMole n = n |> Mole |> toMolar
        /// Create a Molar unit millimole with unit value = n
        let nMilliMole n = n |> MilliMole |> toMolar
        /// Create a Molar unit micromole with unit value = n
        let nMicroMole n = n |> MicroMole |> toMolar

        /// Create a Molar unit mole with unit value = 1
        let mole = 1N |> nMole
        /// Create a Molar unit millimole with unit value = 1
        let milliMole = 1N |> nMilliMole
        /// Create a Molar unit micromole with unit value = 1
        let microMole = 1N |> nMicroMole


    module InterNational =

        module Constants =

            [<Literal>]
            let MIU = "MIU"

            [<Literal>]
            let IU = "IU"

            [<Literal>]
            let MilliIU = "milliIU"


        /// Create a InterNational unit
        let toInterNationalUnit = International

        /// Create a InterNational unit MIU with unit value = n
        let nMIU n = n |> MIU |> toInterNationalUnit
        /// Create a InterNational unit IU with unit value = n
        let nIU n = n |> IU |> toInterNationalUnit
        /// Create a InterNational unit milliIU with unit value = n
        let nMilliIU n = n |> MilliIU |> toInterNationalUnit

        /// Create a InterNational unit MIU with unit value = 1
        let mIU = 1N |> nMIU
        /// Create a InterNational unit IU with unit value = 1
        let iu = 1N |> nIU
        /// Create a InterNational unit milliIU with unit value = 1
        let milliIU = 1N |> nMilliIU


    module Height =

        module Constants =

            [<Literal>]
            let Meter = "m"

            [<Literal>]
            let CentiMeter = "cm"


        /// Create a Height unit
        let toHeight = Height

        /// Create a Height unit meter with unit value = n
        let nMeter n = n |> HeightMeter |> toHeight
        /// Create a Height unit centimeter with unit value = n
        let nCentiMeter n = n |> HeightCentiMeter |> toHeight

        /// Create a Height unit meter with unit value = 1
        let meter = 1N |> HeightMeter |> toHeight
        /// Create a Height unit centimeter with unit value = 1
        let centiMeter = 1N |> HeightCentiMeter |> toHeight


    module BSA =

        module Constants =

            [<Literal>]
            let M2 = "m2"


        /// Create a BSA unit
        let toBSA = BSA

        /// Create a BSA unit m2 with unit value = n
        let nM2 n = n |> M2 |> toBSA

        /// Create a BSA unit m2 with unit value = 1
        let m2 = 1N |> nM2


    module Energy =

        module Constants =

            [<Literal>]
            let Calorie = "cal"

            [<Literal>]
            let KiloCalorie = "kCal"


        let toEnergy = Energy


        let nCalorie n = n |> Calorie |> toEnergy

        let calorie = 1N |> nCalorie

        let nKiloCalorie n = n |> KiloCalorie |> toEnergy

        let kiloCalorie = 1N |> nKiloCalorie


    /// <summary>
    /// Map a unit to a unit value and a unit
    /// </summary>
    /// <example>
    /// mapUnit (Mass (KiloGram 1N)) = (1N, Mass.kiloGram)
    /// </example>
    /// <remarks>
    /// fails if the unit is a CombiUnit
    /// </remarks>

    module UnitDetails =


        /// Utility function to apply a function to a UnitDetails
        let apply f (ud: UnitDetails) = f ud


        /// Utility function to enable type inference
        let get = apply id


        /// Get the Unit from a UnitDetails
        let getUnit ud = (ud |> get).Unit


        /// <summary>
        /// Create a UnitDetails
        /// </summary>
        /// <param name="un">The unit</param>
        /// <param name="gr">The group</param>
        /// <param name="ab">The abbreviation</param>
        /// <param name="nm">The name</param>
        /// <param name="sy">Synonyms</param>
        /// <example>
        /// <code>
        /// create
        ///     (Mass (KiloGram 1N)) (Group.MassGroup)
        ///     { Eng = "kg"; EngPlural = "kg"; Dut = "kg"; DutchPlural = "kg" }
        ///     { Eng = "kilogram"; EngPlural = "kilogram"; Dut = "kilogram"; DutchPlural = "kilogram" }
        ///     []
        ///     =
        /// {
        ///     Unit = Mass (KiloGram 1N);
        ///     Group = Group.MassGroup;
        ///     Abbreviation = { Eng = "kg"; EngPlural = "kg"; Dut = "kg"; DutchPlural = "kg" };
        ///     Name = { Eng = "kilogram"; EngPlural = "kilogram"; Dut = "kilogram"; DutchPlural = "kilogram" };
        ///     Synonyms = []
        /// }
        /// </code>
        /// </example>
        let create un gr ab nm sy =
            {
                Unit = un
                Group = gr
                Abbreviation = ab
                Name = nm
                Synonyms = sy
            }


        /// <summary>
        /// Create a UnitDetails for a General unit
        /// </summary>
        /// <param name="n">the name for the general unit</param>
        /// <param name="v">the unit value</param>
        /// <example>
        /// <code>
        /// createGeneral "abc" 1N =
        /// {
        ///     Unit = General ("abc", 1N);
        ///     Group = Group.GeneralGroup "abc";
        ///     Abbreviation = { Eng = "abc"; EngPlural = "abc"; Dut = "abc"; DutchPlural = "abc" };
        ///     Name = { Eng = "abc"; EngPlural = "abc"; Dut = "abc"; DutchPlural = "abc" };
        ///     Synonyms = []
        /// }
        /// </code>
        /// </example>
        let createGeneral n v =
            if n |> String.isNullOrWhiteSpace then
                invalidArg (nameof n) "The name for a general unit cannot be an empty string"

            let un = (n, v) |> General

            let ab =
                {
                    Eng = n
                    EngPlural = n
                    Dut = n
                    DutchPlural = n
                }

            let nm = ab

            create un (Group.GeneralGroup n) ab nm []


        /// Get the Group from a UnitDetails
        let getGroup ud = (ud |> get).Group


        /// Get the Name from a UnitDetails
        let getName ud = (ud |> get).Name


        /// Get the Abbreviation from a UnitDetails
        let getAbbreviation ud = (ud |> get).Abbreviation


        /// Get the English string from Name from a UnitDetails
        let getEnglishName = getName >> getEnglish


        /// Get the Dutch string from Name from a UnitDetails
        let getDutchName = getName >> getDutch


        /// Get the English string from Abbreviation from a UnitDetails
        let getEnglishAbbreviation = getAbbreviation >> getEnglish


        /// Get the Dutch string from Abbreviation from a UnitDetails
        let getDutchAbbreviation = getAbbreviation >> getDutch


        /// <summary>
        /// Get the string for a unit in a specific language
        /// </summary>
        /// <param name="loc">Localization</param>
        /// <param name="verb">Verbality</param>
        /// <example>
        /// getUnitString English Long (Mass (KiloGram 1N)) = "kilogram"
        /// </example>
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


        /// List of UnitDetails
        let units =
            [
                {
                    Unit = Count.times
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "x"
                            Dut = "x"
                            EngPlural = "x"
                            DutchPlural = "x"
                        }
                    Name =
                        {
                            Eng = "times"
                            Dut = "keer"
                            EngPlural = "keer"
                            DutchPlural = "keer"
                        }
                    Synonyms = []
                }

                {
                    Unit = Mass.kiloGram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "kg"
                            Dut = "kg"
                            EngPlural = "kg"
                            DutchPlural = "kg"
                        }
                    Name =
                        {
                            Eng = "kilogram"
                            Dut = "kilogram"
                            EngPlural = "kilogram"
                            DutchPlural = "kilogram"
                        }
                    Synonyms = []
                }
                {
                    Unit = Mass.gram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "g"
                            Dut = "g"
                            EngPlural = "g"
                            DutchPlural = "g"
                        }
                    Name =
                        {
                            Eng = "gram"
                            Dut = "gram"
                            EngPlural = "gram"
                            DutchPlural = "gram"
                        }
                    Synonyms = [ "gr" ]
                }
                {
                    Unit = Mass.milliGram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "mg"
                            Dut = "mg"
                            EngPlural = "mg"
                            DutchPlural = "mg"
                        }
                    Name =
                        {
                            Eng = "milligram"
                            Dut = "milligram"
                            EngPlural = "milligram"
                            DutchPlural = "milligram"
                        }
                    Synonyms = [ "millig"; "milligr" ]
                }
                {
                    Unit = Mass.microGram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "microg"
                            Dut = "microg"
                            EngPlural = "microg"
                            DutchPlural = "microg"
                        }
                    Name =
                        {
                            Eng = "microgram"
                            Dut = "microgram"
                            EngPlural = "microgram"
                            DutchPlural = "microgram"
                        }
                    Synonyms = [ "mcg"; "µg"; "mcgr" ]
                }
                {
                    Unit = Mass.nanoGram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "nanog"
                            Dut = "nanog"
                            EngPlural = "nanog"
                            DutchPlural = "nanog"
                        }
                    Name =
                        {
                            Eng = "nanogram"
                            Dut = "nanogram"
                            EngPlural = "nanogram"
                            DutchPlural = "nanogram"
                        }
                    Synonyms = [ "nanogr"; "ng" ]
                }

                {
                    Unit = Distance.meter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "m"
                            Dut = "m"
                            EngPlural = "meter"
                            DutchPlural = "meter"
                        }
                    Name =
                        {
                            Eng = "meter"
                            Dut = "meter"
                            EngPlural = "meter"
                            DutchPlural = "meter"
                        }
                    Synonyms = []
                }
                {
                    Unit = Distance.centimeter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "cm"
                            Dut = "cm"
                            EngPlural = "cm"
                            DutchPlural = "cm"
                        }
                    Name =
                        {
                            Eng = "centimeter"
                            Dut = "centimeter"
                            EngPlural = "centimeter"
                            DutchPlural = "centimeter"
                        }
                    Synonyms = []
                }
                {
                    Unit = Distance.millimeter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "mm"
                            Dut = "mm"
                            EngPlural = "mm"
                            DutchPlural = "mm"
                        }
                    Name =
                        {
                            Eng = "millimeter"
                            Dut = "millimeter"
                            EngPlural = "millimeter"
                            DutchPlural = "millimeter"
                        }
                    Synonyms = []
                }


                {
                    Unit = Volume.liter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "l"
                            Dut = "l"
                            EngPlural = "l"
                            DutchPlural = "l"
                        }
                    Name =
                        {
                            Eng = "liter"
                            Dut = "liter"
                            EngPlural = "liter"
                            DutchPlural = "liter"
                        }
                    Synonyms = [ "ltr" ]
                }
                {
                    Unit = Volume.deciLiter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "dl"
                            Dut = "dl"
                            EngPlural = "dl"
                            DutchPlural = "dl"
                        }
                    Name =
                        {
                            Eng = "deciliter"
                            Dut = "deciliter"
                            EngPlural = "deciliter"
                            DutchPlural = "deciliter"
                        }
                    Synonyms = [ "decil" ]
                }
                {
                    Unit = Volume.milliLiter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "ml"
                            Dut = "mL"
                            EngPlural = "ml"
                            DutchPlural = "mL"
                        }
                    Name =
                        {
                            Eng = "milliliter"
                            Dut = "milliliter"
                            EngPlural = "milliliter"
                            DutchPlural = "milliliter"
                        }
                    Synonyms = [ "millil" ]
                }
                {
                    Unit = Volume.microLiter
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "microL"
                            Dut = "microL"
                            EngPlural = "microL"
                            DutchPlural = "microL"
                        }
                    Name =
                        {
                            Eng = "microliter"
                            Dut = "microliter"
                            EngPlural = "microliter"
                            DutchPlural = "microliter"
                        }
                    Synonyms = [ "µl" ]
                }
                {
                    Unit = Volume.droplet
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "droplet"
                            Dut = "druppel"
                            EngPlural = "droplets"
                            DutchPlural = "druppels"
                        }
                    Name =
                        {
                            Eng = "droplet"
                            Dut = "druppel"
                            EngPlural = "droplets"
                            DutchPlural = "druppels"
                        }
                    Synonyms = [ "drop"; "dr" ]
                }

                {
                    Unit = Time.year
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "yr"
                            Dut = "jaar"
                            EngPlural = "yrs"
                            DutchPlural = "jaar"
                        }
                    Name =
                        {
                            Eng = "year"
                            Dut = "jaar"
                            EngPlural = "years"
                            DutchPlural = "jaar"
                        }
                    Synonyms = [ "years"; "jaren" ]
                }
                {
                    Unit = Time.month
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "mo"
                            Dut = "maand"
                            EngPlural = "mos"
                            DutchPlural = "maanden"
                        }
                    Name =
                        {
                            Eng = "month"
                            Dut = "maand"
                            EngPlural = "months"
                            DutchPlural = "maanden"
                        }
                    Synonyms = [ "months"; "maanden" ]
                }
                {
                    Unit = Time.week
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "week"
                            Dut = "week"
                            EngPlural = "weeks"
                            DutchPlural = "weken"
                        }
                    Name =
                        {
                            Eng = "week"
                            Dut = "week"
                            EngPlural = "weeks"
                            DutchPlural = "weken"
                        }
                    Synonyms = [ "weeks"; "weken" ]
                }
                {
                    Unit = Time.day
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "day"
                            Dut = "dag"
                            EngPlural = "days"
                            DutchPlural = "dagen"
                        }
                    Name =
                        {
                            Eng = "day"
                            Dut = "dag"
                            EngPlural = "days"
                            DutchPlural = "dagen"
                        }
                    Synonyms = [ "days"; "dagen" ]
                }
                {
                    Unit = Time.hour
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "hr"
                            Dut = "uur"
                            EngPlural = "hr"
                            DutchPlural = "uur"
                        }
                    Name =
                        {
                            Eng = "hour"
                            Dut = "uur"
                            EngPlural = "hours"
                            DutchPlural = "uur"
                        }
                    Synonyms = [ "hours"; "uren" ]
                }
                {
                    Unit = Time.minute
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "min"
                            Dut = "min"
                            EngPlural = "min"
                            DutchPlural = "min"
                        }
                    Name =
                        {
                            Eng = "minute"
                            Dut = "minuut"
                            EngPlural = "minutes"
                            DutchPlural = "minuten"
                        }
                    Synonyms = [ "minutes"; "minuten" ]
                }
                {
                    Unit = Time.second
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "sec"
                            Dut = "sec"
                            EngPlural = "secs"
                            DutchPlural = "sec"
                        }
                    Name =
                        {
                            Eng = "second"
                            Dut = "seconde"
                            EngPlural = "seconds"
                            DutchPlural = "seconden"
                        }
                    Synonyms = [ "s" ]
                }

                {
                    Unit = Molar.mole
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "mol"
                            Dut = "mol"
                            EngPlural = "mol"
                            DutchPlural = "mol"
                        }
                    Name =
                        {
                            Eng = "mol"
                            Dut = "mol"
                            EngPlural = "mol"
                            DutchPlural = "mol"
                        }
                    Synonyms = []
                }
                {
                    Unit = Molar.milliMole
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "mmol"
                            Dut = "mmol"
                            EngPlural = "mmol"
                            DutchPlural = "mmol"
                        }
                    Name =
                        {
                            Eng = "millimol"
                            Dut = "millimol"
                            EngPlural = "millimol"
                            DutchPlural = "millimol"
                        }
                    Synonyms = []
                }
                {
                    Unit = Molar.microMole
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "micromol"
                            Dut = "micromol"
                            EngPlural = "micromol"
                            DutchPlural = "micromol"
                        }
                    Name =
                        {
                            Eng = "micromol"
                            Dut = "micromol"
                            EngPlural = "micromol"
                            DutchPlural = "micromol"
                        }
                    Synonyms = [ "umol" ]
                }

                {
                    Unit = InterNational.iu
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "IU"
                            Dut = "IE"
                            EngPlural = "IU"
                            DutchPlural = "IE"
                        }
                    Name =
                        {
                            Eng = "IU"
                            Dut = "IE"
                            EngPlural = "IU"
                            DutchPlural = "IE"
                        }
                    Synonyms = [ "E"; "U"; "IU" ]
                }
                {
                    Unit = InterNational.mIU
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "miljIU"
                            Dut = "miljIE"
                            EngPlural = "miljIE"
                            DutchPlural = "miljIE"
                        }
                    Name =
                        {
                            Eng = "millionIU"
                            Dut = "miljoenIE"
                            EngPlural = "miljIE"
                            DutchPlural = "miljIE"
                        }
                    Synonyms =
                        [
                            "miljoenIE"
                            "milj.IE"
                            "milj.E"
                            "miljIE"
                            "miljonIU"
                            "milj.IU"
                            "milj.U"
                        ]
                }
                {
                    Unit = InterNational.milliIU
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "milliIU"
                            Dut = "milliIE"
                            EngPlural = "milliIU"
                            DutchPlural = "milliIE"
                        }
                    Name =
                        {
                            Eng = "milliIU"
                            Dut = "milliIE"
                            EngPlural = "milliIU"
                            DutchPlural = "milliIE"
                        }
                    Synonyms = [ "milli-internationale eenheid"; "mie" ]
                }

                {
                    Unit = Weight.kiloGram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "kg"
                            Dut = "kg"
                            EngPlural = "kg"
                            DutchPlural = "kg"
                        }
                    Name =
                        {
                            Eng = "kilogram"
                            Dut = "kilogram"
                            EngPlural = "kilogram"
                            DutchPlural = "kilogram"
                        }
                    Synonyms = []
                }

                {
                    Unit = Weight.gram
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "g"
                            Dut = "g"
                            EngPlural = "g"
                            DutchPlural = "g"
                        }
                    Name =
                        {
                            Eng = "gram"
                            Dut = "gram"
                            EngPlural = "gram"
                            DutchPlural = "gram"
                        }
                    Synonyms = [ "gr" ]
                }

                {
                    Unit = BSA.m2
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "m2"
                            Dut = "m2"
                            EngPlural = "m2"
                            DutchPlural = "m2"
                        }
                    Name =
                        {
                            Eng = "square meter"
                            Dut = "vierkante meter"
                            EngPlural = "square meter"
                            DutchPlural = "vierkante meter"
                        }
                    Synonyms = [ "m^2" ]
                }

                {
                    Unit = Energy.calorie
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "cal"
                            Dut = "cal"
                            EngPlural = "calories"
                            DutchPlural = "calorieen"
                        }
                    Name =
                        {
                            Eng = "calorie"
                            Dut = "calorie"
                            EngPlural = "calories"
                            DutchPlural = "calorieen"
                        }
                    Synonyms = []
                }

                {
                    Unit = Energy.kiloCalorie
                    Group = Group.NoGroup
                    Abbreviation =
                        {
                            Eng = "kCal"
                            Dut = "kCal"
                            EngPlural = "kilocalories"
                            DutchPlural = "kilocalorieen"
                        }
                    Name =
                        {
                            Eng = "kilocalorie"
                            Dut = "kilocalorie"
                            EngPlural = "kilocalories"
                            DutchPlural = "kilocalorieen"
                        }
                    Synonyms = []
                }
            ]
            // The per-record `Group = Group.NoGroup` above are placeholders only:
            // the authoritative group is derived here from each unit via unitToGroup.
            |> List.map (fun ud -> { ud with Group = ud.Unit |> Group.unitToGroup })


    let nUnit =
        function
        | NoUnit -> (1N, NoUnit)
        | ZeroUnit -> (0N, ZeroUnit)
        | General(n, v) -> (v, ((n, 1N) |> General))
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
            | Droplet(n, m) -> (n, Volume.dropletWithDropsPerMl m)
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
            | Mole n -> (n, Molar.mole)
            | MilliMole n -> (n, Molar.milliMole)
            | MicroMole n -> (n, Molar.microMole)
        | International g ->
            match g with
            | MIU n -> (n, InterNational.mIU)
            | IU n -> (n, InterNational.iu)
            | MilliIU n -> (n, InterNational.milliIU)
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
        | Energy e ->
            match e with
            | Calorie n -> (n, Energy.calorie)
            | KiloCalorie n -> (n, Energy.kiloCalorie)
        | CombiUnit(u1, op, u2) -> failwith <| $"Cannot map combined unit %A{(u1, op, u2) |> CombiUnit}"


    /// <summary>
    /// Check whether unit u1 equals unit u2 irrespective of the unit value.
    /// </summary>
    /// <remarks>
    /// The unit value is ignored for every unit type EXCEPT <c>General</c>:
    /// two general units are equal only when BOTH their name and their value
    /// match (e.g. <c>General("x", 1N)</c> does not equal <c>General("x", 2N)</c>).
    /// This is deliberate — a general unit's value is part of its identity
    /// because there is no canonical base unit to normalise it against.
    /// </remarks>
    /// <param name="u1">The first unit</param>
    /// <param name="u2">The second unit</param>
    let rec eqsUnit u1 u2 =
        match u1, u2 with
        | NoUnit, NoUnit -> true
        | NoUnit, _
        | _, NoUnit -> false
        | ZeroUnit, ZeroUnit -> true
        | ZeroUnit, _
        | _, ZeroUnit -> false
        | General(n1, v1), General(n2, v2) -> n1 = n2 && v1 = v2
        | General _, _
        | _, General _ -> false
        | Count g1, Count g2 ->
            match g1, g2 with
            | Times _, Times _ -> true
        | Count _, _
        | _, Count _ -> false
        | Distance d1, Distance d2 ->
            match d1, d2 with
            | Meter _, Meter _
            | CentiMeter _, CentiMeter _
            | MilliMeter _, MilliMeter _ -> true
            | _ -> false
        | Distance _, _
        | _, Distance _ -> false
        | Volume g1, Volume g2 ->
            match g1, g2 with
            | Liter _, Liter _
            | DeciLiter _, DeciLiter _
            | MilliLiter _, MilliLiter _
            | MicroLiter _, MicroLiter _
            | Droplet _, Droplet _ -> true
            | _ -> false
        | Volume _, _
        | _, Volume _ -> false
        | Mass g1, Mass g2 ->
            match g1, g2 with
            | KiloGram _, KiloGram _
            | Gram _, Gram _
            | MilliGram _, MilliGram _
            | MicroGram _, MicroGram _
            | NanoGram _, NanoGram _ -> true
            | _ -> false
        | Mass _, _
        | _, Mass _ -> false
        | Time g1, Time g2 ->
            match g1, g2 with
            | Year _, Year _
            | Month _, Month _
            | Week _, Week _
            | Day _, Day _
            | Hour _, Hour _
            | Minute _, Minute _
            | Second _, Second _ -> true
            | _ -> false
        | Time _, _
        | _, Time _ -> false
        | Molar g1, Molar g2 ->
            match g1, g2 with
            | Mole _, Mole _
            | MilliMole _, MilliMole _
            | MicroMole _, MicroMole _ -> true
            | _ -> false
        | Molar _, _
        | _, Molar _ -> false
        | International g1, International g2 ->
            match g1, g2 with
            | MIU _, MIU _
            | IU _, IU _
            | MilliIU _, MilliIU _ -> true
            | _ -> false
        | International _, _
        | _, International _ -> false
        | Weight g1, Weight g2 ->
            match g1, g2 with
            | WeightKiloGram _, WeightKiloGram _
            | WeightGram _, WeightGram _ -> true
            | _ -> false
        | Weight _, _
        | _, Weight _ -> false
        | Height g1, Height g2 ->
            match g1, g2 with
            | HeightMeter _, HeightMeter _
            | HeightCentiMeter _, HeightCentiMeter _ -> true
            | _ -> false
        | Height _, _
        | _, Height _ -> false
        | BSA g1, BSA g2 ->
            match g1, g2 with
            | M2 _, M2 _ -> true
        | BSA _, _
        | _, BSA _ -> false
        | Energy e1, Energy e2 ->
            match e1, e2 with
            | Calorie _, Calorie _
            | KiloCalorie _, KiloCalorie _ -> true
            | _ -> false
        | Energy _, _
        | _, Energy _ -> false
        | CombiUnit(ul1, op1, ur1), CombiUnit(ul2, op2, ur2) -> op1 = op2 && eqsUnit ul1 ul2 && eqsUnit ur1 ur2


    /// Try find unit details in the list of units
    /// for unit u
    /// Example: tryFind (Mass.kiloGram) = Some { ... }
    let tryFind u =
        match UnitDetails.units |> List.tryFind (fun udt -> udt.Unit |> eqsUnit u) with
        | Some udt -> Some udt
        | None -> None


    /// <summary>Apply a function f to the unit value(s) of unit u.</summary>
    /// <param name="f">the function to apply to the unit value</param>
    /// <param name="u">the unit</param>
    /// <returns>The unit with the updated value</returns>
    /// <example>apply (fun n -> n * 2N) (Mass (KiloGram 1N)) = Mass (KiloGram 2N)</example>
    let apply f u =
        let rec app u =
            match u with
            | NoUnit
            | ZeroUnit -> u
            | General(s, n) -> (s, n |> f) |> General
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
                | Droplet(n, m) -> n |> f |> (fun n -> Droplet(n, m))
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
                | Mole n -> n |> f |> Mole
                | MilliMole n -> n |> f |> MilliMole
                | MicroMole n -> n |> f |> MicroMole
                |> Molar
            | International g ->
                match g with
                | MIU n -> n |> f |> MIU
                | IU n -> n |> f |> IU
                | MilliIU n -> n |> f |> MilliIU
                |> International
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
            | Energy e ->
                match e with
                | Calorie n -> n |> f |> Calorie
                | KiloCalorie n -> n |> f |> KiloCalorie
                |> Energy
            | CombiUnit(u1, op, u2) -> (app u1, op, app u2) |> CombiUnit

        app u


    //----------------------------------------------------------------------------
    // Unit Setters and Getters
    //----------------------------------------------------------------------------


    /// Change the value of a unit
    /// to the value n
    /// Example: (Mass (KiloGram 1N)) |> setUnitValue 2N = Mass (KiloGram 2N)

    let setUnitValue n =
        let f = fun _ -> n
        apply f


    /// Get the value of the unit
    /// Returns None if no value

    let getUnitValue u =
        let rec app u =
            match u with
            | NoUnit
            | ZeroUnit -> None
            | General(_, n) -> n |> Some
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
                | Droplet(n, _) -> n |> Some
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
                | Mole n -> n |> Some
                | MilliMole n -> n |> Some
                | MicroMole n -> n |> Some
            | International g ->
                match g with
                | MIU n -> n |> Some
                | IU n -> n |> Some
                | MilliIU n -> n |> Some
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
            | Energy e ->
                match e with
                | Calorie n -> n |> Some
                | KiloCalorie n -> n |> Some
            | CombiUnit _ -> None

        app u


    /// <summary>
    /// Turn a unit u to a string with localization, verbality, and optional group annotation.
    /// </summary>
    /// <param name="vw">Optional unit value wrapper</param>
    /// <param name="uw">Optional unit wrapper</param>
    /// <param name="hasGroup">When true, includes the unit group in brackets (e.g., "[Mass]"); when false, omits it</param>
    /// <param name="loc">Localization (English or Dutch)</param>
    /// <param name="verb">Verbality (Short or Long)</param>
    /// <param name="u">The unit to convert to string</param>
    let toString vw uw hasGroup loc verb u =
        let wrapUnit u =
            match uw with
            | Some w -> $"%s{w}{u}{w}"
            | None -> u

        let wrapValue n =
            let n =
                match loc with
                | Dutch -> n |> BigRational.toDecimal |> Decimal.toStringNumberNLWithoutTrailingZeros
                | English -> n |> BigRational.toDecimal |> string

            match vw with
            | Some w -> $"%s{w}{n}{w}"
            | None -> n

        let gtost u g =
            u
            + if hasGroup then
                  "[" + (g |> Group.toString) + "]"
              else
                  "" |> wrapUnit

        let rec str u =
            match u with
            | NoUnit
            | ZeroUnit -> ""

            | CombiUnit(ul, op, ur) ->
                let uls = str ul
                let urs = str ur

                uls + (op |> Core.opToStr) + urs

            | General(n, v) ->
                let ustr = if hasGroup then n + "[General]" else n

                if v > 1N then
                    $"{v |> wrapValue} {ustr |> wrapUnit}"
                else
                    ustr |> wrapUnit

            | _ ->
                let n, u = u |> nUnit

                match u |> tryFind with
                | Some udt ->
                    match loc with
                    | English ->
                        match verb with
                        | Short ->
                            udt.Group
                            |> gtost (
                                if n > 1N then
                                    udt.Abbreviation.EngPlural
                                else
                                    udt.Abbreviation.Eng
                            )
                        | Long -> udt.Group |> gtost (if n > 1N then udt.Name.EngPlural else udt.Name.Eng)
                    | Dutch ->
                        match verb with
                        | Short ->
                            udt.Group
                            |> gtost (
                                if n > 1N then
                                    udt.Abbreviation.DutchPlural
                                else
                                    udt.Abbreviation.Dut
                            )
                        | Long -> udt.Group |> gtost (if n > 1N then udt.Name.DutchPlural else udt.Name.Dut)
                | None -> ""
                |> function
                    | s when s |> String.isNullOrWhiteSpace -> ""
                    | s when n = 1N -> s
                    | s -> $"{n |> wrapValue} {s}"

        str u


    /// <summary>
    /// Turn a unit to a dutch short string with group annotation
    /// </summary>
    /// <example>
    /// toStringDutchShort (Time (Minute 1N)) = "min[Time]"
    /// </example>
    let toStringDutchShort = toString None None true Dutch Short


    /// <summary>
    /// Turn a unit to a dutch short string with group annotation
    ///
    /// </summary>
    /// <example>
    /// toStringDutchShort (Time (Minute 1N)) = "min[Time]"
    /// </example>
    let toStringDutchShortWithWrapper vw uw =
        toString (Some vw) (Some uw) true Dutch Short

    /// <summary>
    /// Turn a unit to a dutch long string with group annotation
    /// </summary>
    /// <example>
    /// toStringDutchLong (Time (Minute 1N)) = "minuut[Time]"
    /// </example>
    let toStringDutchLong = toString None None true Dutch Long

    /// <summary>
    /// Turn a unit to an english short string with group annotation
    /// </summary>
    /// <example>
    /// toStringEngShort (Time (Day 1N)) = "day[Time]"
    /// </example>
    let toStringEngShort = toString None None true English Short

    /// <summary>
    /// Turn a unit to an english short string without group annotation
    /// </summary>
    /// <example>
    /// toStringEngShort (Time (Day 1N)) = "day"
    /// </example>
    let toStringEngShortWithoutGroup = toString None None false English Short

    /// <summary>
    /// Turn a unit to an english long string with group annotation
    /// </summary>
    /// <example>
    /// toStringEngLong (Time (Day 1N)) = "day[Time]"
    /// </example>
    let toStringEngLong = toString None None true English Long


    let rec hasGroup u1 u2 =
        match u1, u2 with
        | CombiUnit(u1L, _, u1R), CombiUnit(u2L, _, u2R) ->
            hasGroup u1L u2L || hasGroup u1L u2R || hasGroup u1R u2L || hasGroup u1R u2R
        | CombiUnit(u1L, _, u1R), u2 -> hasGroup u1L u2 || hasGroup u1R u2
        | u1, CombiUnit(u2L, _, u2R) -> hasGroup u1 u2L || hasGroup u1 u2R
        | u1, u2 -> (u1 |> Group.unitToGroup) = (u2 |> Group.unitToGroup)
