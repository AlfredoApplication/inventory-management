namespace LoginAppFramework
{
    public class AssignableAssetViewModel
    {
        public Asset Asset { get; }
        public string Name => Asset.Name;
        public string Category => Asset.Category;
        public AssignableAssetViewModel(Asset asset) { Asset = asset; }
    }
}