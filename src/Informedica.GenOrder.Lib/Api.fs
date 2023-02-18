namespace Informedica.GenOrder.Lib



module Api =

    open System
    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib




    let getIndications = PrescriptionRule.get >> PrescriptionRule.indications

    let getGenerics = PrescriptionRule.get >> PrescriptionRule.generics

    let getRoutes = PrescriptionRule.get >> PrescriptionRule.routes

    let getShapes = PrescriptionRule.get >> PrescriptionRule.shapes

    let getDiagnoses = PrescriptionRule.get >> PrescriptionRule.diagnoses

    let getFrequencies =  PrescriptionRule.get >> PrescriptionRule.shapes


    let filterIndictions = PrescriptionRule.filter >> PrescriptionRule.indications 

    let filterGenerics = PrescriptionRule.filter >> PrescriptionRule.generics

    let filterRoutes = PrescriptionRule.filter >> PrescriptionRule.routes

    let filterShapes = PrescriptionRule.filter >> PrescriptionRule.shapes

    let filterDiagnoses = PrescriptionRule.filter >> PrescriptionRule.diagnoses

    let filterFrequencies =  PrescriptionRule.filter >> PrescriptionRule.shapes


    let tryHead m = (Array.map m) >> Array.tryHead >> (Option.defaultValue "")


    let createProductComponent noSubst freqUnit (doseLimits : DoseLimit []) (ps : Product []) =
        { DrugOrder.productComponent with
            Name =
                ps
                |> tryHead (fun p -> p.Shape)
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Shape =
                ps
                |> tryHead (fun p -> p.Shape)
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Quantities =
                ps
                |> Array.collect (fun p -> p.ShapeQuantities)
                |> Array.distinct
                |> Array.toList
            TimeUnit = freqUnit
            RateUnit = "uur" //doseRule.RateUnit
            Divisible =
                ps
                |> Array.choose (fun p -> p.Divisible)
                |> Array.tryHead
            Substances =
                if noSubst then []
                else
                    ps
                    |> Array.collect (fun p -> p.Substances)
                    |> Array.groupBy (fun s -> s.Name)
                    |> Array.map (fun (n, xs) ->
                        {
                            Name = n
                            Concentrations =
                                xs
                                |> Array.choose (fun s -> s.Quantity)
                                |> Array.distinct
                                |> Array.toList
                            Unit = xs |> tryHead (fun x -> x.Unit)
                            TimeUnit = freqUnit
                            Dose =
                                doseLimits
                                |> Array.tryFind (fun l -> l.Substance = n)
                            Solution = None
                        }
                    )
                    |> Array.toList
        }


    let setSolutionLimit (sls : SolutionLimit[]) (items : SubstanceItem list) =
        items
        |> List.map (fun item ->
            match sls |> Array.tryFind (fun sl -> sl.Substance |> String.equalsCapInsens item.Name) with
            | None -> item
            | Some sl ->
                { item with
                    Solution = Some sl
                }
        )


    let createDrugOrder (sr: SolutionRule option) (pr : PrescriptionRule) =
        let parenteral = Product.Parenteral.get ()
        let au =
            if pr.DoseRule.AdjustUnit |> String.isNullOrWhiteSpace then "kg"
            else pr.DoseRule.AdjustUnit

        let dose =
            pr.DoseRule.DoseLimits
            |> Array.filter (fun dl -> dl.Substance |> String.isNullOrWhiteSpace)
            |> function
            | [|dl|] -> dl |> Some
            | _ -> None

        let noSubst =
            dose
            |> Option.map (fun d -> d.DoseUnit = "keer")
            |> Option.defaultValue false
        //ToDo ??
        let prods =
            pr.DoseRule.Products
            |> createProductComponent noSubst pr.DoseRule.FreqUnit pr.DoseRule.DoseLimits
            |> List.singleton

        { DrugOrder.drugOrder with
            Id = "1" //Guid.NewGuid().ToString()
            Name = pr.DoseRule.Generic
            Products =
                pr.DoseRule.Products
                |> createProductComponent noSubst pr.DoseRule.FreqUnit pr.DoseRule.DoseLimits
                |> List.singleton
            Quantities = []
            Frequencies = pr.DoseRule.Frequencies |> Array.toList
            FreqUnit = pr.DoseRule.FreqUnit
            Unit =
                pr.DoseRule.Products
                |> tryHead (fun p -> p.ShapeUnit)
            Time = pr.DoseRule.Time
            TimeUnit = pr.DoseRule.TimeUnit
            RateUnit = "uur"
            Route = pr.DoseRule.Route
            DoseCount =
                if pr.SolutionRules |> Array.isEmpty then Some 1N
                else None
            OrderType =
                match pr.DoseRule.DoseType with
                | Informedica.GenForm.Lib.Types.Continuous -> ContinuousOrder
                | _ when pr.DoseRule.TimeUnit |> String.isNullOrWhiteSpace -> DiscontinuousOrder
                | _ -> TimedOrder
            Dose = dose
            Adjust =
                if au = "kg" then
                    pr.Patient.Weight
                    |> Option.map (fun v -> v / 1000N)
                else pr.Patient |> Patient.calcBSA
            AdjustUnit = au
        }
        |> fun dro ->
                match sr with
                | None -> dro
                | Some sr ->
                    { dro with
                        Quantities = sr.Volumes |> Array.toList
                        DoseCount = sr.DosePerc.Maximum
                        Products =
                            let ps =
                                dro.Products
                                |> List.map (fun p ->
                                    { p with
                                        Substances =
                                            p.Substances
                                            |> setSolutionLimit sr.SolutionLimits
                                    }
                                )

                            let s =
                                // ugly hack to get default solution
                                sr.Solutions
                                |> Array.tryHead
                                |> Option.defaultValue "x"

                            parenteral
                            |> Array.tryFind (fun p -> p.Generic |> String.startsWith s)
                            |> function
                            | Some p ->
                                [|p|]
                                |> createProductComponent true pr.DoseRule.FreqUnit [||]
                                |> List.singleton
                                |> List.append ps
                            | None ->
                                printfn $"couldn't find {s} in parenterals"
                                ps
                    }


    let evaluate logger (rule : PrescriptionRule) =
        let rec solve retry sr pr =
            pr
            |> createDrugOrder sr
            |> DrugOrder.toOrder
            |> Order.Dto.fromDto
            |> Order.solveMinMax false logger
            |> function
            | Ok ord ->
                let dto = ord |> Order.Dto.toDto

                let shps =
                    dto.Orderable.Components
                    |> List.choose (fun cDto -> cDto.ComponentQuantity.Variable.Vals)
                    |> List.toArray
                    |> Array.collect (fun dto -> dto.Value |> Array.map BigRational.fromDecimal)

                let sbsts =
                    dto.Orderable.Components
                    |> List.toArray
                    |> Array.collect (fun cDto ->
                        cDto.Items
                        |> List.toArray
                        |> Array.choose (fun iDto ->
                            iDto.ComponentConcentration.Variable.Vals
                            |> Option.map (fun v ->
                                iDto.Name,
                                v.Value
                                |> Array.map BigRational.fromDecimal
                                |> Array.tryHead
                            )
                        )
                    )
                    |> Array.distinct

                let pr =
                    pr
                    |> PrescriptionRule.filterProducts
                        shps
                        sbsts

                Ok (ord, pr)
            | Error (ord, m) when retry ->
                    if sr |> Option.isSome then Error(ord, pr, m)
                    else
                        let dose = ord.Orderable.Components[0].Items[0].Dose.Quantity
                        printfn $"trying a second time with manual product: {dose |> OrderVariable.Quantity.toString}"
                        { pr with
                            DoseRule =
                                { pr.DoseRule with
                                    Products = 
                                        pr.DoseRule.Products
                                        |> Array.map (fun p ->
                                            { p with
                                                Divisible = None
                                            }
                                        )
                                }
                        }
                        |> solve false None
            | Error (ord, m) -> Error (ord, pr, m)

        if rule.SolutionRules |> Array.isEmpty then [| solve true None rule |]
        else
            rule.SolutionRules
            |> Array.map (fun sr -> solve true (Some sr) rule)


    // print an order list
    let toScenarios ind sn (ords : Order[]) =
        ords
        |> Array.mapi (fun i o ->
            o
            |> Order.Print.printPrescription sn
            |> fun (pres, prep, adm) ->
                {
                    No = i
                    Indication = ind
                    Name = o.Orderable.Name |> Informedica.GenSolver.Lib.Variable.Name.toString
                    Shape = o.Orderable.Components[0].Shape
                    Route = o.Route
                    Prescription = pres
                    Preparation = prep
                    Administration = adm
                }
        )

        

