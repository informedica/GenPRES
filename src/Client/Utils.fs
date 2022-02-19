namespace Utils


module Typography =

    open Feliz
    open Feliz.MaterialUI

    let private createTypography v a =
        Mui.typography [
            v
            typography.color.inherit'
            prop.text (a |> string)
        ]

    let subtitle1 a =
        a |> createTypography typography.variant.subtitle1

    let subtitle2 a =
        a |> createTypography typography.variant.subtitle2

    let body1 a =
        a |> createTypography typography.variant.body1

    let body2 a =
        a |> createTypography typography.variant.body2

    let caption a =
        a |> createTypography typography.variant.caption

    let button a =
        a |> createTypography typography.variant.button

    let h1 a =
        a |> createTypography typography.variant.h1

    let h2 a =
        a |> createTypography typography.variant.h2

    let h3 a =
        a |> createTypography typography.variant.h3

    let h4 a =
        a |> createTypography typography.variant.h4

    let h5 a =
        a |> createTypography typography.variant.h5

    let h6 a =
        a |> createTypography typography.variant.h6


module Logging =

    open Browser.Dom

    let log (msg: string) a = console.log (box msg, [| box a |])

    let error (msg: string) e = console.error (box msg, [| box e |])

    let warning (msg: string) a = console.warn (box msg, [| box a |])


// module Promise =

//     open Fable.Promise

//     let getData sheet =
//         promise {
//             let! data =
//                 fetch
//                     $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}"

//             return data
//         }