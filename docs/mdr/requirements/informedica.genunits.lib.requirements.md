| Feature                              | Informedica.GenUnits.Lib (F#) | Pint (Python)         | SymPy Units (Python)   | UnitsNet (C#)        | FSharp.Units (F#)   |
|--------------------------------------|:-----------------------------:|:---------------------:|:----------------------:|:--------------------:|:-------------------:|
| Built-in Unit Conversion             | Yes (`convertTo`)             | Yes (`to`, `to_base_units`) | Yes (via expression)   | Yes (typed methods)  | Yes (type provider) |
| Composite/Compound Units             | Yes (arbitrary, e.g. mg/48h)  | Yes (if defined)      | Yes (symbolic)         | Partial (extendable) | Yes (compile-time)  |
| Retains Custom Time Units (e.g. 48h) | Yes (as first-class)          | Yes (with definition) | Yes (symbolic)         | No (needs extension) | Yes (define unit)   |
| Domain-specific Units (droplet, IU)  | Yes (many built-in)           | No (user-define)      | No (user-define)       | No (user-define)     | No                  |
| Exact Rational Arithmetic            | Yes (BigRational)             | No (float)            | Yes (symbolic/rational)| No (float/decimal)   | Partial             |
| Unit Synonyms/Localization           | Yes (multi-lang, synonyms)    | No                    | No                     | No                   | No                  |
| Conversion API Usability             | Direct (domain-driven)        | Direct                | Symbolic expressions   | Direct (typed)       | Compile-time only   |
| SI Prefix Support                    | Yes                           | Yes                   | Yes                    | Yes                  | Yes                 |
| Serialization Support                | Partial/Custom                | Yes                   | No                     | Yes                  | No                  |
| Test Coverage for Unit Algebra       | Yes (custom tests)            | Yes                   | Yes                    | Yes                  | Yes                 |
| Library Focus                        | Clinical, pharmacy, custom    | Science, engineering  | Math, symbolic         | Science, engineering | Science, compile-time|

**Notes:**
- Informedica.GenUnits.Lib stands out for its domain-specific units, localization, and precise algebraic handling of composite units.
- Pint is flexible, but domain features must be user-defined.
- SymPy Units is symbolic and precise but not domain-focused.
- UnitsNet and FSharp.Units are strong for SI/science but less so for clinical or custom domain units.
- For more on the F# code, see the [source on GitHub](https://github.com/halcwb/GenPres2/blob/1e0826dc2cdccac0e2d1ed14d60ad6658ffccf93/src/Informedica.GenUnits.Lib/ValueUnit.fs).