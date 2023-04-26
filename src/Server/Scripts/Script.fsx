
#load "load.fsx"


Shared.ScenarioResult.empty
|> serverApi.getScenarioResult
|> Async.RunSynchronously
|> ignore