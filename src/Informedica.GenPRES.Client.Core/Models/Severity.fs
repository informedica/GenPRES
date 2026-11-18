namespace Informedica.GenPRES.Client.Core.Models

/// How far a value is from what the rules allow, from Normal to Alert. The cases are ordered from
/// lowest to highest, so max gives the highest. The wire carries a severity as a Level or a TextBlock.
[<RequireQualifiedAccess>]
type Severity =
    | Normal
    | Caution
    | Warning
    | Alert


/// Converts a severity to and from the Level and TextBlock types of the wire.
[<RequireQualifiedAccess>]
module Severity =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// Returns the severity of the level of an order variable.
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


    /// Returns the text items of a text block of any severity.
    let items (block: TextBlock) =
        match block with
        | Valid items
        | Caution items
        | Warning items
        | Alert items -> items


    /// Wraps text items in the text block of a severity.
    let withItems (severity: Severity) (items: TextItem[]) =
        match severity with
        | Severity.Normal -> Valid items
        | Severity.Caution -> Caution items
        | Severity.Warning -> Warning items
        | Severity.Alert -> Alert items


    /// Returns the highest severity, or Normal when there are none.
    let highest (severities: Severity seq) = severities |> Seq.fold max Severity.Normal


    /// The highest severity among some text blocks.
    let ofTextBlocks (blocks: TextBlock[]) = blocks |> Seq.map ofTextBlock |> highest


    /// Returns the highest severity in rows of text blocks, or Normal when there are none.
    let ofTextBlockRows (rows: TextBlock[][]) = rows |> Seq.collect (Seq.map ofTextBlock) |> highest


    /// Whether a severity is above Normal, so the value gets a mark.
    let isRaised (severity: Severity) = severity <> Severity.Normal
