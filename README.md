# WrenchMan

This is a .NET project for analyzing BepInEx log files coming from users playing modded Unity games. It comes with the
following components:

- `BepinexLogAnalysis`, a library that includes a log analyzer and sanitizer functionality
- `WrenchMan`, a Discord bot that can be used to analyze logs from players through DMs or servers

The main use case is processing logs for [ATLYSS](https://store.steampowered.com/app/2768430/ATLYSS/) mods. The project
processes log files and extracts informations such as:
- Game version and log time
- Installed BepInEx plugins and their versions
- Installed content packs, for mods such as [Homebrewery](https://thunderstore.io/c/atlyss/p/Catman232/Homebrewery/)
- A list of most important errors and warnings from the log file, deduplicated as needed

# Configuring the bot

WrenchMan loads its configuration from a few files stores relative to the executable.

The main configuration is stored under `config/wrenchman.json`. If not present, it will be initialized on first
launch.

Any Discord server specific configuration is stored under `config/guilds/{guildId}.json`. If not present, it will be
initialized when guild data is requested.

To authenticate with Discord, the bot reads the bot token from a `.wrenchman_token` file. This file must exist; it is
not created on your behalf.

# Supported applications

This bot mainly deals with BepInEx logs; as such, it expects `LogOutput.log` files that use the BepInEx logging
format.
While `Player.log` files might work, they are not fully supported at the moment.

ATLYSS is the main game that is supported by this project, and as such has custom jobs that output additional
information for core mods within the community. However, the project has basic support for other games that use
BepInEx for modding, and can be extended to provide full support for said modding communities.

# Further reading

- This project is licensed under the [GPL3 license](./LICENSE).
- The Discord bot uses [Discord.NET](https://github.com/discord-net/Discord.Net), licensed under
  the [MIT license](https://github.com/discord-net/Discord.Net/blob/3.15.3/LICENSE).
- [PRIVACY.md](./PRIVACY.md) has extra details about the information that may be logged and stored by the software as
  part of the Discord bot.
