using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using VideoToAnimationTool.Core;

public static class ChromaKeyTests
{
    private static int failures;

    public static int Main()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vta-chroma-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var keyed = GreenScreenRemover.RemoveGreenScreen(CreatePixel(Color.FromArgb(255, 0, 255, 0)), 60, 30);
            AssertEqual(GetAlpha(keyed), 0, "pure green pixel becomes transparent");

            var magenta = GreenScreenRemover.RemoveGreenScreen(CreatePixel(Color.FromArgb(255, 255, 0, 220)), 60, 30);
            AssertEqual(GetAlpha(magenta), 0, "pure magenta background pixel becomes transparent");

            using (var subjectSample = CreateSubjectOnBackgroundSample(Color.FromArgb(255, 0, 255, 0), Color.FromArgb(255, 220, 80, 40)))
            using (var subject = GreenScreenRemover.RemoveGreenScreen(subjectSample, 60, 30))
            {
                AssertTrue(subject.GetPixel(1, 1).A >= 250, "non-background subject pixel remains opaque");
            }

            using (var magentaSubjectSample = CreateSubjectOnBackgroundSample(Color.FromArgb(255, 255, 0, 220), Color.FromArgb(255, 235, 190, 160)))
            using (var magentaSubject = GreenScreenRemover.RemoveGreenScreen(magentaSubjectSample, 80, 60, 120, 70))
            {
                AssertTrue(magentaSubject.GetPixel(1, 1).R >= 225, "color despill preserves subject brightness");
            }

            var fringe = GreenScreenRemover.RemoveGreenScreen(CreatePixel(Color.FromArgb(128, 20, 210, 40)), 60, 30, 255);
            AssertTrue(GetGreen(fringe) < 210, "despill reduces green edge color");

            using (var edgeSource = CreateEdgeCleanupSample())
            using (var edgeResult = GreenScreenRemover.RemoveGreenScreen(edgeSource, 60, 30, 0, 255))
            {
                AssertTrue(edgeResult.GetPixel(1, 1).A < 220, "edge cleanup reduces alpha on green fringe next to transparency");
            }

            using (var watermarkSource = CreateWatermarkSample())
            using (var watermarkResult = WatermarkRemover.RemoveWatermark(watermarkSource, new[] {
                new PointF(1, 1),
                new PointF(3, 1),
                new PointF(3, 3),
                new PointF(1, 3)
            }))
            {
                AssertTrue(watermarkResult.GetPixel(2, 2).G > 200, "watermark removal fills lasso from surrounding pixels");
            }

            var sequenceFolder = Path.Combine(tempRoot, "green-sequence");
            var cutoutFolder = Path.Combine(tempRoot, "cutout");
            Directory.CreateDirectory(sequenceFolder);

            using (var frame1 = CreatePixel(Color.FromArgb(255, 0, 255, 0)))
            {
                frame1.Save(Path.Combine(sequenceFolder, "hero_0001.png"), ImageFormat.Png);
            }

            using (var frame2 = CreatePixel(Color.FromArgb(255, 220, 80, 40)))
            {
                frame2.Save(Path.Combine(sequenceFolder, "hero_0002.png"), ImageFormat.Png);
            }

            var result = GreenScreenRemover.ProcessFolder(sequenceFolder, cutoutFolder, 60, 30, null);
            AssertEqual(result.ProcessedCount, 2, "processes every frame in folder");
            AssertTrue(File.Exists(Path.Combine(cutoutFolder, "hero_0001.png")), "writes first cutout png");
            AssertTrue(File.Exists(Path.Combine(cutoutFolder, "hero_0002.png")), "writes second cutout png");

            using (var output = new Bitmap(Path.Combine(cutoutFolder, "hero_0001.png")))
            {
                AssertEqual(output.GetPixel(0, 0).A, 0, "folder output preserves transparency");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " chroma-key test(s) failed.");
            return 1;
        }

        Console.WriteLine("All chroma-key tests passed.");
        return 0;
    }

    private static void AssertEqual(int actual, int expected, string name)
    {
        if (actual != expected)
        {
            failures++;
            Console.Error.WriteLine("FAIL: " + name + " expected " + expected + " actual " + actual);
        }
        else Console.WriteLine("PASS: " + name);
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
        {
            failures++;
            Console.Error.WriteLine("FAIL: " + name);
        }
        else Console.WriteLine("PASS: " + name);
    }

    private static Bitmap CreatePixel(Color color)
    {
        var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, color);
        return bitmap;
    }

    private static Bitmap CreateEdgeCleanupSample()
    {
        var bitmap = new Bitmap(3, 3, PixelFormat.Format32bppArgb);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++) bitmap.SetPixel(x, y, Color.FromArgb(255, 0, 255, 0));
        }
        bitmap.SetPixel(1, 1, Color.FromArgb(255, 20, 190, 40));
        return bitmap;
    }

    private static Bitmap CreateSubjectOnBackgroundSample(Color background, Color subject)
    {
        var bitmap = new Bitmap(3, 3, PixelFormat.Format32bppArgb);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++) bitmap.SetPixel(x, y, background);
        }
        bitmap.SetPixel(1, 1, subject);
        return bitmap;
    }

    private static Bitmap CreateWatermarkSample()
    {
        var bitmap = new Bitmap(5, 5, PixelFormat.Format32bppArgb);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++) bitmap.SetPixel(x, y, Color.FromArgb(255, 0, 255, 0));
        }
        bitmap.SetPixel(2, 2, Color.FromArgb(255, 255, 255, 255));
        return bitmap;
    }

    private static int GetAlpha(Bitmap bitmap)
    {
        using (bitmap) return bitmap.GetPixel(0, 0).A;
    }

    private static int GetGreen(Bitmap bitmap)
    {
        using (bitmap) return bitmap.GetPixel(0, 0).G;
    }
}
