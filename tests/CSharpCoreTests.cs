using System;
using System.IO;
using VideoToAnimationTool.Core;

public static class CSharpCoreTests
{
    private static int failures;

    public static int Main()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vta-csharp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            AssertEqual(PathUtils.ToSafeName("Hero Girl!*"), "Hero_Girl", "sanitizes names");
            AssertEqual(PathUtils.ToSafeName("   "), "untitled", "empty names use fallback");
            AssertEqual(PathUtils.NewFrameFileName("Hero", "Run", 7, "png"), "Hero_Run_0007.png", "builds frame file names");

            var videoPath = Path.Combine(tempRoot, "source.mp4");
            var outputPath = Path.Combine(tempRoot, "out");
            File.WriteAllText(videoPath, "");
            Directory.CreateDirectory(outputPath);

            var valid = ExportOptionsValidator.Validate(videoPath, outputPath, 0, 2.5, 12, "png");
            AssertTrue(valid.IsValid, "accepts valid export options");

            var invalidFps = ExportOptionsValidator.Validate(videoPath, outputPath, 0, 2.5, 0, "png");
            AssertFalse(invalidFps.IsValid, "rejects zero FPS");
            AssertTrue(Array.IndexOf(invalidFps.Errors, "FPS must be between 1 and 120.") >= 0, "returns FPS validation error");

            var frameFolder = Path.Combine(tempRoot, "frames");
            Directory.CreateDirectory(frameFolder);
            File.WriteAllText(Path.Combine(frameFolder, "walk_10.png"), "");
            File.WriteAllText(Path.Combine(frameFolder, "walk_1.png"), "");
            File.WriteAllText(Path.Combine(frameFolder, "walk_2.png"), "");
            File.WriteAllText(Path.Combine(frameFolder, "notes.txt"), "");

            var frames = PathUtils.GetFrameFiles(frameFolder);
            AssertEqual(Path.GetFileName(frames[0]), "walk_1.png", "natural sort first");
            AssertEqual(Path.GetFileName(frames[1]), "walk_2.png", "natural sort second");
            AssertEqual(Path.GetFileName(frames[2]), "walk_10.png", "natural sort tenth");

            var args = FfmpegHelper.BuildFrameExportArguments(
                @"C:\clips\hero run.mp4",
                @"C:\frames",
                "Hero",
                "Run",
                1.25,
                3.5,
                12,
                "png");

            AssertTrue(Array.IndexOf(args, "-ss") >= 0, "args include -ss");
            AssertTrue(Array.IndexOf(args, "-to") >= 0, "args include -to");
            AssertTrue(Array.IndexOf(args, "-i") >= 0, "args include -i");
            AssertTrue(Array.IndexOf(args, "-vf") >= 0, "args include -vf");
            AssertTrue(Array.IndexOf(args, "fps=12") >= 0, "args include fps filter");
            AssertEqual(args[args.Length - 1], @"C:\frames\Hero_Run_%04d.png", "args end with output pattern");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " C# core test(s) failed.");
            return 1;
        }

        Console.WriteLine("All C# core tests passed.");
        return 0;
    }

    private static void AssertEqual(string actual, string expected, string name)
    {
        if (!String.Equals(actual, expected, StringComparison.Ordinal))
        {
            failures++;
            Console.Error.WriteLine("FAIL: " + name);
            Console.Error.WriteLine("  Expected: " + expected);
            Console.Error.WriteLine("  Actual:   " + actual);
            return;
        }

        Console.WriteLine("PASS: " + name);
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
        {
            failures++;
            Console.Error.WriteLine("FAIL: " + name);
            return;
        }

        Console.WriteLine("PASS: " + name);
    }

    private static void AssertFalse(bool condition, string name)
    {
        AssertTrue(!condition, name);
    }
}
