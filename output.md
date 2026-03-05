## **Dispatch trace (all ticks until work completes)**

### **Tick `t = 0` (0–30)**

* **T1** → `S-1003 Primary`  
   Completed at **30** (Note: deadline was 0 → SLA miss)

### **Tick `t = 30` (30–60)**

* **T1** → `S-1001 Primary`  
   Completed at **60** → creates `S-1001 Review` (ready at 60\)

### **Tick `t = 60` (60–90) — T2 becomes available**

* **T1** → `S-1008 Primary`

* **T2** → `S-1001 Review` (must be different from T1)  
   Completed at **90** → creates `S-1008 Review` (ready at 90); `S-1001` complete

### **Tick `t = 90` (90–120)**

* **T1** → `S-1005 Primary`

* **T2** → `S-1008 Review` (must be different from T1)  
   Completed at **120** → creates `S-1005 Review` (ready at 120); `S-1008` complete

### **Tick `t = 120` (120–150)**

* **T1** → `S-1007 Primary` (next earliest SLA among remaining primaries)

* **T2** → `S-1005 Review` (must be different from T1)  
   Completed at **150** → `S-1007` complete; `S-1005` complete

### **Tick `t = 150` (150–180)**

Remaining primaries: `S-1002` (SLA+priority), `S-1006` (no SLA), `S-1004` (no SLA, double)

* **T1** → `S-1002 Primary`

* **T2** → `S-1006 Primary` (older upload than S-1004 among no-SLA items)  
   Completed at **180** → `S-1002` complete; `S-1006` complete

### **Tick `t = 180` (180–210)**

Remaining: `S-1004` (double, no SLA)

* **T1** → `S-1004 Primary`

* **T2** → *(idle)*  
   Completed at **210** → creates `S-1004 Review` (ready at 210\)

### **Tick `t = 210` (210–240) — last tick T1 is available**

* **T1** → *(idle)* (can’t take review; did the primary)

* **T2** → `S-1004 Review`  
   Completed at **240** → `S-1004` complete

✅ **All work completed by t \= 240\.**

---

## **Completion times and SLA outcomes**

Deadlines are `uploadedAtMinute + slaMinutes`:

| Study | Deadline | Completion | Outcome |
| ----- | ----- | ----- | ----- |
| S-1003 | 0 | 30 | **WillMiss** |
| S-1001 (double) | 150 | 90 | OnTrack |
| S-1008 (double) | 170 | 120 | OnTrack |
| S-1005 (double) | 240 | 150 | OnTrack |
| S-1007 | 270 | 150 | OnTrack |
| S-1002 | 420 | 180 | OnTrack |
| S-1004 (double) | — | 240 | No SLA |
| S-1006 | — | 180 | No SLA |

