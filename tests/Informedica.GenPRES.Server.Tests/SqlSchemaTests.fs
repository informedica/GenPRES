/// The session store's test and development database. For now only that the SQLite package
/// reaches this project through the server, and that its native library loads on this OS.
module Informedica.GenPRES.Server.Tests.SqlSchemaTests

open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite


[<Tests>]
let tests =
    testList
        "the SQLite package"
        [
            test "opens an in-memory database and answers a query" {
                use conn = new SqliteConnection("Data Source=:memory:")
                conn.Open()
                use cmd = conn.CreateCommand()
                cmd.CommandText <- "select 1 + 1"

                cmd.ExecuteScalar() |> unbox<int64> |> Expect.equal "the engine computes" 2L
            }
        ]
