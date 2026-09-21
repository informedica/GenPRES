namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib.BCL


module Parser =

    open FParsec

    open Units


    /// <summary>
    /// Set the value of a unit.
    /// </summary>
    /// <example>
    /// setUnitValue (KiloGram 1N |> Mass) 2N = (KiloGram 2N |> Mass)
    /// </example>
    let setUnitValue u v = u |> apply (fun _ -> v)


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
    let pnumber: Parser<float, unit> = pfloat .>> ws


    /// <summary>
    /// Parses a BigRational from a float
    /// </summary>
    /// <remarks>
    /// A float that <c>BigRational.fromFloat</c> cannot convert (e.g. infinity)
    /// silently parses to <c>0N</c> rather than failing the parser. This is kept
    /// intentionally; callers must not rely on the parser to reject such values.
    /// </remarks>
    /// <example>
    /// Example: "1.2" |> run pBigRat -> Success: 6/5
    /// </example>
    let pBigRat: Parser<BigRational, unit> = pnumber |>> (BigRational.fromFloat >> Option.defaultValue 0N)


    /// <summary>
    /// Parse a unit with an optional group annotation.
    /// Uses word boundaries to ensure complete unit matches and prevent partial matches.
    /// For example, "stuk" won't match "s" (seconds) because 'tuk' follows.
    /// </summary>
    /// <example>
    /// Example: "kg" |> run (pUnitGroup "kg" "mass") -> Success: "kg" <br/>
    /// Example: "kg[Mass]" |> run (pUnitGroup "kg" "mass") -> Success: "kg" <br/>
    /// Example: "stuk" will not match "s" (seconds) due to word boundary check
    /// </example>
    let pUnitGroup (u: string) g =
        let pu =
            pstringCI u
            >>. (notFollowedBy (satisfy (fun c -> System.Char.IsLetterOrDigit c)))
            >>% u

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
                {|
                    unit = ud.Abbreviation.Eng
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Abbreviation.Dut
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Abbreviation.EngPlural
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Abbreviation.DutchPlural
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Name.Eng
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Name.Dut
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Name.EngPlural
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                {|
                    unit = ud.Name.DutchPlural
                    grp = ud.Group
                    f = setUnitValue ud.Unit
                |}
                yield!
                    ud.Synonyms
                    |> List.map (fun s ->
                        {|
                            unit = s
                            grp = ud.Group
                            f = setUnitValue ud.Unit
                        |}
                    )
            ]
        )
        // need to change nan to nnn to avoid getting a float 'nan'
        // NOTE: raw substring replace — a unit name containing the literal
        // "nan" (e.g. a general unit "banana") would be mangled. Acceptable
        // here because all entries are known abbreviations/names/synonyms.
        |> List.map (fun r -> {| r with unit = r.unit |> String.replace "nan" "nnn" |})
        |> List.distinctBy (fun r -> r.unit, r.grp)
        // Deliberately drop kg / kilogram from the Mass group so these tokens
        // always resolve to the Weight group: mass-kg is intentionally
        // unreachable through the parser (see the kg->Weight regression test).
        |> List.filter (fun r ->
            (r.unit = "kg" && r.grp = Group.MassGroup
             || r.unit = "kilogram" && r.grp = Group.MassGroup)
            |> not
        )
        |> List.sortByDescending (fun r -> r.unit |> String.length, r.unit)
        //|> List.map (fun r -> printfn $"{r}"; r)
        |> List.map (fun r ->
            let g = $"{r.grp |> Group.toString}"

            attempt (
                opt pfloat .>> ws .>>. (pUnitGroup r.unit g >>% r.f)
                |>> (fun (f, u) -> f |> Option.bind BigRational.fromFloat |> Option.defaultValue 1N |> u)
            )
        )
        |> choice


    /// <summary>
    /// Parse a General unit with the format: name[General] or just name
    /// When parsing without the [General] annotation, it will match any text
    /// that is not a known unit. Known units are checked first with proper
    /// word boundaries to avoid partial matches.
    /// </summary>
    /// <example>
    /// "stuk[General]" |> run pGeneralUnit -> Success: General ("stuk", 1N) <br/>
    /// "stuk" |> run pGeneralUnit -> Success: General ("stuk", 1N) <br/>
    /// "120 stuk" |> run pGeneralUnit -> Success: General ("stuk", 1N)
    /// </example>
    let pGeneralUnit =
        let pName = many1Chars (noneOf "[ \t\r\n*/")

        attempt (
            opt pfloat .>> ws .>>. (pName .>> ws .>> opt (pstringCI "[General]"))
            |>> (fun (mult, name) ->
                mult
                |> Option.bind BigRational.fromFloat
                |> Option.defaultValue 1N
                |> fun v -> General(name, v)
            )
        )


    /// <summary>
    /// Parse a complex unit using FParsec's OperatorPrecedenceParser.
    /// Tries to match known units first (pUnit), then falls back to general units (pGeneralUnit).
    /// This ensures known units take precedence while allowing arbitrary general unit names.
    /// </summary>
    /// <returns>Parser of Unit, unit</returns>
    /// <example>
    /// "mg/kg[Weight]" |> run parseUnit -> Success: CombiUnit (Mass (MilliGram 1N), OpPer, Weight (WeightKiloGram 1N)) <br/>
    /// "stuk" |> run parseUnit -> Success: General ("stuk", 1N)
    /// </example>
    let parseUnit =

        let opp = OperatorPrecedenceParser<Unit, unit, unit>()
        let expr = opp.ExpressionParser

        opp.TermParser <- pUnit <|> pGeneralUnit <|> between (str_ws "(") (str_ws ")") expr

        let ( *! ) u1 u2 = (u1, OpTimes, u2) |> CombiUnit
        let (/!) u1 u2 = (u1, OpPer, u2) |> CombiUnit

        opp.AddOperator(InfixOperator("*", ws, 1, Associativity.Left, ( *! )))
        opp.AddOperator(InfixOperator("/", ws, 1, Associativity.Left, (/!)))

        ws >>. expr .>> eof
