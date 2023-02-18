namespace Informedica.ZForm.Lib


/// Provide mappings for using units and
/// routes for the G-Stand database, the
/// internal unit structure and an application.
module Mapping =

    open System

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.ZIndex.Lib
    open MathNet.Numerics
    open Informedica.GenUnits.Lib



    let timeUnits =
        [
            ("minuut", Units.Time.nDay)
            ("uur", Units.Time.nHour)
            ("dag", Units.Time.nDay)
            ("week", Units.Time.nWeek)
            ("maand", Units.Time.nMonth)
            ("jaar", Units.Time.nYear)
        ]


    let getUnits_ () = 
        Web.getDataFromSheet "Units"
        |> fun data ->
            data 
            |> Array.tryHead
            |> function 
            | None -> Array.empty
            | Some cs ->  
                let getStr c r = Csv.getStringColumn cs r c
                
                data
                |> Array.skip 1
                |> Array.map (fun r ->
                    {|
                        zindexlong = r |> getStr "ZIndexUnitLong"
                        zindexshort = r |>  getStr "ZIndexUnitShort"
                        mvunit = r |> getStr "MetaVisionUnit"
                        unit = r |> getStr "Unit"
                        group = r |> getStr "Group"
                    |}            
                )
                |> Array.map (fun r ->
                    {
                        ZIndexLong = r.zindexlong
                        ZIndexShort = r.zindexshort
                        MetaVision = r.mvunit
                        Unit = 
                            $"{r.unit}[{r.group}]"
                            |> Units.fromString
                            |> Option.defaultValue NoUnit
                    }
                )
        

    let getUnitMapping = Memoization.memoize getUnits_


    let getFrequencies_ () =
        Web.getDataFromSheet "Frequencies"
        |> fun data ->
            data 
            |> Array.tryHead
            |> function 
            | None -> Array.empty
            | Some cs ->  
                let getStr c r = Csv.getStringColumn cs r c
                let getInt c r = Csv.getInt32OptionColumn cs r c
                
                data
                |> Array.skip 1
                |> Array.map (fun r ->                
                    {|
                        zindex = r |> getStr "ZIndex"
                        zindexfreq = r |> getInt "ZIndexFreq"
                        zindexUnit = r |> getStr "ZIndexUnit"
                        mv1 = r |>  getStr "MetaVision1"
                        mv2 = r |> getStr "MetaVision2"
                        freq = r |> getInt "Freq"
                        n = r |> getInt "n"
                        time = r |> getStr "Unit"
                    |}            
                )
                |> Array.filter (fun r -> r.freq |> Option.isSome)
                |> Array.map (fun r ->
                    {
                        ZIndex = r.zindex
                        ZIndexFreq =    
                            r.zindexfreq
                            |> Option.defaultValue 1
                            |> BigRational.fromInt
                        ZIndexUnit = r.zindexUnit
                        MetaVision1 = r.mv1
                        MetaVision2 = r.mv2
                        Frequency = 
                            r.freq
                            |> Option.defaultValue 1
                            |> BigRational.fromInt

                        Unit =
                            let n = 
                                r.n 
                                |> Option.defaultValue 1
                                |> BigRational.fromInt
                            
                            let tu = 
                                timeUnits
                                |> List.tryFind (fst >> String.equalsCapInsens r.time)
                                |> function
                                | None   -> n |> Units.Count.nTimes
                                | Some u -> n |> (u |> snd)

                            Units.Count.times
                            |> Units.per tu
                    }
                )

    
    let getFrequencyMapping = Memoization.memoize getFrequencies_


    let unitToString (mapping : UnitMapping[]) u =
        mapping
        |> Array.tryFind (fun m -> m.Unit = u)
        |> Option.map (fun m -> m.ZIndexShort)
        |> Option.defaultValue ""


    let stringToUnit (mapping : UnitMapping[]) s =
        mapping
        |> Array.tryFind (fun m ->
            m.ZIndexLong |> String.equalsCapInsens s ||
            m.ZIndexShort |> String.equalsCapInsens s ||
            m.MetaVision |> String.equalsCapInsens s
        )
        |> Option.map (fun m -> m.Unit)
        |> Option.defaultValue NoUnit


    let mapFrequency n s =
        getFrequencyMapping ()
        |> Array.tryFind(fun f ->
            f.ZIndexFreq |> BigRational.toDecimal = n &&
            f.ZIndexUnit = s
        )