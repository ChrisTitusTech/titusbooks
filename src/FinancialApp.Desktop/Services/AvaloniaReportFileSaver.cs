using System.Text;
using Avalonia.Platform.Storage;

namespace FinancialApp.Desktop.Services;

public sealed class AvaloniaReportFileSaver : IReportFileSaver
{
    private readonly Func<IStorageProvider?> storageProviderAccessor;

    public AvaloniaReportFileSaver(Func<IStorageProvider?> storageProviderAccessor)
    {
        this.storageProviderAccessor = storageProviderAccessor;
    }

    public async Task<bool> SaveAsync(
        string suggestedFileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        var storageProvider = storageProviderAccessor();
        if (storageProvider is null)
        {
            return false;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export report",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV file")
                {
                    Patterns = ["*.csv"],
                    MimeTypes = ["text/csv"]
                }
            ]
        });

        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
        return true;
    }
}
