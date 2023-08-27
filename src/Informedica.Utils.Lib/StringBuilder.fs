namespace Informedica.Utils.Lib


/// Utility functions for working with strings.
[<RequireQualifiedAccess>]
module StringBuilder =

    open System.Text

    /// Creates a new StringBuilder with the given initial value.
    let builder (s : string) = StringBuilder(s)

    /// Append the given string to the StringBuilder.
    let append (s : string) (sb : StringBuilder) = sb.Append(s)

    /// Append a string as a new line to the StringBuilder.
    let appendLine (s : string) (sb : StringBuilder) = sb.AppendLine(s)

    /// Append a new line to the StringBuilder.
    let newLine = appendLine ""

    /// Append 2 new lines to the StringBuilder.
    let newLine2 sb =
        sb
        |> appendLine ""
        |> appendLine ""

    /// Append and format the given string to the StringBuilder.
    let appendFormat (fs : string) vs (sb : StringBuilder) = sb.AppendFormat(fs, (vs |> List.toArray))

    /// Append and format the given string as a new line to the StringBuilder.
    let appendLineFormat (fs : string) vs (sb : StringBuilder) = sb.AppendFormat(fs + "\n", (vs |> List.toArray))

    /// Replace all occurences of the given string with the given string in the StringBuilder.
    let replace (s1 : string) s2 (sb : StringBuilder) = sb.Replace(s1, s2)

    /// Return the string representation of the StringBuilder.
    let toString (sb : StringBuilder) = sb.ToString()


    module Tests =

        open Swensen.Unquote

        /// Test StringBuilder.append
        let testAppend () =
            let sb = StringBuilder("Hello")
            let r = sb |> append " World" |> toString
            test <@ r = "Hello World" @>


        /// Test StringBuilder.appendLine
        let testAppendLine () =
            let sb = StringBuilder("Hello")
            let r = sb |> appendLine " World" |> toString
            test <@ r = "Hello World\n" @>


        /// Test StringBuilder.newLine
        let testNewLine () =
            let sb = StringBuilder("Hello")
            let r = sb |> newLine |> toString
            test <@ r = "Hello\n" @>


        /// Test StringBuilder.newLine2
        let testNewLine2 () =
            let sb = StringBuilder("Hello")
            let r = sb |> newLine2 |> toString
            test <@ r = "Hello\n\n" @>


        /// Test StringBuilder.appendFormat
        let testAppendFormat () =
            let sb = StringBuilder("")
            let r = sb |> appendFormat "{0} World" ["Hello"] |> toString
            test <@ r = "Hello World" @>


        /// Test StringBuilder.appendLineFormat
        let testAppendLineFormat () =
            let sb = StringBuilder("")
            let r = sb |> appendLineFormat "{0} World" ["Hello"] |> toString
            test <@ r = "Hello World\n" @>


        /// Test StringBuilder.replace
        let testReplace () =
            let sb = StringBuilder("Hello")
            let r = sb |> replace "Hello" "Goodbye" |> toString
            test <@ r = "Goodbye" @>

        /// Run all tests
        let test () =
            testAppend()
            testAppendLine()
            testNewLine()
            testNewLine2()
            testAppendFormat()
            testAppendLineFormat()
            testReplace()
            ()