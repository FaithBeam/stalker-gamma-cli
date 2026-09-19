var outputPath = Environment.GetEnvironmentVariable("FAKE_EXECUTABLE_OUTPUT")
    ?? throw new InvalidOperationException("FAKE_EXECUTABLE_OUTPUT is required.");

await File.WriteAllLinesAsync(outputPath, [Environment.ProcessPath ?? "", .. args]);
