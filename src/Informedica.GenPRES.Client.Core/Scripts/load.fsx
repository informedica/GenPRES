// The client's pure core in FSI: the shared contract's DLL, then the core's own files in the
// order the project compiles them. Build first (dotnet run build) so that the DLL is there.
#r "../../Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"

#load "../Deferred.fs"
#load "../LanguagePolicy.fs"
#load "../PickPolicy.fs"
#load "../SeverityReason.fs"
#load "../SessionMachine.fs"
#load "../SessionGatePolicy.fs"
#load "../PlanWorkPolicy.fs"
#load "../SigningMachine.fs"
#load "../SigningPolicy.fs"
#load "../UnsignedWorkPolicy.fs"
#load "../OrderPlanMachine.fs"
#load "../OrderContextMachine.fs"
#load "../FilterSync.fs"
