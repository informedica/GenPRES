# FShharp Code Formatting Instructions

```fsharp
// indentation by 4 spaces
for i in 1..10 do
    printfn $"{i}"


// unformatted 
let myList =
    [
        "first"
        "second"
        "third"
    ]


// formatted
// cannot easily copy past additional 
// elements to the list
let myList = [ "first"; "second"; "third" ]

// unformatted 
let myList =
    [
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"
    ]


// formatted
// cannot easily use the default indentation 
// setting of the editor, als moving the first 
// or last element to reorder is awkward
let myList =
    [ "first"
      "second"
      "third"
      "first"
      "second"
      "third"
      "first"
      "second"
      "third" ]


// unformatted
type myRecord =
    {
        Name: string
        Age: float
        BirthDay: DateTime
    }


// formatted
// Same problem, just quicly moving record 
// fields arround is not very easy
type myRecord =
    { Name: string
      Age: float
      BirthDay: DateTime }


// unformatted
myList
|> List.append
    [
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"        
    ]


// formatted
myList
|> List.append
    [ "first"
      "second"
      "third"
      "first"
      "second"
      "third"
      "first"
      "second"
      "third" ]


// unformatted
vars
|> List.sortBy(fun (_, xs) ->
    // some other code
    xs |> List.iter (printfn "%A")

    xs
    |> List.tail
    |> List.sumBy Variable.count
)


// unformatted
// again it is more difficult to see what is
// the indented block container (in this case ")")
// also separation betheen different subsections
// of the function body is lost
vars
|> List.sortBy (fun (_, xs) ->
    // some other code
    xs |> List.iter (printfn "%A")
    xs |> List.tail |> List.sumBy Variable.count)


// if something simple then be done with it
if b = 0 then []
else
    // start a scope block
    [ 
        for i in 0..b do
            let result = calculate b

            if result.IsNone then None
            else
                result 
                |> Option.map (fun x -> x / 2)
    ]


// used indentation on a very simple scope block
if b = 0 then
    []
else
    // start a scope block
    [ for i in 0..b do
          let result = calculate b

          if result.IsNone then
              None
          else
              result |> Option.map (fun x -> x / 2) ]
```
