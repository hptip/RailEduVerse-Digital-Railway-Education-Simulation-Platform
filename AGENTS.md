# UTTVR infrastructure rules (merge into existing AGENTS.md, do not overwrite)
- For railway infrastructure tasks, read `Documentation/InfrastructureDesign/Design_Baseline_v1.0.md`, `Design_Configuration.json`, `Implementation_Rules.md`, `Acceptance_Criteria.csv` and `Implementation_Status.md`. Read relevant reference sheet only.
- Approved design choices are locked; measure actual geometry and record resolved parameters. Reference image numbers are illustrative, never authoritative.
- Reuse SO201 generators/validators and BIM architecture. Fix root cause, deterministic/idempotent generation, full regression before integration. Do not create placeholder BIM leaves.
- Stop at G1 for explicit user visual approval; do not self-approve or change production before approval. G2/G3 only after confirmation. Keep evidence, manifests, hashes, and status; do not re-audit unchanged inputs.
- Preserve accepted SO201, train, spline, track DB/user data, existing five-scene menu, login, XR, other scenes, source assets. No automatic git commit/push, no Library deletion, no paid assets/tools. Respect licenses; no publishing unverified source.
- Atomic replacement and cleanup of legacy instances/generators; Visual and Detailed share canonical geometry; no BIM DB in route. Build Web only after Editor gates. VR NOT_TESTED without hardware. Report actual blockers and evidence, not fabricated PASS.
