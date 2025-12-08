
#r "nuget: Newtonsoft.Json"
#r "nuget: NJsonSchema"


open NJsonSchema


type Person () =
    member val Name = "" with get, set
    member val Age = Age () with get, set
and Age () =
    member val Age = 0 with get, set
    member val Unit = "" with get, set

JsonSchema.FromType<Person>().ToJson()