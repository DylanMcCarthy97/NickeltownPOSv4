namespace NickeltownPOSV4.Services.Stock;

public interface IStockProductImageStorage
{
    string ProductImagesDirectory { get; }

    bool IsManagedPath(string? path);

    ProductImageImportResult ImportFromSource(string sourcePath, long? itemId);

    string? EnsureItemIdFileName(string? storedPath, long itemId);

    string? ResolveImportImagePath(string? imagePath, long itemId);
}
