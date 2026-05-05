# Watchlist Requests Sync

Jellyfin server plugin for Jellyfin `10.11.x` that syncs each user's Seerr or Jellyseerr requests into that same user's KefinTweaks watchlist using additive Jellyfin `Likes`.

## How it works

KefinTweaks documents its watchlist as being backed by Jellyfin item `Likes`, queried via `Filters=Likes`. This plugin integrates with that same storage path instead of inventing a separate Jellyfin watchlist.

- The plugin only ever sets `Likes=true` for matched items.
- The plugin never clears, replaces, removes, or overwrites a watchlist.
- The plugin stores its own sync metadata separately in `WatchlistRequestsSync.state.json`.

## Features

- Per-user Seerr/Jellyseerr to Jellyfin user mapping
- Additive-only watchlist sync for movies and series
- Optional per-user Jellyfin tag inclusion
- Dry-run preview mode
- Manual sync, preview, and connection test actions in the admin dashboard
- Scheduled sync task
- GitHub Releases and Jellyfin plugin repository manifest support

## Install from a Jellyfin repository

After the release workflow has published a stable release and generated the manifest on `gh-pages`, add this repository URL in Jellyfin:

`https://nolanbecker.github.io/WatchlistRequestsSync/manifest.json`

Install steps:

1. Open Jellyfin Dashboard.
2. Go to `Plugins` > `Repositories`.
3. Add a new repository with the manifest URL above.
4. Save, then open the `Catalog` tab.
5. Find `Watchlist Requests Sync` and install it.
6. Restart Jellyfin.

## Manual install fallback

1. Download the plugin zip from GitHub Releases.
2. Extract the zip into a dedicated plugin folder under Jellyfin's plugins directory.
3. Restart Jellyfin.

The plugin folder is typically:

- Windows direct install: `%UserProfile%\AppData\Local\jellyfin\plugins`
- Windows tray install: `%ProgramData%\Jellyfin\Server\plugins`
- Linux: `/var/lib/jellyfin/plugins`

## Configuration

1. Open Jellyfin Dashboard > Plugins > Watchlist Requests Sync.
2. Configure the Seerr/Jellyseerr base URL and API key.
3. Enable sync per Jellyfin user and set each user's Seerr/Jellyseerr user id mapping if Jellyfin user id auto-match is not available from Seerr.
4. Optionally configure a per-user media tag for additive tag-based inclusion.
5. Use `Test Connection`, then `Preview Sync`, then `Run Sync Now`.

## Matching rules

- Movies: TMDb first
- Series: TVDb first, then TMDb, then IMDb
- Fallback: exact title + year
- Ambiguous fallback matches are skipped

## Release and packaging

This repo uses `build.yaml` as the release metadata source for:

- plugin version
- Jellyfin target ABI
- repository manifest metadata
- packaged release artifact list

Local packaging:

```powershell
./scripts/Package-Plugin.ps1
```

This produces:

- a release zip in `artifacts/package`
- an `.md5` checksum file
- package metadata for CI validation

Manifest generation:

```powershell
./scripts/Generate-Manifest.ps1 -Owner "NolanBecker" -Repository "WatchlistRequestsSync"
```

## Maintainer release flow

1. Update `build.yaml` with the next plugin `version`, `targetAbi`, and changelog seed.
2. Commit and push the release commit.
3. Create a Git tag and GitHub Release named `v<version>`.
4. Publish the release.
5. GitHub Actions will:
   - build and test the plugin
   - package the release zip
   - upload the zip and checksum to the GitHub Release
   - generate `manifest.json`
   - publish the manifest to `gh-pages`

Important release rules:

- The GitHub Release tag must exactly match `v<build.yaml version>`.
- The Jellyfin package reference version and `targetAbi` should match the Jellyfin server line you are releasing for. For the current release track, this plugin targets the 10.11.0 ABI baseline.

## Safety notes

- No delete operations are implemented.
- No watchlist rebuild path exists.
- If KefinTweaks cannot be detected, the plugin surfaces a compatibility error and skips writes.
- Seerr/Jellyseerr failures are surfaced as non-destructive sync errors.

## Notes

- Season-specific Seerr TV requests add the parent series only in v1.
- Partially available requests follow the configured partial-availability mode.
