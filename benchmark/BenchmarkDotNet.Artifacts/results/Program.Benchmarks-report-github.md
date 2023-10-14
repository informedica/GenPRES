```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Monterey 12.6.9 (21G726) [Darwin 21.6.0]
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 6.0.403
  [Host]     : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2


```
| Method            | Mean         | Error       | StdDev      |
|------------------ |-------------:|------------:|------------:|
| AllPairesInt_100  |     194.5 μs |     1.31 μs |     1.09 μs |
| AllPairsInt_200   |   1,313.1 μs |    18.35 μs |    16.26 μs |
| AllPairsBR_100    |     972.8 μs |     3.32 μs |     2.77 μs |
| AllPairsBR_200    |   5,275.6 μs |    18.56 μs |    15.50 μs |
| SolveCountMinIncl |     278.4 μs |     0.95 μs |     0.84 μs |
| Solve_1_Eqs_100   |  43,351.1 μs |   825.59 μs |   772.26 μs |
| Solve_1_Eqs_200   | 178,478.8 μs | 3,545.83 μs | 3,941.18 μs |
| Solve_3_Eqs_100   |  45,053.0 μs |   891.40 μs |   990.79 μs |
| Solve_3_Eqs_200   | 178,513.1 μs | 3,469.46 μs | 3,245.34 μs |
