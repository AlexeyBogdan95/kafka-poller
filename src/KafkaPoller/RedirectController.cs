using System.Collections.Concurrent;

namespace KafkaPoller;

internal class RedirectController
{
    private bool _initialized;
    private readonly object _lockObj = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _dictionary = new();

    public bool Initialized
    {
        get
        {
            lock (_lockObj)
            {
                return _initialized;
            }
        }

        set
        {
            lock (_lockObj)
            {
                _initialized = value;
            }
        }
    }

    public bool IsOnRetryFlow(string key) => _dictionary.ContainsKey(key);

    public void Apply(Redirect redirect)
    {
        lock (_lockObj)
        {
            if (_dictionary.TryGetValue(redirect.Key, out var redirectMessages))
            {
                if (redirect.IsTombstone)
                {
                    redirectMessages.Remove(redirect.UniqueId);
                    if (redirectMessages.Count == 0)
                        _dictionary.TryRemove(redirect.Key, out _);

                    return;
                }

                redirectMessages.Add(redirect.UniqueId);
            }
            else if (!redirect.IsTombstone)
            {
                _dictionary.TryAdd(redirect.Key, [redirect.UniqueId]);
            }
        }
    }
}