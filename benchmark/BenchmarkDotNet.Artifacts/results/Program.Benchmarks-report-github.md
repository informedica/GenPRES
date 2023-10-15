```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Monterey 12.6.9 (21G726) [Darwin 21.6.0]
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 6.0.403
  [Host]     : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2


```
| Method              | Mean         | Error       | StdDev      |
|-------------------- |-------------:|------------:|------------:|
| AllPairesInt_100    |     200.5 μs |     2.35 μs |     1.96 μs |
| AllPairsInt_200     |   1,351.2 μs |    13.08 μs |    10.92 μs |
| AllPairsBR_100      |     994.5 μs |     4.76 μs |     4.22 μs |
| AllPairsBR_200      |   5,406.7 μs |    22.60 μs |    18.87 μs |
| AllPairsBR_Rand_100 |   3,571.2 μs |    15.51 μs |    13.75 μs |
| AllPairsBR_Rand_200 |  16,147.1 μs |   158.76 μs |   140.73 μs |
| SolveCountMinIncl   |     281.5 μs |     1.07 μs |     0.89 μs |
| Solve_1_Eqs_100     |  44,174.2 μs |   870.02 μs |   854.48 μs |
| Solve_1_Eqs_200     | 177,626.8 μs | 3,524.22 μs | 4,582.48 μs |
| Solve_3_Eqs_100     |  45,644.5 μs |   724.51 μs |   642.26 μs |
| Solve_3_Eqs_200     | 179,394.1 μs | 3,491.76 μs | 4,779.56 μs |
| Solve_1_Eqs_Rand_10 |  24,787.3 μs |    93.87 μs |    78.39 μs |
| Solve_1_Eqs_Rand_20 | 271,758.6 μs | 3,096.51 μs | 2,896.48 μs |
| Solve_3_Eqs_Rand_10 |  25,649.5 μs |    89.37 μs |    69.78 μs |
| Solve_3_Eqs_Rand_20 | 269,382.4 μs | 1,385.37 μs | 1,295.88 μs |
