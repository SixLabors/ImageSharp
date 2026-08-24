# Six Labors AI Coding Guidelines

These instructions apply to the entire repository. More-specific `AGENTS.md` files may add to or override them for their directory tree.

## Working Practices

- Inspect the relevant implementation, tests, benchmarks, project files, and nearby code before proposing or making changes. Do not infer current behavior when the source is available.
- Make the smallest complete change that solves the requested problem. Avoid unrelated cleanup, speculative abstractions, and formatting churn.
- Match established architecture, naming, formatting, documentation, and test patterns. Treat `.editorconfig`, analyzers, and repository build settings as authoritative.
- Preserve public API and observable behavior unless the task explicitly requires a change. Public API documentation must describe observable behavior, not implementation details.
- Do not use reflection against built assemblies, ad hoc assembly loading, or temporary probe projects unless explicitly requested.
- Build .NET projects in Release configuration unless explicitly instructed otherwise.

## Performance

- Treat throughput, latency, memory use, and binary size as design constraints, especially in pixel-processing, drawing, parsing, encoding, and other hot paths.
- Avoid unnecessary allocations, copies, boxing, closures, interface dispatch, repeated enumeration, and extra passes over data.
- Reuse the repository's existing memory ownership, pooling, span, vectorization, and parallelization patterns. Do not introduce a new mechanism when an established one fits.
- Keep hot loops simple and bounds-check-friendly. Hoist invariant work, preserve locality, and use the narrowest suitable data types without sacrificing correctness.
- Do not trade correctness or maintainability for assumed speed. Support non-obvious optimizations with measurements or clear evidence, and add or update benchmarks when performance is the purpose of the change.
- Consider all supported target frameworks and runtime capabilities. Do not regress fallback paths while optimizing newer runtimes.

## C# Conventions

- Follow the existing code around the change; local patterns take precedence over generic preferences.
- Do not use `record` or `record struct` types.
- Prefer established invariants over redundant guards. Validate at real external boundaries and do not add defensive checks for internally controlled states.
- Do not extract single-use helpers merely to name a block. Extract only for genuine reuse, an established local pattern, or meaningful complexity reduction.
- Add vertical whitespace after multi-line statements and declarations and between distinct logical stages. Never add trailing whitespace.
- Document every method, constructor, and property, regardless of whether it is public, internal, protected, or private. Keep public API documentation limited to observable behavior; use private and internal documentation to capture the contract and intent needed to maintain the code.
- Add inline comments throughout complex code. Explain algorithms, formulas, invariants, ownership, compatibility behavior, and performance tradeoffs at the operations and decisions they govern. Explain why the code is shaped that way rather than narrating the syntax.
- Document SIMD code especially thoroughly. Explain the vector layout, lane meaning, widening or narrowing, masks, shuffles, constants, alignment or remainder handling, supported instruction paths, scalar equivalence, and the reason each non-obvious operation is correct.
- Write algorithm and SIMD comments for a maintainer who is unfamiliar with the implementation. The reader should not need to reconstruct intent from external documentation, issue history, or benchmark results.

## Verification

- Add or update focused tests when behavior changes, following the test framework and conventions already used by the project.
- Never hack, weaken, skip, conditionally bypass, or otherwise manipulate a test to make it pass. Fix the production defect or the genuine test defect while preserving the test's intended coverage and sensitivity.
- Do not update golden files, reference images, snapshots, baselines, or expected-output artifacts to resolve a test failure. Treat a mismatch as evidence to investigate and correct the implementation.
- Run the narrowest relevant formatting, test, and Release build commands, then expand verification in proportion to the risk and scope of the change.
- Report what changed, the verification performed, and any remaining risks or unverified assumptions.
