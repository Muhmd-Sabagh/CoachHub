namespace CoachHub.Application.Auth;

public static class AuthPermissions
{
    public const string ClaimType = "permission";
    public const string ManageUsers = "users.manage";
    public const string ManageClients = "clients.manage";
    public const string ManageAssessments = "assessments.manage";
    public const string ManageCatalog = "catalog.manage";
    public const string ManageMedia = "media.manage";
    public const string ManageSettings = "settings.manage";
    public const string ViewAudit = "audit.view";
    public const string ManageBilling = "billing.manage";
    public const string ManageCommunications = "communications.manage";
    public const string ManagePlans = "plans.manage";
    public const string ViewReports = "reports.view";
    public static readonly string[] All = [ManageUsers, ManageClients, ManageAssessments, ManageCatalog, ManageMedia, ManageSettings, ViewAudit, ManageBilling, ManageCommunications, ManagePlans, ViewReports];
}
