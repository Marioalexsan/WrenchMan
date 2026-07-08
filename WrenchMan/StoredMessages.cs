using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrenchMan;

public static class StoredMessages
{
    public class Attachment
    {
        public string ContentBase64 { get; set; } = "";
        public string ContentType { get; set; } = "text/plain";
        public string Filename { get; set; } = "attachment.txt";
    }
    
    public class Message
    {
        public string Description { get; set; } = "Unknown message content";
        public string Text { get; set; } = "";
        public List<Attachment> Attachments { get; set; } = [];
    }
    
    private static string MessagesPath => Path.Combine(WrenchManBot.ConfigPath, "stored_messages");

    public static IEnumerable<string> GetAvailableMessages()
    {
        if (!Directory.Exists(MessagesPath))
            return [];

        return Directory.EnumerateFiles(MessagesPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(x => Path.GetFileNameWithoutExtension(x));
    }

    public static async Task<Message?> GetMessageAsync(string id)
    {
        var filePath = Path.Combine(MessagesPath, $"{id}.json");

        if (!File.Exists(filePath))
            return null;

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<Message>(stream);
    }

    public static async Task SetMessageAsync(string id, Message message)
    {
        if (!Directory.Exists(MessagesPath))
            Directory.CreateDirectory(MessagesPath);
        
        var filePath = Path.Combine(MessagesPath, $"{id}.json");

        using var stream = File.OpenWrite(filePath);
        await JsonSerializer.SerializeAsync(stream, message);
    }

    public static async Task DeleteMessageAsync(string id)
    {
        var filePath = Path.Combine(MessagesPath, $"{id}.json");
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}