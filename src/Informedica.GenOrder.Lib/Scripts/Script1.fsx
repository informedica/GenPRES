
#load "load.fsx"


#time


open Informedica.GenOrder.Lib


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib.Utils


type TotalItem =
    {
        Name : string
        Age : MinMax
        GestAge : MinMax
        Unit : Unit
        Adj : Unit
        TimeUnit : Unit
        PerTime : PerTime
        PerTimeAdj : PerTimeAdjust
    }


OrderVariable.PerTime.create

let totals =
    Web.GoogleSheets.getDataFromSheet
        "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
        "Totals"

        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r
                let toBrOpt = BigRational.toBrs >> Array.tryHead

                {|
                    Name = get "Name"
                    MinAge = get "MinAge" |> toBrOpt
                    MaxAge = get "MaxAge" |> toBrOpt
                    MinGestAge = get "MinGestAge" |> toBrOpt
                    MaxGestAge = get "MaxGestAge" |> toBrOpt
                    Unit = get "Unit"
                    Adj = get "Adj"
                    TimeUnit = get "TimeUnit"
                    MinPerTime = get "MinPerTime" |> toBrOpt
                    MaxPerTime = get "MaxPerTime" |> toBrOpt
                    MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                    MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                |}
            )


totals
|> Array.filter (_.Name >> String.notEmpty)