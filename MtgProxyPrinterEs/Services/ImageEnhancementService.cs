using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace MtgProxyPrinterEs.Services
{
    /// <summary>
    /// Mejora automática sin modelos de IA:
    /// - Reescalado a una resolución adecuada para impresión (DPI configurable)
    /// - Recorte por relación de aspecto
    /// - Enfoque suave para recuperar bordes
    /// </summary>
    public class ImageEnhancementService
    {
        private readonly int _targetDpi;

        public ImageEnhancementService(int targetDpi = 300)
        {
            _targetDpi = targetDpi;
        }

        public byte[] EnhanceForCardPrint(byte[] inputBytes, double targetWidthCm, double targetHeightCm)
        {
            using var inputStream = new MemoryStream(inputBytes);
            using var image = Image.Load(inputStream);

            var targetW = CmToPixels(targetWidthCm);
            var targetH = CmToPixels(targetHeightCm);

            image.Mutate(ctx =>
            {
                // Upscale/downscale con alta calidad para evitar suavizado.
                ctx.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(targetW, targetH),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                    Sampler = KnownResamplers.Lanczos3
                });

                // Enfoque suave (evita halos agresivos).
                ctx.GaussianSharpen(0.8f);
            });

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private int CmToPixels(double cm) => (int)Math.Round((cm / 2.54) * _targetDpi);
    }
}

