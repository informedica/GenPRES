module Styles

open Fable.MaterialUI.Props
open Fable.MaterialUI.Core


let theme =
    createMuiTheme
        [ ThemeProp.Palette
              [ PaletteProp.Primary
                    [ PaletteIntentionProp.Main
                          Fable.MaterialUI.Colors.blue.``500`` ] ]

          ThemeProp.Typography
              [ ThemeTypographyProp.UseNextVariants true ] ]
