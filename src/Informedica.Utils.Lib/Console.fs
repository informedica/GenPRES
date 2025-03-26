namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module ConsoleWriter =

    open System
    open System.Text


    type Colors = {
        StandardFrontColor: ConsoleColor
        StandardBackColor: ConsoleColor
        QuestionFrontColor: ConsoleColor
        QuestionBackColor: ConsoleColor
        InfoFrontColor: ConsoleColor
        InfoBackColor: ConsoleColor
        InfoMessageFrontColor: ConsoleColor
        InfoMessageBackColor: ConsoleColor
        ErrorFrontColor: ConsoleColor
        ErrorBackColor: ConsoleColor
        ErrorMessageFrontColor: ConsoleColor
        ErrorMessageBackColor: ConsoleColor
        WarningFrontColor: ConsoleColor
        WarningBackColor: ConsoleColor
        WarningMessageFrontColor: ConsoleColor
        WarningMessageBackColor: ConsoleColor
    }


    let colors = {
        StandardFrontColor = ConsoleColor.White
        StandardBackColor = ConsoleColor.Black
        QuestionFrontColor = ConsoleColor.Black
        QuestionBackColor = ConsoleColor.White
        InfoFrontColor = ConsoleColor.Black
        InfoBackColor = ConsoleColor.Green
        InfoMessageFrontColor = ConsoleColor.Green
        InfoMessageBackColor = ConsoleColor.Black
        ErrorFrontColor = ConsoleColor.Black
        ErrorBackColor = ConsoleColor.Red
        ErrorMessageFrontColor = ConsoleColor.Red
        ErrorMessageBackColor = ConsoleColor.Black
        WarningFrontColor = ConsoleColor.Black
        WarningBackColor = ConsoleColor.Yellow
        WarningMessageFrontColor = ConsoleColor.Yellow
        WarningMessageBackColor = ConsoleColor.Black
    }


    let private lock f =
        let lockObj = obj()

        lock lockObj f


    /// Set the colors for the console
    let setColors (frontColor: ConsoleColor) (backgroundColor: ConsoleColor) =
        Console.ForegroundColor <- frontColor
        Console.BackgroundColor <- backgroundColor



    let writeColoredText symbol (text: string) (frontColor: ConsoleColor) (backgroundColor: ConsoleColor) (writeLine: bool) (writeCurrentTime: bool) =
        fun () ->
            Console.ResetColor()

            if writeCurrentTime then
                let clock =
                    Constants.HTMLCodeSymbols.TryFind "clock"
                    |> Option.defaultValue ""

                Console.ForegroundColor <- colors.StandardFrontColor
                Console.BackgroundColor <- colors.StandardBackColor
                Console.Out.Write($"{clock}  {DateTime.Now} : ")

            match symbol with
            | None   -> ()
            | Some s ->
                Console.ForegroundColor <- colors.StandardFrontColor
                Console.BackgroundColor <- colors.StandardBackColor
                Console.Out.Write($"%s{s} ")

            Console.ForegroundColor <- frontColor
            Console.BackgroundColor <- backgroundColor

            if writeLine then
                Console.Out.WriteLine(text)
            else
                Console.Out.Write(text)

            Console.ResetColor ()

            Console.Out.Flush()

        |> lock

    let writeText (text: string) (writeLine: bool) (writeTime: bool) =
        writeColoredText None text colors.StandardFrontColor colors.StandardBackColor writeLine writeTime


    let writeSpace = fun () -> writeText " " false false


    let writeQuestionMessage (text: string) (writeLine: bool) (writeTime: bool) =
        let question = Constants.HTMLCodeSymbols.TryFind "question"
        writeColoredText question text colors.QuestionFrontColor colors.QuestionBackColor writeLine writeTime


    let writeInfoMessage (text: string) (writeLine: bool) (writeTime: bool) =
        let info = Constants.HTMLCodeSymbols.TryFind "info"

        writeColoredText info "INFO:" colors.InfoFrontColor colors.InfoBackColor false writeTime
        writeSpace ()
        writeColoredText None text colors.InfoMessageFrontColor colors.InfoMessageBackColor writeLine false


    let writeErrorMessage (text: string) (writeLine: bool) (writeTime: bool) =
        let error = Constants.HTMLCodeSymbols.TryFind "error"

        writeColoredText error "ERROR:" colors.ErrorFrontColor colors.ErrorBackColor false writeTime
        writeSpace ()
        writeColoredText None text colors.ErrorMessageFrontColor colors.ErrorMessageBackColor writeLine false


    let writeWarningMessage (text: string) (_: bool) (writeTime: bool) =
        let warning = Constants.HTMLCodeSymbols.TryFind "warning"

        writeColoredText warning "WARNING:" colors.WarningFrontColor colors.WarningBackColor false writeTime
        writeSpace ()
        writeColoredText None text colors.WarningMessageFrontColor colors.WarningMessageBackColor true false


    let writeDebugMessage (text: string) (_: bool) (writeTime: bool) =
        if Env.getItem "GENPRES_DEBUG" |> Option.isNone then ()
        else
            let debug = Constants.HTMLCodeSymbols.TryFind "debug"

            writeColoredText debug "DEBUG:" colors.StandardFrontColor colors.StandardBackColor false writeTime
            writeSpace ()
            writeColoredText None text colors.StandardFrontColor colors.StandardBackColor true false


    let writeColoredTextWithStandardBackColor (text: string) (frontColor: ConsoleColor) (writeLine: bool) (writeCurrentTime: bool) =
        writeColoredText None text frontColor colors.StandardBackColor writeLine writeCurrentTime


    module Flip =

        let writeErrorMessage writeLine writeTime text = writeErrorMessage text writeLine writeTime

        let writeWarningMessage writeLine writeTime text = writeWarningMessage text writeLine writeTime

        let writeDebugMessage writeLine writeTime text = writeDebugMessage text writeLine writeTime

        let writeInfoMessage writeLine writeTime text = writeInfoMessage text writeLine writeTime


    module NewLineTime =

        let writeErrorMessage = Flip.writeErrorMessage true true

        let writeWarningMessage = Flip.writeWarningMessage true true

        let writeDebugMessage = Flip.writeDebugMessage true true

        let writeInfoMessage = Flip.writeInfoMessage true true


    module NewLineNoTime =

        let writeErrorMessage = Flip.writeErrorMessage true false

        let writeWarningMessage = Flip.writeWarningMessage true false

        let writeDebugMessage = Flip.writeDebugMessage true false

        let writeInfoMessage = Flip.writeInfoMessage true false