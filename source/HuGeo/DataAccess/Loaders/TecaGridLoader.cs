using HuGeo.Core.Math;

namespace HuGeo.DataAccess.Loaders;

[Obsolete("Use the official grid loaders for production. Keep this only for legacy TECA regression checks.")]
public class TecaGridLoader
{
    private const int H = TecaBilinearGrid.Rows;
    private const int W = TecaBilinearGrid.Cols;

    public TecaBilinearGrid Load()
    {
        var assembly = HuGeo.Resources.EmbeddedResources.Assembly;
        using var stream = assembly.GetManifestResourceStream("HuGeo.Resources.Resources.grid_delta.dat")
            ?? throw new InvalidOperationException("grid_delta.dat embedded resource not found");

        // Bináris formátum (TECA kódból):
        //   3*H*W float (little-endian), soronként: [dx_sor][dy_sor][dz_sor]
        //   md[3*j*W + i]       = dx[j,i]
        //   md[(3*j+1)*W + i]   = dy[j,i]
        //   md[(3*j+2)*W + i]   = dz[j,i]

        int total = 3 * H * W;
        var buffer = new byte[total * sizeof(float)];
        stream.ReadExactly(buffer, 0, buffer.Length);

        var md = new float[total];
        Buffer.BlockCopy(buffer, 0, md, 0, buffer.Length);

        var dx = new double[H * W];
        var dy = new double[H * W];
        var dz = new double[H * W];

        for (int j = 0; j < H; j++)
        {
            for (int i = 0; i < W; i++)
            {
                dx[j * W + i] = md[3 * j * W + i];
                dy[j * W + i] = md[(3 * j + 1) * W + i];
                dz[j * W + i] = md[(3 * j + 2) * W + i];
            }
        }

        return new TecaBilinearGrid(dx, dy, dz);
    }
}
