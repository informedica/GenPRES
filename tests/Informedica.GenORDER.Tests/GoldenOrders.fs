/// The orders the scenarios solve to, as they were when the Order was first built from the
/// Medication without a Dto (issue #831). They are the record of what the builder produces:
/// the printed order after the minimum and maximum calculation, the increment increase, the
/// value calculation and the solve.
///
/// A change here is a change in a dose. Regenerate them only deliberately, and say in the
/// commit what moved and why.
module GoldenOrders


/// Every fixture's solved order, by the name it has in Scenarios.
let all: (string * string) list =
    [
        "pcmSupp",
        """
Route
RECTAAL
Schedule
[]_sch_frq [3;4 x/dag]
Orderable
[paracetamol]_ord_qty <0 stuk..>
[paracetamol]_orb_qty [1 stuk]
[paracetamol]_ord_cnt <0 x..>
[paracetamol]_dos_cnt [1 x]
[paracetamol]_dos_qty [1 stuk]
[paracetamol]_dos_ptm [3;4 stuk/dag]
[paracetamol]_dos_rte <0 ..>
[paracetamol]_dos_tot <0 ..>
[paracetamol]_dos_qty_adj [0,07 stuk/kg]
[paracetamol]_dos_ptm_adj [0,21;0,29 stuk/kg/dag]
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
[paracetamol.paracetamol]_dos_qty_adj [0,07 stuk/kg]
[paracetamol.paracetamol]_dos_ptm_adj [0,21;0,29 stuk/kg/dag]
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

        "amfo",
        """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1 x/dag]
Orderable
[amfotericine b liposomaal]_ord_qty <0 mL..>
[amfotericine b liposomaal]_orb_qty [22;48;49 mL .. 312;313;340 mL]
[amfotericine b liposomaal]_ord_cnt <0 x..>
[amfotericine b liposomaal]_dos_cnt [1 x]
[amfotericine b liposomaal]_dos_qty [22;48;49 mL .. 312;313;340 mL]
[amfotericine b liposomaal]_dos_ptm [22;48;49 mL/dag .. 312;313;340 mL/dag]
[amfotericine b liposomaal]_dos_rte <0 ..>
[amfotericine b liposomaal]_dos_tot <0 ..>
[amfotericine b liposomaal]_dos_qty_adj [1,57;3,43;3,5 mL/kg .. 22,3;22,4;24,3 mL/kg]
[amfotericine b liposomaal]_dos_ptm_adj [1,57;3,43;3,5 mL/kg/dag .. 22,3;22,4;24,3 mL/kg/dag]
[amfotericine b liposomaal]_dos_rte_adj <0 ..>
[amfotericine b liposomaal]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_cmp_qty [1 mL]
[amfotericine b liposomaal.amfotericine b liposomaal]_orb_qty [11;12;13;14;15;16;17 mL]
[amfotericine b liposomaal.amfotericine b liposomaal]_orb_cnt [11;12;13;14;15;16;17 x]
[amfotericine b liposomaal.amfotericine b liposomaal]_orb_cnc [0,03;0,04 x .. 0,68;0,73;0,77 x]
[amfotericine b liposomaal.amfotericine b liposomaal]_ord_qty <0 mL..>
[amfotericine b liposomaal.amfotericine b liposomaal]_ord_cnt <0 x..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty [11;12;13;14;15;16;17 mL]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm [11;12;13;14;15;16;17 mL/dag]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty_adj [0,79;0,86;0,93;1;1,07;1,14;1,21 mL/kg]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm_adj [0,79;0,86;0,93;1;1,07;1,14;1,21 mL/kg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_cmp_qty [72 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_orb_qty [792;864;936;1 008;1 080;1 152;1 224 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_cmp_cnc [72 mg/mL]
[amfotericine b liposomaal.amfotericine b liposomaal.saccharose]_orb_cnc [2,33;2,53;2,54 mg/mL .. 49,1;52,4;55,6 mg/mL]
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
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_orb_cnc [0,2 mg/mL .. 1,39;1,42;2 mg/mL]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty [44;48;52;56;60;64;68 mg]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm [44;48;52;56;60;64;68 mg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_qty_adj [3,14;3,43;3,71;4;4,29;4,57;4,86 mg/kg]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_ptm_adj [3,14;3,43;3,71;4;4,29;4,57;4,86 mg/kg/dag]
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.amfotericine b liposomaal.amfotericine b liposomaal]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.gluc 10%]_cmp_qty [1 mL]
[amfotericine b liposomaal.gluc 10%]_orb_qty [5;37;74;111;148;185;222;259;296;329 mL]
[amfotericine b liposomaal.gluc 10%]_orb_cnt [5;37;74;111;148;185;222;259;296;329 x]
[amfotericine b liposomaal.gluc 10%]_orb_cnc [0,01 x..1 x>
[amfotericine b liposomaal.gluc 10%]_ord_qty <0 mL..>
[amfotericine b liposomaal.gluc 10%]_ord_cnt <0 x..>
[amfotericine b liposomaal.gluc 10%]_dos_qty [5;37;74;111;148;185;222;259;296;329 mL]
[amfotericine b liposomaal.gluc 10%]_dos_ptm [5;37;74;111;148;185;222;259;296;329 mL/dag]
[amfotericine b liposomaal.gluc 10%]_dos_rte <0 ..>
[amfotericine b liposomaal.gluc 10%]_dos_tot <0 ..>
[amfotericine b liposomaal.gluc 10%]_dos_qty_adj [0,36;2,64;5,29;7,93;10,6;13,2;15,9;18,5;21,1;23,5 mL/kg]
[amfotericine b liposomaal.gluc 10%]_dos_ptm_adj [0,36;2,64;5,29;7,93;10,6;13,2;15,9;18,5;21,1;23,5 mL/kg/dag]
[amfotericine b liposomaal.gluc 10%]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.gluc 10%]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_cmp_qty [0,4 kCal]
[amfotericine b liposomaal.gluc 10%.energie]_orb_qty [2;14,8;29,6;44,4;59,2;74;88,8;104;118;132 kCal]
[amfotericine b liposomaal.gluc 10%.energie]_cmp_cnc [0,4 kCal/mL]
[amfotericine b liposomaal.gluc 10%.energie]_orb_cnc [0,01 kCal/mL..5,98 kCal/mL]
[amfotericine b liposomaal.gluc 10%.energie]_dos_qty [2;14,8;29,6;44,4;59,2;74;88,8;104;118;132 kCal]
[amfotericine b liposomaal.gluc 10%.energie]_dos_ptm [2;14,8;29,6;44,4;59,2;74;88,8;104;118;132 kCal/dag]
[amfotericine b liposomaal.gluc 10%.energie]_dos_rte <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_dos_tot <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_dos_qty_adj [0,14;1,06;2,11;3,17;4,23;5,29;6,34;7,4;8,46;9,4 kCal/kg]
[amfotericine b liposomaal.gluc 10%.energie]_dos_ptm_adj [0,14;1,06;2,11;3,17;4,23;5,29;6,34;7,4;8,46;9,4 kCal/kg/dag]
[amfotericine b liposomaal.gluc 10%.energie]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.energie]_dos_tot_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_orb_qty [0,5;3,7;7,4;11,1;14,8;18,5;22,2;25,9;29,6;32,9 g]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_orb_cnc [0 g/mL..1,5 g/mL]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_qty [0,5;3,7;7,4;11,1;14,8;18,5;22,2;25,9;29,6;32,9 g]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_ptm [0,5;3,7;7,4;11,1;14,8;18,5;22,2;25,9;29,6;32,9 g/dag]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_rte <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_tot <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_qty_adj [0,04;0,26;0,53;0,79;1,06;1,32;1,59;1,85;2,11;2,35 g/kg]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_ptm_adj [0,04;0,26;0,53;0,79;1,06;1,32;1,59;1,85;2,11;2,35 g/kg/dag]
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_rte_adj <0 ..>
[amfotericine b liposomaal.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [14 kg]
"""

        "morfCont",
        """
Route
INTRAVENEUS
Schedule
[]_sch_tme [17,9;18,5;19,2 uur .. 55,6;62,5;71,4 uur]
Orderable
[morfine]_ord_qty <0 mL..>
[morfine]_orb_qty [50 mL]
[morfine]_ord_cnt <0 x..>
[morfine]_dos_cnt [1 x]
[morfine]_dos_qty [50 mL]
[morfine]_dos_ptm <0 ..>
[morfine]_dos_rte [0,7;0,8;0,9 mL/uur .. 2,6;2,7;2,8 mL/uur]
[morfine]_dos_tot <0 ..>
[morfine]_dos_qty_adj [3,57 mL/kg]
[morfine]_dos_ptm_adj <0 ..>
[morfine]_dos_rte_adj [0,05;0,06 mL/kg/uur .. 0,19;0,2 mL/kg/uur]
[morfine]_dos_tot_adj <0 ..>
[morfine.morfine]_cmp_qty [1 mL]
[morfine.morfine]_orb_qty [1;10 mL]
[morfine.morfine]_orb_cnt [1;10 x]
[morfine.morfine]_orb_cnc [0,02;0,2 x]
[morfine.morfine]_ord_qty <0 mL..>
[morfine.morfine]_ord_cnt <0 x..>
[morfine.morfine]_dos_qty [1;10 mL]
[morfine.morfine]_dos_ptm <0 ..>
[morfine.morfine]_dos_rte [0,01;0,02 mL/uur .. 0,52;0,54;0,56 mL/uur]
[morfine.morfine]_dos_tot <0 ..>
[morfine.morfine]_dos_qty_adj [0,07;0,71 mL/kg]
[morfine.morfine]_dos_ptm_adj <0 ..>
[morfine.morfine]_dos_rte_adj [0 mL/kg/uur .. 0,04 mL/kg/uur]
[morfine.morfine]_dos_tot_adj <0 ..>
[morfine.morfine.morfin]_cmp_qty [1;10 mg]
[morfine.morfine.morfin]_orb_qty [10 mg]
[morfine.morfine.morfin]_cmp_cnc [1;10 mg/mL]
[morfine.morfine.morfin]_orb_cnc [0,2 mg/mL]
[morfine.morfine.morfin]_dos_qty [10 mg]
[morfine.morfine.morfin]_dos_ptm <0 ..>
[morfine.morfine.morfin]_dos_rte [0,14;0,16;0,18 mg/uur .. 0,52;0,54;0,56 mg/uur]
[morfine.morfine.morfin]_dos_tot <0 ..>
[morfine.morfine.morfin]_dos_qty_adj [0,71 mg/kg]
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
[morfine.gluc 10%]_dos_rte [0,56;0,64;0,69 mL/uur .. 2,55;2,65;2,74 mL/uur]
[morfine.gluc 10%]_dos_tot <0 ..>
[morfine.gluc 10%]_dos_qty_adj [2,86;3,5 mL/kg]
[morfine.gluc 10%]_dos_ptm_adj <0 ..>
[morfine.gluc 10%]_dos_rte_adj [0,04;0,05 mL/kg/uur .. 0,18;0,19;0,2 mL/kg/uur]
[morfine.gluc 10%]_dos_tot_adj <0 ..>
[morfine.gluc 10%.energie]_cmp_qty [0,4 kCal]
[morfine.gluc 10%.energie]_orb_qty [16;19,6 kCal]
[morfine.gluc 10%.energie]_cmp_cnc [0,4 kCal/mL]
[morfine.gluc 10%.energie]_orb_cnc [0,32;0,39 kCal/mL]
[morfine.gluc 10%.energie]_dos_qty [16;19,6 kCal]
[morfine.gluc 10%.energie]_dos_ptm <0 ..>
[morfine.gluc 10%.energie]_dos_rte [0,22;0,26;0,27 kCal/uur .. 1,02;1,06;1,1 kCal/uur]
[morfine.gluc 10%.energie]_dos_tot <0 ..>
[morfine.gluc 10%.energie]_dos_qty_adj [1,14;1,4 kCal/kg]
[morfine.gluc 10%.energie]_dos_ptm_adj <0 ..>
[morfine.gluc 10%.energie]_dos_rte_adj [0,02 kCal/kg/uur .. 0,07;0,08 kCal/kg/uur]
[morfine.gluc 10%.energie]_dos_tot_adj <0 ..>
[morfine.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[morfine.gluc 10%.koolhydraat]_orb_qty [4;4,9 g]
[morfine.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[morfine.gluc 10%.koolhydraat]_orb_cnc [0,08;0,1 g/mL]
[morfine.gluc 10%.koolhydraat]_dos_qty [4;4,9 g]
[morfine.gluc 10%.koolhydraat]_dos_ptm <0 ..>
[morfine.gluc 10%.koolhydraat]_dos_rte [0,06;0,07 g/uur .. 0,26;0,27 g/uur]
[morfine.gluc 10%.koolhydraat]_dos_tot <0 ..>
[morfine.gluc 10%.koolhydraat]_dos_qty_adj [0,29;0,35 g/kg]
[morfine.gluc 10%.koolhydraat]_dos_ptm_adj <0 ..>
[morfine.gluc 10%.koolhydraat]_dos_rte_adj [0 g/kg/uur .. 0,02 g/kg/uur]
[morfine.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [14 kg]
"""

        "pcmDrink",
        """
Route
or
Schedule
[]_sch_frq [3;6 x/dag]
Orderable
[paracetamol drank]_ord_qty <0 mL..>
[paracetamol drank]_orb_qty [5;10 mL]
[paracetamol drank]_ord_cnt <0 x..>
[paracetamol drank]_dos_cnt [1 x]
[paracetamol drank]_dos_qty [5;10 mL]
[paracetamol drank]_dos_ptm [30 mL/dag]
[paracetamol drank]_dos_rte <0 ..>
[paracetamol drank]_dos_tot <0 ..>
[paracetamol drank]_dos_qty_adj [0,5;1 mL/kg]
[paracetamol drank]_dos_ptm_adj [3 mL/kg/dag]
[paracetamol drank]_dos_rte_adj <0 ..>
[paracetamol drank]_dos_tot_adj <0 ..>
[paracetamol drank.paracetamol]_cmp_qty [5 mL]
[paracetamol drank.paracetamol]_orb_qty [5;10 mL]
[paracetamol drank.paracetamol]_orb_cnt [1;2 x]
[paracetamol drank.paracetamol]_orb_cnc [1 x]
[paracetamol drank.paracetamol]_ord_qty <0 mL..>
[paracetamol drank.paracetamol]_ord_cnt <0 x..>
[paracetamol drank.paracetamol]_dos_qty [5;10 mL]
[paracetamol drank.paracetamol]_dos_ptm [30 mL/dag]
[paracetamol drank.paracetamol]_dos_rte <0 ..>
[paracetamol drank.paracetamol]_dos_tot <0 ..>
[paracetamol drank.paracetamol]_dos_qty_adj [0,5;1 mL/kg]
[paracetamol drank.paracetamol]_dos_ptm_adj [3 mL/kg/dag]
[paracetamol drank.paracetamol]_dos_rte_adj <0 ..>
[paracetamol drank.paracetamol]_dos_tot_adj <0 ..>
[paracetamol drank.paracetamol.paracetamol]_cmp_qty [120 mg]
[paracetamol drank.paracetamol.paracetamol]_orb_qty [120;240 mg]
[paracetamol drank.paracetamol.paracetamol]_cmp_cnc [24 mg/mL]
[paracetamol drank.paracetamol.paracetamol]_orb_cnc [24 mg/mL]
[paracetamol drank.paracetamol.paracetamol]_dos_qty [120;240 mg]
[paracetamol drank.paracetamol.paracetamol]_dos_ptm [720 mg/dag]
[paracetamol drank.paracetamol.paracetamol]_dos_rte <0 ..>
[paracetamol drank.paracetamol.paracetamol]_dos_tot <0 ..>
[paracetamol drank.paracetamol.paracetamol]_dos_qty_adj [12;24 mg/kg]
[paracetamol drank.paracetamol.paracetamol]_dos_ptm_adj [72 mg/kg/dag]
[paracetamol drank.paracetamol.paracetamol]_dos_rte_adj <0 ..>
[paracetamol drank.paracetamol.paracetamol]_dos_tot_adj <0 ..>
[]_adj_qty [10 kg]
"""

        "cotrim",
        """
Route
or
Schedule
[]_sch_frq [2 x/dag]
Orderable
[cotrimoxazol]_ord_qty <0 mL..>
[cotrimoxazol]_orb_qty [7 mL]
[cotrimoxazol]_ord_cnt <0 x..>
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

        "tpn",
        """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1 x/dag]
[]_sch_tme [20 uur]
Orderable
[samenstelling c]_ord_qty <0 mL..>
[samenstelling c]_orb_qty [200;800 mL]
[samenstelling c]_ord_cnt <0 x..>
[samenstelling c]_dos_cnt [1 x]
[samenstelling c]_dos_qty [200;800 mL]
[samenstelling c]_dos_ptm [200;800 mL/dag]
[samenstelling c]_dos_rte [10;40 mL/uur]
[samenstelling c]_dos_tot <0 ..>
[samenstelling c]_dos_qty_adj [18,2;72,7 mL/kg]
[samenstelling c]_dos_ptm_adj [18,2;72,7 mL/kg/dag]
[samenstelling c]_dos_rte_adj [0,91;3,64 mL/kg/uur]
[samenstelling c]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C]_cmp_qty [1 mL]
[samenstelling c.Samenstelling C]_orb_qty [110 mL]
[samenstelling c.Samenstelling C]_orb_cnt [110 x]
[samenstelling c.Samenstelling C]_orb_cnc [0,14;0,55 x]
[samenstelling c.Samenstelling C]_ord_qty <0 mL..>
[samenstelling c.Samenstelling C]_ord_cnt <0 x..>
[samenstelling c.Samenstelling C]_dos_qty [110 mL]
[samenstelling c.Samenstelling C]_dos_ptm [110 mL/dag]
[samenstelling c.Samenstelling C]_dos_rte [1,38;5,5;22 mL/uur]
[samenstelling c.Samenstelling C]_dos_tot <0 ..>
[samenstelling c.Samenstelling C]_dos_qty_adj [10 mL/kg]
[samenstelling c.Samenstelling C]_dos_ptm_adj [10 mL/kg/dag]
[samenstelling c.Samenstelling C]_dos_rte_adj <0 ..>
[samenstelling c.Samenstelling C]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.eiwit]_cmp_qty [0,08 g]
[samenstelling c.Samenstelling C.eiwit]_orb_qty [8,8 g]
[samenstelling c.Samenstelling C.eiwit]_cmp_cnc [0,08 g/mL]
[samenstelling c.Samenstelling C.eiwit]_orb_cnc [0,01;0,04 g/mL]
[samenstelling c.Samenstelling C.eiwit]_dos_qty [8,8 g]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm [8,8 g/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte [0,11;0,44;1,76 g/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.eiwit]_dos_qty_adj [0,8 g/kg]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm_adj [0,8 g/kg/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte_adj [0,01;0,04;0,16 g/kg/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.natrium]_cmp_qty [0,01 mmol]
[samenstelling c.Samenstelling C.natrium]_orb_qty [1,1 mmol]
[samenstelling c.Samenstelling C.natrium]_cmp_cnc [0,01 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_orb_cnc [0;0,01 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_dos_qty [1,1 mmol]
[samenstelling c.Samenstelling C.natrium]_dos_ptm [1,1 mmol/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte [0,01;0,06;0,22 mmol/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.natrium]_dos_qty_adj [0,1 mmol/kg]
[samenstelling c.Samenstelling C.natrium]_dos_ptm_adj [0,1 mmol/kg/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte_adj [0;0,01;0,02 mmol/kg/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.kalium]_cmp_qty [0,02 mmol]
[samenstelling c.Samenstelling C.kalium]_orb_qty [2,2 mmol]
[samenstelling c.Samenstelling C.kalium]_cmp_cnc [0,02 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_orb_cnc [0;0,01 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_dos_qty [2,2 mmol]
[samenstelling c.Samenstelling C.kalium]_dos_ptm [2,2 mmol/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte [0,03;0,11;0,44 mmol/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.kalium]_dos_qty_adj [0,2 mmol/kg]
[samenstelling c.Samenstelling C.kalium]_dos_ptm_adj [0,2 mmol/kg/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte_adj [0;0,01;0,04 mmol/kg/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%]_cmp_qty [1 mL]
[samenstelling c.NaCl 3%]_orb_qty [60;66;69 mL]
[samenstelling c.NaCl 3%]_orb_cnt [60;66;69 x]
[samenstelling c.NaCl 3%]_orb_cnc [0,08;0,09;0,3;0,33;0,35 x]
[samenstelling c.NaCl 3%]_ord_qty <0 mL..>
[samenstelling c.NaCl 3%]_ord_cnt <0 x..>
[samenstelling c.NaCl 3%]_dos_qty [60;66;69 mL]
[samenstelling c.NaCl 3%]_dos_ptm [60;66;69 mL/dag]
[samenstelling c.NaCl 3%]_dos_rte [0,75;0,83;0,86;3;3,3;3,45;12;13,2;13,8 mL/uur]
[samenstelling c.NaCl 3%]_dos_tot <0 ..>
[samenstelling c.NaCl 3%]_dos_qty_adj [5,45;6;6,27 mL/kg]
[samenstelling c.NaCl 3%]_dos_ptm_adj [5,45;6;6,27 mL/kg/dag]
[samenstelling c.NaCl 3%]_dos_rte_adj <0 ..>
[samenstelling c.NaCl 3%]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%.natrium]_cmp_qty [0,5 mmol]
[samenstelling c.NaCl 3%.natrium]_orb_qty [30;33;34,5 mmol]
[samenstelling c.NaCl 3%.natrium]_cmp_cnc [0,5 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_orb_cnc [0,04;0,15;0,17 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_dos_qty [30;33;34,5 mmol]
[samenstelling c.NaCl 3%.natrium]_dos_ptm [30;33;34,5 mmol/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte [0,38;0,41;0,43;1,5;1,65;1,72;6;6,6;6,9 mmol/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot <0 ..>
[samenstelling c.NaCl 3%.natrium]_dos_qty_adj [2,73;3;3,14 mmol/kg]
[samenstelling c.NaCl 3%.natrium]_dos_ptm_adj [2,73;3;3,14 mmol/kg/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte_adj [0,03;0,04;0,14;0,15;0,16;0,55;0,6;0,63 mmol/kg/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%]_cmp_qty [1 mL]
[samenstelling c.KCl 7,4%]_orb_qty [20;23 mL]
[samenstelling c.KCl 7,4%]_orb_cnt [20;23 x]
[samenstelling c.KCl 7,4%]_orb_cnc [0,03;0,1;0,12 x]
[samenstelling c.KCl 7,4%]_ord_qty <0 mL..>
[samenstelling c.KCl 7,4%]_ord_cnt <0 x..>
[samenstelling c.KCl 7,4%]_dos_qty [20;23 mL]
[samenstelling c.KCl 7,4%]_dos_ptm [20;23 mL/dag]
[samenstelling c.KCl 7,4%]_dos_rte [0,25;0,29;1;1,15;4;4,6 mL/uur]
[samenstelling c.KCl 7,4%]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%]_dos_qty_adj [1,82;2,09 mL/kg]
[samenstelling c.KCl 7,4%]_dos_ptm_adj [1,82;2,09 mL/kg/dag]
[samenstelling c.KCl 7,4%]_dos_rte_adj <0 ..>
[samenstelling c.KCl 7,4%]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%.kalium]_cmp_qty [1 mmol]
[samenstelling c.KCl 7,4%.kalium]_orb_qty [20;23 mmol]
[samenstelling c.KCl 7,4%.kalium]_cmp_cnc [1 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_orb_cnc [0,03;0,1;0,12 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_dos_qty [20;23 mmol]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm [20;23 mmol/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte [0,25;0,29;1;1,15;4;4,6 mmol/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%.kalium]_dos_qty_adj [1,82;2,09 mmol/kg]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm_adj [1,82;2,09 mmol/kg/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte_adj [0,02;0,03;0,09;0,11;0,36;0,42 mmol/kg/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%]_cmp_qty [1 mL]
[samenstelling c.gluc 10%]_orb_qty [1;610 mL]
[samenstelling c.gluc 10%]_orb_cnt [1;610 x]
[samenstelling c.gluc 10%]_orb_cnc [0;0,01;0,76 x]
[samenstelling c.gluc 10%]_ord_qty <0 mL..>
[samenstelling c.gluc 10%]_ord_cnt <0 x..>
[samenstelling c.gluc 10%]_dos_qty [1;610 mL]
[samenstelling c.gluc 10%]_dos_ptm [1;610 mL/dag]
[samenstelling c.gluc 10%]_dos_rte [0,01;0,05;0,2;7,62;30,5;122 mL/uur]
[samenstelling c.gluc 10%]_dos_tot <0 ..>
[samenstelling c.gluc 10%]_dos_qty_adj [0,09;55,5 mL/kg]
[samenstelling c.gluc 10%]_dos_ptm_adj [0,09;55,5 mL/kg/dag]
[samenstelling c.gluc 10%]_dos_rte_adj <0 ..>
[samenstelling c.gluc 10%]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[samenstelling c.gluc 10%.koolhydraat]_orb_qty [0,1;61 g]
[samenstelling c.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_orb_cnc [0;0,08;0,31 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_dos_qty [0,1;61 g]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm [0,1;61 g/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte [0;0,01;0,02;0,76;3,05;12,2 g/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_dos_qty_adj [0,01;5,55 g/kg]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm_adj [0,01;5,55 g/kg/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte_adj [0;0,07;0,28;1,11 g/kg/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [11 kg]
"""

        "tpnComplete",
        """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1 x/dag]
[]_sch_tme [20 uur]
Orderable
[samenstelling c]_ord_qty <0 mL..>
[samenstelling c]_orb_qty [200;800 mL]
[samenstelling c]_ord_cnt <0 x..>
[samenstelling c]_dos_cnt [1 x]
[samenstelling c]_dos_qty [200;800 mL]
[samenstelling c]_dos_ptm [200;800 mL/dag]
[samenstelling c]_dos_rte [10;40 mL/uur]
[samenstelling c]_dos_tot <0 ..>
[samenstelling c]_dos_qty_adj [18,2;72,7 mL/kg]
[samenstelling c]_dos_ptm_adj [18,2;72,7 mL/kg/dag]
[samenstelling c]_dos_rte_adj [0,91;3,64 mL/kg/uur]
[samenstelling c]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C]_cmp_qty [1 mL]
[samenstelling c.Samenstelling C]_orb_qty [110 mL]
[samenstelling c.Samenstelling C]_orb_cnt [110 x]
[samenstelling c.Samenstelling C]_orb_cnc [0,14;0,55 x]
[samenstelling c.Samenstelling C]_ord_qty <0 mL..>
[samenstelling c.Samenstelling C]_ord_cnt <0 x..>
[samenstelling c.Samenstelling C]_dos_qty [110 mL]
[samenstelling c.Samenstelling C]_dos_ptm [110 mL/dag]
[samenstelling c.Samenstelling C]_dos_rte [1,38;5,5;22 mL/uur]
[samenstelling c.Samenstelling C]_dos_tot <0 ..>
[samenstelling c.Samenstelling C]_dos_qty_adj [10 mL/kg]
[samenstelling c.Samenstelling C]_dos_ptm_adj [10 mL/kg/dag]
[samenstelling c.Samenstelling C]_dos_rte_adj <0 ..>
[samenstelling c.Samenstelling C]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.energie]_cmp_qty [0,32 kCal]
[samenstelling c.Samenstelling C.energie]_orb_qty [35,2 kCal]
[samenstelling c.Samenstelling C.energie]_cmp_cnc [0,32 kCal/mL]
[samenstelling c.Samenstelling C.energie]_orb_cnc [0,04;0,18 kCal/mL]
[samenstelling c.Samenstelling C.energie]_dos_qty [35,2 kCal]
[samenstelling c.Samenstelling C.energie]_dos_ptm [35,2 kCal/dag]
[samenstelling c.Samenstelling C.energie]_dos_rte [0,44;1,76;7,04 kCal/uur]
[samenstelling c.Samenstelling C.energie]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.energie]_dos_qty_adj [3,2 kCal/kg]
[samenstelling c.Samenstelling C.energie]_dos_ptm_adj [3,2 kCal/kg/dag]
[samenstelling c.Samenstelling C.energie]_dos_rte_adj [0,04;0,16;0,64 kCal/kg/uur]
[samenstelling c.Samenstelling C.energie]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.eiwit]_cmp_qty [0,08 g]
[samenstelling c.Samenstelling C.eiwit]_orb_qty [8,8 g]
[samenstelling c.Samenstelling C.eiwit]_cmp_cnc [0,08 g/mL]
[samenstelling c.Samenstelling C.eiwit]_orb_cnc [0,01;0,04 g/mL]
[samenstelling c.Samenstelling C.eiwit]_dos_qty [8,8 g]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm [8,8 g/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte [0,11;0,44;1,76 g/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.eiwit]_dos_qty_adj [0,8 g/kg]
[samenstelling c.Samenstelling C.eiwit]_dos_ptm_adj [0,8 g/kg/dag]
[samenstelling c.Samenstelling C.eiwit]_dos_rte_adj [0,01;0,04;0,16 g/kg/uur]
[samenstelling c.Samenstelling C.eiwit]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.natrium]_cmp_qty [0,01 mmol]
[samenstelling c.Samenstelling C.natrium]_orb_qty [1,1 mmol]
[samenstelling c.Samenstelling C.natrium]_cmp_cnc [0,01 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_orb_cnc [0;0,01 mmol/mL]
[samenstelling c.Samenstelling C.natrium]_dos_qty [1,1 mmol]
[samenstelling c.Samenstelling C.natrium]_dos_ptm [1,1 mmol/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte [0,01;0,06;0,22 mmol/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.natrium]_dos_qty_adj [0,1 mmol/kg]
[samenstelling c.Samenstelling C.natrium]_dos_ptm_adj [0,1 mmol/kg/dag]
[samenstelling c.Samenstelling C.natrium]_dos_rte_adj [0;0,01;0,02 mmol/kg/uur]
[samenstelling c.Samenstelling C.natrium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.kalium]_cmp_qty [0,02 mmol]
[samenstelling c.Samenstelling C.kalium]_orb_qty [2,2 mmol]
[samenstelling c.Samenstelling C.kalium]_cmp_cnc [0,02 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_orb_cnc [0;0,01 mmol/mL]
[samenstelling c.Samenstelling C.kalium]_dos_qty [2,2 mmol]
[samenstelling c.Samenstelling C.kalium]_dos_ptm [2,2 mmol/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte [0,03;0,11;0,44 mmol/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.kalium]_dos_qty_adj [0,2 mmol/kg]
[samenstelling c.Samenstelling C.kalium]_dos_ptm_adj [0,2 mmol/kg/dag]
[samenstelling c.Samenstelling C.kalium]_dos_rte_adj [0;0,01;0,04 mmol/kg/uur]
[samenstelling c.Samenstelling C.kalium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.calcium]_cmp_qty [0,03 mmol]
[samenstelling c.Samenstelling C.calcium]_orb_qty [3,3 mmol]
[samenstelling c.Samenstelling C.calcium]_cmp_cnc [0,03 mmol/mL]
[samenstelling c.Samenstelling C.calcium]_orb_cnc [0;0,02 mmol/mL]
[samenstelling c.Samenstelling C.calcium]_dos_qty [3,3 mmol]
[samenstelling c.Samenstelling C.calcium]_dos_ptm [3,3 mmol/dag]
[samenstelling c.Samenstelling C.calcium]_dos_rte [0,04;0,17;0,66 mmol/uur]
[samenstelling c.Samenstelling C.calcium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.calcium]_dos_qty_adj [0,3 mmol/kg]
[samenstelling c.Samenstelling C.calcium]_dos_ptm_adj [0,3 mmol/kg/dag]
[samenstelling c.Samenstelling C.calcium]_dos_rte_adj [0;0,02;0,06 mmol/kg/uur]
[samenstelling c.Samenstelling C.calcium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.fosfaat]_cmp_qty [0,02 mmol]
[samenstelling c.Samenstelling C.fosfaat]_orb_qty [2,2 mmol]
[samenstelling c.Samenstelling C.fosfaat]_cmp_cnc [0,02 mmol/mL]
[samenstelling c.Samenstelling C.fosfaat]_orb_cnc [0;0,01 mmol/mL]
[samenstelling c.Samenstelling C.fosfaat]_dos_qty [2,2 mmol]
[samenstelling c.Samenstelling C.fosfaat]_dos_ptm [2,2 mmol/dag]
[samenstelling c.Samenstelling C.fosfaat]_dos_rte [0,03;0,11;0,44 mmol/uur]
[samenstelling c.Samenstelling C.fosfaat]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.fosfaat]_dos_qty_adj [0,2 mmol/kg]
[samenstelling c.Samenstelling C.fosfaat]_dos_ptm_adj [0,2 mmol/kg/dag]
[samenstelling c.Samenstelling C.fosfaat]_dos_rte_adj [0;0,01;0,04 mmol/kg/uur]
[samenstelling c.Samenstelling C.fosfaat]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.magnesium]_cmp_qty [0,01 mmol]
[samenstelling c.Samenstelling C.magnesium]_orb_qty [1,1 mmol]
[samenstelling c.Samenstelling C.magnesium]_cmp_cnc [0,01 mmol/mL]
[samenstelling c.Samenstelling C.magnesium]_orb_cnc [0;0,01 mmol/mL]
[samenstelling c.Samenstelling C.magnesium]_dos_qty [1,1 mmol]
[samenstelling c.Samenstelling C.magnesium]_dos_ptm [1,1 mmol/dag]
[samenstelling c.Samenstelling C.magnesium]_dos_rte [0,01;0,06;0,22 mmol/uur]
[samenstelling c.Samenstelling C.magnesium]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.magnesium]_dos_qty_adj [0,1 mmol/kg]
[samenstelling c.Samenstelling C.magnesium]_dos_ptm_adj [0,1 mmol/kg/dag]
[samenstelling c.Samenstelling C.magnesium]_dos_rte_adj [0;0,01;0,02 mmol/kg/uur]
[samenstelling c.Samenstelling C.magnesium]_dos_tot_adj <0 ..>
[samenstelling c.Samenstelling C.chloor]_cmp_qty [0,07 mmol]
[samenstelling c.Samenstelling C.chloor]_orb_qty [7,7 mmol]
[samenstelling c.Samenstelling C.chloor]_cmp_cnc [0,07 mmol/mL]
[samenstelling c.Samenstelling C.chloor]_orb_cnc [0,01;0,04 mmol/mL]
[samenstelling c.Samenstelling C.chloor]_dos_qty [7,7 mmol]
[samenstelling c.Samenstelling C.chloor]_dos_ptm [7,7 mmol/dag]
[samenstelling c.Samenstelling C.chloor]_dos_rte [0,1;0,39;1,54 mmol/uur]
[samenstelling c.Samenstelling C.chloor]_dos_tot <0 ..>
[samenstelling c.Samenstelling C.chloor]_dos_qty_adj [0,7 mmol/kg]
[samenstelling c.Samenstelling C.chloor]_dos_ptm_adj [0,7 mmol/kg/dag]
[samenstelling c.Samenstelling C.chloor]_dos_rte_adj [0,01;0,04;0,14 mmol/kg/uur]
[samenstelling c.Samenstelling C.chloor]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%]_cmp_qty [1 mL]
[samenstelling c.NaCl 3%]_orb_qty [60;66;69 mL]
[samenstelling c.NaCl 3%]_orb_cnt [60;66;69 x]
[samenstelling c.NaCl 3%]_orb_cnc [0,08;0,09;0,3;0,33;0,35 x]
[samenstelling c.NaCl 3%]_ord_qty <0 mL..>
[samenstelling c.NaCl 3%]_ord_cnt <0 x..>
[samenstelling c.NaCl 3%]_dos_qty [60;66;69 mL]
[samenstelling c.NaCl 3%]_dos_ptm [60;66;69 mL/dag]
[samenstelling c.NaCl 3%]_dos_rte [0,75;0,83;0,86;3;3,3;3,45;12;13,2;13,8 mL/uur]
[samenstelling c.NaCl 3%]_dos_tot <0 ..>
[samenstelling c.NaCl 3%]_dos_qty_adj [5,45;6;6,27 mL/kg]
[samenstelling c.NaCl 3%]_dos_ptm_adj [5,45;6;6,27 mL/kg/dag]
[samenstelling c.NaCl 3%]_dos_rte_adj <0 ..>
[samenstelling c.NaCl 3%]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%.natrium]_cmp_qty [0,5 mmol]
[samenstelling c.NaCl 3%.natrium]_orb_qty [30;33;34,5 mmol]
[samenstelling c.NaCl 3%.natrium]_cmp_cnc [0,5 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_orb_cnc [0,04;0,15;0,17 mmol/mL]
[samenstelling c.NaCl 3%.natrium]_dos_qty [30;33;34,5 mmol]
[samenstelling c.NaCl 3%.natrium]_dos_ptm [30;33;34,5 mmol/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte [0,38;0,41;0,43;1,5;1,65;1,72;6;6,6;6,9 mmol/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot <0 ..>
[samenstelling c.NaCl 3%.natrium]_dos_qty_adj [2,73;3;3,14 mmol/kg]
[samenstelling c.NaCl 3%.natrium]_dos_ptm_adj [2,73;3;3,14 mmol/kg/dag]
[samenstelling c.NaCl 3%.natrium]_dos_rte_adj [0,03;0,04;0,14;0,15;0,16;0,55;0,6;0,63 mmol/kg/uur]
[samenstelling c.NaCl 3%.natrium]_dos_tot_adj <0 ..>
[samenstelling c.NaCl 3%.chloor]_cmp_qty [0,5 mmol]
[samenstelling c.NaCl 3%.chloor]_orb_qty [30;33;34,5 mmol]
[samenstelling c.NaCl 3%.chloor]_cmp_cnc [0,5 mmol/mL]
[samenstelling c.NaCl 3%.chloor]_orb_cnc [0,04;0,15;0,17 mmol/mL]
[samenstelling c.NaCl 3%.chloor]_dos_qty [30;33;34,5 mmol]
[samenstelling c.NaCl 3%.chloor]_dos_ptm [30;33;34,5 mmol/dag]
[samenstelling c.NaCl 3%.chloor]_dos_rte [0,38;0,41;0,43;1,5;1,65;1,72;6;6,6;6,9 mmol/uur]
[samenstelling c.NaCl 3%.chloor]_dos_tot <0 ..>
[samenstelling c.NaCl 3%.chloor]_dos_qty_adj [2,73;3;3,14 mmol/kg]
[samenstelling c.NaCl 3%.chloor]_dos_ptm_adj [2,73;3;3,14 mmol/kg/dag]
[samenstelling c.NaCl 3%.chloor]_dos_rte_adj [0,03;0,04;0,14;0,15;0,16;0,55;0,6;0,63 mmol/kg/uur]
[samenstelling c.NaCl 3%.chloor]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%]_cmp_qty [1 mL]
[samenstelling c.KCl 7,4%]_orb_qty [20;23 mL]
[samenstelling c.KCl 7,4%]_orb_cnt [20;23 x]
[samenstelling c.KCl 7,4%]_orb_cnc [0,03;0,1;0,12 x]
[samenstelling c.KCl 7,4%]_ord_qty <0 mL..>
[samenstelling c.KCl 7,4%]_ord_cnt <0 x..>
[samenstelling c.KCl 7,4%]_dos_qty [20;23 mL]
[samenstelling c.KCl 7,4%]_dos_ptm [20;23 mL/dag]
[samenstelling c.KCl 7,4%]_dos_rte [0,25;0,29;1;1,15;4;4,6 mL/uur]
[samenstelling c.KCl 7,4%]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%]_dos_qty_adj [1,82;2,09 mL/kg]
[samenstelling c.KCl 7,4%]_dos_ptm_adj [1,82;2,09 mL/kg/dag]
[samenstelling c.KCl 7,4%]_dos_rte_adj <0 ..>
[samenstelling c.KCl 7,4%]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%.kalium]_cmp_qty [1 mmol]
[samenstelling c.KCl 7,4%.kalium]_orb_qty [20;23 mmol]
[samenstelling c.KCl 7,4%.kalium]_cmp_cnc [1 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_orb_cnc [0,03;0,1;0,12 mmol/mL]
[samenstelling c.KCl 7,4%.kalium]_dos_qty [20;23 mmol]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm [20;23 mmol/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte [0,25;0,29;1;1,15;4;4,6 mmol/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%.kalium]_dos_qty_adj [1,82;2,09 mmol/kg]
[samenstelling c.KCl 7,4%.kalium]_dos_ptm_adj [1,82;2,09 mmol/kg/dag]
[samenstelling c.KCl 7,4%.kalium]_dos_rte_adj [0,02;0,03;0,09;0,11;0,36;0,42 mmol/kg/uur]
[samenstelling c.KCl 7,4%.kalium]_dos_tot_adj <0 ..>
[samenstelling c.KCl 7,4%.chloor]_cmp_qty [1 mmol]
[samenstelling c.KCl 7,4%.chloor]_orb_qty [20;23 mmol]
[samenstelling c.KCl 7,4%.chloor]_cmp_cnc [1 mmol/mL]
[samenstelling c.KCl 7,4%.chloor]_orb_cnc [0,03;0,1;0,12 mmol/mL]
[samenstelling c.KCl 7,4%.chloor]_dos_qty [20;23 mmol]
[samenstelling c.KCl 7,4%.chloor]_dos_ptm [20;23 mmol/dag]
[samenstelling c.KCl 7,4%.chloor]_dos_rte [0,25;0,29;1;1,15;4;4,6 mmol/uur]
[samenstelling c.KCl 7,4%.chloor]_dos_tot <0 ..>
[samenstelling c.KCl 7,4%.chloor]_dos_qty_adj [1,82;2,09 mmol/kg]
[samenstelling c.KCl 7,4%.chloor]_dos_ptm_adj [1,82;2,09 mmol/kg/dag]
[samenstelling c.KCl 7,4%.chloor]_dos_rte_adj [0,02;0,03;0,09;0,11;0,36;0,42 mmol/kg/uur]
[samenstelling c.KCl 7,4%.chloor]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%]_cmp_qty [1 mL]
[samenstelling c.gluc 10%]_orb_qty [1;610 mL]
[samenstelling c.gluc 10%]_orb_cnt [1;610 x]
[samenstelling c.gluc 10%]_orb_cnc [0;0,01;0,76 x]
[samenstelling c.gluc 10%]_ord_qty <0 mL..>
[samenstelling c.gluc 10%]_ord_cnt <0 x..>
[samenstelling c.gluc 10%]_dos_qty [1;610 mL]
[samenstelling c.gluc 10%]_dos_ptm [1;610 mL/dag]
[samenstelling c.gluc 10%]_dos_rte [0,01;0,05;0,2;7,62;30,5;122 mL/uur]
[samenstelling c.gluc 10%]_dos_tot <0 ..>
[samenstelling c.gluc 10%]_dos_qty_adj [0,09;55,5 mL/kg]
[samenstelling c.gluc 10%]_dos_ptm_adj [0,09;55,5 mL/kg/dag]
[samenstelling c.gluc 10%]_dos_rte_adj <0 ..>
[samenstelling c.gluc 10%]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%.energie]_cmp_qty [0,4 kCal]
[samenstelling c.gluc 10%.energie]_orb_qty [0,4;244 kCal]
[samenstelling c.gluc 10%.energie]_cmp_cnc [0,4 kCal/mL]
[samenstelling c.gluc 10%.energie]_orb_cnc [0;0,31;1,22 kCal/mL]
[samenstelling c.gluc 10%.energie]_dos_qty [0,4;244 kCal]
[samenstelling c.gluc 10%.energie]_dos_ptm [0,4;244 kCal/dag]
[samenstelling c.gluc 10%.energie]_dos_rte [0,01;0,02;0,08;3,05;12,2;48,8 kCal/uur]
[samenstelling c.gluc 10%.energie]_dos_tot <0 ..>
[samenstelling c.gluc 10%.energie]_dos_qty_adj [0,04;22,2 kCal/kg]
[samenstelling c.gluc 10%.energie]_dos_ptm_adj [0,04;22,2 kCal/kg/dag]
[samenstelling c.gluc 10%.energie]_dos_rte_adj [0;0,01;0,28;1,11;4,44 kCal/kg/uur]
[samenstelling c.gluc 10%.energie]_dos_tot_adj <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_cmp_qty [0,1 g]
[samenstelling c.gluc 10%.koolhydraat]_orb_qty [0,1;61 g]
[samenstelling c.gluc 10%.koolhydraat]_cmp_cnc [0,1 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_orb_cnc [0;0,08;0,31 g/mL]
[samenstelling c.gluc 10%.koolhydraat]_dos_qty [0,1;61 g]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm [0,1;61 g/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte [0;0,01;0,02;0,76;3,05;12,2 g/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot <0 ..>
[samenstelling c.gluc 10%.koolhydraat]_dos_qty_adj [0,01;5,55 g/kg]
[samenstelling c.gluc 10%.koolhydraat]_dos_ptm_adj [0,01;5,55 g/kg/dag]
[samenstelling c.gluc 10%.koolhydraat]_dos_rte_adj [0;0,07;0,28;1,11 g/kg/uur]
[samenstelling c.gluc 10%.koolhydraat]_dos_tot_adj <0 ..>
[]_adj_qty [11 kg]
"""

        "fullMedication",
        """
Route
INTRAVENEUS
Schedule
[]_sch_frq [1;2 x/dag]
[]_sch_tme [30 min..120 min]
Orderable
[Test Medication Complete]_ord_qty <0 ..>
[Test Medication Complete]_orb_qty [50 mL]
[Test Medication Complete]_ord_cnt <0 x..>
[Test Medication Complete]_dos_cnt [1 x..2 x]
[Test Medication Complete]_dos_qty [5 mL..10 mL]
[Test Medication Complete]_dos_ptm <0 mL/dag..>
[Test Medication Complete]_dos_rte [0,1 mL/uur..0,1 mL/uur..>
[Test Medication Complete]_dos_tot <0 ..>
[Test Medication Complete]_dos_qty_adj <0 ..>
[Test Medication Complete]_dos_ptm_adj <0 ..>
[Test Medication Complete]_dos_rte_adj <0 ..>
[Test Medication Complete]_dos_tot_adj <0 ..>
[Test Medication Complete.Main Component]_cmp_qty [1 mL]
[Test Medication Complete.Main Component]_orb_qty [10 mL..0,5 mL..49,5 mL]
[Test Medication Complete.Main Component]_orb_cnt <0 x..>
[Test Medication Complete.Main Component]_orb_cnc <0 x..1 x>
[Test Medication Complete.Main Component]_ord_qty <0 mL..>
[Test Medication Complete.Main Component]_ord_cnt <0 x..>
[Test Medication Complete.Main Component]_dos_qty [15 mL..75 mL]
[Test Medication Complete.Main Component]_dos_ptm [1,5 mL/dag..15 mL/dag]
[Test Medication Complete.Main Component]_dos_rte <0 ..>
[Test Medication Complete.Main Component]_dos_tot <0 ..>
[Test Medication Complete.Main Component]_dos_qty_adj [1 mL/kg..5 mL/kg]
[Test Medication Complete.Main Component]_dos_ptm_adj [0,1 mL/kg/dag..1 mL/kg/dag]
[Test Medication Complete.Main Component]_dos_rte_adj <0 ..>
[Test Medication Complete.Main Component]_dos_tot_adj <0 ..>
[Test Medication Complete.Main Component.Active Substance]_cmp_qty [10 mg]
[Test Medication Complete.Main Component.Active Substance]_orb_qty [100 mg..5 mg..495 mg]
[Test Medication Complete.Main Component.Active Substance]_cmp_cnc [10 mg/mL]
[Test Medication Complete.Main Component.Active Substance]_orb_cnc [0,5 mg/mL..2 mg/mL]
[Test Medication Complete.Main Component.Active Substance]_dos_qty [150 mg..750 mg]
[Test Medication Complete.Main Component.Active Substance]_dos_ptm [15 mg/dag..150 mg/dag]
[Test Medication Complete.Main Component.Active Substance]_dos_rte <0 ..>
[Test Medication Complete.Main Component.Active Substance]_dos_tot <0 ..>
[Test Medication Complete.Main Component.Active Substance]_dos_qty_adj [10 mg/kg..50 mg/kg]
[Test Medication Complete.Main Component.Active Substance]_dos_ptm_adj [1 mg/kg/dag..10 mg/kg/dag]
[Test Medication Complete.Main Component.Active Substance]_dos_rte_adj <0 ..>
[Test Medication Complete.Main Component.Active Substance]_dos_tot_adj <0 ..>
[Test Medication Complete.Diluent Component]_cmp_qty [1 mL]
[Test Medication Complete.Diluent Component]_orb_qty [0,5 mL..0,5 mL..40 mL]
[Test Medication Complete.Diluent Component]_orb_cnt <0 x..>
[Test Medication Complete.Diluent Component]_orb_cnc <0 x..1 x>
[Test Medication Complete.Diluent Component]_ord_qty <0 mL..>
[Test Medication Complete.Diluent Component]_ord_cnt <0 x..>
[Test Medication Complete.Diluent Component]_dos_qty [0,25 mL..40 mL]
[Test Medication Complete.Diluent Component]_dos_ptm <0 mL/dag..>
[Test Medication Complete.Diluent Component]_dos_rte <0 ..>
[Test Medication Complete.Diluent Component]_dos_tot <0 ..>
[Test Medication Complete.Diluent Component]_dos_qty_adj <0 ..>
[Test Medication Complete.Diluent Component]_dos_ptm_adj <0 ..>
[Test Medication Complete.Diluent Component]_dos_rte_adj <0 ..>
[Test Medication Complete.Diluent Component]_dos_tot_adj <0 ..>
[Test Medication Complete.Diluent Component.sodium]_cmp_qty [0,15 mmol]
[Test Medication Complete.Diluent Component.sodium]_orb_qty [0,08 mmol..0,08 mmol..6,16 mmol]
[Test Medication Complete.Diluent Component.sodium]_cmp_cnc [0,15 mmol/mL]
[Test Medication Complete.Diluent Component.sodium]_orb_cnc <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_qty <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_ptm <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_rte <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_tot <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_qty_adj <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_ptm_adj <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_rte_adj <0 ..>
[Test Medication Complete.Diluent Component.sodium]_dos_tot_adj <0 ..>
[]_adj_qty [15 kg]
"""

    ]
