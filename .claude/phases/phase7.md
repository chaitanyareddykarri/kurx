# Phase 7: Generated Assets (Cards & Certificates)

**Status: not started** (QuestPDF/ImageSharp dependencies present, unused; ImageSharp pinned to 3.1.x per D-011).

## Objective

Generate ticket cards and attendance certificates as downloadable/emailable assets.

## Deliverables

QuestPDF-based certificate templates · ImageSharp-based ticket card rendering · storage of generated assets via the existing `IStorage` abstraction · endpoint(s) to retrieve a generated asset for a ticket/attendee.

## Dependencies

Phase 5 (a ticket/check-in record must exist to generate a certificate for).

## Completion criteria

A real PDF/image asset is generated for a test ticket and retrievable via `IStorage`, with ImageSharp version pin respected.

## Verification

`.claude/checklists/backend.md`. Confirm no dependency bump violates D-011/D-013 pins.

## Loop OS integration

- **Acceptance criteria (testable):** a real PDF certificate (QuestPDF) and image ticket card (ImageSharp 3.1.x) are generated for a test ticket and retrievable via `IStorage`; ImageSharp pin (D-011) respected.
- **Exit criteria:** deliverables shipped + a generated asset verified retrievable, no pinned-dependency bump, required reviews pass, roadmap + this file updated.
- **Required reviews:** backend-review, performance-review (generation is not on a request hot path — background/on-demand with caching), api-review (asset retrieval endpoint).
- **Required documentation:** `D-NNN` if any templating/storage-layout decision is made; `docs/api/README.md` (asset endpoint); `docs/roadmap/README.md`.
