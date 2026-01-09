# Sleep Study Scoring Dispatch Challenge

*(Live Pair-Programming Exercise – 90 minutes)*

## Overview

Beacon receives **Home Sleep Test (HST)** uploads throughout the day. Each uploaded study must be scored by a sleep technician. Some studies also require a double-scoring review by a second technician after the initial scoring is complete.

Your task is to implement a dispatch system that:

1. Assigns work to technicians incrementally as capacity becomes available  
2. Monitors and predicts SLA risk over time

This exercise is a live pair-programming interview focused on general programming ability, .NET/C\# fluency, and collaboration.

You will receive this prompt one day before the interview so you can familiarize yourself with the problem and prepare your development environment. No advance coding or solution preparation is expected or required.

### Before the interview

Please ensure you have:

* A working .NET development environment (e.g., Visual Studio, VS Code)  
* A recent .NET SDK installed

No other preparation is necessary. Please be prepared to share your screen during the interview as you work through the problem.

### During the interview

At the start of the interview, we will provide a project skeleton (basic solution structure, empty classes, and/or a simple runner). You are welcome to use the provided skeleton as-is, modify it, or ignore it and structure the solution differently, if you prefer.

### What we’re looking for

This is not a speed test, and it’s not required that you complete the entire exercise within the allotted time. We are much more interested in the quality and clarity of your code, how you reason about the problem and make tradeoffs, how you communicate and collaborate while pairing, and whether the system you’re building is understandable, correct, and deterministic. You do not need to build a perfect or fully optimized system – a partial solution that is clean, well-structured, and thoughtfully discussed is far preferable to a rushed or brittle “complete” solution, and a clear approach that makes steady progress—and explains what you would do next—is ideal.

### Time Guidance (90 minutes)

* 10 min: clarify rules \+ outline approach  
* 55–60 min: implement dispatch simulation  
* 15 min: SLA monitoring \+ final report  
* 5–10 min: cleanup and discussion

## Time Model

All times in this exercise are relative:

* Time is measured in minutes  
* Time advances in 30-minute ticks  
* Negative values indicate events that occurred before `now`  
* The simulation horizon is 1440 minutes (24 hours)

Example:

* `uploadedAtMinute = -60` → uploaded one hour ago  
* `slaMinutes = 240` → SLA deadline is 4 hours after upload  
* `deadlineMinute = uploadedAtMinute + slaMinutes`

## Problem

Implement a dispatch simulation that:

1. Repeatedly/incrementally assigns eligible work to available technicians based on what work and capacity are available at that moment  
2. Advances time in 30-minute increments  
3. Generates review work for studies requiring double scoring  
4. Produces a report predicting SLA outcomes

## Domain Model

### **Study**

Each study has:

* `studyId` (string)  
* `customerId` (string)  
* `uploadedAtMinute` (int)  
* `slaMinutes` (int? — nullable)  
* `priorityFlag` (bool)  
* `requiresDoubleScoring` (bool)

### **Work Items**

Each study generates:

* One Primary work item  
* One Review work item *only if* `requiresDoubleScoring = true`

Represent work items explicitly:

* `workItemId` (string)  
* `studyId` (string)  
* `type` (`Primary` or `Review`)  
* `readyAtMinute` (int)  
* `deadlineMinute` (int? — inherited from study SLA)

Rules:

* Primary work items have `readyAtMinute = max(uploadedAtMinute, 0)`  
* Review work items are created only after primary completes  
* Review work items inherit the same SLA deadline

**Technician**

Each technician has:

* `techId` (string)  
* `availabilityBlocks`: list of  
  * `startMinute` (inclusive)  
    `endMinute` (exclusive)

A technician is available for a tick starting at minute `t` if:

`startMinute <= t && t + 30 <= endMinute`

Each technician can work on at most one work item per tick.

## Dispatch Simulation

Simulate dispatch as follows:

* Start at `t = 0`  
* Repeat until `t >= 1440` or no further work can be completed:  
  1. Identify technicians available for tick `[t, t + 30)`  
  2. Identify eligible work items (`readyAtMinute <= t` and not completed)  
  3. Assign work items to technicians using the priority rules below  
  4. Mark assigned work items as completed at `t + 30`  
  5. For completed primaries that require double scoring:  
     * Create a Review work item with `readyAtMinute = t + 30`  
     * Must be assigned to a different technician  
  6. Advance `t = t + 30`

## Dispatch Priority Rules

When selecting which work item to assign next, order eligible items by:

1. SLA urgency  
   * Items with deadlines come before items without deadlines  
   * Earlier `deadlineMinute` first  
2. Review urgency  
   * `Review` before `Primary`  
3. Priority customers  
   * `priorityFlag = true` before `false`  
4. Age  
   * Earlier `uploadedAtMinute` first  
5. Tie-breaker  
   * `studyId` or `workItemId` ascending

### Technician selection

Choose a deterministic strategy, such as:

* Lowest `techId`  
* Round-robin by `techId`

## Completion Rules

A study is considered complete when:

* Primary is completed, and  
* Review is completed (if required)

## SLA Risk Monitoring

At the start of each tick (before dispatching work), compute and output an SLA risk snapshot for all SLA-bound studies that are not yet complete. You can use an optimistic earliest-possible completion estimate to classify each study as:

* OnTrack  
   `earliestCompletionMinute <= deadlineMinute`  
* AtRisk  
   `deadlineMinute - earliestCompletionMinute <= thresholdMinutes`  
* WillMiss  
   `earliestCompletionMinute > deadlineMinute`

Choose a deterministic `thresholdMinutes` value (e.g. 60\) and document it.

### Per-tick output (minimum)

For each tick `t`, output:

* `t`  
* Count of SLA studies: `OnTrack / AtRisk / WillMiss`  
* (Optional) List of the most urgent `AtRisk / WillMiss` studyIds

## Final SLA Report

After the simulation completes, produce a final SLA report using actual simulated completion times, including:

* `studyId`  
* `deadlineMinute`  
* `projectedCompletionMinute` (or null)  
* Final `slaStatus`

Also output a customer-level summary:

* `customerId`  
* Counts of `OnTrack / AtRisk / WillMiss`

## Required Output

1. Dispatch log  
   * Tick minute  
   * `techId`  
   * `studyId`  
   * work type (`Primary` / `Review`)  
2. Per-tick SLA risk snapshots  
3. Final SLA report \+ customer summary

Printed output is sufficient; no UI required.

