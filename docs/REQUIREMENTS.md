# ITSM Ticket Anonymizer
## System Requirements Specification

---

| | |
|---|---|
| **Document ID** | SRS-ITSM-ANON-001 |
| **Version** | 1.0 |
| **Date** | 2026-04-07 |
| **Status** | Draft |
| **Prepared by** | Internal Team |
| **Classification** | Internal Use Only |

---

## Revision History

| Version | Date | Author | Description |
|---|---|---|---|
| 1.0 | 2026-04-07 | Internal Team | Initial draft |

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [System Overview](#2-system-overview)
3. [Stakeholders and User Classes](#3-stakeholders-and-user-classes)
4. [Functional Requirements](#4-functional-requirements)
   - 4.1 [File Input](#41-file-input)
   - 4.2 [Data Preview](#42-data-preview)
   - 4.3 [Column Classification](#43-column-classification)
   - 4.4 [Anonymization](#44-anonymization)
   - 4.5 [De-anonymization](#45-de-anonymization)
   - 4.6 [File Output](#46-file-output)
   - 4.7 [Custom Categories](#47-custom-categories)
   - 4.8 [User Interface](#48-user-interface)
5. [Interface Requirements](#5-interface-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Constraints and Assumptions](#7-constraints-and-assumptions)
8. [Requirements Traceability Matrix](#8-requirements-traceability-matrix)

---

## 1. Introduction

### 1.1 Purpose

This document specifies the system requirements for the **ITSM Ticket Anonymizer**, a desktop application that allows company staff to anonymize sensitive data contained in ITSM or Salesforce ticket exports, and to reverse the anonymization when needed.

It is intended as a reference for the internal development team and any stakeholder who needs to understand the system's expected behaviour and boundaries.

### 1.2 Scope

The system is a standalone Windows desktop application. It operates exclusively on local files (CSV and Excel) and does not connect to any external service or database. It is intended for internal use by any member of the company.

> **Out of scope:** Integration with Salesforce or any ITSM platform, user authentication, cloud storage, and multi-user collaboration are explicitly excluded from this version.

### 1.3 Definitions and Acronyms

| Term | Definition |
|---|---|
| **Anonymization** | The process of replacing sensitive values in a dataset with opaque tokens, making the data safe to share. |
| **De-anonymization** | The reverse process: restoring original values from an anonymized dataset using a transcode table. |
| **Token** | A short, opaque identifier (e.g. `CUST-A3F8C2`) used to replace a sensitive value. |
| **Transcode Table** | A file recording the mapping between original values and their corresponding tokens, per column. |
| **Column Classification** | The assignment of a sensitive data category to a specific column in the input file. |
| **HMAC-SHA256** | A cryptographic hash function used to generate deterministic tokens within a session. |
| **Category** | A label describing the type of sensitive data in a column (e.g. `CustomerCompany`, `PersonName`). |
| **CSV** | Comma-Separated Values file format. |
| **XLSX** | Microsoft Excel Open XML Spreadsheet file format. |
| **SRS** | System Requirements Specification. |
| **WPF** | Windows Presentation Foundation — the UI framework used by this application. |

### 1.4 Document Overview

| Section | Content |
|---|---|
| §2 | High-level description of the system and its workflow |
| §3 | Stakeholders and user classes |
| §4 | Functional requirements, grouped by feature area |
| §5 | Interface requirements (file I/O, persistence) |
| §6 | Non-functional requirements (OS, performance, distribution) |
| §7 | Constraints and assumptions |
| §8 | Requirements traceability matrix |

---

## 2. System Overview

The **ITSM Ticket Anonymizer** is a Windows desktop application (WPF, .NET 8) that processes tabular data files exported from ITSM or Salesforce systems. It provides a guided, step-by-step workflow:

| Step | Action |
|---|---|
| **1** | User loads an input file (CSV or XLSX). |
| **2** | System auto-detects columns likely to contain sensitive data and proposes a classification. |
| **3** | User reviews, adjusts, or overrides the classification. |
| **4** | System replaces sensitive values with anonymous tokens and produces an anonymized file and a transcode table. |
| **5** | *(Optional)* User loads the anonymized file and its transcode table to restore the original data. |

> **Privacy note:** The system runs entirely offline. No data leaves the user's machine at any point.

---

## 3. Stakeholders and User Classes

| Stakeholder | Role | Interaction with the System |
|---|---|---|
| **Company Staff** | Primary user | Loads files, reviews column classifications, triggers anonymization/de-anonymization, exports results |
| **Internal Team** | Developer / Maintainer | Develops, maintains, and distributes the application |

All company staff members are considered equivalent users. No authentication, authorization, or role separation is required.

---

## 4. Functional Requirements

> **Priority notation:**
> - **Must** — mandatory; the system shall not be accepted without this.
> - **Should** — strongly desired; expected in the current release unless blocked.

---

### 4.1 File Input

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-001 | The system shall allow the user to load a data file in CSV format. | Must |
| REQ-FUNC-002 | The system shall allow the user to load a data file in XLSX format. | Must |
| REQ-FUNC-003 | The system shall allow the user to clear the current file selection and load a different file without restarting the application. | Must |
| REQ-FUNC-004 | The system shall parse the loaded file and extract its headers and row data. | Must |

---

### 4.2 Data Preview

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-005 | The system shall display a preview of the original loaded data before any processing is performed. | Must |
| REQ-FUNC-006 | The system shall display a preview of the anonymized data after the anonymization process completes. | Must |
| REQ-FUNC-007 | The system shall display a preview of the restored data after the de-anonymization process completes. | Must |
| REQ-FUNC-008 | The system shall present the three previews (original, anonymized, restored) in separate, clearly labelled tabs. | Should |

---

### 4.3 Column Classification

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-009 | The system shall automatically detect columns likely to contain sensitive data based on their name, using pattern matching. | Must |
| REQ-FUNC-010 | The system shall support the following built-in sensitive data categories: `CustomerCompany`, `SerialNumber`, `PersonName`, `MachineType`, `Custom`. | Must |
| REQ-FUNC-011 | The system shall allow the user to manually assign or override the category of any detected column. | Must |
| REQ-FUNC-012 | The system shall allow the user to exclude a detected column from anonymization. | Must |
| REQ-FUNC-013 | Columns not detected as sensitive shall not be anonymized unless explicitly classified by the user. | Must |

---

### 4.4 Anonymization

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-014 | The system shall replace sensitive values with tokens generated via HMAC-SHA256 hashing. | Must |
| REQ-FUNC-015 | The system shall generate the same token for identical input values within the same session (deterministic per session). | Must |
| REQ-FUNC-016 | Tokens generated in different sessions shall differ for the same input value. | Must |
| REQ-FUNC-017 | Tokens for built-in categories shall include a category-specific prefix (e.g. `CUST-`, `SN-`, `PERSON-`, `MACH-`). | Must |
| REQ-FUNC-018 | The system shall produce a transcode table recording, for each replaced value: the row index, column name, category, original value, and anonymized token. | Must |
| REQ-FUNC-019 | The system shall display summary statistics after anonymization: total replacements, number of affected rows, and number of affected columns. | Should |

---

### 4.5 De-anonymization

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-020 | The system shall allow the user to load a transcode table in CSV format to perform de-anonymization. | Must |
| REQ-FUNC-021 | The system shall allow the user to load a transcode table in XLSX format to perform de-anonymization. | Must |
| REQ-FUNC-022 | The system shall restore original values in the anonymized dataset by matching tokens to their originals using the transcode table, scoped per column. | Must |
| REQ-FUNC-023 | The system shall handle duplicate anonymized values in the transcode table without raising an error (last occurrence takes precedence). | Must |
| REQ-FUNC-024 | The system shall display summary statistics after de-anonymization: total restorations, affected rows, and affected columns. | Should |

---

### 4.6 File Output

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-025 | The system shall allow the user to export the anonymized dataset as a CSV file. | Must |
| REQ-FUNC-026 | The system shall allow the user to export the anonymized dataset as an XLSX file. | Must |
| REQ-FUNC-027 | The system shall allow the user to export the transcode table as a CSV file. | Must |
| REQ-FUNC-028 | The system shall allow the user to export the transcode table as an XLSX file. | Must |
| REQ-FUNC-029 | The system shall allow the user to export the restored (de-anonymized) dataset as a CSV file. | Must |
| REQ-FUNC-030 | The system shall preserve all non-sensitive columns unchanged in all exported files. | Must |

---

### 4.7 Custom Categories

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-031 | The system shall allow the user to define custom sensitive data categories through a dedicated UI dialog. | Must |
| REQ-FUNC-032 | Each custom category shall have a user-defined name. | Must |
| REQ-FUNC-033 | The user shall be able to optionally assign a prefix to a custom category; if a prefix is set, tokens for that category shall follow the format `PREFIX-HASH`. | Must |
| REQ-FUNC-034 | If no prefix is assigned to a custom category, the token shall consist of the hash value alone. | Must |
| REQ-FUNC-035 | The system shall persist custom category definitions across sessions, storing them locally on the user's machine. | Must |
| REQ-FUNC-036 | The system shall allow the user to edit or delete existing custom categories. | Must |

---

### 4.8 User Interface

| ID | Requirement | Priority |
|---|---|---|
| REQ-FUNC-037 | The system shall present the workflow as clearly labelled steps so the user understands what action is expected at each stage. | Must |
| REQ-FUNC-038 | The system shall display a wait cursor while a long-running operation (parse, anonymize, de-anonymize) is in progress. | Should |
| REQ-FUNC-039 | The system shall clearly distinguish in the UI whether the user is expected to load an original file (for anonymization) or an already-anonymized file (for restoration). | Must |

---

## 5. Interface Requirements

| ID | Requirement | Priority |
|---|---|---|
| REQ-INT-001 | The system shall read input data exclusively from local files in CSV or XLSX format. | Must |
| REQ-INT-002 | The system shall write output data exclusively to local files in CSV or XLSX format. | Must |
| REQ-INT-003 | The system shall store custom category definitions in a JSON file located at `%AppData%\ITSMTicketAnonymizer\categories.json`. | Must |
| REQ-INT-004 | The system shall not connect to any network resource or external service. | Must |

---

## 6. Non-Functional Requirements

| ID | Requirement | Priority |
|---|---|---|
| REQ-NFR-001 | The system shall run on Windows 10 or Windows 11 (64-bit). | Must |
| REQ-NFR-002 | The system shall be distributable as a single self-contained executable requiring no separate .NET runtime installation on the target machine. | Should |
| REQ-NFR-003 | The application shall start and be ready for user interaction within 5 seconds on standard company hardware. | Should |
| REQ-NFR-004 | The system shall process files of up to 50,000 rows without becoming unresponsive. | Should |

---

## 7. Constraints and Assumptions

### Constraints

| ID | Statement |
|---|---|
| CON-001 | The application is a standalone tool; no integration with external ITSM or Salesforce systems is in scope. |
| CON-002 | The application runs on Windows only (WPF, .NET 8 Windows target). |
| CON-003 | Security of the anonymization relies on the confidentiality of the transcode table; if the transcode table is shared with a third party, re-identification of the original data becomes possible. |

### Assumptions

| ID | Statement |
|---|---|
| ASS-001 | Input files are well-formed CSV or XLSX files containing at least one header row. |
| ASS-002 | The user is responsible for identifying columns containing sensitive data when auto-detection does not cover them. |
| ASS-003 | Custom category definitions stored in `%AppData%` are machine-local and are not shared across users or machines. |

---

## 8. Requirements Traceability Matrix

| Requirement ID(s) | Short Description | Feature Area |
|---|---|---|
| REQ-FUNC-001 – 004 | File loading and parsing | File Input |
| REQ-FUNC-005 – 008 | Data preview tabs | Data Preview |
| REQ-FUNC-009 – 013 | Auto-detection and manual classification | Column Classification |
| REQ-FUNC-014 – 019 | Token generation and transcode table production | Anonymization Engine |
| REQ-FUNC-020 – 024 | Reverse process via transcode table | De-anonymization Engine |
| REQ-FUNC-025 – 030 | CSV and XLSX export | File Writer |
| REQ-FUNC-031 – 036 | User-defined categories with local persistence | Custom Categories |
| REQ-FUNC-037 – 039 | Step labels, wait cursor, UX clarity | User Interface |
| REQ-INT-001 – 004 | Local file I/O, offline operation | Interface |
| REQ-NFR-001 – 004 | OS target, single-file distribution, performance | Non-Functional |
| CON-001 – 003 | Scope and security boundaries | Constraints |
| ASS-001 – 003 | Input format and user responsibility assumptions | Assumptions |

---

*End of document — SRS-ITSM-ANON-001 v1.0*
