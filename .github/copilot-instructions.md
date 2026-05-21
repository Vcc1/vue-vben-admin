# Copilot Instructions

## General Guidelines
- Continue executing remaining planned steps automatically without requiring the user to prompt for each next step.

## 项目指南
- User prefers the layering principle that Application is responsible for orchestration and Domain is responsible for business rules.
- Split each processing step into its own class.
- Create a new English-named project under 2.Application/SupRoomInfo for SupRoomInfo ingestion.

## Performance Optimization
- In the pipeline, when `OTARoomInfo.Extension` has already been loaded on the work item, avoid re-reading `OTARoomInfoExtension` from the database in `SaveParsedHotelBatchTask`; reuse the already loaded extension data instead.
- Log periodic metrics, one stage per line in the order: queue remaining - in progress - completed. Include pending List count, pending Set count, and counts for all other pipeline steps.
- Filter processed (already-handled) items during the read stage; do not enqueue or pass them further into the pipeline.
- Do not propagate deduplication metadata into downstream write or persistence stages. Keep deduplication decisions and metadata ephemeral within the read/filtering context (or an in-memory/ephemeral work context) and avoid storing or passing those flags on work items consumed by write tasks.