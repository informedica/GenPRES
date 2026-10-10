namespace Views


/// The question before a url with a patient, a medication or a launch: it leaves the launched
/// Session, or drops the work not signed. The browser asks the second itself for a reload or a
/// closed tab, but not for a change of the url.
module LeaveDialog =


    open Fable.Core
    open Shared


    /// The dialog; whether the Session is launched and whether the url asks for a launch pick
    /// its title and text.
    [<JSX.Component>]
    let View
        (props:
            {|
                isOpen: bool
                launched: bool
                asksLaunch: bool
                getTerm: string -> Terms -> string
                onConfirm: unit -> unit
                onCancel: unit -> unit
            |})
        =
        let getTerm = props.getTerm

        let title =
            if props.launched then
                Terms.``Url Leave Session Title`` |> getTerm "Sessie verlaten?"
            else
                Terms.``Url Leave Title`` |> getTerm "Orderplan verlaten?"

        let text =
            match props.launched, props.asksLaunch with
            | true, true ->
                Terms.``Url Leave Launch Text``
                |> getTerm
                    "De sessie wordt gesloten en de nieuwe sessie uit het EPD wordt geopend. Nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"
            | true, false ->
                Terms.``Url Leave Session Text``
                |> getTerm
                    "De sessie met de patiënt uit het EPD wordt gesloten en de patiënt uit de url wordt gebruikt, zonder sessie. Nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"
            | false, _ ->
                Terms.``Url Leave Text``
                |> getTerm
                    "De nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"

        Components.ConfirmDialog.View
            {|
                isOpen = props.isOpen
                title = title
                text = text
                confirmLabel = Terms.``Url Leave`` |> getTerm "Verlaten"
                cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                onConfirm = props.onConfirm
                onCancel = props.onCancel
            |}
