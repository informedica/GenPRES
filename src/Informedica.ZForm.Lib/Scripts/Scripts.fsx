#load "load.fsx"

#time

open System


open Informedica.ZIndex.Lib
open Informedica.ZForm.Lib


GenPresProduct.filter "paracetamol" "" "rectaal"


{ Dto.dto with
     Generic = "paracetamol"
     Shape = "zetpil"
     Route = "rectaal"
}
|> Dto.processDto

