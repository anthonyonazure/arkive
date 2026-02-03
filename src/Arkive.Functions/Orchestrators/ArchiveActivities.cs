using Arkive.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public class ArchiveActivities
{
    private readonly IArchiveService _archiveService;
    private readonly ILogger<ArchiveActivities> _logger;

    public ArchiveActivities(IArchiveService archiveService, ILogger<ArchiveActivities> logger)
    {
        _archiveService = archiveService;
        _logger = logger;
    }

    [Function(nameof(GetFilesToArchive))]
    public async Task<List<ArchiveFileInput>> GetFilesToArchive(
        [ActivityTrigger] GetFilesToArchiveInput input)
    {
        _logger.LogInformation("Getting files to archive for tenant {TenantId}", input.TenantId);
        return await _archiveService.GetFilesToArchiveAsync(
            input.TenantId, input.MspOrgId, input.RuleId);
    }

    [Function(nameof(ArchiveSingleFile))]
    public async Task<ArchiveFileResult> ArchiveSingleFile(
        [ActivityTrigger] ArchiveFileInput input)
    {
        _logger.LogInformation(
            "Archiving file {FileName} ({FileId}) for tenant {TenantId}",
            input.FileName, input.FileMetadataId, input.TenantId);

        try
        {
            var result = await _archiveService.ArchiveFileAsync(input);

            return new ArchiveFileResult
            {
                FileMetadataId = input.FileMetadataId,
                Success = result.Status == "Completed",
                ErrorMessage = result.ErrorMessage,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to archive file {FileName} ({FileId}): {Error}",
                input.FileName, input.FileMetadataId, ex.Message);

            return new ArchiveFileResult
            {
                FileMetadataId = input.FileMetadataId,
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }
}
