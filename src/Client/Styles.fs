module Styles

open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Import.React
open Fable.MaterialUI.Core
open Fable.MaterialUI.Props
open Fable.MaterialUI.Themes

let theme =
    createMuiTheme
        [ ThemeProp.Palette
              [ PaletteProp.Primary
                    [ PaletteIntentionProp.Main
                          Fable.MaterialUI.Colors.blue.``500`` ] ]
          ThemeProp.Typography [ ThemeTypographyProp.UseNextVariants true ] ]

let styles (theme : ITheme) : IStyles list =
    [ Styles.Custom("container",
                    [ CSSProp.Height "100vh"
                      CSSProp.BoxSizing "border-box" ])
      Styles.Custom("mainview",
                    [ CSSProp.Height "100vh"
                      CSSProp.Display "flex"
                      CSSProp.Overflow "hidden"
                      CSSProp.FlexDirection "column"
                      CSSProp.MarginLeft "20px"
                      CSSProp.MarginRight "20px" ])
      Styles.Custom("patientpaneldiv",
                    [ CSSProp.MarginTop "70px"
                      CSSProp.Top "0" ])
      Styles.Custom("patientpaneldetails",
                    [ CSSProp.Display "flex"
                      CSSProp.FlexDirection "row" ]) ]
