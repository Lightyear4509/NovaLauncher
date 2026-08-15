---
type: architecture
status: active
created: 2026-07-30
updated: 2026-07-30
---

# Integration Feasibility Matrix

| Integration | Best extension point | Alpha position | Authentication / constraint | Main risk |
|---|---|---|---|---|
| Discord Rich Presence | presence plugin | reference plugin | Local Discord client; explicit opt-in | privacy and SDK/API change |
| HowLongToBeat | metadata/link plugin | link-out only | No stable authorized public API confirmed | scraping/terms and breakage |
| RetroAchievements | achievements plugin | post-alpha | User API key; rate limiting and caching | secrets, matching, large datasets |
| IGDB | metadata plugin | post-alpha | Twitch OAuth client credentials; 4 requests/sec | distributing client secret and licensing |
| ProtonDB | compatibility/link plugin | experimental | No stable official public API confirmed | undocumented endpoint and schema change |
| PCGamingWiki | metadata/link/save-discovery plugin | reference plugin | 30 requests/minute, custom user agent, cache | rate limits and community schema |
| SteamGridDB | artwork plugin | reference/migration | API key and existing provider contract | quotas and attribution |
| Nexus Mods | mod plugin | post-alpha | User/app auth and request quotas | download permissions and account tiers |
| Ludusavi | save-discovery/backup adapter | cloud-save foundation | Local CLI/API and manifest | process/version compatibility |
| Playnite Import | library importer | reference plugin | Local data import; preserve provenance | schema/version mapping |
| Emulator Support | emulator-profile plugins | alpha MVP | User-owned emulator, BIOS, and ROM paths | platform variance and copyrighted content |
| Tailscale | cloud transport | experimental post-alpha | Local client/peer network; Taildrop is alpha | transport is not versioned sync storage |

## Integration policy

- Prefer documented official APIs and local supported CLIs.
- Keep provider credentials outside plugin packages and source control.
- Treat rate limits as contract inputs with caching, backoff, and visible quota
  state.
- Use stable provider IDs before fuzzy name matching.
- Require user confirmation when a match is ambiguous.
- Put undocumented or scraping-based integrations outside the first-party
  catalog unless the provider grants permission.
- Preserve provider attribution and license requirements.

## Source notes

- Discord recommends its current Rich Presence/Social SDK path; local RPC
  requires the desktop client.
- IGDB requires Twitch OAuth client credentials and documents a four
  request-per-second limit.
- RetroAchievements requires a user API key and asks clients to cache static
  data responsibly.
- PCGamingWiki documents a 30-request-per-minute limit and requires a custom
  user agent.
- Ludusavi exposes backup, restore, cloud, wrap, and JSON API CLI commands.
- Tailscale Taildrop is an encrypted peer transfer feature currently labeled
  alpha; it does not provide NovaLauncher conflict/version semantics.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
