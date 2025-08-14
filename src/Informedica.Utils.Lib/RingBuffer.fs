namespace Informedica.Utils.Lib


/// Fixed-size, overwriting ring buffer (oldest -> newest order on iteration)
type RingBuffer<'T> =
    private {
        mutable Head  : int           // next write index
        mutable Count : int           // current number of elements (<= Capacity)
        Data          : 'T array      // storage
    }
    member x.Capacity = x.Data.Length
    member x.CountValue = x.Count
    member x.IsFull   = x.Count = x.Capacity


[<RequireQualifiedAccess>]
module RingBuffer =

    /// Create a ring buffer with given positive capacity
    let create (capacity: int) : RingBuffer<'T> =
        if capacity <= 0 then invalidArg (nameof capacity) "capacity must be > 0"
        { Head = 0; Count = 0; Data = Array.zeroCreate capacity }

    /// Clear without reallocating
    let clear (rb: RingBuffer<'T>) =
        rb.Head <- 0
        rb.Count <- 0

    /// Add (overwrites oldest when full)
    let add (item: 'T) (rb: RingBuffer<'T>) =
        rb.Data[rb.Head] <- item
        rb.Head <- (rb.Head + 1) % rb.Capacity
        if rb.Count < rb.Capacity then rb.Count <- rb.Count + 1

    /// Sequence view (oldest -> newest)
    let toSeq (rb: RingBuffer<'T>) : seq<'T> =
        seq {
            let start = if rb.Count = rb.Capacity then rb.Head else 0
            let len = rb.Count
            for i = 0 to len - 1 do
                let idx = (start + i) % rb.Capacity
                yield rb.Data[idx]
        }

    /// Snapshot as array (oldest -> newest)
    let toArray (rb: RingBuffer<'T>) : 'T[] = toSeq rb |> Array.ofSeq

    /// Apply f for each element, oldest -> newest
    let iter (f: 'T -> unit) (rb: RingBuffer<'T>) =
        let start = if rb.Count = rb.Capacity then rb.Head else 0
        let len = rb.Count
        for i = 0 to len - 1 do
            let idx = (start + i) % rb.Capacity
            f rb.Data[idx]

    /// Map into a new array (oldest -> newest)
    let map (f: 'T -> 'U) (rb: RingBuffer<'T>) : 'U[] =
        let res = Array.zeroCreate<'U> rb.Count
        let start = if rb.Count = rb.Capacity then rb.Head else 0
        let len = rb.Count
        for i = 0 to len - 1 do
            let idx = (start + i) % rb.Capacity
            res[i] <- f rb.Data[idx]
        res