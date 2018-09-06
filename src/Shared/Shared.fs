namespace Shared

type Patient = { Age: Age; Weight : Weight }
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
