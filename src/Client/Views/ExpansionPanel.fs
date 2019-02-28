namespace Views

module ExpansionPanel =
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.Import
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes
    open Fable.PowerPack.Keyboard

    let node x = ReactNode.Case1(ReactChild.Case1 x)

    let panel (classes : IClasses) sum det =
        expansionPanel []
            [ expansionPanelSummary
                  [ ExpansionPanelSummaryProp.ExpandIcon
                        (icon [] [ str "expand_more" ] |> node) ] [ sum ]

              expansionPanelDetails [ Class !!classes?patientpaneldetails ]
                  [ det ] ]
