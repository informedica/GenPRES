
open System

Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "v")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1yn6UC1OMJ0A2wAyX3r0AA2qlKJ7vEAB6OO0DjneiknE")

#load "load.fsx"

open Shared.Types
open Shared


let ctx =
    let pat =
        { Models.Patient.empty with
            Age =
                Models.Patient.Age.create
                    1 None None None
                |> Some
            Weight =
                {
                    Measured = Some 10_000
                    Estimated = None
                }
            Height =
                {
                    Measured = Some 72
                    Estimated = None
                }
            Department = Some "ICK"

        }

    Models.OrderContext.empty
    |> Models.OrderContext.setPatient pat

ctx
|> Api.OrderContextMsg
|> ServerApi.Message.processMsg


ctx
|> ServerApi.OrderContext.evaluate


open ServerApi
open Mappers
open OrderContext
open Informedica.GenForm.Lib

module OrderContext = Informedica.GenOrder.Lib.OrderContext

let pat =
    ctx.Patient
    |> mapFromSharedPatient
    |> Patient.calcPMAge

let ctx2 =
    ctx
    |> mapFromShared pat


OrderContext.create pat
|> OrderContext.evaluate