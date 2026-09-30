using System.Threading.Tasks;

namespace LoginAppFramework
{
    public interface INavigationRefreshable
    {
        Task RefreshForNavigationAsync();
    }
}
