---
name: clean-code-metrics
description: "Use when: creating or reviewing C# code for maintainability, refactoring, or clean-code compliance. Run codemetrics -s <File.cs> to collect single-file complexity metrics, then verify the implementation against clean-code rules, parameter limits, and SOLID principles before finalizing the change."
---

# Clean Code Metrics

## Goal

Create and review C# code that is readable, maintainable, testable, and easy to evolve. Treat the code metrics output as a verification tool, not just a report.

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

5. Re-run `codemetrics -s <File.cs>` after the fix to verify the metrics improved or remained acceptable.

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

## Verification checklist

Before considering a file complete, confirm all of the following:

- Method names clearly communicate intent.
- No method has more than 6 parameters.
- Any method that would otherwise take 7+ parameters uses a context object.
- No member exceeds cyclomatic complexity 7 without a justified exception.
- No member exceeds cognitive complexity 15 without a strong reason and a refactoring plan.
- Complex sections were simplified into helpers or guard clauses.
- Comments were added only where the intent is not obvious; simple methods remain comment-free.
- The design remains SOLID and testable.

## AI usage pattern

When generating or reviewing code:

- Start by running `codemetrics -s <File.cs> -f json` for the file under review.
- Pass the JSON metrics result to the AI verification step.
- Do not approve a patch based on compilation alone; the patch must also satisfy the clean-code thresholds.
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

The code is ready only when it is both correct and within the metric guardrails. If the metrics do not support the change, the file should be refactored before finalizing.
