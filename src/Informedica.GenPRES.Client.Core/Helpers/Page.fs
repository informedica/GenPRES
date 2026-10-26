namespace Informedica.GenPRES.Client.Core.Helpers

/// The pages of the application.
module Page =


    /// A page of the application, one entry of the side menu.
    [<RequireQualifiedAccess>]
    type Page =
        | LifeSupport
        | ContinuousMeds
        | Prescribe
        | Nutrition
        | OrderPlan
        | Formulary
        | Parenteralia
        | Interactions
        | Settings
