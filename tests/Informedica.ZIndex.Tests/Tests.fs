namespace Informedica.ZIndex.Tests



module Tests =

    open Expecto 
    open Expecto.Flip
    

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.ZIndex.Lib



    module ZIndexBaseTableTests =

        let tests = testList "ZIndex base tables" [
            test "table BST711 has record with meronem" {
                Zindex.BST711T.records ()
                |> Array.map (fun r ->
                    Names.getName r.GPNMNR Names.Full
                )
                |> Array.filter (fun n ->
                    n |> String.toLower |> String.contains "meropenem"
                )
                |> Array.isEmpty
                |> Expect.isFalse "should not be empty" 
            }

            test "table BST has at least 562831 records" {
                Zindex.BST921T.records ()
                |> Array.length |> fun x -> x >= 562831
                |> Expect.isTrue "should be more than 562831"

            }

        ] 


    module NameTests =

        let tests = testList "Names" [
            test "has at least 94 routes" {
                Names.getItems Names.Route Names.Fifty
                |> Array.distinct
                |> Array.length |> fun x -> x >= 94
                |> Expect.isTrue "should be at least 94"
            }

            test "always has a short and a full name" {
                Names.getItems Names.ShapeUnit Names.TwentyFive
                |> Array.map snd
                |> Array.zip (Names.getItems Names.ShapeUnit Names.Fifty |> Array.map snd)
                |> Array.map (fun (l, s) ->
                    l |> String.trim |> String.toLower,
                    s |> String.trim |> String.toLower
                )
                |> Array.forall (fun (l, s) -> l |> String.notEmpty && s |> String.notEmpty)
                |> Expect.isTrue "should be true"

            }
        ]


    module SubstanceTests =

        let tests = testList "Substance" [

            test "substance nacl to mmol" {
                Substance.get ()
                |> Array.tryFind (fun s -> s.Name |> String.equalsCapInsens "natriumchloride") 
                |> function
                | None -> 0.
                | Some s -> 
                    1000. / (s.Mole |> float)
                |> Expect.floatClose "should be about 17 mmol" Accuracy.low 17.11
            }

            test "substance can have different names" {
                    // Different substance naming
                    Substance.get ()
                    |> Array.filter (fun s -> s.Id <> s.Pk)
                    |> Array.map (fun s ->
                        s.Name,
                        Substance.get ()
                        |> Array.filter (fun s2 -> s2.Id = s.Pk)
                        |> Array.map (fun s2 -> s2.Name)
                        |> Array.distinct
                        |> String.concat ", "
                    )
                    |> Array.distinct
                    |> Array.groupBy snd

                    |> Array.map (fun (k, v) ->
                        k,
                        v
                        |> Array.map fst
                        |> String.concat ","

                    )
                    |> Array.sortBy fst
                    |> Array.isEmpty
                    |> Expect.isFalse "should not be empty"
            }
        ]



    module ProductTests =

        let tests = testList "product tests" [
            
            test "should have at consumer product for tradeperoduct with id = 2956608" {
                ConsumerProduct.get(2956608)
                |> Array.length
                |> Expect.equal "shoudl be 1" 1
            }

            test "should have genpresproducts" {
                GenPresProduct.get true
                |> Array.length
                |> fun x -> Expect.isGreaterThan "have more than 100" (x, 100)
            }

            test "can take 200 genpresproducts" {
                // GenPres product
                GenPresProduct.get true
                |> Seq.sortBy (fun gpp -> gpp.Name, gpp.Shape, gpp.Routes)
                |> Seq.map (fun gpp -> $"""{gpp.Name}, {gpp.Shape}, {gpp.Routes |> String.concat "/"}""")
                |> Seq.take 200
                |> Seq.length
                |> Expect.equal "should be 200" 200
            }

            test "should have paracetmol products" {
                GenPresProduct.filter true "paracetamol" "" "oraal"
                |> Array.map GenPresProduct.toString
                |> Array.exists (String.containsCapsInsens "paracetamol")
                |> Expect.isTrue "should exist paracetamol"
            }

        ]



    module DoseRuleTests =

        let frqs =
            // Get all distinct frequencies
            DoseRule.frequencies ()
            |> Array.map (fun f ->
                f.Time
                |> String.replace "per " ""
                |> String.split " "
                |> function
                | [u] ->
                    {| Value = Some 1.; Unit = u; Time = f.Time |}
                | [v;u] ->
                    let v = 
                        match v |> Double.tryParse with
                        | Some v -> v |> Some
                        | None ->
                            printfn $"could not parse {v}"
                            None
                    {| Value = v; Unit = u; Time = f.Time |}
                | _ -> {| Value = None; Unit = ""; Time = f.Time|}
            )

        let rts =
            DoseRule.get()
            |> Array.collect (fun dr ->
                dr.Routes
            ) |> Array.sort |> Array.distinct

        let tests = testList "doserule tests" [

            test "should be > 1000 rules" {
                // Dose rules
                DoseRule.get ()
                |> Array.length
                |> fun l -> l > 1000
                |> Expect.isTrue "should have > 1000 rules"
            }

            test "can print 100 dose rules" {
                DoseRule.get ()
                |> Array.take 100
                |> Array.map DoseRule.toString2
                |> Array.length 
                |> Expect.equal "should be 100" 100
            }

            test "should have different dose units" {
                // Get all possible dose units
                DoseRule.get()
                |> Array.map (fun dr -> dr.Unit)
                |> Array.distinct
                |> Array.length |> fun x -> x >= 10
                |> Expect.isTrue "should be at least 10"
            }

            test "should have 3 gender possibilities" {
                // Get all possible patient gender
                DoseRule.get()
                |> Array.map (fun dr -> dr.Gender)
                |> Array.distinct
                |> Array.length
                |> Expect.equal "should be three" 3
            }

            test "should have doserules for pcm oral" {
                // Get all dose rules for the filter
                RuleFinder.createFilter None None None None "PARACETAMOL" "tablet" "ORAAL"
                |> RuleFinder.find true
                |> RuleFinder.convertToResult
                |> Option.isSome
                |> Expect.isTrue "should be true" 
            }

            test "should have indications at least 456" {
                // Get all distinct indications
                DoseRule.indications ()
                |> Array.length 
                |> fun x -> x >= 456
                |> Expect.isTrue "should be at least 456"
            }

            test "should have at least 42 routes" {
                // Get all distinct indications
                // Get all distinct routes
                DoseRule.routes ()
                |> Array.length 
                |> fun x -> x >= 42
                |> Expect.isTrue "should be at least 42"
            }

            test "should have >= 50 dose rule frequency times" {
                // Get all possible freq Time
                DoseRule.frequencies ()
                |> Array.map (fun f ->
                    f.Time
                    |> String.replace "per " ""
                )
                |> Array.distinct
                |> Array.length
                |> fun x -> x >= 50
                |> Expect.isTrue "should be at least 50"
            }

            test "should have at least 236 distinct frequencies" {
                frqs 
                |> Array.length
                |> Expect.equal "should be >= 236" 236                    
            }

            test "can pars freq time in number and unit" {
                frqs
                |> Array.filter (fun f -> f.Value.IsSome && f.Time |> String.notEmpty)
                |> Array.length |> fun v -> v >= 235
                |> Expect.isTrue "should be at least 235"
            }

            test "has time with value 'half'" {
                frqs
                |> Array.filter (fun f -> f.Value.IsNone && f.Time |> String.notEmpty)
                |> Array.tryHead
                |> function 
                | None -> ""
                | Some x -> x.Time
                |> Expect.equal "should be 'per half jaar'" "per half jaar"
            }

            test "has doserules with frequencies with more than 24 x day" {
                DoseRule.get()
                |> Array.filter (fun dr ->
                    dr.Freq.Time |> String.equalsCapInsens "per dag" &&
                    dr.Freq.Frequency > 24m
                )
                |> Array.isEmpty
                |> Expect.isFalse "should be false"
            }

            test "has frequencies 'eenmalig' with freq > 1" {
                DoseRule.get ()
                |> Array.filter (fun dr ->
                    dr.Freq.Time |> String.contains "eenmalig" &&
                    dr.Freq.Frequency > 1.0m
                )
                |> Array.map (DoseRule.toString ", ")
                |> Array.isEmpty 
                |> Expect.isFalse "should not empty"
            }

            test "should have NOT doserule result for pcm 1 yr 10kg (multiple shapes)" {
                // Get all dose rules for age 12 months weight 10 kg paracetamol rect
                RuleFinder.createFilter (Some 12m) (Some 10m) None None "paracetamol" "" ""
                |> RuleFinder.find true
                |> RuleFinder.convertToResult
                |> Expect.isNone "should NOT be there"
            }

            test "should have doserule result for pcm drank 1 yr 10kg" {
                // Get all dose rules for age 12 months weight 10 kg paracetamol rect
                RuleFinder.createFilter (Some 12m) (Some 10m) None None "paracetamol" "drank" ""
                |> RuleFinder.find true
                |> RuleFinder.convertToResult
                |> Expect.isSome "should be there"
            }

            test "should have doserules with freq = 'eenmalig'" {
                DoseRule.get ()
                |> Array.filter (fun dr ->
                    dr.Freq.Time = "eenmalig" && dr.Freq.Frequency > 1.0m
                )
                |> (Array.isEmpty >> not)
                |> Expect.isTrue "should not be empty"
            }

            test "has dose rules without trade or consumer products" {
                DoseRule.get ()
                |> Array.filter (fun dr ->
                    dr.PrescriptionProduct |> Array.length > 0 &&
                    dr.TradeProduct |> Array.length = 0
                )
                |> Array.isEmpty
                |> Expect.isFalse "should not be empty"
            }

            test "doserules for diclofenac are unique per route and patient category" {
                let rs =
                    RuleFinder.createFilter None None None None "diclofenac" "" ""
                    |> RuleFinder.find true
                    |> Array.map (fun dr ->
                        (dr.Routes |> Array.map RuleFinder.createRoute, dr)
                    )
                    |> Array.groupBy (fun (r, _) ->
                        r
                    )
                    |> Array.map (fun (r, drs) ->
                        let drs =
                            drs
                            |> Array.map snd
                            |> Array.groupBy (fun dr ->
                                (dr.Gender, dr.Age, dr.Weight, dr.BSA)
                            )
                        (r, drs)
                    )
                    |> Array.map (fun (r, drs) ->
                        let drs =
                            drs |> Array.map (fun (pat, drs) ->
                                (pat, drs |> Array.map (DoseRule.toString ", ") |> Array.distinct)
                            )
                        {|
                            route = r
                            pats =
                                drs
                                |> Array.map (fun (pat, drs) ->
                                    {|
                                        pat = 
                                            let gender, age, wght, bsa = pat
                                            {|
                                                gender = gender
                                                age = age
                                                wght = wght
                                                bsa = bsa
                                                doserules = drs
                                            |}
                                    |}
                                )
                        |}
                    )
                let totRs = 
                    rs
                    |> Array.collect (fun r ->
                        r.pats
                        |> Array.collect (fun p -> p.pat.doserules)
                    )

                totRs 
                |> Array.length 
                |> Expect.equal "should be unique" 
                    (totRs |> Array.distinct |> Array.length)
            }

            (* long running test
            test "there are > 100 generic products that have no doserule" {
                // look for each generic product if there are doserules
                GenPresProduct.get true
                |> Array.sortBy (fun gpp -> gpp.Name)
                |> Array.filter (fun gpp ->
                    gpp.GenericProducts
                    |> Array.sortBy (fun gp -> gp.Name)
                    |> Array.exists (fun gp ->
                        let zeroRules =
                            RuleFinder.createFilter None  None None (Some gp.Id) "" "" ""
                            |> RuleFinder.find true
                            |> Array.isEmpty

                        zeroRules
                    )
                )
                |> Array.length |> fun x -> x > 100
                |> Expect.isTrue "should be more than 100"
            }
            *)

            test "there are doserules for pcm intradermaal for testing" {
                // Get dose result routes for paracetamol
                // contains intradermal route??
                GenPresProduct.get true
                |> Array.filter (fun gpp ->
                    gpp.Name |> String.equalsCapInsens "paracetamol"
                )
                |> Array.collect (fun gpp ->
                    gpp.GenericProducts
                    |> Array.map (fun gp ->
                        RuleFinder.createFilter None None None (Some gp.Id) "" "" ""
                        |> RuleFinder.find true
                        |> RuleFinder.convertToResult
                    )
                )
                |> Array.filter (fun dro ->
                    match dro with
                    | Some dr ->
                        dr.Product.Routes
                        |> Array.exists (String.equalsCapInsens "intradermaal")
                    | None -> false
                )
                |> Array.distinct
                |> Array.isEmpty
                |> Expect.isFalse "should not be empty"
            }

            test "doserules with route 'parenteraal' exist" {
                // Look for parenteral dose rules (should not exist?)
                DoseRule.get ()
                |> Array.filter (fun dr ->
                    let parent =
                        dr.Routes
                        |> Array.exists (String.equalsCapInsens "parenteraal")
                    parent
                )
                |> Array.isEmpty
                |> Expect.isFalse "should not be empty"
            }

        ]



    module GenPresProductTests =

        let tot = GenPresProduct.get true |> Array.length


        let tests = testList "GenPresProduct" [

            test "filter with empty filter should same amount as all" {
                GenPresProduct.filter true "" "" ""
                |> Array.length
                |>Expect.equal "should be the same amount" tot

            }

            test "generic products can belong to only one GenPres product" {
                GenPresProduct.getGenericProducts ()
                |> Array.distinct 
                |> Array.length
                |> Expect.equal "should be unique"
                    (GenPresProduct.getGenericProducts () |> Array.length)

            }

            test "should have amoxicilline" {
                GenPresProduct.filter true "amoxicilline" "" "intraveneus"
                |> Array.isEmpty
                |> Expect.isFalse "should not be empty"
            }

            test "should be unique by name, shape and routes" {
                GenPresProduct.get true
                |> Array.map (fun gpp -> gpp.Name, gpp.Shape, gpp.Routes)
                |> Array.distinct 
                |> Array.length
                |> Expect.equal "should all be distinct" tot
            }

            test "should not have duplicate name shape" {
                GenPresProduct.get true
                |> Array.filter (fun gpp ->
                    let dbls =
                        GenPresProduct.get true
                        |> Array.filter (fun gpp_ -> gpp.Name = gpp_.Name && gpp.Shape = gpp_.Shape)
                    dbls |> Array.length > 1
                )
                |> Array.isEmpty
                |> Expect.isTrue "should be empty"
            }

            test "should have 27 substance generic units" {
                GenPresProduct.get true
                |> Array.collect (fun gpp ->
                    gpp.GenericProducts
                    |> Array.collect (fun gp ->
                        gp.Substances
                        |> Array.map (fun s -> s.GenericUnit)
                    )
                ) |> Array.distinct |> Array.sort
                |> Array.length |> fun x -> x
                |> Expect.equal "should be 27" 27
            }

            test "should not have routes not in doserule" {
                GenPresProduct.getRoutes()
                |> Array.filter (fun r ->
                    DoseRuleTests.rts
                    |> Array.exists ((=) r)
                    |> not
                )
                |> Array.isEmpty
                |> Expect.isTrue "should be empty"
            }

            test "should not have products without generic products" {
                GenPresProduct.get true
                |> Array.tryFind (fun gpp -> gpp.GenericProducts |> Array.isEmpty)
                |> Expect.isNone "should not be found"
            }

            test "GenPresProduct name should be always a substance(s)" {
                // check if each substance exists
                GenPresProduct.get true
                |> Array.forall (fun gpp ->
                    let ss =
                        gpp.GenericProducts
                        |> Array.collect (fun gp ->
                            gp.Substances
                            |> Array.map (fun s -> s.SubstanceName)
                        )

                    gpp.Name |> String.split "/"
                    |> List.forall (fun sn ->
                        ss
                        |> Array.exists (fun s -> s = sn)
                    )
                )
                |> Expect.isTrue "should be true"
            }

            test "can search for augmentin with at least 2 results" {
                GenPresProduct.search "augmentin"
                |> Array.length |> fun x -> x >= 2
                |> Expect.isTrue "should have at least 2"
            }

            test "at least 2232 products have bar codes" {
                // get barcodes
                GenPresProduct.get true
                |> Array.collect (fun gpp -> gpp.GenericProducts)
                |> Array.collect (fun gp ->
                //    gp.Label,
                    gp.PrescriptionProducts
                    |> Array.collect (fun pp ->
                        pp.TradeProducts
                        |> Array.collect (fun tp ->
                            tp.ConsumerProducts
                            |> Array.collect (fun cp ->
                                cp.BarCodes
                                |> Array.map (fun b -> (gp.Id, b))
                            )
                        )
                    )
                )
                |> Array.groupBy fst
                |> Array.map (fun (gpk, bc) ->
                    gpk, bc |> Array.map snd |> Array.filter String.notEmpty
                )
                |> Array.filter (snd >> Array.isEmpty >> not)
                |> Array.length |> fun x -> x >= 2232
                |> Expect.isTrue "should be at least 2232"

            }

            test "insuline has correct unit" {
                GenPresProduct.get true
                |> Array.collect (fun gpp -> gpp.GenericProducts)
                |> Array.filter (fun gp ->
                    gp.Name
                    |> String.toLower
                    |> String.contains "insuline"
                )
                |> Array.map (fun gp ->
                    gp.Id, gp.Label, gp.Substances[0].SubstanceUnit
                )
                |> Array.forall (fun (_, _, u)  -> u |> String.containsCapsInsens "eenheid")
                |> Expect.isTrue "has always unit 'eenheid'"
            }

            test "should not have an generic product with id 130055" {
                GenPresProduct.getGenericProducts ()
                |> Array.tryFind (fun gp ->
                    gp.Id = 130055
                )
                |> Expect.isNone "should be none"

            }

        ]


    let tests =

        [
            ZIndexBaseTableTests.tests
            NameTests.tests
            SubstanceTests.tests
            ProductTests.tests
            DoseRuleTests.tests
            GenPresProductTests.tests
        ]
        |> testList "ZIndex"



