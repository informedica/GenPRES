# Implementation plan for issue #450

## Problem

[#450](https://github.com/informedica/GenPRES/issues/450) follows on from #447. SDK
10.0.400 broke Aether's cross-assembly inline SRTP dispatch: any module whose
initializer runs `Optic.get`, `Optic.set` or `>->` throws
`Dynamic invocation of op_HatEquals is not supported` at runtime, not only under test
discovery. It is an upstream F# compiler regression
([dotnet/fsharp#20253](https://github.com/dotnet/fsharp/issues/20253)), not our code and
not specific to Aether.

That is already mitigated: `global.json` pins the SDK to `10.0.302` with
`rollForward: latestPatch` (PR #449). This plan removes the dependency, so we are not
holding an SDK band in place while we wait on an upstream fix, and so this whole class of
cross-assembly SRTP fragility goes away.

## Where Aether is used

`open Aether` / `Optic.get` / `Optic.set` / `>->` appears in:

- `src/Informedica.GenCORE.Lib/MinMax.fs`
- `src/Informedica.GenCORE.Lib/Patient.fs` (reference model, not on the production path;
  see the file header)
- `src/Informedica.ZIndex.Lib/DoseRule.fs`
- `src/Informedica.ZForm.Lib/PatientCategory.fs`
- `src/Informedica.ZForm.Lib/DoseRule.fs`
- `src/Informedica.ZForm.Lib/Dto.fs`
- `src/Informedica.ZForm.Lib/GStand.fs`
- `src/Informedica.GenORDER.Lib/Patient.fs`

`GenCORE.Lib/Patient.fs` also depends on `GenCORE.Lib/Aether.fs`, a local module (not the
NuGet package) holding the `Morphisms` isomorphism pairs it composes through `>->`.

## Approaches rejected

**Myriad-generated lenses.** Myriad's `LensesGenerator.fs` only matches a union with
exactly one case. We need a prism into `MinMax.fs`'s two-case `Limit` DU
(`Inclusive`/`Exclusive`) on day one. That gap was raised in
[Myriad #107](https://github.com/MoiraeSoftware/myriad/issues/107) in 2021 and closed in
2023 with nothing merged. Using Myriad here means building the feature upstream first,
plus adding Myriad.Core/Myriad.Sdk as a build-time codegen dependency.

**FSharpPlus `Lens`.** Also SRTP-based (Van Laarhoven style), the same construct that
caused #450. That relocates the fragility, it does not remove it.

## Chosen approach

A small hand-rolled `Lens`/`Prism`/`Isomorphism` module of plain, non-inline functions.
Decide per file whether lens composition is doing real work:

**Track A: keep composing, on the new module.** `ZForm.Lib/DoseRule.fs`, `Dto.fs`,
`GStand.fs`. `DoseRule.fs` composes lenses in the form
`Dosage-variant_ >-> DoseRange-variant_ >-> MinMax.incl/exclMin/MaxLens`, close to 150 of
them, up to four levels deep, including one prism-through-prism. Rewriting those as nested
record updates is a lot of duplicated code and a lot of room for mistakes. `Dto.fs` also
takes a raw `Prism` as a parameter, which a plain get/set pair cannot express.

The module is `Informedica.Utils.Lib.Optic`, in the base library every file in scope
already sits on:

```fsharp
type Lens<'a, 'b> = ('a -> 'b) * ('b -> 'a -> 'a)
type Prism<'a, 'b> = ('a -> 'b option) * ('b -> 'a -> 'a)
type Isomorphism<'a, 'b> = ('a -> 'b) * ('b -> 'a)
```

with `Optic.get`/`getOpt`/`set`, `Lens.composeLens`/`composePrism`/`composeIso`,
`Prism.composeLens`/`composePrism`/`composeIso`, and `fst_`/`snd_`. The shapes and
composition are checked against Aether's `Compose.lens`/`Compose.prism`, so the lens and
prism *values* defined elsewhere need no change; only the dispatch and the `>->` operator
do.

The module sits in its own namespace `Informedica.Utils.Lib.Optics`, not bare
`Informedica.Utils.Lib`. Aether's `Lens`/`Prism` type abbreviations have the same names,
and `Informedica.Utils.Lib` is opened almost everywhere, so putting these at that level
shadows Aether's types in every file still on it and breaks the SRTP composition. A file
opens `Informedica.Utils.Lib.Optics` only in the same commit that drops its `open Aether`.

**Track B: drop optics for plain functions.** `MinMax.fs` (its own `getMin`/`setMin`
etc.), `ZIndex.Lib/DoseRule.fs`, `ZForm.Lib/PatientCategory.fs`,
`GenORDER.Lib/Patient.fs`, `GenCORE.Lib/Patient.fs`. These compose one or two levels deep
at most, and a lens costs more definitions than the direct version:
`GenORDER.Lib/Patient.fs`'s `getAge`/`setAge` is four definitions as a lens, two as a
plain pair.

`MinMax.fs`'s exported lens and prism values (`min_`, `inclMinLens`, `Limit.Inclusive_`
and the rest) stay as plain tuples because Track A composes onto them. Only its internal
`Optic.get`/`Optic.set` calls change.

## Steps

All done. The whole migration is one PR.

1. Add `Informedica.Utils.Lib/Optic.fs` plus lens/prism law tests in
   `Informedica.Utils.Tests`. Pure addition, no consumers touched.
2. `ZIndex.Lib/DoseRule.fs` (Track B). Flat lenses, nothing else affected. Removed the
   now-unused `_`-suffixed static members from `ZIndex.Lib/Types.fs`.
3. `GenCORE.Lib/MinMax.fs` (Track B, its own `getMin`/`setMin`/`getMax`/`setMax` only;
   exported lens/prism values unchanged).
4. `ZForm.Lib/PatientCategory.fs` (Track B). Its `Optics` module is now 16 plain
   `set*` functions plus `setGender`.
5. `ZForm.Lib/DoseRule.fs` + `Dto.fs` + `GStand.fs` (Track A). Kept every composed optic
   value; only swapped the dispatch and operators. `Optic.fs` gained `>->` (Lens∘Lens),
   `>?>` (Prism∘Lens) and `>??>` (Prism∘Prism) as separate non-overloaded operators, since
   a plain function can't pick the optic kind the way Aether's SRTP `>->` does. The two
   Lens∘Prism sites (`minAbsLens`, `indDosDosagesLens`) call `Lens.composePrism` directly.
   `List.pos_` (Aether's list-index prism) moved to `Informedica.Utils.Lib/List.fs`.
6. `GenORDER.Lib/Patient.fs` (Track B). The one file wired into the live server
   mappers/services and the MCP tool surface.
7. `GenCORE.Lib/Patient.fs` (Track A: kept the `Optic` module, swapped Aether for the new
   one and `>->` for explicit `Lens.compose*`). Renamed the local `Aether.fs`
   (`Morphisms` module) to `Morphisms.fs`.
8. Removed `nuget Aether` from `paket.dependencies`/`paket.lock` (via `paket install`),
   the four `paket.references`, and the `#r "nuget: Aether"` lines in the `Scripts/`
   loaders.
