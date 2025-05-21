namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Array =


    open Informedica.Utils.Lib.BCL

    /// Prepend an array to another array
    /// Example: [|1;2|] |> prepend [|3;4|] -> [|1;2;3;4|]
    let prepend xs1 xs2 = xs1 |> Array.append xs2


    let indices pred xs =
        xs
        |> Array.mapi (fun i x -> if pred x then Some i else None)
        |> Array.choose id


    /// Pick elements from an array
    /// using a list of indices `pl`0
    let pickArray pl xs =
        match xs with
        | [||] -> [||]
        | _ ->
            pl
            |> Seq.toArray
            |> Array.choose (fun i ->
                if i >= 0 && i < xs.Length then Some (xs[i])
                else None
            )


    /// Remove elements from an array using a predicate function `pred`.
    let remove pred xs =
        if Array.isEmpty xs then
            xs
        else
            xs
            |> Array.choose
                (fun x ->
                    if Array.exists pred xs then
                        None
                    else
                        Some x
                )


    /// Filter an array of arrays using a predicate function `p`.
    /// The purpose of this function is to filter the elements of the
    /// input array xs based on the condition that at least one element
    /// in each subarray (inside xs) satisfies the predicate function p.
    /// Example: filterArrays (fun x -> x % 2 = 0) [|[|1;3|]; [|4;5;6|]; [|7;8;9|]|] -> [|[|4;5;6|]|]
    let arrayFilter p xs =
        xs |> Array.filter (Array.exists p)


    /// In summary, the collectArrays function filters the elements within
    /// each subarray using the predicate function p and then combines the
    /// filtered elements from all subarrays into a single array,
    /// effectively flattening the subarrays while applying the filtering criterion.
    /// Example: collectArrays (fun x -> x % 2 = 0) [|[|1;2;3|]; [|4;5;6|]; [|7;8;9|]|] -> [|2;4;6;8|]
    let collectArrays p xs =
        xs |> Array.collect (Array.filter p)


    /// Convert an array to a string using a left and right delimiter
    /// and a delimiter between elements.
    /// Example: toString_ "[|" "|]" ";" [|1;2;3|] -> "[|1;2;3|]"
    let inline toString_ left right del xs =
        match xs with
        | [||] -> $"{left}{right}"
        | _ ->
            let del = $"{del}"
            let lng = del |> String.length
            let s =
                xs
                |> Array.fold (fun s x -> s + string x + del) left
            (s |> String.subString 0 ((s |> String.length) - lng)) + right


    /// Convert an array to a string using a left and right delimiter
    /// left = [|, right = |], del = ;
    /// Example: toString [|1;2;3|] -> "[|1;2;3|]"
    let inline toString xs = xs |> toString_ "[|" "|]" ";"


    /// Convert an array to a string using a left and right delimiter
    /// Left = empty string, right = empty string, del = ;
    /// Example: toReadableString [|1;2;3|] -> "1;2;3"
    let inline toReadableString xs = xs |> toString_ "" "" ";"


    /// Determine if all elements in an array are equal.
    /// Pass the distinct element to the succ function if all elements are equal,
    /// else return fail.
    /// Example: allEqual (fun x -> Some x) (fun () -> None) [|1;1;1|] -> Some 1
    let allEqual succ fail xs =
        match xs with
        | [||] -> fail
        | [|x|] -> succ x
        | _ ->
            let first = xs[0]
            if Array.forall ((=) first) xs then succ first
            else fail


    /// Return the string version of an element if all
    /// elements in an array are equal or return an empty string
    /// Example: allEqualToString [|1;1;1|] -> "1"
    let allEqualToString xs = xs |> allEqual string ""


    /// Return an option of an element if all
    /// elements in an array are equal or return None
    let allEqualToOpt xs = xs |> allEqual Some None


    /// Return an option of an element if
    /// the array contains exactly one element or return None
    let someIfOne = function
        | [|x|] -> Some x
        | _   -> None


    /// In summary, the prune function trims the input array xs by keeping its first and last elements,
    /// along with elements that are spaced out proportionally in between, such that the final length
    /// of the array doesn't exceed the specified maxLength.
    /// The spacing between the elements is controlled by the calculated step size d.
    /// Example: prune 5 [|1;2;3;4;5;6;7;8;9|] -> [|1; 4; 7; 9|]
    let prune maxLength xs =
        let length = xs |> Array.length

        if length <= maxLength || length <= 2 then xs
        else
            let step = length / (maxLength - 2)

            xs
            |> Array.mapi (fun i x ->
                if i = 0 || i = length - 1 || i % step = 0 then Some x
                else None
            )
            |> Array.choose id

    let median xs =
        match xs |> Array.sort with
        | [||] -> invalidArg "xs" "Array cannot be empty to calculate median."
        | sorted ->
            let len = sorted.Length
            if len % 2 = 0 then
                // Average of the two middle elements for even length array
                (float sorted.[len / 2 - 1] + float sorted.[len / 2]) / 2.0
            else
                // Middle element for odd length array
                float sorted.[len / 2]

    module Tests =

        open Swensen.Unquote


        // Test prepend
        let testPrepend () =
            test <@ [|1;2|] |> prepend [|3;4|] = [|1;2;3;4|] @>


        // Test pickArray
        let testPickArray () =
            test <@ pickArray [1;2] [|1;2;3;4|] = [|2;3|] @>
            test <@ pickArray [1;2] [|1;2|] = [|2|] @>
            test <@ pickArray [1;2] [|1|] = [||] @>
            test <@ pickArray [1;2] [||] = [||] @>


        // Test arrayFilter
        let testArrayFilter () =
            test <@ arrayFilter (fun x -> x % 2 = 0) [|[|1;3|]; [|4;5;6|]; [|7;8;9|]|] = [|[|4;5;6|]; [|7;8;9|] |] @>
            test <@ arrayFilter (fun x -> x % 2 = 0) [|[|1;3|]; [|5;7|]; [|9;11|]|] = [||] @>
            test <@ arrayFilter (fun x -> x % 2 = 0) [|[|1;3|]; [|5;7|]; [|9;11|]; [|13;15|]|] = [||] @>


        // Test collectArrays
        let testCollectArrays () =
            test <@ collectArrays (fun x -> x % 2 = 0) [|[|1;2;3|]; [|4;5;6|]; [|7;8;9|]|] = [|2;4;6;8|] @>
            test <@ collectArrays (fun x -> x % 2 = 0) [|[|1;2;3|]; [|5;7|]; [|9;11|]|] = [|2|] @>


        // Test toString_
        let testToString_ () =
            test <@ toString_ "[|" "|]" ";" [|1;2;3|] = "[|1;2;3|]" @>
            test <@ toString_ "[|" "|]" ";" [||] = "[||]" @>
            test <@ toString_ "[|" "|]" ";" [|1|] = "[|1|]" @>
            test <@ toString_ "[|" "|]" ";" [|1;2|] = "[|1;2|]" @>
            test <@ toString_ "[|" "|]" ";" [|1;2;3|] = "[|1;2;3|]" @>
            test <@ toString_ "[|" "|]" ";" [|1;2;3;4|] = "[|1;2;3;4|]" @>


        // Test toString
        let testToString () =
            test <@ toString [|1;2;3|] = "[|1;2;3|]" @>
            test <@ toString [||] = "[||]" @>
            test <@ toString [|1|] = "[|1|]" @>
            test <@ toString [|1;2|] = "[|1;2|]" @>
            test <@ toString [|1;2;3|] = "[|1;2;3|]" @>
            test <@ toString [|1;2;3;4|] = "[|1;2;3;4|]" @>


        // Test toReadableString
        let testToReadableString () =
            test <@ toReadableString [|1;2;3|] = "1;2;3" @>
            test <@ toReadableString [||] = "" @>
            test <@ toReadableString [|1|] = "1" @>
            test <@ toReadableString [|1;2|] = "1;2" @>
            test <@ toReadableString [|1;2;3|] = "1;2;3" @>
            test <@ toReadableString [|1;2;3;4|] = "1;2;3;4" @>


        // Test allEqual
        let testAllEqual () =
            test <@ allEqual Some None [||] = None @>
            test <@ allEqual Some None [|1;1;1|] = Some 1 @>
            test <@ allEqual Some None [|1;1;2|] = None @>
            test <@ allEqual Some None [|1;2;1|] = None @>
            test <@ allEqual Some None [|2;1;1|] = None @>
            test <@ allEqual Some None [|1;2;3|] = None @>


        // Test allEqualToString
        let testAllEqualToString () =
            test <@ allEqualToString [||] = "" @>
            test <@ allEqualToString [|1;1;1|] = "1" @>
            test <@ allEqualToString [|1;1;2|] = "" @>
            test <@ allEqualToString [|1;2;1|] = "" @>
            test <@ allEqualToString [|2;1;1|] = "" @>
            test <@ allEqualToString [|1;2;3|] = "" @>


        // Test allEqualToOpt
        let testAllEqualToOpt () =
            test <@ allEqualToOpt [||] = None @>
            test <@ allEqualToOpt [|1;1;1|] = Some 1 @>
            test <@ allEqualToOpt [|1;1;2|] = None @>
            test <@ allEqualToOpt [|1;2;1|] = None @>
            test <@ allEqualToOpt [|2;1;1|] = None @>
            test <@ allEqualToOpt [|1;2;3|] = None @>


        // Test someIfOne
        let testSomeIfOne () =
            test <@ someIfOne [||] = None @>
            test <@ someIfOne [|1|] = Some 1 @>
            test <@ someIfOne [|1;2|] = None @>
            test <@ someIfOne [|1;2;3|] = None @>


        // Test prune
        let testPrune () =
            test <@ prune 5 [||] = [||] @>
            test <@ prune 5 [|1|] = [|1|] @>
            test <@ prune 5 [|1;2;3;4;5;6;7;8;9|] = [|1; 4; 7; 9|] @>
            test <@ prune 5 [|1;2;3;4;5;6;7;8;9;10|] = [|1; 4; 7; 10|] @>


        // Run all tests
        let testAll () =
            testPrepend ()
            testPickArray ()
            testArrayFilter ()
            testCollectArrays ()
            testToString_ ()
            testToString ()
            testToReadableString ()
            testAllEqual ()
            testAllEqualToString ()
            testAllEqualToOpt ()
            testSomeIfOne ()
            testPrune ()