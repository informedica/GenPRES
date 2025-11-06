## Overview

Give a high-level description of the changes. You can refer back to the issue and/or plan.

## Further information

Add any additional information that you think is relevant here.

## Divergence from plan

Sometimes the planned approach isn't suitable. Significant changes should ideally be discussed with a maintainer before an implementation PR is made. (Even more ideally, the plan should be updated before an implementation PR is made.) We appreciate though that sometimes the overhead is not worth it. Either way, document any differences from the plan here.

## Verification

Guideline: in this section you're aiming to minimise the effort required, between you and the reviewer, for the reviewer to be confident that the implementation works. The effort you put into this section counts! So, if it'd be quicker for the reviewer to run changes than for you to document them here, let them do that. But equally, if you can quickly add notes that will convince a reviewer that the change works without them needing to do more than a cursory check, do that.

- Briefly describe how you checked that your changes work, including manual and automated tests, discussing edge cases you considered.
- If appropriate, add screenshots/GIFs that show the changes in the UI.
- If appropriate, add log output that demonstrates the changes.
- Briefly describe how a reviewer can see the changes for themselves when running the code locally.

## Author checklist

> [!NOTE]
> An issue and agreed implementation plan are necessary unless either fewer than 25 lines have been changed or only documentation has been changed.

I confirm that these changes:

- [ ] Follow the process described in [CONTRIBUTING.md](../CONTRIBUTING.md#Pull_Request_Process).
- [ ] Make the changes proposed in the linked issue (if applicable): <issue number, or 'n/a'>
- [ ] Follow the approach documented in the linked implementation plan (if applicable): <link, or 'n/a'>
- [ ] Are no more than 200 lines of changed code, ideally 25-100.
- [ ] Are not more complex than necessary.
- [ ] Cannot easily be split in a way that would make reviewing them significantly easier.
- [ ] Follow the guidelines specified in [DEVELOPMENT.md](../../DEVELOPMENT.md).
- [ ] Have been thoroughly tested.
- [ ] Don't introduce new security vulnerabilities.

I confirm that I have:

- [ ] Added appropriate automated tests.
- [ ] Updated documentation appropriately.
- [ ] Added comments that highlight important changes and add justification, where necessary. (Create the PR as a draft first, add your comments, then mark as ready for review.)

## Reviewer checklist

I confirm that:

- [ ] I am confident that the changes work.
- [ ] The changed code meets our guidelines and standards.
- [ ] The new code is not more complex than necessary.
- [ ] I have clearly labelled suggestions as blocking, required but not blocking, or optional (see [CONTRIBUTING.md](../CONTRIBUTING.md#Pull_Request_Process)).
