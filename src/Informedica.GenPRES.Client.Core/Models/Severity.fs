namespace Informedica.GenPRES.Client.Core.Models

/// How far what is shown stands from what the rules allow: nothing, a note, a warning, an
/// alert. The cases are declared from lowest to highest, so the highest of a set is their
/// maximum. Level and TextBlock carry it on the wire; the Severity module converts.
[<RequireQualifiedAccess>]
type Severity =
    | Normal
    | Caution
    | Warning
    | Alert


/// Conversions between the one severity and the two shapes the wire carries it in.
[<RequireQualifiedAccess>]
module Severity =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// The severity an order variable carries.
    let ofLevel (level: Level) =
        match level with
        | IsNormal -> Severity.Normal
        | IsCaution -> Severity.Caution
        | IsWarning -> Severity.Warning
        | IsAlert -> Severity.Alert


    /// The level an order variable carries for a severity.
    let toLevel (severity: Severity) =
        match severity with
        | Severity.Normal -> IsNormal
        | Severity.Caution -> IsCaution
        | Severity.Warning -> IsWarning
        | Severity.Alert -> IsAlert


    /// The severity a text block carries.
    let ofTextBlock (block: TextBlock) =
        match block with
        | Valid _ -> Severity.Normal
        | Caution _ -> Severity.Caution
        | Warning _ -> Severity.Warning
        | Alert _ -> Severity.Alert


    /// The text a text block carries, whatever its severity.
    let items (block: TextBlock) =
        match block with
        | Valid items
        | Caution items
        | Warning items
        | Alert items -> items


    /// The text block of a severity over some text.
    let withItems (severity: Severity) (items: TextItem[]) =
        match severity with
        | Severity.Normal -> Valid items
        | Severity.Caution -> Caution items
        | Severity.Warning -> Warning items
        | Severity.Alert -> Alert items


    /// The highest of some severities; nothing raised when there are none.
    let highest (severities: Severity seq) = severities |> Seq.fold max Severity.Normal


    /// The highest severity among some text blocks.
    let ofTextBlocks (blocks: TextBlock[]) = blocks |> Seq.map ofTextBlock |> highest


    /// The highest severity among rows of text blocks; an empty row counts as nothing raised.
    let ofTextBlockRows (rows: TextBlock[][]) = rows |> Seq.collect (Seq.map ofTextBlock) |> highest


    /// Whether a severity is anything above normal: what gets a mark.
    let isRaised (severity: Severity) = severity <> Severity.Normal
