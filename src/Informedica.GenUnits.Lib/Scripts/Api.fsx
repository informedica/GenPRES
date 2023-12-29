
//#I "C:\Development\Informedica\libs\GenUnits\src\Informedica.GenUnits.Lib\scripts"
//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL

open Swensen.Unquote

open FParsec


"piece[General]"
|> run Parser.parseUnit

"day[General]"
|> Units.fromString

open Informedica.Utils.Lib

let isGeneral s =
    let xs = (String.regex "[^\[\]]+(?=\])").Matches(s)
    xs
    |> Seq.map _.Value
    |> Seq.forall (String.equalsCapInsens "general")


"piece"
|> isGeneral
