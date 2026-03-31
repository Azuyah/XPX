# Command Reference

XPX exposes chat commands and matching CounterStrikeSharp console commands.

In general:

- chat: `!level`
- console: `css_level`

## Player Commands

| Chat | Console | Purpose |
| --- | --- | --- |
| `!me` | `css_me` | Opens the main player hub |
| `!help` | `css_help` | Opens the help menu |
| `!commands` | `css_commands` | Opens the command reference |
| `!level` | `css_level` | Shows your level overview |
| `!rank` | `css_rank` | Shows your current rank |
| `!top` | `css_top` | Shows the top players |
| `!stats` | `css_stats` | Opens your stats page |
| `!missions` | `css_missions` | Opens daily and weekly missions |
| `!achievements` | `css_achievements` | Opens achievements |
| `!shop` | `css_shop` | Opens the shop |
| `!wallet` | `css_wallet` | Shows Credits, tokens, and totals |
| `!inventory` | `css_inventory` | Shows active boosts and crate inventory state |
| `!inv` | `css_inv` | Alias for inventory |
| `!gamble 50` | `css_gamble 50` | Gambles XP for a chance to gain or lose it |
| `!rtv` | `css_rtv` | Starts or joins RTV |
| `!vote` | `css_vote` | Re-opens an active vote menu |
| `!bindmenu` | `css_bindmenu` | Attempts to bind `1-9` to `slotX;css_X` |

## Admin Commands

| Console | Purpose |
| --- | --- |
| `css_admin` | Opens the XPX admin menu |
| `css_givexp [target] [amount]` | Gives XP |
| `css_removexp [target] [amount]` | Removes XP |
| `css_givecredits [target] [amount]` | Gives Credits |
| `css_removecredits [target] [amount]` | Removes Credits |
| `css_changemap [map]` | Changes the map |
| `css_restartmap` | Restarts the current map |
| `css_setmode [alias]` | Switches the current game mode |
| `css_kick [target]` | Kicks a player |
| `css_kickbots` | Removes all bots and sets quota to `0` |
| `css_addbots [count]` | Adds bots back |
| `css_forceloadout [rifle|pistol|knife|off]` | Forces one loadout for all players |
| `css_forcevote` | Starts a vote immediately |
| `css_cancelvote` | Cancels the current vote |
| `css_kniferound` | Queues a knife-only round |
| `css_pistolround` | Queues a pistol-only round |
| `css_warmupevent [default|pistols|knives|scouts|random]` | Changes the warmup event |

## Menu Controls

XPX uses numbered center menus.

### Keys

- `1-6` select visible items
- `7` is `Back` on page 1, or `Prev` on later pages
- `8` is `Next`
- `9` is `Close`

### Fallback

If your `1-9` binds are not set up, you can use:

- `!1`
- `!2`
- `!3`
- `...`
- `!9`

while a menu is open.

## Notes

- Some commands are chat-first and intended for players.
- Admin commands still work from console even if the matching chat/menu flow is the more common path.
- The shop currently includes crate access; `!crate` is no longer the public flow.
