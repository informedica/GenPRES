namespace Informedica.GenForm.Lib


/// The rows of the "LinkRules" sheet read, checked against the dose rules, and matched against an
/// order.
module LinkRule =

    open Informedica.Utils.Lib.BCL


    /// Whether a row of the sheet is blank: both dose types are empty, so the row is no rule. Text in
    /// the other cells of a blank row is ignored, as the RuleSets sheet ignores a row without a category.
    let isBlank (d: LinkRuleData) =
        String.isNullOrWhiteSpace d.DoseType && String.isNullOrWhiteSpace d.MainDoseType


    /// A side of a row read, with the name of its dose type column in the reasons. Refused when the
    /// dose type is empty or unknown, or is once or onceTimed: a once dose is never linked.
    let side doseTypeColumn generic route indication doseType doseText =
        match DoseType.parse doseType doseText with
        | NoDoseType, warning ->
            let reason = warning |> Option.defaultValue "the dose type is empty"
            Error $"%s{doseTypeColumn}: %s{reason}"
        | Once _, _
        | OnceTimed _, _ -> Error $"%s{doseTypeColumn}: a once dose is never linked"
        | dt, _ ->
            Ok
                {
                    LinkSide.Generic = generic
                    LinkSide.Route = route
                    LinkSide.Indication = indication
                    LinkSide.DoseType = dt
                }


    /// Whether the linked order needs its main order: yes is required; no or empty is optional.
    let requirement s =
        match s |> String.toLower with
        | "yes" -> Ok LinkRequirement.Required
        | "no"
        | "" -> Ok LinkRequirement.Optional
        | _ -> Error $"Required: %s{s} is not yes or no"


    /// A row read, or every reason it is refused.
    let ofData (d: LinkRuleData) =
        let errorOf =
            function
            | Error e -> [ e ]
            | Ok _ -> []

        let linked = side "DoseType" d.Generic d.Route d.Indication d.DoseType d.DoseText

        let main = side "MainDoseType" d.MainGeneric d.MainRoute d.MainIndication d.MainDoseType d.MainDoseText

        let required = d.Required |> requirement

        match linked, main, required with
        | Ok l, Ok m, Ok r ->
            Ok
                {
                    LinkRule.Linked = l
                    LinkRule.Main = m
                    LinkRule.Requirement = r
                }
        | _ -> Error(errorOf linked @ errorOf main @ errorOf required)


    /// Every row that is not blank, with its number in the sheet, the header being row 1. The number
    /// assumes that the export of the sheet keeps the blank rows between the rows, as quoted empty cells.
    let numbered (rows: LinkRuleData[]) =
        rows
        |> Array.indexed
        |> Array.filter (snd >> isBlank >> not)
        |> Array.map (fun (i, d) -> i + 2, d)


    /// Every row that is not blank read, with its number in the sheet.
    let read rows = rows |> numbered |> Array.map (fun (no, d) -> no, d |> ofData)


    /// The rows read, or every refused row with its number in the sheet. One refused row refuses the
    /// sheet: a required link that is left out would let its order be prescribed alone.
    let ofRows (rows: LinkRuleData[]) =
        let read = rows |> read

        match
            read
            |> Array.toList
            |> List.collect (fun (no, r) ->
                match r with
                | Ok _ -> []
                | Error es -> es |> List.map (fun e -> ErrorMsg($"LinkRules row %i{no}: %s{e}", None))
            )
        with
        | [] -> read |> Array.choose (snd >> Result.toOption) |> Ok
        | es -> Error es


    /// A field of a side as an option: a blank field gives none.
    let fieldOf (field: string) =
        if field |> String.isNullOrWhiteSpace then
            None
        else
            Some field


    /// Whether a field of a side matches a value: an empty field matches any value, and names compare
    /// without regard to case.
    let fieldMatches (field: string) (value: string) =
        field |> String.isNullOrWhiteSpace || String.equalsCapInsens field value


    /// Whether the dose type of a side matches a dose type: the same kind, and the same text unless
    /// the side has none.
    let doseTypeMatches (side: DoseType) (doseType: DoseType) =
        DoseType.toCategory side = DoseType.toCategory doseType
        && fieldMatches (DoseType.getText side) (DoseType.getText doseType)


    /// Whether a side matches an order with the indication, the generic label, the route and the dose
    /// type. The routes compare by the route mapping, so a long and a short name of a route match.
    let matches routeMapping (side: LinkSide) indication label route doseType =
        fieldMatches side.Indication indication
        && fieldMatches side.Generic label
        && (route |> Mapping.eqsRoute routeMapping (fieldOf side.Route))
        && doseTypeMatches side.DoseType doseType


    /// The warnings for each side of a row that matches no dose rule, since no order can match it. When
    /// a dose rule has the generic under its plain name, the warning gives the label to write. A refused
    /// row is reported by the read, not here.
    let check routeMapping (doseRules: RuleSet.DoseRuleNames[]) (rows: LinkRuleData[]) =
        let matchesRule (side: LinkSide) (d: RuleSet.DoseRuleNames) =
            matches routeMapping side d.Indication d.Label d.Route d.DoseType

        // the labels of the dose rules a side would match with the generic by its name
        let labelsOf (side: LinkSide) =
            doseRules
            |> Array.filter (fun d ->
                matchesRule { side with LinkSide.Generic = "" } d
                && String.equalsCapInsens d.Generic side.Generic
            )
            |> Array.map _.Label
            |> Array.distinct

        [
            for no, r in rows |> read do
                match r with
                | Error _ -> ()
                | Ok rule ->
                    for name, side in [ "linked", rule.Linked; "main", rule.Main ] do
                        if doseRules |> Array.exists (matchesRule side) |> not then
                            match labelsOf side with
                            | [||] -> Warning $"LinkRules: the %s{name} side of row %i{no} matches no dose rule"
                            | labels ->
                                let labels = labels |> String.concat ", "

                                Warning
                                    $"LinkRules: the %s{name} side of row %i{no} names %s{side.Generic}, offered as %s{labels}"
        ]


    /// The warnings of the rows against the dose rule data.
    let checkData routeMapping doseRules rows =
        rows |> check routeMapping (RuleSet.DoseRuleNames.ofData doseRules)
