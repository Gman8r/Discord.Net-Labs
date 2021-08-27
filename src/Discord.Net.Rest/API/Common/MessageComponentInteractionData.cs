using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord.API
{
    internal class MessageComponentInteractionData : IDiscordInteractionData
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public ApplicationCommandType Type { get; set; }

        [JsonProperty("custom_id")]
        public string CustomId { get; set; }

        [JsonProperty("component_type")]
        public ComponentType ComponentType { get; set; }

        [JsonProperty("values")]
        public Optional<string[]> Values { get; set; }
    }
}
