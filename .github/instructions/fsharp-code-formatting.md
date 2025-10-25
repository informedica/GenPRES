# FSharp Code Formatting Instructions

Block indentation should follow a general standard of 4 spaces per indentation level. Also, whatever is delimiting the scope (like `[` or `{` or `(`) should be aligned with the indentation level of the block when it is closed.
`if then else` blocks, `match with` blocks, `let` bindings that span multiple lines, and function bodies should all follow this convention.

```fsharp
// Good: indentation by 4 spaces
for i in 1..10 do
    printfn $"{i}"


// unformatted 
let myList =
    [
        "first"
        "second"
        "third"
    ]


// Fantomas formatted
// cannot easily copy past additional 
// elements to the list
let myList = [ "first"; "second"; "third" ]

// Good: indentation by 4 spaces
// and aligned delimiters
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


// Fantomas formatted
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


// Good: indentation by 4 spaces
// and aligned delimiters
type myRecord =
    {
        Name: string
        Age: float
        BirthDay: DateTime
    }


// Fantomas formatted
// Same problem, just quicly moving record 
// fields arround is not very easy
type myRecord =
    { Name: string
      Age: float
      BirthDay: DateTime }


// Good : indentation by 4 spaces
// and aligned delimiters
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


// Fantomas formatted
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


// Good : indentation by 4 spaces
// and aligned delimiters
vars
|> List.sortBy(fun (_, xs) ->
    // some other code
    xs |> List.iter (printfn "%A")

    xs
    |> List.tail
    |> List.sumBy Variable.count
)


// Fantomas formatted
// again it is more difficult to see what is
// the indented block container (in this case ")")
// also separation betheen different subsections
// of the function body is lost
vars
|> List.sortBy (fun (_, xs) ->
    // some other code
    xs |> List.iter (printfn "%A")
    xs |> List.tail |> List.sumBy Variable.count)
```

Also, try to put the simple cases on the `then` case on the same line, and use indentation for more complex cases in the `else` case.

```fsharp

// Good: simple then case on same line
// more complex else case indented
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


// Fantomas formatted
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

Try to put pipeline operators at the beginning of the line when breaking long pipelines.

```fsharp
// Good: pipeline indentation
result
|> Result.map (fun x -> x + 1)
|> Result.toOption

// Fantomas formatted
result |> Result.map (fun x -> x + 1) |> Result.toOption
````
