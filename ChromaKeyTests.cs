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

            PointF mapped;
            var mappedOk = PreviewCoordinateMapper.TryMapCanvasToImage(95, 175, 100, 200, 50, 100, 100, 200, out mapped);
            AssertTrue(mappedOk, "maps canvas point inside preview image");
            AssertEqual((int)Math.Round(mapped.X), 95, "maps preview x back to source x");
            AssertEqual((int)Math.Round(mapped.Y), 175, "maps preview y back to source y");

            using (var watermarkSource = CreateWatermarkSample())
            using (var watermarkResult = WatermarkRemover.RemoveWatermark(watermarkSource, new[] {
                new PointF(0, 0),
                new PointF(4, 0),
                new PointF(4, 4),
                new PointF(0, 4)
            }))
            {
                AssertEqual(watermarkResult.GetPixel(2, 2).A, 0, "watermark removal clears lasso to transparent alpha");
            }

            using (var smartSample = CreateSmartMatteSample())
            using (var smartResult = GreenScreenRemover.RemoveSmartMatteBackground(smartSample, 80, 60, 75, 70))
            {
                AssertTrue(smartResult.GetPixel(5, 5).A > 200, "smart matte keeps main subject component");
                AssertEqual(smartResult.GetPixel(0, 5).A, 0, "smart matte removes small border artifact");
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

            var sheetFrame1 = Path.Combine(tempRoot, "sheet-red.png");
            var sheetFrame2 = Path.Combine(tempRoot, "sheet-green.png");
            var removedFrame = Path.Combine(tempRoot, "sheet-blue.png");
            var transparentFrame = Path.Combine(tempRoot, "sheet-transparent.png");
            using (var red = CreatePixel(Color.FromArgb(255, 255, 0, 0))) red.Save(sheetFrame1, ImageFormat.Png);
            using (var green = CreatePixel(Color.FromArgb(255, 0, 255, 0))) green.Save(sheetFrame2, ImageFormat.Png);
            using (var blue = CreatePixel(Color.FromArgb(255, 0, 0, 255))) blue.Save(removedFrame, ImageFormat.Png);
            using (var transparent = CreatePixel(Color.FromArgb(0, 0, 0, 0))) transparent.Save(transparentFrame, ImageFormat.Png);
            var sheetPath = Path.Combine(tempRoot, "sheet.png");
            var sheetInfo = SpriteSheetExporter.Export(new[] { sheetFrame1, sheetFrame2 }, sheetPath, 2);
            AssertEqual(sheetInfo.FrameCount, 2, "sprite sheet exports active playback frame count");
            using (var sheet = new Bitmap(sheetPath))
            {
                AssertEqual(sheet.Width, 2, "sprite sheet width uses active frames only");
                AssertEqual(sheet.Height, 1, "sprite sheet height uses active frames only");
                AssertEqual(sheet.GetPixel(0, 0).R, 255, "sprite sheet first frame is red");
                AssertEqual(sheet.GetPixel(1, 0).G, 255, "sprite sheet second frame is green");
            }

            var transparentSheetPath = Path.Combine(tempRoot, "sheet-transparent-output.png");
            SpriteSheetExporter.Export(new[] { transparentFrame }, transparentSheetPath, 1);
            using (var sheet = new Bitmap(transparentSheetPath))
            {
                AssertEqual(sheet.GetPixel(0, 0).A, 0, "sprite sheet preserves transparent alpha");
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

    private static Bitmap CreateSmartMatteSample()
    {
        var bitmap = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++) bitmap.SetPixel(x, y, Color.FromArgb(255, 255, 0, 220));
        }
        for (var y = 3; y <= 6; y++)
        {
            for (var x = 3; x <= 6; x++) bitmap.SetPixel(x, y, Color.FromArgb(255, 30, 30, 30));
        }
        bitmap.SetPixel(0, 5, Color.FromArgb(255, 35, 35, 35));
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
