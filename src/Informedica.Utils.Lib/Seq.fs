namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Seq =

    open Informedica.Utils.Lib.BCL


    /// Prepend a sequence to another sequence
    /// Example: seq {1;2} |> prepend {3;4} -> seq {1;2;3;4}
    let prepend xs1 xs2 = seq { yield! xs2; yield! xs1 }


    /// Pick elements from a sequence
    /// using a list of indices `pl`
    let pickSeq pl xs =
        pl
        |> List.choose (fun i ->
            if i >= 0 && i < Seq.length xs then Some (Seq.item i xs)
            else None
        )
        |> Seq.ofList


    /// Filter a sequence of sequences using a predicate function `p`.
    /// The purpose of this function is to filter the elements of the
    /// input sequence xs based on the condition that at least one element
    /// in each subsequence (inside xs) satisfies the predicate function p.
    /// Example: filterSeqs (fun x -> x % 2 = 0) seq {seq {1;3}; seq {4;5;6}; seq {7;8;9}} -> seq {seq {4;5;6}}
    let seqFilter p xs =
        xs |> Seq.filter (Seq.exists p)

    /// In summary, the collectSeqs function filters the elements within
    /// each subsequence using the predicate function p and then combines the
    /// filtered elements from all subsequences into a single sequence,
    /// effectively flattening the subsequences while applying the filtering criterion.
    /// Example: collectSeqs (fun x -> x % 2 = 0) seq {seq {1;2;3}; seq {4;5;6}; seq {7;8;9}} -> seq {2;4;6;8}
    let collectSeqs p xs =
        xs |> Seq.collect (Seq.filter p)


    /// Convert a sequence to a string using a left and right delimiter
    /// and a delimiter between elements.
    /// Example: toStringSeq "[|" "|]" ";" seq {1;2;3} -> "[|1;2;3|]"
    let inline toString_ left right del xs =
        match xs |> Seq.toArray with
        | [||] -> $"{left}{right}"
        | _ ->
            let del = $"{del}"
            let lng = del |> String.length
            let s =
                xs
                |> Seq.fold (fun s x -> s + string x + del) left
            (s |> String.subString 0 ((s |> String.length) - lng)) + right

    /// Convert a sequence to a string using a left and right delimiter
    /// left = [|, right = |], del = ;
    /// Example: toString [|1;2;3|] -> "[|1;2;3|]"
    let inline toString xs = xs |> toString_"[|" "|]" ";"

    /// Convert a sequence to a string using a left and right delimiter
    /// Left = empty string, right = empty string, del = ;
    /// Example: toReadableString [|1;2;3|] -> "1;2;3"
    let inline toReadableString xs = xs |> toString_ "" "" ";"

    /// Determine if all elements in a sequence are equal.
    /// Pass the distinct element to the succ function if all elements are equal,
    /// else return fail.
    /// Example: allEqualSeq (fun x -> Some x) (fun () -> None) seq {1;1;1} -> Some 1
    let allEqualSeq succ fail xs =
        match xs |> Seq.toArray with
        | [||] -> fail
        | [|x|] -> succ x
        | _ ->
            let first = xs |> Seq.head
            if Seq.forall ((=) first) xs then succ first
            else fail

    /// Return the string version of an element if all
    /// elements in a sequence are equal or return an empty string
    /// Example: allEqualToStringSeq seq {1;1;1} -> "1"
    let allEqualToStringSeq xs = xs |> allEqualSeq string ""

    /// Return an option of an element if all
    /// elements in a sequence are equal or return None
    let allEqualToOptSeq xs = xs |> allEqualSeq Some None

    /// Return an option of an element if
    /// the sequence contains exactly one element or return None
    let someIfOneSeq xs =
        match xs with
        | _  when xs |> Seq.length = 1 -> xs |> Seq.item 0 |> Some
        | _       -> None

    /// In summary, the pruneSeq function trims the input sequence xs by keeping its first and last elements,
    /// along with elements that are spaced out proportionally in between, such that the final length
    /// of the sequence doesn't exceed the specified maxLength.
    /// The spacing between the elements is controlled by the calculated step size d.
    /// Example: pruneSeq 5 seq {1;2;3;4;5;6;7;8;9} -> seq {1;4;7;9}
    let pruneSeq maxLength xs =
        let length = xs |> Seq.length

        if length <= maxLength || length <= 2 then xs
        else
            let step = length / (maxLength - 2)

            xs
            |> Seq.mapi (fun i x ->
                if i = 0 || i = length - 1 || i % step = 0 then Some x
                else None
            )
            |> Seq.choose id


    /// Check if all elements in a sequence are unique
    /// Example: allUnique seq {1;2;3} -> true

    let allUnique xs =
        (xs
         |> Set.ofSeq
         |> Set.count) = (xs |> Seq.length)


    module Tests =

        open Swensen.Unquote

        // Test prependSeq
        let testPrependSeq () =
            let exp = seq {1;2;3;4} |> Seq.toList
            let act = seq {1;2} |> prepend (seq {3;4}) |> Seq.toList
            test <@ act = exp @>

        // Test pickSeq
        let testPickSeq () =
            let exp = seq {2;3} |> Seq.toList
            let act = seq {1;2;3;4} |> pickSeq [1;2] |> Seq.toList
            test <@ act = exp @>

            let exp = [2; 3; 4]
            let act = seq {1;2;3;4} |> pickSeq [1;2;3] |> Seq.toList
            test <@ act = exp @>

            let exp : List<int> = [2]
            let act = pickSeq [1;2] (seq {1;2}) |> Seq.toList
            test <@ act = exp @>

            let exp : List<int> = []
            let act = pickSeq [1;2] Seq.empty |> Seq.toList
            test <@ act = exp @>

        // Test seqFilter
        let testSeqFilter () =
            let exp = seq {seq {4;5;6}; seq {7;8;9}} |> Seq.toList |> List.map Seq.toList
            let act =
                seq {seq {1;3}; seq {4;5;6}; seq {7;8;9}} |> seqFilter (fun x -> x % 2 = 0)
                |> Seq.toList
                |> List.map Seq.toList
            test <@ exp = act @>

            let exp : List<int seq> = List.empty
            let act = seq {seq {1;3}; seq {5;7}; seq {9;11}} |> seqFilter (fun x -> x % 2 = 0) |> Seq.toList
            test <@ act = exp @>

            let exp : List<int seq> = List.empty
            let act = seq {seq {1;3}; seq {5;7}; seq {9;11}; seq {13;15}} |> seqFilter (fun x -> x % 2 = 0) |> Seq.toList
            test <@ act = exp @>

        // Test collectSeqs
        let testCollectSeqs () =
            let exp = seq {2;4;6;8} |> Seq.toList
            let act = seq {seq {1;2;3}; seq {4;5;6}; seq {7;8;9}} |> collectSeqs (fun x -> x % 2 = 0) |> Seq.toList
            test <@ act = exp @>

            let exp = seq {2} |> Seq.toList
            let act = seq {seq {1;2;3}; seq {5;7}; seq {9;11}} |> collectSeqs (fun x -> x % 2 = 0) |> Seq.toList
            test <@ act = exp @>

        // Test toString_
        let testToStringSeq_ () =
            test <@ toString_ "[|" "|]" ";" (seq {1;2;3}) = "[|1;2;3|]" @>
            test <@ toString_ "[|" "|]" ";" Seq.empty = "[||]" @>
            test <@ toString_ "[|" "|]" ";" (seq {1}) = "[|1|]" @>
            test <@ toString_ "[|" "|]" ";" (seq {1;2}) = "[|1;2|]" @>

        // Test toString
        let testToStringSeq () =
            test <@ toString (seq {1;2;3}) = "[|1;2;3|]" @>
            test <@ toString Seq.empty = "[||]" @>
            test <@ toString (seq {1}) = "[|1|]" @>
            test <@ toString (seq {1;2}) = "[|1;2|]" @>

        // Test toReadableStringSeq
        let testToReadableStringSeq () =
            test <@ toReadableString (seq {1;2;3}) = "1;2;3" @>
            test <@ toReadableString Seq.empty = "" @>
            test <@ toReadableString (seq {1}) = "1" @>
            test <@ toReadableString (seq {1;2}) = "1;2" @>

        // Test allEqualSeq
        let testAllEqualSeq () =
            test <@ allEqualSeq Some None Seq.empty = None @>
            test <@ allEqualSeq Some None (seq {1;1;1}) = Some 1 @>
            test <@ allEqualSeq Some None (seq {1;1;2}) = None @>
            test <@ allEqualSeq Some None (seq {1;2;1}) = None @>
            test <@ allEqualSeq Some None (seq {2;1;1}) = None @>
            test <@ allEqualSeq Some None (seq {1;2;3}) = None @>

        // Test allEqualToStringSeq
        let testAllEqualToStringSeq () =
            test <@ allEqualToStringSeq Seq.empty = "" @>
            test <@ allEqualToStringSeq (seq {1;1;1}) = "1" @>
            test <@ allEqualToStringSeq (seq {1;1;2}) = "" @>
            test <@ allEqualToStringSeq (seq {1;2;1}) = "" @>
            test <@ allEqualToStringSeq (seq {2;1;1}) = "" @>
            test <@ allEqualToStringSeq (seq {1;2;3}) = "" @>

        // Test allEqualToOptSeq
        let testAllEqualToOptSeq () =
            test <@ allEqualToOptSeq Seq.empty = None @>
            test <@ allEqualToOptSeq (seq {1;1;1}) = Some 1 @>
            test <@ allEqualToOptSeq (seq {1;1;2}) = None @>
            test <@ allEqualToOptSeq (seq {1;2;1}) = None @>
            test <@ allEqualToOptSeq (seq {2;1;1}) = None @>
            test <@ allEqualToOptSeq (seq {1;2;3}) = None @>

        // Test someIfOneSeq
        let testSomeIfOneSeq () =
            test <@ someIfOneSeq Seq.empty = None @>
            test <@ someIfOneSeq (seq {1}) = Some 1 @>
            test <@ someIfOneSeq (seq {1;2}) = None @>
            test <@ someIfOneSeq (seq {1;2;3}) = None @>

        // Test pruneSeq
        let testPruneSeq () =
            let exp : List<int> = []
            let act = pruneSeq 5 Seq.empty |> Seq.toList
            test <@ act = exp @>

            let exp = seq {1} |> Seq.toList
            let act = pruneSeq 5 (seq {1}) |> Seq.toList
            test <@ act = exp @>

            let exp = seq {1; 4; 7; 9} |> Seq.toList
            let act = pruneSeq 5 (seq {1;2;3;4;5;6;7;8;9}) |> Seq.toList
            test <@ act = exp @>

            let exp = seq {1; 4; 7; 10} |> Seq.toList
            let act = pruneSeq 5 (seq {1;2;3;4;5;6;7;8;9;10}) |> Seq.toList
            test <@ act = exp @>

        // Run all tests
        let testAll () =
            testPrependSeq ()
            testPickSeq ()
            testSeqFilter ()
            testCollectSeqs ()
            testToStringSeq_ ()
            testToStringSeq ()
            testToReadableStringSeq ()
            testAllEqualSeq ()
            testAllEqualToStringSeq ()
            testAllEqualToOptSeq ()
            testSomeIfOneSeq ()
            testPruneSeq ()