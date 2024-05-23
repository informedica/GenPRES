namespace rec Informedica.GenUnits.Lib

#nowarn "40"

open MathNet.Numerics

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL



module Array =

    /// <summary>
    /// Remove all BigRationals that are multiples of other BigRationals in the array
    /// </summary>
    /// <param name="xs">The array of BigRationals</param>
    /// <returns>The array without multiples</returns>
    /// <example>
    /// [| 2N; 3N; 4N; 5N; 6N; 7N; 8N; 9N; 10N |] |> removeBigRationalMultiples -> [| 2N; 3N; 5N; 7N |]
    /// </example>
    let removeBigRationalMultiples xs =
        if xs |> Array.isEmpty then
            xs
        else
            let xs =
                xs
                |> Array.sort
                |> Array.distinct

            xs
            |> Array.fold
                (fun acc x1 ->
                    acc
                    |> Array.filter (fun x2 ->
                        x1 = x2 ||
                        x2 |> BigRational.isMultiple x1
                        |> not
                    )
                )
                xs


type Unit =
    | NoUnit
    // special case to enable efficient min max calculations where
    // either min or max approaches zero, ZeroUnit means that whatever
    // the actual unit of the value, the value is zero
    | ZeroUnit
    | CombiUnit of Unit * Operator * Unit
    | General of (string * BigRational)
    | Count of CountUnit
    | Mass of MassUnit
    | Distance of DistanceUnit
    | Volume of VolumeUnit
    | Time of TimeUnit
    | Molar of MolarUnit
    | International of InternationalUnit
    | Weight of WeightUnit
    | Height of HeightUnit
    | BSA of BSAUnit
    | Energy of EnergyUnit
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
    // droplet has multiplier * droplets per mL
    | Droplet of BigRational * BigRational

and TimeUnit =
    | Year of BigRational
    | Month of BigRational
    | Week of BigRational
    | Day of BigRational
    | Hour of BigRational
    | Minute of BigRational
    | Second of BigRational

and MolarUnit =
    | Mole of BigRational
    | MilliMole of BigRational
    | MicroMole of BigRational

and InternationalUnit =
    | MIU of BigRational
    | IU of BigRational
    | MilliIU of BigRational

and WeightUnit =
    | WeightKiloGram of BigRational
    | WeightGram of BigRational

and HeightUnit =
    | HeightMeter of BigRational
    | HeightCentiMeter of BigRational

and BSAUnit = M2 of BigRational

and EnergyUnit =
    | Calorie of BigRational
    | KiloCalorie of BigRational

and Operator =
    | OpTimes
    | OpPer
    | OpPlus
    | OpMinus


type ValueUnit = ValueUnit of BigRational [] * Unit



module Group =


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



module Parser =

    open FParsec

    open Units


    /// <summary>
    /// Set the value of a unit.
    /// </summary>
    /// <example>
    /// setUnitValue (KiloGram 1N |> Mass) 2N = (KiloGram 2N |> Mass)
    /// </example>
    let setUnitValue u v =
        u
        |> apply (fun _ -> v)


    /// <summary>
    /// White spaces parser, skips over any number of white spaces
    /// </summary>
    /// <example>
    /// - Example: "  " |> run ws -> Success: () <br/>
    /// - Example: "" |> run ws -> Success: () <br/>
    /// </example>
    let ws = spaces


    /// <summary>
    /// A parser that parses a string and then any nmber of white space
    /// </summary>
    /// <example>
    /// Example: "abc  " |> run (str_ws "abc") -> Success: "abc" <br/>
    /// Example: "abc" |> run (str_ws "abc") -> Success: "abc" <br/>
    /// </example>
    let str_ws s = pstring s >>. ws


    /// <summary>
    /// Parser for a float
    /// Note also parses nan!
    /// </summary>
    /// <example>
    /// Example: "1.2" |> run pfloat -> Success: 1.2 <br/>
    /// Example "nan" |> run pfloat -> Success: nan <br/>
    /// </example>
    let pnumber = pfloat .>> ws


    /// <summary>
    /// Parses a BigRational from a float
    /// </summary>
    /// <example>
    /// Example: "1.2" |> run pBigRat -> Success: 6/5
    /// </example>
    let pBigRat =
        pnumber
        |>> (BigRational.fromFloat >> Option.defaultValue 0N)


    /// <summary>
    /// Parse a unit with an optional group
    /// </summary>
    /// <example>
    /// Example: "kg" |> run (pUnitGroup "kg" "mass") -> Success: "kg" <br/>
    /// Example: "kg[Mass]" |> run (pUnitGroup "kg" "mass") -> Success: "kg" <br/>
    /// </example>
    let pUnitGroup (u : string) g =
        let pu = u |> pstringCI
        let pg = $"[%s{g}]" |> pstringCI
        (pu .>> ws .>> (opt pg))


    /// <summary>
    /// Parses a Unit
    /// </summary>
    /// <example>
    /// "mg" |> run pUnit -> Success: (MilliGram 1N |> Mass)
    /// </example>
    let pUnit =
        UnitDetails.units
        |> List.collect (fun ud ->
            [
                {| unit = ud.Abbreviation.Eng; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Abbreviation.Dut; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.Eng; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.Dut; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.EngPlural; grp = ud.Group; f = setUnitValue ud.Unit |}
                {| unit = ud.Name.DutchPlural; grp = ud.Group; f = setUnitValue ud.Unit |}
                yield!
                    ud.Synonyms
                    |> List.map (fun s ->
                        {| unit = s; grp = ud.Group; f = setUnitValue ud.Unit |}
                    )
            ]
        )
        // need to change nan to xxx to avoid getting a float 'nan'
        |> List.map (fun r -> {| r with unit = r.unit |> String.replace "nan" "nnn" |})
        |> List.distinctBy (fun r -> r.unit, r.grp)
        |> List.filter (fun r ->
            (r.unit = "kg" && r.grp = Group.MassGroup ||
            r.unit = "kilogram" && r.grp = Group.MassGroup)
            |> not
        )
        |> List.sortByDescending (fun r -> r.unit |> String.length,  r.unit)
        //|> List.map (fun r -> printfn $"{r}"; r)
        |> List.map (fun r ->
            let g = $"{r.grp |> ValueUnit.Group.toString}"

            attempt (
                opt pfloat
                .>> ws
                .>>. (pUnitGroup r.unit g >>% r.f)
                |>> (fun (f, u) ->
                    f
                    |> Option.bind BigRational.fromFloat
                    |> Option.defaultValue 1N |> u
                )
            )
        )
        |> choice


    /// <summary>
    /// Parse a complex unit using FParsec's OperatorPrecedenceParser
    /// </summary>
    /// <returns>Parser of Unit, unit</returns>
    /// <example>
    /// "mg/kg[Weight]" |> run parseUnit -> Success: CombiUnit (Mass (MilliGram 1N), OpPer, Weight (WeightKiloGram 1N))
    /// </example>
    let parseUnit =

        let opp  = OperatorPrecedenceParser<Unit, unit, unit>()
        let expr = opp.ExpressionParser

        opp.TermParser <-
            pUnit <|> between (str_ws "(") (str_ws ")") expr

        let ( *! ) u1 u2 = (u1, OpTimes, u2) |> CombiUnit
        let ( /! ) u1 u2 = (u1, OpPer, u2) |> CombiUnit

        opp.AddOperator (InfixOperator("*", ws, 1, Associativity.Left, ( *! )))
        opp.AddOperator (InfixOperator("/", ws, 1, Associativity.Left, ( /! )))

        ws >>. expr .>> eof


    /// <summary>
    /// Parse a string to a ValueUnit
    /// </summary>
    /// <example>
    /// "1.2 mg/kg[Weight]" |> parse -> <br/>
    /// Success: ValueUnit
    /// ([|6/5N|], CombiUnit (Mass (MilliGram 1N), OpPer, Weight (WeightKiloGram 1N)))
    /// </example>
    let parse s =
        // need to change nan to xxx to avoid getting a float 'nan'
        let s = s |> String.replace "nan" "nnn"

        let pBigRatList =
            sepBy pBigRat (ws >>. (pstring ";") .>> ws)

        let pValue =
            (between (pstring "[") (pstring "]") pBigRatList)
            <|> (many pBigRat)

        let p =
            pValue .>>. parseUnit
            |>> (fun (brs, u) ->
                brs
                |> List.toArray
                |> ValueUnit.create u
            )
        s |> run p



module Units =

    open FParsec


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
            DutchPlural : string
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


    let per = ValueUnit.per

    let times = ValueUnit.times


    type UnitDetails =
        {
            Unit: Unit
            Group: Group.Group
            Abbreviation: Language
            Name: Language
            Synonyms: string list
        }



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
                "The name for a general unit cannot be an empty string"
                |> failwith
            let un = (n, v) |> General
            let ab = { Eng = n; EngPlural = n; Dut = n; DutchPlural = n }
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
                    Synonyms =
                        [
                            "milli-internationale eenheid"
                            "mie"
                        ]
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
                    Synonyms = [ ]
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
                    Synonyms = [ ]
                }
            ]
            |> List.map (fun ud ->
                { ud with
                    Group = ud.Unit |> ValueUnit.Group.unitToGroup
                }
            )



    module General =

        /// create a general unit
        let toGeneral = General
        /// create a general unit with unit value = 1
        let general n =
            if n |> String.isNullOrWhiteSpace then
                failwith "the name of a General Unit cannot be an empty string"
            (n, 1N) |> toGeneral


    module Count =

        /// Create a Count unit
        let toCount = Count

        /// Create a Count unit with unit value = n
        let nTimes n = n |> Times |> toCount
        /// Create a Count unit with unit value = 1
        let times = 1N |> nTimes


    module Mass =

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
            | Volume (Droplet (n, _)) ->
                (n, m) |> Droplet |> Volume
            | _ -> dr


    module Time =

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

        /// Create a BSA unit
        let toBSA = BSA

        /// Create a BSA unit m2 with unit value = n
        let nM2 n = n |> M2 |> toBSA

        /// Create a BSA unit m2 with unit value = 1
        let m2 = 1N |> nM2


    module Energy =

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
            | Droplet (n, m) -> (n, Volume.dropletWithDropsPerMl m)
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
        | CombiUnit (u1, op, u2) ->
            failwith
            <| $"Cannot map combined unit %A{(u1, op, u2) |> CombiUnit}"


    /// Try find a the unit details in the list of units
    /// for unit u
    /// Example: tryFind (Mass.kiloGram) = Some { ... }
    let tryFind u =
        match UnitDetails.units |> List.tryFind (fun udt -> udt.Unit |> eqsUnit u) with
        | Some udt -> Some udt
        | None -> None


    /// Append a group to a string that represents a unit
    /// Example: stringWithGroup "mg" = "mg[Mass]"
    let stringWithGroup u =
        UnitDetails.units
        |> List.filter (fun ud ->
            ud.Group <> Group.WeightGroup
        )
        |> List.tryFind (fun ud ->
            [
                ud.Abbreviation.Dut
                ud.Abbreviation.Eng
                ud.Name.Dut
                ud.Name.Eng
            ]
            |> List.append ud.Synonyms
            |> List.exists(String.equalsCapInsens (u |> String.replaceNumbers ""))
        )
        |> function
        | Some ud ->
            ud.Group
            |> ValueUnit.Group.toString
        | None -> "General"
        |> sprintf "%s[%s]" u


    let groupIsGeneralOrNone s =
        let xs = (String.regex "[^\[\]]+(?=\])").Matches(s)
        xs
        |> Seq.map _.Value
        |> Seq.forall (String.equalsCapInsens "general")


    /// <summary>
    /// Creates a Unit from a string s, if possible
    /// otherwise returns None. Note will take care of
    /// the n value of a unit! So, for example, the unit
    /// 36 hour can be parsed correctly.
    /// </summary>
    /// <example>
    /// fromString "36 hours" = Some (Time (Hour 36N))
    /// </example>
    let fromString s =
        if s |> String.isNullOrWhiteSpace then None
        else

            // TODO: ugly hack need to fix this
            s
            |> String.replace "x[Count]" "#"
            |> String.replace "x" "/"
            |> String.replace "#" "x[Count]"
            |> String.split "/"
            |> function
            | us when us |> List.length >= 1 && (us |> List.length <= 3) ->
                us
                |> List.map (fun s ->
                    // need to replace nan as this otherwise will be a float
                    let s = s |> String.replace "nan" "nnn"
                    match s |> run Parser.parseUnit with
                    | Success (u, _, _) -> Some u
                    | Failure _ ->
                        if s |> String.isNullOrWhiteSpace then None
                        else
                            if s |> groupIsGeneralOrNone |> not then
                                failwith $"invalid unit group {s}"
                            s
                            |> String.removeBrackets
                            |> Units.General.general
                            |> Some
                )
                |> fun us ->
                    if us |> List.forall Option.isSome then
                        us
                        |> List.map Option.get
                        |> List.reduce (fun u1 u2 -> u1 |> Units.per u2)
                        |> Some
                    else
                        printfn $"cannot parse {s}"
                        None
            | _ ->
                printfn $"cannot parse {s}"
                None


    /// Turn a unit u to a string with
    /// localization loc and verbality verb.
    /// Example: toString Dutch Short (Mass (KiloGram 1N)) = "kg[Mass]"
    let toString loc verb u =
        let gtost u g =
            u + "[" + (g |> ValueUnit.Group.toString) + "]"

        let rec str u =
            match u with
            | NoUnit
            | ZeroUnit -> ""

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
                let n, u = u |> nUnit

                match u |> tryFind with
                | Some udt ->
                    match loc with
                    | English ->
                        match verb with
                        | Short ->
                            udt.Group
                            |> gtost (if n > 1N then udt.Abbreviation.EngPlural else udt.Abbreviation.Eng)
                        | Long ->
                            udt.Group
                            |> gtost (if n > 1N then udt.Name.EngPlural else udt.Name.Eng)
                    | Dutch ->
                        match verb with
                        | Short ->
                            udt.Group
                            |> gtost (if n > 1N then udt.Abbreviation.DutchPlural else udt.Abbreviation.Dut)
                        | Long ->
                            udt.Group
                            |> gtost (if n > 1N then udt.Name.DutchPlural else udt.Name.Dut)
                | None -> ""
                |> function
                | s when s |> String.isNullOrWhiteSpace -> ""
                | s when n = 1N -> s
                | s -> (n |> BigRational.toString) + " " + s

        str u


    /// Turn a unit to a dutch short string with
    /// Example: toStringDutchShort (Time (Minute 1N)) = "min[Time]"
    let toStringDutchShort =
        toString Dutch Short

    /// Turn a unit to a dutch long string
    /// Example: toStringDutchLong (Time (Minute 1N)) = "minuut[Time]"
    let toStringDutchLong = toString Dutch Long

    /// Turn a unit to a english short string
    /// Example: toStringEngShort (Time (Day 1N)) = "day[Time]"
    let toStringEngShort =
        toString English Short

    /// Turn a unit to a english long string
    /// Example: toStringEngLong (Time (Day 1N)) = "day[Time]"
    let toStringEngLong = toString English Long



    //----------------------------------------------------------------------------
    // Apply and Map
    //----------------------------------------------------------------------------


    /// <summary>
    /// Apply a function f to the unit u
    /// </summary>
    /// <param name="f">the function to apply to the unit value</param>
    /// <param name="u">the unit</param>
    /// <returns>
    /// The unit with the updated value
    /// </returns>
    /// <example>
    /// apply (fun n -> n * 2N) (Mass (KiloGram 1N)) = Mass (KiloGram 2N)
    /// </example>
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
                | Droplet (n, m) -> n |> f |> fun n -> Droplet (n, m)
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
            | CombiUnit (u1, op, u2) -> (app u1, op, app u2) |> CombiUnit

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
                | Droplet (n, _) -> n |> Some
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
    /// Check whether unit u1 equals unit u2
    /// irrespective of the unit value
    /// </summary>
    /// <param name="u1">The first unit</param>
    /// <param name="u2">The second unit</param>
    let eqsUnit u1 u2 =
        match u1, u2 with
        | NoUnit, NoUnit -> true
        | NoUnit, _
        | _, NoUnit -> false
        | ZeroUnit, ZeroUnit -> true
        | ZeroUnit, _
        | _, ZeroUnit -> false
        | General (n1, v1), General (n2, v2) ->
            n1 = n2 && v1 = v2
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
        | CombiUnit (ul1, op1, ur1), CombiUnit (ul2, op2, ur2) ->
            op1 = op2 && eqsUnit ul1 ul2 && eqsUnit ur1 ur2



module ValueUnit =

    open System.Net.NetworkInformation


    //----------------------------------------------------------------------------
    // Operator String functions
    //----------------------------------------------------------------------------


    /// Transforms an operator to a string
    let opToStr op =
        match op with
        | OpPer   -> "/"
        | OpTimes -> "*"
        | OpPlus  -> "+"
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


    module Group =


        /// Get the corresponding group for a unit
        /// Example: unitToGroup (Mass (KiloGram 1N)) = MassGroup
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
                | International _ -> Group.InterNatUnitGroup
                | Weight _ -> Group.WeightGroup
                | Height _ -> Group.HeightGroup
                | BSA _ -> Group.BSAGroup
                | Energy _ -> Group.EnergyGroup
                | CombiUnit (ul, op, ur) -> (get ul, op, get ur) |> Group.CombiGroup

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
                | Group.CombiGroup (gl, _, gr) -> cont gl || cont gr

            cont g1


        /// Get a list of the groups in a group g
        let rec getGroups g =
            match g with
            | Group.CombiGroup (gl, _, gr) -> gl |> getGroups |> List.prepend (gr |> getGroups)
            | _ -> [ g ]


        // separate numerators from denominators
        // isNum is true when we are in the numerator
        // and is false when we are in the denominator
        let rec internal numDenom isNum g =
            match g with
            | Group.CombiGroup (gl, OpTimes, gr) ->
                let lns, lds = gl |> numDenom isNum
                let rns, rds = gr |> numDenom isNum
                lns @ rns, lds @ rds
            | Group.CombiGroup (gl, OpPer, gr) ->
                if isNum then
                    let lns, lds = gl |> numDenom true
                    let rns, rds = gr |> numDenom false
                    lns @ rns, lds @ rds
                else
                    let lns, lds = gr |> numDenom true
                    let rns, rds = gl |> numDenom false
                    lns @ rns, lds @ rds
            | _ ->
                if isNum then
                    (g |> getGroups, [])
                else
                    ([], g |> getGroups)


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
        let eqsGroup u1 u2 =
            match u1, u2 with
            | ZeroUnit, NoUnit
            | NoUnit, ZeroUnit
            | _, _ when u1 = u2 -> true
            | _ ->
                let g1Num, g1Den=
                    u1 |> unitToGroup |> numDenom true
                let g2Num, g2Den =
                    u2 |> unitToGroup |> numDenom true

                g1Num |> List.sort = (g2Num |> List.sort) &&
                g1Den |> List.sort = (g2Den |> List.sort)


        /// Returns group g as a string
        let toString g =
            let rec str g s =
                match g with
                | Group.NoGroup -> ""
                | Group.ZeroGroup -> "Zero"
                | Group.GeneralGroup _ -> "General"
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
                | Group.CombiGroup (gl, op, gr) ->
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
            | Group.MolarGroup ->
                [
                    1N |> Mole |> Molar
                    1N |> MilliMole |> Molar
                ]
            | Group.InterNatUnitGroup ->
                [
                    1N |> MIU |> International
                    1N |> IU |> International
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
            | Group.EnergyGroup ->
                [
                    1N |> Calorie |> Energy
                    1N |> KiloCalorie |> Energy
                ]
            | Group.CombiGroup _ -> []


        /// <summary>
        /// Get all the units that belong to group
        /// or a combination of groups.
        /// </summary>
        /// <example>
        /// <code>
        /// getUnits (CombiGroup (MassGroup, OpPer, VolumeGroup))
        /// returns:
        ///   [CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N));
        ///    CombiUnit (Mass (KiloGram 1N), OpPer, Volume (DeciLiter 1N));
        ///     ...
        ///    CombiUnit (Mass (NanoGram 1N), OpPer, Volume (MilliLiter 1N));
        ///    CombiUnit (Mass (NanoGram 1N), OpPer, Volume (MicroLiter 1N))]
        /// </code>
        /// </example>
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

        /// Returns the value v as a basevalue using multiplier m
        let inline toBase m v = v * m
        /// Returns the value v as a unitvalue using multiplier m
        let inline toUnit m v = v / m


        /// <summary>
        /// Get the multiplier of a unit
        /// (also when this is a combination of units)
        /// </summary>
        /// <example>
        /// getMultiplier (Mass (KiloGram 1N)) = 1000N <br/>
        /// getMultiplier (CombiUnit(Mass (MilliGram 1N), OpPer, Volume (MilliLiter 1N))) = 1N <br/>
        /// </example>
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
                    | Droplet (n, m) -> n * (milli / m)
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
                    | Mole n -> n * one
                    | MilliMole n -> n * milli
                    | MicroMole n -> n * micro
                | International g ->
                    match g with
                    | MIU n -> n * kilo * kilo
                    | IU n -> n * one
                    | MilliIU n -> n * milli
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
                | Energy e ->
                    match e with
                    | Calorie n -> n * one
                    | KiloCalorie n -> n * kilo
                | CombiUnit (u1, op, u2) ->
                    let m1 = get u1 m
                    let m2 = get u2 m

                    match op with
                    | OpTimes -> m1 * m2
                    | OpPer -> m1 / m2
                    | OpMinus
                    | OpPlus -> m

            get u 1N


    //----------------------------------------------------------------------------
    // Create functions
    //----------------------------------------------------------------------------


    /// <summary>
    /// Create a ValueUnit from a value v
    /// (a bigrational array) and a unit u
    /// Makes sure there are nog duplicates and sorts the result.
    /// </summary>
    /// <example>
    /// create (Mass (KiloGram 1N)) [| 1N; 2N; 3N |] = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let create u v =
        match u with
        | NoUnit when v = [| 0N |] -> ([| 0N |], ZeroUnit) |> ValueUnit
        | ZeroUnit -> ([| 0N |], ZeroUnit) |> ValueUnit
        | _ ->
            (v |> Array.distinct |> Array.sort, u)
            |> ValueUnit


    /// An empty ValueUnit that has no value
    /// and no unit, i.e. an empty array with
    /// NoUnit.
    let empty = create NoUnit [||]


    /// <summary>
    /// Create a a ValueUnit from a single
    /// value v and a unit u
    /// </summary>
    /// <example>
    /// createSingle (Mass (KiloGram 1N)) 1N = ValueUnit ([|1N|], Mass (KiloGram 1N))
    /// </example>
    let createSingle u v = [| v |] |> create u


    /// <summary>
    /// Utility create function to allow piping
    /// v |> WithUnit u
    /// </summary>
    /// <example>
    /// [| 1N; 2N; 3N |] |> withUnit (Mass (KiloGram 1N)) = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let withUnit u v = v |> create u


    /// <summary>
    /// Utility create function to allow piping
    /// with a single value v |> WithUnit u
    /// </summary>
    /// <example>
    /// 1N |> singleWithUnit (Mass (KiloGram 1N)) = ValueUnit ([|1N|], Mass (KiloGram 1N))
    /// </example>
    let singleWithUnit u v = [| v |] |> withUnit u


    /// <summary>
    /// create a ValueUnit with syntax like
    /// u |> withValue v
    /// </summary>
    /// <example>
    /// (Mass (KiloGram 1N)) |> withValue [| 1N; 2N; 3N |] = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let withValue v u = create u v


    /// <summary>
    /// create a ValueUnit with syntax like
    /// u |> withValue v, where v is a single value
    /// </summary>
    /// <example>
    /// (Mass (KiloGram 1N)) |> withSingleValue 1N = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let singleWithValue v u = [| v |] |> create u


    /// create a general unit with unit value n
    /// and string s
    let generalUnit n s = (s, n) |> General


    /// Create a general ValueUnit with unit value
    /// n general text s and value v
    let generalValueUnit v n s = create (generalUnit n s) v


    /// Create a general 'single' ValueUnit with unit value
    /// n general text s and single value v
    let generalSingleValueUnit v n s = generalValueUnit [| v |] n s


    //----------------------------------------------------------------------------
    // Create CombiUnit
    //----------------------------------------------------------------------------


    /// <summary>
    /// Create a CombiUnit with u1, Operator op and unit u2.
    /// Recalculates the unit n values. Takes care of dividing
    /// by the same unitgroups and multipying with count groups.
    /// </summary>
    /// <param name="u1">Unit 1</param>
    /// <param name="op">Operator</param>
    /// <param name="u2">Unit 2</param>
    /// <example>
    /// <code>
    /// createCombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N)) =
    /// CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N))
    /// // Example with same unit division
    /// createCombiUnit (Mass (KiloGram 4N), OpPer, Mass (KiloGram 2N)) =
    /// Count (Times 2N)
    /// // Example with count unit multiplication
    /// createCombiUnit (Mass (KiloGram 4N), OpTimes, Count (Times 2N)) =
    /// Mass (KiloGram 8N)
    /// </code>
    /// </example>
    /// <remarks>
    /// Will fail when adding or subtracting units with different groups
    /// </remarks>
    let createCombiUnit (u1, op, u2) = // ToDo: need to check if this is correct!!
        match u1, u2 with
        | NoUnit, NoUnit -> NoUnit
        | ZeroUnit, ZeroUnit -> ZeroUnit
        | _ ->
            match op with
            | OpPer ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup ZeroUnit ||
                         u2 |> Group.eqsGroup ZeroUnit -> ZeroUnit
                // this is not enough when u2 is combiunit but
                // contains u1!
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = (u1 |> Units.getUnitValue)
                    let n2 = (u2 |> Units.getUnitValue)

                    match n1, n2 with
                    | Some x1, Some x2 -> count |> Units.setUnitValue (x1 / x2)
                    | _ -> count
                | _ when u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 / x2)
                    | _ -> u1
                | _ -> (u1, OpPer, u2) |> CombiUnit
            | OpTimes ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup ZeroUnit -> ZeroUnit
                | _ when u2 |> Group.eqsGroup ZeroUnit -> ZeroUnit
                | _ when
                    u1 |> Group.eqsGroup count
                    && u2 |> Group.eqsGroup count
                    ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 * x2)
                    | _ -> u1
                | _ when u1 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u2 |> Units.setUnitValue (x1 * x2)
                    | _ -> u2
                | _ when u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 * x2)
                    | _ -> u1
                | _ ->
                    // In physics, multiplying quantities with different units, like mass and volume,
                    // doesn't yield a meaningful result and is generally not done.

                    // The multiplication of physical quantities should be dimensionally consistent,
                    // meaning the units on both sides of the equation should be the same.
                    // This is a principle of dimensional analysis.

                    // Mass is typically measured in kilograms (kg) and volume is measured in cubic meters (m^3).
                    // The product of mass and volume would have units of kg*m^3,
                    // which is not a standard unit and doesn't correspond to any commonly recognized physical quantity.

                    // However, there are instances in physics where you multiply quantities with different units
                    // to get a meaningful result. For example, multiplying mass (kg) and acceleration (m/s^2)
                    // gives you force (N), which is meaningful and consistent with Newton's second law (F=ma).
                    (u1, OpTimes, u2) |> CombiUnit
            | OpPlus
            | OpMinus ->
                match u1, u2 with
                | ZeroUnit, u
                | u, ZeroUnit -> u
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 + x2)
                    | _ -> u1
                | _ ->
                    failwith <| $"Cannot combine units {u1} and {u2} with operator {op}"


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpPer and unit u2. If u1 is u2
    /// then return Count (Times 1N)
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// <code>
    /// Mass (KiloGram 1N) |> per (Volume (Liter 2N)) = CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 2N))
    /// // units are the same, so return Count (Times 1N)
    /// Mass (KiloGram 1N) |> per (Mass (KiloGram 1N)) = Count (Times 1N)
    /// </code>
    /// </example>
    let per u2 u1 = (u1, OpPer, u2) |> createCombiUnit


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpTimes and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Distance (Meter 2N) |> times (Distance (Meter 3N)) = CombiUnit (Distance (Meter 2N), OpTimes, Distance (Meter 3N))
    /// </example>
    let times u2 u1 = (u1, OpTimes, u2) |> createCombiUnit


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpPlus and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Mass (KiloGram 1N) |> plus (Volume (Liter 1N)) = CombiUnit (Mass (KiloGram 1N), OpPlus, Volume (Liter 1N))
    /// </example>
    let plus u2 u1 = (u1, OpPlus, u2) |> createCombiUnit


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpMinus and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Mass (KiloGram 1N) |> minus (Volume (Liter 1N)) = CombiUnit (Mass (KiloGram 1N), OpMinus, Volume (Liter 1N))
    /// </example>
    let minus u2 u1 =
        match u2, u1 with
        | ZeroUnit, u
        | u, ZeroUnit -> u
        | _ -> (u1, OpMinus, u2) |> createCombiUnit


    //----------------------------------------------------------------------------
    // ValueUnit Setters and Getters
    //----------------------------------------------------------------------------


    /// Get the value and the unit of a ValueUnit as a tuple.
    /// Example: get (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    let get (ValueUnit (v, u)) = v, u


    /// Get the value of a ValueUnit.
    /// Example: getValue (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// [|1N; 2N; 3N|]
    let getValue (ValueUnit (v, _)) = v


    /// Just sets a value without calculation.
    /// Example: ValueUnit ([|1N|], Mass (KiloGram 1N)) |> setValue [| 1N; 2N; 3N |] =
    /// ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    let setValue v (ValueUnit (_, u)) = v |> create u


    /// Sets a single value to a ValueUnit.
    /// Example: ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)) |> setSingleValue 1N =
    /// ValueUnit ([|1N|], Mass (KiloGram 1N))
    let setSingleValue v = setValue [| v |]


    /// Get the unit of a ValueUnit
    let getUnit (ValueUnit (_, u)) = u


    /// Get the full unit group of a ValueUnit.
    /// Example: getGroup (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// MassGroup
    let getGroup = getUnit >> Group.unitToGroup


    //----------------------------------------------------------------------------
    // Constants
    //----------------------------------------------------------------------------


    /// Create a 'zero' with unit u
    let zero u = [| 0N |] |> create u


    /// Create a 'one' with unit u
    let one u = [| 1N |] |> create u


    /// A 'count' unit with n = 1
    let count = 1N |> Times |> Count


    //----------------------------------------------------------------------------
    // Logic functions
    //----------------------------------------------------------------------------


    /// Check whether the unit is a count unit, i.e.
    /// belongs to the Count group
    let isCountUnit =
        Group.eqsGroup (1N |> Times |> Count)


    /// Checks whether a ValueUnit has an
    /// empty value
    let isEmpty = getValue >> Array.isEmpty


    let hasZeroUnit = getUnit >> ((=) ZeroUnit)


    let hasNoUnit = getUnit >> ((=) NoUnit)


    /// Check whether a ValueUnit is a single value
    let isSingleValue =
        getValue >> Array.length >> ((=) 1)


    /// Checks whether vu1 is of the
    /// same unit group as vu2
    let eqsGroup vu1 vu2 =
        let u1 = vu1 |> getUnit
        let u2 = vu2 |> getUnit
        u1 |> Group.eqsGroup u2


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


    //----------------------------------------------------------------------------
    // Conversions
    //----------------------------------------------------------------------------


    /// Convert a value to v to the
    /// base value of unit u.
    /// For example u = mg v = 1 -> 1/1000
    let valueToBase u v =
        v
        |> Multipliers.toBase (u |> Multipliers.getMultiplier)

    /// Get the value of a ValueUnit as
    /// a base value.
    /// For example ValueUnit(1000, mg) -> 1
    let toBaseValue vu =
        let v, u = vu |> get
        if u |> Multipliers.getMultiplier = 1N then v
        else
            v |> Array.map (valueToBase u)


    /// Convert a value to v to the
    /// unit value of unit u.
    /// For example u = mg v = 1 -> 1000
    let valueToUnit u v =
        v
        |> Multipliers.toUnit (u |> Multipliers.getMultiplier)


    /// Get the value of a ValueUnit as
    /// a unit value ValueUnit(1, mg) -> 1000
    let toUnitValue vu =
        let v, u = vu |> get
        if u |> Multipliers.getMultiplier = 1N then v
        else
            v |> Array.map (valueToUnit u)


    /// Replace the Value in a ValueUnit to its base.
    /// For example ValueUnit(1000, mg) -> ValueUnit(1, mg)
    let toBase vu =
        let v, u = vu |> get
        if u |> Multipliers.getMultiplier = 1N then vu
        else
            v |> Array.map (valueToBase u) |> create u


    /// Transforms a ValueUnit to its unit.
    /// For example ValueUnit(1, mg) -> ValueUnit(1000, mg)
    let toUnit vu =
        let v, u = vu |> get
        if u |> Multipliers.getMultiplier = 1N then vu
        else
            v |> Array.map (valueToUnit u) |> create u


    /// Make sure that a ValueUnit has a positive value
    /// or zero. NoUnit is transformed to ZeroUnit to enable
    /// logic for calculation of min and max values. If a ValueUnit
    /// has a Value then all negative or zero values are removed.
    let setZeroOrPositive vu =
        if vu |> getUnit = NoUnit then ZeroUnit |> zero
        else
            let vu = vu |> filter (fun br -> br > 0N)

            if vu |> isEmpty |> not then
                vu
            else
                vu |> setValue [| 0N |]



    module private UnitItem =

        type UnitItem =
            | UnitItem of Unit
            | OpPlusMinItem of Operator
            | OpMultItem of Operator
            | OpDivItem of Operator


        // Takes a list of UnitItems and create a Unit from it
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


    //----------------------------------------------------------------------------
    // Operations
    //----------------------------------------------------------------------------



    /// Get a list of the units in a unit u
    let rec getUnits u =
        match u with
        | CombiUnit (ul, _, ur) -> ul |> getUnits |> List.prepend (ur |> getUnits)
        | _ -> [ u ]



    // separate numerators from denominators
    // isNum is true when we are in the numerator
    // and is false when we are in the denominator
    let rec internal numDenom isNum u =
        match u with
        | CombiUnit (ul, OpTimes, ur) ->
            let lns, lds = ul |> numDenom isNum
            let rns, rds = ur |> numDenom isNum
            lns @ rns, lds @ rds
        | CombiUnit (ul, OpPer, ur) ->
            if isNum then
                let lns, lds = ul |> numDenom true
                let rns, rds = ur |> numDenom false
                lns @ rns, lds @ rds
            else
                let lns, lds = ur |> numDenom true
                let rns, rds = ul |> numDenom false
                lns @ rns, lds @ rds
        | _ ->
            if isNum then
                (u |> getUnits, [])
            else
                ([], u |> getUnits)


    // Build a unit from a list of numerators and denominators.
    // Uses an accumulator to build the unit and a boolean to indicate
    // whether there is a count unit in the numerator.
    // isCount is true when there is a count unit in the numerator
    // and false when there is no count unit in the numerator.
    // Note when ns = ds then the result is isCount = true and u = NoUnit
    let rec build ns ds (isCount, u) =
        match ns with
        | [] ->
            match ds with
            | [] ->
                if isCount && u = NoUnit then
                    (true, count)
                else
                    (isCount, u)
            | _ ->
                let d = ds |> List.rev |> List.reduce times

                if u = NoUnit then
                    Count(Times 1N) |> per d
                else
                    u |> per d
                |> fun u -> (isCount, u)
        | h :: tail ->
            if ds |> List.exists (Group.eqsGroup h) then
                build tail (ds |> List.removeFirst (Group.eqsGroup h)) (true, u)
            else
                let isCount =
                    isCount
                    || (u |> Group.eqsGroup count)
                    || (h |> Group.eqsGroup count)

                if u = NoUnit then h else u |> times h
                |> fun u -> build tail ds (isCount, u)


    /// <summary>
    /// Simplify a unit u such that units are algebraically removed or
    /// transformed to count units, where applicable.
    /// </summary>
    /// <param name="u">The unit to simplify</param>
    /// <returns>
    /// The simplified unit
    /// </returns>
    let simplifyUnit u =
        if u = NoUnit then u
        else
            let ns, ds = u |> numDenom true

            (false, NoUnit)
            |> build ns ds
            |> fun (_, newU) ->
                // nothing changed so just return original
                if u = newU then u
                else
                    match newU with
                    | CombiUnit(u1, OpPer, CombiUnit(u2, OpTimes, u3)) ->
                        if u2 |> Group.eqsGroup u3 then newU
                        else
                            CombiUnit(CombiUnit(u1, OpPer, u2), OpPer, u3)
                    | _ -> newU


    /// <summary>
    /// Simplify a value unit u such that units are algebraically removed or
    /// transformed to count units, where applicable.
    /// </summary>
    /// <param name="vu">The value unit to simplify</param>
    /// <returns>
    /// The simplified value unit
    /// </returns>
    /// <example>
    /// <code>
    /// simplify (ValueUnit ([|1N; 2N; 3N|], CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N)))) =
    /// ValueUnit ([|1N; 2N; 3N|], CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N)))
    /// simplify (ValueUnit ([|1N; 2N; 3N|], CombiUnit (Mass (KiloGram 1N), OpPer, Mass (KiloGram 1N)))) =
    /// ValueUnit ([|1N; 2N; 3N|], Count (Times 1N))
    /// </code>
    /// </example>
    let rec simplify vu =
        let v, u = vu |> get

        if u = NoUnit then
            vu
        else
            let u = simplifyUnit u
            v
            |> create u
            // calculate to the new combiunit
            |> toUnitValue
            // recreate again to final value unit
            |> create u


    /// <summary>
    /// Calculate a ValueUnit by applying an operator op
    /// to ValueUnit vu1 and vu2. The operator can be addition,
    /// subtraction, multiplication or division.
    /// The boolean b results in whether or not the result is
    /// simplified.
    /// </summary>
    /// <param name="b">Whether or not to simplify the result</param>
    /// <param name="op">The operator to apply</param>
    /// <param name="vu1">The first ValueUnit</param>
    /// <param name="vu2">The second ValueUnit</param>
    /// <returns>
    /// The result of applying the operator to the ValueUnits
    /// </returns>
    /// <remarks>
    /// fails when adding or subtracting different units
    /// </remarks>
    /// <example>
    /// <code>
    /// calc true (+) (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// ValueUnit ([|2N; 3N; 4N; 5N; 6N|], Mass (KiloGram 1N))
    /// </code>
    /// </example>
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
            |> fun u -> if b then simplifyUnit u else u
        // recreate valueunit with base value and combined unit
        v
        |> create u
        // calculate to the new combiunit
        |> toUnitValue
        // recreate again to final value unit
        |> create u


    /// <summary>
    /// Compare a ValueUnit vu1 with vu2.Comparison can be:
    /// greater, greater or equal, smaller, smaller or equal.
    /// </summary>
    /// <remarks>
    /// Checks if the comparison is true for all individual values.
    /// Doesn't work for equal.
    /// </remarks>
    /// <param name="cp">The operator to use</param>
    /// <param name="vu1">The first ValueUnit</param>
    /// <param name="vu2">The second ValueUnit</param>
    /// <returns>
    /// True if the comparison is true, false otherwise
    /// </returns>
    /// <example>
    /// <code>
    /// // 1 kg > 1000 g = true
    /// cmp (>) (ValueUnit ([|1N|], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = true
    /// </code>
    /// </example>
    let cmp cp vu1 vu2 =
        // ToDo need better eqsGroup like mg/kg/day = (mg/kg)/day = (mg/kg*day) <> mg/(kg/day) = mg*day/kg
        if (vu1 |> hasZeroUnit |> not && vu2 |> hasZeroUnit |> not) &&
           (vu1 |> hasNoUnit |> not && vu2 |> hasNoUnit |> not) &&
           (vu1 |> eqsGroup vu2 |> not) then
            failwith $"cannot compare {vu1} with {vu2}"
        //else
        let vs1 = vu1 |> toBaseValue
        let vs2 = vu2 |> toBaseValue

        Array.allPairs vs1 vs2
        |> Array.forall (fun (v1, v2) -> v1 |> cp <| v2)


    /// <summary>
    /// Determine if vu1 equals vu2. This is true when
    /// both ValueUnits have the same unit and the same value
    /// </summary>
    /// <param name="vu1">The first ValueUnit</param>
    /// <param name="vu2">The second ValueUnit</param>
    let eqs vu1 vu2 =
        let vs1 =
            vu1 |> toBaseValue |> Array.distinct |> Array.sort

        let vs2 =
            vu2 |> toBaseValue |> Array.distinct |> Array.sort

        vs1 = vs2


    /// <summary>
    /// Apply a function fValue to the Value of a ValueUnit vu
    /// </summary>
    /// <param name="fValue">The function to apply to the Value</param>
    /// <param name="vu">The ValueUnit</param>
    /// <returns>The updated ValueUnit</returns>
    /// <example>
    /// <code>
    /// let fValue = Array.map ((+) 1N) // add 1 to each value
    /// applyToValue fValue (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N))
    /// </code>
    /// </example>
    let applyToValue fValue vu =
        let u = vu |> getUnit
        vu |> getValue |> fValue |> create u


    // Apply an array function to a ValueUnit
    let internal applyArrayFunction fArr fVal vu =
        let u = vu |> getUnit
        vu |> getValue |> fArr fVal |> create u


    /// <summary>
    /// Filter the values in a ValueUnit using a predicate function pred.
    /// </summary>
    /// <param name="fPred">The predicate function to use</param>
    let filterValues fPred =
        applyArrayFunction Array.filter fPred


    /// <summary>
    /// Map the values in a ValueUnit using a function fMap.
    /// </summary>
    /// <param name="fMap">The function to appy to each individual value</param>
    let mapValues fMap = applyArrayFunction Array.map fMap


    /// <summary>
    /// Validates the values of Value for a ValueUnit.
    /// </summary>
    /// <param name="fValid">The validator function</param>
    /// <param name="errMsg">The error message</param>
    /// <param name="vu">The ValueUnit</param>
    /// <returns>
    /// Result.Ok vu if the values are valid, Result.Error errMsg otherwise
    /// </returns>
    let validate fValid errMsg vu =
        if vu |> getValue |> fValid then
            vu |> Ok
        else
            errMsg |> Error


    /// Check if first ValueUnit is greater than second ValueUnit
    /// Example: gt (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = true
    let gt = cmp (>)


    /// Check if first ValueUnit is smaller than second ValueUnit
    /// Example: st (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = false
    // Check if left vu is greater than or equal to right vu
    let st = cmp (<)


    /// Check if first ValueUnit is greater than or equal to second ValueUnit
    /// Example: gte (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = true
    let gte = cmp (>=)


    /// Check if first ValueUnit is smaller than or equal to second ValueUnit
    /// Example: ste (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = false
    let ste = cmp (<=)


    /// <summary>
    /// Convert a ValueUnit vu to
    /// a unit u.
    /// Do not convert to no unit or zerounit
    /// </summary>
    /// <example>
    /// <code>
    /// //For example 1 gram -> 1000 mg:
    /// ValueUnit([|1N|], Units.Mass.gram) |> convertTo Units.Mass.milliGram
    /// </code>
    /// </example>
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


    /// <summary>
    /// Get the Value of a ValueUnit vu as the base value
    /// </summary>
    /// <example>
    /// <code>
    /// ValueUnit([|1N|], Units.Mass.kiloGram) |> getBaseValue = [|1000N|]
    /// </code>
    /// </example>
    let getBaseValue = toBase >> getValue


    /// Check if Value is zero
    let isZero =
        getValue >> Array.forall ((=) 0N)

    /// Check if Value is > 0
    let gtZero =
        getValue >> Array.forall ((<) 0N)

    /// Check if Value >= 0
    let gteZero =
        getValue >> Array.forall ((<=) 0N)

    /// Check if Value < 0
    let stZero =
        getValue >> Array.forall ((>) 0N)

    /// Check if Value <= 0
    let steZero =
        getValue >> Array.forall ((>=) 0N)


    /// Get the smallest value of a ValueUnit.
    /// Returns None if the ValueUnit is empty.
    let minValue vu =
        if vu |> isEmpty then None
        else
            vu
            |> applyToValue (Array.min >> Array.singleton)
            |> Some


    /// Get the largest value of a ValueUnit.
    /// Returns None if the ValueUnit is empty.
    let maxValue vu =
        if vu |> isEmpty then None
        else
            vu
            |> applyToValue (Array.max >> Array.singleton)
            |> Some


    // Helper function to calculate the min or max value
    // that is inclusive or exclusive and is a multiple of
    // increment 'incr'.
    let internal multipleOf f incr vu =
        vu
        |> toBase
        |> applyToValue (fun vs ->
            let incr =
                incr |> getBaseValue |> Set.ofArray

            vs |> Array.map (f incr) //|> Array.map snd
        )
        |> toUnit


    /// <summary>
    /// Calculate the minimum value of a ValueUnit that is a minimum inclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// minInclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|4N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let minInclMultipleOf incr vu =
        multipleOf BigRational.minInclMultipleOf incr vu
        |> minValue
        |> Option.defaultValue vu


    /// <summary>
    /// Calculate the minimum value of a ValueUnit that is a minimum exclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// minExclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|4N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let minExclMultipleOf incr vu =
        multipleOf BigRational.minExclMultipleOf incr vu
        |> minValue
        |> Option.defaultValue vu


    /// <summary>
    /// Calculate the maximum value of a ValueUnit that is a maximum inclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// maxInclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|8N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let maxInclMultipleOf incr vu =
        multipleOf BigRational.maxInclMultipleOf incr vu
        |> maxValue
        |> Option.defaultValue vu


    /// <summary>
    /// Calculate the maximum value of a ValueUnit that is a maximum exclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// maxExclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|9N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let maxExclMultipleOf incr vu =
        multipleOf BigRational.maxExclMultipleOf incr vu
        |> maxValue
        |> Option.defaultValue vu


    /// <summary>
    /// Get the denominators of the value of a ValueUnit.
    /// </summary>
    /// <example>
    /// <code>
    /// // returns 2, 3, 5
    /// denominator (ValueUnit ([|1N/2N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let denominator =
        getValue >> (Array.map BigRational.denominator)


    /// <summary>
    /// Get the numerators of the value of a ValueUnit.
    /// </summary>
    /// <example>
    /// <code>
    /// // returns 1, 2, 3
    /// numerator (ValueUnit ([|1N/2N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let numerator =
        getValue >> (Array.map BigRational.numerator)


    /// <summary>
    /// Filter the value of a value unit using
    /// a predicate function pred. This function
    /// is parameterized on the base value of the value
    /// unit.
    /// </summary>
    /// <example>
    /// <code>
    /// // Get all even numbers, note that the base value of 1 KiloGram is 1000
    /// // so the predicate function is applied to 1000, 2000, 3000, etc.
    /// // ValueUnit = ValueUnit ([|-2N; 2N|], Mass (KiloGram 1N))
    /// filter (fun br -> (br / 2000N).Denominator = 1I) (ValueUnit ([|1N; 2N; 3N; -1N; -2N; -3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let filter pred =
        toBase
        >> applyToValue (Array.filter pred)
        >> toUnit


    /// <summary>
    /// Remove all big rational multiples of the value of a ValueUnit.
    /// </summary>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|2N; 3N; 5N; 7N|], Mass (KiloGram 1N))
    /// removeBigRationalMultiples (ValueUnit ([|2N..1N..10N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let removeBigRationalMultiples =
        toBase
        >> applyToValue Array.removeBigRationalMultiples
        >> toUnit


    /// <summary>
    /// Get the intersection of two ValueUnits.
    /// </summary>
    /// <param name="vu1">ValueUnit 1</param>
    /// <param name="vu2">ValueUnit 2</param>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))
    /// intersect (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
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


    /// <summary>
    /// Check if a ValueUnit is a subset of another ValueUnit.
    /// </summary>
    /// <param name="vu1">ValueUnit 1 the possible subset</param>
    /// <param name="vu2">ValueUnit 2 the set to check against</param>
    /// <example>
    /// <code>
    /// // returns true
    /// isSubset (ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// // returns false
    /// isSubset (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let isSubset vu1 vu2 =
        let s1 = vu1 |> getBaseValue |> Set.ofArray
        let s2 = vu2 |> getBaseValue |> Set.ofArray
        Set.isSubset s1 s2


    /// <summary>
    /// Check if ValueUnit vu1 contains ValueUnit vu2.
    /// </summary>
    /// <param name="vu2">The ValueUnit to check</param>
    /// <param name="vu1">The ValueUnit that should contain vu2</param>
    /// <example>
    /// <code>
    /// // returns true
    /// containsValue (ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// // returns true
    /// containsValue (ValueUnit ([|2000N; 3000N|], Mass (Gram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// // returns false
    /// containsValue (ValueUnit ([|2N; 3N|], Mass (Gram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let containsValue vu2 vu1 =
        vu2
        |> toBase
        |> getValue
        |> Array.forall (fun v -> vu1 |> toBase |> getValue |> Array.exists ((=) v))


    /// <summary>
    /// Take the first n elements of a Value in a ValueUnit
    /// </summary>
    /// <param name="n">The n elements to take</param>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|1N; 2N|], Mass (KiloGram 1N))
    /// takeFirst 2 (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let takeFirst n = applyToValue (Array.take n)


    /// <summary>
    /// Take the last n elements of a Value in a ValueUnit
    /// </summary>
    /// <param name="n">The n elements to take</param>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))
    /// takeLast 2 (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let takeLast n =
        applyToValue (Array.rev >> Array.take n >> Array.rev)


    /// <summary>
    /// Get the count of elements in a Value
    /// </summary>
    /// <example>
    /// <code>
    /// // returns 3
    /// valueCount (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let valueCount = getValue >> Array.length


    let setNearestValue vu1 vu2 =
        if vu1 |> valueCount <> 1 then vu2
        else
            if vu1 >? vu2 || vu1 <? vu2 then vu2
            else
                let vu1 = vu1 |> getBaseValue |> Array.head
                let vs2 = vu2 |> getBaseValue
                // find the nearest value in vs2 to vu1
                vs2
                |> Array.map (fun v -> (v, v - vu1 |> BigRational.Abs))
                |> Array.minBy snd
                |> fun (v, _) ->
                    setSingleValue v vu2
                    |> toUnit


    //----------------------------------------------------------------------------
    // ValueUnit string functions
    //----------------------------------------------------------------------------


    /// <summary>
    /// Returns a string representation of a ValueUnit comparing operator.
    /// When  the operator is unknown, "unknown comparison" is returned.
    /// </summary>
    /// <example>
    /// <code>
    /// cmpToStr (>) = ">"
    /// </code>
    /// </example>
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


    /// <summary>
    /// Get the user readable string version
    /// of a unit, i.e. without unit group between
    /// brackets
    /// </summary>
    /// <example>
    /// <code>
    /// unitToReadableDutchString (Mass (KiloGram 1N)) = "kg"
    /// </code>
    /// </example>
    let unitToReadableDutchString u =
        u
        |> Units.toString Units.Dutch Units.Short
        |> String.removeBrackets


    /// <summary>
    /// Get the user readable string version
    /// </summary>
    /// <param name="brf">The function to turn a BigRational into a string</param>
    /// <param name="loc">The localization to use</param>
    /// <param name="verb">The verbosity to use</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// toString
    ///     BigRational.toString
    ///     Units.Dutch
    ///     Units.Short
    ///     (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) = "1;2;3 kg[Mass]"
    /// </code>
    /// </example>
    let toString brf loc verb vu =
        let v, u = vu |> get

        $"{v |> Array.map brf |> Array.distinct |> Array.toReadableString} {Units.toString loc verb u}"


    /// <summary>
    /// Get the user readable string version in Dutch with verbosity short
    /// </summary>
    let toStringDutchShort =
        toString BigRational.toString Units.Dutch Units.Short

    /// <summary>
    /// Get the user readable string version in Dutch with verbosity long
    /// </summary>
    let toStringDutchLong =
        toString BigRational.toString Units.Dutch Units.Long

    /// <summary>
    /// Get the user readable string version in English with verbosity short
    /// </summary>
    let toStringEngShort =
        toString BigRational.toString Units.English Units.Short

    /// <summary>
    /// Get the user readable string version in English with verbosity long
    /// </summary>
    let toStringEngLong =
        toString BigRational.toString Units.English Units.Long

    /// <summary>
    /// Get the user readable string version in Dutch with verbosity short and
    /// value as decimal
    /// </summary>
    let toStringDecimalDutchShort =
        toString (BigRational.toDecimal >> string) Units.Dutch Units.Short

    /// <summary>
    /// Get the user readable string version in Dutch with verbosity long and
    /// value as decimal
    /// </summary>
    let toStringDecimalDutchLong =
        toString (BigRational.toDecimal >> string) Units.Dutch Units.Long

    /// <summary>
    /// Get the user readable string version in English with verbosity short and
    /// value as decimal
    /// </summary>
    let toStringDecimalEngShort =
        toString (BigRational.toDecimal >> string) Units.English Units.Short

    /// <summary>
    /// Get the user readable string version in English with verbosity long and
    /// value as decimal
    /// </summary>
    let toStringDecimalEngLong =
        toString (BigRational.toDecimal >> string) Units.English Units.Long


    /// <summary>
    /// Get the user readable string version in Dutch with verbosity short and
    /// value as decimal with a fixed precision
    /// </summary>
    /// <param name="prec">The precision</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// toStringDecimalDutchShortWithPrec 2 (ValueUnit ([|1N/3N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
    /// = "0,33;0,67;0,6 kg"
    /// </code>
    /// </example>
    let toStringDecimalDutchShortWithPrec prec vu =
        let v, u = vu |> get

        let vs =
            v
            |> Array.map BigRational.toDecimal
            |> Array.map (Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision prec)
            |> Array.distinct
            |> Array.toReadableString

        let us = u |> unitToReadableDutchString

        vs + " " + us


    /// <summary>
    /// Parse a string into a ValueUnit
    /// </summary>
    /// <example>
    /// <code>
    /// // returns Success: ValueUnit ([|3N|], Volume (MilliLiter 1N))
    /// fromString "3 mL[Volume]"
    ///
    /// // returns Success: ValueUnit ([|3N|], CombiUnit (Volume (MilliLiter 1N), OpPer, Time (Minute 1N)))
    /// fromString "3 mL/min" = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </code>
    /// </example>
    let fromString = Parser.parse



    module Operators =

        let (=?) vu1 vu2 = eqs vu1 vu2

        let (>?) vu1 vu2 = cmp (>) vu1 vu2

        let (<?) vu1 vu2 = cmp (<) vu1 vu2

        let (>=?) vu1 vu2 = cmp (>=) vu1 vu2

        let (<=?) vu1 vu2 = cmp (<=) vu1 vu2

        /// <summary>
        /// Convert a ValueUnit vu to
        /// </summary>
        /// <param name="vu">The ValueUnit</param>
        /// <param name="u">The Unit to convert to</param>
        let (==>) vu u = vu |> convertTo u


    module Dto =

        module Group = ValueUnit.Group


        type Dto() =
            member val Value : (string * decimal) [] = [||] with get, set
            member val Unit = "" with get, set
            member val Group = "" with get, set
            member val Short = true with get, set
            member val Language = "" with get, set
            member val Json = "" with get, set


        [<Literal>]
        let english = "english"

        [<Literal>]
        let dutch = "dutch"

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

                let v, u = vu |> ValueUnit.get
                let v =
                    v |> Array.map (fun v ->
                        v |> string,
                        v |> BigRational.toDecimal
                    )

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
                dto.Json <- vu |> ValueUnit.getUnit |> Json.serialize

                dto |> Some


        let toDtoDutchShort vu = vu |> toDto true dutch |> Option.get
        let toDtoDutchLong vu = vu |> toDto false dutch |> Option.get
        let toDtoEnglishShort vu = vu |> toDto true english |> Option.get
        let toDtoEnglishLong vu = vu |> toDto false english |> Option.get


        let fromDto (dto: Dto ) =
            let v =
                dto.Value |> Array.map (fst >> BigRational.parse)

            if dto.Json |> String.notEmpty then dto.Json |> Json.deSerialize<Unit> |> Some
            else
                if dto.Group |> String.isNullOrWhiteSpace then
                    dto.Unit
                    |> Units.fromString
                else
                    // TODO only works for "per" combiunits
                    let us = dto.Unit |> String.split "/"
                    let gs = dto.Group |> String.split "/"

                    if us |> List.length <> (gs |> List.length) then
                        printfn $"warning: {us} not the same length as {gs}!"
                        printfn $"unit: {dto.Unit} group {dto.Group}!"

                        $"{dto.Unit}[{dto.Group}]"
                        |> Units.fromString
                    else
                        List.zip us gs
                        |> List.choose (fun (u, g) ->
                            $"{u}[{g}]"
                            |> Units.fromString
                        )
                        |> function
                            | [] -> None
                            | [u] -> u |> Some
                            | u::rest ->
                                rest
                                |> List.fold(fun acc u ->
                                    CombiUnit (acc, OpPer, u)
                                ) u
                                |> Some
            |> function
            | None -> None
            | Some u ->
                v
                |> ValueUnit.withUnit u
                |> Some



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

    /// <summary>
    /// Convert a ValueUnit vu to
    /// </summary>
    /// <param name="vu">The ValueUnit</param>
    /// <param name="u">The Unit to convert to</param>
    static member (==>)(vu, u) = vu |> ValueUnit.convertTo u



module Tests =

    open Swensen.Unquote
    open Units


    // Test numDenom
    let testNumDenom () =
        // kg
        let u = Mass (KiloGram 1N)
        // calc kg = num [kg], denom []
        let act = u |> ValueUnit.numDenom true
        let exp = ([Mass (KiloGram 1N)], [])

        test <@ act = exp @>

        // 1/kg
        let u = Count (Times 1N) |> per (Mass (KiloGram 1N))
        // calc 1/kg = num [times], denom [kg]
        let act = u |> ValueUnit.numDenom true
        let exp = ([Count (Times 1N)], [Mass (KiloGram 1N)])
        test <@ act = exp @>

        // 1/(kg*m)
        let u = Count (Times 1N) |> per (Mass (KiloGram 1N) |> times (Distance (Meter 1N)))
        // calc 1/1/(kg*m) = num [kg; m], denom [times]
        let act = u |> ValueUnit.numDenom false
        let exp = ([Mass (KiloGram 1N); Distance (Meter 1N)], [Count (Times 1N)])
        test <@ act = exp @>

        // kg*m/L
        let u = Mass (KiloGram 1N) |> per (Volume (Liter 1N)) |> times (Distance (Meter 1N))
        // calc kg*m/L = num [[kg;m], denom [L]
        let act = u |> ValueUnit.numDenom true
        let exp = ([Mass (KiloGram 1N); Distance (Meter 1N)], [Volume (Liter 1N)])
        test <@ act = exp @>

        // kg*m/L
        let u = Mass (KiloGram 1N) |> per (Volume (Liter 1N)) |> times (Distance (Meter 1N))
        // calc 1/(kg*m/L) = num[L], denom [kg;m]
        let act = u |> ValueUnit.numDenom false
        let exp = ([Volume (Liter 1N)], [Mass (KiloGram 1N); Distance (Meter 1N)])
        test <@ act = exp  @>

        // (kg*m)/(m*L)
        let u = Mass (KiloGram 1N) |> times (Distance (Meter 1N)) |> per (Volume (Liter 1N) |> times (Distance (Meter 1N)))
        // calc (kg*m)/(m*L) = num [kg;m], denom [L;m]
        let act = u |> ValueUnit.numDenom true
        let exp = ([Mass (KiloGram 1N); Distance (Meter 1N)], [ Volume (Liter 1N); Distance (Meter 1N)])
        test <@ act = exp @>


        // (kg*m)/(m*L)
        let u = Mass (KiloGram 1N) |> times (Distance (Meter 1N)) |> per (Volume (Liter 1N) |> times (Distance (Meter 1N)))
        // calc 1/(kg*m)/(m*L) = num [L;m], denom [kg;m]
        let act = u |> ValueUnit.numDenom false
        let exp = ([ Volume (Liter 1N); Distance (Meter 1N)], [Mass (KiloGram 1N); Distance (Meter 1N)])
        test <@ act = exp @>


    // Test the 'build' function
    let testBuild () =
        // [] [] -> (false, NoUnit)
        let act =
            (false, NoUnit)
            |> ValueUnit.build [] []
        let exp = (false, NoUnit)
        test <@ act = exp @>

        // [kg] [] -> (false, kg)
        let act =
            (false, NoUnit)
            |> ValueUnit.build [Mass (KiloGram 1N)] []
        let exp = (false, Mass (KiloGram 1N))
        test <@ act = exp @>

        // [] [kg] -> (false, 1/kg)
        let act =
            (false, NoUnit)
            |> ValueUnit.build [] [Mass (KiloGram 1N)]
        let exp = (false, Count (Times 1N) |> per (Mass (KiloGram 1N)))
        test <@ act = exp @>

        // [kg] [kg] -> (true, 1)
        let act =
            (false, NoUnit)
            |> ValueUnit.build [Mass (KiloGram 1N)] [Mass (KiloGram 1N)]
        let exp = (true, Count (Times 1N))
        test <@ act = exp @>

