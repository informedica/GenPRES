namespace GenPres.Shared

/// This module defines shared types between
/// the client and the server
module Types =

    module Configuration =

        type Configuration = Setting list
        and Setting =
            { Department : string
              MinAge : int
              MaxAge : int
              MinWeight : float
              MaxWeight : float }


    module Patient =

        module Age = 

            type Age =
                { Years : int
                  Months : int
                  Weeks : int
                  Days : int }


        type Age = Age.Age

        /// Patient model for calculations
        type Patient =
            { Age : Age
              Weight : Weight
              Height : Height }

        /// Weight in kg
        and Weight =
            { Estimated : double
              Measured : double }

        /// Length in cm
        and Height =
            { Estimated : double
              Measured : double }


    module Request =

        module Configuration =

            type Msg = Get

        module Patient =

            type Msg =
                | Init
                | Clear
                | Get
                | Year of int
                | Month of int
                | Weight of float
                | Height of float

        module AcuteList =
            type Msg = Get of Patient.Patient

        type Msg =
            | PatientMsg of Patient.Msg
            | ConfigMsg of Configuration.Msg



    module Response =


        type Response =
            | Configuration of Configuration.Configuration
            | Patient of Patient.Patient
