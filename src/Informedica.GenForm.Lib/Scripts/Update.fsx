

#load "load.fsx"
#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../MinMax.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"



[<AutoOpen>]
module Types =

    open MathNet.Numerics
    open Informedica.GenUnits.Lib

    type MinMax = Informedica.GenCore.Lib.Ranges.MinMax

    /// Associate a Route and a Shape
    /// setting default values for the other fields
    type RouteShape =
        {
            /// The Route
            Route : string
            /// The pharmacological Shape
            Shape : string
            /// The Unit of the Shape
            Unit : Unit
            /// The Dose Unit to use for Dose Limits
            DoseUnit : Unit
            /// The minimum Dose quantity
            MinDoseQty : ValueUnit option
            /// The maximum Dose quantity
            MaxDoseQty : ValueUnit option
            /// Whether a Dose runs over a Time
            Timed : bool
            /// Whether the Shape needs to be reconstituted
            Reconstitute : bool
            /// Whether the Shape is a solution
            IsSolution : bool
        }


    /// The types for VenousAccess.
    type VenousAccess =
        /// Peripheral Venous Access
        | PVL
        /// Central Venous Access
        | CVL
        /// Any Venous Access
        | AnyAccess


    /// Possible Genders.
    type Gender = Male | Female | AnyGender


    /// Possible Dose Types.
    type DoseType =
        /// A Start Dose
        | Start
        /// A Once only Dose
        | Once
        /// A PRN Dose
        | PRN
        /// A Maintenance Dose
        | Maintenance
        /// A Continuous Dose
        | Continuous
        /// A Step Down Dose in a number of steps.
        | StepDown of int
        /// A Step Up Dose in a number of steps.
        | StepUp of int

        | Contraindicated
        /// Any Dosetype
        | AnyDoseType


    /// A Shape Route with associated attributes.
    type ShapeRoute =
        {
            /// The pharmacological Shape
            Shape : string
            /// The Route of administration
            Route : string
            /// The Unit of the Shape
            Unit  : Unit
            /// The Dose Unit to use for Dose Limits
            DoseUnit : Unit
            /// Whether a Dose runs over a Time
            Timed : bool
            /// Whether the Shape needs to be reconstituted
            Reconstitute : bool
            /// Whether the Shape is a solution
            IsSolution : bool
        }


    /// A Substance type.
    type Substance =
        {
            /// The name of the Substance
            Name : string
            /// The Quantity of the Substance
            Quantity : ValueUnit option
            /// The indivisible Quantity of the Substance
            MultipleQuantity : ValueUnit option
        }

    /// A Product type.
    type Product =
        {
            /// The GPK id of the Product
            GPK : string
            /// The ATC code of the Product
            ATC : string
            /// The ATC main group of the Product
            MainGroup : string
            /// The ATC sub group of the Product
            SubGroup : string
            /// The Generic name of the Product
            Generic : string
            /// A tall-man representation of the Generic name of the Product
            TallMan : string
            /// Synonyms for the Product
            Synonyms : string array
            /// The full product name of the Product
            Product : string
            /// The label of the Product
            Label : string
            /// The pharmacological Shape of the Product
            Shape : string
            /// The possible Routes of administration of the Product
            Routes : string []
            /// The possible quantities of the Shape of the Product
            ShapeQuantities : ValueUnit
            /// The uid of the Shape of the Product
            ShapeUnit : Unit
            /// Whether the Shape of the Product requires reconstitution
            RequiresReconstitution : bool
            /// The possible reconstitution rules for the Product
            Reconstitution : Reconstitution []
            /// The division factor of the Product
            Divisible : ValueUnit
            /// The Substances in the Product
            Substances : Substance array
        }
    and Reconstitution =
        {
            /// The route for the reconstitution
            Route : string
            /// The DoseType for the reconstitution
            DoseType: DoseType
            /// The department for the reconstitution
            Department : string
            /// The location for the reconstitution
            Location : VenousAccess
            /// The volume of the reconstitution
            DiluentVolume : ValueUnit
            /// An optional expansion volume of the reconstitution
            ExpansionVolume : ValueUnit option
            /// The Diluents for the reconstitution
            Diluents : string []
        }


    /// A DoseLimit for a Shape or Substance.
    type DoseLimit =
        {
            /// The Target for the Doselimit
            DoseLimitTarget : DoseLimitTarget
            /// A MinMax Dose Quantity for the DoseLimit
            Quantity : MinMax
            /// An optional Dose Quantity Adjust for the DoseLimit.
            /// Note: if this is specified a min and max QuantityAdjust
            /// will be assumed to be 10% minus and plus the normal value
            NormQuantityAdjust : ValueUnit option
            /// A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust : MinMax
            /// An optional Dose Per Time for the DoseLimit
            PerTime : MinMax
            /// An optional Per Time Adjust for the DoseLimit
            /// Note: if this is specified a min and max NormPerTimeAdjust
            /// will be assumed to be 10% minus and plus the normal value
            NormPerTimeAdjust : ValueUnit option
            /// A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust : MinMax
            /// A MinMax Rate for the DoseLimit
            Rate : MinMax
            /// A MinMax Rate Adjust for the DoseLimit
            RateAdjust : MinMax
        }
    and DoseLimitTarget =
        | NoDoseLimitTarget
        | SubstanceDoseLimitTarget of string
        | ShapeDoseLimitTarget of string


    /// A PatientCategory to which a DoseRule applies.
    type PatientCategory =
        {
            Department : string option
            Diagnoses : string []
            Gender : Gender
            Age : MinMax
            Weight : MinMax
            BSA : MinMax
            GestAge : MinMax
            PMAge : MinMax
            Location : VenousAccess
        }


    /// A specific Patient to filter DoseRules.
    type Patient =
        {
            /// The Department of the Patient
            Department : string option
            /// A list of Diagnoses of the Patient
            Diagnoses : string []
            /// The Gender of the Patient
            Gender : Gender
            /// The Age in days of the Patient
            Age : ValueUnit option
            /// The Weight in grams of the Patient
            Weight : ValueUnit option
            /// The Height in cm of the Patient
            Height : ValueUnit option
            /// The Gestational Age in days of the Patient
            GestAge : ValueUnit option
            /// The Post Menstrual Age in days of the Patient
            PMAge : ValueUnit option
            /// The Venous Access of the Patient
            VenousAccess : VenousAccess list
        }
        static member Gender_ =
            (fun (p : Patient) -> p.Gender), (fun g (p : Patient) -> { p with Gender = g})

        static member Age_ =
            (fun (p : Patient) -> p.Age), (fun a (p : Patient) -> { p with Age = a})

        static member Weight_ =
            (fun (p : Patient) -> p.Weight), (fun w (p : Patient) -> { p with Weight = w})

        static member Height_ =
            (fun (p : Patient) -> p.Height), (fun b (p : Patient) -> { p with Height = b})

        static member GestAge_ =
            (fun (p : Patient) -> p.GestAge), (fun a (p : Patient) -> { p with GestAge = a})

        static member PMAge_ =
            (fun (p : Patient) -> p.PMAge), (fun a (p : Patient) -> { p with PMAge = a})

        static member Department_ =
            (fun (p : Patient) -> p.Department), (fun d (p : Patient) -> { p with Department = d})


    /// The DoseRule type. Identifies exactly one DoseRule
    /// for a specific PatientCategory, Indication, Generic, Shape, Route and DoseType.
    type DoseRule =
        {
            /// The Indication of the DoseRule
            Indication : string
            /// The Generic of the DoseRule
            Generic : string
            /// The pharmacological Shape of the DoseRule
            Shape : string
            /// The Route of administration of the DoseRule
            Route : string
            /// The PatientCategory of the DoseRule
            PatientCategory : PatientCategory
            /// The DoseType of the DoseRule
            DoseType : DoseType
            /// The possible Frequencies of the DoseRule
            Frequencies : ValueUnit option
            /// The MinMax Administration Time of the DoseRule
            AdministrationTime : MinMax
            /// The MinMax Interval Time of the DoseRule
            IntervalTime : MinMax
            /// The MinMax Duration of the DoseRule
            Duration : MinMax
            /// The list of associated DoseLimits of the DoseRule.
            /// In principle for the Shape and each Substance .
            DoseLimits : DoseLimit array
            /// The list of associated Products of the DoseRule.
            Products : Product array
        }


    /// A SolutionLimit for a Substance.
    type SolutionLimit =
        {
            /// The Substance for the SolutionLimit
            Substance : string
            /// The MinMax Quantity of the Substance for the SolutionLimit
            Quantity : MinMax
            /// A list of possible Quantities of the Substance for the SolutionLimit
            Quantities : ValueUnit option
            /// The Minmax Concentration of the Substance for the SolutionLimit
            Concentration : MinMax
        }


    /// A SolutionRule for a specific Generic, Shape, Route, DoseType, Department
    /// Venous Access Location, Age range, Weight range, Dose range and Generic Products.
    type SolutionRule =
        {
            /// The Generic of the SolutionRule
            Generic : string
            /// The Shape of the SolutionRule
            Shape : string
            /// The Route of the SolutionRule
            Route : string
            /// The DoseType of the SolutionRule
            DoseType : DoseType
            /// The Department of the SolutionRule
            Department : string
            /// The Venous Access Location of the SolutionRule
            Location : VenousAccess
            /// The MinMax Age range of the SolutionRule
            Age : MinMax
            /// The MinMax Weight range of the SolutionRule
            Weight : MinMax
            /// The MinMax Dose range of the SolutionRule
            Dose : MinMax
            /// The Products the SolutionRule applies to
            Products : Product []
            /// The possible Solutions to use
            Solutions : string []
            /// The possible Volumes to use
            Volumes : ValueUnit option
            /// A MinMax Volume range to use
            Volume : MinMax
            /// The percentage to be use as a DoseQuantity
            DosePerc : MinMax
            /// The SolutionLimits for the SolutionRule
            SolutionLimits : SolutionLimit []
        }


    /// A Filter to get the DoseRules for a specific Patient.
    type Filter =
        {
            /// The Indication to filter on
            Indication : string option
            /// The Generic to filter on
            Generic : string option
            /// The Shape to filter on
            Shape : string option
            /// The Route to filter on
            Route : string option
            /// The DoseType to filter on
            DoseType : DoseType
            /// The patient to filter on
            Patient : Patient
        }


    /// A PrescriptionRule for a specific Patient
    /// with a DoseRule and a list of SolutionRules.
    type PrescriptionRule =
        {
            Patient : Patient
            DoseRule : DoseRule
            SolutionRules : SolutionRule []
        }



[<AutoOpen>]
module Utils =

    open System
    open System.IO
    open System.Net.Http

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL



    module Web =


        /// The url to the data sheet for Constraints
        let [<Literal>] dataUrlIdConstraints = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        /// The url to the data sheet for GenPRES
        /// https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
        let [<Literal>] dataUrlIdGenPres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"


        /// <summary>
        /// Get data from a web sheet
        /// </summary>
        /// <param name="urlId">The Url Id of the web sheet</param>
        /// <param name="sheet">The specific sheet</param>
        /// <returns>The data as a table of string array array</returns>
        let getDataFromSheet urlId sheet =
            fun () -> Web.GoogleSheets.getDataFromSheet urlId sheet
            |> StopWatch.clockFunc $"loaded {sheet} from web sheet"



    module BigRational =


        /// <summary>
        /// Parse an array of strings in float format to an array of BigRational
        /// </summary>
        /// <remarks>
        /// Uses ; as separator. Filters out non parsable strings.
        /// </remarks>
        /// <example>
        /// <code>
        /// let brs = toBrs "1.0;2.0;3.0"
        /// // returns [|1N; 2N; 3N|]
        /// let brs = toBrs "1.0;2.0;3.0;abc"
        /// // returns [|1N; 2N; 3N|]
        /// </code>
        /// </example>
        let toBrs s =
            s
            |> String.splitAt ';'
            |> Array.choose Double.tryParse
            |> Array.choose BigRational.fromFloat


        /// <summary>
        /// Return 2 BigRational arrays as a tuple of optional first BigRational
        /// of the first and second array. A None is returned for an empty array.
        /// </summary>
        /// <example>
        /// <code>
        /// let brs1 = [|1N|]
        /// let brs2 = [|4N|]
        /// tupleBrOpt brs1 brs2
        /// // returns (Some 1N, Some 4N)
        /// let brs1 = [|1N|]
        /// let brs2 = [||]
        /// tupleBrOpt brs1 brs2
        /// // returns (Some 1N, None)
        /// </code>
        /// </example>
        let tupleBrOpt brs1 brs2 =
            brs1 |> Array.tryHead,
            brs2 |> Array.tryHead


    module Calculations =

        open MathNet.Numerics
        open Informedica.GenUnits.Lib

        module Conversions = Informedica.GenCore.Lib.Conversions
        module BSA = Informedica.GenCore.Lib.Calculations.BSA

        let calcDuBois weight height =
            let w =
                weight
                |> ValueUnit.convertTo Units.Mass.kiloGram
                |> ValueUnit.getValue
                |> Array.tryHead
                |> Option.defaultValue 0N
                |> BigRational.toDecimal
                |> Conversions.kgFromDecimal
            let h =
                height
                |> ValueUnit.convertTo Units.Height.centiMeter
                |> ValueUnit.getValue
                |> Array.tryHead
                |> Option.defaultValue 0N
                |> BigRational.toDecimal
                |> Conversions.cmFromDecimal

            BSA.calcDuBois (Some 2) w h
            |> decimal
            |> BigRational.fromDecimal
            |> ValueUnit.singleWithUnit Units.BSA.m2


    module ValueUnit =

        open MathNet.Numerics
        open Informedica.GenUnits.Lib


        /// The full term age for a neonate
        /// which is 37 weeks
        let ageFullTerm =
            37N
            |> ValueUnit.singleWithUnit Units.Time.week


    module MinMax =

        open Informedica.GenUnits.Lib
        open Informedica.GenCore.Lib.Ranges

        let fromTuple u (min, max) =
            match u |> Units.fromString with
            | None -> MinMax.empty
            | Some u ->
                {
                    Min =
                        min
                        |> Option.map (ValueUnit.singleWithUnit u)
                        |> Option.map Inclusive
                    Max =
                        max
                        |> Option.map (ValueUnit.singleWithUnit u)
                        |> Option.map Inclusive
                }


module Mapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib


    /// Mapping of long Z-index route names to short names
    let routeMapping =
        Web.getDataFromSheet Web.dataUrlIdGenPres "Routes"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {|
                    Long = get "ZIndex"
                    Short = get "ShortDutch"
                |}
            )


    /// Mapping of long Z-index unit names to short names
    let unitMapping =
        Web.getDataFromSheet Web.dataUrlIdGenPres "Units"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get = getColumn r

                {|
                    Long = get "ZIndexUnitLong"
                    Short = get "Unit"
                    MV = get "MetaVisionUnit"
                |}
            )


    /// Try to find mapping for a route
    let mapRoute rte =
        routeMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens rte ||
            r.Short |> String.equalsCapInsens rte

        )
        |> Option.map (fun r -> r.Short)


    /// Try to map a unit to a short name
    let mapUnit unt =
        unitMapping
        |> Array.tryFind (fun r ->
            r.Long |> String.equalsCapInsens unt ||
            r.Short |> String.equalsCapInsens unt
        )
        |> Option.map (fun r -> r.Short)


    /// Get the array of RouteShape records
    let mappingRouteShape =
        Web.getDataFromSheet Web.dataUrlIdGenPres "ShapeRoute"
        |> fun data ->
            let inline getColumn get =
                data
                |> Array.head
                |> get

            data
            |> Array.tail
            |> Array.map (fun r ->
                let getStr = getColumn Csv.getStringColumn r
                let getFlt = getColumn Csv.getFloatOptionColumn r

                {
                    Route = getStr "Route"
                    Shape = getStr "Shape"
                    Unit = getStr "Unit" |> Units.fromString |> Option.defaultValue NoUnit
                    DoseUnit = getStr "DoseUnit" |> Units.fromString |> Option.defaultValue NoUnit
                    MinDoseQty = None // getFlt "MinDoseQty"
                    MaxDoseQty = None //getFlt "MaxDoseQty"
                    Timed = getStr "Timed" |> String.equalsCapInsens "true"
                    Reconstitute = getStr "Reconstitute" |> String.equalsCapInsens "true"
                    IsSolution = getStr "IsSolution" |> String.equalsCapInsens "true"
                }
                |> fun rs ->
                    match rs.DoseUnit with
                    | NoUnit -> rs
                    | du ->
                        { rs with
                            MinDoseQty =
                                getFlt "MinDoseQty"
                                |> Option.bind (fun v ->
                                   v
                                   |> BigRational.fromFloat
                                   |> Option.map (ValueUnit.singleWithUnit du)
                                )
                            MaxDoseQty =
                                getFlt "MaxDoseQty"
                                |> Option.bind (fun v ->
                                   v
                                   |> BigRational.fromFloat
                                   |> Option.map (ValueUnit.singleWithUnit du)
                                )
                        }
            )


    /// <summary>
    /// Filter the mappingRouteShape array on route, shape and unit
    /// </summary>
    /// <param name="rte">The Route</param>
    /// <param name="shape">The Shape</param>
    /// <param name="unt">The Unit</param>
    /// <returns>An array of RouteShape records</returns>
    let filterRouteShapeUnit rte shape unt =
        mappingRouteShape
        |> Array.filter (fun xs ->
            let eqsRte =
                rte |> String.isNullOrWhiteSpace ||
                rte |> String.trim |> String.equalsCapInsens xs.Route ||
                xs.Route |> mapRoute |> Option.map (String.equalsCapInsens (rte |> String.trim)) |> Option.defaultValue false
            let eqsShp = shape |> String.isNullOrWhiteSpace || shape |> String.trim |> String.equalsCapInsens xs.Shape
            let eqsUnt =
                unt = NoUnit ||
                unt |> Units.eqsUnit xs.Unit
            eqsRte && eqsShp && eqsUnt
        )


    let private requires_ (rtes, unt, shape) =
        rtes
        |> Array.collect (fun rte ->
            filterRouteShapeUnit rte shape unt
        )
        |> Array.map (fun xs -> xs.Reconstitute)
        |> Array.exists id


    /// Check if reconstitution is required for a route, shape and unit
    let requiresReconstitution =
        Memoization.memoize requires_



module Gender =

    open Informedica.Utils.Lib.BCL


    /// Map a string to a Gender.
    let fromString s =
        let s = s |> String.toLower |> String.trim
        match s with
        | "man" -> Male
        | "vrouw" -> Female
        | _ -> AnyGender


    /// Get the string representation of a Gender.
    let toString = function
        | Male -> "man"
        | Female -> "vrouw"
        | AnyGender -> ""


    /// Check if a Filter contains a Gender.
    /// Note if AnyGender is specified, this will always return true.
    let filter gender (filter : Filter) =
        match filter.Patient.Gender, gender with
        | _, AnyGender -> true
        | _ -> filter.Patient.Gender = gender



module PatientCategory =


    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Limit = Informedica.GenCore.Lib.Ranges.Limit
    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
    module Conversions = Informedica.GenCore.Lib.Conversions


    /// <summary>
    /// Use a PatientCategory to get a sort value.
    /// </summary>
    /// <remarks>
    /// The order will be based on the following:
    /// - Age
    /// - Weight
    /// - Gestational Age
    /// - Post Menstrual Age
    /// The first will receive the highest weight and the last the lowest.
    /// </remarks>
    let sortBy (pat : PatientCategory) =
        let toInt = function
            | Some x ->
                x
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.tryHead
                |> function
                    | None -> 0
                    | Some x -> x |> BigRational.ToInt32
            | None -> 0

        (pat.Age.Min |> toInt |> fun i -> if i > 0 then i + 300 else i) +
        (pat.GestAge.Min |> toInt) +
        (pat.PMAge.Min |> toInt) +
        (pat.Weight.Min |> toInt |> fun w -> w / 1000)


    /// <summary>
    /// Filters a PatientCategory using a Filter.
    /// Returns true if the PatientCategory matches the Filter criteria.
    /// </summary>
    /// <param name="filter">The Filter to filter the PatientCategory with</param>
    /// <param name="patCat">The Patient Category</param>
    let filter (filter : Filter) (patCat : PatientCategory) =
        let eqs a b =
            match a, b with
            | None, _
            | _, None -> true
            | Some a, Some b -> a = b

        let inRange mm v =
            v
            |> Option.map (fun v ->
                mm |> MinMax.inRange v
            )
            |> Option.defaultValue true

        ([| patCat |]
        |> Array.filter (fun p ->
            if filter.Patient.Diagnoses |> Array.isEmpty then true
            else
                p.Diagnoses
                |> Array.exists (fun d ->
                    filter.Patient.Diagnoses
                    |> Array.exists (String.equalsCapInsens d)
                )
        ),
        [|
            fun (p: PatientCategory) -> filter.Patient.Department |> eqs p.Department
            fun (p: PatientCategory) -> filter.Patient.Age |> inRange p.Age
            fun (p: PatientCategory) -> filter.Patient.Weight |> inRange p.Weight
            fun (p: PatientCategory) ->
                match filter.Patient.Weight, filter.Patient.Height with
                | Some w, Some h ->
                    Utils.Calculations.calcDuBois w h
                    |> Some
                | _ -> None
                |> inRange p.BSA
            if filter.Patient.Age |> Option.isSome then
                yield! [|
                    fun (p: PatientCategory) ->
                        // defaults to normal gestation
                        filter.Patient.GestAge
                        |> Option.defaultValue Utils.ValueUnit.ageFullTerm
                        |> Some
                        |> inRange p.GestAge
                    fun (p: PatientCategory) ->
                        // defaults to normal postmenstrual age
                        filter.Patient.PMAge
                        |> Option.defaultValue Utils.ValueUnit.ageFullTerm
                        |> Some
                        |> inRange p.PMAge
                |]
            fun (p: PatientCategory) -> filter |> Gender.filter p.Gender
            fun (p: PatientCategory) ->
                match p.Location, filter.Patient.VenousAccess with
                | AnyAccess, xs
                | _, xs when xs |> List.isEmpty  -> true
                | _ ->
                    filter.Patient.VenousAccess
                    |> List.exists ((=) p.Location)
        |])
        ||> Array.fold(fun acc pred ->
            acc
            |> Array.filter pred
        )
        |> fun xs -> xs |> Array.length > 0


    /// <summary>
    /// Check whether an age and weight are between the
    /// specified age MinMax and weight MinMax.
    /// </summary>
    /// <param name="age">An optional age</param>
    /// <param name="weight">An optional weight</param>
    /// <param name="aMinMax">The age MinMax</param>
    /// <param name="wMinMax">The weight MinMax</param>
    /// <remarks>
    /// When age and or weight are not specified, they are
    /// considered to be between the minimum and maximum.
    /// </remarks>
    let checkAgeWeightMinMax age weight aMinMax wMinMax =
        // TODO rename aMinMax and wMinMax to ageMinMax and weightMinMax
        let inRange mm v =
            v
            |> Option.map (fun v ->
                mm |> MinMax.inRange v
            )
            |> Option.defaultValue true

        age |> inRange aMinMax &&
        weight |> inRange wMinMax


    /// Prints an age in days as a string.
    let printAge age =
        let age =
            age
            |> ValueUnit.convertTo Units.Time.day
            |> ValueUnit.getValue
            |> Array.tryHead
            |> Option.defaultValue 0N
        let a = age |> BigRational.ToInt32
        match a with
        | _ when a < 7 ->
            if a = 1 then $"%i{a} dag"
            else $"%i{a} dagen"
        | _ when a <= 30 ->
            let a = a / 7
            if a = 1 then $"%i{a} week"
            else $"%i{a} weken"
        | _ when a < 365 ->
            let a = a / 30
            if a = 1 then $"%i{a} maand"
            else $"%i{a} maanden"
        | _ ->
            let a = a / 365
            if a = 1 then $"%A{a} jaar"
            else $"%A{a} jaar"


    /// Print days as weeks.
    let printDaysToWeeks d =
        let d =
            d
            |> ValueUnit.convertTo Units.Time.day
            |> ValueUnit.getValue
            |> Array.tryHead
            |> Option.defaultValue 0N

        let d = d |> BigRational.ToInt32
        (d / 7) |> sprintf "%i weken"


    /// Print an MinMax age as a string.
    let printAgeMinMax (age : MinMax) =
        let printAge = Limit.getValueUnit >> printAge
        match age.Min, age.Max with
        | Some min, Some max ->
            let min = min |> printAge
            let max = max |> printAge
            $"leeftijd %s{min} tot %s{max}"
        | Some min, None ->
            let min = min |> printAge
            $"leeftijd vanaf %s{min}"
        | None, Some max ->
            let max = max |> printAge
            $"leeftijd tot %s{max}"
        | _ -> ""



    /// Print an PatientCategory as a string.
    let toString (pat : PatientCategory) =

        let gender = pat.Gender |> Gender.toString

        let age = pat.Age |> printAgeMinMax

        let neonate =
            let s =
                if pat.GestAge.Max.IsSome &&
                   pat.GestAge.Max.Value
                   |> Limit.getValueUnit  <? Utils.ValueUnit.ageFullTerm then "prematuren"
                else "neonaten"

            let printDaysToWeeks = Limit.getValueUnit >> printDaysToWeeks

            match pat.GestAge.Min, pat.GestAge.Max, pat.PMAge.Min, pat.PMAge.Max with
            | _, _, Some min, Some max ->
                let min = min |> printDaysToWeeks
                let max = max |> printDaysToWeeks
                $"{s} postconceptie leeftijd %s{min} tot %s{max}"
            | _, _, Some min, None ->
                let min = min |> printDaysToWeeks
                $"{s} postconceptie leeftijd vanaf %s{min}"
            | _, _, None, Some max ->
                let max = max |> printDaysToWeeks
                $"{s} postconceptie leeftijd tot %s{max}"

            | Some min, Some max, _, _ ->
                let min = min |> printDaysToWeeks
                let max = max |> printDaysToWeeks
                $"{s} zwangerschapsduur %s{min} tot %s{max}"
            | Some min, None, _, _ ->
                let min = min |> printDaysToWeeks
                if s = "neonaten" then s
                else
                    $"{s} zwangerschapsduur vanaf %s{min}"
            | None, Some max, _, _ ->
                let max = max |> printDaysToWeeks
                $"{s} zwangerschapsduur tot %s{max}"
            | _ -> ""

        let weight =
            let toStr vu =
                let v =
                    vu
                    |> Limit.getValueUnit
                    |> ValueUnit.getValue
                    |> Array.tryHead
                    |> Option.defaultValue 0N
                if v.Denominator = 1I then v |> BigRational.ToInt32 |> sprintf "%i"
                else
                    v
                    |> BigRational.ToDouble
                    |> sprintf "%A"

            match pat.Weight.Min, pat.Weight.Max with
            | Some min, Some max -> $"gewicht %s{min |> toStr} tot %s{max |> toStr} kg"
            | Some min, None     -> $"gewicht vanaf %s{min |> toStr} kg"
            | None,     Some max -> $"gewicht tot %s{max |> toStr} kg"
            | None,     None     -> ""

        [
            pat.Department |> Option.defaultValue ""
            gender
            neonate
            age
            weight
        ]
        |> List.filter String.notEmpty
        |> List.filter (String.isNullOrWhiteSpace >> not)
        |> String.concat ", "



module Patient =

    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Conversions = Informedica.GenCore.Lib.Conversions
    module Limit = Informedica.GenCore.Lib.Ranges.Limit

    /// An empty Patient.
    let patient =
        {
            Department = None
            Diagnoses = [||]
            Gender = AnyGender
            Age = None
            Weight = None
            Height = None
            GestAge = None
            PMAge = None
            VenousAccess = [ ]
        }


    let calcPMAge (pat: Patient) =
        { pat with
            PMAge =
                match pat.Age, pat.GestAge with
                | Some ad, Some ga -> ad + ga |> Some
                | _ -> None
        }


    /// Calculate the BSA of a Patient.
    let calcBSA (pat: Patient) =
        match pat.Weight, pat.Height with
        | Some w, Some h ->
            Utils.Calculations.calcDuBois w h
            |> Some
        | _ -> None


    /// Get the string representation of a Patient.
    let rec toString (pat: Patient) =
        [
            pat.Department |> Option.defaultValue ""
            pat.Gender |> Gender.toString
            pat.Age
            |> Option.map PatientCategory.printAge
            |> Option.defaultValue ""

            let printDaysToWeeks = PatientCategory.printDaysToWeeks

            let s =
                if pat.GestAge.IsSome &&
                   pat.GestAge.Value  < Utils.ValueUnit.ageFullTerm then "prematuren"
                else "neonaten"

            match pat.GestAge, pat.PMAge with
            | _, Some a ->
                let a = a |> printDaysToWeeks
                $"{s} postconceptie leeftijd %s{a}"
            | Some a, _ ->
                let a = a |> printDaysToWeeks
                $"{s} zwangerschapsduur %s{a}"
            | _ -> ""

            let toStr u vu =
                let v =
                    vu
                    |> ValueUnit.convertTo u
                    |> ValueUnit.getValue
                    |> Array.tryHead
                    |> Option.defaultValue 0N
                if v.Denominator = 1I then v |> BigRational.ToInt32 |> sprintf "%i"
                else
                    v
                    |> BigRational.ToDouble
                    |> sprintf "%A"

            pat.Weight
            |> Option.map (fun w -> $"gewicht %s{w |> toStr Units.Mass.kiloGram } kg")
            |> Option.defaultValue ""

            pat.Height
            |> Option.map (fun h -> $"lengte {h |> toStr Units.Height.centiMeter} cm")
            |> Option.defaultValue ""

            pat
            |> calcBSA
            |> Option.map (fun bsa ->
                $"BSA {bsa |> ValueUnit.toStringDutchShort}"
            )
            |> Option.defaultValue ""
        ]
        |> List.filter String.notEmpty
        |> List.filter (String.isNullOrWhiteSpace >> not)
        |> String.concat ", "



module DoseType =


    open Informedica.Utils.Lib.BCL


    /// Get a sort order for a dose type.
    let sortBy = function
        | Start -> 0
        | Once -> 1
        | PRN -> 2
        | Maintenance -> 3
        | Continuous -> 4
        | StepUp n -> 50 + n
        | StepDown n -> 100 + n
        | AnyDoseType -> 200
        | Contraindicated -> -1


    /// Get a dose type from a string.
    let fromString s =
        let s = s |> String.toLower |> String.trim

        match s with
        | "start" -> Start
        | "eenmalig" -> Once
        | "prn" -> PRN
        | "onderhoud" -> Maintenance
        | "continu" -> Continuous
        | "contra" -> Contraindicated

        | _ when s |> String.startsWith "afbouw" ->
            match s |> String.split(" ") with
            | [_;i] ->
                match i |> Int32.tryParse with
                | Some i -> StepDown i
                | None ->
                    printfn $"DoseType.fromString couldn't match {s}"
                    AnyDoseType
            | _ ->
                printfn $"DoseType.fromString couldn't match {s}"
                AnyDoseType

        | _ when s |> String.startsWith "opbouw" ->
            match s |> String.split(" ") with
            | [_;i] ->
                match i |> Int32.tryParse with
                | Some i -> StepUp i
                | None ->
                    printfn $"DoseType.fromString couldn't match {s}"
                    AnyDoseType
            | _ ->
                printfn $"DoseType.fromString couldn't match {s}"
                AnyDoseType

        | _ when s |> String.isNullOrWhiteSpace -> AnyDoseType

        | _ ->
            printfn $"DoseType.fromString couldn't match {s}"
            AnyDoseType


    /// Get a string representation of a dose type.
    let toString = function
        | Start -> "start"
        | Once -> "eenmalig"
        | PRN -> "prn"
        | Maintenance -> "onderhoud"
        | Continuous -> "continu"
        | StepDown i -> $"afbouw {i}"
        | StepUp i -> $"opbouw {i}"
        | Contraindicated -> "contra"
        | AnyDoseType -> ""



module Product =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL


    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module ATCGroup = Informedica.ZIndex.Lib.ATCGroup


    module Location =


        /// Get a string representation of the VenousAccess.
        let toString = function
            | PVL -> "PVL"
            | CVL -> "CVL"
            | AnyAccess -> ""


        /// Get a VenousAccess from a string.
        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens "PVL" -> PVL
            | _ when s |> String.equalsCapInsens "CVL" -> CVL
            | _ -> AnyAccess



    module ShapeRoute =

        open Informedica.GenUnits.Lib


        let private get_ () =
            Web.getDataFromSheet Web.dataUrlIdGenPres "ShapeRoute"
            |> fun data ->

                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r
                    {
                        Shape = get "Shape"
                        Route = get "Route"
                        Unit =
                            get "Unit"
                            |> Units.fromString
                            |> Option.defaultValue NoUnit
                        DoseUnit =
                            get "DoseUnit"
                            |> Units.fromString
                            |> Option.defaultValue NoUnit
                        Timed = get "Timed" = "TRUE"
                        Reconstitute = get "Reconstitute" = "TRUE"
                        IsSolution = get "IsSolution" = "TRUE"
                    }
                )


        /// <summary>
        /// Get the ShapeRoute array.
        /// </summary>
        /// <remarks>
        /// This function is memoized.
        /// </remarks>
        let get : unit -> ShapeRoute [] =
            Memoization.memoize get_


        /// <summary>
        /// Check if the given shape is a solution using
        /// a ShapeRoute array.
        /// </summary>
        /// <param name="srs">The ShapeRoute array</param>
        /// <param name="shape">The Shape</param>
        let isSolution (srs : ShapeRoute []) shape  =
            srs
            |> Array.tryFind (fun sr ->
                sr.Shape |> String.equalsCapInsens shape
            )
            |> Option.map (fun sr -> sr.IsSolution)
            |> Option.defaultValue false



    module Reconstitution =

        open Utils

        // GPK
        // Route
        // DoseType
        // Dep
        // CVL
        // PVL
        // DiluentVol
        // ExpansionVol
        // Diluents
        let private get_ () =
            Web.getDataFromSheet Web.dataUrlIdGenPres "Reconstitution"
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
                        GPK = get "GPK"
                        Route = get "Route"
                        Location =
                            match get "CVL", get "PVL" with
                            | s1, _ when s1 |> String.isNullOrWhiteSpace |> not -> CVL
                            | _, s2 when s2 |> String.isNullOrWhiteSpace |> not -> PVL
                            | _ -> AnyAccess
                        DoseType = get "DoseType" |> DoseType.fromString
                        Dep = get "Dep"
                        DiluentVol = get "DiluentVol" |> toBrOpt
                        ExpansionVol = get "ExpansionVol" |> toBrOpt
                        Diluents = get "Diluents"
                    |}
                )


        /// Get the Reconstitution array.
        /// Returns an anonymous record with the following fields:
        /// unit -> {| Dep: string; DiluentVol: BigRational option; Diluents: string; DoseType: DoseType; ExpansionVol: BigRational option; GPK: string; Location: VenousAccess; Route: string |} array
        let get = Memoization.memoize get_


        /// <summary>
        /// Filter the Reconstitution array to get all the reconstitution rules
        /// that match the given filter.
        /// </summary>
        /// <param name="filter">The Filter</param>
        /// <param name="rs">The array of reconstitution rules</param>
        let filter (filter : Filter) (rs : Reconstitution []) =
            let eqs a b =
                a
                |> Option.map (fun x -> x = b)
                |> Option.defaultValue true

            [|
                fun (r : Reconstitution) -> r.Route |> eqs filter.Route
                fun (r : Reconstitution) ->
                    if filter.Patient.VenousAccess = [AnyAccess] ||
                       filter.Patient.VenousAccess |> List.isEmpty then true
                    else
                        match filter.DoseType with
                        | AnyDoseType -> true
                        | _ -> filter.DoseType = r.DoseType
                fun (r : Reconstitution) -> r.Department |> eqs filter.Patient.Department
                fun (r : Reconstitution) ->
                    match r.Location, filter.Patient.VenousAccess with
                    | AnyAccess, _
                    | _, []
                    | _, [ AnyAccess ] -> true
                    | _ ->
                        filter.Patient.VenousAccess
                        |> List.exists ((=) r.Location)
            |]
            |> Array.fold (fun (acc : Reconstitution[]) pred ->
                acc |> Array.filter pred
            ) rs



    module Parenteral =

        open Informedica.GenUnits.Lib


        let private get_ () =
            Web.getDataFromSheet Web.dataUrlIdGenPres "ParentMeds"
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
                        Substances =
                            [|
                                "volume mL", get "volume mL" |> toBrOpt
                                "energie kCal", get "energie kCal" |> toBrOpt
                                "eiwit g", get "eiwit g" |> toBrOpt
                                "KH g", get "KH g" |> toBrOpt
                                "vet g", get "vet g" |> toBrOpt
                                "Na mmol", get "Na mmol" |> toBrOpt
                                "K mmol", get "K mmol" |> toBrOpt
                                "Ca mmol", get "Ca mmol" |> toBrOpt
                                "P mmol", get "P mmol" |> toBrOpt
                                "Mg mmol", get "Mg mmol" |> toBrOpt
                                "Fe mmol", get "Fe mmol" |> toBrOpt
                                "VitD IE", get "VitD IE" |> toBrOpt
                                "Cl mmol", get "Cl mmol" |> toBrOpt

                            |]
                        Oplosmiddel = get "volume mL"
                        Verdunner = get "volume mL"
                    |}
                )
                |> Array.map (fun r ->
                    {
                        GPK =  r.Name
                        ATC = ""
                        MainGroup = ""
                        SubGroup = ""
                        Generic = r.Name
                        TallMan = "" //r.TallMan
                        Synonyms = [||]
                        Product = r.Name
                        Label = r.Name
                        Shape = ""
                        Routes = [||]
                        ShapeQuantities =
                            1N
                            |> ValueUnit.singleWithUnit
                                   Units.Volume.milliLiter
                        ShapeUnit =
                            Units.Volume.milliLiter
                        RequiresReconstitution = false
                        Reconstitution = [||]
                        Divisible =
                            10N
                            |> ValueUnit.singleWithUnit
                                   Units.Count.times
                        Substances =
                            r.Substances
                            |> Array.map (fun (s, q) ->
                                let n, u =
                                    match s |> String.split " " with
                                    | [n; u] -> n |> String.trim, u |> String.trim
                                    | _ -> failwith $"cannot parse substance {s}"
                                {
                                    Name = n
                                    Quantity =
                                        q
                                        |> Option.bind (fun q ->
                                            u
                                            |> Units.fromString
                                            |> function
                                                | None -> None
                                                | Some u ->
                                                    q
                                                    |> ValueUnit.singleWithUnit u
                                                    |> Some
                                        )
                                    MultipleQuantity = None
                                }
                            )
                    }
                )


        /// Get the Parenterals as a Product array.
        let get : unit -> Product [] =
            Memoization.memoize get_



    open Informedica.GenUnits.Lib


    let private get_ () =
        // check if the shape is a solution
        let isSol = ShapeRoute.isSolution (ShapeRoute.get ())

        let rename (subst : Informedica.ZIndex.Lib.Types.ProductSubstance) defN =
            if subst.SubstanceName |> String.startsWithCapsInsens "AMFOTERICINE B" ||
               subst.SubstanceName |> String.startsWithCapsInsens "COFFEINE" then
                subst.GenericName
                |> String.replace "0-WATER" "BASE"
            else defN
            |> String.toLower

        fun () ->
            // first get the products from the GenPres Formulary, i.e.
            // the assortment
            Web.getDataFromSheet Web.dataUrlIdGenPres "Formulary"
            |> fun data ->
                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                let formulary =
                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let get = getColumn r

                        {|
                            GPKODE = get "GPKODE" |> Int32.parse
                            Apotheek = get "UMCU"
                            ICC = get "ICC"
                            NEO = get "NEO"
                            ICK = get "ICK"
                            HCK = get "HCK"
                            tallMan = get "TallMan"
                        |}
                    )

                formulary
                // find the matching GenPresProducts
                |> Array.collect (fun r ->
                    r.GPKODE
                    |> GenPresProduct.findByGPK
                )
                // collect the GenericProducts
                |> Array.collect (fun gpp ->
                    gpp.GenericProducts
                    |> Array.map (fun gp -> gpp, gp)
                )
                // create the Product records
                |> Array.map (fun (gpp, gp) ->
                    let atc =
                        gp.ATC
                        |> ATCGroup.findByATC5
                    let su =
                        gp.Substances[0].ShapeUnit
                        |> String.toLower
                        |> Units.fromString
                        |> Option.defaultValue NoUnit

                    {
                        GPK =  $"{gp.Id}"
                        ATC = gp.ATC |> String.trim
                        MainGroup =
                            atc
                            |> Array.map (fun g -> g.AnatomicalGroup)
                            |> Array.tryHead
                            |> Option.defaultValue ""
                        SubGroup =
                            atc
                            |> Array.map (fun g -> g.TherapeuticSubGroup)
                            |> Array.tryHead
                            |> Option.defaultValue ""
                        Generic =
                            rename gp.Substances[0] gpp.Name
                        TallMan =
                            match formulary |> Array.tryFind(fun f -> f.GPKODE = gp.Id) with
                            | Some p when p.tallMan |> String.notEmpty -> p.tallMan
                            | _ -> ""
                        Synonyms =
                            gpp.GenericProducts
                            |> Array.collect (fun gp ->
                                gp.PrescriptionProducts
                                |> Array.collect (fun pp ->
                                    pp.TradeProducts
                                    |> Array.map (fun tp -> tp.Brand)
                                )
                            )
                            |> Array.distinct
                            |> Array.filter String.notEmpty
                        Product =
                            gp.PrescriptionProducts
                            |> Array.collect (fun pp ->
                                pp.TradeProducts
                                |> Array.map (fun tp -> tp.Label)
                            )
                            |> Array.distinct
                            |> function
                            | [| p |] -> p
                            | _ -> ""
                        Label = gp.Label
                        Shape = gp.Shape |> String.toLower
                        Routes = gp.Route |> Array.choose Mapping.mapRoute
                        ShapeQuantities =
                            gpp.GenericProducts
                            |> Array.collect (fun gp ->
                                gp.PrescriptionProducts
                                |> Array.map (fun pp -> pp.Quantity)
                                |> Array.choose BigRational.fromFloat

                            )
                            |> Array.filter (fun br -> br > 0N)
                            |> Array.distinct
                            |> fun xs ->
                                if xs |> Array.isEmpty then [| 1N |] else xs
                                |> ValueUnit.withUnit su
                        ShapeUnit = su
                        RequiresReconstitution =
                            Mapping.requiresReconstitution (gp.Route, su, gp.Shape)
                        Reconstitution =
                            Reconstitution.get ()
                            |> Array.filter (fun r ->
                                r.GPK = $"{gp.Id}" &&
                                r.DiluentVol |> Option.isSome
                            )
                            |> Array.map (fun r ->
                                {
                                    Route = r.Route
                                    DoseType = r.DoseType
                                    Department = r.Dep
                                    Location = r.Location
                                    DiluentVolume =
                                        r.DiluentVol.Value
                                        |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                                    ExpansionVolume =
                                        r.ExpansionVol
                                        |> Option.map (fun v ->
                                            v
                                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                                        )
                                    Diluents =
                                        r.Diluents
                                        |> String.splitAt ';'
                                        |> Array.map String.trim
                                }
                            )
                        Divisible =
                            // TODO: need to map this to a config setting
                            if gp.Shape |> String.containsCapsInsens "druppel" then 20N
                            else
                                if isSol gp.Shape then 10N
                                    else 1N
                            |> ValueUnit.singleWithUnit Units.Count.times
                        Substances =
                            gp.Substances
                            |> Array.map (fun s ->
                                let su =
                                    s.SubstanceUnit
                                    |> Units.fromString
                                    |> Option.defaultValue NoUnit
                                {
                                    Name = rename s s.SubstanceName
                                    Quantity =
                                        s.SubstanceQuantity
                                        |> BigRational.fromFloat
                                        |> Option.map (fun q ->
                                            q
                                            |> ValueUnit.singleWithUnit su
                                        )
                                    MultipleQuantity = None
                                }
                            )
                    }
                )
        |> StopWatch.clockFunc "created products"


    /// <summary>
    /// Get the Product array.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : unit -> Product [] =
        Memoization.memoize get_


    /// <summary>
    /// Reconstitute the given product according to
    /// route, DoseType, department and VenousAccess location.
    /// </summary>
    /// <param name="rte">The route</param>
    /// <param name="dtp">The dose type</param>
    /// <param name="dep">The department</param>
    /// <param name="loc">The venous access location</param>
    /// <param name="prod">The product</param>
    /// <returns>
    /// The reconstituted product or None if the product
    /// does not require reconstitution.
    /// </returns>
    let reconstitute rte dtp dep loc (prod : Product) =
        if prod.RequiresReconstitution |> not then None
        else
            prod.Reconstitution
            |> Array.filter (fun r ->
                (rte |> String.isNullOrWhiteSpace || r.Route |> String.equalsCapInsens rte) &&
                (r.DoseType = AnyDoseType || r.DoseType = dtp) &&
                (dep |> String.isNullOrWhiteSpace || r.Department |> String.equalsCapInsens dep) &&
                (r.Location = AnyAccess || r.Location = loc)
            )
            |> Array.map (fun r ->
                { prod with
                    ShapeUnit =
                        Units.Volume.milliLiter
                    ShapeQuantities = r.DiluentVolume
                    Substances =
                        prod.Substances
                        |> Array.map (fun s ->
                            { s with
                                Quantity =
                                    s.Quantity
                                    |> Option.map (fun q -> q / r.DiluentVolume)
                            }
                        )
                }
            )
            |> function
            | [| p |] -> Some p
            | _       -> None


    /// <summary>
    /// Filter the Product array to get all the products
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="prods">The array of Products</param>
    let filter (filter : Filter) (prods : Product []) =
        let repl s =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""

        let eqs s1 s2 =
            match s1, s2 with
            | Some s1, s2 ->
                let s1 = s1 |> repl
                let s2 = s2 |> repl
                s1 |> String.equalsCapInsens s2
            | _ -> false

        prods
        |> Array.filter (fun p ->
            p.Generic |> eqs filter.Generic &&
            p.Shape |> eqs filter.Shape &&
            p.Routes |> Array.exists (eqs filter.Route)
        )
        |> Array.map (fun p ->
            { p with
                Reconstitution =
                    p.Reconstitution
                    |> Reconstitution.filter filter
            }
        )


    /// Get all Generics from the given Product array.
    let generics (products : Product array) =
        products
        |> Array.map (fun p ->
            p.Generic
        )
        |> Array.distinct


    /// Get all Synonyms from the given Product array.
    let synonyms (products : Product array) =
        products
        |> Array.collect (fun p ->
            p.Synonyms
        )
        |> Array.append (generics products)
        |> Array.distinct


    /// Get all Shapes from the given Product array.
    let shapes  (products : Product array) =
        products
        |> Array.map (fun p -> p.Shape)
        |> Array.distinct



module Filter =


    /// An empty Filter.
    let filter =
        {
            Indication = None
            Generic = None
            Shape = None
            Route = None
            DoseType = AnyDoseType
            Patient = Patient.patient
        }


    /// <summary>
    /// Apply a Patient to a Filter.
    /// </summary>
    /// <param name="pat">The Patient</param>
    /// <param name="filter">The Filter</param>
    /// <returns>The Filter with the Patient applied</returns>
    let setPatient (pat : Patient) (filter : Filter) =
        let pat =
            pat
            |> Patient.calcPMAge

        { filter with
            Patient = pat
        }


    let calcPMAge (filter : Filter) =
        { filter with
            Patient =
                filter.Patient
                |> Patient.calcPMAge
        }



module DoseRule =

    open System
    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges

    module DoseLimit =


        /// An empty DoseLimit.
        let limit =
            {
                DoseLimitTarget = NoDoseLimitTarget
                Quantity = MinMax.empty
                NormQuantityAdjust = None
                QuantityAdjust = MinMax.empty
                PerTime = MinMax.empty
                NormPerTimeAdjust = None
                PerTimeAdjust = MinMax.empty
                Rate = MinMax.empty
                RateAdjust = MinMax.empty
            }


        /// <summary>
        /// Check whether an adjust is used in
        /// the DoseLimit.
        /// </summary>
        /// <remarks>
        /// If any of the adjust values is not None
        /// then an adjust is used.
        /// </remarks>
        let useAdjust (dl : DoseLimit) =
            [
                dl.NormQuantityAdjust = None
                dl.QuantityAdjust = MinMax.empty
                dl.NormPerTimeAdjust = None
                dl.PerTimeAdjust = MinMax.empty
                dl.RateAdjust = MinMax.empty
            ]
            |> List.forall id
            |> not


        /// Get the DoseLimitTarget as a string.
        let doseLimitTargetToString = function
            | NoDoseLimitTarget -> ""
            | ShapeDoseLimitTarget s
            | SubstanceDoseLimitTarget s -> s


        /// Get the substance from the SubstanceDoseLimitTarget.
        let substanceDoseLimitTargetToString = function
            | SubstanceDoseLimitTarget s -> s
            | _ -> ""


        /// Check whether the DoseLimitTarget is a SubstanceDoseLimitTarget.
        let isSubstanceLimit (dl : DoseLimit) =
            dl.DoseLimitTarget
            |> function
            | SubstanceDoseLimitTarget _ -> true
            | _ -> false


        /// Check whether the DoseLimitTarget is a SubstanceDoseLimitTarget.
        let isShapeLimit (dl : DoseLimit) =
            dl.DoseLimitTarget
            |> function
            | ShapeDoseLimitTarget _ -> true
            | _ -> false



    module Print =


        open Informedica.GenUnits.Lib

        let printFreqs (r : DoseRule) =
                let frs =
                    r.Frequencies
                    |> Option.map (fun vu ->
                        vu
                        |> ValueUnit.getValue
                        |> Array.map BigRational.ToInt32
                        |> Array.map string
                        |> String.concat ", "
                    )
                    |> Option.defaultValue ""

                if frs |> String.isNullOrWhiteSpace then ""
                else
                    let tu =
                        r.Frequencies
                        |> Option.map ValueUnit.getUnit
                        |> Option.map Units.toStringDutchShort
                        |> Option.defaultValue ""

                    if tu |> String.isNullOrWhiteSpace then $"{frs} x"
                    else
                        $"{frs} {tu}"


        let printInterval (dr: DoseRule) =
            if dr.IntervalTime = MinMax.empty then ""
            else
                dr.IntervalTime
                |> MinMax.toString
                    "min. interval "
                    "min. interval "
                    "max. interval "
                    "max. interval "


        let printTime (dr: DoseRule) =
            if dr.AdministrationTime = MinMax.empty then ""
            else
                dr.AdministrationTime
                |> MinMax.toString
                    "min. "
                    "min. "
                    "max. "
                    "max. "


        let printDuration (dr: DoseRule) =
            if dr.Duration = MinMax.empty then ""
            else
                dr.Duration
                |> MinMax.toString
                    "min. duur "
                    "min. duur "
                    "max. duur "
                    "max. duur "


        let printMinMaxDose (minMax : MinMax) =
            if minMax = MinMax.empty then ""
            else
                minMax
                |> MinMax.toString
                    "> "
                    "> "
                    "< "
                    "< "


        let printNormDose vu =
            match vu with
            | None    -> ""
            | Some vu -> $"{vu |> ValueUnit.toStringDutchShort}"


        let printDose wrap (dr : DoseRule) =
            let substDls =
                    dr.DoseLimits
                    |> Array.filter DoseLimit.isSubstanceLimit

            let shapeDls =
                dr.DoseLimits
                |> Array.filter DoseLimit.isShapeLimit

            let useSubstDl = substDls |> Array.length > 0
            // only use shape dose limits if there are no substance dose limits
            if useSubstDl then substDls
            else shapeDls
            |> Array.map (fun dl ->
                [
                    $"{dl.Rate |> printMinMaxDose}"
                    $"{dl.RateAdjust |> printMinMaxDose}"

                    $"{dl.NormPerTimeAdjust |> printNormDose} " +
                    $"{dl.PerTimeAdjust |> printMinMaxDose}"

                    $"{dl.PerTime |> printMinMaxDose}"

                    $"{dl.NormQuantityAdjust |> printNormDose} " +
                    $"{dl.QuantityAdjust |> printMinMaxDose}"

                    $"{dl.Quantity |> printMinMaxDose}"
                ]
                |> List.map String.trim
                |> List.filter (String.IsNullOrEmpty >> not)
                |> String.concat " "
                |> fun s ->
                    $"%s{dl.DoseLimitTarget |> DoseLimit.substanceDoseLimitTargetToString} {wrap}{s}{wrap}"
            )


        /// See for use of anonymous record in
        /// fold: https://github.com/dotnet/fsharp/issues/6699
        let toMarkdown (rules : DoseRule array) =
            let generic_md generic =
                $"\n# {generic}\n---\n"

            let route_md route products =
                $"\n### Route: {route}\n\n#### Producten\n%s{products}\n"

            let product_md product =  $"* {product}"

            let indication_md indication = $"\n## Indicatie: %s{indication}\n---\n"

            let doseCapt_md = "\n#### Doseringen\n"

            let dose_md dt dose freqs intv time dur =
                let dt = dt |> DoseType.toString
                let freqs =
                    if freqs |> String.isNullOrWhiteSpace then ""
                    else
                        $" in {freqs}"

                let s =
                    [
                        if intv |> String.isNullOrWhiteSpace |> not then
                            $" {intv}"
                        if time |> String.isNullOrWhiteSpace |> not then
                            $" inloop tijd {time}"
                        if dur |> String.isNullOrWhiteSpace |> not then
                            $" {dur}"
                    ]
                    |> String.concat ", "
                    |> fun s ->
                        if s |> String.isNullOrWhiteSpace then ""
                        else
                            $" ({s |> String.trim})"

                $"* *{dt}*: {dose}{freqs}{s}"

            let patient_md patient diagn =
                if diagn |> String.isNullOrWhiteSpace then
                    $"\n##### Patient: **%s{patient}**\n"
                else
                    $"\n##### Patient: **%s{patient}**\n\n%s{diagn}"

            let printDoses (rules : DoseRule array) =
                ("", rules |> Array.groupBy (fun d -> d.DoseType))
                ||> Array.fold (fun acc (dt, ds) ->
                    let dose =
                        if ds |> Array.isEmpty then ""
                        else
                            ds
                            |> Array.collect (printDose "")
                            |> Array.distinct
                            |> String.concat " "
                            |> fun s -> $"{s}\n"

                    let freqs =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printFreqs
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    let intv =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printInterval
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    let time =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printTime
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    let dur =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printDuration
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    if dt = Contraindicated then $"{acc}\n*gecontra-indiceerd*"
                    else
                        $"{acc}\n{dose_md dt dose freqs intv time dur}"
                )

            ({| md = ""; rules = [||] |},
             rules
             |> Array.groupBy (fun d -> d.Generic)
            )
            ||> Array.fold (fun acc (generic, rs) ->
                {| acc with
                    md = generic_md generic
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then r
                    else
                        (r, r.rules |> Array.groupBy (fun d -> d.Indication))
                        ||> Array.fold (fun acc (indication, rs) ->
                            {| acc with
                                md = acc.md + (indication_md indication)
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules = Array.empty then r
                                else
                                    (r, r.rules |> Array.groupBy (fun r -> r.Route))
                                    ||> Array.fold (fun acc (route, rs) ->

                                        let prods =
                                            rs
                                            |> Array.collect (fun d -> d.Products)
                                            |> Array.sortBy (fun p ->
                                                p.Substances
                                                |> Array.sumBy (fun s ->
                                                    s.Quantity
                                                    |> Option.map ValueUnit.getValue
                                                    |> Option.bind Array.tryHead
                                                    |> Option.defaultValue 0N
                                                )
                                            )
                                            |> Array.map (fun p -> product_md p.Label)
                                            |> Array.distinct
                                            |> String.concat "\n"
                                        {| acc with
                                            md = acc.md + (route_md route prods)
                                                        + doseCapt_md
                                            rules = rs
                                        |}
                                        |> fun r ->
                                            if r.rules = Array.empty then r
                                            else
                                                (r, r.rules
                                                    |> Array.sortBy (fun d -> d.PatientCategory |> PatientCategory.sortBy)
                                                    |> Array.groupBy (fun d -> d.PatientCategory))
                                                ||> Array.fold (fun acc (pat, rs) ->
                                                    let doses =
                                                        rs
                                                        |> Array.sortBy (fun r -> r.DoseType |> DoseType.sortBy)
                                                        |> printDoses
                                                    let diagn =
                                                        if pat.Diagnoses |> Array.isEmpty then ""
                                                        else
                                                            let s = pat.Diagnoses |> String.concat ", "
                                                            $"* Diagnose: **{s}**"
                                                    let pat = pat |> PatientCategory.toString

                                                    {| acc with
                                                        rules = rs
                                                        md = acc.md + (patient_md pat diagn) + $"\n{doses}"
                                                    |}
                                                )
                                    )
                        )
            )
            |> fun r -> r.md


        let printGenerics generics (doseRules : DoseRule[]) =
            doseRules
            |> generics
            |> Array.sort
            |> Array.map(fun g ->
                doseRules
                |> Array.filter (fun dr -> dr.Generic = g)
                |> toMarkdown
            )


    open Utils
    open Informedica.GenUnits.Lib


    /// <summary>
    /// Reconstitute the products in a DoseRule that require reconstitution.
    /// </summary>
    /// <param name="dep">The Department to select the reconstitution</param>
    /// <param name="loc">The VenousAccess location to select the reconstitution</param>
    /// <param name="dr">The DoseRule</param>
    let reconstitute dep loc (dr : DoseRule) =
        { dr with
            Products =
                if dr.Products
                   |> Array.exists (fun p -> p.RequiresReconstitution)
                   |> not then dr.Products
                else
                    dr.Products
                    |> Array.choose (Product.reconstitute dr.Route dr.DoseType dep loc)

        }


    let private get_ () =
        Web.getDataFromSheet Web.dataUrlIdGenPres "DoseRules"
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
                    Indication = get "Indication"
                    Generic = get "Generic"
                    Shape = get "Shape"
                    Route = get "Route"
                    Department = get "Dep"
                    Diagn = get "Diagn"
                    Gender = get "Gender" |> Gender.fromString
                    MinAge = get "MinAge" |> toBrOpt
                    MaxAge = get "MaxAge" |> toBrOpt
                    MinWeight = get "MinWeight" |> toBrOpt
                    MaxWeight = get "MaxWeight" |> toBrOpt
                    MinBSA = get "MinBSA" |> toBrOpt
                    MaxBSA = get "MaxBSA" |> toBrOpt
                    MinGestAge = get "MinGestAge" |> toBrOpt
                    MaxGestAge = get "MaxGestAge" |> toBrOpt
                    MinPMAge = get "MinPMAge" |> toBrOpt
                    MaxPMAge = get "MaxPMAge" |> toBrOpt
                    DoseType = get "DoseType" |> DoseType.fromString
                    Frequencies = get "Freqs" |> BigRational.toBrs
                    DoseUnit = get "DoseUnit"
                    AdjustUnit = get "AdjustUnit"
                    FreqUnit = get "FreqUnit"
                    RateUnit = get "RateUnit"
                    MinTime = get "MinTime" |> toBrOpt
                    MaxTime = get "MaxTime" |> toBrOpt
                    TimeUnit = get "TimeUnit"
                    MinInterval = get "MinInt" |> toBrOpt
                    MaxInterval = get "MaxInt" |> toBrOpt
                    IntervalUnit = get "IntUnit"
                    MinDur = get "MinDur" |> toBrOpt
                    MaxDur = get "MaxDur" |> toBrOpt
                    DurUnit = get "DurUnit"
                    Substance = get "Substance"
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    NormQtyAdj = get "NormQtyAdj" |> toBrOpt
                    MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                    MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                    MinPerTime = get "MinPerTime" |> toBrOpt
                    MaxPerTime = get "MaxPerTime" |> toBrOpt
                    NormPerTimeAdj = get "NormPerTimeAdj" |> toBrOpt
                    MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                    MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                    MinRate = get "MinRate" |> toBrOpt
                    MaxRate = get "MaxRate" |> toBrOpt
                    MinRateAdj = get "MinRateAdj" |> toBrOpt
                    MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                |}
            )
            |> Array.groupBy (fun r ->
                {
                    Indication = r.Indication
                    Generic = r.Generic
                    Shape = r.Shape
                    Route = r.Route
                    PatientCategory =
                        {
                            Department =
                                if r.Department |> String.isNullOrWhiteSpace then None
                                else
                                    r.Department |> Some
                            Diagnoses = [| r.Diagn |] |> Array.filter String.notEmpty
                            Gender = r.Gender
                            Age = (r.MinAge, r.MaxAge) |> MinMax.fromTuple "days"
                            Weight = (r.MinWeight, r.MaxWeight) |> MinMax.fromTuple "gram"
                            BSA = (r.MinBSA, r.MaxBSA) |> MinMax.fromTuple "m2"
                            GestAge = (r.MinGestAge, r.MaxGestAge) |> MinMax.fromTuple "days"
                            PMAge = (r.MinPMAge, r.MaxPMAge) |> MinMax.fromTuple "days"
                            Location = AnyAccess
                        }
                    DoseType = r.DoseType
                    Frequencies =
                        match r.FreqUnit |> Units.fromString with
                        | None -> None
                        | Some u ->
                            r.Frequencies
                            |> ValueUnit.withUnit u
                            |> Some
                    AdministrationTime = (r.MinTime, r.MaxTime) |> MinMax.fromTuple
                    IntervalTime = (r.MinInterval, r.MaxInterval) |> MinMax.fromTuple
                    Duration = (r.MinDur, r.MaxDur) |> MinMax.fromTuple
                    DoseLimits = [||]
                    Products = [||]
                }
            )
            |> Array.map (fun (dr, rs) ->
                { dr with
                    DoseLimits =
                        let shapeLimits =
                             Mapping.filterRouteShapeUnit dr.Route dr.Shape NoUnit
                             |> Array.map (fun rsu ->
                                { DoseLimit.limit with
                                    DoseLimitTarget = dr.Shape |> ShapeDoseLimitTarget
                                    DoseUnit = rsu.DoseUnit
                                    Quantity =
                                        let min = rsu.MinDoseQty |> Option.bind BigRational.fromFloat
                                        let max = rsu.MaxDoseQty |> Option.bind BigRational.fromFloat
                                        (min, max) |> MinMax.fromTuple
                                }
                             )
                             |> Array.distinct

                        rs
                        |> Array.map (fun r ->
                            {
                                DoseLimitTarget = r.Substance |> SubstanceDoseLimitTarget
                                Quantity = (r.MinQty, r.MaxQty) |> MinMax.fromTuple
                                NormQuantityAdjust = r.NormQtyAdj
                                QuantityAdjust = (r.MinQtyAdj, r.MaxQtyAdj) |> MinMax.fromTuple
                                PerTime = (r.MinPerTime, r.MaxPerTime) |> MinMax.fromTuple
                                NormPerTimeAdjust = r.NormPerTimeAdj
                                PerTimeAdjust = (r.MinPerTimeAdj, r.MaxPerTimeAdj) |> MinMax.fromTuple
                                Rate = (r.MinRate, r.MaxRate) |> MinMax.fromTuple
                                RateAdjust = (r.MinRateAdj, r.MaxRateAdj) |> MinMax.fromTuple
                            }
                        )
                        |> Array.append shapeLimits

                    Products =
                        Product.get ()
                        |> Product.filter
                         { Filter.filter with
                             Generic = dr.Generic |> Some
                             Shape = dr.Shape |> Some
                             Route = dr.Route |> Some
                         }
                }
            )

    (*

    /// <summary>
    /// Get the DoseRules from the Google Sheet.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : unit -> DoseRule [] =
        Memoization.memoize get_


    /// <summary>
    /// Filter the DoseRules according to the Filter.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="drs">The DoseRule array</param>
    let filter (filter : Filter) (drs : DoseRule array) =
        let eqs a b =
            a
            |> Option.map (fun x -> x = b)
            |> Option.defaultValue true

        [|
            fun (dr : DoseRule) -> dr.Indication |> eqs filter.Indication
            fun (dr : DoseRule) -> dr.Generic |> eqs filter.Generic
            fun (dr : DoseRule) -> dr.Shape |> eqs filter.Shape
            fun (dr : DoseRule) -> dr.Route |> eqs filter.Route
            fun (dr : DoseRule) -> dr.PatientCategory |> PatientCategory.filter filter
            fun (dr : DoseRule) ->
                match filter.DoseType, dr.DoseType with
                | AnyDoseType, _
                | _, AnyDoseType -> true
                | _ -> filter.DoseType = dr.DoseType
        |]
        |> Array.fold (fun (acc : DoseRule[]) pred ->
            acc |> Array.filter pred
        ) drs


    let private getMember getter (drs : DoseRule[]) =
        drs
        |> Array.map getter
        |> Array.map String.trim
        |> Array.distinctBy String.toLower
        |> Array.sortBy String.toLower



    /// Extract all indications from the DoseRules.
    let indications = getMember (fun dr -> dr.Indication)


    /// Extract all the generics from the DoseRules.
    let generics = getMember (fun dr -> dr.Generic)


    /// Extract all the shapes from the DoseRules.
    let shapes = getMember (fun dr -> dr.Shape)


    /// Extract all the routes from the DoseRules.
    let routes = getMember (fun dr -> dr.Route)


    /// Extract all the departments from the DoseRules.
    let departments = getMember (fun dr -> dr.PatientCategory.Department |> Option.defaultValue "")


    /// Extract all the diagnoses from the DoseRules.
    let diagnoses (drs : DoseRule []) =
        drs
        |> Array.collect (fun dr ->
            dr.PatientCategory.Diagnoses
        )
        |> Array.distinct
        |> Array.sortBy String.toLower


    /// Extract all genders from the DoseRules.
    let genders = getMember (fun dr -> dr.PatientCategory.Gender |> Gender.toString)


    /// Extract all patient categories from the DoseRules as strings.
    let patients (drs : DoseRule array) =
        drs
        |> Array.map (fun r -> r.PatientCategory)
        |> Array.sortBy PatientCategory.sortBy
        |> Array.map PatientCategory.toString
        |> Array.distinct


    /// Extract all frequencies from the DoseRules as strings.
    let frequencies (drs : DoseRule array) =
        drs
        |> Array.map Print.printFreqs
        |> Array.distinct


    let useAdjust (dr : DoseRule) =
        dr.DoseLimits
        |> Array.filter DoseLimit.isSubstanceLimit
        |> Array.exists DoseLimit.useAdjust
    *)


open MathNet.Numerics
open Informedica.GenUnits.Lib

module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
module Limit = Informedica.GenCore.Lib.Ranges.Limit

{ Patient.patient with
    GestAge = Some (ValueUnit.singleWithUnit Units.Time.week 36N)
    Age = Some (ValueUnit.singleWithUnit Units.Time.day 1N)
    Weight = Some (ValueUnit.singleWithUnit Units.Mass.gram (2500N))
}
|> Patient.toString

Product.Parenteral.get ()


DoseRule.Print.printInterval
    {
        Indication = ""
        Generic = "todo"
        Shape = "todo"
        Route = "todo"
        PatientCategory =
            {
                Department = None
                Diagnoses = [||]
                Gender = AnyGender
                Age = MinMax.empty
                Weight = MinMax.empty
                BSA = MinMax.empty
                GestAge = MinMax.empty
                PMAge = MinMax.empty
                Location = AnyAccess

            }
        DoseType = AnyDoseType
        Frequencies = None
        AdministrationTime = MinMax.empty
        IntervalTime =
            { MinMax.empty with
                Min =
                    1N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Informedica.GenCore.Lib.Ranges.Inclusive
                    |> Some
                Max =
                    2N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Informedica.GenCore.Lib.Ranges.Inclusive
                    |> Some
            }
        Duration = MinMax.empty
        DoseLimits = [||]
        Products = [||]

    }