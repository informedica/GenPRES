// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

#time

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib
open Informedica.GenSolver.Lib.Variable.Operators


let scenarios (pr: PrescriptionResult) =
    let getRules filter =
        { pr with Filter = filter } |> PrescriptionResult.getRules

    [ for g in pr.Filter.Generics do
          let pr, rules = { pr.Filter with Generic = Some g } |> getRules

          if rules |> Array.isEmpty |> not then
              printfn $"evaluting {g}..."
              pr |> Api.evaluate
          else
              for i in pr.Filter.Indications do
                  let pr, rules = { pr.Filter with Indication = Some i } |> getRules

                  if rules |> Array.isEmpty |> not then
                      printfn $"evaluting {g} {i}..."
                      pr |> Api.evaluate
                  else
                      for r in pr.Filter.Routes do
                          let pr, rules = { pr.Filter with Route = Some r } |> getRules

                          if rules |> Array.isEmpty |> not then
                              printfn $"evaluting {g} {i} {r}..."
                              pr |> Api.evaluate

                          else
                              for d in pr.Filter.DoseTypes do
                                  let pr, rules = { pr.Filter with DoseType = Some d } |> getRules

                                  if rules |> Array.isEmpty |> not then
                                      printfn $"evaluting {g} {i} {r} {d}..."
                                      pr |> Api.evaluate ]


let pr = Patient.infant |> PrescriptionResult.create


pr |> scenarios


{ pr with
    Filter =
        { pr.Filter with
            Generic = Some "natriumfosfaat" } }
|> PrescriptionResult.getRules
|> snd
|> Array.head
|> DrugOrder.fromRule

{ pr with
    Filter =
        { pr.Filter with
            Generic = Some "natriumfosfaat" } }
|> Api.evaluate