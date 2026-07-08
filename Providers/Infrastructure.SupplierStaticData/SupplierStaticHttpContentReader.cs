using System.IO.Compression;
using System.Text;

namespace Infrastructure.SupplierStaticData;

static class SupplierStaticHttpContentReader
{
    public static async Task<string> ReadAsStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
        {
            await using var input = new MemoryStream(bytes);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            await gzip.CopyToAsync(output, cancellationToken);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
