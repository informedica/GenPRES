namespace Utils

module Typography =

    open Feliz
    open Feliz.MaterialUI

    let private createTypography v (s : string) =
        Mui.typography [
            v
            typography.color.inherit'
            prop.text s
        ]

    let subtitle1 = createTypography typography.variant.subtitle1
    let subtitle2 = createTypography typography.variant.subtitle2
    let body1 = createTypography typography.variant.body1
    let body2 = createTypography typography.variant.body2
    let caption = createTypography typography.variant.caption
    let button = createTypography typography.variant.button
    let h1 = createTypography typography.variant.h1
    let h2 = createTypography typography.variant.h2
    let h3 = createTypography typography.variant.h3
    let h4 = createTypography typography.variant.h4
    let h5 = createTypography typography.variant.h5
    let h6 = createTypography typography.variant.h6

    // let subtitle1 s =
    //     typography [ TypographyProp.Variant TypographyVariant.Subtitle1
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let subtitle2 s =
    //     typography [ TypographyProp.Variant TypographyVariant.Subtitle2
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let body1 s =
    //     typography [ TypographyProp.Variant TypographyVariant.Body1
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let body2 s =
    //     typography [ TypographyProp.Variant TypographyVariant.Body2
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let caption s =
    //     typography [ TypographyProp.Variant TypographyVariant.Caption
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let button s =
    //     typography [ TypographyProp.Variant TypographyVariant.Button
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let h1 s =
    //     typography [ TypographyProp.Variant TypographyVariant.H1
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let h2 s =
    //     typography [ TypographyProp.Variant TypographyVariant.H2
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let h3 s =
    //     typography [ TypographyProp.Variant TypographyVariant.H3
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let h4 s =
    //     typography [ TypographyProp.Variant TypographyVariant.H4
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let h5 s =
    //     typography [ TypographyProp.Variant TypographyVariant.H5
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]

    // let h6 s =
    //     typography [ TypographyProp.Variant TypographyVariant.H6
    //                  TypographyProp.Color TypographyColor.Inherit ] [ str s ]
