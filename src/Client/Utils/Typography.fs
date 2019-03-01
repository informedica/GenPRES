namespace Utils

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

    let h1 s =
        typography [ TypographyProp.Variant TypographyVariant.H1
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let h2 s =
        typography [ TypographyProp.Variant TypographyVariant.H2
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let h3 s =
        typography [ TypographyProp.Variant TypographyVariant.H3
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let h4 s =
        typography [ TypographyProp.Variant TypographyVariant.H4
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let h5 s =
        typography [ TypographyProp.Variant TypographyVariant.H5
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    let h6 s =
        typography [ TypographyProp.Variant TypographyVariant.H6
                     TypographyProp.Color TypographyColor.Inherit ] [ str s ]
