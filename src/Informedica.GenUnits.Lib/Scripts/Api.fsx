
//#I "C:\Development\Informedica\libs\GenUnits\src\Informedica.GenUnits.Lib\scripts"
//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL

open Swensen.Unquote
open Tests
open ValueUnit


let drDef = Units.Volume.droplet
let dr25 = Units.Volume.dropletWithDropsPerMl 1N 25N

let mL = Units.Volume.milliLiter

mL |> Units.per drDef
mL |> Units.per dr25

let v1 = 1N |> singleWithUnit Units.Volume.droplet
let v2 = 1N |> singleWithUnit Units.Volume.milliLiter

v2 / v1
v2 / (1N |> singleWithUnit (Units.Volume.dropletWithDropsPerMl 1N 25N))



Tests.testNumDenom()

// Test Array.removeBigRationalMultiples
let testRemoveBigRationalMultiples () =
    let test (act : BigRational[]) (exp : BigRational[]) =
        let act = act |> Array.removeBigRationalMultiples
        test <@ act = exp @>

    test [| |] [|  |]
    test [| 1N; 1N; 1N; 1N; 1N |] [| 1N |]
    test [| 1N; 2N; 3N; 4N; 5N |] [| 1N |]
    test [| 2N; 3N; 4N; 5N |] [|2N; 3N; 5N|]
    test [| 2N; 3N |] [| 2N; 3N |]
    test [| 2N; 3N; 4N |] [| 2N; 3N |]

testRemoveBigRationalMultiples()

[| 1N; 12N |] |> Array.removeBigRationalMultiples
|> Array.distinct

"ng[Mass]"
|> Units.fromString

Units.UnitDetails.units
|> List.iter (fun ud -> printfn $"{ud}")



module Parser =

    open FParsec

    open Units


    let setUnitValue u v =
        u
        |> ValueUnit.apply (fun _ -> v)


    let ws = spaces


    let str_ws s = pstring s >>. ws


    let pnumber = pfloat .>> ws


    let pBigRat =
        pnumber
        |>> (BigRational.fromFloat >> Option.defaultValue 0N)


    /// <summary>
    /// Parse a unit with an optional group
    /// </summary>
    let pUnitGroup (u : string) g =
        let pu = u |> pstringCI
        let pg = $"[%s{g}]" |> pstringCI
        (pu .>> ws .>> (opt pg))


    let pUnit =
        Units.UnitDetails.units
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



open FParsec


let test s =
    match s |> Parser.parse with
    | Success(result, _, _)   -> printfn "Success: %A" result
    | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg

let testParser s p =
    match s |> run p with
    | Success(result, _, _)   -> printfn "Success: %A" result
    | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg



"mL[volume]"
|> run Parser.parseUnit



"[1.4;  2] nanog[Mass]/kg/2 dag[Time]"
|> test


"[1.4;  2] mg/kg[Weight]/day"
|> test


"xxnog"
|> run Parser.pnumber


"nanog[Mass]"
//|> String.replace "nan" "nnn"
|> run Parser.parseUnit


(*
didn't catch System.Exception: cannot add or subtract different units CombiUnit
  (CombiUnit (Volume (MilliLiter 1N), OpPer, Weight (WeightKiloGram 1N)), OpPer,
   Time (Hour 1N)) CombiUnit
  (CombiUnit (Volume (MilliLiter 1N), OpPer, Time (Hour 1N)), OpPer,
   Weight (WeightKiloGram 1N))
*)
let un1 =
    CombiUnit
      (CombiUnit (Volume (MilliLiter 1N), OpPer, Weight (WeightKiloGram 1N)), OpPer,
       Time (Hour 1N))

let un2 =
    CombiUnit
      (CombiUnit (Volume (MilliLiter 1N), OpPer, Time (Hour 1N)), OpPer,
       Weight (WeightKiloGram 1N))

open Informedica.Utils.Lib
open Group

/// Get a list of the groups in a group g
let rec getGroups g =
    match g with
    | CombiGroup (gl, _, gr) -> gl |> getGroups |> List.prepend (gr |> getGroups)
    | _ -> [ g ]


// separate numerators from denominators
// isNum is true when we are in the numerator
// and is false when we are in the denominator
let rec internal numDenom isNum g =
    match g with
    | CombiGroup (gl, OpTimes, gr) ->
        let lns, lds = gl |> numDenom isNum
        let rns, rds = gr |> numDenom isNum
        lns @ rns, lds @ rds
    | CombiGroup (gl, OpPer, gr) ->
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
    if u1 = u2 then
        true
    else
        let g1Num, g1Den=
            u1 |> unitToGroup |> numDenom true
        let g2Num, g2Den =
            u2 |> unitToGroup |> numDenom true

        g1Num |> List.sort = (g2Num |> List.sort) &&
        g1Den |> List.sort = (g2Den |> List.sort)


eqsGroup un1 un2
