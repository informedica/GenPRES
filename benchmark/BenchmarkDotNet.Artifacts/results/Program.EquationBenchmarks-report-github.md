```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Sonoma 14.0 (23A344) [Darwin 23.0.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method              | Mean        | Error     | StdDev      |
|-------------------- |------------:|----------:|------------:|
| AllPairesInt_100    |    127.0 μs |   0.26 μs |     0.25 μs |
| AllPairsInt_200     |    751.4 μs |   3.80 μs |     3.55 μs |
| SolveCountMinIncl   |    101.7 μs |   0.78 μs |     0.69 μs |
| Solve_1_Eqs_100     | 12,681.1 μs | 143.63 μs |   134.35 μs |
| Solve_1_Eqs_200     | 50,893.2 μs | 892.15 μs |   991.62 μs |
| Solve_3_Eqs_100     | 13,256.2 μs | 122.22 μs |   114.32 μs |
| Solve_3_Eqs_200     | 52,605.5 μs | 989.61 μs | 1,058.87 μs |
| Solve_1_Eqs_Rand_10 |  9,373.4 μs |  22.58 μs |    20.02 μs |
| Solve_1_Eqs_Rand_20 | 90,342.7 μs | 292.40 μs |   273.51 μs |
| Solve_3_Eqs_Rand_10 |  9,707.7 μs |  22.19 μs |    19.67 μs |
| Solve_3_Eqs_Rand_20 | 94,725.7 μs | 210.39 μs |   196.80 μs |
