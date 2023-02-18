namespace Informedica.MetaVision.Lib

#nowarn "0058"



[<AutoOpen>]
module Types =



    type PatternMode = | Standard | Cyclical | Tapering | LoadingAndMaintenance


    type Frequency = | Continuous | DosePerDay | DosePerWeek | Once | SelectedTimes | SetInterval


    type OrderingType = Both | NonInfuse


    type MedicationStatus = Active | Inactive | Retired


    type Assortment = UMCU | ICC | ICK | NEO


    type Units =
        | VolumeUnit of string
        | MassUnit of string
        | MolarUnit of string
        | UnitUnit of string
        | WeightUnit of string


    type DoseForm =
        {
            ExternalCode : int
            DoseFormName : string
            Routes : string[]
            OrderingType : OrderingType
            IsDrugInSolution : bool
            Category : string
            IsDispensableAmountAllowed : bool
        }



    type Ingredient =
        {
            ExternalCode : string
            IngredientName : string
            Unit : string
        }


    type Product =
        {
            Id : string
            ProductID : string
            ProductName : string
            MedicationName : string
            Manufacturer : string
            DoseForm : string
            Routes : string
            Format : string
            IncrementValue : decimal
            DefaultUnit : string
            IsUnknownStrength : string
            StrengthLEFT : decimal
            StrengthLEFTUnit : string
            StrengthRIGHT : string
            StrengthRIGHTUnit : string
            DiluentGroup : string
            ProductRequiresReconstitution : string
            IsVolumeKnown : string
            Volume : string
            DiluentName : string
            IsFormulary : bool
        }


    type ComplexMedication =
        {
            ComplexMedictionName : string
            IngredientName : string
            Concentration : decimal
            ConcentrationUnit : string
            In : string
            InUnit : string
        }


    type Medication =
            {
                ExternalCode : string
                MedicationName : string
                Title : string
                Unit  : string
                ATC  : string
                Status : MedicationStatus
                Format  : string
                IncrementValue : decimal
                CodeSnippetName  : string
                Frequencies  : string
                DoseForms : string
                Routes  : string
                AdditivesGroup  : string
                DiluentsGroup  : string
                DrugInDiluentGroup : string
                DrugFamily  : string
                DrugSubfamily  : string
                Assortment : Assortment[]
                IsFormulary : bool
                CreateProduct : bool
                ComplexMedications : ComplexMedication[]
                Brands : string[]
                Products : Product []
                IsSolution : bool
            }



    type OrderingStyle =
        | NoInfusedOver
        | SpecifyInfuseOver
        | SetDoseAndRate


    type OrderTemplate =
            {
                OrderTemplateName : string
                MedicationName : string
                ProductName  : string
                DoseForm  : string
                Route : string
                IsPRN  : string
                PatternMode  : string
                Frequency  : string
                ComponentType  : string
                OrderingStyle  : string
                LockerTemplate  : string
                ComponentMedicationName  : string
                ComponentProductName  : string
                ComponentQuantityVolumeValue : decimal
                ComponentQuantityVolumeUnit  : string
                ComponentConcentrationMassUnit  : string
                ComponentConcentrationVolumeUnit  : string
                ComponentDrugInDiluentDiluentMedicationName : string
                ComponentDrugInDiluentVolumeValue : decimal option
                ComponentDrugInDiluentVolumeUnit : string
                TotalVolumeUnit  : string
                StartMethod  : string
                EndMethod : string
                WeightType  : string
                Comment  : string
                Caption  : string
                AvailableInRT  : string
            }


    type ImportConfig =
        {
            Ingredients : string
            Medications : string
            ComplexMedications : string
            Brands : string
            Products : string
            OrderTemplates : string
            ImportFile : string
            IncludeAssortment : Assortment[]
        }

