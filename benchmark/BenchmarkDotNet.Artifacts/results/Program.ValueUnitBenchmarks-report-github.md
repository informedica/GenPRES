```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Sonoma 14.0 (23A344) [Darwin 23.0.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method                            | Mean      | Error    | StdDev   |
|---------------------------------- |----------:|---------:|---------:|
| BaseValue_200                     |  39.32 ms | 0.502 ms | 0.419 ms |
| AllPairs_True_100                 |  25.08 ms | 0.117 ms | 0.104 ms |
| AllPairs_True_200                 | 128.50 ms | 0.730 ms | 0.683 ms |
| AllPairs_True_Rand_100            | 133.47 ms | 0.397 ms | 0.372 ms |
| AllPairs_False_Rand_200           | 555.15 ms | 4.057 ms | 3.596 ms |
| AllPairs_True_Rand_200            | 554.77 ms | 3.653 ms | 3.051 ms |
| AllPairs_True_Rand_100_mgPerMl_ml | 114.64 ms | 2.232 ms | 2.088 ms |
