namespace Informedica.GenOrder.Lib




/// Functions that deal with the `VariableUnit` type
module OrderVariable =

    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenSolver.Lib
    open Informedica.GenUnits.Lib

    module ValueRange = Variable.ValueRange
    module Minimum    = ValueRange.Minimum
    module Maximum    = ValueRange.Maximum
    module Increment  = ValueRange.Increment
    module ValueSet   = ValueRange.ValueSet

    module VariableDto = Variable.Dto
    module Units = ValueUnit.Units
    module Multipliers = ValueUnit.Multipliers


    module Constraints =


        let create min incr max vs =
            {
                Min = min
                Max = max
                Incr = incr
                Values = vs
            }

        let toString (cs : Constraints) =
            let toStr = ValueUnit.toStringDutchShort

            match cs.Values with
            | None ->
                let min = cs.Min |> Option.map Minimum.toBoolValueUnit
                let max = cs.Max |> Option.map Maximum.toBoolValueUnit
                let incr =
                    cs.Incr
                    |> Option.map Increment.toValueUnit
                    |> Option.map List.singleton

                MinIncrMax.Calculator.toStringNL toStr min incr max
            | Some vs ->
                vs
                |> ValueSet.toValueUnit
                |> ValueUnit.toStringDutchShort


        let map fMin fMax fIncr fVals (cs : Constraints) =
            { cs with
                Min = cs.Min |> Option.map fMin
                Max = cs.Max |> Option.map fMax
                Incr = cs.Incr |> Option.map fIncr
                Values = cs.Values |> Option.map fVals

            }


    /// Create a `OrderVariable` with preset values
    let create n min incr max vs cs =
        ValueRange.create min incr max vs
        |> fun vlr ->
            let var = Variable.create id n vlr
            {
                Constraints = cs
                Variable = var
            }


    /// Create a new `VariableUnit` with
    /// `Name` **nm** and `Unit` **un**
    let createNew n un =
        let vu = 0N |> ValueUnit.createSingle un
        let min = Minimum.create false vu |> Some

        Constraints.create min None None None
        |> create n min None None None

    /// Apply **f** to `VariableUnit` **vru**
    let apply f (ovar: OrderVariable) = ovar |> f


    let map f (ovar: OrderVariable) =
        { ovar with
            Variable =
                { ovar.Variable with
                    Values =
                        ovar.Variable.Values
                        |> ValueRange.mapValueUnit f
                }

        }


    /// Utility function to facilitate type inference
    let get = apply id


    /// Get the `Variable` from a `VariableUnit`
    let getVar { Variable = var } = var


    /// Get the `Variable.Name` from a `VariableUnit` **vru**
    let getName ovar = (ovar |> getVar).Name


    let eqsName ovar1 ovar2 = (ovar1 |> getName) = (ovar2 |> getName)


    let applyConstraints (ovar : OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable.Values
                |> ValueRange.setOptMin ovar.Constraints.Min
                |> ValueRange.setOptMax ovar.Constraints.Max
                |> ValueRange.setOptIncr ovar.Constraints.Incr
                // only set a ValueSet if there is no increment
                |> fun vr ->
                    if ovar.Constraints.Incr.IsSome then vr
                    else
                        vr
                        |> ValueRange.setOptVs ovar.Constraints.Values
                |> Variable.setValueRange true ovar.Variable
        }


    let increaseIncrement lim incr (ovar : OrderVariable) =
        { ovar with
            Variable = ovar.Variable |> Variable.increaseIncrement lim incr
        }


    let fromOrdVar toOvar c ovars a =
        ovars
        |> List.tryFind (eqsName (a |> toOvar))
        |> Option.map c
        |> Option.defaultValue a


    /// Set the 'Name' to the `Variable` of the `VariableUnit`
    let setName nm ovar =
        { ovar with
            Variable = ovar.Variable |> Variable.setName nm
        }


    /// Get the string representation of a `VariableUnit` **vru**
    let toString exact ovar =
        let ns = ovar |> getName |> Variable.Name.toString

        ns +
        (ovar.Variable
        |> Variable.getValueRange
        |> ValueRange.toString exact)


    /// Returns the values with the string equivalents
    /// of an order variable value set
    let toValueUnitStringList get x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.getValSet
        |> Option.map (ValueSet.toValueUnit >> ValueUnit.toStringDutchShort)


    let toValueUnitString get (_ : int) x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.toString false //TODO: need something to set precision and number formatting
        |> String.replace "x" "keer"
        |> String.replace "*" "/"


    let toValueUnitMarkdown get (prec : int) x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.toMarkdown prec
        |> String.replace "x" "keer"
        |> String.replace "*" "/"


    let minIncrMaxToValues n (ovar: OrderVariable) =
        { ovar with
            Variable =
                if ovar.Variable |> Variable.isMinIncrMax |> not then ovar.Variable
                else
                    ovar.Variable
                    |> Variable.minIncrMaxToValues n
        }


    let equalUnit (ovar: OrderVariable) =
        [
            ovar.Constraints.Min |> Option.map (Minimum.toValueUnit >> ValueUnit.getUnit)
            ovar.Constraints.Incr |> Option.map (Increment.toValueUnit >> ValueUnit.getUnit)
            ovar.Constraints.Max |> Option.map (Maximum.toValueUnit >> ValueUnit.getUnit)
            ovar.Constraints.Values |> Option.map (ValueSet.toValueUnit >> ValueUnit.getUnit)
        ]
        |> List.choose id
        |> List.distinct
        |> fun xs -> if xs.Length = 1 then xs.Head |> Some else None
        |> function
            | None -> ovar
            | Some u ->
                { ovar with
                    Variable = ovar.Variable |> Variable.setUnit u
                }


    /// Type and functions to handle the `Dto`
    /// data transfer type for a `VariableUnit`
    module Dto =


        type Dto () =
            member val Name = "" with get, set
            member val Constraints = Variable.Dto.dto () with get, set
            member val Variable = Variable.Dto.dto () with get, set


        let dto () = Dto ()


        let fromDto (dto: Dto) =
            let cs =
                let vs =
                    dto.Constraints.ValsOpt
                    |> Option.bind ValueUnit.Dto.fromDto
                    |> Option.bind (fun vu ->
                        if vu |> ValueUnit.isEmpty then None
                        else
                            vu
                            |> ValueSet.create
                            |> Some
                    )

                let incr =
                    dto.Constraints.IncrOpt
                    |> Option.bind ValueUnit.Dto.fromDto
                    |> Option.bind (fun vu ->
                        if vu |> ValueUnit.isEmpty then None
                        else
                            vu
                            |> Increment.create
                            |> Some
                    )

                let min  = dto.Constraints.MinOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Minimum.create  dto.Constraints.MinIncl)))
                let max  = dto.Constraints.MaxOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Maximum.create  dto.Constraints.MaxIncl)))
                Constraints.create min incr max vs

            let n = dto.Name |> Name.fromString
            let vals =
                dto.Variable.ValsOpt
                |> Option.bind ValueUnit.Dto.fromDto
                |> Option.bind (fun vu ->
                    if vu |> ValueUnit.isEmpty then None
                    else
                        vu
                        |> ValueSet.create
                        |> Some
                )

            let incr =
                dto.Variable.IncrOpt
                |> Option.bind ValueUnit.Dto.fromDto
                |> Option.bind (fun vu ->
                    if vu |> ValueUnit.isEmpty then None
                    else
                        vu
                        |> Increment.create
                        |> Some
                )

            let min  = dto.Variable.MinOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Minimum.create  dto.Variable.MinIncl)))
            let max  = dto.Variable.MaxOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Maximum.create  dto.Variable.MaxIncl)))

            create n min incr max vals cs


        let toDto (ovar : OrderVariable) =
            let vuToDto = ValueUnit.Dto.toDto true "dutch"
            let dto = dto ()
            let vr =
                ovar
                |> getVar
                |> Variable.getValueRange

            dto.Name <-
                ovar |> getName |> Name.toString

            dto.Variable.ValsOpt <-
                vr
                |> ValueRange.getValSet
                |> Option.map ValueSet.toValueUnit
                |> Option.bind vuToDto
            dto.Variable.IncrOpt <-
                vr
                |> ValueRange.getIncr
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto
            dto.Variable.MinOpt <-
                vr
                |> ValueRange.getMin
                |> Option.map Minimum.toValueUnit
                |> Option.bind vuToDto
            dto.Variable.MinIncl <-
                vr
                |> ValueRange.getMin
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false
            dto.Variable.MaxOpt <-
                vr
                |> ValueRange.getMax
                |> Option.map Maximum.toValueUnit
                |> Option.bind vuToDto
            dto.Variable.MaxIncl <-
                vr
                |> ValueRange.getMax
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto.Constraints.ValsOpt <-
                ovar.Constraints.Values
                |> Option.map ValueSet.toValueUnit
                |> Option.bind vuToDto
            dto.Constraints.IncrOpt <-
                ovar.Constraints.Incr
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MinOpt <-
                ovar.Constraints.Min
                |> Option.map Minimum.toValueUnit
                |> Option.bind vuToDto
            dto.Constraints.MinIncl <-
                ovar.Constraints.Min
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false
            dto.Constraints.MaxOpt <-
                ovar.Constraints.Max
                |> Option.map Maximum.toValueUnit
                |> Option.bind vuToDto
            dto.Constraints.MaxIncl <-
                ovar.Constraints.Max
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto



    /// Type and functions that represent a count
    module Count =

        let count = Count.Count


        let [<Literal>] name = "cnt"


        /// Turn `Count` in a `VariableUnit`
        let toOrdVar (Count.Count cnt) = cnt


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> count


        /// Set a `Count` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar count


        /// Create a `Count` with name **n**
        let create n =
            Units.Count.times
            |> createNew (n |> Name.add name)
            |> Count.Count


        /// Turn a `Count` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `Count` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let applyConstraints = toOrdVar >> applyConstraints >> count




    /// Type and functions that represent a time
    module Time =

        let time = Time.Time


        let [<Literal>] name = "tme"


        /// Turn `Time` in a `VariableUnit`
        let toOrdVar (Time.Time tme) = tme


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> time


        /// Set a `Time` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar time


        /// Create a `Time` with name **n**
        /// with `Unit` **un**
        let create n un =
            un
            |> createNew (n |> Name.add name)
            |> Time.Time


        /// Turn a `Time` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `Time` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> time



    /// Type and functions that represent a frequency
    module Frequency =


        let [<Literal>] name = "frq"


        /// Turn `Frequency` in a `VariableUnit`
        let toOrdVar (Frequency frq) = frq


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> Frequency


        /// Set a `Frequency` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar Frequency


        /// Create a `Frequency` with name **n**
        /// with `Unit` time unit **tu**
        let create n tu =
            match tu with
            | Unit.NoUnit -> Unit.NoUnit
            | _ ->
                Units.Count.times
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> Frequency


        /// Turn a `Frequency` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `Frequency` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> Frequency



    /// Type and functions that represent a concentration,
    /// and a concentration is a quantity per time
    module Concentration =


        let [<Literal>] name = "cnc"


        /// Turn `Concentration` in a `VariableUnit`
        let toOrdVar (Concentration cnc) = cnc


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> Concentration


        /// Set a `Concentration` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar Concentration


        /// Create a `Concentration` with name **n**
        /// and `Unit` **un** per shape unit **su**
        let create n un su =
            match un, su with
            | Unit.NoUnit, _
            | _, Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per su
            |> createNew (n |> Name.add name)
            |> Concentration


        /// Turn a `Concentration` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `Concentration` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> Concentration



    /// Type and functions that represent a quantity
    module Quantity =


        let [<Literal>] name = "qty"


        /// Turn `Quantity` in a `VariableUnit`
        let toOrdVar (Quantity qty) = qty


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> Quantity


        /// Set a `Quantity` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar Quantity


        /// Create a `Quantity` with name **n**
        /// and `Unit` **un**
        let create n un =
            un
            |> createNew (n |> Name.add name)
            |> Quantity


        /// Turn a `Quantity` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `Quantity` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let applyConstraints = toOrdVar >> applyConstraints >> Quantity


        let increaseIncrement lim incr = toOrdVar >> increaseIncrement lim incr >> Quantity



    /// Type and functions that represent a quantity per time
    module PerTime =


        let [<Literal>] name = "ptm"


        /// Turn `PerTime` in a `VariableUnit`
        let toOrdVar (PerTime ptm) = ptm


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> PerTime


        /// Set a `PerTime` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar PerTime


        /// Create a `PerTime` with name **n**
        /// and `Unit` **un** and time unit **tu**
        let create n un tu =
            match un with
            | Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> PerTime


        /// Turn a `PerTime` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `PerTime` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> PerTime



    module Rate =


        let [<Literal>] name = "rte"


        /// Turn `PerTime` in a `VariableUnit`
        let toOrdVar (Rate rte) = rte


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> Rate


        /// Set a `PerTime` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar Rate


        /// Create a `PerTime` with name **n**
        /// and `Unit` **un** and time unit **tu**
        let create n un tu =
            match un with
            | Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> Rate


        /// Turn a `PerTime` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `PerTime` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let applyConstraints = toOrdVar >> applyConstraints >> Rate


        let increaseIncrement lim incr = toOrdVar >> increaseIncrement lim incr >> Rate



    /// Type and functions that represent a total
    module Total =


        let [<Literal>] name = "tot"


        /// Turn `Quantity` in a `VariableUnit`
        let toOrdVar (Total tot) = tot


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> Total


        /// Set a `Quantity` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar Total


        /// Create a `Quantity` with name **n**
        /// and `Unit` **un**
        let create n un =
            un
            |> createNew (n |> Name.add name)
            |> Total


        /// Turn a `Quantity` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `Quantity` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let applyConstraints = toOrdVar >> applyConstraints >> Total



    /// Type and functions that represent a adjusted quantity,
    /// and a adjusted quantity is a quantity per time
    module QuantityAdjust =


        let [<Literal>] name = "qty_adj"


        /// Turn `QuantityAdjust` in a `VariableUnit`
        let toOrdVar (QuantityAdjust qty_adj) = qty_adj


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> QuantityAdjust


        /// Set a `QuantityAdjust` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar QuantityAdjust


        /// Create a `QuantityAdjust` with name **n**
        /// and `Unit` **un** per adjust **adj**
        let create n un adj =
            match un, adj with
            | Unit.NoUnit, _
            | _, Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per adj
            |> createNew (n |> Name.add name)
            |> QuantityAdjust


        /// Turn a `QuantityAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `QuantityAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let applyConstraints = toOrdVar >> applyConstraints >> QuantityAdjust



    /// Type and functions that represent a adjusted total,
    /// and a adjusted total is a quantity per time
    module PerTimeAdjust =


        let [<Literal>] name = "ptm_adj"


        /// Turn `TotalAdjust` in a `VariableUnit`
        let toOrdVar (PerTimeAdjust ptm_adj) = ptm_adj


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> (map ValueUnit.correctAdjustOrder >> PerTimeAdjust)


        /// Set a `TotalAdjust` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar (map ValueUnit.correctAdjustOrder >> PerTimeAdjust)


        /// Create a `TotalAdjust` with name **n**
        /// and `Unit` **un** per adjust **adj** per time unit **tu**
        let create n un adj tu =
            match un, adj, tu with
            | Unit.NoUnit, _, _
            | _, Unit.NoUnit, _
            | _, _, Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per adj
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> PerTimeAdjust


        /// Turn a `TotalAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `TotalAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> PerTimeAdjust



    /// Type and functions that represent a adjusted total,
    /// and a adjusted total is a quantity per time
    module RateAdjust =


        let [<Literal>] name = "rte_adj"


        /// Turn `TotalAdjust` in a `VariableUnit`
        let toOrdVar (RateAdjust rte_adj) = rte_adj


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> (map ValueUnit.correctAdjustOrder >> RateAdjust)


        /// Set a `TotalAdjust` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar (map ValueUnit.correctAdjustOrder >> RateAdjust)


        /// Create a `TotalAdjust` with name **n**
        /// and `Unit` **un** per adjust **adj** per time unit **tu**
        let create n un adj tu =
            match un, adj, tu with
            | Unit.NoUnit, _, _
            | _, Unit.NoUnit, _
            | _, _, Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per adj
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> RateAdjust


        /// Turn a `TotalAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `TotalAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> RateAdjust



    /// Type and functions that represent a adjusted quantity,
    /// and a adjusted quantity is a quantity per time
    module TotalAdjust =


        let [<Literal>] name = "tot_adj"


        /// Turn `QuantityAdjust` in a `VariableUnit`
        let toOrdVar (TotalAdjust tot_adj) = tot_adj


        let toDto = toOrdVar >> Dto.toDto


        let fromDto dto = dto |> Dto.fromDto |> TotalAdjust

        /// Set a `QuantityAdjust` with a `Variable`
        /// in a list fromVariable` lists
        let fromOrdVar = fromOrdVar toOrdVar TotalAdjust


        /// Create a `QuantityAdjust` with name **n**
        /// and `Unit` **un** per adjust **adj**
        let create n un adj =
            match un, adj with
            | Unit.NoUnit, _
            | _, Unit.NoUnit -> Unit.NoUnit
            | _ ->
                un
                |> ValueUnit.per adj
            |> createNew (n |> Name.add name)
            |> TotalAdjust


        /// Turn a `QuantityAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Print a `QuantityAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let applyConstraints = toOrdVar >> applyConstraints >> TotalAdjust


