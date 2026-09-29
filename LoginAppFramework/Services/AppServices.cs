namespace LoginAppFramework
{
    public static class AppServices
    {
        public static IAuthorizationService Authorization { get; } = new AuthorizationService();
        public static IAssetService Assets { get; } = new AssetService();
        public static IWorkerService Workers { get; } = new WorkerService();
        public static IAuditService Audit { get; } = new AuditService();
        public static IAssetExcelService AssetExcel { get; } = new AssetExcelService();
        public static IUserService Users { get; } = new UserService();
    }
}
