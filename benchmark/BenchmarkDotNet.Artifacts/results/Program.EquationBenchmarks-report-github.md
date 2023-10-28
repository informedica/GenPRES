```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Sonoma 14.0 (23A344) [Darwin 23.0.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method              | Mean        | Error       | StdDev      |
|-------------------- |------------:|------------:|------------:|
| AllPairesInt_100    |    124.4 μs |     0.50 μs |     0.46 μs |
| AllPairsInt_200     |    740.2 μs |     4.20 μs |     3.93 μs |
| SolveCountMinIncl   |    100.3 μs |     0.56 μs |     0.49 μs |
| Solve_1_Eqs_100     | 12,808.2 μs |   123.12 μs |   115.17 μs |
| Solve_1_Eqs_200     | 51,027.0 μs | 1,018.90 μs | 2,034.85 μs |
| Solve_3_Eqs_100     | 13,494.7 μs |   140.81 μs |   131.72 μs |
| Solve_3_Eqs_200     | 52,360.1 μs |   997.85 μs | 1,109.10 μs |
| Solve_1_Eqs_Rand_10 |  9,078.6 μs |    23.49 μs |    20.82 μs |
| Solve_1_Eqs_Rand_20 | 89,190.0 μs |   441.00 μs |   412.51 μs |
| Solve_3_Eqs_Rand_10 |  9,599.2 μs |    28.00 μs |    26.19 μs |
| Solve_3_Eqs_Rand_20 | 90,438.7 μs |   358.47 μs |   335.31 μs |
