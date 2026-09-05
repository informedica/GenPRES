# GenPRES – MainEHR Integration

<!-- Concepts, Rules and Guarantees are cited by number throughout this document and
     from the model script, so their numbering is load-bearing. Markdown restarts an
     ordered list after each intervening paragraph, which MD029 then reads as
     mis-numbering; renumbering would break every citation. -->
<!-- markdownlint-disable-file MD029 -->

## Use Cases

What the User does, and what they see. Each use case gives its goal, precondition and main path, then a trace whose steps cite the System Model, then extensions labelled by the step they branch from.

### Cast

1. User A: Prescriber  

2. User B: Prescriber  

3. User C: Reader  

4. Patient 1: no GenPRES PatientRecord yet  

5. Patient 2: has a GenPRES PatientRecord; its head is the most recent TreatmentPlan

### UC-1 User launches GenPRES

**Goal** User A opens GenPRES on the selected Patient, able to prescribe and sign.

**Precondition** User A is logged in at a MainEHR Workstation.

**Main path** User A selects Patient 1 and clicks the GenPRES button; GenPRES opens in the browser showing Patient 1\.

**Trace**

1. User A selects Patient 1 and triggers MainEHR LaunchScript.  

2. MainEHR LaunchScript seals the PatientId into a Launch under the key from the MainEHR database (Concept 3), opens GenPRES Client, and exits.  

3. GenPRES Client presents the Launch. GenPRES Server sends the browser to the IdentityProvider and gets it back with the BrowserIdentity: who is at the keyboard (Concept 4; Rule 4).  

4. GenPRES Server verifies the Launch — key, lifetime, single use (Rules 2, 3\) — and takes the PatientId.  

5. GenPRES Server asks the UserRegistry for the Role (Rule 5\) and for the Patient User A has active in MainEHR, which must be the Launch's (Rule 6), and checks that a PIN is set (Rule 24).  

6. GenPRES Server reads Patient 1's data from the PatientDataPlatform, once (Concept 2).  

7. GenPRES Server reads the GenPRES PatientRecord and picks the TreatmentPlan to start from (Rule 19). Patient 1 has none: the Session starts from nothing.  

8. GenPRES Server opens the Session, writes its SessionRecord, and closes any other Session of User A's or of this browser's, in one act (Rules 8, 40).  

9. GenPRES Server returns the SessionId, UserContext, PatientContext, the OrderContexts to start from, and the OpenedToken (Rule 34). It keeps nothing else (Rule 32).

**Extensions**

*1a No Patient is active.* The Launch carries none; steps 6–7 are skipped. User A can prescribe with hand-entered data but not open or submit a TreatmentPlan (Rule 13).

*1b The button is not User A's to press.* MainEHR LaunchScript refuses (Rule 1); nothing leaves the workstation.

*2a The key cannot be read.* MainEHR LaunchScript reports it and exits; nothing was opened.

*2b GenPRES Server is unreachable.* The browser shows its own error page; the Launch stays in the address bar and a refresh retries within its lifetime (Rule 2).

*3a GenPRES Server becomes unreachable after the page was served.* GenPRES Client retries from memory while the Launch lives (Rules 2, 39).

*3b The Launch is stolen before it is presented.* The thief's browser proves the thief, not User A (Rule 4), and the thief's own Role and own active Patient decide what opens (Rules 5, 6; Guarantee 5): no Role, or Patient 1 not active in the thief's own MainEHR — nothing opens. At most the thief gets the Session their own launch would have given, everything in it under their own name. User A's Launch is spent (Rule 2); A relaunches.

*3c The IdentityProvider cannot be reached, or does not recognise the browser.* No launch (Rule 7); GenPRES Client retries while the Launch lives.

*4a The Launch is expired, used by another browser, or not sealed under the key.* No launch (Rules 2, 3, 7); GenPRES Client asks for a relaunch from MainEHR.

*5a The UserRegistry returns no Role, or cannot answer.* No launch (Rule 7). GenPRES Client offers a fresh anonymous open (UC-7), carrying nothing over.

*5b The UserRegistry names another active Patient than the Launch's, or none.* No launch (Rules 6, 7). User A activates the right Patient in MainEHR and relaunches.

*5c The User is a Reader.* A Session opens from the most recent TreatmentPlan; signing is not offered, no PIN is asked (Rules 18, 19, 26).

*5d User A has no PIN yet.* UC-2.

*6a The PatientDataPlatform is unreachable or empty.* The launch continues without data; User A enters it by hand (Concepts 2, 15). TreatmentPlans open and submit as normal: the PatientId is there (Rule 13).

*8a User A already has an open Session.* It is closed and User A is told at this launch (Rules 8, 11\) — unless it was in this same browser, which owes nothing (Rule 8).

*8b Two launches of User A race.* The Database opens one and closes the rest in one act (Rules 8, 40); the loser is told as ext 8a.

*9a The Patient shown is not the one meant.* The Launch carried whatever Patient was active (Invariant 1\) — Rule 6 confirms the active Patient, not the intended one — and MainEHR never learns of the mismatch (Consequence 1). User A activates the right Patient and relaunches, which closes the wrong Session (ext 8a).

### UC-2 First launch as a Prescriber: no PIN yet

**Goal** User A gets a PIN so that later Submissions can be signed.

**Precondition** UC-1 step 5: Prescriber Role, no PIN set.

**Main path** GenPRES mails a one-time confirmation code; User A returns it with a chosen PIN; GenPRES mails again and the launch continues.

**Trace**

1. GenPRES Server finds no PIN for a Prescriber (Rules 24, 25\) and mails a one-time confirmation code (Rules 27, 37). GenPRES Client asks for confirmation code and PIN and offers nothing else.  

2. User A enters the confirmation code and a PIN.  

3. GenPRES Server verifies the confirmation code, sets the PIN — creating the UserCredential if GenPRES holds none — with a count of zero, records it, and mails User A (Rules 27, 28, 37, 46). The launch continues at UC-1 step 6\.

The confirmation code goes to the address the UserRegistry holds, so an unrecognised login never enrols (UC-1 ext 5a) and a Reader is never asked (ext 5c).

**Extensions**

*2a User A does not answer.* No PIN is set, no Session opens (Rule 7); the confirmation code expires and the next launch mails a fresh one.

*2b The confirmation code is wrong.* A few tries, then the confirmation code is void; a fresh launch mails a fresh one (Rule 37).

*2c Someone else sits at the workstation of a Prescriber who never enrolled.* The confirmation code went to User A's mail, which the other hands do not control (Rule 37; Possibility 1). Nothing is set, and the mail tells User A someone tried.

### UC-3 Prescribe and sign

**Goal** User A records orders for Patient 2 and takes responsibility for them, in one act.

**Precondition** UC-1: an open Session for Patient 2 from its head, Prescriber Role.

**Main path** User A builds up the orders and signs; the TreatmentPlan is committed.

**Trace**

1. User A adds and adjusts orders. Each change is computed by GenPRES Server, which keeps none of it (Rules 9, 32); nothing of the work exists outside the browser (Concept 16).  

2. User A signs: the whole WorkPlan goes with the OpenedToken (Rules 33, 34). Nothing newer exists, so nothing blocks (Rule 20). GenPRES Server re-reads the Patient Data (Rule 44\) and issues the SigningChallenge (Rule 43).  

3. GenPRES Client shows the challenge modally and asks the PIN (Rule 43). The commit verifies PIN, challenge and tokens, re-takes the Role, and appends the TreatmentPlan, in one transaction (Rules 23, 28, 38, 42). It now counts clinically (Rule 17).

**Extensions**

*1a The record moved on while User A works.* A response says a newer TreatmentPlan exists — whose, and when it was signed (Rule 21). Nothing is blocked yet (Rule 22); A opens the newer plan and reapplies when ready (UC-4).

*1b A new KnowledgeRuleSet is published while User A works.* The next computation runs under it (Concept 18): what no longer fits is shown. The challenge, too, is issued under the current set, and the signed plan records it (Rule 44).

*2a The record moved on unseen.* No response happened to tell A first: the Submission itself is refused (Rule 20), which is the notice; A opens the newest TreatmentPlan and reapplies (UC-4 step 4).

*2b The Patient Data changed, or cannot be read.* No challenge yet: User A is shown the data as it stands, or that it is unverified, and proceeds by returning the DataNoticeToken (Rule 44).

*3a Wrong PIN, or cancel.* No TreatmentPlan is committed and no token is spent (Rule 34). Wrong entries count across Sessions; at the limit the Session ends and signing locks for a growing delay (Rules 10, 28).

*3b A dose needs fixing on the challenge.* User A cancels, edits, and signs against a fresh challenge (Rule 43).

*3c Someone else takes the keyboard during the challenge.* Editing requires cancelling the modal; any other change no longer matches the challenge and is refused (Rule 43).

*3d A Submission arrives late, repeated or out of order.* Its challenge no longer matches or is spent, and it is refused (Rules 34, 43); a retry of one that was committed returns the first result (Rule 45).

*3e User A does not sign.* Nothing enters the record: the WorkPlan lives only in the browser and dies with it, or is carried into A's next Session for the same Patient (Concept 16; UC-8).

### UC-4 Two Users, one Patient

**Goal** Two Prescribers on the same Patient at once: neither sees the other's work, the first to sign wins, and the other is told the moment they next act.

**Precondition** UC-1 twice: User A and User B each hold a Session for Patient 2 (Rule 8 is per User).

**Main path** Both build orders; User A signs first; User B is told at B's next request and takes up A's plan.

**Trace**

1. Both edit. Neither sees the other's work, or knows of it: each WorkPlan lives only in its own Client (Rule 32; Guarantee 3).  

2. User A signs (UC-3); the plan counts (Rule 17).  

3. User B acts — any request — and the response says a newer TreatmentPlan exists: A's, signed at such a time (Rule 21). Nothing is blocked yet (Rule 22); B keeps working if B chooses.  

4. User B opens A's plan (Rules 18, 20), reapplies, and signs. Changed OrderContexts carry B's stamp, untouched ones keep A's (Rule 15).

**Extensions**

*3a User B's next request is the signing itself.* The Submission is refused (Rule 20\) — that refusal is the notice — and B continues as step 4\.

*3b Both sign at once.* The Database commits exactly one (Rule 36); the other continues as step 3\.

Nothing signed is ever lost: the record is append-only and every base is kept (Concepts 12, 13).

### UC-5 Someone else takes over the workstation

**Goal** User B, at a workstation User A left open, can look and explore but attest nothing — in A's name or anyone's.

**Precondition** UC-1: User A's Session for Patient 1 is open; A walks away.

**Main path** User B works in the browser; nothing reaches the record without A's PIN.

**Trace**

1. User B acts in GenPRES Client, which sends A's SessionId (Rule 12); GenPRES Server serves the open Session.  

2. User B prescribes. The WorkPlan carries no attribution and sits in no record (Concept 16); the Server keeps none of it (Rule 32).  

3. User B signs and is asked for User A's PIN (Concept 14), cancels, and nothing is committed (Rule 42).

**Extensions**

*2a User B signs in to the workstation as themselves and relaunches.* A Session of B's own opens; A's is untouched (Rule 8 is per User, and B's own session is another browser). B re-enters the work and signs as themselves.

*3a User B guesses.* At the limit the Session ends; User A is mailed and told at their next launch (Rules 10, 11, 27, 28). The lock belongs to the credential and grows with each guess (Rule 28); the reset stalls without A's mail (Rule 37; UC-6 ext 1a).

### UC-6 A User forgets their PIN

**Goal** User A gets a new PIN — and learns if someone else tried.

**Precondition** UC-1: an open Session and a PIN set but forgotten.

**Main path** User A asks for a reset; GenPRES mails a confirmation code; User A returns it with a new PIN, replacing the old in one act. Two mails: the confirmation code, and the notice of the change.

**Trace**

1. User A asks GenPRES Server to reset the PIN; a confirmation code is mailed (Rules 27, 37). The old PIN stands.  

2. User A enters confirmation code and new PIN; GenPRES Server verifies, replaces with a count of zero, records, and mails (Rules 27, 28, 46).  

3. User A signs with the new PIN, in the same Session.

**Extensions**

*1a User B triggers the reset at A's workstation.* The confirmation code goes to A's mail, which B does not control (Rule 37). The PIN stands and the mail tells A someone asked; A's own reset waits until that confirmation code is void (Rule 37).

*1b User A never returns the confirmation code.* Nothing changes; the confirmation code expires.

*1c The UserRegistry cannot say where to mail.* No confirmation code goes out and nothing is parked; User A is told, and the PIN in force stands (Rule 27). A remembered address is never used for a confirmation code (Rule 37).

*2a The confirmation code is wrong.* A few tries, then void; a fresh reset mails a fresh confirmation code, one at a time (Rule 37).

*2b The UserRegistry cannot be asked when User A answers.* The PIN is still replaced — the confirmation code already proved the mailbox — and the notice goes to the address the Session holds, the audit saying it was the fallback (Rule 27; Concept 9).

### UC-7 User opens GenPRES directly

**Goal** GenPRES without MainEHR: prescribe, never sign — decision support, not order management.

**Precondition** A browser that can reach GenPRES Server; no Launch, so no BrowserIdentity is asked for.

**Main path** GenPRES opens with no User and no Patient; User A enters details by hand and prescribes.

**Trace**

1. GenPRES Client has no Launch and asks for a Session without one; GenPRES Server opens an anonymous Session (Rule 14).  

2. User A prescribes by hand (Concept 15); the Server computes and keeps nothing (Rule 32).  

3. No TreatmentPlan can be opened or submitted, and there is nobody to sign as (Rule 13; Concepts 7, 14). The WorkPlan dies with the browser (Concept 16).

Neither the GenPRES PatientRecord nor the PatientDataPlatform is touched, and no UserRegistry check is made. Anonymous use is rate-limited and capped, and ends at an absolute limit (Rule 14). CDS for anyone; order management only through a launch.

**Extensions**

*1a The browser does present a Launch.* UC-1 from step 3\.

*1b The same browser later launches properly.* The launch replaces the anonymous Session (Rule 8); its WorkPlan is gone with the page.

### UC-8 A Session ends out from under the User

**Goal** User A learns once that their Session ended and can carry what was on screen into the next Session.

**Precondition** UC-3 step 1: unsigned work on screen.

**Main path** The Session idles out; the screen looks alive; A's next action fails and A is told at the next launch.

**Trace**

1. The idle clock runs out and the Session ends (Rules 9, 10); the Client cannot be told (Consequence 6).  

2. User A acts; GenPRES Server refuses and says why (Rule 11).  

3. The WorkPlan is still in the Client (Rule 32\) and may be carried into the next Session of the same User for the same Patient (Concept 15; Rule 33). Nothing else exists: what was not signed is nowhere but the browser (Concept 16).  

4. User A relaunches (UC-1), acknowledges the notice, and it never returns (Rule 11).

The carry-over of step 3 is a hand-off between tabs of the same browser: the relaunched tab, once its Session is open, asks the old tab — still open, still holding the WorkPlan in memory — to hand it over, for the same User and Patient only. Memory to memory, nothing stored anywhere (Concept 16): if the old tab is gone, so is the work.

**Extensions**

*1a GenPRES Server restarts instead.* Nothing ends (Rules 10, 32); the next request continues the Session, the idle clock permitting.

*1b GenPRES is upgraded instead.* Still nothing ends: A's Session is served by the version it opened on until it ends (Rule 32). Only if that version is withdrawn early does A end up in this use case — and then the carry-over of step 3 is not promised: a WorkPlan need not fit the next version, so A may have to re-enter the work.

*1c User A opens another Session at another workstation.* The launch ends the old Session and delivers the notice (Rules 8, 11; UC-1 ext 8a).

*2a No sweep has run.* The request itself ends the Session then and there (Rule 41).

### UC-9 A Reader consults a Patient

**Goal** User C sees the plan that counts, and is told when it moves on.

**Precondition** Patient 2 has a head; User A holds an open Prescriber Session for it; User C launches (UC-1 ext 5c).

**Main path** User C reads the plan; when User A signs a newer one, C is told at C's next action and opens it.

**Trace**

1. The Session opens from the most recent TreatmentPlan (Rules 18, 19); User C reads the plan that counts (Rule 17).  

2. User A signs a new plan meanwhile. At C's next action the response says a newer TreatmentPlan exists — whose, and when (Rule 21). C opens it (Rule 18).  

3. User C prescribes to explore (Concept 15); signing and the PIN are not offered (Rule 26).

### UC-10 User closes GenPRES

**Goal** No stray Session, no notice at the next launch.

**Precondition** UC-3: an open Session, its work signed.

**Main path** User A closes the Session in GenPRES Client; it ends as closed by the User and the next launch starts clean.

**Trace**

1. User A closes the Session; GenPRES Server marks the SessionRecord ended by the User (Rule 10; Concept 9).  

2. No notice follows, now or at the next launch (Rule 11).

**Extensions**

*1a Unsigned work remains.* GenPRES Client warns; closing drops it (Rule 32; Concept 16).

*1b User A closes the browser instead.* Nothing reaches the Server (Rule 10). The Session idles out and A is told at the next launch (Rule 11\) — a harmless notice.

### UC-11 A User's authority is withdrawn

**Goal** User A keeps anonymous CDS and nothing more; nothing of A's is left pending anywhere.

**Precondition** The UserRegistry stops returning a Role for A.

**Main path** A's next launch gets no Role and no Session; the anonymous open is offered.

**Trace**

1. User A launches; at UC-1 step 5 no Role comes back, so no Session opens (Rules 5, 7). User A accepts the anonymous open (UC-7).  

2. A's UserCredential remains but is inert: it carries no Role and there is no launched Session to sign in (Concept 7).  

3. The record holds exactly what A signed and nothing half-done: unsigned work never left A's browser (Concept 16; Guarantee 2).

**Extensions**

*1a The withdrawal happens while A's Session is open.* The Session keeps its Role (Rule 5\) but every signature re-takes it (Rule 38), so signing is blocked at once; reading and prescribing ride out the Session, but nothing of it can be committed any more.

## System Model

### Actors

The kinds of participants that appear in the use cases. \[ours\] \= under construction. \[given\] \= existing infrastructure, not ours to change. The User is neither — they are who the system is for.

1. MainEHR Workstation \[given\]: the running EHR Client.  

2. MainEHR LaunchScript \[ours\]: a VB.NET script behind a button in MainEHR Workstation.  

   - Reads a key from the MainEHR database, seals the Launch, opens the browser, exits.  
   - The only part of MainEHR we control, and only what its scripting context allows: a database read and a browser launch.



3. GenPRES Client \[ours\]: GenPRES UI running in the browser that carries the User's hospital sign-on (Actor 8).  

   - Delivered in two parts: a thin launch shell before a Session exists — redirects, refusals, enrolment — and the full Client once one does; the heavy part loads only after the last navigation of the launch.



4. GenPRES Server \[ours\]: GenPRES backend.  

5. GenPRES Database \[ours\]: two stores, one writer — GenPRES Server.  

   - The clinical store holds the TreatmentPlans of the GenPRES PatientRecords, each with its base (Concept 13), and is what the PatientDataPlatform copies.  
   - The private store holds everything else — SessionRecords, UserCredentials, the spent-state of Launches (Rule 2\) and of Tokens (Concept 17), the audit — and is never copied anywhere.  
   - Both stores are append-only, by definition: rows are added, never changed — the record by its nature (Concept 12), Sessions and UserCredentials as chains of events, spent-marks and request keys as rows written once. What may be forgotten (an old idle heartbeat) is dropped whole, never rewritten.



6. PatientDataPlatform \[given\]: a shared, read-only copy of the databases of MainEHR, GenPRES and other applications. How data gets there is out of scope.  

7. User: person who uses MainEHR and GenPRES.  

8. IdentityProvider \[given\]: Entra ID.  

   - Already signs the User into Windows with badge and PIN, and recognises the browser in that session without a prompt.  
   - Says who is at a browser — no Role, no Patient.



9. UserRegistry \[given\]: says who a login belongs to, what that person may do, how to reach them by mail — and which Patient that person has active in MainEHR right now.  

10. MailService \[given\]: sends mail to a person, outside GenPRES and outside MainEHR.

### Roles

The kinds of authority a User can hold; what each may do. The UserRegistry decides the Role. MainEHR and GenPRES enforce it independently, each within its own application.

1. Prescriber: may read and write — writing meaning creating TreatmentPlans.  

2. Reader: may never create a TreatmentPlan. Like any User they may prescribe within their Session (Concept 15), but nothing of it can be signed.

### Concepts

The things passed between actors, or held by them, and what each one means.

1. **UserContext**: User identification and User Role.  

2. **PatientContext**: PatientId and Patient Data relevant for GenPRES.  

   - Only a launch can supply the identification; the data the User can also enter by hand.  
   - Launched, the data is read from the PatientDataPlatform once, at the launch, and not refreshed while the Session lives: that it may go out of date is accepted.



3. **Launch**: the active Patient's PatientId, if any, sealed by MainEHR LaunchScript under a key it shares with GenPRES Server.  

   - Single use, short-lived, opaque, and carrying no login (Rule 4).  
   - Its Patient is checked against the UserRegistry at the launch (Rule 6): the Session opens only on the Patient the User really has active in MainEHR.  
   - Patient-level authorisation is MainEHR's; GenPRES enforces nothing finer.



4. **BrowserIdentity**: who the IdentityProvider says is at the browser presenting a Launch — the login MainEHR and the UserRegistry know the person by.  

   - Obtained once, at the launch; it is the Session's User (Rule 4).  
   - It is the workstation's login, not proof that the person is still there: the PIN is what protects the signature (UC-5).



5. **MainEHR Session**: the period a User is logged in at a MainEHR Workstation. Many Patients can be handled in it, one active at a time.  

6. **MainEHR PatientRecord**: all patient data maintained by MainEHR.  

7. **GenPRES UserCredential**: held by GenPRES for one User, keyed by who they are, not by their renameable login.  

   - Holds the login the UserRegistry currently knows the person by, a PIN if one is set, and the count of consecutive wrong PIN entries (Rule 28).  
   - The PIN is optional — without one the User cannot sign; once set, it is only ever replaced, never merely removed (Rule 37).  
   - Carries no Role and no identity of its own: it only lets a User prove, during a Session, that they are the person named in the UserContext.



8. **GenPRES Session**: the interaction of a User with GenPRES — for a Patient if the launch supplied one, otherwise for no Patient.  

   - Opened without a launch it is anonymous (Rule 14); only a Session with a Patient allows opening or submitting TreatmentPlans (Rule 13).  
   - It has no state in GenPRES Server between requests: its identity and standing live in its SessionRecord, its work in GenPRES Client (Rule 32).



9. **GenPRES SessionRecord**: binds a SessionId to exactly one User — or to none, when the Session is anonymous — and to a Patient if the Session has one.  

   - Records whether the Session is open or ended, when it last heard from the Client (Rule 9), and whether the User has acknowledged its ending (Rule 11).  
   - Holds the mail address the registry last gave for this User — written at the launch and whenever a mail is sent — read only as the fallback for a notice (Rule 27).  
   - Realised as appended events (Actor 5; Rule 40); "when it last heard" is the newest entry of an activity stream beside it, where only the newest entry counts.  
   - Kept after the Session ends.



10. **OrderContext**: a PatientContext together with the OrderScenarios currently under consideration for that Patient.  

    - Its identity persists across TreatmentPlans.  
    - Carries the UserContext of the User whose Session last changed it — stamped at each Submission (Rule 15), so one that is never submitted carries none.



11. **OrderScenario**: one proposed Order together with the prescribing information that gives it meaning but is not part of the Order itself.  

12. **GenPRES PatientRecord**: the append-only history of a Patient in GenPRES — a sequence of TreatmentPlans, every one signed and carrying that Patient's PatientId: the one thing no TreatmentPlan may change.  

13. **TreatmentPlan**: the Patient's treatment plan as it stood when signed — a set of the Patient's OrderContexts.  

    - Carries the UserContext of the User who signed it, the Session it was created in, and a reference to the TreatmentPlan it was created from — its base — if any.  
    - Records the Patient Data it was built on: the values, where each came from (the PatientDataPlatform, or entered by hand) and when they were read (Concept 2\) — so every plan can be explained from its own record.  
    - Records the KnowledgeRuleSet it was checked under (Concept 18; Rule 44).  
    - Never changes (Rule 16).



14. **Submission**: submitting is signing — the WorkPlan goes to GenPRES Server with the PIN of the Session's User and becomes a TreatmentPlan.  

    - There is no other way a TreatmentPlan comes into being, and no saving without signing.  
    - None is ever changed or saved again: changing means creating a new one whose base is the old.



15. **Prescribing**: changing, within a GenPRES Session, the Patient Data of the PatientContext and adding, removing or changing OrderContexts.  

    - Touches only the WorkPlan (Concept 16): nothing reaches the GenPRES PatientRecord until a TreatmentPlan is signed.  
    - GenPRES Server computes on what the Client sends — Patient Data included — and keeps none of it.



16. **WorkPlan**: the plan being composed in GenPRES Client — the Patient Data and the OrderContexts under the User's hands (Concept 15).  

    - Changeable, carries no attribution, sits in no record: it becomes a TreatmentPlan only by being signed (Concept 14), and otherwise dies with the browser.  
    - Held only by its own Client (Rule 32), which may carry it into the next Session of the same User for the same Patient (UC-8).  
    - The cart of the shopping-cart metaphor (Guarantee 3).



17. **Token**: a short-lived note GenPRES Server writes to itself and hands to GenPRES Client, which returns it unaltered — the Server's memory across requests, where it keeps none of its own (Rule 32).  

    - Bound to what it names, impossible for a Client to make, and spent by the Submission it accompanies; a refused Submission spends none (Rule 34).  
    - Three exist: the OpenedToken — which TreatmentPlan the Session opened (Rule 34); the SigningChallenge — the exact plan a signature would approve (Rule 43); the DataNoticeToken — the Patient Data the User was shown had changed (Rule 44).  
    - The OpenedToken travels with every request, so every response can say whether the record moved on (Rule 21), and every Submission proves it: has anything appeared since the User started (Rule 20)?  
    - A signing Submission carries the SigningChallenge besides: is the plan committed the plan the User last saw (Rule 43)?  
    - One guards where the User began, the other what the User reviewed; between them the Server needs no memory of the Session at all. The Client holds a token just long enough to return it; the Server holds only the key that verifies them and the spent-marks of those already used (Actor 5).



18. **KnowledgeRuleSet**: the versioned, published set of knowledge rules — dose rules and their kin — that GenPRES computes with.  

    - The Server computes every request with the latest published set, so a set published while a User works reaches their WorkPlan at its next computation: what no longer fits is shown, not silently kept.  
    - Every published set is kept, identified by its version, so a signed plan can always be explained from the set it names (Concept 13; Rule 44).

### Constraints

Notation — how to read the edges below. Not itself a constraint.

- X \-\> Y — X initiates a connection to Y and receives Y's response on it. Grants initiation in that direction only; the reverse is never implied.  

- X \=\> Y — X launches Y with initial parameters. One-way: no response, no error path back.  

- X \<-\> Y — interaction, not request–response: a User can read what Y shows and act on it.

Any pair without an edge cannot exchange data at all. Edges do not compose — no component relays on another's behalf unless stated.

**User Interaction** — which components a User can read and act on, or start.

1. Any User \<-\> MainEHR Workstation  

2. Any User \<-\> MainEHR LaunchScript — the User starts it; while it runs it can report its own acts back, and it exits at once, so nothing later ever comes from it.  

3. Any User \<-\> GenPRES Client

**Communication** — which components may reach which, and nothing else is possible. Edges touching a \[given\] component are what the deployment allows; edges between \[ours\] components are what we choose to build.

1. MainEHR Workstation \-\> UserRegistry  

2. MainEHR Workstation \-\> PatientDataPlatform  

3. GenPRES Client \-\> IdentityProvider  

4. MainEHR LaunchScript \=\> GenPRES Client  

5. GenPRES Client \-\> GenPRES Server  

6. GenPRES Server \-\> IdentityProvider  

7. GenPRES Server \-\> UserRegistry  

8. GenPRES Server \-\> PatientDataPlatform  

9. GenPRES Server \-\> GenPRES Database  

10. GenPRES Server \-\> MailService

### Consequences

Derived from the edges above — not new assertions, and not negotiable without changing an edge.

1. MainEHR LaunchScript learns nothing after the launch.  

   - What it can report to the User (User Interaction 2\) ends with its own acts: reading the key (UC-1 ext 2a) and launching the browser.  
   - Expired Launch, GenPRES Server down, wrong patient — none of it reaches MainEHR LaunchScript.  
   - Error handling falls to GenPRES Client — except when GenPRES Server is unreachable: the Client is served by the Server, so then no Client is served either and the User is left with the browser's error page.



2. The key is all that authenticates a Launch: whoever holds it can seal any Patient into one.  

   - But a forged Patient opens nothing: the UserRegistry must confirm it as the User's active Patient (Rule 6).  
   - And the User a Launch cannot name at all: edges 3 and 6 settle that (Rule 4).



3. Only GenPRES Server knows whether a Launch was used, and it cannot tell MainEHR LaunchScript, which has exited.  

4. The Launch travels in a URL, so it ends up in browser history, the address bar, and possibly referrer and proxy logs — hence single use, short lifetime.  

5. Three actors run on the User's PC: MainEHR Workstation, MainEHR LaunchScript, and the browser with GenPRES Client.  

   - That one machine must reach: the UserRegistry and the PatientDataPlatform (MainEHR's own work), the key in the MainEHR database (the LaunchScript's), and GenPRES Server and the IdentityProvider (the Client's).  
   - Everything GenPRES Server needs — the UserRegistry, the PatientDataPlatform, its Database, the IdentityProvider, the MailService — it reaches from where it runs; none of it touches the User's PC.



6. GenPRES Server cannot reach a GenPRES Client (edge 5 goes one way only).  

   - A Client only learns its Session ended — or that the record moved on (Rule 21\) — at its next request.  
   - Until then the screen shows what it last heard.

### Invariants

Always true of the environment. Given: not ours to change.

1. A User has at most one active Patient at any moment in a MainEHR Session.

### Possibilities

May occur in the environment. Given: not ours to prevent, only to handle.

1. Users can leave a logged in MainEHR Session open and another User can act in it.  

2. Multiple Users can have the same Patient active each in their own MainEHR Session.

### Rules

What the \[ours\] components must enforce. Chosen, and changeable by decision. One assertion each; grouped for reading, numbered straight through for citing.

**Launch**

1. MainEHR LaunchScript decides which MainEHR User may run it, and opens GenPRES. The Session's User is Rule 4's.  

2. A Launch is accepted once; a second presentation is refused, unless it comes from the same BrowserIdentity within the Launch's lifetime, in which case it is answered as the first was (Rule 45\) and nothing is opened twice.  

   - The spent-marks are checked as soon as the Launch is verified, so a second use is refused before any further work — an early check, and only a check: it decides nothing.  
   - Spent is a mark in the GenPRES Database, written in the same act that opens the Session (Rule 40), and that act alone decides: when the same Launch is presented twice and both pass the early check before either has opened, still only one Session opens. Server memory cannot hold the mark — a restart ends nothing (Rule 32\) and more than one Server may run (Rule 36).



3. A Launch is accepted only within its lifetime.  

4. The Session's User is the BrowserIdentity (Concept 4), never anything the Launch says.  

5. GenPRES Server takes the Role from the UserRegistry at each launch, never from the Launch.  

6. GenPRES Server asks the UserRegistry, at each launch, which Patient the User has active in MainEHR, and opens the Session only for that Patient.  

   - A Launch naming another Patient, or a User with none active, opens nothing.  
   - Checked once, at the launch — like the Patient Data (Concept 2).



7. If a launch cannot be honoured, no GenPRES Session is opened by it.  

   - Not honoured: no Launch, no BrowserIdentity, no Role, another active Patient (Rule 6), or a required PIN that is still missing after enrolment failed or was abandoned.  
   - A missing PIN alone refuses nothing: it suspends the launch into enrolment (Rule 25; UC-2), and the launch continues once the PIN is set. Only an enrolment that ends without one leaves the launch unhonoured.  
   - There is no silent fallback: at most, GenPRES Client offers the User a fresh anonymous open (Rule 14; UC-7), which carries nothing over from the launch — no User, no Patient.

**Session**

8. A User has at most one open Session, and so has a browser; opening another closes the rest.  

   - One replaced in its own browser owes no notice (Rule 11).  
   - The limit is per User, not per Patient.



9. Every request from GenPRES Client refreshes its Session's idle clock.  

10. A GenPRES Session ends when the User closes it, when it has been idle too long, at its absolute lifetime (Rule 30), at the wrong-PIN limit (Rule 28), or when the same User or browser opens another (Rule 8).  

    - Closing is an explicit act; a vanished browser is left to idle out.  
    - A Server restart ends nothing (Rule 32).



11. When a Session ends other than by the User closing it, or replacing it in the same browser (Rule 8), the User is told at their next launch and acknowledges it there.  

    - A Client still holding the SessionId is only refused, with the reason.  
    - Acknowledged once, the notice never returns.



12. The SessionId is a bearer credential, so it never travels in a URL and never sits where script can read it: an HttpOnly, Secure, SameSite=Strict cookie.  

    - A request that changes anything is refused unless its Origin is GenPRES's own.



13. A GenPRES Session without a PatientId lets the User prescribe (Concept 15), Patient Data included, but a TreatmentPlan cannot be opened or submitted.  

14. A Session opened without a launch is anonymous: no User, no Role, no PatientId.  

    - Rule 8's per-User limit and Rule 11 do not apply to it — its browser's limit does — nor does idling: it ends when closed, replaced, or at an absolute limit.  
    - Opens beyond a configured number of open anonymous Sessions are refused without a SessionRecord.  
    - It can commit nothing (Rule 13), so Rules 40–45 have nothing to guard in it.

**Record**

15. Every TreatmentPlan is created under the credentials of exactly one User — the Session's — and carries that User's identity.  

    - Within it, every OrderContext changed in the Session is stamped with that same UserContext; an unchanged OrderContext keeps the stamp it had.



16. A TreatmentPlan never changes: it is corrected only by a newer one whose base it is.  

    - And it never goes unreadable: every later version of GenPRES must still open every plan ever signed — an old plan can be tomorrow's base, and the record must stay explainable (Concept 13).



17. Only the most recent TreatmentPlan counts clinically.  

18. TreatmentPlans are open to every User, to read: any of them may be opened, but only the most recent can be built upon — opening an older one leaves Submission blocked (Rule 20).  

19. A User starts with the most recent TreatmentPlan; where none exists, from nothing.  

20. A User may submit a new TreatmentPlan, unless a TreatmentPlan exists that is newer than the one the User opened with.  

    - Opening that newest TreatmentPlan makes it the one the Session opened with — after that, Submission is possible again (UC-4).

**Notification**

21. With every response, GenPRES Server compares the head of the GenPRES PatientRecord against the TreatmentPlan named by the request's OpenedToken (Rule 34): if a newer one exists, the response says so — whose it is and when it was signed.  

    - Two references compared — quick, cheap, no state.



22. The notice informs and gates nothing: no acknowledgment, no token — Rule 20 is the only guard.  

    - The Server cannot push (Consequence 6), so the User learns at their own next request; when that next request is the Submission itself, its refusal (Rule 20\) is the notice.

**Signing**

23. GenPRES Server is the only party that verifies a GenPRES UserCredential; the PIN never leaves GenPRES.  

24. Every launch checks whether a PIN is set for the login.  

25. A Prescriber with no PIN must set one before the launch continues, and only after the UserRegistry has recognised their login.  

26. A Reader is never asked for a PIN: a Reader never creates a TreatmentPlan (Roles), so they have nothing to prove.  

27. GenPRES Server mails the User and records the change on every setting of a PIN and every replacement of one, the first setting included — and when the wrong-PIN limit is reached (Rule 28).  

    - The address is the UserRegistry's, asked fresh on the request that sends the mail — before the act for a confirmation code or a replacement, after it for the wrong-PIN limit, which only the commit discovers. So a changed address takes effect at once, and no copy goes stale in GenPRES.  
    - The audit names the address every mail went to (Rule 46), so "I never received it" can be answered from GenPRES's own records.  
    - When the registry cannot answer: a notice may fall back to the address the SessionRecord holds (Concept 9), and the audit says it did — but a confirmation code is never sent to a remembered address: no fresh answer, no code, and the PIN stands (Rule 37). A notice degrades; a credential fails closed.



28. Wrong PIN entries count per UserCredential, across Sessions, in one conditional operation at the Database (Rule 40).  

    - At the limit the Session ends (Rule 10\) and signing locks for a delay that doubles with each further wrong entry and decays with time; the reset (Rule 37\) lifts it at once.  
    - A correct entry, or a new PIN, resets the count.

**Configuration**

29. A Launch lives long enough to carry one launch — a page load, the IdentityProvider round trip, and a retry or two — and no longer.  

30. A Session lives long enough to span the gaps between a clinician's actions, and no longer than a shift whatever happens in it.  

    - GenPRES Client sends no request without a user action.



31. The wrong-PIN limit is small enough to make guessing hopeless, large enough to forgive mistyping — and the PIN itself short enough to remember, large enough in its space that the limit keeps guessing hopeless.

**State** — where Session state lives; chosen so that GenPRES Server keeps none of it.

32. GenPRES Server holds no Session state between requests: the WorkPlan (Concept 16\) lives in GenPRES Client, and a Session's identity and standing live in its SessionRecord in the GenPRES Database.  

    - Two Users' work cannot meet in the Server, because the Server holds neither.  
    - A restart therefore ends nothing: the next request continues the Session (Rule 10).  
    - An upgrade is not a restart — it drains instead of breaking: new launches open on the new version, open Sessions are served by the old until they end, which Rule 30 bounds at a shift. Two versions running side by side is no worse than two Servers (Rule 36): the Database decides what is committed.



33. GenPRES Server takes the User and the Patient of a request from the SessionRecord, never from what the request carries — and a Submission whose OrderContexts name another Patient than the SessionRecord's is refused whole (Guarantee 1).  

34. The TreatmentPlan a Session opened with travels as the OpenedToken (Concept 17\) — bound to the Session, the Patient and the TreatmentPlan.  

    - Returned by GenPRES Client with every request, so that every response can say whether the record moved on (Rule 21), and verified at every Submission (Rules 19, 20).  
    - It works exactly once as a Submission's proof: consumed by the Submission that is committed and re-issued over the new baseline; a refused Submission consumes nothing.  
    - A spent or expired one is refused.



35. The stamps of Rule 15 are computed by GenPRES Server against the base TreatmentPlan; a stamp arriving from GenPRES Client is never accepted.  

36. The Rule 20 check and the append are one act at the GenPRES Database: a TreatmentPlan is appended only if none newer than the one its Session opened with has appeared meanwhile.  

    - More than one GenPRES Server may run; the Database decides which is committed.

**Security** — what \[ours\] enforces against a hostile environment.

37. A PIN is set or replaced only by its User: GenPRES Server mails a one-time confirmation code (Rule 27), and returning it with the chosen PIN sets the PIN in one act — at enrolment (UC-2) as at a reset (UC-6); the old PIN stands until then.  

    - A confirmation code is void after its lifetime or a few wrong entries; one confirmation code at a time per credential, so a request while one is outstanding mails nothing until that confirmation code is void.  
    - Changing a PIN without a reset requires the current PIN.



38. Every signature re-takes the Role from the UserRegistry: authority withdrawn since the launch blocks the signature at its commit.  

    - If the registry cannot answer, the launch's Role stands for a bounded grace after the launch, then the signature fails closed.



39. GenPRES Client erases the Launch from the URL and the browser history at first presentation.  

    - Until the Client first runs, the URL itself is the only copy and the browser's refresh the only retry (UC-1 ext 2b).  
    - It keeps the Launch only in memory for retries within its lifetime (Rules 3, 29\) and in the request state across the IdentityProvider round trip.  
    - GenPRES Server serves the Client so that nothing of a Session is cached or carried in a referrer, and no script but the Client's own runs in its pages.

**Atomicity** — what must be one act at the GenPRES Database.

40. Every change to a SessionRecord is one conditional operation, guarded by the state it expects.  

    - An ended Session can never return to open.  
    - One open Session per User and per browser (Rule 8\) is a Database constraint, enforced in the same act that opens the next.  
    - The append-only Database (Actor 5\) enforces both by shape: a Session is a chain of events, each opening naming the Session it replaces, with uniqueness on that predecessor — the Database decides races, the newest of a chain is the open one, and an ended Session returning to open cannot even be written.



41. Expiry is checked when a request arrives, not only by a sweep: a request from a Session past its idle limit ends the Session then and there (Rules 9, 10\) — it does not refresh it.  

42. Committing a Submission is one transaction: at its commit the Database re-verifies everything the request rests on, and all of it holds together, or the Submission is refused and nothing is committed.  

    - Re-verified: the Session open, unexpired, and for this User and Patient (Rules 40, 41); the Role (Rule 38); the tokens (Rules 34, 44); the head (Rule 36); the challenge (Rule 43); and the PIN against the UserCredential as it stands at that moment, replaced or locked included (Rules 23, 28).



43. A signature approves exactly what was shown.  

    - GenPRES Server issues the SigningChallenge (Concept 17), naming the plan to be signed — content, base, Patient.  
    - GenPRES Client shows it modally: sign as shown, or cancel and edit.  
    - The PIN comes back with the challenge, and the commit checks that the plan submitted is the plan named, then consumes it (Rule 42).



44. Before issuing the SigningChallenge, GenPRES Server re-reads the PatientDataPlatform and takes the current KnowledgeRuleSet (Concept 18).  

    - If the data changed, or cannot be read, no challenge is issued yet: the User is shown the data as it stands, or that it is unverified, and proceeds by returning the DataNoticeToken (Concept 17).  
    - The challenge is computed under the current KnowledgeRuleSet: a set published since the User began surfaces its conflicts here, before anything is signed.  
    - The signed plan records the data the User saw and the set it was checked under (Concepts 13, 18).



45. A request that changes anything (i.e. writing to the GenPRES Database) can take effect only once.  

    - A Submission carries a key of its own: the Database commits a key once, and a retry returns the first result (UC-3 ext 3d).  
    - Every other change is one conditional write, guarded by the state it expects (Rule 40): a retry finds the state already changed and is answered from it, never re-applied.

**Audit** — the record of the acts around the record.

46. GenPRES Server appends to the audit, in the private store.  

    - Recorded: every launch, honoured or refused; every Session opening and ending, with the reason; every Submission, committed or refused, failed PIN entries included; every PIN change; every refused request, anonymous ones by count.  
    - Append-only; who reads it is out of scope (Guarantee 4).

### Guarantees

What the Rules add up to. Derived, not asserted: each holds because the Rules cited enforce it, and none is negotiable without changing a Rule.

1. **One constant.** A GenPRES PatientRecord is a sequence of TreatmentPlans in which the PatientId is the only constant.  

   - The Patient Data, the orders and the ordering User may all differ from TreatmentPlan to TreatmentPlan (Concepts 12, 13, 15).  
   - Only a launch supplies a PatientId (Concept 2\) and no Session signs without one (Rules 13, 14), so no hand ever changes it.



2. **One version.** At any moment exactly one TreatmentPlan is the visible version of the PatientRecord and the only starting point for updating it: the most recent (Rules 17–19).  

   - Nothing else can be built upon (Rule 20), and nothing unsigned exists outside its own browser (Concept 16).  
   - Reading is wider than building: the whole history is open to read (Rule 18\) — old versions the record keeps, from which nothing grows.



3. **Carts and one checkout.** Changing orders works like a shopping cart per User with a single shared checkout (signing) — the cart being the WorkPlan (Concept 16).  

   - The cart is private because of where it lives: in the User's own GenPRES Client, and GenPRES Server keeps none of it (Rule 32; Concepts 15, 16).  
   - Signing is the only checkout, and there is one (Concept 14; Rules 17, 36): the first User to sign wins the version, and every other WorkPlan must be rebuilt on top of it (Rules 19, 20; UC-4) — its owner told at their next request (Rules 21, 22).  
   - A cart that is never checked out leaves no trace in the record.



4. **Audit.** Every version of every order and every act around the record is on the record — and nothing secret rides along with the copy.  

   - A TreatmentPlan carries the User who signed it (Concepts 13, 14; Rule 15), and every OrderContext in it carries the User whose Session last changed it (Concept 10; Rule 15).  
   - The record keeps every version: append-only, each TreatmentPlan with its base (Concepts 12, 13\) — a full audit trail of every version of every OrderContext, held in the clinical store.  
   - The copy the PatientDataPlatform takes names nothing in the private store (Actors 5, 6): SessionRecords, UserCredentials and tokens are never copied.  
   - Beside it stands the security audit (Rule 46): who launched, opened, submitted, signed, failed and changed what, and when.  
   - Reading either is out of scope for this document: no Session shows them (Rule 18).  
   - A signature proves two things: the IdentityProvider saw this person at the launch (Concept 4; Rule 4), and someone who knew this User's PIN was at the signature (Rules 23, 43; UC-5). It does not prove they were the same person — so it is not claimed as proof the signer cannot deny. That claim waits until the sign-on itself confirms the person at the moment of signing (Open Question 3).



5. **A stolen Launch steals no authority.** Whoever presents a Launch is identified as themselves (Rule 4), gets their own Role (Rule 5), and gets only the Patient they themselves have active in MainEHR (Rule 6).  

   - No Role: nothing opens (Rule 7).  
   - A Reader: reading only — nothing can be signed (Roles; Rule 26).  
   - A Prescriber: exactly the Session their own launch would have given, every act in it under their own name (Rules 15, 46).  
   - What the thief gains is nothing; what the victim loses is a spent Launch (Rule 2\) and one relaunch.

### Open Questions

Decisions not yet made. Each one blocks something.

1. **Mail deliverability.** Rule 27's notices and Rule 37's confirmation codes hold only if the UserRegistry address is current and MailService delivers.  

   - Neither can be checked from here — and where the hospital mailbox is open on the very workstation Possibility 1 is about, the answer rests on Open Question 3\.  
   - Blocks: the failure paths of UC-2 step 3 and UC-6; gone if Open Question 3 removes the confirmation code.



2. **Payload.** Under Rule 32 the whole WorkPlan (Concept 16\) travels with every computing request and every Submission.  

   - Whether that is acceptable is a measurement, not a judgement.  
   - Blocks: nothing yet — but a bad number would force a server-side cache of the WorkPlan, which must then be built as an optimisation the Rules never depend on, losable without breaking anything.



3. **Step-up signing — pending the hospital's answer.** GenPRES runs a PIN of its own only because it cannot yet reuse the hospital's sign-on at the moment of signing. Two questions are with IT, one per frequency:  

   - For the frequent act — every signature: can the badge product confirm a single action with its PIN alone? (A badge re-tap logs the workstation out, so the tap cannot be the gesture.)  
   - For the rare act — setting or replacing a PIN: does Entra ID's forced re-authentication prompt usably on these workstations, and asking for which credential?  
   - Yes to the first: the hospital's PIN replaces the GenPRES PIN for signing — Rules 23–28 and 37, Concept 7, UC-2 and UC-6 all go — and non-repudiation can be claimed, on one condition: the fresh proof signs over the SigningChallenge's digest and is kept with the plan, so every signature carries evidence GenPRES cannot make. "The software wrote it, not me" is then answered by producing it, or proven by its absence.  
   - Yes only to the second: signing keeps the GenPRES PIN (a full re-authentication at every signature is too disruptive), but it replaces the mailed confirmation code for the rare acts — Open Question 1 gone.  
   - Blocks: which of the two the next revision is.



4. **Finer patient authorisation.** The launch, confirmed against the UserRegistry (Rule 6), says this User has this Patient open in MainEHR right now; GenPRES enforces nothing finer.  

   - No care relationship, encounter, or co-sign requirement: only MainEHR knows them.  
   - Blocks: any rule finer than the Prescriber/Reader split.



5. **A tamper-resistant audit.** The Database only ever adds rows (Actor 5), so within GenPRES any changed row is tampering by definition — but the administrator who runs the store can still change rows, and nothing outside GenPRES would notice.  

   - Its schema is GenPRES's own, not HL7 AuditEvent.  
   - Blocks: audit that binds anyone but GenPRES.



6. **Proof under concurrency.** The Atomicity rules (40–45) are stated, not proven.  

   - The append-only shape (Actor 5; Rule 40\) already settles two invariants by construction — an ended Session never reopens, one open Session per chain — and event chains are easier to model-check than guarded updates.  
   - What remains to prove is what the conditional appends promise: no commit after revocation or expiry, one result per key — before the Guarantees are claimed under load.  
   - Blocks: nothing in the design; everything in the confidence.



7. **Patient identity across systems.** The PatientId is the one thing no TreatmentPlan may change (Guarantee 1), so a MainEHR patient merge or duplicate registration cannot be reflected.  

   - A corrective plan fixes a wrong plan, not a wrong Patient.  
   - Needs the PatientDataPlatform to carry merge history, which is \[given\].  
   - Blocks: any promise about records after a merge.



8. **Interrupted work.** With signing the only way anything persists, a half-finished plan lives only in its browser (Concept 16).  

   - A shift change, a crash or a closed browser loses it, and the carry-over (UC-8) helps only the same User in the same browser.  
   - Whether clinical practice can live without parking unfinished plans is for user testing to answer.  
   - Blocks: nothing in the design; acceptance of the workflow.

## Appendix Simulation Runs

*The traces are produced by `Integration.fsx` (see [README](README.md)); they are written to `Integration.run.txt` beside the script and are not reproduced here.*  
