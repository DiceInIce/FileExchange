using System.Collections.Concurrent;

namespace FileShareServer.Services
{
    public interface IUserConnectionManager
    {
        bool AddConnection(int userId, string connectionId);
        bool RemoveConnection(int userId, string connectionId);
        IReadOnlyCollection<string> GetConnections(int userId);
        bool HasConnections(int userId);
    }

    public class UserConnectionManager : IUserConnectionManager
    {
        private readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();

        public bool AddConnection(int userId, string connectionId)
        {
            var connections = _connections.GetOrAdd(userId, _ => new HashSet<string>());
            lock (connections)
            {
                var wasEmpty = connections.Count == 0;
                connections.Add(connectionId);
                return wasEmpty;
            }
        }

        public bool RemoveConnection(int userId, string connectionId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                return false;
            }

            lock (connections)
            {
                if (!connections.Remove(connectionId))
                {
                    return false;
                }

                if (connections.Count == 0)
                {
                    _connections.TryRemove(userId, out _);
                    return true;
                }

                return false;
            }
        }

        public IReadOnlyCollection<string> GetConnections(int userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                return Array.Empty<string>();
            }

            lock (connections)
            {
                return connections.ToArray();
            }
        }

        public bool HasConnections(int userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                return false;
            }

            lock (connections)
            {
                return connections.Count > 0;
            }
        }
    }
}
