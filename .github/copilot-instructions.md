# Copilot Instructions

## 项目指南
- User prefers the layering principle that Application is responsible for orchestration and Domain is responsible for business rules.
- User prefers periodic metrics logs formatted one stage per line in the order: queue remaining - in progress - completed.

## Performance Optimization
- In the pipeline, when `OTARoomInfo.Extension` has already been loaded on the work item, avoid re-reading `OTARoomInfoExtension` from the database in `SaveParsedHotelBatchTask`; reuse the already loaded extension data instead.