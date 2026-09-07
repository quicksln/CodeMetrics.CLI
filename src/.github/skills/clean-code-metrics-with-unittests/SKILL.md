---
name: clean-code-metrics-with-unittests
description: "Use when: creating or reviewing C# code for maintainability, refactoring, and testability. Run codemetrics -s <File.cs> to collect single-file complexity metrics, verify the implementation against clean-code rules and SOLID principles, then add or update unit tests that cover the branching paths implied by cyclomatic complexity before finalizing the change. If the project does not already contain a unit test project, ask the user to create one and confirm the test framework to use."
---

# Clean Code Metrics with Unit Tests

## Goal

Create and review C# code that is readable, maintainable, testable, and easy to evolve. Use code metrics as objective evidence and pair each logic-heavy method with unit tests that cover the meaningful branches created by its decision points.

## Required workflow

When working on a single C# file, always gather objective evidence before finalizing the implementation:

1. Run the single-file metrics command:

   ```bash
   codemetrics -s path/to/File.cs -f json
   ```

   In this repo, single-file mode writes the report to stdout and saves a matching file next to the source, such as `File.codemetrics.json`.

2. Inspect the member-level results and focus on these metrics:
   - `cyclomaticComplexity`
   - `cognitiveComplexity`
   - `parameterCount`
   - `maxNestingDepth`
   - `maintainabilityIndex`
   - `linesOfCode`

3. Compare the file against the clean-code constraints below.

4. Refactor any method that violates the gate before considering the code ready.

5. Determine whether unit tests are needed for the changed methods.

6. If a method has meaningful conditional logic, add unit tests that exercise the distinct execution paths implied by its complexity.

7. Re-run `codemetrics -s <File.cs>` after the fix and validate the tests to confirm the behavior and the metrics remain acceptable.

## Clean code rules

Apply the following rules when creating or modifying code:

- Prefer clear, descriptive, full names over abbreviations, initials, or cryptic short names.
- Keep classes and methods small and focused on one responsibility.
- Prefer early returns and small helper methods over deeply nested branching.
- Keep method signatures explicit and stable; if a method needs more than 6 arguments, wrap the values in a context object or options record.
- Keep `cyclomaticComplexity` <= 7 for normal methods.
- Keep `cognitiveComplexity` <= 15 for standard logic; prefer <= 10 for high-risk or frequently changed methods.
- Keep nesting depth low and reduce duplication through extraction.
- Follow SOLID principles:
  - SRP: one reason to change
  - OCP: extend through abstractions, not by modifying known implementations
  - LSP: honor the contract of inherited abstractions
  - ISP: keep interfaces narrow and client-specific
  - DIP: depend on abstractions, not concrete implementations
- Prefer simple, deterministic code over clever tricks or hidden behavior.
- Favor explicit validation and guard clauses over long conditional chains.
- Add comments only when the method is not obvious from its name and flow. Prefer self-explanatory code over explanatory comments.
- If a method is simple and self-explanatory, do not add comments. Do not comment obvious logic, trivial branches, or code that reads clearly without explanation.
- Use comments to explain business intent, non-obvious rules, edge cases, trade-offs, or why a particular design was chosen—not to restate the code line by line.

## Unit testing rule

After the implementation is correct and readable, add unit tests according to the method's cyclomatic complexity.

- If a method has no branches or only a trivial path, it may not require a dedicated unit test.
- If a method contains conditionals, loops, switch logic, or validation branches, add tests that cover the distinct outcomes.
- Use the cyclomatic complexity as a guide:
  - complexity 1-2: basic happy-path and edge-case validation
  - complexity 3-5: cover each branch and the guard clause
  - complexity 6-7: cover each meaningful decision path and boundary condition
  - above 7: refactor before finalizing unless there is a strong reason; tests should cover the branch matrix after refactoring
- Prefer table-driven tests or parameterized tests when many conditions are similar.
- Keep tests deterministic and fast; assert behavior, not implementation details.
- If the project has no existing unit test project, stop and ask the user to create one and confirm the unit test framework before writing tests.
- Framework choice should be determined by the project:
  - use xUnit for .NET standard projects when no framework is specified
  - use NUnit or MSTest if the repository already uses them
  - if there is no test project at all, ask the user whether they want xUnit, NUnit, or MSTest and then create the project accordingly

## Verification checklist

Before considering a file complete, confirm all of the following:

- Method names clearly communicate intent.
- No method has more than 6 parameters.
- Any method that would otherwise take 7+ parameters uses a context object.
- No member exceeds cyclomatic complexity 7 without a justified exception.
- No member exceeds cognitive complexity 15 without a strong reason and a refactoring plan.
- Complex sections were simplified into helpers or guard clauses.
- Comments were added only where the intent is not obvious; simple methods remain comment-free.
- Unit tests exist for the meaningful branch paths created by complex logic.
- The design remains SOLID and testable.

## AI usage pattern

When generating or reviewing code:

- Start by running `codemetrics -s <File.cs> -f json` for the file under review.
- Pass the JSON metrics result to the AI verification step.
- Do not approve a patch based on compilation alone; the patch must also satisfy the clean-code thresholds.
- For each method with branch-heavy logic, determine whether a unit test is required based on the decision paths.
- If the project does not already contain a test project, ask the user to create one and specify the framework first.
- If the metrics worsen, explain the reason and refactor before completion.
- If the code is too complex, prefer extracting a smaller method or a context object over adding more branches or arguments.

## Examples

Good:

```csharp
public Result CreateReport(CreateReportRequest request, ILogger logger)
{
    if (request is null)
    {
        throw new ArgumentNullException(nameof(request));
    }

    return BuildReport(request, logger);
}
```

A simple method with clear intent should not have comments:

```csharp
public bool IsReadyForProcessing(Order order)
{
    return order is not null && order.Status == OrderStatus.Pending;
}
```

Use a brief explanatory comment only when the intent is non-obvious:

```csharp
public bool ShouldRetry(Exception exception)
{
    // Retry only on transient HTTP failures; validation errors are never safe to retry.
    return exception is HttpRequestException or TimeoutException;
}
```

A branch-heavy method should have test coverage for each path:

```csharp
public bool IsEligibleForDiscount(Customer customer, bool isMember)
{
    if (customer is null)
    {
        return false;
    }

    if (isMember)
    {
        return customer.TotalSpent >= 100;
    }

    return customer.TotalSpent >= 250;
}
```

Example tests:

```csharp
[Theory]
[InlineData(null, true, false)]
[InlineData(50, true, false)]
[InlineData(100, true, true)]
[InlineData(249, false, false)]
[InlineData(250, false, true)]
public void IsEligibleForDiscount_ReturnsExpectedResult(
    decimal? totalSpent,
    bool isMember,
    bool expected)
{
    var customer = totalSpent is null ? null : new Customer(totalSpent.Value);

    var result = new DiscountService().IsEligibleForDiscount(customer, isMember);

    Assert.Equal(expected, result);
}
```

Prefer a context object when there are many values:

```csharp
public Result CreateReport(ReportCreationContext context)
{
    // context holds request, logger, tenant, correlationId, etc.
    return BuildReport(context);
}
```

Avoid:

```csharp
public Result CreateUsrRpt(req, log, ctx, x, y, z, id, status, token)
{
    // Too many parameters and unclear naming.
}
```

## Completion rule

The code is ready only when it is both correct, within the metric guardrails, and covered by tests for the meaningful branch paths created by the logic. If the project has no unit test project, stop and ask the user to create one and choose the framework before creating tests.
