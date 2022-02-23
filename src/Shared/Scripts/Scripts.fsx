// this first to FSI
#load "../Data.fs"
#load "../Domain.fs"

open Shared

Patient.create None (Some 1) None None None None
|> Option.map (Patient.toString false)
|> Option.defaultValue ""
