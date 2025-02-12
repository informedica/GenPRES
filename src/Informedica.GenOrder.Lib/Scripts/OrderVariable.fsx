

#load "load.fsx"


#time

open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib



let path = Some $"{__SOURCE_DIRECTORY__}/log.txt"



let ovar1 =
    OrderVariable.createNew
        (Name.fromString "ovar1")
        Units.Count.times


let ovar2 =
    OrderVariable.createNew
        (Name.fromString "ovar2")
        Units.Count.times

let dto1 = ovar1 |> OrderVariable.Dto.toDto
let dto2 = ovar2 |> OrderVariable.Dto.toDto

dto1.Variable.IncrOpt <- ValueUnit.Dto.dto () |> Some
dto1.Variable.IncrOpt.Value.Unit <- "x"
dto1.Variable.IncrOpt.Value.Value <- [| 1N |]

dto2.Variable.IncrOpt <- ValueUnit.Dto.dto () |> Some
dto2.Variable.IncrOpt.Value.Unit <- "x"
dto2.Variable.IncrOpt.Value.Value <- [| 1N |]


open Informedica.GenSolver.Lib.Variable.Operators

dto1.Variable.ValsOpt <- ValueUnit.Dto.dto () |> Some
dto1.Variable.ValsOpt.Value.Group <- "Count"
dto1.Variable.ValsOpt.Value.Unit <- "x"
dto1.Variable.ValsOpt.Value.Value <- [| 1N |]

dto1.Variable.ValsOpt.Value |> ValueUnit.Dto.fromDto

dto1 |> OrderVariable.Dto.fromDto

let ovar3 =
    (dto1 |> OrderVariable.Dto.fromDto).Variable ^*
    (dto1 |> OrderVariable.Dto.fromDto).Variable
