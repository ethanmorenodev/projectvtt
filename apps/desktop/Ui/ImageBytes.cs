using Godot;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Decodifica los formatos raster que acepta el backend para mapas,
/// portadas y retratos. El SVG queda fuera: `Image` de Godot no lo
/// rasteriza (la web sí lo muestra); en ese caso la UI usa su alternativa
/// (inicial del nombre).
/// </summary>
public static class ImageBytes
{
    public static bool TryDecode(byte[] bytes, out Image image)
    {
        image = new Image();
        var error = bytes switch
        {
            [0x89, 0x50, 0x4E, 0x47, ..] => image.LoadPngFromBuffer(bytes),
            [0xFF, 0xD8, 0xFF, ..] => image.LoadJpgFromBuffer(bytes),
            [0x52, 0x49, 0x46, 0x46, ..] => image.LoadWebpFromBuffer(bytes),
            _ => Error.FileUnrecognized,
        };
        return error == Error.Ok;
    }
}
