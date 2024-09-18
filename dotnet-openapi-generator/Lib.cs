using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.openapi.generator;

internal static class Lib
{
    public static async Task EnsureTextWritten(string path, string content, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            var existingContent = await File.ReadAllTextAsync(path, cancellationToken);
            if (existingContent == content)
                return;
        }
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }
}
