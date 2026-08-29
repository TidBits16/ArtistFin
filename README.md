<p align="center">
  <img src="logo.svg" alt="ArtistFin" width="128" height="128">
</p>

# ArtistFin: Know Your Artists

<p align="center">
  <img src="backdrop.svg" alt="ArtistFin backdrop" width="100%">
</p>

A Jellyfin plugin that fills **artist bios**, **images**, and profile details — the Fin counterpart to MusicFin’s album/track tagging.

**Jellyfin 10.11+** · scheduled task + settings button + native Identify/Refresh provider.

## How it works

1. **Scheduled task** (`ArtistFin: Know Your Artists`) — fills artists that are missing a bio and/or primary image (weekly by default).
2. **Settings → Refresh all artists** — queues a force overwrite (runs under Scheduled Tasks).
3. Lookups combine:
   - **MusicBrainz** — IDs, hometown, formed/disbanded, genres, official site
   - **TheAudioDB** — images, genre, country, formed year (bios when the free API returns them)
   - **Deezer** — high-res primary artist images
   - **Wikipedia** — biography extract (the reliable free bio source)
4. Also registers as a Jellyfin metadata + image provider so **Identify** / **Refresh** on an artist can use ArtistFin.

Skips junk names like *Various Artists*.

## Install

1. **Dashboard → Plugins → Repositories** → add:
   - Name: `FinPlugins`
   - URL: `https://raw.githubusercontent.com/TidBits16/FinPlugins/main/manifest.json`
2. **Catalog** → refresh → install **ArtistFin: Know Your Artists** → restart when asked.
3. Configure under **Plugins → ArtistFin**, or run from **Scheduled Tasks**.

## Build locally

```bash
dotnet build Jellyfin.Plugin.ArtistFin.csproj -c Release
./scripts/package.sh
```

The release zip lands in `dist/`.
