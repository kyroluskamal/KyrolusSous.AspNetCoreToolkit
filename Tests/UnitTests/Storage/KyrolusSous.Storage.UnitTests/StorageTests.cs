using System.Text;
using KyrolusSous.Compression;
using KyrolusSous.Storage.Abstractions;
using KyrolusSous.Storage.FileSystem;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace KyrolusSous.Storage.UnitTests;

public sealed class StorageTests : IDisposable
{
    private readonly string _testDir;
    private readonly KyrolusFileStorageProvider _provider;

    public StorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "KyrolusStorageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _provider = new KyrolusFileStorageProvider(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact(DisplayName = "File Storage Provider Upload And Download Work Correctly")]
    public async Task FileStorage_UploadAndDownload_WorksCorrectly()
    {
        var content = "Hello Kyrolus Storage World!";
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var props = await _provider.UploadAsync("documents", "hello.txt", uploadStream, new KyrolusBlobDescriptor { ContentType = "text/plain" });
        props.ShouldNotBeNull();
        props.BlobName.ShouldBe("hello.txt");
        props.ContentType.ShouldBe("text/plain");

        var exists = await _provider.ExistsAsync("documents", "hello.txt");
        exists.ShouldBeTrue();

        using var downloadStream = await _provider.DownloadAsync("documents", "hello.txt");
        using var reader = new StreamReader(downloadStream, Encoding.UTF8);
        var downloadedText = await reader.ReadToEndAsync();
        downloadedText.ShouldBe(content);
    }

    [Fact(DisplayName = "File Storage Provider List And Delete Work Correctly")]
    public async Task FileStorage_ListAndDelete_WorksCorrectly()
    {
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("File 1"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("File 2"));

        await _provider.UploadAsync("photos", "album1/pic1.jpg", stream1);
        await _provider.UploadAsync("photos", "album1/pic2.jpg", stream2);

        var list = await _provider.ListBlobsAsync("photos", prefix: "album1/");
        list.Count.ShouldBe(2);

        var deleted = await _provider.DeleteAsync("photos", "album1/pic1.jpg");
        deleted.ShouldBeTrue();

        var listAfter = await _provider.ListBlobsAsync("photos", prefix: "album1/");
        listAfter.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Compressed Storage Decorator Compresses And Decompresses Transparently")]
    public async Task CompressedStorage_CompressesAndDecompresses_Transparently()
    {
        var compressor = new GzipCompressor();
        var decorated = new KyrolusCompressedStorageDecorator(_provider, compressor);

        var content = new string('A', 5000); // 5 KB repeating text
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var props = await decorated.UploadAsync("compressed", "test.txt", uploadStream);
        props.ShouldNotBeNull();

        // Download raw stream from underlying provider to verify it is compressed
        using var rawStream = await _provider.DownloadAsync("compressed", "test.txt");
        rawStream.Length.ShouldBeLessThan(5000);

        // Download through decorator to verify transparent decompression
        using var decompressedStream = await decorated.DownloadAsync("compressed", "test.txt");
        using var reader = new StreamReader(decompressedStream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        text.ShouldBe(content);
    }

    [Fact(DisplayName = "Protected Storage Decorator Encrypts And Decrypts Transparently")]
    public async Task ProtectedStorage_EncryptsAndDecrypts_Transparently()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var dataProtection = sp.GetRequiredService<IDataProtectionProvider>();

        var decorated = new KyrolusProtectedStorageDecorator(_provider, dataProtection);

        var content = "Secret Financial Report";
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        await decorated.UploadAsync("secure", "report.pdf", uploadStream);

        // Raw underlying file is encrypted and does not match original text
        using var rawStream = await _provider.DownloadAsync("secure", "report.pdf");
        using var rawReader = new StreamReader(rawStream, Encoding.UTF8);
        var rawText = await rawReader.ReadToEndAsync();
        rawText.ShouldNotBe(content);

        // Download through decorator decrypts transparently
        using var decryptedStream = await decorated.DownloadAsync("secure", "report.pdf");
        using var reader = new StreamReader(decryptedStream, Encoding.UTF8);
        var decryptedText = await reader.ReadToEndAsync();
        decryptedText.ShouldBe(content);
    }

    [Fact(DisplayName = "File Storage Multipart Upload Sessions Assemble Chunks Correctly")]
    public async Task FileStorage_MultipartUpload_WorksCorrectly()
    {
        var multipartProvider = (IKyrolusMultipartStorageProvider)_provider;
        var session = await multipartProvider.InitiateMultipartUploadAsync("bigfiles", "video.mp4");
        session.ShouldNotBeNull();
        session.UploadId.ShouldNotBeNullOrWhiteSpace();

        var part1 = "Chunk1Data-";
        var part2 = "Chunk2Data-";
        var part3 = "Chunk3Data";

        using var s1 = new MemoryStream(Encoding.UTF8.GetBytes(part1));
        using var s2 = new MemoryStream(Encoding.UTF8.GetBytes(part2));
        using var s3 = new MemoryStream(Encoding.UTF8.GetBytes(part3));

        var info1 = await multipartProvider.UploadPartAsync(session, 1, s1);
        var info2 = await multipartProvider.UploadPartAsync(session, 2, s2);
        var info3 = await multipartProvider.UploadPartAsync(session, 3, s3);

        info1.PartNumber.ShouldBe(1);
        info2.PartNumber.ShouldBe(2);
        info3.PartNumber.ShouldBe(3);

        var completed = await multipartProvider.CompleteMultipartUploadAsync(session, [info1, info2, info3]);
        completed.ShouldNotBeNull();
        completed.BlobName.ShouldBe("video.mp4");
        completed.ContentLength.ShouldBe(part1.Length + part2.Length + part3.Length);

        using var downloadStream = await _provider.DownloadAsync("bigfiles", "video.mp4");
        using var reader = new StreamReader(downloadStream, Encoding.UTF8);
        var fullText = await reader.ReadToEndAsync();
        fullText.ShouldBe(part1 + part2 + part3);
    }
}
