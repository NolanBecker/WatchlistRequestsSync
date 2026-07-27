# Watchlist Requests Sync

Jellyfin server plugin for Jellyfin `10.11.x` that finds Sonarr series and Radarr movies with configured tags, then adds the matching library items to each enabled Jellyfin user's KefinTweaks watchlist using additive Jellyfin `Likes`.

## How it works

KefinTweaks documents its watchlist as being backed by Jellyfin item `Likes`, queried via `Filters=Likes`. This plugin integrates with that same storage path instead of inventing a separate Jellyfin watchlist.

- The plugin only ever sets `Likes=true` for matched items.
- The plugin never clears, replaces, removes, or overwrites a watchlist.
- The plugin stores its own sync metadata separately in `WatchlistRequestsSync.state.json`.

## Features

- Sonarr tag sync for series
- Radarr tag sync for movies
- Per-user Jellyfin enablement and movie/series inclusion toggles
- Dry-run preview mode
- Manual sync, preview, and connection test actions in the admin dashboard
- Scheduled sync task
- GitHub Releases and Jellyfin plugin repository manifest support

## Configuration

1. Open Jellyfin Dashboard > Plugins > Watchlist Requests Sync.
2. Configure Sonarr and/or Radarr base URLs and API keys.
3. Configure the Sonarr tags and Radarr tags to include. Use comma-separated labels or numeric tag ids.
4. Enable sync for the Jellyfin user accounts that should receive the matching watchlist items.
5. Use `Test Connections`, then `Preview Sync`, then `Run Sync Now`.

If both Sonarr and Radarr are configured, the plugin merges both sources before writing to each enabled Jellyfin watchlist.

## Matching rules

- Movies: TMDb first, then IMDb, then exact title + year
- Series: TVDb first, then TMDb, then IMDb, then exact title + year
- Ambiguous fallback matches are skipped

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

Manifest generation:

```powershell
./scripts/Generate-Manifest.ps1 -Owner "NolanBecker" -Repository "WatchlistRequestsSync"
```

## Safety notes

- No delete operations are implemented.
- No watchlist rebuild path exists.
- If KefinTweaks cannot be positively detected from plugin metadata, the plugin surfaces a warning and continues because the integration uses Jellyfin `Likes`.
- Sonarr or Radarr fetch failures are surfaced as non-destructive sync errors.

## Verification

- `dotnet build` succeeds through the solution build.
- `dotnet test WatchlistRequestsSync.sln` passes after installing a local `.NET 9.0.18` ASP.NET runtime under `.dotnet/runtime9` for test execution on this machine.
