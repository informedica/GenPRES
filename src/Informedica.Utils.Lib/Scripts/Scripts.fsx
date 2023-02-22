//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL


"9-87987"
|> String.replaceNumbers ""

"[ab]"
|> String.removeTextBetween "[" "]"

"(ab)"
|> String.removeTextBetween "(" ")"


1.123456m
|> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3


1000000.1
|> Double.toStringNumberNLWithoutTrailingZerosFixPrecision 3

