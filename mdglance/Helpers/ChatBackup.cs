/**
 * ChatBackup.cs
 *
 * @brief Reads a ChatGPT "conversations.json" export into a lean, renderable model.
 * @author Albin George Kurian <albingeorgekurian3@gmail.com>
 * @license MIT
 * @date 2026-08-20
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace mdglance.Helpers
{
    /// <summary>A single turn in a conversation.</summary>
    internal class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime? Timestamp { get; set; }

        /// <summary>Heading label used when the conversation is rendered as markdown.</summary>
        public string DisplayRole
        {
            get { return Role == "user" ? "👤 You" : "🤖 ChatGPT"; }
        }
    }

    /// <summary>One chat thread out of a backup, already flattened to its active branch.</summary>
    internal class ChatConversation
    {
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        /// <summary>
        /// Renders the whole thread as one markdown document so it can go through the
        /// same Markdig pipeline that regular .md files use.
        /// </summary>
        public string ToMarkdown()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# " + Title);
            sb.AppendLine();
            string summary = $"{Messages.Count} message{(Messages.Count == 1 ? "" : "s")}";
            sb.AppendLine(CreatedAt == DateTime.MinValue
                ? $"*{summary}*"
                : $"*{CreatedAt:dd MMM yyyy HH:mm} · {summary}*");
            sb.AppendLine();

            if (Messages.Count == 0)
            {
                sb.AppendLine("*This conversation has no readable messages.*");
                return sb.ToString();
            }

            foreach (ChatMessage message in Messages)
            {
                // h2 already carries a bottom border in the viewer stylesheet, so role
                // headings double up as clean turn separators without any extra CSS
                sb.AppendLine("## " + message.DisplayRole);
                sb.AppendLine();
                sb.AppendLine(message.Text);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>A parsed LLM backup file: the chats it holds, newest first.</summary>
    internal class ChatBackup
    {
        private const string UnsupportedMessage =
            "This does not look like a ChatGPT conversations.json export.";

        public string SourcePath { get; set; } = "";
        public List<ChatConversation> Conversations { get; set; } = new List<ChatConversation>();

        /// <summary>
        /// Parses a ChatGPT export. The file is streamed one conversation at a time because
        /// real exports routinely run into hundreds of megabytes.
        /// </summary>
        /// <exception cref="FormatException">The file is valid JSON but not a recognised export.</exception>
        public static ChatBackup Load(string path)
        {
            ChatBackup backup = new ChatBackup { SourcePath = path };
            bool recognised = false;

            using (StreamReader stream = new StreamReader(path))
            using (JsonTextReader reader = new JsonTextReader(stream))
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartArray)
                {
                    throw new FormatException(UnsupportedMessage);
                }

                while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                {
                    if (reader.TokenType != JsonToken.StartObject)
                    {
                        reader.Skip();
                        continue;
                    }

                    // Load and discard one element at a time so the whole graph never sits in memory
                    JObject item = JObject.Load(reader);
                    JObject mapping = item["mapping"] as JObject;
                    if (mapping == null) continue;

                    recognised = true;
                    backup.Conversations.Add(ParseConversation(item, mapping));
                }
            }

            if (!recognised)
            {
                throw new FormatException(UnsupportedMessage);
            }

            backup.Conversations = backup.Conversations
                .OrderByDescending(c => c.UpdatedAt)
                .ToList();

            return backup;
        }

        private static ChatConversation ParseConversation(JObject item, JObject mapping)
        {
            DateTime? created = ToLocalTime(item["create_time"]);
            DateTime? updated = ToLocalTime(item["update_time"]);

            ChatConversation conversation = new ChatConversation
            {
                Title = CleanTitle(item["title"]),
                CreatedAt = created ?? updated ?? DateTime.MinValue,
                UpdatedAt = updated ?? created ?? DateTime.MinValue
            };

            string currentNode = item["current_node"] != null && item["current_node"].Type == JTokenType.String
                ? item["current_node"].Value<string>()
                : null;

            foreach (JObject raw in ActiveBranch(mapping, currentNode))
            {
                ChatMessage message = ParseMessage(raw);
                if (message != null) conversation.Messages.Add(message);
            }

            return conversation;
        }

        /// <summary>
        /// Walks the mapping graph from the current leaf back up to the root. An export keeps
        /// every edited and regenerated branch, so only this chain is the chat as the user left it.
        /// </summary>
        private static List<JObject> ActiveBranch(JObject mapping, string currentNode)
        {
            List<JObject> chain = new List<JObject>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            string nodeId = currentNode;

            // visited.Add() returning false also breaks the loop on a malformed cyclic export
            while (!string.IsNullOrEmpty(nodeId) && visited.Add(nodeId))
            {
                JObject node = mapping[nodeId] as JObject;
                if (node == null) break;

                JObject message = node["message"] as JObject;
                if (message != null) chain.Add(message);

                JToken parent = node["parent"];
                nodeId = parent != null && parent.Type == JTokenType.String ? parent.Value<string>() : null;
            }

            chain.Reverse();

            if (chain.Count == 0)
            {
                // Missing or broken current_node: fall back to everything the mapping holds
                chain = mapping.Properties()
                    .Select(p => (p.Value as JObject)?["message"] as JObject)
                    .Where(m => m != null)
                    .OrderBy(m => ToLocalTime(m["create_time"]) ?? DateTime.MinValue)
                    .ToList();
            }

            return chain;
        }

        private static ChatMessage ParseMessage(JObject message)
        {
            JObject author = message["author"] as JObject;
            string role = author?["role"]?.ToString();

            // System prompts and tool plumbing are not part of the visible chat
            if (string.IsNullOrEmpty(role) || role == "system" || role == "tool") return null;

            JObject metadata = message["metadata"] as JObject;
            JToken hidden = metadata?["is_visually_hidden_from_conversation"];
            if (hidden != null && hidden.Type == JTokenType.Boolean && hidden.Value<bool>()) return null;

            string text = ExtractText(message["content"] as JObject);
            if (string.IsNullOrWhiteSpace(text)) return null;

            return new ChatMessage
            {
                Role = role,
                Text = text.Trim(),
                Timestamp = ToLocalTime(message["create_time"])
            };
        }

        private static string ExtractText(JObject content)
        {
            if (content == null) return null;

            string contentType = content["content_type"]?.ToString() ?? "text";

            switch (contentType)
            {
                case "text":
                    return JoinParts(content["parts"] as JArray);

                case "code":
                    string code = content["text"]?.ToString();
                    if (string.IsNullOrWhiteSpace(code)) return null;
                    string language = content["language"]?.ToString();
                    if (string.IsNullOrWhiteSpace(language) || language == "unknown") language = "";
                    return $"```{language}\n{code}\n```";

                case "multimodal_text":
                    return JoinParts(content["parts"] as JArray);

                case "thoughts":
                case "reasoning_recap":
                    // Reasoning traces are noise here, the user asked for the chat itself
                    return null;

                default:
                    // Unknown or future content types: salvage a plain text payload if there is one
                    return content["text"]?.ToString();
            }
        }

        /// <summary>Flattens a parts array, standing in a placeholder for non-text attachments.</summary>
        private static string JoinParts(JArray parts)
        {
            if (parts == null) return null;

            List<string> pieces = new List<string>();
            foreach (JToken part in parts)
            {
                if (part.Type == JTokenType.String)
                {
                    string value = part.Value<string>();
                    if (!string.IsNullOrWhiteSpace(value)) pieces.Add(value);
                    continue;
                }

                JObject blob = part as JObject;
                if (blob == null) continue;

                // Audio transcriptions and similar carry their own readable text
                string inner = blob["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    pieces.Add(inner);
                    continue;
                }

                string partType = blob["content_type"]?.ToString() ?? "";
                pieces.Add(partType.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "*[image]*"
                    : "*[attachment]*");
            }

            return pieces.Count == 0 ? null : string.Join("\n\n", pieces);
        }

        private static string CleanTitle(JToken token)
        {
            string title = token?.Type == JTokenType.String ? token.Value<string>() : null;
            if (string.IsNullOrWhiteSpace(title)) return "(untitled)";

            // Titles are shown as tree node text and as an h1, so collapse any stray line breaks
            return title.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>Converts a ChatGPT epoch-seconds timestamp (a double) to local time.</summary>
        private static DateTime? ToLocalTime(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            double seconds;
            if (!double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out seconds))
            {
                return null;
            }
            if (seconds <= 0) return null;

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000)).LocalDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Out-of-range garbage in the export, treat it as an absent timestamp
                return null;
            }
        }
    }
}
