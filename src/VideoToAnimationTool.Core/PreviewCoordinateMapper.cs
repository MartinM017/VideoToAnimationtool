using System;
using System.Drawing;

namespace VideoToAnimationTool.Core
{
    public static class PreviewCoordinateMapper
    {
        public static bool TryMapCanvasToImage(
            double canvasX,
            double canvasY,
            double canvasWidth,
            double canvasHeight,
            int previewPixelWidth,
            int previewPixelHeight,
            int sourcePixelWidth,
            int sourcePixelHeight,
            out PointF imagePoint)
        {
            imagePoint = new PointF();
            if (canvasWidth <= 0 || canvasHeight <= 0 || previewPixelWidth <= 0 || previewPixelHeight <= 0 || sourcePixelWidth <= 0 || sourcePixelHeight <= 0) return false;

            var scale = Math.Min(canvasWidth / previewPixelWidth, canvasHeight / previewPixelHeight);
            var displayedWidth = previewPixelWidth * scale;
            var displayedHeight = previewPixelHeight * scale;
            var offsetX = (canvasWidth - displayedWidth) / 2.0;
            var offsetY = (canvasHeight - displayedHeight) / 2.0;
            var previewX = (canvasX - offsetX) / scale;
            var previewY = (canvasY - offsetY) / scale;
            if (previewX < 0 || previewY < 0 || previewX >= previewPixelWidth || previewY >= previewPixelHeight) return false;

            var sourceX = previewX * sourcePixelWidth / previewPixelWidth;
            var sourceY = previewY * sourcePixelHeight / previewPixelHeight;
            imagePoint = new PointF((float)sourceX, (float)sourceY);
            return true;
        }
    }
}
