namespace Shared

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


    let patient = 
        let age = { Years = 0; Months = 0 }
        let wght : Weight = { Estimated = 0.; Measured = 0.} 
        let hght = { Estimated = 0.; Measured = 0. }
        { Age = age; Weight = wght; Height = hght }


    let getAge p = (p |> get).Age


    let getAgeYears p = (p |> getAge).Years


    let getAgeMonths p = (p |> getAge).Months


    /// Estimate the weight according to age
    let ageToWeight pat =
        let yr, mo = pat |> getAgeYears, pat |> getAgeMonths

        let age = (double yr) * 12. + (double mo)
        match
            Shared.Data.NormalValues.ageWeight
            |> List.filter (fun (a, _) -> age <= a) with
        | (_, w)::_  -> w
        | [] -> 0.    


    /// Get either the measured weight or the 
    /// estimated weight if measured weight = 0
    let getWeight pat =
        if (pat |> get).Weight.Measured = 0. then pat.Weight.Estimated else pat.Weight.Measured




type Patient = { Age: Age; Weight : Weight; Length : int }
and Age = { Years : int ; Months : int }
and Weight = { Estimated : double; Measured : double }


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
