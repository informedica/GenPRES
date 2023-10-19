```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Ventura 13.5 (22G74) [Darwin 22.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method              | Mean         | Error       | StdDev      | Median       |
|-------------------- |-------------:|------------:|------------:|-------------:|
| AllPairesInt_100    |     126.3 μs |     1.33 μs |     1.24 μs |     126.3 μs |
| AllPairsInt_200     |   1,076.4 μs |     6.78 μs |     6.01 μs |   1,074.6 μs |
| AllPairsBR_100      |     639.7 μs |    12.69 μs |    33.89 μs |     628.6 μs |
| AllPairsBR_200      |   3,585.7 μs |     7.39 μs |     6.91 μs |   3,583.7 μs |
| AllPairsBR_Rand_100 |   2,068.7 μs |     5.88 μs |     5.50 μs |   2,071.2 μs |
| AllPairsBR_Rand_200 |  10,738.3 μs |    95.29 μs |    74.40 μs |  10,724.1 μs |
| SolveCountMinIncl   |     153.3 μs |     2.28 μs |     2.13 μs |     153.8 μs |
| Solve_1_Eqs_100     |  28,284.4 μs |   255.51 μs |   239.01 μs |  28,372.7 μs |
| Solve_1_Eqs_200     | 105,200.0 μs | 1,523.82 μs | 1,425.38 μs | 105,116.0 μs |
| Solve_3_Eqs_100     |  29,274.8 μs |   585.01 μs |   518.60 μs |  29,154.9 μs |
| Solve_3_Eqs_200     | 107,030.6 μs | 1,688.19 μs | 1,579.14 μs | 107,266.9 μs |
| Solve_1_Eqs_Rand_10 |  16,717.6 μs |   183.67 μs |   171.81 μs |  16,639.5 μs |
| Solve_1_Eqs_Rand_20 | 177,293.1 μs | 1,016.90 μs |   901.46 μs | 177,328.1 μs |
| Solve_3_Eqs_Rand_10 |  16,968.0 μs |    60.90 μs |    53.99 μs |  16,959.7 μs |
| Solve_3_Eqs_Rand_20 | 177,484.6 μs |   828.61 μs |   775.08 μs | 177,584.3 μs |
