namespace Informedica.Utils.Lib.Optics


/// A lens focuses on a part `'b` of a whole `'a` that is always present.
type Lens<'a, 'b> = ('a -> 'b) * ('b -> 'a -> 'a)


/// A prism focuses on a part `'b` of a whole `'a` that may or may not be present.
type Prism<'a, 'b> = ('a -> 'b option) * ('b -> 'a -> 'a)


/// A total, bidirectional conversion between `'a` and `'b`.
type Isomorphism<'a, 'b> = ('a -> 'b) * ('b -> 'a)


/// <summary>
/// Functions to get and set the value a `Lens` or `Prism` focuses on.
/// </summary>
/// <remarks>
/// A non-SRTP, non-inline replacement for the parts of Aether this codebase
/// actually uses. See issue #450: Aether's cross-assembly inline SRTP dispatch
/// broke under SDK 10.0.400 (dotnet/fsharp#20253). `Lens`/`Prism` here are the
/// same plain tuple shapes Aether uses, so existing lens/prism *values* defined
/// elsewhere as `('a -> 'b) * ('b -> 'a -> 'a)` need no changes; only the
/// dispatch functions below replace Aether's `Optic.get`/`Optic.set` and the
/// `Aether.Operators` `>->`/`>?>` operators.
/// </remarks>
[<RequireQualifiedAccess>]
module Optic =

    /// Get the value a `Lens` focuses on.
    let get ((getter, _): Lens<'a, 'b>) (a: 'a) : 'b = getter a

    /// Get the value a `Prism` focuses on, if present.
    let getOpt ((getter, _): Prism<'a, 'b>) (a: 'a) : 'b option = getter a

    /// Set the value a `Lens` or `Prism` focuses on. The setter shape
    /// (`'b -> 'a -> 'a`) is identical for both, so one function covers either
    /// without needing SRTP/inline dispatch to pick between them.
    let set (optic: 'get * ('b -> 'a -> 'a)) (v: 'b) (a: 'a) : 'a = (snd optic) v a


/// Functions to compose a `Lens` with another optic.
[<RequireQualifiedAccess>]
module Lens =

    /// Compose a `Lens` with a `Lens`, giving a `Lens`.
    let composeLens ((g1, s1): Lens<'a, 'b>) ((g2, s2): Lens<'b, 'c>) : Lens<'a, 'c> =
        (g1 >> g2), (fun c a -> s1 (s2 c (g1 a)) a)

    /// Compose a `Lens` with a `Prism`, giving a `Prism`.
    let composePrism ((g1, s1): Lens<'a, 'b>) ((g2, s2): Prism<'b, 'c>) : Prism<'a, 'c> =
        (fun a -> g2 (g1 a)), (fun c a -> s1 (s2 c (g1 a)) a)

    /// Compose a `Lens` with an `Isomorphism`, giving a `Lens`.
    let composeIso ((g1, s1): Lens<'a, 'b>) ((f, t): Isomorphism<'b, 'c>) : Lens<'a, 'c> =
        (fun a -> f (g1 a)), (fun c a -> s1 (t c) a)


/// Functions to compose a `Prism` with another optic.
[<RequireQualifiedAccess>]
module Prism =

    /// Compose a `Prism` with a `Lens`, giving a `Prism`.
    let composeLens ((g1, s1): Prism<'a, 'b>) ((g2, s2): Lens<'b, 'c>) : Prism<'a, 'c> =
        (fun a -> Option.map g2 (g1 a)),
        (fun c a ->
            match Option.map (s2 c) (g1 a) with
            | Some b -> s1 b a
            | None -> a
        )

    /// Compose a `Prism` with a `Prism`, giving a `Prism`.
    let composePrism ((g1, s1): Prism<'a, 'b>) ((g2, s2): Prism<'b, 'c>) : Prism<'a, 'c> =
        (fun a -> Option.bind g2 (g1 a)),
        (fun c a ->
            match Option.map (s2 c) (g1 a) with
            | Some b -> s1 b a
            | None -> a
        )

    /// Compose a `Prism` with an `Isomorphism`, giving a `Prism`.
    let composeIso ((g1, s1): Prism<'a, 'b>) ((f, t): Isomorphism<'b, 'c>) : Prism<'a, 'c> =
        (fun a -> Option.map f (g1 a)), (fun c a -> s1 (t c) a)


/// Lenses onto the elements of a 2-tuple. Matches Aether's `Optics.fst_`/`Optics.snd_`
/// exactly, since `ZForm.Lib/DoseRule.fs` composes onto them directly (e.g.
/// `DoseRange.NormWeight_ >-> fst_`).
[<AutoOpen>]
module TupleOptics =

    /// Lens onto the first element of a 2-tuple.
    let fst_: Lens<'a * 'b, 'a> = fst, (fun a (_, b) -> a, b)

    /// Lens onto the second element of a 2-tuple.
    let snd_: Lens<'a * 'b, 'b> = snd, (fun b (a, _) -> a, b)


/// Infix composition operators. Non-SRTP replacements for `Aether.Operators`.
/// Because these are plain (non-inline) functions they cannot overload on the
/// optic kind the way Aether's `>->`/`>?>` do, so the four composition shapes
/// this codebase uses get one operator each; the compiler rejects a wrong pick.
[<AutoOpen>]
module Operators =

    /// Compose a `Lens` with a `Lens`.
    let (>->) (l1: Lens<'a, 'b>) (l2: Lens<'b, 'c>) : Lens<'a, 'c> = Lens.composeLens l1 l2

    /// Compose a `Prism` with a `Lens`.
    let (>?>) (p: Prism<'a, 'b>) (l: Lens<'b, 'c>) : Prism<'a, 'c> = Prism.composeLens p l

    /// Compose a `Prism` with a `Prism`.
    let (>??>) (p1: Prism<'a, 'b>) (p2: Prism<'b, 'c>) : Prism<'a, 'c> = Prism.composePrism p1 p2
