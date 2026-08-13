# Phase 4 media storage abstraction

## Architecture

Business modules depend on IMediaStorage and MediaService in Application. They store only MediaAsset identifiers and never deployment paths. Infrastructure owns provider details and metadata persistence. API endpoints are Administrator-only and thin.

The storage contract supports upload, read/open, and delete. MediaAsset stores a collision-safe provider key, original display name, content type, byte size, and creation timestamp.

## Development provider

FileSystemMediaStorage is available only when the host explicitly identifies Development or isolated test usage. It:

- generates GUID storage keys;
- derives extensions from an allowed content type rather than trusting the submitted extension;
- prevents directory traversal;
- keeps original names only as metadata;
- supports asynchronous streaming;
- deletes provider content when requested.

The development storage root is .coachhub-media and is ignored by Git.

## Production requirement

The committed base configuration uses Provider=External. Startup fails until an external/cloud provider is registered. This prevents assessment, nutrition, or training uploads from silently falling back to deployment-server storage.

A future provider (Azure Blob, S3-compatible storage, Google Drive, or equivalent) implements IMediaStorage without changing Domain, Application, or consuming business modules.

## Upload policy

Initial supported types are JPEG, PNG, WebP, and PDF, with a 20 MB limit. Unsupported, empty, oversized, or unreadable uploads receive application validation errors. Endpoints require the Administrator role.

Generated plan PDFs are not Media uploads and remain an on-demand stream in the later PDF phase.
