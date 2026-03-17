// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Models;

namespace ECP.Core.Negotiation;

/// <summary>
/// Result of a capability negotiation.
/// </summary>
public readonly record struct CapabilityNegotiationResult(byte NegotiatedVersion, EcpCapabilities NegotiatedCapabilities);

/// <summary>
/// Caches negotiated capabilities per peer.
/// </summary>
public sealed class CapabilityHandshakeCache
{
    /// <summary>Default maximum number of cached peers.</summary>
    public const int DefaultMaxEntries = 1024;

    private readonly Dictionary<string, CapabilityNegotiationResult> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.Ordinal);
    private readonly int _maxEntries;
    private readonly object _sync = new();

    /// <summary>
    /// Creates a cache with a maximum number of entries.
    /// </summary>
    public CapabilityHandshakeCache(int maxEntries = DefaultMaxEntries)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Max entries must be greater than zero.");
        }

        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Attempts to retrieve a cached result for a peer.
    /// </summary>
    public bool TryGet(string peerId, out CapabilityNegotiationResult result)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            throw new ArgumentException("PeerId must be provided.", nameof(peerId));
        }

        lock (_sync)
        {
            return _cache.TryGetValue(peerId, out result);
        }
    }

    /// <summary>
    /// Caches a result for a peer.
    /// </summary>
    public void Store(string peerId, CapabilityNegotiationResult result)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            throw new ArgumentException("PeerId must be provided.", nameof(peerId));
        }

        lock (_sync)
        {
            if (_cache.ContainsKey(peerId))
            {
                _cache[peerId] = result;
                if (_nodes.TryGetValue(peerId, out var existingNode))
                {
                    _order.Remove(existingNode);
                    _order.AddLast(existingNode);
                }
                else
                {
                    var node = _order.AddLast(peerId);
                    _nodes[peerId] = node;
                }

                return;
            }

            _cache[peerId] = result;
            var newNode = _order.AddLast(peerId);
            _nodes[peerId] = newNode;
            EvictIfNeeded();
        }
    }

    /// <summary>
    /// Clears a cached result for a peer.
    /// </summary>
    public void Clear(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            throw new ArgumentException("PeerId must be provided.", nameof(peerId));
        }

        lock (_sync)
        {
            if (_cache.Remove(peerId) && _nodes.TryGetValue(peerId, out var node))
            {
                _order.Remove(node);
                _nodes.Remove(peerId);
            }
        }
    }

    private void EvictIfNeeded()
    {
        while (_cache.Count > _maxEntries && _order.First is not null)
        {
            var oldest = _order.First.Value;
            _order.RemoveFirst();
            _cache.Remove(oldest);
            _nodes.Remove(oldest);
        }
    }
}

/// <summary>
/// Negotiates protocol version and capabilities with peers.
/// </summary>
public sealed class CapabilityHandshake
{
    private readonly byte _minVersion;
    private readonly byte _maxVersion;
    private readonly EcpCapabilities _capabilities;
    private readonly CapabilityHandshakeCache _cache;

    /// <summary>
    /// Creates a capability handshake helper.
    /// </summary>
    public CapabilityHandshake(
        byte minVersion,
        byte maxVersion,
        EcpCapabilities capabilities,
        int cacheMaxEntries = CapabilityHandshakeCache.DefaultMaxEntries)
    {
        if (minVersion > maxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(minVersion), "MinVersion must be <= MaxVersion.");
        }

        _minVersion = minVersion;
        _maxVersion = maxVersion;
        _capabilities = capabilities;
        _cache = new CapabilityHandshakeCache(cacheMaxEntries);
    }

    /// <summary>
    /// Creates a local capability offer.
    /// </summary>
    public CapabilityNegotiationPayload CreateOffer()
    {
        return new CapabilityNegotiationPayload(_minVersion, _maxVersion, _capabilities);
    }

    /// <summary>
    /// Attempts to negotiate with a peer offer and caches the result per peer.
    /// </summary>
    public bool TryProcessOffer(string peerId, CapabilityNegotiationPayload offer, out CapabilityNegotiationResult result)
    {
        if (_cache.TryGet(peerId, out result))
        {
            return true;
        }

        if (!TryNegotiate(offer, out result))
        {
            return false;
        }

        _cache.Store(peerId, result);
        return true;
    }

    /// <summary>
    /// Negotiates with a peer offer and caches the result per peer.
    /// </summary>
    public CapabilityNegotiationResult ProcessOffer(string peerId, CapabilityNegotiationPayload offer)
    {
        if (!TryProcessOffer(peerId, offer, out var result))
        {
            throw new InvalidOperationException("No compatible protocol version available.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to retrieve a cached result for a peer.
    /// </summary>
    public bool TryGetCached(string peerId, out CapabilityNegotiationResult result)
    {
        return _cache.TryGet(peerId, out result);
    }

    private bool TryNegotiate(CapabilityNegotiationPayload offer, out CapabilityNegotiationResult result)
    {
        var min = Math.Max(_minVersion, offer.MinVersion);
        var max = Math.Min(_maxVersion, offer.MaxVersion);
        if (min > max)
        {
            result = default;
            return false;
        }

        var negotiatedCapabilities = _capabilities & offer.Capabilities;
        result = new CapabilityNegotiationResult(max, negotiatedCapabilities);
        return true;
    }
}
