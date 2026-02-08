---
model: haiku
---

Run all TraitSharp tests (unit + integration) and summarize results.

Execute these commands in order:

```bash
dotnet test TraitSharp.sln --verbosity minimal 2>&1
```

```bash
dotnet run --project samples/TraitExample/TraitExample.csproj 2>&1
```

After both commands complete, summarize results in this exact table format:

| Test Suite | Result | Details |
|---|---|---|
| Unit Tests | ✅ PASS / ❌ FAIL | X passed, Y failed |
| Integration Tests | ✅ PASS / ❌ FAIL | X passed, Y failed |

If any test suite fails, indicate which tests failed below the table.
