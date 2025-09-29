
//#I "C:\Development\Informedica\libs\GenUnits\src\Informedica.GenUnits.Lib\scripts"
//#I __SOURCE_DIRECTORY__


#load "load.fsx"

open System
open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL



type ParallelismHelpers =
    static member MaxDepth =
        int (Math.Log(float Environment.ProcessorCount, 2.0))

    static member TotalWorkers =
        int (2.0 ** float ParallelismHelpers.MaxDepth)



module ValueUnit =

    open Informedica.Utils.Lib.BCL
    open ValueUnit

    let cartesianMap op (vs1: BigRational[]) (vs2: BigRational[]) : BigRational[] =
        let n = vs1.Length
        let m = vs2.Length
        match n, m with
        | 0, _ | _, 0 -> [||]
        | 1, m ->
            let a = vs1[0]
            Array.init m (fun x -> op a vs2[x])
        | n, 1 ->
            let b0 = vs2[0]
            Array.init n (fun x -> op vs1[x] b0)
        | _ ->
            let res = Array.zeroCreate<_> (n * m)
            if n = 0 || m = 0 then res else
            for i = 0 to n - 1 do
                let v1 = vs1[i]
                let baseIdx = i * m
                for j = 0 to m - 1 do
                    res[baseIdx + j] <- op v1 vs2[j]
            res


    let calcCartesian op (vs1: BigRational[]) (vs2: BigRational[]) : BigRational[] =
        let n = vs1.Length
        let m = vs2.Length
        if n = 0 || m = 0 then
            [||]
        else
            // Fast paths for broadcasting
            match n, m with
            | 1, _ ->
                let a = vs1[0]
                match op with
                | BigRational.Add -> Array.init m (fun j -> a + vs2[j])
                | BigRational.Subtr -> Array.init m (fun j -> a - vs2[j])
                | BigRational.Mult ->
                    if a.IsZero then Array.zeroCreate m
                    else Array.init m (fun j -> a * vs2[j])
                | BigRational.Div ->
                    Array.init m (fun j ->
                        let b = vs2[j]
                        // (optional) handle division-by-zero as appropriate for your BR
                        a / b)
            | _, 1 ->
                let b0 = vs2[0]
                match op with
                | BigRational.Add -> Array.init n (fun i -> vs1[i] + b0)
                | BigRational.Subtr -> Array.init n (fun i -> vs1[i] - b0)
                | BigRational.Mult ->
                    if b0.IsZero then Array.zeroCreate n
                    else Array.init n (fun i -> vs1[i] * b0)
                | BigRational.Div ->
                    // dividing by a single scalar
                    Array.init n (fun i -> vs1[i] / b0)
            | _ ->
                // General case: preallocate and use tight loops
                let res = Array.zeroCreate<BigRational> (n * m)
                match op with
                | BigRational.Add ->
                    for i = 0 to n - 1 do
                        let v1 = vs1[i]
                        let baseIdx = i * m
                        for j = 0 to m - 1 do
                            res[baseIdx + j] <- v1 + vs2[j]
                    res
                | BigRational.Subtr ->
                    for i = 0 to n - 1 do
                        let v1 = vs1[i]
                        let baseIdx = i * m
                        for j = 0 to m - 1 do
                            res[baseIdx + j] <- v1 - vs2[j]
                    res
                | BigRational.Mult ->
                    // Tiny optimization: if either side has many zeros, this helps avoid doing full multiplies
                    // (still O(n*m), but cheaper work on zeros)
                    for i = 0 to n - 1 do
                        let v1 = vs1[i]
                        let baseIdx = i * m
                        if v1.IsZero then
                            // fill the whole row with zero
                            System.Array.Fill(res, BigRational.Zero, baseIdx, m)
                        else
                            for j = 0 to m - 1 do
                                let v2 = vs2[j]
                                res[baseIdx + j] <- if v2.IsZero then BigRational.Zero else v1 * v2
                    res
                | BigRational.Div ->
                    for i = 0 to n - 1 do
                        let v1 = vs1[i]
                        let baseIdx = i * m
                        for j = 0 to m - 1 do
                            let v2 = vs2[j]
                            // (optional) if your BR throws on div-by-zero, guard here
                            res[baseIdx + j] <- v1 / v2
                    res

    let calc_ calcF b op vu1 vu2 =

        let (ValueUnit (_, u1)) = vu1
        let (ValueUnit (_, u2)) = vu2
        // calculate value in base
        let v =
            let vs1 = vu1 |> toBaseValue
            let vs2 = vu2 |> toBaseValue

            calcF op vs1 vs2
            (*
            Array.allPairs vs1 vs2
            |> Array.map (fun (v1, v2) -> v1 |> op <| v2)
            *)

        // calculate new combi unit
        let u =
            match op with
            | BigRational.Mult -> u1 |> times u2
            | BigRational.Div -> u1 |> per u2
            | BigRational.Add
            | BigRational.Subtr ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup u2 -> u2
                // Special case when one value is a dimensionless zero
                | ZeroUnit, u
                | u, ZeroUnit -> u
                // Otherwise fail
                | _ ->
                    failwith
                    <| $"cannot add or subtract different units %A{u1} %A{u2}"
            |> fun u -> if b then simplifyUnit u else u
        // recreate valueunit with base value and combined unit
        v
        |> create u
        // calculate to the new combiunit
        |> toUnitValue
        // recreate again to final value unit
        |> create u


    let calcFChunked2 op vs1 vs2 =
        let pairs =
            Array.allPairs vs1 vs2

        let chunkSize =
            (pairs |> Array.length) / ParallelismHelpers.TotalWorkers

        pairs
        |> Array.chunkBySize chunkSize // make batches
        |> Array.map (fun batch -> async { return batch |> Array.map (fun (v1, v2 ) -> op v1 v2) })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect id

    let calc1 =
        let calcF op vs1 vs2 = calcFChunked2 op vs1 vs2
        calc_ calcF

    let calc2 =
        calc_ cartesianMap

    let calc3 =
        calc_ calcCartesian

    let calc4 b op =
        match op with
        | BigRational.Div -> calc_ calcCartesian b op
        | _ -> calc_ cartesianMap b op


let v1 =
    [|1N..1N..10_000N|]
    |> ValueUnit.withUnit Units.Count.times

let v2 =
    [| 1N..1N..10_000N |]
    |> ValueUnit.withUnit Units.Count.times



#time

printfn "start"


let run msg op =
    printfn $"{msg}"

    let run msg f =
        let stopWatch = Diagnostics.Stopwatch()

        stopWatch.Start()
        f() |> ignore
        stopWatch.Stop()
        printfn $"{msg}: {stopWatch.ElapsedMilliseconds}"

    fun () ->
        ValueUnit.calc1 true op v1 v2 |> ValueUnit.getValue |> Array.length
    |> run "calc1"

    fun () ->
        ValueUnit.calc2 true op v1 v2 |> ValueUnit.getValue |> Array.length
    |> run "calc2"

    fun () ->
        ValueUnit.calc3 true op v1 v2 |> ValueUnit.getValue |> Array.length
    |> run "calc3"

    fun () ->
        ValueUnit.calc4 true op v1 v2 |> ValueUnit.getValue |> Array.length
    |> run "calc4"


run "+" (+)
run "-" (-)
run "*" (*)
run "/" (/)
