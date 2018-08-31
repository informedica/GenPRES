namespace Shared

type Patient = { Age: Age; Weight : Weight }
and Age = { Years : int ; Months : int }
and Weight = { Estimated : double; Measured : double }

type Medication =
    | Bolus of Bolus
    | Continuous of Continuous
// ind, item, dose, min, max, conc, unit, rem
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
//                                          Standaard oplossingen								            Advies doseringen						
//                                          2 tot 6		    6 tot 11	    11 tot 40	    vanaf 40								
// Tbl_Ped_MedContIV	Eenheid	DosEenheid	Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    Hoev	Vol	    MinDos	MaxDos	AbsMax	MinConc	MaxConc	OplKeuze
and Continuous =
    {
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


type GenPres = { Name: string; Version: string; Patient : Patient }

