namespace Informedica.KinderFormularium.Lib


[<AutoOpen>]
module Utils =


    module Regex =

        open System.Text.RegularExpressions

        let regex s = Regex(s)

        let regexMatch m s = (m |> regex).Match(s)

        [<Literal>]
        let alphaRegex = "(?<Alpha>[a-zA-Z]*)"

        [<Literal>]
        let numRegex = "(?<Numeric>[0-9]*)"

        [<Literal>]
        let floatRegex = "(?<Float>[-+]?(\d*[.])?\d+)"

        let matchFloat s =
            (s |> regexMatch floatRegex).Groups["Float"].Value

        let matchAlpha s =
            (s |> regexMatch alphaRegex).Groups["Alpha"].Value

        let matchFloatAlpha s =
            let grps = (floatRegex + alphaRegex |> regex).Match(s).Groups
            grps["Float"].Value, grps["Alpha"].Value


    module File =

        open System.IO

        [<Literal>]
        let cachePath = "pediatric.cache"

        (*
        *)
        let writeTextToFile path text =
            File.WriteAllText(path, text)

        let exists path =
            File.Exists(path)

        let readAllLines path = File.ReadAllLines(path)



    module Json =

        open Newtonsoft.Json

        ///
        let serialize x =
            JsonConvert.SerializeObject(x)


        let deSerialize<'T> (s: string) =
            JsonConvert.DeserializeObject<'T>(s)



    module Web =

        open Informedica.Utils.Lib


        /// The url to the data sheet for Constraints
        let [<Literal>] dataUrlIdConstraints = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        /// The url to the data sheet for GenPRES
        /// https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
        let [<Literal>] private dataUrlIdGenPres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"


        let private getDataUrlId () =
            Env.getItem "GENPRES_URL_ID"
            |> Option.defaultValue  dataUrlIdGenPres
            |> fun s  ->
                ConsoleWriter.writeInfoMessage $"using: {s}" true false
                s


        let getDataUrlIdGenPres = Memoization.memoize getDataUrlId


        /// <summary>
        /// Get data from a web sheet
        /// </summary>
        /// <param name="urlId">The Url Id of the web sheet</param>
        /// <param name="sheet">The specific sheet</param>
        /// <returns>The data as a table of string array array</returns>
        let getDataFromSheet urlId sheet =
            fun () -> Web.GoogleSheets.getDataFromSheet urlId sheet
            |> StopWatch.clockFunc $"loaded {sheet} from web sheet"





    module Units =

        open Informedica.Utils.Lib.BCL
        open Informedica.GenUnits.Lib

        let week = Units.Time.week

        let day = Units.Time.day

        let weightGram = Units.Weight.gram

        let heightCm = Units.Height.centiMeter

        let bsaM2 = Units.BSA.m2


        let timeUnit s =
            if s |> String.isNullOrWhiteSpace then None
            else
                // TODO need better fix than this
                if s = "keer" then
                    $"times[Count]"
                else
                    $"{s}[Time]"
                |> Units.fromString


        let weightUnit s =
            if s |> String.isNullOrWhiteSpace then None
            else
                // TODO need to fix this in ValueUnit
                if s = "gr" || s = "g" then Some Units.Weight.gram
                else
                    $"{s}[Weight]"
                    |> Units.fromString


        let massUnit s =
            if s |> String.isNullOrWhiteSpace then None
            else
                $"{s}[Mass]"
                |> Units.fromString


        let freqUnit s =
            if s |> String.isNullOrWhiteSpace then None
            else
                $"times[Count]/{s}[Time]" |> Units.fromString


        let adjustUnit s =
            match s with
            | _ when s |> String.equalsCapInsens "kg" -> Units.Weight.kiloGram |> Some
            | _ when s |> String.equalsCapInsens "m2" -> bsaM2 |> Some
            | _ -> None


        let mL = Units.Volume.milliLiter


        let units =
            [
                "AntiXa/kg/dag","AntiXa[General]","kg[Weight]","day[Time]"
                "AntiXa/kg/dosis","AntiXa[General]","kg[Weight]",""
                "E/kg/dosis","IE[InternationalUnit]","kg[Weight]",""
                "IE/dag","IE[InternationalUnit]","","day[Time]"
                "IE/dosis","IE[InternationalUnit]","",""
                "IE/kg/dag","IE[InternationalUnit]","kg[Weight]","day[Time]"
                "IE/kg/dosis","IE[InternationalUnit]","kg[Weight]",""
                "IE/kg/uur","IE[InternationalUnit]","kg[Weight]","hour[Time]"
                "IE/kg/week","IE[InternationalUnit]","kg[Weight]","week[Time]"
                "IE/m²/dag","IE[InternationalUnit]","m2[BSA]","day[Time]"
                "IE/m²/dosis","IE[InternationalUnit]","m2[BSA]",""
                "IU/dag","IE[InternationalUnit]","","day[Time]"
                "IU/kg/uur","IE[InternationalUnit]","kg[Weight]","hour[Time]"
                "druppel(s)/dag","dr[Volume]","","day[Time]"
                "druppel(s)/dosis","dr[Volume]","",""
                "g/dag","g[Mass]","","day[Time]"
                "g/dosis","g[Mass]","",""
                "g/kg/dag","g[Mass]","kg[Weight]","day[Time]"
                "g/kg/dosis","g[Mass]","kg[Weight]",""
                "g/m²/dag","g[Mass]","m2[BSA]","day[Time]"
                "mg/dag","mg[Mass]","","day[Time]"
                "mg/dosis","mg[Mass]","",""
                "mg/dosis/dag","mg[Mass]","","day[Time]"
                "mg/kg per 2 weken","mg[Mass]","kg[Weight]","2 week[Time]"
                "mg/kg per 36 uren","mg[Mass]","kg[Weight]","36 hour[Time]"
                "mg/kg per 4 weken","mg[Mass]","kg[Weight]","4 week[Time]"
                "mg/kg per 48 uren","mg[Mass]","kg[Weight]","48 hour[Time]"
                "mg/kg per 72 uren","mg[Mass]","kg[Weight]","72 hour[Time]"
                "mg/kg/dag","mg[Mass]","kg[Weight]","day[Time]"
                "mg/kg/dosis","mg[Mass]","kg[Weight]",""
                "mg/kg/uur","mg[Mass]","kg[Weight]","hour[Time]"
                "mg/kg/week","mg[Mass]","kg[Weight]","week[Time]"
                "mg/m²/dag","mg[Mass]","m2[BSA]","day[Time]"
                "mg/m²/dosis","mg[Mass]","m2[BSA]",""
                "microg./dag","microg[Mass]","","day[Time]"
                "microg./dosis","microg[Mass]","",""
                "microg./kg/dag","microg[Mass]","kg[Weight]","day[Time]"
                "microg./kg/dosis","microg[Mass]","kg[Weight]",""
                "microg./kg/minuut","microg[Mass]","kg[Weight]","minute[Time]"
                "microg./kg/uur","microg[Mass]","kg[Weight]","hour[Time]"
                "microg./kg/week","microg[Mass]","kg[Weight]","week[Time]"
                "microg./m²/dag","microg[Mass]","m2[BSA]","day[Time]"
                "microg./m²/dosis","microg[Mass]","m2[BSA]",""
                "microg./m²/week","microg[Mass]","m2[BSA]","week[Time]"
                "microg./uur","microg[Mass]","","hour[Time]"
                "ml/dag","mL[Volume]","","day[Time]"
                "ml/dosis","mL[Volume]","",""
                "ml/dosis/dosis","mL[Volume]","",""
                "ml/kg/dag","mL[Volume]","kg[Weight]","day[Time]"
                "ml/kg/dosis","mL[Volume]","kg[Weight]",""
                "ml/kg/uur","mL[Volume]","kg[Weight]","hour[Time]"
                "mmol/dag","mmol[Molar]","","day[Time]"
                "mmol/kg/dag","mmol[Molar]","kg[Weight]","day[Time]"
                "mmol/kg/dosis","mmol[Molar]","kg[Weight]",""
                "mmol/m²/dag","mmol[Molar]","m2[BSA]","day[Time]"
                "ng/kg/dag","nanog[Mass]","kg[Weight]","day[Time]"
                "ng/kg/minuut","nanog[Mass]","kg[Weight]","minute[Time]"
                "pufje(s)/dosis","puf[General]","",""
            ]
            |> List.map (fun (s, du, au, tu) ->
                s,
                du |> Units.fromString,
                if au |> String.isNullOrWhiteSpace then None
                else
                    au |> Units.fromString
                ,
                if tu |> String.isNullOrWhiteSpace then None
                else
                    tu |> Units.fromString
            )


    module ValueUnit =

        open MathNet.Numerics
        open Informedica.Utils.Lib.BCL
        open Informedica.GenUnits.Lib


        /// The full term age for a neonate
        /// which is 37 weeks
        let ageFullTerm = 37N |> ValueUnit.singleWithUnit Units.Time.week


        let withOptionalUnit u v =
            match v, u with
            | Some v, Some u ->
                v
                |> ValueUnit.singleWithUnit u
                |> Some
            | _ -> None


        let toString prec vu =
            ValueUnit.toStringDecimalDutchShortWithPrec prec vu
            |> String.replace ";" ", "

