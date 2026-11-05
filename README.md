# Path Filter for Jellyfin 12

Hide folders inside a library from chosen users. Each show (or movie) is all-or-nothing.

```
/media/tv/shows1/...   default Visible
/media/tv/shows2/...   default Hidden   → bob: override Visible
```

## How it works

1. Every Series, Season, Episode and Movie under a configured folder gets a tag like `pathfilter-1a2b3c4d`.
2. Each user's **Blocked Tags** list gets the tags for the folders hidden from them.
3. Jellyfin enforces blocked tags itself: browsing, search, Next Up, Latest, Continue Watching and direct item links, in every client.

The plugin only ever adds or removes tags starting with `pathfilter-`. Your own tags and blocked tags are left alone.

## Rules

- **Folder default** (Visible/Hidden) applies to every user with no override, including users created later.
- **Override** (Visible/Hidden on a user) always wins and survives default changes until you set the user back to *Default*.
- **Administrators** are never restricted unless you untick "Never restrict administrators".
- Folders not listed are visible to everyone.

## When things sync

| Event | What happens |
|---|---|
| Save settings | Full re-tag + every user re-synced |
| New/updated item (scan, metadata refresh) | That item re-tagged (so a "replace all metadata" refresh can't strip it) |
| User created | Defaults applied immediately, and again 10 s later (the dashboard's new-user form saves its own policy) |
| User edited | That user's hidden folders re-applied |
| Startup + every 12 h | Full sync (Dashboard → Scheduled Tasks → Path Filter) |

## Build

Needs the .NET 10 SDK.

```
dotnet publish Jellyfin.Plugin.PathFilter/Jellyfin.Plugin.PathFilter.csproj -c Release -o out
```

Or push this folder to a GitHub repo: `.github/workflows/build.yml` builds it and uploads a ready-to-install `PathFilter_1.0.0.0` folder as an artifact.

## Install

1. Copy `Jellyfin.Plugin.PathFilter.dll` and `meta.json` into `<jellyfin config>/plugins/PathFilter_1.0.0.0/`
   (Docker: usually `/config/plugins/PathFilter_1.0.0.0/`).
2. Restart Jellyfin.
3. Dashboard → Plugins → Path Filter → add folders, set defaults and overrides, Save.

Paths must be written exactly as Jellyfin sees them (inside the container if you use Docker). The path box suggests your library roots.

## Testing checklist

- Log in as a restricted user: the hidden shows shouldn't appear in the library, search, Next Up or Latest.
- Open a direct link to a hidden episode as that user: it should fail rather than play.
- Run "Replace all metadata" on a hidden show, then check it stays hidden.

## Notes

- If you have NFO saving turned on, the `pathfilter-` tags will be written into `.nfo` files. Harmless, but visible.
- The tags show in a user's Parental Control → Blocked Tags in the dashboard. If you delete one there, the plugin puts it back.
- Removing a folder from the plugin removes its tags from items and users on save.
