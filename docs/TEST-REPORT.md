# Test Report — SfAnonymizer.Core.Tests

**Date:** 2026-04-09
**Framework:** xUnit 2.9 / .NET 8.0
**Result:** ✅ 111 / 111 passed — 0 failures — 0 skipped
**Total duration:** ~2.2 s

---

## Summary by class

| Test class | Tests | Result | Source |
|---|---|---|---|
| `TokenGeneratorTests` | 14 | ✅ All passed | `TokenGeneratorTests.cs` |
| `AnonymizationEngineTests` | 12 | ✅ All passed | `AnonymizationEngineTests.cs` |
| `DeAnonymizationEngineTests` | 10 | ✅ All passed | `DeAnonymizationEngineTests.cs` |
| `SalesforceColumnDetectorTests` | 39 | ✅ All passed | `SalesforceColumnDetectorTests.cs` |
| `FileParserTests` | 15 | ✅ All passed | `FileParserTests.cs` |
| `AnonymizationResultTests` | 6 | ✅ All passed | `AnonymizationResultTests.cs` |

---

## What is tested and why

### TokenGenerator (14 tests)

`TokenGenerator` is the security-critical component that produces all anonymized tokens. Bugs here would silently corrupt output or make the transcode table inconsistent.

| Test | Purpose |
|---|---|
| `GetToken_EmptyString_ReturnsInputUnchanged` | Empty values must never be replaced |
| `GetToken_WhitespaceOnly_ReturnsInputUnchanged` (×3 — space, multiple spaces, tab) | Whitespace-only values must never be replaced |
| `GetToken_SameInput_ReturnsSameTokenWithinSession` | Same original value → same token within one session (transcode table consistency) |
| `GetToken_DifferentInputs_ReturnDifferentTokens` | Different originals → different tokens (no accidental merging) |
| `GetToken_BuiltInCategory_HasExpectedPrefix` (×5 — all categories) | Each built-in category produces its expected prefix: `CUST-`, `SN-`, `PERSON-`, `MACH-`, `CUSTOM-` |
| `GetToken_CustomCategoryWithPrefix_UsesCustomPrefix` | Custom category with prefix enabled → token uses that prefix |
| `GetToken_CustomCategoryWithoutPrefix_ReturnsRawHash` | Custom category with prefix disabled → raw 6-char hash, no dash |
| `GetToken_CustomCategoryWithEmptyPrefix_ReturnsRawHash` | Custom category with `UsePrefix=true` but blank prefix → raw hash (no leading dash) |
| `GetToken_CustomCategoryOverridesBuiltInPrefix` | Custom definition takes priority over the built-in enum prefix |
| `GetToken_HashPortion_Is6CharHex` | Hash portion is exactly 6 uppercase hex characters |
| `Reset_ChangesTokensForSameInput` | After `Reset()`, same input produces a different token |
| `Reset_MaintainsDeterminismAfterReset` | Determinism is preserved within the new session after `Reset()` |

---

### AnonymizationEngine (12 tests)

`AnonymizationEngine` orchestrates the full anonymization pipeline. All tests use explicit `overrideClassifications` to isolate engine logic from the detector.

| Test | Purpose |
|---|---|
| `Anonymize_SensitiveColumn_ValueIsReplaced` | Values in classified columns are replaced with a token |
| `Anonymize_NonSensitiveColumn_ValueUnchanged` | Unclassified columns pass through untouched |
| `Anonymize_EmptyValue_NotReplaced` | Empty string in a sensitive column is left as-is |
| `Anonymize_WhitespaceOnlyValue_NotReplaced` | Whitespace-only value in a sensitive column is left as-is |
| `Anonymize_SameValueInSameColumn_GetsSameToken` | Same value repeated across rows maps to the same token (determinism within one call) |
| `Anonymize_TranscodeTable_ContainsOriginalAndAnonymizedValues` | Transcode table entry has correct `ColumnName`, `OriginalValue`, and `AnonymizedValue` |
| `Anonymize_TranscodeTable_RowIndex_IsOneBased` | `RowIndex` starts at 1 (human-readable, matches file row number) |
| `Anonymize_EmptyValue_NotIncludedInTranscodeTable` | Empty values are not written to the transcode table |
| `Anonymize_MultipleRows_TranscodeTableHasOneEntryPerReplacedCell` | Each replaced cell produces exactly one transcode entry |
| `Anonymize_ClassificationColumnNameCaseInsensitive_StillMatched` | Classification `"Company"` matches header `"COMPANY"` |
| `Anonymize_OverrideClassifications_BypassAutoDetection` | A column not auto-detectable (e.g. `"Notes"`) is anonymized when forced via overrides |
| `Anonymize_EmptyOverrideList_NoColumnsAnonymized` | Empty override list → no replacements, no transcode entries |
| `Anonymize_ResultHeaders_MatchInputHeaders` | Result headers are identical to input headers |

---

### DeAnonymizationEngine (10 tests)

`DeAnonymizationEngine` reverses anonymization. The most important property is **per-column scoping**: the same anonymized token in two different columns must restore to each column's own original value.

| Test | Purpose |
|---|---|
| `DeAnonymize_KnownToken_RestoresOriginalValue` | A token present in the transcode table is correctly restored |
| `DeAnonymize_UnknownToken_LeftAsIs` | A token not in the transcode table is left as-is (no crash, no data loss) |
| `DeAnonymize_ColumnNotInTranscodeTable_LeftAsIs` | A column absent from the transcode table passes through unchanged |
| `DeAnonymize_EmptyTranscodeTable_AllValuesUnchanged` | Empty transcode table → all values unchanged |
| `DeAnonymize_SameTokenInTwoColumns_EachRestoredToItsOwnOriginal` | **Per-column scoping**: identical token in `CompanyA` and `CompanyB` resolves to different originals |
| `DeAnonymize_TotalRestorations_CountsAllRestoredCells` | `TotalRestorations` counts every individual cell that was restored |
| `DeAnonymize_AffectedRows_CountsDistinctRowsWithAtLeastOneRestoration` | `AffectedRows` counts distinct row indexes (not total restorations) |
| `DeAnonymize_AffectedColumns_CountsDistinctColumnsWithAtLeastOneRestoration` | `AffectedColumns` counts distinct column names |
| `DeAnonymize_DuplicateAnonymizedValue_LastWins` | If two transcode entries share the same anonymized value in the same column, the last one wins |
| `DeAnonymize_ResultHeaders_MatchInputHeaders` | Result headers are identical to input headers |

---

### SalesforceColumnDetector (39 tests)

Pattern-matching logic is easy to silently regress (e.g. a regex change could stop detecting `"Account.Name"`). These tests cover every supported column name variant and an equal number of names that must **not** be matched.

#### CustomerCompany — detected names (9 tests)

`Account.Name`, `Account Name`, `AccountName`, `Company`, `Customer`, `Client`, `Organization Name`, `Organisation Name`, `Organisation`, `Organization`

#### SerialNumber — detected names (6 tests)

`Serial Number`, `SerialNumber`, `Serial No`, `Serial#`, `Asset Serial`, `SN`

#### PersonName — detected names (5 tests)

`First Name`, `Last Name`, `Full Name`, `Contact Name`, `Name`

#### MachineType — detected names (7 tests)

`Machine Type`, `Machine Family`, `Machine Model`, `Product Family`, `Asset Type`, `Model`, `Family`

#### Non-sensitive columns — must NOT be detected (8 tests)

`Status`, `Priority`, `Description`, `Ticket ID`, `Created Date`, `Category`, `Subject`, `Owner`

#### Case variants — correct category regardless of case (5 tests)

`COMPANY` → CustomerCompany, `company` → CustomerCompany, `SERIAL NUMBER` → SerialNumber, `FIRST NAME` → PersonName, `MACHINE TYPE` → MachineType

#### Partial / ambiguous names — must NOT be detected (3 tests)

| Name | Why it must not match |
|---|---|
| `"Company Name"` | `company` only matches as an exact standalone word |
| `"First"` | PersonName requires `"First Name"`, not the word alone |
| `"Serial"` | SerialNumber requires `"Serial Number/No/Num/#"`, not the word alone |

#### Other (3 tests)

| Test | Purpose |
|---|---|
| `Classify_MultipleHeaders_AllSensitiveColumnsDetected` | All 4 sensitive columns in a mixed list are detected at once |
| `Classify_NoSensitiveHeaders_ReturnsEmptyList` | A list with no sensitive columns returns an empty result |
| `Classify_EmptyHeaderList_ReturnsEmptyList` | Empty header list returns empty result without crashing |
| `Classify_DetectedColumn_HasIsAutoDetectedTrue` | Auto-detected columns have `IsAutoDetected = true` |

---

### FileParser (15 tests)

Integration tests: real temporary files are written to disk, parsed, then cleaned up. Covers both happy-path parsing and error-handling scenarios.

#### CSV parsing — happy path (6 tests)

| Test | Purpose |
|---|---|
| `ParseAsync_CsvFile_ReturnsCorrectHeaders` | Headers parsed in correct order |
| `ParseAsync_CsvFile_ReturnsCorrectRowCount` | All data rows returned |
| `ParseAsync_CsvFile_RowValuesAreCorrect` | Cell values match file content |
| `ParseAsync_CsvFile_ValuesTrimmed` | Leading/trailing whitespace stripped from values |
| `ParseAsync_CsvRowsAccessible_CaseInsensitive` | Row dictionaries support case-insensitive key access |
| `ParseAsync_CsvWithHeadersOnly_ReturnsEmptyRows` | File with only a header row → empty row list, no crash |

#### Excel parsing — happy path (2 tests)

| Test | Purpose |
|---|---|
| `ParseAsync_ExcelFile_ReturnsCorrectHeaders` | Headers parsed from first sheet |
| `ParseAsync_ExcelFile_ReturnsCorrectRowValues` | Cell values from Excel rows match expected |

#### Transcode table parsing (2 tests)

| Test | Purpose |
|---|---|
| `ParseTranscodeTableAsync_Csv_ReturnsCorrectEntries` | All five fields (Row, Column, Category, Original Value, Anonymized Value) parsed correctly |
| `ParseTranscodeTableAsync_Csv_MultipleEntries_AllParsed` | Multiple transcode rows all returned |

#### Error handling — no unhandled crashes (5 tests)

These tests verify that `FileParser` raises typed, descriptive exceptions for bad input. Since the ViewModel wraps all `FileParser` calls in `catch (Exception ex)`, a typed exception becomes a user-visible `StatusMessage` instead of an application crash.

| Test | Scenario | Expected exception |
|---|---|---|
| `ParseAsync_UnsupportedExtension_ThrowsNotSupportedException` | `.txt` file passed as input | `NotSupportedException` |
| `ParseTranscodeTableAsync_UnsupportedExtension_ThrowsNotSupportedException` | `.txt` file passed as transcode | `NotSupportedException` |
| `ParseAsync_ExcelWithEmptyFirstSheet_ThrowsInvalidOperationException` | Excel where first sheet is blank (e.g. active sheet was different in Excel) | `InvalidOperationException` with "empty" in message |
| `ParseTranscodeTableAsync_ExcelWithEmptyFirstSheet_ThrowsInvalidOperationException` | Blank first sheet in transcode Excel | `InvalidOperationException` with "empty" in message |
| `ParseTranscodeTableAsync_ExcelMissingRequiredColumn_ThrowsInvalidOperationException` | Transcode Excel missing the `Column` header | `InvalidOperationException` naming the missing column |
| `ParseAsync_FileDoesNotExist_ThrowsFileNotFoundException` | Path points to a non-existent file | `FileNotFoundException` |

---

### AnonymizationResult (6 tests)

Computed properties on the result model are tested independently to catch any regression in the statistics displayed in the UI.

| Test | Purpose |
|---|---|
| `TotalReplacements_EmptyTranscodeTable_ReturnsZero` | Zero replacements when transcode table is empty |
| `TotalReplacements_ReflectsTranscodeTableCount` | Count equals number of entries in the transcode table |
| `AffectedRows_EmptyTranscodeTable_ReturnsZero` | Zero affected rows when transcode table is empty |
| `AffectedRows_CountsDistinctRowIndexes` | Multiple entries on the same row count as one affected row |
| `AffectedColumns_EmptyTranscodeTable_ReturnsZero` | Zero affected columns when transcode table is empty |
| `AffectedColumns_CountsDistinctColumnNames` | Multiple entries in the same column count as one affected column |

---

## What is not tested and why

| Component | Reason excluded |
|---|---|
| `MainViewModel` | Tightly coupled to WPF infrastructure (`DataTable`, `ObservableCollection`, file dialogs). Automated WPF ViewModel testing requires a full UI harness, with high setup cost and low reliability. Manual testing is more practical here. |
| `MainWindow` / `ManageCategoriesDialog` | UI code. Automated WPF UI testing (e.g. FlaUI) is out of scope for this project. |
| `CategoryStorage` | Trivial JSON file read/write with no business logic. |
| `FileWriter` | Output formatting logic. Could be covered by a round-trip integration test (write → parse → compare) in the future if regressions appear. |
| `App.xaml.cs` | DI bootstrapping — no business logic. |
| Converters (`BoolToVisibility`, etc.) | One-liner transforms; risk of regression is negligible. |
