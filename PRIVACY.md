# Privacy Statement

As part of the server functionality, the bot application stores some data regarding requests made to it. Part of this
data can potentially identify users and servers that utilize the application.

The main usage of this data is to monitor the performance of the service, to ensure that it behaves correctly, and to
protect against any potential abuse from malicious users or applications.

## WrenchMan

### What is stored?

Basic data about requests made to the Discord bot are stored. These are:

- **Server ID / Name** and **Channel ID / Name** are stored as part of the per-server configuration of the bot, and
  logged for requests coming from servers
- **User IDs** and **User Names** are stored as part of the per-user configuration of the bot, and logged for requests
  coming from users in direct messages

### Why is this information stored?

The main goal is to detect and prevent abuse of the bot infrastructure by malicious users. This means detecting users
or servers that are sending unreasonable volumes of data, and potentially throttling or limiting their access to the
bot.

### What is *not* stored?

The bot explicitly does not log or store any game log content sent in requests or responses, and there is no intention
to start doing so in the future.