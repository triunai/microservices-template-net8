namespace Rgt.Space.Core.Constants;

public static class TaskAllocationConstants
{
    public static class Positions
    {
        public const string TechPic = "TECH_PIC";
        public const string TechBackup = "TECH_BACKUP";
        public const string FuncPic = "FUNC_PIC";
        public const string FuncBackup = "FUNC_BACKUP";
        public const string SupportPic = "SUPPORT_PIC";
        public const string SupportBackup = "SUPPORT_BACKUP";

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            TechPic, TechBackup,
            FuncPic, FuncBackup,
            SupportPic, SupportBackup
        };
    }
}
