using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State
{
    public static class UsagePrices
    {
        public const string Version = "2026-10-27";

        private const double PerToken = 1e-6;

        // Dollars per 1M tokens: input, output, cache write (5 minutes), cache read.
        // OpenAI caches on its own and has no cache-write price.
        private static readonly List<(string Prefix, UsagePrice Price)> s_prices = new()
        {
            ("claude-haiku-5-5", new UsagePrice(0.10, 0.50, 0.125, 0.01) { LongAbove = 100_000, Long = new UsagePrice(0.50, 2.50, 0.625, 0.05) }),
            ("claude-sonnet-5-5", new UsagePrice(2, 10, 2.50, 0.20)),
            ("claude-opus-5-5", new UsagePrice(4, 20, 5, 0.20)),
            ("claude-fable-5-1", new UsagePrice(10, 50, 12.50, 0.25)),
            ("gpt-6-luna", new UsagePrice(0.10, 0.50, 0, 0.01)),
            ("gpt-6.1-terra", new UsagePrice(1, 5, 0, 0.05)),
            ("gpt-6.1-sol", new UsagePrice(2, 10, 0, 0.20)),
            ("gpt-6-astra", new UsagePrice(10, 50, 0, 1)),
        };

        private static readonly UsagePrice s_fallback = s_prices.First(p => p.Prefix == "claude-sonnet-5-5").Price;

        public static UsagePrice For(string model, long promptTokens)
        {
            var key = model?.Trim() ?? string.Empty;
            var price = s_prices
                .Where(p => key.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Price)
                .FirstOrDefault() ?? s_fallback;

            return price.Long != null && promptTokens > price.LongAbove ? price.Long : price;
        }

        public static double Dollars(string model, long input, long cacheWrite, long cacheRead, long output)
        {
            var price = For(model, input + cacheWrite + cacheRead);
            return (input * price.Input + cacheWrite * price.CacheWrite + cacheRead * price.CacheRead + output * price.Output) * PerToken;
        }

        public static double Saved(string model, long input, long cacheWrite, long cacheRead)
        {
            var price = For(model, input + cacheWrite + cacheRead);
            return (cacheRead * (price.Input - price.CacheRead) - cacheWrite * Math.Max(0, price.CacheWrite - price.Input)) * PerToken;
        }
    }

    public class UsagePrice
    {
        public UsagePrice(double input, double output, double cacheWrite, double cacheRead)
        {
            Input = input;
            Output = output;
            CacheWrite = cacheWrite;
            CacheRead = cacheRead;
        }

        public double Input { get; }

        public double Output { get; }

        public double CacheWrite { get; }

        public double CacheRead { get; }

        public long LongAbove { get; init; }

        public UsagePrice Long { get; init; }
    }
}
