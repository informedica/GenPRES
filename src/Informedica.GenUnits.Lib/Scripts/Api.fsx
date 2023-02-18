
//#I "C:\Development\Informedica\libs\GenUnits\src\Informedica.GenUnits.Lib\scripts"
//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL

// (mg/piece) / (mass/mass) = (mg/piece) / count = mg/piece  
Api.eval "1 mg[Mass] / 1 piece[General]"
|> fun vu -> vu / (Api.eval "1 mg[Mass] / 1 mg[Mass]")
|> ValueUnit.toStringEngShort

Api.eval "100 mg[Mass] * 200 mg[Mass] / 5 ml[Volume] / 10 times[Count]"
|> ValueUnit.toStringEngShort

let vu =
    Api.eval "100 mg[Mass] * 1 x[Count]/day[Time]"
//|> ValueUnit.toStringEngShort
vu
|> ValueUnit.simplify
|> ValueUnit.toStringEngShort
|> ignore


Api.eval "1 piece[General]/kg[Weight]/day[Time]"
|> ValueUnit.simplify

4. / 2. / 2. = 4. / (2. * 2.)

4. / (2. / 2.)

// (mg / kg) * (x / day) = mg / kg * day = mg / kg / day

Api.eval "100 mg[Mass]/1 kg[Weight]"
|> fun vu ->
    vu / (Api.eval "1 day[Time]/1 times[Count]")
    |> ValueUnit.simplify
    |> ValueUnit.toStringEngShort

