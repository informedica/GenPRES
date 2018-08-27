namespace Shared

type Patient = { Age: Age; Weight : Weight }
and Age = { Years : int ; Months : int }
and Weight = double

type GenPres = { Name: string; Version: string; Patient : Patient }

