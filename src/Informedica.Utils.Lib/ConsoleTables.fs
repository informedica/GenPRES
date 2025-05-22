namespace Informedica.Utils.Lib


module ConsoleTables =

    open ConsoleTables

    let from<'T> rows =
        ConsoleTable.From<'T>(rows)


    let write format (table: ConsoleTable)=
        table.Write(format)