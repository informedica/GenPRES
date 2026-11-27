/// The severity of a total against the bounds of its Totals row, the units a total shows in, the Dto of the
/// totals, and the orders that count in the totals by the Totals column of the FormRoute rows.
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


    let isValid =
        function
        | Some(Valid _) -> true
        | _ -> false


    let isWarning =
        function
        | Some(Warning _) -> true
        | _ -> false


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


let isWithinTests =
    testList
        "isWithin"
        [
            test "a range within both bounds" {
                total (Some 120N) (Some 150N)
                |> Totals.isWithin mlKgDay (Some 100N) (Some 160N)
                |> Expect.isTrue "within"
            }

            test "a range on its bounds is within" {
                total (Some 120N) (Some 150N)
                |> Totals.isWithin mlKgDay (Some 120N) (Some 150N)
                |> Expect.isTrue "within"
            }

            test "a range below the minimum" {
                total (Some 50N) (Some 80N)
                |> Totals.isWithin mlKgDay (Some 100N) None
                |> Expect.isFalse "below"
            }

            test "a range above the maximum" {
                total (Some 170N) (Some 180N)
                |> Totals.isWithin mlKgDay None (Some 160N)
                |> Expect.isFalse "above"
            }

            test "a range partly above the maximum" {
                total (Some 120N) (Some 160N)
                |> Totals.isWithin mlKgDay (Some 120N) (Some 150N)
                |> Expect.isFalse "partly"
            }

            test "a range partly below the minimum" {
                total (Some 110N) (Some 130N)
                |> Totals.isWithin mlKgDay (Some 120N) (Some 150N)
                |> Expect.isFalse "partly"
            }

            test "no bounds hold for any total" {
                total None None
                |> Totals.isWithin mlKgDay None None
                |> Expect.isTrue "no bounds"
            }

            test "a total without a maximum lies partly above a maximum" {
                total (Some 120N) None
                |> Totals.isWithin mlKgDay None (Some 150N)
                |> Expect.isFalse "open upper side"
            }

            test "a total without a minimum lies partly below a minimum" {
                total None (Some 130N)
                |> Totals.isWithin mlKgDay (Some 120N) None
                |> Expect.isFalse "open lower side"
            }

            test "a minimum only, a total above it" {
                total (Some 120N) None
                |> Totals.isWithin mlKgDay (Some 100N) None
                |> Expect.isTrue "within"
            }

            test "a maximum only, a total below it" {
                total None (Some 130N)
                |> Totals.isWithin mlKgDay None (Some 150N)
                |> Expect.isTrue "within"
            }

            test "a value set within the bounds" {
                valueSet [ 120N; 140N ]
                |> Totals.isWithin mlKgDay (Some 100N) (Some 150N)
                |> Expect.isTrue "within"
            }

            test "a value set with one value above the maximum" {
                valueSet [ 120N; 160N ]
                |> Totals.isWithin mlKgDay (Some 100N) (Some 150N)
                |> Expect.isFalse "partly"
            }

            // 1/10 mL/kg/min is 144 mL/kg/day
            test "a total per minute within bounds per day" {
                totalIn (mlKgPer Units.Time.minute) (Some(1N / 10N)) (Some(1N / 10N))
                |> Totals.isWithin mlKgDay (Some 120N) (Some 150N)
                |> Expect.isTrue "within"
            }

            test "a total per minute above bounds per day" {
                totalIn (mlKgPer Units.Time.minute) (Some(1N / 10N)) (Some(1N / 10N))
                |> Totals.isWithin mlKgDay (Some 100N) (Some 140N)
                |> Expect.isFalse "above"
            }
        ]


let getTotalsTests =
    testList
        "getTotals"
        [
            test "a total without a target range is a Valid block" {
                volume |> volumeOf |> isValid |> Expect.isTrue "valid"
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
                    |> Option.map Order.Print.unwrap
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
                |> Option.map Order.Print.unwrap
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
                        Volume = Some(Warning "142 ml/kg/dag (120 - 150)")
                        Energy = Some(Valid "80 kCal/kg/dag")
                    }

                t |> Totals.Dto.toDto |> Totals.Dto.fromDto |> Expect.equal "the same" (Ok t)
            }

            test "the totals round trip through the canonical JSON" {
                let t = { Totals.empty with Sodium = Some(Warning "5 mmol/kg/dag (max 4)") }

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
                                Kind = "loud"
                                Text = "1"
                            }
                }
                |> Totals.Dto.fromDto
                |> Expect.equal "unknown kind" (Error [ DtoError.UnknownTextKind "loud" ])
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

            test "a component with a product counts only for a row in the unit of the product" {
                let product: ComponentProduct =
                    {
                        Id = "p-1"
                        FormQuantities = ValueUnit.singleWithUnit Units.Volume.milliLiter 1N |> Some
                        Strengths = []
                        Reconstitution = None
                        Predilution = None
                    }

                // the components of the fixture have no products, so each gets a product in mL
                let ord = infusion.Value

                let ord =
                    { ord with
                        Orderable =
                            { ord.Orderable with
                                Components =
                                    ord.Orderable.Components
                                    |> List.map (fun c -> { c with Products = [ product ] })
                            }
                    }

                ord
                |> isTotals [| formRoute "intraveneus" "injectievloeistof" Units.Volume.milliLiter true |]
                |> Expect.isTrue "the same unit"

                ord
                |> isTotals [| formRoute "intraveneus" "injectievloeistof" Units.Mass.milliGram true |]
                |> Expect.isFalse "another unit"
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


[<Tests>]
let tests =
    testList "Totals severity and the orders that count" [ isWithinTests; getTotalsTests; dtoTests; isTotalsOrderTests ]
