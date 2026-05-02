using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Linq;

namespace FuseDrill.Core;

public enum DevProxyMode
{
    Off,
    Capture,
    Replay,
    Auto
}

public sealed class DevProxyOptions
{
    public DevProxyMode Mode { get; set; } = DevProxyMode.Off;
    public string StoragePath { get; set; } = "dev-proxy.json";
    public IReadOnlyCollection<string> InternalHosts { get; set; } = Array.Empty<string>();
}

public static class DevProxyConfiguration
{
    public const string ModeVariable = "FUSEDRILL_DEV_PROXY_MODE";
    public const string StorageVariable = "FUSEDRILL_DEV_PROXY_STORE";
    public const string InternalHostsVariable = "FUSEDRILL_DEV_PROXY_INTERNAL_HOSTS";

    public static DevProxyOptions? FromEnvironment()
    {
        var modeValue = Environment.GetEnvironmentVariable(ModeVariable);
        if (string.IsNullOrWhiteSpace(modeValue))
        {
            return null;
        }

        if (!Enum.TryParse<DevProxyMode>(modeValue, true, out var mode))
        {
            return null;
        }

        if (mode == DevProxyMode.Off)
        {
            return null;
        }

        var storagePath = Environment.GetEnvironmentVariable(StorageVariable);
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            storagePath = "dev-proxy.json";
        }

        var internalHostsValue = Environment.GetEnvironmentVariable(InternalHostsVariable);
        var internalHosts = string.IsNullOrWhiteSpace(internalHostsValue)
            ? Array.Empty<string>()
            : internalHostsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new DevProxyOptions
        {
            Mode = mode,
            StoragePath = storagePath,
            InternalHosts = internalHosts
        };
    }
}

public sealed class DevProxyHandler : DelegatingHandler
{
    private readonly DevProxyOptions _options;
    private readonly DevProxyStore _store;

    public DevProxyHandler(DevProxyOptions options, HttpMessageHandler? innerHandler = null)
        : base(innerHandler ?? new HttpClientHandler())
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _store = DevProxyStore.Load(options.StoragePath);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestSnapshot = await DevProxyMessageSnapshot.FromRequestAsync(request, _options.InternalHosts, cancellationToken);
        var cacheKey = requestSnapshot.GetCacheKey();

        if (_options.Mode is DevProxyMode.Replay or DevProxyMode.Auto)
        {
            if (_store.TryDequeue(cacheKey, out var replayEntry))
            {
                return replayEntry.ToHttpResponseMessage(request);
            }

            if (_options.Mode == DevProxyMode.Replay)
            {
                throw new InvalidOperationException($"Dev proxy replay failed for {request.Method} {request.RequestUri}.");
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (_options.Mode is DevProxyMode.Capture or DevProxyMode.Auto)
        {
            var responseSnapshot = await DevProxyMessageSnapshot.FromResponseAsync(response, cancellationToken);
            RestoreResponseContent(response, responseSnapshot);
            var entry = new DevProxyEntry
            {
                Request = requestSnapshot,
                Response = responseSnapshot,
                CapturedAt = DateTimeOffset.UtcNow
            };
            _store.Add(entry);
            _store.Save();
        }

        return response;
    }

    private static void RestoreResponseContent(HttpResponseMessage response, DevProxyMessageSnapshot snapshot)
    {
        if (response.Content == null || snapshot.Body == null)
        {
            return;
        }

        var restored = new ByteArrayContent(snapshot.Body);
        if (snapshot.ContentHeaders != null)
        {
            foreach (var header in snapshot.ContentHeaders)
            {
                restored.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        response.Content = restored;
    }
}

public sealed class DevProxyStore
{
    private readonly string _path;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DevProxyEntry>> _entries;
    private readonly List<DevProxyEntry> _orderedEntries;

    private DevProxyStore(string path, IEnumerable<DevProxyEntry> entries)
    {
        _path = path;
        _orderedEntries = entries.ToList();
        _entries = new ConcurrentDictionary<string, ConcurrentQueue<DevProxyEntry>>(
            _orderedEntries
                .GroupBy(entry => entry.Request.GetCacheKey())
                .ToDictionary(group => group.Key, group => new ConcurrentQueue<DevProxyEntry>(group)));
    }

    public static DevProxyStore Load(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var payload = JsonSerializer.Deserialize<DevProxyPayload>(json, DevProxyJson.Options);
                if (payload?.Entries != null)
                {
                    return new DevProxyStore(path, payload.Entries);
                }
            }
        }

        return new DevProxyStore(path, Array.Empty<DevProxyEntry>());
    }

    public bool TryDequeue(string key, out DevProxyEntry entry)
    {
        entry = null!;
        if (_entries.TryGetValue(key, out var queue))
        {
            return queue.TryDequeue(out entry);
        }

        return false;
    }

    public void Add(DevProxyEntry entry)
    {
        var key = entry.Request.GetCacheKey();
        var queue = _entries.GetOrAdd(key, _ => new ConcurrentQueue<DevProxyEntry>());
        queue.Enqueue(entry);
        _orderedEntries.Add(entry);
    }

    public void Save()
    {
        var payload = new DevProxyPayload
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Entries = _orderedEntries
        };

        var json = JsonSerializer.Serialize(payload, DevProxyJson.Options);
        File.WriteAllText(_path, json);
    }
}

public sealed class DevProxyPayload
{
    public DateTimeOffset CapturedAt { get; set; }
    public List<DevProxyEntry> Entries { get; set; } = new();
}

public sealed class DevProxyEntry
{
    public DevProxyMessageSnapshot Request { get; set; } = new();
    public DevProxyMessageSnapshot Response { get; set; } = new();
    public DateTimeOffset CapturedAt { get; set; }

    public HttpResponseMessage ToHttpResponseMessage(HttpRequestMessage request)
    {
        var response = new HttpResponseMessage((HttpStatusCode)Response.StatusCode)
        {
            RequestMessage = request
        };

        if (Response.Body?.Length > 0)
        {
            response.Content = new ByteArrayContent(Response.Body);
            if (Response.ContentHeaders != null)
            {
                foreach (var header in Response.ContentHeaders)
                {
                    response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        if (Response.Headers != null)
        {
            foreach (var header in Response.Headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return response;
    }
}

public sealed class DevProxyMessageSnapshot
{
    public string? Method { get; set; }
    public string? Url { get; set; }
    public string? Host { get; set; }
    public bool IsExternal { get; set; }
    public int StatusCode { get; set; }
    public byte[]? Body { get; set; }
    public Dictionary<string, string[]>? Headers { get; set; }
    public Dictionary<string, string[]>? ContentHeaders { get; set; }

    public string GetCacheKey()
    {
        var bodyHash = Body == null ? string.Empty : Convert.ToBase64String(Body);
        return $"{Method}|{Url}|{bodyHash}";
    }

    public static async Task<DevProxyMessageSnapshot> FromRequestAsync(HttpRequestMessage request, IReadOnlyCollection<string> internalHosts, CancellationToken cancellationToken)
    {
        var snapshot = new DevProxyMessageSnapshot
        {
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString(),
            Host = request.RequestUri?.Host
        };

        snapshot.IsExternal = snapshot.Host != null &&
                              internalHosts.Count > 0 &&
                              !internalHosts.Any(host => string.Equals(host, snapshot.Host, StringComparison.OrdinalIgnoreCase));

        if (request.Content != null)
        {
            snapshot.Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            snapshot.ContentHeaders = ExtractHeaders(request.Content.Headers);
        }

        snapshot.Headers = ExtractHeaders(request.Headers);
        return snapshot;
    }

    public static async Task<DevProxyMessageSnapshot> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var snapshot = new DevProxyMessageSnapshot
        {
            StatusCode = (int)response.StatusCode
        };

        if (response.Content != null)
        {
            snapshot.Body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            snapshot.ContentHeaders = ExtractHeaders(response.Content.Headers);
        }

        snapshot.Headers = ExtractHeaders(response.Headers);
        return snapshot;
    }

    private static Dictionary<string, string[]> ExtractHeaders(HttpHeaders headers)
    {
        return headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
    }
}

public static class DevProxyJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
