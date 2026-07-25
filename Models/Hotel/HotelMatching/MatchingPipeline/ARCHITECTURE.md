# Hotel Matching Pipeline Layout

Code is grouped first by layer and then by feature ownership.

```text
HotelMatchingPipeline.App/
  Cleaning/       Cleaning orchestration, metrics, options, and contracts
  Matching/       Matching orchestration, review models, options, and contracts
  ServiceCollectionExtensions.cs

HotelMatchingPipeline.Domain/
  Cleaning/       Normalization, boundary results, quality rules, and raw data
  Matching/       Candidate evaluation and hotel-name matching rules
  Shared/         Domain models required by both features

HotelMatchingPipeline.Infra/
  Cleaning/       Geo-boundary loading and cleaning-only persistence helpers
  Matching/       Third-party match delivery
  Shared/         Database implementations used by both features
  Testing/        Shared test doubles

HotelMatchingPipeline.Api/
  Controllers/Matching/  Matching analysis and manual-review endpoints

HotelMatchingPipeline.Cli/
  HotelMatchingPipelineWorker.cs  Runs run-all, clean-once, or match-loop
```

## Ownership Rules

- Put ID loading, detail loading, normalization, quality checks, boundary resolution,
  cleaning queues, and cleaning metrics under `Cleaning`.
- Put candidate claiming, spatial recall, scoring, decisions, delivery, and manual
  review under `Matching`.
- Put a type under `Shared` only when both features directly depend on it.
- Keep startup, dependency injection, and command selection at the layer root.
- Do not add matching behavior to `HotelCleaningService` or cleaning behavior to
  `HotelMatchingService`.

Namespaces remain unchanged during this directory-only reorganization so existing
callers and serialized type identities are not affected.
