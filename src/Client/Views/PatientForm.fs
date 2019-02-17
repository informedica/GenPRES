namespace Views

module PatientForm =
    open System
    open Elmish
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes

    open GenPres
    open Components
    open Utils.Utils

    type Patient = Shared.Types.Patient.Patient

    type Model =
        { Patient : Patient Option
          Year : Select.Model
          Month : Select.Model
          Weight : Select.Model
          Height : Select.Model }

    type ResponseLoaded = Result<Shared.Types.Response.Response Option, exn>

    type Msg =
        | PatientLoaded of ResponseLoaded
        | ClearPatient 
        | YearChange of (bool *  Select.Msg)
        | MonthChange of (bool *  Select.Msg)
        | WeightChange of (bool *  Select.Msg)
        | HeightChange of (bool *  Select.Msg)

    let cmdOfPromise task =
        Cmd.ofPromise
            (fun () -> task)
            ()
            (Ok >> PatientLoaded)
            (Result.Error >> PatientLoaded)

    let getPatient msg =
        msg
        |> Shared.Types.Request.PatientMsg
        |> Utils.Request.post
        |> cmdOfPromise

    let nearestItem (sel : Select.Model) n  =
        sel.Items
        |> List.map (fun i -> i.Value)
        |> List.map Double.TryParse
        |> List.filter fst
        |> List.map snd
        |> Utils.Utils.List.findNearestMax n
        |> string

    let inline toMsg get msg pat =
        pat
        |> get
        |> int
        |> string
        |> Select.Select
        |> (fun s -> (false, s) |> msg)
        |> Cmd.ofMsg


    let processResponse model resp =
        match resp with
        | None -> model, Cmd.none
        | Some resp ->
            match resp with
            | Shared.Types.Response.Patient pat ->
                let yr =
                    pat
                    |> toMsg Domain.Patient.getAgeYears YearChange
                let mo =
                    pat
                    |> toMsg Domain.Patient.getAgeMonths MonthChange
                let wt =
                    pat
                    |> toMsg Domain.Patient.getWeight WeightChange
                let ht =
                    pat
                    |> toMsg Domain.Patient.getHeight HeightChange
                { model with Patient = Some pat }, Cmd.batch [ yr; mo; wt; ht ]
            | _ -> model, Cmd.none
            

    let init (yrs : int list) (mos : int list) (whts : float list) (hths : int list) =
        let model =
            { Patient = None
              Year = Select.init "Jaren" (yrs |> List.map string)
              Month = Select.init "Maanden" (mos |> List.map string)
              Weight = Select.init "Gewicht (kg)" (whts |> List.map string)
              Height = Select.init "Lengte (cm)" (hths |> List.map string) }

        let loadPatient = getPatient Shared.Types.Request.Patient.Init

        model, loadPatient

    let show pat =
        match pat with
        | Some p -> p |> Domain.Patient.show
        | None -> ""

    let setModelYear msg model = 
        let (Select.Select (yr)) = msg
        match  yr |> Int32.TryParse with
        | (true, n) ->
            { model with Year = Select.update msg model.Year
                         Patient = match model.Patient with
                                   | Some p ->
                                        
                                        { p with Age = { p.Age with Years = n } } |> Some
                                   | None -> None }
        | (false, _) -> model
        
    let setModelMonth msg model = 
        let (Select.Select (mo)) = msg
        match mo |> Int32.TryParse with
        | (true, n) ->
            { model with Month = Select.update msg model.Month
                         Patient = match model.Patient with
                                   | Some p ->
                                        { p with Age = { p.Age with Months = n } } |> Some
                                   | None -> None }
        | (false, _) -> model
        
    let setModelWeight msg model = 
        let (Select.Select (wt)) = msg
        match wt |> Double.TryParse with
        | (true, n) ->
            { model with Weight = Select.update msg model.Weight
                         Patient = match model.Patient with
                                   | Some p ->
                                        { p with Weight = { p.Weight with Measured = n } } |> Some
                                   | None -> None }
        | (false, _) -> model
        
    let setModelHeight msg model = 
        let (Select.Select (ht)) = msg
        match ht |> Double.TryParse with
        | (true, n) ->
            { model with Height = Select.update msg model.Height
                         Patient = match model.Patient with
                                   | Some p ->
                                        { p with Height = { p.Height with Measured = n } } |> Some
                                   | None -> None }
        | (false, _) -> model
        

    let update msg model =
        let change set calc msg model =
            let model = model |> set msg
            let cmd =
                if not calc then Cmd.none
                else
                    match model.Patient with
                    | Some pat -> 
                        getPatient (Shared.Types.Request.Patient.Calculate pat)
                    | None -> Cmd.none
            model, cmd

        match msg with
        | PatientLoaded (Ok resp) ->
            printfn "patient received: %A" resp
            resp |> processResponse model
        | PatientLoaded (Result.Error err) ->
            printfn "couldn't load patient: %s" err.Message
            model, Cmd.none
        | ClearPatient -> model, (getPatient Shared.Types.Request.Patient.Init)
        | YearChange (calc, msg) ->
            printfn "received a year change: %A" msg
            model |> change setModelYear calc msg
        | MonthChange (calc, msg) ->
            printfn "received a month change: %A" msg
            model |> change setModelMonth calc msg
        | WeightChange (calc, msg) ->
            printfn "received a weight change: %A" msg
            model |> change setModelWeight calc msg
        | HeightChange (calc, msg) ->
            printfn "received a height change: %A" msg
            model |> change setModelHeight calc msg

    let private styles (theme : ITheme) : IStyles list =
        [ Styles.Form [ CSSProp.Padding "20px"; CSSProp.Flex "1" ]
          Styles.Button
            [ CSSProp.FlexBasis "auto"
              CSSProp.Flex "1"
              CSSProp.MarginTop "10px"
              CSSProp.BackgroundColor Fable.MaterialUI.Colors.green.``50`` ]
          Styles.Paper [ CSSProp.MarginTop "60px" ]
          Styles.Custom ("show", [ CSSProp.PaddingTop "10px" ]) ]

    let private view' (classes : IClasses) model dispatch =
        let toMsg msg s =
            (true, s)
            |> msg
            |> dispatch
            
        paper [ Class classes?paper ]
            [ form [ Id "patientform"
                     Class classes?form ]
                [ formGroup [ FormGroupProp.Row true ]
                      [ Select.view model.Year (YearChange |> toMsg)
                        Select.view model.Month (MonthChange |> toMsg)
                        Select.view model.Weight (WeightChange |> toMsg)
                        Select.view model.Height (HeightChange |> toMsg) ]
                  div
                    [ Style [CSSProp.Display "flex" ]]
                    [ button
                        [ OnClick (fun _ -> ClearPatient |> dispatch)
                          ButtonProp.Variant ButtonVariant.Contained
                          Class classes?button ]
                        [ typography
                            [ TypographyProp.Color TypographyColor.Inherit
                              TypographyProp.Variant TypographyVariant.Body1 ]
                            [ str "verwijder" ] ] ] 
                  typography
                    [ Class classes?show
                      TypographyProp.Variant TypographyVariant.Subtitle2 ]
                    [ model.Patient |> show |> str ] ] ]

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract model : Model with get, set
        abstract dispatch : (Msg -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.model p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view (model : Model) (dispatch : Msg -> unit) : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.model <- model
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
