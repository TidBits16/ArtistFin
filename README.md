<div align="center">

<p align="center">
  <img src="logo.svg" alt="ArtistFin" width="128" height="128">
</p>

# ArtistFin: Know Your Artists

<p align="center">
  <img src="backdrop.svg" alt="ArtistFin backdrop" width="100%">
</p>

A Jellyfin plugin that fills <strong>artist bios</strong>, <strong>images</strong>, and profile details - the Fin counterpart to MusicFin’s album/track tagging.

<strong>Jellyfin 10.11+</strong> · scheduled task + settings button + native Identify/Refresh provider.

## How it works

<strong>Scheduled task</strong> (`ArtistFin: Know Your Artists`) - fills artists that are missing a bio and/or primary image (weekly by default).
<strong>Settings --> Refresh all artists</strong> - queues a force overwrite (runs under Scheduled Tasks).
Lookups combine:
   <strong>MusicBrainz</strong> - IDs, hometown, formed/disbanded, genres, official site
   <strong>TheAudioDB</strong> - images, genre, country, formed year (bios when the free API returns them)
   <strong>Deezer</strong> - high-res primary artist images
   <strong>Wikipedia</strong> - biography extract (the reliable free bio source)
Also registers as a Jellyfin metadata + image provider so <strong>Identify</strong> / <strong>Refresh</strong> on an artist can use ArtistFin.

Skips junk names like <em>Various Artists</em>.

## Installing
<strong>Step 1</strong>
<p align="center">
  <img src="repo_graphics/plugins.jpg" alt="Plugins Location" width="100%">
</p>

<strong>Dashboard --> Plugins --> Manage Repositories</strong> --> <strong>+ New Repository</strong>:<br>
Name: <code>FinPlugins</code> (or whatever :P )<br>
URL: <code>https://raw.githubusercontent.com/TidBits16/FinPlugins/main/manifest.json</code><br>
<br>
(p.s. this bundle includes my other FinPlugins since they are designed to work together. <strong><em>they are not required to install!</em></strong>)
<br>
<br>
<strong>Then Restart JellyFin!</strong>

<strong>Step 2</strong>
<p align="center">
  <img src="repo_graphics/where_to_find.jpg" alt="Where To Find Repo" width="100%">
</p>

<strong>Plugins</strong> --> <strong>All</strong> --> <strong>ArtistFin: Know Your Artists</strong> --> <strong>Install</strong><br>
<br>
<strong>Once Installed, Restart JellyFin Again!</strong></center>

## Build Locally

For development or packaging your own build:

```bash
dotnet build Jellyfin.Plugin.ArtistFin.csproj -c Release
./scripts/package.sh
```

The release zip will be in `dist/`.

Designed for <strong>Jellyfin 10.11+</strong> (you probably have this already :D)
<p align="center">
  <a href="https://github.com/TidBits16/MusicFin"><img src="repo_graphics/musicfin.svg" alt="MusicFin" width="72" height="72"></a>
  &nbsp;
  <a href="https://github.com/TidBits16/ExplicitFin"><img src="repo_graphics/explicitfin.svg" alt="ExplicitFin" width="72" height="72"></a>
  &nbsp;
  <a href="https://github.com/TidBits16/LyricFin"><img src="repo_graphics/lyricfin.svg" alt="LyricFin" width="72" height="72"></a>
  &nbsp;
  <a href="https://github.com/TidBits16/ArtistFin"><img src="repo_graphics/artistfin.svg" alt="ArtistFin" width="72" height="72"></a>
</p>
</div>
