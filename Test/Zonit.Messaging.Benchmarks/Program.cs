using BenchmarkDotNet.Running;
using Zonit.Messaging.Benchmarks;

// Run all benchmarks (or filter with: dotnet run -c Release -- --filter *EventPublish*).
BenchmarkSwitcher.FromAssembly(typeof(MessagingBenchmarks).Assembly).Run(args);
