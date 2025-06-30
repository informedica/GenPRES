# Critical Tasks for Usability Evaluation of GenPRES

This document outlines the initial set of critical tasks identified for usability evaluation of GenPRES, a clinical decision support software used in pediatric and neonatal intensive care environments. These tasks reflect the core functionalities that users must perform accurately and efficiently to ensure patient safety and clinical effectiveness.

## Task 1: Accessing the Application

- Open GenPRES using a URL from an EPD system with embedded patient parameters.
- Verify that the correct patient context is displayed.

## Task 2: Prescribing Medication

- Select a medication from the provided list.
- Enter patient-specific dosage information.
- Validate automatic dose calculation results.
- Confirm prescription entry.

## Task 3: Responding to Safety Alerts

- Receive and review alerts related to dose range violations or drug interactions.
- Take appropriate corrective actions (e.g., adjust dose, choose alternative medication).
- Document rationale for override if applicable.

## Task 4: Modifying an Active Prescription

- Access the current list of active prescriptions for a patient.
- Select and modify an existing medication order.
- Save the updated prescription with system confirmation.

## Task 5: Calculating and Ordering TPN

- Navigate to the TPN interface.
- Input relevant patient parameters (e.g., weight, age).
- Verify auto-generated TPN formulation.
- Export or print the TPN order for pharmacy processing.

## Task 6: Logging Out and Session Handling

- Exit the application.
- Ensure that no patient or session data persists after logout (stateless behavior).

## Task 7: Handling Network Interruptions

- Simulate a temporary network loss.
- Resume session via URL after reconnection without data loss or corruption.

---

**Version**: 1.0  
**Date**: May 2025  
**Author**: Usability Lead, GenPRES Project