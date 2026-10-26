namespace Informedica.GenPRES.Client.Core.Helpers

/// Checks and formats the plain text the user types and reads.
module TextFormat =


    /// Whether the text is only digits, between the given lengths.
    let digits (min: int) (max: int) (s: string) =
        not (isNull s)
        && s.Length >= min
        && s.Length <= max
        && s |> Seq.forall (fun c -> c >= '0' && c <= '9')


    /// A moment as the user reads it: the local time of day.
    let time (at: System.DateTime) = at.ToLocalTime().ToString "HH:mm"
