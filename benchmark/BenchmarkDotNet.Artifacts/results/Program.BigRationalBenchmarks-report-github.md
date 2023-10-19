```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Sonoma 14.0 (23A344) [Darwin 23.0.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 6.0.415
  [Host]     : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 6.0.23 (6.0.2323.48002), Arm64 RyuJIT AdvSIMD


```
| Method              | Mean      | Error     | StdDev    |
|-------------------- |----------:|----------:|----------:|
| AllPairsBR_100      |  6.251 ms | 0.0584 ms | 0.0547 ms |
| AllPairsBR_200      | 39.407 ms | 0.6607 ms | 0.6181 ms |
| AllPairsBR_Rand_100 |  9.480 ms | 0.1136 ms | 0.1063 ms |
| AllPairsBR_Rand_200 | 50.965 ms | 0.1216 ms | 0.1078 ms |
