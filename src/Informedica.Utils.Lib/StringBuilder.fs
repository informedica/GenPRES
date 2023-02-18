namespace Informedica.Utils.Lib


module StringBuilder =

    open System.Text

    let builder (s : string) = StringBuilder(s)

    let append (s : string) (sb : StringBuilder) = sb.Append(s)

    let appendLine (s : string) (sb : StringBuilder) = sb.AppendLine(s)

    let newLine = appendLine ""

    let newLine2 sb =
        sb
        |> appendLine ""
        |> appendLine ""

    let appendFormat (fs : string) vs (sb : StringBuilder) = sb.AppendFormat(fs, (vs |> List.toArray))

    let appendLineFormat (fs : string) vs (sb : StringBuilder) = sb.AppendFormat(fs + "\n", (vs |> List.toArray))

    let replace (s1 : string) s2 (sb : StringBuilder) = sb.Replace(s1, s2)

    let toString (sb : StringBuilder) = sb.ToString()

