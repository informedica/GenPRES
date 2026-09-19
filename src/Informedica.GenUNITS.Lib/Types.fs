namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL


// Foundational unit types.
//
// Wrapped in an [<AutoOpen>] module so the types AND their DU cases are visible
// unqualified across every file of this namespace and to consumers via
// `open Informedica.GenUnits.Lib`. Union cases of a bare namespace-level type
// are not accessible unqualified from other files in the same namespace, and
// the library uses `Mass`, `Distance`, `NoUnit`, ... unqualified throughout.
//
// The `ValueUnit` type lives with its operations and operators in ValueUnit.fs.

[<AutoOpen>]
module Types =

    type Operator =
        | OpTimes
        | OpPer
        | OpPlus
        | OpMinus


    type CountUnit = | Times of BigRational

    type MassUnit =
        | KiloGram of BigRational
        | Gram of BigRational
        | MilliGram of BigRational
        | MicroGram of BigRational
        | NanoGram of BigRational

    type DistanceUnit =
        | Meter of BigRational
        | CentiMeter of BigRational
        | MilliMeter of BigRational

    type VolumeUnit =
        | Liter of BigRational
        | DeciLiter of BigRational
        | MilliLiter of BigRational
        | MicroLiter of BigRational
        // droplet has multiplier * droplets per mL
        | Droplet of BigRational * BigRational

    type TimeUnit =
        | Year of BigRational
        | Month of BigRational
        | Week of BigRational
        | Day of BigRational
        | Hour of BigRational
        | Minute of BigRational
        | Second of BigRational

    type MolarUnit =
        | Mole of BigRational
        | MilliMole of BigRational
        | MicroMole of BigRational

    type InternationalUnit =
        | MIU of BigRational
        | IU of BigRational
        | MilliIU of BigRational

    type WeightUnit =
        | WeightKiloGram of BigRational
        | WeightGram of BigRational

    type HeightUnit =
        | HeightMeter of BigRational
        | HeightCentiMeter of BigRational

    type BSAUnit = | M2 of BigRational

    type EnergyUnit =
        | Calorie of BigRational
        | KiloCalorie of BigRational

    type Unit =
        | NoUnit
        // special case to enable efficient min max calculations where
        // either min or max approaches zero, ZeroUnit means that whatever
        // the actual unit of the value, the value is zero
        | ZeroUnit
        | CombiUnit of Unit * Operator * Unit
        | General of (string * BigRational)
        | Count of CountUnit
        | Mass of MassUnit
        | Distance of DistanceUnit
        | Volume of VolumeUnit
        | Time of TimeUnit
        | Molar of MolarUnit
        | International of InternationalUnit
        | Weight of WeightUnit
        | Height of HeightUnit
        | BSA of BSAUnit
        | Energy of EnergyUnit
