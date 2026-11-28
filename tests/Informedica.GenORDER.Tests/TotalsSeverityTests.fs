/// The state and the severity of a total against the bounds of its Totals row, the units a total shows in, the
/// Dto of the totals, the orders that count in the totals by the Totals column of the FormRoute rows, and the
/// text of the norm after a total.
module TotalsSeverityTests

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module Fixtures =

    // the morphine continuous infusion solved to its bounds; for a patient of 32 kg its volume is
    // 0.525 to 2.1 mL/kg/day, which is 16.8 to 67.2 mL/day
    let infusion =
        lazy
            (Scenarios.morfCont
             |> Medication.toOrderDto Scenarios.testStart
             |> Order.Dto.fromDto
             |> Result.mapError string
             |> Result.bind (fun o ->
                 OrderProcessor.processPipeline OrderLogging.noOp (CalcMinMax o)
                 |> Result.mapError (fun (_, e) -> $"%A{e}")
             )
             |> function
                 | Ok o -> o
                 | Error e -> failtest $"fixture order could not be solved: %s{e}")


    let weight = ValueUnit.singleWithUnit Units.Weight.kiloGram 32N |> Some


    let routeMapping: Informedica.GenForm.Lib.Types.Data.RouteMapping[] =
        [|
            {
                Long = "INTRAVENEUS"
                Short = "iv"
            }
            {
                Long = "ORAAL"
                Short = "or"
            }
        |]


    // a FormRoute row as the sheet has it, with only the columns the totals read
    let formRoute route form unt totals : FormRoute =
        {
            Route = route
            Form = form
            Unit = unt
            DoseUnit = NoUnit
            MinDoseQty = None
            MaxDoseQty = None
            MinDoseQtyPerKg = None
            MaxDoseQtyPerKg = None
            Divisibility = None
            Timed = false
            Reconstitute = false
            IsSolution = false
            Totals = totals
        }


    // the morphine of the infusion is an injectievloeistof, its diluent an iv fluid
    let formRoutes = [| formRoute "intraveneus" "injectievloeistof" Units.Volume.milliLiter true |]


    let withRoute route = { infusion.Value with Route = route }


    // the infusion with the form for every component, each with a product in the unit u
    let withProductIn u form =
        let product: ComponentProduct =
            {
                Id = "p-1"
                FormQuantities = ValueUnit.singleWithUnit u 1N |> Some
                Strengths = []
                Reconstitution = None
                Predilution = None
            }

        let ord = infusion.Value

        { ord with
            Orderable =
                { ord.Orderable with
                    Components =
                        ord.Orderable.Components
                        |> List.map (fun c ->
                            { c with
                                Form = form
                                Products = [ product ]
                            }
                        )
                }
        }


    let volume: Informedica.GenForm.Lib.Types.Data.TotalsData =
        {
            Name = "volume"
            MinAge = None
            MaxAge = None
            MinWeight = None
            MaxWeight = None
            MinGestAge = None
            MaxGestAge = None
            Unit = Some Units.Volume.milliLiter
            Adj = None
            TimeUnit = Some Units.Time.day
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = None
            MaxPerTimeAdj = None
        }


    let volumeOf row =
        [| infusion.Value |]
        |> Totals.getTotals routeMapping formRoutes [| row |] None weight None
        |> _.Volume


    let adj min max =
        { volume with
            MinPerTimeAdj = min
            MaxPerTimeAdj = max
        }


    let perTime min max =
        { volume with
            MinPerTime = min
            MaxPerTime = max
        }


    let isValid (total: IntakeTotal option) =
        match total |> Option.map _.Text with
        | Some(Valid _) -> true
        | _ -> false


    let isWarning (total: IntakeTotal option) =
        match total |> Option.map _.Text with
        | Some(Warning _) -> true
        | _ -> false


    let stateOf row = row |> volumeOf |> Option.map _.State


    /// A total with a text block, within its norm for a Valid block and above it for any other block.
    let intakeTotal text : IntakeTotal =
        {
            Text = text
            State =
                match text with
                | Valid _ -> TotalState.WithinNorm
                | _ -> TotalState.AboveNorm
        }


    let mlKgPer tu =
        Units.Volume.milliLiter
        |> ValueUnit.per Units.Weight.kiloGram
        |> ValueUnit.per tu


    let mlKgDay = mlKgPer Units.Time.day


    // a total in the unit u from a minimum and a maximum, each None for an open side
    let totalIn u min max =
        let vr =
            match min, max with
            | Some mn, Some mx ->
                Variable.ValueRange.create
                    (Variable.ValueRange.Minimum.create true (ValueUnit.singleWithUnit u mn) |> Some)
                    None
                    (Variable.ValueRange.Maximum.create true (ValueUnit.singleWithUnit u mx) |> Some)
                    None
            | Some mn, None ->
                Variable.ValueRange.create
                    (Variable.ValueRange.Minimum.create true (ValueUnit.singleWithUnit u mn) |> Some)
                    None
                    None
                    None
            | None, Some mx ->
                Variable.ValueRange.create
                    None
                    None
                    (Variable.ValueRange.Maximum.create true (ValueUnit.singleWithUnit u mx) |> Some)
                    None
            | None, None -> Variable.ValueRange.create None None None None

        Name.create [ "total" ] |> Variable.empty |> Variable.setValueRange vr


    // a total in mL/kg/day
    let total = totalIn mlKgDay


    let valueSet (vs: BigRational list) =
        Name.create [ "total" ]
        |> Variable.empty
        |> Variable.setValueRange (
            vs
            |> List.toArray
            |> ValueUnit.withUnit mlKgDay
            |> Variable.ValueRange.ValueSet.create
            |> ValSet
        )


    // a Totals row for carbohydrate in mg/kg/min, without bounds
    let carbohydrate =
        { volume with
            Name = "koolhydraat"
            Unit = Some Units.Mass.milliGram
            TimeUnit = Some Units.Time.minute
        }


open Fixtures


let normStateTests =
    testList
        "normState"
        [
            test "a range within both bounds" {
                total (Some 120N) (Some 150N)
                |> Totals.normState mlKgDay (Some 100N) (Some 160N)
                |> Expect.equal "within" TotalState.WithinNorm
            }

            test "a range on its bounds is within" {
                total (Some 120N) (Some 150N)
                |> Totals.normState mlKgDay (Some 120N) (Some 150N)
                |> Expect.equal "within" TotalState.WithinNorm
            }

            test "a range below the minimum" {
                total (Some 50N) (Some 80N)
                |> Totals.normState mlKgDay (Some 100N) None
                |> Expect.equal "below" TotalState.BelowNorm
            }

            test "a range above the maximum" {
                total (Some 170N) (Some 180N)
                |> Totals.normState mlKgDay None (Some 160N)
                |> Expect.equal "above" TotalState.AboveNorm
            }

            test "a range partly above the maximum" {
                total (Some 120N) (Some 160N)
                |> Totals.normState mlKgDay (Some 120N) (Some 150N)
                |> Expect.equal "partly" TotalState.AboveNorm
            }

            test "a range partly below the minimum" {
                total (Some 110N) (Some 130N)
                |> Totals.normState mlKgDay (Some 120N) (Some 150N)
                |> Expect.equal "partly" TotalState.BelowNorm
            }

            test "a range below the minimum and above the maximum is outside" {
                total (Some 110N) (Some 160N)
                |> Totals.normState mlKgDay (Some 120N) (Some 150N)
                |> Expect.equal "outside" TotalState.OutsideNorm
            }

            test "no bounds give no norm" {
                total None None
                |> Totals.normState mlKgDay None None
                |> Expect.equal "no bounds" TotalState.NoNorm
            }

            test "a total without a maximum lies partly above a maximum" {
                total (Some 120N) None
                |> Totals.normState mlKgDay None (Some 150N)
                |> Expect.equal "open upper side" TotalState.AboveNorm
            }

            test "a total without a minimum lies partly below a minimum" {
                total None (Some 130N)
                |> Totals.normState mlKgDay (Some 120N) None
                |> Expect.equal "open lower side" TotalState.BelowNorm
            }

            test "a minimum only, a total above it" {
                total (Some 120N) None
                |> Totals.normState mlKgDay (Some 100N) None
                |> Expect.equal "within" TotalState.WithinNorm
            }

            test "a maximum only, a total below it" {
                total None (Some 130N)
                |> Totals.normState mlKgDay None (Some 150N)
                |> Expect.equal "within" TotalState.WithinNorm
            }

            test "a value set within the bounds" {
                valueSet [ 120N; 140N ]
                |> Totals.normState mlKgDay (Some 100N) (Some 150N)
                |> Expect.equal "within" TotalState.WithinNorm
            }

            test "a value set with one value above the maximum" {
                valueSet [ 120N; 160N ]
                |> Totals.normState mlKgDay (Some 100N) (Some 150N)
                |> Expect.equal "partly" TotalState.AboveNorm
            }

            // 1/10 mL/kg/min is 144 mL/kg/day
            test "a total per minute within bounds per day" {
                totalIn (mlKgPer Units.Time.minute) (Some(1N / 10N)) (Some(1N / 10N))
                |> Totals.normState mlKgDay (Some 120N) (Some 150N)
                |> Expect.equal "within" TotalState.WithinNorm
            }

            test "a total per minute above bounds per day" {
                totalIn (mlKgPer Units.Time.minute) (Some(1N / 10N)) (Some(1N / 10N))
                |> Totals.normState mlKgDay (Some 100N) (Some 140N)
                |> Expect.equal "above" TotalState.AboveNorm
            }
        ]


let combineStatesTests =
    testList
        "combineStates"
        [
            for s1, s2, exp in
                [
                    TotalState.NoNorm, TotalState.NoNorm, TotalState.NoNorm
                    TotalState.NoNorm, TotalState.BelowNorm, TotalState.BelowNorm
                    TotalState.WithinNorm, TotalState.NoNorm, TotalState.WithinNorm
                    TotalState.WithinNorm, TotalState.AboveNorm, TotalState.AboveNorm
                    TotalState.BelowNorm, TotalState.BelowNorm, TotalState.BelowNorm
                    TotalState.BelowNorm, TotalState.AboveNorm, TotalState.OutsideNorm
                    TotalState.AboveNorm, TotalState.BelowNorm, TotalState.OutsideNorm
                    TotalState.OutsideNorm, TotalState.WithinNorm, TotalState.OutsideNorm
                ] do
                test $"%A{s1} and %A{s2} is %A{exp}" {
                    Totals.combineStates s1 s2 |> Expect.equal "the combined state" exp
                }
        ]


let getTotalsTests =
    testList
        "getTotals"
        [
            test "a total without a target range is a Valid block" {
                volume |> volumeOf |> isValid |> Expect.isTrue "valid"
            }

            test "a total without a target range has no norm" {
                volume |> stateOf |> Expect.equal "no norm" (Some TotalState.NoNorm)
            }

            test "a total below its minimum per kg is below the norm" {
                adj (Some 3N) (Some 5N)
                |> stateOf
                |> Expect.equal "below" (Some TotalState.BelowNorm)
            }

            test "a total above its maximum per kg is above the norm" {
                adj None (Some(1N / 10N))
                |> stateOf
                |> Expect.equal "above" (Some TotalState.AboveNorm)
            }

            test "a total within its norm per kg is within the norm" {
                adj (Some(1N / 2N)) (Some 3N)
                |> stateOf
                |> Expect.equal "within" (Some TotalState.WithinNorm)
            }

            test "a total below its minimum per kg and above its maximum per time is outside the norm" {
                { adj (Some 3N) None with MaxPerTime = Some 50N }
                |> stateOf
                |> Expect.equal "outside" (Some TotalState.OutsideNorm)
            }

            test "a total within its target range per kg is a Valid block" {
                adj (Some(1N / 2N)) (Some 3N) |> volumeOf |> isValid |> Expect.isTrue "valid"
            }

            test "a total below its minimum per kg is a Warning block" {
                adj (Some 3N) (Some 5N) |> volumeOf |> isWarning |> Expect.isTrue "warning"
            }

            test "a total above its maximum per kg is a Warning block" {
                adj None (Some(1N / 10N)) |> volumeOf |> isWarning |> Expect.isTrue "warning"
            }

            test "a total partly above its maximum per kg is a Warning block" {
                adj (Some(1N / 2N)) (Some 1N)
                |> volumeOf
                |> isWarning
                |> Expect.isTrue "warning"
            }

            test "a minimum per kg only, a total above it, is a Valid block" {
                adj (Some(1N / 2N)) None |> volumeOf |> isValid |> Expect.isTrue "valid"
            }

            test "a maximum per kg only, a total below it, is a Valid block" {
                adj None (Some 3N) |> volumeOf |> isValid |> Expect.isTrue "valid"
            }

            test "a total within its target range per time is a Valid block" {
                perTime (Some 10N) (Some 100N) |> volumeOf |> isValid |> Expect.isTrue "valid"
            }

            test "a total partly above its maximum per time is a Warning block" {
                perTime None (Some 50N) |> volumeOf |> isWarning |> Expect.isTrue "warning"
            }

            test "a total partly below its minimum per time is a Warning block" {
                perTime (Some 20N) None |> volumeOf |> isWarning |> Expect.isTrue "warning"
            }

            test "a total within per kg and above per time is a Warning block" {
                { adj (Some(1N / 2N)) (Some 3N) with MaxPerTime = Some 50N }
                |> volumeOf
                |> isWarning
                |> Expect.isTrue "warning"
            }

            test "the text of a total is the total per kg with the bounds per kg of its row" {
                [
                    volume, "#0,525# - #2,1# mL||/kg||/dag||"
                    adj (Some 1N) (Some 3N), "#0,525# - #2,1# mL||/kg||/dag|| (1 - 3)"
                    adj None (Some 3N), "#0,525# - #2,1# mL||/kg||/dag|| (max 3)"
                    perTime None (Some 50N), "#0,525# - #2,1# mL||/kg||/dag||"
                ]
                |> List.iter (fun (row, text) ->
                    row
                    |> volumeOf
                    |> Option.map (_.Text >> Order.Print.unwrap)
                    |> Expect.equal "the text" (Some text)
                )
            }

            // the volume total then comes from the carbohydrate row, in mL/kg/min, while the bounds of the
            // volume row are in mL/kg/day
            test "a row per minute before the volume row: a total within its range per day is a Valid block" {
                [| infusion.Value |]
                |> Totals.getTotals
                    routeMapping
                    formRoutes
                    [| carbohydrate; adj (Some(1N / 2N)) (Some 3N) |]
                    None
                    weight
                    None
                |> _.Volume
                |> isValid
                |> Expect.isTrue "valid"
            }

            test "a row per minute before the volume row: a total partly below its range per day is a Warning block" {
                [| infusion.Value |]
                |> Totals.getTotals routeMapping formRoutes [| carbohydrate; adj (Some 1N) (Some 3N) |] None weight None
                |> _.Volume
                |> isWarning
                |> Expect.isTrue "warning"
            }

            test "a row per minute before the volume row: the volume shows per day, as with the volume row first" {
                let first = adj (Some(1N / 2N)) (Some 3N)

                [| infusion.Value |]
                |> Totals.getTotals routeMapping formRoutes [| carbohydrate; first |] None weight None
                |> _.Volume
                |> Expect.equal
                    "the same block"
                    ([| infusion.Value |]
                     |> Totals.getTotals routeMapping formRoutes [| first; carbohydrate |] None weight None
                     |> _.Volume)
            }

            test "without a volume row, no volume total" {
                [| infusion.Value |]
                |> Totals.getTotals routeMapping formRoutes [| carbohydrate |] None weight None
                |> _.Volume
                |> Expect.isNone "no volume row"
            }

            test "a volume row in L/day shows the volume in L/kg/day" {
                let liter = { (adj None (Some(3N / 1000N))) with Unit = Some Units.Volume.liter }

                [| infusion.Value |]
                |> Totals.getTotals routeMapping formRoutes [| liter |] None weight None
                |> _.Volume
                |> Option.map (_.Text >> Order.Print.unwrap)
                |> Option.map (String.contains "#0,000525# - #0,0021# l||/kg||/dag||")
                |> Expect.equal "in L/kg/day" (Some true)
            }

            test "no weight, no totals" {
                [| infusion.Value |]
                |> Totals.getTotals routeMapping formRoutes [| volume |] None None None
                |> Expect.equal "empty" Totals.empty
            }
        ]


let dtoTests =
    testList
        "Dto"
        [
            test "the totals round trip through the Dto with their severity" {
                let t =
                    { Totals.empty with
                        Volume = Some(intakeTotal (Warning "142 ml/kg/dag (120 - 150)"))
                        Energy = Some(intakeTotal (Valid "80 kCal/kg/dag"))
                    }

                t |> Totals.Dto.toDto |> Totals.Dto.fromDto |> Expect.equal "the same" (Ok t)
            }

            test "the totals round trip through the canonical JSON" {
                let t = { Totals.empty with Sodium = Some(intakeTotal (Warning "5 mmol/kg/dag (max 4)")) }

                t
                |> Totals.Dto.toDto
                |> Canonical.serialize
                |> Canonical.deserialize<Totals.Dto.Dto>
                |> Totals.Dto.fromDto
                |> Expect.equal "the same" (Ok t)
            }

            test "a null Dto is missing" {
                Unchecked.defaultof<Totals.Dto.Dto>
                |> Totals.Dto.fromDto
                |> Expect.equal "missing" (Error [ DtoError.Missing "Totals" ])
            }

            test "a block with an unknown kind is an error" {
                { (Totals.empty |> Totals.Dto.toDto) with
                    Fat =
                        Some
                            {
                                Text =
                                    {
                                        Kind = "loud"
                                        Text = "1"
                                    }
                                State = "withinNorm"
                            }
                }
                |> Totals.Dto.fromDto
                |> Expect.equal "unknown kind" (Error [ DtoError.UnknownTextKind "loud" ])
            }

            test "a total with an unknown state is an error" {
                { (Totals.empty |> Totals.Dto.toDto) with
                    Fat =
                        Some
                            {
                                Text =
                                    {
                                        Kind = "valid"
                                        Text = "1"
                                    }
                                State = "loud"
                            }
                }
                |> Totals.Dto.fromDto
                |> Expect.equal "unknown state" (Error [ DtoError.UnknownTotalState "loud" ])
            }

            test "every state round trips through the Dto" {
                for state in
                    [
                        TotalState.NoNorm
                        TotalState.WithinNorm
                        TotalState.BelowNorm
                        TotalState.AboveNorm
                        TotalState.OutsideNorm
                    ] do
                    let t =
                        { Totals.empty with
                            Fat =
                                Some
                                    {
                                        Text = Valid "1"
                                        State = state
                                    }
                        }

                    t |> Totals.Dto.toDto |> Totals.Dto.fromDto |> Expect.equal $"%A{state}" (Ok t)
            }
        ]


let isTotalsOrderTests =
    let isTotals formRoutes ord = ord |> Totals.isTotalsOrder routeMapping formRoutes

    testList
        "isTotalsOrder"
        [
            test "a form and route marked Totals count" {
                infusion.Value |> isTotals formRoutes |> Expect.isTrue "counts"
            }

            test "a form and route not marked Totals do not count" {
                infusion.Value
                |> isTotals [| formRoute "intraveneus" "injectievloeistof" Units.Volume.milliLiter false |]
                |> Expect.isFalse "does not count"
            }

            test "a form without a FormRoute row does not count" {
                infusion.Value |> isTotals [||] |> Expect.isFalse "no rows"
            }

            test "a form marked Totals on another route does not count" {
                infusion.Value
                |> isTotals [| formRoute "oraal" "injectievloeistof" Units.Volume.milliLiter true |]
                |> Expect.isFalse "another route"
            }

            test "a reconstituted product in mL fits a row in stuk with DoseUnit mL" {
                withProductIn Units.Volume.milliLiter "poeder voor oplossing voor infusie"
                |> isTotals
                    [|
                        { formRoute
                              "intraveneus"
                              "poeder voor oplossing voor infusie"
                              (Units.General.general "stuk")
                              true with
                            DoseUnit = Units.Volume.milliLiter
                        }
                    |]
                |> Expect.isTrue "counts"
            }

            test "drops in mL fit a row with DoseUnit druppel" {
                withProductIn Units.Volume.milliLiter "druppels voor oraal gebruik"
                |> isTotals
                    [|
                        { formRoute "intraveneus" "druppels voor oraal gebruik" Units.Volume.milliLiter true with
                            DoseUnit = Units.Volume.droplet
                        }
                    |]
                |> Expect.isTrue "counts"
            }

            test "a product in mL fits a row in mL without a DoseUnit" {
                withProductIn Units.Volume.milliLiter "injectievloeistof"
                |> isTotals formRoutes
                |> Expect.isTrue "counts"
            }

            test "a product in mL does not fit a row whose Unit and DoseUnit are a mass" {
                withProductIn Units.Volume.milliLiter "injectievloeistof"
                |> isTotals
                    [|
                        { formRoute "intraveneus" "injectievloeistof" Units.Mass.milliGram true with
                            DoseUnit = Units.Mass.milliGram
                        }
                    |]
                |> Expect.isFalse "another group"
            }

            test "a product in IU does not fit a row with DoseUnit mL" {
                withProductIn Units.InterNational.iu "injectie/infuus"
                |> isTotals
                    [|
                        { formRoute "intraveneus" "injectie/infuus" Units.Mass.milliGram true with
                            DoseUnit = Units.Volume.milliLiter
                        }
                    |]
                |> Expect.isFalse "another group"
            }

            test "a component without a product counts for a row in any unit" {
                infusion.Value
                |> isTotals [| formRoute "intraveneus" "injectievloeistof" Units.Mass.milliGram true |]
                |> Expect.isTrue "any unit"
            }

            test "the short route of the order matches the long route of the sheet" {
                withRoute "iv" |> isTotals formRoutes |> Expect.isTrue "counts"
            }

            test "an order without a route does not count" {
                withRoute "" |> isTotals formRoutes |> Expect.isFalse "no route"
            }

            test "the diluent alone marked Totals counts" {
                infusion.Value
                |> isTotals [| formRoute "intraveneus" "iv fluid" Units.Volume.milliLiter true |]
                |> Expect.isTrue "any component"
            }

            test "the getTotals of an order that does not count are empty" {
                [| infusion.Value |]
                |> Totals.getTotals routeMapping [||] [| volume |] None weight None
                |> Expect.equal "empty" Totals.empty
            }

            test "only the orders that count add to the totals" {
                let excluded = { infusion.Value with Route = "rectaal" }

                [| infusion.Value; excluded |]
                |> Totals.getTotals routeMapping formRoutes [| volume |] None weight None
                |> Expect.equal
                    "as the infusion alone"
                    ([| infusion.Value |]
                     |> Totals.getTotals routeMapping formRoutes [| volume |] None weight None)
            }
        ]


let normTextTests =
    testList
        "normText"
        [
            test "a norm with a fraction has a decimal comma" {
                Totals.normText (Some(1N / 10N)) (Some(3N / 10N))
                |> Expect.equal "should be (0,1 - 0,3)" (Some "(0,1 - 0,3)")
            }

            test "a norm with three significant digits is not rounded" {
                Totals.normText (Some(27N / 40N)) (Some(33N / 40N))
                |> Expect.equal "should be (0,675 - 0,825)" (Some "(0,675 - 0,825)")
            }

            test "a norm with more significant digits is rounded to three" {
                Totals.normText (Some(2N / 3N)) (Some(4N / 3N))
                |> Expect.equal "should be (0,667 - 1,33)" (Some "(0,667 - 1,33)")
            }

            test "a norm with whole numbers has no decimals" {
                Totals.normText (Some 80N) (Some 100N)
                |> Expect.equal "should be (80 - 100)" (Some "(80 - 100)")
            }

            test "a norm of a thousand or more has a narrow no-break space between the thousands" {
                Totals.normText (Some 1000N) (Some 1500N)
                |> Expect.equal "should be (1 000 - 1 500)" (Some "(1\u202F000 - 1\u202F500)")
            }

            test "a norm with only a maximum shows max" {
                Totals.normText None (Some(3N / 2N))
                |> Expect.equal "should be (max 1,5)" (Some "(max 1,5)")
            }

            test "a norm with only a minimum shows no norm" {
                Totals.normText (Some 2N) None |> Expect.isNone "should be None"
            }

            test "no norm shows no norm" { Totals.normText None None |> Expect.isNone "should be None" }
        ]


[<Tests>]
let tests =
    testList
        "Totals severity and the orders that count"
        [
            normStateTests
            combineStatesTests
            getTotalsTests
            dtoTests
            isTotalsOrderTests
            normTextTests
        ]
