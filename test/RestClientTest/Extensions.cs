using RestClient.Parser;
using System.Threading.Tasks;

namespace RestClientTest
{
    public static class Extensions
    {
        public static async Task WaitForParsingCompleteAsync(this Document document)
        {
            while (document.IsParsing)
            {
                await Task.Delay(2);
            }
        }
    }
}
