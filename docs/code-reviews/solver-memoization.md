# Code Review & Memoization Design for Solver.fs

Repository: halcwb/GenPres2  
File reviewed: src/Informedica.GenSolver.Lib/Solver.fs (commit c66e953)

Date: 2025-10-17  
Reviewer: senior developer (AI)

---

## What I did

I performed a thorough code review of `Solver.fs`, identified hotspots and potential performance bottlenecks, and designed a detailed specification for a memoization mechanism that canonicalizes variable names (by replacing substrings with deterministic short symbols), includes variable values in keys, and safely re-maps names after retrieving cached results. I drafted an F# implementation sketch and an integration plan focused on placing the cache at the `Equation.solve` call site. Below I provide detailed findings, line-referenced observations, the memoization specification, an implementation sketch, testing plan, and recommended roadmap.

---

## High-level summary / conclusion

- Yes — you can improve performance by memoizing results of individual equation solves, provided you canonicalize variable names and include the relevant variable value attributes in the cache key.
- The canonicalization must be deterministic and collision-free within a single key generation and you must deep-copy or use immutable DTOs for cached results.
- Best integration point: wrap calls to `Equation.solve` in `solveE` with a memoization layer.
- Start with a per-solve-call (per `solve` invocation) cache to bound memory and limit semantic leakage across unrelated runs.

---

## Annotated code review (with line ranges and contextual info)

The line numbers below are approximate and reflect the layout in the provided file.

### Header & imports (approx lines 1–18)

- Observations: Clear module declaration and imports. Nothing critical.
- Note: `module EQD = Equation.Dto` and `module Name = Variable.Name` are present but not heavily used inside `Solver.fs`. Fine.

### sortByName (approx lines 20–30)

- Code:
  - Sorts equations by the name of the first variable: `Equation.toVars |> List.head |> Variable.getName`.
- Issue:
  - Risk of runtime exception if an equation can have zero variables (List.head on empty). Document precondition or guard (e.g., Option.head or match).

### printEqs (approx lines 32–52)

- Utility logging; fine.

### contains (approx lines 55–62)

- Simple: `let contains eq eqs = eqs |> List.exists ((=) eq)`. Fine.

### replace (approx lines 64–92)

- Behavior: partitions eqs into those that contain any variable in `vars`, then applies replacements over `rpl`.
- Performance concern:
  - The fold `vars |> List.fold (fun acc v -> acc |> List.map (Equation.replace v)) rpl` has complexity O(|vars| * |rpl|). If many variables and many equations, this can be expensive.
  - Memoizing `Equation.replace` or precomputing var->equation indices (or using maps) could help here.
- Correctness: returns `(replaced, rest)` which is used by caller; good.

### sortQue (approx lines 95–104)

- Sorts queue by `Equation.count onlyMinMax`.
- Note: Sorting every iteration of main loop may be expensive if `Equation.count` is not trivial.

### check (approx lines 106–116)

- Logic is a bit non-intuitive:
  - If all equations are either not solvable OR already solved, then verify them with `Equation.check`.
  - Else return true.
- Suggestion: add a code comment describing the desired semantics and why `true` is returned early.

### solve (core — approx lines 120–260)

This is the main hotspot and focus for memoization.

Key areas:

1. `solveE` wrapper (approx lines 122–141)
   - Wraps `Equation.solve` with try/with and converts exceptions.
   - Catch-all branch prints and `failwith` for unexpected exceptions — this kills process; you might prefer wrapping into `Exceptions.SolverErrored` consistently.
   - **Primary memoization target**: this is the single place the module calls `Equation.solve`. Wrap here with a memoization layer.

2. `loop` recursion (approx lines 143–236)
   - Recomputes `que @ acc` frequently to calculate limit max; constructing `que @ acc` every loop allocates lists. Precompute lengths or pass sizes around to avoid repeated concatenation.
   - Sorts queue each iteration: `let que = que |> sortQue onlyMinIncrMax` — if `sortQue` is expensive, consider caching sorted order or using a priority structure.
   - Many list concatenations and rebuilds: `acc @ que`, `tail |> replace vars |> ... |> List.append rpl`. Using more efficient structures (ResizeArray or Deque) may help but requires weighing against F# idioms and immutability.
   - When an equation is `Changed cs`, `cs |> List.map fst` extracts vars and `replace` puts some eqs back on the queue — potentially triggering many repeated `replace` calculations. If the same variable replacements recur frequently, caching replacement results could help.

3. Error handling
   - A mix of Exceptions APIs and `failwith`. Consider normalizing.

### Helper functions `solveVariable` and `solveAll` (approx lines 258–end)

- They wrap `solve` and check eq counts before & after. The equality check (number of equations unchanged) is enforced via `failwith` — fine for now but consider more structured error propagation.

---

## Potential runtime bottlenecks and where memoization helps

- Repeatedly calling `Equation.solve` on structurally identical equations that differ only by variable names: memoization brings big wins here.
- Repeated string manipulation when replacing variable names: caching replaced equations for given (equation, var) pairs could help.
- Frequent sorting of queue if `Equation.count` is expensive.
- Repetitive `que @ acc` concatenation to compute lengths: micro-optimization by passing sizes.

---

## Memoization: detailed specification draft

Goal: Memoize results of solving individual equations in a way that:

- treats structurally identical equations with different variable names as equal (if variable semantics and values match),
- includes variable values/ranges used by `Equation.solve` in the key,
- re-maps canonical names back to original names when returning cached results,
- is deterministic, safe, and memory-bounded.

Assumptions

- `Equation.solve` is deterministic given:
  - the equation structure (operators, operands),
  - variables' relevant attributes (e.g., min/incr/max, current value or intervals),
  - solver mode flags such as `onlyMinIncrMax`.
- If `Equation.solve` reads external mutable state or randomness, memoization is unsafe unless those aspects are included in the key.

High-level design

- Granularity: cache results for single `Equation.solve` invocations (equation + solver options + per-variable relevant attributes).
- Key components:
  1. Canonicalized equation structure: AST or deterministic string representation using canonical variable names.
  2. Canonical variable names: derived from original names by deterministic tokenization and token->symbol mapping (short strings, single letters or multi-letter if necessary).
  3. Per-variable value snapshot: textual representation of attributes that influence the solve (min, max, incr, intervals, sets).
  4. Solver mode flags that affect behavior (e.g., `onlyMinIncrMax`).
- Key hashing:
  - Build canonical representation string; compute SHA-256 hex digest for compact key.
  - Optional: store canonical representation alongside hashed key in cache for extra collision checks.

Canonicalization algorithm (for variable names)

1. Tokenize variable names by splitting on non-alphanumeric separators (e.g., regex `[^A-Za-z0-9_]+`) — exact tokenization rule can be tuned.
2. Collect tokens deterministically:
   - Iterate variables in sorted order by their original names (to ensure reproducibility).
   - For each variable, iterate tokens in order of appearance.
   - Deduplicate tokens preserving first-appearance ordering (or final sorted order — choose one deterministic order).
3. Map each unique token to a short symbol deterministically:
   - Generator: `a, b, ..., z, A, B, ..., Z, aa, ab, ...` (ensures enough symbols for large token sets).
4. Rebuild each variable's canonical name by replacing tokens with mapped symbols and joining with a fixed canonical separator (e.g., `_`).
5. Use canonical names when serializing equation structure.

Including variable values

- For each variable used in the equation, include a canonical serialization of its relevant attributes:
  - e.g., `min=1,max=100,incr=1`, or `interval=[0;10]`, or discrete `values=[a;b;c]`.
- Sort the per-variable attribute entries by canonical variable name order to keep key deterministic.

Cache storage & format

- Backing store: `ConcurrentDictionary<string, CachedValue>` (thread-safe).
- CachedValue record fields:
  - `CanonicalRep: string` (for debugging / collision check),
  - `Result: EquationSolveResultDto` (immutable or serialized DTO),
  - `TokenMap: Map<string,string>` (optional; needed to remap canonical names back to original),
  - `Timestamp`, `HitCount` (optional).
- Store a deep-copy (or a purely immutable DTO) to ensure cached values can't be mutated by subsequent code.

Cache lifecycle & eviction

- Recommend: per-solve-call cache (instantiate empty cache at start of `solve` and drop at end) to avoid uncontrolled growth and cross-run pollution.
- Optionally, provide a global cache with controlled eviction:
  - Implement max-size + LRU eviction, or TTL-based eviction.
  - Monitor memory usage; expose clear/resets.

Remapping results

- `Equation.solve` returns e.g. `(equation, Changed cs)` where `cs : list<(Variable, Value)>`.
- If stored results contain canonical variable names, remap canonical var names back to original using the token map:
  - For variables, reverse the mapping to get original names.
  - Values that are purely numeric or ranges do not need remapping; but if any value embeds variable names, remap those references too.

Safety & correctness checks

- If returning from cache, optionally assert the cached `CanonicalRep` equals recomputed canonicalization of the current input; if not, discard and recompute (guards against hash collisions).
- Audit `Equation.solve` and dependencies for side-effects or non-determinism — if present, include those in the key or disable memoization.

API & integration points

- New module: `Informedica.GenSolver.Lib.Memo` (or `Solver.Memo`).
- Public functions:
  - `getOrComputeEquationSolve : (onlyMinIncrMax:bool) -> (log:Logger) -> equation:Equation -> EquationSolveResult`
  - `clearCache : unit -> unit` (if using global cache)
  - `stats : unit -> CacheStats`
- Integration:
  - In `Solver.solve`, wrap the call to `Equation.solve` in `solveE` using `Memo.getOrComputeEquationSolve`.
  - Keep `solveE`'s existing exception handling semantics, but memoize only the successful `Equation.solve` result (or store errored results too, but be careful about error semantics).

---

## Implementation sketch (F#) — draft module and integration notes

Below is a concise sketch to adapt into the repository. Replace `obj` placeholders with your actual `Equation`, `Variable`, `Value`, and `EquationSolveResult` types.

```fsharp
module Informedica.GenSolver.Lib.Memo

open System
open System.Text
open System.Security.Cryptography
open System.Collections.Concurrent
open System.Text.RegularExpressions

// TODO: replace with concrete types
type Equation = obj
type VarAttr = obj
type SolveResult = obj

type CachedValue =
    { CanonicalRep : string
      Result : SolveResult
      Timestamp : DateTime
      HitCount : int64 }

let private cache = ConcurrentDictionary<string, CachedValue>()

let private sha256hex (s: string) =
    use sha = SHA256.Create()
    s |> Encoding.UTF8.GetBytes |> sha.ComputeHash
    |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

let private tokenize (name:string) =
    Regex.Split(name, "[^A-Za-z0-9_]+")
    |> Array.filter (String.IsNullOrWhiteSpace >> not)

let private symbolsFor n =
    let letters = [| for c in 'a'..'z' -> string c |] |> Array.append [| for c in 'A'..'Z' -> string c |]
    let rec symbol i =
        if i < letters.Length then letters.[i]
        else
            let prefix = symbol (i / letters.Length - 1)
            prefix + letters.[i % letters.Length]
    [| for i in 0 .. n-1 -> symbol i |]

/// Build a canonical representation of an equation including canonical var names and per-var attributes
let buildCanonicalRepresentation (eq: Equation) (extractVars: Equation -> (string * VarAttr) list) =
    let vars = extractVars eq |> List.sortBy fst
    // tokens in deterministic order (first appearance across sorted variables)
    let allTokens =
        vars
        |> List.collect (fun (name, _) -> tokenize name |> Array.toList)
        |> Seq.distinct |> Seq.toList
    let symbols = symbolsFor allTokens.Length
    let tokenMap = allTokens |> List.mapi (fun i t -> t, symbols.[i]) |> Map.ofList
    let canonVars =
        vars |> List.map (fun (name, attr) ->
            let tokens = tokenize name
            let canonName = tokens |> Array.map (fun t -> tokenMap.[t]) |> String.concat "_"
            (canonName, attr))
    // TODO: produce a deterministic structural serialization of eq using canonical var names
    let sb = StringBuilder()
    sb.AppendLine("vars:") |> ignore
    canonVars |> List.iter (fun (cn, attr) -> sb.AppendLine(sprintf "%s:%A" cn attr) |> ignore)
    sb.ToString(), tokenMap

let getOrCompute
    (eq: Equation)
    (extractVars: Equation -> (string * VarAttr) list)
    (compute: unit -> SolveResult) : SolveResult =
    let canonical, tokenMap = buildCanonicalRepresentation eq extractVars
    let key = sha256hex canonical
    match cache.TryGetValue key with
    | true, cached ->
        // Optionally validate canonical == cached.CanonicalRep
        // Return cached.Result (deep-clone if needed)
        cached.Result
    | false, _ ->
        let res = compute()
        let cv = { CanonicalRep = canonical; Result = res; Timestamp = DateTime.UtcNow; HitCount = 0L }
        cache.TryAdd(key, cv) |> ignore
        res

let clear () = cache.Clear()
```

Integration sketch (in `Solver.fs`, inside `solveE`):

- Replace direct `Equation.solve` call with:

```fsharp
let solveE n eqs eq =
    let compute () = Equation.solve onlyMinIncrMax log eq
    // supply a function to extract variable names and their relevant attributes:
    let extractVars (eq: Equation) : (string * VarAttr) list =
        // implement using Equation.toVars and Variable attributes (min,incr,max, intervals)
        // e.g., eq |> Equation.toVars |> List.map (fun v -> Variable.getName v, Variable.snapshotAttrs v)
        []
    try
        Memo.getOrCompute eq extractVars compute
    with
    | Exceptions.SolverException errs -> ...
```

Notes:

- Implement `Variable.snapshotAttrs` to return only attributes that influence `Equation.solve`.
- If `Equation.solve` can return mutated equations or contain mutable fields, store serialized DTO instead.

---

## Testing & validation plan

1. Unit tests:
   - Equality test: create two equations with identical structure but different variable names (and identical variable attributes). Assert memoized result equals direct `Equation.solve`.
   - Value-sensitivity test: same structure but different variable ranges produce distinct outcomes and should create distinct cache keys.
2. Property-based tests:
   - Randomly generate structural templates and random variable renamings; assert cache hits on canonical equivalents.
3. Benchmarks:
   - Measure average time of `Equation.solve` vs canonicalization + cache lookup. Run representative workloads where many equations differ only by name.
4. Memory tests:
   - Run long solver workloads, monitor cache size; ensure eviction (if global) behaves correctly.
5. Correctness under errors:
   - Validate that cached errored results are handled consistently (likely store error tuples in cache too, or avoid caching errored runs).

---

## Example canonicalization walkthrough

Given:

- eq1 variables: ["A.person_age", "B.min", "C.offset"]
- eq2 variables: ["Z.pa", "X.mn", "Y.off"]

Tokenization (split on `.`):

- eq1 tokens: ["A","person_age","B","min","C","offset"]
- eq2 tokens: ["Z","pa","X","mn","Y","off"]

Token map (deterministic by sorted var names / first appearance):

- e.g., "A"->"a", "person_age"->"b", "B"->"c", "min"->"d", "C"->"e", "offset"->"f"

Canonical names become:

- "A.person_age" => "a_b"
- "B.min" => "c_d"
- "C.offset" => "e_f"

If eq2 canonicalizes to the same structural serialization, the hash key matches and a cache hit occurs.

---

## Pitfalls and gotchas

- Non-determinism: if `Equation.solve` depends on external mutable state, memoization can be incorrect unless that state is included in the key.
- Mutable objects: avoid storing mutable objects directly in cache; prefer immutable DTOs or serialized snapshots and deep clone on retrieval.
- Tokenization ambiguity: ensure tokenization rules are consistent and robust (document and reuse).
- Collision: SHA256 collisions are virtually impossible, but for absolute safety you can store canonicalRep in cache and verify equality.
- Overhead: canonicalization & hashing add overhead; only profitable when `Equation.solve` cost >> canonicalization cost.

---

## Recommendations & roadmap

1. Implement `Memo` module (per-solve-call cache initially).
2. Implement deterministic canonical serializer for equations (this is critical).
3. Integrate memoization at `solveE` (wrap `Equation.solve`).
4. Add unit tests for correctness and benchmarks for performance.
5. If beneficial, extend caching to other hot functions (`Equation.replace`, `Equation.count`) and consider LRU/TTL eviction for a global cache.

---

## What's next (offer)

I can:

- Produce a concrete PR that implements the `Memo` module using the real `Equation` and `Variable` types from your repo and integrates it into `Solver.fs`.
- Or provide focused patches: (a) canonical serializer for `Equation`, (b) `Memo` module, (c) `solveE` integration and tests/benchmarks.

Tell me which you prefer and I will implement it using the repository types and produce the patch/PR.

---

## Appendix — Quick checklist for the implementation

- [ ] Audit `Equation.solve` for side effects.
- [ ] Implement deterministic equation-to-string serializer using canonical var names.
- [ ] Implement `Variable.snapshotAttrs` to capture min/incr/max/intervals.
- [ ] Implement `Memo` with ConcurrentDictionary, per-solve-call or global with eviction.
- [ ] Integrate into `solveE`.
- [ ] Add tests & benchmarks.

---

If you'd like, I will now fetch the repository's `Equation` and `Variable` type definitions and produce a concrete implementation and PR. Which scope do you want: a minimal working memoization prototype (per-solve call cache) or a robust global cache with eviction and metrics?
