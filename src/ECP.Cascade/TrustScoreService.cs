// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Strategy;

namespace ECP.Cascade;

/// <summary>
/// Manages trust scores per node and derives fan-out limits.
/// </summary>
public sealed class TrustScoreService
{
    private readonly int _minScore;
    private readonly int _maxScore;
    private readonly int _lowFanOut;
    private readonly int _midFanOut;
    private readonly int _highFanOut;
    private readonly int _highScoreThreshold;
    private readonly int _midScoreThreshold;
    private readonly int _defaultScore;

    private readonly Dictionary<string, int> _scores = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// Creates a trust scoring service with generic default tiers.
    /// </summary>
    public TrustScoreService()
        : this(new TrustScoringOptions())
    {
    }

    /// <summary>
    /// Creates a trust scoring service with custom tiers.
    /// </summary>
    public TrustScoreService(TrustScoringOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        _minScore = options.MinScore;
        _maxScore = options.MaxScore;
        _lowFanOut = options.LowFanOut;
        _midFanOut = options.MidFanOut;
        _highFanOut = options.HighFanOut;
        _midScoreThreshold = options.MidScoreThreshold;
        _highScoreThreshold = options.HighScoreThreshold;
        _defaultScore = options.DefaultScore;
    }

    /// <summary>
    /// Returns the trust score for the given node.
    /// </summary>
    public int GetScore(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id must be provided.", nameof(nodeId));
        }

        lock (_sync)
        {
            return _scores.TryGetValue(nodeId, out var score) ? score : _defaultScore;
        }
    }

    /// <summary>
    /// Sets the trust score for a node, clamped between 0 and 100.
    /// </summary>
    public void SetScore(string nodeId, int score)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id must be provided.", nameof(nodeId));
        }

        var clamped = Math.Min(_maxScore, Math.Max(_minScore, score));
        lock (_sync)
        {
            _scores[nodeId] = clamped;
        }
    }

    /// <summary>
    /// Gets the fan-out limit derived from the node trust score.
    /// </summary>
    public int GetFanOutLimit(string nodeId)
    {
        var score = GetScore(nodeId);
        if (score >= _highScoreThreshold)
        {
            return _highFanOut;
        }

        if (score >= _midScoreThreshold)
        {
            return _midFanOut;
        }

        return _lowFanOut;
    }

    private static void Validate(TrustScoringOptions options)
    {
        if (options.MinScore >= options.MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Min score must be lower than max score.");
        }

        if (options.MidScoreThreshold < options.MinScore || options.MidScoreThreshold > options.MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Mid score threshold must be inside the score range.");
        }

        if (options.HighScoreThreshold < options.MidScoreThreshold || options.HighScoreThreshold > options.MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "High score threshold must be >= mid threshold and inside the score range.");
        }

        if (options.DefaultScore < options.MinScore || options.DefaultScore > options.MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Default score must be inside the score range.");
        }

        if (options.LowFanOut <= 0 || options.MidFanOut <= 0 || options.HighFanOut <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "All fan-out values must be greater than zero.");
        }

        if (options.LowFanOut > options.MidFanOut || options.MidFanOut > options.HighFanOut)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Fan-out tiers must satisfy Low <= Mid <= High.");
        }
    }
}
