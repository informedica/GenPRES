#load "load.fsx"

#time

open System

let pwd = Environment.GetEnvironmentVariable("HOME")
Environment.CurrentDirectory <- 
    if pwd |> String.IsNullOrWhiteSpace then 
        __SOURCE_DIRECTORY__ + "/../../../"

    else 
        pwd + "/Development/GenForm/" //__SOURCE_DIRECTORY__ + "/../../../"


module EventStore =


    let addToStore store events = 
        events 
        |> List.map (fun e ->
            printfn "Storing event: %A" e 
            e
        )
        |> List.append store



module Program =

    let replay init apply events =
        events
        |> List.fold apply init


    let decide init apply runCmd cmd events = 
        events 
        |> replay init apply
        |> runCmd cmd
        |> EventStore.addToStore events


    let run init apply runCmd cmds =
        cmds
        |> List.fold (fun events cmd ->
            printfn "Running cmd %A" cmd
            events
            |> decide init apply runCmd cmd
        ) []


module Treatment =

    type Treatment = Treatment of string


    type Patient = Patient of string | NoPatient


    type Problem = Problem of string


    type State =
        | TreatmentPlan of Patient * ((Problem * Treatment list) list)


    type Event =
        | PatientAdmitted of PatientAdmitted
        | PatientChecked of PatientChecked
        | PatientTreated of PatientTreated
        | PatientDischarged of PatientDischarged
    and PatientAdmitted = Patient
    and PatientChecked = Problem list
    and PatientTreated = Problem * Treatment
    and PatientDischarged = unit


    type Command =
        | AdmitPatient of (Patient -> PatientAdmitted)
        | CheckPatient of (Patient -> PatientChecked)
        | TreatPatient of ((Patient * (Problem * Treatment list) list) -> PatientTreated)
        | DischargePatient of (Patient -> PatientDischarged)


    let init  = (NoPatient, []) |> TreatmentPlan


    let apply state event = 

        match event with
        | PatientAdmitted patient -> 
            (patient, [])
            |> TreatmentPlan
        | PatientChecked problems -> 
            match state with
            | TreatmentPlan (patient, _) ->
                (patient, problems |> List.map (fun p -> (p, [])))
                |> TreatmentPlan
            | _ -> state
        | PatientTreated (problem, treatment) ->
            match state with
            | TreatmentPlan(pat, problems) ->
                (pat ,
                 problems
                 |> List.map (fun (p, tl) ->
                    if p = problem then 
                        (p, treatment::tl)
                    else (p, tl)
                ))
                |> TreatmentPlan
        | PatientDischarged _ ->
            init 



    let runCmd cmd state = 
        printfn "State is %A" state
        match cmd with
        | AdmitPatient f -> 
            match state with
            | _ ->  
                [ NoPatient |> f |> PatientAdmitted ]
        | CheckPatient f -> 
            match state with
            | TreatmentPlan (patient, _) ->  [ patient |> f |> PatientChecked ]
        | TreatPatient f -> 
            match state with
            | TreatmentPlan (patient, pts) ->
                [ (patient, pts)  |> f |> PatientTreated ]
        | DischargePatient f ->
            match state with
            | TreatmentPlan _ -> []


    let run = Program.run init apply runCmd

    (*
    let runCmds () =
        [
            (fun _ -> "Test Patient" |> Patient) |> AdmitPatient
            (fun _ -> 
                ["Infection"; "Low bloodpressure"] 
                |> List.map Problem
            ) |> CheckPatient
            (fun (_, pl) ->
                pl
//                |> 

                
            )
        ]
        |> run
        *)