using System.Diagnostics;
using Discord;
using Discord.WebSocket;

namespace WrenchMan;

public partial class WrenchManBot
{
    private async Task SetupCommands()
    {
        List<ApplicationCommandProperties> cmds = [];
        
        RegisterSlashCommand(AnalyzeLogCommand, new SlashCommandBuilder()
            .WithName("analyze-log")
            .WithDescription("Extracts logs from arguments or a given message, and analyzes them.")
            .AddOption("file", ApplicationCommandOptionType.Attachment, "Attach a log file to analyze.", isRequired: true)
            .AddOption("sanitize", ApplicationCommandOptionType.Boolean, "Remove sensitive information from the log (such as Steam IDs)"));

        RegisterSlashCommand(ToggleLogsForChannel, new SlashCommandBuilder()
            .WithName("toggle-channel-logs")
            .WithDescription("Toggles on/off whenever the bot automatically analyzes logs sent in this channel."));

        RegisterSlashCommand(GetBotStats, new SlashCommandBuilder()
            .WithName("stats")
            .WithDescription("Gets bot stats."));

        RegisterSlashCommand(StoreBotMessage, new SlashCommandBuilder()
            .WithName("store-ref")
            .WithDescription("Stores a message in the bot. Requires bot admin. Overwrites existing message if any.")
            .AddOption("id", ApplicationCommandOptionType.String, "The identifier to use for the message.", isRequired: true, minLength: 4, maxLength: 32)
            .AddOption("description", ApplicationCommandOptionType.String, "The identifier to use for the message.", isRequired: true, minLength: 4, maxLength: 64)
            .AddOption("text", ApplicationCommandOptionType.String, "Message text to send.")
            .AddOption("file1", ApplicationCommandOptionType.Attachment, "First file attachment.")
            .AddOption("file2", ApplicationCommandOptionType.Attachment, "Second file attachment.")
            .AddOption("file3", ApplicationCommandOptionType.Attachment, "Third file attachment.")
            .AddOption("file4", ApplicationCommandOptionType.Attachment, "Fourth file attachment."));
        
        RegisterSlashCommand(RemoveBotMessage, new SlashCommandBuilder()
            .WithName("remove-ref")
            .WithDescription("Removes a message from the bot. Requires bot admin.")
            .AddOption("id", ApplicationCommandOptionType.String, "The identifier of the message.", isRequired: true));
        
        RegisterSlashCommand(GetAvailableBotMessages, new SlashCommandBuilder()
            .WithName("list-ref")
            .WithDescription("Get available messages you can use. Paginated with 10 items per page.")
            .AddOption("page", ApplicationCommandOptionType.Integer, "Page number to show.", minValue: 1, maxValue: 9999));
        
        RegisterSlashCommand(GetBotMessage, new SlashCommandBuilder()
            .WithName("ref")
            .WithDescription("Get a message from the bot.")
            .AddOption("id", ApplicationCommandOptionType.String, "The identifier of the message.", isRequired: true));

        RegisterMessageCommand(AnalyzeLogOnMessageCommand, new MessageCommandBuilder()
            .WithName("Analyze logs"));

        await SocketClient.BulkOverwriteGlobalApplicationCommandsAsync(cmds.ToArray());

        void RegisterSlashCommand(Func<SocketSlashCommand, Task> cmd, SlashCommandBuilder builder)
        {
            _slashCommandHandlers[builder.Name] = cmd;
            cmds.Add(builder.Build());
        }
        
        void RegisterMessageCommand(Func<SocketMessageCommand, Task> cmd, MessageCommandBuilder builder)
        {
            _messageCommandHandlers[builder.Name] = cmd;
            cmds.Add(builder.Build());
        }
    }
    // TODO: The entire message code surface is rushed, should be refactored
    private async Task StoreBotMessage(SocketSlashCommand cmd)
    {
        if (!GetGlobalConfig().GlobalAdministrators.Contains(cmd.User.Id.ToString()))
        {
            await cmd.FollowupAsync("You cannot use that command!", ephemeral: true);
            return;
        }
        
        var id = cmd.Data.Options.FirstOrDefault(x => x.Name == "id")?.Value as string;
        var description = cmd.Data.Options.FirstOrDefault(x => x.Name == "description")?.Value as string;
        var text = cmd.Data.Options.FirstOrDefault(x => x.Name == "text")?.Value as string;
        var file1 = cmd.Data.Options.FirstOrDefault(x => x.Name == "file1")?.Value as IAttachment;
        var file2 = cmd.Data.Options.FirstOrDefault(x => x.Name == "file2")?.Value as IAttachment;
        var file3 = cmd.Data.Options.FirstOrDefault(x => x.Name == "file3")?.Value as IAttachment;
        var file4 = cmd.Data.Options.FirstOrDefault(x => x.Name == "file4")?.Value as IAttachment;
        var attachments = new[] { file1, file2, file3, file4 }.Where(x => x != null).ToArray();

        if (!StoredMessages.IsValidId(id) || description == null)
        {
            await cmd.FollowupAsync("Invalid arguments!", ephemeral: true);
            return;
        }

        if (text == null && attachments.Length == 0)
        {
            await cmd.FollowupAsync("Specify at least a text or an image!", ephemeral: true);
            return;
        }

        var files = await Task.WhenAll(attachments.Select(x => x == null ? Task.FromResult<byte[]?>(null) : FetchAsync(x.Url, CancellationToken.None)));

        if (files.Any(x => x == null))
        {
            await cmd.FollowupAsync("Failed to fetch one or more attachments!", ephemeral: true);
            return;
        }
        
        await StoredMessages.SetMessageAsync(id, new StoredMessages.Message()
        {
            Attachments = files.Zip(attachments).Select(x => new StoredMessages.Attachment()
            {
                Filename = x.Second!.Filename,
                ContentType = x.Second!.ContentType,
                ContentBase64 = Convert.ToBase64String(x.First!)
            }).ToList(),
            Description = description,
            Text = text ?? ""
        });
        
        await cmd.FollowupAsync("Message stored!", ephemeral: true);
    }
    
    private async Task RemoveBotMessage(SocketSlashCommand cmd)
    {
        if (!GetGlobalConfig().GlobalAdministrators.Contains(cmd.User.Id.ToString()))
        {
            await cmd.FollowupAsync("You cannot use that command!", ephemeral: true);
            return;
        }

        var id = (cmd.Data.Options.FirstOrDefault(x => x.Name == "id")?.Value as string)?.ToLowerInvariant();
        
        if (!StoredMessages.IsValidId(id))
        {
            await cmd.FollowupAsync("Invalid arguments!", ephemeral: true);
            return;
        }

        var msg = await StoredMessages.GetMessageAsync(id);
        
        if (msg == null)
        {
            await cmd.FollowupAsync("No such message found!", ephemeral: true);
            return;
        }

        await StoredMessages.DeleteMessageAsync(id);
        await cmd.FollowupAsync("Message deleted!", ephemeral: true);
    }
    
    private async Task GetBotMessage(SocketSlashCommand cmd)
    {
        var id = (cmd.Data.Options.FirstOrDefault(x => x.Name == "id")?.Value as string)?.ToLowerInvariant();
        
        if (!StoredMessages.IsValidId(id))
        {
            await cmd.FollowupAsync("Invalid arguments!", ephemeral: true);
            return;
        }

        var msg = await StoredMessages.GetMessageAsync(id);
        
        if (msg == null)
        {
            await cmd.FollowupAsync("No such message found!", ephemeral: true);
            return;
        }

        if (msg.Attachments.Count == 0)
            await cmd.FollowupAsync(msg.Text.Length > 0 ? msg.Text : "<No message data available!>");
        else
            await cmd.FollowupWithFilesAsync(msg.Attachments.Select(x =>
            {
                var memoryStream = new MemoryStream(Convert.FromBase64String(x.ContentBase64));
                return new FileAttachment(memoryStream, x.Filename);
            }), msg.Text);
    }
    
    private async Task GetAvailableBotMessages(SocketSlashCommand cmd)
    {
        var page = (int)(cmd.Data.Options.FirstOrDefault(x => x.Name == "page")?.Value is long thePage ? thePage : 1);
        var msgs = StoredMessages.GetAvailableMessages();

        var chunks = msgs.Order().Chunk(10).ToList();

        if (page < 1 || page > chunks.Count)
        {
            await cmd.FollowupAsync($"Page number not found! Specify a page number between 1 and {chunks.Count}.", ephemeral: true);
            return;
        }

        var retrievedMessages = await Task.WhenAll(chunks[page - 1].Select(x => StoredMessages.GetMessageAsync(x)));

        await cmd.FollowupAsync(
            $"Command list (page {page} of {chunks.Count}):\n" + 
            string.Join('\n', chunks[page - 1].Zip(retrievedMessages).Select(x => $"- `{x.First}` => {x.Second?.Description ?? "<No description available>"}")),
            ephemeral: true
        );
    }

    private async Task AnalyzeLogCommand(SocketSlashCommand cmd)
    {
        var attachmentOption = cmd.Data.Options.FirstOrDefault(x => x.Name == "file");

        if (attachmentOption == null)
        {
            await cmd.FollowupAsync($"You must provide a log to analyze!", ephemeral: true);
            return;
        }

        var attachment = (IAttachment)attachmentOption.Value;
        
        var sanitizeOption = cmd.Data.Options.FirstOrDefault(x => x.Name == "sanitize");

        bool sanitize = sanitizeOption != null ? (bool)sanitizeOption.Value : true;

        const int maxSizeInMib = 20;

        if (attachment.Size >= 1024 * 1024 * maxSizeInMib)
        {
            await cmd.FollowupAsync($"Sorry, I can only parse up to {maxSizeInMib} MiB of logs at once!", ephemeral: true);
            return;
        }

        IAttachment[] logsToAnalize = [attachment];

        var responseTime = Stopwatch.StartNew();
        
        await AnalyzeReceivedLogs(cmd.Channel, async (replyMessage, attachments) =>
        {
            if (attachments.Length == 0)
                await cmd.FollowupAsync(replyMessage);
            else
                await cmd.FollowupWithFilesAsync(attachments, replyMessage);
        }, logsToAnalize, true, sanitize, CancellationToken.None);

        Program.Debug(nameof(WrenchManBot), $"Response in total took {responseTime.ElapsedMilliseconds:F2}ms");
    }

    private async Task AnalyzeLogOnMessageCommand(SocketMessageCommand cmd)
    {
        IEnumerable<IAttachment> logSource;

        if (cmd.Data.Message is not SocketUserMessage userMessage)
        {
            await cmd.FollowupAsync($"I cannot grab logs from that message!", ephemeral: true);
            return;
        }
        
        if (userMessage.Reference != null && userMessage.Reference.ReferenceType.IsSpecified && userMessage.Reference.ReferenceType.Value == MessageReferenceType.Forward)
        {
            logSource = userMessage.ForwardedMessages.Count > 0 
                ? userMessage.ForwardedMessages.First().Message.Attachments 
                : [];
        }
        else
        {
            logSource = userMessage.Attachments;
        }

        var attachments = logSource.ToList();
        
        // These are not sanitized, since it's implied the logs are already posted in a channel if you use context
        // menu commands on them
        if (attachments.Count == 0)
        {
            await cmd.FollowupAsync($"There are no log attachments to analyze in this message!", ephemeral: true);
            return;
        }
        
        const int maxSizeInMib = 20;

        if (attachments.Select(x => x.Size).Sum() >= 1024 * 1024 * maxSizeInMib)
        {
            await cmd.FollowupAsync($"Sorry, I can only parse up to {maxSizeInMib} MiB of logs at once!", ephemeral: true);
            return;
        }

        var responseTime = Stopwatch.StartNew();
        
        await AnalyzeReceivedLogs(cmd.Channel, async (replyMessage, fileAttachments) =>
        {
            if (fileAttachments.Length == 0)
                await cmd.FollowupAsync(replyMessage);
            else
                await cmd.FollowupWithFilesAsync(fileAttachments, replyMessage);
        }, [.. attachments], false, false, CancellationToken.None);

        Program.Debug(nameof(WrenchManBot), $"Response in total took {responseTime.ElapsedMilliseconds:F2}ms");
    }

    private async Task GetBotStats(SocketSlashCommand cmd)
    {
        if (!GetGlobalConfig().GlobalAdministrators.Contains(cmd.User.Id.ToString()))
        {
            await cmd.FollowupAsync("You cannot use that command!", ephemeral: true);
            return;
        }
        
        await cmd.FollowupAsync($"Currently in {SocketClient.Guilds.Count} guilds", ephemeral: true);
    }

    private async Task ToggleLogsForChannel(SocketSlashCommand cmd)
    {
        if (cmd.Channel is not IGuildChannel channel)
        {
            await cmd.FollowupAsync("This command can only be used as part of servers!", ephemeral: true);
            return;
        }

        var guildUser = await channel.Guild.GetUserAsync(cmd.User.Id);

        if (!guildUser.GuildPermissions.ManageGuild && !GetGlobalConfig().GlobalAdministrators.Contains(cmd.User.Id.ToString()))
        {
            await cmd.FollowupAsync("You need the \"Manage Server\" to toggle channel logging!", ephemeral: true);
            return;
        }

        var config = GetConfigForGuild(channel.Guild.Id.ToString());
        var channelsToMonitor = config.LogAnalyzer.ChannelsToMonitor;

        if (channelsToMonitor.Contains(channel.Id.ToString()))
        {
            channelsToMonitor.Remove(channel.Id.ToString());
            Program.Info(nameof(WrenchManBot), $"Toggled off logging for channel {channel.Name} ({channel.Id}) in {channel.Guild.Name} ({channel.Guild.Id}).");
            await cmd.FollowupAsync("Toggled off logging for this channel!", ephemeral: true);
        }
        else
        {
            channelsToMonitor.Add(channel.Id.ToString());
            Program.Info(nameof(WrenchManBot), $"Toggled on logging for channel {channel.Name} ({channel.Id}) in {channel.Guild.Name} ({channel.Guild.Id}).");
            await cmd.FollowupAsync("Toggled on logging for this channel!", ephemeral: true);
        }
        
        UpdateConfigForGuild(channel.Guild.Id.ToString());
    }
}