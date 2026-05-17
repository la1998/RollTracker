using System;

namespace RollTracker.Services;

public sealed record RollEntry(string PlayerName, int Value, DateTimeOffset Time);
