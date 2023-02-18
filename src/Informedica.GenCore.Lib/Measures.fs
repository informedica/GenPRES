namespace Informedica.GenCore.Lib


[<AutoOpen>]
module Measures =


    [<Measure>] type cm

    [<Measure>] type m

    [<Measure>] type bsa = m^2

    [<Measure>] type kg

    [<Measure>] type gram

    [<Measure>] type min

    [<Measure>] type day

    [<Measure>] type week

    [<Measure>] type month

    [<Measure>] type year

    [<Measure>] type mg

    [<Measure>] type mL

    [<Measure>] type dL

    [<Measure>] type L

    [<Measure>] type mmol

    [<Measure>] type microMol

    [<Measure>] type normalM2




module Constants =


    let [<Literal>] fullTerm = 40<week>

    let [<Literal>] premature = 37<week>

    let [<Literal>] daysInWeek = 7

    let [<Literal>] weeksInYear = 52

    let [<Literal>] monthsInYear = 12

    let [<Literal>] daysInYear = 365

    let [<Literal>] kilo = 1000

    let [<Literal>] centi = 100

    let [<Literal>] milli = 1000

    let [<Literal>] micro = 1000000

    let [<Literal>] deci = 10


module Conversions =

    open System


    let fromInt (one: int<_>) (x :int) = x * one


    let fromDecimal (one: decimal<_>) (x : decimal) = x * one


    let dayFromInt x : int<day> = x |> fromInt 1<day>


    let weekFromInt x : int<week> = x |> fromInt 1<week>


    let monthFromInt x : int<month> = x |> fromInt 1<month>


    let yearFromInt x : int<year> = x |> fromInt 1<year>


    let gramFromInt x : int<gram> = x |> fromInt 1<gram>


    let gramFromDecimal x : decimal<gram> = x |> fromDecimal 1m<gram>


    let kgFromDecimal x : decimal<kg> = x |> fromDecimal 1m<kg>


    let cmFromInt x : int<cm> = x |> fromInt 1<cm>


    let cmFromDecimal x : decimal<cm> = x |> fromDecimal 1m<cm>


    let meterFromInt x : int<m> = x |> fromInt 1<m>


    let meterFromDecimal x : decimal<m> = x |> fromDecimal 1m<m>


    let deciLiterFromDecimal x : decimal<dL> = x |> fromDecimal 1m<dL>


    let milliLiterFromDecimal x : decimal<mL> = x |> fromDecimal 1m<mL>


    let literFromDecimal x : decimal<L> = x |> fromDecimal 1m<L> 


    let kilo = decimal Constants.kilo

    let centi = decimal Constants.centi


    let weeksToDays (weeks: int<week>) =
        weeks * Constants.daysInWeek
        |> int
        |> dayFromInt


    let daysToWeeks (days: int<day>) =
        (int days) / Constants.daysInWeek |> weekFromInt,
        (int days) % Constants.daysInWeek |> dayFromInt


    let intYearsToDays (years : int<year>) =
        years * Constants.daysInYear
        |> int
        |> dayFromInt


    let decimalYearsToIntDays (years : decimal<year>) =
        years * (decimal Constants.daysInYear)
        |> decimal |> int
        |> dayFromInt


    let decimalKgToDecimalGram (kg : decimal<kg>) =
        (decimal kg * kilo)
        |> gramFromDecimal


    let intGramToDecKg (gram : int<gram>) =
        (decimal gram) / kilo
        |> kgFromDecimal


    let decKgToIntGram (kg : decimal<kg>) =
        kg
        |> decimalKgToDecimalGram
        |> decimal
        |> int
        |> gramFromInt


    let decMtoDecCm (m : decimal<m>) =
        m * centi
        |> decimal
        |> cmFromDecimal


    let decMtoIntCm (m : decimal<m>) =
        m * centi
        |> decimal
        |> int
        |> cmFromInt

    
    let inline intToString u1 u2 x =
        let x = int x
        match x with
        | _ when x |> abs = 0 -> $"{x} {u1}"
        | _ when x |> abs = 1 -> $"{x} {u1}"
        | _ when x |> abs > 1 -> $"{x} {u2}"
        | _ -> ""


    let yearToString u1 u2 (y : int<year>) = intToString u1 u2 y 

    let monthToString u1 u2 (m : int<month>) = intToString u1 u2 m

    let weekToString u1 u2 (w : int<week>) = intToString u1 u2 w

    let dayToString u1 u2 (d : int<day>) = intToString u1 u2 d


    let milliLiterToLiter (x: decimal<mL>) =
        x / (Constants.milli |> decimal)
        |> decimal
        |> literFromDecimal


    let literToMilliLiter (x : decimal<L>) =
        x * (Constants.milli |> decimal)
        |> decimal
        |> milliLiterFromDecimal


    let deciLiterToLiter (x: decimal<dL>) =
        x / (Constants.deci |> decimal)
        |> decimal
        |> literFromDecimal


    let literToDeciLiter (x : decimal<dL>) =
        x * (Constants.deci |> decimal)
        |> decimal
        |> deciLiterFromDecimal


    module Creatinine =

        let kMicroMolePerLiter = 88.42<microMol/L>

        let kMilliGramPerDeciLiter = (1./kMicroMolePerLiter |> float) * (1.<mg/dL>)


        let toMicroMolePerLiter (cr : float<mg/dL>) : float<microMol/L> = (cr  |> float) * kMicroMolePerLiter

        let toMilliGramPerDeciLiter (cr : float<microMol/L>) : float<mg/dL> = (cr  |> float) * kMilliGramPerDeciLiter
    

    module Urea =

        let kMilliGramPerDeciLiter = 1. / 0.3571 * (1.<mg/dL>)

        let kMilliMolePerLiter = 0.3571 * (1.<mmol/L>)


        let toMilliMolePerLiter (urea : float<mg/dL>) = (urea |> float) * kMilliMolePerLiter


        let toMilliGramPerDeciLiter (urea : float<mmol/L>) = (urea |> float) * kMilliGramPerDeciLiter
