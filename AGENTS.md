# Six Labors AI Coding Guidelines

These instructions apply to the entire repository. More-specific `AGENTS.md` files may add to or override them for their directory tree.

## Working Practices

- Inspect only the source and supporting files needed for the task before proposing or making changes. Do not infer current behavior when the source is available.
- Make the smallest complete change that solves the requested problem. Avoid unrelated cleanup, speculative abstractions, and formatting churn.
- Trace the existing execution path before changing it. Extend and optimize the existing implementation within the requested behavior and affected path.
- Before implementing an operation, find and inspect the existing APIs that provide it. Use or extend those APIs instead of writing equivalent logic elsewhere. This includes color conversion, pixel conversion, memory management, and vectorized operations. If an API lacks required behavior, improve its owning implementation rather than bypassing it with a separate implementation.
- Follow existing naming, formatting, documentation, and test patterns. Treat `.editorconfig`, analyzers, and repository build settings as authoritative.
- Use simple technical English in all communication, documentation, and comments. Use short sentences, active voice, and existing code terminology.
- Preserve public API and observable behavior unless the task explicitly requires a change. Public API documentation must describe observable behavior, not implementation details.
- Do not use reflection against built assemblies, ad hoc assembly loading, or temporary probe projects unless explicitly requested.
- Build .NET projects in Release configuration unless explicitly instructed otherwise.

## Performance

- Treat throughput, latency, memory use, and binary size as design constraints, especially in pixel-processing, drawing, parsing, encoding, and other hot paths.
- Avoid unnecessary allocations, copies, boxing, closures, interface dispatch, repeated enumeration, and extra passes over data.
- Reuse existing memory ownership, pooling, span, vectorization, and parallelization mechanisms.
- Use a SIMD-first design for suitable data processing code. Build on existing vectorized operations, dispatch mechanisms, and scalar fallbacks. Keep hot loops simple, hoist invariant work, and preserve memory locality.
- Preserve correctness and maintainability during optimization. Distinguish source-based reasoning from measured results. Claim a speedup only with measurements. Use existing benchmarks first. Add or update cases only when they measure relevant behavior that existing benchmarks miss.
- Consider all supported target frameworks and runtime capabilities. Do not regress fallback paths while optimizing newer runtimes.

## C# Conventions

- Follow local patterns when they comply with the explicit rules.
- Do not use `record` or `record struct` types.
- Prefer established invariants over redundant guards. Validate at real external boundaries and do not add defensive checks for internally controlled states.
- Do not extract single-use helpers merely to name a block. Extract only for genuine reuse, an established local pattern, or independently complex logic.
- Add vertical whitespace after multi-line statements and declarations and between distinct logical stages. Never add trailing whitespace.
- Document every added method, constructor, and property, regardless of visibility. Update documentation when a member's behavior or contract changes. Keep public API documentation limited to observable behavior. Use private and internal documentation to explain the contract and intent.
- Add inline comments that explain why complex code works. Explain algorithms, formulas, invariants, memory ownership, compatibility behavior, and performance decisions where they apply.
- For SIMD, explain lane layouts, operations, alignment, remainders, and scalar equivalence so an unfamiliar maintainer can follow the code.

## Verification

- Add or update tests only when they prove a required behavior or expose a real defect. Derive expectations from established contracts, explicit requirements, or defect evidence. Use source to identify contracts and integration points, not to copy implementation results into expectations. Prefer extending existing tests. Avoid redundant cases and assertions that merely repeat the implementation.
- Never weaken, skip, or bypass a valid test to make it pass. Fix the production defect or the genuine test defect. Replace or remove a test only with evidence that its expectation is invalid or its coverage is redundant.
- Do not update golden files, reference images, snapshots, baselines, or expected-output artifacts merely to silence a failure. Investigate mismatches. Update expected results only for an established requirement change or an incorrect reference result proved by independent contract or defect evidence.
- Test the actual behavior through the existing execution path. Synthetic inputs must exercise an established requirement or reproduce a real defect. Do not invent requirements or substitute internal structure and helper calls for assertions about required behavior. Run the narrowest relevant formatting, test, and Release build commands.
- Report what changed, the verification performed, and any remaining risks or unverified assumptions.
