using QRCoder;

namespace VenueGo.Helpers
{
    public static class QrCodeHelper
    {
        public static byte[] GeneratePng(string content, int pixelsPerModule = 20)
        {
            using var generator = new QRCodeGenerator();
            var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            return new PngByteQRCode(qrData).GetGraphic(pixelsPerModule);
        }
    }
}
