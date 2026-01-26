namespace EventManagementMvc.Services;

public static class EventPermissionService
{
    public static bool CanViewEvent(bool isAdmin, string? userId, string? createdByUserId, bool isActive, bool hasViewPermission)
    {
        if (isActive) return true;
        if (isAdmin) return true;
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (!string.IsNullOrWhiteSpace(createdByUserId) && createdByUserId == userId) return true;
        return hasViewPermission;
    }

    public static bool CanEditEvent(bool isAdmin, string? userId, string? createdByUserId, bool hasEditPermission)
    {
        if (isAdmin) return true;
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (!string.IsNullOrWhiteSpace(createdByUserId) && createdByUserId == userId) return true;
        return hasEditPermission;
    }
}
