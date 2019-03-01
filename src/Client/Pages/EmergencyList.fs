namespace Pages

module EmergencyList =
    open Elmish
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.Import
    open Components
    open GenPres
    open Fable.Import

    module TG = Utils.Typography

    type Msg =
        | SortMsg of SortableTable.Msg
        | TreatmentLoaded of Shared.Types.Response.Result
        | CalculateTreatment of float * float

    type Model =
        { SortableTableModel : SortableTable.Model Option
          MedicationDefs : Shared.Types.Treatment.MedicationDefs }

    let joules = [ 1; 2; 3; 5; 7; 10; 20; 30; 50; 70; 100; 150 ]

    let treatment age wght medDefs =
        Domain.EmergencyTreatment.getTableData age wght joules medDefs
        |> List.map (fun row ->
               match row with
               | ind :: interv :: calc :: prep :: adv :: [] ->
                   [ (ind, TG.caption ind)
                     (interv, TG.subtitle2 interv)
                     (calc, TG.body2 calc)
                     (prep, TG.body2 prep)
                     (adv, str adv) ]
               | _ -> [])

    let createTableModel age wght medDefs =
        let head =
            [ ("Indicatie", true)
              ("Interventie", true)
              ("Berekend", false)
              ("Bereiding", false)
              ("Advies", false) ]
            |> List.map (fun (lbl, sort) -> SortableTable.createHeader lbl sort)

        let data = treatment age wght medDefs
        { SortableTable.HeaderRow = head
          SortableTable.Rows = data
          SortableTable.Dialog = [] }

    let processResponse model resp =
        fun model resp ->
            match resp with
            | Shared.Types.Response.MedicationDefs defs ->
                { model with MedicationDefs = defs }, Cmd.none
            | _ -> model, Cmd.none
        |> Utils.Response.processResponse model resp

    let init() =
        { SortableTableModel = None
          MedicationDefs = [] },
        Shared.Types.Request.AcuteList.Get
        |> Shared.Types.Request.AcuteListMsg
        |> Utils.Request.requestToResponseCommand TreatmentLoaded

    let update model msg =
        match msg with
        | SortMsg msg ->
            match model.SortableTableModel with
            | Some tabMod ->
                { model with SortableTableModel =
                                 SortableTable.update tabMod msg |> Some },
                Cmd.none
            | None -> model, Cmd.none
        | TreatmentLoaded resp -> resp |> processResponse model
        | CalculateTreatment(age, wght) ->
            { model with SortableTableModel =
                             createTableModel age wght model.MedicationDefs
                             |> Some }, Cmd.none

    let view model dispatch =
        match model.SortableTableModel with
        | Some model -> SortableTable.view model (SortMsg >> dispatch)
        | None -> div [] []
