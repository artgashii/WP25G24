namespace EventManagementMvc.Services
{
    public interface IAuditLogger
    {
        Task LogAsync(string action, string? entityType = null, int? entityId = null, string? details = null);
    }
}
