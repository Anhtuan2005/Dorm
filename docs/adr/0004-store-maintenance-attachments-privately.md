# ADR 0004: Store maintenance evidence outside the public web root

Status: Accepted

Date: 2026-09-11

## Context

Students may attach room-condition photos to a repair request. Serving these files as static assets would bypass application authorization, while trusting extensions or browser MIME values would allow invalid content into the evidence store.

## Decision

Store maintenance attachments under `App_Data/maintenance-attachments` with random server-generated names and database metadata. Validate count, size and JPG/PNG/WebP file signatures on the server. Serve files through an authenticated controller: managers and admins may read all evidence; students may read only evidence belonging to their own requests.

The runtime storage directory is ignored by Git. Deleting a database fixture also removes its physical test file.

## Consequences

- Attachment URLs remain authorization-aware and are not guessable static-file paths.
- Backup and deployment procedures must include the private evidence directory or replace it with managed object storage later.
- Upload validation and access-control paths require end-to-end regression coverage.
