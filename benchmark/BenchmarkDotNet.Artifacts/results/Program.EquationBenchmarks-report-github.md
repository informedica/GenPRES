```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Sonoma 14.0 (23A344) [Darwin 23.0.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method              | Mean         | Error        | StdDev       |
|-------------------- |-------------:|-------------:|-------------:|
| AllPairesInt_100    |    124.16 μs |     0.235 μs |     0.208 μs |
| AllPairsInt_200     |    750.41 μs |     3.810 μs |     3.377 μs |
| SolveCountMinIncl   |     97.92 μs |     1.198 μs |     1.120 μs |
| Solve_1_Eqs_100     | 12,788.84 μs |   134.926 μs |   126.210 μs |
| Solve_1_Eqs_200     | 50,817.55 μs | 1,013.622 μs | 2,093.306 μs |
| Solve_3_Eqs_100     | 13,385.99 μs |    81.075 μs |    75.838 μs |
| Solve_3_Eqs_200     | 51,935.72 μs |   993.614 μs | 1,182.827 μs |
| Solve_1_Eqs_Rand_10 |  9,183.25 μs |    27.803 μs |    26.007 μs |
| Solve_1_Eqs_Rand_20 | 88,937.43 μs |    55.626 μs |    46.450 μs |
| Solve_3_Eqs_Rand_10 |  9,415.45 μs |    19.153 μs |    16.978 μs |
| Solve_3_Eqs_Rand_20 | 94,196.45 μs |   260.758 μs |   231.155 μs |
