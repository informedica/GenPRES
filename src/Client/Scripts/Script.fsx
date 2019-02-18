#r "netstandard"
#load "./../../../.paket/load/net472/Client/client.group.fsx"
#load "./../../Shared/SharedTypes.fs"

module Request =
    open Fable.PowerPack.Fetch
    open Fable.PowerPack.PromiseImpl
    open GenPres
    open Thoth.Json

    type Response = Shared.Types.Response.Response

    let post req =
        let decode = Decode.Auto.unsafeFromString<Response Option>
        promise { let! res = postRecord "http://localhost:8085/api/request" req
                                 []
                  let! pat = res.text()
                  return pat |> decode }

module Patient =
    open Elmish
    open Fable.PowerPack.Fetch
    open Fable.PowerPack.PromiseImpl
    open GenPres
    open Thoth.Json

    type Model = Shared.Types.Patient.Patient Option

    type Msg = PatientLoaded of Result<Shared.Types.Response.Response Option, exn>

    let getInitPatient() =
        Shared.Types.Request.Patient.Init
        |> Shared.Types.Request.PatientMsg
        |> Request.post

    let init() =
        None,
        Cmd.ofPromise getInitPatient () (Ok >> PatientLoaded)
            (Error >> PatientLoaded)
