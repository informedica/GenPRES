namespace Informedica.Utils.Lib.BCL


 module Tuple =


        /// Get the first element of a tuple of 3 elements
        let fstOf3 (x, _, _) = x


        /// Get the second element of a tuple of 3 elements
        let sndOf3 (_, x, _) = x


        /// Get the third element of a tuple of 3 elements
        let thrdOf3 (_, _, x) = x