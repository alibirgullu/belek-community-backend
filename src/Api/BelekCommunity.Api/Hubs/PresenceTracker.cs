using System.Collections.Concurrent;

namespace BelekCommunity.Api.Hubs
{
    public class PresenceTracker
    {
        private readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();

        public Task<bool> UserConnected(int userId, string connectionId)
        {
            var isFirstConnection = false;
            _connections.AddOrUpdate(
                userId,
                _ =>
                {
                    isFirstConnection = true;
                    return new HashSet<string> { connectionId };
                },
                (_, existing) =>
                {
                    lock (existing)
                    {
                        existing.Add(connectionId);
                    }
                    return existing;
                });

            return Task.FromResult(isFirstConnection);
        }

        public Task<bool> UserDisconnected(int userId, string connectionId)
        {
            var lastConnection = false;
            if (_connections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        _connections.TryRemove(userId, out _);
                        lastConnection = true;
                    }
                }
            }
            return Task.FromResult(lastConnection);
        }

        public Task<int[]> GetOnlineUserIds()
        {
            return Task.FromResult(_connections.Keys.ToArray());
        }

        public Task<bool> IsOnline(int userId)
        {
            return Task.FromResult(_connections.ContainsKey(userId));
        }
    }
}
