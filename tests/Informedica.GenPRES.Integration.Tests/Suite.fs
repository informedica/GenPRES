/// Every browser test, in one list run one test at a time. The tests share the demo server's
/// stub logins, and a User has one open Session: a launch of one test running beside another of
/// the same login would end the other's Session. The order is the use cases', with the test that
/// waits out a lock of a minute last.
module Informedica.GenPRES.Integration.Tests.Suite

open Expecto


/// The suite.
[<Tests>]
let tests =
    testSequenced
    <| testList
        "GenPRES in the browser"
        [
            LaunchTests.tests
            SessionTests.tests
            AuthorityWithdrawnTests.tests
            DirectOpenTests.tests
            EnrolmentTests.tests
            SigningTests.tests
            HeldContextTests.tests
            TwoUsersTests.tests
            ReaderTests.tests
            SessionEndingTests.tests
            WorkstationTakeoverTests.tests
        ]
