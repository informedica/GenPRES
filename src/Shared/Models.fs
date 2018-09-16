namespace Shared

open Shared.Utils

module Models =

    module Patient =

        /// Patient model for calculations
        type Patient = 
            { 
                Age: Age
                Weight : Weight
                Height : Height 
            }
        and Age = { Years : int ; Months : int }
        /// Weight in kg
        and Weight = { Estimated : double; Measured : double }
        /// Length in cm
        and Height =  { Estimated : double; Measured : double }


        let apply f (p : Patient) = f p

        
        let get = apply id


        /// Estimate the weight according to age
        /// in `yr` years and `mo` months
        let ageToWeight yr mo =
            let age = (double yr) * 12. + (double mo)
            match
                Shared.Data.NormalValues.ageWeight
                |> List.filter (fun (a, _) -> age <= a) with
            | (_, w)::_  -> w
            | [] -> 0.    


        let patient = 
            let age = { Years = 0; Months = 0 }
            let wght : Weight = { Estimated = ageToWeight 0 0; Measured = 0.} 
            let hght = { Estimated = 0.; Measured = 0. }
            { Age = age; Weight = wght; Height = hght }


        let getAge p = (p |> get).Age


        let getAgeYears p = (p |> getAge).Years


        let getAgeMonths p = (p |> getAge).Months


        /// Get either the measured weight or the 
        /// estimated weight if measured weight = 0
        let getWeight pat =
            if (pat |> get).Weight.Measured = 0. then pat.Weight.Estimated else pat.Weight.Measured


        /// Get either the measured height or the 
        /// estimated height if measured weight = 0
        let getHeight pat =
            if (pat |> get).Height.Measured = 0. then pat.Height.Estimated else pat.Height.Measured


        /// ToDo: make function more general by 
        /// being able to set mo > 12 -> yr
        let private updateAge yr mo (pat: Patient) =

            match yr, mo with
            | Some y, None ->
                if y > 18 || y < 0 then pat
                else
                    let w =  ageToWeight y (pat.Age.Months)
                    
                    { pat with Age = { pat.Age  with Years = y }; Weight = { pat.Weight with Estimated = w } }

            | None, Some m ->
                let age    = pat.Age
                let weight = pat.Weight

                let w = ageToWeight (age.Years) m

                let y = 
                    if m = 12 && age.Years < 18 then 
                        age.Years + 1 
                    else if m = -1 && pat.Age.Years > 0 then  
                        age.Years - 1
                    else
                        age.Years

                let m =
                    if m >= 12 then 0
                    else if m = -1 && y = 0 then 0
                    else if m = -1 && y > 0 then 11
                    else m
                   
                { pat with Age = { age with Months = m; Years = y }; Weight = { weight with Estimated = w } }


            | _ -> pat


        let updateAgeYears yr = updateAge (Some yr) None


        let updateAgeMonths mo = updateAge None (Some mo)


        let calcBMI pat = 
            let l = pat |> getHeight
            if l  > 0. then
                ((pat |> getWeight) / ((l |> float)  ** 2.)) |> Some
            else None


        let show pat =
            let pat = pat |> get

            let wght = 
                let w = pat |> getWeight
                if w < 2. then "" else 
                    w |> Math.fixPrecision 2 |> string

            let e = pat.Weight.Estimated |> Math.fixPrecision 2 |> string

            let bmi = 
                match pat |> calcBMI with
                | Some bmi -> sprintf " BMI %A" bmi
                | None     ->  ""
                    

            sprintf "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg (geschat %s kg)%s" pat.Age.Years pat.Age.Months wght e bmi




type GenPres = { Name: string; Version: string;  }


type Medication =
    | Bolus of Bolus
    | Continuous of Continuous
and Bolus =
    {
        Indication : string
        Generic : string
        NormDose : float
        MinDose : float
        MaxDose : float
        Concentration : float
        Unit : string
        Remark : string
    }
and Continuous =
    {
        Indication: string
        Generic : string
        Unit : string
        DoseUnit : string
        Quantity2To6 : float
        Volume2To6 : float
        Quantity6To11 : float
        Volume6To11 : float
        Quantity11To40 : float
        Volume11To40 : float
        QuantityFrom40 : float
        VolumeFrom40 : float
        MinDose : float
        MaxDose : float
        AbsMax : float
        MinConc : float
        MaxConc : float
        Solution : string
    }
