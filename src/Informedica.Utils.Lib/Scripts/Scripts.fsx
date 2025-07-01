//#I __SOURCE_DIRECTORY__

#load "load.fsx"

#time

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL

Array.Tests.testAll()
Seq.Tests.testAll()
Set.Tests.testRemoveBigRationalMultiples()
Path.Tests.testAll ()
Csv.Tests.testAll ()
Char.Tests.testAll ()
BigInteger.Tests.testFareySequence()

