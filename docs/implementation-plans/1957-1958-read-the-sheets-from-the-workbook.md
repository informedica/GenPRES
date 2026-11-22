# Implementation plan for issues #1957 and #1958: read the sheets from one xlsx download of the workbook

2026-11-22. Line numbers are from `master` at `71da9e35`.

## Problem description

`Web.GoogleSheets.createUrl` (`src/Informedica.Utils.Lib/Web.fs:18`) downloads each sheet as CSV through the gviz
endpoint. The gviz endpoint has two problems:

- It guesses how many header rows a sheet has. While the numeric columns of the first rows are empty, it takes those
  rows as extra header rows: it merges their values into the column names and leaves the rows out of the data. The
  Formulary works only because its second row is a dummy row with a 1 in every numeric column (#1957).
- It gives each column one type, taken from the majority of its cells. A cell of the other type comes out empty. A
  number stored as text in a numeric column is lost without an error. On 2026-11-22 that is 38 MinAge and MaxAge
  cells in SolutionRules and 6 cells of the macrogol dose rules for neonates in DoseRules (#1958).

## Approaches considered

1. Add `headers=1` to the gviz url. This fixes the header guess, but not the lost numbers.
2. Keep gviz, also download the CSV export of each sheet and warn about each lost number. The CSV export addresses a
   sheet by its id, which is found only by reading an undocumented page, and it ignores sheet filters, so the rows of
   a filtered sheet cannot be compared.
3. Download the whole spreadsheet once as an xlsx workbook (`export?format=xlsx`) and read every sheet from it. The
   workbook has no header guess, keeps each cell with its own type, and marks the rows a filter hides.

## Chosen approach

Approach 3, with the decisions made when the issues were picked up:

- **The rows stay as the sheet readers get them now.** Rows hidden by a filter are left out, because the filters in
  the spreadsheet are deliberate. Empty rows are left out, cells are trimmed and every double quote is removed, as
  the CSV parser did. A boolean is written as TRUE or FALSE, and an error cell, such as #N/A, is empty.
- **A number is written as a plain decimal with at most 15 significant digits**, the precision of a spreadsheet
  number: 30.0 gives 30, 2.4E7 gives 24000000, 9.129E-5 gives 0.00009129, and the floating point result
  0.16699999999999998 gives 0.167. The gviz download wrote the displayed value with 10 significant digits, so 11
  cells now have more digits: Formulary row 7571 and DoseRules rows 5021 and 5022.
- **One download serves a whole load.** The workbook is kept per spreadsheet id until the cache is cleared; callers
  that ask at the same time share one download, and a failed download is not kept. The resource provider clears the
  cache before every load, also the load after the time to live, so a reload sees the edits.
- **The sheet readers do not change.** `getCsvDataFromSheet` and `getCsvDataFromSheetSync` keep their names and
  signatures.
- **The dummy row goes.** The code that drops the Formulary row with GPKODE 0 is deleted. The maintainer deletes the
  dummy row from the sheet after the release.

The script `src/Informedica.Utils.Lib/Scripts/Issue1957SheetWorkbook.fsx` prototypes both steps. With `--live` it
compares the new read with the gviz download for the 21 sheets the code reads: the only differences are the 44
recovered cells and the 11 cells with more digits.

## Confidence

High. The live comparison covers every sheet the code reads, the filtered RenalRules sheet included.

## Steps

### Step 1: read an xlsx workbook (Utils)

- New file `src/Informedica.Utils.Lib/Xlsx.fs`, before `Web.fs`: the module `Xlsx` with `read`, which reads the rows
  of every sheet of the workbook bytes by name. It follows the package format: the workbook is the
  office document that the package relationships in `_rels/.rels` name, and the shared strings and the sheets are
  found through the relationships of the workbook. It uses only System.IO.Compression and System.Xml.Linq.
- The tests move to `tests/Informedica.Utils.Tests/Tests.fs`, with the workbook built in memory.
- Nothing calls the module yet.

### Step 2: read the sheets from the workbook (Utils, GenFORM)

- `Web.GoogleSheets` gets `createWorkbookUrl`, `downloadWorkbook`, the workbook cache with `clearWorkbookCache` and
  `getWorkbookWith`, and `sheetRows`. `downloadResult` returns the downloaded bytes instead of text.
  `getCsvDataFromSheet` reads the sheet from the workbook. `createUrl` and `getDataFromSheet` are deleted.
- `CachedResourceProvider.loadFresh` (`src/Informedica.GenFORM.Lib/Resources.fs:858`) calls
  `Web.GoogleSheets.clearWorkbookCache ()` before it loads. The dependency rule check allows `Web.` in
  `Resources.fs`, as it does for the other sheet loaders in GenFORM.
- The filter of the dummy row (`src/Informedica.GenFORM.Lib/Product.fs:117`), its comment on `GPKODE`
  (`src/Informedica.GenFORM.Lib/Types.fs:228`) and its test (`tests/Informedica.GenFORM.Tests/Tests.fs:1423`) are
  deleted.
- The cache tests move to `tests/Informedica.Utils.Tests/Tests.fs`; the script is deleted.
- Closes #1957 and #1958.
