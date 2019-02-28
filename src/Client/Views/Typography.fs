namespace Views

module Typography =
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

    let subtitle1 s =
        typography [ TypographyProp.Variant TypographyVariant.Subtitle1
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let subtitle2 s =
        typography [ TypographyProp.Variant TypographyVariant.Subtitle2
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let body1 s =
        typography [ TypographyProp.Variant TypographyVariant.Body1
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let body2 s =
        typography [ TypographyProp.Variant TypographyVariant.Body2
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let caption s =
        typography [ TypographyProp.Variant TypographyVariant.Caption
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let button s =
        typography [ TypographyProp.Variant TypographyVariant.Button
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]
