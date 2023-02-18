
//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open MathNet.Numerics
open Informedica.GenUnits.Lib
open ValueUnit

// Example parsing a frequency
"1 x / 3 weken"
|> Parser.parse

// Example parsing a dose
// using kg as a weight
let parse =
    let exclude u (ud : Units.UnitDetails) = ud.Unit <> u
    Units.units
    // leave out kg as mass so only kg as weigh is recognized
    |> List.filter (exclude Units.Mass.kiloGram)
    // leave out gr as weight to avoid confusion with gr as mass
    |> List.filter (exclude Units.Weight.gram)
    |> Parser.parseWitUnits

"10 mg / kg / dag"
|> parse
|> function
| Some x ->
    "10 kg"
    |> parse
    |> function
    | Some w -> w * x
    | None -> failwith "cannot parse"
| None ->
    failwith "cannot parse"

// fix calculation count when combining units
"1 g/100 mg"
|> parse
|> Option.map (fun vu -> vu * ([|1N|] |> create count))

"10 gram/dag"
|> parse
|> Option.map (fun vu ->
    vu
    |> get
    |> snd
    |> setUnitValue 2N
)

Units.Mass.milliGram |> Multipliers.getMultiplier

let oneMg = "4000 mg" |> parse |> Option.get
let onePerDay = "1 x / day" |> parse |> Option.get

oneMg * onePerDay
|> ValueUnit.toBaseValue




