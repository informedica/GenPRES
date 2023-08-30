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
        |> ValueUnit.apply (fun _ -> v)


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
                    |> Option.map (decimal >> BigRational.fromDecimal)
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
        let general n = (n, 1N) |> toGeneral


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
        let nDroplet n = n |> Droplet |> toVolume

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
        | CombiUnit (u1, op, u2) ->
            failwith
            <| $"Cannot map combined unit %A{(u1, op, u2) |> CombiUnit}"


    /// Try find a the unit details in the list of units
    /// for unit u
    /// Example: tryFind (Mass.kiloGram) = Some { ... }
    let tryFind u =
        match UnitDetails.units |> List.tryFind (fun udt -> udt.Unit = u) with
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
                    | Success (u, _, _) -> u
                    | Failure _ ->
                        s |> Units.General.general
                )
                |> List.reduce (fun u1 u2 -> u1 |> Units.per u2)
                |> Some
            | _ -> None


    /// Turn a unit u to a string with
    /// localization loc and verbality verb.
    /// Example: toString Dutch Short (Mass (KiloGram 1N)) = "kg[Mass]"
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



module ValueUnit =


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
            | CombiUnit _ -> None

        app u


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


        /// <summary>
        /// Checks whether u1 contains
        /// the same unit groups as u2
        /// </summary>
        /// <example>
        /// eqsGroup (Mass (KiloGram 1N)) (Mass (Gram 1N)) = true
        /// </example>
        let eqsGroup u1 u2 =
            if u1 = u2 then
                true
            else
                let g1 = u1 |> unitToGroup
                let g2 = u2 |> unitToGroup

                g1 = g2


        /// Returns group g as a string
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


        /// Get all the units that belong to a group in a list.
        /// Example: getGroupUnits MassGroup = [Mass (KiloGram 1N); Mass (Gram 1N); ...]
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
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = u1 |> getUnitValue
                    let n2 = u2 |> getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> setUnitValue (x1 + x2)
                    | _ -> u1
                | _ ->
                    failwith <| $"Cannot combine units {u1} and {u2} with operator {op}"


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpPer and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Mass (KiloGram 1N) |> per (Volume (Liter 2N)) = CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 2N))
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
    let plus u2 u1 =
        match u2, u1 with
        | ZeroUnit, u
        | u, ZeroUnit -> u
        | _ -> (u1, OpPlus, u2) |> createCombiUnit


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


    let setZeroNonNegative vu =
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

            vs |> Array.map (f incr) //|> Array.map snd
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


    /// Filter the value of a value unit using
    /// a predicate function pred. This function
    /// is parameterized on the base value of the value
    /// unit.
    let filter pred =
        toBase
        >> applyToValue (Array.filter pred)
        >> toUnit

    let removeBigRationalMultiples =
        toBase
        >> applyToValue Array.removeBigRationalMultiples
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


    //----------------------------------------------------------------------------
    // ValueUnit string functions
    //----------------------------------------------------------------------------


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

        $"{v |> Array.map brf |> Array.distinct |> Array.toReadableString} {Units.toString loc verb u}"


    let toStringDutchShort =
        toString BigRational.toString Units.Dutch Units.Short

    let toStringDutchLong =
        toString BigRational.toString Units.Dutch Units.Long

    let toStringEngShort =
        toString BigRational.toString Units.English Units.Short

    let toStringEngLong =
        toString BigRational.toString Units.English Units.Long

    let toStringDecimalDutchShort =
        toString (BigRational.toDecimal >> string) Units.Dutch Units.Short

    let toStringDecimalDutchLong =
        toString (BigRational.toDecimal >> string) Units.Dutch Units.Long

    let toStringDecimalEngShort =
        toString (BigRational.toDecimal >> string) Units.English Units.Short

    let toStringDecimalEngLong =
        toString (BigRational.toDecimal >> string) Units.English Units.Long


    /// Turn a `ValueUnit` `vu` into
    /// a string using precision `prec`.
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


    /// Parse a string into a ValueUnit
    let fromString = Parser.parse



    module Operators =

        let (=?) vu1 vu2 = eqs vu1 vu2

        let (>?) vu1 vu2 = cmp (>) vu1 vu2

        let (<?) vu1 vu2 = cmp (<) vu1 vu2

        let (>=?) vu1 vu2 = cmp (>=) vu1 vu2

        let (<=?) vu1 vu2 = cmp (<=) vu1 vu2

        let (==>) vu u = vu |> convertTo u


    module Dto =


        module Group = ValueUnit.Group


        type Dto() =
            member val Value : (string * decimal) [] = [||] with get, set
            member val Unit = "" with get, set
            member val Group = "" with get, set
            member val Short = true with get, set
            member val Language = "" with get, set


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

                dto |> Some

        let toDtoDutchShort vu = vu |> toDto true dutch |> Option.get
        let toDtoDutchLong vu = vu |> toDto false dutch |> Option.get
        let toDtoEnglishShort vu = vu |> toDto true english |> Option.get
        let toDtoEnglishLong vu = vu |> toDto false english |> Option.get

        let fromDto (dto: Dto ) =
            let v =
                dto.Value |> Array.map (fst >> BigRational.parse)

            dto.Unit
            |> Units.fromString
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

    static member (==>)(vu, u) = vu |> ValueUnit.convertTo u