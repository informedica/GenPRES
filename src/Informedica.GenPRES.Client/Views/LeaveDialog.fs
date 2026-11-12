namespace Views


/// The question before a url with a patient, a medication or a launch: it leaves the launched
/// Session, or drops the work not signed. The browser asks the second itself for a reload or a
/// closed tab, but not for a change of the url.
module LeaveDialog =


    open Informedica.GenPRES.Client.Core.Models
    open Fable.Core
    open Informedica.GenPRES.Shared


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
                Terms.``Url Leave Session Title`` |> getTerm "Leave the session?"
            else
                Terms.``Url Leave Title`` |> getTerm "Leave the order plan?"

        let text =
            match props.launched, props.asksLaunch with
            | true, true ->
                Terms.``Url Leave Launch Text``
                |> getTerm
                    "The session is closed and the new session from the EHR is opened. New and changed orders and the medication being prescribed are lost. Do you want to continue?"
            | true, false ->
                Terms.``Url Leave Session Text``
                |> getTerm
                    "The session with the patient from the EHR is closed and the patient from the url is used, without a session. New and changed orders and the medication being prescribed are lost. Do you want to continue?"
            | false, _ ->
                Terms.``Url Leave Text``
                |> getTerm
                    "The new and changed orders and the medication being prescribed are lost. Do you want to continue?"

        Components.ConfirmDialog.View
            {|
                isOpen = props.isOpen
                title = title
                text = text
                confirmLabel = Terms.``Url Leave`` |> getTerm "Leave"
                cancelLabel = Terms.Cancel |> getTerm "Cancel"
                onConfirm = props.onConfirm
                onCancel = props.onCancel
            |}
