

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
            Department : string
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
            /// The Department to filter on
            Department : string option
            /// The list of Diagnoses to filter on
            Diagnoses : string []
            /// The Gender to filter on
            Gender : Gender
            /// The Age in days to filter on
            Age : ValueUnit option
            /// The Weight in grams to filter on
            Weight : ValueUnit option
            /// The Height in cm to filter on
            Height : ValueUnit option
            /// The Gestational Age in days to filter on
            GestAge : ValueUnit option
            /// The Post Menstrual Age in days to filter on
            PMAge : ValueUnit option
            /// The DoseType to filter on
            DoseType : DoseType
            /// The Venous Access Location to filter on
            Location : VenousAccess
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
                unt = xs.Unit
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

    (*
    *)


Mapping.mappingRouteShape[9]