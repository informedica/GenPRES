namespace Informedica.Utils.Lib

/// Additional utitilty functions
/// for lists
[<RequireQualifiedAccess>]
module List =

    open Informedica.Utils.Lib.BCL


    /// Concatenates two lists, placing the elements of xs2 before the elements of xs1.
    /// /// Example: [1;2] |> prepend [3;4] = [1;2;3;4]
    let prepend xs1 xs2 = xs1 |> List.append xs2


    //----------------------------------------------------------------------------
    // Change lists
    //----------------------------------------------------------------------------


    /// In summary, the remove function removes the first element in the list
    /// that satisfies the given predicate, while keeping the order of the other
    /// elements intact. If no element satisfying the predicate is found,
    /// the original list is returned unchanged.
    let remove pred xs =
        match xs |> List.tryFindIndex pred with
        | Some ind ->
            xs
            |> List.mapi (fun i x ->
                if i = ind then None else Some x
            )
            |> List.choose id
        | None -> xs


    /// Replace an element **x** in a list **xs**
    /// when the **pred** function returns `true`.
    /// Note: will only replace the *first* element
    /// that satisfies the condition in **pred**
    /// Example: [1;2;3;2] |> replace (fun x -> x = 2) 4 = [1;4;3;2]
    let replace pred x xs =
        match List.tryFindIndex pred xs with
        | Some ind ->
            let before = xs |> List.take ind
            let after = xs |> List.skip (ind + 1)
            before @ [x] @ after
        | None -> xs


    /// filter a list of lists
    let listFilter p = List.filter (List.exists p)


    let collectLists p = List.collect (List.filter p)


    let pickList pl (xs: 'a List) =
        match pl with
        | [] -> xs
        | _ -> [ for i in pl -> xs[i] ]


    let removeFirst pred =
        List.fold (fun acc x ->
            let b, xs = acc
            if b then (true, x::(acc |> snd))
            else
                if x |> pred then (true, xs)
                else (false, x::(acc |> snd))
        ) (false, [])
        >> snd



    //----------------------------------------------------------------------------
    // Logic
    //----------------------------------------------------------------------------

    let hasExactlyOne pred xs =
        xs
        |> List.filter pred
        |> List.length = 1


    //----------------------------------------------------------------------------
    // Find
    //----------------------------------------------------------------------------


    let tryFindInList pred xs =
        xs
        |> List.collect id
        |> List.tryFind pred

    /// Try find the first element with **n**
    /// in a list of list **xsl**
    /// with a function **get** to
    /// get **n** from an element
    let tryFindFirst get n xs =
        let pred x = x |> get = n
        match xs |> List.filter (fun xs -> xs |> List.exists pred) with
        | [] -> None
        | xs::_ -> xs |> List.find pred |> Some


    let tryFindRest pred xs =
        let rec find x xs notx =
            match xs with
            | [] -> x, notx
            | h::tail ->
                if h |> pred then find (Some h) tail notx
                else find x tail ([h] |> List.append notx)

        find None xs []


    //----------------------------------------------------------------------------
    // Other
    //----------------------------------------------------------------------------


    let headTail xs =
        match xs with
        | [] -> (None, None)
        | h::tail ->
            match tail |> List.rev with
            | []   -> (Some h, None)
            | t::_ -> (Some h, Some t)


    let inline isConsecutive zero diff xs =
        match xs with
        | []  | [_] -> false
        | _ ->
            xs
            |> List.sort
            |> List.fold (fun acc x ->
                let isC, prev = acc

                if prev = zero then (true, x)
                else
                    (x - prev = diff && isC, x)

            ) (true, zero)
            |> fst


    let countByList xs1 xs2 =
        xs2
        |> List.append xs1
        |> List.countBy id
        |> List.map (fun (k, v) -> k, v - 1)
        |> List.sortBy (fun (k, _) ->
            try
                xs1 |> List.findIndex ((=) k)
            with
            | _ ->
                xs1 |> String.concat ", "
                |> sprintf "countByList couldn't find %s in %s" k
                |> failwith
        )

    let inline findNearestMax n ns =
        match ns with
        | [] -> n
        | _ ->
            let n =
                if n > (ns |> List.max) then
                    ns |> List.max
                else
                    n

            ns
            |> List.sort
            |> List.rev
            |> List.fold (fun x a -> if (a - x) < (n - x) then x else a) n


    let removeDuplicates xs =
        xs
        |> List.fold
            (fun xs x ->
                if xs |> List.exists ((=) x) then
                    xs
                else
                    [ x ] |> List.append xs
            )
            []


    let distinct xs = xs |> Seq.ofList |> Seq.distinct |> Seq.toList


    let replaceOrAdd pred x xs =
        if xs |> List.exists pred then
            xs |> replace pred x
        else
            x :: xs


    let someIfOne = function
        | [x] -> Some x
        | _   -> None



    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------

    let inline toString xs =
        match xs with
        | [] -> "[]"
        | _ ->
            let s =
                xs
                |> List.fold (fun s x -> s + string x + ";") "["
            (s
            |> String.subString 0 ((s |> String.length) - 1)) + "]"


    let inline toString2 xs =
        xs
        |> toString
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace ";" ","
