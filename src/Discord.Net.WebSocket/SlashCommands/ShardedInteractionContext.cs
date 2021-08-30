using Discord.WebSocket;

namespace Discord.ApplicationCommands
{
    /// <summary>
    /// The sharded variant of <see cref="SocketInteractionContext"/>
    /// </summary>
    public class ShardedInteractionContext : SocketInteractionContext, IInteractionContext
    {
        /// <summary>
        /// Get the <see cref="DiscordSocketClient"/> that the command will be executed with
        /// </summary>
        public new DiscordShardedClient Client { get; }

        /// <summary>
        /// Initializes a <see cref="ShardedInteractionContext"/>
        /// </summary>
        /// <param name="client">The underlying client.</param>
        /// <param name="interaction">The underlying interaction</param>
        public ShardedInteractionContext (DiscordShardedClient client, SocketInteraction interaction)
            : base(client.GetShard(GetShardId(client, ( interaction.User as SocketGuildUser )?.Guild)), interaction)
        {
            Client = client;
        }

        private static int GetShardId (DiscordShardedClient client, IGuild guild)
            => guild == null ? 0 : client.GetShardIdFor(guild);
    }
}
