
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#load "../RingBuffer.fs"

open Informedica.Utils.Lib
open Expecto
open FsCheck



module RingBufferTests =

    // Basic functionality tests
    let basicTests =
        testList "Basic RingBuffer Operations" [
            
            test "create with positive capacity should succeed" {
                let rb = RingBuffer.create 5
                Expect.equal rb.Capacity 5 "Capacity should be set correctly"
                Expect.equal rb.CountValue 0 "Initial count should be 0"
                Expect.isFalse rb.IsFull "Should not be full initially"
            }
            
            test "create with zero capacity should throw" {
                Expect.throws (fun () -> RingBuffer.create 0 |> ignore) "Should throw for zero capacity"
            }
            
            test "create with negative capacity should throw" {
                Expect.throws (fun () -> RingBuffer.create -1 |> ignore) "Should throw for negative capacity"
            }
            
            test "add single item should work" {
                let rb = RingBuffer.create 3
                RingBuffer.add 42 rb
                
                Expect.equal rb.CountValue 1 "Count should be 1"
                Expect.isFalse rb.IsFull "Should not be full"
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|42|] "Should contain the added item"
            }
            
            test "add items up to capacity" {
                let rb = RingBuffer.create 3
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                
                Expect.equal rb.CountValue 3 "Count should be 3"
                Expect.isTrue rb.IsFull "Should be full"
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|1; 2; 3|] "Should contain all items in order"
            }
            
            test "add beyond capacity should overwrite oldest" {
                let rb = RingBuffer.create 3
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                RingBuffer.add 4 rb  // Should overwrite 1
                
                Expect.equal rb.CountValue 3 "Count should stay at capacity"
                Expect.isTrue rb.IsFull "Should remain full"
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|2; 3; 4|] "Should contain newest items, oldest first"
            }
            
            test "clear should reset buffer" {
                let rb = RingBuffer.create 3
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.clear rb
                
                Expect.equal rb.CountValue 0 "Count should be 0 after clear"
                Expect.isFalse rb.IsFull "Should not be full after clear"
                
                let items = RingBuffer.toArray rb
                Expect.isEmpty items "Should be empty after clear"
            }
            
            test "toSeq should return items in oldest-to-newest order" {
                let rb = RingBuffer.create 3
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                RingBuffer.add 4 rb  // Overwrites 1
                
                let items = RingBuffer.toSeq rb |> List.ofSeq
                Expect.equal items [2; 3; 4] "Should return items oldest to newest"
            }
            
            test "iter should visit items in oldest-to-newest order" {
                let rb = RingBuffer.create 3
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                RingBuffer.add 4 rb  // Overwrites 1
                
                let mutable visited = []
                RingBuffer.iter (fun x -> visited <- x :: visited) rb
                
                let visitedInOrder = List.rev visited
                Expect.equal visitedInOrder [2; 3; 4] "Should visit items oldest to newest"
            }
            
            test "map should transform items in oldest-to-newest order" {
                let rb = RingBuffer.create 3
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                
                let doubled = RingBuffer.map (fun x -> x * 2) rb
                Expect.equal doubled [|2; 4; 6|] "Should map items in correct order"
            }
        ]

    // Edge case tests
    let edgeTests =
        testList "Edge Cases" [
            
            test "single capacity buffer behavior" {
                let rb = RingBuffer.create 1
                
                RingBuffer.add 1 rb
                Expect.equal (RingBuffer.toArray rb) [|1|] "Should contain first item"
                Expect.isTrue rb.IsFull "Should be full"
                
                RingBuffer.add 2 rb
                Expect.equal (RingBuffer.toArray rb) [|2|] "Should contain second item, first overwritten"
                
                RingBuffer.add 3 rb
                Expect.equal (RingBuffer.toArray rb) [|3|] "Should contain third item"
            }
            
            test "empty buffer operations" {
                let rb = RingBuffer.create 5
                
                let items = RingBuffer.toArray rb
                Expect.isEmpty items "Empty buffer should return empty array"
                
                let seqItems = RingBuffer.toSeq rb |> List.ofSeq
                Expect.isEmpty seqItems "Empty buffer should return empty sequence"
                
                let mutable iterCount = 0
                RingBuffer.iter (fun _ -> iterCount <- iterCount + 1) rb
                Expect.equal iterCount 0 "Iter should not execute on empty buffer"
                
                let mapped = RingBuffer.map (fun x -> x * 2) rb
                Expect.isEmpty mapped "Map should return empty array for empty buffer"
            }
            
            test "clear and reuse buffer" {
                let rb = RingBuffer.create 3
                
                // Fill buffer
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                
                // Clear and reuse
                RingBuffer.clear rb
                RingBuffer.add 10 rb
                RingBuffer.add 20 rb
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|10; 20|] "Should work correctly after clear"
            }
            
            test "multiple wrap-arounds" {
                let rb = RingBuffer.create 3
                
                // Add 10 items (multiple wrap-arounds)
                for i in 1..10 do
                    RingBuffer.add i rb
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|8; 9; 10|] "Should contain last 3 items"
            }
        ]

    // Property-based tests using FsCheck
    let propertyTests =
        testList "Property-based Tests" [
            
            testProperty
                "capacity is always preserved" <| fun (PositiveInt capacity) ->
                (capacity > 0 && capacity < 1000) ==> lazy (
                    let rb = RingBuffer.create capacity
                    rb.Capacity = capacity
                )
            
            testProperty
                "count never exceeds capacity" <| fun (PositiveInt capacity) (items: int list) ->
                (capacity > 0 && capacity < 100) ==> lazy (
                    let rb = RingBuffer.create capacity
                    items |> List.iter (fun item -> RingBuffer.add item rb)
                    rb.CountValue <= rb.Capacity
                )
            
            testProperty
                "toArray length equals count" <| fun (PositiveInt capacity) (items: int list) ->
                (capacity > 0 && capacity < 100) ==> lazy (
                    let rb = RingBuffer.create capacity
                    items |> List.iter (fun item -> RingBuffer.add item rb)
                    let arr = RingBuffer.toArray rb
                    arr.Length = rb.CountValue
                )
            

            testProperty
                "map preserves count and order" <| fun (PositiveInt capacity) (items: int list) ->
                (capacity > 0 && capacity < 100) ==> lazy (
                    let rb = RingBuffer.create capacity
                    items |> List.iter (fun item -> RingBuffer.add item rb)
                    
                    let original = RingBuffer.toArray rb
                    let mapped = RingBuffer.map (fun x -> x * 2) rb
                    
                    mapped.Length = original.Length &&
                    Array.zip original mapped |> Array.forall (fun (orig, map) -> map = orig * 2)
                )


            testProperty 
                "toSeq and toArray are equivalent" <| fun (PositiveInt capacity) (items: int list) ->
                (capacity > 0 && capacity < 100) ==> lazy (
                    let rb = RingBuffer.create capacity
                    items |> List.iter (fun item -> RingBuffer.add item rb)
                    
                    let fromSeq = RingBuffer.toSeq rb |> Array.ofSeq
                    let fromArray = RingBuffer.toArray rb
                    
                    fromSeq = fromArray
                )


            testProperty
                "clear always resets to empty state" <| fun (PositiveInt capacity) (items: int list) ->
                (capacity > 0 && capacity < 100 && items |> List.length < 200) ==> lazy (
                    let rb = RingBuffer.create capacity
                    items |> List.iter (fun item -> RingBuffer.add item rb)
                    
                    RingBuffer.clear rb
                    
                    rb.CountValue = 0 && 
                    not rb.IsFull && 
                    (RingBuffer.toArray rb).Length = 0
                )


            testProperty
                "adding items maintains newest-first property when full" <| fun (capacity: int) ->
                (capacity > 0 && capacity <= 100) ==> lazy (
                    let rb = RingBuffer.create capacity
                    
                    // Fill beyond capacity
                    let totalItems = capacity + 5
                    for i in 1..totalItems do
                        RingBuffer.add i rb
                    
                    let items = RingBuffer.toArray rb 
                    let expectedStart = totalItems - capacity + 1
                    let expected = [|expectedStart..totalItems|]
                    
                    if not (items = expected) then printfn $"{capacity} -> items: {items}, expected: {expected}"
                    items = expected
                )

        ]

    // Performance and stress tests
    let performanceTests =
        testList "Performance Tests" [
            
            test "large buffer operations should be fast" {
                let capacity = 10000
                let rb = RingBuffer.create capacity
                
                let sw = System.Diagnostics.Stopwatch.StartNew()
                
                // Add many items
                for i in 1..50000 do
                    RingBuffer.add i rb
                
                sw.Stop()
                
                Expect.isLessThan sw.ElapsedMilliseconds 1000L "Should add 50k items quickly"
                Expect.equal rb.CountValue capacity "Should maintain capacity"
                
                // Verify the buffer contains the last 'capacity' items
                let items = RingBuffer.toArray rb
                let expectedStart = 50000 - capacity + 1
                Expect.equal items.[0] expectedStart "Should start with correct item"
                Expect.equal items.[capacity - 1] 50000 "Should end with correct item"
            }
            
            test "frequent clear and refill should be efficient" {
                let rb = RingBuffer.create 1000
                let sw = System.Diagnostics.Stopwatch.StartNew()
                
                for cycle in 1..100 do
                    RingBuffer.clear rb
                    for i in 1..1000 do
                        RingBuffer.add (cycle * 1000 + i) rb
                
                sw.Stop()
                
                Expect.isLessThan sw.ElapsedMilliseconds 1000L "Should handle frequent clear/refill efficiently"
            }
            
            test "toSeq should handle large buffers efficiently" {
                let capacity = 10000
                let rb = RingBuffer.create capacity
                
                for i in 1..capacity do
                    RingBuffer.add i rb
                
                let sw = System.Diagnostics.Stopwatch.StartNew()
                let items = RingBuffer.toSeq rb |> Array.ofSeq
                sw.Stop()
                
                Expect.isLessThan sw.ElapsedMilliseconds 100L "Should convert to sequence quickly"
                Expect.equal items.Length capacity "Should have correct length"
            }
        ]

    // Sequence and ordering tests
    let orderingTests =
        testList "Ordering and Sequence Tests" [
            
            test "partial fill maintains insertion order" {
                let rb = RingBuffer.create 5
                
                RingBuffer.add 10 rb
                RingBuffer.add 20 rb
                RingBuffer.add 30 rb
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|10; 20; 30|] "Should maintain insertion order when not full"
            }
            
            test "wrap-around maintains oldest-to-newest order" {
                let rb = RingBuffer.create 4
                
                // Fill completely
                for i in 1..4 do
                    RingBuffer.add i rb
                
                // Add more to cause wrap-around
                RingBuffer.add 5 rb
                RingBuffer.add 6 rb
                
                let items = RingBuffer.toArray rb
                Expect.equal items [|3; 4; 5; 6|] "Should maintain oldest-to-newest after wrap-around"
            }
            
            test "iter visits all elements exactly once" {
                let rb = RingBuffer.create 3
                
                RingBuffer.add 1 rb
                RingBuffer.add 2 rb
                RingBuffer.add 3 rb
                RingBuffer.add 4 rb  // Overwrites 1
                
                let mutable sum = 0
                let mutable count = 0
                
                RingBuffer.iter (fun x -> 
                    sum <- sum + x
                    count <- count + 1) rb
                
                Expect.equal count 3 "Should visit exactly 3 elements"
                Expect.equal sum 9 "Should visit 2+3+4 = 9"
            }
        ]

    // Main test suite
    let allTests =
        testList "Informedica.Utils.Lib RingBuffer Tests" [
            basicTests
            edgeTests
            propertyTests
            performanceTests
            orderingTests
        ]


// Run tests
runTestsWithCLIArgs [] [|"--debug"|] RingBufferTests.allTests
