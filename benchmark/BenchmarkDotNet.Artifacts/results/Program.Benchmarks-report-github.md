```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, macOS Monterey 12.6.9 (21G726) [Darwin 21.6.0]
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 6.0.403
  [Host]     : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2


```
| Method            | Mean           | Error        | StdDev        |
|------------------ |---------------:|-------------:|--------------:|
| AllPairesInt_100  |       103.9 μs |      0.57 μs |       0.50 μs |
| AllPairsInt_1_000 |    60,215.7 μs |    941.20 μs |     880.39 μs |
| AllPaires_100     |       459.6 μs |      2.40 μs |       2.00 μs |
| AllPairs_1_000    |   160,309.7 μs |  1,069.78 μs |     893.32 μs |
| SolveCountMinIncl |     3,682.7 μs |     48.15 μs |      45.04 μs |
| SolveAll_100      |    45,892.0 μs |    909.44 μs |   1,150.15 μs |
| SolveAll_1_000    | 4,632,200.1 μs | 91,113.74 μs | 171,133.69 μs |
