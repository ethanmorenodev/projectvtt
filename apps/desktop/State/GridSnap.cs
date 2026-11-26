using Godot;

namespace ProjectVTT.Desktop.State;

/// <summary>
/// Ajuste de coordenadas al centro de una celda del grid (cuadrada o
/// hexagonal flat-top odd-q, ADR-023). Réplica de la semántica web
/// (`apps/web/src/pixi/snap.ts` y `grid/hex.ts`): el cliente envía ya las
/// coordenadas ajustadas; el servidor no conoce esta preferencia.
/// `JRound` equivale a `Math.round` de JS (mitad hacia arriba) para
/// producir las mismas posiciones que la web.
/// </summary>
public static class GridSnap
{
    public const string Square = "square";
    public const string Hex = "hex";

    private static readonly double Sqrt3 = Math.Sqrt(3);

    public static Vector2 Snap(double x, double y, int cellSize, string shape)
    {
        var size = cellSize > 0 ? cellSize : 1;
        return shape == Hex ? SnapHex(x, y, size) : SnapSquare(x, y, size);
    }

    private static Vector2 SnapSquare(double x, double y, double size) => new(
        (float)(JRound(x / size) * size),
        (float)(JRound(y / size) * size));

    private static Vector2 SnapHex(double x, double y, double size)
    {
        var r = size / 2;

        // Fraccional axial (flat-top) y redondeo cúbico al hexágono más cercano.
        var qf = (2.0 / 3.0 * x) / r;
        var rf = ((-1.0 / 3.0) * x + (Sqrt3 / 3.0) * y) / r;
        var sf = -qf - rf;
        var q = JRound(qf);
        var axialR = JRound(rf);
        var s = JRound(sf);
        var qDiff = Math.Abs(q - qf);
        var rDiff = Math.Abs(axialR - rf);
        var sDiff = Math.Abs(s - sf);
        if (qDiff > rDiff && qDiff > sDiff)
        {
            q = -axialR - s;
        }
        else if (rDiff > sDiff)
        {
            axialR = -q - s;
        }

        // Axial → offset odd-q (columnas impares desplazadas hacia abajo).
        var col = (long)q;
        var row = (long)axialR + (col - (col & 1)) / 2;

        // Centro del hexágono (hexCellCenter de la web).
        return new(
            (float)(1.5 * r * col),
            (float)(Sqrt3 * r * (row + ((col & 1) == 1 ? 0.5 : 0))));
    }

    private static double JRound(double value) => Math.Floor(value + 0.5);
}
