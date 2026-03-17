// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using ECP.Core.Strategy;

var recipients = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 50;
var message = args.Length > 1 ? args[1] : "Fire at Zone A";

var size = Encoding.UTF8.GetByteCount(message);
var selector = new NeverWorseSelector();
var strategy = selector.SelectStrategy(recipients, size, hasTemplate: false, hasDictionary: false);

Console.WriteLine($"Recipients: {recipients}");
Console.WriteLine($"Message size: {size} bytes");
Console.WriteLine($"Strategy: {strategy.Mode}");
Console.WriteLine($"Estimated bytes: {strategy.EstimatedTotalBytes}");
Console.WriteLine($"Reasoning: {strategy.Reasoning}");
