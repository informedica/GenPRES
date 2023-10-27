

#load "load.fsx"


#time



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
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

dto1.Constraints.Incr.Value.Value <- [|("x", 1m)|]
dto2.Constraints.Incr.Value.Value <- [|("x", 1m)|]


open Informedica.GenSolver.Lib.Variable.Operators

let ovar3 =
    (dto1 |> OrderVariable.Dto.fromDto).Variable ^*
    (dto1 |> OrderVariable.Dto.fromDto).Variable
