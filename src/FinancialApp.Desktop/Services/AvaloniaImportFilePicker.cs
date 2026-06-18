using Avalonia.Platform.Storage;

namespace FinancialApp.Desktop.Services;

public sealed class AvaloniaImportFilePicker : IImportFilePicker
{
    private readonly Func<IStorageProvider?> storageProviderAccessor;

    public AvaloniaImportFilePicker(Func<IStorageProvider?> storageProviderAccessor)
    {
        this.storageProviderAccessor = storageProviderAccessor;
    }

    public async Task<ImportFileContent?> PickCsvAsync(CancellationToken cancellationToken = default)
    {
        var storageProvider = storageProviderAccessor();
        if (storageProvider is null)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose transaction CSV",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV file")
                {
                    Patterns = ["*.csv"],
                    MimeTypes = ["text/csv", "text/plain"]
                }
            ]
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return new ImportFileContent(file.Name, content);
    }
}
