// Runs the Fable-compiled Csv.tryParseFloat against the answers .NET gives (#1176).
//
// TryParseFloat.fsx writes the expected answers to TryParseFloat.expected.json. This script
// imports the compiled JavaScript and compares every answer, the value included.
//
// Compile Shared to JavaScript, then run the check on its Utils:
//
//   dotnet fsi TryParseFloat.fsx
//   dotnet fable ../Informedica.GenPRES.Shared.fsproj -o <dir>
//   node TryParseFloat.check.mjs <dir>/Utils.js
//
// Exits with 1 when an answer differs.

import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const target = process.argv[2];

if (!target) {
    console.error("usage: node TryParseFloat.check.mjs <compiled .js file>");
    process.exit(2);
}

const compiled = await import(pathToFileURL(resolve(target)).href);
// Fable exports a function of a module under its name prefixed with the module's
const tryParseFloat = compiled.Csv_tryParseFloat;

if (typeof tryParseFloat !== "function") {
    console.error(`no tryParseFloat in ${target}`);
    process.exit(2);
}

const expected = JSON.parse(readFileSync(join(here, "TryParseFloat.expected.json"), "utf8"));

const differ = expected.filter(([input, ok, value]) => {
    const [actualOk, actual] = tryParseFloat(input);
    return actualOk !== ok || (ok && actual !== value);
});

for (const [input, ok, value] of differ.slice(0, 20)) {
    console.log(`${JSON.stringify(input)}: expected ${ok} ${value}, got ${JSON.stringify(tryParseFloat(input))}`);
}

console.log(`${expected.length - differ.length} of ${expected.length} answers as .NET gives them`);
process.exit(differ.length === 0 ? 0 : 1);
