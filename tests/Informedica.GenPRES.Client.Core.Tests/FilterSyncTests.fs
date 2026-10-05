namespace Informedica.GenPRES.Client.Core.Tests


/// A choice on the formulary or parenteralia page put on the prescribing context: written, the
/// diluent and the components kept only when the choices above them are unchanged, the
/// scenarios gone.
module FilterSyncTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open Shared.Models


    let held =
        { OrderContext.empty with
            Filter =
                { OrderContext.empty.Filter with
                    Indication = Some "pijn"
                    Generic = Some "morfine"
                    Route = Some "INTRAVENEUS"
                    Form = Some "injectievloeistof"
                    DoseType = Some(DoseType.Continuous "")
                    Diluent = Some "gluc 5%"
                    SelectedComponents = [| "morfine" |]
                }
        }


    let formulary =
        { Formulary.empty with
            Indication = held.Filter.Indication
            Generic = held.Filter.Generic
            Route = held.Filter.Route
            Form = held.Filter.Form
            DoseType = held.Filter.DoseType
        }


    let parenteralia =
        { Parenteralia.empty with
            Generic = held.Filter.Generic
            Route = held.Filter.Route
            Form = held.Filter.Form
        }


    [<Tests>]
    let tests =
        testList
            "FilterSync"
            [
                test "the formulary's same choices keep the diluent and the components" {
                    let ctx = held |> FilterSync.syncFormularyToFilter formulary

                    ctx.Filter.Diluent |> Expect.equal "the diluent kept" held.Filter.Diluent
                    ctx.Filter.SelectedComponents
                    |> Expect.equal "the components kept" [| "morfine" |]
                }

                test "another indication on the formulary lets go of the diluent and the components" {
                    let ctx =
                        held
                        |> FilterSync.syncFormularyToFilter { formulary with Indication = Some "sedatie" }

                    ctx.Filter.Indication |> Expect.equal "the indication written" (Some "sedatie")
                    ctx.Filter.Diluent |> Expect.isNone "the diluent goes"
                    ctx.Filter.SelectedComponents |> Expect.isEmpty "the components go"
                }

                test "the parenteralia page clears the indication and the dose type, keeps the diluent" {
                    let ctx = held |> FilterSync.syncParenteraliaToFilter parenteralia

                    ctx.Filter.Indication |> Expect.isNone "the indication cleared"
                    ctx.Filter.DoseType |> Expect.isNone "the dose type cleared"
                    ctx.Filter.Diluent |> Expect.equal "the diluent kept" held.Filter.Diluent
                }

                test "another route on the parenteralia page lets go of the diluent and the components" {
                    let ctx =
                        held
                        |> FilterSync.syncParenteraliaToFilter { parenteralia with Route = Some "ORAAL" }

                    ctx.Filter.Diluent |> Expect.isNone "the diluent goes"
                    ctx.Filter.SelectedComponents |> Expect.isEmpty "the components go"
                }
            ]
