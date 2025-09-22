
#load "load.fsx"


#time


open Informedica.GenOrder.Lib


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib.Utils

let rates =
    [ 100N .. 10N .. 1000N ]
    |> List.append [ 50N .. 5N .. 95N ]
    |> List.append [ 10N .. 1N .. 49N ]
    |> List.append [ 1N / 10N .. 1N / 10N .. 99N / 10N ]
