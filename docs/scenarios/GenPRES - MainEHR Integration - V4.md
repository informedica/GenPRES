# **`GenPRES – MainEHR Integration`**

## **`Use Cases`**

`What the User does, and what they see. Each use case states its goal and precondition, then the main path in the User's language, then a trace. Each trace step is justified by an edge in the System Model below, or by an internal action of a single actor. Extensions are labelled by the step they branch from.`

### **`Cast`**

`Named actor instances used by the use cases. Their state is the state before the first use case runs; later use cases inherit whatever earlier ones left behind.`

1. `User A: Prescriber`  
2. `User B: Prescriber`  
3. `User C: Reader`  
4. `Patient 1: no GenPRES PatientRecord yet`  
5. `Patient 2: a GenPRES PatientRecord whose head is a Signed TreatmentPlan`  
6. `Patient 3: a GenPRES PatientRecord whose head is an Unsigned TreatmentPlan saved by User A, over an older Signed one`

### **`UC-1 User launches GenPRES`**

**`Goal`** `User A gets GenPRES open on the Patient they have selected, able to prescribe, save and sign. Precondition User A is logged in with a UserName and Password at a MainEHR Workstation. Main path User A selects Patient 1 and clicks the GenPRES button. GenPRES opens in a browser, already showing Patient 1, and User A can prescribe, save and sign.`

**`Trace`**

1. `User A selects Patient 1.`  
2. `User A triggers MainEHR LaunchScript.`  
3. `MainEHR LaunchScript asks the Broker to prepare a launch for User A's MainEHR login and Patient 1.`  
4. `The Broker creates a LaunchAssertion and returns a LaunchCredential referring to it.`  
5. `MainEHR LaunchScript opens GenPRES Client with the LaunchCredential.`  
6. `MainEHR LaunchScript exits.`  
7. `GenPRES Client presents the LaunchCredential to GenPRES Server.`  
8. `GenPRES Server redeems it at the Broker (Rule 4) and receives the LaunchAssertion: a login and a Patient.`  
9. `GenPRES Server asks the UserRegistry who that login is and what Role they hold, and builds the UserContext (Rule 5).`  
10. `GenPRES Server checks whether a PIN is set for that login (Rule 23). User A holds the Prescriber Role and has one, so the launch continues; the PIN itself is asked for only at Signing (Concept 14).`  
11. `GenPRES Server asks the PatientDataPlatform for Patient 1's data, and builds the PatientContext — read once, not refreshed during the Session (Concept 2).`  
12. `GenPRES Server asks GenPRES Database for Patient 1's GenPRES PatientRecord and takes the TreatmentPlan the Session is to start from: the most recent one that is either Signed, by whoever, or Unsigned and User A's own (Rule 19). Patient 1 has no GenPRES PatientRecord, so the Session starts from nothing: a WorkPlan with no OrderContexts — only the Patient Data the launch fetched (Concept 16).`  
13. `GenPRES Server opens a GenPRES Session, writes its SessionRecord to GenPRES Database, and closes any other open Session of User A — one conditional act at the Database (Rules 7, 40).`  
14. `GenPRES Server returns to GenPRES Client the SessionId, the UserContext, the PatientContext, the OrderContexts of the TreatmentPlan the Session starts from — none, here — and the OpenedToken that will accompany every create (Concept 17; Rule 33). From here on the Server keeps nothing of the Session but its SessionRecord (Rule 31).`

**`Extensions`**

*`1a No Patient is active in the MainEHR Session.`* `GenPRES opens and User A can enter Patient Data by hand and prescribe, but a TreatmentPlan cannot be opened or created (Rule 12).`

* `1a.1 MainEHR LaunchScript asks the Broker to prepare a launch for User A's login, without a Patient.`  
* `1a.2 Steps 4–10 as the main path, the LaunchAssertion carrying a login but no Patient.`  
* `1a.3 Steps 11 and 12 are skipped: no Patient, no data to fetch, no record to read.`  
* `1a.4 GenPRES Server opens a GenPRES Session without a Patient, writes its SessionRecord, and returns the SessionId, the UserContext and an empty PatientContext.`

*`2a The button is not User A's to press.`* `MainEHR LaunchScript decides which MainEHR User may run it and refuses (Rule 1); nothing leaves the workstation. What that decision looks like is MainEHR's affair — only the refusal is ours to state.`

*`3a The Broker is unreachable.`* `The call from MainEHR LaunchScript fails, and MainEHR LaunchScript reports it to the User before exiting (User Interaction 2) — the one launch failure the EHR side can report: its Broker edge is request–response, and it has not yet exited. Its reporting ends with its own acts — the Broker exchange and the launching of the browser; what happens in the browser it never learns (Consequence 1). No credential exists, nothing was opened; the User tries the button again.`

*`5a GenPRES Server is unreachable.`* `The browser cannot load GenPRES at all — GenPRES Client is served by GenPRES Server, so there is nothing of ours to show a message. The User sees the browser's own error page. MainEHR LaunchScript has exited and never learns of it (Consequence 1). The URL, with the LaunchCredential, stays in the address bar; a refresh retries while the credential is within its lifetime (Rule 3).`

*`7a GenPRES Server becomes unreachable after the page was served.`* `GenPRES Client shows that GenPRES is unavailable and retries while the credential lives (Rule 3) — from its own memory: the URL no longer carries it (Rule 39).`

*`7b The LaunchCredential is stolen before the Client presents it.`* `The credential is in a URL (Consequence 4): whoever presents it first wins (Rule 2), so within its lifetime a thief gains User A's Session. The window is the first page load and no longer: at first presentation the Client erases the credential from the URL and the history (Rule 39), so afterwards there is nothing left to steal but what transit logging already captured — and that is dead within the lifetime (Rules 3, 28). The damage inside the window is bounded: signing needs User A's PIN (Concept 14), the PIN reset stalls without A's mail (Rule 37), so the thief saves at most Unsigned work in A's name — and User A's own next launch closes the thief's Session (Rule 7), with the notice telling A that something else held it (Rule 10). Single use, the short lifetime and the erasing limit the damage; they do not prevent it (Rules 2, 3, 28, 39).`

*`8a The LaunchCredential is expired or already redeemed.`* `No launched Session is opened (Rules 2, 3, 6). GenPRES Client shows that the launch failed and asks the User to relaunch from MainEHR.`

*`8b GenPRES Server cannot reach the Broker.`* `Redemption fails; GenPRES Client shows that the launch failed and retries from memory while the credential lives (Rules 3, 39); after that, relaunch as ext 8a.`

*`9a The UserRegistry cannot say what the login may do.`* `No launched Session is opened (Rule 6). GenPRES Client shows that authorisation could not be checked and offers to continue anonymously — a fresh open as in UC-8, carrying nothing from the launch: no User, no Patient, details entered by hand (Rules 6, 13). Relaunching would not help here, so this is the only offer worth making; contrast ext 8a, where relaunching cures it.`

*`9b The launching User is a Reader.`* `User C launches for Patient 1 instead of User A. The UserRegistry returns the Reader Role; a Session is opened and User C can view and prescribe within it (Concept 15), but saving and signing are not offered: a Reader never creates a TreatmentPlan (Roles), so User C has no Unsigned TreatmentPlan of their own and always starts from the most recent Signed TreatmentPlan (Rules 17, 19). User C is never asked for a PIN (Rule 25).`

*`10a User A has no PIN yet.`* `First launch as a Prescriber. UC-2 follows: a PIN must be set before the launch continues (Rule 24).`

*`11a The PatientDataPlatform is unreachable, or holds nothing for Patient 1.`* `The launch continues: the PatientContext carries the PatientId and no data, and User A fills the Patient Data in by hand (Concepts 2, 15). TreatmentPlans can be opened and created as normal — the PatientId is there (Rule 12).`

*`13a User A already has an open GenPRES Session.`* `The earlier Session is closed and User A is told that work in it may have been lost (Rules 7, 9, 10).`

*`13b Two launches of User A race.`* `Both reach step 13 believing they may open. The Database decides in one act (Rule 40): one Session opens, and one open Session per User (Rule 7) closes the rest — the loser is told as ext 13a. Never are two open.`

*`14a The Patient shown is not the one User A meant.`* `The launch faithfully carried whatever Patient was active in MainEHR (Invariant 1); MainEHR never learns of the mismatch (Consequence 1). The remedy is always another launch: User A activates the right Patient in MainEHR and relaunches, which closes the wrong Session (Rules 7, 9) — told at that launch, as ext 13a.`

### **`UC-2 First launch as a Prescriber: no PIN yet`**

**`Goal`** `User A, launching GenPRES as a Prescriber for the first time, gets a PIN so that later saves can be signed. Precondition UC-1 has reached step 10: GenPRES Server has the UserContext, it carries the Prescriber Role, and no PIN is set for that login. Main path Before anything else, GenPRES asks User A to choose a PIN; nothing else is offered until it is set. Once set, GenPRES mails User A, the launch continues, and User A works as in UC-1. This happens once per Prescriber: a forgotten PIN is replaced in-Session (UC-7), never through this flow again.`

**`Trace`**

1. `GenPRES Server finds no PIN set for the login, and the UserContext carries the Prescriber Role (Rules 23, 24).`  
2. `GenPRES Client asks User A to choose a PIN, and offers nothing else.`  
3. `User A chooses a PIN and confirms it.`  
4. `GenPRES Server sets the PIN on User A's GenPRES UserCredential — creating the UserCredential if GenPRES holds none for that login yet — records the change in the audit, and mails User A (Rules 26, 46).`  
5. `The launch continues from UC-1 step 11.`

`The order matters: the PIN is offered only after the UserRegistry has said who the login belongs to and what they may do (Rule 24). A login the registry does not recognise never gets to enrol (UC-1 ext 9a), and a Reader is never asked at all (UC-1 ext 9b).`

**`Extensions`**

*`3a User A does not set a PIN.`* `A required PIN is not set, so the launch cannot be honoured and no launched Session is opened (Rule 6). The next launch asks again.`

### **`UC-3 Prescribe, save and sign`**

**`Goal`** `User A records a set of orders for Patient 2 and takes responsibility for them. Precondition UC-1 completed: User A has an open GenPRES Session for Patient 2, started from its Signed head, and holds the Prescriber Role. Main path User A builds up the orders, saves, and signs. The signed plan is attested in User A's name and can be acted on.`

**`Trace`**

1. `User A adds and adjusts orders in GenPRES Client. Each change goes to GenPRES Server, which answers — and the request refreshes the Session's idle clock (Rule 8), nothing more: the Server keeps none of the work (Rule 31).`  
2. `User A saves. GenPRES Client sends the whole WorkPlan with the OpenedToken; GenPRES Server takes the User and Patient from the SessionRecord, not from what the request carries (Rules 32, 33). No TreatmentPlan newer than the one the Session opened with exists, so nothing blocks or warns (Rules 20, 21). GenPRES Server computes the stamps against the base (Rule 35) and appends an Unsigned TreatmentPlan carrying User A's UserContext and its base (Rule 14, Concept 13).`  
3. `User A signs: Signing is creating a TreatmentPlan while supplying the Session's User's PIN (Concept 14). GenPRES Server issues the SigningChallenge — the exact plan, its base, its Patient — and GenPRES Client shows it and asks for User A's PIN (Rule 43). PIN and challenge go back together; GenPRES Server verifies both — the PIN against the UserCredential as it stands at that moment, so one replaced or suspended since the launch fails here (Rules 22, 27), and the PIN never leaves GenPRES (Rule 22) — re-reads the Patient Data (Rule 44) and the Role (Rule 38), and appends a Signed TreatmentPlan in User A's name, all in one transaction (Rules 14, 15, 42). It is now the most recent Signed TreatmentPlan and counts clinically (Rule 16).`

**`Extensions`**

*`2a The record has moved on since User A opened.`* `If what appeared is Unsigned, User A is notified — and may create anyway, or hold off (Rule 21). If a Signed TreatmentPlan appeared, creating is blocked (Rule 20): User A opens that Signed TreatmentPlan (Rule 17), reapplies their work, and continues. UC-6 is this case in full.`

*`3a User A does not sign.`* `The Unsigned TreatmentPlan stays at the head, inert (Rule 16). Only User A can open it (Rule 18), and only User A can later create a Signed TreatmentPlan from it — unless a newer Signed TreatmentPlan has appeared by then, which blocks it (Rules 19, 20). UC-11 is the continuation.`

*`3b User A gives the wrong PIN, or cancels.`* `Verification fails; no TreatmentPlan is created. User A can try again — each wrong entry counts against the UserCredential's limit, a correct one resets it, and at the limit the Session ends and the credential stops signing until the PIN is replaced (Rules 9, 27, 37). The count survives the Session: it is not a fresh start.`

*`3c User A signs without saving first.`* `Signing is itself the creation of a TreatmentPlan (Concept 14): steps 2 and 3 become one act, and the block and notification checks run before the PIN is asked for (Rules 20, 21).`

*`3d The plan on screen shows a dose to fix.`* `User A cancels, corrects, and signs against a fresh challenge. A WorkPlan that no longer matches its challenge is refused (Rule 43), but on this path nobody ever sees that: cancelling first is the only way back to editing.`

*`3e Someone else takes the keyboard between the challenge and the PIN.`* `To edit, they must cancel the modal (Possibility 1; Rule 43) — and User A, coming back to no modal, knows to review afresh. A WorkPlan changed any other way no longer matches the challenge and is refused. Either way, the signature covers only what User A saw.`

*`3f A create arrives late, repeated, or out of order.`* `It carries a WorkPlan from another moment: its challenge no longer matches, or is already spent, and the commit refuses (Rules 33, 43). The Server needs no memory of the conversation for this (Rule 31) — the challenge is the memory. A retry of a create that already landed is the one exception: its key returns the first result, and nothing lands twice (Rule 45).`

### **`UC-4 Work left unsigned by someone else`**

**`Goal`** `Establish what User B can do when the head of the record is a TreatmentPlan User A saved but never signed: know it exists, work past it — but never open it or sign it. Precondition UC-1 completed: User B has an open GenPRES Session for Patient 3, whose head is an Unsigned TreatmentPlan of User A's over an older Signed one, and holds the Prescriber Role. Main path GenPRES starts User B from the last Signed TreatmentPlan — the last state anyone stood behind. User A's Unsigned work above it is closed to User B. User B prescribes their own orders; at the save User B is told Unsigned work of User A's exists and chooses to proceed. User B signs. From then on User A's old work is superseded and can no longer be signed by anyone.`

**`Trace`**

1. `User B's Session starts from the older Signed TreatmentPlan: the head is Unsigned and User A's, and only User A can open that (Rules 18, 19).`  
2. `User B enters orders and saves. An Unsigned TreatmentPlan of another User, newer than the one User B opened with, exists, so User B is notified — the work and whose it is — and chooses to create anyway, returning the NoticeToken (Rules 21, 34). GenPRES Server appends an Unsigned TreatmentPlan of User B's own (Rule 14).`  
3. `User B signs (Concept 14; Rules 20–22, 42, 43). GenPRES Server appends a Signed TreatmentPlan in User B's name; it now counts clinically (Rule 16).`  
4. `User A's Unsigned work is superseded: User A's next Session starts from User B's Signed TreatmentPlan (Rule 19), and creating anything from the old work is blocked (Rule 20). Nobody but User A could ever open it; User A may still look into it (Rule 18) — its contents are not lost to them — but nothing can be built from it any more (Rule 20).`

**`Extensions`**

*`2a User B holds off at the notification.`* `User B chooses not to create (Rule 21), leaves the record as it is, and telephones User A to come and sign — both TreatmentPlans stand, each usable only by its own User (Rules 18, 19).`

*`4a User A launches for Patient 3 before User B signs.`* `User A's Session starts from User A's own Unsigned head — User B's TreatmentPlan is Unsigned too, so it does not supersede it (Rule 19). User A may sign: no newer Signed TreatmentPlan exists (Rule 20), though User A is notified of User B's newer Unsigned work (Rule 21). Whichever of the two signs first blocks the other (Rule 20).`

### **`UC-5 Someone else takes over the workstation`**

**`Goal`** `User B, sitting down at a workstation User A left open, works on Patient 1 without anything being attested in User A's name. Precondition UC-1 completed: User A has an open GenPRES Session for Patient 1, and walks away. Main path User B sits down; the browser still shows Patient 1 in User A's Session. User B works and saves; the save is Unsigned and carries User A's identity. Signing asks for User A's PIN — signing always names the Session's User — which User B does not have, so the work stays Unsigned and inert.`

**`Trace`**

1. `User B acts in GenPRES Client, which still holds User A's SessionId.`  
2. `GenPRES Client sends the request with that SessionId — riding in its cookie (Rule 11).`  
3. `GenPRES Server sees an open Session belonging to User A and serves the request.`  
4. `User B saves. The TreatmentPlan is created under the Session's credentials: it carries User A's UserContext, and so do the stamps on every OrderContext User B changed (Rule 14) — attribution is per credential, not per person.`  
5. `User B signs. GenPRES Client asks for the PIN of User A, the Session's User (Concept 14).`  
6. `User B does not have it and cancels. The TreatmentPlan stays Unsigned and does not count clinically (Rules 15, 16).`

**`Extensions`**

*`5a User B relaunches GenPRES from MainEHR as themselves, with Patient 1 active.`* `A Session of User B's own opens (Rule 7 is per User; User A's Session is untouched). It starts from nothing: Patient 1 has no Signed TreatmentPlan, and the Unsigned one carries User A's UserContext, so only User A can open it (Rules 18, 19). User B re-enters the work; at the save User B is notified of the newer Unsigned TreatmentPlan and proceeds (Rule 21), then signs as themselves.`

*`5b User B cannot log in to MainEHR at that workstation.`* `No path to a Session of User B's own. The work stays Unsigned until User A opens it in a Session of their own and signs (Rules 18, 19); nobody else can.`

*`6a User B guesses instead.`* `At the configured number of consecutive wrong entries (Rule 30) the Session ends (Rules 9, 27); the Unsigned TreatmentPlan stays, and User A is told of the ending at their next opportunity (Rule 10). Relaunching as User A does not help: the count belongs to the UserCredential, and at the limit the credential stops signing until the PIN is replaced (Rules 27, 37) — guessing is capped outright. The PIN reset is no way out either: it stalls without User A's mail (Rule 37; UC-7 ext 1a). Nothing remains.`

### **`UC-6 Two Users, one Patient`**

**`Goal`** `Establish what happens when two Prescribers work on the same Patient at the same time. Precondition UC-1 completed twice: User A and User B each hold an open GenPRES Session for Patient 2 at their own workstation. Rule 7 permits this: it limits Sessions per User, not per Patient. Main path Both work on Patient 2's orders; neither sees the other's work — a Client only learns anything at its own next request (Consequence 6). The first to sign wins: the other is blocked from saving over it and must take up the Signed TreatmentPlan.`

**`Trace`**

1. `Both Users edit; each request refreshes only its own Session (Rule 8).`  
2. `User A saves and signs: an Unsigned then a Signed TreatmentPlan in User A's name (Rules 14, 15; Concept 14).`  
3. `User B saves. A Signed TreatmentPlan newer than the one User B opened with now exists, so creating is blocked (Rule 20).`  
4. `User B opens User A's Signed TreatmentPlan (Rule 17), reapplies their own work, saves and signs. The OrderContexts User B changed are stamped with User B's UserContext; those left untouched keep User A's stamp (Rule 14) — the signature still attests the whole set in User B's name.`

**`Extensions`**

*`2a User B saves, Unsigned, before User A signs.`* `User B is not blocked — nothing Signed is newer (Rule 20) — but User A is notified of User B's Unsigned work when creating, and proceeds or holds off (Rule 21). Once User A signs, User B is blocked as in step 3 and continues as in step 4.`

*`2b Both sign at the same moment.`* `Each passed its check against the record it read; the append is one act at the Database, which lands exactly one (Rule 36). The other is refused and continues as step 3 — blocked, and taking up the winner's TreatmentPlan.`

`Nothing attested is ever lost: the GenPRES PatientRecord is append-only (Concept 12), so a Signed TreatmentPlan survives whatever follows, and each TreatmentPlan's base keeps any divergence on record, and the Signed history stays readable (Concept 13; Rule 17). What is not protected is Unsigned work: superseded, it can never be signed (Rules 19, 20).`

### **`UC-7 A User forgets their PIN`**

**`Goal`** `User A, who cannot remember their PIN, gets a new one — and finds out if someone else did it for them. Precondition UC-1 completed: User A has an open GenPRES Session, and a GenPRES UserCredential with a PIN set but forgotten. Main path User A asks GenPRES to reset the PIN. GenPRES mails a one-time code; User A returns the code together with a chosen new PIN, and the old is replaced in that one act — no relaunch, and never a moment without a PIN. Two mails go to User A: the code, and the notice that the PIN changed.`

**`Trace`**

1. `User A asks GenPRES Server to reset the PIN.`  
2. `GenPRES Server mails User A a one-time code (Rules 26, 37). Nothing has changed yet: the old PIN stands until the code comes back.`  
3. `User A enters the code and a new PIN in GenPRES Client. GenPRES Server verifies the code, replaces the PIN in the same act — the count starts at zero (Rule 27) — records the change in the audit, and mails User A (Rules 26, 46).`  
4. `User A signs as before, with the new PIN (Concept 14). Nothing else changed: same Session, same work.`

**`Extensions`**

*`1a User B, at User A's open workstation, triggers the reset.`* `The request runs as the main path — but the code goes to User A's mail, which User B does not control (Rule 37). The reset stalls at step 3, the PIN stands, and the code mail tells User A someone asked. What used to be detection after the fact is now the gate: launching proves control of a MainEHR Session, not a person (Possibility 1), and Rule 37 is what keeps that from becoming a signature.`

*`2a User A never returns the code.`* `Nothing changes: the PIN stands, the code expires with its short lifetime, and the request leaves only the mail and the record of the attempt.`

*`3a The code comes back wrong.`* `Nothing is replaced: the old PIN stands and User A may try again — a few times. At the limit, or at the lifetime, the code is void (Rule 37): a fresh reset mails a fresh code, so guessing at the gate cannot outlast it, and every fresh attempt lands another mail in User A's inbox.`

### **`UC-8 User opens GenPRES directly`**

**`Goal`** `User A gets GenPRES open without MainEHR, can enter Patient details and prescribe, but can never save or sign: GenPRES as Clinical Decision Support (CDS), not as order management. Precondition User A has a Browser that can reach GenPRES Server. No launch, so no LaunchCredential — and GenPRES cannot know who is at the keyboard. (Also reached by choice from a launch that failed authorisation: UC-1 ext 9a.) Main path User A opens GenPRES in a browser. GenPRES opens with no Patient and no User. User A enters Patient details by hand and prescribes. Nothing can be saved.`

**`Trace`**

1. `User A opens the GenPRES address in a browser; GenPRES Server serves GenPRES Client.`  
2. `GenPRES Client has no LaunchCredential to present, and asks GenPRES Server to open a Session without one.`  
3. `GenPRES Server opens an anonymous GenPRES Session — no User, no UserContext, no Role, no PatientId (Rule 13) — and writes its SessionRecord (Concept 9).`  
4. `User A prescribes — Patient Data and OrderContexts by hand (Concept 15); GenPRES Server answers each request and keeps nothing (Rules 12, 31).`  
5. `No TreatmentPlan can be opened or created (Rule 12), and with no UserContext and no GenPRES UserCredential there is nobody to sign as (Concepts 7, 14). The WorkPlan exists only in GenPRES Client (Rule 31; Concept 16) and is gone when the browser goes.`

`Neither the GenPRES PatientRecord nor the PatientDataPlatform is ever touched: with no PatientId there is nothing to read (Rule 12). And anyone who can reach GenPRES Server gets exactly this much — anonymous use needs no Role and no UserRegistry check, is rate-limited, and runs out at its absolute limit (Rule 13). That is the point: CDS for anyone, order management only through a launch.`

**`Extensions`**

*`2a The browser does present a LaunchCredential.`* `This is a launch: UC-1 from step 7.`

*`2b The same Browser later launches properly (UC-1).`* `The launched Session is another Session; the anonymous one is untouched — Rule 7 counts only a User's Sessions, and an anonymous Session binds to none — and runs out at its absolute limit (Rule 13).`

### **`UC-9 A Session ends out from under the User`**

**`Goal`** `User A learns — once, and only once — that their Session ended without them closing it, and keeps everything that was saved — and, as long as the browser stands, everything it still shows. Precondition UC-3 ran through step 2 and stopped (ext 3a): User A's Session for Patient 2 is open, an Unsigned TreatmentPlan of User A's stands at the head, and further unsaved changes sit on the screen. Main path User A is called away. The Session idles too long and ends; the screen still looks alive. At User A's next action the request fails: User A is told the Session ended. The saved Unsigned TreatmentPlan stands in the record, User A's own to resume — and the unsaved WorkPlan is still on the screen, and can be taken along into the next Session.`

**`Trace`**

1. `User A stops acting; the idle clock runs out (Rule 29) and GenPRES Server ends the Session, marking its SessionRecord ended (Rules 8, 9).`  
2. `GenPRES Server cannot reach GenPRES Client: the Client keeps showing a live-looking screen (Consequence 6).`  
3. `User A returns and acts. GenPRES Client sends the request with the ended Session's SessionId.`  
4. `GenPRES Server refuses the request and tells User A the Session has ended; User A acknowledges, and the notice never returns (Rule 10).`  
5. `The unsaved WorkPlan was never anywhere but GenPRES Client (Rule 31; Concept 16): the ended Session accepts nothing, but the Client still holds it and may offer to carry it into the next Session as fresh prescribing (Concept 15) — the same User's Session, for the same Patient, and no other: anything else the Client discards, and GenPRES Server refuses regardless (Rule 32). It survives exactly as far as the browser does — closed, it is gone. The Unsigned TreatmentPlan stands in the GenPRES PatientRecord, User A's own to resume (Rules 18, 19; UC-11).`  
6. `User A relaunches from MainEHR (UC-1). Acknowledged once already, the notice does not return (Rule 10).`

**`Extensions`**

*`1a GenPRES Server restarts instead.`* `Nothing ends: the Session's identity and standing are in its SessionRecord, its work in GenPRES Client, and the Server itself held nothing (Rules 9, 31). While it is down, requests fail as in UC-1 ext 7a; when it is back, the next request continues the Session — the idle clock permitting (Rules 8, 9).`

*`1b User A opens another Session instead, at another workstation.`* `The launch itself ends the old Session (Rules 7, 9) and the notice comes with the new launch (Rule 10; UC-1 ext 13a). The old Client's next request is refused, but the acknowledged notice does not return (Rule 10).`

*`3a No sweep has run when User A returns.`* `The request itself finds the Session past its idle limit and ends it then and there (Rule 41); the trace continues at step 4. Expiry does not wait for housekeeping.`

### **`UC-10 A Reader consults a Patient`**

**`Goal`** `User C sees the current clinical plan for a Patient — and it is established what a Reader is never told. Precondition As UC-9: Patient 2's head is an Unsigned TreatmentPlan of User A's over its Signed one. User C, a Reader, launches for Patient 2 (UC-1 ext 9b). Main path User C sees the most recent Signed TreatmentPlan — the plan that counts clinically. User A's newer Unsigned work is invisible, and even its existence goes unannounced. User C can prescribe within the Session to explore alternatives, but nothing can be saved.`

**`Trace`**

1. `The Session starts from the most recent Signed TreatmentPlan: User C never creates a TreatmentPlan (Roles), so no Unsigned TreatmentPlan of their own can exist (Rules 17, 19).`  
2. `GenPRES Server returns that TreatmentPlan's OrderContexts; User C reads the plan that counts clinically (Rule 16).`  
3. `User A's newer Unsigned TreatmentPlan is not shown — only its creator can open it (Rule 18) — and its existence is not announced either: the only notification of another's Unsigned work fires at TreatmentPlan creation (Rule 21), and a Reader never creates one.`  
4. `User C prescribes within the Session to explore (Concept 15); saving and signing are not offered (Roles), and no PIN is ever asked (Rule 25).`

`A Reader can thus be reading a plan that a Prescriber already knows is being superseded. The model accepts this deliberately: Unsigned work counts for nothing until it is signed (Rule 16), so there is nothing yet to tell a Reader about.`

### **`UC-11 A User resumes their own Unsigned work`**

**`Goal`** `User A continues the saved-but-unsigned orders for Patient 2 and signs them. Precondition UC-9 completed: Patient 2's head is User A's own Unsigned TreatmentPlan over the older Signed one, and User A launches again for Patient 2 (UC-1). Main path The Session starts from User A's own Unsigned head — not from the older Signed TreatmentPlan. User A reviews, adjusts, and signs; the plan now counts clinically.`

**`Trace`**

1. `At the launch, the most recent TreatmentPlan that is Signed or Unsigned-and-own is User A's own Unsigned head: the Session starts from it (Rule 19).`  
2. `User A reviews and adjusts (Concept 15); each request refreshes the Session's idle clock (Rule 8).`  
3. `User A signs. No Signed TreatmentPlan newer than the one opened exists, so nothing blocks (Rule 20); no other User's Unsigned TreatmentPlan is newer, so nothing warns (Rule 21). GenPRES Server verifies PIN and challenge (Rules 22, 43) and appends a Signed TreatmentPlan in User A's name in one transaction (Concept 14; Rules 14, 15, 42). It now counts clinically (Rule 16).`

**`Extensions`**

*`3a A Signed TreatmentPlan appeared since the launch.`* `Creating is blocked (Rule 20): User A opens it (Rule 17), reapplies, and continues — UC-6 step 4.`

*`3b Another User's Unsigned TreatmentPlan appeared since the launch.`* `User A is notified and decides (Rule 21) — UC-4 ext 4a is the full race.`

### **`UC-12 User closes GenPRES`**

**`Goal`** `User A finishes with GenPRES and leaves nothing dangling: no stray Session, no notice at the next launch. Precondition UC-11 completed: User A has an open GenPRES Session for Patient 2, its work signed. Main path User A closes the Session in GenPRES Client. The Session ends as closed by the User, and the next launch starts clean — no notice, because there is nothing to warn about.`

**`Trace`**

1. `User A has saved or signed what was worth keeping; nothing unsaved remains.`  
2. `User A closes the Session in GenPRES Client, which tells GenPRES Server.`  
3. `GenPRES Server ends the Session and marks its SessionRecord ended by the User (Rule 9; Concept 9).`  
4. `No notice follows, now or at the next launch: Rule 10 speaks only of endings other than by the User.`

**`Extensions`**

*`1a Unsaved changes remain at the close.`* `GenPRES Client can warn that they are about to be discarded, but closed is closed: the WorkPlan existed only in GenPRES Client (Rule 31; Concept 16), and closing discards it. Anything saved stands (Concept 12).`

*`2a User A closes the browser instead.`* `Nothing reaches GenPRES Server: a vanished browser is indistinguishable from a silent one, so no close can be inferred (Rule 9). The Session idles out and User A is told at the next opportunity (Rule 10; UC-9) — a harmless notice, the price of the indistinguishability.`

### **`UC-13 A User's authority is withdrawn`**

**`Goal`** `User A, whose authority is withdrawn, keeps anonymous CDS use of GenPRES and nothing more — and User A's unfinished work is shown to be a dead end, not a loss. Precondition UC-3 ran once more through step 2 and stopped: Patient 2's head is an Unsigned TreatmentPlan of User A's over the Signed one from UC-11. Then the UserRegistry stops returning a Role for User A's login. Main path At User A's next launch the UserRegistry returns no Role, so no launched Session opens — then or ever, while the withdrawal stands. GenPRES Client offers the anonymous open; CDS is all that remains. User A's Unsigned head can never be signed again; the next Prescriber works past it.`

**`Trace`**

1. `User A launches (UC-1). At step 9 the UserRegistry returns no Role for the login.`  
2. `The launch cannot be honoured: no Role, no launched Session (Rules 5, 6). GenPRES Client offers the fresh anonymous open (UC-1 ext 9a).`  
3. `User A accepts and has GenPRES as CDS (UC-8; Rule 13): hand-entered patients, no records, nothing saved. Every later launch ends the same way (Rule 5).`  
4. `User A's GenPRES UserCredential remains — login, PIN, count (Concept 7) — but is inert: the Role never lived on it, and with no launched Session there is nothing to sign (Concepts 7, 14).`  
5. `The Unsigned TreatmentPlan at Patient 2's head is stranded: only User A could open it (Rule 18), and User A can no longer reach it. User B's next Session starts from the Signed TreatmentPlan below (Rule 19), User B is notified of the stranded work at the save (Rule 21), and User B's signature supersedes it for good (Rules 16, 20; UC-4).`

**`Extensions`**

*`1a The withdrawal happens while User A's Session is open.`* `The open Session keeps the Role it was launched with (Rule 5) — but only as far as the next signature: every signature re-takes the Role from the UserRegistry (Rule 38), so the withdrawal blocks signing at once. What rides out the Session is saving Unsigned work, which counts for nothing (Rule 16) and ends as the stranded head of the main path.`

## **`System Model`**

### **`Actors`**

`The kinds of participants that appear in the use cases. [ours] = under construction. [given] = existing infrastructure, not ours to change. The User is neither — they are who the system is for.`

1. `MainEHR Workstation [given]: the running EHR Client.`  
2. `MainEHR LaunchScript [ours]: a VB.NET script behind a button in MainEHR Workstation. Runs on trigger, then exits. The only part of MainEHR we control.`  
3. `GenPRES Client [ours]: GenPRES UI running in a Browser.`  
4. `GenPRES Server [ours]: GenPRES backend.`  
5. `GenPRES Database [ours]: two stores, one writer — GenPRES Server. The clinical store holds the Signed TreatmentPlans of the GenPRES PatientRecords, with their base references, and is what the PatientDataPlatform copies. The private store holds everything else — Unsigned TreatmentPlans, SessionRecords, UserCredentials, the spent-state of Tokens (Concept 17), the audit — and is never copied anywhere.`  
6. `PatientDataPlatform [given]: a shared, read-only copy of the databases of MainEHR, GenPRES and other applications. How data gets there is out of scope.`  
7. `User: person who uses MainEHR and GenPRES.`  
8. `Broker [ours]: hands a launch from MainEHR LaunchScript to GenPRES Server.`  
9. `UserRegistry [ours]: says who a login belongs to, what that person may do, and how to reach them by mail.`  
10. `MailService [given]: sends mail to a person, outside GenPRES and outside MainEHR.`

### **`Roles`**

`The kinds of authority a User can hold; what each may do. The UserRegistry decides the Role. MainEHR and GenPRES enforce it independently, each within its own application.`

1. `Prescriber: may read and write — writing meaning creating TreatmentPlans.`  
2. `Reader: may never create a TreatmentPlan. Like any User they may prescribe within their Session (Concept 15), but nothing of it can be saved.`

### **`Concepts`**

`The things passed between actors, or held by them, and what each one means.`

1. **`UserContext`**`: User identification and User Role.`  
2. **`PatientContext`**`: PatientId and Patient Data relevant for GenPRES. The User can supply the data by hand; only a launch can supply the identification. Launched, the data is read from the PatientDataPlatform once, at the launch, and not refreshed while the Session lives: that it may go out of date during the Session is accepted.`  
3. **`LaunchAssertion`**`: asserts a MainEHR login, and the Patient if one is active — no verified identity, no Role. The active Patient is also MainEHR's word that this User may work on this Patient now: patient-level authorisation is MainEHR's, and GenPRES enforces nothing finer.`  
4. **`LaunchCredential`**`: a single-use reference to a LaunchAssertion, short-lived and opaque — it reveals nothing about what it refers to.`  
5. **`MainEHR Session`**`: the period a User is logged in at a MainEHR Workstation. Many Patients can be handled in it, one active at a time.`  
6. **`MainEHR PatientRecord`**`: all patient data maintained by MainEHR.`  
7. **`GenPRES UserCredential`**`: held by GenPRES for one User and keyed by who they are, not by their renameable login — holding the login by which the UserRegistry currently knows that person, a PIN if one is set, and the count of consecutive wrong PIN entries (Rule 27). The PIN is optional: a UserCredential may hold none — the User has never set one — and one without a PIN cannot sign; once set, a PIN is only ever replaced, never merely removed (Rule 37). The UserCredential carries no Role and no identity of its own: it only lets a User prove, during a GenPRES Session, that they are the person named in the UserContext.`  
8. **`GenPRES Session`**`: the interaction of a User with GenPRES — for a Patient if the launch supplied one, otherwise for no Patient; opened without a launch, it is anonymous (Rule 13). Only a Session with a Patient allows opening or creating TreatmentPlans (Rule 12). A Session has no state in GenPRES Server between requests: its identity and standing live in its SessionRecord, its work in GenPRES Client (Rule 31).`  
9. **`GenPRES SessionRecord`**`: binds a SessionId to exactly one User — or to no User, when the Session is anonymous — and to a Patient if the Session has one. Records whether that Session is open or ended, when it last heard from the Client (Rule 8), and whether the User has acknowledged its ending (Rule 10). Kept after the Session ends.`  
10. **`OrderContext`**`: a PatientContext together with the OrderScenarios currently under consideration for that Patient. An OrderContext has an identity that persists across TreatmentPlans, and carries the UserContext of the User whose Session last changed it — stamped at each save (Rule 14), so an OrderContext that is never saved carries none.`  
11. **`OrderScenario`**`: one proposed Order together with the prescribing information that gives it meaning but is not part of the Order itself.`  
12. **`GenPRES PatientRecord`**`: the append-only history of a Patient in GenPRES — a sequence of TreatmentPlans, every one carrying that Patient's PatientId: the one thing no TreatmentPlan may change.`  
13. **`TreatmentPlan`**`: the Patient's treatment plan as it stood when saved — a set of the Patient's OrderContexts, carrying the UserContext of the User who created it and a reference to the TreatmentPlan it was created from — its base — if any. It also records the Patient Data it was built on: the values, where each came from (the PatientDataPlatform, or entered by hand) and when they were read (Concept 2) — so a signed plan can be explained from its own record. A TreatmentPlan is either Signed or Unsigned.`  
14. **`Saving and Signing`**`: one act — creating a TreatmentPlan. Signing is saving while supplying the PIN of the Session's User: the TreatmentPlan is then Signed, otherwise Unsigned. There is no other way a TreatmentPlan comes into being — and none is ever changed or saved again: changing means creating a new one whose base is the old.`  
15. **`Prescribing`**`: changing, within a GenPRES Session, the Patient Data of the PatientContext and adding, removing or changing OrderContexts. Prescribing touches only the WorkPlan (Concept 16): nothing reaches the GenPRES PatientRecord until a TreatmentPlan is created, and GenPRES Server computes on what the Client sends — Patient Data included — without keeping any of it.`  
16. **`WorkPlan`**`: the plan being composed in GenPRES Client — the Patient Data and the OrderContexts under the User's hands (Concept 15). It is mutable, carries no attribution and sits in no record: it becomes a TreatmentPlan only by being created (Concept 14), and otherwise dies with the browser. Held only by its own Client (Rule 31), it is what the shopping-cart metaphor names (Guarantee 3).`  
17. **`Token`**`: a short-lived note GenPRES Server writes to itself and hands to GenPRES Client, which returns it unaltered — the Server's memory across requests, where it keeps none of its own (Rule 31). Bound to what it names, impossible for a Client to make, and spent by the create it accompanies. Three exist: the OpenedToken — which TreatmentPlan the Session opened (Rule 33); the NoticeToken — whose Unsigned work a notice disclosed (Rule 34); the SigningChallenge — the exact plan a signature would approve (Rule 43). In use: every create carries and proves the OpenedToken — has anything Signed appeared since the User started (Rule 20)? A signing create carries the SigningChallenge besides — is the plan committed the plan the User last saw (Rule 43)? One guards where the User began, the other what the User reviewed; between them the Server needs no memory of the Session at all. The Client holds a token in memory just long enough to return it; the Server holds only the key that verifies any of them, and the spent-marks of those already used (Actor 5).`

### **`Constraints`**

`Notation — how to read the edges below. Not itself a constraint.`

* `X -> Y — X initiates a connection to Y and receives Y's response on it. Grants initiation in that direction only; the reverse is never implied.`  
* `X => Y — X launches Y with initial parameters. One-way: no response, no error path back.`  
* `X <-> Y — interaction, not request–response: a User can read what Y shows and act on it.`

`Any pair without an edge cannot exchange data at all. Edges do not compose — no component relays on another's behalf unless stated.`

**`User Interaction`** `— which components a User can read and act on, or start.`

1. `Any User <-> MainEHR Workstation`  
2. `Any User <-> MainEHR LaunchScript — the User starts it; while it runs it can report its own acts back (the Broker exchange, the launching of the browser), and it exits at once, so nothing later ever comes from it.`  
3. `Any User <-> GenPRES Client`

**`Communication`** `— which components may reach which, and nothing else is possible. Edges touching a [given] component are what the deployment allows; edges between [ours] components are what we choose to build.`

1. `MainEHR Workstation -> UserRegistry`  
2. `MainEHR Workstation -> PatientDataPlatform`  
3. `MainEHR LaunchScript -> Broker`  
4. `MainEHR LaunchScript => GenPRES Client`  
5. `GenPRES Client -> GenPRES Server`  
6. `GenPRES Server -> Broker`  
7. `GenPRES Server -> UserRegistry`  
8. `GenPRES Server -> PatientDataPlatform`  
9. `GenPRES Server -> GenPRES Database`  
10. `GenPRES Server -> MailService`

### **`Consequences`**

`Derived from the edges above — not new assertions, and not negotiable without changing an edge.`

1. `MainEHR LaunchScript learns nothing after the launch. What it can report to the User (User Interaction 2) ends with its own acts: the Broker exchange (UC-1 ext 3a) and the launching of the browser. Expired credential, GenPRES Server down, wrong patient — none of it reaches MainEHR LaunchScript. Error handling falls to GenPRES Client, except when GenPRES Server is unreachable: GenPRES Client is served by GenPRES Server, so then no Client is served either and the User is left with the browser's error page.`  
2. `The Broker is the only party both MainEHR LaunchScript and GenPRES Server can reach, so it is the sole channel between the EHR side and GenPRES. Nothing else can carry a launch.`  
3. `Only the Broker knows whether a credential was redeemed, and it cannot tell MainEHR LaunchScript, which has exited.`  
4. `The credential travels in a URL, so it lands in browser history, the address bar, and possibly referrer and proxy logs — hence single use, short lifetime.`  
5. `Both MainEHR Workstation and MainEHR LaunchScript run on the User's PC, so their calls originate there: the Workstation reaches UserRegistry and the PatientDataPlatform, MainEHR LaunchScript reaches the Broker. Every workstation therefore needs network access to all three, plus whatever secret authenticates it.`  
6. `GenPRES Server cannot reach a GenPRES Client (edge 5 goes one way only), so a Client only learns its Session ended at its next request. Until then it shows a live-looking screen.`

### **`Invariants`**

`Always true of the environment. Given: not ours to change.`

1. `A User has at most one active Patient at any moment in a MainEHR Session.`

### **`Possibilities`**

`May occur in the environment. Given: not ours to prevent, only to handle.`

1. `Users can leave a logged in MainEHR Session open and another User can act in it.`  
2. `Multiple Users can have the same Patient active each in their own MainEHR Session.`

### **`Rules`**

`What the [ours] components must enforce. Chosen, and changeable by decision. One assertion each; grouped for reading, numbered straight through for citing.`

**`Launch`**

1. `MainEHR LaunchScript decides which MainEHR User may run it.`  
2. `A LaunchCredential is accepted once; a second presentation is refused.`  
3. `A LaunchCredential is accepted only within its lifetime.`  
4. `Only GenPRES Server may redeem a LaunchCredential at the Broker.`  
5. `GenPRES Server takes the Role from the UserRegistry at each launch, never from the launch itself.`  
6. `If a launch cannot be honoured — no credential, no Role, or a required PIN not set (Rule 24) — no GenPRES Session is opened by it. There is no silent fallback: at most, GenPRES Client offers the User a fresh anonymous open (Rule 13; UC-8), which carries nothing over from the launch — no User, no Patient.`

**`Session`**

7. `A User has at most one open GenPRES Session; opening another closes the rest. The limit is per User, not per Patient: two Users may each hold their own Sessions for the same Patient at once.`  
8. `Every request from GenPRES Client refreshes its Session's idle clock.`  
9. `A GenPRES Session ends when the User closes it, when it has been idle too long, when the wrong-PIN limit is reached (Rule 27), or when that same User opens another Session (Rule 7). Closing is an explicit act in GenPRES Client: a browser that vanishes is indistinguishable from one gone quiet, so the Session is left to idle out. A GenPRES Server restart ends nothing: the Server holds no Session state to lose (Rule 31).`  
10. `When a GenPRES Session ends other than by the User closing it, the User is told at the next opportunity: through any Client still holding that Session's SessionId, at its next request, or at the User's next launch. The notice stands until the User acknowledges it, and never returns after: acknowledged once — sending alone does not count as telling.`  
11. `The SessionId is a bearer credential — whoever holds it can use it — so it never travels in a URL and never sits where script can read it: it rides in a cookie the browser alone handles.`  
12. `A GenPRES Session without a PatientId lets the User prescribe (Concept 15), Patient Data included, but a TreatmentPlan cannot be opened or created.`  
13. `A GenPRES Session opened without a launch is anonymous: it binds to no User and carries no UserContext, no Role, and no PatientId. Rules that speak of the Session's User (7, 10) do not apply to it, and neither does idling: it ends when closed, or at an absolute limit — enough to bound the SessionRecords it leaves behind, which are all it ever amounts to on the Server (Rule 31); its WorkPlan lives and dies with the browser (Concept 16). The Atomicity rules (40–45) have nothing to guard in it — it can commit nothing (Rule 12), so there is no transaction for them to protect.`

**`Record`**

14. `Every TreatmentPlan is created under the credentials of exactly one User — the Session's — and carries that User's identity. Within it, every OrderContext changed in the Session is stamped with that same UserContext; an unchanged OrderContext keeps the stamp it had.`  
15. `A TreatmentPlan is either Signed or Unsigned.`  
16. `Only the most recent Signed TreatmentPlan counts clinically.`  
17. `Signed TreatmentPlans are open to every User, to read: any of them may be opened, but only the most recent one can be built upon — opening an older one leaves creating blocked (Rule 20). An Unsigned TreatmentPlan is Rule 18's alone.`  
18. `Only the User who created an Unsigned TreatmentPlan can open that TreatmentPlan.`  
19. `A User can only start with the most recent TreatmentPlan that is either Signed or Unsigned and their own. Where neither exists, the User works from nothing: the Session's WorkPlan begins with no OrderContexts (Concept 16).`  
20. `A User may create a new TreatmentPlan, unless a Signed TreatmentPlan exists that is newer than the one the User opened with. Opening that newest Signed TreatmentPlan makes it the one the Session opened with — after that, creating is possible again (UC-6).`

**`Notification`**

21. `If a User is about to create a TreatmentPlan and an Unsigned TreatmentPlan of another User exists that is newer than the TreatmentPlan the User opened with — any TreatmentPlan at all, where the User opened with nothing — the User is notified — told whose work it is, not its contents — and may choose not to create.`

**`Signing`**

22. `GenPRES Server is the only party that verifies a GenPRES UserCredential; the PIN never leaves GenPRES.`  
23. `Every launch checks whether a PIN is set for the login.`  
24. `A Prescriber with no PIN must set one before the launch continues, and only after the UserRegistry has recognised their login.`  
25. `A Reader is never asked for a PIN: a Reader never creates a TreatmentPlan (Roles), so they have nothing to prove.`  
26. `GenPRES Server mails the User and records the change on every setting of a PIN and every replacement of one, the first setting included. The address comes from the UserRegistry.`  
27. `Wrong PIN entries count per GenPRES UserCredential, across Sessions, the count updated as one conditional operation at the GenPRES Database (Rule 40): a wrong entry at the configurable limit ends the Session (Rule 9) and suspends signing on the credential until the PIN is replaced (Rule 37). A correct entry resets the count, and a newly set PIN (Rule 26) starts with a count of zero.`

**`Configuration`**

28. `A LaunchCredential lives long enough to carry one launch — a page load and a retry or two — and no longer.`  
29. `A GenPRES Session lives long enough to span the gaps between a clinician's actions.`  
30. `The wrong-PIN limit is small enough to make guessing hopeless, large enough to forgive mistyping — and the PIN itself short enough to remember, large enough in its space that the limit keeps guessing hopeless.`

**`State`** `— where Session state lives; chosen so that GenPRES Server keeps none of it.`

31. `GenPRES Server holds no Session state between requests: the WorkPlan (Concept 16) lives in GenPRES Client, and a Session's identity and standing live in its SessionRecord in the GenPRES Database. Two Users' work cannot meet in the Server, because the Server holds neither.`  
32. `GenPRES Server takes the User and the Patient of a request from the SessionRecord, never from what the request carries — and a create whose OrderContexts name another Patient than the SessionRecord's is refused whole (Guarantee 1).`  
33. `The TreatmentPlan a Session opened with travels as the OpenedToken (Concept 17) — bound to the Session, the Patient and the TreatmentPlan — returned by GenPRES Client with every create and verified then (Rules 19, 20). It works exactly once: consumed by the create it accompanies and re-issued with the new baseline, a spent or expired one is refused.`  
34. `A choice to create anyway (Rule 21) travels as the NoticeToken (Concept 17): issued with the notice, naming the Unsigned TreatmentPlans it disclosed, honoured for those and for nothing newer.`  
35. `The stamps of Rule 14 are computed by GenPRES Server against the base TreatmentPlan; a stamp arriving from GenPRES Client is never accepted.`  
36. `The Rule 20 check and the append are one act at the GenPRES Database: a TreatmentPlan lands only if no Signed TreatmentPlan newer than the one its Session opened with has appeared meanwhile — an intervening Unsigned one does not block, it notifies (Rules 20, 21). More than one GenPRES Server may run; the Database decides which lands. A refusal never names a TreatmentPlan the caller may not open: it says whose, not which (Rules 17, 18, 21).`

**`Security`** `— what [ours] enforces against a hostile environment.`

37. `A PIN is replaced only by its User: a reset mails a one-time code (Rule 26), and returning the code together with the chosen new PIN through GenPRES Client replaces the old one in a single act — there is never a moment without a PIN. A code survives its short lifetime and a few wrong entries, then it is void: a fresh reset, with a fresh mail, is the only way on. Changing a PIN without a reset requires the current PIN.`  
38. `Every signature re-takes the Role from the UserRegistry: authority withdrawn since the launch blocks the signature at its commit.`  
39. `GenPRES Client erases the LaunchCredential from the URL and the browser history at first presentation, keeping it only in memory for retries within its lifetime (Rules 3, 28); GenPRES Server serves the Client so that nothing of a Session is cached or carried in a referrer, and no script but the Client's own runs in its pages.`

**`Atomicity`** `— what must be one act at the GenPRES Database.`

40. `Every change to a SessionRecord is one conditional operation, guarded by the state it expects: an ended Session can never return to open, and one open Session per User (Rule 7) is a Database constraint, enforced in the same act that opens the next.`  
41. `Expiry is checked when a request arrives, not only by a sweep: a request from a Session past its idle limit ends the Session then and there (Rules 8, 9) — it does not refresh it.`  
42. `Creating a TreatmentPlan is one transaction. At its commit the Database re-verifies everything the request rests on — the Session open, unexpired, and for this User and Patient (Rules 40, 41), the Role (Rule 38), the tokens (Rules 33, 34), the head (Rule 36), and for a signature the challenge (Rule 43) and the PIN against the UserCredential as it stands at that moment, replaced or suspended included (Rules 22, 27) — and all of it holds together, or nothing lands.`  
43. `A signature approves exactly what was shown. GenPRES Server issues the SigningChallenge (Concept 17), naming the plan to be signed — content, base, Patient. GenPRES Client shows it modally: sign as shown, or cancel and edit. The PIN comes back with the challenge, and the commit checks that the plan submitted is the plan named, then consumes it (Rule 42).`  
44. `Within the signing transaction GenPRES Server reads the PatientDataPlatform once more: where the Patient Data changed since the launch, the User is told and must choose to proceed before the signature lands — Rule 21's pattern, for data (Concept 2).`  
45. `Every request that changes anything carries a key of its own. The Database commits a key once: a retry returns the first result and never repeats the change.`

**`Audit`** `— the record of the acts around the record.`

46. `GenPRES Server appends to the audit, in the private store: every launch, honoured or refused; every Session opening and ending, with the reason; every create; every signature and every failed one; every PIN change; every refused request. Append-only; who reads it is out of scope (Guarantee 4).`

### **`Guarantees`**

`What the Rules add up to. Derived, not asserted: each holds because the Rules cited enforce it, and none is negotiable without changing a Rule.`

1. **`One constant.`** `A GenPRES PatientRecord is a sequence of TreatmentPlans in which the PatientId is the only constant: the Patient Data, the orders and the ordering User may all differ from TreatmentPlan to TreatmentPlan (Concepts 12, 13, 15). Only a launch supplies a PatientId (Concept 2) and no Session saves without one (Rules 12, 13), so no hand ever changes it.`  
2. **`One version.`** `At any moment exactly one TreatmentPlan is the visible version of the PatientRecord and the only starting point for updating it: the most recent Signed TreatmentPlan (Rules 16, 17) — or, for its creator alone, their own Unsigned TreatmentPlan where it is newer (Rules 18, 19). Nothing else can be built upon (Rule 20). Reading is wider than building: the Signed history is open to read (Rule 17), and a User may still look into their own superseded Unsigned work (Rule 18) — old versions and dead ends the record keeps, from which nothing grows.`  
3. **`Carts and one checkout.`** `Changing orders works like a shopping cart per User with a single shared checkout — the cart being the WorkPlan (Concept 16). It is private because of where it lives: in the User's own GenPRES Client, and GenPRES Server keeps none of it (Rule 31; Concepts 15, 16), and a User's Unsigned TreatmentPlans are closed to everyone else, existence excepted (Rules 18, 21). Signing is the only checkout, and there is one (Concept 14; Rules 16, 36): the first User to sign wins the version, and every other WorkPlan must be rebuilt on top of it (Rules 19, 20; UC-6).`  
4. **`Audit.`** `A Signed TreatmentPlan carries the User who signed it (Concepts 13, 14; Rule 14), and every OrderContext in it carries the User whose Session last changed it (Concept 10; Rule 14). The record keeps every version: append-only, each TreatmentPlan with its base (Concepts 12, 13). Together that is a full audit trail of every signed version of every OrderContext — held in the GenPRES Database's clinical store — every Signed TreatmentPlan with its base references — which is what the PatientDataPlatform copies (Actors 5, 6). Unsigned TreatmentPlans, SessionRecords, UserCredentials and tokens live in the private store and are never copied. Beside it stands the security audit (Rule 46): who launched, opened, created, signed, failed and changed what, and when. Reading either is out of scope for this document: no Session shows them (Rule 17). What is guaranteed here is that the trail exists, complete, for whatever reads the copy — that nothing secret rides along with it — and what a signature attests, said plainly: the holder of the credential in an authenticated Session (Rules 22, 43), per credential, not per person (UC-5). Non-repudiation is not claimed.`

### **`Open Questions`**

`Decisions not yet made. Each one blocks something.`

1. **`Mail deliverability.`** `Rule 26's guarantee — and the tamper evidence UC-7 is built on — holds only if the UserRegistry address is current and MailService delivers. Neither can be checked from here. Blocks: the failure paths of UC-2 step 4 and UC-7.`  
2. **`Payload.`** `Under Rule 31 the whole WorkPlan (Concept 16) travels with every computing request and every create. Whether that is acceptable is a measurement, not a judgement. Blocks: nothing yet — but a bad number would force a server-side cache of the WorkPlan, which must then be built as an optimisation the Rules never depend on, losable without breaking anything.`  
3. **`A bound launch.`** `The LaunchCredential is an unbound bearer code: nothing ties it to the browser the LaunchScript opened, because the LaunchScript's only channel to that browser is the URL itself. Rule 39 shrinks the theft window to the first page load (UC-1 ext 7b); closing it needs the EHR side to run an authorisation flow that can bind the transaction — SMART App Launch is the shape — and that side is [given]. Blocks: retiring the race that remains in ext 7b.`  
4. **`Step-up signing.`** `The PIN attests a credential holder, not a person (Guarantee 4). Attesting the person needs an authenticator GenPRES does not have — an identity provider, WebAuthn, a smartcard — none of which exists as an actor here. Rules 37, 43 and 27 are the interim. Blocks: claiming non-repudiation; retiring the per-credential caveat of UC-5.`  
5. **`Finer patient authorisation.`** `The launch is MainEHR's word that this User may work on this Patient now (Concept 3), and GenPRES enforces nothing finer — no care relationship, encounter, or co-sign requirement, because only MainEHR knows them. Blocks: any rule finer than the Prescriber/Reader split.`  
6. **`A tamper-resistant audit.`** `Rule 46's audit is append-only in the private store, but the same administrator who runs the store could alter it, and its schema is GenPRES's own, not HL7 AuditEvent. Blocks: audit that binds anyone but GenPRES.`  
7. **`Proof under concurrency.`** `The Atomicity rules (40–45) are stated, not proven: their invariants — once ended always ended, one open Session per User, no commit after revocation or expiry, one result per key — deserve model checking before the Guarantees are claimed under load. Blocks: nothing in the design; everything in the confidence.`

