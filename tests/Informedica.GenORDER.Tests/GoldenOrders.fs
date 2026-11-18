/// The orders the scenarios solve to, as they were when the Order was first built from the
/// Medication without a Dto (issue #831). Each is the printed order after the minimum and
/// maximum calculation, the increment increase, the value calculation and the solve.
///
/// A change here is a change in a dose. Regenerate them only deliberately, and say in the
/// commit what moved and why.
module GoldenOrders

open Informedica.GenOrder.Lib.Types


/// A scenario, and what it produced when it was recorded.
type GoldenOrder =
    {
        /// The name the scenario has in Scenarios
        Name: string
        /// The Medication the order is built from
        Medication: Medication
        /// The processing stages that reported an error, in the order they ran. Empty for an
        /// order that processes cleanly; a stage that starts or stops erroring is a change
        /// worth failing on, whichever way it goes.
        Errors: string list
        /// The printed order after the full processing
        Order: string
    }


/// Every scenario an order is recorded for. This is the one list: the builder tests take
/// their fixtures from it, so a scenario cannot be added to one and forgotten in the other.
let all: GoldenOrder list =
    [
        {
            Name = "pcmSupp"
            Medication = Scenarios.pcmSupp
            Errors = []
            Order =
                """
Route
RECTAAL
Schedule
[]_sch_frq [3;4 x/dag]
Orderable
[paracetamol]_ord_qty [1 stuk..1 stuk..>
[paracetamol]_orb_qty [1 stuk]
[paracetamol]_ord_cnt [1 x..1 x..>
[paracetamol]_dos_cnt [1 x]
[paracetamol]_dos_qty [1 stuk]
[paracetamol]_dos_ptm [3;4 stuk/dag]
[paracetamol]_dos_rte <0 ..>
[paracetamol]_dos_tot <0 ..>
[paracetamol]_dos_qty_adj [0,0714 stuk/kg]
[paracetamol]_dos_ptm_adj [0,214;0,286 stuk/kg/dag]
[paracetamol]_dos_rte_adj <0 ..>
[paracetamol]_dos_tot_adj <0 ..>
[paracetamol.paracetamol]_cmp_qty [1 stuk]
[paracetamol.paracetamol]_orb_qty [1 stuk]
[paracetamol.paracetamol]_orb_cnt [1 x]
[paracetamol.paracetamol]_orb_cnc [1 x]
[paracetamol.paracetamol]_ord_qty <0 stuk..>
[paracetamol.paracetamol]_ord_cnt <0 x..>
[paracetamol.paracetamol]_dos_qty [1 stuk]
[paracetamol.paracetamol]_dos_ptm [3;4 stuk/dag]
[paracetamol.paracetamol]_dos_rte <0 ..>
[paracetamol.paracetamol]_dos_tot <0 ..>
[paracetamol.paracetamol]_dos_qty_adj [0,0714 stuk/kg]
[paracetamol.paracetamol]_dos_ptm_adj [0,214;0,286 stuk/kg/dag]
[paracetamol.paracetamol]_dos_rte_adj <0 ..>
[paracetamol.paracetamol]_dos_tot_adj <0 ..>
[paracetamol.paracetamol.paracetamol]_cmp_qty [180;240;250 mg]
[paracetamol.paracetamol.paracetamol]_orb_qty [180;240;250 mg]
[paracetamol.paracetamol.paracetamol]_cmp_cnc [180;240;250 mg/stuk]
[paracetamol.paracetamol.paracetamol]_orb_cnc [180;240;250 mg/stuk]
[paracetamol.paracetamol.paracetamol]_dos_qty [180;240;250 mg]
[paracetamol.paracetamol.paracetamol]_dos_ptm [540;720;750;960;1 000 mg/dag]
[paracetamol.paracetamol.paracetamol]_dos_rte <0 ..>
[paracetamol.paracetamol.paracetamol]_dos_tot <0 ..>
[paracetamol.paracetamol.paracetamol]_dos_qty_adj [12,9;17,1;17,9 mg/kg]
[paracetamol.paracetamol.paracetamol]_dos_ptm_adj [38,6;51,4;53,6;68,6;71,4 mg/kg/dag]
[paracetamol.paracetamol.paracetamol]_dos_rte_adj <0 ..>
[paracetamol.paracetamol.paracetamol]_dos_tot_adj <0 ..>
[]_adj_qty [14 kg]
"""
        }

        {
            Name = "amfo"
            Medication = Scenarios.amfo
            Errors = []
            Order =
                """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1 x/dag]
Orderable
[amfotericine b liposomaal]_ord_qty [61 mL..>
[amfotericine b liposomaal]_orb_qty [61;62;63 mL .. 267;316;317 mL]
[amfotericine b liposomaal]_ord_cnt [1 x..1 x..>
[amfotericine b liposomaal]_dos_cnt [1 x]
[amfotericine b liposomaal]_dos_qty [61;62;63 mL .. 267;316;317 mL]
[amfotericine b liposomaal]_dos_ptm [61;62;63 mL/dag .. 267;316;317 mL/dag]
[amfotericine b liposomaal]_dos_rte <0 ..>
[amfotericine b liposomaal]_dos_tot <0 ..>
[amfotericine b liposomaal]_dos_qty_adj [4,36;4,43;4,5 mL/kg .. 19,1;22,6 mL/kg]
[amfotericine b liposomaal]_dos_ptm_adj [4,36;4,43;4,5 mL/kg/dag .. 19,1;22,6 mL/kg/dag]
[amfotericine b liposomaal]_dos_rte_adj <0 ..>
[amfotericine b liposomaal]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_cmp_qty [1 mL]
[amfotericine b liposomaal.amfotericine b liposomaal]_orb_qty [11;12;13;14;15;16;17 mL]
[amfotericine b liposomaal.amfotericine b liposomaal]_orb_cnt [11;12;13;14;15;16;17 x]
[amfotericine b liposomaal.amfotericine b liposomaal]_orb_cnc [0,0506;0,0521;0,053 x .. 0,231;0,242;0,254 x]
[amfotericine b liposomaal.amfotericine b liposomaal]_ord_qty <0 mL..>
[amfotericine b liposomaal.amfotericine b liposomaal]_ord_cnt <0 x..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty [11;12;13;14;15;16;17 mL]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm [11;12;13;14;15;16;17 mL/dag]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty_adj [0,786;0,857;0,929;1;1,07;1,14;1,21 mL/kg]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm_adj [0,786;0,857;0,929;1;1,07;1,14;1,21 mL/kg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_cmp_qty [72 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_orb_qty [792;864;936;1 008;1 080;1 152;1 224 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_cmp_cnc [72 mg/mL]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_orb_cnc [3,65;3,75;3,82 mg/mL .. 16,6;17,5;18,3 mg/mL]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_qty [792;864;936;1 008;1 080;1 152;1 224 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_ptm [792;864;936;1 008;1 080;1 152;1 224 mg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_rte <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_tot <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_qty_adj [56,6;61,7;66,9;72;77,1;82,3;87,4 mg/kg]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_ptm_adj [56,6;61,7;66,9;72;77,1;82,3;87,4 mg/kg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_cmp_qty [4 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_orb_qty [44;48;52;56;60;64;68 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_cmp_cnc [4 mg/mL]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_orb_cnc [0,203;0,209;0,212 mg/mL .. 0,923;0,97;1,01 mg/mL]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty [44;48;52;56;60;64;68 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm [44;48;52;56;60;64;68 mg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty_adj [3,14;3,43;3,71;4;4,29;4,57;4,86 mg/kg]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm_adj [3,14;3,43;3,71;4;4,29;4,57;4,86 mg/kg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.gluc 10%]_cmp_qty [1 mL]
[amfotericine b liposomaal.gluc 10%]_orb_qty [50;100;150;200;250;300 mL]
[amfotericine b liposomaal.gluc 10%]_orb_cnt [50;100;150;200;250;300 x]
[amfotericine b liposomaal.gluc 10%]_orb_cnc [0,746;0,758;0,769 x .. 0,947;0,948;0,949 x]
[amfotericine b liposomaal.gluc 10%]_ord_qty <0 mL..>
[amfotericine b liposomaal.gluc 10%]_ord_cnt <0 x..>
[amfotericine b liposomaal.gluc 10%]_dos_qty [50;100;150;200;250;300 mL]
[amfotericine b liposomaal.gluc 10%]_dos_ptm [50;100;150;200;250;300 mL/dag]
[amfotericine b liposomaal.gluc 10%]_dos_rte <0 ..>
[amfotericine b liposomaal.gluc 10%]_dos_tot <0 ..>
[amfotericine b liposomaal.gluc 10%]_dos_qty_adj [3,57;7,14;10,7;14,3;17,9;21,4 mL/kg]
[amfotericine b liposomaal.gluc 10%]_dos_ptm_adj [3,57;7,14;10,7;14,3;17,9;21,4 mL/kg/dag]
[amfotericine b liposomaal.gluc 10%]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.gluc 10%]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_cmp_qty [0,4 kCal]
[amfotericine b liposomaal.gluc 10%.energie]_orb_qty [20;40;60;80;100;120 kCal]
[amfotericine b liposomaal.gluc 10%.energie]_cmp_cnc [0,4 kCal/mL]
[amfotericine b liposomaal.gluc 10%.energie]_orb_cnc [0,299;0,303;0,308 kCal/mL .. 0,379;0,38 kCal/mL]
[amfotericine b liposomaal.gluc 10%.energie]_dos_qty [20;40;60;80;100;120 kCal]
[amfotericine b liposomaal.gluc 10%.energie]_dos_ptm [20;40;60;80;100;120 kCal/dag]
[amfotericine b liposomaal.gluc 10%.energie]_dos_rte <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_dos_tot <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_dos_qty_adj [1,43;2,86;4,29;5,71;7,14;8,57 kCal/kg]
[amfotericine b liposomaal.gluc 10%.energie]_dos_ptm_adj [1,43;2,86;4,29;5,71;7,14;8,57 kCal/kg/dag]
[amfotericine b liposomaal.gluc 10%.energie]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_orb_qty [5;10;15;20;25;30 g]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_orb_cnc [0,0746;0,0758;0,0769 g/mL .. 0,0947;0,0948;0,0949 g/mL]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_qty [5;10;15;20;25;30 g]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_ptm [5;10;15;20;25;30 g/dag]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_rte <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_tot <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_qty_adj [0,357;0,714;1,07;1,43;1,79;2,14 g/kg]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_ptm_adj [0,357;0,714;1,07;1,43;1,79;2,14 g/kg/dag]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [14 kg]
"""
        }

        {
            Name = "morfCont"
            Medication = Scenarios.morfCont
            Errors = []
            Order =
                """
Route
INTRAVENEUS
Schedule
[]_sch_tme [17,9;18,5;19,2 uur .. 55,6;62,5;71,4 uur]
Orderable
[morfine]_ord_qty [50 mL..50 mL..>
[morfine]_orb_qty [50 mL]
[morfine]_ord_cnt [1 x..1 x..>
[morfine]_dos_cnt [1 x]
[morfine]_dos_qty [50 mL]
[morfine]_dos_ptm <0 ..>
[morfine]_dos_rte [0,7;0,8;0,9 mL/uur .. 2,6;2,7;2,8 mL/uur]
[morfine]_dos_tot <0 ..>
[morfine]_dos_qty_adj [3,57 mL/kg]
[morfine]_dos_ptm_adj <0 ..>
[morfine]_dos_rte_adj [0,05;0,0571;0,0643 mL/kg/uur .. 0,186;0,193;0,2 mL/kg/uur]
[morfine]_dos_tot_adj <0 ..>
[morfine.morfine]_cmp_qty [1 mL]
[morfine.morfine]_orb_qty [1;10 mL]
[morfine.morfine]_orb_cnt [1;10 x]
[morfine.morfine]_orb_cnc [0,02;0,2 x]
[morfine.morfine]_ord_qty <0 mL..>
[morfine.morfine]_ord_cnt <0 x..>
[morfine.morfine]_dos_qty [1;10 mL]
[morfine.morfine]_dos_ptm <0 ..>
[morfine.morfine]_dos_rte [0,014;0,016;0,018 mL/uur .. 0,52;0,54;0,56 mL/uur]
[morfine.morfine]_dos_tot <0 ..>
[morfine.morfine]_dos_qty_adj [0,0714;0,714 mL/kg]
[morfine.morfine]_dos_ptm_adj <0 ..>
[morfine.morfine]_dos_rte_adj [0,001;0,00114;0,00129 mL/kg/uur .. 0,0371;0,0386;0,04 mL/kg/uur]
[morfine.morfine]_dos_tot_adj <0 ..>
[morfine.morfine.morfin]_cmp_qty [1;10 mg]
[morfine.morfine.morfin]_orb_qty [10 mg]
[morfine.morfine.morfin]_cmp_cnc [1;10 mg/mL]
[morfine.morfine.morfin]_orb_cnc [0,2 mg/mL]
[morfine.morfine.morfin]_dos_qty [10 mg]
[morfine.morfine.morfin]_dos_ptm <0 ..>
[morfine.morfine.morfin]_dos_rte [0,14;0,16;0,18 mg/uur .. 0,52;0,54;0,56 mg/uur]
[morfine.morfine.morfin]_dos_tot <0 ..>
[morfine.morfine.morfin]_dos_qty_adj [0,714 mg/kg]
[morfine.morfine.morfin]_dos_ptm_adj <0 ..>
[morfine.morfine.morfin]_dos_rte_adj [10;11,4;12,9 microg/kg/uur .. 37,1;38,6;40 microg/kg/uur]
[morfine.morfine.morfin]_dos_tot_adj <0 ..>
[morfine.gluc 10%]_cmp_qty [1 mL]
[morfine.gluc 10%]_orb_qty [40;49 mL]
[morfine.gluc 10%]_orb_cnt [40;49 x]
[morfine.gluc 10%]_orb_cnc [0,8;0,98 x]
[morfine.gluc 10%]_ord_qty <0 mL..>
[morfine.gluc 10%]_ord_cnt <0 x..>
[morfine.gluc 10%]_dos_qty [40;49 mL]
[morfine.gluc 10%]_dos_ptm <0 ..>
[morfine.gluc 10%]_dos_rte [0,56;0,64;0,686 mL/uur .. 2,55;2,65;2,74 mL/uur]
[morfine.gluc 10%]_dos_tot <0 ..>
[morfine.gluc 10%]_dos_qty_adj [2,86;3,5 mL/kg]
[morfine.gluc 10%]_dos_ptm_adj <0 ..>
[morfine.gluc 10%]_dos_rte_adj [0,04;0,0457;0,049 mL/kg/uur .. 0,182;0,189;0,196 mL/kg/uur]
[morfine.gluc 10%]_dos_tot_adj <0 ..>
[morfine.gluc 10%.energie]_cmp_qty [0,4 kCal]
[morfine.gluc 10%.energie]_orb_qty [16;19,6 kCal]
[morfine.gluc 10%.energie]_cmp_cnc [0,4 kCal/mL]
[morfine.gluc 10%.energie]_orb_cnc [0,32;0,392 kCal/mL]
[morfine.gluc 10%.energie]_dos_qty [16;19,6 kCal]
[morfine.gluc 10%.energie]_dos_ptm <0 ..>
[morfine.gluc 10%.energie]_dos_rte [0,224;0,256;0,274 kCal/uur .. 1,02;1,06;1,1 kCal/uur]
[morfine.gluc 10%.energie]_dos_tot <0 ..>
[morfine.gluc 10%.energie]_dos_qty_adj [1,14;1,4 kCal/kg]
[morfine.gluc 10%.energie]_dos_ptm_adj <0 ..>
[morfine.gluc 10%.energie]_dos_rte_adj [0,016;0,0183;0,0196 kCal/kg/uur .. 0,0728;0,0756;0,0784 kCal/kg/uur]
[morfine.gluc 10%.energie]_dos_tot_adj <0 ..>
[morfine.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[morfine.gluc 10%.koolhydraat]_orb_qty [4;4,9 g]
[morfine.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[morfine.gluc 10%.koolhydraat]_orb_cnc [0,08;0,098 g/mL]
[morfine.gluc 10%.koolhydraat]_dos_qty [4;4,9 g]
[morfine.gluc 10%.koolhydraat]_dos_ptm <0 ..>
[morfine.gluc 10%.koolhydraat]_dos_rte [0,056;0,064;0,0686 g/uur .. 0,255;0,265;0,274 g/uur]
[morfine.gluc 10%.koolhydraat]_dos_tot <0 ..>
[morfine.gluc 10%.koolhydraat]_dos_qty_adj [0,286;0,35 g/kg]
[morfine.gluc 10%.koolhydraat]_dos_ptm_adj <0 ..>
[morfine.gluc 10%.koolhydraat]_dos_rte_adj [0,004;0,00457;0,0049 g/kg/uur .. 0,0182;0,0189;0,0196 g/kg/uur]
[morfine.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [14 kg]
"""
        }

        {
            Name = "pcmDrink"
            Medication = Scenarios.pcmDrink
            Errors = []
            Order =
                """
Route
or
Schedule
[]_sch_frq [3;4;6 x/dag]
Orderable
[paracetamol drank]_ord_qty [5 mL..5;6;7;8;9;11 mL..>
[paracetamol drank]_orb_qty [5;6;7;8;9;10;11;12 mL]
[paracetamol drank]_ord_cnt [1 x..1 x..>
[paracetamol drank]_dos_cnt [1 x]
[paracetamol drank]_dos_qty [5;6;7;8;9;10;11;12 mL]
[paracetamol drank]_dos_ptm [27;28;30;32;33;36 mL/dag]
[paracetamol drank]_dos_rte <0 ..>
[paracetamol drank]_dos_tot <0 ..>
[paracetamol drank]_dos_qty_adj [0,5;0,6;0,7;0,8;0,9;1;1,1;1,2 mL/kg]
[paracetamol drank]_dos_ptm_adj [2,7;2,8;3;3,2;3,3;3,6 mL/kg/dag]
[paracetamol drank]_dos_rte_adj <0 ..>
[paracetamol drank]_dos_tot_adj <0 ..>
[paracetamol drank.paracetamol]_cmp_qty [5 mL]
[paracetamol drank.paracetamol]_orb_qty [5;6;7;8;9;10;11;12 mL]
[paracetamol drank.paracetamol]_orb_cnt [1;1,2;1,4;1,6;1,8;2;2,2;2,4 x]
[paracetamol drank.paracetamol]_orb_cnc [1 x]
[paracetamol drank.paracetamol]_ord_qty <0 mL..>
[paracetamol drank.paracetamol]_ord_cnt <0 x..>
[paracetamol drank.paracetamol]_dos_qty [5;6;7;8;9;10;11;12 mL]
[paracetamol drank.paracetamol]_dos_ptm [27;28;30;32;33;36 mL/dag]
[paracetamol drank.paracetamol]_dos_rte <0 ..>
[paracetamol drank.paracetamol]_dos_tot <0 ..>
[paracetamol drank.paracetamol]_dos_qty_adj [0,5;0,6;0,7;0,8;0,9;1;1,1;1,2 mL/kg]
[paracetamol drank.paracetamol]_dos_ptm_adj [2,7;2,8;3;3,2;3,3;3,6 mL/kg/dag]
[paracetamol drank.paracetamol]_dos_rte_adj <0 ..>
[paracetamol drank.paracetamol]_dos_tot_adj <0 ..>
[paracetamol drank.paracetamol.paracetamol]_cmp_qty [120 mg]
[paracetamol drank.paracetamol.paracetamol]_orb_qty [120;144;168;192;216;240;264;288 mg]
[paracetamol drank.paracetamol.paracetamol]_cmp_cnc [24 mg/mL]
[paracetamol drank.paracetamol.paracetamol]_orb_cnc [24 mg/mL]
[paracetamol drank.paracetamol.paracetamol]_dos_qty [120;144;168;192;216;240;264;288 mg]
[paracetamol drank.paracetamol.paracetamol]_dos_ptm [648;672;720;768;792;864 mg/dag]
[paracetamol drank.paracetamol.paracetamol]_dos_rte <0 ..>
[paracetamol drank.paracetamol.paracetamol]_dos_tot <0 ..>
[paracetamol drank.paracetamol.paracetamol]_dos_qty_adj [12;14,4;16,8;19,2;21,6;24;26,4;28,8 mg/kg]
[paracetamol drank.paracetamol.paracetamol]_dos_ptm_adj [64,8;67,2;72;76,8;79,2;86,4 mg/kg/dag]
[paracetamol drank.paracetamol.paracetamol]_dos_rte_adj <0 ..>
[paracetamol drank.paracetamol.paracetamol]_dos_tot_adj <0 ..>
[]_adj_qty [10 kg]
"""
        }

        {
            Name = "cotrim"
            Medication = Scenarios.cotrim
            Errors = []
            Order =
                """
Route
or
Schedule
[]_sch_frq [2 x/dag]
Orderable
[cotrimoxazol]_ord_qty [7 mL..7 mL..>
[cotrimoxazol]_orb_qty [7 mL]
[cotrimoxazol]_ord_cnt [1 x..1 x..>
[cotrimoxazol]_dos_cnt [1 x]
[cotrimoxazol]_dos_qty [7 mL]
[cotrimoxazol]_dos_ptm [14 mL/dag]
[cotrimoxazol]_dos_rte <0 ..>
[cotrimoxazol]_dos_tot <0 ..>
[cotrimoxazol]_dos_qty_adj [0,7 mL/kg]
[cotrimoxazol]_dos_ptm_adj [1,4 mL/kg/dag]
[cotrimoxazol]_dos_rte_adj <0 ..>
[cotrimoxazol]_dos_tot_adj <0 ..>
[cotrimoxazol.cotrimoxazol]_cmp_qty [1 mL]
[cotrimoxazol.cotrimoxazol]_orb_qty [7 mL]
[cotrimoxazol.cotrimoxazol]_orb_cnt [7 x]
[cotrimoxazol.cotrimoxazol]_orb_cnc [1 x]
[cotrimoxazol.cotrimoxazol]_ord_qty <0 mL..>
[cotrimoxazol.cotrimoxazol]_ord_cnt <0 x..>
[cotrimoxazol.cotrimoxazol]_dos_qty [7 mL]
[cotrimoxazol.cotrimoxazol]_dos_ptm [14 mL/dag]
[cotrimoxazol.cotrimoxazol]_dos_rte <0 ..>
[cotrimoxazol.cotrimoxazol]_dos_tot <0 ..>
[cotrimoxazol.cotrimoxazol]_dos_qty_adj [0,7 mL/kg]
[cotrimoxazol.cotrimoxazol]_dos_ptm_adj [1,4 mL/kg/dag]
[cotrimoxazol.cotrimoxazol]_dos_rte_adj <0 ..>
[cotrimoxazol.cotrimoxazol]_dos_tot_adj <0 ..>
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_cmp_qty [40 mg]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_orb_qty [280 mg]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_cmp_cnc [40 mg/mL]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_orb_cnc [40 mg/mL]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_qty [280 mg]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_ptm [560 mg/dag]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_rte <0 ..>
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_tot <0 ..>
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_qty_adj [28 mg/kg]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_ptm_adj [56 mg/kg/dag]
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_rte_adj <0 ..>
[cotrimoxazol.cotrimoxazol.sulfamethoxazol]_dos_tot_adj <0 ..>
[cotrimoxazol.cotrimoxazol.trimethoprim]_cmp_qty [8 mg]
[cotrimoxazol.cotrimoxazol.trimethoprim]_orb_qty [56 mg]
[cotrimoxazol.cotrimoxazol.trimethoprim]_cmp_cnc [8 mg/mL]
[cotrimoxazol.cotrimoxazol.trimethoprim]_orb_cnc [8 mg/mL]
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_qty [56 mg]
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_ptm [112 mg/dag]
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_rte <0 ..>
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_tot <0 ..>
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_qty_adj [5,6 mg/kg]
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_ptm_adj [11,2 mg/kg/dag]
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_rte_adj <0 ..>
[cotrimoxazol.cotrimoxazol.trimethoprim]_dos_tot_adj <0 ..>
[]_adj_qty [10 kg]
"""
        }

        {
            Name = "tpn"
            Medication = Scenarios.tpn
            Errors = []
            Order =
                """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1 x/dag]
[]_sch_tme [20 uur..24 uur]
Orderable
[samenstelling c]_ord_qty [380 mL..>
[samenstelling c]_orb_qty [380;381;382 mL .. 793;794;830 mL]
[samenstelling c]_ord_cnt [1 x..1 x..>
[samenstelling c]_dos_cnt [1 x]
[samenstelling c]_dos_qty [380;381;382 mL .. 793;794;830 mL]
[samenstelling c]_dos_ptm [380;381;382 mL/dag .. 793;794;830 mL/dag]
[samenstelling c]_dos_rte [15,9 mL/uur..0,1 mL/uur..41,5 mL/uur]
[samenstelling c]_dos_tot <0 ..>
[samenstelling c]_dos_qty_adj [34,5;34,6;34,7 mL/kg .. 72,1;72,2;75,5 mL/kg]
[samenstelling c]_dos_ptm_adj [34,5;34,6;34,7 mL/kg/dag .. 72,1;72,2;75,5 mL/kg/dag]
[samenstelling c]_dos_rte_adj [1,45 mL/kg/uur..0,00909 mL/kg/uur..3,77 mL/kg/uur]
[samenstelling c]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C]_cmp_qty [1 mL]
[samenstelling c.Samenstelling C]_orb_qty [150;200;250 mL]
[samenstelling c.Samenstelling C]_orb_cnt [150;200;250 x]
[samenstelling c.Samenstelling C]_orb_cnc [0,181;0,189 x .. 0,579;0,58;0,581 x]
[samenstelling c.Samenstelling C]_ord_qty <0 mL..>
[samenstelling c.Samenstelling C]_ord_cnt <0 x..>
[samenstelling c.Samenstelling C]_dos_qty [150;200;250 mL]
[samenstelling c.Samenstelling C]_dos_ptm [150;200;250 mL/dag]
[samenstelling c.Samenstelling C]_dos_rte [2,87 mL/uur..24,1 mL/uur]
[samenstelling c.Samenstelling C]_dos_tot <0 ..>
[samenstelling c.Samenstelling C]_dos_qty_adj [13,6;18,2;22,7 mL/kg]
[samenstelling c.Samenstelling C]_dos_ptm_adj [13,6;18,2;22,7 mL/kg/dag]
[samenstelling c.Samenstelling C]_dos_rte_adj <0 ..>
[samenstelling c.Samenstelling C]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.eiwit]_cmp_qty [0,08 g]
[samenstelling c.Samenstelling C.eiwit]_orb_qty [12;16;20 g]
[samenstelling c.Samenstelling C.eiwit]_cmp_cnc [0,08 g/mL]
[samenstelling c.Samenstelling C.eiwit]_orb_cnc [0,0145;0,0151 g/mL .. 0,0463;0,0464;0,0465 g/mL]
[samenstelling c.Samenstelling C.eiwit]_dos_qty [12;16;20 g]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm [12;16;20 g/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte [0,23 g/uur..1,93 g/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.eiwit]_dos_qty_adj [1,09;1,45;1,82 g/kg]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm_adj [1,09;1,45;1,82 g/kg/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte_adj [0,0209 g/kg/uur..0,175 g/kg/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.natrium]_cmp_qty [0,01 mmol]
[samenstelling c.Samenstelling C.natrium]_orb_qty [1,5;2;2,5 mmol]
[samenstelling c.Samenstelling C.natrium]_cmp_cnc [0,01 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_orb_cnc [0,00181;0,00189 mmol/mL .. 0,00579;0,0058;0,00581 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_dos_qty [1,5;2;2,5 mmol]
[samenstelling c.Samenstelling C.natrium]_dos_ptm [1,5;2;2,5 mmol/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte [0,0287 mmol/uur..0,241 mmol/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.natrium]_dos_qty_adj [0,136;0,182;0,227 mmol/kg]
[samenstelling c.Samenstelling C.natrium]_dos_ptm_adj [0,136;0,182;0,227 mmol/kg/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte_adj [0,00261 mmol/kg/uur..0,0219 mmol/kg/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.kalium]_cmp_qty [0,02 mmol]
[samenstelling c.Samenstelling C.kalium]_orb_qty [3;4;5 mmol]
[samenstelling c.Samenstelling C.kalium]_cmp_cnc [0,02 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_orb_cnc [0,00361;0,00378 mmol/mL .. 0,0116 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_dos_qty [3;4;5 mmol]
[samenstelling c.Samenstelling C.kalium]_dos_ptm [3;4;5 mmol/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte [0,0575 mmol/uur..0,483 mmol/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.kalium]_dos_qty_adj [0,273;0,364;0,455 mmol/kg]
[samenstelling c.Samenstelling C.kalium]_dos_ptm_adj [0,273;0,364;0,455 mmol/kg/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte_adj [0,00522 mmol/kg/uur..0,0439 mmol/kg/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%]_cmp_qty [1 mL]
[samenstelling c.NaCl 3%]_orb_qty [60;65;70 mL]
[samenstelling c.NaCl 3%]_orb_cnt [60;65;70 x]
[samenstelling c.NaCl 3%]_orb_cnc [0,0723;0,0756;0,0757 x .. 0,183;0,184 x]
[samenstelling c.NaCl 3%]_ord_qty <0 mL..>
[samenstelling c.NaCl 3%]_ord_cnt <0 x..>
[samenstelling c.NaCl 3%]_dos_qty [60;65;70 mL]
[samenstelling c.NaCl 3%]_dos_ptm [60;65;70 mL/dag]
[samenstelling c.NaCl 3%]_dos_rte [1,15 mL/uur..7,64 mL/uur]
[samenstelling c.NaCl 3%]_dos_tot <0 ..>
[samenstelling c.NaCl 3%]_dos_qty_adj [5,45;5,91;6,36 mL/kg]
[samenstelling c.NaCl 3%]_dos_ptm_adj [5,45;5,91;6,36 mL/kg/dag]
[samenstelling c.NaCl 3%]_dos_rte_adj <0 ..>
[samenstelling c.NaCl 3%]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%.natrium]_cmp_qty [0,5 mmol]
[samenstelling c.NaCl 3%.natrium]_orb_qty [30;32,5;35 mmol]
[samenstelling c.NaCl 3%.natrium]_cmp_cnc [0,5 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_orb_cnc [0,0361;0,0378 mmol/mL .. 0,0916;0,0919;0,0921 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_dos_qty [30;32,5;35 mmol]
[samenstelling c.NaCl 3%.natrium]_dos_ptm [30;32,5;35 mmol/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte [0,575 mmol/uur..3,82 mmol/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot <0 ..>
[samenstelling c.NaCl 3%.natrium]_dos_qty_adj [2,73;2,95;3,18 mmol/kg]
[samenstelling c.NaCl 3%.natrium]_dos_ptm_adj [2,73;2,95;3,18 mmol/kg/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte_adj [0,0522 mmol/kg/uur..0,347 mmol/kg/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%]_cmp_qty [1 mL]
[samenstelling c.KCl 7,4%]_orb_qty [20;21;22;23;24 mL]
[samenstelling c.KCl 7,4%]_orb_cnt [20;21;22;23;24 x]
[samenstelling c.KCl 7,4%]_orb_cnc [0,0241 x..0,0632 x]
[samenstelling c.KCl 7,4%]_ord_qty <0 mL..>
[samenstelling c.KCl 7,4%]_ord_cnt <0 x..>
[samenstelling c.KCl 7,4%]_dos_qty [20;21;22;23;24 mL]
[samenstelling c.KCl 7,4%]_dos_ptm [20;21;22;23;24 mL/dag]
[samenstelling c.KCl 7,4%]_dos_rte [0,383 mL/uur..2,62 mL/uur]
[samenstelling c.KCl 7,4%]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%]_dos_qty_adj [1,82;1,91;2;2,09;2,18 mL/kg]
[samenstelling c.KCl 7,4%]_dos_ptm_adj [1,82;1,91;2;2,09;2,18 mL/kg/dag]
[samenstelling c.KCl 7,4%]_dos_rte_adj <0 ..>
[samenstelling c.KCl 7,4%]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%.kalium]_cmp_qty [1 mmol]
[samenstelling c.KCl 7,4%.kalium]_orb_qty [20;21;22;23;24 mmol]
[samenstelling c.KCl 7,4%.kalium]_cmp_cnc [1 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_orb_cnc [0,0241 mmol/mL..0,0632 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_dos_qty [20;21;22;23;24 mmol]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm [20;21;22;23;24 mmol/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte [0,383 mmol/uur..2,62 mmol/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%.kalium]_dos_qty_adj [1,82;1,91;2;2,09;2,18 mmol/kg]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm_adj [1,82;1,91;2;2,09;2,18 mmol/kg/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte_adj [0,0348 mmol/kg/uur..0,238 mmol/kg/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%]_cmp_qty [1 mL]
[samenstelling c.gluc 10%]_orb_qty [150;300;450;600 mL]
[samenstelling c.gluc 10%]_orb_cnt [150;300;450;600 x]
[samenstelling c.gluc 10%]_orb_cnc [0,181 x..0,723 x]
[samenstelling c.gluc 10%]_ord_qty <0 mL..>
[samenstelling c.gluc 10%]_ord_cnt <0 x..>
[samenstelling c.gluc 10%]_dos_qty [150;300;450;600 mL]
[samenstelling c.gluc 10%]_dos_ptm [150;300;450;600 mL/dag]
[samenstelling c.gluc 10%]_dos_rte [2,87 mL/uur..30 mL/uur]
[samenstelling c.gluc 10%]_dos_tot <0 ..>
[samenstelling c.gluc 10%]_dos_qty_adj [13,6;27,3;40,9;54,5 mL/kg]
[samenstelling c.gluc 10%]_dos_ptm_adj [13,6;27,3;40,9;54,5 mL/kg/dag]
[samenstelling c.gluc 10%]_dos_rte_adj <0 ..>
[samenstelling c.gluc 10%]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[samenstelling c.gluc 10%.koolhydraat]_orb_qty [15;30;45;60 g]
[samenstelling c.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_orb_cnc [0,0181 g/mL..0,0723 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_dos_qty [15;30;45;60 g]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm [15;30;45;60 g/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte [0,287 g/uur..3 g/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_dos_qty_adj [1,36;2,73;4,09;5,45 g/kg]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm_adj [1,36;2,73;4,09;5,45 g/kg/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte_adj [0,0261 g/kg/uur..0,273 g/kg/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [11 kg]
"""
        }

        {
            Name = "tpnComplete"
            Medication = Scenarios.tpnComplete
            Errors = []
            Order =
                """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1 x/dag]
[]_sch_tme [20 uur..24 uur]
Orderable
[samenstelling c]_ord_qty [380 mL..>
[samenstelling c]_orb_qty [380;381;382 mL .. 793;794;830 mL]
[samenstelling c]_ord_cnt [1 x..1 x..>
[samenstelling c]_dos_cnt [1 x]
[samenstelling c]_dos_qty [380;381;382 mL .. 793;794;830 mL]
[samenstelling c]_dos_ptm [380;381;382 mL/dag .. 793;794;830 mL/dag]
[samenstelling c]_dos_rte [15,9 mL/uur..0,1 mL/uur..41,5 mL/uur]
[samenstelling c]_dos_tot <0 ..>
[samenstelling c]_dos_qty_adj [34,5;34,6;34,7 mL/kg .. 72,1;72,2;75,5 mL/kg]
[samenstelling c]_dos_ptm_adj [34,5;34,6;34,7 mL/kg/dag .. 72,1;72,2;75,5 mL/kg/dag]
[samenstelling c]_dos_rte_adj [1,45 mL/kg/uur..0,00909 mL/kg/uur..3,77 mL/kg/uur]
[samenstelling c]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C]_cmp_qty [1 mL]
[samenstelling c.Samenstelling C]_orb_qty [150;200;250 mL]
[samenstelling c.Samenstelling C]_orb_cnt [150;200;250 x]
[samenstelling c.Samenstelling C]_orb_cnc [0,181;0,189 x .. 0,579;0,58;0,581 x]
[samenstelling c.Samenstelling C]_ord_qty <0 mL..>
[samenstelling c.Samenstelling C]_ord_cnt <0 x..>
[samenstelling c.Samenstelling C]_dos_qty [150;200;250 mL]
[samenstelling c.Samenstelling C]_dos_ptm [150;200;250 mL/dag]
[samenstelling c.Samenstelling C]_dos_rte [2,87 mL/uur..24,1 mL/uur]
[samenstelling c.Samenstelling C]_dos_tot <0 ..>
[samenstelling c.Samenstelling C]_dos_qty_adj [13,6;18,2;22,7 mL/kg]
[samenstelling c.Samenstelling C]_dos_ptm_adj [13,6;18,2;22,7 mL/kg/dag]
[samenstelling c.Samenstelling C]_dos_rte_adj <0 ..>
[samenstelling c.Samenstelling C]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.energie]_cmp_qty [0,32 kCal]
[samenstelling c.Samenstelling C.energie]_orb_qty [48;64;80 kCal]
[samenstelling c.Samenstelling C.energie]_cmp_cnc [0,32 kCal/mL]
[samenstelling c.Samenstelling C.energie]_orb_cnc [0,0578;0,0605 kCal/mL .. 0,185;0,186 kCal/mL]
[samenstelling c.Samenstelling C.energie]_dos_qty [48;64;80 kCal]
[samenstelling c.Samenstelling C.energie]_dos_ptm [48;64;80 kCal/dag]
[samenstelling c.Samenstelling C.energie]_dos_rte [0,92 kCal/uur..7,72 kCal/uur]
[samenstelling c.Samenstelling C.energie]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.energie]_dos_qty_adj [4,36;5,82;7,27 kCal/kg]
[samenstelling c.Samenstelling C.energie]_dos_ptm_adj [4,36;5,82;7,27 kCal/kg/dag]
[samenstelling c.Samenstelling C.energie]_dos_rte_adj [0,0836 kCal/kg/uur..0,702 kCal/kg/uur]
[samenstelling c.Samenstelling C.energie]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.eiwit]_cmp_qty [0,08 g]
[samenstelling c.Samenstelling C.eiwit]_orb_qty [12;16;20 g]
[samenstelling c.Samenstelling C.eiwit]_cmp_cnc [0,08 g/mL]
[samenstelling c.Samenstelling C.eiwit]_orb_cnc [0,0145;0,0151 g/mL .. 0,0463;0,0464;0,0465 g/mL]
[samenstelling c.Samenstelling C.eiwit]_dos_qty [12;16;20 g]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm [12;16;20 g/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte [0,23 g/uur..1,93 g/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.eiwit]_dos_qty_adj [1,09;1,45;1,82 g/kg]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm_adj [1,09;1,45;1,82 g/kg/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte_adj [0,0209 g/kg/uur..0,175 g/kg/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.natrium]_cmp_qty [0,01 mmol]
[samenstelling c.Samenstelling C.natrium]_orb_qty [1,5;2;2,5 mmol]
[samenstelling c.Samenstelling C.natrium]_cmp_cnc [0,01 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_orb_cnc [0,00181;0,00189 mmol/mL .. 0,00579;0,0058;0,00581 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_dos_qty [1,5;2;2,5 mmol]
[samenstelling c.Samenstelling C.natrium]_dos_ptm [1,5;2;2,5 mmol/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte [0,0287 mmol/uur..0,241 mmol/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.natrium]_dos_qty_adj [0,136;0,182;0,227 mmol/kg]
[samenstelling c.Samenstelling C.natrium]_dos_ptm_adj [0,136;0,182;0,227 mmol/kg/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte_adj [0,00261 mmol/kg/uur..0,0219 mmol/kg/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.kalium]_cmp_qty [0,02 mmol]
[samenstelling c.Samenstelling C.kalium]_orb_qty [3;4;5 mmol]
[samenstelling c.Samenstelling C.kalium]_cmp_cnc [0,02 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_orb_cnc [0,00361;0,00378 mmol/mL .. 0,0116 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_dos_qty [3;4;5 mmol]
[samenstelling c.Samenstelling C.kalium]_dos_ptm [3;4;5 mmol/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte [0,0575 mmol/uur..0,483 mmol/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.kalium]_dos_qty_adj [0,273;0,364;0,455 mmol/kg]
[samenstelling c.Samenstelling C.kalium]_dos_ptm_adj [0,273;0,364;0,455 mmol/kg/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte_adj [0,00522 mmol/kg/uur..0,0439 mmol/kg/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.calcium]_cmp_qty [0,03 mmol]
[samenstelling c.Samenstelling C.calcium]_orb_qty [4,5;6;7,5 mmol]
[samenstelling c.Samenstelling C.calcium]_cmp_cnc [0,03 mmol/mL]
[samenstelling c.Samenstelling C.calcium]_orb_cnc [0,00542;0,00567 mmol/mL .. 0,0174 mmol/mL]
[samenstelling c.Samenstelling C.calcium]_dos_qty [4,5;6;7,5 mmol]
[samenstelling c.Samenstelling C.calcium]_dos_ptm [4,5;6;7,5 mmol/dag]
[samenstelling c.Samenstelling C.calcium]_dos_rte [0,0862 mmol/uur..0,724 mmol/uur]
[samenstelling c.Samenstelling C.calcium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.calcium]_dos_qty_adj [0,409;0,545;0,682 mmol/kg]
[samenstelling c.Samenstelling C.calcium]_dos_ptm_adj [0,409;0,545;0,682 mmol/kg/dag]
[samenstelling c.Samenstelling C.calcium]_dos_rte_adj [0,00784 mmol/kg/uur..0,0658 mmol/kg/uur]
[samenstelling c.Samenstelling C.calcium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.fosfaat]_cmp_qty [0,02 mmol]
[samenstelling c.Samenstelling C.fosfaat]_orb_qty [3;4;5 mmol]
[samenstelling c.Samenstelling C.fosfaat]_cmp_cnc [0,02 mmol/mL]
[samenstelling c.Samenstelling C.fosfaat]_orb_cnc [0,00361;0,00378 mmol/mL .. 0,0116 mmol/mL]
[samenstelling c.Samenstelling C.fosfaat]_dos_qty [3;4;5 mmol]
[samenstelling c.Samenstelling C.fosfaat]_dos_ptm [3;4;5 mmol/dag]
[samenstelling c.Samenstelling C.fosfaat]_dos_rte [0,0575 mmol/uur..0,483 mmol/uur]
[samenstelling c.Samenstelling C.fosfaat]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.fosfaat]_dos_qty_adj [0,273;0,364;0,455 mmol/kg]
[samenstelling c.Samenstelling C.fosfaat]_dos_ptm_adj [0,273;0,364;0,455 mmol/kg/dag]
[samenstelling c.Samenstelling C.fosfaat]_dos_rte_adj [0,00522 mmol/kg/uur..0,0439 mmol/kg/uur]
[samenstelling c.Samenstelling C.fosfaat]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.magnesium]_cmp_qty [0,01 mmol]
[samenstelling c.Samenstelling C.magnesium]_orb_qty [1,5;2;2,5 mmol]
[samenstelling c.Samenstelling C.magnesium]_cmp_cnc [0,01 mmol/mL]
[samenstelling c.Samenstelling C.magnesium]_orb_cnc [0,00181;0,00189 mmol/mL .. 0,00579;0,0058;0,00581 mmol/mL]
[samenstelling c.Samenstelling C.magnesium]_dos_qty [1,5;2;2,5 mmol]
[samenstelling c.Samenstelling C.magnesium]_dos_ptm [1,5;2;2,5 mmol/dag]
[samenstelling c.Samenstelling C.magnesium]_dos_rte [0,0287 mmol/uur..0,241 mmol/uur]
[samenstelling c.Samenstelling C.magnesium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.magnesium]_dos_qty_adj [0,136;0,182;0,227 mmol/kg]
[samenstelling c.Samenstelling C.magnesium]_dos_ptm_adj [0,136;0,182;0,227 mmol/kg/dag]
[samenstelling c.Samenstelling C.magnesium]_dos_rte_adj [0,00261 mmol/kg/uur..0,0219 mmol/kg/uur]
[samenstelling c.Samenstelling C.magnesium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.chloor]_cmp_qty [0,07 mmol]
[samenstelling c.Samenstelling C.chloor]_orb_qty [10,5;14;17,5 mmol]
[samenstelling c.Samenstelling C.chloor]_cmp_cnc [0,07 mmol/mL]
[samenstelling c.Samenstelling C.chloor]_orb_cnc [0,0127;0,0132 mmol/mL .. 0,0405;0,0406;0,0407 mmol/mL]
[samenstelling c.Samenstelling C.chloor]_dos_qty [10,5;14;17,5 mmol]
[samenstelling c.Samenstelling C.chloor]_dos_ptm [10,5;14;17,5 mmol/dag]
[samenstelling c.Samenstelling C.chloor]_dos_rte [0,201 mmol/uur..1,69 mmol/uur]
[samenstelling c.Samenstelling C.chloor]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.chloor]_dos_qty_adj [0,955;1,27;1,59 mmol/kg]
[samenstelling c.Samenstelling C.chloor]_dos_ptm_adj [0,955;1,27;1,59 mmol/kg/dag]
[samenstelling c.Samenstelling C.chloor]_dos_rte_adj [0,0183 mmol/kg/uur..0,154 mmol/kg/uur]
[samenstelling c.Samenstelling C.chloor]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%]_cmp_qty [1 mL]
[samenstelling c.NaCl 3%]_orb_qty [60;65;70 mL]
[samenstelling c.NaCl 3%]_orb_cnt [60;65;70 x]
[samenstelling c.NaCl 3%]_orb_cnc [0,0723;0,0756;0,0757 x .. 0,183;0,184 x]
[samenstelling c.NaCl 3%]_ord_qty <0 mL..>
[samenstelling c.NaCl 3%]_ord_cnt <0 x..>
[samenstelling c.NaCl 3%]_dos_qty [60;65;70 mL]
[samenstelling c.NaCl 3%]_dos_ptm [60;65;70 mL/dag]
[samenstelling c.NaCl 3%]_dos_rte [1,15 mL/uur..7,64 mL/uur]
[samenstelling c.NaCl 3%]_dos_tot <0 ..>
[samenstelling c.NaCl 3%]_dos_qty_adj [5,45;5,91;6,36 mL/kg]
[samenstelling c.NaCl 3%]_dos_ptm_adj [5,45;5,91;6,36 mL/kg/dag]
[samenstelling c.NaCl 3%]_dos_rte_adj <0 ..>
[samenstelling c.NaCl 3%]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%.natrium]_cmp_qty [0,5 mmol]
[samenstelling c.NaCl 3%.natrium]_orb_qty [30;32,5;35 mmol]
[samenstelling c.NaCl 3%.natrium]_cmp_cnc [0,5 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_orb_cnc [0,0361;0,0378 mmol/mL .. 0,0916;0,0919;0,0921 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_dos_qty [30;32,5;35 mmol]
[samenstelling c.NaCl 3%.natrium]_dos_ptm [30;32,5;35 mmol/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte [0,575 mmol/uur..3,82 mmol/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot <0 ..>
[samenstelling c.NaCl 3%.natrium]_dos_qty_adj [2,73;2,95;3,18 mmol/kg]
[samenstelling c.NaCl 3%.natrium]_dos_ptm_adj [2,73;2,95;3,18 mmol/kg/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte_adj [0,0522 mmol/kg/uur..0,347 mmol/kg/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%.chloor]_cmp_qty [0,5 mmol]
[samenstelling c.NaCl 3%.chloor]_orb_qty [30;32,5;35 mmol]
[samenstelling c.NaCl 3%.chloor]_cmp_cnc [0,5 mmol/mL]
[samenstelling c.NaCl 3%.chloor]_orb_cnc [0,0361;0,0378 mmol/mL .. 0,0916;0,0919;0,0921 mmol/mL]
[samenstelling c.NaCl 3%.chloor]_dos_qty [30;32,5;35 mmol]
[samenstelling c.NaCl 3%.chloor]_dos_ptm [30;32,5;35 mmol/dag]
[samenstelling c.NaCl 3%.chloor]_dos_rte [0,575 mmol/uur..3,82 mmol/uur]
[samenstelling c.NaCl 3%.chloor]_dos_tot <0 ..>
[samenstelling c.NaCl 3%.chloor]_dos_qty_adj [2,73;2,95;3,18 mmol/kg]
[samenstelling c.NaCl 3%.chloor]_dos_ptm_adj [2,73;2,95;3,18 mmol/kg/dag]
[samenstelling c.NaCl 3%.chloor]_dos_rte_adj [0,0522 mmol/kg/uur..0,347 mmol/kg/uur]
[samenstelling c.NaCl 3%.chloor]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%]_cmp_qty [1 mL]
[samenstelling c.KCl 7,4%]_orb_qty [20;21;22;23;24 mL]
[samenstelling c.KCl 7,4%]_orb_cnt [20;21;22;23;24 x]
[samenstelling c.KCl 7,4%]_orb_cnc [0,0241 x..0,0632 x]
[samenstelling c.KCl 7,4%]_ord_qty <0 mL..>
[samenstelling c.KCl 7,4%]_ord_cnt <0 x..>
[samenstelling c.KCl 7,4%]_dos_qty [20;21;22;23;24 mL]
[samenstelling c.KCl 7,4%]_dos_ptm [20;21;22;23;24 mL/dag]
[samenstelling c.KCl 7,4%]_dos_rte [0,383 mL/uur..2,62 mL/uur]
[samenstelling c.KCl 7,4%]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%]_dos_qty_adj [1,82;1,91;2;2,09;2,18 mL/kg]
[samenstelling c.KCl 7,4%]_dos_ptm_adj [1,82;1,91;2;2,09;2,18 mL/kg/dag]
[samenstelling c.KCl 7,4%]_dos_rte_adj <0 ..>
[samenstelling c.KCl 7,4%]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%.kalium]_cmp_qty [1 mmol]
[samenstelling c.KCl 7,4%.kalium]_orb_qty [20;21;22;23;24 mmol]
[samenstelling c.KCl 7,4%.kalium]_cmp_cnc [1 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_orb_cnc [0,0241 mmol/mL..0,0632 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_dos_qty [20;21;22;23;24 mmol]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm [20;21;22;23;24 mmol/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte [0,383 mmol/uur..2,62 mmol/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%.kalium]_dos_qty_adj [1,82;1,91;2;2,09;2,18 mmol/kg]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm_adj [1,82;1,91;2;2,09;2,18 mmol/kg/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte_adj [0,0348 mmol/kg/uur..0,238 mmol/kg/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%.chloor]_cmp_qty [1 mmol]
[samenstelling c.KCl 7,4%.chloor]_orb_qty [20;21;22;23;24 mmol]
[samenstelling c.KCl 7,4%.chloor]_cmp_cnc [1 mmol/mL]
[samenstelling c.KCl 7,4%.chloor]_orb_cnc [0,0241 mmol/mL..0,0632 mmol/mL]
[samenstelling c.KCl 7,4%.chloor]_dos_qty [20;21;22;23;24 mmol]
[samenstelling c.KCl 7,4%.chloor]_dos_ptm [20;21;22;23;24 mmol/dag]
[samenstelling c.KCl 7,4%.chloor]_dos_rte [0,383 mmol/uur..2,62 mmol/uur]
[samenstelling c.KCl 7,4%.chloor]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%.chloor]_dos_qty_adj [1,82;1,91;2;2,09;2,18 mmol/kg]
[samenstelling c.KCl 7,4%.chloor]_dos_ptm_adj [1,82;1,91;2;2,09;2,18 mmol/kg/dag]
[samenstelling c.KCl 7,4%.chloor]_dos_rte_adj [0,0348 mmol/kg/uur..0,238 mmol/kg/uur]
[samenstelling c.KCl 7,4%.chloor]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%]_cmp_qty [1 mL]
[samenstelling c.gluc 10%]_orb_qty [150;300;450;600 mL]
[samenstelling c.gluc 10%]_orb_cnt [150;300;450;600 x]
[samenstelling c.gluc 10%]_orb_cnc [0,181 x..0,723 x]
[samenstelling c.gluc 10%]_ord_qty <0 mL..>
[samenstelling c.gluc 10%]_ord_cnt <0 x..>
[samenstelling c.gluc 10%]_dos_qty [150;300;450;600 mL]
[samenstelling c.gluc 10%]_dos_ptm [150;300;450;600 mL/dag]
[samenstelling c.gluc 10%]_dos_rte [2,87 mL/uur..30 mL/uur]
[samenstelling c.gluc 10%]_dos_tot <0 ..>
[samenstelling c.gluc 10%]_dos_qty_adj [13,6;27,3;40,9;54,5 mL/kg]
[samenstelling c.gluc 10%]_dos_ptm_adj [13,6;27,3;40,9;54,5 mL/kg/dag]
[samenstelling c.gluc 10%]_dos_rte_adj <0 ..>
[samenstelling c.gluc 10%]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%.energie]_cmp_qty [0,4 kCal]
[samenstelling c.gluc 10%.energie]_orb_qty [60;120;180;240 kCal]
[samenstelling c.gluc 10%.energie]_cmp_cnc [0,4 kCal/mL]
[samenstelling c.gluc 10%.energie]_orb_cnc [0,0723 kCal/mL..0,289 kCal/mL]
[samenstelling c.gluc 10%.energie]_dos_qty [60;120;180;240 kCal]
[samenstelling c.gluc 10%.energie]_dos_ptm [60;120;180;240 kCal/dag]
[samenstelling c.gluc 10%.energie]_dos_rte [1,15 kCal/uur..12 kCal/uur]
[samenstelling c.gluc 10%.energie]_dos_tot <0 ..>
[samenstelling c.gluc 10%.energie]_dos_qty_adj [5,45;10,9;16,4;21,8 kCal/kg]
[samenstelling c.gluc 10%.energie]_dos_ptm_adj [5,45;10,9;16,4;21,8 kCal/kg/dag]
[samenstelling c.gluc 10%.energie]_dos_rte_adj [0,104 kCal/kg/uur..1,09 kCal/kg/uur]
[samenstelling c.gluc 10%.energie]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[samenstelling c.gluc 10%.koolhydraat]_orb_qty [15;30;45;60 g]
[samenstelling c.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_orb_cnc [0,0181 g/mL..0,0723 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_dos_qty [15;30;45;60 g]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm [15;30;45;60 g/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte [0,287 g/uur..3 g/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_dos_qty_adj [1,36;2,73;4,09;5,45 g/kg]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm_adj [1,36;2,73;4,09;5,45 g/kg/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte_adj [0,0261 g/kg/uur..0,273 g/kg/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [11 kg]
"""
        }

        {
            Name = "fullMedication"
            Medication = Scenarios.fullMedication
            Errors = []
            Order =
                """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1;2 x/dag]
[]_sch_tme [30 min..120 min]
Orderable
[Test Medication Complete]_ord_qty [50 mL..50 mL..>
[Test Medication Complete]_orb_qty [50 mL]
[Test Medication Complete]_ord_cnt [1 x..1 x..>
[Test Medication Complete]_dos_cnt [5;5,56;6,25;7,14;8,33;10 x]
[Test Medication Complete]_dos_qty [5;6;7;8;9;10 mL]
[Test Medication Complete]_dos_ptm [8;9;10;12;14;16;18;20 mL/dag]
[Test Medication Complete]_dos_rte [2,5 mL/uur..0,1 mL/uur..20 mL/uur]
[Test Medication Complete]_dos_tot <0 ..>
[Test Medication Complete]_dos_qty_adj [0,333;0,4;0,467;0,533;0,6;0,667 mL/kg]
[Test Medication Complete]_dos_ptm_adj [0,533;0,6;0,667;0,8;0,933;1,07;1,2;1,33 mL/kg/dag]
[Test Medication Complete]_dos_rte_adj [0,167 mL/kg/uur..0,00667 mL/kg/uur..0,0222 mL/kg/min]
[Test Medication Complete]_dos_tot_adj <0 ..>
[Test Medication Complete.Main Component]_cmp_qty [1 mL]
[Test Medication Complete.Main Component]_orb_qty [10 mL]
[Test Medication Complete.Main Component]_orb_cnt [10 x]
[Test Medication Complete.Main Component]_orb_cnc [0,2 x]
[Test Medication Complete.Main Component]_ord_qty <0 mL..>
[Test Medication Complete.Main Component]_ord_cnt <0 x..>
[Test Medication Complete.Main Component]_dos_qty [1;1,2;1,4;1,6;1,8;2 mL]
[Test Medication Complete.Main Component]_dos_ptm [1,6;1,8;2;2,4;2,8;3,2;3,6;4 mL/dag]
[Test Medication Complete.Main Component]_dos_rte [0,5 mL/uur..0,02 mL/uur..0,0667 mL/min]
[Test Medication Complete.Main Component]_dos_tot <0 ..>
[Test Medication Complete.Main Component]_dos_qty_adj [0,0667;0,08;0,0933;0,107;0,12;0,133 mL/kg]
[Test Medication Complete.Main Component]_dos_ptm_adj [0,107;0,12;0,133;0,16;0,187;0,213;0,24;0,267 mL/kg/dag]
[Test Medication Complete.Main Component]_dos_rte_adj <0 ..>
[Test Medication Complete.Main Component]_dos_tot_adj <0 ..>
[Test Medication Complete.Main Component.Active Substance]_cmp_qty [10 mg]
[Test Medication Complete.Main Component.Active Substance]_orb_qty [100 mg]
[Test Medication Complete.Main Component.Active Substance]_cmp_cnc [10 mg/mL]
[Test Medication Complete.Main Component.Active Substance]_orb_cnc [2 mg/mL]
[Test Medication Complete.Main Component.Active Substance]_dos_qty [10;12;14;16;18;20 mg]
[Test Medication Complete.Main Component.Active Substance]_dos_ptm [16;18;20;24;28;32;36;40 mg/dag]
[Test Medication Complete.Main Component.Active Substance]_dos_rte [5 mg/uur..0,2 mg/uur..0,667 mg/min]
[Test Medication Complete.Main Component.Active Substance]_dos_tot <0 ..>
[Test Medication Complete.Main Component.Active Substance]_dos_qty_adj [0,667;0,8;0,933;1,07;1,2;1,33 mg/kg]
[Test Medication Complete.Main Component.Active Substance]_dos_ptm_adj [1,07;1,2;1,33;1,6;1,87;2,13;2,4;2,67 mg/kg/dag]
[Test Medication Complete.Main Component.Active Substance]_dos_rte_adj [0,333 mg/kg/uur..0,0133 mg/kg/uur..0,0444 mg/kg/min]
[Test Medication Complete.Main Component.Active Substance]_dos_tot_adj <0 ..>
[Test Medication Complete.Diluent Component]_cmp_qty [1 mL]
[Test Medication Complete.Diluent Component]_orb_qty [40 mL]
[Test Medication Complete.Diluent Component]_orb_cnt [40 x]
[Test Medication Complete.Diluent Component]_orb_cnc [0,8 x]
[Test Medication Complete.Diluent Component]_ord_qty <0 mL..>
[Test Medication Complete.Diluent Component]_ord_cnt <0 x..>
[Test Medication Complete.Diluent Component]_dos_qty [4;4,8;5,6;6,4;7,2;8 mL]
[Test Medication Complete.Diluent Component]_dos_ptm [6,4;7,2;8;9,6;11,2;12,8;14,4;16 mL/dag]
[Test Medication Complete.Diluent Component]_dos_rte [2 mL/uur..0,08 mL/uur..0,267 mL/min]
[Test Medication Complete.Diluent Component]_dos_tot <0 ..>
[Test Medication Complete.Diluent Component]_dos_qty_adj [0,267;0,32;0,373;0,427;0,48;0,533 mL/kg]
[Test Medication Complete.Diluent Component]_dos_ptm_adj [0,427;0,48;0,533;0,64;0,747;0,853;0,96;1,07 mL/kg/dag]
[Test Medication Complete.Diluent Component]_dos_rte_adj <0 ..>
[Test Medication Complete.Diluent Component]_dos_tot_adj <0 ..>
[Test Medication Complete.Diluent Component.natrium]_cmp_qty [0,154 mmol]
[Test Medication Complete.Diluent Component.natrium]_orb_qty [6,16 mmol]
[Test Medication Complete.Diluent Component.natrium]_cmp_cnc [0,154 mmol/mL]
[Test Medication Complete.Diluent Component.natrium]_orb_cnc [0,123 mmol/mL]
[Test Medication Complete.Diluent Component.natrium]_dos_qty [0,616;0,739;0,862;0,986;1,11;1,23 mmol]
[Test Medication Complete.Diluent Component.natrium]_dos_ptm [0,986;1,11;1,23;1,48;1,72;1,97;2,22;2,46 mmol/dag]
[Test Medication Complete.Diluent Component.natrium]_dos_rte [0,308 mmol/uur..0,0123 mmol/uur..0,0411 mmol/min]
[Test Medication Complete.Diluent Component.natrium]_dos_tot <0 ..>
[Test Medication Complete.Diluent Component.natrium]_dos_qty_adj [0,0411;0,0493;0,0575;0,0657;0,0739;0,0821 mmol/kg]
[Test Medication Complete.Diluent Component.natrium]_dos_ptm_adj [0,0657;0,0739;0,0821;0,0986;0,115;0,131;0,148;0,164 mmol/kg/dag]
[Test Medication Complete.Diluent Component.natrium]_dos_rte_adj [0,0205 mmol/kg/uur..0,000821 mmol/kg/uur..0,00274 mmol/kg/min]
[Test Medication Complete.Diluent Component.natrium]_dos_tot_adj <0 ..>
[]_adj_qty [15 kg]
"""
        }

    ]
