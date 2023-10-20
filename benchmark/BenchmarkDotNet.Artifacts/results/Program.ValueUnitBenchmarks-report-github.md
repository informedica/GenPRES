```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Sonoma 14.0 (23A344) [Darwin 23.0.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method                  | Mean      | Error    | StdDev   |
|------------------------ |----------:|---------:|---------:|
| BaseValue_200           |  41.12 ms | 0.661 ms | 0.618 ms |
| AllPairs_True_100       |  25.70 ms | 0.089 ms | 0.083 ms |
| AllPairs_True_200       | 129.14 ms | 0.579 ms | 0.513 ms |
| AllPairs_True_Rand_100  | 127.78 ms | 0.404 ms | 0.378 ms |
| AllPairs_False_Rand_200 | 623.85 ms | 4.947 ms | 4.627 ms |
| AllPairs_True_Rand_200  | 624.27 ms | 2.291 ms | 2.031 ms |
