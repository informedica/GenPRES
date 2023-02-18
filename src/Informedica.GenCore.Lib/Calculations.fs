namespace Informedica.GenCore.Lib



module Calculations =


    open Informedica.Utils.Lib.BCL



    module Age =


        let yearsMonthsWeeksDaysToDays y (m : int<month>) w d =
            let dy, dm, dw =
                y |> Conversions.intYearsToDays,
                m |> int |> (*) 30 |> Conversions.dayFromInt,
                w |> Conversions.weeksToDays

            d + dy + dm + dw


        let yearsMonthsWeeksToDaysOpt y m w d =
            let y, m, w, d =
                y |> Option.defaultValue 0<year>,
                m |> Option.defaultValue 0<month>,
                w |> Option.defaultValue 0<week>,
                d |> Option.defaultValue 0<day>

            yearsMonthsWeeksDaysToDays y m w d


        let fromBirthDate bd dt =
            let y, m, w, d = DateTime.age bd dt
            y |> Conversions.yearFromInt,
            m |> Conversions.monthFromInt,
            w |> Conversions.weekFromInt,
            d |> Conversions.dayFromInt


        let toBirthDate dt (ys : int<year>) (ms : int<month>) (ws : int<week>) (ds : int<day>) =
            let ys = int ys * -1
            let ms = int ms * -1
            let ws = int ws * -1
            let ds = int ds * -1

            dt 
            |> DateTime.addYears ys
            |> DateTime.addMonths ms
            |> DateTime.addWeeks ws
            |> DateTime.addDays ds


        let adjustedAge (gestDays: int<day>) (gestWeeks: int<week>) dtBirth dtNow =
            let fullTerm = Constants.fullTerm |> Conversions.weeksToDays
            let chronological = DateTime.dateDiffDays dtNow dtBirth |> Conversions.dayFromInt
            let ageDiff = (fullTerm - (gestDays + (gestWeeks |> Conversions.weeksToDays)))
            chronological - ageDiff


        let postMenstrualAge (actAge: int<day>) (gestWeeks: int<week>) (gestDays: int<day>) =
            (gestWeeks |> Conversions.weeksToDays) + gestDays + actAge
            |> Conversions.daysToWeeks


        let ageToString yrs mos wks dys =
            let yToStr = Conversions.yearToString "year" "years"
            let mToStr = Conversions.monthToString "month" "months"
            let wToStr = Conversions.weekToString "week" "weeks"
            let dToStr = Conversions.dayToString "day" "days"

            [
                yrs |> Option.map yToStr 
                mos |> Option.map mToStr 
                wks |> Option.map wToStr 
                dys |> Option.map dToStr 
            ]
            |> List.map (Option.defaultValue "")


        let ageToStringNL yrs mos wks dys =
            let yToStr = Conversions.yearToString "jaar" "jaar"
            let mToStr = Conversions.monthToString "maand" "maanden"
            let wToStr = Conversions.weekToString "week" "weken"
            let dToStr = Conversions.dayToString "dag" "dagen"

            [
                yrs |> Option.map yToStr 
                mos |> Option.map mToStr 
                wks |> Option.map wToStr 
                dys |> Option.map dToStr 
            ]
            |> List.map (Option.defaultValue "")


        let ageToStringNlShort yrs mos wks dys =
            ageToStringNL yrs mos wks dys 
            |> function
            | [ys; ms; _; _] when ys |> String.notEmpty -> 
                [ys; ms] 
            | [_; ms; ws; _] when ms |> String.notEmpty -> 
                [ms; ws] 
            | [_; _; ws; ds] when ws |> String.notEmpty -> 
                [ws; ds] 
            | xs -> xs
            |> List.filter String.notEmpty
            |> String.concat ", "



    module BSA =


        let mosteller = fun w h -> sqrt (w * h / 3600.)

        let duBois = fun w h -> 0.007184 * (w ** 0.425) * (h ** 0.725)

        let haycock = fun w h -> 0.024265 * (w ** 0.5378) * (h ** 0.3964)

        let gehanAndGeorge = fun w h -> 0.0235 * (w ** 0.51456) * (h ** 0.42246)

        let fujimoto = fun w h -> 0.008883 * (w ** 0.444) * (h ** 0.663)


        let calcBSA formula fixPrec (weight : decimal<kg>) (height: decimal<cm>)  =
            let w = decimal weight |> float
            let h = decimal height |> float
            formula w h
            |> decimal
            |> fun x ->
                let x =
                    match fixPrec with
                    | Some p ->
                        x
                        |> Decimal.fixPrecision p
                    | None -> x

                x * 1m<bsa>


        let calcDuBois = calcBSA duBois

        let calcMosteller = calcBSA mosteller

        let calcHaycock = calcBSA haycock

        let calcGehanAndGeorge = calcBSA gehanAndGeorge

        let calcFujimoto = calcBSA fujimoto


    
    module Renal =

        
        type Gender = Male | Female

        
        type Race = Black | Other

        
        type Creat = | CreatinineMicroMolePerLiter of float<microMol/L> | CreatinineMilligramPerDeciLiter of float<mg/dL>

        
        type Cystatin = CystatinMilligramPerLiter of float<mg/L>

        
        type Urea = UreaMilligramPerDeciLiter of float<mg/dL> | UreaMilliMolePerLiter of float<mmol/L>


        type RenalFunction = 
            | Normal
            | MildlyDecreased
            | MildToModeratelyDecreased
            | ModerateToSeverlyDecreased
            | SeverelyDecreased
            | KidneyFailure
            | InvalidKidneyFunction of string


        let normal = 90.<mL/min/normalM2>

        let mild = 60.<mL/min/normalM2>

        let moderate = 45.<mL/min/normalM2>

        let severe = 30.<mL/min/normalM2>

        let failure = 15.<mL/min/normalM2>


        let renalFunction eGfr =
            match eGfr with
            | _ when eGfr >= normal -> Normal
            | _ when eGfr >= mild -> MildlyDecreased
            | _ when eGfr >= moderate -> MildToModeratelyDecreased
            | _ when eGfr >= severe -> ModerateToSeverlyDecreased
            | _ when eGfr >= failure -> SeverelyDecreased
            | _ when eGfr < failure && eGfr >= 0.<mL/min/normalM2> -> KidneyFailure
            | _ -> $"this {eGfr} is not valid" |> InvalidKidneyFunction
        


        let toMlMinNormBsa x = x * 1.<mL/min/normalM2>


        let creat09Formula (sCr: float<mg/dL>) (age : float<year>) alpha k a b =
            let sCr = sCr |> float
            let age = age |> float

            141. * 
            (([ sCr / k; 1. ] |> List.min) ** alpha) *
            (([ sCr / k; 1. ] |> List.max) ** (-1.209)) *
            (0.993 ** age) * a * b
            |> toMlMinNormBsa


        let calcCreatinine09 gend race age creat =
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter
            
            let alpha, k, a =
                match gend with
                | Female -> -0.329, 0.7, 1.018
                | Male -> -0.411, 0.9, 1.

            let b = 
                match race with
                | Black -> 1.159
                | Other -> 1.

            creat09Formula sCr age alpha k a b


        let creat21Formula (sCr: float<mg/dL>) (age : float<year>) alpha k a =
            let sCr = sCr |> float
            let age = age |> float

            142. * 
            (([ sCr / k; 1. ] |> List.min) ** alpha) *
            (([ sCr / k; 1. ] |> List.max) ** (-1.200)) *
            (0.9938 ** age) * a
            |> toMlMinNormBsa


        let calcCreatinine21 gend age creat =
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter
            
            let alpha, k, a =
                match gend with
                | Female -> -0.241, 0.7, 1.018
                | Male -> -0.302, 0.9, 1.

            creat21Formula sCr age alpha k a


        let cystatinCreatinine12Formula (sCr : float<mg/dL>) (sCy : float<mg/L>) (age : float<year>) alpha k a b =
            let sCr = sCr |> float
            let sCy = sCy |> float
            let age = age |> float

            135. * 
            (([ sCr / k; 1. ] |> List.min) ** alpha) *
            (([ sCr / k; 1. ] |> List.max) ** -0.601) *
            (([ sCy / 0.8; 1. ] |> List.min) ** -0.375) *
            (([ sCy / 0.8; 1. ] |> List.max) ** -0.711) *
            (0.995 ** age) * a * b
            |> toMlMinNormBsa


        let calcCystatinCreatinine12 gend race age creat cystatin =
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter
            
            let (CystatinMilligramPerLiter sCy) = cystatin

            let alpha, k,  a =
                match gend with
                | Female -> -0.248, 0.7, 0.969
                | Male -> -0.207, 0.9, 1.

            let b = 
                match race with
                | Black -> 1.08
                | Other -> 1.

            cystatinCreatinine12Formula sCr sCy age alpha k a b


        let cystatinCreatinine21Formula (sCr : float<mg/dL>) (sCy : float<mg/L>) (age : float<year>) alpha k a =
            let sCr = sCr |> float
            let sCy = sCy |> float
            let age = age |> float

            135. * 
            (([ sCr / k; 1. ] |> List.min) ** alpha) *
            (([ sCr / k; 1. ] |> List.max) ** -0.544) *
            (([ sCy / 0.8; 1. ] |> List.min) ** -0.323) *
            (([ sCy / 0.8; 1. ] |> List.max) ** -0.778) *
            (0.9961 ** age) * a
            |> toMlMinNormBsa


        let calcCystatinCreatinine21 gend age creat cystatin =
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter
            
            let (CystatinMilligramPerLiter sCy) = cystatin

            let alpha, k,  a =
                match gend with
                | Female -> -0.219, 0.7, 0.963
                | Male -> -0.144, 0.9, 1.

            cystatinCreatinine21Formula sCr sCy age alpha k a


        let cystatin12Formula (sCy : float<mg/L>) (age : float<year>) a =
            let sCy = sCy |> float
            let age = age |> float

            133. * 
            (([ sCy / 0.8; 1. ] |> List.min) ** -0.4999) *
            (([ sCy / 0.8; 1. ] |> List.max) ** -1.328) * 
            (0.996 ** age) * a
            |> toMlMinNormBsa


        let calcCystatin12 gend age cystatin =            
            let (CystatinMilligramPerLiter sCy) = cystatin

            let a =
                match gend with
                | Female -> 0.932
                | Male -> 1.

            cystatin12Formula sCy age a 


        let mdrdFormula (sCr: float<mg/dL>) (age : float<year>) a b =
            let sCr = sCr |> float
            let age = age |> float

            175. * 
            (sCr ** -1.154) *
            (age ** -0.203) * a * b
            |> toMlMinNormBsa


        let calcMDRD gend race age creat =
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let a =
                match gend with
                | Female -> 0.742
                | Male -> 1.

            let b = 
                match race with
                | Black -> 1.212
                | Other -> 1.

            mdrdFormula sCr age a b


        let pediatricSchwartzFormula (sCr: float<mg/dL>) (height : float<cm>) =
            let sCr = sCr |> float
            let height = height |> float

            0.413 * (height / sCr)
            |> toMlMinNormBsa


        let calcPediatricScharz height creat =
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            pediatricSchwartzFormula sCr height


        let pediatricCystatinCreatinineCKIDFormula (cyst: float<mg/L>) (sCr: float<mg/dL>) (bun: float<mg/dL>) (height : float<m>)  k =
            let sCr = sCr |> float
            let cyst = cyst |> float
            let bun = bun |> float
            let height = height |> float
             
            39.8 * // 39.8 
            ((height / sCr) ** 0.456) * // x [ht/Scr]0.456
            ((1.8 / cyst) ** 0.418) * // x [1.8/cysC]0.418 
            ((30. / bun) ** 0.079) * // x [30/BUN]0.079
            ((height / 1.4) ** 0.179) * // x [ht/140]0.179
            k // x [1.076male] [1.00female] 
            |> toMlMinNormBsa

    
        let calcPediatricCystatinCreatinineCKID gender height creat cystatin bun =
            let bun = 
                match bun with 
                | UreaMilligramPerDeciLiter v -> v
                | UreaMilliMolePerLiter v -> v |> Conversions.Urea.toMilliGramPerDeciLiter
            
            let sCr = 
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let (CystatinMilligramPerLiter cyst) = cystatin

            let k = 
                match gender with
                | Female -> 1.
                | Male -> 1.076

            pediatricCystatinCreatinineCKIDFormula cyst sCr bun height k

