# GenPRES review

## Preamble

It's clear that this application could be very useful in clinical practice:

- Being able to narrow constraints to either be shown that something is not practical (giving precise fractions of a tablet to a child) or to be shown a limited list of viable options enables coming to decisions quickly and easily.
- Being able to make selections in ways that are most useful to physicians (for example quantity per kg per day) and have the system determine prescribed dosage and administration reduces their mental load helping them to focus on other important things and to conserve energy for better performance.
- I can also see how automatic calculation of relevant information from the treatment plan, such as adjusted energy and nutrition very helpful.
- Having the formulary available for reference is really helpful, particularly where it links to specific pages of the formulary's website.

## UX review

- There are some cases where only icons, only labels, or both are clickable to trigger an action. For example, language selection is done via icon, page selection in the sidebar is done via label, buttons afford clicking either. Consistency would be good. We recommend making both clickable to maximise the target area for users.
- Clearer indication of the current page that a user is on could be valuable. Although it is shown in the nav bar, the patient data box staying in place makes the difference between the pages somewhat subtle. An on-page heading underneath the patient data box could be a simple solution.
- The patient data box collapsing when a selection was made struck us as odd. What if the clinician wants to enter multiple details in one go, such as age, sex, height and weight? Of course, we are not experts in clinical or emergency settings, so it may well be that the existing behaviour may be preferable in some contexts.
- Calculation happened at seemingly surprising times. For example, when clearing all text from any of the "Prescribe" inputs, a loading spinner was shown for a few seconds.
- Sometimes pressing the cross to clear inputs resulted in calculation followed by the old input being reinstated. For example, selecting once the medication ibuprofen is selected when prescribing for a 14-year old patient, clearing the input results in it being reinstated again. The only recourse was to delete the entry. While we can make sense of this behaviour because of the mutual constraints between medications and indications, it is not intuitive and is likely to confuse a significant proportion of users.
- Another behaviour that was confusing was that the input for selecting form for a medication scenario disappeared after a selection had been made.

## Code review

There's lots to like!

- A working, useful application!
- Generally the code is of high quality
- Effective use of F# features, such as units of measure.
- Good documentation including notebooks.
- Good use of custom operators, for example `ml50 >? l5`.
- Support for adding, subtracting, multiplying and dividing `valueUnit`s is great.

That said, I found a few areas that could be improved in the Informedica.GenUnits.Lib library.

### Incorrect results

- It's great to have a `simplify` function, but I seemed to get incorrect results in some cases; is this something that you're aware of?

### Non-idomatic code

- Recursive namespaces; generally speaking, the F# community likes the linear order imposed by the compiler and it's best to use it unless absolutely won't work (very rare).
- Unnecessary use of `and` for type declarations when the types are not mutually recursive.

### Unclear intentions

- The purpose of some of the core functions like `toUnit` and `toBase`. For example, in the following code snippet we get back `ValueUnit`s that represent different quantities to the input in a way that isn't obviously useful. To be clear, I can see that calculating that 5 ml is 1/200 l would be helpful, but converting 5 ml to 1/200 ml doesn't seem useful.
  ```fsharp
  let ml5 = 5m |> withUnit Units.Volume.milliLiter
  ml5 |> toBase // => ValueUnit ([|1/200N|], Volume (MilliLiter 1N))
  ml5 |> toUnit // => ValueUnit ([|5000N|], Volume (MilliLiter 1N))
- It's hard to quickly understand the intended behaviour of all of the `valueToBase`, `toBaseValue`, `valueToUnit`, `toUnitValue`, etc. functions 
  ```

### Overcomplicated implementation?

- Comparator implementations.
- It's hard to quickly understand the implementation of the `numDenom` functions

### Suggestions

- Consider extracting parsing code into a separate file, to keep the main file more focused on logic.
- Likewise, the big list of unit details is implementation that might be better extracted to elsewhere.
- Remove the boolean parameter for the `calc` function, and allow callers to call simplify directly afterwards if that's what they want to do.
- I'm not sure how useful functions like `UnitDetails.create` are and I'd remove it; it's already easy to just create the record directly whre required. Where constructor functions have more value is places like `createGeneral`, which provides some validation.
- Functions like `let getDutchName = getName >> getDutch` are not that useful because they provide almost no meaningful reduction in complexity at the callsite. I think that it would be better to simply use the composed function at the callsite instead.
- Likewise for examples like `let toStringDutchLong = toString Dutch Long`
- There were a few places where it looked like the choice of types could be suboptimal for performance. If there are any performance issues with the application, further investigation might be valuable.
- Consider matching tuples to avoid nesting, if preferred. For example, 

  ```diff
  -                match u |> tryFind with
  -                | Some udt ->
  -                    match loc with
  -                    | English ->
  -                        match verb with
  -                        | Short ->
  -                            udt.Group
  -                            |> gtost (if n > 1N then udt.Abbreviation.EngPlural else udt.Abbreviation.Eng)
  -                        | Long ->
  -                            udt.Group
  -                            |> gtost (if n > 1N then udt.Name.EngPlural else udt.Name.Eng)
  -                    | Dutch ->
  -                        match verb with
  -                        | Short ->
  -                            udt.Group
  -                            |> gtost (if n > 1N then udt.Abbreviation.DutchPlural else udt.Abbreviation.Dut)
  -                        | Long ->
  -                            udt.Group
  -                            |> gtost (if n > 1N then udt.Name.DutchPlural else udt.Name.Dut)
  -                | None -> ""
  +                match u |> tryFind, loc, verb with
  +                | Some udt, English, Short ->
  +                    udt.Group
  +                    |> gtost (if n > 1N then udt.Abbreviation.EngPlural else udt.Abbreviation.Eng)
  +                | Some udt, English, Long ->
  +                    udt.Group
  +                    |> gtost (if n > 1N then udt.Name.EngPlural else udt.Name.Eng)
  +                | Some udt, Dutch, Short ->
  +                    udt.Group
  +                    |> gtost (if n > 1N then udt.Abbreviation.DutchPlural else udt.Abbreviation.Dut)
  +                | Some udt, Dutch, Long ->
  +                    udt.Group
  +                    |> gtost (if n > 1N then udt.Name.DutchPlural else udt.Name.Dut)
  +                | None, _, _ -> ""
  ```

### Questions

- Would `Quantity` be a better type name than `ValueUnit`? I found it difficult to talk about values and units in sentences that also referred to value units.
- Is there an existing .NET library that could provide support for working with quantities that have units. For example, would [UnitsNet](https://github.com/angularsen/UnitsNet) be suitable?
- Might it be worth considering other design options for quantities? The CombiUnit in particular adds complexity. One alternative approach could be that, rather than the unit type including a numeric value as data, perhaps there could be primitive single-dimensional units (mg, kg, ml, day, etc.), and units could be represented as a multiple of primitive units divided by another multiple of primitive units. Quantities would then be a combination of numbers and units. This could simplify a lot of functions doing cacluations with units, and perhaps remove the need for some altogether.
  - Of course, actually making such a change would be a large undertaking; so large as to probably be prohibitively difficult to do in one go. Instead, it would probably be more feasible to take an approach where both representations live side-by-side, with conversion functions between the representations and the code migrated piecemeal.
- I like the `==>` operator, but might it make more sense to have everything stored in SI units? Then rather than having different units within the same group, you could just have different constructors (e.g. 5 ml is stored as 0.005 l). I suppose that knowing what the original unit was might be useful for decisions like the units to show back to the end user after calculations, but perhaps that could be captured separately.
- Might it be worth considering using the error case of the result type rather than exceptions in some cases, e.g. adding quantities with different units.

