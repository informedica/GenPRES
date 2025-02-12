namespace Views

module Intake =

    open System
    open Fable.Core
    open Fable.React
    open Feliz
    open Browser.Types
    open Shared
    open Types
    open Elmish
    open Fable.Core.JsInterop

    let private rows1 = [|
        [| "volume"; ""; "ml/kg/dag" |]
        [| "energy"; ""; "kCal/kg/dag" |]
        [| "koolhydraat"; ""; "mg/kg/min" |]
        [| "ewit"; ""; "g/kg/dag" |]
        [| "vet"; ""; "g/kg/dag" |]

    |]

    let private rows2 = [|
        [| "natrium"; ""; "mmol/kg/dag" |]
        [| "kalium"; ""; "mmol/kg/dag" |]
        [| "chloride"; ""; "mmol/kg/dag" |]
        [| "caldium"; ""; "mmol/kg/dag" |]
        [| "magnesium"; ""; "mmol/kg/dag" |]
    |]

    let private rows3 = [|
        [| "fosfaat"; ""; "mmol/kg/dag" |]
        [| "ijzer"; ""; "mmol/kg/dag" |]
        [| "vit D"; ""; "mmol/kg/dag" |]
    |]

    [<JSX.Component>]
    let View(res: Deferred<Intake>) =
        let mapRow (intake: Intake) row =
            row
            |> Array.map (fun cells ->
                match cells |> Array.head with
                | "volume" ->
                    let vol = intake.Volume
                    [| cells[0]; vol |> Option.map string |> Option.defaultValue ""; cells[2] |]
                (*
                | "energy" -> [| cells[0]; intake.Energy |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "koolhydraat" -> [| cells[0]; intake.Carbohydrate |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "ewit" -> [| cells[0]; intake.Protein |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "vet" -> [| cells[0]; intake.Fat |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "natrium" -> [| cells[0]; intake.Sodium |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "kalium" -> [| cells[0]; intake.Potassium |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "chloride" -> [| cells[0]; intake.Chloride |> olOption.map string |> Option.defaultValue ""; cells[2] |]
                | "caldium" -> [| cells[0]; intake.Calcium |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "magnesium" -> [| cells[0]; intake.Magnesium |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "fosfaat" -> [| cells[0]; intake.Phosphate |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "ijzer" -> [| cells[0]; intake.Iron |> Option.map string |> Option.defaultValue ""; cells[2] |]
                | "vit D" -> [| cells[0]; intake.VitaminD |> Option.map string |> Option.defaultValue ""; cells[2] |]
                *)
                | _ -> cells
            )

        let rows1, rows2, rows3 =
            match res with
            | Resolved intake ->
                let map = mapRow intake
                map rows1
                ,
                map rows2
                ,
                map rows3
            | _ ->
                rows1, rows2, rows3

        let content1 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows1
            |})
            |> toReact

        let content2 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows2
            |})
            |> toReact

        let content3 =
            Components.BasicTable.View({|
                header = [||]
                rows =rows3
            |})
            |> toReact

        Components.BottomDrawer.View {|
            isOpen = true;
            content = [| content1; content2; content3 |]
            |}
