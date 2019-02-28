namespace Utils

module Request =
    open Elmish
    open Fable.PowerPack.Fetch
    open Fable.PowerPack.PromiseImpl
    open GenPres
    open Thoth.Json

    type Response = Shared.Types.Response.Response

    let post req =
        let decode = Decode.Auto.unsafeFromString<Response Option>
        promise { let! res = postRecord "/api/request" req []
                  let! pat = res.text()
                  return pat |> decode }

    let requestToResponseCommand respMsg reqMsg =
        reqMsg
        |> post
        |> (fun task ->
        Cmd.ofPromise (fun () -> task) () (Ok >> respMsg)
            (Result.Error >> respMsg))
