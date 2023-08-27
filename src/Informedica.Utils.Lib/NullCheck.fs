namespace Informedica.Utils.Lib

/// Function to perform a safe null check
module NullCheck =

    /// Check if a value is null
    /// If null return default value d
    /// Else return f of v
    let nullOrDef f d v =
        if isNull v then d
        else f v

    /// Check if values v1 or v2 are null
    /// If null return default value d
    /// Else return f of v1 and v2
    let nullOrDef2 f d v1 v2 =
        if isNull v1 || isNull v2 then d
        else f v1 v2

    /// Check if values v1, v2 or v3 are null
    /// If one or more are null return default value d
    /// Else return f of v1, v2 and v3
    let nullOrDef3 f d v1 v2 v3 =
        if isNull v1 || isNull v2 || isNull v3 then d
        else f v1 v2 v3


    module Tests =

        open Swensen.Unquote

        /// Test nullOrDef
        let testNullOrDef() =
            let f _ = "is not null"
            let d = "is null"
            let v = null
            let r = nullOrDef f d v

            test <@ d = r @>
            let r = nullOrDef f d "not null"
            test <@ "is not null" = r @>

        /// Test nullOrDef2
        let testNullOrDef2() =
            let f _ _ = "is not null"
            let d = "is null"
            let v1 = null
            let v2 = "not null"
            let r = nullOrDef2 f d v1 v2

            test <@ d = r @>
            let r = nullOrDef2 f d v2 v2
            test <@ "is not null" = r @>

        /// Test nullOrDef3
        let testNullOrDef3() =
            let f _ _ _ = "is not null"
            let d = "is null"
            let v1 = null
            let v2 = "not null"
            let v3 = "not null"
            let r = nullOrDef3 f d v1 v2 v3

            test <@ d = r @>
            let r = nullOrDef3 f d v2 v3 v3
            test <@ "is not null" = r @>


        /// Run all tests
        let test() =
            testNullOrDef()
            testNullOrDef2()
            testNullOrDef3()